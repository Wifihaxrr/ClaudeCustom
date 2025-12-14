# RUST OXIDE COMPLETE WORKING EXAMPLES

**THESE ARE REAL WORKING PLUGINS. COPY PATTERNS EXACTLY.**

---

## CRITICAL: CLASS CONFUSION TO AVOID

### ResourceDispenser vs ResourceContainer - THEY ARE DIFFERENT!

```csharp
// ResourceDispenser - For getting resources from mining nodes (OreResourceEntity)
var dispenser = ore.GetComponent<ResourceDispenser>();
foreach (var item in dispenser.containedItems)  // containedItems is correct!
{
    string shortname = item.itemDef.shortname;
    float amount = item.amount;
}

// ResourceContainer - A lootable container (like barrels), used in hooks
// Hook signature:
object CanLootEntity(ResourceContainer container, BasePlayer player)
// ResourceContainer does NOT have containedItems or resourceSpawnList!
```

### The CORRECT way to gather from OreResourceEntity:
```csharp
private void GatherFromNode(BasePlayer player, OreResourceEntity ore)
{
    if (ore == null || player == null) return;
    
    // Get the resource dispenser - THIS IS THE CORRECT CLASS
    var dispenser = ore.GetComponent<ResourceDispenser>();
    if (dispenser == null) return;
    
    // containedItems is the correct property
    foreach (var item in dispenser.containedItems)
    {
        if (item.amount <= 0) continue;
        
        var giveItem = ItemManager.CreateByName(item.itemDef.shortname, (int)item.amount);
        if (giveItem != null)
        {
            player.GiveItem(giveItem);
        }
    }
}
```

---

## EXAMPLE: COMPLETE MININGGUN PLUGIN (WORKING)

This plugin demonstrates: UI, input handling, timers, effects, raycasting, resource gathering.

```csharp
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("MiningGun", "ZombiePVE", "1.0.0")]
    [Description("Custom Mining Gun - Nailgun that shoots beam to mine nodes from distance")]
    public class MiningGun : RustPlugin
    {
        #region Fields
        private Configuration config;
        private Dictionary<ulong, float> lastFireTime = new Dictionary<ulong, float>();
        private HashSet<ulong> playersWithUI = new HashSet<ulong>();
        
        private Dictionary<uint, NodeHitData> nodeHits = new Dictionary<uint, NodeHitData>();
        private Dictionary<ulong, PlayerHeatData> playerHeat = new Dictionary<ulong, PlayerHeatData>();
        private Dictionary<ulong, Timer> autoFireTimers = new Dictionary<ulong, Timer>();
        
        private const string PERM_USE = "mininggun.use";
        private const string PERM_ADMIN = "mininggun.admin";
        private const string UI_PANEL = "MiningGunUI";
        private const string HEAT_UI = "MiningGunHeat";
        
        private class NodeHitData
        {
            public int HitsTaken;
            public int HitsNeeded;
        }
        
        private class PlayerHeatData
        {
            public float Heat;
            public bool Overheated;
            public float LastFireTime;
            public float OverheatEndTime;
        }
        #endregion

        #region Configuration
        private class Configuration
        {
            [JsonProperty("Mining Gun Settings")]
            public MiningGunSettings Gun { get; set; } = new MiningGunSettings();
        }
        
        private class MiningGunSettings
        {
            [JsonProperty("Mining range (meters)")]
            public float Range { get; set; } = 20f;
            
            [JsonProperty("AOE radius (meters)")]
            public float AOERadius { get; set; } = 10f;
            
            [JsonProperty("Minimum hits to break node")]
            public int MinHitsToBreak { get; set; } = 1;
            
            [JsonProperty("Maximum hits to break node")]
            public int MaxHitsToBreak { get; set; } = 6;
            
            [JsonProperty("Custom item name")]
            public string ItemName { get; set; } = "Mining Laser";
            
            [JsonProperty("Custom item skin ID")]
            public ulong SkinID { get; set; } = 0;
            
            [JsonProperty("Require special nailgun")]
            public bool RequireSpecialNailgun { get; set; } = true;
            
            [JsonProperty("Fire rate (shots per second)")]
            public float FireRate { get; set; } = 3f;
            
            [JsonProperty("Heat per shot")]
            public float HeatPerShot { get; set; } = 5f;
            
            [JsonProperty("Heat cooldown per second")]
            public float HeatCooldownRate { get; set; } = 4f;
            
            [JsonProperty("Overheat threshold")]
            public float OverheatThreshold { get; set; } = 100f;
            
            [JsonProperty("Overheat cooldown time")]
            public float OverheatCooldown { get; set; } = 15f;
            
            [JsonProperty("Beam effect prefab")]
            public string BeamEffectPrefab { get; set; } = "assets/bundled/prefabs/fx/impacts/additive/fire.prefab";
            
            [JsonProperty("Impact effect prefab")]
            public string ImpactEffectPrefab { get; set; } = "assets/bundled/prefabs/fx/survey_explosion.prefab";
            
            [JsonProperty("Rock hit sound prefab")]
            public string RockHitSoundPrefab { get; set; } = "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab";
            
            [JsonProperty("Rock break sound prefab")]
            public string RockBreakSoundPrefab { get; set; } = "assets/bundled/prefabs/fx/ore_break.prefab";
        }

        protected override void LoadDefaultConfig()
        {
            config = new Configuration();
            SaveConfig();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) LoadDefaultConfig();
            }
            catch
            {
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig() => Config.WriteObject(config);
        #endregion

        #region Oxide Hooks
        private void OnServerInitialized()
        {
            permission.RegisterPermission(PERM_USE, this);
            permission.RegisterPermission(PERM_ADMIN, this);
            
            foreach (var player in BasePlayer.activePlayerList)
                CheckAndShowUI(player);
        }
        
        private void Unload()
        {
            nodeHits.Clear();
            
            foreach (var t in autoFireTimers.Values)
                t?.Destroy();
            autoFireTimers.Clear();
            
            foreach (var player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, UI_PANEL);
                CuiHelper.DestroyUi(player, HEAT_UI);
            }
            playersWithUI.Clear();
            playerHeat.Clear();
        }
        
        private void OnPlayerDisconnected(BasePlayer player)
        {
            if (player != null)
            {
                playersWithUI.Remove(player.userID);
                lastFireTime.Remove(player.userID);
                playerHeat.Remove(player.userID);
                
                if (autoFireTimers.TryGetValue(player.userID, out Timer t))
                {
                    t?.Destroy();
                    autoFireTimers.Remove(player.userID);
                }
            }
        }
        
        private void OnActiveItemChanged(BasePlayer player, Item oldItem, Item newItem)
        {
            if (player == null) return;
            NextTick(() => CheckAndShowUI(player));
        }
        
        private void CheckAndShowUI(BasePlayer player)
        {
            if (player == null) return;
            
            var heldItem = player.GetActiveItem();
            bool shouldShowUI = false;
            
            if (heldItem != null && heldItem.info.shortname == "pistol.nailgun")
            {
                if (config.Gun.RequireSpecialNailgun)
                {
                    if (!string.IsNullOrEmpty(heldItem.name) && heldItem.name.Contains(config.Gun.ItemName))
                        shouldShowUI = true;
                    if (config.Gun.SkinID > 0 && heldItem.skin == config.Gun.SkinID)
                        shouldShowUI = true;
                }
                else
                {
                    shouldShowUI = true;
                }
            }
            
            if (shouldShowUI && !playersWithUI.Contains(player.userID))
            {
                ShowMiningGunUI(player);
                playersWithUI.Add(player.userID);
            }
            else if (!shouldShowUI && playersWithUI.Contains(player.userID))
            {
                CuiHelper.DestroyUi(player, UI_PANEL);
                playersWithUI.Remove(player.userID);
            }
        }
        
        private void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (player == null || input == null) return;
            
            var heldItem = player.GetActiveItem();
            if (heldItem == null || heldItem.info.shortname != "pistol.nailgun") return;
            
            if (config.Gun.RequireSpecialNailgun)
            {
                bool isMiningGun = false;
                if (!string.IsNullOrEmpty(heldItem.name) && heldItem.name.Contains(config.Gun.ItemName))
                    isMiningGun = true;
                if (config.Gun.SkinID > 0 && heldItem.skin == config.Gun.SkinID)
                    isMiningGun = true;
                if (!isMiningGun) return;
            }
            
            if (!permission.UserHasPermission(player.UserIDString, PERM_USE) && !player.IsAdmin)
                return;
            
            if (input.WasJustPressed(BUTTON.FIRE_PRIMARY))
                StartAutoFire(player);
            else if (input.WasJustReleased(BUTTON.FIRE_PRIMARY))
                StopAutoFire(player);
        }
        
        private void StartAutoFire(BasePlayer player)
        {
            if (autoFireTimers.ContainsKey(player.userID)) return;
            
            if (!playerHeat.TryGetValue(player.userID, out PlayerHeatData heatData))
            {
                heatData = new PlayerHeatData { Heat = 0, Overheated = false };
                playerHeat[player.userID] = heatData;
            }
            
            if (heatData.Overheated)
            {
                SendReply(player, "<color=#ff4444>[!] OVERHEATED!</color>");
                return;
            }
            
            TryFireShot(player);
            
            float fireInterval = 1f / config.Gun.FireRate;
            autoFireTimers[player.userID] = timer.Every(fireInterval, () =>
            {
                if (player == null || !player.IsConnected)
                {
                    StopAutoFire(player);
                    return;
                }
                
                var item = player.GetActiveItem();
                if (item == null || item.info.shortname != "pistol.nailgun")
                {
                    StopAutoFire(player);
                    return;
                }
                
                TryFireShot(player);
            });
        }
        
        private void StopAutoFire(BasePlayer player)
        {
            if (player == null) return;
            
            if (autoFireTimers.TryGetValue(player.userID, out Timer t))
            {
                t?.Destroy();
                autoFireTimers.Remove(player.userID);
            }
        }
        
        private void TryFireShot(BasePlayer player)
        {
            if (!playerHeat.TryGetValue(player.userID, out PlayerHeatData heatData))
            {
                heatData = new PlayerHeatData { Heat = 0, Overheated = false };
                playerHeat[player.userID] = heatData;
            }
            
            if (heatData.Overheated)
            {
                StopAutoFire(player);
                return;
            }
            
            FireMiningLaser(player);
            
            heatData.Heat += config.Gun.HeatPerShot;
            heatData.LastFireTime = Time.realtimeSinceStartup;
            
            if (heatData.Heat >= config.Gun.OverheatThreshold)
            {
                heatData.Heat = config.Gun.OverheatThreshold;
                heatData.Overheated = true;
                heatData.OverheatEndTime = Time.realtimeSinceStartup + config.Gun.OverheatCooldown;
                StopAutoFire(player);
                SendReply(player, "<color=#ff4444>[!] OVERHEATED!</color>");
                
                timer.Once(config.Gun.OverheatCooldown, () =>
                {
                    if (playerHeat.TryGetValue(player.userID, out PlayerHeatData data))
                    {
                        data.Overheated = false;
                        data.Heat = 0;
                        UpdateHeatUI(player);
                        if (player != null && player.IsConnected)
                            SendReply(player, "<color=#44ff44>[OK] Cooled down!</color>");
                    }
                });
            }
            
            UpdateHeatUI(player);
        }
        #endregion

        #region Mining Laser
        private bool HasNodeInSight(BasePlayer player, out Vector3 hitPoint, out List<OreResourceEntity> nodesInRange)
        {
            hitPoint = Vector3.zero;
            nodesInRange = new List<OreResourceEntity>();
            
            Vector3 startPos = player.eyes.position;
            Vector3 direction = player.eyes.HeadForward();
            
            RaycastHit hit;
            Vector3 endPos;
            
            if (Physics.Raycast(startPos, direction, out hit, config.Gun.Range))
            {
                endPos = hit.point;
                
                var directHitNode = hit.GetEntity() as OreResourceEntity;
                if (directHitNode != null && !directHitNode.IsDestroyed)
                {
                    hitPoint = endPos;
                    nodesInRange.Add(directHitNode);
                    
                    var nearby = new List<OreResourceEntity>();
                    Vis.Entities(endPos, config.Gun.AOERadius, nearby);
                    foreach (var ore in nearby)
                    {
                        if (ore != null && !ore.IsDestroyed && ore != directHitNode)
                            nodesInRange.Add(ore);
                    }
                    return true;
                }
            }
            else
            {
                endPos = startPos + direction * config.Gun.Range;
            }
            
            Vis.Entities(endPos, config.Gun.AOERadius, nodesInRange);
            nodesInRange.RemoveAll(x => x == null || x.IsDestroyed);
            
            if (nodesInRange.Count > 0)
            {
                hitPoint = endPos;
                return true;
            }
            
            return false;
        }
        
        private void FireMiningLaser(BasePlayer player)
        {
            Vector3 startPos = player.eyes.position;
            
            if (!HasNodeInSight(player, out Vector3 hitPoint, out List<OreResourceEntity> nodes))
                return;
            
            ShowBeamEffect(player, startPos, hitPoint);
            
            foreach (var ore in nodes)
            {
                if (ore != null && !ore.IsDestroyed)
                    DamageNode(player, ore);
            }
        }
        
        private void ShowBeamEffect(BasePlayer player, Vector3 start, Vector3 end)
        {
            float distance = Vector3.Distance(start, end);
            int segments = Mathf.Max(8, (int)(distance / 1.5f));
            
            for (int i = 1; i <= segments; i++)
            {
                float t = (float)i / segments;
                Vector3 pos = Vector3.Lerp(start, end, t);
                Effect.server.Run(config.Gun.BeamEffectPrefab, pos);
            }
            
            Effect.server.Run(config.Gun.ImpactEffectPrefab, end);
        }
        
        private void DamageNode(BasePlayer player, OreResourceEntity ore)
        {
            if (ore == null || ore.IsDestroyed) return;
            
            uint nodeId = (uint)ore.net.ID.Value;
            
            if (!nodeHits.TryGetValue(nodeId, out NodeHitData hitData))
            {
                hitData = new NodeHitData
                {
                    HitsTaken = 0,
                    HitsNeeded = UnityEngine.Random.Range(config.Gun.MinHitsToBreak, config.Gun.MaxHitsToBreak + 1)
                };
                nodeHits[nodeId] = hitData;
            }
            
            hitData.HitsTaken++;
            
            Effect.server.Run(config.Gun.RockHitSoundPrefab, ore.transform.position);
            Effect.server.Run("assets/bundled/prefabs/fx/survey_explosion.prefab", ore.transform.position);
            
            if (hitData.HitsTaken >= hitData.HitsNeeded)
            {
                Effect.server.Run(config.Gun.RockBreakSoundPrefab, ore.transform.position);
                GatherFromNode(player, ore);
                nodeHits.Remove(nodeId);
                ore.Kill();
            }
        }
        
        // THIS IS THE CORRECT WAY TO GATHER FROM ORE NODES
        private void GatherFromNode(BasePlayer player, OreResourceEntity ore)
        {
            if (ore == null || player == null) return;
            
            // Use ResourceDispenser - NOT ResourceContainer!
            var dispenser = ore.GetComponent<ResourceDispenser>();
            if (dispenser == null) return;
            
            // containedItems is the correct property
            foreach (var item in dispenser.containedItems)
            {
                if (item.amount <= 0) continue;
                
                var giveItem = ItemManager.CreateByName(item.itemDef.shortname, (int)item.amount);
                if (giveItem != null)
                {
                    player.GiveItem(giveItem);
                }
            }
        }
        #endregion

        #region UI
        private void ShowMiningGunUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, UI_PANEL);
            
            var elements = new CuiElementContainer();
            
            elements.Add(new CuiPanel
            {
                Image = { Color = "0.1 0.1 0.12 0.85" },
                RectTransform = { AnchorMin = "0.01 0.11", AnchorMax = "0.18 0.14" },
                CursorEnabled = false
            }, "Hud", UI_PANEL);
            
            elements.Add(new CuiPanel
            {
                Image = { Color = "0.2 0.8 1 0.9" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "0.015 1" }
            }, UI_PANEL);
            
            elements.Add(new CuiLabel
            {
                Text = { Text = $"[>] MINING LASER | {config.Gun.Range}m | AOE {config.Gun.AOERadius}m", FontSize = 10, Align = TextAnchor.MiddleLeft, Color = "0.2 0.8 1 1" },
                RectTransform = { AnchorMin = "0.03 0", AnchorMax = "0.98 1" }
            }, UI_PANEL);
            
            CuiHelper.AddUi(player, elements);
        }
        
        private void UpdateHeatUI(BasePlayer player)
        {
            if (player == null) return;
            
            CuiHelper.DestroyUi(player, HEAT_UI);
            
            if (!playerHeat.TryGetValue(player.userID, out PlayerHeatData heatData)) return;
            if (heatData.Heat <= 0) return;
            
            var elements = new CuiElementContainer();
            
            float heatPercent = heatData.Heat / config.Gun.OverheatThreshold;
            string barColor = heatData.Overheated ? "1 0.2 0.2 0.9" : 
                              heatPercent > 0.7f ? "1 0.5 0.1 0.9" : 
                              heatPercent > 0.4f ? "1 0.8 0.2 0.9" : "0.2 0.8 1 0.9";
            
            elements.Add(new CuiPanel
            {
                Image = { Color = "0.1 0.1 0.12 0.85" },
                RectTransform = { AnchorMin = "0.01 0.145", AnchorMax = "0.18 0.17" },
                CursorEnabled = false
            }, "Hud", HEAT_UI);
            
            float barWidth = 0.02f + (0.96f * heatPercent);
            elements.Add(new CuiPanel
            {
                Image = { Color = barColor },
                RectTransform = { AnchorMin = "0.02 0.15", AnchorMax = $"{barWidth:F3} 0.85" }
            }, HEAT_UI);
            
            string heatText = heatData.Overheated 
                ? $"OVERHEATED! {(heatData.OverheatEndTime - Time.realtimeSinceStartup):F1}s"
                : $"HEAT: {(heatPercent * 100):F0}%";
            
            elements.Add(new CuiLabel
            {
                Text = { Text = heatText, FontSize = 9, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
            }, HEAT_UI);
            
            CuiHelper.AddUi(player, elements);
        }
        #endregion

        #region Commands
        [ChatCommand("mininggun")]
        private void CmdMiningGun(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin && !permission.UserHasPermission(player.UserIDString, PERM_ADMIN))
            {
                SendReply(player, "<color=#ff4444>Admin only.</color>");
                return;
            }
            
            if (args.Length == 0)
            {
                SendReply(player,
                    "<color=#ff8800>=== MINING GUN ===</color>\n" +
                    $"Range: {config.Gun.Range}m\n" +
                    $"AOE: {config.Gun.AOERadius}m\n" +
                    "<color=#888888>/mininggun give</color> - Give Mining Gun");
                return;
            }
            
            if (args[0].ToLower() == "give")
            {
                BasePlayer target = player;
                
                if (args.Length > 1)
                {
                    target = BasePlayer.Find(args[1]);
                    if (target == null)
                    {
                        SendReply(player, $"<color=#ff4444>Player not found.</color>");
                        return;
                    }
                }
                
                GiveMiningGun(target);
                SendReply(player, $"<color=#ff8800>Mining Gun given to {target.displayName}!</color>");
            }
        }
        
        private void GiveMiningGun(BasePlayer player)
        {
            var item = ItemManager.CreateByName("pistol.nailgun", 1, config.Gun.SkinID);
            if (item == null) return;
            
            item.name = config.Gun.ItemName;
            
            var weapon = item.GetHeldEntity() as BaseProjectile;
            if (weapon != null)
            {
                weapon.primaryMagazine.contents = 0;
                weapon.primaryMagazine.capacity = 0;
            }
            
            player.GiveItem(item);
            SendReply(player, $"<color=#ff8800>You received a {config.Gun.ItemName}!</color>");
        }
        #endregion
    }
}
```



---

## EXAMPLE: BRIGHTNIGHTS (Simple Config + Permissions + Timer)

```csharp
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
            [JsonProperty("Night Vision Brightness")]
            public float brightness = 1f;
            
            [JsonProperty("Night Vision Distance")]
            public float distance = 100f;
            
            [JsonProperty("Atmosphere Brightness Boost")]
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
            
            player.SendConsoleCommand("env.nightlight_enabled", "true");
            player.SendConsoleCommand("env.nightlight_brightness", config.brightness);
            player.SendConsoleCommand("env.nightlight_distance", config.distance);
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
```

---

## EXAMPLE: NOGIVENOTICES (Minimal Plugin)

```csharp
namespace Oxide.Plugins
{
    [Info("No Give Notices", "Wulf", "0.3.0")]
    [Description("Prevents F1 item giving notices from showing in the chat")]
    class NoGiveNotices : RustPlugin
    {
        private object OnServerMessage(string message, string name)
        {
            if (message.Contains("gave") && name == "SERVER")
            {
                return true;  // Block message
            }

            return null;  // Allow message
        }
    }
}
```

---

