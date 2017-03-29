# NTumbleBit
TumbleBit implementation in .NET Core.  

##[Check out the in-depth user guide on the Wiki](https://github.com/NTumbleBit/NTumbleBit/wiki)

##Trailer/Motivation
[![IMAGE ALT TEXT HERE](https://img.youtube.com/vi/T2nbxe7gH_4/2.jpg)](https://www.youtube.com/watch?v=T2nbxe7gH_4)

##Resources
Cross-platform library, based on ["TumbleBit: An Untrusted Bitcoin-Compatible Anonymous Payment Hub"](https://eprint.iacr.org/2016/575). 
Another implementation can be found on [the official repository of TumbleBit](https://github.com/BUSEC/TumbleBit). 
An "easy" to understand explanation of the protocol has been presented by Ethan Heilman and Leen Al Shenibr at [Scaling Bitcoin Milan](https://www.youtube.com/watch?v=iGVSnxz1mn8).

## Requirements

As a user, you will need:

1. [NET Core Runtime 1.1.1](https://github.com/dotnet/core/blob/master/release-notes/download-archives/1.1.1-download.md)
2. [NET Core SDK 1.1.0 Preview 2.1](https://github.com/dotnet/core/blob/master/release-notes/download-archives/1.1-preview2.1-download.md) (see below)
3. [Bitcoin Core 0.13.1](https://bitcoin.org/bin/bitcoin-core-0.13.1/) fully sync, rpc enabled.

You can easily install the SDK on ubuntu systems after having installed the runtime by running
```
sudo apt-get install dotnet-dev-1.0.0-preview2.1-003177
```
Using Bitcoin Core with later version should work as well.

As a developer, you need additionally one of those:

1. [Visual studio 2015 Update 3](https://go.microsoft.com/fwlink/?LinkId=691129) (Windows only)
2. [Visual studio code](https://code.visualstudio.com/) with [C# Extension](https://marketplace.visualstudio.com/items?itemName=ms-vscode.csharp) (Cross plateform)

If you have any issue, please check the [FAQ](https://github.com/NTumbleBit/NTumbleBit/wiki/FAQ), before posting an issue.

##Project status
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

We recommend that you use [Visual Studio 2015 Update 3 (free)](https://www.visualstudio.com/vs/community/) for building and running the tests.
You can of course use Visual use command line or [Visual Studio Code](https://code.visualstudio.com/) as well.

##Acknowledgements
Special thanks to Ethan Heilman and Leen AlShenibr for their work, their research and proof of work made this project possible.
