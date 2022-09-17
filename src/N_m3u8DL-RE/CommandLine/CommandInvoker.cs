﻿using N_m3u8DL_RE.Common.Log;
using N_m3u8DL_RE.Common.Resource;
using N_m3u8DL_RE.Entity;
using N_m3u8DL_RE.Enum;
using N_m3u8DL_RE.Util;
using NiL.JS.Expressions;
using System.CommandLine;
using System.CommandLine.Binding;
using System.CommandLine.Parsing;
using System.Globalization;
using System.Text.RegularExpressions;

namespace N_m3u8DL_RE.CommandLine
{
    internal partial class CommandInvoker
    {
        [GeneratedRegex("((best|worst)\\d*|all)")]
        private static partial Regex ForStrRegex();

        private readonly static Argument<string> Input = new(name: "input", description: ResString.cmd_Input);
        private readonly static Option<string?> TmpDir = new(new string[] { "--tmp-dir" }, description: ResString.cmd_tmpDir);
        private readonly static Option<string?> SaveDir = new(new string[] { "--save-dir" }, description: ResString.cmd_saveDir);
        private readonly static Option<string?> SaveName = new(new string[] { "--save-name" }, description: ResString.cmd_saveName, parseArgument: ParseSaveName);
        private readonly static Option<string?> SavePattern = new(new string[] { "--save-pattern" }, description: ResString.cmd_savePattern, getDefaultValue: () => "<SaveName>_<Id>_<Codecs>_<Language>_<Ext>");
        private readonly static Option<string?> UILanguage = new Option<string?>(new string[] { "--ui-language" }, description: ResString.cmd_uiLanguage).FromAmong("en-US", "zh-CN", "zh-TW");
        private readonly static Option<string?> UrlProcessorArgs = new(new string[] { "--urlprocessor-args" }, description: ResString.cmd_urlProcessorArgs);
        private readonly static Option<string[]?> Keys = new(new string[] { "--key" }, description: ResString.cmd_keys) { Arity = ArgumentArity.OneOrMore, AllowMultipleArgumentsPerToken = false };
        private readonly static Option<string> KeyTextFile = new(new string[] { "--key-text-file" }, description: ResString.cmd_keyText);
        private readonly static Option<Dictionary<string, string>> Headers = new(new string[] { "-H", "--header" }, description: ResString.cmd_header, parseArgument: ParseHeaders) { Arity = ArgumentArity.OneOrMore, AllowMultipleArgumentsPerToken = false };
        private readonly static Option<LogLevel> LogLevel = new(name: "--log-level", description: ResString.cmd_logLevel, getDefaultValue: () => Common.Log.LogLevel.INFO);
        private readonly static Option<SubtitleFormat> SubtitleFormat = new(name: "--sub-format", description: ResString.cmd_subFormat, getDefaultValue: () => Enum.SubtitleFormat.VTT);
        private readonly static Option<bool> AutoSelect = new(new string[] { "--auto-select" }, description: ResString.cmd_autoSelect, getDefaultValue: () => false);
        private readonly static Option<bool> SubOnly = new(new string[] { "--sub-only" }, description: ResString.cmd_subOnly, getDefaultValue: () => false);
        private readonly static Option<int> ThreadCount = new(new string[] { "--thread-count" }, description: ResString.cmd_threadCount, getDefaultValue: () => 8) { ArgumentHelpName = "number" };
        private readonly static Option<int> DownloadRetryCount = new(new string[] { "--download-retry-count" }, description: ResString.cmd_downloadRetryCount, getDefaultValue: () => 3) { ArgumentHelpName = "number" };
        private readonly static Option<bool> SkipMerge = new(new string[] { "--skip-merge" }, description: ResString.cmd_skipMerge, getDefaultValue: () => false);
        private readonly static Option<bool> SkipDownload = new(new string[] { "--skip-download" }, description: ResString.cmd_skipDownload, getDefaultValue: () => false);
        private readonly static Option<bool> BinaryMerge = new(new string[] { "--binary-merge" }, description: ResString.cmd_binaryMerge, getDefaultValue: () => false);
        private readonly static Option<bool> DelAfterDone = new(new string[] { "--del-after-done" }, description: ResString.cmd_delAfterDone, getDefaultValue: () => true);
        private readonly static Option<bool> AutoSubtitleFix = new(new string[] { "--auto-subtitle-fix" }, description: ResString.cmd_subtitleFix, getDefaultValue: () => true);
        private readonly static Option<bool> CheckSegmentsCount = new(new string[] { "--check-segments-count" }, description: ResString.cmd_checkSegmentsCount, getDefaultValue: () => true);
        private readonly static Option<bool> WriteMetaJson = new(new string[] { "--write-meta-json" }, description: ResString.cmd_writeMetaJson, getDefaultValue: () => false);
        private readonly static Option<bool> AppendUrlParams = new(new string[] { "--append-url-params" }, description: ResString.cmd_appendUrlParams, getDefaultValue: () => false);
        private readonly static Option<bool> MP4RealTimeDecryption = new (new string[] { "--mp4-real-time-decryption" }, description: ResString.cmd_MP4RealTimeDecryption, getDefaultValue: () => false);
        private readonly static Option<bool> UseShakaPackager = new (new string[] { "--use-shaka-packager" }, description: ResString.cmd_useShakaPackager, getDefaultValue: () => false);
        private readonly static Option<string?> DecryptionBinaryPath = new(new string[] { "--decryption-binary-path" }, description: ResString.cmd_decryptionBinaryPath) { ArgumentHelpName = "PATH" };
        private readonly static Option<string?> FFmpegBinaryPath = new(new string[] { "--ffmpeg-binary-path" }, description: ResString.cmd_ffmpegBinaryPath) { ArgumentHelpName = "PATH" };
        private readonly static Option<string?> BaseUrl = new(new string[] { "--base-url" }, description: ResString.cmd_baseUrl);
        private readonly static Option<bool> ConcurrentDownload = new(new string[] { "-mt", "--concurrent-download" }, description: ResString.cmd_concurrentDownload, getDefaultValue: () => false);