## EXAMPLE: AUTODOORS (Complex Data Storage + Timers)

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Facepunch;
using Newtonsoft.Json;
using Oxide.Core;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Auto Doors", "Wulf/lukespragg", "3.3.11")]
    [Description("Automatically closes doors behind players")]
    public class AutoDoors : RustPlugin
    {
        #region Fields
        private const string PERMISSION_USE = "autodoors.use";
        private readonly Hash<ulong, Timer> doorTimers = new Hash<ulong, Timer>();
        private StoredData storedData;
        private ConfigData configData;
        #endregion

        #region Oxide Hooks
        private void Init()
        {
            LoadData();
            permission.RegisterPermission(PERMISSION_USE, this);
            
            foreach (var command in configData.chatS.commands)
                cmd.AddChatCommand(command, this, nameof(CmdAutoDoor));
        }

        private void OnEntityKill(Door door)
        {
            if (door == null || door.net == null) return;
            var doorID = door.net.ID.Value;
            
            if (doorTimers.TryGetValue(doorID, out Timer value))
            {
                value?.Destroy();
                doorTimers.Remove(doorID);
            }
        }

        private void OnServerSave() => timer.Once(UnityEngine.Random.Range(0f, 60f), SaveData);

        private void Unload()
        {
            foreach (var value in doorTimers.Values)
                value?.Destroy();
            SaveData();
        }

        private void OnDoorOpened(Door door, BasePlayer player)
        {
            if (door == null || door.net == null || !door.IsOpen()) return;
            if (!permission.UserHasPermission(player.UserIDString, PERMISSION_USE)) return;

            var playerData = GetPlayerData(player.userID);
            if (!playerData.doorData.enabled) return;
            
            float autoCloseTime = playerData.doorData.time;
            if (autoCloseTime <= 0) return;

            var doorID = door.net.ID.Value;
            
            if (doorTimers.TryGetValue(doorID, out Timer value))
                value?.Destroy();
            
            doorTimers[doorID] = timer.Once(autoCloseTime, () =>
            {
                doorTimers.Remove(doorID);
                if (door == null || !door.IsOpen()) return;
                
                door.SetFlag(BaseEntity.Flags.Open, false);
                door.SendNetworkUpdateImmediate();
            });
        }

        private void OnDoorClosed(Door door, BasePlayer player)
        {
            if (door == null || door.net == null || door.IsOpen()) return;
            
            if (doorTimers.TryGetValue(door.net.ID.Value, out Timer value))
            {
                value?.Destroy();
                doorTimers.Remove(door.net.ID.Value);
            }
        }
        #endregion

        #region Commands
        private void CmdAutoDoor(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, PERMISSION_USE))
            {
                SendReply(player, "No permission!");
                return;
            }
            
            var playerData = GetPlayerData(player.userID);
            
            if (args == null || args.Length == 0)
            {
                playerData.doorData.enabled = !playerData.doorData.enabled;
                SendReply(player, $"Auto door: {(playerData.doorData.enabled ? "Enabled" : "Disabled")}");
                return;
            }
            
            if (float.TryParse(args[0], out float time))
            {
                playerData.doorData.time = time;
                playerData.doorData.enabled = true;
                SendReply(player, $"Auto door delay set to {time}s");
            }
        }
        #endregion

        #region Data
        private StoredData.PlayerData GetPlayerData(ulong playerID)
        {
            if (!storedData.playerData.TryGetValue(playerID, out var playerData))
            {
                playerData = new StoredData.PlayerData
                {
                    doorData = new StoredData.DoorData
                    {
                        enabled = configData.globalS.defaultEnabled,
                        time = configData.globalS.defaultDelay,
                    }
                };
                storedData.playerData.Add(playerID, playerData);
            }
            return playerData;
        }

        private void LoadData()
        {
            try
            {
                storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name);
            }
            catch
            {
                storedData = null;
            }
            
            if (storedData == null)
                storedData = new StoredData();
        }

        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(Name, storedData);

        private class StoredData
        {
            public readonly Dictionary<ulong, PlayerData> playerData = new Dictionary<ulong, PlayerData>();

            public class PlayerData
            {
                public DoorData doorData = new DoorData();
            }

            public class DoorData
            {
                public bool enabled;
                public float time;
            }
        }
        #endregion

        #region Config
        private class ConfigData
        {
            [JsonProperty("Global settings")]
            public GlobalSettings globalS = new GlobalSettings();

            [JsonProperty("Chat settings")]
            public ChatSettings chatS = new ChatSettings();

            public class GlobalSettings
            {
                [JsonProperty("Default enabled")]
                public bool defaultEnabled = true;

                [JsonProperty("Default delay")]
                public float defaultDelay = 5f;
            }

            public class ChatSettings
            {
                [JsonProperty("Chat command")]
                public string[] commands = { "ad", "autodoor" };
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                configData = Config.ReadObject<ConfigData>();
                if (configData == null) LoadDefaultConfig();
            }
            catch
            {
                LoadDefaultConfig();
            }
            SaveConfig();
        }

        protected override void LoadDefaultConfig() => configData = new ConfigData();

        protected override void SaveConfig() => Config.WriteObject(configData);
        #endregion
    }
}
```

---

## EXAMPLE: CHESTSTACKS (MonoBehaviour + ProtoStorage + Input)

```csharp
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Facepunch;
using Newtonsoft.Json;
using Oxide.Core;
using ProtoBuf;
using Rust;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Chest Stacks", "supreme", "1.4.2")]
    [Description("Allows players to stack chests")]
    public class ChestStacks : RustPlugin
    {
        #region Fields
        private static ChestStacks _pluginInstance;
        private PluginConfig _pluginConfig;
        private PluginData _pluginData;
        
        private readonly Hash<ulong, ChestStacking> _cachedComponents = new();
        
        private const string UsePermission = "cheststacks.use";
        private const string LargeBoxPrefab = "assets/prefabs/deployable/large wood storage/box.wooden.large.prefab";
        private const int BoxLayer = Layers.Mask.Deployed;
        
        private const BaseEntity.Flags StackedFlag = BaseEntity.Flags.Reserved1;
        #endregion

        #region Hooks
        private void Init()
        {
            _pluginInstance = this;
            LoadData();
            permission.RegisterPermission(UsePermission, this);
        }

        private void OnServerInitialized()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
                OnPlayerConnected(player);
        }

        private void Unload()
        {
            List<ChestStacking> components = Pool.Get<List<ChestStacking>>();
            components.AddRange(_cachedComponents.Values);
            
            for (int i = 0; i < components.Count; i++)
                components[i].Destroy();

            SaveData();
            _pluginInstance = null;
            Pool.FreeUnmanaged(ref components);
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (_cachedComponents[player.userID]) return;
            player.gameObject.AddComponent<ChestStacking>();
        }
        
        private void OnPlayerDisconnected(BasePlayer player)
        {
            ChestStacking component = _cachedComponents[player.userID];
            if (component) component.Destroy();
        }
        
        private void OnEntityKill(BoxStorage box)
        {
            if (!box || !IsStacked(box)) return;
            _pluginData.StoredBoxes.Remove(box.net.ID.Value);
        }
        #endregion

        #region Helpers
        private bool IsStacked(BoxStorage box) => box.HasFlag(StackedFlag);
        
        private bool HasPermission(BasePlayer player, string perm) =>
            permission.UserHasPermission(player.UserIDString, perm);
        #endregion

        #region Component
        public class ChestStacking : FacepunchBehaviour
        {
            private BasePlayer Player { get; set; }
            private float NextTime { get; set; }
            
            private void Awake()
            {
                Player = GetComponent<BasePlayer>();
                _pluginInstance._cachedComponents[Player.userID] = this;
            }

            private void Update()
            {
                if (!Player || !_pluginInstance.HasPermission(Player, UsePermission))
                    return;

                if (!Player.serverInput.WasJustPressed(BUTTON.FIRE_SECONDARY))
                    return;
                
                if (NextTime > Time.time) return;
                NextTime = Time.time + 0.5f;
                
                Item activeItem = Player.GetActiveItem();
                if (activeItem == null || activeItem.info.shortname != "box.wooden.large")
                    return;
                
                BoxStorage box = GetLookingAtBox();
                if (!box) return;
                
                StackChest(box, activeItem);
            }
            
            private BoxStorage GetLookingAtBox()
            {
                if (!Physics.Raycast(Player.eyes.HeadRay(), out RaycastHit hit, 3f, BoxLayer))
                    return null;
                return hit.GetEntity() as BoxStorage;
            }
            
            private void StackChest(BoxStorage existingBox, Item activeItem)
            {
                Vector3 newPos = existingBox.transform.position + new Vector3(0f, 0.8f, 0f);
                Quaternion rotation = existingBox.transform.rotation;
                
                BoxStorage newBox = (BoxStorage)GameManager.server.CreateEntity(
                    LargeBoxPrefab, newPos, rotation);
                
                if (!newBox) return;
                
                newBox.Spawn();
                newBox.OwnerID = Player.userID;
                newBox.skinID = activeItem.skin;
                newBox.SetFlag(StackedFlag, true);
                newBox.SendNetworkUpdateImmediate();
                
                _pluginInstance._pluginData.StoredBoxes[newBox.net.ID.Value] = new BoxData
                {
                    BottomBoxId = existingBox.net.ID.Value
                };
                
                activeItem.UseItem();
                _pluginInstance.SaveData();
                
                Player.ChatMessage("Chest stacked!");
            }

            public void Destroy()
            {
                _pluginInstance._cachedComponents.Remove(Player.userID);
                DestroyImmediate(this);
            }
        }
        #endregion

        #region Config
        private class PluginConfig
        {
            [DefaultValue(true)]
            [JsonProperty("Building privilege required")]
            public bool BuildingPrivilegeRequired { get; set; }
            
            [JsonProperty("Max stack height")]
            public int MaxStackHeight { get; set; } = 5;
        }

        protected override void LoadDefaultConfig() => PrintWarning("Loading Default Config");

        protected override void LoadConfig()
        {
            base.LoadConfig();
            Config.Settings.DefaultValueHandling = DefaultValueHandling.Populate;
            _pluginConfig = Config.ReadObject<PluginConfig>();
            Config.WriteObject(_pluginConfig);
        }
        #endregion
        
        #region Data (ProtoStorage)
        private void SaveData()
        {
            if (_pluginData == null) return;
            ProtoStorage.Save(_pluginData, Name);
        }

        private void LoadData()
        {
            _pluginData = ProtoStorage.Load<PluginData>(Name) ?? new PluginData();
        }

        [ProtoContract]
        private class PluginData
        {
            [ProtoMember(1)]
            public Hash<ulong, BoxData> StoredBoxes { get; set; } = new();
        }

        [ProtoContract]
        private class BoxData
        {
            [ProtoMember(1)]
            public ulong BottomBoxId { get; set; }
        }
        #endregion
    }
}
```

---

## CRITICAL PATTERNS SUMMARY

### Getting Resources from Mining Nodes (OreResourceEntity)
```csharp
// CORRECT - Use ResourceDispenser
var dispenser = ore.GetComponent<ResourceDispenser>();
foreach (var item in dispenser.containedItems)
{
    string shortname = item.itemDef.shortname;
    float amount = item.amount;
    
    var giveItem = ItemManager.CreateByName(shortname, (int)amount);
    player.GiveItem(giveItem);
}
```

### Configuration with Nested Classes
```csharp
private class Configuration
{
    [JsonProperty("Main Settings")]
    public MainSettings Main { get; set; } = new MainSettings();
    
    [JsonProperty("Sound Settings")]
    public SoundSettings Sound { get; set; } = new SoundSettings();
}

private class MainSettings
{
    [JsonProperty("Enabled")]
    public bool Enabled { get; set; } = true;
}

private class SoundSettings
{
    [JsonProperty("Volume")]
    public float Volume { get; set; } = 1.0f;
    
    [JsonProperty("Effect Prefab")]
    public string EffectPrefab { get; set; } = "assets/bundled/prefabs/fx/survey_explosion.prefab";
}
```

### Timer Cleanup in Unload
```csharp
private Dictionary<ulong, Timer> playerTimers = new Dictionary<ulong, Timer>();

private void Unload()
{
    foreach (var t in playerTimers.Values)
        t?.Destroy();
    playerTimers.Clear();
}
```

### MonoBehaviour Cleanup
```csharp
private readonly Hash<ulong, MyComponent> _components = new();

private void Unload()
{
    foreach (var component in _components.Values.ToList())
        component?.Destroy();
    _components.Clear();
}
```


---

## EXAMPLE: ECONOMICS (CovalencePlugin + API Methods + Data Storage)

This is a complete economy system showing CovalencePlugin, API methods, data persistence, and localization.

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("Economics", "Wulf", "3.9.2")]
    [Description("Basic economics system and economy API")]
    public class Economics : CovalencePlugin
    {
        #region Configuration
        private Configuration config;

        private class Configuration
        {
            [JsonProperty("Allow negative balance for accounts")]
            public bool AllowNegativeBalance = false;

            [JsonProperty("Balance limit for accounts (0 to disable)")]
            public int BalanceLimit = 0;

            [JsonProperty("Negative balance limit for accounts (0 to disable)")]
            public int NegativeBalanceLimit = 0;

            [JsonProperty("Remove unused accounts")]
            public bool RemoveUnused = true;

            [JsonProperty("Log transactions to file")]
            public bool LogTransactions = false;

            [JsonProperty("Starting account balance (0 or higher)")]
            public int StartingBalance = 1000;

            [JsonProperty("Wipe balances on new save file")]
            public bool WipeOnNewSave = false;

            public string ToJson() => JsonConvert.SerializeObject(this);
            public Dictionary<string, object> ToDictionary() => 
                JsonConvert.DeserializeObject<Dictionary<string, object>>(ToJson());
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
                LogWarning($"Configuration file {Name}.json is invalid; using defaults");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig() => Config.WriteObject(config, true);
        #endregion

        #region Stored Data
        private DynamicConfigFile data;
        private StoredData storedData;
        private bool changed;

        private class StoredData
        {
            public readonly Dictionary<string, double> Balances = new Dictionary<string, double>();
        }

        private void SaveData()
        {
            if (changed)
            {
                Puts("Saving balances for players...");
                Interface.Oxide.DataFileSystem.WriteObject(Name, storedData);
            }
        }

        private void OnServerSave() => SaveData();
        private void Unload() => SaveData();
        #endregion

        #region Localization
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CommandBalance"] = "balance",
                ["CommandDeposit"] = "deposit",
                ["CommandTransfer"] = "transfer",
                ["CommandWithdraw"] = "withdraw",
                ["YourBalance"] = "Your balance is: {0:C}",
                ["PlayerBalance"] = "Balance for {0}: {1:C}",
                ["NotAllowed"] = "You are not allowed to use the '{0}' command",
                ["NoPlayersFound"] = "No players found with name or ID '{0}'",
                ["TransactionFailed"] = "Transaction failed!",
                ["YouLackMoney"] = "You do not have enough money!",
                ["ZeroAmount"] = "Amount cannot be zero"
            }, this);
        }
        #endregion

        #region Initialization
        private const string permissionBalance = "economics.balance";
        private const string permissionDeposit = "economics.deposit";
        private const string permissionTransfer = "economics.transfer";
        private const string permissionWithdraw = "economics.withdraw";

        private void Init()
        {
            AddLocalizedCommand(nameof(CommandBalance));
            AddLocalizedCommand(nameof(CommandDeposit));
            AddLocalizedCommand(nameof(CommandTransfer));
            AddLocalizedCommand(nameof(CommandWithdraw));

            permission.RegisterPermission(permissionBalance, this);
            permission.RegisterPermission(permissionDeposit, this);
            permission.RegisterPermission(permissionTransfer, this);
            permission.RegisterPermission(permissionWithdraw, this);

            data = Interface.Oxide.DataFileSystem.GetFile(Name);
            storedData = data.ReadObject<StoredData>() ?? new StoredData();
        }

        private void OnNewSave()
        {
            if (config.WipeOnNewSave)
            {
                storedData.Balances.Clear();
                changed = true;
                Interface.Call("OnEconomicsDataWiped");
            }
        }
        #endregion

        #region API Methods
        private double Balance(string playerId)
        {
            if (string.IsNullOrEmpty(playerId)) return 0.0;
            double playerData;
            return storedData.Balances.TryGetValue(playerId, out playerData) 
                ? playerData : config.StartingBalance;
        }

        private bool Deposit(string playerId, double amount)
        {
            if (string.IsNullOrEmpty(playerId) || amount <= 0) return false;
            if (SetBalance(playerId, amount + Balance(playerId)))
            {
                Interface.Call("OnEconomicsDeposit", playerId, amount);
                if (config.LogTransactions)
                    LogToFile("transactions", $"[{DateTime.Now}] {amount} deposited to {playerId}", this);
                return true;
            }
            return false;
        }

        private bool SetBalance(string playerId, double amount)
        {
            if (string.IsNullOrEmpty(playerId)) return false;
            if (amount >= 0 || config.AllowNegativeBalance)
            {
                amount = Math.Round(amount, 2);
                if (config.BalanceLimit > 0 && amount > config.BalanceLimit)
                    amount = config.BalanceLimit;
                
                storedData.Balances[playerId] = amount;
                changed = true;
                Interface.Call("OnEconomicsBalanceUpdated", playerId, amount);
                return true;
            }
            return false;
        }

        private bool Transfer(string playerId, string targetId, double amount)
        {
            if (Withdraw(playerId, amount) && Deposit(targetId, amount))
            {
                Interface.Call("OnEconomicsTransfer", playerId, targetId, amount);
                return true;
            }
            return false;
        }

        private bool Withdraw(string playerId, double amount)
        {
            if (string.IsNullOrEmpty(playerId)) return false;
            double balance = Balance(playerId);
            if (balance >= amount && SetBalance(playerId, balance - amount))
            {
                Interface.Call("OnEconomicsWithdrawl", playerId, amount);
                return true;
            }
            return false;
        }
        #endregion

        #region Commands
        private void CommandBalance(IPlayer player, string command, string[] args)
        {
            if (args != null && args.Length > 0)
            {
                if (!player.HasPermission(permissionBalance))
                {
                    Message(player, "NotAllowed", command);
                    return;
                }
                IPlayer target = FindPlayer(args[0], player);
                if (target == null) return;
                Message(player, "PlayerBalance", target.Name, Balance(target.Id));
                return;
            }
            Message(player, "YourBalance", Balance(player.Id));
        }

        private void CommandDeposit(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(permissionDeposit))
            {
                Message(player, "NotAllowed", command);
                return;
            }
            if (args == null || args.Length <= 1) return;

            double amount;
            double.TryParse(args[1], out amount);
            if (amount <= 0) { Message(player, "ZeroAmount"); return; }

            IPlayer target = FindPlayer(args[0], player);
            if (target == null) return;

            if (Deposit(target.Id, amount))
                Message(player, "PlayerBalance", target.Name, Balance(target.Id));
            else
                Message(player, "TransactionFailed");
        }

        private void CommandTransfer(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(permissionTransfer))
            {
                Message(player, "NotAllowed", command);
                return;
            }
            if (args == null || args.Length <= 1) return;

            double amount;
            double.TryParse(args[1], out amount);
            if (amount <= 0) { Message(player, "ZeroAmount"); return; }

            IPlayer target = FindPlayer(args[0], player);
            if (target == null) return;

            if (!Withdraw(player.Id, amount))
            {
                Message(player, "YouLackMoney");
                return;
            }
            Deposit(target.Id, amount);
        }

        private void CommandWithdraw(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(permissionWithdraw))
            {
                Message(player, "NotAllowed", command);
                return;
            }
            if (args == null || args.Length <= 1) return;

            double amount;
            double.TryParse(args[1], out amount);
            if (amount <= 0) { Message(player, "ZeroAmount"); return; }

            IPlayer target = FindPlayer(args[0], player);
            if (target == null) return;

            if (Withdraw(target.Id, amount))
                Message(player, "PlayerBalance", target.Name, Balance(target.Id));
            else
                Message(player, "YouLackMoney");
        }
        #endregion

        #region Helpers
        private IPlayer FindPlayer(string playerNameOrId, IPlayer player)
        {
            IPlayer[] foundPlayers = players.FindPlayers(playerNameOrId).ToArray();
            if (foundPlayers.Length > 1)
            {
                Message(player, "PlayersFound", string.Join(", ", foundPlayers.Select(p => p.Name).Take(10)));
                return null;
            }
            IPlayer target = foundPlayers.Length == 1 ? foundPlayers[0] : null;
            if (target == null)
            {
                Message(player, "NoPlayersFound", playerNameOrId);
                return null;
            }
            return target;
        }

        private void AddLocalizedCommand(string command)
        {
            foreach (string language in lang.GetLanguages(this))
            {
                Dictionary<string, string> messages = lang.GetMessages(language, this);
                foreach (KeyValuePair<string, string> message in messages)
                {
                    if (message.Key.Equals(command) && !string.IsNullOrEmpty(message.Value))
                        AddCovalenceCommand(message.Value, command);
                }
            }
        }

        private string GetLang(string langKey, string playerId = null, params object[] args) =>
            string.Format(lang.GetMessage(langKey, this, playerId), args);

        private void Message(IPlayer player, string textOrLang, params object[] args)
        {
            if (player.IsConnected)
            {
                string message = GetLang(textOrLang, player.Id, args);
                player.Reply(message != textOrLang ? message : textOrLang);
            }
        }
        #endregion
    }
}
```

---

## EXAMPLE: NOTECHTREE (Simple Hook Blocking)

This is a minimal plugin showing how to block game mechanics using hooks.

```csharp
using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("No Tech Tree", "Sche1sseHund", 1.3)]
    [Description("Completely disables the tech tree")]
    class NoTechTree : RustPlugin
    {
        private const string PERMISSION_BYPASS = "notechtree.bypass";
        private Configuration config;

        private class Configuration
        {
            [JsonProperty("Block Tech Tree Unlocking")]
            public bool BlockTechTree { get; set; } = true;

            [JsonProperty("Block Research Table")]
            public bool BlockResearchTable { get; set; } = false;

            [JsonProperty("Block Experimenting at Workbench")]
            public bool BlockExperimenting { get; set; } = false;
        }

        protected override void LoadDefaultConfig() => config = new Configuration();

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

            // Unsubscribe from hooks we don't need based on config
            if (!config.BlockResearchTable)
                Unsubscribe(nameof(CanResearchItem));
            if (!config.BlockExperimenting)
                Unsubscribe(nameof(CanExperiment));
        }

        // Block tech tree node unlocking
        private object CanUnlockTechTreeNode(BasePlayer player, TechTreeData.NodeInstance node, TechTreeData techTree)
        {
            if (!config.BlockTechTree) return null;
            if (permission.UserHasPermission(player.UserIDString, PERMISSION_BYPASS)) return null;

            PrintToChat(player, lang.GetMessage("NoTechTree", this, player.UserIDString));
            return false;  // Return false to block
        }

        // Alternative hook for some versions
        private object OnTechTreeNodeUnlock(Workbench workbench, TechTreeData.NodeInstance node, BasePlayer player)
        {
            if (!config.BlockTechTree) return null;
            if (permission.UserHasPermission(player.UserIDString, PERMISSION_BYPASS)) return null;

            PrintToChat(player, lang.GetMessage("NoTechTree", this, player.UserIDString));
            return false;
        }

        // Block research table usage (optional)
        private object CanResearchItem(BasePlayer player, Item item)
        {
            if (permission.UserHasPermission(player.UserIDString, PERMISSION_BYPASS)) return null;
            PrintToChat(player, lang.GetMessage("NoResearch", this, player.UserIDString));
            return false;
        }

        // Block experimenting at workbench (optional)
        private object CanExperiment(BasePlayer player, Workbench workbench)
        {
            if (permission.UserHasPermission(player.UserIDString, PERMISSION_BYPASS)) return null;
            PrintToChat(player, lang.GetMessage("NoExperiment", this, player.UserIDString));
            return false;
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NoTechTree"] = "<color=#ff5555>The Tech Tree has been disabled.</color>",
                ["NoResearch"] = "<color=#ff5555>Research Table has been disabled.</color>",
                ["NoExperiment"] = "<color=#ff5555>Experimenting has been disabled.</color>"
            }, this, "en");
        }
    }
}
```

---

## EXAMPLE: BGRADE (MonoBehaviour + Extension Methods + Building Upgrades)

This plugin shows MonoBehaviour attached to players, extension methods, and building system interaction.

```csharp
using System;
using System.Linq;
using System.Collections.Generic;
using Rust;
using UnityEngine;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Plugins.BGradeExt;

namespace Oxide.Plugins
{
    [Info("BGrade", "Ryan / Rustoria.co", "1.1.6")]
    [Description("Auto update building blocks when placed")]
    public class BGrade : RustPlugin
    {
        #region Declaration
        public static BGrade Instance;
        private ListHashSet<string> _registeredPermissions = new ListHashSet<string>();
        #endregion

        #region Config
        private bool AllowTimer;
        private int MaxTimer;
        private int DefaultTimer;
        private bool RefundOnBlock;
        private List<string> ChatCommands;

        private void InitConfig()
        {
            AllowTimer = GetConfig(true, "Timer Settings", "Enabled");
            DefaultTimer = GetConfig(30, "Timer Settings", "Default Timer");
            MaxTimer = GetConfig(180, "Timer Settings", "Max Timer");
            ChatCommands = GetConfig(new List<string> { "bgrade", "grade" }, "Command Settings", "Chat Commands");
            RefundOnBlock = GetConfig(true, "Refund Settings", "Refund on Block");
        }

        private T GetConfig<T>(T defaultVal, params string[] path)
        {
            var data = Config.Get(path);
            if (data != null) return Config.ConvertValue<T>(data);
            Config.Set(path.Concat(new object[] { defaultVal }).ToArray());
            SaveConfig();
            return defaultVal;
        }
        #endregion

        #region Lang
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Permission"] = "You don't have permission to use that command",
                ["Error.Resources"] = "You don't have enough resources to upgrade.",
                ["Notice.SetGrade"] = "Automatic upgrading is now set to grade <color=orange>{0}</color>.",
                ["Notice.Disabled"] = "Automatic upgrading is now disabled.",
                ["Notice.Time"] = "It'll automatically disable in <color=orange>{0}</color> seconds."
            }, this);
        }
        #endregion

        #region BGrade Player Component
        private class BGradePlayer : FacepunchBehaviour
        {
            public static Dictionary<BasePlayer, BGradePlayer> Players = new Dictionary<BasePlayer, BGradePlayer>();

            private BasePlayer _player;
            private Timer _timer;
            private int _grade;
            private int _time;

            public void Awake()
            {
                _player = GetComponent<BasePlayer>();
                if (_player == null || !_player.IsConnected) return;
                Players[_player] = this;
                _time = Instance.DefaultTimer;
            }

            public int GetGrade() => _grade;
            public void SetGrade(int newGrade) => _grade = newGrade;
            public void SetTime(int newTime) => _time = newTime;
            public int GetTime() => _time != 0 ? _time : Instance.DefaultTimer;

            public void UpdateTime()
            {
                if (_time <= 0) return;
                DestroyTimer();
                _timer = Instance.timer.Once(_time, () =>
                {
                    _grade = 0;
                    DestroyTimer();
                    _player.ChatMessage("Notice.Disabled.Auto".Lang(_player.UserIDString));
                });
            }

            public void DestroyTimer()
            {
                _timer?.Destroy();
                _timer = null;
            }

            public void OnDestroy()
            {
                if (Players.ContainsKey(_player))
                    Players.Remove(_player);
            }
        }
        #endregion

        #region Hooks
        private void Init()
        {
            Instance = this;
            InitConfig();
            RegisterPermissions();
            foreach (var command in ChatCommands)
                cmd.AddChatCommand(command, this, BGradeCommand);
        }

        private void Unload()
        {
            Instance = null;
            foreach (var player in BGradePlayer.Players.Keys.ToList())
            {
                var component = BGradePlayer.Players[player];
                component?.DestroyTimer();
                UnityEngine.Object.Destroy(component);
            }
            BGradePlayer.Players.Clear();
        }

        private void RegisterPermissions()
        {
            for (var i = 1; i < 5; i++)
                permission.RegisterPermission($"bgrade.{i}", this);
            permission.RegisterPermission("bgrade.nores", this);
            permission.RegisterPermission("bgrade.all", this);
        }

        private void OnEntityBuilt(Planner plan, GameObject gameObject)
        {
            var player = plan?.GetOwnerPlayer();
            if (player == null || plan.isTypeDeployable) return;

            var buildingBlock = gameObject.GetComponent<BuildingBlock>();
            if (buildingBlock == null || !player.CanBuild()) return;

            BGradePlayer bgradePlayer;
            if (!BGradePlayer.Players.TryGetValue(player, out bgradePlayer)) return;

            var playerGrade = bgradePlayer.GetGrade();
            if (playerGrade == 0) return;

            // Check permission
            if (!player.HasPluginPerm("all") && !player.HasPluginPerm(playerGrade.ToString()))
                return;

            // Check if upgrade is valid
            if (playerGrade < (int)buildingBlock.grade || 
                buildingBlock.blockDefinition.grades[playerGrade] == null)
                return;

            // Check resources (unless has nores permission)
            if (!player.HasPluginPerm("nores"))
            {
                Dictionary<int, int> itemsToTake;
                if (!TakeResources(player, playerGrade, buildingBlock, out itemsToTake))
                {
                    player.ChatMessage("Error.Resources".Lang(player.UserIDString));
                    return;
                }
                foreach (var itemToTake in itemsToTake)
                    player.TakeItem(itemToTake.Key, itemToTake.Value);
            }

            if (AllowTimer) bgradePlayer.UpdateTime();

            // Perform the upgrade
            buildingBlock.SetGrade((BuildingGrade.Enum)playerGrade);
            buildingBlock.SetHealthToMax();
            buildingBlock.StartBeingRotatable();
            buildingBlock.SendNetworkUpdate();
            buildingBlock.UpdateSkin();
            buildingBlock.ResetUpkeepTime();
            buildingBlock.GetBuilding()?.Dirty();
        }

        private bool TakeResources(BasePlayer player, int grade, BuildingBlock block, out Dictionary<int, int> items)
        {
            items = new Dictionary<int, int>();
            List<ItemAmount> costToBuild = null;
            
            foreach (var g in block.blockDefinition.grades)
            {
                if (g.gradeBase.type == (BuildingGrade.Enum)grade)
                {
                    costToBuild = g.CostToBuild();
                    break;
                }
            }
            if (costToBuild == null) return false;

            foreach (var itemAmount in costToBuild)
            {
                if (!items.ContainsKey(itemAmount.itemid))
                    items.Add(itemAmount.itemid, 0);
                items[itemAmount.itemid] += (int)itemAmount.amount;
            }

            foreach (var itemToTake in items)
            {
                if (!player.HasItemAmount(itemToTake.Key, itemToTake.Value))
                    return false;
            }
            return true;
        }
        #endregion

        #region Commands
        private void BGradeCommand(BasePlayer player, string command, string[] args)
        {
            if (args.Length == 0) return;

            switch (args[0].ToLower())
            {
                case "0":
                    player.ChatMessage("Notice.Disabled".Lang(player.UserIDString));
                    BGradePlayer bgradePlayer;
                    if (BGradePlayer.Players.TryGetValue(player, out bgradePlayer))
                    {
                        bgradePlayer.DestroyTimer();
                        bgradePlayer.SetGrade(0);
                    }
                    break;

                case "1":
                case "2":
                case "3":
                case "4":
                    if (!player.HasPluginPerm("all") && !player.HasPluginPerm(args[0]))
                    {
                        player.ChatMessage("Permission".Lang(player.UserIDString));
                        return;
                    }
                    var grade = Convert.ToInt32(args[0]);
                    BGradePlayer bp;
                    if (!BGradePlayer.Players.TryGetValue(player, out bp))
                        bp = player.gameObject.AddComponent<BGradePlayer>();
                    
                    bp.SetGrade(grade);
                    player.ChatMessage("Notice.SetGrade".Lang(player.UserIDString, grade));
                    if (AllowTimer && bp.GetTime() > 0)
                        player.ChatMessage("Notice.Time".Lang(player.UserIDString, bp.GetTime()));
                    break;
            }
        }
        #endregion
    }
}

// Extension methods in separate namespace
namespace Oxide.Plugins.BGradeExt
{
    public static class BGradeExtensions
    {
        private static readonly Permission permission = Interface.Oxide.GetLibrary<Permission>();
        private static readonly Lang lang = Interface.Oxide.GetLibrary<Lang>();

        public static bool HasPluginPerm(this BasePlayer player, string perm) =>
            permission.UserHasPermission(player.UserIDString, "bgrade." + perm);

        public static string Lang(this string key, string id = null, params object[] args) =>
            string.Format(lang.GetMessage(key, BGrade.Instance, id), args);

        public static bool HasItemAmount(this BasePlayer player, int itemId, int itemAmount)
        {
            var count = 0;
            foreach (var item in player.inventory.containerMain.itemList)
                if (item.info.itemid == itemId) count += item.amount;
            foreach (var item in player.inventory.containerBelt.itemList)
                if (item.info.itemid == itemId) count += item.amount;
            return count >= itemAmount;
        }

        public static void TakeItem(this BasePlayer player, int itemId, int itemAmount)
        {
            if (player.inventory.Take(null, itemId, itemAmount) > 0)
                player.SendConsoleCommand("note.inv", itemId, itemAmount * -1);
        }
    }
}
```

