﻿using Mp4SubtitleParser;
using N_m3u8DL_RE.Column;
using N_m3u8DL_RE.Common.Entity;
using N_m3u8DL_RE.Common.Enum;
using N_m3u8DL_RE.Common.Log;
using N_m3u8DL_RE.Common.Resource;
using N_m3u8DL_RE.Config;
using N_m3u8DL_RE.Downloader;
using N_m3u8DL_RE.Entity;
using N_m3u8DL_RE.Util;
using Spectre.Console;
using Spectre.Console.Rendering;
using System.Collections.Concurrent;
using System.Text;

namespace N_m3u8DL_RE.DownloadManager
{
    internal class SimpleDownloadManager
    {
        IDownloader Downloader;
        DownloaderConfig DownloaderConfig;
        DateTime NowDateTime;
        List<OutputFile> OutputFiles = new();

        public SimpleDownloadManager(DownloaderConfig downloaderConfig) 
        { 
            this.DownloaderConfig = downloaderConfig;
            Downloader = new SimpleDownloader(DownloaderConfig);
            NowDateTime = DateTime.Now;
        }

        private string? ReadInit(byte[] data)
        {
            var info = MP4InitUtil.ReadInit(data);
            if (info.Scheme != null) Logger.WarnMarkUp($"[grey]Type: {info.Scheme}[/]");
            if (info.PSSH != null) Logger.WarnMarkUp($"[grey]PSSH(WV): {info.PSSH}[/]");
            if (info.KID != null) Logger.WarnMarkUp($"[grey]KID: {info.KID}[/]");
            return info.KID;
        }

        private string? ReadInit(string output)
        {
            using (var fs = File.OpenRead(output))
            {
                var header = new byte[4096]; //4KB
                fs.Read(header);
                return ReadInit(header);
            }
        }

        //若该文件夹为空，删除，同时判断其父文件夹，直到遇到根目录或不为空的目录
        private void SafeDeleteDir(string dirPath)
        {
            if (string.IsNullOrEmpty(dirPath) || !Directory.Exists(dirPath))
                return;

            var parent = Path.GetDirectoryName(dirPath)!;
            if (!Directory.EnumerateFileSystemEntries(dirPath).Any())
            {
                Directory.Delete(dirPath);
            }
            else
            {
                return;
            }
            SafeDeleteDir(parent);
        }

        //从文件读取KEY
        private async Task SearchKeyAsync(string? currentKID)
        {
            var _key = await MP4DecryptUtil.SearchKeyFromFile(DownloaderConfig.MyOptions.KeyTextFile, currentKID);
            if (_key != null)
            {
                if (DownloaderConfig.MyOptions.Keys == null)
                    DownloaderConfig.MyOptions.Keys = new string[] { _key };
                else
                    DownloaderConfig.MyOptions.Keys = DownloaderConfig.MyOptions.Keys.Concat(new string[] { _key }).ToArray();
            }
        }

        private void ChangeSpecInfo(StreamSpec streamSpec, List<Mediainfo> mediainfos, ref bool useAACFilter)
        {
            if (!DownloaderConfig.MyOptions.BinaryMerge && mediainfos.Any(m => m.DolbyVison == true))
            {
                DownloaderConfig.MyOptions.BinaryMerge = true;
                Logger.WarnMarkUp($"[darkorange3_1]{ResString.autoBinaryMerge2}[/]");
            }

            if (DownloaderConfig.MyOptions.MuxAfterDone && mediainfos.Any(m => m.DolbyVison == true))
            {
                DownloaderConfig.MyOptions.MuxAfterDone = false;
                Logger.WarnMarkUp($"[darkorange3_1]{ResString.autoBinaryMerge5}[/]");
            }

            if (mediainfos.Where(m => m.Type == "Audio").All(m => m.BaseInfo!.Contains("aac")))
            {
                useAACFilter = true;
            }

            if (mediainfos.All(m => m.Type == "Audio"))
            {
                streamSpec.MediaType = MediaType.AUDIO;
            }
            else if (mediainfos.All(m => m.Type == "Subtitle"))
            {
                streamSpec.MediaType = MediaType.SUBTITLES;
                if (streamSpec.Extension == null || streamSpec.Extension == "ts")
                    streamSpec.Extension = "vtt";
            }
        }