        //直播相关
        private readonly static Option<bool> LivePerformAsVod = new(new string[] { "--live-perform-as-vod" }, description: ResString.cmd_livePerformAsVod, getDefaultValue: () => false);
        private readonly static Option<bool> LiveRealTimeMerge = new(new string[] { "--live-real-time-merge" }, description: ResString.cmd_liveRealTimeMerge, getDefaultValue: () => false);
        private readonly static Option<bool> LiveKeepSegments = new(new string[] { "--live-keep-segments" }, description: ResString.cmd_liveKeepSegments, getDefaultValue: () => true);
        private readonly static Option<TimeSpan?> LiveRecordLimit = new(new string[] { "--live-record-limit" }, description: ResString.cmd_liveRecordLimit, parseArgument: ParseLiveLimit) { ArgumentHelpName = "HH:mm:ss" };


        //复杂命令行如下
        private readonly static Option<MuxOptions?> MuxAfterDone = new(new string[] { "-M", "--mux-after-done" }, description: ResString.cmd_muxAfterDone, parseArgument: ParseMuxAfterDone) { ArgumentHelpName = "OPTIONS" };
        private readonly static Option<List<OutputFile>> MuxImports = new("--mux-import", description: ResString.cmd_muxImport, parseArgument: ParseImports) { Arity = ArgumentArity.OneOrMore, AllowMultipleArgumentsPerToken = false, ArgumentHelpName = "OPTIONS" };
        private readonly static Option<StreamFilter?> VideoFilter = new(new string[] { "-sv", "--select-video" }, description: ResString.cmd_selectVideo, parseArgument: ParseStreamFilter) { ArgumentHelpName = "OPTIONS" };
        private readonly static Option<StreamFilter?> AudioFilter = new(new string[] { "-sa", "--select-audio" }, description: ResString.cmd_selectAudio, parseArgument: ParseStreamFilter) { ArgumentHelpName = "OPTIONS" };
        private readonly static Option<StreamFilter?> SubtitleFilter = new(new string[] { "-ss", "--select-subtitle" }, description: ResString.cmd_selectSubtitle, parseArgument: ParseStreamFilter) { ArgumentHelpName = "OPTIONS" };