---

## EXAMPLE: QUICKSMELT (FacepunchBehaviour Controller + Furnace Modification)

This plugin shows how to modify furnace behavior using a MonoBehaviour controller.

```csharp
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Oxide.Plugins
{
    [Info("Quick Smelt", "misticos", "5.1.5")]
    [Description("Increases the speed of the furnace smelting")]
    class QuickSmelt : RustPlugin
    {
        #region Variables
        private static QuickSmelt _instance;
        private const string PermissionUse = "quicksmelt.use";
        #endregion

        #region Configuration
        private static Configuration _config;

        private class Configuration
        {
            [JsonProperty("Use Permission")]
            public bool UsePermission = true;

            [JsonProperty("Speed Multipliers", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, float> SpeedMultipliers = new Dictionary<string, float>
            {
                { "global", 1.0f },
                { "furnace.shortname", 1.0f }
            };

            [JsonProperty("Output Multipliers", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, Dictionary<string, float>> OutputMultipliers =
                new Dictionary<string, Dictionary<string, float>>
                {
                    { "global", new Dictionary<string, float> { { "global", 1.0f } } }
                };
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null) throw new Exception();
            }
            catch
            {
                PrintError("Configuration error. Using defaults.");
                LoadDefaultConfig();
            }
        }

        protected override void LoadDefaultConfig() => _config = new Configuration();
        protected override void SaveConfig() => Config.WriteObject(_config);
        #endregion

        #region Hooks
        private void Unload()
        {
            var ovens = UnityEngine.Object.FindObjectsOfType<BaseOven>();
            for (var i = 0; i < ovens.Length; i++)
            {
                var oven = ovens[i];
                var component = oven.GetComponent<FurnaceController>();
                if (oven.IsOn())
                {
                    component.StopCooking();
                    oven.StartCooking();
                }
                UnityEngine.Object.Destroy(component);
            }
        }

        private void OnServerInitialized()
        {
            _instance = this;
            permission.RegisterPermission(PermissionUse, this);

            var ovens = UnityEngine.Object.FindObjectsOfType<BaseOven>();
            for (var i = 0; i < ovens.Length; i++)
                OnEntitySpawned(ovens[i]);
        }

        private void OnEntitySpawned(BaseNetworkable entity)
        {
            var oven = entity as BaseOven;
            if (oven == null) return;
            oven.gameObject.AddComponent<FurnaceController>();
        }

        private object OnOvenToggle(StorageContainer oven, BasePlayer player)
        {
            if (oven is BaseFuelLightSource) return null;
            
            var component = oven.gameObject.GetComponent<FurnaceController>();
            var canUse = CanUse(oven.OwnerID) || CanUse(player.userID);
            
            if (oven.IsOn())
                component.StopCooking();
            else if (canUse)
                component.StartCooking();
            else
                return null;
            
            return false;
        }
        #endregion

        private bool CanUse(ulong id) =>
            !_config.UsePermission || permission.UserHasPermission(id.ToString(), PermissionUse);

        #region Furnace Controller
        public class FurnaceController : FacepunchBehaviour
        {
            private BaseOven _oven;
            private BaseOven Furnace => _oven ?? (_oven = GetComponent<BaseOven>());
            
            private float _speedMultiplier;
            private Dictionary<string, float> _outputModifiers;

            private float OutputMultiplier(string shortname)
            {
                float modifier;
                if (_outputModifiers == null || 
                    (!_outputModifiers.TryGetValue(shortname, out modifier) &&
                     !_outputModifiers.TryGetValue("global", out modifier)))
                    modifier = 1.0f;
                return modifier;
            }

            private void Awake()
            {
                float modifierF;
                if (!_config.SpeedMultipliers.TryGetValue(Furnace.ShortPrefabName, out modifierF) &&
                    !_config.SpeedMultipliers.TryGetValue("global", out modifierF))
                    modifierF = 1.0f;
                _speedMultiplier = 0.5f / modifierF;

                if (!_config.OutputMultipliers.TryGetValue(Furnace.ShortPrefabName, out _outputModifiers) &&
                    !_config.OutputMultipliers.TryGetValue("global", out _outputModifiers))
                { }
            }

            private Item FindBurnable()
            {
                if (Furnace.inventory == null) return null;
                foreach (var item in Furnace.inventory.itemList)
                {
                    if (_oven.IsBurnableItem(item))
                        return item;
                }
                return null;
            }

            public void Cook()
            {
                var itemBurnable = FindBurnable();
                if (itemBurnable == null)
                {
                    StopCooking();
                    return;
                }

                SmeltItems();

                var burnable = itemBurnable.info.GetComponent<ItemModBurnable>();
                itemBurnable.fuel -= 0.5f * (Furnace.cookingTemperature / 200f);

                if (!itemBurnable.HasFlag(global::Item.Flag.OnFire))
                {
                    itemBurnable.SetFlag(global::Item.Flag.OnFire, true);
                    itemBurnable.MarkDirty();
                }

                if (itemBurnable.fuel <= 0f)
                    ConsumeFuel(itemBurnable, burnable);
            }

            private void ConsumeFuel(Item fuel, ItemModBurnable burnable)
            {
                if (Furnace.allowByproductCreation && burnable.byproductItem != null &&
                    Random.Range(0f, 1f) > burnable.byproductChance)
                {
                    var def = burnable.byproductItem;
                    var item = ItemManager.Create(def, 
                        (int)(burnable.byproductAmount * OutputMultiplier(def.shortname)));
                    if (!item.MoveToContainer(Furnace.inventory))
                    {
                        StopCooking();
                        item.Drop(Furnace.inventory.dropPosition, Furnace.inventory.dropVelocity);
                    }
                }

                if (fuel.amount <= 1)
                    fuel.Remove();
                else
                {
                    fuel.UseItem(1);
                    fuel.fuel = burnable.fuelAmount;
                    fuel.MarkDirty();
                }
            }

            private void SmeltItems()
            {
                for (var i = 0; i < Furnace.inventory.itemList.Count; i++)
                {
                    var item = Furnace.inventory.itemList[i];
                    if (item == null || !item.IsValid()) continue;

                    var cookable = item.info.GetComponent<ItemModCookable>();
                    if (cookable == null) continue;

                    var temperature = item.temperature;
                    if (!cookable.CanBeCookedByAtTemperature(temperature))
                    {
                        if (cookable.setCookingFlag && item.HasFlag(global::Item.Flag.Cooking))
                        {
                            item.SetFlag(global::Item.Flag.Cooking, false);
                            item.MarkDirty();
                        }
                        continue;
                    }

                    if (cookable.setCookingFlag && !item.HasFlag(global::Item.Flag.Cooking))
                    {
                        item.SetFlag(global::Item.Flag.Cooking, true);
                        item.MarkDirty();
                    }

                    var amountConsumed = (int)_oven.GetSmeltingSpeed();
                    amountConsumed = Math.Min(amountConsumed, item.amount);
                    
                    if (item.amount > amountConsumed)
                    {
                        item.amount -= amountConsumed;
                        item.MarkDirty();
                    }
                    else
                        item.Remove();

                    if (cookable.becomeOnCooked == null) continue;

                    var itemProduced = ItemManager.Create(cookable.becomeOnCooked,
                        (int)(cookable.amountOfBecome * amountConsumed * 
                              OutputMultiplier(cookable.becomeOnCooked.shortname)));

                    if (itemProduced == null || itemProduced.MoveToContainer(item.parent))
                        continue;

                    itemProduced.Drop(item.parent.dropPosition, item.parent.dropVelocity);
                    StopCooking();
                }
            }

            public void StartCooking()
            {
                if (FindBurnable() == null) return;
                StopCooking();
                
                Furnace.inventory.temperature = Furnace.cookingTemperature;
                Furnace.UpdateAttachmentTemperature();
                Furnace.InvokeRepeating(Cook, _speedMultiplier, _speedMultiplier);
                Furnace.SetFlag(BaseEntity.Flags.On, true);
            }

            public void StopCooking()
            {
                Furnace.CancelInvoke(Cook);
                Furnace.StopCooking();
            }
        }
        #endregion
    }
}
```

---

## EXAMPLE: TURRETCONFIG (Entity Modification + Permission-Based Settings)

This plugin shows how to modify entity properties based on owner permissions.

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Turret Config", "Calytic", "2.1.0")]
    [Description("Allows customizing turret behavior")]
    class TurretConfig : RustPlugin
    {
        [PluginReference] private Plugin Vanish = null;

        private const string AutoTurretPrefab = "assets/prefabs/npc/autoturret/autoturret_deployed.prefab";
        private uint _autoTurretPrefabId;

        // Settings
        private bool _adminOverride;
        private bool _sleepOverride;
        private float _defaultSightRange;
        private float _defaultBulletSpeed;
        private float _defaultAutoHealth;
        private Dictionary<string, object> _sightRanges;
        private Dictionary<string, object> _bulletSpeeds;
        private Dictionary<string, object> _autoHealths;

        private void Init()
        {
            LoadData();
            _autoTurretPrefabId = StringPool.Get(AutoTurretPrefab);

            _adminOverride = GetConfig("Settings", "adminOverride", true);
            _sleepOverride = GetConfig("Settings", "sleepOverride", false);
            _defaultSightRange = GetConfig("Auto", "defaultSightRange", 30f);
            _defaultBulletSpeed = GetConfig("Auto", "defaultBulletSpeed", 200f);
            _defaultAutoHealth = GetConfig("Auto", "defaultAutoHealth", 1000f);

            _sightRanges = GetConfig("Auto", "sightRanges", new Dictionary<string, object>
                { { "turretconfig.default", 30f } });
            _bulletSpeeds = GetConfig("Auto", "bulletSpeeds", new Dictionary<string, object>
                { { "turretconfig.default", 200f } });
            _autoHealths = GetConfig("Auto", "autoHealths", new Dictionary<string, object>
                { { "turretconfig.default", 1000f } });

            LoadPermissions(_sightRanges);
            LoadPermissions(_bulletSpeeds);
            LoadPermissions(_autoHealths);
        }

        private void OnServerInitialized() => LoadAutoTurrets();

        private void LoadPermissions(Dictionary<string, object> type)
        {
            foreach (var kvp in type)
            {
                if (!string.IsNullOrEmpty(kvp.Key) && !permission.PermissionExists(kvp.Key))
                    permission.RegisterPermission(kvp.Key, this);
            }
        }

        protected void LoadAutoTurrets()
        {
            var turrets = UnityEngine.Object.FindObjectsOfType<AutoTurret>();
            var i = 0;
            for (var index = turrets.Length - 1; index >= 0; index--)
            {
                UpdateAutoTurret(turrets[index]);
                i++;
            }
            PrintWarning($"Configured {i} turrets");
        }

        private void LoadData()
        {
            if (Config["VERSION"] == null)
            {
                Config["VERSION"] = Version.ToString();
                SaveConfig();
            }
        }

        protected override void LoadDefaultConfig()
        {
            Config["Settings", "adminOverride"] = true;
            Config["Settings", "sleepOverride"] = false;
            Config["Auto", "defaultSightRange"] = 30f;
            Config["Auto", "defaultBulletSpeed"] = 200f;
            Config["Auto", "defaultAutoHealth"] = 1000f;
            Config["Auto", "sightRanges"] = new Dictionary<string, object> { { "turretconfig.default", 30f } };
            Config["Auto", "bulletSpeeds"] = new Dictionary<string, object> { { "turretconfig.default", 200f } };
            Config["Auto", "autoHealths"] = new Dictionary<string, object> { { "turretconfig.default", 1000f } };
        }

        // Get value based on owner's permissions
        private T FromPermission<T>(string userID, Dictionary<string, object> options, T defaultValue)
        {
            if (!string.IsNullOrEmpty(userID) && userID != "0")
            {
                foreach (var kvp in options)
                {
                    if (permission.UserHasPermission(userID, kvp.Key))
                        return (T)Convert.ChangeType(kvp.Value, typeof(T));
                }
            }
            return defaultValue;
        }

        private void OnEntitySpawned(BaseNetworkable entity)
        {
            if (entity == null) return;
            if (entity.prefabID == _autoTurretPrefabId)
                UpdateAutoTurret((AutoTurret)entity, true);
        }

        private void UpdateAutoTurret(AutoTurret turret, bool justCreated = false)
        {
            var userID = turret.OwnerID.ToString();
            var turretHealth = FromPermission(userID, _autoHealths, _defaultAutoHealth);

            // Initialize health
            if (justCreated)
                turret._health = turretHealth;
            turret._maxHealth = turretHealth;
            turret.startHealth = turretHealth;

            // Set turret properties based on owner permissions
            turret.bulletSpeed = FromPermission(userID, _bulletSpeeds, _defaultBulletSpeed);
            turret.sightRange = FromPermission(userID, _sightRanges, _defaultSightRange);

            if (turret.IsPowered() && turret.IsOnline())
                turret.Reload();

            turret.SendNetworkUpdateImmediate();
        }

        // Prevent targeting admins or sleeping players
        private object CanBeTargeted(BaseCombatEntity target, MonoBehaviour turret)
        {
            if (!(turret is AutoTurret)) return null;

            // Check Vanish plugin
            if (target is BasePlayer)
            {
                var isInvisible = Vanish?.Call("IsInvisible", target);
                if (isInvisible != null && (bool)isInvisible)
                    return null;
            }

            var targetPlayer = target.ToPlayer();
            if (targetPlayer == null) return null;

            if (_adminOverride && targetPlayer.IsConnected && targetPlayer.net.connection.authLevel > 0)
                return false;
            if (_sleepOverride && targetPlayer.IsSleeping())
                return false;

            return null;
        }

        private T GetConfig<T>(string name, string name2, T defaultValue)
        {
            if (Config[name, name2] == null) return defaultValue;
            return (T)Convert.ChangeType(Config[name, name2], typeof(T));
        }
    }
}
```

---

## MORE CRITICAL PATTERNS

### Calling Other Plugin APIs
```csharp
[PluginReference] private Plugin Economics;
[PluginReference] private Plugin ServerRewards;

// Check if plugin is loaded
if (Economics != null)
{
    double balance = Economics.Call<double>("Balance", player.UserIDString);
    bool success = Economics.Call<bool>("Withdraw", player.UserIDString, 100.0);
}

// Safe call with null check
var result = Economics?.Call("Balance", player.UserIDString);
```

### Creating Items with Attachments
```csharp
private Item CreateWeaponWithAttachments(string shortname, ulong skin = 0)
{
    var item = ItemManager.CreateByName(shortname, 1, skin);
    if (item == null) return null;
    
    var weapon = item.GetHeldEntity() as BaseProjectile;
    if (weapon != null)
    {
        // Add attachments
        var silencer = ItemManager.CreateByName("weapon.mod.silencer");
        if (silencer != null && !silencer.MoveToContainer(item.contents))
            silencer.Remove();
        
        var scope = ItemManager.CreateByName("weapon.mod.small.scope");
        if (scope != null && !scope.MoveToContainer(item.contents))
            scope.Remove();
    }
    
    return item;
}
```

### Entity Spawning with Ownership
```csharp
private BaseEntity SpawnEntity(string prefab, Vector3 position, Quaternion rotation, ulong ownerID)
{
    var entity = GameManager.server.CreateEntity(prefab, position, rotation);
    if (entity == null) return null;
    
    entity.OwnerID = ownerID;
    entity.Spawn();
    
    return entity;
}

// Example: Spawn a box
var box = SpawnEntity(
    "assets/prefabs/deployable/large wood storage/box.wooden.large.prefab",
    player.transform.position + player.transform.forward * 2f,
    player.transform.rotation,
    player.userID
) as StorageContainer;
```

### Finding Entities in Radius
```csharp
// Find all players in radius
private List<BasePlayer> GetNearbyPlayers(Vector3 position, float radius)
{
    var players = new List<BasePlayer>();
    Vis.Entities(position, radius, players);
    return players.Where(p => p != null && !p.IsNpc && p.IsConnected).ToList();
}

// Find all entities of type in radius
private List<T> GetNearbyEntities<T>(Vector3 position, float radius) where T : BaseEntity
{
    var entities = new List<T>();
    Vis.Entities(position, radius, entities);
    return entities.Where(e => e != null && !e.IsDestroyed).ToList();
}

// Find building blocks in radius
var blocks = new List<BuildingBlock>();
Vis.Entities(position, 10f, blocks, Layers.Mask.Construction);
```

### Safe Player Messaging
```csharp
private void SendMessage(BasePlayer player, string message)
{
    if (player == null || !player.IsConnected) return;
    player.ChatMessage(message);
}

private void SendMessageToAll(string message)
{
    foreach (var player in BasePlayer.activePlayerList)
        SendMessage(player, message);
}

// With color formatting
private void SendColoredMessage(BasePlayer player, string message)
{
    SendMessage(player, $"<color=#ff8800>[MyPlugin]</color> {message}");
}
```

### Raycast from Player Eyes
```csharp
private BaseEntity GetLookingAtEntity(BasePlayer player, float maxDistance = 10f)
{
    RaycastHit hit;
    if (!Physics.Raycast(player.eyes.HeadRay(), out hit, maxDistance))
        return null;
    
    return hit.GetEntity();
}

// With layer mask
private BuildingBlock GetLookingAtBlock(BasePlayer player)
{
    RaycastHit hit;
    if (!Physics.Raycast(player.eyes.HeadRay(), out hit, 10f, Layers.Mask.Construction))
        return null;
    
    return hit.GetEntity() as BuildingBlock;
}
```

### Timer Patterns
```csharp
// One-time delayed action
timer.Once(5f, () => {
    // Runs once after 5 seconds
});

// Repeating action
var repeatingTimer = timer.Every(1f, () => {
    // Runs every 1 second
});

// Stop repeating timer
repeatingTimer?.Destroy();

// Timer with player reference (safe)
timer.Once(5f, () => {
    if (player == null || !player.IsConnected) return;
    player.ChatMessage("5 seconds passed!");
});
```

### Effect/Sound Patterns
```csharp
// Play effect at position
Effect.server.Run("assets/bundled/prefabs/fx/survey_explosion.prefab", position);

// Play effect on entity
Effect.server.Run("assets/bundled/prefabs/fx/impacts/additive/fire.prefab", entity.transform.position);

// Common effect prefabs:
// "assets/bundled/prefabs/fx/survey_explosion.prefab" - Survey charge explosion
// "assets/bundled/prefabs/fx/ore_break.prefab" - Ore breaking
// "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab" - Rock hit
// "assets/bundled/prefabs/fx/gestures/drink_vomit.prefab" - Vomit effect
// "assets/prefabs/misc/xmas/presents/effects/unwrap.prefab" - Unwrap effect
```


### Building Privilege Check
```csharp
// Check if player has building privilege
private bool HasBuildingPrivilege(BasePlayer player)
{
    return player.IsBuildingAuthed();
}

// Check if player can build at position
private bool CanBuildAt(BasePlayer player, Vector3 position)
{
    return player.CanBuild(new OBB(position, Quaternion.identity, Vector3.one));
}

// Get the TC that covers a position
private BuildingPrivlidge GetBuildingPrivilege(Vector3 position)
{
    var privs = new List<BuildingPrivlidge>();
    Vis.Entities(position, 1f, privs);
    return privs.FirstOrDefault(p => p.IsInsideBuildingPrivilege(position));
}
```

### Inventory Operations
```csharp
// Give item to player
private bool GiveItem(BasePlayer player, string shortname, int amount = 1, ulong skin = 0)
{
    var item = ItemManager.CreateByName(shortname, amount, skin);
    if (item == null) return false;
    return player.GiveItem(item);
}

// Take item from player
private bool TakeItem(BasePlayer player, string shortname, int amount)
{
    var itemDef = ItemManager.FindItemDefinition(shortname);
    if (itemDef == null) return false;
    
    int taken = player.inventory.Take(null, itemDef.itemid, amount);
    return taken >= amount;
}

// Check if player has item
private bool HasItem(BasePlayer player, string shortname, int amount = 1)
{
    var itemDef = ItemManager.FindItemDefinition(shortname);
    if (itemDef == null) return false;
    return player.inventory.GetAmount(itemDef.itemid) >= amount;
}

// Clear player inventory
private void ClearInventory(BasePlayer player)
{
    player.inventory.Strip();
}
```

### Zone/Area Detection
```csharp
// Check if position is in monument
private bool IsInMonument(Vector3 position)
{
    foreach (var monument in TerrainMeta.Path.Monuments)
    {
        if (monument.IsInBounds(position))
            return true;
    }
    return false;
}

// Check if position is in safe zone
private bool IsInSafeZone(Vector3 position)
{
    var zones = new List<TriggerSafeZone>();
    Vis.Entities(position, 0f, zones);
    return zones.Count > 0;
}
```

### Network Update Patterns
```csharp
// Update entity for all clients
entity.SendNetworkUpdateImmediate();

// Update specific player's client
entity.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);

// Force full update
entity.SendNetworkUpdateImmediate(true);
```

### Data File Patterns (JSON)
```csharp
// Save data
private void SaveData<T>(T data, string filename)
{
    Interface.Oxide.DataFileSystem.WriteObject(filename, data);
}

// Load data
private T LoadData<T>(string filename) where T : new()
{
    try
    {
        return Interface.Oxide.DataFileSystem.ReadObject<T>(filename) ?? new T();
    }
    catch
    {
        return new T();
    }
}

// Usage
private PluginData _data;

private void Init()
{
    _data = LoadData<PluginData>(Name);
}

private void Unload()
{
    SaveData(_data, Name);
}
```

### ProtoStorage (Binary - Faster for Large Data)
```csharp
using ProtoBuf;

[ProtoContract]
private class PluginData
{
    [ProtoMember(1)]
    public Dictionary<ulong, PlayerInfo> Players { get; set; } = new Dictionary<ulong, PlayerInfo>();
}

[ProtoContract]
private class PlayerInfo
{
    [ProtoMember(1)]
    public int Kills { get; set; }
    
    [ProtoMember(2)]
    public int Deaths { get; set; }
}

// Save
ProtoStorage.Save(_data, Name);

