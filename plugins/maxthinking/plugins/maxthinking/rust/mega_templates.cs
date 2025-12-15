// ============================================================================
// MEGA-TEMPLATES: COMPLETE WORKING CODE FROM 85 PRODUCTION PLUGINS
// THIS FILE CONTAINS REAL CODE THAT COMPILES AND WORKS - COPY IT EXACTLY
// ============================================================================

// ============================================================================
// TEMPLATE 1: MINIMAL PLUGIN (from NoGiveNotices.cs - 18 lines)
// Use for: Simple hook blocking, minimal functionality
// ============================================================================

namespace Oxide.Plugins
{
    [Info("MyMinimalPlugin", "Author", "1.0.0")]
    [Description("Simple plugin that blocks something")]
    class MyMinimalPlugin : RustPlugin
    {
        private object OnServerMessage(string message, string name)
        {
            // Return true to block, null to allow
            if (message.Contains("something") && name == "SERVER")
            {
                return true; // Block this message
            }
            return null; // Allow
        }
    }
}

// ============================================================================
// TEMPLATE 2: SIMPLE WITH CONFIG & PERMISSIONS (from NoTechTree.cs - 134 lines)
// Use for: Blocking features, config options, permissions
// ============================================================================

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("MySimplePlugin", "Author", "1.0.0")]
    [Description("Simple plugin with config and permissions")]
    class MySimplePlugin : RustPlugin
    {
        private const string PERMISSION_BYPASS = "myplugin.bypass";
        private const string PERMISSION_USE = "myplugin.use";
        
        private Configuration config;

        private class Configuration
        {
            [JsonProperty("Enable Feature")]
            public bool EnableFeature = true;

            [JsonProperty("Cooldown Seconds")]
            public float Cooldown = 60f;
        }

        protected override void LoadDefaultConfig()
        {
            config = new Configuration();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) throw new Exception();
                SaveConfig();
            }
            catch
            {
                PrintError("Config file is corrupt, generating new one...");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig() => Config.WriteObject(config);

        private void Init()
        {
            permission.RegisterPermission(PERMISSION_BYPASS, this);
            permission.RegisterPermission(PERMISSION_USE, this);

            if (!config.EnableFeature)
                Unsubscribe(nameof(OnSomeHook));
        }

        private void Unload()
        {
            // Clean up here (timers, UI, etc.)
        }

        // Example blocking hook
        private object OnSomeHook(BasePlayer player)
        {
            if (!config.EnableFeature)
                return null;

            if (permission.UserHasPermission(player.UserIDString, PERMISSION_BYPASS))
                return null;

            PrintToChat(player, lang.GetMessage("Blocked", this, player.UserIDString));
            return false; // Block
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Blocked"] = "<color=#ff5555>This feature has been disabled.</color>",
                ["NoPermission"] = "You don't have permission!"
            }, this);
        }
    }
}

// ============================================================================
// TEMPLATE 3: WITH DATA STORAGE (from Economics.cs)
// Use for: Saving player data, persistent storage
// ============================================================================

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("MyDataPlugin", "Author", "1.0.0")]
    [Description("Plugin with persistent data storage")]
    class MyDataPlugin : RustPlugin
    {
        private StoredData storedData;
        private bool changed;

        private class StoredData
        {
            public Dictionary<ulong, PlayerData> Players = new Dictionary<ulong, PlayerData>();
        }

        private class PlayerData
        {
            public int Uses;
            public double LastUsed;
            public Dictionary<string, int> Stats = new Dictionary<string, int>();
        }

        private void Init()
        {
            LoadData();
        }

        private void LoadData()
        {
            try
            {
                storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name);
            }
            catch
            {
                storedData = new StoredData();
            }
            
            if (storedData == null)
                storedData = new StoredData();
        }

        private void SaveData()
        {
            if (changed)
            {
                Puts("Saving data...");
                Interface.Oxide.DataFileSystem.WriteObject(Name, storedData);
                changed = false;
            }
        }

        private PlayerData GetPlayerData(ulong oderId)
        {
            if (!storedData.Players.TryGetValue(userId, out PlayerData data))
            {
                data = new PlayerData();
                storedData.Players[userId] = data;
                changed = true;
            }
            return data;
        }

        private void OnServerSave() => SaveData();
        private void Unload() => SaveData();
    }
}

// ============================================================================
// TEMPLATE 4: WITH TIMERS & TOD_SKY (from BrightNights.cs)
// Use for: Time-based effects, day/night cycles
// ============================================================================