        private async Task<bool> DownloadStreamAsync(StreamSpec streamSpec, ProgressTask task, SpeedContainer speedContainer)
        {
            speedContainer.ResetVars();
            bool useAACFilter = false; //ffmpeg合并flag
            List<Mediainfo> mediaInfos = new();
            ConcurrentDictionary<MediaSegment, DownloadResult?> FileDic = new();

            var segments = streamSpec.Playlist?.MediaParts.SelectMany(m => m.MediaSegments);
            if (segments == null) return false;
            if (segments.Count() == 1) speedContainer.SingleSegment = true;

            var type = streamSpec.MediaType ?? Common.Enum.MediaType.VIDEO;
            var dirName = $"{DownloaderConfig.MyOptions.SaveName ?? NowDateTime.ToString("yyyy-MM-dd_HH-mm-ss")}_{task.Id}_{streamSpec.GroupId}_{streamSpec.Codecs}_{streamSpec.Bandwidth}_{streamSpec.Language}";
            var tmpDir = Path.Combine(DownloaderConfig.MyOptions.TmpDir ?? Environment.CurrentDirectory, dirName);
            var saveDir = DownloaderConfig.MyOptions.SaveDir ?? Environment.CurrentDirectory;
            var saveName = DownloaderConfig.MyOptions.SaveName != null ? $"{DownloaderConfig.MyOptions.SaveName}.{streamSpec.Language}".TrimEnd('.') : dirName;
            var headers = DownloaderConfig.Headers;

            //mp4decrypt
            var mp4decrypt = DownloaderConfig.MyOptions.DecryptionBinaryPath!;
            var mp4InitFile = "";
            var currentKID = "";
            var readInfo = false; //是否读取过

            Logger.Debug($"dirName: {dirName}; tmpDir: {tmpDir}; saveDir: {saveDir}; saveName: {saveName}");

            //创建文件夹
            if (!Directory.Exists(tmpDir)) Directory.CreateDirectory(tmpDir);
            if (!Directory.Exists(saveDir)) Directory.CreateDirectory(saveDir);

            var totalCount = segments.Count();
            if (streamSpec.Playlist?.MediaInit != null)
            {
                totalCount++;
            }

            task.MaxValue = totalCount;
            task.StartTask();

            //开始下载
            Logger.InfoMarkUp(ResString.startDownloading + streamSpec.ToShortString());

            //对于CENC，全部自动开启二进制合并
            if (!DownloaderConfig.MyOptions.BinaryMerge && totalCount >= 1 && streamSpec.Playlist!.MediaParts.First().MediaSegments.First().EncryptInfo.Method == Common.Enum.EncryptMethod.CENC)
            {
                DownloaderConfig.MyOptions.BinaryMerge = true;
                Logger.WarnMarkUp($"[darkorange3_1]{ResString.autoBinaryMerge4}[/]");
            }

            //下载init
            if (streamSpec.Playlist?.MediaInit != null)
            {
                //对于fMP4，自动开启二进制合并
                if (!DownloaderConfig.MyOptions.BinaryMerge && streamSpec.MediaType != MediaType.SUBTITLES)
                {
                    DownloaderConfig.MyOptions.BinaryMerge = true;
                    Logger.WarnMarkUp($"[darkorange3_1]{ResString.autoBinaryMerge}[/]");
                }

                var path = Path.Combine(tmpDir, "_init.mp4.tmp");
                var result = await Downloader.DownloadSegmentAsync(streamSpec.Playlist.MediaInit, path, speedContainer, headers);
                FileDic[streamSpec.Playlist.MediaInit] = result;
                if (result == null || !result.Success)
                {
                    throw new Exception("Download init file failed!");
                }
                mp4InitFile = result.ActualFilePath;
                task.Increment(1);

                //读取mp4信息
                if (result != null && result.Success) 
                {
                    var data = File.ReadAllBytes(result.ActualFilePath);
                    currentKID = ReadInit(data);
                    //从文件读取KEY
                    await SearchKeyAsync(currentKID);
                    //实时解密
                    if (DownloaderConfig.MyOptions.MP4RealTimeDecryption && !string.IsNullOrEmpty(currentKID))
                    {
                        var enc = result.ActualFilePath;
                        var dec = Path.Combine(Path.GetDirectoryName(enc)!, Path.GetFileNameWithoutExtension(enc) + "_dec" + Path.GetExtension(enc));
                        var dResult = await MP4DecryptUtil.DecryptAsync(DownloaderConfig.MyOptions.UseShakaPackager, mp4decrypt, DownloaderConfig.MyOptions.Keys, enc, dec, currentKID);
                        if (dResult)
                        {
                            FileDic[streamSpec.Playlist.MediaInit]!.ActualFilePath = dec;
                        }
                    }
                    //ffmpeg读取信息
                    if (!readInfo)
                    {
                        Logger.WarnMarkUp(ResString.readingInfo);
                        mediaInfos = await MediainfoUtil.ReadInfoAsync(DownloaderConfig.MyOptions.FFmpegBinaryPath!, result.ActualFilePath);
                        mediaInfos.ForEach(info => Logger.InfoMarkUp(info.ToStringMarkUp()));
                        ChangeSpecInfo(streamSpec, mediaInfos, ref useAACFilter);
                        readInfo = true;
                    }
                }
            }

            //计算填零个数
            var pad = "0".PadLeft(segments.Count().ToString().Length, '0');

            //下载第一个分片
            if (!readInfo)
            {
                var seg = segments.First();
                segments = segments.Skip(1);

                var index = seg.Index;
                var path = Path.Combine(tmpDir, index.ToString(pad) + $".{streamSpec.Extension ?? "clip"}.tmp");
                var result = await Downloader.DownloadSegmentAsync(seg, path, speedContainer, headers);
                FileDic[seg] = result;
                if (result == null || !result.Success)
                {
                    throw new Exception("Download first segment failed!");
                }
                task.Increment(1);
                if (result != null && result.Success)
                {
                    //读取init信息
                    if (string.IsNullOrEmpty(currentKID))
                    {
                        currentKID = ReadInit(result.ActualFilePath);
                    }
                    //从文件读取KEY
                    await SearchKeyAsync(currentKID);
                    //实时解密
                    if (DownloaderConfig.MyOptions.MP4RealTimeDecryption && !string.IsNullOrEmpty(currentKID))
                    {
                        var enc = result.ActualFilePath;
                        var dec = Path.Combine(Path.GetDirectoryName(enc)!, Path.GetFileNameWithoutExtension(enc) + "_dec" + Path.GetExtension(enc));
                        var dResult = await MP4DecryptUtil.DecryptAsync(DownloaderConfig.MyOptions.UseShakaPackager, mp4decrypt, DownloaderConfig.MyOptions.Keys, enc, dec, currentKID, mp4InitFile);
                        if (dResult)
                        {
                            File.Delete(enc);
                            result.ActualFilePath = dec;
                        }
                    }
                    //ffmpeg读取信息
                    Logger.WarnMarkUp(ResString.readingInfo);
                    mediaInfos = await MediainfoUtil.ReadInfoAsync(DownloaderConfig.MyOptions.FFmpegBinaryPath!, result!.ActualFilePath);
                    mediaInfos.ForEach(info => Logger.InfoMarkUp(info.ToStringMarkUp()));
                    ChangeSpecInfo(streamSpec, mediaInfos, ref useAACFilter);
                    readInfo = true;
                }
            }

            //开始下载
            var options = new ParallelOptions()
            {
                MaxDegreeOfParallelism = DownloaderConfig.MyOptions.ThreadCount
            };
            await Parallel.ForEachAsync(segments, options, async (seg, _) =>
            {
                var index = seg.Index;
                var path = Path.Combine(tmpDir, index.ToString(pad) + $".{streamSpec.Extension ?? "clip"}.tmp");
                var result = await Downloader.DownloadSegmentAsync(seg, path, speedContainer, headers);
                FileDic[seg] = result;
                task.Increment(1);
                //实时解密
                if (DownloaderConfig.MyOptions.MP4RealTimeDecryption && result != null && result.Success && !string.IsNullOrEmpty(currentKID)) 
                {
                    var enc = result.ActualFilePath;
                    var dec = Path.Combine(Path.GetDirectoryName(enc)!, Path.GetFileNameWithoutExtension(enc) + "_dec" + Path.GetExtension(enc));
                    var dResult = await MP4DecryptUtil.DecryptAsync(DownloaderConfig.MyOptions.UseShakaPackager, mp4decrypt, DownloaderConfig.MyOptions.Keys, enc, dec, currentKID, mp4InitFile);
                    if (dResult)
                    {
                        File.Delete(enc);
                        result.ActualFilePath = dec;
                    }
                }
            });

            //修改输出后缀
            var outputExt = "." + streamSpec.Extension;
            if (streamSpec.Extension == null) outputExt = ".ts";
            else if (streamSpec.MediaType == MediaType.AUDIO && streamSpec.Extension == "m4s") outputExt = ".m4a";
            else if (streamSpec.MediaType != MediaType.SUBTITLES && streamSpec.Extension == "m4s") outputExt = ".mp4";

            var output = Path.Combine(saveDir, saveName + outputExt);

            //检测目标文件是否存在
            while (File.Exists(output))
            {
                Logger.WarnMarkUp($"{Path.GetFileName(output)} => {Path.GetFileName(output = Path.ChangeExtension(output, $"copy" + Path.GetExtension(output)))}");
            }

            if (!string.IsNullOrEmpty(currentKID) && DownloaderConfig.MyOptions.MP4RealTimeDecryption && DownloaderConfig.MyOptions.Keys != null && DownloaderConfig.MyOptions.Keys.Length > 0 && mp4InitFile != "")
            {
                File.Delete(mp4InitFile);
                //shaka实时解密不需要init文件用于合并
                if (DownloaderConfig.MyOptions.UseShakaPackager)
                {
                    FileDic!.Remove(streamSpec.Playlist!.MediaInit, out _);
                }
            }

            //校验分片数量
            if (DownloaderConfig.MyOptions.CheckSegmentsCount && FileDic.Values.Any(s => s == null))
            {
                Logger.WarnMarkUp(ResString.segmentCountCheckNotPass, totalCount, FileDic.Values.Where(s => s != null).Count());
                return false;
            }

            //移除无效片段
            var badKeys = FileDic.Where(i => i.Value == null).Select(i => i.Key);
            foreach (var badKey in badKeys)
            {
                FileDic!.Remove(badKey, out _);
            }

            //校验完整性
            if (DownloaderConfig.CheckContentLength && FileDic.Values.Any(a => a!.Success == false)) 
            {
                return false;
            }

            //自动修复VTT raw字幕
            if (DownloaderConfig.MyOptions.AutoSubtitleFix && streamSpec.MediaType == Common.Enum.MediaType.SUBTITLES 
                && streamSpec.Extension != null && streamSpec.Extension.Contains("vtt")) 
            {
                Logger.WarnMarkUp(ResString.fixingVTT);
                //排序字幕并修正时间戳
                bool first = true;
                var finalVtt = new WebVttSub();
                var keys = FileDic.Keys.OrderBy(k => k.Index);
                foreach (var seg in keys)
                {
                    var vttContent = File.ReadAllText(FileDic[seg]!.ActualFilePath);
                    var vtt = WebVttSub.Parse(vttContent);
                    //手动计算MPEGTS
                    if (finalVtt.MpegtsTimestamp == 0 && vtt.MpegtsTimestamp == 0)
                    {
                        vtt.MpegtsTimestamp = 90 * (long)(seg.Duration * 1000) * seg.Index;
                    }
                    if (first)
                    {
                        finalVtt = vtt;
                        first = false;
                    }
                    else
                    {
                        finalVtt.AddCuesFromOne(vtt);
                    }
                }
                //写出字幕
                var files = FileDic.Values.Select(v => v!.ActualFilePath).OrderBy(s => s).ToArray();
                foreach (var item in files) File.Delete(item);
                FileDic.Clear();
                var index = 0;
                var path = Path.Combine(tmpDir, index.ToString(pad) + ".fix.vtt");
                var subContentFixed = finalVtt.ToStringWithHeader();
                //转换字幕格式
                if (DownloaderConfig.MyOptions.SubtitleFormat != Enum.SubtitleFormat.VTT)
                {
                    path = Path.ChangeExtension(path, ".srt");
                    subContentFixed = OtherUtil.WebVtt2Other(finalVtt, DownloaderConfig.MyOptions.SubtitleFormat);
                    output = Path.ChangeExtension(output, ".srt");
                }
                await File.WriteAllTextAsync(path, subContentFixed, Encoding.UTF8);
                FileDic[keys.First()] = new DownloadResult()
                {
                    ActualContentLength = subContentFixed.Length,
                    ActualFilePath = path
                };
            }

            //自动修复VTT mp4字幕
            if (DownloaderConfig.MyOptions.AutoSubtitleFix && streamSpec.MediaType == Common.Enum.MediaType.SUBTITLES
                && streamSpec.Codecs != "stpp" && streamSpec.Extension != null && streamSpec.Extension.Contains("m4s"))
            {
                var initFile = FileDic.Values.Where(v => Path.GetFileName(v!.ActualFilePath).StartsWith("_init")).FirstOrDefault();
                var iniFileBytes = File.ReadAllBytes(initFile!.ActualFilePath);
                var (sawVtt, timescale) = MP4VttUtil.CheckInit(iniFileBytes);
                if (sawVtt)
                {
                    Logger.WarnMarkUp(ResString.fixingVTTmp4);
                    var mp4s = FileDic.Values.Select(v => v!.ActualFilePath).Where(p => p.EndsWith(".m4s")).OrderBy(s => s).ToArray();
                    var finalVtt = MP4VttUtil.ExtractSub(mp4s, timescale);
                    //写出字幕
                    var firstKey = FileDic.Keys.First();
                    var files = FileDic.Values.Select(v => v!.ActualFilePath).OrderBy(s => s).ToArray();
                    foreach (var item in files) File.Delete(item);
                    FileDic.Clear();
                    var index = 0;
                    var path = Path.Combine(tmpDir, index.ToString(pad) + ".fix.vtt");
                    var subContentFixed = finalVtt.ToStringWithHeader();
                    //转换字幕格式
                    if (DownloaderConfig.MyOptions.SubtitleFormat != Enum.SubtitleFormat.VTT)
                    {
                        path = Path.ChangeExtension(path, ".srt");
                        subContentFixed = OtherUtil.WebVtt2Other(finalVtt, DownloaderConfig.MyOptions.SubtitleFormat);
                        output = Path.ChangeExtension(output, ".srt");
                    }
                    await File.WriteAllTextAsync(path, subContentFixed, Encoding.UTF8);
                    FileDic[firstKey] = new DownloadResult()
                    {
                        ActualContentLength = subContentFixed.Length,
                        ActualFilePath = path
                    };
                    //修改输出后缀
                    output = Path.ChangeExtension(output, Path.GetExtension(path));
                }
            }

            //自动修复TTML raw字幕
            if (DownloaderConfig.MyOptions.AutoSubtitleFix && streamSpec.MediaType == Common.Enum.MediaType.SUBTITLES
                && streamSpec.Extension != null && streamSpec.Extension.Contains("ttml"))
            {
                Logger.WarnMarkUp(ResString.fixingTTML);
                var mp4s = FileDic.Values.Select(v => v!.ActualFilePath).Where(p => p.EndsWith(".ttml")).OrderBy(s => s).ToArray();
                var finalVtt = MP4TtmlUtil.ExtractFromTTMLs(mp4s, 0);
                //写出字幕
                var firstKey = FileDic.Keys.First();
                var files = FileDic.Values.Select(v => v!.ActualFilePath).OrderBy(s => s).ToArray();
                foreach (var item in files) File.Delete(item);
                FileDic.Clear();
                var index = 0;
                var path = Path.Combine(tmpDir, index.ToString(pad) + ".fix.vtt");
                var subContentFixed = finalVtt.ToStringWithHeader();
                //转换字幕格式
                if (DownloaderConfig.MyOptions.SubtitleFormat != Enum.SubtitleFormat.VTT)
                {
                    path = Path.ChangeExtension(path, ".srt");
                    subContentFixed = OtherUtil.WebVtt2Other(finalVtt, DownloaderConfig.MyOptions.SubtitleFormat);
                    output = Path.ChangeExtension(output, ".srt");
                }
                await File.WriteAllTextAsync(path, subContentFixed, Encoding.UTF8);
                FileDic[firstKey] = new DownloadResult()
                {
                    ActualContentLength = subContentFixed.Length,
                    ActualFilePath = path
                };
                //修改输出后缀
                output = Path.ChangeExtension(output, Path.GetExtension(path));
            }

            //自动修复TTML mp4字幕
            if (DownloaderConfig.MyOptions.AutoSubtitleFix && streamSpec.MediaType == Common.Enum.MediaType.SUBTITLES
                && streamSpec.Extension != null && streamSpec.Extension.Contains("m4s")
                && streamSpec.Codecs != null && streamSpec.Codecs.Contains("stpp")) 
            {
                Logger.WarnMarkUp(ResString.fixingTTMLmp4);
                //sawTtml暂时不判断
                //var initFile = FileDic.Values.Where(v => Path.GetFileName(v!.ActualFilePath).StartsWith("_init")).FirstOrDefault();
                //var iniFileBytes = File.ReadAllBytes(initFile!.ActualFilePath);
                //var sawTtml = MP4TtmlUtil.CheckInit(iniFileBytes);
                var mp4s = FileDic.Values.Select(v => v!.ActualFilePath).Where(p => p.EndsWith(".m4s")).OrderBy(s => s).ToArray();
                var finalVtt = MP4TtmlUtil.ExtractFromMp4s(mp4s, 0);
                //写出字幕
                var firstKey = FileDic.Keys.First();
                var files = FileDic.Values.Select(v => v!.ActualFilePath).OrderBy(s => s).ToArray();
                foreach (var item in files) File.Delete(item);
                FileDic.Clear();
                var index = 0;
                var path = Path.Combine(tmpDir, index.ToString(pad) + ".fix.vtt");
                var subContentFixed = finalVtt.ToStringWithHeader();
                //转换字幕格式
                if (DownloaderConfig.MyOptions.SubtitleFormat != Enum.SubtitleFormat.VTT)
                {
                    path = Path.ChangeExtension(path, ".srt");
                    subContentFixed = OtherUtil.WebVtt2Other(finalVtt, DownloaderConfig.MyOptions.SubtitleFormat);
                    output = Path.ChangeExtension(output, ".srt");
                }
                await File.WriteAllTextAsync(path, subContentFixed, Encoding.UTF8);
                FileDic[firstKey] = new DownloadResult()
                {
                    ActualContentLength = subContentFixed.Length,
                    ActualFilePath = path
                };
                //修改输出后缀
                output = Path.ChangeExtension(output, Path.GetExtension(path));
            }

            bool mergeSuccess = false;
            //合并
            if (!DownloaderConfig.MyOptions.SkipMerge)
            {
                //字幕也使用二进制合并
                if (DownloaderConfig.MyOptions.BinaryMerge || streamSpec.MediaType == MediaType.SUBTITLES)
                {
                    Logger.InfoMarkUp(ResString.binaryMerge);
                    var files = FileDic.Values.Select(v => v!.ActualFilePath).OrderBy(s => s).ToArray();
                    MergeUtil.CombineMultipleFilesIntoSingleFile(files, output);
                    mergeSuccess = true;
                }
                else
                {
                    //ffmpeg合并
                    var files = FileDic.Values.Select(v => v!.ActualFilePath).OrderBy(s => s).ToArray();
                    Logger.InfoMarkUp(ResString.ffmpegMerge);
                    var ext = streamSpec.MediaType == MediaType.AUDIO ? "m4a" : "mp4";
                    var ffOut = Path.Combine(Path.GetDirectoryName(output)!, Path.GetFileNameWithoutExtension(output) + $".{ext}");
                    //检测目标文件是否存在
                    while (File.Exists(ffOut))
                    {
                        Logger.WarnMarkUp($"{Path.GetFileName(ffOut)} => {Path.GetFileName(ffOut = Path.ChangeExtension(ffOut, $"copy" + Path.GetExtension(ffOut)))}");
                    }
                    mergeSuccess = MergeUtil.MergeByFFmpeg(DownloaderConfig.MyOptions.FFmpegBinaryPath!, files, Path.ChangeExtension(ffOut, null), ext, useAACFilter);
                    if (mergeSuccess) output = ffOut;
                }
            }

            //删除临时文件夹
            if (!DownloaderConfig.MyOptions.SkipMerge && DownloaderConfig.MyOptions.DelAfterDone && mergeSuccess)
            {
                var files = FileDic.Values.Select(v => v!.ActualFilePath);
                foreach (var file in files)
                {
                    File.Delete(file);
                }
                SafeDeleteDir(tmpDir);
            }

            //重新读取init信息
            if (mergeSuccess && totalCount >= 1 && string.IsNullOrEmpty(currentKID) && streamSpec.Playlist!.MediaParts.First().MediaSegments.First().EncryptInfo.Method != Common.Enum.EncryptMethod.NONE)
            {
                currentKID = ReadInit(output);
                //从文件读取KEY
                await SearchKeyAsync(currentKID);
            }

            //调用mp4decrypt解密
            if (mergeSuccess && File.Exists(output) && !string.IsNullOrEmpty(currentKID) && !DownloaderConfig.MyOptions.MP4RealTimeDecryption && DownloaderConfig.MyOptions.Keys != null && DownloaderConfig.MyOptions.Keys.Length > 0)
            {
                var enc = output;
                var dec = Path.Combine(Path.GetDirectoryName(enc)!, Path.GetFileNameWithoutExtension(enc) + "_dec" + Path.GetExtension(enc));
                Logger.InfoMarkUp($"[grey]Decrypting...[/]");
                var result = await MP4DecryptUtil.DecryptAsync(DownloaderConfig.MyOptions.UseShakaPackager, mp4decrypt, DownloaderConfig.MyOptions.Keys, enc, dec, currentKID);
                if (result)
                {
                    File.Delete(enc);
                    File.Move(dec, enc);
                }
            }

            //记录所有文件信息
            if (File.Exists(output))
                OutputFiles.Add(new OutputFile()
                {
                    Index = task.Id,
                    FilePath = output,
                    LangCode = streamSpec.Language,
                    Description = streamSpec.Name,
                    Mediainfos = mediaInfos
                });

            return true;
        }

