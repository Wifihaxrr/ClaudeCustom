using Facepunch;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("Gather Multiplier", "MaxThinking Rust", "1.0.0")]
    [Description("Increases gather rates by configurable multiplier (default 3x)")]
    public class GatherMultiplier : RustPlugin
    {
        #region Fields
        private const string PERMISSION_USE = "gathermultiplier.use";
        private const string PERMISSION_ADMIN = "gathermultiplier.admin";
        private const string PERMISSION_BYPASS = "gathermultiplier.bypass";

        private static GatherMultiplier Instance;
        private Configuration config;
        private readonly Dictionary<string, float> originalGatherRates = new Dictionary<string, float>();
        #endregion

        #region Oxide Hooks
        private void Init()
        {
            Instance = this;

            // Register permissions
            permission.RegisterPermission(PERMISSION_USE, this);
            permission.RegisterPermission(PERMISSION_ADMIN, this);
            permission.RegisterPermission(PERMISSION_BYPASS, this);

            // Register commands
            cmd.AddChatCommand("gather", this, nameof(CmdGather));
            cmd.AddConsoleCommand("gathermultiplier.reload", this, nameof(CmdReload));

            // Store original gather rates
            StoreOriginalGatherRates();
        }

        private void OnServerInitialized()
        {
            ApplyGatherMultiplier();
            PrintWarning($"Gather multiplier set to {config.GlobalMultiplier}x");
        }

        private void OnEntitySpawned(BaseNetworkable entity)
        {
            // Apply to dynamically spawned resources
            if (entity is ResourceContainer resource && config.ApplyToResources.Contains(resource.PrefabName))
            {
                NextTick(() => ModifyResourceContainer(resource));
            }
        }

        private void OnDispenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item)
        {
            if (config.EnableDebug)
                Puts($"Dispenser gathered: {dispenser.gameObject.name} - {item.info.shortname} x{item.amount}");
        }

        private void OnCollectiblePickup(Item item, BasePlayer player)
        {
            if (player != null && HasPermission(player, PERMISSION_USE) && config.AffectCollectibles)
            {
                var originalAmount = item.amount;
                item.amount = Mathf.CeilToInt(item.amount * config.GlobalMultiplier);

                if (config.EnableDebug && originalAmount != item.amount)
                {
                    Puts($"Collectible multiplied: {item.info.shortname} {originalAmount} -> {item.amount} for {player.displayName}");
                }
            }
        }

        private void Unload()
        {
            RestoreOriginalGatherRates();
            Instance = null;
        }

        protected override void LoadDefaultConfig() => config = new Configuration();

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) throw new JsonException();

                if (!config.ToDictionary().Keys.SequenceEqual(Config.ToDictionary(x => x.Key, x => x.Value).Keys))
                {
                    LogWarning("Config outdated; updating");
                    SaveConfig();
                }
            }
            catch
            {
                LogWarning("Config invalid; using defaults");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig() => Config.WriteObject(config, true);

        protected override void LoadDefaultMessages() => lang.RegisterMessages(Messages, this);
        #endregion

        #region Methods
        private void StoreOriginalGatherRates()
        {
            // Store original gather rates for resources
            foreach (var resource in Resources.All)
            {
                if (resource != null && !string.IsNullOrEmpty(resource.gameObject.name))
                {
                    // This is a simplified approach - in practice, you'd need to access the actual gather rates
                    originalGatherRates[resource.gameObject.name] = 1.0f;
                }
            }
        }

        private void ApplyGatherMultiplier()
        {
            // Modify global gather rate if configured
            if (config.ModifyGlobalRate)
            {
                // Note: This would require reflection or hooking into internal methods
                // This is a placeholder for where you'd modify the global gather multiplier
            }
        }

        private void RestoreOriginalGatherRates()
        {
            // Restore any modified rates
            if (config.ModifyGlobalRate)
            {
                // Restore original rates
            }
        }

        private void ModifyResourceContainer(ResourceContainer container)
        {
            if (container == null) return;

            // Multiply the contents
            foreach (var item in container.itemList)
            {
                if (item != null)
                {
                    item.amount = Mathf.CeilToInt(item.amount * config.GlobalMultiplier);
                }
            }
        }

        private bool HasPermission(BasePlayer player, string perm)
        {
            return player != null && permission.UserHasPermission(player.UserIDString, perm);
        }

        private void GiveMultiplierEffect(BasePlayer player)
        {
            if (!HasPermission(player, PERMISSION_USE)) return;

            // Visual feedback
            Effect.server.Run("assets/prefabs/misc/christmas/effects/xmas.song.note.fx.prefab", player.transform.position);

            // Show UI if enabled
            if (config.ShowNotification)
            {
                ShowNotification(player);
            }
        }

        private void ShowNotification(BasePlayer player)
        {
            var container = new CuiElementContainer();

            // Main panel
            container.Add(new CuiPanel
            {
                Image = { Color = "0.1 0.1 0.1 0.8" },
                RectTransform = { AnchorMin = "0.3 0.8", AnchorMax = "0.7 0.9" },
                CursorEnabled = false
            }, "Overlay", "GatherNotification");

            // Text
            container.Add(new CuiLabel
            {
                Text = { Text = $"Gather Rate: {config.GlobalMultiplier}x Active", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
            }, "GatherNotification");

            CuiHelper.DestroyUi(player, "GatherNotification");
            CuiHelper.AddUi(player, container);

            // Auto-hide after 3 seconds
            timer.Once(3f, () => CuiHelper.DestroyUi(player, "GatherNotification"));
        }
        #endregion

        #region Commands
        private void CmdGather(BasePlayer player, string command, string[] args)
        {
            if (!HasPermission(player, PERMISSION_USE))
            {
                player.ChatMessage(Lang("NoPermission", player.UserIDString));
                return;
            }

            if (args.Length == 0)
            {
                player.ChatMessage($"Current gather multiplier: {config.GlobalMultiplier}x");
                return;
            }

            if (args[0].ToLower() == "info")
            {
                player.ChatMessage($"Gather Multiplier Plugin v{Version}");
                player.ChatMessage($"Current multiplier: {config.GlobalMultiplier}x");
                player.ChatMessage($"Affects mining: {config.AffectMining}");
                player.ChatMessage($"Affects wood: {config.AffectWood}");
                player.ChatMessage($"Affects collectibles: {config.AffectCollectibles}");
                return;
            }
        }

        [ConsoleCommand("gathermultiplier.reload")]
        private void CmdReload(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null && !arg.IsAdmin) return;

            LoadConfig();
            ApplyGatherMultiplier();
            Puts("Configuration reloaded successfully!");
        }
        #endregion

        #region Localization
        private Dictionary<string, string> Messages = new Dictionary<string, string>
        {
            ["NoPermission"] = "You don't have permission to use this command!",
            ["GatherRate"] = "Gather rate set to {0}x",
            ["Reloaded"] = "Gather Multiplier configuration reloaded!"
        };

        private string Lang(string key, string userId = null, params object[] args)
        {
            return string.Format(lang.GetMessage(key, this, userId), args);
        }
        #endregion

        #region Configuration
        private class Configuration
        {
            [JsonProperty("Global Multiplier")]
            public float GlobalMultiplier = 3.0f;

            [JsonProperty("Affect Mining")]
            public bool AffectMining = true;

            [JsonProperty("Affect Wood")]
            public bool AffectWood = true;

            [JsonProperty("Affect Skinning")]
            public bool AffectSkinning = true;

            [JsonProperty("Affect Quarries")]
            public bool AffectQuarries = true;

            [JsonProperty("Affect Excavators")]
            public bool AffectExcavators = true;

            [JsonProperty("Affect Collectibles")]
            public bool AffectCollectibles = true;

            [JsonProperty("Modify Global Rate")]
            public bool ModifyGlobalRate = false;

            [JsonProperty("Show Notification")]
            public bool ShowNotification = true;

            [JsonProperty("Enable Debug")]
            public bool EnableDebug = false;

            [JsonProperty("Blacklisted Resources")]
            public List<string> BlacklistedResources = new List<string>
            {
                "hemp",
                "mushroom",
                "corn",
                "pumpkin"
            };

            [JsonProperty("Apply To Resources")]
            public List<string> ApplyToResources = new List<string>
            {
                "ore",
                "stone-ore",
                "sulfur-ore",
                "metal-ore"
            };

            [JsonProperty("VIP Multipliers")]
            public Dictionary<string, float> VipMultipliers = new Dictionary<string, float>
            {
                ["gathermultiplier.vip"] = 5.0f,
                ["gathermultiplier.premium"] = 10.0f
            };

            public string ToJson() => JsonConvert.SerializeObject(this);
            public Dictionary<string, object> ToDictionary() => JsonConvert.DeserializeObject<Dictionary<string, object>>(ToJson());
        }
        #endregion

        #region Hook Overrides for Specific Resource Types
        private object OnDispenserBonus(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            if (player == null || !HasPermission(player, PERMISSION_USE)) return null;

            // Check if player has bypass permission
            if (HasPermission(player, PERMISSION_BYPASS))
            {
                return null;
            }

            // Check VIP multipliers
            float multiplier = config.GlobalMultiplier;
            foreach (var vip in config.VipMultipliers)
            {
                if (HasPermission(player, vip.Key))
                {
                    multiplier = vip.Value;
                    break;
                }
            }

            // Check blacklist
            if (config.BlacklistedResources.Any(resource =>
                dispenser.gameObject.name.Contains(resource, StringComparison.OrdinalIgnoreCase)))
            {
                return null;
            }

            // Apply multiplier based on dispenser type
            if (config.AffectMining && dispenser.gatherType == ResourceDispenser.GatherType.Ore)
            {
                item.amount = Mathf.CeilToInt(item.amount * multiplier);
            }
            else if (config.AffectWood && dispenser.gatherType == ResourceDispenser.GatherType.Tree)
            {
                item.amount = Mathf.CeilToInt(item.amount * multiplier);
            }
            else if (config.AffectSkinning && dispenser.gatherType == ResourceDispenser.GatherType.Flesh)
            {
                item.amount = Mathf.CeilToInt(item.amount * multiplier);
            }

            if (config.EnableDebug)
                Puts($"Modified {item.info.shortname}: x{multiplier} for {player.displayName}");

            return null;
        }

        private object OnQuarryGather(MiningQuarry quarry, Item item)
        {
            if (config.AffectQuarries)
            {
                item.amount = Mathf.CeilToInt(item.amount * config.GlobalMultiplier);
                if (config.EnableDebug)
                    Puts($"Quarry modified: {item.info.shortname} x{config.GlobalMultiplier}");
            }
            return null;
        }

        private object OnExcavatorGather(MiningExcavator excavator, Item item)
        {
            if (config.AffectExcavators)
            {
                item.amount = Mathf.CeilToInt(item.amount * config.GlobalMultiplier);
                if (config.EnableDebug)
                    Puts($"Excavator modified: {item.info.shortname} x{config.GlobalMultiplier}");
            }
            return null;
        }
        #endregion
    }
}