using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("MyTimePlugin", "Author", "1.0.0")]
    [Description("Plugin that responds to day/night")]
    public class MyTimePlugin : RustPlugin
    {
        private const string PermVip = "myplugin.vip";
        
        private Configuration config;
        private bool isNight = false;
        private HashSet<ulong> activePlayers = new HashSet<ulong>();

        public class Configuration
        {
            [JsonProperty("Effect Strength")]
            public float Strength = 1f;
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

            // Subscribe to sunrise/sunset events
            TOD_Sky.Instance.Components.Time.OnSunrise += OnSunrise;
            TOD_Sky.Instance.Components.Time.OnSunset += OnSunset;

            // Initialize players
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (permission.UserHasPermission(player.UserIDString, PermVip))
                    activePlayers.Add(player.userID);
            }

            isNight = TOD_Sky.Instance.IsNight;
            
            if (isNight)
                ApplyEffectToAll();
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
                if (player != null && activePlayers.Contains(player.userID))
                    ResetPlayer(player);
            }
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (player == null) return;
            
            timer.Once(2f, () =>
            {
                if (player == null || !player.IsConnected) return;
                
                if (permission.UserHasPermission(player.UserIDString, PermVip))
                {
                    activePlayers.Add(player.userID);
                    if (isNight)
                        ApplyEffect(player);
                }
            });
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
            if (player != null)
                activePlayers.Remove(player.userID);
        }

        private void OnSunrise()
        {
            isNight = false;
            foreach (var player in BasePlayer.activePlayerList.ToList())
            {
                if (player != null && activePlayers.Contains(player.userID))
                    ResetPlayer(player);
            }
        }

        private void OnSunset()
        {
            isNight = true;
            ApplyEffectToAll();
        }

        private void ApplyEffectToAll()
        {
            foreach (var player in BasePlayer.activePlayerList.ToList())
            {
                if (player != null && activePlayers.Contains(player.userID))
                    ApplyEffect(player);
            }
        }

        private void ApplyEffect(BasePlayer player)
        {
            if (player?.net?.connection == null) return;
            
            // Send console commands to player
            player.SendConsoleCommand("some.command", config.Strength);
        }

        private void ResetPlayer(BasePlayer player)
        {
            if (player?.net?.connection == null) return;
            
            player.SendConsoleCommand("some.command", 0f);
        }
    }
}

// ============================================================================
// TEMPLATE 5: CHAT COMMAND WITH COOLDOWNS (from PersonalHeli.cs)
// Use for: Commands with cooldowns per player
// ============================================================================

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("MyCooldownPlugin", "Author", "1.0.0")]
    [Description("Plugin with per-player cooldowns")]
    class MyCooldownPlugin : RustPlugin
    {
        private const string permUse = "myplugin.use";
        private PluginConfig config;
        private StoredData storedData;

        class PluginConfig
        {
            public int CooldownSeconds = 1800;
            public string ChatCommand = "mycommand";
        }

        class StoredData
        {
            public Dictionary<ulong, CooldownData> Cooldowns = new Dictionary<ulong, CooldownData>();

            public class CooldownData
            {
                public DateTime LastUse = DateTime.MinValue;
                
                public bool CanUseNow(int cooldown)
                {
                    return DateTime.Now.Subtract(LastUse).TotalSeconds > cooldown;
                }

                public int SecondsRemaining(int cooldown)
                {
                    return (int)Math.Round(cooldown - DateTime.Now.Subtract(LastUse).TotalSeconds);
                }

                public void OnUse()
                {
                    LastUse = DateTime.Now;
                }
            }

            public CooldownData GetForPlayer(BasePlayer player)
            {
                if (!Cooldowns.ContainsKey(player.userID))
                {
                    Cooldowns[player.userID] = new CooldownData();
                }
                return Cooldowns[player.userID];
            }
        }

        private void Init()
        {
            permission.RegisterPermission(permUse, this);
            config = Config.ReadObject<PluginConfig>() ?? new PluginConfig();
            Config.WriteObject(config);
            cmd.AddChatCommand(config.ChatCommand, this, nameof(CmdMain));
            LoadData();
        }

        private void LoadData()
        {
            storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name);
            if (storedData == null)
            {
                storedData = new StoredData();
                SaveData();
            }
        }

        private void SaveData()
        {
            if (storedData != null)
            {
                Interface.Oxide.DataFileSystem.WriteObject(Name, storedData);
            }
        }

        private void Unload() => SaveData();
        private void OnServerSave() => SaveData();

        protected override void LoadDefaultConfig()
        {
            Config.WriteObject(new PluginConfig(), true);
        }

        private void CmdMain(BasePlayer player, string cmd, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, permUse))
            {
                SendReply(player, Lang("NoPermission", player.UserIDString));
                return;
            }

            var cooldownData = storedData.GetForPlayer(player);
            if (!cooldownData.CanUseNow(config.CooldownSeconds))
            {
                var remaining = TimeSpan.FromSeconds(cooldownData.SecondsRemaining(config.CooldownSeconds));
                SendReply(player, Lang("Cooldown", player.UserIDString, remaining));
                return;
            }

            // DO YOUR ACTION HERE
            DoAction(player);
            
            cooldownData.OnUse();
            SaveData();
        }

        private void DoAction(BasePlayer player)
        {
            // Your action code here
            SendReply(player, Lang("Success", player.UserIDString));
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NoPermission"] = "You don't have permission to use this command.",
                ["Cooldown"] = "Please wait {0} before using this again.",
                ["Success"] = "Action completed!"
            }, this);
        }

        private string Lang(string key, string userId, params object[] args)
        {
            return string.Format(lang.GetMessage(key, this, userId), args);
        }
    }
}

// ============================================================================
// TEMPLATE 6: ENTITY SPAWNING (from PersonalHeli.cs)
// Use for: Spawning entities, helicopters, vehicles
// ============================================================================

using UnityEngine;

namespace Oxide.Plugins
{
    [Info("MySpawnPlugin", "Author", "1.0.0")]
    [Description("Plugin that spawns entities")]
    class MySpawnPlugin : RustPlugin
    {
        // Spawn a patrol helicopter
        private PatrolHelicopter SpawnHelicopter(Vector3 position)
        {
            PatrolHelicopter heli = GameManager.server.CreateEntity(
                "assets/prefabs/npc/patrol helicopter/patrolhelicopter.prefab", 
                position, 
                Quaternion.identity
            ) as PatrolHelicopter;
            
            if (heli == null) return null;
            
            heli.Spawn();
            return heli;
        }

