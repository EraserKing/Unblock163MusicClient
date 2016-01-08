using FSLib.Network.Http;
using Newtonsoft.Json.Linq;
using System;
using System.Net;
using System.Windows.Forms;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Titanium.Web.Proxy;
using Titanium.Web.Proxy.EventArguments;
using Titanium.Web.Proxy.Models;

namespace Unblock163MusicClient
{
    internal static class Configuration
    {
        public static int Port = 3412;

        public static string ForcePlaybackBitrate { get; private set; } = string.Empty;
        public static string PlaybackBitrate { get; set; } = "320000";
        public static string PlaybackQuality { get; set; } = "hMusic";

        public static string ForceDownloadBitrate { get; private set; } = string.Empty;
        public static string DownloadBitrate { get; set; } = "320000";
        public static string DownloadQuality { get; set; } = "hMusic";

        public static bool Overseas { get; private set; } = false;
        public static bool Verbose { get; private set; } = false;

        /// <summary>
        /// Set configuration according to the arguments passed in.
        /// </summary>
        /// <param name="args">Command line arguments.</param>
        public static void SetStartupParameters(string[] args)
        {
            for (int i = 0; i < args.Length;)
            {
                string argKey = args[i];
                if (argKey == "/port")
                {
                    if (i != args.Length - 1 && !args[i + 1].StartsWith("/"))
                    {
                        try
                        {
                            Port = int.Parse(args[i + 1]);
                            i += 2;
                            if (Port < 1 || Port > 65535)
                            {
                                throw new ArgumentException("Invalid port number.");
                            }
                        }
                        catch (FormatException)
                        {

                            throw new ArgumentException("Invalid port number.");
                        }

                    }
                    else
                    {
                        throw new ArgumentException("No port number specified.");
                    }
                }
                else if (argKey == "/overseas")
                {
                    Console.WriteLine("Overseas mode is turned on.");
                    Overseas = true;
                    i++;
                }
                else if (argKey == "/verbose")
                {
                    Console.WriteLine("Verbose output is turned on.");
                    Verbose = true;
                    i++;
                }
                else if (argKey == "/playbackbitrate")
                {
                    if (i != args.Length - 1 && !args[i + 1].StartsWith("/"))
                    {
                        ForcePlaybackBitrate = args[i + 1];
                        Console.WriteLine($"Playback bitrate is forced to {ForcePlaybackBitrate}");
                        i += 2;
                    }
                    else
                    {
                        throw new ArgumentException("No playback bitrate specified.");
                    }
                    if (ForcePlaybackBitrate != "" && ForcePlaybackBitrate != "96000" && ForcePlaybackBitrate != "128000" &&
                        ForcePlaybackBitrate != "192000" && ForcePlaybackBitrate != "320000")
                    {
                        throw new ArgumentException("Unrecognized playback bitrate.");
                    }
                }
                else if (argKey == "/downloadbitrate")
                {
                    if (i != args.Length - 1 && !args[i + 1].StartsWith("/"))
                    {
                        ForceDownloadBitrate = args[i + 1];
                        Console.WriteLine($"Download bitrate is forced to {ForceDownloadBitrate}");
                        i += 2;
                    }
                    else
                    {
                        throw new ArgumentException("No download bitrate specified.");
                    }
                    if (ForceDownloadBitrate != "" && ForceDownloadBitrate != "96000" && ForceDownloadBitrate != "128000" &&
                        ForceDownloadBitrate != "192000" && ForceDownloadBitrate != "320000")
                    {
                        throw new ArgumentException("Unrecognized download bitrate.");
                    }
                }
                else
                {
                    throw new ArgumentException("Unrecognized startup parameter.");
                }
            }
        }
    }

    internal static class Program
    {
        private static readonly Regex RexPl = new Regex("\"pl\":\\d+", RegexOptions.Compiled);
        private static readonly Regex RexDl = new Regex("\"dl\":\\d+", RegexOptions.Compiled);
        private static readonly Regex RexSt = new Regex("\"st\":-?\\d+", RegexOptions.Compiled);
        private static readonly Regex RexSubp = new Regex("\"subp\":\\d+", RegexOptions.Compiled);

        private static void Main(string[] args)
        {
            try
            {
                Configuration.SetStartupParameters(args);
            }
            catch (ArgumentException ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Environment.Exit(1);
            }

            ProxyServer.BeforeResponse += OnResponse;

            ProxyServer.ListeningPort = Configuration.Port;
            ProxyServer.SetAsSystemProxy = false;
            ProxyServer.Start();

            Console.WriteLine($"Proxy started, listening at port {Configuration.Port}");
            Console.Read();

            ProxyServer.BeforeResponse -= OnResponse;
            ProxyServer.Stop();
        }

