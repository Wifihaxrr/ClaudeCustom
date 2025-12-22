# OXIDE ADVANCED CODE TEMPLATES

**Context-aware templates for complex Oxide plugin scenarios.**

---

## TEMPLATE: NPC AI SYSTEM

```csharp
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.AI;

namespace Oxide.Plugins
{
    [Info("NPCSystem", "Author", "1.0.0")]
    [Description("Custom NPC AI system")]
    class NPCSystem : RustPlugin
    {
        private Configuration config;
        private HashSet<NetworkableId> spawnedNPCs = new();
        
        #region Configuration
        private class Configuration
        {
            [JsonProperty("NPC Prefab")]
            public string Prefab = "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_heavy.prefab";
            
            [JsonProperty("Health")]
            public float Health = 200f;
            
            [JsonProperty("Roam Range")]
            public float RoamRange = 20f;
            
            [JsonProperty("Aggro Range")]
            public float AggroRange = 30f;
        }
        
        protected override void LoadDefaultConfig() => config = new Configuration();
        protected override void LoadConfig()
        {
            base.LoadConfig();
            try { config = Config.ReadObject<Configuration>() ?? new Configuration(); }
            catch { LoadDefaultConfig(); }
            SaveConfig();
        }
        protected override void SaveConfig() => Config.WriteObject(config, true);
        #endregion

        #region Lifecycle
        private void Unload()
        {
            foreach (var id in spawnedNPCs.ToList())
            {
                var entity = BaseNetworkable.serverEntities.Find(id);
                entity?.Kill();
            }
            spawnedNPCs.Clear();
        }
        
        private void OnEntityKill(BaseNetworkable entity)
        {
            if (entity?.net != null)
                spawnedNPCs.Remove(entity.net.ID);
        }
        #endregion

        #region NPC Spawning
        private ScientistNPC SpawnNPC(Vector3 position)
        {
            var npc = GameManager.server.CreateEntity(config.Prefab, position) as ScientistNPC;
            if (npc == null) return null;
            
            npc.Spawn();
            npc.SetMaxHealth(config.Health);
            npc.SetHealth(config.Health);
            
            spawnedNPCs.Add(npc.net.ID);
            
            SetupNavigation(npc);
            
            return npc;
        }
        
        private void SetupNavigation(ScientistNPC npc)
        {
            var nav = npc.GetComponent<NavMeshAgent>();
            if (nav != null)
            {
                nav.stoppingDistance = 2f;
                nav.speed = 3.5f;
            }
        }
        #endregion

        #region Commands
        [ChatCommand("spawnnpc")]
        private void CmdSpawnNPC(BasePlayer player, string cmd, string[] args)
        {
            if (!player.IsAdmin) return;
            
            var pos = player.transform.position + player.eyes.HeadForward() * 3f;
            var npc = SpawnNPC(pos);
            
            if (npc != null)
                SendReply(player, $"Spawned NPC at {pos}");
            else
                SendReply(player, "Failed to spawn NPC");
        }
        #endregion
    }
}
```

---

## TEMPLATE: ECONOMY INTEGRATION

```csharp
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("EconomyShop", "Author", "1.0.0")]
    [Description("Shop with economy integration")]
    class EconomyShop : RustPlugin
    {
        [PluginReference] private Plugin Economics;
        [PluginReference] private Plugin ServerRewards;
        
        private Configuration config;
        
        private class Configuration
        {
            [JsonProperty("Use Economics")]
            public bool UseEconomics = true;
            
            [JsonProperty("Use ServerRewards")]
            public bool UseServerRewards = false;
            
            [JsonProperty("Shop Items")]
            public Dictionary<string, ShopItem> Items = new()
            {
                ["rifle.ak"] = new ShopItem { Price = 1000, DisplayName = "AK-47" },
                ["largemedkit"] = new ShopItem { Price = 100, DisplayName = "Large Medkit" }
            };
        }
        
        private class ShopItem
        {
            public double Price;
            public string DisplayName;
        }
        
        protected override void LoadDefaultConfig() => config = new Configuration();
        protected override void LoadConfig()
        {
            base.LoadConfig();
            try { config = Config.ReadObject<Configuration>() ?? new Configuration(); }
            catch { LoadDefaultConfig(); }
            SaveConfig();
        }
        protected override void SaveConfig() => Config.WriteObject(config, true);
        
        #region Economy Methods
        private double GetBalance(ulong playerId)
        {
            var id = playerId.ToString();
            
            if (config.UseEconomics && Economics != null)
                return Economics.Call<double>("Balance", id);
            
            if (config.UseServerRewards && ServerRewards != null)
                return ServerRewards.Call<int>("CheckPoints", id);
            
            return 0;
        }
        
        private bool TryWithdraw(ulong playerId, double amount)
        {
            var id = playerId.ToString();
            
            if (config.UseEconomics && Economics != null)
                return Economics.Call<bool>("Withdraw", id, amount);
            
            if (config.UseServerRewards && ServerRewards != null)
                return ServerRewards.Call<object>("TakePoints", id, (int)amount) != null;
            
            return false;
        }
        
        private bool TryDeposit(ulong playerId, double amount)
        {
            var id = playerId.ToString();
            
            if (config.UseEconomics && Economics != null)
                return Economics.Call<bool>("Deposit", id, amount);
            
            if (config.UseServerRewards && ServerRewards != null)
                return ServerRewards.Call<object>("AddPoints", id, (int)amount) != null;
            
            return false;
        }
        #endregion
        
        #region Commands
        [ChatCommand("buy")]
        private void CmdBuy(BasePlayer player, string cmd, string[] args)
        {
            if (args.Length < 1)
            {
                SendReply(player, "Usage: /buy <item>");
                return;
            }
            
            var itemName = args[0].ToLower();
            if (!config.Items.TryGetValue(itemName, out var shopItem))
            {
                SendReply(player, "Item not found in shop");
                return;
            }
            
            var balance = GetBalance(player.userID);
            if (balance < shopItem.Price)
            {
                SendReply(player, $"Not enough money. Need: {shopItem.Price}, Have: {balance}");
                return;
            }
            
            if (!TryWithdraw(player.userID, shopItem.Price))
            {
                SendReply(player, "Failed to process payment");
                return;
            }
            
            var item = ItemManager.CreateByName(itemName, 1);
            if (item != null)
            {
                player.GiveItem(item);
                SendReply(player, $"Purchased {shopItem.DisplayName} for {shopItem.Price}");
            }
        }
        #endregion
    }
}
```

