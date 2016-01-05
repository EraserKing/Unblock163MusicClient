# Unblock163MusicClient

Unblock 163 Cloud Music Windows client.

Acts as a proxy.

Didn't do much about this, but just to prove it works!

When you access song / album / artist / playlist page, or search result, you should find the disabled songs return to enabled.

## Usage

1. Run and it listens at port 3412 (default).
   
2. Open Windows client, set proxy to IP 127.0.0.1, port 3412.
   
3. It reports errors if you click "Test" and this is okay. *Of course, you could skip clicking that button and it's fine :)* 
   
4. Save and restart the client.
   
5. Enjoy!
   
   Command line arguments

## Command line arugments

`/port` : specify the port it listens at. **Example**: `/port 3456`

`/verbose`: turn on verbose log.

`/overseas`: turn on overseas mode (not tested yet). *Not recommended for Mainland China users.*

`/playbackbitrate`: override the playback bitrate specified in the client. **Accepted values**: `96000`, `128000`, `192000`, `320000`. **Example**: `/playbackbitrate 320000`

`/downloadbitrate`: override the download bitrate specified in the client. **Accepted values**: `96000`, `128000`, `192000`, `320000`. **Example**: `/downloadbitrate 320000`

## Download

See [https://github.com/EraserKing/Unblock163MusicClient/releases]

## Known issues

1. Download is not working (The reason seems simple - the proxy component is not functioning for downloading file). However you can add songs to the download queue, disable proxy settings in app and then resume. Everything goes smoothly then. Certainly, you need to set proxy back after having all songs downloaded!
2. The settings of music quality won't take effect immediately after you change them; instead it will only be switched properly after it meets a normal song (not disabled). *You could use command line arguments to override this!*

## Open issues

Please report the following information:

* Operating system
* Song / Album / Playlist name (how I can locate to that song)
* Whether the issue happens on specific songs, or all songs
* Whether it can be reproduced on web client for the same song

## Building

Need the following packages:

Titanium.Web.Proxy 1.0.0.88 [https://github.com/titanium007/Titanium-Web-Proxy]

Newtonsoft.Json 6.0.8 [https://github.com/JamesNK/Newtonsoft.Json]

network.fishlee.net 1.5.6 [https://www.nuget.org/packages/network.fishlee.net/]

Under Visual Studio 2015.

## Thanks

Thanks yanunon for his API analysis! [https://github.com/yanunon/NeteaseCloudMusic]