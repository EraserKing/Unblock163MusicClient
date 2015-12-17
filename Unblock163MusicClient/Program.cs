using FSLib.Network.Http;
using Newtonsoft.Json.Linq;
using System;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Titanium.Web.Proxy;
using Titanium.Web.Proxy.EventArguments;

namespace Unblock163MusicClient
{
    internal class Program
    {
        private static Regex _rexPl = new Regex("\"pl\":\\d+", RegexOptions.Compiled);
        private static Regex _rexDl = new Regex("\"dl\":\\d+", RegexOptions.Compiled);
        private static Regex _rexFl = new Regex("\"fl\":\\d+", RegexOptions.Compiled);
        private static Regex _rexSt = new Regex("\"st\":-?\\d+", RegexOptions.Compiled);

        private static string _playbackBitrate = "320000";
        private static string _playbackQuality = "hMusic";

        private static string _downloadBitrate = "320000";
        private static string _downloadQuality = "hMusic";

        private static void Main(string[] args)
        {
            int port = 3412;

            ProxyServer.BeforeResponse += OnResponse;

            ProxyServer.ListeningPort = port;
            ProxyServer.SetAsSystemProxy = false;
            ProxyServer.Start();

            Console.WriteLine($"Proxy started, listening at port {port}");
            Console.Read();

            ProxyServer.BeforeResponse -= OnResponse;
            ProxyServer.Stop();
        }

        // When we receive the result, we need to check if it need to be modified.
        private static void OnResponse(object sender, SessionEventArgs e)
        {
            if (e.ResponseStatusCode == HttpStatusCode.OK)
            {
                if (e.ResponseContentType.Trim().ToLower().Contains("text/plain"))
                {
                    if (e.RequestUrl.Contains("/eapi/v3/song/detail/") || e.RequestUrl.Contains("/eapi/v1/album/") || e.RequestUrl.Contains("/eapi/v3/playlist/detail"))
                    {
                        string modified = ModifyDetailApi(e.GetResponseBodyAsString());
                        e.SetResponseBodyString(modified);
                    }
                    else if (e.RequestUrl.Contains("/eapi/song/enhance/player/url"))
                    {
                        SetPlaybackMusicQuality(e.GetResponseBodyAsString());
                        string modified = ModifyPlayerApi(e.GetResponseBodyAsString(), _playbackBitrate, _playbackQuality);
                        e.SetResponseBodyString(modified);
                    }
                    else if (e.RequestUrl.Contains("/eapi/song/download/limit"))
                    {
                        string modified = ModifyDownloadLimitApi();
                        e.SetResponseBodyString(modified);
                    }
                    else if (e.RequestUrl.Contains("/eapi/song/enhance/download/url"))
                    {
                        string modified = e.GetResponseBodyAsString();
                        SetDownloadMusicQuality(modified);
                        modified = ModifyDownloadApi(modified, _downloadBitrate, _downloadQuality);
                        e.SetResponseBodyString(modified);
                    }
                }
            }
        }

        /// <summary>
        /// Set music quality for playback.
        /// </summary>
        /// <param name="apiResult">The API result containing current music quality settings.</param>
        private static void SetPlaybackMusicQuality(string apiResult)
        {
            JObject root = JObject.Parse(apiResult);
            string bitrate = root["data"][0]["br"].Value<string>();
            if (bitrate != _playbackBitrate && bitrate != "0")
            {
                Console.WriteLine($"Playback quality changed to {bitrate}");
                _playbackBitrate = bitrate;
                switch (bitrate)
                {
                    case "96000":
                        _playbackQuality = "bMusic";
                        break;

                    case "128000":
                        _playbackQuality = "lMusic";
                        break;

                    case "192000":
                        _playbackQuality = "bMusic";
                        break;

                    case "320000":
                        _playbackQuality = "hMusic";
                        break;
                }
            }
        }

        /// <summary>
        /// Set music quality for download.
        /// </summary>
        /// <param name="apiResult">The API result containing current music quality settings.</param>
        private static void SetDownloadMusicQuality(string apiResult)
        {
            JObject root = JObject.Parse(apiResult);
            string bitrate = root["data"]["br"].Value<string>();
            if (bitrate != _downloadBitrate && bitrate != "0")
            {
                Console.WriteLine($"Download quality changed to {bitrate}");
                _downloadBitrate = bitrate;
                switch (bitrate)
                {
                    case "96000":
                        _downloadQuality = "bMusic";
                        break;

                    case "128000":
                        _downloadQuality = "lMusic";
                        break;

                    case "192000":
                        _downloadQuality = "bMusic";
                        break;

                    case "320000":
                        _downloadQuality = "hMusic";
                        break;
                }
            }
        }

        /// <summary>
        /// Hack the result of download limit API.
        /// </summary>
        /// <returns>Just return a normal status.</returns>
        private static string ModifyDownloadLimitApi()
        {
            return "{\"overflow\":false,\"code\":200}";
        }

        /// <summary>
        /// Hack the result of download API and redirects it to the new URL.
        /// </summary>
        /// <param name="originalContent">The original API result.</param>
        /// <param name="targetBitrate">Target bitrate.</param>
        /// <param name="targetQuality">Target quality.</param>
        /// <returns>The modified API result.</returns>
        private static string ModifyDownloadApi(string originalContent, string targetBitrate, string targetQuality)
        {
            Console.WriteLine("Hack download API");

            string modified = originalContent;
            JObject root = JObject.Parse(modified);
            string songId = root["data"]["id"].Value<string>();
            string newUrl = NeteaseIdProcess.GetUrl(songId, targetQuality);
            root["data"]["url"] = newUrl;
            root["data"]["br"] = targetBitrate;
            root["data"]["code"] = "200";

            return root.ToString(Formatting.None);
        }

