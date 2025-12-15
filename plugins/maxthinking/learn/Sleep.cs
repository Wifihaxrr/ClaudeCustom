using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("Sleep" , "Krungh Crow" , "1.0.2" , ResourceId = 1156)]
    [Description("Allows players with permission to get a well-rested sleep")]
    public class Sleep : CovalencePlugin
    {
        #region Configuration

        private Configuration config;

        public class Configuration
        {
            [JsonProperty(PropertyName = "Cure while sleeping (true/false)")]
            public bool CureWhileSleeping;
            [JsonProperty(PropertyName = "Heal while sleeping (true/false)")]
            public bool HealWhileSleeping;
            [JsonProperty(PropertyName = "Restore while sleeping (true/false)")]
            public bool RestoreWhileSleeping;
            [JsonProperty(PropertyName = "Curing rate (0 - 100)")]
            public int CuringRate;
            [JsonProperty(PropertyName = "Healing rate (0 - 100)")]
            public int HealingRate;
            [JsonProperty(PropertyName = "Restoration rate (0 - 100)")]
            public int RestorationRate;
            public static Configuration DefaultConfig()
            {
                return new Configuration
                {
                    CureWhileSleeping = false ,
                    HealWhileSleeping = true ,
                    RestoreWhileSleeping = true ,
                    CuringRate = 5 ,
                    HealingRate = 5 ,
                    RestorationRate = 5
                };
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config?.HealWhileSleeping == null) SaveConfig();
            }
            catch
            {
                LogWarning($"Could not read oxide/config/{Name}.json, creating new config file");
            }
            LoadDefaultConfig();
        }

        protected override void LoadDefaultConfig() => config = Configuration.DefaultConfig();

        protected override void SaveConfig() => Config.WriteObject(config);

        #endregion

        #region Localization

        private new void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string , string>
            {
                ["Command"] = "sleep" ,
                ["Dirty"] = "You seem to be a bit dirty, go take a dip!" ,
                ["Hungry"] = "You seem to be a bit hungry, eat something" ,
                ["NotAllowed"] = "You can't go to sleep right now" ,
                ["Restored"] = "You have awaken restored and rested!" ,
                ["Thirsty"] = "You seem to be a bit thirsty, drink something!" ,
                ["WentToSleep"] = "You went to sleep."
            } , this);
        }

        #endregion

        #region Initialization

        private readonly Dictionary<string , Timer> sleepTimers = new Dictionary<string , Timer>();
        private const string permAllow = "sleep.allow";

        private void Init()
        {
            AddCovalenceCommand(Lang("Command") , "SleepCommand");
            permission.RegisterPermission(permAllow , this);
        }

        #endregion

        #region Restoration

        private void Restore(BasePlayer player)
        {
            if (player == null || !player.IsConnected) return;

            var metabolism = player.metabolism;

            if (config.CureWhileSleeping)
            {
                if (metabolism.poison.value > 0) metabolism.poison.value = metabolism.poison.value - (metabolism.poison.value / config.CuringRate);
                if (metabolism.radiation_level.value > 0) metabolism.radiation_level.value = metabolism.radiation_level.value - (metabolism.radiation_level.value / config.CuringRate);
                if (metabolism.radiation_poison.value > 0) metabolism.radiation_poison.value = metabolism.radiation_poison.value - (metabolism.radiation_poison.value / config.CuringRate);
            }

            if (config.HealWhileSleeping)
            {
                if (metabolism.bleeding.value.Equals(1)) metabolism.bleeding.value = 0;
                if (player.health < 100) player.health = player.health + (player.health / config.HealingRate);
            }

            if (config.RestoreWhileSleeping)
            {
                if (player.health < 100)
                {
                    if (metabolism.calories.value < 1000) metabolism.calories.value = metabolism.calories.value + (metabolism.calories.value / config.RestorationRate);
                    if (metabolism.comfort.value < 0.5) metabolism.comfort.value = metabolism.comfort.value + (metabolism.comfort.value / config.RestorationRate);
                    if (metabolism.heartrate.value > 0.5) metabolism.heartrate.value = metabolism.heartrate.value + (metabolism.heartrate.value / config.RestorationRate);
                    if (metabolism.temperature.value < 20) metabolism.temperature.value = metabolism.temperature.value + (metabolism.temperature.value / config.RestorationRate);
                    else metabolism.temperature.value = metabolism.temperature.value - (metabolism.temperature.value / config.RestorationRate);
                }
            }
        }

        private void OnPlayerSleepEnded(BasePlayer basePlayer)
        {
            if (basePlayer == null) return;

            if (sleepTimers.ContainsKey(basePlayer.UserIDString))
            {
                sleepTimers[basePlayer.UserIDString].Destroy();
                sleepTimers.Remove(basePlayer.UserIDString);
            }

            var player = players.FindPlayerById(basePlayer.UserIDString);
            if (player == null || !player.IsConnected) return;

            Message(player , "Restored");
            if (basePlayer.metabolism.calories.value < 40) Message(player , "Hungry");
            if (basePlayer.metabolism.dirtyness.value > 0) Message(player , "Dirty");
            if (basePlayer.metabolism.hydration.value < 40) Message(player , "Thirsty");
        }

        #endregion

        #region Command

        private void SleepCommand(IPlayer player , string command , string[] args)
        {
            if (!player.HasPermission(permAllow))
            {
                Message(player , "NotAllowed");
                return;
            }

            var basePlayer = player.Object as BasePlayer;
            if (basePlayer == null) return;

            basePlayer.StartSleeping();

            if (sleepTimers.ContainsKey(player.Id))
            {
                sleepTimers[player.Id].Destroy();
                sleepTimers.Remove(player.Id);
            }

            sleepTimers[player.Id] = timer.Every(10f , () =>
            {
                if (!player.IsSleeping)
                {
                    if (sleepTimers.ContainsKey(player.Id))
                    {
                        sleepTimers[player.Id].Destroy();
                        sleepTimers.Remove(player.Id);
                    }
                    return;
                }

                Restore(basePlayer);
            });

            Message(player , "WentToSleep");
        }

        #endregion

        #region Helpers

        private string Lang(string key , string id = null , params object[] args) => string.Format(lang.GetMessage(key , this , id) , args);

        private void Message(IPlayer player , string key , params object[] args) => player.Reply(Lang(key , player.Id , args));

        #endregion
    }
}