        public async Task<bool> StartDownloadAsync(IEnumerable<StreamSpec> streamSpecs)
        {
            ConcurrentDictionary<int, SpeedContainer> SpeedContainerDic = new(); //速度计算
            ConcurrentDictionary<StreamSpec, bool?> Results = new();

            var progress = AnsiConsole.Progress().AutoClear(true);

            //进度条的列定义
            progress.Columns(new ProgressColumn[]
            {
                new TaskDescriptionColumn() { Alignment = Justify.Left },
                new ProgressBarColumn(),
                new PercentageColumn(),
                new DownloadSpeedColumn(SpeedContainerDic), //速度计算
                new RemainingTimeColumn(),
                new SpinnerColumn(),
            });

            await progress.StartAsync(async ctx =>
            {
                //创建任务
                var dic = streamSpecs.Select(item =>
                {
                    var task = ctx.AddTask(item.ToShortString(), autoStart: false);
                    SpeedContainerDic[task.Id] = new SpeedContainer(); //速度计算
                    return (item, task);
                }).ToDictionary(item => item.item, item => item.task);

                if (!DownloaderConfig.MyOptions.ConcurrentDownload)
                {
                    //遍历，顺序下载
                    foreach (var kp in dic)
                    {
                        var task = kp.Value;
                        var result = await DownloadStreamAsync(kp.Key, task, SpeedContainerDic[task.Id]);
                        Results[kp.Key] = result;
                    }
                }
                else
                {
                    //并发下载
                    await Parallel.ForEachAsync(dic, async (kp, _) =>
                    {
                        var task = kp.Value;
                        var result = await DownloadStreamAsync(kp.Key, task, SpeedContainerDic[task.Id]);
                        Results[kp.Key] = result;
                    });
                }
            });

            var success = Results.Values.All(v => v == true);

            //混流
            if (success && DownloaderConfig.MyOptions.MuxAfterDone && OutputFiles.Count > 0) 
            {
                OutputFiles = OutputFiles.OrderBy(o => o.Index).ToList();
                if (DownloaderConfig.MyOptions.MuxImports != null)
                {
                    OutputFiles.AddRange(DownloaderConfig.MyOptions.MuxImports);
                }
                OutputFiles.ForEach(f => Logger.WarnMarkUp($"[grey]{Path.GetFileName(f.FilePath).EscapeMarkup()}[/]"));
                var saveDir = DownloaderConfig.MyOptions.SaveDir ?? Environment.CurrentDirectory;
                var ext = DownloaderConfig.MyOptions.MuxToMp4 ? ".mp4" : ".mkv";
                var outName = $"{DownloaderConfig.MyOptions.SaveName ?? NowDateTime.ToString("yyyy-MM-dd_HH-mm-ss")}.MUX";
                var outPath = Path.Combine(saveDir, outName);
                Logger.WarnMarkUp($"Muxing to [grey]{outName.EscapeMarkup()}{ext}[/]");
                var result = false;
                if (DownloaderConfig.MyOptions.UseMkvmerge) result = MergeUtil.MuxInputsByMkvmerge(DownloaderConfig.MyOptions.MkvmergeBinaryPath!, OutputFiles.ToArray(), outPath);
                else result = MergeUtil.MuxInputsByFFmpeg(DownloaderConfig.MyOptions.FFmpegBinaryPath!, OutputFiles.ToArray(), outPath, DownloaderConfig.MyOptions.MuxToMp4);
                //完成后删除各轨道文件
                if (result && !DownloaderConfig.MyOptions.MuxKeepFiles)
                {
                    Logger.WarnMarkUp("[grey]Cleaning files...[/]");
                    OutputFiles.ForEach(f => File.Delete(f.FilePath));
                    var tmpDir = DownloaderConfig.MyOptions.TmpDir ?? Environment.CurrentDirectory;
                    SafeDeleteDir(tmpDir);  
                }
                else Logger.ErrorMarkUp($"Mux failed");
                //判断是否要改名
                var newPath = Path.ChangeExtension(outPath, ext);
                if (result && !File.Exists(newPath))
                {
                    Logger.WarnMarkUp($"Rename to [grey]{Path.GetFileName(newPath).EscapeMarkup()}[/]");
                    File.Move(outPath + ext, newPath);
                }
            }

            return success;
        }
    }
}