        /// <summary>
        /// 解析录制直播时长限制
        /// </summary>
        /// <param name="result"></param>
        /// <returns></returns>
        private static TimeSpan? ParseLiveLimit(ArgumentResult result)
        {
            var input = result.Tokens.First().Value;
            try
            {
                return OtherUtil.ParseDur(input);
            }
            catch (Exception)
            {
                result.ErrorMessage = "error in parse LiveRecordLimit: " + input;
                return null;
            }
        }

        private static string? ParseSaveName(ArgumentResult result)
        {
            var input = result.Tokens.First().Value;
            var newName = OtherUtil.GetValidFileName(input);
            if (string.IsNullOrEmpty(newName))
            {
                result.ErrorMessage = "Invalid save name!";
                return null;
            }
            return newName;
        }

        /// <summary>
        /// 流过滤器
        /// </summary>
        /// <param name="result"></param>
        /// <returns></returns>
        private static StreamFilter? ParseStreamFilter(ArgumentResult result)
        {
            var streamFilter = new StreamFilter();
            var input = result.Tokens.First().Value;
            var p = new ComplexParamParser(input);


            //目标范围
            var forStr = "";
            if (input == ForStrRegex().Match(input).Value)
            {
                forStr = input;
            }
            else
            {
                forStr = p.GetValue("for") ?? "best";
                if (forStr != ForStrRegex().Match(forStr).Value)
                {
                    result.ErrorMessage = $"for={forStr} not valid";
                    return null;
                }
            }
            streamFilter.For = forStr;

            var id = p.GetValue("id");
            if (!string.IsNullOrEmpty(id))
                streamFilter.GroupIdReg = new Regex(id);

            var lang = p.GetValue("lang");
            if (!string.IsNullOrEmpty(lang))
                streamFilter.LanguageReg = new Regex(lang);

            var name = p.GetValue("name");
            if (!string.IsNullOrEmpty(name))
                streamFilter.NameReg = new Regex(name);

            var codecs = p.GetValue("codecs");
            if (!string.IsNullOrEmpty(codecs))
                streamFilter.CodecsReg = new Regex(codecs);

            var res = p.GetValue("res");
            if (!string.IsNullOrEmpty(res))
                streamFilter.ResolutionReg = new Regex(res);

            var frame = p.GetValue("frame");
            if (!string.IsNullOrEmpty(frame))
                streamFilter.FrameRateReg = new Regex(frame);

            var channel = p.GetValue("channel");
            if (!string.IsNullOrEmpty(channel))
                streamFilter.ChannelsReg = new Regex(channel);

            var range = p.GetValue("range");
            if (!string.IsNullOrEmpty(range))
                streamFilter.VideoRangeReg = new Regex(range);

            var url = p.GetValue("url");
            if (!string.IsNullOrEmpty(url))
                streamFilter.UrlReg = new Regex(url);

            return streamFilter;
        }

        /// <summary>
        /// 分割Header
        /// </summary>
        /// <param name="result"></param>
        /// <returns></returns>
        private static Dictionary<string, string> ParseHeaders(ArgumentResult result)
        {
            var array = result.Tokens.Select(t => t.Value).ToArray();
            return OtherUtil.SplitHeaderArrayToDic(array);
        }

        /// <summary>
        /// 解析混流引入的外部文件
        /// </summary>
        /// <param name="result"></param>
        /// <returns></returns>
        private static List<OutputFile> ParseImports(ArgumentResult result)
        {
            var imports = new List<OutputFile>();

            foreach (var item in result.Tokens)
            {
                var p = new ComplexParamParser(item.Value);
                var path = p.GetValue("path") ?? item.Value; //若未获取到，直接整个字符串作为path
                var lang = p.GetValue("lang");
                var name = p.GetValue("name");
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                {
                    result.ErrorMessage = "path empty or file not exists!";
                    return imports;
                }
                imports.Add(new OutputFile()
                {
                    Index = 999,
                    FilePath = path,
                    LangCode = lang,
                    Description = name
                });
            }

            return imports;
        }

