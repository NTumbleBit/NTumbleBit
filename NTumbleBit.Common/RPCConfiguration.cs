using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.RPC;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace NTumbleBit.Common
{
    public class RPCConfiguration
    {
		public static RPCClient ConfigureRPCClient(ILogger logger, string configurationFile, Network network)
		{
			Dictionary<string, string> configFile = null;
			try
			{
				configFile = TextFileConfiguration.Parse(File.ReadAllText(configurationFile));
			}
			catch(FormatException ex)
			{
				logger.LogError("Configuration file incorrectly formatted: " + ex.Message);
				throw new ConfigException();
			}

			RPCClient rpcClient = null;
			var url = configFile.TryGet("rpc.url") ?? "http://localhost:" + network.RPCPort + "/";
			var usr = configFile.TryGet("rpc.user");
			var pass = configFile.TryGet("rpc.password");
			if(url != null && usr != null && pass != null)
				rpcClient = new RPCClient(new System.Net.NetworkCredential(usr, pass), new Uri(url), network);
			if(rpcClient == null)
			{
				var cookieFile = configFile.TryGet("rpc.cookiefile");
				if(url != null && cookieFile != null)
				{
					try
					{

						rpcClient = new RPCClient(File.ReadAllText(cookieFile), new Uri(url), network);
					}
					catch(IOException)
					{
						logger.LogWarning("RPC Cookie file not found at " + cookieFile);
					}
				}

				if(rpcClient == null)
				{
					try
					{
						rpcClient = new RPCClient(network);
					}
					catch { }
					if(rpcClient == null)
					{
						var error = "RPC connection settings not configured at " + configurationFile;
						logger.LogError(error);
						throw new ConfigException();
					}
				}
			}

			logger.LogInformation("Testing RPC connection to " + rpcClient.Address.AbsoluteUri);
			try
			{
				rpcClient.SendCommand("whatever");
			}
			catch(RPCException ex)
			{
				if(ex.RPCCode != RPCErrorCode.RPC_METHOD_NOT_FOUND)
				{
					logger.LogError("Error connecting to RPC " + ex.Message);
					throw new ConfigException();
				}
			}
			catch(Exception ex)
			{
				logger.LogError("Error connecting to RPC " + ex.Message);
				throw new ConfigException();
			}
			logger.LogInformation("RPC connection successfull");

			if(rpcClient.GetBlockHash(0) != network.GenesisHash)
			{
				logger.LogError("The RPC server is not using the chain " + network.Name);
				throw new ConfigException();
			}
			return rpcClient;
		}
	}
}