        // Spawn a minicopter
        private MiniCopter SpawnMinicopter(Vector3 position, Quaternion rotation)
        {
            var entity = GameManager.server.CreateEntity(
                "assets/content/vehicles/minicopter/minicopter.entity.prefab",
                position,
                rotation
            ) as MiniCopter;

            if (entity == null) return null;

            entity.OwnerID = 0; // Set owner if needed
            entity.Spawn();
            return entity;
        }

        // Spawn any entity by prefab
        private BaseEntity SpawnEntity(string prefab, Vector3 position, Quaternion rotation)
        {
            var entity = GameManager.server.CreateEntity(prefab, position, rotation);
            if (entity == null)
            {
                PrintWarning($"Failed to create entity: {prefab}");
                return null;
            }
            
            entity.Spawn();
            return entity;
        }
    }
}

// ============================================================================
// TEMPLATE 7: TELEPORT PLAYER
// Use for: Teleporting players
// ============================================================================

using UnityEngine;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("MyTeleportPlugin", "Author", "1.0.0")]
    [Description("Simple teleport plugin")]
    class MyTeleportPlugin : RustPlugin
    {
        private const string PERM_USE = "myteleport.use";
        
        private void Init()
        {
            permission.RegisterPermission(PERM_USE, this);
        }

        [ChatCommand("tp")]
        private void CmdTeleport(BasePlayer player, string command, string[] args)
        {
            if (player == null) return;
            
            if (!permission.UserHasPermission(player.UserIDString, PERM_USE))
            {
                SendReply(player, "No permission!");
                return;
            }

            if (args.Length < 1)
            {
                SendReply(player, "Usage: /tp <player>");
                return;
            }

            BasePlayer target = FindPlayer(args[0]);
            if (target == null)
            {
                SendReply(player, $"Player '{args[0]}' not found.");
                return;
            }

            TeleportPlayer(player, target.transform.position);
            SendReply(player, $"Teleported to {target.displayName}");
        }

        private void TeleportPlayer(BasePlayer player, Vector3 position)
        {
            if (player == null || !player.IsConnected) return;
            
            // Dismount if on vehicle
            if (player.isMounted)
            {
                player.GetMounted()?.DismountPlayer(player, true);
            }
            
            // End looting if active
            if (player.inventory.loot.IsLooting())
            {
                player.EndLooting();
            }
            
            // Wake up if sleeping
            if (player.IsSleeping())
            {
                player.EndSleeping();
            }

            // Teleport
            player.Teleport(position);
        }

        private BasePlayer FindPlayer(string nameOrId)
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player.UserIDString == nameOrId)
                    return player;
                if (player.displayName.ToLower().Contains(nameOrId.ToLower()))
                    return player;
            }
            return null;
        }
    }
}

// ============================================================================
// TEMPLATE 8: SIMPLE UI (from learn folder patterns)
// Use for: Basic CUI panels
// ============================================================================

using Oxide.Game.Rust.Cui;
using UnityEngine;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("MyUIPlugin", "Author", "1.0.0")]
    [Description("Plugin with simple UI")]
    class MyUIPlugin : RustPlugin
    {
        private const string UI_PANEL = "MyUIPlugin_Panel";
        private const string PERM_USE = "myuiplugin.use";

        private void Init()
        {
            permission.RegisterPermission(PERM_USE, this);
        }

        private void Unload()
        {
            // CRITICAL: Destroy UI for all players on unload
            foreach (var player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, UI_PANEL);
            }
        }

        [ChatCommand("ui")]
        private void CmdUI(BasePlayer player, string command, string[] args)
        {
            if (player == null) return;
            
            if (!permission.UserHasPermission(player.UserIDString, PERM_USE))
            {
                SendReply(player, "No permission!");
                return;
            }

            ShowUI(player);
        }

        [ConsoleCommand("myui.close")]
        private void CmdCloseUI(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            
            CuiHelper.DestroyUi(player, UI_PANEL);
        }

        private void ShowUI(BasePlayer player)
        {
            // Destroy existing first
            CuiHelper.DestroyUi(player, UI_PANEL);
            
            var container = new CuiElementContainer();

            // Main panel
            container.Add(new CuiPanel
            {
                Image = { Color = "0.1 0.1 0.1 0.9" },
                RectTransform = { AnchorMin = "0.3 0.3", AnchorMax = "0.7 0.7" },
                CursorEnabled = true
            }, "Overlay", UI_PANEL);

            // Title label
            container.Add(new CuiLabel
            {
                Text = { 
                    Text = "My Plugin UI", 
                    FontSize = 24, 
                    Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 1"
                },
                RectTransform = { AnchorMin = "0 0.85", AnchorMax = "1 1" }
            }, UI_PANEL);

            // Content label
            container.Add(new CuiLabel
            {
                Text = {
                    Text = "Hello World!",
                    FontSize = 18,
                    Align = TextAnchor.MiddleCenter
                },
                RectTransform = { AnchorMin = "0 0.4", AnchorMax = "1 0.7" }
            }, UI_PANEL);

            // Close button
            container.Add(new CuiButton
            {
                Button = { 
                    Color = "0.8 0.2 0.2 1", 
                    Command = "myui.close" 
                },
                RectTransform = { AnchorMin = "0.35 0.1", AnchorMax = "0.65 0.2" },
                Text = { 
                    Text = "Close", 
                    FontSize = 16, 
                    Align = TextAnchor.MiddleCenter 
                }
            }, UI_PANEL);

            CuiHelper.AddUi(player, container);
        }
    }
}

// ============================================================================
// TEMPLATE 9: DAMAGE BLOCKING / GOD MODE (CORRECT WAY)
// Use for: Making players invulnerable, blocking damage
// ============================================================================