        /// <summary>
        /// 解析混流选项
        /// </summary>
        /// <param name="result"></param>
        /// <returns></returns>
        private static MuxOptions? ParseMuxAfterDone(ArgumentResult result)
        {
            var v = result.Tokens.First().Value;
            var p = new ComplexParamParser(v);
            //混流格式
            var format = p.GetValue("format") ?? v.Split(':')[0]; //若未获取到，直接:前的字符串作为format解析
            if (format != "mp4" && format != "mkv")
            {
                result.ErrorMessage = $"format={format} not valid";
                return null;
            }
            //混流器
            var muxer = p.GetValue("muxer") ?? "ffmpeg";
            if (muxer != "ffmpeg" && muxer != "mkvmerge")
            {
                result.ErrorMessage = $"muxer={muxer} not valid";
                return null;
            }
            //混流器路径
            var bin_path = p.GetValue("bin_path") ?? "auto";
            if (string.IsNullOrEmpty(bin_path))
            {
                result.ErrorMessage = $"bin_path={bin_path} not valid";
                return null;
            }
            //是否删除
            var keep = p.GetValue("keep") ?? "false";
            if (keep != "true" && keep != "false")
            {
                result.ErrorMessage = $"keep={keep} not valid";
                return null;
            }
            //冲突检测
            if (muxer == "mkvmerge" && format == "mp4")
            {
                result.ErrorMessage = $"mkvmerge can not do mp4";
                return null;
            }
            return new MuxOptions()
            {
                UseMkvmerge = muxer == "mkvmerge",
                MuxToMp4 = format == "mp4",
                KeepFiles = keep == "true",
                BinPath = bin_path == "auto" ? null : bin_path
            };
        }

