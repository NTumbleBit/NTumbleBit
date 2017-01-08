# Server Configuration

The server can be configured via the command line or via the configuration file.

##  Command line

**-testnet**
 runs the server on the testnet if specified
 Example : 
```
dotnet run -testnet 
```

**-port**
the port on which the server will listen 
Default : 5000
Example : 
```
dotnet run -port=5005
```

**-listen **
the address on which the server will listen
Default : 0.0.0.0
Example : 
```
dotnet run -listen=0.0.0.0
dotnet run -listen=localhost
```
**-conf**
The configuration file name. This MUST be used with -datadir 
Default : server.conf
Example : 
```
dotnet run -conf=MyServer.conf -datadir=/home/tumble/.mydatadir/
```

**-datadir**
The data directory, This MUST be used with -conf

Default : server.conf
Example : 
```
dotnet run -conf=MyServer.conf -datadir=/home/tumble/.mydatadir/
```






# Configuration file

by Default it's location will be prompted to you when running the server for the first time.

if not overridden by **-conf** and **-datadir** this file will be used.

the symbol **#** at the beginning of a file defines a comment.

by Default it should look like this (the port may change if running on Main or Testnet) : 
```
#rpc.url=http://localhost:18332/
#rpc.user=bitcoinuser
#rpc.password=bitcoinpassword
#rpc.cookiefile=yourbitcoinfolder/.cookie
```

### RPC-BITCOIN : 

Configuration to connect to the rpc interface of the bitcoin core wallet used for the server.

As every bitcoin configuration varies (and should do so !) the "Default" are only suggestion that are written automatically in the configuration file. They must be configured manually and the server won't run if not.

**rpc.url=** 
The bitcoin core rpc url  
Default : http://localhost:18332/ on testnet or http://localhost:8332/ on main
Example :
```
rpc.url=http://localhost:18332/
```

**rpc.user=**
The rpc user for authentification
Default : bitcoinuser
Example : 
```
rpc.user=bitcoinpassword
```
**rpc.password=** 
The rpc password for authentification
Default : bitcoinuser
Example : 
```
rpc.password=bitcoinpassword
```
**rpc.cookiefile=** 
path to the rpc cookie file for authentification
Default : yourbitcoinfolder/.cookie
Example : 
```
rpc.cookiefile=yourbitcoinfolder/.cookie
```
### Networking : 


**port**
The port on which the server will listen
Default : 5000
Example : 
```
port=5005
```

**listen**
The address on which the server will listen
Default : 0.0.0.0
Example : 
```
listen=0.0.0.0
or
listen=localhost
```