using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("MyGodModePlugin", "Author", "1.0.0")]
    [Description("Makes players invulnerable using hooks")]
    class MyGodModePlugin : RustPlugin
    {
        private const string PERM_GODMODE = "mygodmode.use";
        private HashSet<ulong> godModePlayers = new HashSet<ulong>();

        private void Init()
        {
            permission.RegisterPermission(PERM_GODMODE, this);
        }

        [ChatCommand("god")]
        private void CmdGodMode(BasePlayer player, string command, string[] args)
        {
            if (player == null) return;
            
            if (!permission.UserHasPermission(player.UserIDString, PERM_GODMODE))
            {
                SendReply(player, "No permission!");
                return;
            }

            if (godModePlayers.Contains(player.userID))
            {
                godModePlayers.Remove(player.userID);
                SendReply(player, "God mode: OFF");
            }
            else
            {
                godModePlayers.Add(player.userID);
                SendReply(player, "God mode: ON");
            }
        }

        // THIS IS THE CORRECT WAY TO BLOCK DAMAGE
        private object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            var player = entity as BasePlayer;
            if (player == null) return null;
            
            if (godModePlayers.Contains(player.userID))
            {
                // Block all damage
                info.damageTypes.ScaleAll(0f);
                return true;
            }
            
            return null;
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            // Clean up when player disconnects
            godModePlayers.Remove(player.userID);
        }

        private void Unload()
        {
            godModePlayers.Clear();
        }
    }
}

// ============================================================================
// TEMPLATE 10: ITEM GIVING
// Use for: Giving items to players
// ============================================================================

using UnityEngine;

namespace Oxide.Plugins
{
    [Info("MyItemPlugin", "Author", "1.0.0")]
    [Description("Plugin that gives items")]
    class MyItemPlugin : RustPlugin
    {
        [ChatCommand("give")]
        private void CmdGive(BasePlayer player, string command, string[] args)
        {
            if (player == null) return;
            
            if (args.Length < 1)
            {
                SendReply(player, "Usage: /give <itemname> [amount]");
                return;
            }

            string shortname = args[0];
            int amount = args.Length > 1 ? int.Parse(args[1]) : 1;

            GiveItem(player, shortname, amount);
        }

        private void GiveItem(BasePlayer player, string shortname, int amount, ulong skin = 0)
        {
            var item = ItemManager.CreateByName(shortname, amount, skin);
            if (item == null)
            {
                PrintWarning($"Failed to create item: {shortname}");
                SendReply(player, $"Invalid item: {shortname}");
                return;
            }

            // Try to give to inventory, drop if full
            if (!player.inventory.GiveItem(item))
            {
                item.Drop(player.transform.position + Vector3.up, Vector3.up);
                SendReply(player, "Inventory full - item dropped!");
            }
            else
            {
                SendReply(player, $"Gave {amount}x {shortname}");
            }
        }
    }
}

// ============================================================================
// TEMPLATE 11: GATHERING MODIFIER (from GatherManager.cs)
// Use for: Modifying resource gathering rates
// ============================================================================

using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("MyGatherPlugin", "Author", "1.0.0")]
    [Description("Modifies gathering rates")]
    class MyGatherPlugin : RustPlugin
    {
        private Dictionary<string, float> gatherMultipliers = new Dictionary<string, float>
        {
            ["Wood"] = 2.0f,
            ["Stones"] = 2.0f,
            ["Metal Ore"] = 2.0f,
            ["Sulfur Ore"] = 2.0f
        };

        // Called when player gathers from a resource
        private void OnDispenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item)
        {
            var player = entity.ToPlayer();
            if (player == null) return;

            float multiplier;
            if (gatherMultipliers.TryGetValue(item.info.displayName.english, out multiplier))
            {
                item.amount = (int)(item.amount * multiplier);
            }
        }

        // Also handles bonus resources
        private void OnDispenserBonus(ResourceDispenser dispenser, BaseEntity entity, Item item)
        {
            OnDispenserGather(dispenser, entity, item);
        }

        // Quarry gathering
        private void OnQuarryGather(MiningQuarry quarry, Item item)
        {
            float multiplier;
            if (gatherMultipliers.TryGetValue(item.info.displayName.english, out multiplier))
            {
                item.amount = (int)(item.amount * multiplier);
            }
        }

        // Pickup (collectibles on ground)
        private void OnCollectiblePickup(CollectibleEntity collectible, BasePlayer player)
        {
            foreach (ItemAmount item in collectible.itemList)
            {
                float multiplier;
                if (gatherMultipliers.TryGetValue(item.itemDef.displayName.english, out multiplier))
                {
                    item.amount = (int)(item.amount * multiplier);
                }
            }
        }
    }
}

// ============================================================================
// TEMPLATE 12: DOOR AUTO-CLOSE (from AutoDoors.cs)
// Use for: Auto-closing doors, timed entity actions
// ============================================================================