// Load
_data = ProtoStorage.Load<PluginData>(Name) ?? new PluginData();
```


---

## EXAMPLE: NIGHTLANTERN (Complete - Harmony Patching, Coroutines, TOD_Sky Events)

This is a complete production plugin showing: Harmony patches, coroutines, TOD_Sky sunrise/sunset events, component-based entity management, and complex configuration.

```csharp
using HarmonyLib;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Night Lantern", "k1lly0u", "2.1.1")]
    [Description("Automatically turns ON and OFF lanterns after sunset and sunrise")]
    class NightLantern : RustPlugin
    {
        #region Fields
        [PluginReference] private Plugin NoFuelRequirements;

        private static Hash<ulong, Dictionary<EntityType, bool>> _toggleList = new Hash<ulong, Dictionary<EntityType, bool>>();
        private readonly HashSet<LightController> _lightControllers = new HashSet<LightController>();
        private static readonly Hash<BaseOven, LightController> OvenControllers = new Hash<BaseOven, LightController>();

        private bool _lightsOn = false;
        private bool _globalToggle = true;
        private Timer _timeCheck;
        private static Func<string, ulong, object> _ignoreFuelConsumptionFunction;
        #endregion

        #region Oxide Hooks
        private void Init()
        {
            permission.RegisterPermission("nightlantern.global", this);
            foreach (EntityType type in (EntityType[])Enum.GetValues(typeof(EntityType)))
            {
                if (type == EntityType.CeilingLight) continue;
                permission.RegisterPermission($"nightlantern.{type}", this);
            }
            _ignoreFuelConsumptionFunction = NoFuelRequirementsIgnoreFuelConsumption;
            LoadData();
            Unsubscribe(nameof(OnEntitySpawned));
            Unsubscribe(nameof(OnEntityKill));
        }

        private void OnServerInitialized()
        {
            Subscribe(nameof(OnEntitySpawned));
            Subscribe(nameof(OnEntityKill));
            ServerMgr.Instance.StartCoroutine(CreateAllLights(
                BaseNetworkable.serverEntities.Where(x => x is BaseOven || x is SearchLight)));
        }

        private object OnFuelConsume(BaseOven baseOven, Item fuel, ItemModBurnable burnable)
        {
            if (!baseOven || baseOven.IsDestroyed) return null;
            if (!OvenControllers.TryGetValue(baseOven, out LightController lightController) || !lightController)
                return null;
            return lightController.OnConsumeFuel();
        }

        private void OnOvenToggle(BaseOven baseOven, BasePlayer player)
        {
            if (!baseOven || baseOven.IsDestroyed) return;
            if (baseOven.needsBuildingPrivilegeToUse && !player.CanBuild()) return;
            if (!OvenControllers.TryGetValue(baseOven, out LightController lightController) || !lightController)
                return;
            lightController.OnOvenToggled();
        }

        private void OnEntitySpawned(BaseEntity entity) => InitializeLightController(entity);

        private void OnEntityKill(BaseNetworkable entity)
        {
            LightController lightController = entity.GetComponent<LightController>();
            if (lightController)
            {
                _lightControllers.Remove(lightController);
                UnityEngine.Object.Destroy(lightController);
            }
        }

        private void OnServerSave() => SaveData();

        private void Unload()
        {
            foreach (LightController lightController in _lightControllers)
            {
                lightController.ToggleLight(false);
                UnityEngine.Object.DestroyImmediate(lightController);
            }
            _timeCheck?.Destroy();
            _lightControllers.Clear();
            _configData = null;
            _toggleList = null;
        }
        #endregion

        #region Functions
        private IEnumerator CreateAllLights(IEnumerable<BaseNetworkable> entities)
        {
            foreach (BaseNetworkable baseNetworkable in entities)
            {
                yield return new WaitForSeconds(UnityEngine.Random.Range(0.1f, 0.25f));
                if (!baseNetworkable || baseNetworkable.IsDestroyed) continue;
                InitializeLightController(baseNetworkable as BaseEntity);
            }
            CheckCurrentTime();
        }

        private void InitializeLightController(BaseEntity entity)
        {
            if (!entity || entity.IsDestroyed) return;
            EntityType entityType = StringToType(entity.ShortPrefabName);
            if (entityType == EntityType.None || !_configData.Types[entityType].Enabled) return;
            _lightControllers.Add(entity.GetOrAddComponent<LightController>());
        }

        private void CheckCurrentTime()
        {
            if (_globalToggle)
            {
                float time = TOD_Sky.Instance.Cycle.Hour;
                if (time >= _configData.Sunset || (time >= 0 && time < _configData.Sunrise))
                {
                    if (!_lightsOn)
                    {
                        ServerMgr.Instance.StartCoroutine(ToggleAllLights(_lightControllers, true));
                        _lightsOn = true;
                    }
                }
                else if (time >= _configData.Sunrise && time < _configData.Sunset)
                {
                    if (_lightsOn)
                    {
                        ServerMgr.Instance.StartCoroutine(ToggleAllLights(_lightControllers, false));
                        _lightsOn = false;
                    }
                }
            }
            _timeCheck = timer.Once(20, CheckCurrentTime);
        }

        private static IEnumerator ToggleAllLights(IEnumerable<LightController> lights, bool status)
        {
            foreach (LightController lightController in lights)
            {
                yield return new WaitForSeconds(UnityEngine.Random.Range(0.1f, 0.25f));
                if (lightController) lightController.ToggleLight(status);
            }
        }

        private static EntityType StringToType(string name)
        {
            return name switch
            {
                "campfire" => EntityType.Campfire,
                "skull_fire_pit" => EntityType.Firepit,
                "fireplace.deployed" => EntityType.Fireplace,
                "furnace" => EntityType.Furnace,
                "furnace.large" => EntityType.LargeFurnace,
                "lantern.deployed" => EntityType.Lanterns,
                "jackolantern.angry" => EntityType.JackOLantern,
                "jackolantern.happy" => EntityType.JackOLantern,
                "tunalight.deployed" => EntityType.TunaLight,
                "searchlight.deployed" => EntityType.Searchlight,
                "bbq.deployed" => EntityType.BBQ,
                "refinery_small_deployed" => EntityType.Refinery,
                _ => EntityType.None
            };
        }

        private static bool ConsumeTypeEnabled(ulong playerId, EntityType entityType)
        {
            if (_toggleList.TryGetValue(playerId, out Dictionary<EntityType, bool> userPreferences))
                return userPreferences[entityType];
            return _configData.Types[entityType].Enabled;
        }

        private object NoFuelRequirementsIgnoreFuelConsumption(string shortname, ulong playerId)
            => NoFuelRequirements?.Call("IgnoreFuelConsumption", shortname, playerId);
        #endregion

        #region Light Controller Component
        private class LightController : MonoBehaviour
        {
            private BaseEntity _entity;
            private ConfigData.LightSettings _config;
            private bool _isSearchlight;
            private bool _ignoreFuelConsumption;
            private bool _automaticallyToggled;
            public EntityType entityType;

            public bool ShouldIgnoreFuelConsumption
            {
                get
                {
                    if (_config.ConsumeFuelWhenToggled && !_automaticallyToggled) return false;
                    return _ignoreFuelConsumption || !_config.ConsumeFuel;
                }
            }

            private void Awake()
            {
                _entity = GetComponent<BaseEntity>();
                entityType = StringToType(_entity.ShortPrefabName);
                _config = _configData.Types[entityType];
                _isSearchlight = _entity is SearchLight;

                object success = _ignoreFuelConsumptionFunction(entityType.ToString(), _entity.OwnerID);
                if (success != null) _ignoreFuelConsumption = true;
            }

            private void OnEnable()
            {
                if (_entity is BaseOven baseOven)
                    OvenControllers[baseOven] = this;
            }

            private void OnDisable()
            {
                if (_entity is BaseOven baseOven)
                    OvenControllers.Remove(baseOven);
            }

            public void ToggleLight(bool status)
            {
                if (_config.Owner && !ConsumeTypeEnabled(_entity.OwnerID, entityType))
                    status = false;

                object success = Interface.CallHook("OnNightLanternToggle", _entity, status);
                if (success != null) return;

                if (_isSearchlight)
                {
                    SearchLight searchLight = _entity as SearchLight;
                    if (searchLight) searchLight.SetFlag(BaseEntity.Flags.On, status);
                }
                else
                {
                    BaseOven baseOven = _entity as BaseOven;
                    if (baseOven)
                    {
                        if (_config.ConsumeFuel)
                        {
                            if (status) baseOven.StartCooking();
                            else baseOven.StopCooking();
                        }
                        else
                        {
                            if (baseOven.IsOn() != status)
                            {
                                _automaticallyToggled = true;
                                baseOven.SetFlag(BaseEntity.Flags.On, status);
                            }
                        }
                    }
                }
                _entity.SendNetworkUpdate();
            }

            public void OnOvenToggled() => _automaticallyToggled = false;
            public object OnConsumeFuel() => ShouldIgnoreFuelConsumption ? true : (object)null;
            public bool IsOwner(ulong playerId) => _entity.OwnerID == playerId;
        }
        #endregion

        #region Harmony Patch (Advanced)
        [AutoPatch]
        [HarmonyPatch(typeof(BaseOven))]
        [HarmonyPatch(nameof(BaseOven.CanRunWithNoFuel), MethodType.Getter)]
        private static class BaseOven_CanRunWithNoFuelPatch
        {
            [HarmonyPrefix]
            private static bool Prefix(BaseOven __instance, ref bool __result)
            {
                if (!OvenControllers.TryGetValue(__instance, out LightController lightController))
                    return false;

                if (lightController && lightController.ShouldIgnoreFuelConsumption)
                {
                    __result = true;
                    return false;
                }
                return true;
            }
        }
        #endregion

        #region Commands
        [ChatCommand("lantern")]
        private void cmdLantern(BasePlayer player, string command, string[] args)
        {
            if (args.Length == 0)
            {
                // Show status
                player.ChatMessage("Night Lantern Status");
                return;
            }

            if (args[0].ToLower() == "global" && permission.UserHasPermission(player.UserIDString, "nightlantern.global"))
            {
                _globalToggle = !_globalToggle;
                ServerMgr.Instance.StartCoroutine(ToggleAllLights(_lightControllers, _globalToggle));
                player.ChatMessage($"Global toggle: {(_globalToggle ? "enabled" : "disabled")}");
            }
        }
        #endregion

        #region Config
        private enum EntityType { BBQ, Campfire, CeilingLight, Firepit, Fireplace, Furnace, LargeFurnace, Lanterns, JackOLantern, TunaLight, Searchlight, Refinery, None }

        private static ConfigData _configData;

        class ConfigData
        {
            [JsonProperty("Light Settings")]
            public Dictionary<EntityType, LightSettings> Types { get; set; }

            [JsonProperty("Time autolights are disabled")]
            public float Sunrise { get; set; }

            [JsonProperty("Time autolights are enabled")]
            public float Sunset { get; set; }

            public class LightSettings
            {
                [JsonProperty("This type is enabled")]
                public bool Enabled { get; set; }

                [JsonProperty("This type consumes fuel")]
                public bool ConsumeFuel { get; set; }

                [JsonProperty("This type consumes fuel when toggled by a player")]
                public bool ConsumeFuelWhenToggled { get; set; }

                [JsonProperty("This type can be toggled by the owner")]
                public bool Owner { get; set; }

                [JsonProperty("This type requires permission")]
                public bool Permission { get; set; }
            }

            public VersionNumber Version { get; set; }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _configData = Config.ReadObject<ConfigData>();
            if (_configData.Version < Version) UpdateConfigValues();
            Config.WriteObject(_configData, true);
        }

        protected override void LoadDefaultConfig() => _configData = GetBaseConfig();

        private ConfigData GetBaseConfig()
        {
            return new ConfigData
            {
                Types = new Dictionary<EntityType, ConfigData.LightSettings>
                {
                    [EntityType.Campfire] = new ConfigData.LightSettings { ConsumeFuel = true, Enabled = true, Permission = true, Owner = true },
                    [EntityType.Furnace] = new ConfigData.LightSettings { ConsumeFuel = true, Enabled = true, Permission = false },
                    [EntityType.Lanterns] = new ConfigData.LightSettings { ConsumeFuel = true, Enabled = true, Permission = true, Owner = true },
                    [EntityType.Searchlight] = new ConfigData.LightSettings { ConsumeFuel = true, Enabled = true, Permission = true, Owner = true }
                },
                Sunrise = 7.5f,
                Sunset = 18.5f,
                Version = Version
            };
        }

        protected override void SaveConfig() => Config.WriteObject(_configData, true);

        private void UpdateConfigValues()
        {
            PrintWarning("Config update detected! Updating...");
            _configData.Version = Version;
            PrintWarning("Config update completed!");
        }
        #endregion

        #region Data Management
        private void SaveData() => Interface.Oxide.DataFileSystem.GetFile("nightlantern_data").WriteObject(_toggleList);

        private void LoadData()
        {
            try
            {
                _toggleList = Interface.Oxide.DataFileSystem.GetFile("nightlantern_data")?.ReadObject<Hash<ulong, Dictionary<EntityType, bool>>>() 
                    ?? new Hash<ulong, Dictionary<EntityType, bool>>();
            }
            catch
            {
                _toggleList = new Hash<ulong, Dictionary<EntityType, bool>>();
            }
        }
        #endregion

        #region Localization
        private string GetMessage(string key, ulong playerId = 0U) => 
            lang.GetMessage(key, this, playerId == 0U ? null : playerId.ToString());

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["global.title"] = "<color=#FFA500>Night Lantern</color>",
                ["global.toggle"] = "Auto lights are {0} server wide",
                ["user.enabled"] = "<color=#8ee700>enabled</color>",
                ["user.disabled"] = "<color=#e90000>disabled</color>"
            }, this);
        }
        #endregion
    }
}
```

---

## EXAMPLE: PERSONALHELI (Complete - Helicopter Control, Team/Friends/Clans Integration)

This plugin shows: PatrolHelicopter spawning and control, Friends/Clans/Teams integration, MonoBehaviour components on entities, and API methods.

```csharp
using System.Collections.Generic;
using System;
using UnityEngine;
using Oxide.Core;
using Oxide.Core.Plugins;
using System.Text;
using System.Linq;
using Facepunch;

namespace Oxide.Plugins
{
    [Info("Personal Heli", "Egor Blagov", "1.1.11")]
    [Description("Calls heli to player and their team")]
    class PersonalHeli : RustPlugin
    {
        #region Constants & Dependencies
        const string permUse = "personalheli.use";
        const string permConsole = "personalheli.console";
        const float HelicopterEntitySpawnRadius = 10.0f;

        [PluginReference] Plugin Friends, Clans;
        #endregion

        #region Config
        class PluginConfig
        {
            public bool UseFriends = true;
            public bool UseTeams = true;
            public bool UseClans = true;
            public int CooldownSeconds = 1800;
            public string ChatCommand = "callheli";
            public bool ResetCooldownsOnWipe = true;
            public bool MemorizeTeamOnCall = false;
            public bool DenyCratesLooting = true;
            public bool DenyGibsMining = true;
            public bool RemoveFireFromCrates = true;
        }
        private PluginConfig config;
        #endregion

        #region Stored Data
        class StoredData
        {
            public Dictionary<ulong, CallData> CallDatas = new Dictionary<ulong, CallData>();

            public class CallData
            {
                public DateTime LastCall = DateTime.MinValue;
                
                public bool CanCallNow(int cooldown) =>
                    DateTime.Now.Subtract(LastCall).TotalSeconds > cooldown;

                public int SecondsToWait(int cooldown) =>
                    (int)Math.Round(cooldown - DateTime.Now.Subtract(LastCall).TotalSeconds);

                public void OnCall() => LastCall = DateTime.Now;
            }

            public CallData GetForPlayer(BasePlayer player)
            {
                if (!CallDatas.ContainsKey(player.userID))
                    CallDatas[player.userID] = new CallData();
                return CallDatas[player.userID];
            }
        }
        private StoredData storedData;

        private void SaveData()
        {
            if (storedData != null)
                Interface.Oxide.DataFileSystem.WriteObject(Name, storedData, true);
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
        #endregion

        #region Localization
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NoPermission"] = "You have no permission to use this command",
                ["Cooldown"] = "Helicopter call is on cooldown, time remaining: {0}",
                ["LootDenied"] = "You are forbidden to loot this crate, it belongs to: {0}",
                ["DamageDenied"] = "You are forbidden to damage this helicopter, it was called by: {0}",
                ["PlayerCalled"] = "Personal helicopter is called for {0}"
            }, this);
        }

        private string _(string key, string userId, params object[] args) =>
            string.Format(lang.GetMessage(key, this, userId), args);
        #endregion

        #region Hooks
        private void Init()
        {
            permission.RegisterPermission(permUse, this);
            permission.RegisterPermission(permConsole, this);
            config = Config.ReadObject<PluginConfig>();
            Config.WriteObject(config);
            cmd.AddChatCommand(config.ChatCommand, this, CmdCallHeli);
            LoadData();
        }

        private void Unload()
        {
            foreach (var personal in UnityEngine.Object.FindObjectsOfType<PersonalComponent>())
                UnityEngine.Object.Destroy(personal);
            SaveData();
        }

        protected override void LoadDefaultConfig() => Config.WriteObject(new PluginConfig(), true);

        private void OnNewSave()
        {
            if (config.ResetCooldownsOnWipe)
            {
                storedData = new StoredData();
                SaveData();
            }
        }

        private void OnServerSave() => SaveData();

        private void OnEntityKill(BaseEntity entity) =>
            InvokePersonal<PersonalHeliComponent>(entity.gameObject, personalHeli => personalHeli.OnKill());

        private object CanLootEntity(BasePlayer player, StorageContainer container)
        {
            return InvokePersonal<PersonalCrateComponent, object>(container?.gameObject, personalCrate =>
            {
                if (!personalCrate.CanInterractWith(player))
                {
                    SendReply(player, _("LootDenied", player.UserIDString, 
                        GetPlayerOwnerDescription(player, personalCrate.Player)));
                    return false;
                }
                return null;
            });
        }

        private object OnHelicopterTarget(HelicopterTurret turret, BaseCombatEntity entity)
        {
            return InvokePersonal<PersonalHeliComponent, object>(
                turret?._heliAI?.helicopterBase?.gameObject, 
                personalHeli => personalHeli.CanInterractWith(entity) ? null : (object)false);
        }

        private object OnPlayerAttack(BasePlayer attacker, HitInfo info)
        {
            return InvokePersonal<PersonalHeliComponent, object>(info?.HitEntity?.gameObject, personalHeli =>
            {
                if (!personalHeli.CanInterractWith(attacker))
                {
                    SendReply(attacker, _("DamageDenied", attacker.UserIDString, 
                        GetPlayerOwnerDescription(attacker, personalHeli.Player)));
                    return false;
                }
                return null;
            });
        }
        #endregion

        #region Core Methods
        private bool CallHeliForPlayer(BasePlayer player)
        {
            var playerPos = player.transform.position;
            float mapWidth = (TerrainMeta.Size.x / 2) - 50f;
            var heliPos = new Vector3(
                playerPos.x < 0 ? -mapWidth : mapWidth,
                30,
                playerPos.z < 0 ? -mapWidth : mapWidth
            );

            PatrolHelicopter heli = GameManager.server.CreateEntity(
                "assets/prefabs/npc/patrol helicopter/patrolhelicopter.prefab", 
                new Vector3(), new Quaternion(), true) as PatrolHelicopter;
            
            if (!heli) return false;
            
            heli.Spawn();
            heli.transform.position = heliPos;
            
            var component = heli.gameObject.AddComponent<PersonalHeliComponent>();
            component.Init(this, player);
            
            foreach (var p in BasePlayer.activePlayerList)
                SendReply(p, _("PlayerCalled", p.UserIDString, $"<color=#63ff64>{player.displayName}</color>"));
            
            return true;
        }

        private void CmdCallHeli(BasePlayer player, string cmd, string[] argv)
        {
            if (!permission.UserHasPermission(player.UserIDString, permUse))
            {
                SendReply(player, _("NoPermission", player.UserIDString));
                return;
            }

            StoredData.CallData callData = storedData.GetForPlayer(player);
            if (!callData.CanCallNow(config.CooldownSeconds))
            {
                SendReply(player, _("Cooldown", player.UserIDString, 
                    TimeSpan.FromSeconds(callData.SecondsToWait(config.CooldownSeconds))));
                return;
            }

            if (CallHeliForPlayer(player))
                callData.OnCall();
        }

        private string GetPlayerOwnerDescription(BasePlayer player, BasePlayer playerOwner)
        {
            StringBuilder result = new StringBuilder($"<color=#63ff64>{playerOwner.displayName}</color>");
            if (config.UseFriends && Friends != null) result.Append(", their friends");
            if (config.UseTeams) result.Append(", their team");
            if (config.UseClans) result.Append(", their clan");
            return result.ToString();
        }

        private T InvokePersonal<C, T>(GameObject obj, Func<C, T> action) where C : PersonalComponent
        {
            var comp = obj?.GetComponent<C>();
            if (comp == null) return default(T);
            return action(comp);
        }

        private void InvokePersonal<C>(GameObject obj, Action<C> action) where C : PersonalComponent =>
            InvokePersonal<C, object>(obj, comp => { action(comp); return null; });
        #endregion

        #region API
        private bool IsPersonal(PatrolHelicopter heli) =>
            InvokePersonal<PersonalHeliComponent, object>(heli?.gameObject, (comp) => true) != null;
        #endregion

        #region Components
        abstract class PersonalComponent : FacepunchBehaviour
        {
            protected PersonalHeli Plugin;
            protected PluginConfig Config => Plugin.config;
            public List<BasePlayer> SavedTeam;
            public BasePlayer Player;

            public void Init(PersonalHeli plugin, BasePlayer player)
            {
                Player = player;
                Plugin = plugin;
                OnInitChild();
            }

            protected virtual void OnInitChild() { }

            public virtual bool CanInterractWith(BaseEntity target)
            {
                if (Config.MemorizeTeamOnCall && SavedTeam != null)
                    return SavedTeam.Contains(target as BasePlayer);

                if (!(target is BasePlayer) || target is NPCPlayer) return false;
                if (target == Player) return true;

                if (Plugin.config.UseFriends && AreFriends(target as BasePlayer)) return true;
                if (Plugin.config.UseTeams && AreSameTeam(target as BasePlayer)) return true;
                if (Plugin.config.UseClans && AreSameClan(target as BasePlayer)) return true;

                return false;
            }

            protected bool AreSameClan(BasePlayer basePlayer)
            {
                if (Plugin.Clans == null) return false;
                var playerClan = Plugin.Clans.Call<string>("GetClanOf", Player);
                var otherPlayerClan = Plugin.Clans.Call<string>("GetClanOf", basePlayer);
                if (playerClan == null || otherPlayerClan == null) return false;
                return playerClan == otherPlayerClan;
            }

            protected bool AreSameTeam(BasePlayer otherPlayer)
            {
                if (Player.currentTeam == 0UL || otherPlayer.currentTeam == 0UL) return false;
                return Player.currentTeam == otherPlayer.currentTeam;
            }

            protected bool AreFriends(BasePlayer otherPlayer)
            {
                if (Plugin.Friends == null) return false;
                return Plugin.Friends.Call<bool>("AreFriends", Player.userID, otherPlayer.userID);
            }

            private void OnDestroy() => OnDestroyChild();
            protected virtual void OnDestroyChild() { }
        }

        class PersonalCrateComponent : PersonalComponent
        {
            private StorageContainer Crate;
            private void Awake() => Crate = GetComponent<StorageContainer>();
            protected override void OnDestroyChild()
            {
                if (Crate != null && Crate.IsValid() && !Crate.IsDestroyed)
                    Crate.Kill();
            }
        }

        class PersonalGibComponent : PersonalComponent
        {
            private HelicopterDebris Gib;
            private void Awake() => Gib = GetComponent<HelicopterDebris>();
            protected override void OnDestroyChild()
            {
                if (Gib != null && Gib.IsValid() && !Gib.IsDestroyed)
                    Gib.Kill();
            }
        }

        class PersonalHeliComponent : PersonalComponent
        {
            private const int MaxHeliDistanceToPlayer = 140;
            public static List<PersonalHeliComponent> ActiveHelis = new List<PersonalHeliComponent>();
            private PatrolHelicopter Heli;
            private PatrolHelicopterAI HeliAi => Heli.GetComponent<PatrolHelicopterAI>();

            private void Awake() => Heli = GetComponent<PatrolHelicopter>();

            protected override void OnInitChild()
            {
                HeliAi.State_Move_Enter(Player.transform.position + new Vector3(
                    UnityEngine.Random.Range(10f, 50f), 20f, UnityEngine.Random.Range(10f, 50f)));
                InvokeRepeating(new Action(UpdateTargets), 5.0f, 5.0f);
                if (Config.MemorizeTeamOnCall) SavedTeam = GetAllPlayersInTeam();
                ActiveHelis.Add(this);
            }

            private void UpdateTargets()
            {
                if (HeliAi._targetList.Count == 0)
                {
                    List<BasePlayer> team = Config.MemorizeTeamOnCall ? SavedTeam : GetAllPlayersInTeam();
                    foreach (var player in team)
                    {
                        if (player != null && player.IsConnected)
                            HeliAi._targetList.Add(new PatrolHelicopterAI.targetinfo(Player, Player));
                    }
                }

                if (HeliAi._targetList.Count == 1 && HeliAi._targetList[0].ply == Player &&
                    Vector3Ex.Distance2D(Heli.transform.position, Player.transform.position) > MaxHeliDistanceToPlayer)
                {
                    if (HeliAi._currentState != PatrolHelicopterAI.aiState.MOVE || 
                        Vector3Ex.Distance2D(HeliAi.destination, Player.transform.position) > MaxHeliDistanceToPlayer)
                    {
                        HeliAi.ExitCurrentState();
                        var heliTarget = Player.transform.position.XZ() + Vector3.up * 250;
                        HeliAi.State_Move_Enter(heliTarget);
                    }
                }
            }

            protected override void OnDestroyChild()
            {
                CancelInvoke(new Action(UpdateTargets));
                if (Heli != null && Heli.IsValid() && !Heli.IsDestroyed)
                    Heli.Kill();
                ActiveHelis.Remove(this);
            }

            private List<BasePlayer> GetAllPlayersInTeam()
            {
                var fullTeam = new List<BasePlayer>();
                foreach (var player in BasePlayer.activePlayerList)
                {
                    if (player == Player) fullTeam.Add(player);
                    else if ((Config.UseFriends && AreFriends(player)) ||
                            Config.UseClans && AreSameClan(player) ||
                            Config.UseTeams && AreSameTeam(player))
                        fullTeam.Add(player);
                }
                return fullTeam;
            }

            public void OnKill()
            {
                if (Config.DenyCratesLooting)
                {
                    var crates = Pool.GetList<LootContainer>();
                    Vis.Entities(Heli.transform.position, HelicopterEntitySpawnRadius, crates);
                    foreach (var crate in crates)
                    {
                        var component = crate.gameObject.AddComponent<PersonalCrateComponent>();
                        component.Init(Plugin, Player);
                        if (Config.MemorizeTeamOnCall) component.SavedTeam = SavedTeam;
                        if (Config.RemoveFireFromCrates && crate is LockedByEntCrate)
                            (crate as LockedByEntCrate).lockingEnt?.ToBaseEntity()?.Kill();
                    }
                    Pool.FreeList(ref crates);
                }

                if (Config.DenyGibsMining)
                {
                    var gibs = Pool.GetList<HelicopterDebris>();
                    Vis.Entities(Heli.transform.position, HelicopterEntitySpawnRadius, gibs);
                    foreach (var gib in gibs)
                    {
                        var component = gib.gameObject.AddComponent<PersonalGibComponent>();
                        component.Init(Plugin, Player);
                        if (Config.MemorizeTeamOnCall) component.SavedTeam = SavedTeam;
                    }
                    Pool.FreeList(ref gibs);
                }
            }
        }
        #endregion
    }
}
```


---

## EXAMPLE: GATHERMANAGER (Gathering Hooks, Resource Modification)

Key patterns from GatherManager showing how to modify gathering amounts:

```csharp
// Modify items gathered from dispensers (trees, ores, corpses)
private void OnDispenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item)
{
    if (!entity.ToPlayer()) return;  // Only for players

    var gatherType = dispenser.gatherType.ToString("G");  // "Tree", "Ore", "Flesh"
    
    float modifier;
    if (GatherResourceModifiers.TryGetValue(item.info.displayName.english, out modifier))
    {
        item.amount = (int)(item.amount * modifier);
    }
    else if (GatherResourceModifiers.TryGetValue("*", out modifier))  // Wildcard
    {
        item.amount = (int)(item.amount * modifier);
    }
}

