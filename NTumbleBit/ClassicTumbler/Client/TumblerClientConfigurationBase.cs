using NBitcoin;
using NTumbleBit.ClassicTumbler.Client.ConnectionSettings;
using NTumbleBit.Services;

namespace NTumbleBit.ClassicTumbler.Client
{
    public abstract class TumblerClientConfigurationBase
    {
        public string DataDir { get; set; }

        public Network Network { get; set; }

        public bool OnlyMonitor { get; set; }

        public bool CheckIp { get; set; } = true;

        public bool Cooperative { get; set; }

        public TumblerUrlBuilder TumblerServer { get; set; }

        public ConnectionSettingsBase BobConnectionSettings { get; set; } = new ConnectionSettingsBase();

        public ConnectionSettingsBase AliceConnectionSettings { get; set; } = new ConnectionSettings.ConnectionSettingsBase();

        public OutputWalletConfiguration OutputWallet { get; set; } = new OutputWalletConfiguration();

        public bool AllowInsecure { get; set; } = false;

        public string TorPath { get; set; }

        public bool TorMandatory { get; set; } = false;

        public Tracker Tracker { get; set; }

        public DBreezeRepository DBreezeRepository { get; set; }

        public IExternalServices Services { get; set; }
        
        public IDestinationWallet DestinationWallet { get; set; }

        protected bool IsTest(Network network)
        {
            return network == Network.TestNet || network == Network.RegTest;
        }
    }
}