        /// <summary>
        /// Hack the result of song / album / playlist API and treat the client to let it work as the song is not disabled.
        /// </summary>
        /// <param name="originalContent">The original API result.</param>
        /// <returns>The modified API result.</returns>
        private static string ModifyDetailApi(string originalContent)
        {
            Console.WriteLine("Hack detail API");

            string modified = originalContent;
            modified = _rexPl.Replace(modified, $"\"pl\":{_playbackBitrate}");
            modified = _rexDl.Replace(modified, $"\"dl\":{_downloadBitrate}");
            modified = _rexFl.Replace(modified, "\"fl\":320000");
            modified = _rexSt.Replace(modified, "\"st\":0");
            return modified;
        }

        /// <summary>
        /// Hack the result of player getting song URL and redirects it to the new URL.
        /// </summary>
        /// <param name="originalContent">The original API result.</param>
        /// <param name="targetBitrate">Target bitrate.</param>
        /// <param name="targetQuality">Target quality.</param>
        /// <returns>The modified API result.</returns>
        private static string ModifyPlayerApi(string originalContent, string targetBitrate, string targetQuality)
        {
            Console.WriteLine("Hack API player/url");

            string modified = originalContent;
            JObject root = JObject.Parse(modified);
            string songId = root["data"][0]["id"].Value<string>();
            string newUrl = NeteaseIdProcess.GetUrl(songId, targetQuality);
            root["data"][0]["url"] = newUrl;
            root["data"][0]["br"] = targetBitrate;
            root["data"][0]["code"] = "200";

            return root.ToString(Formatting.None);
        }
    }

    public static class Utility
    {
        public static string DownloadPage(HttpMethod method, string url)
        {
            HttpClient client = new HttpClient();
            HttpContext<string> context = client.Create<string>(method, url);
            context.Send();
            return context.Result;
        }
    }

    /// <summary>
    /// Get song url from song id.
    /// It works as this flow: extract song id from original page -> get dfs ID -> call another API to get URL -> replace the original result with the new URL.
    /// </summary>
    public static class NeteaseIdProcess
    {
        /// <summary>
        /// Get song url from original song ID.
        /// </summary>
        /// <param name="songId">Song ID.</param>
        /// <param name="type">Bitrate. Accepts: bMusic, lMusic, mMusic, hMusic.</param>
        /// <returns>Song URL.</returns>
        public static string GetUrl(string songId, string type)
        {
            string dfsId = GetDfsId(Utility.DownloadPage(HttpMethod.Get, $"http://music.163.com/api/song/detail?id={songId}&ids=[{songId}]"), type);
            return GenerateUrl(dfsId);
        }

        /// <summary>
        /// Calculate enc ID from dfs ID.
        /// </summary>
        /// <param name="dfsId">dfs ID.</param>
        /// <returns>enc ID.</returns>
        private static string GetEncId(string dfsId)
        {
            byte[] magicBytes = new ASCIIEncoding().GetBytes("3go8&$8*3*3h0k(2)2");
            byte[] songId = new ASCIIEncoding().GetBytes(dfsId);
            for (int i = 0; i < songId.Length; i++)
            {
                songId[i] = (byte)(songId[i] ^ magicBytes[i % magicBytes.Length]);
            }
            byte[] hash = MD5.Create().ComputeHash(songId);
            return Convert.ToBase64String(hash).Replace('/', '_').Replace('+', '-');
        }

        /// <summary>
        /// Generate final URL with dfsId.
        /// </summary>
        /// <param name="dfsId"></param>
        /// <returns></returns>
        private static string GenerateUrl(string dfsId)
        {
            return $"http://m{DateTime.Now.Second % 2 + 1}.music.126.net/{GetEncId(dfsId)}/{dfsId}.mp3";
        }

        /// <summary>
        /// Extract dfs ID from the original API return value.
        /// </summary>
        /// <param name="pageContent">The original API return value.</param>
        /// <param name="type">Bitrate. Accepts: bMusic, lMusic, mMusic, hMusic.</param>
        /// <returns>dfs ID.</returns>
        private static string GetDfsId(string pageContent, string type)
        {
            JObject root = JObject.Parse(pageContent);

            // Downgrade if we don't have higher quality...

            if (type == "hMusic" && !root["songs"][0]["hMusic"].HasValues)
            {
                Console.WriteLine("Downgrade to medium quality.");
                type = "mMusic";
            }
            if (type == "mMusic" && !root["songs"][0]["mMusic"].HasValues)
            {
                Console.WriteLine("Downgrade to low quality.");
                type = "lMusic";
            }
            if (type == "lMusic" && !root["songs"][0]["lMusic"].HasValues)
            {
                Console.WriteLine("Downgrade to can't be lower quality.");
                type = "bMusic";
            }

            if (type == "bMusic" && !root["songs"][0]["bMusic"].HasValues)
            {
                // Don't ask me what to do if there's even no lowest quality...
            }

            return root["songs"][0][type]["dfsId"].Value<string>();
        }
    }
}