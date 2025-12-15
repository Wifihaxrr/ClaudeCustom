using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("BrightNights", "Whispers88/Updated", "1.4.0")]
    [Description("Makes Nights brighter for VIP players")]
    public class BrightNights : RustPlugin
    {
        private const string PermVip = "brightnights.vip";
        
        private Configuration config;
        private bool isNight = false;
        private HashSet<ulong> vipPlayers = new HashSet<ulong>();

        public class Configuration
        {
            [JsonProperty("Night Vision Brightness (higher = brighter, default 0.0175)")]
            public float brightness = 1f;
            
            [JsonProperty("Night Vision Distance (default 7)")]
            public float distance = 100f;
            
            [JsonProperty("Atmosphere Brightness Boost (1 = normal)")]
            public float atmosphereBrightness = 3f;
        }

        protected override void LoadDefaultConfig() => config = new Configuration();

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) throw new JsonException();
            }
            catch
            {
                LoadDefaultConfig();
                SaveConfig();
            }
        }

        protected override void SaveConfig() => Config.WriteObject(config, true);

        private void OnServerInitialized()
        {
            permission.RegisterPermission(PermVip, this);

            if (TOD_Sky.Instance == null)
            {
                timer.Once(15f, OnServerInitialized);
                return;
            }

            TOD_Sky.Instance.Components.Time.OnSunrise += OnSunrise;
            TOD_Sky.Instance.Components.Time.OnSunset += OnSunset;

            foreach (var player in BasePlayer.activePlayerList)
            {
                if (HasPerm(player.UserIDString, PermVip))
                    vipPlayers.Add(player.userID);
            }

            isNight = TOD_Sky.Instance.IsNight;
            
            if (isNight)
                ApplyBrightNightsToAll();
        }

        private void Unload()
        {
            if (TOD_Sky.Instance != null)
            {
                TOD_Sky.Instance.Components.Time.OnSunrise -= OnSunrise;
                TOD_Sky.Instance.Components.Time.OnSunset -= OnSunset;
            }
            
            foreach (var player in BasePlayer.activePlayerList.ToList())
            {
                if (player != null && vipPlayers.Contains(player.userID))
                    ResetPlayer(player);
            }
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (player == null) return;
            
            timer.Once(2f, () =>
            {
                if (player == null || !player.IsConnected) return;
                
                if (HasPerm(player.UserIDString, PermVip))
                {
                    vipPlayers.Add(player.userID);
                    if (isNight)
                        ApplyBrightNights(player);
                }
            });
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
            if (player != null)
                vipPlayers.Remove(player.userID);
        }

        private void OnSunrise()
        {
            isNight = false;
            foreach (var player in BasePlayer.activePlayerList.ToList())
            {
                if (player != null && vipPlayers.Contains(player.userID))
                    ResetPlayer(player);
            }
        }

        private void OnSunset()
        {
            isNight = true;
            ApplyBrightNightsToAll();
        }

        private void ApplyBrightNightsToAll()
        {
            foreach (var player in BasePlayer.activePlayerList.ToList())
            {
                if (player != null && vipPlayers.Contains(player.userID))
                    ApplyBrightNights(player);
            }
        }

        private void ApplyBrightNights(BasePlayer player)
        {
            if (player?.net?.connection == null) return;
            
            // Nightlight for immediate area
            player.SendConsoleCommand("env.nightlight_enabled", "true");
            player.SendConsoleCommand("env.nightlight_brightness", config.brightness);
            player.SendConsoleCommand("env.nightlight_distance", config.distance);
            
            // Boost overall atmosphere brightness for distance visibility
            player.SendConsoleCommand("weather.atmosphere_brightness", config.atmosphereBrightness);
        }

        private void ResetPlayer(BasePlayer player)
        {
            if (player?.net?.connection == null) return;
            
            player.SendConsoleCommand("env.nightlight_enabled", "false");
            player.SendConsoleCommand("weather.atmosphere_brightness", 1f);
        }

        private bool HasPerm(string id, string perm) => permission.UserHasPermission(id, perm);
    }
}
