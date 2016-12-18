# NTumbleBit
.NET implementation of TumbleBit primitives.
Please check out our [wiki](https://github.com/NTumbleBit/NTumbleBit/wiki) for more details.

##Resources
Cross-platform library, based on ["TumbleBit: An Untrusted Bitcoin-Compatible Anonymous Payment Hub"](https://eprint.iacr.org/2016/575). 
Another implementation can be found on [the official repository of TumbleBit](https://github.com/BUSEC/TumbleBit). 
An "easy" to understand explanation of the protocol has been presented by Ethan Heilman and Leen Al Shenibr at [Scaling Bitcoin Milan](https://www.youtube.com/watch?v=iGVSnxz1mn8).

##Requirements
This implementation is compatible with .NETCore 1.1 (as of today counter intuitively named dotnet-dev-1.0.0-preview2.1-003177) and works on [Windows](https://www.microsoft.com/net/core#windowsvs2015), [Linux](https://www.microsoft.com/net/core#linuxredhat), [Mac](https://www.microsoft.com/net/core#macos) or [Docker](https://www.microsoft.com/net/core#dockercmd).

You need to install dotnet core on your system as instructed on [.NET Core installation guide](https://www.microsoft.com/net/core).

You will also need a synchronized Bitcoin Full node (pruned nodes also work), with the RPC server running.

##Build and run

Check out on the [wiki](https://github.com/NTumbleBit/NTumbleBit/wiki/How-to-Run).

##Project status
The current version has an implementation of:
* Puzzle Solver algorithm
* Puzzle Promise algorithm
* Tumbler mode server

### What is next

1. TOR integration for Tumbler server and client
2. Localhost website as user interface for Tumbler server and Tumbler Client.
3. Payment Hub

## Developing on Linux or Mac

We recommend that you use [Visual Studio Code](https://code.visualstudio.com/), which is free IDE supporting C# development and testing.

## Developing on Windows

We recommend that you use [Visual Studio 2015 Update 3 (free)](https://www.visualstudio.com/vs/community/) for building and running the tests.
You can of course use Visual use command line or [Visual Studio Code](https://code.visualstudio.com/) as well.

##Acknowledgements
Special thanks to Ethan Heilman and Leen AlShenibr for their work, their research and proof of work made this project possible.
