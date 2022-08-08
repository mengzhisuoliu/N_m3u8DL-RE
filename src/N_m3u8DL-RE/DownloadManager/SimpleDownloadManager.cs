﻿using Mp4SubtitleParser;
using N_m3u8DL_RE.Common.Entity;
using N_m3u8DL_RE.Common.Enum;
using N_m3u8DL_RE.Common.Log;
using N_m3u8DL_RE.Common.Resource;
using N_m3u8DL_RE.Config;
using N_m3u8DL_RE.Downloader;
using N_m3u8DL_RE.Entity;
using N_m3u8DL_RE.Util;
using Spectre.Console;
using System.Collections.Concurrent;
using System.Text;

namespace N_m3u8DL_RE.DownloadManager
{
    internal class SimpleDownloadManager
    {
        IDownloader Downloader;
        DownloaderConfig DownloaderConfig;
        DateTime NowDateTime;

        public SimpleDownloadManager(DownloaderConfig downloaderConfig) 
        { 
            this.DownloaderConfig = downloaderConfig;
            Downloader = new SimpleDownloader(DownloaderConfig);
            NowDateTime = DateTime.Now;
        }

        private async Task<bool> DownloadStreamAsync(StreamSpec streamSpec, ProgressTask task)
        {
            string? ReadInit(byte[] data)
            {
                var info = MP4InitUtil.ReadInit(data);
                if (info.Scheme != null) Logger.WarnMarkUp($"[grey]Type: {info.Scheme}[/]");
                if (info.PSSH != null) Logger.WarnMarkUp($"[grey]PSSH(WV): {info.PSSH}[/]");
                if (info.KID != null) Logger.WarnMarkUp($"[grey]KID: {info.KID}[/]");
                return info.KID;
            }

            ConcurrentDictionary<MediaSegment, DownloadResult?> FileDic = new();

            var segments = streamSpec.Playlist?.MediaParts.SelectMany(m => m.MediaSegments);
            if (segments == null) return false;

            var type = streamSpec.MediaType ?? Common.Enum.MediaType.VIDEO;
            var dirName = $"{DownloaderConfig.SaveName ?? NowDateTime.ToString("yyyy-MM-dd_HH-mm-ss")}_{streamSpec.GroupId}_{streamSpec.Codecs}_{streamSpec.Language}";
            //去除非法字符
            dirName = ConvertUtil.GetValidFileName(dirName, filterSlash: true);
            var tmpDir = Path.Combine(DownloaderConfig.TmpDir ?? Environment.CurrentDirectory, dirName);
            var saveDir = DownloaderConfig.SaveDir ?? Environment.CurrentDirectory;
            var saveName = DownloaderConfig.SaveName != null ? $"{DownloaderConfig.SaveName}.{type}.{streamSpec.Language}".TrimEnd('.') : dirName;
            var headers = DownloaderConfig.Headers;
            var output = Path.Combine(saveDir, saveName + $".{streamSpec.Extension ?? "ts"}");
            //检测目标文件是否存在
            while (File.Exists(output))
            {
                Logger.WarnMarkUp($"{output} => {output = Path.ChangeExtension(output, $"copy" + Path.GetExtension(output))}");
            }

            //mp4decrypt
            var mp4decrypt = DownloaderConfig.DecryptionBinaryPath!;
            var mp4InitFile = "";
            var currentKID = "";

            Logger.Debug($"dirName: {dirName}; tmpDir: {tmpDir}; saveDir: {saveDir}; saveName: {saveName}; output: {output}");

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

            //下载init
            if (streamSpec.Playlist?.MediaInit != null)
            {
                totalCount++;
                var path = Path.Combine(tmpDir, "_init.mp4.tmp");
                var result = await Downloader.DownloadSegmentAsync(streamSpec.Playlist.MediaInit, path, headers);
                FileDic[streamSpec.Playlist.MediaInit] = result;
                if (result == null)
                {
                    throw new Exception("Download init file failed!");
                }
                mp4InitFile = result.ActualFilePath;
                task.Increment(1);

                //修改输出后缀
                if (streamSpec.MediaType == Common.Enum.MediaType.AUDIO)
                    output = Path.ChangeExtension(output, ".m4a");
                else
                    output = Path.ChangeExtension(output, ".mp4");

                //读取mp4信息
                if (result != null && result.Success) 
                {
                    var data = File.ReadAllBytes(result.ActualFilePath);
                    currentKID = ReadInit(data);
                    //实时解密
                    if (DownloaderConfig.MP4RealTimeDecryption && streamSpec.Playlist.MediaInit.EncryptInfo.Method != Common.Enum.EncryptMethod.NONE)
                    {
                        var enc = result.ActualFilePath;
                        var dec = Path.Combine(Path.GetDirectoryName(enc)!, Path.GetFileNameWithoutExtension(enc) + "_dec" + Path.GetExtension(enc));
                        var dResult = await MP4DecryptUtil.DecryptAsync(DownloaderConfig.UseShakaPackager, mp4decrypt, DownloaderConfig.Keys, enc, dec, currentKID);
                        if (dResult)
                        {
                            FileDic[streamSpec.Playlist.MediaInit]!.ActualFilePath = dec;
                        }
                    }
                }
            }

            //开始下载
            var pad = "0".PadLeft(segments.Count().ToString().Length, '0');
            var options = new ParallelOptions()
            {
                MaxDegreeOfParallelism = DownloaderConfig.ThreadCount
            };
            await Parallel.ForEachAsync(segments, options, async (seg, _) =>
            {
                var index = seg.Index;
                var path = Path.Combine(tmpDir, index.ToString(pad) + $".{streamSpec.Extension ?? "clip"}.tmp");
                var result = await Downloader.DownloadSegmentAsync(seg, path, headers);
                FileDic[seg] = result;
                task.Increment(1);
                //实时解密
                if (DownloaderConfig.MP4RealTimeDecryption && seg.EncryptInfo.Method != Common.Enum.EncryptMethod.NONE && result != null) 
                {
                    var enc = result.ActualFilePath;
                    var dec = Path.Combine(Path.GetDirectoryName(enc)!, Path.GetFileNameWithoutExtension(enc) + "_dec" + Path.GetExtension(enc));
                    var dResult = await MP4DecryptUtil.DecryptAsync(DownloaderConfig.UseShakaPackager, mp4decrypt, DownloaderConfig.Keys, enc, dec, currentKID, mp4InitFile);
                    if (dResult)
                    {
                        File.Delete(enc);
                        result.ActualFilePath = dec;
                    }
                }
            });

            if (DownloaderConfig.MP4RealTimeDecryption && mp4InitFile != "")
            {
                File.Delete(mp4InitFile);
                //shaka实时解密不需要init文件用于合并
                if (DownloaderConfig.UseShakaPackager)
                {
                    FileDic!.Remove(streamSpec.Playlist!.MediaInit, out _);
                }
            }

            //校验分片数量
            if (DownloaderConfig.CheckSegmentsCount && FileDic.Values.Any(s => s == null))
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
            if (DownloaderConfig.AutoSubtitleFix && streamSpec.MediaType == Common.Enum.MediaType.SUBTITLES 
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
                if (DownloaderConfig.SubtitleFormat != Enum.SubtitleFormat.VTT)
                {
                    path = Path.ChangeExtension(path, ".srt");
                    subContentFixed = ConvertUtil.WebVtt2Other(finalVtt, DownloaderConfig.SubtitleFormat);
                    output = Path.ChangeExtension(output, ".srt");
                }
                await File.WriteAllTextAsync(path, subContentFixed, new UTF8Encoding(false));
                FileDic[keys.First()] = new DownloadResult()
                {
                    ActualContentLength = subContentFixed.Length,
                    ActualFilePath = path
                };
            }

            //自动修复VTT mp4字幕
            if (DownloaderConfig.AutoSubtitleFix && streamSpec.MediaType == Common.Enum.MediaType.SUBTITLES
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
                    if (DownloaderConfig.SubtitleFormat != Enum.SubtitleFormat.VTT)
                    {
                        path = Path.ChangeExtension(path, ".srt");
                        subContentFixed = ConvertUtil.WebVtt2Other(finalVtt, DownloaderConfig.SubtitleFormat);
                        output = Path.ChangeExtension(output, ".srt");
                    }
                    await File.WriteAllTextAsync(path, subContentFixed, new UTF8Encoding(false));
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
            if (DownloaderConfig.AutoSubtitleFix && streamSpec.MediaType == Common.Enum.MediaType.SUBTITLES
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
                if (DownloaderConfig.SubtitleFormat != Enum.SubtitleFormat.VTT)
                {
                    path = Path.ChangeExtension(path, ".srt");
                    subContentFixed = ConvertUtil.WebVtt2Other(finalVtt, DownloaderConfig.SubtitleFormat);
                    output = Path.ChangeExtension(output, ".srt");
                }
                await File.WriteAllTextAsync(path, subContentFixed, new UTF8Encoding(false));
                FileDic[firstKey] = new DownloadResult()
                {
                    ActualContentLength = subContentFixed.Length,
                    ActualFilePath = path
                };
                //修改输出后缀
                output = Path.ChangeExtension(output, Path.GetExtension(path));
            }

            //自动修复TTML mp4字幕
            if (DownloaderConfig.AutoSubtitleFix && streamSpec.MediaType == Common.Enum.MediaType.SUBTITLES
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
                if (DownloaderConfig.SubtitleFormat != Enum.SubtitleFormat.VTT)
                {
                    path = Path.ChangeExtension(path, ".srt");
                    subContentFixed = ConvertUtil.WebVtt2Other(finalVtt, DownloaderConfig.SubtitleFormat);
                    output = Path.ChangeExtension(output, ".srt");
                }
                await File.WriteAllTextAsync(path, subContentFixed, new UTF8Encoding(false));
                FileDic[firstKey] = new DownloadResult()
                {
                    ActualContentLength = subContentFixed.Length,
                    ActualFilePath = path
                };
                //修改输出后缀
                output = Path.ChangeExtension(output, Path.GetExtension(path));
            }

            //合并
            if (!DownloaderConfig.SkipMerge)
            {
                //对于fMP4，自动开启二进制合并
                if (!DownloaderConfig.BinaryMerge && mp4InitFile != "")
                {
                    DownloaderConfig.BinaryMerge = true;
                    Logger.WarnMarkUp($"[white on darkorange3_1]{ResString.autoBinaryMerge}[/]");
                }

                if (DownloaderConfig.BinaryMerge)
                {
                    Logger.InfoMarkUp(ResString.binaryMerge);
                    var files = FileDic.Values.Select(v => v!.ActualFilePath).OrderBy(s => s).ToArray();
                    MergeUtil.CombineMultipleFilesIntoSingleFile(files, output);
                }
                else
                {
                    var files = FileDic.Values.Select(v => v!.ActualFilePath).OrderBy(s => s).ToArray();
                    Logger.InfoMarkUp(ResString.ffmpegMerge);
                    MergeUtil.MergeByFFmpeg(DownloaderConfig.FFmpegBinaryPath!, files, Path.ChangeExtension(output, null), "mp4");
                }
            }

            //删除临时文件夹
            if (!DownloaderConfig.SkipMerge && DownloaderConfig.DelAfterDone)
            {
                var files = FileDic.Values.Select(v => v!.ActualFilePath);
                foreach (var file in files)
                {
                    File.Delete(file);
                }
                if (!Directory.EnumerateFiles(tmpDir).Any())
                {
                    Directory.Delete(tmpDir);
                }
            }

            //调用mp4decrypt解密
            if (File.Exists(output) && !DownloaderConfig.MP4RealTimeDecryption && DownloaderConfig.Keys != null && DownloaderConfig.Keys.Length > 0) 
            {
                if (totalCount >= 1 && streamSpec.Playlist!.MediaParts.First().MediaSegments.First().EncryptInfo.Method != Common.Enum.EncryptMethod.NONE) 
                {
                    if (string.IsNullOrEmpty(currentKID))
                    {
                        using (var fs = File.OpenRead(output))
                        {
                            var header = new byte[4096]; //4KB
                            fs.Read(header);
                            currentKID = ReadInit(header);
                        }
                    }
                    var enc = output;
                    var dec = Path.Combine(Path.GetDirectoryName(enc)!, Path.GetFileNameWithoutExtension(enc) + "_dec" + Path.GetExtension(enc));
                    Logger.InfoMarkUp($"[grey]Decrypting...[/]");
                    var result = await MP4DecryptUtil.DecryptAsync(DownloaderConfig.UseShakaPackager, mp4decrypt, DownloaderConfig.Keys, enc, dec, currentKID);
                    if (result) 
                    {
                        File.Delete(enc);
                        File.Move(dec, enc);
                        output = dec;
                    }
                }
            }

            return true;
        }

        public async Task<bool> StartDownloadAsync(IEnumerable<StreamSpec> streamSpecs)
        {
            ConcurrentDictionary<StreamSpec, bool?> Results = new();

            var progress = AnsiConsole.Progress().AutoClear(true);

            //进度条的列定义
            progress.Columns(new ProgressColumn[]
            {
                new TaskDescriptionColumn() { Alignment = Justify.Left },
                new ProgressBarColumn(),
                new PercentageColumn(),
                new RemainingTimeColumn(),
                new SpinnerColumn(),
            });

            await progress.StartAsync(async ctx =>
            {
                //创建任务
                var dic = streamSpecs.Select(item =>
                {
                    var task = ctx.AddTask(item.ToShortString(), autoStart: false);
                    return (item, task);
                }).ToDictionary(item => item.item, item => item.task);
                //遍历，顺序下载
                foreach (var kp in dic)
                {
                    var task = kp.Value;
                    var result = await DownloadStreamAsync(kp.Key, task);
                    Results[kp.Key] = result;
                }
            });

            return Results.Values.All(v => v == true);
        }
    }
}
