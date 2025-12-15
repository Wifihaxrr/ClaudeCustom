using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("MiniGunner", "ZombiePVE", "1.1.0")]
    [Description("Allows players to reload miniguns by holding R key without a workbench")]
    public class MiniGunner : RustPlugin
    {
        #region Fields
        private Configuration config;
        private Dictionary<ulong, ReloadData> reloadingPlayers = new Dictionary<ulong, ReloadData>();
        private const string UI_PANEL = "MiniGunner_Progress";
        private const string MINIGUN_SHORTNAME = "minigun";
        
        // All 556 ammo types
        private readonly string[] ammoShortnames = new string[]
        {
            "ammo.rifle",           // Regular 5.56
            "ammo.rifle.explosive", // Explosive 5.56
            "ammo.rifle.incendiary",// Incendiary 5.56
            "ammo.rifle.hv"         // High Velocity 5.56
        };
        #endregion

        #region Classes
        private class ReloadData
        {
            public float StartTime;
            public float Duration;
            public BaseProjectile Weapon;
            public Timer UpdateTimer;
            public Vector3 StartPosition;
            public ItemDefinition AmmoType;
            public int AmmoToLoad;
        }
        #endregion

        #region Configuration
        private class Configuration
        {
            [JsonProperty("Permission to Use")]
            public string Permission { get; set; } = "minigunner.use";

            [JsonProperty("Max Ammo")]
            public int MaxAmmo { get; set; } = 600;

            [JsonProperty("Reload Time (seconds)")]
            public float ReloadTime { get; set; } = 10f;

            [JsonProperty("Allow Movement While Reloading")]
            public bool AllowMovement { get; set; } = true;
            
            [JsonProperty("Show Progress Bar")]
            public bool ShowProgressBar { get; set; } = true;
            
            [JsonProperty("Play Reload Sounds")]
            public bool PlaySounds { get; set; } = true;
            
            [JsonProperty("Durability Multiplier (2.0 = lasts 2x longer)")]
            public float DurabilityMultiplier { get; set; } = 2.0f;
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
            permission.RegisterPermission(config.Permission, this);
            Puts($"MiniGunner loaded - Reload time: {config.ReloadTime}s, Max ammo: {config.MaxAmmo}");
        }

        private void Unload()
        {
            foreach (var data in reloadingPlayers.Values)
                data.UpdateTimer?.Destroy();
            
            foreach (var player in BasePlayer.activePlayerList)
                CuiHelper.DestroyUi(player, UI_PANEL);
            
            reloadingPlayers.Clear();
        }

        private void OnPlayerDisconnected(BasePlayer player) => CancelReload(player);

        private void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (player == null || !player.IsConnected || player.IsDead()) return;
            if (!permission.UserHasPermission(player.UserIDString, config.Permission)) return;

            if (input.WasJustPressed(BUTTON.RELOAD))
            {
                TryStartReload(player);
            }
            else if (input.WasJustReleased(BUTTON.RELOAD))
            {
                if (reloadingPlayers.ContainsKey(player.userID))
                {
                    CancelReload(player);
                    SendReply(player, "<color=#ff4444>Reload cancelled</color>");
                }
            }
        }

        private void OnActiveItemChanged(BasePlayer player, Item oldItem, Item newItem)
        {
            if (player == null) return;
            CancelReload(player);
        }
        
        // Reduce durability loss on minigun
        private void OnLoseCondition(Item item, ref float amount)
        {
            if (item == null || item.info.shortname != MINIGUN_SHORTNAME) return;
            if (config.DurabilityMultiplier <= 0) return;
            
            // Reduce durability loss
            amount /= config.DurabilityMultiplier;
        }
        #endregion

        #region Reload Logic
        private void TryStartReload(BasePlayer player)
        {
            if (reloadingPlayers.ContainsKey(player.userID)) return;

            var heldItem = player.GetActiveItem();
            if (heldItem == null || heldItem.info.shortname != MINIGUN_SHORTNAME) return;

            var weapon = heldItem.GetHeldEntity() as BaseProjectile;
            if (weapon == null) return;

            if (weapon.primaryMagazine.contents >= config.MaxAmmo)
            {
                SendReply(player, "<color=#ffaa00>Minigun is already fully loaded!</color>");
                return;
            }

            // Find best ammo type - prioritize current ammo type, then check all 556 types
            ItemDefinition bestAmmo = null;
            int bestAmount = 0;
            
            // First check current ammo type
            var currentAmmoType = weapon.primaryMagazine.ammoType;
            if (currentAmmoType != null)
            {
                int currentAmount = player.inventory.GetAmount(currentAmmoType.itemid);
                if (currentAmount > 0)
                {
                    bestAmmo = currentAmmoType;
                    bestAmount = currentAmount;
                }
            }
            
            // If no current ammo, check all 556 types
            if (bestAmmo == null)
            {
                foreach (var ammoName in ammoShortnames)
                {
                    var ammoDef = ItemManager.FindItemDefinition(ammoName);
                    if (ammoDef == null) continue;
                    
                    int amount = player.inventory.GetAmount(ammoDef.itemid);
                    if (amount > bestAmount)
                    {
                        bestAmmo = ammoDef;
                        bestAmount = amount;
                    }
                }
            }
            
            if (bestAmmo == null || bestAmount <= 0)
            {
                SendReply(player, "<color=#ff4444>No 5.56 ammo available!</color>");
                return;
            }

            StartReload(player, weapon, bestAmmo, bestAmount);
        }

        private void StartReload(BasePlayer player, BaseProjectile weapon, ItemDefinition ammoType, int availableAmmo)
        {
            int currentAmmo = weapon.primaryMagazine.contents;
            int neededAmmo = config.MaxAmmo - currentAmmo;
            int ammoToLoad = Math.Min(neededAmmo, availableAmmo);
            
            var data = new ReloadData
            {
                StartTime = Time.realtimeSinceStartup,
                Duration = config.ReloadTime,
                Weapon = weapon,
                StartPosition = player.transform.position,
                AmmoType = ammoType,
                AmmoToLoad = ammoToLoad
            };

            reloadingPlayers[player.userID] = data;

            string ammoName = GetAmmoDisplayName(ammoType.shortname);
            SendReply(player, $"<color=#44ff44>🔄 Loading {ammoToLoad}x {ammoName}... Hold R</color>");

            if (config.PlaySounds)
                Effect.server.Run("assets/prefabs/locks/keypad/effects/lock.code.lock.prefab", player.transform.position);

            if (config.ShowProgressBar)
                ShowProgressUI(player, 0f, ammoName);

            data.UpdateTimer = timer.Every(0.033f, () => // ~30fps for smooth animation
            {
                if (!reloadingPlayers.ContainsKey(player.userID)) return;

                if (!config.AllowMovement)
                {
                    float moved = Vector3.Distance(player.transform.position, data.StartPosition);
                    if (moved > 0.5f)
                    {
                        CancelReload(player);
                        SendReply(player, "<color=#ff4444>Reload cancelled - you moved!</color>");
                        return;
                    }
                }

                var currentItem = player.GetActiveItem();
                if (currentItem == null || currentItem.info.shortname != MINIGUN_SHORTNAME)
                {
                    CancelReload(player);
                    return;
                }

                float elapsed = Time.realtimeSinceStartup - data.StartTime;
                float progress = elapsed / data.Duration;

                if (progress >= 1f)
                    CompleteReload(player, data);
                else if (config.ShowProgressBar)
                    ShowProgressUI(player, progress, ammoName);
            });
        }

        private string GetAmmoDisplayName(string shortname)
        {
            switch (shortname)
            {
                case "ammo.rifle": return "5.56";
                case "ammo.rifle.explosive": return "Explosive 5.56";
                case "ammo.rifle.incendiary": return "Incendiary 5.56";
                case "ammo.rifle.hv": return "HV 5.56";
                default: return "5.56";
            }
        }

        private void CompleteReload(BasePlayer player, ReloadData data)
        {
            if (data.Weapon == null || data.Weapon.IsDestroyed)
            {
                CancelReload(player);
                return;
            }

            var weapon = data.Weapon;
            
            // Verify player still has the ammo
            int actualAvailable = player.inventory.GetAmount(data.AmmoType.itemid);
            int ammoToAdd = Math.Min(data.AmmoToLoad, actualAvailable);

            if (ammoToAdd > 0)
            {
                player.inventory.Take(null, data.AmmoType.itemid, ammoToAdd);
                
                // Set ammo type and load
                weapon.primaryMagazine.ammoType = data.AmmoType;
                weapon.primaryMagazine.capacity = config.MaxAmmo;
                weapon.primaryMagazine.contents += ammoToAdd;
                weapon.SendNetworkUpdateImmediate();
                
                if (config.PlaySounds)
                    Effect.server.Run("assets/prefabs/locks/keypad/effects/lock.code.unlock.prefab", player.transform.position);

                string ammoName = GetAmmoDisplayName(data.AmmoType.shortname);
                SendReply(player, $"<color=#44ff44>[OK] Loaded {ammoToAdd}x {ammoName} ({weapon.primaryMagazine.contents}/{config.MaxAmmo})</color>");
            }

            CancelReload(player);
        }

        private void CancelReload(BasePlayer player)
        {
            if (player == null) return;

            if (reloadingPlayers.TryGetValue(player.userID, out var data))
            {
                data.UpdateTimer?.Destroy();
                reloadingPlayers.Remove(player.userID);
            }

            CuiHelper.DestroyUi(player, UI_PANEL);
        }
        #endregion

        #region UI
        private void ShowProgressUI(BasePlayer player, float progress, string ammoName)
        {
            CuiHelper.DestroyUi(player, UI_PANEL);

            var elements = new CuiElementContainer();
            
            // Smooth progress value
            float smoothProgress = Mathf.Clamp01(progress);
            int percent = Mathf.RoundToInt(smoothProgress * 100f);
            
            // Main container - sleek compact bar
            elements.Add(new CuiPanel
            {
                Image = { Color = "0 0 0 0" },
                RectTransform = { AnchorMin = "0.38 0.125", AnchorMax = "0.62 0.155" },
                CursorEnabled = false
            }, "Overlay", UI_PANEL);

            // Outer border glow
            elements.Add(new CuiPanel
            {
                Image = { Color = "0.18 0.55 0.18 0.4" },
                RectTransform = { AnchorMin = "-0.008 -0.15", AnchorMax = "1.008 1.15" }
            }, UI_PANEL, UI_PANEL + "_glow");

            // Main background
            elements.Add(new CuiPanel
            {
                Image = { Color = "0.08 0.08 0.1 0.95" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
            }, UI_PANEL, UI_PANEL + "_bg");

            // Inner shadow
            elements.Add(new CuiPanel
            {
                Image = { Color = "0 0 0 0.3" },
                RectTransform = { AnchorMin = "0.01 0.08", AnchorMax = "0.99 0.25" }
            }, UI_PANEL + "_bg");

            // Progress track
            elements.Add(new CuiPanel
            {
                Image = { Color = "0.04 0.04 0.06 1" },
                RectTransform = { AnchorMin = "0.12 0.22", AnchorMax = "0.88 0.78" }
            }, UI_PANEL + "_bg", UI_PANEL + "_track");

            // Progress fill - gradient green
            if (smoothProgress > 0.005f)
            {
                // Base fill
                elements.Add(new CuiPanel
                {
                    Image = { Color = "0.15 0.65 0.25 1" },
                    RectTransform = { AnchorMin = "0.01 0.1", AnchorMax = $"{smoothProgress * 0.98f + 0.01f} 0.9" }
                }, UI_PANEL + "_track", UI_PANEL + "_fill");

                // Highlight on top half
                elements.Add(new CuiPanel
                {
                    Image = { Color = "0.25 0.85 0.35 0.6" },
                    RectTransform = { AnchorMin = "0 0.5", AnchorMax = "1 0.95" }
                }, UI_PANEL + "_fill", UI_PANEL + "_highlight");

                // Animated pulse edge
                float pulseAlpha = 0.4f + (Mathf.Sin(Time.realtimeSinceStartup * 8f) * 0.2f);
                elements.Add(new CuiPanel
                {
                    Image = { Color = $"0.5 1 0.5 {pulseAlpha}" },
                    RectTransform = { AnchorMin = "0.97 0", AnchorMax = "1 1" }
                }, UI_PANEL + "_fill");
            }

            // Left icon area
            elements.Add(new CuiPanel
            {
                Image = { Color = "0.12 0.12 0.15 1" },
                RectTransform = { AnchorMin = "0.015 0.15", AnchorMax = "0.1 0.85" }
            }, UI_PANEL + "_bg", UI_PANEL + "_icon");

            // Bullet icon
            elements.Add(new CuiLabel
            {
                Text = { Text = "▶", FontSize = 9, Align = TextAnchor.MiddleCenter, Color = "0.4 0.9 0.4 1" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
            }, UI_PANEL + "_icon");

            // Percentage on right
            elements.Add(new CuiPanel
            {
                Image = { Color = "0.12 0.12 0.15 1" },
                RectTransform = { AnchorMin = "0.89 0.15", AnchorMax = "0.985 0.85" }
            }, UI_PANEL + "_bg", UI_PANEL + "_pct_bg");

            elements.Add(new CuiLabel
            {
                Text = { Text = $"{percent}%", FontSize = 8, Align = TextAnchor.MiddleCenter, Color = "0.4 0.95 0.4 1", Font = "robotocondensed-bold.ttf" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
            }, UI_PANEL + "_pct_bg");

            CuiHelper.AddUi(player, elements);
        }
        #endregion

        #region Commands
        [ChatCommand("minigun")]
        private void CmdMinigun(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, config.Permission))
            {
                SendReply(player, "<color=#ff4444>You don't have permission to use this.</color>");
                return;
            }

            SendReply(player, "<color=#ffcc00>═══ MiniGunner ═══</color>\n" +
                             $"Hold <color=#44ff44>R</color> while holding a minigun to reload\n" +
                             $"Supports: <color=#44ff44>Regular, Explosive, Incendiary, HV</color> 5.56\n" +
                             $"Reload time: <color=#44ff44>{config.ReloadTime}s</color>\n" +
                             $"Max ammo: <color=#44ff44>{config.MaxAmmo}</color>");
        }

        [ConsoleCommand("minigunner.give")]
        private void CmdGiveMinigun(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin) return;

            var player = arg.Player();
            if (player == null)
            {
                arg.ReplyWith("Must be run as a player");
                return;
            }

            var item = ItemManager.CreateByName(MINIGUN_SHORTNAME, 1);
            if (item != null)
            {
                player.GiveItem(item);
                arg.ReplyWith("Minigun given!");
            }
        }
        #endregion
    }
}