// Bonus items from dispensers
private void OnDispenserBonus(ResourceDispenser dispenser, BaseEntity entity, Item item)
{
    OnDispenserGather(dispenser, entity, item);  // Same logic
}

// Modify items from growables (plants)
private void OnGrowableGathered(GrowableEntity growable, Item item, BasePlayer player)
{
    float modifier;
    if (GatherResourceModifiers.TryGetValue(item.info.displayName.english, out modifier))
    {
        item.amount = (int)(item.amount * modifier);
    }
}

// Modify quarry output
private void OnQuarryGather(MiningQuarry quarry, Item item)
{
    float modifier;
    if (QuarryResourceModifiers.TryGetValue(item.info.displayName.english, out modifier))
    {
        item.amount = (int)(item.amount * modifier);
    }
}

// Modify excavator output
private void OnExcavatorGather(ExcavatorArm excavator, Item item)
{
    float modifier;
    if (ExcavatorResourceModifiers.TryGetValue(item.info.displayName.english, out modifier))
    {
        item.amount = (int)(item.amount * modifier);
    }
}

// Modify collectible pickups (hemp, mushrooms, etc)
private void OnCollectiblePickup(CollectibleEntity collectible, BasePlayer player)
{
    foreach (ItemAmount item in collectible.itemList)
    {
        float modifier;
        if (PickupResourceModifiers.TryGetValue(item.itemDef.displayName.english, out modifier))
        {
            item.amount = (int)(item.amount * modifier);
        }
    }
}

// Modify survey charge results
private void OnSurveyGather(SurveyCharge surveyCharge, Item item)
{
    float modifier;
    if (SurveyResourceModifiers.TryGetValue(item.info.displayName.english, out modifier))
    {
        item.amount = (int)(item.amount * modifier);
    }
}

// Modify quarry tick rate
private void OnMiningQuarryEnabled(MiningQuarry quarry)
{
    if (MiningQuarryResourceTickRate == DefaultTickRate) return;
    quarry.CancelInvoke("ProcessResources");
    quarry.InvokeRepeating("ProcessResources", MiningQuarryResourceTickRate, MiningQuarryResourceTickRate);
}
```

---

## CRITICAL: COMMON HOOK PATTERNS

### Blocking Actions (Return false or non-null)
```csharp
// Block looting
private object CanLootEntity(BasePlayer player, StorageContainer container)
{
    if (ShouldBlock(player, container))
        return false;  // Block
    return null;  // Allow
}

// Block building
private object CanBuild(Planner planner, Construction prefab, Construction.Target target)
{
    var player = planner.GetOwnerPlayer();
    if (ShouldBlockBuilding(player))
        return false;
    return null;
}

// Block mounting
private object CanMountEntity(BasePlayer player, BaseMountable entity)
{
    if (ShouldBlockMount(player, entity))
        return false;
    return null;
}

// Block damage
private object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
{
    if (ShouldBlockDamage(entity, info))
    {
        info.damageTypes.ScaleAll(0);
        return true;  // Block
    }
    return null;
}
```

### Modifying Values
```csharp
// Modify damage
private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
{
    if (entity is BasePlayer)
    {
        info.damageTypes.ScaleAll(0.5f);  // 50% damage
    }
}

// Modify item amounts
private void OnDispenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item)
{
    item.amount *= 2;  // Double resources
}
```

### Entity Ownership Checks
```csharp
private bool IsOwner(BaseEntity entity, BasePlayer player)
{
    return entity.OwnerID == player.userID;
}

private bool IsAuthorized(BaseEntity entity, BasePlayer player)
{
    // Check building privilege
    var priv = entity.GetBuildingPrivilege();
    if (priv != null)
        return priv.IsAuthed(player);
    return entity.OwnerID == player.userID;
}
```

---

## CRITICAL: UI PATTERNS (CUI)

### Basic Panel with Label
```csharp
private void ShowUI(BasePlayer player)
{
    var container = new CuiElementContainer();
    
    // Main panel
    container.Add(new CuiPanel
    {
        Image = { Color = "0.1 0.1 0.1 0.9" },
        RectTransform = { AnchorMin = "0.3 0.3", AnchorMax = "0.7 0.7" },
        CursorEnabled = true
    }, "Overlay", "MyPanel");
    
    // Label
    container.Add(new CuiLabel
    {
        Text = { Text = "Hello World", FontSize = 20, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
        RectTransform = { AnchorMin = "0 0.8", AnchorMax = "1 1" }
    }, "MyPanel");
    
    // Button
    container.Add(new CuiButton
    {
        Button = { Color = "0.2 0.6 0.2 0.9", Command = "mycommand.close" },
        RectTransform = { AnchorMin = "0.4 0.1", AnchorMax = "0.6 0.2" },
        Text = { Text = "Close", FontSize = 14, Align = TextAnchor.MiddleCenter }
    }, "MyPanel");
    
    CuiHelper.AddUi(player, container);
}

private void DestroyUI(BasePlayer player)
{
    CuiHelper.DestroyUi(player, "MyPanel");
}
```

### Progress Bar
```csharp
private void ShowProgressBar(BasePlayer player, float progress)
{
    CuiHelper.DestroyUi(player, "ProgressBar");
    
    var container = new CuiElementContainer();
    
    // Background
    container.Add(new CuiPanel
    {
        Image = { Color = "0.2 0.2 0.2 0.8" },
        RectTransform = { AnchorMin = "0.3 0.45", AnchorMax = "0.7 0.55" }
    }, "Overlay", "ProgressBar");
    
    // Fill (based on progress 0-1)
    container.Add(new CuiPanel
    {
        Image = { Color = "0.2 0.8 0.2 0.9" },
        RectTransform = { AnchorMin = "0.01 0.1", AnchorMax = $"{0.01 + (0.98 * progress)} 0.9" }
    }, "ProgressBar");
    
    // Text
    container.Add(new CuiLabel
    {
        Text = { Text = $"{(progress * 100):F0}%", FontSize = 14, Align = TextAnchor.MiddleCenter },
        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
    }, "ProgressBar");
    
    CuiHelper.AddUi(player, container);
}
```

---

## CRITICAL: VEHICLE PATTERNS

### Spawning Vehicles
```csharp
// Spawn minicopter
private Minicopter SpawnMinicopter(Vector3 position, Quaternion rotation, ulong ownerID)
{
    var mini = GameManager.server.CreateEntity(
        "assets/content/vehicles/minicopter/minicopter.entity.prefab",
        position, rotation) as Minicopter;
    
    if (mini == null) return null;
    
    mini.OwnerID = ownerID;
    mini.Spawn();
    
    // Add fuel
    var fuelContainer = mini.GetFuelSystem()?.GetFuelContainer();
    if (fuelContainer != null)
    {
        var fuel = ItemManager.CreateByName("lowgradefuel", 100);
        fuel.MoveToContainer(fuelContainer.inventory);
    }
    
    return mini;
}

// Spawn boat
private MotorRowboat SpawnBoat(Vector3 position, Quaternion rotation, ulong ownerID)
{
    var boat = GameManager.server.CreateEntity(
        "assets/content/vehicles/boats/rowboat/rowboat.prefab",
        position, rotation) as MotorRowboat;
    
    if (boat == null) return null;
    
    boat.OwnerID = ownerID;
    boat.Spawn();
    return boat;
}

// Spawn horse
private RidableHorse SpawnHorse(Vector3 position, Quaternion rotation, ulong ownerID)
{
    var horse = GameManager.server.CreateEntity(
        "assets/content/vehicles/horse/ridablehorse.prefab",
        position, rotation) as RidableHorse;
    
    if (horse == null) return null;
    
    horse.OwnerID = ownerID;
    horse.Spawn();
    return horse;
}
```

### Vehicle Hooks
```csharp
// When player mounts vehicle
private void OnEntityMounted(BaseMountable mountable, BasePlayer player)
{
    var vehicle = mountable.VehicleParent();
    if (vehicle == null) return;
    
    // Do something when player mounts
}

// When player dismounts
private void OnEntityDismounted(BaseMountable mountable, BasePlayer player)
{
    var vehicle = mountable.VehicleParent();
    if (vehicle == null) return;
    
    // Do something when player dismounts
}

// Block mounting
private object CanMountEntity(BasePlayer player, BaseMountable mountable)
{
    var vehicle = mountable.VehicleParent();
    if (vehicle != null && vehicle.OwnerID != 0 && vehicle.OwnerID != player.userID)
    {
        player.ChatMessage("This vehicle belongs to someone else!");
        return false;
    }
    return null;
}
```


---

## CRITICAL: NPC PATTERNS

### Spawning NPCs
```csharp
// Spawn scientist NPC
private ScientistNPC SpawnScientist(Vector3 position, Quaternion rotation)
{
    var npc = GameManager.server.CreateEntity(
        "assets/prefabs/npc/scientist/scientist.prefab",
        position, rotation) as ScientistNPC;
    
    if (npc == null) return null;
    
    npc.Spawn();
    npc.SetHealth(200f);
    
    return npc;
}

// Spawn murderer NPC
private ScarecrowNPC SpawnMurderer(Vector3 position, Quaternion rotation)
{
    var npc = GameManager.server.CreateEntity(
        "assets/prefabs/npc/scarecrow/scarecrow.prefab",
        position, rotation) as ScarecrowNPC;
    
    if (npc == null) return null;
    
    npc.Spawn();
    return npc;
}

// Give NPC a weapon
private void GiveNPCWeapon(BasePlayer npc, string weaponShortname)
{
    var item = ItemManager.CreateByName(weaponShortname);
    if (item == null) return;
    
    if (!item.MoveToContainer(npc.inventory.containerBelt))
        item.Remove();
    else
    {
        var weapon = item.GetHeldEntity() as BaseProjectile;
        if (weapon != null)
        {
            weapon.primaryMagazine.contents = weapon.primaryMagazine.capacity;
        }
    }
}
```

### NPC Hooks
```csharp
// When NPC is killed
private void OnEntityDeath(ScientistNPC npc, HitInfo info)
{
    var killer = info?.InitiatorPlayer;
    if (killer != null)
    {
        // Reward killer
    }
}

// Block NPC targeting
private object OnNpcTarget(BaseNpc npc, BaseEntity target)
{
    if (target is BasePlayer player && ShouldIgnore(player))
        return true;  // Block targeting
    return null;
}
```

---

## CRITICAL: LOOT/CONTAINER PATTERNS

### Modifying Loot
```csharp
// When loot spawns in container
private void OnLootSpawn(LootContainer container)
{
    if (container.inventory == null) return;
    
    // Double all loot
    foreach (var item in container.inventory.itemList.ToList())
    {
        item.amount *= 2;
        item.MarkDirty();
    }
}

// Add custom items to loot
private void OnLootSpawn(LootContainer container)
{
    if (container.ShortPrefabName.Contains("crate_elite"))
    {
        var customItem = ItemManager.CreateByName("rifle.ak", 1);
        if (customItem != null)
            customItem.MoveToContainer(container.inventory);
    }
}

// Populate container manually
private void PopulateContainer(StorageContainer container, List<ItemInfo> items)
{
    container.inventory.Clear();
    
    foreach (var info in items)
    {
        var item = ItemManager.CreateByName(info.Shortname, info.Amount, info.SkinID);
        if (item != null)
            item.MoveToContainer(container.inventory);
    }
}
```

---

## CRITICAL: COMBAT/DAMAGE PATTERNS

### Damage Modification
```csharp
// Scale all damage
private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
{
    // Double damage to NPCs
    if (entity is BaseNpc)
        info.damageTypes.ScaleAll(2f);
    
    // Halve damage to players
    if (entity is BasePlayer)
        info.damageTypes.ScaleAll(0.5f);
}

// Block specific damage types
private object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
{
    // Block fall damage
    if (info.damageTypes.Has(DamageType.Fall))
    {
        info.damageTypes.Scale(DamageType.Fall, 0);
        return true;
    }
    
    // Block decay
    if (info.damageTypes.Has(DamageType.Decay))
    {
        info.damageTypes.Scale(DamageType.Decay, 0);
        return true;
    }
    
    return null;
}

// Get attacker info
private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
{
    var attacker = info.InitiatorPlayer;
    if (attacker == null) return;
    
    var weapon = info.Weapon?.GetItem()?.info?.shortname;
    var distance = Vector3.Distance(attacker.transform.position, entity.transform.position);
    var damageType = info.damageTypes.GetMajorityDamageType();
    
    Puts($"{attacker.displayName} hit {entity.ShortPrefabName} with {weapon} from {distance}m ({damageType})");
}
```

### PvP Detection
```csharp
// Detect PvP hit
private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
{
    var victim = entity as BasePlayer;
    var attacker = info?.InitiatorPlayer;
    
    if (victim == null || attacker == null) return;
    if (victim == attacker) return;  // Self damage
    if (!victim.userID.IsSteamId() || !attacker.userID.IsSteamId()) return;  // NPCs
    
    // This is PvP
    OnPvPHit(attacker, victim, info);
}

private void OnPvPHit(BasePlayer attacker, BasePlayer victim, HitInfo info)
{
    // Handle PvP event
}
```

---

## CRITICAL: ZONE/AREA PATTERNS

### Check if in Monument
```csharp
private bool IsInMonument(Vector3 position)
{
    foreach (var monument in TerrainMeta.Path.Monuments)
    {
        if (monument.IsInBounds(position))
            return true;
    }
    return false;
}

private MonumentInfo GetMonument(Vector3 position)
{
    foreach (var monument in TerrainMeta.Path.Monuments)
    {
        if (monument.IsInBounds(position))
            return monument;
    }
    return null;
}
```

### Check if in Safe Zone
```csharp
private bool IsInSafeZone(BasePlayer player)
{
    return player.InSafeZone();
}

private bool IsInSafeZone(Vector3 position)
{
    var triggers = new List<TriggerSafeZone>();
    Vis.Entities(position, 0f, triggers);
    return triggers.Count > 0;
}
```

### Check if in Building
```csharp
private bool IsInBuilding(Vector3 position)
{
    var building = BuildingManager.server.GetBuilding(position);
    return building != null;
}

private BuildingPrivlidge GetTC(Vector3 position)
{
    var privs = new List<BuildingPrivlidge>();
    Vis.Entities(position, 1f, privs);
    return privs.FirstOrDefault(p => p.IsInsideBuildingPrivilege(position));
}
```

---

## CRITICAL: TEAM/FRIENDS/CLAN PATTERNS

### Check if Friends (using Friends API)
```csharp
[PluginReference] private Plugin Friends;

private bool AreFriends(ulong playerId, ulong friendId)
{
    if (Friends == null) return false;
    return Friends.Call<bool>("AreFriends", playerId, friendId);
}

private bool IsFriend(ulong playerId, ulong friendId)
{
    if (Friends == null) return false;
    return Friends.Call<bool>("IsFriend", playerId, friendId);
}
```

### Check if Same Team (Built-in)
```csharp
private bool AreSameTeam(BasePlayer player1, BasePlayer player2)
{
    if (player1.currentTeam == 0UL || player2.currentTeam == 0UL)
        return false;
    return player1.currentTeam == player2.currentTeam;
}

private List<BasePlayer> GetTeamMembers(BasePlayer player)
{
    var members = new List<BasePlayer>();
    if (player.currentTeam == 0UL) return members;
    
    var team = RelationshipManager.ServerInstance.FindTeam(player.currentTeam);
    if (team == null) return members;
    
    foreach (var memberId in team.members)
    {
        var member = BasePlayer.FindByID(memberId);
        if (member != null)
            members.Add(member);
    }
    return members;
}
```

### Check if Same Clan (using Clans API)
```csharp
[PluginReference] private Plugin Clans;

private bool AreSameClan(ulong playerId1, ulong playerId2)
{
    if (Clans == null) return false;
    
    var clan1 = Clans.Call<string>("GetClanOf", playerId1);
    var clan2 = Clans.Call<string>("GetClanOf", playerId2);
    
    if (string.IsNullOrEmpty(clan1) || string.IsNullOrEmpty(clan2))
        return false;
    
    return clan1 == clan2;
}

private string GetClanTag(ulong playerId)
{
    if (Clans == null) return null;
    return Clans.Call<string>("GetClanOf", playerId);
}
```

### Combined Check
```csharp
private bool IsAlly(BasePlayer player1, BasePlayer player2)
{
    if (player1 == player2) return true;
    if (AreSameTeam(player1, player2)) return true;
    if (AreFriends(player1.userID, player2.userID)) return true;
    if (AreSameClan(player1.userID, player2.userID)) return true;
    return false;
}
```


---

## COMPLETE EXAMPLE: KITS PLUGIN PATTERNS

### Kit Data Storage Pattern
```csharp
private class KitData
{
    public Dictionary<string, Kit> Kits = new Dictionary<string, Kit>();
    
    public class Kit
    {
        public string Name;
        public string Description;
        public string RequiredPermission;
        public int Cooldown;
        public int MaximumUses;
        public int Cost;
        public bool IsHidden;
        public string KitImage;
        public ItemData[] MainItems = new ItemData[0];
        public ItemData[] WearItems = new ItemData[0];
        public ItemData[] BeltItems = new ItemData[0];
    }
}

private class ItemData
{
    public int ItemID;
    public int Amount;
    public ulong Skin;
    public int Position;
    public string Ammotype;
    public int BlueprintItemID;
    public ItemData[] Contents;
}
```

### Player Usage Tracking Pattern
```csharp
private class PlayerData
{
    public Dictionary<ulong, PlayerUsageData> Players = new Dictionary<ulong, PlayerUsageData>();
    
    public class PlayerUsageData
    {
        public Dictionary<string, int> KitUses = new Dictionary<string, int>();
        public Dictionary<string, double> Cooldowns = new Dictionary<string, double>();
        
        public int GetKitUses(string kitName)
        {
            return KitUses.TryGetValue(kitName, out int uses) ? uses : 0;
        }
        
        public double GetCooldownRemaining(string kitName)
        {
            if (!Cooldowns.TryGetValue(kitName, out double endTime))
                return 0;
            return Math.Max(0, endTime - CurrentTime);
        }
        
        public void OnKitClaimed(string kitName, int cooldown)
        {
            if (!KitUses.ContainsKey(kitName))
                KitUses[kitName] = 0;
            KitUses[kitName]++;
            
            if (cooldown > 0)
                Cooldowns[kitName] = CurrentTime + cooldown;
        }
    }
}

private static double CurrentTime => DateTime.UtcNow.Subtract(Epoch).TotalSeconds;
private static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0);
```


### Give Kit Items Pattern
```csharp
private void GiveKit(BasePlayer player, KitData.Kit kit)
{
    // Give main inventory items
    foreach (var itemData in kit.MainItems)
        GiveItem(player, itemData, player.inventory.containerMain);
    
    // Give wear items
    foreach (var itemData in kit.WearItems)
        GiveItem(player, itemData, player.inventory.containerWear);
    
    // Give belt items
    foreach (var itemData in kit.BeltItems)
        GiveItem(player, itemData, player.inventory.containerBelt);
}

private void GiveItem(BasePlayer player, ItemData itemData, ItemContainer container)
{
    var item = ItemManager.CreateByItemID(itemData.ItemID, itemData.Amount, itemData.Skin);
    if (item == null) return;
    
    // Handle weapon ammo
    var weapon = item.GetHeldEntity() as BaseProjectile;
    if (weapon != null && !string.IsNullOrEmpty(itemData.Ammotype))
    {
        var ammoDef = ItemManager.FindItemDefinition(itemData.Ammotype);
        if (ammoDef != null)
        {
            weapon.primaryMagazine.ammoType = ammoDef;
            weapon.primaryMagazine.contents = weapon.primaryMagazine.capacity;
        }
    }
    
    // Handle item contents (attachments)
    if (itemData.Contents != null && item.contents != null)
    {
        foreach (var content in itemData.Contents)
        {
            var contentItem = ItemManager.CreateByItemID(content.ItemID, content.Amount, content.Skin);
            contentItem?.MoveToContainer(item.contents);
        }
    }
    
    // Move to specific position or any available
    if (itemData.Position >= 0)
        item.MoveToContainer(container, itemData.Position);
    else if (!item.MoveToContainer(container))
        item.MoveToContainer(player.inventory.containerMain);
}
```

### Copy Player Inventory Pattern
```csharp
private void CopyInventoryToKit(BasePlayer player, KitData.Kit kit)
{
    kit.MainItems = CopyContainer(player.inventory.containerMain);
    kit.WearItems = CopyContainer(player.inventory.containerWear);
    kit.BeltItems = CopyContainer(player.inventory.containerBelt);
}

private ItemData[] CopyContainer(ItemContainer container)
{
    var items = new List<ItemData>();
    
    foreach (var item in container.itemList)
    {
        var data = new ItemData
        {
            ItemID = item.info.itemid,
            Amount = item.amount,
            Skin = item.skin,
            Position = item.position
        };
        
        // Copy weapon ammo type
        var weapon = item.GetHeldEntity() as BaseProjectile;
        if (weapon != null)
            data.Ammotype = weapon.primaryMagazine.ammoType?.shortname;
        
        // Copy contents (attachments)
        if (item.contents?.itemList?.Count > 0)
        {
            data.Contents = item.contents.itemList.Select(c => new ItemData
            {
                ItemID = c.info.itemid,
                Amount = c.amount,
                Skin = c.skin
            }).ToArray();
        }
        
        items.Add(data);
    }
    
    return items.ToArray();
}
```


---

## COMPLETE EXAMPLE: REMOVER TOOL PATTERNS

### Raycast to Get Entity Pattern
```csharp
private BaseEntity GetLookingAtEntity(BasePlayer player, float maxDistance = 4f)
{
    RaycastHit hit;
    if (!Physics.Raycast(player.eyes.HeadRay(), out hit, maxDistance, LAYER_TARGET))
        return null;
    
    return hit.GetEntity();
}

private const int LAYER_TARGET = ~(1 << 2 | 1 << 3 | 1 << 4 | 1 << 10 | 1 << 18 | 1 << 28 | 1 << 29);
```

### Remove Entity with Refund Pattern
```csharp
private void RemoveEntity(BasePlayer player, BaseEntity entity, bool refund = true)
{
    if (entity == null || entity.IsDestroyed) return;
    
    if (refund)
        RefundEntity(player, entity);
    
    entity.Kill(BaseNetworkable.DestroyMode.Gib);
}

private void RefundEntity(BasePlayer player, BaseEntity entity)
{
    var buildingBlock = entity as BuildingBlock;
    if (buildingBlock != null)
    {
        RefundBuildingBlock(player, buildingBlock);
        return;
    }
    
    // For deployables, try to get the item
    var deployable = entity as BaseCombatEntity;
    if (deployable?.pickup?.itemTarget != null)
    {
        var item = ItemManager.Create(deployable.pickup.itemTarget, 1, entity.skinID);
        if (item != null)
            player.GiveItem(item);
    }
}

private void RefundBuildingBlock(BasePlayer player, BuildingBlock block)
{
    var costs = block.BuildCost();
    if (costs == null) return;
    
    // Refund percentage based on grade
    float refundPercent = GetRefundPercent(block.grade);
    
    foreach (var cost in costs)
    {
        int amount = Mathf.RoundToInt(cost.amount * refundPercent);
        if (amount <= 0) continue;
        
        var item = ItemManager.Create(cost.itemDef, amount);
        if (item != null)
            player.GiveItem(item);
    }
}

private float GetRefundPercent(BuildingGrade.Enum grade)
{
    switch (grade)
    {
        case BuildingGrade.Enum.Twigs: return 1f;
        case BuildingGrade.Enum.Wood: return 0.5f;
        case BuildingGrade.Enum.Stone: return 0.5f;
        case BuildingGrade.Enum.Metal: return 0.5f;
        case BuildingGrade.Enum.TopTier: return 0.5f;
        default: return 0f;
    }
}
```

### Check Entity Ownership Pattern
```csharp
private bool CanRemoveEntity(BasePlayer player, BaseEntity entity)
{
    // Admin bypass
    if (player.IsAdmin) return true;
    
    // Check owner
    if (entity.OwnerID == player.userID) return true;
    
    // Check building privilege
    var buildingBlock = entity as BuildingBlock;
    if (buildingBlock != null)
    {
        var priv = buildingBlock.GetBuildingPrivilege();
        if (priv != null && priv.IsAuthed(player))
            return true;
    }
    
    // Check friends/team/clan
    if (entity.OwnerID != 0)
    {
        if (AreFriends(player.userID, entity.OwnerID)) return true;
        if (AreSameTeam(player, entity.OwnerID)) return true;
        if (AreSameClan(player.userID, entity.OwnerID)) return true;
    }
    
    return false;
}

private bool AreSameTeam(BasePlayer player, ulong targetId)
{
    if (player.currentTeam == 0UL) return false;
    var team = RelationshipManager.ServerInstance.FindTeam(player.currentTeam);
    return team != null && team.members.Contains(targetId);
}
```


---

## COMPLETE EXAMPLE: ZONE MANAGER PATTERNS

### Zone Definition Pattern
```csharp
private class ZoneDefinition
{
    public string Id;
    public string Name;
    public Vector3 Location;
    public Vector3 Size;
    public float Radius;
    public ZoneFlags Flags = new ZoneFlags();
    public string EnterMessage;
    public string LeaveMessage;
}

private class ZoneFlags
{
    public bool NoBuild;
    public bool NoDeploy;
    public bool NoUpgrade;
    public bool NoDamage;
    public bool PvpGod;
    public bool PveGod;
    public bool NoDecay;
    public bool NoLoot;
    public bool NoPickup;
    public bool NoGather;
    public bool NoKillSleepers;
    public bool NoSuicide;
}
```

### Check if Position in Zone Pattern
```csharp
private bool IsInZone(string zoneId, Vector3 position)
{
    if (!zones.TryGetValue(zoneId, out Zone zone))
        return false;
    
    return zone.IsInside(position);
}

