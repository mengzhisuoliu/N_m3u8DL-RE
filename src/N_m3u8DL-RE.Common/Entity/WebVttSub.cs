﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace N_m3u8DL_RE.Common.Entity
{
    public partial class WebVttSub
    {
        [GeneratedRegex("X-TIMESTAMP-MAP.*")]
        private static partial Regex TSMapRegex();
        [GeneratedRegex("MPEGTS:(\\d+)")]
        private static partial Regex TSValueRegex();
        [GeneratedRegex("\\s")]
        private static partial Regex SplitRegex();

        public List<SubCue> Cues { get; set; } = new List<SubCue>();
        public long MpegtsTimestamp { get; set; } = 0L;

        /// <summary>
        /// 从字节数组解析WEBVTT
        /// </summary>
        /// <param name="textBytes"></param>
        /// <returns></returns>
        public static WebVttSub Parse(byte[] textBytes)
        {
            return Parse(Encoding.UTF8.GetString(textBytes));
        }

        /// <summary>
        /// 从字节数组解析WEBVTT
        /// </summary>
        /// <param name="textBytes"></param>
        /// <param name="encoding"></param>
        /// <returns></returns>
        public static WebVttSub Parse(byte[] textBytes, Encoding encoding)
        {
            return Parse(encoding.GetString(textBytes));
        }

        /// <summary>
        /// 从字符串解析WEBVTT
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public static WebVttSub Parse(string text)
        {
            if (!text.Trim().StartsWith("WEBVTT"))
                throw new Exception("Bad vtt!");


            var webSub = new WebVttSub();
            var needPayload = false;
            var timeLine = "";
            var regex1 = TSMapRegex();

            if (regex1.IsMatch(text))
            {
                var timestamp = TSValueRegex().Match(regex1.Match(text).Value).Groups[1].Value;
                webSub.MpegtsTimestamp = Convert.ToInt64(timestamp);
            }

            var payloads = new List<string>();
            foreach (var line in text.Split('\n'))
            {
                if (line.Contains(" --> "))
                {
                    needPayload = true;
                    timeLine = line.Trim();
                    continue;
                }

                if (needPayload)
                {
                    if (string.IsNullOrEmpty(line.Trim()))
                    {
                        var payload = string.Join(Environment.NewLine, payloads);
                        var arr = SplitRegex().Split(timeLine.Replace("-->", "")).Where(s => !string.IsNullOrEmpty(s)).ToList();
                        var startTime = ConvertToTS(arr[0]);
                        var endTime = ConvertToTS(arr[1]);
                        var style = arr.Count > 2 ? string.Join(" ", arr.Skip(2)) : "";
                        webSub.Cues.Add(new SubCue()
                        {
                            StartTime = startTime,
                            EndTime = endTime,
                            Payload = string.Join("", payload.Where(c => c != 8203)), //Remove Zero Width Space!
                            Settings = style
                        });
                        payloads.Clear();
                        needPayload = false;
                    }
                    else
                    {
                        payloads.Add(line.Trim());
                    }
                }
            }

            return webSub;
        }

        /// <summary>
        /// 从另一个字幕中获取所有Cue，并加载此字幕中，且自动修正偏移
        /// </summary>
        /// <param name="webSub"></param>
        /// <returns></returns>
        public WebVttSub AddCuesFromOne(WebVttSub webSub)
        {
            FixTimestamp(webSub, this.MpegtsTimestamp);
            foreach (var item in webSub.Cues)
            {
                if (!this.Cues.Contains(item))
                {
                    //如果相差只有1ms，且payload相同，则拼接
                    var last = this.Cues.LastOrDefault();
                    if (last != null && this.Cues.Count > 0 && (item.StartTime - last.EndTime).TotalMilliseconds <= 1 && item.Payload == last.Payload) 
                    {
                        last.EndTime = item.EndTime;
                    }
                    else
                    {
                        this.Cues.Add(item);
                    }
                }
            }
            return this;
        }

        private void FixTimestamp(WebVttSub sub, long baseTimestamp)
        {
            if (sub.MpegtsTimestamp == 0)
            {
                return;
            }

            //确实存在时间轴错误的情况，才修复
            if ((this.Cues.Count > 0 && sub.Cues.Count > 0 && sub.Cues.First().StartTime < this.Cues.Last().EndTime) || this.Cues.Count == 0)
            {
                //The MPEG2 transport stream clocks (PCR, PTS, DTS) all have units of 1/90000 second
                var seconds = (sub.MpegtsTimestamp - baseTimestamp) / 90000;
                for (int i = 0; i < sub.Cues.Count; i++)
                {
                    sub.Cues[i].StartTime += TimeSpan.FromSeconds(seconds);
                    sub.Cues[i].EndTime += TimeSpan.FromSeconds(seconds);
                }
            }
        }

        private static TimeSpan ConvertToTS(string str)
        {
            var ms = Convert.ToInt32(str.Split('.').Last());
            var o = str.Split('.').First();
            var t = o.Split(':').Reverse().ToList();
            var time = 0L + ms;
            for (int i = 0; i < t.Count(); i++)
            {
                time += (long)Math.Pow(60, i) * Convert.ToInt32(t[i]) * 1000;
            }
            return TimeSpan.FromMilliseconds(time);
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            foreach (var c in this.Cues)
            {
                sb.AppendLine(c.StartTime.ToString(@"hh\:mm\:ss\.fff") + " --> " + c.EndTime.ToString(@"hh\:mm\:ss\.fff") + " " + c.Settings);
                sb.AppendLine(c.Payload);
                sb.AppendLine();
            }
            sb.AppendLine();
            return sb.ToString();
        }

        public string ToStringWithHeader()
        {
            return "WEBVTT" + Environment.NewLine + Environment.NewLine + ToString();
        }
    }
}
