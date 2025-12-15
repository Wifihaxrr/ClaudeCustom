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
        
        // Track hits per node - key is node NetworkID, value is (hits taken, hits needed to break)
        private Dictionary<uint, NodeHitData> nodeHits = new Dictionary<uint, NodeHitData>();
        
        // Full-auto and overheat tracking
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
            public float Heat; // 0-100
            public bool Overheated;
            public float LastFireTime;
            public float OverheatEndTime; // When overheat ends
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
            
            [JsonProperty("AOE radius - damage nodes within this distance of impact (meters)")]
            public float AOERadius { get; set; } = 10f;
            
            [JsonProperty("Fire cooldown (seconds)")]
            public float Cooldown { get; set; } = 0.2f;
            
            [JsonProperty("Minimum hits to break node")]
            public int MinHitsToBreak { get; set; } = 1;
            
            [JsonProperty("Maximum hits to break node")]
            public int MaxHitsToBreak { get; set; } = 6;
            
            [JsonProperty("Custom item name for Mining Gun")]
            public string ItemName { get; set; } = "Mining Laser";
            
            [JsonProperty("Custom item skin ID (0 for default)")]
            public ulong SkinID { get; set; } = 0;
            
            [JsonProperty("Require special nailgun (by name/skin) or any nailgun")]
            public bool RequireSpecialNailgun { get; set; } = true;
            
            [JsonProperty("Fire rate (shots per second) - full auto")]
            public float FireRate { get; set; } = 3f;
            
            [JsonProperty("Heat per shot")]
            public float HeatPerShot { get; set; } = 5f;
            
            [JsonProperty("Heat cooldown per second")]
            public float HeatCooldownRate { get; set; } = 4f;
            
            [JsonProperty("Overheat threshold (0-100)")]
            public float OverheatThreshold { get; set; } = 100f;
            
            [JsonProperty("Overheat cooldown time (seconds before can fire again)")]
            public float OverheatCooldown { get; set; } = 15f;
            
            [JsonProperty("Beam effect prefab (effect shown along beam path)")]
            public string BeamEffectPrefab { get; set; } = "assets/bundled/prefabs/fx/impacts/additive/fire.prefab";
            
            [JsonProperty("Impact effect prefab (effect shown at hit point)")]
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
            
            // Check all online players for mining gun
            foreach (var player in BasePlayer.activePlayerList)
            {
                CheckAndShowUI(player);
            }
            
            Puts("===========================================");
            Puts("  MINING GUN v1.0 - LOADED!");
            Puts($"  Range: {config.Gun.Range}m | AOE: {config.Gun.AOERadius}m");
            Puts($"  Hits to break: {config.Gun.MinHitsToBreak}-{config.Gun.MaxHitsToBreak}");
            Puts("===========================================");
        }
        
        private void Unload()
        {
            nodeHits.Clear();
            
            // Stop all auto-fire timers
            foreach (var t in autoFireTimers.Values)
                t?.Destroy();
            autoFireTimers.Clear();
            
            // Remove UI from all players
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
            
            // Handle fire button press - start auto fire
            if (input.WasJustPressed(BUTTON.FIRE_PRIMARY))
            {
                StartAutoFire(player);
            }
            // Handle fire button release - stop auto fire
            else if (input.WasJustReleased(BUTTON.FIRE_PRIMARY))
            {
                StopAutoFire(player);
            }
        }
        
        private void StartAutoFire(BasePlayer player)
        {
            if (autoFireTimers.ContainsKey(player.userID)) return;
            
            // Initialize heat data if needed
            if (!playerHeat.TryGetValue(player.userID, out PlayerHeatData heatData))
            {
                heatData = new PlayerHeatData { Heat = 0, Overheated = false };
                playerHeat[player.userID] = heatData;
            }
            
            // Check if overheated
            if (heatData.Overheated)
            {
                SendReply(player, "<color=#ff4444>[!] OVERHEATED - Wait for cooldown!</color>");
                return;
            }
            
            // Fire immediately
            TryFireShot(player);
            
            // Start auto-fire timer
            float fireInterval = 1f / config.Gun.FireRate;
            autoFireTimers[player.userID] = timer.Every(fireInterval, () =>
            {
                if (player == null || !player.IsConnected)
                {
                    StopAutoFire(player);
                    return;
                }
                
                // Check if still holding mining gun
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
            
            // Start cooling down
            StartCooldown(player);
        }
        
        private void TryFireShot(BasePlayer player)
        {
            if (!playerHeat.TryGetValue(player.userID, out PlayerHeatData heatData))
            {
                heatData = new PlayerHeatData { Heat = 0, Overheated = false };
                playerHeat[player.userID] = heatData;
            }
            
            // Check overheat
            if (heatData.Overheated)
            {
                StopAutoFire(player);
                return;
            }
            
            // Fire the laser
            FireMiningLaser(player);
            
            // Add heat
            heatData.Heat += config.Gun.HeatPerShot;
            heatData.LastFireTime = Time.realtimeSinceStartup;
            
            // Check if overheated
            if (heatData.Heat >= config.Gun.OverheatThreshold)
            {
                heatData.Heat = config.Gun.OverheatThreshold;
                heatData.Overheated = true;
                heatData.OverheatEndTime = Time.realtimeSinceStartup + config.Gun.OverheatCooldown;
                StopAutoFire(player);
                SendReply(player, "<color=#ff4444>[!] OVERHEATED!</color>");
                
                // Start countdown UI updates
                StartOverheatCountdown(player);
                
                // End overheat after cooldown
                timer.Once(config.Gun.OverheatCooldown, () =>
                {
                    if (playerHeat.TryGetValue(player.userID, out PlayerHeatData data))
                    {
                        data.Overheated = false;
                        data.Heat = 0;
                        UpdateHeatUI(player);
                        if (player != null && player.IsConnected)
                            SendReply(player, "<color=#44ff44>[OK] Cooled down - Ready to fire!</color>");
                    }
                });
            }
            
            UpdateHeatUI(player);
        }
        
        private void StartCooldown(BasePlayer player)
        {
            if (player == null) return;
            
            // Gradual cooldown when not firing
            timer.Every(0.1f, () =>
            {
                if (player == null || !player.IsConnected) return;
                if (autoFireTimers.ContainsKey(player.userID)) return; // Still firing
                
                if (!playerHeat.TryGetValue(player.userID, out PlayerHeatData heatData)) return;
                if (heatData.Overheated) return; // Waiting for overheat cooldown
                
                heatData.Heat -= config.Gun.HeatCooldownRate * 0.1f;
                if (heatData.Heat <= 0)
                {
                    heatData.Heat = 0;
                    CuiHelper.DestroyUi(player, HEAT_UI);
                    return;
                }
                
                UpdateHeatUI(player);
            });
        }
        
        private void StartOverheatCountdown(BasePlayer player)
        {
            // Update UI every 0.5 seconds during overheat
            timer.Every(0.5f, () =>
            {
                if (player == null || !player.IsConnected) return;
                if (!playerHeat.TryGetValue(player.userID, out PlayerHeatData heatData)) return;
                if (!heatData.Overheated) return; // No longer overheated
                
                UpdateHeatUI(player);
            });
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
                
                // Check if we hit a node directly
                var directHitNode = hit.GetEntity() as OreResourceEntity;
                if (directHitNode != null && !directHitNode.IsDestroyed)
                {
                    hitPoint = endPos;
                    nodesInRange.Add(directHitNode);
                    
                    // Also get nearby nodes in AOE
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
            
            // Check if there are any nodes near where we're aiming (within AOE)
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
            
            // Check if we have a node targeted
            if (!HasNodeInSight(player, out Vector3 hitPoint, out List<OreResourceEntity> nodes))
            {
                // No node in sight - don't fire
                return;
            }
            
            // Show beam effect to the hit point
            ShowBeamEffect(player, startPos, hitPoint);
            
            // Damage all nodes in range
            foreach (var ore in nodes)
            {
                if (ore != null && !ore.IsDestroyed)
                {
                    DamageNode(player, ore);
                }
            }
        }
        
        private void ShowBeamEffect(BasePlayer player, Vector3 start, Vector3 end)
        {
            // Create beam using effects along the path
            float distance = Vector3.Distance(start, end);
            int segments = Mathf.Max(8, (int)(distance / 1.5f));
            
            for (int i = 1; i <= segments; i++)
            {
                float t = (float)i / segments;
                Vector3 pos = Vector3.Lerp(start, end, t);
                
                // Beam effect from config
                Effect.server.Run(config.Gun.BeamEffectPrefab, pos);
            }
            
            // Impact effect at end point from config
            Effect.server.Run(config.Gun.ImpactEffectPrefab, end);
        }
        
        private void DamageNode(BasePlayer player, OreResourceEntity ore)
        {
            if (ore == null || ore.IsDestroyed) return;
            
            uint nodeId = (uint)ore.net.ID.Value;
            
            // Get or create hit tracking for this node
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
            
            // Play rock hit sound
            Effect.server.Run(config.Gun.RockHitSoundPrefab, ore.transform.position);
            
            // Show hit effect on the node
            Effect.server.Run("assets/bundled/prefabs/fx/survey_explosion.prefab", ore.transform.position);
            
            // Check if node should break
            if (hitData.HitsTaken >= hitData.HitsNeeded)
            {
                // Play break sound
                Effect.server.Run(config.Gun.RockBreakSoundPrefab, ore.transform.position);
                
                // Give resources to player (gather the node)
                GatherFromNode(player, ore);
                
                // Clean up tracking
                nodeHits.Remove(nodeId);
                
                // Destroy the node
                ore.Kill();
            }
        }
        
        private void GatherFromNode(BasePlayer player, OreResourceEntity ore)
        {
            if (ore == null || player == null) return;
            
            // Get the resource dispenser
            var dispenser = ore.GetComponent<ResourceDispenser>();
            if (dispenser == null) return;
            
            // Gather all remaining resources from the node
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
            
            // Main panel - bottom left, just above hotbar
            elements.Add(new CuiPanel
            {
                Image = { Color = "0.1 0.1 0.12 0.85" },
                RectTransform = { AnchorMin = "0.01 0.11", AnchorMax = "0.18 0.14" },
                CursorEnabled = false
            }, "Hud", UI_PANEL);
            
            // Cyan accent bar on left
            elements.Add(new CuiPanel
            {
                Image = { Color = "0.2 0.8 1 0.9" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "0.015 1" }
            }, UI_PANEL);
            
            // Title + Stats combined (compact single line)
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
            
            // Heat bar - just above the main UI panel
            elements.Add(new CuiPanel
            {
                Image = { Color = "0.1 0.1 0.12 0.85" },
                RectTransform = { AnchorMin = "0.01 0.145", AnchorMax = "0.18 0.17" },
                CursorEnabled = false
            }, "Hud", HEAT_UI);
            
            // Heat bar fill
            float barWidth = 0.02f + (0.96f * heatPercent);
            elements.Add(new CuiPanel
            {
                Image = { Color = barColor },
                RectTransform = { AnchorMin = "0.02 0.15", AnchorMax = $"{barWidth:F3} 0.85" }
            }, HEAT_UI);
            
            // Heat label with countdown when overheated
            string heatText;
            if (heatData.Overheated)
            {
                float timeLeft = heatData.OverheatEndTime - Time.realtimeSinceStartup;
                if (timeLeft < 0) timeLeft = 0;
                heatText = $"OVERHEATED! {timeLeft:F1}s";
            }
            else
            {
                heatText = $"HEAT: {(heatPercent * 100):F0}%";
            }
            
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
                    $"AOE Radius: {config.Gun.AOERadius}m\n" +
                    $"Hits to break: {config.Gun.MinHitsToBreak}-{config.Gun.MaxHitsToBreak}\n\n" +
                    "<color=#888888>/mininggun give</color> - Give yourself a Mining Gun\n" +
                    "<color=#888888>/mininggun give <player></color> - Give to player");
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
                        SendReply(player, $"<color=#ff4444>Player '{args[1]}' not found.</color>");
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
            
            // Set custom description with weapon stats
            item.text = $"[MINING LASER]\n" +
                       $"Range: {config.Gun.Range}m\n" +
                       $"AOE: {config.Gun.AOERadius}m radius\n" +
                       $"Hits to break: {config.Gun.MinHitsToBreak}-{config.Gun.MaxHitsToBreak}\n" +
                       $"No ammo required!";
            
            var weapon = item.GetHeldEntity() as BaseProjectile;
            if (weapon != null)
            {
                weapon.primaryMagazine.contents = 0;
                weapon.primaryMagazine.capacity = 0;
            }
            
            player.GiveItem(item);
            
            SendReply(player, 
                $"<color=#ff8800>You received a {config.Gun.ItemName}!</color>\n" +
                "<color=#aaaaaa>Shoot nodes from distance - breaks in 1-6 hits!</color>\n" +
                $"<color=#666666>Range: {config.Gun.Range}m | AOE: {config.Gun.AOERadius}m</color>");
        }
        #endregion
    }
}