private class Zone : MonoBehaviour
{
    public ZoneDefinition Definition;
    private BoxCollider boxCollider;
    private SphereCollider sphereCollider;
    
    public bool IsInside(Vector3 position)
    {
        if (sphereCollider != null)
        {
            return Vector3.Distance(transform.position, position) <= sphereCollider.radius;
        }
        
        if (boxCollider != null)
        {
            return boxCollider.bounds.Contains(position);
        }
        
        return false;
    }
}
```

### Zone Trigger Pattern
```csharp
private class ZoneTrigger : MonoBehaviour
{
    public Zone Zone;
    private HashSet<BasePlayer> playersInZone = new HashSet<BasePlayer>();
    
    private void OnTriggerEnter(Collider other)
    {
        var player = other.GetComponentInParent<BasePlayer>();
        if (player == null || !player.userID.IsSteamId()) return;
        
        if (playersInZone.Add(player))
            OnPlayerEnterZone(player, Zone);
    }
    
    private void OnTriggerExit(Collider other)
    {
        var player = other.GetComponentInParent<BasePlayer>();
        if (player == null) return;
        
        if (playersInZone.Remove(player))
            OnPlayerExitZone(player, Zone);
    }
}

private void OnPlayerEnterZone(BasePlayer player, Zone zone)
{
    if (!string.IsNullOrEmpty(zone.Definition.EnterMessage))
        SendReply(player, zone.Definition.EnterMessage);
    
    Interface.CallHook("OnEnterZone", zone.Definition.Id, player);
}

private void OnPlayerExitZone(BasePlayer player, Zone zone)
{
    if (!string.IsNullOrEmpty(zone.Definition.LeaveMessage))
        SendReply(player, zone.Definition.LeaveMessage);
    
    Interface.CallHook("OnExitZone", zone.Definition.Id, player);
}
```

### Zone Flag Hooks Pattern
```csharp
// Block building in NoBuild zones
private object OnEntityBuilt(Planner planner, GameObject go)
{
    var player = planner.GetOwnerPlayer();
    if (player == null) return null;
    
    var entity = go.ToBaseEntity();
    if (entity == null) return null;
    
    if (HasPlayerFlag(player, "NoBuild"))
    {
        entity.Kill();
        SendReply(player, "Building is not allowed in this zone!");
        return false;
    }
    
    return null;
}

// Block damage in NoDamage zones
private object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
{
    if (HasEntityFlag(entity, "NoDamage"))
    {
        info.damageTypes.ScaleAll(0);
        return true;
    }
    return null;
}

// Block looting in NoLoot zones
private object CanLootEntity(BasePlayer player, StorageContainer container)
{
    if (HasPlayerFlag(player, "NoLoot"))
    {
        SendReply(player, "Looting is not allowed in this zone!");
        return false;
    }
    return null;
}

private bool HasPlayerFlag(BasePlayer player, string flag)
{
    foreach (var zone in GetPlayerZones(player))
    {
        if (zone.Definition.Flags.GetType().GetField(flag)?.GetValue(zone.Definition.Flags) is bool value && value)
            return true;
    }
    return false;
}
```


---

## COMPLETE EXAMPLE: VEHICLE LICENCE PATTERNS

### Vehicle Data Storage Pattern
```csharp
private class StoredData
{
    public Dictionary<ulong, PlayerVehicles> Players = new Dictionary<ulong, PlayerVehicles>();
}

private class PlayerVehicles
{
    public Dictionary<string, VehicleInfo> Vehicles = new Dictionary<string, VehicleInfo>();
}

private class VehicleInfo
{
    public NetworkableId EntityId;
    public double LastDismount;
    public double PurchaseTime;
}
```

### Spawn Vehicle Pattern
```csharp
private BaseEntity SpawnVehicle(BasePlayer player, string vehicleType)
{
    var prefab = GetVehiclePrefab(vehicleType);
    if (string.IsNullOrEmpty(prefab)) return null;
    
    // Find spawn position
    Vector3 position;
    Quaternion rotation;
    if (!FindSpawnPosition(player, vehicleType, out position, out rotation))
    {
        SendReply(player, "No valid spawn position found!");
        return null;
    }
    
    // Create entity
    var entity = GameManager.server.CreateEntity(prefab, position, rotation);
    if (entity == null) return null;
    
    entity.OwnerID = player.userID;
    entity.Spawn();
    
    // Setup vehicle
    SetupVehicle(entity, player);
    
    // Track vehicle
    TrackVehicle(player.userID, vehicleType, entity);
    
    return entity;
}

private string GetVehiclePrefab(string vehicleType)
{
    switch (vehicleType.ToLower())
    {
        case "minicopter": return "assets/content/vehicles/minicopter/minicopter.entity.prefab";
        case "scraptransport": return "assets/content/vehicles/scrap heli carrier/scraptransporthelicopter.prefab";
        case "rowboat": return "assets/content/vehicles/boats/rowboat/rowboat.prefab";
        case "rhib": return "assets/content/vehicles/boats/rhib/rhib.prefab";
        case "horse": return "assets/rust.ai/agents/horse/ridablehorse.prefab";
        case "sedan": return "assets/content/vehicles/sedan_a/sedantest.entity.prefab";
        case "snowmobile": return "assets/content/vehicles/snowmobiles/snowmobile.prefab";
        case "attackheli": return "assets/content/vehicles/attackhelicopter/attackhelicopter.entity.prefab";
        default: return null;
    }
}
```

### Find Spawn Position Pattern
```csharp
private bool FindSpawnPosition(BasePlayer player, string vehicleType, out Vector3 position, out Quaternion rotation)
{
    position = Vector3.zero;
    rotation = Quaternion.identity;
    
    Vector3 forward = player.eyes.HeadForward();
    forward.y = 0;
    forward.Normalize();
    
    float spawnDistance = GetSpawnDistance(vehicleType);
    Vector3 checkPos = player.transform.position + forward * spawnDistance;
    
    // For boats, find water
    if (IsWaterVehicle(vehicleType))
    {
        if (!FindWaterPosition(checkPos, out position))
            return false;
        rotation = Quaternion.LookRotation(forward);
        return true;
    }
    
    // For helicopters, spawn in air
    if (IsAirVehicle(vehicleType))
    {
        position = checkPos + Vector3.up * 2f;
        rotation = Quaternion.LookRotation(forward);
        return true;
    }
    
    // For ground vehicles, find ground
    if (FindGroundPosition(checkPos, out position))
    {
        rotation = Quaternion.LookRotation(forward);
        return true;
    }
    
    return false;
}

private bool FindGroundPosition(Vector3 checkPos, out Vector3 position)
{
    position = Vector3.zero;
    
    RaycastHit hit;
    if (Physics.Raycast(checkPos + Vector3.up * 100f, Vector3.down, out hit, 200f, Layers.Solid))
    {
        position = hit.point + Vector3.up * 0.5f;
        return true;
    }
    
    return false;
}

private bool FindWaterPosition(Vector3 checkPos, out Vector3 position)
{
    position = Vector3.zero;
    
    float waterHeight = TerrainMeta.WaterMap.GetHeight(checkPos);
    if (waterHeight <= 0) return false;
    
    position = new Vector3(checkPos.x, waterHeight, checkPos.z);
    return true;
}
```

### Vehicle Ownership Check Pattern
```csharp
private bool IsVehicleOwner(BasePlayer player, BaseEntity vehicle)
{
    if (vehicle.OwnerID == player.userID) return true;
    if (AreFriends(vehicle.OwnerID, player.userID)) return true;
    if (AreSameTeam(player, vehicle.OwnerID)) return true;
    if (AreSameClan(vehicle.OwnerID, player.userID)) return true;
    return false;
}

// Block mounting other players' vehicles
private object CanMountEntity(BasePlayer player, BaseMountable mountable)
{
    var vehicle = mountable.VehicleParent();
    if (vehicle == null) return null;
    
    if (!vehiclesCache.ContainsKey(vehicle)) return null;  // Not our tracked vehicle
    
    if (!IsVehicleOwner(player, vehicle))
    {
        SendReply(player, "This vehicle belongs to someone else!");
        return false;
    }
    
    return null;
}
```


---

## COMPLETE EXAMPLE: SHOP PLUGIN PATTERNS

### Shop Item Definition Pattern
```csharp
private class ShopItem
{
    public int Id;
    public string Shortname;
    public string DisplayName;
    public ulong SkinId;
    public int Amount = 1;
    public double BuyPrice;
    public double SellPrice;
    public string Category;
    public string Command;  // For command-based items
    public bool Enabled = true;
}

private class ShopCategory
{
    public string Name;
    public string DisplayName;
    public string Permission;
    public bool Enabled = true;
    public List<int> Items = new List<int>();
}
```

### Buy Item Pattern
```csharp
private bool TryBuyItem(BasePlayer player, ShopItem item, int quantity)
{
    if (item == null || !item.Enabled)
    {
        SendReply(player, "This item is not available!");
        return false;
    }
    
    double totalCost = item.BuyPrice * quantity;
    
    // Check balance
    double balance = GetBalance(player.userID);
    if (balance < totalCost)
    {
        SendReply(player, $"Not enough money! Need: {totalCost}, Have: {balance}");
        return false;
    }
    
    // Check inventory space
    if (!HasInventorySpace(player, item, quantity))
    {
        SendReply(player, "Not enough inventory space!");
        return false;
    }
    
    // Withdraw money
    if (!Withdraw(player.userID, totalCost))
    {
        SendReply(player, "Failed to withdraw money!");
        return false;
    }
    
    // Give item
    GiveShopItem(player, item, quantity);
    
    SendReply(player, $"Purchased {quantity}x {item.DisplayName} for {totalCost}");
    LogPurchase(player, item, quantity, totalCost);
    
    return true;
}

private void GiveShopItem(BasePlayer player, ShopItem item, int quantity)
{
    // Command-based item
    if (!string.IsNullOrEmpty(item.Command))
    {
        for (int i = 0; i < quantity; i++)
        {
            string cmd = item.Command.Replace("{steamid}", player.UserIDString)
                                     .Replace("{name}", player.displayName);
            Server.Command(cmd);
        }
        return;
    }
    
    // Regular item
    int totalAmount = item.Amount * quantity;
    var createdItem = ItemManager.CreateByName(item.Shortname, totalAmount, item.SkinId);
    if (createdItem != null)
    {
        if (!player.inventory.GiveItem(createdItem))
            createdItem.Drop(player.transform.position, Vector3.up);
    }
}
```

### Sell Item Pattern
```csharp
private bool TrySellItem(BasePlayer player, ShopItem item, int quantity)
{
    if (item == null || item.SellPrice <= 0)
    {
        SendReply(player, "This item cannot be sold!");
        return false;
    }
    
    // Check if player has the items
    int playerAmount = GetPlayerItemCount(player, item.Shortname, item.SkinId);
    int sellAmount = item.Amount * quantity;
    
    if (playerAmount < sellAmount)
    {
        SendReply(player, $"You don't have enough! Need: {sellAmount}, Have: {playerAmount}");
        return false;
    }
    
    // Take items
    TakePlayerItems(player, item.Shortname, item.SkinId, sellAmount);
    
    // Give money
    double totalEarned = item.SellPrice * quantity;
    Deposit(player.userID, totalEarned);
    
    SendReply(player, $"Sold {sellAmount}x {item.DisplayName} for {totalEarned}");
    LogSale(player, item, quantity, totalEarned);
    
    return true;
}

private int GetPlayerItemCount(BasePlayer player, string shortname, ulong skinId)
{
    int count = 0;
    foreach (var item in player.inventory.AllItems())
    {
        if (item.info.shortname == shortname && (skinId == 0 || item.skin == skinId))
            count += item.amount;
    }
    return count;
}

private void TakePlayerItems(BasePlayer player, string shortname, ulong skinId, int amount)
{
    int remaining = amount;
    foreach (var item in player.inventory.AllItems().ToList())
    {
        if (remaining <= 0) break;
        if (item.info.shortname != shortname) continue;
        if (skinId != 0 && item.skin != skinId) continue;
        
        if (item.amount <= remaining)
        {
            remaining -= item.amount;
            item.Remove();
        }
        else
        {
            item.amount -= remaining;
            item.MarkDirty();
            remaining = 0;
        }
    }
}
```


---

## COMPLETE EXAMPLE: BACKPACKS PLUGIN PATTERNS

### Backpack Container Pattern
```csharp
private class BackpackData
{
    public Dictionary<ulong, BackpackInfo> Backpacks = new Dictionary<ulong, BackpackInfo>();
}

private class BackpackInfo
{
    public int Capacity = 6;
    public List<SavedItem> Items = new List<SavedItem>();
}

private class SavedItem
{
    public int ItemId;
    public int Amount;
    public ulong Skin;
    public int Position;
    public float Condition;
    public int Ammo;
    public string AmmoType;
    public List<SavedItem> Contents;
}
```

### Create Backpack Container Pattern
```csharp
private Dictionary<ulong, ItemContainer> backpackContainers = new Dictionary<ulong, ItemContainer>();

private ItemContainer GetBackpack(ulong playerId)
{
    if (backpackContainers.TryGetValue(playerId, out ItemContainer container))
        return container;
    
    // Create new container
    container = new ItemContainer();
    container.ServerInitialize(null, GetBackpackCapacity(playerId));
    container.GiveUID();
    container.entityOwner = null;
    container.isServer = true;
    
    // Load saved items
    LoadBackpackItems(playerId, container);
    
    backpackContainers[playerId] = container;
    return container;
}

private int GetBackpackCapacity(ulong playerId)
{
    string oderId = playerId.ToString();
    
    if (permission.UserHasPermission(userId, "backpacks.size.48"))
        return 48;
    if (permission.UserHasPermission(userId, "backpacks.size.36"))
        return 36;
    if (permission.UserHasPermission(userId, "backpacks.size.24"))
        return 24;
    if (permission.UserHasPermission(userId, "backpacks.size.12"))
        return 12;
    
    return 6;  // Default
}
```

### Open Backpack UI Pattern
```csharp
private void OpenBackpack(BasePlayer player, ulong ownerId)
{
    var container = GetBackpack(ownerId);
    if (container == null) return;
    
    // Close any existing loot
    player.EndLooting();
    
    // Create loot panel
    var lootPanel = player.inventory.loot;
    lootPanel.Clear();
    lootPanel.PositionChecks = false;
    lootPanel.entitySource = null;
    lootPanel.itemSource = null;
    lootPanel.AddContainer(container);
    lootPanel.SendImmediate();
    
    player.ClientRPC(RpcTarget.Player("RPC_OpenLootPanel", player), "generic_resizable");
}

// Hook to save when closed
private void OnLootEntityEnd(BasePlayer player, BaseCombatEntity entity)
{
    // Check if this was a backpack
    if (backpackContainers.TryGetValue(player.userID, out ItemContainer container))
    {
        SaveBackpackItems(player.userID, container);
    }
}
```

### Save/Load Backpack Items Pattern
```csharp
private void SaveBackpackItems(ulong playerId, ItemContainer container)
{
    var info = GetBackpackInfo(playerId);
    info.Items.Clear();
    
    foreach (var item in container.itemList)
    {
        info.Items.Add(ItemToSaved(item));
    }
    
    SaveData();
}

private void LoadBackpackItems(ulong playerId, ItemContainer container)
{
    var info = GetBackpackInfo(playerId);
    
    foreach (var saved in info.Items)
    {
        var item = SavedToItem(saved);
        if (item != null)
            item.MoveToContainer(container, saved.Position);
    }
}

private SavedItem ItemToSaved(Item item)
{
    var saved = new SavedItem
    {
        ItemId = item.info.itemid,
        Amount = item.amount,
        Skin = item.skin,
        Position = item.position,
        Condition = item.condition
    };
    
    var weapon = item.GetHeldEntity() as BaseProjectile;
    if (weapon != null)
    {
        saved.Ammo = weapon.primaryMagazine.contents;
        saved.AmmoType = weapon.primaryMagazine.ammoType?.shortname;
    }
    
    if (item.contents?.itemList?.Count > 0)
    {
        saved.Contents = item.contents.itemList.Select(ItemToSaved).ToList();
    }
    
    return saved;
}

private Item SavedToItem(SavedItem saved)
{
    var item = ItemManager.CreateByItemID(saved.ItemId, saved.Amount, saved.Skin);
    if (item == null) return null;
    
    item.condition = saved.Condition;
    
    var weapon = item.GetHeldEntity() as BaseProjectile;
    if (weapon != null && !string.IsNullOrEmpty(saved.AmmoType))
    {
        weapon.primaryMagazine.contents = saved.Ammo;
        weapon.primaryMagazine.ammoType = ItemManager.FindItemDefinition(saved.AmmoType);
    }
    
    if (saved.Contents != null && item.contents != null)
    {
        foreach (var content in saved.Contents)
        {
            var contentItem = SavedToItem(content);
            contentItem?.MoveToContainer(item.contents);
        }
    }
    
    return item;
}
```


---

## COMPLETE EXAMPLE: ADVANCED UI PATTERNS

### Paginated UI Pattern
```csharp
private const int ITEMS_PER_PAGE = 20;

private void ShowPaginatedUI(BasePlayer player, int page = 0)
{
    var allItems = GetAllItems();
    int totalPages = Mathf.CeilToInt((float)allItems.Count / ITEMS_PER_PAGE);
    page = Mathf.Clamp(page, 0, totalPages - 1);
    
    var pageItems = allItems.Skip(page * ITEMS_PER_PAGE).Take(ITEMS_PER_PAGE).ToList();
    
    var container = new CuiElementContainer();
    
    // Main panel
    container.Add(new CuiPanel
    {
        Image = { Color = "0.1 0.1 0.1 0.95" },
        RectTransform = { AnchorMin = "0.2 0.2", AnchorMax = "0.8 0.8" },
        CursorEnabled = true
    }, "Overlay", "MainPanel");
    
    // Items grid
    for (int i = 0; i < pageItems.Count; i++)
    {
        int row = i / 5;
        int col = i % 5;
        
        float xMin = 0.02f + col * 0.19f;
        float xMax = xMin + 0.18f;
        float yMax = 0.85f - row * 0.2f;
        float yMin = yMax - 0.18f;
        
        AddItemButton(container, pageItems[i], xMin, yMin, xMax, yMax);
    }
    
    // Pagination buttons
    if (page > 0)
    {
        container.Add(new CuiButton
        {
            Button = { Color = "0.3 0.3 0.3 1", Command = $"ui.page {page - 1}" },
            RectTransform = { AnchorMin = "0.02 0.02", AnchorMax = "0.15 0.08" },
            Text = { Text = "< Previous", FontSize = 14, Align = TextAnchor.MiddleCenter }
        }, "MainPanel");
    }
    
    // Page indicator
    container.Add(new CuiLabel
    {
        Text = { Text = $"Page {page + 1} / {totalPages}", FontSize = 14, Align = TextAnchor.MiddleCenter },
        RectTransform = { AnchorMin = "0.4 0.02", AnchorMax = "0.6 0.08" }
    }, "MainPanel");
    
    if (page < totalPages - 1)
    {
        container.Add(new CuiButton
        {
            Button = { Color = "0.3 0.3 0.3 1", Command = $"ui.page {page + 1}" },
            RectTransform = { AnchorMin = "0.85 0.02", AnchorMax = "0.98 0.08" },
            Text = { Text = "Next >", FontSize = 14, Align = TextAnchor.MiddleCenter }
        }, "MainPanel");
    }
    
    // Close button
    container.Add(new CuiButton
    {
        Button = { Color = "0.8 0.2 0.2 1", Command = "ui.close", Close = "MainPanel" },
        RectTransform = { AnchorMin = "0.92 0.92", AnchorMax = "0.98 0.98" },
        Text = { Text = "X", FontSize = 16, Align = TextAnchor.MiddleCenter }
    }, "MainPanel");
    
    CuiHelper.DestroyUi(player, "MainPanel");
    CuiHelper.AddUi(player, container);
}
```

### Input Field Pattern
```csharp
private void ShowInputUI(BasePlayer player, string currentValue = "")
{
    var container = new CuiElementContainer();
    
    // Panel
    container.Add(new CuiPanel
    {
        Image = { Color = "0.1 0.1 0.1 0.95" },
        RectTransform = { AnchorMin = "0.35 0.4", AnchorMax = "0.65 0.6" },
        CursorEnabled = true
    }, "Overlay", "InputPanel");
    
    // Label
    container.Add(new CuiLabel
    {
        Text = { Text = "Enter Value:", FontSize = 16, Align = TextAnchor.MiddleCenter },
        RectTransform = { AnchorMin = "0 0.7", AnchorMax = "1 0.95" }
    }, "InputPanel");
    
    // Input field
    container.Add(new CuiElement
    {
        Parent = "InputPanel",
        Components =
        {
            new CuiInputFieldComponent
            {
                Text = currentValue,
                FontSize = 14,
                Command = "ui.input.submit",
                Align = TextAnchor.MiddleCenter
            },
            new CuiRectTransformComponent { AnchorMin = "0.1 0.4", AnchorMax = "0.9 0.65" }
        }
    });
    
    // Submit button
    container.Add(new CuiButton
    {
        Button = { Color = "0.2 0.6 0.2 1", Command = "ui.input.confirm" },
        RectTransform = { AnchorMin = "0.3 0.1", AnchorMax = "0.7 0.3" },
        Text = { Text = "Submit", FontSize = 14, Align = TextAnchor.MiddleCenter }
    }, "InputPanel");
    
    CuiHelper.DestroyUi(player, "InputPanel");
    CuiHelper.AddUi(player, container);
}

[ConsoleCommand("ui.input.submit")]
private void CmdInputSubmit(ConsoleSystem.Arg arg)
{
    var player = arg.Player();
    if (player == null) return;
    
    string input = arg.GetString(0);
    // Store input for later use
    playerInputs[player.userID] = input;
}
```

### Confirmation Dialog Pattern
```csharp
private void ShowConfirmDialog(BasePlayer player, string message, string confirmCmd, string cancelCmd = "ui.close")
{
    var container = new CuiElementContainer();
    
    // Backdrop
    container.Add(new CuiPanel
    {
        Image = { Color = "0 0 0 0.8" },
        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
        CursorEnabled = true
    }, "Overlay", "ConfirmBackdrop");
    
    // Dialog box
    container.Add(new CuiPanel
    {
        Image = { Color = "0.15 0.15 0.15 1" },
        RectTransform = { AnchorMin = "0.35 0.35", AnchorMax = "0.65 0.65" }
    }, "ConfirmBackdrop", "ConfirmDialog");
    
    // Message
    container.Add(new CuiLabel
    {
        Text = { Text = message, FontSize = 16, Align = TextAnchor.MiddleCenter },
        RectTransform = { AnchorMin = "0.05 0.4", AnchorMax = "0.95 0.9" }
    }, "ConfirmDialog");
    
    // Confirm button
    container.Add(new CuiButton
    {
        Button = { Color = "0.2 0.6 0.2 1", Command = confirmCmd, Close = "ConfirmBackdrop" },
        RectTransform = { AnchorMin = "0.1 0.1", AnchorMax = "0.45 0.3" },
        Text = { Text = "Confirm", FontSize = 14, Align = TextAnchor.MiddleCenter }
    }, "ConfirmDialog");
    
    // Cancel button
    container.Add(new CuiButton
    {
        Button = { Color = "0.6 0.2 0.2 1", Command = cancelCmd, Close = "ConfirmBackdrop" },
        RectTransform = { AnchorMin = "0.55 0.1", AnchorMax = "0.9 0.3" },
        Text = { Text = "Cancel", FontSize = 14, Align = TextAnchor.MiddleCenter }
    }, "ConfirmDialog");
    
    CuiHelper.DestroyUi(player, "ConfirmBackdrop");
    CuiHelper.AddUi(player, container);
}
```


---

## COMPLETE EXAMPLE: ECONOMICS INTEGRATION PATTERNS

### Economics Plugin Integration
```csharp
[PluginReference] private Plugin Economics;

private double GetBalance(ulong playerId)
{
    if (Economics == null) return 0;
    return Economics.Call<double>("Balance", playerId);
}

private bool Withdraw(ulong playerId, double amount)
{
    if (Economics == null) return false;
    return Economics.Call<bool>("Withdraw", playerId, amount);
}

private bool Deposit(ulong playerId, double amount)
{
    if (Economics == null) return false;
    return Economics.Call<bool>("Deposit", playerId, amount);
}

private bool Transfer(ulong fromId, ulong toId, double amount)
{
    if (Economics == null) return false;
    return Economics.Call<bool>("Transfer", fromId, toId, amount);
}

private void SetBalance(ulong playerId, double amount)
{
    if (Economics == null) return;
    Economics.Call("SetBalance", playerId, amount);
}
```

### ServerRewards Integration
```csharp
[PluginReference] private Plugin ServerRewards;

private int GetPoints(ulong playerId)
{
    if (ServerRewards == null) return 0;
    return ServerRewards.Call<int>("CheckPoints", playerId);
}

private bool TakePoints(ulong playerId, int amount)
{
    if (ServerRewards == null) return false;
    return ServerRewards.Call<bool>("TakePoints", playerId, amount);
}

private bool AddPoints(ulong playerId, int amount)
{
    if (ServerRewards == null) return false;
    return ServerRewards.Call<bool>("AddPoints", playerId, amount);
}
```

### Multi-Currency Support Pattern
```csharp
public enum CurrencyType { Economics, ServerRewards, Scrap, Custom }

private bool HasCurrency(BasePlayer player, CurrencyType type, double amount)
{
    switch (type)
    {
        case CurrencyType.Economics:
            return GetBalance(player.userID) >= amount;
        case CurrencyType.ServerRewards:
            return GetPoints(player.userID) >= (int)amount;
        case CurrencyType.Scrap:
            return player.inventory.GetAmount(-932201673) >= (int)amount;
        case CurrencyType.Custom:
            return GetCustomCurrency(player.userID) >= amount;
        default:
            return false;
    }
}

