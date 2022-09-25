﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Cache;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using N_m3u8DL_RE.Common.Log;
using N_m3u8DL_RE.Common.Resource;

namespace N_m3u8DL_RE.Common.Util
{
    public class HTTPUtil
    {
        public static readonly HttpClientHandler HttpClientHandler = new()
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.All,
            ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
        };

        public static readonly HttpClient AppHttpClient = new(HttpClientHandler)
        {
            Timeout = TimeSpan.FromSeconds(100)
        };

        private static async Task<HttpResponseMessage> DoGetAsync(string url, Dictionary<string, string>? headers = null)
        {
            Logger.Debug(ResString.fetch + url); 
            using var webRequest = new HttpRequestMessage(HttpMethod.Get, url);
            webRequest.Headers.TryAddWithoutValidation("Accept-Encoding", "gzip, deflate");
            webRequest.Headers.CacheControl = CacheControlHeaderValue.Parse("no-cache");
            webRequest.Headers.Connection.Clear();
            if (headers != null)
            {
                foreach (var item in headers)
                {
                    webRequest.Headers.TryAddWithoutValidation(item.Key, item.Value);
                }
            }
            Logger.Debug(webRequest.Headers.ToString());
            //手动处理跳转，以免自定义Headers丢失
            var webResponse = await AppHttpClient.SendAsync(webRequest, HttpCompletionOption.ResponseHeadersRead);
            if (webResponse.StatusCode == HttpStatusCode.Found || webResponse.StatusCode == HttpStatusCode.Moved)
            {
                HttpResponseHeaders respHeaders = webResponse.Headers;
                Logger.Debug(respHeaders.ToString());
                if (respHeaders != null && respHeaders.Location != null)
                {
                    var redirectedUrl = "";
                    if (!respHeaders.Location.IsAbsoluteUri)
                    {
                        Uri uri1 = new Uri(url);
                        Uri uri2 = new Uri(uri1, respHeaders.Location);
                        redirectedUrl = uri2.ToString();
                    }
                    else
                    {
                        redirectedUrl = respHeaders.Location.AbsoluteUri;
                    }
                    
                    if (redirectedUrl != url)
                    {
                        return await DoGetAsync(redirectedUrl, headers);
                    }
                }
            }
            //手动将跳转后的URL设置进去, 用于后续取用
            webResponse.Headers.Location = new Uri(url);
            webResponse.EnsureSuccessStatusCode();
            return webResponse;
        }

        public static async Task<byte[]> GetBytesAsync(string url, Dictionary<string, string>? headers = null)
        {
            byte[] bytes = new byte[0];
            var webResponse = await DoGetAsync(url, headers);
            bytes = await webResponse.Content.ReadAsByteArrayAsync();
            Logger.Debug(HexUtil.BytesToHex(bytes, " "));
            return bytes;
        }

        /// <summary>
        /// 获取网页源码
        /// </summary>
        /// <param name="url"></param>
        /// <param name="headers"></param>
        /// <returns></returns>
        public static async Task<string> GetWebSourceAsync(string url, Dictionary<string, string>? headers = null)
        {
            string htmlCode = string.Empty;
            var webResponse = await DoGetAsync(url, headers);
            htmlCode = await webResponse.Content.ReadAsStringAsync();
            Logger.Debug(htmlCode);
            return htmlCode;
        }

        /// <summary>
        /// 获取网页源码和跳转后的URL
        /// </summary>
        /// <param name="url"></param>
        /// <param name="headers"></param>
        /// <returns>(Source Code, RedirectedUrl)</returns>
        public static async Task<(string, string)> GetWebSourceAndNewUrlAsync(string url, Dictionary<string, string>? headers = null)
        {
            string htmlCode = string.Empty;
            var webResponse = await DoGetAsync(url, headers);
            htmlCode = await webResponse.Content.ReadAsStringAsync();
            Logger.Debug(htmlCode);
            return (htmlCode, webResponse.Headers.Location != null ? webResponse.Headers.Location.AbsoluteUri : url);
        }

        public static async Task<string> GetPostResponseAsync(string Url, byte[] postData)
        {
            string htmlCode = string.Empty;
            using HttpRequestMessage request = new(HttpMethod.Post, Url);
            request.Headers.TryAddWithoutValidation("Content-Type", "application/json");
            request.Headers.TryAddWithoutValidation("Content-Length", postData.Length.ToString());
            request.Content = new ByteArrayContent(postData);
            var webResponse = await AppHttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            htmlCode = await webResponse.Content.ReadAsStringAsync();
            return htmlCode;
        }
    }
}