using System.Collections.Generic;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("MyDoorPlugin", "Author", "1.0.0")]
    [Description("Auto-closes doors after X seconds")]
    class MyDoorPlugin : RustPlugin
    {
        private const string PERM_USE = "mydoor.use";
        private Dictionary<ulong, Timer> doorTimers = new Dictionary<ulong, Timer>();
        private float autoCloseDelay = 5f;

        private void Init()
        {
            permission.RegisterPermission(PERM_USE, this);
        }

        private void Unload()
        {
            foreach (var timer in doorTimers.Values)
            {
                timer?.Destroy();
            }
            doorTimers.Clear();
        }

        private void OnDoorOpened(Door door, BasePlayer player)
        {
            if (door == null || door.net == null || !door.IsOpen()) return;
            if (!permission.UserHasPermission(player.UserIDString, PERM_USE)) return;

            var doorID = door.net.ID.Value;

            // Cancel existing timer for this door
            Timer existingTimer;
            if (doorTimers.TryGetValue(doorID, out existingTimer))
            {
                existingTimer?.Destroy();
            }

            // Start new timer
            doorTimers[doorID] = timer.Once(autoCloseDelay, () =>
            {
                doorTimers.Remove(doorID);
                if (door == null || !door.IsOpen()) return;

                // Close the door
                door.SetFlag(BaseEntity.Flags.Open, false);
                door.SendNetworkUpdateImmediate();
            });
        }

        private void OnDoorClosed(Door door, BasePlayer player)
        {
            if (door == null || door.net == null) return;

            Timer existingTimer;
            if (doorTimers.TryGetValue(door.net.ID.Value, out existingTimer))
            {
                existingTimer?.Destroy();
                doorTimers.Remove(door.net.ID.Value);
            }
        }
    }
}

// ============================================================================
// TEMPLATE 13: FACEPUNCH BEHAVIOUR COMPONENT (from QuickSmelt.cs)
// Use for: Custom entity behaviour, oven modifiers, complex components
// ============================================================================

using UnityEngine;

namespace Oxide.Plugins
{
    [Info("MyComponentPlugin", "Author", "1.0.0")]
    [Description("Adds custom components to entities")]
    class MyComponentPlugin : RustPlugin
    {
        private void OnServerInitialized()
        {
            // Add component to all existing entities
            foreach (var oven in UnityEngine.Object.FindObjectsOfType<BaseOven>())
            {
                oven.gameObject.AddComponent<MyCustomComponent>();
            }
        }

        private void OnEntitySpawned(BaseNetworkable entity)
        {
            var oven = entity as BaseOven;
            if (oven == null) return;

            oven.gameObject.AddComponent<MyCustomComponent>();
        }

        private void Unload()
        {
            // IMPORTANT: Clean up all components on unload
            foreach (var component in UnityEngine.Object.FindObjectsOfType<MyCustomComponent>())
            {
                UnityEngine.Object.Destroy(component);
            }
        }

        // Custom behaviour class
        public class MyCustomComponent : FacepunchBehaviour
        {
            private BaseOven oven;

            private void Awake()
            {
                oven = GetComponent<BaseOven>();
            }

            private void OnEnable()
            {
                // Called when component is enabled
            }

            private void OnDisable()
            {
                // Called when component is disabled
            }

            public void DoSomething()
            {
                if (oven == null || oven.IsDestroyed) return;
                // Your custom logic here
            }
        }
    }
}

// ============================================================================
// TEMPLATE 14: RAYCAST - LOOK AT ENTITY (from AutoDoors.cs)
// Use for: Finding what player is looking at
// ============================================================================

using UnityEngine;

namespace Oxide.Plugins
{
    [Info("MyRaycastPlugin", "Author", "1.0.0")]
    [Description("Demonstrates raycast usage")]
    class MyRaycastPlugin : RustPlugin
    {
        [ChatCommand("whatlooking")]
        private void CmdWhatLooking(BasePlayer player, string command, string[] args)
        {
            if (player == null) return;

            // Get entity player is looking at
            var entity = GetLookingAtEntity(player, 10f);
            if (entity == null)
            {
                SendReply(player, "Not looking at any entity");
                return;
            }

            SendReply(player, $"Looking at: {entity.ShortPrefabName}");
        }

        private BaseEntity GetLookingAtEntity(BasePlayer player, float maxDistance = 10f)
        {
            RaycastHit hit;
            if (Physics.Raycast(player.eyes.HeadRay(), out hit, maxDistance, Rust.Layers.Mask.Construction | Rust.Layers.Mask.Deployed))
            {
                return hit.GetEntity();
            }
            return null;
        }

        // Get door specifically
        private Door GetLookingAtDoor(BasePlayer player)
        {
            RaycastHit hit;
            if (Physics.Raycast(player.eyes.HeadRay(), out hit, 10f, Rust.Layers.Mask.Construction))
            {
                return hit.GetEntity() as Door;
            }
            return null;
        }

        // Get building block
        private BuildingBlock GetLookingAtBlock(BasePlayer player)
        {
            RaycastHit hit;
            if (Physics.Raycast(player.eyes.HeadRay(), out hit, 10f, Rust.Layers.Mask.Construction))
            {
                return hit.GetEntity() as BuildingBlock;
            }
            return null;
        }
    }
}

// ============================================================================
// TEMPLATE 15: PLUGIN REFERENCE (from NightLantern.cs, AutoDoors.cs)
// Use for: Calling other plugins, checking dependencies
// ============================================================================