private bool TakeCurrency(BasePlayer player, CurrencyType type, double amount)
{
    switch (type)
    {
        case CurrencyType.Economics:
            return Withdraw(player.userID, amount);
        case CurrencyType.ServerRewards:
            return TakePoints(player.userID, (int)amount);
        case CurrencyType.Scrap:
            return player.inventory.Take(null, -932201673, (int)amount) > 0;
        case CurrencyType.Custom:
            return TakeCustomCurrency(player.userID, amount);
        default:
            return false;
    }
}

private void GiveCurrency(BasePlayer player, CurrencyType type, double amount)
{
    switch (type)
    {
        case CurrencyType.Economics:
            Deposit(player.userID, amount);
            break;
        case CurrencyType.ServerRewards:
            AddPoints(player.userID, (int)amount);
            break;
        case CurrencyType.Scrap:
            var scrap = ItemManager.CreateByItemID(-932201673, (int)amount);
            player.GiveItem(scrap);
            break;
        case CurrencyType.Custom:
            AddCustomCurrency(player.userID, amount);
            break;
    }
}
```

---

## COMPLETE EXAMPLE: IMAGELIBRARYINTEGRATION

### ImageLibrary Integration Pattern
```csharp
[PluginReference] private Plugin ImageLibrary;

private bool AddImage(string url, string name, ulong skin = 0)
{
    if (ImageLibrary == null) return false;
    return ImageLibrary.Call<bool>("AddImage", url, name, skin);
}

private string GetImage(string name, ulong skin = 0)
{
    if (ImageLibrary == null) return null;
    return ImageLibrary.Call<string>("GetImage", name, skin);
}

private bool HasImage(string name, ulong skin = 0)
{
    if (ImageLibrary == null) return false;
    return ImageLibrary.Call<bool>("HasImage", name, skin);
}

// Register images on server init
private void OnServerInitialized()
{
    if (ImageLibrary == null)
    {
        PrintWarning("ImageLibrary not found! Images will not work.");
        return;
    }
    
    // Register custom images
    AddImage("https://example.com/icon.png", "myicon");
    AddImage("https://example.com/background.png", "mybg");
    
    // Register item images by shortname
    foreach (var item in ItemManager.itemList)
    {
        // ImageLibrary auto-registers item images, but you can add custom ones
    }
}

// Use in UI
private void AddImageToUI(CuiElementContainer container, string parent, string imageName)
{
    string imageId = GetImage(imageName);
    if (string.IsNullOrEmpty(imageId))
    {
        // Fallback to sprite
        container.Add(new CuiElement
        {
            Parent = parent,
            Components =
            {
                new CuiImageComponent { Sprite = "assets/icons/gear.png" },
                new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
            }
        });
        return;
    }
    
    container.Add(new CuiElement
    {
        Parent = parent,
        Components =
        {
            new CuiRawImageComponent { Png = imageId },
            new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
        }
    });
}
```


---

## COMPLETE EXAMPLE: COROUTINE PATTERNS

### Coroutine for Heavy Operations
```csharp
private Coroutine activeCoroutine;

private void StartHeavyOperation()
{
    if (activeCoroutine != null)
        ServerMgr.Instance.StopCoroutine(activeCoroutine);
    
    activeCoroutine = ServerMgr.Instance.StartCoroutine(HeavyOperationCoroutine());
}

private IEnumerator HeavyOperationCoroutine()
{
    var entities = BaseNetworkable.serverEntities.ToList();
    int processed = 0;
    
    foreach (var entity in entities)
    {
        if (entity == null || entity.IsDestroyed) continue;
        
        // Process entity
        ProcessEntity(entity);
        processed++;
        
        // Yield every 100 entities to prevent lag
        if (processed % 100 == 0)
        {
            yield return CoroutineEx.waitForSeconds(0.01f);
        }
    }
    
    Puts($"Processed {processed} entities");
    activeCoroutine = null;
}

private void Unload()
{
    if (activeCoroutine != null)
        ServerMgr.Instance.StopCoroutine(activeCoroutine);
}
```

### Delayed Spawn Coroutine
```csharp
private IEnumerator SpawnEntitiesCoroutine(List<SpawnInfo> spawns, float delay = 0.1f)
{
    foreach (var spawn in spawns)
    {
        var entity = GameManager.server.CreateEntity(spawn.Prefab, spawn.Position, spawn.Rotation);
        if (entity != null)
        {
            entity.Spawn();
            OnEntitySpawned(entity);
        }
        
        yield return CoroutineEx.waitForSeconds(delay);
    }
}
```

### Progress Callback Coroutine
```csharp
private IEnumerator ProcessWithProgress(List<BaseEntity> entities, Action<float> onProgress, Action onComplete)
{
    int total = entities.Count;
    int processed = 0;
    
    foreach (var entity in entities)
    {
        if (entity != null && !entity.IsDestroyed)
        {
            ProcessEntity(entity);
        }
        
        processed++;
        float progress = (float)processed / total;
        onProgress?.Invoke(progress);
        
        if (processed % 50 == 0)
            yield return CoroutineEx.waitForEndOfFrame;
    }
    
    onComplete?.Invoke();
}

// Usage
private void StartProcessing(BasePlayer player)
{
    var entities = GetEntitiesToProcess();
    
    ServerMgr.Instance.StartCoroutine(ProcessWithProgress(
        entities,
        progress => SendReply(player, $"Progress: {progress:P0}"),
        () => SendReply(player, "Complete!")
    ));
}
```

---

## COMPLETE EXAMPLE: HARMONY PATCHING PATTERNS

### Basic Harmony Patch
```csharp
using HarmonyLib;

private Harmony harmony;

private void OnServerInitialized()
{
    harmony = new Harmony(Name);
    harmony.PatchAll();
}

private void Unload()
{
    harmony?.UnpatchAll(Name);
}

[HarmonyPatch(typeof(BasePlayer), nameof(BasePlayer.OnAttacked))]
private static class BasePlayer_OnAttacked_Patch
{
    [HarmonyPrefix]
    private static bool Prefix(BasePlayer __instance, HitInfo info)
    {
        // Return false to skip original method
        // Return true to continue to original method
        
        if (ShouldBlockDamage(__instance, info))
            return false;
        
        return true;
    }
    
    [HarmonyPostfix]
    private static void Postfix(BasePlayer __instance, HitInfo info)
    {
        // Called after original method
        OnPlayerAttacked(__instance, info);
    }
}
```

### Transpiler Patch (Advanced)
```csharp
[HarmonyPatch(typeof(ItemCrafter), nameof(ItemCrafter.CraftItem))]
private static class ItemCrafter_CraftItem_Patch
{
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = new List<CodeInstruction>(instructions);
        
        for (int i = 0; i < codes.Count; i++)
        {
            // Find and modify specific instructions
            if (codes[i].opcode == OpCodes.Ldc_R4 && (float)codes[i].operand == 1f)
            {
                // Change craft time multiplier
                codes[i].operand = 0.5f;
            }
        }
        
        return codes;
    }
}
```

---

## COMPLETE EXAMPLE: MONOBEHAVIOUR PATTERNS

### Player Component Pattern
```csharp
private class PlayerComponent : MonoBehaviour
{
    public BasePlayer Player;
    public ulong PlayerId;
    
    private float lastUpdate;
    private const float UPDATE_INTERVAL = 1f;
    
    private void Awake()
    {
        Player = GetComponent<BasePlayer>();
        if (Player != null)
            PlayerId = Player.userID;
    }
    
    private void Update()
    {
        if (Player == null || !Player.IsConnected)
        {
            Destroy(this);
            return;
        }
        
        if (Time.time - lastUpdate < UPDATE_INTERVAL)
            return;
        
        lastUpdate = Time.time;
        OnUpdate();
    }
    
    private void OnUpdate()
    {
        // Called every UPDATE_INTERVAL seconds
    }
    
    private void OnDestroy()
    {
        // Cleanup
    }
}

// Add to player
private void OnPlayerConnected(BasePlayer player)
{
    player.gameObject.AddComponent<PlayerComponent>();
}

// Remove from all players on unload
private void Unload()
{
    foreach (var player in BasePlayer.activePlayerList)
    {
        var comp = player.GetComponent<PlayerComponent>();
        if (comp != null)
            UnityEngine.Object.Destroy(comp);
    }
}
```

### Entity Controller Pattern
```csharp
private class EntityController : FacepunchBehaviour
{
    public BaseEntity Entity;
    public ulong OwnerId;
    
    private void Awake()
    {
        Entity = GetComponent<BaseEntity>();
    }
    
    public void Initialize(ulong ownerId)
    {
        OwnerId = ownerId;
        InvokeRepeating(nameof(Think), 1f, 1f);
    }
    
    private void Think()
    {
        if (Entity == null || Entity.IsDestroyed)
        {
            Destroy(this);
            return;
        }
        
        // Controller logic
    }
    
    private void OnDestroy()
    {
        CancelInvoke();
    }
}
```


---

## COMPLETE EXAMPLE: BUILDING SYSTEM PATTERNS

### Get Building Info Pattern
```csharp
private BuildingPrivlidge GetBuildingPrivilege(Vector3 position)
{
    var privs = new List<BuildingPrivlidge>();
    Vis.Entities(position, 1f, privs);
    
    foreach (var priv in privs)
    {
        if (priv.IsInsideBuildingPrivilege(position))
            return priv;
    }
    
    return null;
}

private bool IsAuthorized(BasePlayer player, Vector3 position)
{
    var priv = GetBuildingPrivilege(position);
    if (priv == null) return true;  // No TC = allowed
    return priv.IsAuthed(player);
}

private List<BasePlayer> GetAuthorizedPlayers(BuildingPrivlidge priv)
{
    var players = new List<BasePlayer>();
    foreach (var auth in priv.authorizedPlayers)
    {
        var player = BasePlayer.FindByID(auth.userid);
        if (player != null)
            players.Add(player);
    }
    return players;
}
```

### Get All Building Blocks Pattern
```csharp
private List<BuildingBlock> GetBuildingBlocks(BuildingPrivlidge priv)
{
    var blocks = new List<BuildingBlock>();
    if (priv == null) return blocks;
    
    var building = priv.GetBuilding();
    if (building == null) return blocks;
    
    foreach (var block in building.buildingBlocks)
    {
        if (block != null && !block.IsDestroyed)
            blocks.Add(block);
    }
    
    return blocks;
}

private List<DecayEntity> GetBuildingEntities(BuildingPrivlidge priv)
{
    var entities = new List<DecayEntity>();
    if (priv == null) return entities;
    
    var building = priv.GetBuilding();
    if (building == null) return entities;
    
    foreach (var entity in building.decayEntities)
    {
        if (entity != null && !entity.IsDestroyed)
            entities.Add(entity);
    }
    
    return entities;
}
```

### Upgrade Building Pattern
```csharp
private void UpgradeBuilding(BuildingPrivlidge priv, BuildingGrade.Enum targetGrade, bool payForUpgrade = false)
{
    var blocks = GetBuildingBlocks(priv);
    
    foreach (var block in blocks)
    {
        if (block.grade >= targetGrade) continue;
        if (!block.CanChangeToGrade(targetGrade, 0, null).EqualTo(true)) continue;
        
        if (payForUpgrade)
        {
            var costs = block.GetGradeCost(targetGrade);
            // Check and take resources
        }
        
        block.SetGrade(targetGrade);
        block.SetHealthToMax();
        block.SendNetworkUpdate();
    }
}

// Hook for when player upgrades
private void OnStructureUpgrade(BuildingBlock block, BasePlayer player, BuildingGrade.Enum grade)
{
    Puts($"{player.displayName} upgraded {block.ShortPrefabName} to {grade}");
}

// Block upgrade
private object CanChangeGrade(BasePlayer player, BuildingBlock block, BuildingGrade.Enum grade)
{
    if (!CanUpgrade(player, block, grade))
    {
        SendReply(player, "You cannot upgrade this!");
        return false;
    }
    return null;
}
```

### Decay Management Pattern
```csharp
// Block decay
private object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
{
    if (!info.damageTypes.Has(DamageType.Decay))
        return null;
    
    // Check if in protected zone
    if (IsProtectedFromDecay(entity))
    {
        info.damageTypes.Scale(DamageType.Decay, 0);
        return true;
    }
    
    // Scale decay
    float decayScale = GetDecayScale(entity);
    if (decayScale != 1f)
    {
        info.damageTypes.Scale(DamageType.Decay, decayScale);
    }
    
    return null;
}

private float GetDecayScale(BaseCombatEntity entity)
{
    var block = entity as BuildingBlock;
    if (block == null) return 1f;
    
    // No decay for high tier
    if (block.grade == BuildingGrade.Enum.TopTier)
        return 0f;
    
    // Half decay for metal
    if (block.grade == BuildingGrade.Enum.Metal)
        return 0.5f;
    
    return 1f;
}
```

---

## COMPLETE EXAMPLE: SPAWNING ENTITIES PATTERNS

### Spawn Prefab Pattern
```csharp
private T SpawnEntity<T>(string prefab, Vector3 position, Quaternion rotation, ulong ownerId = 0) where T : BaseEntity
{
    var entity = GameManager.server.CreateEntity(prefab, position, rotation) as T;
    if (entity == null) return null;
    
    if (ownerId != 0)
        entity.OwnerID = ownerId;
    
    entity.Spawn();
    return entity;
}

// Common prefabs
private const string PREFAB_WOODEN_BOX = "assets/prefabs/deployable/woodenbox/woodbox_deployed.prefab";
private const string PREFAB_LARGE_BOX = "assets/prefabs/deployable/large wood storage/box.wooden.large.prefab";
private const string PREFAB_FURNACE = "assets/prefabs/deployable/furnace/furnace.prefab";
private const string PREFAB_CAMPFIRE = "assets/prefabs/deployable/campfire/campfire.prefab";
private const string PREFAB_SLEEPING_BAG = "assets/prefabs/deployable/sleeping bag/sleepingbag_leather_deployed.prefab";
private const string PREFAB_TOOL_CUPBOARD = "assets/prefabs/deployable/tool cupboard/cupboard.tool.deployed.prefab";
private const string PREFAB_AUTO_TURRET = "assets/prefabs/npc/autoturret/autoturret_deployed.prefab";
private const string PREFAB_SAM_SITE = "assets/prefabs/npc/sam_site_turret/sam_site_turret_deployed.prefab";
```

### Spawn Loot Container Pattern
```csharp
private StorageContainer SpawnLootBox(Vector3 position, List<ItemInfo> items)
{
    var box = SpawnEntity<StorageContainer>(PREFAB_LARGE_BOX, position, Quaternion.identity);
    if (box == null) return null;
    
    // Clear default loot
    box.inventory.Clear();
    
    // Add custom items
    foreach (var info in items)
    {
        var item = ItemManager.CreateByName(info.Shortname, info.Amount, info.SkinId);
        if (item != null)
            item.MoveToContainer(box.inventory);
    }
    
    return box;
}
```

### Spawn NPC with Loadout Pattern
```csharp
private ScientistNPC SpawnArmedNPC(Vector3 position, string weaponShortname, string[] wearItems)
{
    var npc = SpawnEntity<ScientistNPC>(
        "assets/prefabs/npc/scientist/scientist.prefab",
        position, Quaternion.identity);
    
    if (npc == null) return null;
    
    // Clear default loadout
    npc.inventory.Strip();
    
    // Give weapon
    var weapon = ItemManager.CreateByName(weaponShortname);
    if (weapon != null)
    {
        if (!weapon.MoveToContainer(npc.inventory.containerBelt))
            weapon.Remove();
        else
        {
            var heldWeapon = weapon.GetHeldEntity() as BaseProjectile;
            if (heldWeapon != null)
            {
                heldWeapon.primaryMagazine.contents = heldWeapon.primaryMagazine.capacity;
            }
        }
    }
    
    // Give wear items
    foreach (var wearShortname in wearItems)
    {
        var wearItem = ItemManager.CreateByName(wearShortname);
        if (wearItem != null && !wearItem.MoveToContainer(npc.inventory.containerWear))
            wearItem.Remove();
    }
    
    // Set health
    npc.SetHealth(200f);
    
    return npc;
}
```


---

## COMPLETE EXAMPLE: EFFECT AND SOUND PATTERNS

### Play Effects Pattern
```csharp
// Play effect at position
private void PlayEffect(string prefab, Vector3 position)
{
    Effect.server.Run(prefab, position);
}

// Play effect on entity
private void PlayEffect(string prefab, BaseEntity entity)
{
    Effect.server.Run(prefab, entity, 0, Vector3.zero, Vector3.forward);
}

// Play effect for specific player only
private void PlayEffectForPlayer(string prefab, BasePlayer player, Vector3 position)
{
    var effect = new Effect(prefab, position, Vector3.forward);
    EffectNetwork.Send(effect, player.net.connection);
}

// Common effect prefabs
private const string EFFECT_EXPLOSION = "assets/bundled/prefabs/fx/explosions/explosion_01.prefab";
private const string EFFECT_FIRE = "assets/bundled/prefabs/fx/impacts/additive/fire.prefab";
private const string EFFECT_SPARK = "assets/bundled/prefabs/fx/impacts/spark_metal.prefab";
private const string EFFECT_HEAL = "assets/bundled/prefabs/fx/gestures/drink_vomit.prefab";
private const string EFFECT_LEVEL_UP = "assets/bundled/prefabs/fx/invite_notice.prefab";
private const string EFFECT_CODE_LOCK = "assets/prefabs/locks/keypad/effects/lock.code.lock.prefab";
private const string EFFECT_SURVEY = "assets/bundled/prefabs/fx/survey_explosion.prefab";
private const string EFFECT_ORE_BREAK = "assets/bundled/prefabs/fx/ore_break.prefab";
```

### Screen Effects Pattern
```csharp
// Hurt effect (red screen flash)
private void ShowHurtEffect(BasePlayer player)
{
    player.ClientRPC(RpcTarget.Player("HurtEffect", player));
}

// Gesture/animation
private void PlayGesture(BasePlayer player, string gesture)
{
    player.Server_StartGesture(StringPool.Get(gesture));
}

// Common gestures: "friendly", "thumbsup", "wave", "shrug", "clap", "point", "victory"
```

### Sound Patterns
```csharp
// Play sound at position
private void PlaySound(string prefab, Vector3 position)
{
    Effect.server.Run(prefab, position);
}

// Common sound prefabs
private const string SOUND_DEPLOY = "assets/prefabs/deployable/repair bench/effects/skinchange_spraypaint.prefab";
private const string SOUND_PICKUP = "assets/bundled/prefabs/fx/notice/item.select.fx.prefab";
private const string SOUND_ERROR = "assets/prefabs/locks/keypad/effects/lock.code.denied.prefab";
private const string SOUND_SUCCESS = "assets/prefabs/locks/keypad/effects/lock.code.unlock.prefab";
private const string SOUND_CLICK = "assets/bundled/prefabs/fx/notice/loot.drag.grab.fx.prefab";
```

---

## COMPLETE EXAMPLE: CHAT AND MESSAGE PATTERNS

### Formatted Chat Messages
```csharp
private void SendFormattedMessage(BasePlayer player, string message)
{
    // With prefix
    player.ChatMessage($"<color=#ff8800>[MyPlugin]</color> {message}");
}

private void SendColoredMessage(BasePlayer player, string message, string color = "#ffffff")
{
    player.ChatMessage($"<color={color}>{message}</color>");
}

private void SendMultilineMessage(BasePlayer player, params string[] lines)
{
    player.ChatMessage(string.Join("\n", lines));
}
```

### Broadcast Patterns
```csharp
// Broadcast to all players
private void BroadcastMessage(string message)
{
    foreach (var player in BasePlayer.activePlayerList)
    {
        if (player.IsConnected)
            player.ChatMessage(message);
    }
}

// Broadcast to players with permission
private void BroadcastToPermission(string message, string permission)
{
    foreach (var player in BasePlayer.activePlayerList)
    {
        if (player.IsConnected && this.permission.UserHasPermission(player.UserIDString, permission))
            player.ChatMessage(message);
    }
}

// Server console message (shows in F1 console)
private void SendConsoleMessage(BasePlayer player, string message)
{
    player.ConsoleMessage(message);
}
```

### Toast/Popup Notifications
```csharp
// Show game tip (bottom of screen)
private void ShowGameTip(BasePlayer player, string message, float duration = 5f)
{
    player.ShowToast(GameTip.Styles.Blue_Normal, message, duration);
}

// Toast styles: Blue_Normal, Blue_Long, Red_Normal, Server_Event
```

### Localization Pattern
```csharp
private Dictionary<string, Dictionary<string, string>> messages = new Dictionary<string, Dictionary<string, string>>
{
    ["en"] = new Dictionary<string, string>
    {
        ["NoPermission"] = "You don't have permission!",
        ["Success"] = "Action completed successfully!",
        ["Error"] = "An error occurred: {0}",
        ["Welcome"] = "Welcome, {0}!"
    },
    ["ru"] = new Dictionary<string, string>
    {
        ["NoPermission"] = "У вас нет разрешения!",
        ["Success"] = "Действие выполнено успешно!",
        ["Error"] = "Произошла ошибка: {0}",
        ["Welcome"] = "Добро пожаловать, {0}!"
    }
};

protected override void LoadDefaultMessages()
{
    foreach (var lang in messages)
    {
        lang.RegisterMessages(lang.Value, this, lang.Key);
    }
}

private string Lang(string key, string userId = null, params object[] args)
{
    var message = lang.GetMessage(key, this, userId);
    return args.Length > 0 ? string.Format(message, args) : message;
}

private void Message(BasePlayer player, string key, params object[] args)
{
    SendReply(player, Lang(key, player.UserIDString, args));
}
```

---

## COMPLETE EXAMPLE: WIPE DETECTION PATTERNS

### Detect New Wipe
```csharp
private DateTime lastWipeTime;

private void OnNewSave(string filename)
{
    // Called when server wipes
    lastWipeTime = DateTime.UtcNow;
    
    // Clear data on wipe
    if (config.ClearDataOnWipe)
    {
        storedData = new StoredData();
        SaveData();
        Puts("Data cleared due to wipe");
    }
}

private void OnServerInitialized()
{
    // Get wipe time
    lastWipeTime = SaveRestore.SaveCreatedTime;
    
    // Check if this is a fresh wipe
    var timeSinceWipe = DateTime.UtcNow - lastWipeTime;
    if (timeSinceWipe.TotalMinutes < 5)
    {
        Puts("Fresh wipe detected!");
        OnFreshWipe();
    }
}

private double GetSecondsSinceWipe()
{
    return (DateTime.UtcNow - SaveRestore.SaveCreatedTime).TotalSeconds;
}

private bool IsWipeDay()
{
    return GetSecondsSinceWipe() < 86400;  // 24 hours
}
```


---

## COMPLETE EXAMPLE: COMMON HOOK PATTERNS

### All Player Hooks
```csharp
// Player connects (before fully loaded)
private void OnPlayerConnected(BasePlayer player)
{
    // Player is connecting but not fully spawned
    Puts($"{player.displayName} is connecting...");
}

// Player fully spawned and ready
private void OnPlayerSleepEnded(BasePlayer player)
{
    // Player is now fully loaded and awake
    ShowWelcomeUI(player);
}

// Player disconnects
private void OnPlayerDisconnected(BasePlayer player, string reason)
{
    // Clean up player data
    CleanupPlayer(player);
    Puts($"{player.displayName} disconnected: {reason}");
}

// Player dies
private void OnPlayerDeath(BasePlayer player, HitInfo info)
{
    var killer = info?.InitiatorPlayer;
    if (killer != null && killer != player)
    {
        // PvP kill
        OnPvPKill(killer, player);
    }
}

// Player respawns
private void OnPlayerRespawned(BasePlayer player)
{
    // Give starter kit, etc.
    GiveStarterKit(player);
}

// Player goes to sleep
private void OnPlayerSleep(BasePlayer player)
{
    // Player is now sleeping (disconnected or F1 sleep)
}

// Player wakes up
private void OnPlayerSleepEnded(BasePlayer player)
{
    // Player woke up
}

// Player wounded (downed)
private void OnPlayerWound(BasePlayer player, HitInfo info)
{
    // Player is now in wounded state
}

// Player recovers from wounded
private void OnPlayerRecover(BasePlayer player)
{
    // Player got back up
}
```

### All Entity Hooks
```csharp
// Entity spawned
private void OnEntitySpawned(BaseNetworkable entity)
{
    // Called for ALL entities - be careful with performance!
    if (entity is LootContainer loot)
        ModifyLoot(loot);
}

// Entity about to die
private void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
{
    if (entity is BasePlayer player)
        OnPlayerDeath(player, info);
}

// Entity killed/destroyed
private void OnEntityKill(BaseNetworkable entity)
{
    // Entity is being destroyed
    if (trackedEntities.Contains(entity))
        trackedEntities.Remove(entity);
}

// Entity takes damage
private object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
{
    // Return non-null to modify/block damage
    if (ShouldBlockDamage(entity))
    {
        info.damageTypes.ScaleAll(0);
        return true;
    }
    return null;
}
```

### All Building Hooks
```csharp
// Structure placed
private void OnEntityBuilt(Planner planner, GameObject go)
{
    var player = planner.GetOwnerPlayer();
    var entity = go.ToBaseEntity();
    
    if (entity is BuildingBlock block)
        OnBlockPlaced(player, block);
}

// Structure upgraded
private void OnStructureUpgrade(BuildingBlock block, BasePlayer player, BuildingGrade.Enum grade)
{
    Puts($"{player.displayName} upgraded to {grade}");
}

// Structure repaired
private void OnStructureRepair(BaseCombatEntity entity, BasePlayer player)
{
    // Player repaired structure
}

// Structure rotated
private void OnStructureRotate(BuildingBlock block, BasePlayer player)
{
    // Player rotated structure
}

// Can build check
private object CanBuild(Planner planner, Construction prefab, Construction.Target target)
{
    var player = planner.GetOwnerPlayer();
    if (!CanPlayerBuild(player, target.position))
    {
        SendReply(player, "You cannot build here!");
        return false;
    }
    return null;
}
```

### All Item Hooks
```csharp
// Item added to container
private void OnItemAddedToContainer(ItemContainer container, Item item)
{
    // Item was added to a container
}

