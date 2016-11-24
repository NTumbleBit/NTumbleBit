# NTumbleBit
Implementation of Tumblebit primitives.

.NET library based on ["TumbleBit: An Untrusted Bitcoin-Compatible Anonymous Payment Hub"](https://eprint.iacr.org/2016/575).
Another implementation can be found on [TumbleBit official repository](https://github.com/BUSEC/TumbleBit).

This implementation is compatible with .NETCore 1.1 (as of today counter intuitively named dotnet-dev-1.0.0-preview2.1-003177) and works on [Windows](https://www.microsoft.com/net/core#windowsvs2015), [Linux](https://www.microsoft.com/net/core#linuxredhat), [Mac](https://www.microsoft.com/net/core#macos) or [Docker](https://www.microsoft.com/net/core#dockercmd).

You need to install dotnet core on your system as instructed on [.NET Core installation guide](https://www.microsoft.com/net/core).

You can then build NTumbleBit for your system with:

```
cd NTumbleBit
dotnet restore
dotnet build
```
You can run the tests with:
```
cd NTumbleBit.Tests
dotnet restore
dotnet build
dotnet test
```

The current version has an implementation of the Puzzle Solver algorithm, with a serializer of all the data structures exchanges between the client and server.
The promise protocol is still a work in progress.

NTumbleBit depends on NBitcoin and BouncyCastle. BouncyCastle is compiled inside NTumbleBit.

Special thanks to Ethan Heilman and Leen Al Shenibr for their work, their research and proof of work made this project possible.

# What is next

1. Puzzle Promise Protocol Implementation
2. Nuget Package
3. TumbleBit Server hostable by ASP.NET Core
