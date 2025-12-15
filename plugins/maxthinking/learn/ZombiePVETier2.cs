using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("ZombiePVETier2", "ZombiePVE", "1.0.0")]
    [Description("Zombie apocalypse - Acid Rain storms that damage anything exposed")]
    public class ZombiePVETier2 : RustPlugin
    {
        #region Fields
        private Configuration config;
        
        [PluginReference] private Plugin ZombiePVECore, ZombiePVETier1;
        
        // Tree fall sound effect
        private const string TREE_FALL_SOUND = "assets/bundled/prefabs/fx/impacts/physics/phys-impact-wood-hard.prefab";
        
        // Storm tracking
        private bool isStormActive = false;
        private bool isStormBuilding = false;
        private float stormIntensity = 0f; // 0-1, builds up slowly
        private Timer stormTimer;
        private Timer stormScheduleTimer;
        private Timer buildupTimer;
        private Timer damageTimer;
        
        // Constants
        private const string PERM_ADMIN = "zombiepvetier2.admin";
        private const string STORM_UI = "ZombieTier2_Storm";
        #endregion

        #region Configuration
        private class Configuration
        {
            [JsonProperty("1. Acid Rain Settings")]
            public AcidRainSettings AcidRain { get; set; } = new AcidRainSettings();
            
            [JsonProperty("2. Damage Settings")]
            public DamageSettings Damage { get; set; } = new DamageSettings();
            
            [JsonProperty("3. Loot Settings")]
            public LootSettings Loot { get; set; } = new LootSettings();
            
            [JsonProperty("4. General Settings")]
            public GeneralSettings General { get; set; } = new GeneralSettings();
        }
        
        private class AcidRainSettings
        {
            [JsonProperty("Enable acid rain system")]
            public bool Enabled { get; set; } = true;
            
            [JsonProperty("Minimum hours between storms")]
            public float MinHoursBetween { get; set; } = 2f;
            
            [JsonProperty("Maximum hours between storms")]
            public float MaxHoursBetween { get; set; } = 6f;
            
            [JsonProperty("Storm buildup time (seconds) - how long to reach full intensity")]
            public float BuildupSeconds { get; set; } = 180f; // 3 minutes
            
            [JsonProperty("Storm duration at full intensity (seconds)")]
            public float FullIntensityDuration { get; set; } = 600f; // 10 minutes
            
            [JsonProperty("Storm fadeout time (seconds)")]
            public float FadeoutSeconds { get; set; } = 120f; // 2 minutes
            
            [JsonProperty("Announce storm warning")]
            public bool AnnounceWarning { get; set; } = true;
            
            [JsonProperty("Announce storm start")]
            public bool AnnounceStart { get; set; } = true;
            
            [JsonProperty("Announce storm end")]
            public bool AnnounceEnd { get; set; } = true;
            
            [JsonProperty("Warning message")]
            public string WarningMessage { get; set; } = "<color=#aaff00>[!] WARNING: Toxic clouds detected on the horizon...</color>";
            
            [JsonProperty("Storm starting message")]
            public string StartMessage { get; set; } = "<color=#88ff00>[ACID RAIN] The sky turns green... SEEK SHELTER IMMEDIATELY!</color>";
            
            [JsonProperty("Full intensity message")]
            public string FullIntensityMessage { get; set; } = "<color=#44ff00>[!] ACID RAIN AT FULL STRENGTH - GET INSIDE NOW!</color>";
            
            [JsonProperty("Storm ending message")]
            public string EndMessage { get; set; } = "<color=#44ff44>[CLEAR] The acid rain is subsiding. It's safe to go outside.</color>";
            
            [JsonProperty("Max fog intensity (0-1)")]
            public float MaxFog { get; set; } = 1f;
            
            [JsonProperty("Max rain intensity (0-1)")]
            public float MaxRain { get; set; } = 1f;
            
            [JsonProperty("Max wind intensity (0-1)")]
            public float MaxWind { get; set; } = 1f;
            
            [JsonProperty("Thunder frequency (0=none, 1=constant)")]
            public float ThunderFrequency { get; set; } = 0.8f;
        }
        
        private class DamageSettings
        {
            [JsonProperty("Enable acid damage")]
            public bool Enabled { get; set; } = true;
            
            [JsonProperty("Damage tick interval (seconds)")]
            public float DamageInterval { get; set; } = 2f;
            
            [JsonProperty("Player damage per tick (at full intensity)")]
            public float PlayerDamage { get; set; } = 2f;
            
            [JsonProperty("NPC/Zombie damage per tick")]
            public float NpcDamage { get; set; } = 8f;
            
            [JsonProperty("Barrel damage per tick")]
            public float BarrelDamage { get; set; } = 15f;
            
            [JsonProperty("Enable building damage")]
            public bool BuildingDamageEnabled { get; set; } = true;
            
            [JsonProperty("Building damage per tick (exposed walls/foundations)")]
            public float BuildingDamage { get; set; } = 0.5f;
            
            [JsonProperty("Enable environmental destruction (trees, ores, animals)")]
            public bool EnvironmentDamageEnabled { get; set; } = true;
            
            [JsonProperty("Animal damage per tick")]
            public float AnimalDamage { get; set; } = 25f;
            
            [JsonProperty("Tree damage per tick")]
            public float TreeDamage { get; set; } = 50f;
            
            [JsonProperty("Ore node damage per tick")]
            public float OreDamage { get; set; } = 40f;
            
            [JsonProperty("Chance to destroy tree per tick (0-100)")]
            public float TreeDestroyChance { get; set; } = 7.5f;
            
            [JsonProperty("Chance to destroy ore per tick (0-100)")]
            public float OreDestroyChance { get; set; } = 8f;
            
            [JsonProperty("Damage scales with storm intensity")]
            public bool ScaleWithIntensity { get; set; } = true;
            
            [JsonProperty("Show damage warning to players")]
            public bool ShowDamageWarning { get; set; } = true;
        }
        
        private class LootSettings
        {
            [JsonProperty("Tier2 zombie loot - better than Tier1")]
            public List<LootItem> ZombieLoot { get; set; } = new List<LootItem>
            {
                new LootItem { Shortname = "scrap", Min = 1, Max = 4, Weight = 35 },
                new LootItem { Shortname = "metal.fragments", Min = 3, Max = 12, Weight = 30 },
                new LootItem { Shortname = "cloth", Min = 2, Max = 5, Weight = 28 },
                new LootItem { Shortname = "lowgradefuel", Min = 2, Max = 5, Weight = 22 },
                new LootItem { Shortname = "bandage", Min = 1, Max = 2, Weight = 18 },
                new LootItem { Shortname = "bone.fragments", Min = 3, Max = 8, Weight = 22 },
                new LootItem { Shortname = "syringe.medical", Min = 1, Max = 1, Weight = 5 },
                new LootItem { Shortname = "ammo.pistol", Min = 2, Max = 5, Weight = 7 },
                new LootItem { Shortname = "ammo.shotgun", Min = 1, Max = 3, Weight = 6 },
                new LootItem { Shortname = "ammo.rifle", Min = 1, Max = 2, Weight = 4 }
            };
        }
        
        private class LootItem
        {
            public string Shortname { get; set; }
            public int Min { get; set; }
            public int Max { get; set; }
            public int Weight { get; set; }
        }
        
        private class GeneralSettings
        {
            [JsonProperty("Enable debug logging")]
            public bool Debug { get; set; } = false;
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
            permission.RegisterPermission(PERM_ADMIN, this);
            
            // Start storm scheduler
            if (config.AcidRain.Enabled)
                ScheduleNextStorm();
            
            PrintStartupMessage();
        }

        private void Unload()
        {
            // End any active storm immediately
            if (isStormActive || isStormBuilding)
                ForceEndStorm();
            
            // Kill all timers
            stormTimer?.Destroy();
            stormScheduleTimer?.Destroy();
            buildupTimer?.Destroy();
            damageTimer?.Destroy();
            
            // Clean up UI
            foreach (var player in BasePlayer.activePlayerList)
                CuiHelper.DestroyUi(player, STORM_UI);
        }
        
        private void OnPlayerConnected(BasePlayer player)
        {
            if (isStormActive || isStormBuilding)
            {
                timer.Once(1f, () =>
                {
                    if (player != null && player.IsConnected)
                        UpdateStormUI(player);
                });
            }
        }
        #endregion


        #region Acid Rain System
        private void ScheduleNextStorm()
        {
            stormScheduleTimer?.Destroy();
            
            float hoursUntilStorm = UnityEngine.Random.Range(config.AcidRain.MinHoursBetween, config.AcidRain.MaxHoursBetween);
            float secondsUntilStorm = hoursUntilStorm * 3600f;
            
            DebugLog($"Next acid rain scheduled in {hoursUntilStorm:F1} hours");
            
            stormScheduleTimer = timer.Once(secondsUntilStorm, () =>
            {
                // Send warning first
                if (config.AcidRain.AnnounceWarning)
                    PrintToChat(config.AcidRain.WarningMessage);
                
                // Start buildup after 30 seconds
                timer.Once(30f, StartStormBuildup);
            });
        }
        
        private void StartStormBuildup()
        {
            if (isStormActive || isStormBuilding) return;
            
            // Check if blood moon is active - can't have both
            if (IsBloodMoonActive())
            {
                DebugLog("Cannot start acid rain - Blood Moon is active");
                ScheduleNextStorm(); // Reschedule
                return;
            }
            
            isStormBuilding = true;
            stormIntensity = 0f;
            
            if (config.AcidRain.AnnounceStart)
                PrintToChat(config.AcidRain.StartMessage);
            
            // Show UI to all players
            foreach (var player in BasePlayer.activePlayerList)
                UpdateStormUI(player);
            
            // Start damage timer
            if (config.Damage.Enabled)
            {
                damageTimer?.Destroy();
                damageTimer = timer.Every(config.Damage.DamageInterval, ApplyAcidDamage);
            }
            
            // Gradually increase intensity over buildup time
            float tickInterval = 2f; // Update every 2 seconds
            float intensityPerTick = tickInterval / config.AcidRain.BuildupSeconds;
            
            buildupTimer?.Destroy();
            buildupTimer = timer.Every(tickInterval, () =>
            {
                if (!isStormBuilding) return;
                
                stormIntensity += intensityPerTick;
                
                if (stormIntensity >= 1f)
                {
                    stormIntensity = 1f;
                    isStormBuilding = false;
                    isStormActive = true;
                    buildupTimer?.Destroy();
                    
                    if (config.AcidRain.AnnounceStart)
                        PrintToChat(config.AcidRain.FullIntensityMessage);
                    
                    // Schedule end of full intensity
                    stormTimer?.Destroy();
                    stormTimer = timer.Once(config.AcidRain.FullIntensityDuration, StartStormFadeout);
                }
                
                // Update weather based on intensity
                UpdateWeather();
                
                // Update UI for all players
                foreach (var player in BasePlayer.activePlayerList)
                    UpdateStormUI(player);
            });
            
            DebugLog("Acid rain buildup started");
        }
        
        private void StartStormFadeout()
        {
            if (!isStormActive) return;
            
            isStormActive = false;
            isStormBuilding = true; // Reuse building flag for fadeout
            
            // Gradually decrease intensity
            float tickInterval = 2f;
            float intensityPerTick = tickInterval / config.AcidRain.FadeoutSeconds;
            
            buildupTimer?.Destroy();
            buildupTimer = timer.Every(tickInterval, () =>
            {
                stormIntensity -= intensityPerTick;
                
                if (stormIntensity <= 0f)
                {
                    stormIntensity = 0f;
                    EndStorm();
                    return;
                }
                
                UpdateWeather();
                
                foreach (var player in BasePlayer.activePlayerList)
                    UpdateStormUI(player);
            });
        }
        
        private void EndStorm()
        {
            isStormActive = false;
            isStormBuilding = false;
            stormIntensity = 0f;
            
            buildupTimer?.Destroy();
            damageTimer?.Destroy();
            stormTimer?.Destroy();
            
            if (config.AcidRain.AnnounceEnd)
                PrintToChat(config.AcidRain.EndMessage);
            
            // Start aftermath - dark fog lingers
            StartAftermath();
            
            // Hide UI
            foreach (var player in BasePlayer.activePlayerList)
                CuiHelper.DestroyUi(player, STORM_UI);
            
            // Schedule next storm
            if (config.AcidRain.Enabled)
                ScheduleNextStorm();
            
            DebugLog("Acid rain ended");
        }
        
        private Timer aftermathTimer;
        private void StartAftermath()
        {
            // Set dark/black fog for aftermath
            ConsoleSystem.Run(ConsoleSystem.Option.Server, "weather.fog", "0.7");
            ConsoleSystem.Run(ConsoleSystem.Option.Server, "weather.rain", "0");
            ConsoleSystem.Run(ConsoleSystem.Option.Server, "weather.wind", "0.2");
            ConsoleSystem.Run(ConsoleSystem.Option.Server, "weather.thunder", "0");
            ConsoleSystem.Run(ConsoleSystem.Option.Server, "weather.atmosphere_brightness", "0.4");
            
            PrintToChat("<color=#666666>[AFTERMATH] A dark haze lingers over the land...</color>");
            
            // Gradually clear over 2-3 minutes
            float aftermathDuration = 150f; // 2.5 minutes
            float tickInterval = 10f;
            float fogLevel = 0.7f;
            
            aftermathTimer?.Destroy();
            aftermathTimer = timer.Every(tickInterval, () =>
            {
                fogLevel -= tickInterval / aftermathDuration * 0.7f;
                
                if (fogLevel <= 0)
                {
                    aftermathTimer?.Destroy();
                    RestoreWeather();
                    PrintToChat("<color=#88aa88>[CLEAR] The air has finally cleared.</color>");
                    return;
                }
                
                float brightness = 0.4f + (1f - fogLevel / 0.7f) * 0.6f;
                ConsoleSystem.Run(ConsoleSystem.Option.Server, "weather.fog", fogLevel.ToString("F2"));
                ConsoleSystem.Run(ConsoleSystem.Option.Server, "weather.atmosphere_brightness", brightness.ToString("F2"));
            });
        }
        
        private void ForceEndStorm()
        {
            isStormActive = false;
            isStormBuilding = false;
            stormIntensity = 0f;
            
            buildupTimer?.Destroy();
            damageTimer?.Destroy();
            stormTimer?.Destroy();
            
            RestoreWeather();
            
            foreach (var player in BasePlayer.activePlayerList)
                CuiHelper.DestroyUi(player, STORM_UI);
        }
        
        private void UpdateWeather()
        {
            float fog = config.AcidRain.MaxFog * stormIntensity;
            float rain = config.AcidRain.MaxRain * stormIntensity;
            float wind = config.AcidRain.MaxWind * stormIntensity;
            float thunder = config.AcidRain.ThunderFrequency * stormIntensity;
            
            // Set all weather to maximum hurricane levels
            ConsoleSystem.Run(ConsoleSystem.Option.Server, "weather.fog", fog.ToString("F2"));
            ConsoleSystem.Run(ConsoleSystem.Option.Server, "weather.rain", rain.ToString("F2"));
            ConsoleSystem.Run(ConsoleSystem.Option.Server, "weather.wind", wind.ToString("F2"));
            ConsoleSystem.Run(ConsoleSystem.Option.Server, "weather.thunder", thunder.ToString("F2"));
            
            // Also set atmosphere for darker sky
            if (stormIntensity > 0.5f)
            {
                ConsoleSystem.Run(ConsoleSystem.Option.Server, "weather.atmosphere_contrast", "1.5");
                ConsoleSystem.Run(ConsoleSystem.Option.Server, "weather.atmosphere_brightness", "0.3");
            }
        }
        
        private void RestoreWeather()
        {
            ConsoleSystem.Run(ConsoleSystem.Option.Server, "weather.fog", "0");
            ConsoleSystem.Run(ConsoleSystem.Option.Server, "weather.rain", "0");
            ConsoleSystem.Run(ConsoleSystem.Option.Server, "weather.wind", "0");
            ConsoleSystem.Run(ConsoleSystem.Option.Server, "weather.thunder", "0");
            ConsoleSystem.Run(ConsoleSystem.Option.Server, "weather.atmosphere_contrast", "1");
            ConsoleSystem.Run(ConsoleSystem.Option.Server, "weather.atmosphere_brightness", "1");
        }
        #endregion

        #region Acid Damage
        private void ApplyAcidDamage()
        {
            if (stormIntensity <= 0) return;
            
            float damageMultiplier = config.Damage.ScaleWithIntensity ? stormIntensity : 1f;
            
            // Damage players outside
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player == null || player.IsDead()) continue;
                if (IsPlayerSheltered(player)) continue;
                
                float damage = config.Damage.PlayerDamage * damageMultiplier;
                player.Hurt(damage, Rust.DamageType.Poison, null, true);
                
                if (config.Damage.ShowDamageWarning && UnityEngine.Random.Range(0, 5) == 0)
                    SendReply(player, "<color=#88ff00>[!] The acid rain burns! Find shelter!</color>");
            }
            
            // Process all entities for damage
            var entitiesToKill = new List<BaseNetworkable>();
            
            foreach (var entity in BaseNetworkable.serverEntities)
            {
                if (entity == null) continue;
                
                var baseCombat = entity as BaseCombatEntity;
                string prefabName = entity.ShortPrefabName ?? "";
                
                // Damage animals
                if (config.Damage.EnvironmentDamageEnabled && entity is BaseAnimalNPC)
                {
                    var animal = entity as BaseAnimalNPC;
                    if (animal != null && !animal.IsDead())
                    {
                        float damage = config.Damage.AnimalDamage * damageMultiplier;
                        animal.Hurt(damage, Rust.DamageType.Poison);
                    }
                    continue;
                }
                
                // Damage/destroy trees
                if (config.Damage.EnvironmentDamageEnabled && entity is TreeEntity)
                {
                    var tree = entity as TreeEntity;
                    if (tree != null)
                    {
                        // Random chance to destroy tree outright (50% higher chance)
                        if (UnityEngine.Random.Range(0f, 100f) < config.Damage.TreeDestroyChance * damageMultiplier)
                        {
                            // Play tree falling sound for nearby players
                            PlayTreeFallSound(tree.transform.position);
                            entitiesToKill.Add(tree);
                        }
                    }
                    continue;
                }
                
                // Damage/destroy ore nodes
                if (config.Damage.EnvironmentDamageEnabled && entity is OreResourceEntity)
                {
                    var ore = entity as OreResourceEntity;
                    if (ore != null)
                    {
                        if (UnityEngine.Random.Range(0f, 100f) < config.Damage.OreDestroyChance * damageMultiplier)
                        {
                            entitiesToKill.Add(ore);
                        }
                    }
                    continue;
                }
                
                // Skip if no combat component for remaining checks
                if (baseCombat == null) continue;
                
                // Damage zombies/NPCs
                if (prefabName.Contains("murderer") || prefabName.Contains("scarecrow") || 
                    prefabName.Contains("scientist") || entity is ScarecrowNPC)
                {
                    if (!IsPositionSheltered(baseCombat.transform.position))
                    {
                        float damage = config.Damage.NpcDamage * damageMultiplier;
                        baseCombat.Hurt(damage, Rust.DamageType.Poison);
                    }
                    continue;
                }
                
                // Damage barrels
                if (entity is LootContainer && prefabName.Contains("barrel"))
                {
                    if (!IsPositionSheltered(baseCombat.transform.position))
                    {
                        float damage = config.Damage.BarrelDamage * damageMultiplier;
                        baseCombat.Hurt(damage, Rust.DamageType.Decay);
                    }
                    continue;
                }
                
                // Damage exposed building parts
                if (config.Damage.BuildingDamageEnabled && entity is BuildingBlock)
                {
                    var block = entity as BuildingBlock;
                    if (!IsPositionSheltered(block.transform.position + Vector3.up * 1.5f))
                    {
                        float damage = config.Damage.BuildingDamage * damageMultiplier;
                        block.Hurt(damage, Rust.DamageType.Decay);
                    }
                }
            }
            
            // Kill entities marked for destruction (trees, ores)
            foreach (var entity in entitiesToKill)
            {
                if (entity != null && !entity.IsDestroyed)
                {
                    entity.Kill();
                }
            }
        }
        
        private bool IsPlayerSheltered(BasePlayer player)
        {
            // Check if there's a roof above the player
            return IsPositionSheltered(player.transform.position);
        }
        
        private bool IsPositionSheltered(Vector3 position)
        {
            // Raycast upward to check for cover
            RaycastHit hit;
            if (Physics.Raycast(position + Vector3.up * 0.5f, Vector3.up, out hit, 50f, LayerMask.GetMask("Construction", "Deployed", "World")))
            {
                return true; // Something above, sheltered
            }
            return false;
        }
        #endregion


        #region UI
        private void UpdateStormUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, STORM_UI);
            
            if (stormIntensity <= 0) return;
            
            var elements = new CuiElementContainer();
            
            // Sleek dark panel with toxic green accent
            string panelColor = "0.05 0.05 0.05 0.9";
            string accentColor;
            string statusText;
            string iconText;
            
            if (stormIntensity < 0.3f)
            {
                accentColor = "0.4 0.6 0.2 1";
                statusText = "TOXIC STORM APPROACHING";
                iconText = "/!\\";
            }
            else if (stormIntensity < 0.7f)
            {
                accentColor = "0.6 0.8 0.1 1";
                statusText = "ACID RAIN - SEEK SHELTER";
                iconText = "/!\\";
            }
            else
            {
                accentColor = "0.7 1 0 1";
                statusText = "MAXIMUM TOXICITY";
                iconText = "[X]";
            }
            
            // Main container - sleek horizontal bar
            elements.Add(new CuiPanel
            {
                Image = { Color = panelColor },
                RectTransform = { AnchorMin = "0.3 0.94", AnchorMax = "0.7 0.98" },
                CursorEnabled = false
            }, "Overlay", STORM_UI);
            
            // Left accent bar (toxic green)
            elements.Add(new CuiPanel
            {
                Image = { Color = accentColor },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "0.008 1" }
            }, STORM_UI);
            
            // Icon on left
            elements.Add(new CuiLabel
            {
                Text = { Text = iconText, FontSize = 12, Align = TextAnchor.MiddleCenter, Color = accentColor, Font = "robotocondensed-bold.ttf" },
                RectTransform = { AnchorMin = "0.02 0", AnchorMax = "0.1 1" }
            }, STORM_UI);
            
            // Status text
            elements.Add(new CuiLabel
            {
                Text = { Text = statusText, FontSize = 13, Align = TextAnchor.MiddleLeft, Color = "0.9 0.9 0.9 1", Font = "robotocondensed-regular.ttf" },
                RectTransform = { AnchorMin = "0.11 0", AnchorMax = "0.65 1" }
            }, STORM_UI);
            
            // Intensity percentage on right
            elements.Add(new CuiLabel
            {
                Text = { Text = $"{(stormIntensity * 100):F0}%", FontSize = 14, Align = TextAnchor.MiddleRight, Color = accentColor, Font = "robotocondensed-bold.ttf" },
                RectTransform = { AnchorMin = "0.85 0", AnchorMax = "0.98 1" }
            }, STORM_UI);
            
            // Progress bar background
            elements.Add(new CuiPanel
            {
                Image = { Color = "0.15 0.15 0.15 1" },
                RectTransform = { AnchorMin = "0.66 0.35", AnchorMax = "0.84 0.65" }
            }, STORM_UI, STORM_UI + "_barbg");
            
            // Progress bar fill
            float barWidth = 0.66f + (0.18f * stormIntensity);
            elements.Add(new CuiPanel
            {
                Image = { Color = accentColor },
                RectTransform = { AnchorMin = "0.66 0.35", AnchorMax = $"{barWidth:F3} 0.65" }
            }, STORM_UI, STORM_UI + "_bar");
            
            CuiHelper.AddUi(player, elements);
        }
        #endregion

        #region Loot Hook
        public void OnZombieLootDrop(Vector3 position, BasePlayer killer)
        {
            if (killer == null) return;
            
            int totalWeight = 0;
            foreach (var entry in config.Loot.ZombieLoot)
                totalWeight += entry.Weight;
            
            int roll = UnityEngine.Random.Range(0, totalWeight);
            int cumulative = 0;
            
            foreach (var loot in config.Loot.ZombieLoot)
            {
                cumulative += loot.Weight;
                if (roll < cumulative)
                {
                    var item = ItemManager.CreateByName(loot.Shortname, UnityEngine.Random.Range(loot.Min, loot.Max + 1));
                    if (item != null)
                    {
                        Vector3 dropPos = position + Vector3.up * 0.5f;
                        item.Drop(dropPos, Vector3.up * 2f + UnityEngine.Random.insideUnitSphere * 0.3f);
                    }
                    break;
                }
            }
        }
        #endregion

        #region Commands
        [ChatCommand("storm")]
        private void CmdStorm(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin && !permission.UserHasPermission(player.UserIDString, PERM_ADMIN))
            {
                SendReply(player, "<color=#ff4444>Admin only.</color>");
                return;
            }
            
            if (args.Length == 0)
            {
                string status;
                if (isStormActive) status = "<color=#88ff00>FULL INTENSITY</color>";
                else if (isStormBuilding) status = "<color=#aaff00>BUILDING/FADING</color>";
                else status = "<color=#44ff44>Inactive</color>";
                
                SendReply(player, 
                    $"<color=#88ff00>=== ACID RAIN SYSTEM ===</color>\n" +
                    $"Status: {status}\n" +
                    $"Intensity: <color=#88ff00>{(stormIntensity * 100):F0}%</color>\n\n" +
                    $"<color=#888888>/storm start</color> - Start acid rain\n" +
                    $"<color=#888888>/storm stop</color> - End acid rain\n" +
                    $"<color=#888888>/storm intensity <0-100></color> - Set intensity %");
                return;
            }
            
            switch (args[0].ToLower())
            {
                case "start":
                    if (isStormActive || isStormBuilding)
                    {
                        SendReply(player, "<color=#ff4444>Acid rain is already active.</color>");
                        return;
                    }
                    stormScheduleTimer?.Destroy();
                    StartStormBuildup();
                    SendReply(player, "<color=#88ff00>Acid rain starting... buildup will take 3 minutes.</color>");
                    break;
                    
                case "stop":
                case "end":
                    if (!isStormActive && !isStormBuilding)
                    {
                        SendReply(player, "<color=#ff4444>No acid rain is active.</color>");
                        return;
                    }
                    ForceEndStorm();
                    if (config.AcidRain.Enabled)
                        ScheduleNextStorm();
                    SendReply(player, "<color=#44ff44>Acid rain stopped!</color>");
                    break;
                    
                case "intensity":
                    if (args.Length > 1 && float.TryParse(args[1], out float pct))
                    {
                        pct = Mathf.Clamp(pct, 0f, 100f);
                        stormIntensity = pct / 100f;
                        
                        if (stormIntensity > 0)
                        {
                            isStormBuilding = false;
                            isStormActive = true;
                            buildupTimer?.Destroy();
                            
                            if (config.Damage.Enabled && damageTimer == null)
                                damageTimer = timer.Every(config.Damage.DamageInterval, ApplyAcidDamage);
                        }
                        
                        UpdateWeather();
                        foreach (var p in BasePlayer.activePlayerList)
                            UpdateStormUI(p);
                        
                        SendReply(player, $"<color=#88ff00>Acid rain intensity set to {pct:F0}%</color>");
                    }
                    else
                    {
                        SendReply(player, "<color=#ff4444>Usage: /storm intensity <0-100></color>");
                    }
                    break;
                    
                case "reset":
                    ForceEndStorm();
                    if (config.AcidRain.Enabled)
                        ScheduleNextStorm();
                    SendReply(player, "<color=#44ff44>Acid rain system reset.</color>");
                    break;
                    
                default:
                    SendReply(player, "<color=#ff4444>Unknown command. Use /storm for help.</color>");
                    break;
            }
        }
        
        [ChatCommand("zt2help")]
        private void CmdHelp(BasePlayer player, string command, string[] args)
        {
            string msg = "<color=#88ff00>=== ZOMBIE PVE TIER 2 - ACID RAIN ===</color>\n\n" +
                "<color=#aaff00>Acid Rain System:</color>\n" +
                "- Toxic storms occur every 2-6 hours\n" +
                "- Storms build slowly over 3 minutes\n" +
                "- Acid rain RESHAPES THE MAP!\n" +
                "- Kills animals, destroys trees and ore nodes\n" +
                "- Damages players, zombies, buildings\n" +
                "- GET INSIDE when you see the warning!\n";
            
            if (player.IsAdmin || permission.UserHasPermission(player.UserIDString, PERM_ADMIN))
            {
                msg += "\n<color=#ffcc00>Admin Commands:</color>\n" +
                    "<color=#ff8800>/storm</color> - Acid rain status\n" +
                    "<color=#ff8800>/storm start</color> - Start acid rain\n" +
                    "<color=#ff8800>/storm stop</color> - End acid rain\n" +
                    "<color=#ff8800>/storm intensity <0-100></color> - Set intensity\n";
            }
            
            SendReply(player, msg);
        }
        #endregion

        #region Helpers
        private void DebugLog(string message)
        {
            if (config.General.Debug)
                Puts($"[Debug] {message}");
        }
        
        private void PrintStartupMessage()
        {
            Puts("===========================================");
            Puts("  ZOMBIE PVE TIER 2 v1.0 - ACID RAIN");
            Puts("===========================================");
            if (config.AcidRain.Enabled) Puts("  [+] Acid Rain System");
            if (config.Damage.Enabled) Puts($"  [+] Acid Damage ({config.Damage.PlayerDamage}/tick to players)");
            if (config.Damage.EnvironmentDamageEnabled) Puts("  [+] Environmental Destruction (trees/ores/animals)");
            Puts("===========================================");
        }
        
        private void PlayTreeFallSound(Vector3 position)
        {
            // Play tree falling/crashing sound for nearby players
            Effect.server.Run("assets/bundled/prefabs/fx/impacts/physics/phys-impact-wood-hard.prefab", position);
            
            // Also play a louder crash sound
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player == null) continue;
                float dist = Vector3.Distance(player.transform.position, position);
                if (dist < 100f)
                {
                    // Send effect to player
                    Effect.server.Run("assets/bundled/prefabs/fx/building/wood_gib.prefab", position);
                }
            }
        }
        
        private bool IsBloodMoonActive()
        {
            // Check ZombiePVECore for blood moon status
            if (ZombiePVECore == null) return false;
            
            var result = ZombiePVECore.Call("IsBloodMoonActive");
            if (result is bool)
                return (bool)result;
            
            return false;
        }
        
        // Hook for ZombiePVECore to check if acid rain is active
        private bool IsAcidRainActive()
        {
            return isStormActive || isStormBuilding;
        }
        #endregion
    }
}