---

## TEMPLATE: ZONE SYSTEM

```csharp
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("ZoneFeature", "Author", "1.0.0")]
    [Description("Zone-based feature system")]
    class ZoneFeature : RustPlugin
    {
        [PluginReference] private Plugin ZoneManager;
        
        private Configuration config;
        private Dictionary<ulong, string> playerZones = new();
        
        private class Configuration
        {
            [JsonProperty("Protected Zones")]
            public List<string> ProtectedZones = new() { "safezone", "pvezone" };
            
            [JsonProperty("Bonus Zones")]
            public Dictionary<string, float> BonusZones = new()
            {
                ["vipzone"] = 2.0f,
                ["farmzone"] = 1.5f
            };
        }
        
        protected override void LoadDefaultConfig() => config = new Configuration();
        protected override void LoadConfig()
        {
            base.LoadConfig();
            try { config = Config.ReadObject<Configuration>() ?? new Configuration(); }
            catch { LoadDefaultConfig(); }
            SaveConfig();
        }
        protected override void SaveConfig() => Config.WriteObject(config, true);
        
        #region Zone Hooks (from ZoneManager)
        private void OnEnterZone(string zoneId, BasePlayer player)
        {
            playerZones[player.userID] = zoneId;
            
            if (config.ProtectedZones.Contains(zoneId))
                SendReply(player, "You entered a protected zone");
        }
        
        private void OnExitZone(string zoneId, BasePlayer player)
        {
            if (playerZones.TryGetValue(player.userID, out var currentZone) && currentZone == zoneId)
                playerZones.Remove(player.userID);
        }
        #endregion
        
        #region Zone Checks
        private bool IsInZone(BasePlayer player, string zoneId)
        {
            if (ZoneManager == null) return false;
            return ZoneManager.Call<bool>("IsPlayerInZone", zoneId, player);
        }
        
        private bool IsInProtectedZone(BasePlayer player)
        {
            if (!playerZones.TryGetValue(player.userID, out var zone))
                return false;
            return config.ProtectedZones.Contains(zone);
        }
        
        private float GetZoneMultiplier(BasePlayer player)
        {
            if (!playerZones.TryGetValue(player.userID, out var zone))
                return 1.0f;
            return config.BonusZones.GetValueOrDefault(zone, 1.0f);
        }
        #endregion
        
        #region Example Usage
        private object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            var victim = entity as BasePlayer;
            if (victim == null) return null;
            
            if (IsInProtectedZone(victim))
            {
                info.damageTypes.ScaleAll(0f);
                return true;
            }
            
            return null;
        }
        
        private void OnDispenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item)
        {
            var player = entity.ToPlayer();
            if (player == null) return;
            
            float multiplier = GetZoneMultiplier(player);
            item.amount = (int)(item.amount * multiplier);
        }
        #endregion
    }
}
```

---

## TEMPLATE: VEHICLE MANAGEMENT