        // When we receive the result, we need to check if it needs to be modified.
        private static void OnResponse(object sender, SessionEventArgs e)
        {
            if (e.ResponseStatusCode == HttpStatusCode.OK)
            {
                // Most APIs are returned in text/plain but serach songs page is returned in JSON. Don't forget this!
                if (e.ResponseContentType.Trim().ToLower().Contains("text/plain") ||
                    e.ResponseContentType.Trim().ToLower().Contains("application/json"))
                {
                    if (Configuration.Verbose)
                    {
                        Console.WriteLine($"Accessing URL {e.RequestUrl}");
                    }
                    // It should include album / playlist / artist / search pages.
                    if (e.RequestUrl.Contains("/eapi/v3/song/detail/") || e.RequestUrl.Contains("/eapi/v1/album/") || e.RequestUrl.Contains("/eapi/v3/playlist/detail") ||
                        e.RequestUrl.Contains("/eapi/batch") || e.RequestUrl.Contains("/eapi/cloudsearch/pc") || e.RequestUrl.Contains("/eapi/v1/artist") ||
                        e.RequestUrl.Contains("/eapi/v1/search/get"))
                    {
                        string modified = ModifyDetailApi(e.GetResponseBodyAsString());
                        e.SetResponseBodyString(modified);
                    }
                    // This is called when player tries to get the URL for a song.
                    else if (e.RequestUrl.Contains("/eapi/song/enhance/player/url"))
                    {
                        string bitrate = GetPlaybackMusicQuality(e.GetResponseBodyAsString());
                        // Whatever current playback bitrate is, it's overriden.
                        if (!string.IsNullOrEmpty(Configuration.ForcePlaybackBitrate))
                        {
                            bitrate = Configuration.ForcePlaybackBitrate;
                            Console.WriteLine($"Plackback bitrate is forced set to {bitrate}");
                        }
                        // We receive a wrong bitrate...
                        else if (bitrate == "0")
                        {
                            bitrate = string.IsNullOrEmpty(Configuration.ForcePlaybackBitrate) ? "320000": Configuration.ForcePlaybackBitrate;
                            Console.WriteLine($"Plackback bitrate is forced set to {bitrate} as the given bitrate is not valid.");
                        }
                        else if (bitrate != Configuration.PlaybackBitrate)
                        {
                            Console.WriteLine($"Plackback bitrate is switched to {bitrate} from {Configuration.PlaybackBitrate}");
                        }
                        Configuration.PlaybackBitrate = bitrate;
                        Configuration.PlaybackQuality = ParseBitrate(Configuration.ForcePlaybackBitrate);

                        string modified = ModifyPlayerApi(e.GetResponseBodyAsString());
                        e.SetResponseBodyString(modified);
                    }
                    // When we try to download a song, the API tells whether it exceeds the limit. Of course no!
                    else if (e.RequestUrl.Contains("/eapi/song/download/limit"))
                    {
                        string modified = ModifyDownloadLimitApi();
                        e.SetResponseBodyString(modified);
                    }
                    // Similar to the player URL API, but used for download.
                    else if (e.RequestUrl.Contains("/eapi/song/enhance/download/url"))
                    {
                        string bitrate = GetDownloadMusicQuality(e.GetResponseBodyAsString());

                        // Whatever current download bitrate is, it's overriden.
                        if (!string.IsNullOrEmpty(Configuration.ForceDownloadBitrate))
                        {
                            bitrate = Configuration.ForceDownloadBitrate;
                            Console.WriteLine($"Download bitrate is forced set to {bitrate}");
                        }
                        // We receive a wrong bitrate...
                        else if (bitrate == "0")
                        {
                            bitrate = string.IsNullOrEmpty(Configuration.ForceDownloadBitrate) ? "320000" : Configuration.ForceDownloadBitrate;
                            Console.WriteLine($"Download bitrate is forced set to {bitrate} as the given bitrate is not valid.");
                        }
                        else if (bitrate != Configuration.DownloadBitrate)
                        {
                            Console.WriteLine($"Download bitrate is switched to {bitrate} from {Configuration.DownloadBitrate}");
                        }
                        Configuration.DownloadBitrate = bitrate;
                        Configuration.DownloadQuality = ParseBitrate(bitrate);

                        string modified = ModifyDownloadApi(e.GetResponseBodyAsString());
                        e.SetResponseBodyString(modified);
                    }
                }
            }
        }

