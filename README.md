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

1. [NET Core SDK 2.1](https://www.microsoft.com/net/core)
2. At least [Bitcoin Core 0.13.1](https://bitcoin.org/bin/bitcoin-core-0.13.1/) fully sync, rpc enabled.

On Tumbler server side, run Bitcoin Core with a big RPC work queue. TumbleBit has peak of activity making it likely to reach the limit.
```
bitcoind -rpcworkqueue=100
```

Alternatively, you can also put the `rpcworkqueue=100` in the configuration file of your bitcoin instance.

As a developer, you need additionally one of those:

1. [Visual studio 2017 with update 7 (15.7)](https://www.visualstudio.com/downloads/) (Windows only)
2. [Visual studio code](https://code.visualstudio.com/) with [C# Extension](https://marketplace.visualstudio.com/items?itemName=ms-vscode.csharp) (Cross plateform)

If you have any issue, please check the [FAQ](https://github.com/NTumbleBit/NTumbleBit/wiki/FAQ), before posting an issue.

## Project status
The current version has an implementation of:
* Puzzle Solver Algorithm
* Puzzle Promise Algorithm
* TumbleBit: Classic Tumbler Mode

### What is next

1. Tor integration for Tumbler server and client
2. Localhost website as user interface for Tumbler server and Tumbler Client.
3. TumbleBit: Uni-directional Payment Hub Mode

## Developing on Linux or Mac

We recommend that you use [Visual Studio Code](https://code.visualstudio.com/), which is free IDE supporting C# development and testing.

## Developing on Windows

We recommend that you use [Visual studio 2017](https://www.visualstudio.com/downloads/) for building and running the tests.
You can of course use Visual use command line or [Visual Studio Code](https://code.visualstudio.com/) as well.

## Acknowledgements

Thanks to Boston University (Ethan Heilman, Leen AlShenibr, Foteini Baldimtsi, Alessandra Scafuro, and Sharon Goldberg) for inventing the TumbleBit protocol.

Thanks to Omar Sagga and Sharon Goldberg for the crypto review of TumbleBit, and [PoupardStern and PermutationTest proofs](https://github.com/osagga/TumbleBitSetup).

Special thanks to @Yzord (aka @badass.sx on Stratis slack) and the dedicated TumbleBit testers in the Stratis team, which brought TumbleBit to the next level.

Thanks to Stratis to dedicate resources to develop NTumbleBit and integrate it to Breeze wallet.