```csharp
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("VehicleManager", "Author", "1.0.0")]
    [Description("Vehicle spawning and management")]
    class VehicleManager : RustPlugin
    {
        private Configuration config;
        private Dictionary<ulong, NetworkableId> playerVehicles = new();
        
        private class Configuration
        {
            [JsonProperty("Vehicle Prefabs")]
            public Dictionary<string, string> Prefabs = new()
            {
                ["mini"] = "assets/content/vehicles/minicopter/minicopter.entity.prefab",
                ["scrap"] = "assets/content/vehicles/scrap heli carrier/scraptransporthelicopter.prefab",
                ["rhib"] = "assets/content/vehicles/boats/rhib/rhib.prefab"
            };
            
            [JsonProperty("Spawn Cooldown (seconds)")]
            public float Cooldown = 300f;
            
            [JsonProperty("Max Fuel on Spawn")]
            public int MaxFuel = 100;
        }
        
        protected override void LoadDefaultConfig() => config = new Configuration();
        protected override void LoadConfig()
        {
            base.LoadConfig();
            try { config = Config.ReadObject<Configuration>() ?? new Configuration(); }
            catch { LoadDefaultConfig(); }
            SaveConfig();
        }
        protected override void SaveConfig() => Config.WriteObject(config, true);
        
        private Dictionary<ulong, float> cooldowns = new();
        
        private void Unload()
        {
            // Don't destroy player vehicles on unload (they own them)
            playerVehicles.Clear();
        }
        
        private void OnEntityKill(BaseNetworkable entity)
        {
            if (entity?.net == null) return;
            
            // Find and remove from tracking
            foreach (var kvp in playerVehicles.ToList())
            {
                if (kvp.Value == entity.net.ID)
                {
                    playerVehicles.Remove(kvp.Key);
                    break;
                }
            }
        }
        
        [ChatCommand("vehicle")]
        private void CmdVehicle(BasePlayer player, string cmd, string[] args)
        {
            if (args.Length < 1)
            {
                SendReply(player, "Usage: /vehicle <mini|scrap|rhib>");
                return;
            }
            
            var vehicleType = args[0].ToLower();
            if (!config.Prefabs.TryGetValue(vehicleType, out var prefab))
            {
                SendReply(player, "Unknown vehicle type");
                return;
            }
            
            // Check cooldown
            if (cooldowns.TryGetValue(player.userID, out var lastSpawn))
            {
                float remaining = config.Cooldown - (Time.realtimeSinceStartup - lastSpawn);
                if (remaining > 0)
                {
                    SendReply(player, $"Cooldown: {remaining:F0}s remaining");
                    return;
                }
            }
            
            // Destroy existing vehicle
            if (playerVehicles.TryGetValue(player.userID, out var existingId))
            {
                var existing = BaseNetworkable.serverEntities.Find(existingId);
                existing?.Kill();
            }
            
            // Spawn new vehicle
            var pos = player.transform.position + player.eyes.HeadForward() * 5f;
            pos.y += 2f;
            
            var vehicle = GameManager.server.CreateEntity(prefab, pos) as BaseVehicle;
            if (vehicle == null)
            {
                SendReply(player, "Failed to spawn vehicle");
                return;
            }
            
            vehicle.OwnerID = player.userID;
            vehicle.Spawn();
            
            // Add fuel
            var fuel = vehicle.GetFuelSystem();
            if (fuel != null)
            {
                var fuelItem = ItemManager.CreateByName("lowgradefuel", config.MaxFuel);
                if (!fuel.GetFuelContainer().inventory.GiveItem(fuelItem))
                    fuelItem.Remove();
            }
            
            playerVehicles[player.userID] = vehicle.net.ID;
            cooldowns[player.userID] = Time.realtimeSinceStartup;
            
            SendReply(player, $"Spawned {vehicleType}!");
        }
    }
}
```

---

## TEMPLATE: DATA MIGRATION

```csharp
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("DataMigration", "Author", "2.0.0")]
    [Description("Example of data format migration")]
    class DataMigration : RustPlugin
    {
        private CurrentData data;
        
        // Current data format (v2)
        private class CurrentData
        {
            public int Version = 2;
            public Dictionary<ulong, PlayerDataV2> Players = new();
        }
        
        private class PlayerDataV2
        {
            public string Name;
            public int Score;
            public List<string> Achievements = new();
        }
        
        // Old data format (v1) for migration
        private class LegacyDataV1
        {
            public Dictionary<ulong, int> PlayerScores = new();
        }
        
        private void OnServerInitialized()
        {
            LoadAndMigrateData();
        }
        
        private void LoadAndMigrateData()
        {
            // Try to load current format
            data = Interface.Oxide.DataFileSystem.ReadObject<CurrentData>(Name);
            
            if (data == null)
            {
                // Try to load legacy format
                var legacy = Interface.Oxide.DataFileSystem.ReadObject<LegacyDataV1>($"{Name}_old");
                if (legacy != null && legacy.PlayerScores.Count > 0)
                {
                    MigrateFromV1(legacy);
                    Puts($"Migrated {legacy.PlayerScores.Count} player records from v1 to v2");
                }
                else
                {
                    data = new CurrentData();
                }
            }
            else if (data.Version < 2)
            {
                // Handle partial migration if needed
                data.Version = 2;
            }
            
            SaveData();
        }
        
        private void MigrateFromV1(LegacyDataV1 legacy)
        {
            data = new CurrentData { Version = 2 };
            
            foreach (var kvp in legacy.PlayerScores)
            {
                data.Players[kvp.Key] = new PlayerDataV2
                {
                    Name = "Unknown", // Will be updated on next connect
                    Score = kvp.Value,
                    Achievements = new List<string>()
                };
            }
            
            // Backup old data
            Interface.Oxide.DataFileSystem.WriteObject($"{Name}_backup_v1", legacy);
        }
        
        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(Name, data);
        private void OnServerSave() => SaveData();
    }
}
```
