# Unblock163MusicClient
Unblock 163 Cloud Music Windows client.
Acts as a proxy.
Didn't do much about this, but just to prove it works!

## Usage
Run and it listens at port 3412.
Open Windows client, set proxy to IP 127.0.0.1, port 3412.
(It reports errors if you click "Test" and this is okay).
Save and restart the client.
Enjoy!

## Building
Need the following packages:
Titanium.Web.Proxy 1.0.0.88 [https://github.com/titanium007/Titanium-Web-Proxy]
Newtonsoft.Json 6.0.8 [https://github.com/JamesNK/Newtonsoft.Json]
network.fishlee.net 1.5.6 [https://www.nuget.org/packages/network.fishlee.net/]

Under Visual Studio 2015

## Thanks
Thanks yanunon for his API analysis! [https://github.com/yanunon/NeteaseCloudMusic]