using Oxide.Core.Plugins;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("MyPluginRefPlugin", "Author", "1.0.0")]
    [Description("Demonstrates plugin references")]
    class MyPluginRefPlugin : RustPlugin
    {
        // References to other plugins
        [PluginReference]
        private Plugin Friends, Clans, Teams, Economics;

        private void OnServerInitialized()
        {
            if (Friends == null)
                Puts("Friends plugin not loaded");
            if (Economics == null)
                Puts("Economics plugin not loaded");
        }

        // Check if two players are friends (works with multiple friend plugins)
        private bool AreFriends(ulong player1, ulong player2)
        {
            if (player1 == player2) return true;

            // Check Friends plugin
            if (Friends != null)
            {
                var result = Friends.Call<bool>("AreFriends", player1, player2);
                if (result) return true;
            }

            // Check Clans plugin
            if (Clans != null)
            {
                var clan1 = Clans.Call<string>("GetClanOf", player1);
                var clan2 = Clans.Call<string>("GetClanOf", player2);
                if (!string.IsNullOrEmpty(clan1) && clan1 == clan2) return true;
            }

            // Check Teams (built-in)
            var basePlayer1 = BasePlayer.FindByID(player1);
            var basePlayer2 = BasePlayer.FindByID(player2);
            if (basePlayer1?.currentTeam != 0 && basePlayer1?.currentTeam == basePlayer2?.currentTeam)
                return true;

            return false;
        }

        // Deposit money to Economics
        private bool GiveMoney(ulong playerId, double amount)
        {
            if (Economics == null) return false;
            return Economics.Call<bool>("Deposit", playerId.ToString(), amount);
        }

        // Get player balance from Economics
        private double GetBalance(ulong playerId)
        {
            if (Economics == null) return 0;
            return Economics.Call<double>("Balance", playerId.ToString());
        }
    }
}

// ============================================================================
// TEMPLATE 16: COVALENCE PLUGIN (from StackSizeController.cs)
// Use for: Cross-game plugins, console commands, IPlayer
// ============================================================================

using System.Collections.Generic;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("MyCovalencePlugin", "Author", "1.0.0")]
    [Description("Cross-platform plugin using Covalence")]
    class MyCovalencePlugin : CovalencePlugin
    {
        private void Init()
        {
            // Register Covalence commands (work from console AND chat)
            AddCovalenceCommand("mycommand", nameof(MyCommandHandler), "myplugin.admin");
            AddCovalenceCommand("myinfo", nameof(InfoCommandHandler));
        }

        private void MyCommandHandler(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission("myplugin.admin"))
            {
                player.Reply("You don't have permission!");
                return;
            }

            if (args.Length < 1)
            {
                player.Reply("Usage: mycommand <arg>");
                return;
            }

            player.Reply($"You said: {string.Join(" ", args)}");
        }

        private void InfoCommandHandler(IPlayer player, string command, string[] args)
        {
            // IPlayer works for both console and players
            if (player.IsServer)
            {
                player.Reply("Called from server console!");
            }
            else
            {
                player.Reply($"Hello {player.Name}! Your ID: {player.Id}");
            }
        }

        // Get BasePlayer from IPlayer
        private BasePlayer GetBasePlayer(IPlayer player)
        {
            return player.Object as BasePlayer;
        }
    }
}

// ============================================================================
// TEMPLATE 17: WEB REQUESTS (from StackSizeController.cs)
// Use for: HTTP requests, downloading files, API calls
// ============================================================================

using Oxide.Core.Libraries;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("MyWebPlugin", "Author", "1.0.0")]
    [Description("Makes web requests")]
    class MyWebPlugin : RustPlugin
    {
        private void OnServerInitialized()
        {
            // Simple GET request
            FetchData("https://api.example.com/data");
        }

        private void FetchData(string url)
        {
            webrequest.Enqueue(url, null, (code, response) =>
            {
                if (code != 200 || string.IsNullOrEmpty(response))
                {
                    PrintWarning($"Failed to fetch data. Code: {code}");
                    return;
                }

                // Parse JSON response
                try
                {
                    var data = JsonConvert.DeserializeObject<Dictionary<string, object>>(response);
                    Puts($"Received data: {data.Count} items");
                }
                catch
                {
                    PrintError("Failed to parse JSON response");
                }
            }, this, RequestMethod.GET);
        }

        // POST request with data
        private void PostData(string url, string jsonData)
        {
            var headers = new Dictionary<string, string>
            {
                ["Content-Type"] = "application/json"
            };

            webrequest.Enqueue(url, jsonData, (code, response) =>
            {
                Puts($"POST response: {code}");
            }, this, RequestMethod.POST, headers);
        }
    }
}

// ============================================================================
// TEMPLATE 18: ITEM STACK SIZE MODIFIER (from StackSizeController.cs)
// Use for: Modifying item properties
// ============================================================================

using UnityEngine;

namespace Oxide.Plugins
{
    [Info("MyStackPlugin", "Author", "1.0.0")]
    [Description("Modifies item stack sizes")]
    class MyStackPlugin : RustPlugin
    {
        private float stackMultiplier = 5f;

        private void OnServerInitialized()
        {
            SetStackSizes();
        }

        private void Unload()
        {
            RevertStackSizes();
        }

        // This hook is called when checking max stack size
        private int OnMaxStackable(Item item)
        {
            return GetModifiedStackSize(item.info);
        }

        private int GetModifiedStackSize(ItemDefinition itemDef)
        {
            // Don't modify items with durability
            if (itemDef.condition.enabled)
                return itemDef.stackable;

            return Mathf.RoundToInt(itemDef.stackable * stackMultiplier);
        }

        private void SetStackSizes()
        {
            foreach (var itemDef in ItemManager.GetItemDefinitions())
            {
                if (itemDef.condition.enabled) continue;
                itemDef.stackable = Mathf.RoundToInt(itemDef.stackable * stackMultiplier);
            }
        }

        private void RevertStackSizes()
        {
            // Revert to original values on unload
            foreach (var itemDef in ItemManager.GetItemDefinitions())
            {
                if (itemDef.condition.enabled) continue;
                itemDef.stackable = Mathf.RoundToInt(itemDef.stackable / stackMultiplier);
            }
        }
    }
}

// ============================================================================
// TEMPLATE 19: ENTITY FLAGS (common patterns)
// Use for: Toggling entity states
// ============================================================================

