# Third-party dependencies

## ZXing.Net 0.16.11

The QRCodeTracking sample includes the unmodified .NET Standard 2.0 assembly from [ZXing.Net 0.16.11 on NuGet](https://www.nuget.org/packages/ZXing.Net/0.16.11), maintained at [micjahn/ZXing.Net](https://github.com/micjahn/ZXing.Net). Licensed under Apache-2.0; see `Assets/Plugins/ZXing/LICENSE.txt`.

Assembly SHA-256: `f3b823b6fd6492525a7547989056883def5d43be1e12c4f63fa54df73e3c5cfc`.

## WebRTC

The manifest pins Unity WebRTC 3.0.0, SimpleWebRTC 1.6.0 at commit `d3fc983d0c91c150a41f42e6ceb5124baaba5891` (MIT), and NativeWebSocket 1.1.6 at `ea014c9ae534d56111962d96f89f8a046e302dc9` (Apache-2.0). Their licenses remain in their packages. A signaling server and receiving peer are still required.

## Quak

The included `com.xrdevrob.quak-0.4.0-alpha.1.tgz` is the unmodified official release asset, verified against its published SHA256SUMS. Its own `LICENSE.md` applies. It provides development-only device testing; it is not part of the sample's production interaction logic.
