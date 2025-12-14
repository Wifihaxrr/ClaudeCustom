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
