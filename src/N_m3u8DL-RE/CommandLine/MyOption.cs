﻿using N_m3u8DL_RE.Common.Log;
using N_m3u8DL_RE.Entity;
using N_m3u8DL_RE.Enum;

namespace N_m3u8DL_RE.CommandLine
{
    internal class MyOption
    {
        /// <summary>
        /// See: <see cref="CommandInvoker.Input"/>.
        /// </summary>
        public string Input { get; set; } = default!;
        /// <summary>
        /// See: <see cref="CommandInvoker.Headers"/>.
        /// </summary>
        public Dictionary<string, string> Headers { get; set; } = new Dictionary<string, string>();
        /// <summary>
        /// See: <see cref="CommandInvoker.Keys"/>.
        /// </summary>
        public string[]? Keys { get; set; }
        /// <summary>
        /// See: <see cref="CommandInvoker.BaseUrl"/>.
        /// </summary>
        public string? BaseUrl { get; set; }
        /// <summary>
        /// See: <see cref="CommandInvoker.KeyTextFile"/>.
        /// </summary>
        public string? KeyTextFile { get; set; }
        /// <summary>
        /// See: <see cref="CommandInvoker.UrlProcessorArgs"/>.
        /// </summary>
        public string? UrlProcessorArgs { get; set; }
        /// <summary>
        /// See: <see cref="CommandInvoker.LogLevel"/>.
        /// </summary>
        public LogLevel LogLevel { get; set; }
        /// <summary>
        /// See: <see cref="CommandInvoker.AutoSelect"/>.
        /// </summary>
        public bool AutoSelect { get; set; }
        /// <summary>
        /// See: <see cref="CommandInvoker.SubOnly"/>.
        /// </summary>
        public bool SubOnly { get; set; }
        /// <summary>
        /// See: <see cref="CommandInvoker.ThreadCount"/>.
        /// </summary>
        public int ThreadCount { get; set; }
        /// <summary>
        /// See: <see cref="CommandInvoker.DownloadRetryCount"/>.
        /// </summary>
        public int DownloadRetryCount { get; set; }
        /// <summary>
        /// See: <see cref="CommandInvoker.LiveRecordLimit"/>.
        /// </summary>
        public TimeSpan? LiveRecordLimit { get; set; }
        /// <summary>
        /// See: <see cref="CommandInvoker.SkipMerge"/>.
        /// </summary>
        public bool SkipMerge { get; set; }
        /// <summary>
        /// See: <see cref="CommandInvoker.BinaryMerge"/>.
        /// </summary>
        public bool BinaryMerge { get; set; }
        /// <summary>
        /// See: <see cref="CommandInvoker.DelAfterDone"/>.
        /// </summary>
        public bool DelAfterDone { get; set; }
        /// <summary>
        /// See: <see cref="CommandInvoker.AutoSubtitleFix"/>.
        /// </summary>
        public bool AutoSubtitleFix { get; set; }
        /// <summary>
        /// See: <see cref="CommandInvoker.CheckSegmentsCount"/>.
        /// </summary>
        public bool CheckSegmentsCount { get; set; }
        /// <summary>
        /// See: <see cref="CommandInvoker.SkipDownload"/>.
        /// </summary>
        public bool SkipDownload { get; set; }
        /// <summary>
        /// See: <see cref="CommandInvoker.WriteMetaJson"/>.
        /// </summary>
        public bool WriteMetaJson { get; set; }
        /// <summary>
        /// See: <see cref="CommandInvoker.AppendUrlParams"/>.
        /// </summary>
        public bool AppendUrlParams { get; set; }
        /// <summary>
        /// See: <see cref="CommandInvoker.MP4RealTimeDecryption"/>.
        /// </summary>
        public bool MP4RealTimeDecryption { get; set; }
        /// <summary>
        /// See: <see cref="CommandInvoker.UseShakaPackager"/>.
        /// </summary>
        public bool UseShakaPackager { get; set; }
        /// <summary>
        /// See: <see cref="CommandInvoker.MuxAfterDone"/>.
        /// </summary>
        public bool MuxAfterDone { get; set; }
        /// <summary>
        /// See: <see cref="CommandInvoker.MuxToMp4"/>.
        /// </summary>
        public bool MuxToMp4 { get; set; }
        /// <summary>
        /// See: <see cref="CommandInvoker.UseMkvmerge"/>.
        /// </summary>
        public bool UseMkvmerge { get; set; }
        /// <summary>
        /// See: <see cref="CommandInvoker.ConcurrentDownload"/>.
        /// </summary>
        public bool ConcurrentDownload { get; set; }
        /// <summary>
        /// See: <see cref="CommandInvoker.LiveRealTimeMerge"/>.
        /// </summary>
        public bool LiveRealTimeMerge { get; set; }
        /// <summary>
        /// See: <see cref="CommandInvoker.LiveKeepSegments"/>.
        /// </summary>
        public bool LiveKeepSegments { get; set; }
        /// <summary>
        /// See: <see cref="CommandInvoker.LivePerformAsVod"/>.
        /// </summary>
        public bool LivePerformAsVod { get; set; }
        /// <summary>
        /// See: <see cref="CommandInvoker.SubtitleFormat"/>.
        /// </summary>
        public SubtitleFormat SubtitleFormat { get; set; }
        /// <summary>
        /// See: <see cref="CommandInvoker.TmpDir"/>.
        /// </summary>
        public string? TmpDir { get; set; }
        /// <summary>
        /// See: <see cref="CommandInvoker.SaveDir"/>.
        /// </summary>
        public string? SaveDir { get; set; }
        /// <summary>
        /// See: <see cref="CommandInvoker.SaveName"/>.
        /// </summary>
        public string? SaveName { get; set; }
        /// <summary>
        /// See: <see cref="CommandInvoker.SavePattern"/>.
        /// </summary>
        public string? SavePattern { get; set; }
        /// <summary>
        /// See: <see cref="CommandInvoker.UILanguage"/>.
        /// </summary>
        public string? UILanguage { get; set; }
        /// <summary>
        /// See: <see cref="CommandInvoker.DecryptionBinaryPath"/>.
        /// </summary>
        public string? DecryptionBinaryPath { get; set; }
        /// <summary>
        /// See: <see cref="CommandInvoker.FFmpegBinaryPath"/>.
        /// </summary>
        public string? FFmpegBinaryPath { get; set; }
        /// <summary>
        /// See: <see cref="CommandInvoker.MkvmergeBinaryPath"/>.
        /// </summary>
        public string? MkvmergeBinaryPath { get; set; }
        /// <summary>
        /// See: <see cref="CommandInvoker.MuxImports"/>.
        /// </summary>
        public List<OutputFile>? MuxImports { get; set; }
        /// <summary>
        /// See: <see cref="CommandInvoker.VideoFilter"/>.
        /// </summary>
        public StreamFilter? VideoFilter { get; set; }
        /// <summary>
        /// See: <see cref="CommandInvoker.AudioFilter"/>.
        /// </summary>
        public StreamFilter? AudioFilter { get; set; }
        /// <summary>
        /// See: <see cref="CommandInvoker.SubtitleFilter"/>.
        /// </summary>
        public StreamFilter? SubtitleFilter { get; set; }
        public bool MuxKeepFiles { get; set; }
    }
}