using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("ZombiePVESystem", "ZombiePVE", "1.0.0")]
    [Description("Advanced PVE zombie system with waves, variants, missions, and more")]
    public class ZombiePVESystem : RustPlugin
    {
        #region Fields
        private Configuration config;
        private StoredData storedData;
        
        // Active tracking
        private Dictionary<ulong, ZombieData> activeZombies = new Dictionary<ulong, ZombieData>();
        private Dictionary<ulong, PlayerMission> activeMissions = new Dictionary<ulong, PlayerMission>();
        private Dictionary<ulong, BaseDefenseEvent> activeDefenseEvents = new Dictionary<ulong, BaseDefenseEvent>();
        
        // Wave system
        private bool waveActive = false;
        private int currentWave = 0;
        private Timer waveTimer;
        private List<ulong> waveParticipants = new List<ulong>();
        
        // Timers
        private Timer dayNightTimer;
        private Timer missionTimer;
        private Timer defenseTimer;
        
        // Constants
        private const string PERM_ADMIN = "zombiepvesystem.admin";
        private const string PERM_VIP = "zombiepvesystem.vip";
        private const string WAVE_UI = "ZombiePVE_Wave";
        private const string MISSION_UI = "ZombiePVE_Mission";
        
        // Zombie prefab
        private const string ZOMBIE_PREFAB = "assets/prefabs/npc/scarecrow/scarecrow.prefab";
        
        [PluginReference] private Plugin ZombiePVECore;
        #endregion

        #region Classes
        private class ZombieData
        {
            public ZombieType Type;
            public float SpawnTime;
            public Vector3 SpawnPosition;
            public ulong TargetPlayer;
            public bool IsBoss;
        }
        
        private class PlayerMission
        {
            public MissionType Type;
            public int Target;
            public int Progress;
            public float StartTime;
            public float TimeLimit;
            public int RewardScrap;
            public string Description;
        }
        
        private class BaseDefenseEvent
        {
            public Vector3 Position;
            public float StartTime;
            public int WavesRemaining;
            public int ZombiesRemaining;
            public List<ulong> Defenders;
        }
        
        private class PlayerStats
        {
            public int TotalKills;
            public int BossKills;
            public int WavesSurvived;
            public int MissionsCompleted;
            public int BasesDefended;
            public int Deaths;
            public string Title;
        }
        
        public enum ZombieType
        {
            Normal,
            Crawler,    // Fast, low HP
            Tank,       // Slow, high HP
            Spitter,    // Ranged
            Screamer,   // Alerts others
            Exploder    // Explodes on death
        }
        
        public enum MissionType
        {
            KillZombies,
            KillBoss,
            SurviveWave,
            DefendBase,
            KillVariant
        }
        #endregion

        #region Configuration
        private class Configuration
        {
            [JsonProperty("1. Zombie Variants")]
            public ZombieVariantSettings Variants { get; set; } = new ZombieVariantSettings();
            
            [JsonProperty("2. Wave System")]
            public WaveSettings Waves { get; set; } = new WaveSettings();
            
            [JsonProperty("3. Day/Night Cycle")]
            public DayNightSettings DayNight { get; set; } = new DayNightSettings();
            
            [JsonProperty("4. Mission System")]
            public MissionSettings Missions { get; set; } = new MissionSettings();
            
            [JsonProperty("5. Base Defense")]
            public BaseDefenseSettings BaseDefense { get; set; } = new BaseDefenseSettings();
            
            [JsonProperty("6. Safe Zones")]
            public SafeZoneSettings SafeZones { get; set; } = new SafeZoneSettings();
            
            [JsonProperty("7. Loot System")]
            public LootSettings Loot { get; set; } = new LootSettings();
            
            [JsonProperty("8. Reputation System")]
            public ReputationSettings Reputation { get; set; } = new ReputationSettings();
            
            [JsonProperty("9. Monument Zombies")]
            public MonumentSettings Monuments { get; set; } = new MonumentSettings();
        }
        
        private class ZombieVariantSettings
        {
            [JsonProperty("Enable zombie variants")]
            public bool Enabled { get; set; } = true;
            
            [JsonProperty("Crawler spawn chance (0-100)")]
            public int CrawlerChance { get; set; } = 15;
            
            [JsonProperty("Tank spawn chance (0-100)")]
            public int TankChance { get; set; } = 10;
            
            [JsonProperty("Spitter spawn chance (0-100)")]
            public int SpitterChance { get; set; } = 8;
            
            [JsonProperty("Screamer spawn chance (0-100)")]
            public int ScreamerChance { get; set; } = 5;
            
            [JsonProperty("Exploder spawn chance (0-100)")]
            public int ExploderChance { get; set; } = 5;
            
            [JsonProperty("Crawler - Speed multiplier")]
            public float CrawlerSpeed { get; set; } = 1.8f;
            
            [JsonProperty("Crawler - Health multiplier")]
            public float CrawlerHealth { get; set; } = 0.5f;
            
            [JsonProperty("Tank - Speed multiplier")]
            public float TankSpeed { get; set; } = 0.5f;
            
            [JsonProperty("Tank - Health multiplier")]
            public float TankHealth { get; set; } = 4.0f;
            
            [JsonProperty("Tank - Damage multiplier")]
            public float TankDamage { get; set; } = 2.0f;
            
            [JsonProperty("Exploder - Explosion radius")]
            public float ExploderRadius { get; set; } = 5f;
            
            [JsonProperty("Exploder - Explosion damage")]
            public float ExploderDamage { get; set; } = 50f;
            
            [JsonProperty("Screamer - Alert radius")]
            public float ScreamerAlertRadius { get; set; } = 50f;
        }
        
        private class WaveSettings
        {
            [JsonProperty("Enable wave system")]
            public bool Enabled { get; set; } = true;
            
            [JsonProperty("Total waves per event")]
            public int TotalWaves { get; set; } = 10;
            
            [JsonProperty("Base zombies per wave")]
            public int BaseZombiesPerWave { get; set; } = 5;
            
            [JsonProperty("Extra zombies per wave (scales with wave number)")]
            public int ExtraZombiesPerWave { get; set; } = 2;
            
            [JsonProperty("Time between waves (seconds)")]
            public float TimeBetweenWaves { get; set; } = 30f;
            
            [JsonProperty("Wave spawn radius around player")]
            public float SpawnRadius { get; set; } = 50f;
            
            [JsonProperty("Scrap reward per wave survived")]
            public int ScrapPerWave { get; set; } = 25;
            
            [JsonProperty("Bonus scrap for completing all waves")]
            public int CompletionBonus { get; set; } = 500;
            
            [JsonProperty("Boss spawns on wave (0 = disabled)")]
            public int BossWave { get; set; } = 5;
            
            [JsonProperty("Final boss on last wave")]
            public bool FinalBoss { get; set; } = true;
            
            [JsonProperty("Command to start waves")]
            public string StartCommand { get; set; } = "waves";
        }
        
        private class DayNightSettings
        {
            [JsonProperty("Enable day/night zombie changes")]
            public bool Enabled { get; set; } = true;
            
            [JsonProperty("Night zombie speed multiplier")]
            public float NightSpeedMultiplier { get; set; } = 1.5f;
            
            [JsonProperty("Night zombie damage multiplier")]
            public float NightDamageMultiplier { get; set; } = 1.5f;
            
            [JsonProperty("Night spawn rate multiplier")]
            public float NightSpawnMultiplier { get; set; } = 2.0f;
            
            [JsonProperty("Night loot drop multiplier")]
            public float NightLootMultiplier { get; set; } = 1.5f;
            
            [JsonProperty("Day zombie speed multiplier")]
            public float DaySpeedMultiplier { get; set; } = 0.8f;
            
            [JsonProperty("Day zombie damage multiplier")]
            public float DayDamageMultiplier { get; set; } = 0.8f;
            
            [JsonProperty("Announce day/night changes")]
            public bool AnnounceChanges { get; set; } = true;
            
            [JsonProperty("Night start message")]
            public string NightMessage { get; set; } = "<color=#ff4444>[NIGHT] Night falls... The zombies grow stronger!</color>";
            
            [JsonProperty("Day start message")]
            public string DayMessage { get; set; } = "<color=#44ff44>[DAY] Dawn breaks... The zombies weaken.</color>";
        }

        private class MissionSettings
        {
            [JsonProperty("Enable mission system")]
            public bool Enabled { get; set; } = true;
            
            [JsonProperty("Mission refresh interval (seconds)")]
            public float RefreshInterval { get; set; } = 1800f;
            
            [JsonProperty("Max active missions per player")]
            public int MaxActiveMissions { get; set; } = 1;
            
            [JsonProperty("Mission time limit (seconds)")]
            public float TimeLimit { get; set; } = 600f;
            
            [JsonProperty("Kill zombies mission - Target count")]
            public int KillTarget { get; set; } = 20;
            
            [JsonProperty("Kill zombies mission - Scrap reward")]
            public int KillReward { get; set; } = 100;
            
            [JsonProperty("Kill boss mission - Scrap reward")]
            public int BossReward { get; set; } = 250;
            
            [JsonProperty("Kill variant mission - Target count")]
            public int VariantTarget { get; set; } = 5;
            
            [JsonProperty("Kill variant mission - Scrap reward")]
            public int VariantReward { get; set; } = 150;
            
            [JsonProperty("Command to get mission")]
            public string MissionCommand { get; set; } = "mission";
        }
        
        private class BaseDefenseSettings
        {
            [JsonProperty("Enable base defense events")]
            public bool Enabled { get; set; } = true;
            
            [JsonProperty("Random attack chance per hour (0-100)")]
            public int AttackChance { get; set; } = 10;
            
            [JsonProperty("Warning time before attack (seconds)")]
            public float WarningTime { get; set; } = 60f;
            
            [JsonProperty("Waves per defense event")]
            public int DefenseWaves { get; set; } = 3;
            
            [JsonProperty("Zombies per defense wave")]
            public int ZombiesPerWave { get; set; } = 10;
            
            [JsonProperty("Time between defense waves (seconds)")]
            public float WaveInterval { get; set; } = 45f;
            
            [JsonProperty("Defense radius around TC")]
            public float DefenseRadius { get; set; } = 30f;
            
            [JsonProperty("Scrap reward for successful defense")]
            public int DefenseReward { get; set; } = 200;
            
            [JsonProperty("Minimum building grade to trigger (0=twig, 4=hqm)")]
            public int MinBuildingGrade { get; set; } = 1;
            
            [JsonProperty("Warning message")]
            public string WarningMessage { get; set; } = "<color=#ff4444>[!] ZOMBIE HORDE INCOMING! Defend your base in {0} seconds!</color>";
        }
        
        private class SafeZoneSettings
        {
            [JsonProperty("Enable safe zones at night")]
            public bool Enabled { get; set; } = true;
            
            [JsonProperty("Safe zone monuments")]
            public List<string> SafeMonuments { get; set; } = new List<string> { "Outpost", "Bandit Camp" };
            
            [JsonProperty("Safe zone radius")]
            public float SafeRadius { get; set; } = 150f;
            
            [JsonProperty("Announce safe zones at night")]
            public bool AnnounceSafeZones { get; set; } = true;
            
            [JsonProperty("Safe zone message")]
            public string SafeZoneMessage { get; set; } = "<color=#44ff44>Safe zones active at Outpost and Bandit Camp!</color>";
        }
        
        private class LootSettings
        {
            [JsonProperty("Enable tiered zombie loot")]
            public bool Enabled { get; set; } = true;
            
            [JsonProperty("Normal zombie loot chance (0-100)")]
            public int NormalLootChance { get; set; } = 30;
            
            [JsonProperty("Variant zombie loot chance (0-100)")]
            public int VariantLootChance { get; set; } = 50;
            
            [JsonProperty("Boss zombie loot chance (0-100)")]
            public int BossLootChance { get; set; } = 100;
            
            [JsonProperty("Normal zombie loot table")]
            public List<LootItem> NormalLoot { get; set; } = new List<LootItem>
            {
                new LootItem { Shortname = "scrap", MinAmount = 5, MaxAmount = 15 },
                new LootItem { Shortname = "cloth", MinAmount = 10, MaxAmount = 30 },
                new LootItem { Shortname = "metal.fragments", MinAmount = 20, MaxAmount = 50 }
            };
            
            [JsonProperty("Variant zombie loot table")]
            public List<LootItem> VariantLoot { get; set; } = new List<LootItem>
            {
                new LootItem { Shortname = "scrap", MinAmount = 15, MaxAmount = 35 },
                new LootItem { Shortname = "metal.refined", MinAmount = 5, MaxAmount = 15 },
                new LootItem { Shortname = "techparts", MinAmount = 1, MaxAmount = 3 }
            };
            
            [JsonProperty("Boss zombie loot table")]
            public List<LootItem> BossLoot { get; set; } = new List<LootItem>
            {
                new LootItem { Shortname = "scrap", MinAmount = 100, MaxAmount = 250 },
                new LootItem { Shortname = "metal.refined", MinAmount = 25, MaxAmount = 50 },
                new LootItem { Shortname = "rifle.ak", MinAmount = 1, MaxAmount = 1 },
                new LootItem { Shortname = "ammo.rifle", MinAmount = 60, MaxAmount = 120 }
            };
        }
        
        private class LootItem
        {
            public string Shortname { get; set; }
            public int MinAmount { get; set; }
            public int MaxAmount { get; set; }
        }
        
        private class ReputationSettings
        {
            [JsonProperty("Enable reputation system")]
            public bool Enabled { get; set; } = true;
            
            [JsonProperty("Points per zombie kill")]
            public int PointsPerKill { get; set; } = 1;
            
            [JsonProperty("Points per boss kill")]
            public int PointsPerBoss { get; set; } = 25;
            
            [JsonProperty("Points per wave survived")]
            public int PointsPerWave { get; set; } = 10;
            
            [JsonProperty("Points per mission completed")]
            public int PointsPerMission { get; set; } = 50;
            
            [JsonProperty("Points lost on death")]
            public int PointsLostOnDeath { get; set; } = 5;
            
            [JsonProperty("Titles (points required)")]
            public Dictionary<string, int> Titles { get; set; } = new Dictionary<string, int>
            {
                { "Survivor", 0 },
                { "Zombie Hunter", 100 },
                { "Undead Slayer", 500 },
                { "Horde Breaker", 1000 },
                { "Apocalypse Veteran", 2500 },
                { "Zombie Lord", 5000 }
            };
            
            [JsonProperty("Leaderboard command")]
            public string LeaderboardCommand { get; set; } = "ztop";
        }
        
        private class MonumentSettings
        {
            [JsonProperty("Enable monument zombie spawns")]
            public bool Enabled { get; set; } = true;
            
            [JsonProperty("Monument zombie configs")]
            public Dictionary<string, MonumentZombieConfig> Monuments { get; set; } = new Dictionary<string, MonumentZombieConfig>
            {
                { "Airfield", new MonumentZombieConfig { ZombieCount = 15, RespawnTime = 300, BossChance = 10, Difficulty = 3 } },
                { "Launch Site", new MonumentZombieConfig { ZombieCount = 20, RespawnTime = 300, BossChance = 15, Difficulty = 4 } },
                { "Military Tunnel", new MonumentZombieConfig { ZombieCount = 25, RespawnTime = 300, BossChance = 20, Difficulty = 5 } },
                { "Water Treatment", new MonumentZombieConfig { ZombieCount = 12, RespawnTime = 300, BossChance = 8, Difficulty = 3 } },
                { "Train Yard", new MonumentZombieConfig { ZombieCount = 10, RespawnTime = 300, BossChance = 5, Difficulty = 2 } },
                { "Power Plant", new MonumentZombieConfig { ZombieCount = 12, RespawnTime = 300, BossChance = 8, Difficulty = 3 } },
                { "Dome", new MonumentZombieConfig { ZombieCount = 8, RespawnTime = 300, BossChance = 5, Difficulty = 2 } },
                { "Sewer", new MonumentZombieConfig { ZombieCount = 10, RespawnTime = 300, BossChance = 10, Difficulty = 3 } }
            };
        }
        
        private class MonumentZombieConfig
        {
            public int ZombieCount { get; set; }
            public float RespawnTime { get; set; }
            public int BossChance { get; set; }
            public int Difficulty { get; set; } // 1-5, affects variant spawn rates
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

        #region Data
        private class StoredData
        {
            public Dictionary<ulong, PlayerStats> PlayerStats = new Dictionary<ulong, PlayerStats>();
        }
        
        private void LoadData()
        {
            storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>("ZombiePVESystem");
            if (storedData == null) storedData = new StoredData();
        }
        
        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject("ZombiePVESystem", storedData);
        }
        
        private PlayerStats GetPlayerStats(ulong playerId)
        {
            if (!storedData.PlayerStats.ContainsKey(playerId))
                storedData.PlayerStats[playerId] = new PlayerStats { Title = "Survivor" };
            return storedData.PlayerStats[playerId];
        }
        #endregion


        #region Oxide Hooks
        private void OnServerInitialized()
        {
            permission.RegisterPermission(PERM_ADMIN, this);
            permission.RegisterPermission(PERM_VIP, this);
            
            LoadData();
            
            // Register commands
            if (config.Waves.Enabled)
                cmd.AddChatCommand(config.Waves.StartCommand, this, nameof(CmdStartWaves));
            
            if (config.Missions.Enabled)
                cmd.AddChatCommand(config.Missions.MissionCommand, this, nameof(CmdMission));
            
            if (config.Reputation.Enabled)
                cmd.AddChatCommand(config.Reputation.LeaderboardCommand, this, nameof(CmdLeaderboard));
            
            // Start day/night monitoring
            if (config.DayNight.Enabled)
                StartDayNightMonitor();
            
            // Start base defense checks
            if (config.BaseDefense.Enabled)
                StartDefenseMonitor();
            
            Puts("===========================================");
            Puts("  ZOMBIE PVE SYSTEM v1.0 - LOADED!");
            Puts("===========================================");
            if (config.Variants.Enabled) Puts("  [+] Zombie Variants");
            if (config.Waves.Enabled) Puts($"  [+] Wave System (/{config.Waves.StartCommand})");
            if (config.DayNight.Enabled) Puts("  [+] Day/Night Effects");
            if (config.Missions.Enabled) Puts($"  [+] Mission System (/{config.Missions.MissionCommand})");
            if (config.BaseDefense.Enabled) Puts("  [+] Base Defense Events");
            if (config.SafeZones.Enabled) Puts("  [+] Night Safe Zones");
            if (config.Loot.Enabled) Puts("  [+] Tiered Loot System");
            if (config.Reputation.Enabled) Puts($"  [+] Reputation System (/{config.Reputation.LeaderboardCommand})");
            if (config.Monuments.Enabled) Puts("  [+] Monument Zombies");
            Puts("===========================================");
        }

        private void Unload()
        {
            SaveData();
            
            waveTimer?.Destroy();
            dayNightTimer?.Destroy();
            missionTimer?.Destroy();
            defenseTimer?.Destroy();
            
            // Clean up UI
            foreach (var player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, WAVE_UI);
                CuiHelper.DestroyUi(player, MISSION_UI);
            }
            
            // Kill spawned zombies
            foreach (var zombieId in activeZombies.Keys.ToList())
            {
                var zombie = BaseNetworkable.serverEntities.Find(new NetworkableId(zombieId)) as BasePlayer;
                zombie?.Kill();
            }
            activeZombies.Clear();
        }

        private void OnServerSave() => SaveData();

        private void OnPlayerDisconnected(BasePlayer player)
        {
            if (player == null) return;
            
            activeMissions.Remove(player.userID);
            waveParticipants.Remove(player.userID);
            
            CuiHelper.DestroyUi(player, WAVE_UI);
            CuiHelper.DestroyUi(player, MISSION_UI);
        }
        #endregion

        #region Day/Night System
        private bool isNight = false;
        
        private void StartDayNightMonitor()
        {
            dayNightTimer?.Destroy();
            dayNightTimer = timer.Every(30f, CheckDayNight);
            CheckDayNight();
        }
        
        private void CheckDayNight()
        {
            bool wasNight = isNight;
            isNight = IsNightTime();
            
            if (wasNight != isNight && config.DayNight.AnnounceChanges)
            {
                string msg = isNight ? config.DayNight.NightMessage : config.DayNight.DayMessage;
                PrintToChat(msg);
                
                if (isNight && config.SafeZones.Enabled && config.SafeZones.AnnounceSafeZones)
                {
                    timer.Once(3f, () => PrintToChat(config.SafeZones.SafeZoneMessage));
                }
            }
        }
        
        private bool IsNightTime()
        {
            var time = TOD_Sky.Instance?.Cycle?.Hour ?? 12f;
            return time < 6f || time > 18f;
        }
        
        private float GetSpeedMultiplier()
        {
            if (!config.DayNight.Enabled) return 1f;
            return isNight ? config.DayNight.NightSpeedMultiplier : config.DayNight.DaySpeedMultiplier;
        }
        
        private float GetDamageMultiplier()
        {
            if (!config.DayNight.Enabled) return 1f;
            return isNight ? config.DayNight.NightDamageMultiplier : config.DayNight.DayDamageMultiplier;
        }
        #endregion

        #region Zombie Variants
        private ZombieType GetRandomZombieType()
        {
            if (!config.Variants.Enabled) return ZombieType.Normal;
            
            int roll = UnityEngine.Random.Range(0, 100);
            int cumulative = 0;
            
            cumulative += config.Variants.CrawlerChance;
            if (roll < cumulative) return ZombieType.Crawler;
            
            cumulative += config.Variants.TankChance;
            if (roll < cumulative) return ZombieType.Tank;
            
            cumulative += config.Variants.SpitterChance;
            if (roll < cumulative) return ZombieType.Spitter;
            
            cumulative += config.Variants.ScreamerChance;
            if (roll < cumulative) return ZombieType.Screamer;
            
            cumulative += config.Variants.ExploderChance;
            if (roll < cumulative) return ZombieType.Exploder;
            
            return ZombieType.Normal;
        }
        
        private ScarecrowNPC SpawnZombie(Vector3 position, ZombieType type, bool isBoss = false)
        {
            var zombie = GameManager.server.CreateEntity(ZOMBIE_PREFAB, position, Quaternion.identity) as ScarecrowNPC;
            if (zombie == null) return null;
            
            zombie.Spawn();
            
            if (zombie.net == null) return null;
            
            // Track zombie
            activeZombies[zombie.net.ID.Value] = new ZombieData
            {
                Type = type,
                SpawnTime = Time.realtimeSinceStartup,
                SpawnPosition = position,
                IsBoss = isBoss
            };
            
            // Apply variant stats
            ApplyZombieStats(zombie, type, isBoss);
            
            // Set name based on type
            zombie.displayName = GetZombieName(type, isBoss);
            
            return zombie;
        }
        
        private void ApplyZombieStats(ScarecrowNPC zombie, ZombieType type, bool isBoss)
        {
            float healthMult = 1f;
            float speedMult = GetSpeedMultiplier();
            
            switch (type)
            {
                case ZombieType.Crawler:
                    healthMult = config.Variants.CrawlerHealth;
                    speedMult *= config.Variants.CrawlerSpeed;
                    break;
                case ZombieType.Tank:
                    healthMult = config.Variants.TankHealth;
                    speedMult *= config.Variants.TankSpeed;
                    break;
                case ZombieType.Spitter:
                    healthMult = 0.8f;
                    break;
                case ZombieType.Screamer:
                    healthMult = 0.6f;
                    speedMult *= 1.2f;
                    break;
                case ZombieType.Exploder:
                    healthMult = 0.7f;
                    break;
            }
            
            if (isBoss)
            {
                healthMult *= 5f;
                speedMult *= 1.2f;
            }
            
            // Apply health
            zombie.SetMaxHealth(zombie.MaxHealth() * healthMult);
            zombie.SetHealth(zombie.MaxHealth());
            
            // Apply speed via brain
            timer.Once(0.5f, () =>
            {
                if (zombie == null || zombie.IsDestroyed) return;
                
                var brain = zombie.GetComponent<ScarecrowBrain>();
                if (brain?.Navigator != null)
                {
                    brain.Navigator.Speed = 5f * speedMult;
                }
            });
        }
        
        private string GetZombieName(ZombieType type, bool isBoss)
        {
            string prefix = isBoss ? "[BOSS] " : "";
            switch (type)
            {
                case ZombieType.Crawler: return prefix + "Crawler";
                case ZombieType.Tank: return prefix + "Tank";
                case ZombieType.Spitter: return prefix + "Spitter";
                case ZombieType.Screamer: return prefix + "Screamer";
                case ZombieType.Exploder: return prefix + "Exploder";
                default: return prefix + "Zombie";
            }
        }
        #endregion


        #region Zombie Death Handling
        private void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null) return;
            
            var zombie = entity as ScarecrowNPC;
            if (zombie?.net == null) return;
            
            ulong zombieId = zombie.net.ID.Value;
            if (!activeZombies.TryGetValue(zombieId, out var zombieData))
                return;
            
            activeZombies.Remove(zombieId);
            
            // Get killer
            var killer = info?.InitiatorPlayer;
            if (killer == null || !killer.userID.IsSteamId()) return;
            
            // Handle exploder death
            if (zombieData.Type == ZombieType.Exploder)
            {
                CreateExplosion(zombie.transform.position);
            }
            
            // Update player stats
            if (config.Reputation.Enabled)
            {
                var stats = GetPlayerStats(killer.userID);
                stats.TotalKills++;
                
                if (zombieData.IsBoss)
                {
                    stats.BossKills++;
                    AddReputation(killer, config.Reputation.PointsPerBoss);
                }
                else
                {
                    AddReputation(killer, config.Reputation.PointsPerKill);
                }
                
                UpdatePlayerTitle(killer.userID);
            }
            
            // Update mission progress
            if (config.Missions.Enabled && activeMissions.TryGetValue(killer.userID, out var mission))
            {
                bool missionProgress = false;
                
                if (mission.Type == MissionType.KillZombies)
                    missionProgress = true;
                else if (mission.Type == MissionType.KillBoss && zombieData.IsBoss)
                    missionProgress = true;
                else if (mission.Type == MissionType.KillVariant && zombieData.Type != ZombieType.Normal)
                    missionProgress = true;
                
                if (missionProgress)
                {
                    mission.Progress++;
                    if (mission.Progress >= mission.Target)
                        CompleteMission(killer);
                    else
                        UpdateMissionUI(killer);
                }
            }
            
            // Drop loot
            if (config.Loot.Enabled)
            {
                DropZombieLoot(zombie.transform.position, zombieData);
            }
            
            // Wave tracking
            if (waveActive && waveParticipants.Contains(killer.userID))
            {
                // Wave zombie killed - check if wave complete
                CheckWaveComplete();
            }
        }
        
        private void CreateExplosion(Vector3 position)
        {
            // Visual effect
            Effect.server.Run("assets/bundled/prefabs/fx/explosions/explosion_03.prefab", position);
            
            // Damage nearby players
            var players = new List<BasePlayer>();
            Vis.Entities(position, config.Variants.ExploderRadius, players);
            
            foreach (var player in players)
            {
                if (player == null || player.IsDead()) continue;
                
                float distance = Vector3.Distance(position, player.transform.position);
                float damage = config.Variants.ExploderDamage * (1f - (distance / config.Variants.ExploderRadius));
                
                if (damage > 0)
                    player.Hurt(damage, Rust.DamageType.Explosion);
            }
        }
        
        private void DropZombieLoot(Vector3 position, ZombieData zombieData)
        {
            List<LootItem> lootTable;
            int lootChance;
            
            if (zombieData.IsBoss)
            {
                lootTable = config.Loot.BossLoot;
                lootChance = config.Loot.BossLootChance;
            }
            else if (zombieData.Type != ZombieType.Normal)
            {
                lootTable = config.Loot.VariantLoot;
                lootChance = config.Loot.VariantLootChance;
            }
            else
            {
                lootTable = config.Loot.NormalLoot;
                lootChance = config.Loot.NormalLootChance;
            }
            
            // Night bonus
            if (isNight)
                lootChance = (int)(lootChance * config.DayNight.NightLootMultiplier);
            
            if (UnityEngine.Random.Range(0, 100) >= lootChance) return;
            
            // Drop random item from table
            if (lootTable.Count == 0) return;
            
            var lootItem = lootTable[UnityEngine.Random.Range(0, lootTable.Count)];
            var itemDef = ItemManager.FindItemDefinition(lootItem.Shortname);
            if (itemDef == null) return;
            
            int amount = UnityEngine.Random.Range(lootItem.MinAmount, lootItem.MaxAmount + 1);
            var item = ItemManager.Create(itemDef, amount);
            
            if (item != null)
                item.Drop(position + Vector3.up, Vector3.up * 2f);
        }
        #endregion

        #region Wave System
        private int waveZombiesRemaining = 0;
        
        [ChatCommand("waves")]
        private void CmdStartWaves(BasePlayer player, string command, string[] args)
        {
            if (!config.Waves.Enabled)
            {
                SendReply(player, "<color=#ff4444>Wave system is disabled.</color>");
                return;
            }
            
            if (waveActive)
            {
                SendReply(player, $"<color=#ffaa00>Wave {currentWave}/{config.Waves.TotalWaves} in progress! {waveZombiesRemaining} zombies remaining.</color>");
                return;
            }
            
            StartWaveEvent(player);
        }
        
        private void StartWaveEvent(BasePlayer player)
        {
            waveActive = true;
            currentWave = 0;
            waveParticipants.Clear();
            waveParticipants.Add(player.userID);
            
            SendReply(player, "<color=#44ff44>[WAVE] WAVE SURVIVAL STARTED! Survive all waves for bonus rewards!</color>");
            
            StartNextWave(player);
        }
        
        private void StartNextWave(BasePlayer player)
        {
            if (player == null || !player.IsConnected || player.IsDead())
            {
                EndWaveEvent(false);
                return;
            }
            
            currentWave++;
            
            if (currentWave > config.Waves.TotalWaves)
            {
                EndWaveEvent(true);
                return;
            }
            
            int zombieCount = config.Waves.BaseZombiesPerWave + (currentWave * config.Waves.ExtraZombiesPerWave);
            waveZombiesRemaining = zombieCount;
            
            // Announce wave
            string waveMsg = $"<color=#ff4444>═══ WAVE {currentWave}/{config.Waves.TotalWaves} ═══</color>\n<color=#ffaa00>{zombieCount} zombies incoming!</color>";
            SendReply(player, waveMsg);
            
            // Spawn zombies
            for (int i = 0; i < zombieCount; i++)
            {
                timer.Once(i * 0.5f, () =>
                {
                    if (!waveActive || player == null) return;
                    
                    Vector3 spawnPos = GetSpawnPositionAround(player.transform.position, config.Waves.SpawnRadius);
                    if (spawnPos == Vector3.zero) return;
                    
                    ZombieType type = GetRandomZombieType();
                    bool isBoss = false;
                    
                    // Boss wave
                    if (currentWave == config.Waves.BossWave || (currentWave == config.Waves.TotalWaves && config.Waves.FinalBoss))
                    {
                        if (i == 0) // First zombie of boss wave is the boss
                        {
                            isBoss = true;
                            PrintToChat($"<color=#ff0000>[BOSS] A BOSS ZOMBIE has appeared in wave {currentWave}!</color>");
                        }
                    }
                    
                    SpawnZombie(spawnPos, type, isBoss);
                });
            }
            
            // Update UI
            ShowWaveUI(player);
        }
        
        private void CheckWaveComplete()
        {
            waveZombiesRemaining--;
            
            if (waveZombiesRemaining <= 0 && waveActive)
            {
                // Wave complete
                foreach (var playerId in waveParticipants)
                {
                    var player = BasePlayer.FindByID(playerId);
                    if (player == null) continue;
                    
                    // Give wave reward
                    GiveScrap(player, config.Waves.ScrapPerWave);
                    SendReply(player, $"<color=#44ff44>Wave {currentWave} complete! +{config.Waves.ScrapPerWave} scrap</color>");
                    
                    // Update stats
                    if (config.Reputation.Enabled)
                    {
                        var stats = GetPlayerStats(player.userID);
                        stats.WavesSurvived++;
                        AddReputation(player, config.Reputation.PointsPerWave);
                    }
                }
                
                // Start next wave after delay
                var firstPlayer = waveParticipants.Count > 0 ? BasePlayer.FindByID(waveParticipants[0]) : null;
                if (firstPlayer != null)
                {
                    waveTimer?.Destroy();
                    waveTimer = timer.Once(config.Waves.TimeBetweenWaves, () => StartNextWave(firstPlayer));
                    
                    SendReply(firstPlayer, $"<color=#ffaa00>Next wave in {config.Waves.TimeBetweenWaves} seconds...</color>");
                }
            }
            
            // Update UI
            foreach (var playerId in waveParticipants)
            {
                var player = BasePlayer.FindByID(playerId);
                if (player != null) ShowWaveUI(player);
            }
        }
        
        private void EndWaveEvent(bool success)
        {
            waveActive = false;
            waveTimer?.Destroy();
            
            foreach (var playerId in waveParticipants)
            {
                var player = BasePlayer.FindByID(playerId);
                if (player == null) continue;
                
                CuiHelper.DestroyUi(player, WAVE_UI);
                
                if (success)
                {
                    GiveScrap(player, config.Waves.CompletionBonus);
                    SendReply(player, $"<color=#44ff44>[WIN] ALL WAVES COMPLETE! Bonus: +{config.Waves.CompletionBonus} scrap!</color>");
                }
                else
                {
                    SendReply(player, $"<color=#ff4444>Wave event ended. You survived {currentWave - 1} waves.</color>");
                }
            }
            
            waveParticipants.Clear();
            currentWave = 0;
        }
        
        private void ShowWaveUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, WAVE_UI);
            
            var elements = new CuiElementContainer();
            
            elements.Add(new CuiPanel
            {
                Image = { Color = "0.1 0.1 0.1 0.8" },
                RectTransform = { AnchorMin = "0.4 0.9", AnchorMax = "0.6 0.97" }
            }, "Overlay", WAVE_UI);
            
            elements.Add(new CuiLabel
            {
                Text = { Text = $"WAVE {currentWave}/{config.Waves.TotalWaves}", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "1 0.3 0.3 1" },
                RectTransform = { AnchorMin = "0 0.5", AnchorMax = "1 1" }
            }, WAVE_UI);
            
            elements.Add(new CuiLabel
            {
                Text = { Text = $"Zombies: {waveZombiesRemaining}", FontSize = 12, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0.5" }
            }, WAVE_UI);
            
            CuiHelper.AddUi(player, elements);
        }
        #endregion


        #region Mission System
        private void CmdMission(BasePlayer player, string command, string[] args)
        {
            if (!config.Missions.Enabled)
            {
                SendReply(player, "<color=#ff4444>Mission system is disabled.</color>");
                return;
            }
            
            if (activeMissions.ContainsKey(player.userID))
            {
                var mission = activeMissions[player.userID];
                float timeLeft = mission.TimeLimit - (Time.realtimeSinceStartup - mission.StartTime);
                
                if (timeLeft <= 0)
                {
                    FailMission(player);
                    return;
                }
                
                SendReply(player, $"<color=#ffcc00>Current Mission:</color> {mission.Description}\n" +
                                 $"Progress: <color=#44ff44>{mission.Progress}/{mission.Target}</color>\n" +
                                 $"Time left: <color=#ffaa00>{timeLeft:F0}s</color>\n" +
                                 $"Reward: <color=#44ff44>{mission.RewardScrap} scrap</color>");
                return;
            }
            
            // Generate new mission
            GenerateMission(player);
        }
        
        private void GenerateMission(BasePlayer player)
        {
            var missionTypes = new List<MissionType> { MissionType.KillZombies };
            if (config.Variants.Enabled) missionTypes.Add(MissionType.KillVariant);
            
            var type = missionTypes[UnityEngine.Random.Range(0, missionTypes.Count)];
            
            var mission = new PlayerMission
            {
                Type = type,
                StartTime = Time.realtimeSinceStartup,
                TimeLimit = config.Missions.TimeLimit,
                Progress = 0
            };
            
            switch (type)
            {
                case MissionType.KillZombies:
                    mission.Target = config.Missions.KillTarget;
                    mission.RewardScrap = config.Missions.KillReward;
                    mission.Description = $"Kill {mission.Target} zombies";
                    break;
                case MissionType.KillVariant:
                    mission.Target = config.Missions.VariantTarget;
                    mission.RewardScrap = config.Missions.VariantReward;
                    mission.Description = $"Kill {mission.Target} variant zombies (Crawler, Tank, etc.)";
                    break;
            }
            
            activeMissions[player.userID] = mission;
            
            SendReply(player, $"<color=#44ff44>[MISSION] NEW MISSION:</color> {mission.Description}\n" +
                             $"Time limit: <color=#ffaa00>{mission.TimeLimit / 60f:F0} minutes</color>\n" +
                             $"Reward: <color=#44ff44>{mission.RewardScrap} scrap</color>");
            
            UpdateMissionUI(player);
        }
        
        private void CompleteMission(BasePlayer player)
        {
            if (!activeMissions.TryGetValue(player.userID, out var mission)) return;
            
            GiveScrap(player, mission.RewardScrap);
            
            SendReply(player, $"<color=#44ff44>[OK] MISSION COMPLETE!</color> +{mission.RewardScrap} scrap");
            
            if (config.Reputation.Enabled)
            {
                var stats = GetPlayerStats(player.userID);
                stats.MissionsCompleted++;
                AddReputation(player, config.Reputation.PointsPerMission);
            }
            
            activeMissions.Remove(player.userID);
            CuiHelper.DestroyUi(player, MISSION_UI);
        }
        
        private void FailMission(BasePlayer player)
        {
            if (!activeMissions.ContainsKey(player.userID)) return;
            
            SendReply(player, "<color=#ff4444>[X] Mission failed - time ran out!</color>");
            
            activeMissions.Remove(player.userID);
            CuiHelper.DestroyUi(player, MISSION_UI);
        }
        
        private void UpdateMissionUI(BasePlayer player)
        {
            if (!activeMissions.TryGetValue(player.userID, out var mission)) return;
            
            CuiHelper.DestroyUi(player, MISSION_UI);
            
            var elements = new CuiElementContainer();
            
            elements.Add(new CuiPanel
            {
                Image = { Color = "0.1 0.1 0.1 0.7" },
                RectTransform = { AnchorMin = "0.01 0.85", AnchorMax = "0.2 0.92" }
            }, "Overlay", MISSION_UI);
            
            elements.Add(new CuiLabel
            {
                Text = { Text = $"[M] {mission.Description}", FontSize = 10, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0.05 0.5", AnchorMax = "0.95 1" }
            }, MISSION_UI);
            
            float progress = (float)mission.Progress / mission.Target;
            elements.Add(new CuiPanel
            {
                Image = { Color = "0.2 0.6 0.2 1" },
                RectTransform = { AnchorMin = "0.05 0.1", AnchorMax = $"{0.05f + (0.9f * progress)} 0.4" }
            }, MISSION_UI);
            
            elements.Add(new CuiLabel
            {
                Text = { Text = $"{mission.Progress}/{mission.Target}", FontSize = 9, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0.5" }
            }, MISSION_UI);
            
            CuiHelper.AddUi(player, elements);
        }
        #endregion

        #region Base Defense System
        private void StartDefenseMonitor()
        {
            defenseTimer?.Destroy();
            defenseTimer = timer.Every(3600f, CheckBaseDefenseSpawn); // Check every hour
        }
        
        private void CheckBaseDefenseSpawn()
        {
            if (UnityEngine.Random.Range(0, 100) >= config.BaseDefense.AttackChance) return;
            
            // Find a random player's base to attack
            var eligiblePlayers = BasePlayer.activePlayerList
                .Where(p => p != null && p.IsConnected && !p.IsDead())
                .ToList();
            
            if (eligiblePlayers.Count == 0) return;
            
            var targetPlayer = eligiblePlayers[UnityEngine.Random.Range(0, eligiblePlayers.Count)];
            
            // Find their TC
            var tc = FindPlayerTC(targetPlayer);
            if (tc == null) return;
            
            StartBaseDefense(targetPlayer, tc.transform.position);
        }
        
        private BuildingPrivlidge FindPlayerTC(BasePlayer player)
        {
            var tcs = UnityEngine.Object.FindObjectsOfType<BuildingPrivlidge>();
            foreach (var tc in tcs)
            {
                if (tc.IsAuthed(player))
                    return tc;
            }
            return null;
        }
        
        private void StartBaseDefense(BasePlayer player, Vector3 position)
        {
            if (activeDefenseEvents.ContainsKey(player.userID)) return;
            
            // Warning
            SendReply(player, string.Format(config.BaseDefense.WarningMessage, config.BaseDefense.WarningTime));
            
            timer.Once(config.BaseDefense.WarningTime, () =>
            {
                if (player == null || !player.IsConnected) return;
                
                var defenseEvent = new BaseDefenseEvent
                {
                    Position = position,
                    StartTime = Time.realtimeSinceStartup,
                    WavesRemaining = config.BaseDefense.DefenseWaves,
                    Defenders = new List<ulong> { player.userID }
                };
                
                activeDefenseEvents[player.userID] = defenseEvent;
                
                SendReply(player, "<color=#ff4444>[!] THE HORDE HAS ARRIVED! DEFEND YOUR BASE!</color>");
                
                SpawnDefenseWave(player.userID);
            });
        }
        
        private void SpawnDefenseWave(ulong playerId)
        {
            if (!activeDefenseEvents.TryGetValue(playerId, out var defenseEvent)) return;
            
            var player = BasePlayer.FindByID(playerId);
            if (player == null)
            {
                EndBaseDefense(playerId, false);
                return;
            }
            
            defenseEvent.ZombiesRemaining = config.BaseDefense.ZombiesPerWave;
            int waveNum = config.BaseDefense.DefenseWaves - defenseEvent.WavesRemaining + 1;
            
            SendReply(player, $"<color=#ff4444>Defense Wave {waveNum}/{config.BaseDefense.DefenseWaves} - {defenseEvent.ZombiesRemaining} zombies!</color>");
            
            for (int i = 0; i < config.BaseDefense.ZombiesPerWave; i++)
            {
                timer.Once(i * 0.3f, () =>
                {
                    if (!activeDefenseEvents.ContainsKey(playerId)) return;
                    
                    Vector3 spawnPos = GetSpawnPositionAround(defenseEvent.Position, config.BaseDefense.DefenseRadius + 20f);
                    if (spawnPos == Vector3.zero) return;
                    
                    SpawnZombie(spawnPos, GetRandomZombieType(), false);
                });
            }
            
            defenseEvent.WavesRemaining--;
        }
        
        private void EndBaseDefense(ulong playerId, bool success)
        {
            if (!activeDefenseEvents.ContainsKey(playerId)) return;
            
            activeDefenseEvents.Remove(playerId);
            
            var player = BasePlayer.FindByID(playerId);
            if (player == null) return;
            
            if (success)
            {
                GiveScrap(player, config.BaseDefense.DefenseReward);
                SendReply(player, $"<color=#44ff44>[WIN] BASE DEFENDED! +{config.BaseDefense.DefenseReward} scrap</color>");
                
                if (config.Reputation.Enabled)
                {
                    var stats = GetPlayerStats(playerId);
                    stats.BasesDefended++;
                }
            }
            else
            {
                SendReply(player, "<color=#ff4444>Base defense failed!</color>");
            }
        }
        #endregion

        #region Reputation System
        private void AddReputation(BasePlayer player, int points)
        {
            // Reputation is tracked via total kills/missions/etc in PlayerStats
            // This just notifies the player
            if (points > 0)
                SendReply(player, $"<color=#44ff44>+{points} reputation</color>");
        }
        
        private void UpdatePlayerTitle(ulong playerId)
        {
            var stats = GetPlayerStats(playerId);
            int totalPoints = stats.TotalKills * config.Reputation.PointsPerKill +
                             stats.BossKills * config.Reputation.PointsPerBoss +
                             stats.WavesSurvived * config.Reputation.PointsPerWave +
                             stats.MissionsCompleted * config.Reputation.PointsPerMission -
                             stats.Deaths * config.Reputation.PointsLostOnDeath;
            
            string newTitle = "Survivor";
            foreach (var title in config.Reputation.Titles.OrderByDescending(t => t.Value))
            {
                if (totalPoints >= title.Value)
                {
                    newTitle = title.Key;
                    break;
                }
            }
            
            if (stats.Title != newTitle)
            {
                stats.Title = newTitle;
                var player = BasePlayer.FindByID(playerId);
                if (player != null)
                    SendReply(player, $"<color=#ffcc00>[TITLE] New title unlocked: {newTitle}!</color>");
            }
        }
        
        private void CmdLeaderboard(BasePlayer player, string command, string[] args)
        {
            var topPlayers = storedData.PlayerStats
                .Select(kvp => new {
                    PlayerId = kvp.Key,
                    Stats = kvp.Value,
                    Points = kvp.Value.TotalKills * config.Reputation.PointsPerKill +
                            kvp.Value.BossKills * config.Reputation.PointsPerBoss +
                            kvp.Value.WavesSurvived * config.Reputation.PointsPerWave +
                            kvp.Value.MissionsCompleted * config.Reputation.PointsPerMission
                })
                .OrderByDescending(x => x.Points)
                .Take(10)
                .ToList();
            
            string msg = "<color=#ffcc00>═══ ZOMBIE HUNTER LEADERBOARD ═══</color>\n";
            
            for (int i = 0; i < topPlayers.Count; i++)
            {
                var entry = topPlayers[i];
                string name = GetPlayerName(entry.PlayerId);
                msg += $"{i + 1}. <color=#44ff44>{name}</color> - {entry.Points} pts ({entry.Stats.Title})\n";
            }
            
            // Show player's own stats
            var myStats = GetPlayerStats(player.userID);
            int myPoints = myStats.TotalKills * config.Reputation.PointsPerKill +
                          myStats.BossKills * config.Reputation.PointsPerBoss +
                          myStats.WavesSurvived * config.Reputation.PointsPerWave +
                          myStats.MissionsCompleted * config.Reputation.PointsPerMission;
            
            msg += $"\n<color=#888888>Your stats: {myPoints} pts | Kills: {myStats.TotalKills} | Title: {myStats.Title}</color>";
            
            SendReply(player, msg);
        }
        
        private string GetPlayerName(ulong playerId)
        {
            var player = BasePlayer.FindByID(playerId);
            if (player != null) return player.displayName;
            
            var sleeper = BasePlayer.FindSleeping(playerId);
            if (sleeper != null) return sleeper.displayName;
            
            return playerId.ToString();
        }
        #endregion


        #region Helpers
        private Vector3 GetSpawnPositionAround(Vector3 center, float radius)
        {
            for (int attempts = 0; attempts < 10; attempts++)
            {
                float angle = UnityEngine.Random.Range(0f, 360f) * Mathf.Deg2Rad;
                float distance = UnityEngine.Random.Range(radius * 0.5f, radius);
                
                Vector3 offset = new Vector3(Mathf.Cos(angle) * distance, 0, Mathf.Sin(angle) * distance);
                Vector3 pos = center + offset;
                
                RaycastHit hit;
                if (Physics.Raycast(pos + Vector3.up * 100f, Vector3.down, out hit, 200f, LayerMask.GetMask("Terrain", "World")))
                {
                    pos = hit.point;
                    
                    if (!WaterLevel.Test(pos, true, true))
                        return pos;
                }
            }
            return Vector3.zero;
        }
        
        private void GiveScrap(BasePlayer player, int amount)
        {
            var item = ItemManager.CreateByName("scrap", amount);
            if (item != null)
                player.GiveItem(item);
        }
        #endregion

        #region Admin Commands
        [ChatCommand("zspawn")]
        private void CmdAdminSpawn(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin && !permission.UserHasPermission(player.UserIDString, PERM_ADMIN))
            {
                SendReply(player, "<color=#ff4444>Admin only.</color>");
                return;
            }
            
            ZombieType type = ZombieType.Normal;
            bool isBoss = false;
            
            if (args.Length > 0)
            {
                switch (args[0].ToLower())
                {
                    case "crawler": type = ZombieType.Crawler; break;
                    case "tank": type = ZombieType.Tank; break;
                    case "spitter": type = ZombieType.Spitter; break;
                    case "screamer": type = ZombieType.Screamer; break;
                    case "exploder": type = ZombieType.Exploder; break;
                    case "boss": isBoss = true; break;
                }
            }
            
            RaycastHit hit;
            if (!Physics.Raycast(player.eyes.HeadRay(), out hit, 100f))
            {
                SendReply(player, "Look at a valid spawn location");
                return;
            }
            
            var zombie = SpawnZombie(hit.point, type, isBoss);
            if (zombie != null)
                SendReply(player, $"<color=#44ff44>Spawned {GetZombieName(type, isBoss)}</color>");
        }
        
        [ChatCommand("zstats")]
        private void CmdStats(BasePlayer player, string command, string[] args)
        {
            var stats = GetPlayerStats(player.userID);
            
            int totalPoints = stats.TotalKills * config.Reputation.PointsPerKill +
                             stats.BossKills * config.Reputation.PointsPerBoss +
                             stats.WavesSurvived * config.Reputation.PointsPerWave +
                             stats.MissionsCompleted * config.Reputation.PointsPerMission -
                             stats.Deaths * config.Reputation.PointsLostOnDeath;
            
            SendReply(player, $"<color=#ffcc00>═══ YOUR ZOMBIE STATS ═══</color>\n" +
                             $"Title: <color=#44ff44>{stats.Title}</color>\n" +
                             $"Total Points: <color=#44ff44>{totalPoints}</color>\n" +
                             $"Zombies Killed: <color=#44ff44>{stats.TotalKills}</color>\n" +
                             $"Bosses Killed: <color=#44ff44>{stats.BossKills}</color>\n" +
                             $"Waves Survived: <color=#44ff44>{stats.WavesSurvived}</color>\n" +
                             $"Missions Completed: <color=#44ff44>{stats.MissionsCompleted}</color>\n" +
                             $"Bases Defended: <color=#44ff44>{stats.BasesDefended}</color>\n" +
                             $"Deaths: <color=#ff4444>{stats.Deaths}</color>");
        }
        
        [ChatCommand("zhelp2")]
        private void CmdHelp(BasePlayer player, string command, string[] args)
        {
            string msg = "<color=#ffcc00>═══ ZOMBIE PVE SYSTEM ═══</color>\n\n";
            
            if (config.Waves.Enabled)
                msg += $"<color=#44ff44>/{config.Waves.StartCommand}</color> - Start wave survival\n";
            
            if (config.Missions.Enabled)
                msg += $"<color=#44ff44>/{config.Missions.MissionCommand}</color> - Get/check mission\n";
            
            if (config.Reputation.Enabled)
                msg += $"<color=#44ff44>/{config.Reputation.LeaderboardCommand}</color> - View leaderboard\n";
            
            msg += "<color=#44ff44>/zstats</color> - View your stats\n";
            
            if (player.IsAdmin)
            {
                msg += "\n<color=#ff8800>Admin Commands:</color>\n";
                msg += "<color=#ff8800>/zspawn [type]</color> - Spawn zombie (crawler/tank/spitter/screamer/exploder/boss)\n";
            }
            
            msg += "\n<color=#888888>Zombie Types:</color>\n";
            msg += "• <color=#ff4444>Crawler</color> - Fast but weak\n";
            msg += "• <color=#ff4444>Tank</color> - Slow but very tough\n";
            msg += "• <color=#ff4444>Spitter</color> - Ranged attacks\n";
            msg += "• <color=#ff4444>Screamer</color> - Alerts nearby zombies\n";
            msg += "• <color=#ff4444>Exploder</color> - Explodes on death!\n";
            
            SendReply(player, msg);
        }
        #endregion

        #region Player Death
        private void OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            if (player == null || !player.userID.IsSteamId()) return;
            
            // Check if killed by zombie
            var attacker = info?.InitiatorPlayer;
            if (attacker != null && !attacker.userID.IsSteamId())
            {
                if (config.Reputation.Enabled)
                {
                    var stats = GetPlayerStats(player.userID);
                    stats.Deaths++;
                }
                
                // End wave if in wave
                if (waveActive && waveParticipants.Contains(player.userID))
                {
                    EndWaveEvent(false);
                }
                
                // Fail mission
                if (activeMissions.ContainsKey(player.userID))
                {
                    FailMission(player);
                }
            }
        }
        #endregion
    }
}
