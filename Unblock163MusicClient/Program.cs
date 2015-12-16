using FSLib.Network.Http;
using Newtonsoft.Json.Linq;
using System;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Titanium.Web.Proxy;
using Titanium.Web.Proxy.EventArguments;

namespace Unblock163MusicClient
{
    internal class Program
    {
        private static Regex rexPl = new Regex("\"pl\":\\d+", RegexOptions.Compiled);
        private static Regex rexSt = new Regex("\"st\":-?\\d+", RegexOptions.Compiled);

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
        public static void OnResponse(object sender, SessionEventArgs e)
        {
            if (e.ResponseStatusCode == HttpStatusCode.OK)
            {
                if (e.ResponseContentType.Trim().ToLower().Contains("text/plain"))
                {
                    if (e.RequestUrl.Contains("/eapi/v3/song/detail") || e.RequestUrl.Contains("/eapi/v1/album") || e.RequestUrl.Contains("/eapi/v3/playlist/detail"))
                    {
                        string modified = ModifyDetailApi(e.GetResponseBodyAsString());
                        e.SetResponseBodyString(modified);
                    }
                    else if (e.RequestUrl.Contains("/enhance/player/url"))
                    {
                        string modified = ModifyPlayerApi(e.GetResponseBodyAsString());
                        e.SetResponseBodyString(modified);
                    }
                }
            }
        }

        /// <summary>
        /// Hack the result of song / album / playlist API and treat the client to let it work as the song is not disabled.
        /// </summary>
        /// <param name="originalContent">The original API result.</param>
        /// <returns>The modified API result.</returns>
        private static string ModifyDetailApi(string originalContent)
        {
            Console.WriteLine("Hack API detail");

            string modified = originalContent;
            modified = rexPl.Replace(modified, "\"pl\":320000");
            modified = rexSt.Replace(modified, "\"st\":0");
            return modified;
        }

        /// <summary>
        /// Hack the result of player getting song URL and redirects it to the new URL.
        /// </summary>
        /// <param name="originalContent"></param>
        /// <returns></returns>
        private static string ModifyPlayerApi(string originalContent)
        {
            Console.WriteLine("Hack API player/url");

            string modified = originalContent;
            JObject root = JObject.Parse(modified);
            string songId = root["data"][0]["id"].Value<string>();
            string newUrl = NeteaseIdProcess.GetUrl(songId, "hMusic");
            root["data"][0]["url"] = newUrl;
            root["data"][0]["br"] = "320000";
            root["data"][0]["code"] = "200";

            Console.WriteLine(newUrl);
            return root.ToString();
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
            return $"http://m{DateTime.Now.Second % 3 + 1}.music.126.net/{GetEncId(dfsId)}/{dfsId}.mp3";
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

            if (!root["songs"][0]["hMusic"].HasValues)
            {
                Console.WriteLine("Downgrade to medium quality.");
                type = "mMusic";
            }
            if (!root["songs"][0]["mMusic"].HasValues)
            {
                Console.WriteLine("Downgrade to low quality.");
                type = "lMusic";
            }
            if (!root["songs"][0]["lMusic"].HasValues)
            {
                Console.WriteLine("Downgrade to can't be lower quality.");
                type = "bMusic";
            }

            // Don't ask me what to do if there's even no lowest quality...

            return root["songs"][0][type]["dfsId"].Value<string>();
        }
    }
}