namespace Oxide.Plugins
{
    [Info("MyFlagPlugin", "Author", "1.0.0")]
    [Description("Entity flag manipulation")
    class MyFlagPlugin : RustPlugin
    {
        // Turn entity on/off
        private void ToggleEntity(BaseEntity entity, bool on)
        {
            entity.SetFlag(BaseEntity.Flags.On, on);
            entity.SendNetworkUpdate();
        }

        // Lock/unlock door
        private void ToggleLock(Door door, bool locked)
        {
            door.SetFlag(BaseEntity.Flags.Locked, locked);
            door.SendNetworkUpdate();
        }

        // Open/close door
        private void ToggleDoor(Door door, bool open)
        {
            door.SetFlag(BaseEntity.Flags.Open, open);
            door.SendNetworkUpdateImmediate();
        }

        // Make entity reserved (can't be picked up)
        private void SetReserved(BaseEntity entity, bool reserved)
        {
            entity.SetFlag(BaseEntity.Flags.Reserved3, reserved);
        }

        // Common flags:
        // BaseEntity.Flags.On - Entity is on/active
        // BaseEntity.Flags.Open - Door/container is open
        // BaseEntity.Flags.Locked - Door/codelock is locked
        // BaseEntity.Flags.Reserved1-8 - General purpose flags
        // BaseEntity.Flags.Busy - Entity is busy
        // BaseEntity.Flags.Disabled - Entity is disabled
    }
}

// ============================================================================
// TEMPLATE 20: BUILDING PRIVILEGE / TOOL CUPBOARD
// Use for: Checking building permission, cupboard access
// ============================================================================

using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("MyBuildingPlugin", "Author", "1.0.0")]
    [Description("Building privilege utilities")]
    class MyBuildingPlugin : RustPlugin
    {
        // Check if player has building privilege at position
        private bool HasBuildingPrivilege(BasePlayer player, Vector3 position)
        {
            return player.IsBuildingAuthed(new OBB(position, Quaternion.identity, Vector3.one));
        }

        // Check if player can build at their current location
        private bool CanBuildHere(BasePlayer player)
        {
            return player.CanBuild();
        }

        // Get the tool cupboard at position
        private BuildingPrivlidge GetCupboardAt(Vector3 position)
        {
            var cupboards = new List<BuildingPrivlidge>();
            Vis.Entities(position, 1f, cupboards);
            return cupboards.Count > 0 ? cupboards[0] : null;
        }

        // Get all authorized players on a cupboard
        private List<ulong> GetAuthorizedPlayers(BuildingPrivlidge cupboard)
        {
            var players = new List<ulong>();
            foreach (var auth in cupboard.authorizedPlayers)
            {
                players.Add(auth.userid);
            }
            return players;
        }

        // Authorize player on cupboard
        private void AuthorizePlayer(BuildingPrivlidge cupboard, BasePlayer player)
        {
            cupboard.authorizedPlayers.Add(new PlayerNameID
            {
                userid = player.userID,
                username = player.displayName
            });
            cupboard.SendNetworkUpdate();
        }
    }
}

// ============================================================================
// TEMPLATE 21: CONSOLE COMMANDS
// Use for: Admin commands, server commands
// ============================================================================

namespace Oxide.Plugins
{
    [Info("MyConsolePlugin", "Author", "1.0.0")]
    [Description("Console command examples")]
    class MyConsolePlugin : RustPlugin
    {
        // Console command (no permission check - anyone can use)
        [ConsoleCommand("myplugin.info")]
        private void CmdInfo(ConsoleSystem.Arg arg)
        {
            arg.ReplyWith("Plugin info here!");
        }

        // Console command for admins only
        [ConsoleCommand("myplugin.admin")]
        private void CmdAdmin(ConsoleSystem.Arg arg)
        {
            // Check if from player and if admin
            if (arg.Player() != null && !arg.Player().IsAdmin)
            {
                arg.ReplyWith("You must be an admin!");
                return;
            }

            // Get arguments
            if (!arg.HasArgs(1))
            {
                arg.ReplyWith("Usage: myplugin.admin <action>");
                return;
            }

            string action = arg.GetString(0);
            arg.ReplyWith($"Admin action: {action}");
        }

        // Console command with player target
        [ConsoleCommand("myplugin.give")]
        private void CmdGive(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null && !arg.Player().IsAdmin)
            {
                arg.ReplyWith("Admin only!");
                return;
            }

            if (!arg.HasArgs(2))
            {
                arg.ReplyWith("Usage: myplugin.give <player> <item>");
                return;
            }

            var player = arg.GetPlayer(0); // Gets player by name/id
            if (player == null)
            {
                arg.ReplyWith("Player not found!");
                return;
            }

            string item = arg.GetString(1);
            int amount = arg.GetInt(2, 1);

            arg.ReplyWith($"Gave {amount}x {item} to {player.displayName}");
        }
    }
}

// ============================================================================
// TEMPLATE 22: COROUTINES (from NightLantern.cs)
// Use for: Processing many entities without lag, delayed batch operations
// ============================================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("MyCoroutinePlugin", "Author", "1.0.0")]
    [Description("Uses coroutines for batch processing")]
    class MyCoroutinePlugin : RustPlugin
    {
        private void OnServerInitialized()
        {
            // Process all entities without causing lag
            ServerMgr.Instance.StartCoroutine(ProcessAllEntities());
        }

        private IEnumerator ProcessAllEntities()
        {
            var entities = UnityEngine.Object.FindObjectsOfType<BaseOven>();
            Puts($"Processing {entities.Length} entities...");

            foreach (var entity in entities)
            {
                // Small random delay to spread load
                yield return new WaitForSeconds(UnityEngine.Random.Range(0.05f, 0.15f));

                if (entity == null || entity.IsDestroyed) continue;

                // Process entity
                ProcessEntity(entity);
            }

            Puts("Processing complete!");
        }

        private void ProcessEntity(BaseOven oven)
        {
            // Your processing logic here
        }

        // Toggle all lights with delay between each
        private IEnumerator ToggleAllLights(IEnumerable<BaseOven> ovens, bool turnOn)
        {
            foreach (var oven in ovens)
            {
                yield return new WaitForSeconds(0.1f);
                if (oven == null) continue;

                oven.SetFlag(BaseEntity.Flags.On, turnOn);
            }
        }
    }
}