        private static string GetPlaybackMusicQuality(string apiResult)
        {
            JObject root = JObject.Parse(apiResult);
            string bitrate = root["data"][0]["br"].Value<string>();
            return bitrate;
        }

        private static string GetDownloadMusicQuality(string apiResult)
        {
            JObject root = JObject.Parse(apiResult);
            string bitrate = root["data"]["br"].Value<string>();
            return bitrate;
        }

        /// <summary>
        /// Set music quality for playback.
        /// </summary>
        /// <param name="apiResult">The API result containing current music quality settings.</param>
        private static void SetPlaybackMusicQuality(string apiResult, string bitrateToSet)
        {
            JObject root = JObject.Parse(apiResult);
            string bitrate = root["data"][0]["br"].Value<string>();
            if (bitrate != Configuration.PlaybackBitrate && bitrate != "0")
            {
                if (Configuration.Verbose)
                {
                    Console.WriteLine($"Setting playback music quality {bitrate}");
                }
                Console.WriteLine($"Playback quality changed to {bitrate}");
                Configuration.PlaybackBitrate = bitrate;
                Configuration.PlaybackQuality = ParseBitrate(bitrate);
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
            if (bitrate != Configuration.DownloadBitrate && bitrate != "0")
            {
                if (Configuration.Verbose)
                {
                    Console.WriteLine($"Setting download music quality {bitrate}");
                }
                Console.WriteLine($"Download quality changed to {bitrate}");
                Configuration.DownloadBitrate = bitrate;
                Configuration.DownloadQuality = ParseBitrate(bitrate);
            }
        }

        /// <summary>
        /// Get quality string from bitrate. Default to HQ.
        /// </summary>
        /// <param name="bitrate">Bitrate.</param>
        /// <returns>Quality.</returns>
        private static string ParseBitrate(string bitrate)
        {
            switch (bitrate)
            {
                case "320000":
                    return "hMusic";

                case "192000":
                    return "mMusic";

                case "128000":
                    return "lMusic";

                case "96000":
                    return "bMusic";
            }
            return "hMusic";
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
        /// <returns>The modified API result.</returns>
        private static string ModifyDownloadApi(string originalContent)
        {
            Console.WriteLine("Hack download API");

            string modified = originalContent;
            JObject root = JObject.Parse(modified);
            string songId = root["data"]["id"].Value<string>();
            string newUrl = NeteaseIdProcess.GetUrl(songId, Configuration.DownloadQuality);
            root["data"]["url"] = newUrl;
            root["data"]["br"] = Configuration.DownloadBitrate;
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
            //Playback bitrate
            modified = RexPl.Replace(modified, $"\"pl\":{Configuration.PlaybackBitrate}");

            //Download bitrate
            modified = RexDl.Replace(modified, $"\"dl\":{Configuration.DownloadBitrate}");

            //Disabled
            modified = RexSt.Replace(modified, "\"st\":0");

            //Can favorite
            modified = RexSubp.Replace(modified, "\"subp\":1");
            return modified;
        }

        /// <summary>
        /// Hack the result of player getting song URL and redirects it to the new URL.
        /// </summary>
        /// <param name="originalContent">The original API result.</param>
        /// <returns>The modified API result.</returns>
        private static string ModifyPlayerApi(string originalContent)
        {
            Console.WriteLine("Hack player API");

            string modified = originalContent;
            JObject root = JObject.Parse(modified);
            string songId = root["data"][0]["id"].Value<string>();
            string newUrl = NeteaseIdProcess.GetUrl(songId, Configuration.PlaybackQuality);
            root["data"][0]["url"] = newUrl;
            root["data"][0]["br"] = Configuration.PlaybackBitrate;
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
            if (Configuration.Verbose)
            {
                Console.WriteLine($"Song ID = {songId}, DFS ID = {dfsId}");
            }
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
            string url = $"http://m{DateTime.Now.Second % 2 + 1}.music.126.net/{GetEncId(dfsId)}/{dfsId}.mp3";
            if (Configuration.Overseas)
            {
                url = url.Replace("http://m", "http://p");
            }
            if (Configuration.Verbose)
            {
                Console.WriteLine($"Song URL = {url}");
            }
            return url;
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
                Console.WriteLine("No resource available.");
            }

            return root["songs"][0][type]["dfsId"].Value<string>();
        }
    }
}