// Item removed from container
private void OnItemRemovedFromContainer(ItemContainer container, Item item)
{
    // Item was removed from a container
}

// Item picked up
private object OnItemPickup(Item item, BasePlayer player)
{
    // Return non-null to block pickup
    return null;
}

// Item dropped
private void OnItemDropped(Item item, BaseEntity entity)
{
    // Item was dropped on ground
}

// Item crafted
private void OnItemCraftFinished(ItemCraftTask task, Item item, ItemCrafter crafter)
{
    var player = crafter.owner;
    Puts($"{player.displayName} crafted {item.info.shortname}");
}

// Can craft check
private object CanCraft(ItemCrafter crafter, ItemBlueprint bp, int amount)
{
    var player = crafter.owner;
    if (!CanPlayerCraft(player))
    {
        SendReply(player, "You cannot craft right now!");
        return false;
    }
    return null;
}
```

### All Loot Hooks
```csharp
// Player starts looting
private void OnLootEntity(BasePlayer player, BaseEntity entity)
{
    if (entity is StorageContainer container)
        OnLootContainer(player, container);
}

// Player stops looting
private void OnLootEntityEnd(BasePlayer player, BaseCombatEntity entity)
{
    // Player closed loot
}

// Can loot check
private object CanLootEntity(BasePlayer player, StorageContainer container)
{
    if (!CanPlayerLoot(player, container))
    {
        SendReply(player, "You cannot loot this!");
        return false;
    }
    return null;
}

// Can loot player corpse
private object CanLootEntity(BasePlayer player, LootableCorpse corpse)
{
    if (corpse.playerSteamID != player.userID && !CanLootOtherCorpses(player))
    {
        SendReply(player, "You cannot loot other players' bodies!");
        return false;
    }
    return null;
}
```


---

## COMPLETE EXAMPLE: GATHERING/RESOURCE PATTERNS

### Modify Gather Rates
```csharp
// When player gathers from dispenser (trees, ores, etc.)
private object OnDispenserGather(ResourceDispenser dispenser, BasePlayer player, Item item)
{
    // Double gather rate
    item.amount *= 2;
    
    // Or return non-null to block gathering
    return null;
}

// When player gathers bonus (hitting sweet spot)
private object OnDispenserBonus(ResourceDispenser dispenser, BasePlayer player, Item item)
{
    // Modify bonus amount
    item.amount *= 3;
    return null;
}

// When player picks up collectible (hemp, mushrooms, etc.)
private object OnCollectiblePickup(CollectibleEntity collectible, BasePlayer player)
{
    // Return non-null to block pickup
    return null;
}

// After collectible picked up - modify items
private void OnCollectiblePickup(CollectibleEntity collectible, BasePlayer player, Item item)
{
    // Double the amount
    item.amount *= 2;
}

// When player harvests growable (farm plants)
private object OnGrowableGather(GrowableEntity plant, Item item, BasePlayer player)
{
    // Modify harvest amount
    item.amount *= 2;
    return null;
}
```

### Custom Gather Multipliers
```csharp
private float GetGatherMultiplier(BasePlayer player, string resourceType)
{
    float multiplier = config.DefaultMultiplier;
    
    // VIP bonus
    if (permission.UserHasPermission(player.UserIDString, "gather.vip"))
        multiplier *= 2f;
    
    // Resource-specific multipliers
    switch (resourceType)
    {
        case "wood":
            multiplier *= config.WoodMultiplier;
            break;
        case "stones":
            multiplier *= config.StoneMultiplier;
            break;
        case "metal.ore":
            multiplier *= config.MetalMultiplier;
            break;
        case "sulfur.ore":
            multiplier *= config.SulfurMultiplier;
            break;
    }
    
    return multiplier;
}

private object OnDispenserGather(ResourceDispenser dispenser, BasePlayer player, Item item)
{
    float multiplier = GetGatherMultiplier(player, item.info.shortname);
    item.amount = Mathf.RoundToInt(item.amount * multiplier);
    return null;
}
```

---

## COMPLETE EXAMPLE: TELEPORT PATTERNS

### Basic Teleport
```csharp
private void TeleportPlayer(BasePlayer player, Vector3 position)
{
    // End looting first
    player.EndLooting();
    
    // Teleport
    player.Teleport(position);
}

// Teleport to another player
private void TeleportToPlayer(BasePlayer player, BasePlayer target)
{
    TeleportPlayer(player, target.transform.position);
}

// Teleport with ground check
private void SafeTeleport(BasePlayer player, Vector3 position)
{
    // Find ground
    RaycastHit hit;
    if (Physics.Raycast(position + Vector3.up * 100f, Vector3.down, out hit, 200f, Layers.Solid))
    {
        position = hit.point + Vector3.up * 0.5f;
    }
    
    TeleportPlayer(player, position);
}
```

### Teleport with Countdown
```csharp
private Dictionary<ulong, Timer> pendingTeleports = new Dictionary<ulong, Timer>();

private void StartTeleport(BasePlayer player, Vector3 destination, float countdown = 5f)
{
    // Cancel existing teleport
    CancelTeleport(player);
    
    Vector3 startPos = player.transform.position;
    
    SendReply(player, $"Teleporting in {countdown} seconds. Don't move!");
    
    pendingTeleports[player.userID] = timer.Once(countdown, () =>
    {
        pendingTeleports.Remove(player.userID);
        
        // Check if player moved
        if (Vector3.Distance(player.transform.position, startPos) > 1f)
        {
            SendReply(player, "Teleport cancelled - you moved!");
            return;
        }
        
        // Check if player took damage
        if (player.IsWounded() || player.IsDead())
        {
            SendReply(player, "Teleport cancelled!");
            return;
        }
        
        TeleportPlayer(player, destination);
        SendReply(player, "Teleported!");
    });
}

private void CancelTeleport(BasePlayer player)
{
    if (pendingTeleports.TryGetValue(player.userID, out Timer t))
    {
        t?.Destroy();
        pendingTeleports.Remove(player.userID);
    }
}

// Cancel on damage
private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
{
    var player = entity as BasePlayer;
    if (player != null && pendingTeleports.ContainsKey(player.userID))
    {
        CancelTeleport(player);
        SendReply(player, "Teleport cancelled - you took damage!");
    }
}
```

### Home/Warp System Pattern
```csharp
private class HomeData
{
    public Dictionary<ulong, Dictionary<string, Vector3>> Homes = new Dictionary<ulong, Dictionary<string, Vector3>>();
}

private void SetHome(BasePlayer player, string homeName)
{
    if (!homeData.Homes.ContainsKey(player.userID))
        homeData.Homes[player.userID] = new Dictionary<string, Vector3>();
    
    homeData.Homes[player.userID][homeName] = player.transform.position;
    SaveData();
    
    SendReply(player, $"Home '{homeName}' set!");
}

private void TeleportHome(BasePlayer player, string homeName)
{
    if (!homeData.Homes.TryGetValue(player.userID, out var homes))
    {
        SendReply(player, "You have no homes set!");
        return;
    }
    
    if (!homes.TryGetValue(homeName, out Vector3 position))
    {
        SendReply(player, $"Home '{homeName}' not found!");
        return;
    }
    
    StartTeleport(player, position);
}

private List<string> GetHomeNames(BasePlayer player)
{
    if (!homeData.Homes.TryGetValue(player.userID, out var homes))
        return new List<string>();
    
    return homes.Keys.ToList();
}
```

---

## COMPLETE EXAMPLE: COOLDOWN PATTERNS

### Simple Cooldown
```csharp
private Dictionary<ulong, double> cooldowns = new Dictionary<ulong, double>();

private bool IsOnCooldown(ulong playerId, out double remaining)
{
    remaining = 0;
    
    if (!cooldowns.TryGetValue(playerId, out double endTime))
        return false;
    
    remaining = endTime - CurrentTime;
    return remaining > 0;
}

private void SetCooldown(ulong playerId, double seconds)
{
    cooldowns[playerId] = CurrentTime + seconds;
}

private void ClearCooldown(ulong playerId)
{
    cooldowns.Remove(playerId);
}

private static double CurrentTime => DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;

// Usage
private void UseAbility(BasePlayer player)
{
    if (IsOnCooldown(player.userID, out double remaining))
    {
        SendReply(player, $"On cooldown! Wait {remaining:F1} seconds.");
        return;
    }
    
    // Do ability
    DoAbility(player);
    
    // Set cooldown
    SetCooldown(player.userID, 60);  // 60 second cooldown
}
```

### Multi-Action Cooldowns
```csharp
private Dictionary<ulong, Dictionary<string, double>> actionCooldowns = new Dictionary<ulong, Dictionary<string, double>>();

private bool IsOnCooldown(ulong playerId, string action, out double remaining)
{
    remaining = 0;
    
    if (!actionCooldowns.TryGetValue(playerId, out var actions))
        return false;
    
    if (!actions.TryGetValue(action, out double endTime))
        return false;
    
    remaining = endTime - CurrentTime;
    return remaining > 0;
}

private void SetCooldown(ulong playerId, string action, double seconds)
{
    if (!actionCooldowns.ContainsKey(playerId))
        actionCooldowns[playerId] = new Dictionary<string, double>();
    
    actionCooldowns[playerId][action] = CurrentTime + seconds;
}

// Usage
private void TeleportCommand(BasePlayer player)
{
    if (IsOnCooldown(player.userID, "teleport", out double remaining))
    {
        SendReply(player, $"Teleport on cooldown! Wait {FormatTime(remaining)}");
        return;
    }
    
    // Do teleport
    SetCooldown(player.userID, "teleport", config.TeleportCooldown);
}

private string FormatTime(double seconds)
{
    var ts = TimeSpan.FromSeconds(seconds);
    if (ts.TotalHours >= 1)
        return $"{ts.Hours}h {ts.Minutes}m";
    if (ts.TotalMinutes >= 1)
        return $"{ts.Minutes}m {ts.Seconds}s";
    return $"{ts.Seconds}s";
}
```


---

## COMPLETE EXAMPLE: PERMISSION-BASED SETTINGS

### Permission Tiers Pattern
```csharp
private class PermissionTier
{
    public string Permission;
    public int Priority;
    public float Multiplier;
    public int MaxUses;
    public float Cooldown;
}

private List<PermissionTier> permissionTiers = new List<PermissionTier>
{
    new PermissionTier { Permission = "plugin.vip3", Priority = 3, Multiplier = 3f, MaxUses = -1, Cooldown = 30f },
    new PermissionTier { Permission = "plugin.vip2", Priority = 2, Multiplier = 2f, MaxUses = 50, Cooldown = 60f },
    new PermissionTier { Permission = "plugin.vip1", Priority = 1, Multiplier = 1.5f, MaxUses = 20, Cooldown = 120f },
    new PermissionTier { Permission = "plugin.use", Priority = 0, Multiplier = 1f, MaxUses = 5, Cooldown = 300f }
};

private PermissionTier GetPlayerTier(BasePlayer player)
{
    PermissionTier bestTier = null;
    
    foreach (var tier in permissionTiers)
    {
        if (permission.UserHasPermission(player.UserIDString, tier.Permission))
        {
            if (bestTier == null || tier.Priority > bestTier.Priority)
                bestTier = tier;
        }
    }
    
    return bestTier;
}

// Usage
private void UseFeature(BasePlayer player)
{
    var tier = GetPlayerTier(player);
    if (tier == null)
    {
        SendReply(player, "You don't have permission!");
        return;
    }
    
    // Apply tier settings
    float multiplier = tier.Multiplier;
    float cooldown = tier.Cooldown;
    
    // Check cooldown
    if (IsOnCooldown(player.userID, out double remaining))
    {
        SendReply(player, $"Wait {remaining:F0} seconds!");
        return;
    }
    
    // Do feature with multiplier
    DoFeature(player, multiplier);
    
    // Set cooldown based on tier
    SetCooldown(player.userID, cooldown);
}
```

### Dynamic Permission Registration
```csharp
private void Init()
{
    // Register base permissions
    permission.RegisterPermission("plugin.use", this);
    permission.RegisterPermission("plugin.admin", this);
    
    // Register tier permissions from config
    foreach (var tier in config.Tiers)
    {
        if (!string.IsNullOrEmpty(tier.Permission))
            permission.RegisterPermission(tier.Permission, this);
    }
    
    // Register per-item permissions
    foreach (var item in config.Items)
    {
        if (!string.IsNullOrEmpty(item.Permission))
            permission.RegisterPermission(item.Permission, this);
    }
}
```

---

## COMPLETE EXAMPLE: WEB REQUEST PATTERNS

### GET Request
```csharp
private void FetchData(string url, Action<string> callback)
{
    webrequest.Enqueue(url, null, (code, response) =>
    {
        if (code != 200 || string.IsNullOrEmpty(response))
        {
            PrintError($"Web request failed: {code}");
            callback?.Invoke(null);
            return;
        }
        
        callback?.Invoke(response);
    }, this, RequestMethod.GET);
}

// Usage
private void GetPlayerStats(ulong oderId)
{
    FetchData($"https://api.example.com/stats/{userId}", response =>
    {
        if (response == null) return;
        
        var stats = JsonConvert.DeserializeObject<PlayerStats>(response);
        // Use stats
    });
}
```

### POST Request
```csharp
private void PostData(string url, object data, Action<bool> callback)
{
    string json = JsonConvert.SerializeObject(data);
    
    webrequest.Enqueue(url, json, (code, response) =>
    {
        callback?.Invoke(code >= 200 && code < 300);
    }, this, RequestMethod.POST, new Dictionary<string, string>
    {
        ["Content-Type"] = "application/json"
    });
}

// Usage
private void ReportKill(BasePlayer killer, BasePlayer victim)
{
    var data = new
    {
        killer = killer.UserIDString,
        victim = victim.UserIDString,
        timestamp = DateTime.UtcNow
    };
    
    PostData("https://api.example.com/kills", data, success =>
    {
        if (!success)
            PrintWarning("Failed to report kill");
    });
}
```

### Discord Webhook
```csharp
private void SendDiscordMessage(string webhookUrl, string message, string username = "Rust Server")
{
    var payload = new
    {
        content = message,
        username = username
    };
    
    string json = JsonConvert.SerializeObject(payload);
    
    webrequest.Enqueue(webhookUrl, json, (code, response) =>
    {
        if (code != 204 && code != 200)
            PrintWarning($"Discord webhook failed: {code}");
    }, this, RequestMethod.POST, new Dictionary<string, string>
    {
        ["Content-Type"] = "application/json"
    });
}

// Rich embed
private void SendDiscordEmbed(string webhookUrl, string title, string description, int color = 0x00FF00)
{
    var payload = new
    {
        embeds = new[]
        {
            new
            {
                title = title,
                description = description,
                color = color,
                timestamp = DateTime.UtcNow.ToString("o")
            }
        }
    };
    
    string json = JsonConvert.SerializeObject(payload);
    
    webrequest.Enqueue(webhookUrl, json, (code, response) => { }, this, RequestMethod.POST,
        new Dictionary<string, string> { ["Content-Type"] = "application/json" });
}
```

---

## COMPLETE EXAMPLE: CONSOLE COMMAND PATTERNS

### Admin Console Commands
```csharp
[ConsoleCommand("myplugin.give")]
private void CmdGive(ConsoleSystem.Arg arg)
{
    // Check if from server console or admin
    if (!arg.IsAdmin && arg.Connection != null)
    {
        arg.ReplyWith("Admin only!");
        return;
    }
    
    if (!arg.HasArgs(2))
    {
        arg.ReplyWith("Usage: myplugin.give <player> <item>");
        return;
    }
    
    var target = BasePlayer.Find(arg.GetString(0));
    if (target == null)
    {
        arg.ReplyWith("Player not found!");
        return;
    }
    
    string itemName = arg.GetString(1);
    int amount = arg.GetInt(2, 1);
    
    // Give item
    var item = ItemManager.CreateByName(itemName, amount);
    if (item == null)
    {
        arg.ReplyWith($"Invalid item: {itemName}");
        return;
    }
    
    target.GiveItem(item);
    arg.ReplyWith($"Gave {amount}x {itemName} to {target.displayName}");
}

[ConsoleCommand("myplugin.reload")]
private void CmdReload(ConsoleSystem.Arg arg)
{
    if (!arg.IsAdmin) return;
    
    LoadConfig();
    LoadData();
    
    arg.ReplyWith("Plugin reloaded!");
}
```

### Player Console Commands
```csharp
[ConsoleCommand("myplugin.ui")]
private void CmdUI(ConsoleSystem.Arg arg)
{
    var player = arg.Player();
    if (player == null) return;
    
    string action = arg.GetString(0);
    
    switch (action)
    {
        case "open":
            ShowUI(player);
            break;
        case "close":
            CloseUI(player);
            break;
        case "page":
            int page = arg.GetInt(1, 0);
            ShowPage(player, page);
            break;
        case "buy":
            int itemId = arg.GetInt(1);
            int quantity = arg.GetInt(2, 1);
            BuyItem(player, itemId, quantity);
            break;
    }
}
```

---

## CRITICAL: COMMON MISTAKES TO AVOID

### ❌ WRONG: Using wrong class for resources
```csharp
// WRONG - ResourceContainer doesn't have containedItems!
var container = ore.GetComponent<ResourceContainer>();
foreach (var item in container.containedItems)  // ERROR!
```

### ✅ CORRECT: Use ResourceDispenser for mining nodes
```csharp
// CORRECT - ResourceDispenser has containedItems
var dispenser = ore.GetComponent<ResourceDispenser>();
foreach (var item in dispenser.containedItems)
{
    string shortname = item.itemDef.shortname;  // ItemAmount uses itemDef
    float amount = item.amount;
}
```

### ❌ WRONG: Using userID for permissions
```csharp
// WRONG - userID is ulong, permissions need string
permission.UserHasPermission(player.userID, "perm");  // ERROR!
```

### ✅ CORRECT: Use UserIDString for permissions
```csharp
// CORRECT - UserIDString is the string version
permission.UserHasPermission(player.UserIDString, "perm");
```

### ❌ WRONG: Not cleaning up in Unload
```csharp
// WRONG - UI and timers will persist!
private void Unload()
{
    SaveData();
    // Missing UI cleanup!
    // Missing timer cleanup!
}
```

### ✅ CORRECT: Clean up everything in Unload
```csharp
// CORRECT - Clean up all resources
private void Unload()
{
    SaveData();
    
    // Destroy all UI
    foreach (var player in BasePlayer.activePlayerList)
        CuiHelper.DestroyUi(player, UI_PANEL);
    
    // Destroy all timers
    foreach (var t in playerTimers.Values)
        t?.Destroy();
    playerTimers.Clear();
    
    // Destroy all components
    foreach (var player in BasePlayer.activePlayerList)
    {
        var comp = player.GetComponent<MyComponent>();
        if (comp != null) UnityEngine.Object.Destroy(comp);
    }
    
    // Clear static instance
    Instance = null;
}
```

### ❌ WRONG: Not null-checking players
```csharp
// WRONG - player could be null or disconnected!
private void DoSomething(BasePlayer player)
{
    player.ChatMessage("Hello!");  // Could crash!
}
```

### ✅ CORRECT: Always null-check players
```csharp
// CORRECT - Check player validity
private void DoSomething(BasePlayer player)
{
    if (player == null || !player.IsConnected) return;
    player.ChatMessage("Hello!");
}
```


---

## COMPLETE EXAMPLE: FULL WORKING PLUGIN TEMPLATE

Use this as a starting point for any new plugin:

```csharp
using Facepunch;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("MyPlugin", "YourName", "1.0.0")]
    [Description("Description of what this plugin does")]
    public class MyPlugin : RustPlugin
    {
        #region Fields
        [PluginReference] private Plugin Economics, ImageLibrary, Friends, Clans;
        
        private static MyPlugin Instance;
        private Configuration config;
        private StoredData storedData;
        private bool dataChanged;
        
        private const string PERM_USE = "myplugin.use";
        private const string PERM_ADMIN = "myplugin.admin";
        private const string UI_PANEL = "MyPlugin_UI";
        
        private Dictionary<ulong, Timer> playerTimers = new Dictionary<ulong, Timer>();
        #endregion

        #region Configuration
        private class Configuration
        {
            [JsonProperty("Enable Feature")]
            public bool EnableFeature = true;
            
            [JsonProperty("Cooldown (seconds)")]
            public float Cooldown = 60f;
            
            [JsonProperty("Max Uses")]
            public int MaxUses = 10;
            
            public string ToJson() => JsonConvert.SerializeObject(this);
            public Dictionary<string, object> ToDictionary() => JsonConvert.DeserializeObject<Dictionary<string, object>>(ToJson());
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
                    PrintWarning("Config outdated; updating...");
                    SaveConfig();
                }
            }
            catch
            {
                PrintWarning("Invalid config; using defaults");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig() => Config.WriteObject(config, true);
        #endregion

        #region Data Storage
        private class StoredData
        {
            public Dictionary<ulong, PlayerData> Players = new Dictionary<ulong, PlayerData>();
        }

        private class PlayerData
        {
            public int Uses;
            public double LastUsed;
            public Dictionary<string, object> Custom = new Dictionary<string, object>();
        }

        private void LoadData()
        {
            storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name) ?? new StoredData();
        }

        private void SaveData()
        {
            if (dataChanged)
            {
                Interface.Oxide.DataFileSystem.WriteObject(Name, storedData);
                dataChanged = false;
            }
        }

        private PlayerData GetPlayerData(ulong oderId)
        {
            if (!storedData.Players.TryGetValue(userId, out var data))
            {
                data = new PlayerData();
                storedData.Players[userId] = data;
                dataChanged = true;
            }
            return data;
        }
        #endregion

        #region Localization
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NoPermission"] = "You don't have permission to use this!",
                ["Cooldown"] = "Please wait {0} seconds.",
                ["Success"] = "Action completed successfully!",
                ["Error"] = "An error occurred: {0}",
                ["InvalidSyntax"] = "Usage: /{0} <args>"
            }, this);
        }

        private string Lang(string key, string userId = null, params object[] args)
        {
            var message = lang.GetMessage(key, this, userId);
            return args.Length > 0 ? string.Format(message, args) : message;
        }

        private void Message(BasePlayer player, string key, params object[] args)
        {
            if (player?.IsConnected == true)
                SendReply(player, Lang(key, player.UserIDString, args));
        }
        #endregion

        #region Oxide Hooks
        private void Init()
        {
            Instance = this;
            permission.RegisterPermission(PERM_USE, this);
            permission.RegisterPermission(PERM_ADMIN, this);
            LoadData();
        }

        private void OnServerInitialized()
        {
            // Plugin fully loaded
        }

        private void OnServerSave() => SaveData();

        private void Unload()
        {
            SaveData();
            
            // Destroy UI for all players
            foreach (var player in BasePlayer.activePlayerList)
                CuiHelper.DestroyUi(player, UI_PANEL);
            
            // Destroy all timers
            foreach (var t in playerTimers.Values)
                t?.Destroy();
            playerTimers.Clear();
            
            Instance = null;
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (player == null) return;
            // Handle player connect
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (player == null) return;
            
            // Cleanup player data
            CuiHelper.DestroyUi(player, UI_PANEL);
            
            if (playerTimers.TryGetValue(player.userID, out var t))
            {
                t?.Destroy();
                playerTimers.Remove(player.userID);
            }
        }
        #endregion

        #region Commands
        [ChatCommand("mycommand")]
        private void CmdMyCommand(BasePlayer player, string command, string[] args)
        {
            if (player == null || !player.IsConnected) return;
            
            if (!permission.UserHasPermission(player.UserIDString, PERM_USE))
            {
                Message(player, "NoPermission");
                return;
            }
            
            if (args.Length == 0)
            {
                Message(player, "InvalidSyntax", command);
                return;
            }
            
            // Handle command
            switch (args[0].ToLower())
            {
                case "help":
                    ShowHelp(player);
                    break;
                case "ui":
                    ShowUI(player);
                    break;
                default:
                    Message(player, "InvalidSyntax", command);
                    break;
            }
        }

        [ConsoleCommand("myplugin.action")]
        private void CmdAction(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            
            string action = arg.GetString(0);
            // Handle console command
        }
        #endregion

        #region UI
        private void ShowUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, UI_PANEL);
            
            var container = new CuiElementContainer();
            
            // Main panel
            container.Add(new CuiPanel
            {
                Image = { Color = "0.1 0.1 0.1 0.95" },
                RectTransform = { AnchorMin = "0.3 0.3", AnchorMax = "0.7 0.7" },
                CursorEnabled = true
            }, "Overlay", UI_PANEL);
            
            // Title
            container.Add(new CuiLabel
            {
                Text = { Text = "My Plugin", FontSize = 20, Align = TextAnchor.MiddleCenter },
                RectTransform = { AnchorMin = "0 0.85", AnchorMax = "1 1" }
            }, UI_PANEL);
            
            // Close button
            container.Add(new CuiButton
            {
                Button = { Color = "0.8 0.2 0.2 1", Close = UI_PANEL },
                RectTransform = { AnchorMin = "0.9 0.9", AnchorMax = "0.98 0.98" },
                Text = { Text = "X", FontSize = 14, Align = TextAnchor.MiddleCenter }
            }, UI_PANEL);
            
            CuiHelper.AddUi(player, container);
        }
        #endregion

        #region Helpers
        private bool HasPermission(BasePlayer player, string perm)
        {
            return permission.UserHasPermission(player.UserIDString, perm);
        }

        private void ShowHelp(BasePlayer player)
        {
            SendReply(player, "=== My Plugin Help ===");
            SendReply(player, "/mycommand help - Show this help");
            SendReply(player, "/mycommand ui - Open UI");
        }
        #endregion
    }
}
```

---

## END OF EXAMPLES

**Remember:**
1. ALWAYS read this file before writing any plugin code
2. COPY patterns exactly - don't invent your own
3. NULL CHECK everything - players, entities, items
4. CLEAN UP in Unload() - UI, timers, components
5. Use UserIDString for permissions, NOT userID
6. ResourceDispenser for mining nodes, NOT ResourceContainer
7. ItemAmount.itemDef for dispenser items, Item.info for inventory items