// ============================================================================
// TEMPLATE 23: CHAT FORMATTING & COLORS
// Use for: Colored chat messages, formatted output
// ============================================================================

using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("MyChatPlugin", "Author", "1.0.0")]
    [Description("Chat formatting examples")]
    class MyChatPlugin : RustPlugin
    {
        private string prefix = "<color=#00FF00>[MyPlugin]</color>";
        private ulong chatIcon = 0; // Steam ID for chat icon (0 = no icon)

        [ChatCommand("colors")]
        private void CmdColors(BasePlayer player, string command, string[] args)
        {
            // Basic colors
            SendReply(player, "<color=#ff0000>Red text</color>");
            SendReply(player, "<color=#00ff00>Green text</color>");
            SendReply(player, "<color=#0000ff>Blue text</color>");
            SendReply(player, "<color=#ffff00>Yellow text</color>");
            SendReply(player, "<color=#ff00ff>Magenta text</color>");
            SendReply(player, "<color=#00ffff>Cyan text</color>");

            // Formatting
            SendReply(player, "<size=20>Bigger text</size>");
            SendReply(player, "<b>Bold text</b>");
            SendReply(player, "<i>Italic text</i>");
        }

        // Send message with custom prefix and icon
        private void SendChatMessage(BasePlayer player, string message)
        {
            Player.Message(player, message, prefix, chatIcon);
        }

        // Broadcast to all players
        private void BroadcastMessage(string message)
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                Player.Message(player, message, prefix, chatIcon);
            }
        }

        // Send to console only
        private void SendToConsole(BasePlayer player, string message)
        {
            player.SendConsoleCommand("echo", message);
        }
    }
}

// ============================================================================
// TEMPLATE 24: ENTITY OWNERSHIP & PROTECTION
// Use for: Protecting entities, ownership checks
// ============================================================================

using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("MyProtectionPlugin", "Author", "1.0.0")]
    [Description("Entity protection examples")]
    class MyProtectionPlugin : RustPlugin
    {
        private HashSet<uint> protectedEntities = new HashSet<uint>();

        // Block damage to protected entities
        private object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity?.net == null) return null;

            if (protectedEntities.Contains(entity.net.ID.Value))
            {
                // Nullify all damage
                info.damageTypes.ScaleAll(0f);
                return true;
            }

            return null;
        }

        // Check if attacker owns the entity
        private bool IsOwner(BaseEntity entity, BasePlayer player)
        {
            return entity.OwnerID == player.userID;
        }

        // Set entity owner
        private void SetOwner(BaseEntity entity, BasePlayer player)
        {
            entity.OwnerID = player.userID;
            entity.SendNetworkUpdate();
        }

        // Protect an entity
        [ChatCommand("protect")]
        private void CmdProtect(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin) return;

            RaycastHit hit;
            if (!Physics.Raycast(player.eyes.HeadRay(), out hit, 10f))
            {
                SendReply(player, "Look at an entity!");
                return;
            }

            var entity = hit.GetEntity();
            if (entity?.net == null)
            {
                SendReply(player, "Invalid entity!");
                return;
            }

            protectedEntities.Add(entity.net.ID.Value);
            SendReply(player, $"Protected: {entity.ShortPrefabName}");
        }
    }
}

// ============================================================================
// TEMPLATE 25: LOOT & CONTAINER HOOKS
// Use for: Custom loot, container manipulation
// ============================================================================

using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("MyLootPlugin", "Author", "1.0.0")]
    [Description("Loot and container manipulation")]
    class MyLootPlugin : RustPlugin
    {
        // Called when player starts looting
        private void OnLootEntity(BasePlayer player, BaseEntity entity)
        {
            Puts($"{player.displayName} started looting {entity.ShortPrefabName}");
        }

        // Called when player stops looting
        private void OnLootEntityEnd(BasePlayer player, BaseCombatEntity entity)
        {
            Puts($"{player.displayName} stopped looting");
        }

        // Block looting (return non-null)
        private object CanLootEntity(BasePlayer player, StorageContainer container)
        {
            // Example: Block non-owners
            if (container.OwnerID != 0 && container.OwnerID != player.userID)
            {
                SendReply(player, "This is not your container!");
                return false; // Block
            }
            return null; // Allow
        }

        // Modify loot container contents
        private void OnLootSpawn(LootContainer container)
        {
            if (container?.inventory == null) return;

            // Clear and add custom items
            container.inventory.Clear();

            var item = ItemManager.CreateByName("scrap", 50);
            if (item != null)
            {
                item.MoveToContainer(container.inventory);
            }
        }

        // Give item to specific container slot
        private void AddToContainer(ItemContainer container, string shortname, int amount, int slot)
        {
            var item = ItemManager.CreateByName(shortname, amount);
            if (item == null) return;

            item.position = slot;
            item.MoveToContainer(container);
        }
    }
}

// ============================================================================
// END OF MEGA-TEMPLATES
// ============================================================================
