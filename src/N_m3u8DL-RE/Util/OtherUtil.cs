﻿using N_m3u8DL_RE.Common.Entity;
using N_m3u8DL_RE.Common.Log;
using N_m3u8DL_RE.Enum;
using System.CommandLine;
using System.Text;

namespace N_m3u8DL_RE.Util
{
    internal class OtherUtil
    {
        public static Dictionary<string, string> SplitHeaderArrayToDic(string[]? headers)
        {
            Dictionary<string, string> dic = new();

            if (headers != null)
            {
                foreach (string header in headers)
                {
                    var index = header.IndexOf(':');
                    if (index != -1)
                    {
                        dic[header[..index].Trim().ToLower()] = header[(index + 1)..].Trim();
                    }
                }
            }

            return dic;
        }

        private static string WebVtt2Srt(WebVttSub vtt)
        {
            StringBuilder sb = new StringBuilder();
            int index = 1;
            foreach (var c in vtt.Cues)
            {
                sb.AppendLine($"{index++}");
                sb.AppendLine(c.StartTime.ToString(@"hh\:mm\:ss\,fff") + " --> " + c.EndTime.ToString(@"hh\:mm\:ss\,fff"));
                sb.AppendLine(c.Payload);
                sb.AppendLine();
            }
            sb.AppendLine();
            return sb.ToString();
        }

        public static string WebVtt2Other(WebVttSub vtt, SubtitleFormat toFormat)
        {
            Logger.Debug($"Convert {SubtitleFormat.VTT} ==> {toFormat}");
            return toFormat switch
            {
                SubtitleFormat.VTT => vtt.ToStringWithHeader(),
                SubtitleFormat.SRT => WebVtt2Srt(vtt),
                _ => throw new NotSupportedException($"{toFormat} not supported!")
            };
        }

        public static string GetValidFileName(string input, string re = ".", bool filterSlash = false)
        {
            string title = input;
            foreach (char invalidChar in Path.GetInvalidFileNameChars())
            {
                title = title.Replace(invalidChar.ToString(), re);
            }
            if (filterSlash)
            {
                title = title.Replace("/", re);
                title = title.Replace("\\", re);
            }
            return title.Trim('.');
        }

        /// <summary>
        /// 从输入自动获取文件名
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static string GetFileNameFromInput(string input, bool addSuffix = true)
        {
            var saveName = addSuffix ? DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") : string.Empty;
            if (File.Exists(input))
            {
                saveName = Path.GetFileNameWithoutExtension(input) + "_" + saveName;
            }
            else
            {
                var uri = new Uri(input);
                var name = uri.GetLeftPart(UriPartial.Path).Split('/').Last();
                name = string.Join(".", name.Split('.').SkipLast(1)).Trim('.');
                saveName = GetValidFileName(name) + "_" + saveName;
            }
            return saveName;
        }

        /// <summary>
        /// 从 hh:mm:ss 解析TimeSpan
        /// </summary>
        /// <param name="timeStr"></param>
        /// <returns></returns>
        public static TimeSpan ParseDur(string timeStr)
        {
            var arr = timeStr.Replace("：", ":").Split(':');
            var days = -1;
            var hours = -1;
            var mins = -1;
            var secs = -1;
            arr.Reverse().Select(i => Convert.ToInt32(i)).ToList().ForEach(item =>
            {
                if (secs == -1) secs = item;
                else if (mins == -1) mins = item;
                else if (hours == -1) hours = item;
                else if (days == -1) days = item;
            });

            if (days == -1) days = 0;
            if (hours == -1) hours = 0;
            if (mins == -1) mins = 0;
            if (secs == -1) secs = 0;

            return new TimeSpan(days, hours, mins, secs);
        }
    }
}
