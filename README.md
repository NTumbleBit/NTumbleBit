| Windows | Linux | OS X |
| :---- | :---- | :---- |
[![Windows build status][1]][2] | [![Linux build status][3]][4] | [![OS X build status][5]][6] |

[1]: https://ci.appveyor.com/api/projects/status/vs4sn7saan0n9sx3?svg=true
[2]: https://ci.appveyor.com/project/BreezeHubAdmin/breezeprotocol/branch/master
[3]: https://travis-ci.org/BreezeHub/BreezeProtocol.svg?branch=master
[4]: https://travis-ci.org/BreezeHub/BreezeProtocol
[5]: https://travis-ci.org/BreezeHub/BreezeProtocol.svg?branch=master
[6]: https://travis-ci.org/BreezeHub/BreezeProtocol

# NTumbleBit
TumbleBit implementation in .NET Core.  

## [Check out the in-depth user guide on the Wiki](https://github.com/NTumbleBit/NTumbleBit/wiki)

## Trailer/Motivation
[![IMAGE ALT TEXT HERE](https://img.youtube.com/vi/T2nbxe7gH_4/2.jpg)](https://www.youtube.com/watch?v=T2nbxe7gH_4)

## Resources
Cross-platform library, based on ["TumbleBit: An Untrusted Bitcoin-Compatible Anonymous Payment Hub"](https://eprint.iacr.org/2016/575). 
Another proof of concept implementation can be found in [the old repository of TumbleBit](https://github.com/BUSEC/TumbleBit).  
An "easy" to understand explanation of the protocol has been presented by Ethan Heilman and Leen Al Shenibr at [Scaling Bitcoin Milan](https://www.youtube.com/watch?v=iGVSnxz1mn8), and on NTumbleBit implementation by Nicolas Dorier at [Blockchain Core Camp Tokyo](https://player.vimeo.com/video/215151763).

## Requirements

As a user, you will need:

1. [NET Core SDK 1.0.4](https://github.com/dotnet/core/blob/master/release-notes/download-archives/1.0.4-sdk-download.md) (see below)
2. [Bitcoin Core 0.13.1](https://bitcoin.org/bin/bitcoin-core-0.13.1/) fully sync, rpc enabled.

On Tumbler server side, run Bitcoin Core with a big RPC work queue. TumbleBit has peak of activity making it likely to reach the limit.
```
bitcoind -rpcworkqueue=100
```

Alternatively, you can also put the `rpcworkqueue=100` in the configuration file of your bitcoin instance.

You can easily install the SDK on ubuntu systems after having installed the runtime by running
```
sudo apt-get install dotnet-dev-1.0.4
```
You can known more about install on your system on [this link](https://www.microsoft.com/net/core).
Using Bitcoin Core with later version should work as well.

As a developer, you need additionally one of those:

1. [Visual studio 2017](https://www.visualstudio.com/downloads/) (Windows only)
2. [Visual studio code](https://code.visualstudio.com/) with [C# Extension](https://marketplace.visualstudio.com/items?itemName=ms-vscode.csharp) (Cross plateform)

If you have any issue, please check the [FAQ](https://github.com/NTumbleBit/NTumbleBit/wiki/FAQ), before posting an issue.

## Project status
The current version has an implementation of:
* Puzzle Solver Algorithm
* Puzzle Promise Algorithm
* TumbleBit: Classic Tumbler Mode

### What is next

1. TOR integration for Tumbler server and client
2. Localhost website as user interface for Tumbler server and Tumbler Client.
3. TumbleBit: Uni-directional Paymen Hub Mode

## Developing on Linux or Mac

We recommend that you use [Visual Studio Code](https://code.visualstudio.com/), which is free IDE supporting C# development and testing.

## Developing on Windows

We recommend that you use [Visual studio 2017](https://www.visualstudio.com/downloads/) for building and running the tests.
You can of course use Visual use command line or [Visual Studio Code](https://code.visualstudio.com/) as well.

## Acknowledgements
Special thanks to Ethan Heilman and Leen AlShenibr for their work, their research and proof of work made this project possible.