        class MyOptionBinder : BinderBase<MyOption>
        {
            protected override MyOption GetBoundValue(BindingContext bindingContext)
            {
                var option = new MyOption
                {
                    Input = bindingContext.ParseResult.GetValueForArgument(Input),
                    LogLevel = bindingContext.ParseResult.GetValueForOption(LogLevel),
                    AutoSelect = bindingContext.ParseResult.GetValueForOption(AutoSelect),
                    SkipMerge = bindingContext.ParseResult.GetValueForOption(SkipMerge),
                    BinaryMerge = bindingContext.ParseResult.GetValueForOption(BinaryMerge),
                    DelAfterDone = bindingContext.ParseResult.GetValueForOption(DelAfterDone),
                    AutoSubtitleFix = bindingContext.ParseResult.GetValueForOption(AutoSubtitleFix),
                    CheckSegmentsCount = bindingContext.ParseResult.GetValueForOption(CheckSegmentsCount),
                    SubtitleFormat = bindingContext.ParseResult.GetValueForOption(SubtitleFormat),
                    SubOnly = bindingContext.ParseResult.GetValueForOption(SubOnly),
                    TmpDir = bindingContext.ParseResult.GetValueForOption(TmpDir),
                    SaveDir = bindingContext.ParseResult.GetValueForOption(SaveDir),
                    SaveName = bindingContext.ParseResult.GetValueForOption(SaveName),
                    ThreadCount = bindingContext.ParseResult.GetValueForOption(ThreadCount),
                    UILanguage = bindingContext.ParseResult.GetValueForOption(UILanguage),
                    SkipDownload = bindingContext.ParseResult.GetValueForOption(SkipDownload),
                    WriteMetaJson = bindingContext.ParseResult.GetValueForOption(WriteMetaJson),
                    AppendUrlParams = bindingContext.ParseResult.GetValueForOption(AppendUrlParams),
                    SavePattern = bindingContext.ParseResult.GetValueForOption(SavePattern),
                    Keys = bindingContext.ParseResult.GetValueForOption(Keys),
                    UrlProcessorArgs = bindingContext.ParseResult.GetValueForOption(UrlProcessorArgs),
                    MP4RealTimeDecryption = bindingContext.ParseResult.GetValueForOption(MP4RealTimeDecryption),
                    UseShakaPackager = bindingContext.ParseResult.GetValueForOption(UseShakaPackager),
                    DecryptionBinaryPath = bindingContext.ParseResult.GetValueForOption(DecryptionBinaryPath),
                    FFmpegBinaryPath = bindingContext.ParseResult.GetValueForOption(FFmpegBinaryPath),
                    KeyTextFile = bindingContext.ParseResult.GetValueForOption(KeyTextFile),
                    DownloadRetryCount = bindingContext.ParseResult.GetValueForOption(DownloadRetryCount),
                    BaseUrl = bindingContext.ParseResult.GetValueForOption(BaseUrl),
                    MuxImports = bindingContext.ParseResult.GetValueForOption(MuxImports),
                    ConcurrentDownload = bindingContext.ParseResult.GetValueForOption(ConcurrentDownload),
                    VideoFilter = bindingContext.ParseResult.GetValueForOption(VideoFilter),
                    AudioFilter = bindingContext.ParseResult.GetValueForOption(AudioFilter),
                    SubtitleFilter = bindingContext.ParseResult.GetValueForOption(SubtitleFilter),
                    LiveRealTimeMerge = bindingContext.ParseResult.GetValueForOption(LiveRealTimeMerge),
                    LiveKeepSegments = bindingContext.ParseResult.GetValueForOption(LiveKeepSegments),
                    LiveRecordLimit = bindingContext.ParseResult.GetValueForOption(LiveRecordLimit),
                    LivePerformAsVod = bindingContext.ParseResult.GetValueForOption(LivePerformAsVod),
                };

                var parsedHeaders = bindingContext.ParseResult.GetValueForOption(Headers);
                if (parsedHeaders != null)
                    option.Headers = parsedHeaders;


                //以用户选择语言为准优先
                if (option.UILanguage != null)
                {
                    CultureInfo.DefaultThreadCurrentCulture = new CultureInfo(option.UILanguage);
                    Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(option.UILanguage);
                    Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo(option.UILanguage);
                }

                //混流设置
                var muxAfterDoneValue = bindingContext.ParseResult.GetValueForOption(MuxAfterDone);
                if (muxAfterDoneValue != null) 
                {
                    option.MuxAfterDone = true;
                    option.MuxKeepFiles = muxAfterDoneValue.KeepFiles;
                    option.MuxToMp4 = muxAfterDoneValue.MuxToMp4;
                    option.UseMkvmerge = muxAfterDoneValue.UseMkvmerge;
                    if (option.UseMkvmerge) option.MkvmergeBinaryPath = muxAfterDoneValue.BinPath;
                    else option.FFmpegBinaryPath = muxAfterDoneValue.BinPath;
                }


                return option;
            }
        }


        public static async Task<int> InvokeArgs(string[] args, Func<MyOption, Task> action)
        {
            var rootCommand = new RootCommand("N_m3u8DL-RE (Beta version) 20220917")
            {
                Input, TmpDir, SaveDir, SaveName, BaseUrl, ThreadCount, DownloadRetryCount, AutoSelect, SkipMerge, SkipDownload, CheckSegmentsCount,
                BinaryMerge, DelAfterDone, WriteMetaJson, AppendUrlParams, ConcurrentDownload, Headers, /**SavePattern,**/ SubOnly, SubtitleFormat, AutoSubtitleFix,
                FFmpegBinaryPath,
                LogLevel, UILanguage, UrlProcessorArgs, Keys, KeyTextFile, DecryptionBinaryPath, UseShakaPackager, MP4RealTimeDecryption,
                MuxAfterDone,
                LivePerformAsVod, LiveRealTimeMerge, LiveKeepSegments, LiveRecordLimit,
                MuxImports, VideoFilter, AudioFilter, SubtitleFilter
            };
            rootCommand.TreatUnmatchedTokensAsErrors = true;
            rootCommand.SetHandler(async (myOption) => await action(myOption), new MyOptionBinder());

            return await rootCommand.InvokeAsync(args);
        }
    }
}
