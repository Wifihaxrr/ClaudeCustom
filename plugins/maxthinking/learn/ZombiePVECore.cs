using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("ZombiePVECore", "ZombiePVE", "3.0.0")]
    [Description("Ultimate PVE zombie survival - Infection, hordes, loot, events & more!")]
    public class ZombiePVECore : RustPlugin
    {
        #region Fields
        private Dictionary<ulong, PlayerStatus> playerStatuses = new Dictionary<ulong, PlayerStatus>();
        private Dictionary<ulong, PlayerStats> playerStats = new Dictionary<ulong, PlayerStats>();
        private HashSet<ulong> spawnedZombies = new HashSet<ulong>();
        private Dictionary<ulong, List<Item>> deathZombieLoot = new Dictionary<ulong, List<Item>>();
        private const string BLEED_UI = "ZombiePVE_Bleed";
        private const string POISON_UI = "ZombiePVE_Poison";
        private const string HORDE_UI = "ZombiePVE_Horde";
        private const string STATS_UI = "ZombiePVE_Stats";
        private const string VIP_PERM = "zombiepvecore.vip";
        private const string ADMIN_PERM = "zombiepvecore.admin";
        private Timer zombieSpawnTimer;
        private Timer zombieCleanupTimer;
        private Timer ambientZombieTimer;
        private Timer bloodMoonTimer;
        private bool isBloodMoon = false;
        private int currentWave = 0;
        
        // Night vision tracking
        private HashSet<ulong> nightVisionPlayers = new HashSet<ulong>();
        private bool isCurrentlyNight = false;
        
        // Admin ESP tracking
        private HashSet<ulong> espPlayers = new HashSet<ulong>();
        private Timer espTimer;
        
        // Smooth Saver tracking
        private bool isSaving = false;
        private Timer autoSaveTimer;
        private DateTime lastSaveTime;
        private float lastSaveDuration;
        private int lastSaveEntityCount;
        
        // Tier plugin references - these override Core functionality when loaded
        [PluginReference] private Plugin ZombiePVETier1, ZombiePVETier2, ZombiePVETier3;
        
        // Zombie types for variety
        private readonly string[] zombiePrefabs = new string[]
        {
            "assets/prefabs/npc/scarecrow/scarecrow.prefab",
            "assets/prefabs/npc/murderer/murderer.prefab"
        };
        
        private readonly string[] zombieNames = new string[]
        {
            "Zombie", "Walker", "Shambler", "Crawler", "Infected",
            "Rotten", "Ghoul", "Undead", "Corpse", "Risen"
        };
        #endregion

        #region Classes
        private class PlayerStatus
        {
            public bool IsBleeding;
            public bool IsPoisoned;
            public Timer BleedTimer;
            public Timer PoisonTimer;
        }
        
        private class PlayerStats
        {
            public int ZombiesKilled;
            public int HeadshotKills;
            public int DeathsToZombies;
            public int InfectionsSurvived;
            public int CurrentKillStreak;
            public int BestKillStreak;
            public DateTime LastKillTime;
        }
        #endregion

        #region Configuration
        private Configuration config;

        private class Configuration
        {
            [JsonProperty("═══════════════════════════════════════")]
            public string Header1 { get; set; } = "PVE PROTECTION SETTINGS";
            
            [JsonProperty("1. PVE Protection Settings")]
            public PVESettings PVE { get; set; } = new PVESettings();
            
            [JsonProperty("2. Zombie Spawning Settings")]
            public ZombieSpawnSettings ZombieSpawn { get; set; } = new ZombieSpawnSettings();
            
            [JsonProperty("3. Zombie Combat Settings")]
            public ZombieCombatSettings ZombieCombat { get; set; } = new ZombieCombatSettings();
            
            [JsonProperty("4. Infection System Settings")]
            public InfectionSettings Infection { get; set; } = new InfectionSettings();
            
            [JsonProperty("5. Blood Moon Event Settings")]
            public BloodMoonSettings BloodMoon { get; set; } = new BloodMoonSettings();
            
            [JsonProperty("6. Boss Zombie Settings")]
            public BossSettings Boss { get; set; } = new BossSettings();
            
            [JsonProperty("7. Loot & Rewards Settings")]
            public LootSettings Loot { get; set; } = new LootSettings();
            
            [JsonProperty("8. Kill Streak Settings")]
            public KillStreakSettings KillStreak { get; set; } = new KillStreakSettings();
            
            [JsonProperty("9. VIP Settings")]
            public VIPSettings VIP { get; set; } = new VIPSettings();
            
            [JsonProperty("10. World Settings")]
            public WorldSettings World { get; set; } = new WorldSettings();
            
            [JsonProperty("11. UI & Broadcast Settings")]
            public UISettings UI { get; set; } = new UISettings();
            
            [JsonProperty("12. Death Zombie Settings")]
            public DeathZombieSettings DeathZombie { get; set; } = new DeathZombieSettings();
            
            [JsonProperty("13. Debug Settings")]
            public DebugSettings Debug { get; set; } = new DebugSettings();
            
            [JsonProperty("14. Smooth Saver Settings")]
            public SmoothSaverSettings SmoothSaver { get; set; } = new SmoothSaverSettings();
            
            // Nested config classes
            public class PVESettings
            {
                [JsonProperty("Block player vs player damage")]
                public bool BlockPVP { get; set; } = true;
                
                [JsonProperty("Block player building damage (raiding)")]
                public bool BlockRaiding { get; set; } = true;
                
                [JsonProperty("Allow team damage")]
                public bool AllowTeamDamage { get; set; } = false;
                
                [JsonProperty("Block trap damage to other players")]
                public bool BlockTrapDamage { get; set; } = true;
                
                [JsonProperty("Protect sleeping players")]
                public bool ProtectSleepers { get; set; } = true;
                
                [JsonProperty("Block looting other players")]
                public bool BlockPlayerLooting { get; set; } = true;
                
                [JsonProperty("Block looting sleepers")]
                public bool BlockSleeperLooting { get; set; } = true;
                
                [JsonProperty("Block helicopter damage to player buildings")]
                public bool BlockHeliDamage { get; set; } = true;
                
                [JsonProperty("Block Bradley damage to player buildings")]
                public bool BlockBradleyDamage { get; set; } = true;
                
                [JsonProperty("Allow decay on buildings")]
                public bool AllowDecay { get; set; } = true;
                
                [JsonProperty("Decay multiplier (1.0 = normal, 0.5 = half speed)")]
                public float DecayMultiplier { get; set; } = 0.5f;
                
                [JsonProperty("Kill sleepers in safe zones")]
                public bool KillSafeZoneSleepers { get; set; } = true;
            }
            
            public class ZombieSpawnSettings
            {
                [JsonProperty("Enable zombie spawning system")]
                public bool Enabled { get; set; } = true;
                
                [JsonProperty("Minimum zombies per spawn cycle")]
                public int MinZombiesSolo { get; set; } = 1;
                
                [JsonProperty("Maximum zombies per spawn cycle")]
                public int MaxZombiesSolo { get; set; } = 2;
                
                [JsonProperty("Minimum zombies for teams (2+ players)")]
                public int MinZombiesTeam { get; set; } = 1;
                
                [JsonProperty("Maximum zombies for teams (2+ players)")]
                public int MaxZombiesTeam { get; set; } = 2;
                
                [JsonProperty("Maximum zombies near a single player")]
                public int MaxZombiesPerPlayer { get; set; } = 3;
                
                [JsonProperty("Minimum spawn distance from player (meters)")]
                public float MinSpawnDistance { get; set; } = 140f;
                
                [JsonProperty("Maximum spawn distance from player (meters)")]
                public float MaxSpawnDistance { get; set; } = 200f;
                
                [JsonProperty("Minimum spawn interval (seconds)")]
                public float MinSpawnInterval { get; set; } = 12f;
                
                [JsonProperty("Maximum spawn interval (seconds)")]
                public float MaxSpawnInterval { get; set; } = 48f;
                
                [JsonProperty("Maximum total zombies on server")]
                public int MaxTotalZombies { get; set; } = 100;
                
                [JsonProperty("Despawn range (meters) - zombies despawn when no player nearby")]
                public float DespawnRange { get; set; } = 240f;
                
                [JsonProperty("Cleanup check interval (seconds)")]
                public float CleanupInterval { get; set; } = 10f;
                
                [JsonProperty("Respawn chance when killed without headshot (0-100)")]
                public int RespawnChance { get; set; } = 30;
                
                [JsonProperty("Enable night spawn boost")]
                public bool NightBoostEnabled { get; set; } = true;
                
                [JsonProperty("Night spawn multiplier (2.0 = double zombies at night)")]
                public float NightSpawnMultiplier { get; set; } = 2f;
                
                [JsonProperty("Enable spawn sounds")]
                public bool SpawnSoundsEnabled { get; set; } = true;
            }
            
            public class ZombieCombatSettings
            {
                [JsonProperty("Zombie speed multiplier (0.8 = 20% slower)")]
                public float SpeedMultiplier { get; set; } = 0.8f;
                
                [JsonProperty("Zombie damage multiplier (0.4 = 60% less damage)")]
                public float DamageMultiplier { get; set; } = 0.4f;
                
                [JsonProperty("Zombie aggro range (meters)")]
                public float AggroRange { get; set; } = 15f;
                
                [JsonProperty("Gunshot/tool noise range (meters) - guns, jackhammer, chainsaw")]
                public float GunshotAggroRange { get; set; } = 130f;
                
                [JsonProperty("Explosion noise range (meters) - rockets, C4, satchels")]
                public float ExplosionAggroRange { get; set; } = 300f;
                
                [JsonProperty("Running footstep noise range (meters)")]
                public float RunningNoiseRange { get; set; } = 40f;
                
                [JsonProperty("Crouching makes player invisible to zombies")]
                public bool CrouchingHidesPlayer { get; set; } = true;
                
                [JsonProperty("Headshot instant kill chance (0-100)")]
                public int HeadshotKillChance { get; set; } = 75;
                
                [JsonProperty("Beancan throw chance (0-100)")]
                public int BeancanChance { get; set; } = 5;
                
                [JsonProperty("Enable fire zombies (burning zombies deal extra damage)")]
                public bool FireZombiesEnabled { get; set; } = true;
                
                [JsonProperty("Fire zombie extra damage multiplier")]
                public float FireDamageMultiplier { get; set; } = 1.5f;
            }
            
            public class InfectionSettings
            {
                [JsonProperty("Enable infection system")]
                public bool Enabled { get; set; } = true;
                
                [JsonProperty("Bleed chance on zombie hit (0-100)")]
                public int BleedChance { get; set; } = 25;
                
                [JsonProperty("Poison chance when already bleeding (0-100)")]
                public int PoisonChance { get; set; } = 25;
                
                [JsonProperty("Bleed damage per tick")]
                public float BleedDamage { get; set; } = 0.2f;
                
                [JsonProperty("Bleed tick interval (seconds)")]
                public float BleedInterval { get; set; } = 3f;
                
                [JsonProperty("Poison damage per tick")]
                public float PoisonDamage { get; set; } = 0.5f;
                
                [JsonProperty("Poison tick interval (seconds)")]
                public float PoisonInterval { get; set; } = 4f;
                
                [JsonProperty("Bandage cures bleeding")]
                public bool BandageCuresBleed { get; set; } = true;
                
                [JsonProperty("Syringe cures poison")]
                public bool SyringeCuresPoison { get; set; } = true;
                
                [JsonProperty("Syringe also cures bleeding")]
                public bool SyringeCuresBleed { get; set; } = true;
            }
            
            public class BloodMoonSettings
            {
                [JsonProperty("Enable Blood Moon events")]
                public bool Enabled { get; set; } = true;
                
                [JsonProperty("Random Blood Moon chance (0-100) - checked every 30 min")]
                public int RandomChance { get; set; } = 15;
                
                [JsonProperty("Blood Moon duration (seconds)")]
                public float Duration { get; set; } = 300f;
                
                [JsonProperty("Zombie spawn multiplier during Blood Moon")]
                public float ZombieMultiplier { get; set; } = 3f;
                
                [JsonProperty("Zombie speed boost during Blood Moon")]
                public float SpeedBoost { get; set; } = 1.5f;
                
                [JsonProperty("Zombie aggro range boost during Blood Moon")]
                public float AggroBoost { get; set; } = 2f;
                
                [JsonProperty("Wave interval (seconds)")]
                public float WaveInterval { get; set; } = 30f;
                
                [JsonProperty("Extra zombies per wave")]
                public int ExtraZombiesPerWave { get; set; } = 2;
            }
            
            public class BossSettings
            {
                [JsonProperty("Enable boss zombies")]
                public bool Enabled { get; set; } = true;
                
                [JsonProperty("Boss spawn chance (0-100)")]
                public int SpawnChance { get; set; } = 5;
                
                [JsonProperty("Boss health multiplier")]
                public float HealthMultiplier { get; set; } = 5f;
                
                [JsonProperty("Boss damage multiplier")]
                public float DamageMultiplier { get; set; } = 2f;
                
                [JsonProperty("Boss size scale")]
                public float SizeScale { get; set; } = 2.0f;
                
                [JsonProperty("Boss speed multiplier")]
                public float SpeedMultiplier { get; set; } = 1.2f;
                
                [JsonProperty("Boss aggro range")]
                public float AggroRange { get; set; } = 50f;
                
                [JsonProperty("Announce boss spawn to server")]
                public bool AnnounceSpawn { get; set; } = true;
                
                [JsonProperty("Boss guaranteed loot drop")]
                public bool GuaranteedLoot { get; set; } = true;
            }
            
            public class LootSettings
            {
                [JsonProperty("Enable zombie loot drops")]
                public bool Enabled { get; set; } = true;
                
                [JsonProperty("Loot drop chance (0-100)")]
                public int DropChance { get; set; } = 35;
                
                [JsonProperty("Maximum items per drop")]
                public int MaxItems { get; set; } = 3;
                
                [JsonProperty("Boss loot multiplier")]
                public float BossLootMultiplier { get; set; } = 3f;
                
                [JsonProperty("Night loot bonus multiplier")]
                public float NightLootBonus { get; set; } = 1.5f;
                
                [JsonProperty("Blood Moon loot bonus multiplier")]
                public float BloodMoonLootBonus { get; set; } = 2f;
            }
            
            public class KillStreakSettings
            {
                [JsonProperty("Enable kill streak system")]
                public bool Enabled { get; set; } = true;
                
                [JsonProperty("Kill streak timeout (seconds)")]
                public float Timeout { get; set; } = 30f;
                
                [JsonProperty("Milestone interval (broadcast every X kills)")]
                public int MilestoneInterval { get; set; } = 10;
                
                [JsonProperty("Scrap bonus at 5 kills")]
                public int Bonus5Kills { get; set; } = 10;
                
                [JsonProperty("Scrap bonus at 10 kills")]
                public int Bonus10Kills { get; set; } = 25;
                
                [JsonProperty("Scrap bonus at 25 kills")]
                public int Bonus25Kills { get; set; } = 100;
                
                [JsonProperty("Broadcast milestones to server")]
                public bool BroadcastMilestones { get; set; } = true;
            }
            
            public class VIPSettings
            {
                [JsonProperty("VIP radiation immunity")]
                public bool RadiationImmunity { get; set; } = true;
                
                [JsonProperty("VIP full stats on respawn")]
                public bool FullStatsOnRespawn { get; set; } = true;
                
                [JsonProperty("VIP half fuel consumption")]
                public bool HalfFuelConsumption { get; set; } = true;
                
                [JsonProperty("VIP infection immunity")]
                public bool InfectionImmunity { get; set; } = false;
                
                [JsonProperty("VIP loot bonus multiplier")]
                public float LootBonus { get; set; } = 1.5f;
                
                [JsonProperty("VIP kill streak bonus multiplier")]
                public float KillStreakBonus { get; set; } = 2f;
                
                [JsonProperty("VIP night vision enabled")]
                public bool NightVisionEnabled { get; set; } = true;
                
                [JsonProperty("Night vision brightness (higher = brighter)")]
                public float NightVisionBrightness { get; set; } = 1f;
                
                [JsonProperty("Night vision distance")]
                public float NightVisionDistance { get; set; } = 100f;
                
                [JsonProperty("Atmosphere brightness boost at night")]
                public float AtmosphereBrightness { get; set; } = 3f;
            }
            
            public class WorldSettings
            {
                [JsonProperty("Resource node spawn multiplier (0.5 = 50% less)")]
                public float ResourceMultiplier { get; set; } = 0.5f;
                
                [JsonProperty("Animal spawn multiplier")]
                public float AnimalMultiplier { get; set; } = 1f;
                
                [JsonProperty("Barrel spawn multiplier")]
                public float BarrelMultiplier { get; set; } = 1f;
                
                [JsonProperty("Crate spawn multiplier")]
                public float CrateMultiplier { get; set; } = 1f;
            }
            
            public class UISettings
            {
                [JsonProperty("Show infection status UI")]
                public bool ShowInfectionUI { get; set; } = true;
                
                [JsonProperty("Show Blood Moon UI")]
                public bool ShowBloodMoonUI { get; set; } = true;
                
                [JsonProperty("Show kill streak notifications")]
                public bool ShowKillStreakNotifications { get; set; } = true;
                
                [JsonProperty("Broadcast individual zombie kills")]
                public bool BroadcastKills { get; set; } = false;
                
                [JsonProperty("Broadcast kill streak milestones")]
                public bool BroadcastKillStreaks { get; set; } = true;
                
                [JsonProperty("Broadcast Blood Moon events")]
                public bool BroadcastBloodMoon { get; set; } = true;
                
                [JsonProperty("Broadcast boss spawns")]
                public bool BroadcastBossSpawns { get; set; } = true;
            }
            
            public class DeathZombieSettings
            {
                [JsonProperty("Enable zombie spawn on player death")]
                public bool Enabled { get; set; } = true;
                
                [JsonProperty("Transfer player gear to zombie (player must kill zombie to recover)")]
                public bool TakePlayerGear { get; set; } = true;
                
                [JsonProperty("Transfer player backpack contents to zombie")]
                public bool TakeBackpack { get; set; } = true;
                
                [JsonProperty("Give zombie a Pitchfork weapon")]
                public bool GivePitchfork { get; set; } = true;
                
                [JsonProperty("Zombie roam radius from death location (meters)")]
                public float RoamRadius { get; set; } = 10f;
                
                [JsonProperty("Zombie lifetime (seconds, 0 = until killed)")]
                public float Lifetime { get; set; } = 0f;
                
                [JsonProperty("Announce death zombie spawn")]
                public bool AnnounceSpawn { get; set; } = true;
            }
            
            public class DebugSettings
            {
                [JsonProperty("Enable debug logging to console")]
                public bool Enabled { get; set; } = false;
            }
            
            public class SmoothSaverSettings
            {
                [JsonProperty("Enable smooth saver system")]
                public bool Enabled { get; set; } = true;
                
                [JsonProperty("Auto-save interval (seconds)")]
                public float AutoSaveInterval { get; set; } = 300f;
                
                [JsonProperty("Save cache size (increase for large servers)")]
                public int SaveCacheSize { get; set; } = 50000;
                
                [JsonProperty("Warn players before save")]
                public bool WarnBeforeSave { get; set; } = false;
                
                [JsonProperty("Warning time before save (seconds)")]
                public float WarnSeconds { get; set; } = 5f;
                
                [JsonProperty("Announce save start")]
                public bool AnnounceStart { get; set; } = false;
                
                [JsonProperty("Announce save complete")]
                public bool AnnounceComplete { get; set; } = false;
                
                [JsonProperty("Skip save if players in combat")]
                public bool SkipIfPlayersInCombat { get; set; } = false;
                
                [JsonProperty("Max combat delay (seconds)")]
                public float MaxCombatDelay { get; set; } = 60f;
                
                [JsonProperty("Cleanup entities before save")]
                public bool CleanupBeforeSave { get; set; } = true;
                
                [JsonProperty("Warning message")]
                public string WarnMessage { get; set; } = "<color=#ffaa00>[SAVE] Server saving in {0} seconds...</color>";
                
                [JsonProperty("Save start message")]
                public string SaveStartMessage { get; set; } = "<color=#ffaa00>[SAVE] Saving...</color>";
                
                [JsonProperty("Save complete message")]
                public string SaveCompleteMessage { get; set; } = "<color=#00ff00>[OK] Saved! ({0} entities in {1}ms)</color>";
                
                [JsonProperty("Limit server FPS")]
                public bool LimitFPS { get; set; } = true;
                
                [JsonProperty("Server FPS limit (30 recommended for stability)")]
                public int FPSLimit { get; set; } = 30;
                

            }
            
            // Legacy property mappings for backwards compatibility
            [JsonIgnore] public bool BlockPVP => PVE.BlockPVP;
            [JsonIgnore] public bool BlockRaiding => PVE.BlockRaiding;
            [JsonIgnore] public bool AllowTeamDamage => PVE.AllowTeamDamage;
            [JsonIgnore] public bool BlockTrapDamage => PVE.BlockTrapDamage;
            [JsonIgnore] public bool ProtectSleepers => PVE.ProtectSleepers;
            [JsonIgnore] public bool BlockPlayerLooting => PVE.BlockPlayerLooting;
            [JsonIgnore] public bool ZombieSpawningEnabled => ZombieSpawn.Enabled;
            [JsonIgnore] public int MinZombiesPerPlayer => ZombieSpawn.MinZombiesSolo;
            [JsonIgnore] public int MaxZombiesPerPlayer => ZombieSpawn.MaxZombiesSolo;
            [JsonIgnore] public int TeamMinZombies => ZombieSpawn.MinZombiesTeam;
            [JsonIgnore] public int TeamMaxZombies => ZombieSpawn.MaxZombiesTeam;
            [JsonIgnore] public float MinSpawnDistance => ZombieSpawn.MinSpawnDistance;
            [JsonIgnore] public float MaxSpawnDistance => ZombieSpawn.MaxSpawnDistance;
            [JsonIgnore] public float MinSpawnInterval => ZombieSpawn.MinSpawnInterval;
            [JsonIgnore] public float MaxSpawnInterval => ZombieSpawn.MaxSpawnInterval;
            [JsonIgnore] public int MaxTotalZombies => ZombieSpawn.MaxTotalZombies;
            [JsonIgnore] public float ZombieDespawnRange => ZombieSpawn.DespawnRange;
            [JsonIgnore] public float CleanupInterval => ZombieSpawn.CleanupInterval;
            [JsonIgnore] public int ZombieRespawnChance => ZombieSpawn.RespawnChance;
            [JsonIgnore] public float ZombieSpeedMultiplier => ZombieCombat.SpeedMultiplier;
            [JsonIgnore] public float ZombieDamageMultiplier => ZombieCombat.DamageMultiplier;
            [JsonIgnore] public float ZombieAggroRange => ZombieCombat.AggroRange;
            [JsonIgnore] public float GunshotAggroRange => ZombieCombat.GunshotAggroRange;
            [JsonIgnore] public int HeadshotInstantKillChance => ZombieCombat.HeadshotKillChance;
            [JsonIgnore] public int BeancanChance => ZombieCombat.BeancanChance;
            [JsonIgnore] public int BleedChance => Infection.BleedChance;
            [JsonIgnore] public int PoisonChance => Infection.PoisonChance;
            [JsonIgnore] public float BleedDamage => Infection.BleedDamage;
            [JsonIgnore] public float PoisonDamage => Infection.PoisonDamage;
            [JsonIgnore] public bool BloodMoonEnabled => BloodMoon.Enabled;
            [JsonIgnore] public float BloodMoonDuration => BloodMoon.Duration;
            [JsonIgnore] public float BloodMoonZombieMultiplier => BloodMoon.ZombieMultiplier;
            [JsonIgnore] public bool BossZombiesEnabled => Boss.Enabled;
            [JsonIgnore] public int BossSpawnChance => Boss.SpawnChance;
            [JsonIgnore] public float BossHealthMultiplier => Boss.HealthMultiplier;
            [JsonIgnore] public float BossDamageMultiplier => Boss.DamageMultiplier;
            [JsonIgnore] public bool ZombieLootEnabled => Loot.Enabled;
            [JsonIgnore] public int ZombieLootChance => Loot.DropChance;
            [JsonIgnore] public bool KillStreakEnabled => KillStreak.Enabled;
            [JsonIgnore] public float KillStreakTimeout => KillStreak.Timeout;
            [JsonIgnore] public int KillStreakMilestone => KillStreak.MilestoneInterval;
            [JsonIgnore] public bool BroadcastKills => UI.BroadcastKills;
            [JsonIgnore] public bool BroadcastKillStreaks => UI.BroadcastKillStreaks;
            [JsonIgnore] public bool NightBoostEnabled => ZombieSpawn.NightBoostEnabled;
            [JsonIgnore] public float NightSpawnMultiplier => ZombieSpawn.NightSpawnMultiplier;
            [JsonIgnore] public bool ZombieSoundsEnabled => ZombieSpawn.SpawnSoundsEnabled;
            [JsonIgnore] public float ResourceNodeMultiplier => World.ResourceMultiplier;
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
            SaveConfig();
        }

        protected override void LoadDefaultConfig() => config = new Configuration();
        protected override void SaveConfig() => Config.WriteObject(config);
        #endregion

        #region Oxide Hooks
        private void OnServerInitialized()
        {
            permission.RegisterPermission(VIP_PERM, this);
            permission.RegisterPermission(ADMIN_PERM, this);
            LoadPlayerStats();
            StartRadiationProtection();
            StartSafeZoneSleeperCheck();
            StartZombieSpawning();
            StartBloodMoonCheck();
            SetupNightVision();
            InitializeSmoothSaver();
            
            Puts("================================================");
            Puts("    ZOMBIE PVE CORE v3.0 - LOADED!");
            Puts("================================================");
            Puts("  [+] PVE Protection System");
            Puts("  [+] Zombie Spawning & Infection");
            Puts("  [+] Blood Moon Events");
            Puts("  [+] Boss Zombies & Hordes");
            Puts("  [+] Loot Drops & Kill Streaks");
            Puts("  [+] Player Statistics & Leaderboards");
            Puts("  [+] VIP Perks & Bonuses");
            Puts("  [+] VIP Night Vision");
            Puts("  [+] Smooth Saver System");
            Puts("================================================");
            Puts("  Type /zhelp in-game for commands");
            Puts("================================================");
        }

        private void Unload()
        {
            SavePlayerStats();
            
            // Unsubscribe from day/night events
            if (TOD_Sky.Instance != null)
            {
                TOD_Sky.Instance.Components.Time.OnSunrise -= OnSunrise;
                TOD_Sky.Instance.Components.Time.OnSunset -= OnSunset;
            }
            
            radiationTimer?.Destroy();
            safeZoneTimer?.Destroy();
            zombieSpawnTimer?.Destroy();
            zombieCleanupTimer?.Destroy();
            ambientZombieTimer?.Destroy();
            bloodMoonTimer?.Destroy();
            espTimer?.Destroy();
            espPlayers.Clear();
            autoSaveTimer?.Destroy();
            
            // Restore default save settings
            if (config.SmoothSaver.Enabled)
            {
                ConVar.Server.saveinterval = 300;
                ConVar.Server.savecachesize = 8000;
                if (config.SmoothSaver.LimitFPS)
                    ConVar.FPS.limit = -1; // Restore to unlimited
                    

            }
            
            // Kill all spawned zombies
            foreach (var zombieId in spawnedZombies)
            {
                var zombie = BaseNetworkable.serverEntities.Find(new NetworkableId(zombieId)) as BasePlayer;
                zombie?.Kill();
            }
            spawnedZombies.Clear();
            
            // Clean up death zombie loot (items will be lost on unload)
            foreach (var loot in deathZombieLoot.Values)
            {
                foreach (var item in loot)
                    item?.Remove();
            }
            deathZombieLoot.Clear();
            
            foreach (var status in playerStatuses.Values)
            {
                status.BleedTimer?.Destroy();
                status.PoisonTimer?.Destroy();
            }
            
            foreach (var player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, BLEED_UI);
                CuiHelper.DestroyUi(player, POISON_UI);
                CuiHelper.DestroyUi(player, HORDE_UI);
                CuiHelper.DestroyUi(player, STATS_UI);
                
                // Reset night vision
                if (nightVisionPlayers.Contains(player.userID))
                    ResetNightVision(player);
            }
            nightVisionPlayers.Clear();
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
            if (player != null)
            {
                nightVisionPlayers.Remove(player.userID);
                espPlayers.Remove(player.userID);
                
                // Stop ESP timer if no one is using it
                if (espPlayers.Count == 0)
                {
                    espTimer?.Destroy();
                    espTimer = null;
                }
            }
            
            if (playerStatuses.TryGetValue(player.userID, out var status))
            {
                status.BleedTimer?.Destroy();
                status.PoisonTimer?.Destroy();
                playerStatuses.Remove(player.userID);
            }
            CuiHelper.DestroyUi(player, BLEED_UI);
            CuiHelper.DestroyUi(player, POISON_UI);
        }

        // Clear status on death
        private void OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            if (player == null) return;
            
            if (playerStatuses.TryGetValue(player.userID, out var status))
            {
                status.IsBleeding = false;
                status.IsPoisoned = false;
                status.BleedTimer?.Destroy();
                status.PoisonTimer?.Destroy();
            }
            CuiHelper.DestroyUi(player, BLEED_UI);
            CuiHelper.DestroyUi(player, POISON_UI);
            
            // Track death to zombie
            var attacker = info?.InitiatorPlayer;
            if (attacker != null && !attacker.userID.IsSteamId())
            {
                var stats = GetPlayerStats(player.userID);
                stats.DeathsToZombies++;
                stats.CurrentKillStreak = 0; // Reset kill streak on death
            }
            
            // Spawn death zombie with player's gear
            if (config.DeathZombie.Enabled && player.userID.IsSteamId())
            {
                SpawnDeathZombie(player);
            }
        }

        // VIP gets full stats on respawn
        private void OnPlayerRespawned(BasePlayer player)
        {
            if (player == null) return;
            
            // Clear any lingering UI
            CuiHelper.DestroyUi(player, BLEED_UI);
            CuiHelper.DestroyUi(player, POISON_UI);
            
            // VIP full stats
            if (permission.UserHasPermission(player.UserIDString, VIP_PERM))
            {
                timer.Once(0.5f, () =>
                {
                    if (player == null || !player.IsConnected) return;
                    player.health = player.MaxHealth();
                    player.metabolism.calories.value = player.metabolism.calories.max;
                    player.metabolism.hydration.value = player.metabolism.hydration.max;
                    SendReply(player, "<color=#44ff44>[VIP] Full health, food & water!</color>");
                });
            }
        }

        // VIP radiation immunity - timer based
        private Timer radiationTimer;
        private Timer safeZoneTimer;

        private void StartRadiationProtection()
        {
            radiationTimer?.Destroy();
            radiationTimer = timer.Every(1f, () =>
            {
                foreach (var player in BasePlayer.activePlayerList)
                {
                    if (player == null || !player.IsConnected) continue;
                    if (!permission.UserHasPermission(player.UserIDString, VIP_PERM)) continue;
                    
                    if (player.metabolism.radiation_poison.value > 0)
                    {
                        player.metabolism.radiation_poison.value = 0;
                        player.metabolism.radiation_poison.SetValue(0);
                    }
                    if (player.metabolism.radiation_level.value > 0)
                    {
                        player.metabolism.radiation_level.value = 0;
                        player.metabolism.radiation_level.SetValue(0);
                    }
                }
            });
        }

        // Kill sleepers in safe zones
        private void StartSafeZoneSleeperCheck()
        {
            safeZoneTimer?.Destroy();
            safeZoneTimer = timer.Every(10f, () =>
            {
                foreach (var player in BasePlayer.sleepingPlayerList)
                {
                    if (player == null || player.IsConnected) continue;
                    if (player.InSafeZone())
                    {
                        player.Die();
                        Puts($"Killed sleeper {player.displayName} in safe zone");
                    }
                }
            });
        }

        private bool IsPVPModeActive()
        {
            var pvpScheduler = plugins.Find("PVPScheduler");
            if (pvpScheduler != null)
            {
                var result = pvpScheduler.Call("IsPVPActive");
                if (result is bool) return (bool)result;
            }
            return false;
        }

        private object CanEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null || info == null) return null;
            
            var victim = entity as BasePlayer;
            var attacker = info?.InitiatorPlayer;
            
            // Allow damage to/from NPCs (zombies)
            if (victim != null && !victim.userID.IsSteamId())
                return true;
            
            if (attacker != null && !attacker.userID.IsSteamId() && victim != null && victim.userID.IsSteamId())
                return true;
            
            // Explicitly allow damage to RaidableBases entities
            if (victim == null && IsRaidableBase(entity))
                return true;
            
            // Don't interfere with non-player entities at all - let other plugins handle them
            if (victim == null)
                return null;
            
            return null;
        }

        private object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null || info == null) return null;
            
            // Check for explosion damage - alert zombies from 300m away
            if (info.damageTypes.GetMajorityDamageType() == Rust.DamageType.Explosion)
            {
                var explosionAttacker = info.InitiatorPlayer;
                if (explosionAttacker != null && explosionAttacker.userID.IsSteamId())
                {
                    AlertZombiesToPosition(entity.transform.position, config.ZombieCombat.ExplosionAggroRange, explosionAttacker);
                }
            }
            
            if (IsPVPModeActive()) return null;
            
            // Only handle damage TO players - let other plugins handle building/entity damage
            BasePlayer victim = entity as BasePlayer;
            if (victim == null)
                return null;
            
            BasePlayer attacker = info?.InitiatorPlayer;

            bool victimIsNPC = victim != null && !victim.userID.IsSteamId();
            bool attackerIsNPC = attacker != null && !attacker.userID.IsSteamId();
            
            // Zombie hit player - check for infection and reduce damage
            // Must verify it's actually a zombie (scarecrow/murderer), not just any NPC
            if (attackerIsNPC && victim != null && victim.userID.IsSteamId())
            {
                // Verify attacker is actually a zombie we spawned
                bool isZombie = false;
                if (attacker != null && attacker.net != null)
                {
                    ulong attackerId = attacker.net.ID.Value;
                    isZombie = spawnedZombies.Contains(attackerId);
                    
                    // Also check by prefab name if not in our list (could be from other sources)
                    if (!isZombie && attacker is ScarecrowNPC)
                        isZombie = true;
                    if (!isZombie && attacker.ShortPrefabName != null && 
                        (attacker.ShortPrefabName.Contains("scarecrow") || attacker.ShortPrefabName.Contains("murderer")))
                        isZombie = true;
                }
                
                if (isZombie)
                {
                    // VIP infection immunity
                    if (!config.VIP.InfectionImmunity || !permission.UserHasPermission(victim.UserIDString, VIP_PERM))
                    {
                        CheckZombieInfection(victim, attacker);
                    }
                    // Reduce zombie damage by configured amount (default 60% less)
                    info.damageTypes.ScaleAll(config.ZombieDamageMultiplier);
                }
                return null;
            }

            if (victimIsNPC || attackerIsNPC)
                return null;

            // PVP protection
            if (victim != null && attacker != null && victim != attacker)
            {
                if (config.BlockPVP)
                {
                    if (config.AllowTeamDamage && victim.currentTeam != 0 && victim.currentTeam == attacker.currentTeam)
                        return null;
                    return true;
                }
            }

            // Sleeper protection
            if (victim != null && victim.IsSleeping() && attacker != null && config.ProtectSleepers)
                return true;

            // Trap damage protection
            if (victim != null && config.BlockTrapDamage)
            {
                if (info.Initiator is AutoTurret || info.Initiator is FlameTurret ||
                    info.Initiator is GunTrap || info.Initiator is Landmine || info.Initiator is BearTrap ||
                    info.Initiator is SamSite)
                {
                    var deployable = info.Initiator as BaseEntity;
                    if (deployable != null && deployable.OwnerID != victim.userID)
                    {
                        // Allow if same team
                        if (victim.currentTeam != 0)
                        {
                            var owner = BasePlayer.FindByID(deployable.OwnerID);
                            if (owner != null && owner.currentTeam == victim.currentTeam)
                                return null;
                        }
                        return true;
                    }
                }
            }

            return null;
        }

        private object CanLootPlayer(BasePlayer target, BasePlayer looter)
        {
            if (!config.BlockPlayerLooting) return null;
            if (target == null || looter == null || target == looter) return null;
            // Allow looting NPCs (bots, zombies, etc.)
            if (!target.userID.IsSteamId()) return null;
            if (target.currentTeam != 0 && target.currentTeam == looter.currentTeam) return null;
            if (IsPVPModeActive()) return null;
            SendReply(looter, "<color=#ff4444>You cannot loot other players in PVE mode.</color>");
            return false;
        }
        #endregion

        #region Zombie Infection System
        private void CheckZombieInfection(BasePlayer victim, BasePlayer zombie)
        {
            if (!playerStatuses.TryGetValue(victim.userID, out var status))
            {
                status = new PlayerStatus();
                playerStatuses[victim.userID] = status;
            }

            // If already bleeding, check for poison
            if (status.IsBleeding)
            {
                if (!status.IsPoisoned && UnityEngine.Random.Range(0, 100) < config.PoisonChance)
                {
                    ApplyPoison(victim, status);
                }
            }
            else
            {
                // Check for bleed
                if (UnityEngine.Random.Range(0, 100) < config.BleedChance)
                {
                    ApplyBleed(victim, status);
                }
            }

            // Zombie beancan throw chance
            if (UnityEngine.Random.Range(0, 100) < config.BeancanChance)
            {
                ThrowBeancan(zombie, victim);
            }
        }

        private void ApplyBleed(BasePlayer player, PlayerStatus status)
        {
            status.IsBleeding = true;
            SendReply(player, "<color=#ff4444>[BLEED] You are BLEEDING! Use a bandage to stop it!</color>");
            ShowBleedUI(player);

            status.BleedTimer?.Destroy();
            status.BleedTimer = timer.Every(3f, () =>
            {
                if (player == null || !player.IsConnected || player.IsDead())
                {
                    status.BleedTimer?.Destroy();
                    return;
                }
                if (!status.IsBleeding)
                {
                    status.BleedTimer?.Destroy();
                    return;
                }
                player.Hurt(config.BleedDamage, Rust.DamageType.Bleeding);
            });
        }

        private void ApplyPoison(BasePlayer player, PlayerStatus status)
        {
            status.IsPoisoned = true;
            SendReply(player, "<color=#44ff44>[POISON] You are POISONED! Use a medical syringe to cure it!</color>");
            ShowPoisonUI(player);

            status.PoisonTimer?.Destroy();
            status.PoisonTimer = timer.Every(4f, () =>
            {
                if (player == null || !player.IsConnected || player.IsDead())
                {
                    status.PoisonTimer?.Destroy();
                    return;
                }
                if (!status.IsPoisoned)
                {
                    status.PoisonTimer?.Destroy();
                    return;
                }
                player.Hurt(config.PoisonDamage, Rust.DamageType.Poison);
            });
        }

        private void ThrowBeancan(BasePlayer zombie, BasePlayer target)
        {
            if (zombie == null || target == null) return;

            Vector3 direction = (target.transform.position - zombie.eyes.position).normalized;
            Vector3 spawnPos = zombie.eyes.position + direction * 1f;

            var beancan = GameManager.server.CreateEntity("assets/prefabs/weapons/beancan grenade/grenade.beancan.deployed.prefab", spawnPos) as ThrownWeapon;
            if (beancan != null)
            {
                beancan.creatorEntity = zombie;
                beancan.Spawn();
                beancan.SetVelocity(direction * 15f + Vector3.up * 5f);
            }
        }
        #endregion

        #region Item Usage Detection
        private void OnItemUse(Item item, int amount)
        {
            if (item?.parent?.playerOwner == null) return;
            var player = item.parent.playerOwner;

            if (!playerStatuses.TryGetValue(player.userID, out var status)) return;

            // Bandage cures bleeding
            if (item.info.shortname == "bandage" && status.IsBleeding)
            {
                status.IsBleeding = false;
                status.BleedTimer?.Destroy();
                CuiHelper.DestroyUi(player, BLEED_UI);
                SendReply(player, "<color=#44ff44>[OK] Bleeding stopped!</color>");
            }

            // Medical syringe cures poison AND bleed
            if (item.info.shortname == "syringe.medical")
            {
                bool curedSomething = false;
                bool curedPoison = false;
                if (status.IsPoisoned)
                {
                    status.IsPoisoned = false;
                    status.PoisonTimer?.Destroy();
                    CuiHelper.DestroyUi(player, POISON_UI);
                    curedSomething = true;
                    curedPoison = true;
                }
                if (status.IsBleeding)
                {
                    status.IsBleeding = false;
                    status.BleedTimer?.Destroy();
                    CuiHelper.DestroyUi(player, BLEED_UI);
                    curedSomething = true;
                }
                if (curedSomething)
                {
                    SendReply(player, "<color=#44ff44>[OK] Poison & bleeding cured!</color>");
                    if (curedPoison)
                    {
                        var stats = GetPlayerStats(player.userID);
                        stats.InfectionsSurvived++;
                    }
                }
            }
        }

        private void OnHealingItemUse(MedicalTool tool, BasePlayer player)
        {
            if (player == null) return;
            if (!playerStatuses.TryGetValue(player.userID, out var status)) return;

            var item = tool.GetItem();
            if (item == null) return;

            if (item.info.shortname == "bandage" && status.IsBleeding)
            {
                status.IsBleeding = false;
                status.BleedTimer?.Destroy();
                CuiHelper.DestroyUi(player, BLEED_UI);
                SendReply(player, "<color=#44ff44>[OK] Bleeding stopped!</color>");
            }

            // Medical syringe cures poison AND bleed
            if (item.info.shortname == "syringe.medical")
            {
                bool curedSomething = false;
                if (status.IsPoisoned)
                {
                    status.IsPoisoned = false;
                    status.PoisonTimer?.Destroy();
                    CuiHelper.DestroyUi(player, POISON_UI);
                    curedSomething = true;
                }
                if (status.IsBleeding)
                {
                    status.IsBleeding = false;
                    status.BleedTimer?.Destroy();
                    CuiHelper.DestroyUi(player, BLEED_UI);
                    curedSomething = true;
                }
                if (curedSomething)
                    SendReply(player, "<color=#44ff44>[OK] Poison & bleeding cured!</color>");
            }
        }
        #endregion


        #region UI
        private void ShowBleedUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, BLEED_UI);

            var elements = new CuiElementContainer();
            
            // Main container - positioned left of center, above hotbar
            elements.Add(new CuiPanel
            {
                Image = { Color = "0 0 0 0" },
                RectTransform = { AnchorMin = "0.395 0.115", AnchorMax = "0.465 0.155" },
                CursorEnabled = false
            }, "Overlay", BLEED_UI);

            // Dark background with red tint
            elements.Add(new CuiPanel
            {
                Image = { Color = "0.15 0.05 0.05 0.9" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
            }, BLEED_UI, BLEED_UI + "_bg");

            // Red accent bar on left
            elements.Add(new CuiPanel
            {
                Image = { Color = "0.8 0.2 0.2 1" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "0.06 1" }
            }, BLEED_UI + "_bg");

            // Pulsing red glow effect
            elements.Add(new CuiPanel
            {
                Image = { Color = "0.6 0.15 0.15 0.3" },
                RectTransform = { AnchorMin = "0.06 0", AnchorMax = "1 1" }
            }, BLEED_UI + "_bg");

            // Blood drop icon
            elements.Add(new CuiLabel
            {
                Text = { Text = "!", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0.08 0", AnchorMax = "0.35 1" }
            }, BLEED_UI + "_bg");

            // BLEED text
            elements.Add(new CuiLabel
            {
                Text = { Text = "BLEED", FontSize = 11, Align = TextAnchor.MiddleCenter, Color = "1 0.4 0.4 1", Font = "robotocondensed-bold.ttf" },
                RectTransform = { AnchorMin = "0.35 0", AnchorMax = "1 1" }
            }, BLEED_UI + "_bg");

            CuiHelper.AddUi(player, elements);
        }

        private void ShowPoisonUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, POISON_UI);

            var elements = new CuiElementContainer();
            
            // Main container - positioned right of center, above hotbar
            elements.Add(new CuiPanel
            {
                Image = { Color = "0 0 0 0" },
                RectTransform = { AnchorMin = "0.535 0.115", AnchorMax = "0.615 0.155" },
                CursorEnabled = false
            }, "Overlay", POISON_UI);

            // Dark background with green tint
            elements.Add(new CuiPanel
            {
                Image = { Color = "0.05 0.12 0.05 0.9" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
            }, POISON_UI, POISON_UI + "_bg");

            // Green accent bar on left
            elements.Add(new CuiPanel
            {
                Image = { Color = "0.2 0.7 0.2 1" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "0.06 1" }
            }, POISON_UI + "_bg");

            // Pulsing green glow effect
            elements.Add(new CuiPanel
            {
                Image = { Color = "0.15 0.4 0.15 0.3" },
                RectTransform = { AnchorMin = "0.06 0", AnchorMax = "1 1" }
            }, POISON_UI + "_bg");

            // Skull icon
            elements.Add(new CuiLabel
            {
                Text = { Text = "!", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "0.4 1 0.4 1" },
                RectTransform = { AnchorMin = "0.08 0", AnchorMax = "0.35 1" }
            }, POISON_UI + "_bg");

            // POISON text
            elements.Add(new CuiLabel
            {
                Text = { Text = "POISON", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = "0.5 1 0.5 1", Font = "robotocondensed-bold.ttf" },
                RectTransform = { AnchorMin = "0.32 0", AnchorMax = "1 1" }
            }, POISON_UI + "_bg");

            CuiHelper.AddUi(player, elements);
        }
        #endregion

        #region Zombie Respawn
        
        // Combined OnEntitySpawned - handles corpse removal, loot bag conversion, and resource control
        private void OnEntitySpawned(BaseNetworkable entity)
        {
            if (entity == null) return;
            
            // Remove zombie corpses
            var corpse = entity as NPCPlayerCorpse;
            if (corpse != null)
            {
                // Check if this corpse belongs to a zombie (NPC, not player)
                if (corpse.playerSteamID != 0 && !IsPlayerSteamId(corpse.playerSteamID))
                {
                    // This is an NPC corpse - remove it immediately
                    timer.Once(0.01f, () =>
                    {
                        if (corpse != null && !corpse.IsDestroyed)
                            corpse.Kill();
                    });
                }
                return;
            }
            
            // Convert NPC loot bags/backpacks to ground items
            var lootBag = entity as DroppedItemContainer;
            if (lootBag != null)
            {
                // Check if this is from an NPC (not a player) OR is a backpack with no valid player owner
                bool isNpcLoot = false;
                
                // Check by steam ID
                if (lootBag.playerSteamID != 0 && !IsPlayerSteamId(lootBag.playerSteamID))
                    isNpcLoot = true;
                
                // Also check if it's a backpack with playerSteamID of 0 (NPC default)
                if (lootBag.playerSteamID == 0 && lootBag.ShortPrefabName.Contains("backpack"))
                    isNpcLoot = true;
                    
                // Check if playerName contains "Zombie" or similar
                if (lootBag.playerName != null && (lootBag.playerName.Contains("Zombie") || lootBag.playerName.Contains("Scarecrow") || lootBag.playerName.Contains("Murderer")))
                    isNpcLoot = true;
                
                if (isNpcLoot)
                {
                    // This is an NPC loot bag - convert to ground items
                    timer.Once(0.01f, () =>
                    {
                        if (lootBag == null || lootBag.IsDestroyed) return;
                        if (lootBag.inventory == null || lootBag.inventory.itemList.Count == 0)
                        {
                            lootBag.Kill();
                            return;
                        }
                        
                        Vector3 pos = lootBag.transform.position;
                        var items = lootBag.inventory.itemList.ToList();
                        
                        // Only drop 1 random item, destroy the rest
                        if (items.Count > 0)
                        {
                            // Pick one random item to drop
                            int randomIndex = UnityEngine.Random.Range(0, items.Count);
                            
                            for (int i = 0; i < items.Count; i++)
                            {
                                var item = items[i];
                                if (item == null) continue;
                                item.RemoveFromContainer();
                                
                                if (i == randomIndex)
                                {
                                    // Drop this one item
                                    Vector3 dropPos = pos + Vector3.up * 0.3f;
                                    item.Drop(dropPos, Vector3.up * 2f + UnityEngine.Random.insideUnitSphere * 0.3f);
                                }
                                else
                                {
                                    // Destroy other items
                                    item.Remove();
                                }
                            }
                        }
                        
                        // Remove the bag
                        lootBag.Kill();
                    });
                }
                return;
            }
            
            // Resource node control
            if (config.ResourceNodeMultiplier < 1f)
            {
                if (entity is OreResourceEntity || entity.ShortPrefabName.Contains("ore_") || 
                    entity.ShortPrefabName.Contains("stone-") || entity.ShortPrefabName.Contains("sulfur-") ||
                    entity.ShortPrefabName.Contains("metal-"))
                {
                    float removeChance = 1f - config.ResourceNodeMultiplier;
                    if (UnityEngine.Random.Range(0f, 1f) < removeChance)
                        entity.Kill();
                    return;
                }
            }
            
            // Animal spawn control
            if (config.World.AnimalMultiplier < 1f && entity is BaseAnimalNPC)
            {
                float removeChance = 1f - config.World.AnimalMultiplier;
                if (UnityEngine.Random.Range(0f, 1f) < removeChance)
                    entity.Kill();
                return;
            }
            
            // Barrel spawn control
            if (config.World.BarrelMultiplier < 1f && entity.ShortPrefabName.Contains("barrel"))
            {
                float removeChance = 1f - config.World.BarrelMultiplier;
                if (UnityEngine.Random.Range(0f, 1f) < removeChance)
                    entity.Kill();
            }
        }
        
        private bool IsPlayerSteamId(ulong id)
        {
            return id >= 76561197960265728UL; // Steam ID range
        }
        
        private void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null) return;

            var npc = entity as BasePlayer;
            if (npc == null || npc.userID.IsSteamId()) return;

            var killer = info?.InitiatorPlayer;
            if (killer == null || !killer.userID.IsSteamId()) return;

            bool wasHeadshot = info?.HitBone == StringPool.Get("head") || info?.boneArea == HitArea.Head;
            
            // Track kill stats
            ProcessZombieKill(killer, wasHeadshot);
            
            // Check if this is a death zombie with player loot
            ulong zombieId = npc.net?.ID.Value ?? 0;
            bool isDeathZombie = false;
            if (zombieId != 0 && deathZombieLoot.TryGetValue(zombieId, out var playerLoot))
            {
                // Drop player's items in a backpack (death zombies keep backpack for player gear)
                DropDeathZombieLoot(entity.transform.position, playerLoot, killer);
                deathZombieLoot.Remove(zombieId);
                isDeathZombie = true;
            }
            else
            {
                // Regular zombie - drop loot directly on ground (no bag)
                DropZombieLoot(entity.transform.position, killer);
            }
            
            // Remove from tracking
            if (npc.net != null)
                spawnedZombies.Remove(npc.net.ID.Value);
            
            // Broadcast kill if enabled
            if (config.BroadcastKills)
            {
                string killType = wasHeadshot ? "<color=#ffcc00>HEADSHOT!</color>" : "";
                BroadcastToAll($"<color=#888888>{killer.displayName} killed a zombie {killType}</color>");
            }
            
            // Headshot prevents respawn (don't respawn death zombies)
            if (wasHeadshot || isDeathZombie) return;

            if (UnityEngine.Random.Range(0, 100) >= config.ZombieRespawnChance) return;

            Vector3 spawnPos = entity.transform.position;
            timer.Once(0.5f, () => SpawnZombie(spawnPos, killer));
        }
        
        private void DropDeathZombieLoot(Vector3 position, List<Item> items, BasePlayer killer)
        {
            if (items == null || items.Count == 0) return;
            
            // Create a backpack to hold the player's items
            var backpack = GameManager.server.CreateEntity("assets/prefabs/misc/item drop/item_drop_backpack.prefab", position + Vector3.up * 0.5f) as DroppedItemContainer;
            if (backpack == null) return;
            
            backpack.lootPanelName = "generic_resizable";
            backpack.playerSteamID = killer?.userID ?? 0;
            backpack.playerName = "Zombie Loot";
            
            // Calculate required capacity
            int capacity = Math.Max(items.Count, 36);
            backpack.inventory = new ItemContainer();
            backpack.inventory.ServerInitialize(null, capacity);
            backpack.inventory.GiveUID();
            backpack.inventory.entityOwner = backpack;
            backpack.inventory.SetFlag(ItemContainer.Flag.NoItemInput, true);
            
            backpack.Spawn();
            
            // Move all items to the backpack
            foreach (var item in items)
            {
                if (item != null && !item.MoveToContainer(backpack.inventory))
                {
                    item.DropAndTossUpwards(position);
                }
            }
            
            // Notify killer
            if (killer != null)
            {
                SendReply(killer, $"<color=#44ff44>The zombie dropped a backpack with {items.Count} items!</color>");
            }
        }

        private void SpawnZombie(Vector3 position, BasePlayer target)
        {
            var zombie = GameManager.server.CreateEntity("assets/prefabs/npc/scarecrow/scarecrow.prefab", position, Quaternion.identity);
            if (zombie == null)
                zombie = GameManager.server.CreateEntity("assets/prefabs/npc/murderer/murderer.prefab", position, Quaternion.identity);
            
            if (zombie != null)
            {
                zombie.Spawn();
                var npc = zombie as BasePlayer;
                if (npc != null)
                {
                    npc.displayName = "Risen Zombie";
                    
                    // Only clear belt items, NOT wear items (keep the scarecrow/murderer look)
                    if (npc.inventory?.containerBelt != null)
                    {
                        foreach (var item in npc.inventory.containerBelt.itemList.ToList())
                            item.Remove();
                    }
                    
                    // Give pitchfork weapon
                    var weapon = ItemManager.CreateByName("pitchfork", 1);
                    if (weapon != null)
                    {
                        if (!weapon.MoveToContainer(npc.inventory.containerBelt, 0))
                            weapon.Remove();
                        else
                            npc.UpdateActiveItem(weapon.uid);
                    }
                    
                    // Track as spawned zombie
                    if (npc.net != null)
                        spawnedZombies.Add(npc.net.ID.Value);
                }

                if (target != null)
                    SendReply(target, "<color=#ff4444>[!] The corpse rises as a ZOMBIE!</color>");
            }
        }
        
        private void SpawnDeathZombie(BasePlayer deadPlayer)
        {
            if (deadPlayer == null) return;
            
            Vector3 deathPosition = deadPlayer.transform.position;
            string playerName = deadPlayer.displayName;
            
            // Collect all player's items to store for when zombie dies
            List<Item> allPlayerItems = new List<Item>();
            List<Item> wearItems = new List<Item>();
            
            if (config.DeathZombie.TakePlayerGear)
            {
                // Take wear items from player (these go on the zombie visually)
                if (deadPlayer.inventory?.containerWear != null)
                {
                    foreach (var item in deadPlayer.inventory.containerWear.itemList.ToList())
                    {
                        item.RemoveFromContainer();
                        wearItems.Add(item);
                    }
                }
                
                // Take belt items from player
                if (deadPlayer.inventory?.containerBelt != null)
                {
                    foreach (var item in deadPlayer.inventory.containerBelt.itemList.ToList())
                    {
                        item.RemoveFromContainer();
                        allPlayerItems.Add(item);
                    }
                }
                
                // Take main inventory items from player
                if (deadPlayer.inventory?.containerMain != null)
                {
                    foreach (var item in deadPlayer.inventory.containerMain.itemList.ToList())
                    {
                        item.RemoveFromContainer();
                        allPlayerItems.Add(item);
                    }
                }
            }
            
            // Take backpack items (Backpacks plugin integration)
            if (config.DeathZombie.TakeBackpack)
            {
                var backpacksPlugin = plugins.Find("Backpacks");
                if (backpacksPlugin != null)
                {
                    var backpackContainer = backpacksPlugin.Call("API_GetBackpackContainer", deadPlayer.userID) as ItemContainer;
                    if (backpackContainer != null)
                    {
                        foreach (var item in backpackContainer.itemList.ToList())
                        {
                            item.RemoveFromContainer();
                            allPlayerItems.Add(item);
                        }
                    }
                }
            }
            
            // Spawn zombie after a short delay
            timer.Once(1f, () =>
            {
                var zombie = GameManager.server.CreateEntity("assets/prefabs/npc/scarecrow/scarecrow.prefab", deathPosition, Quaternion.identity) as ScarecrowNPC;
                if (zombie == null) return;
                
                zombie.Spawn();
                
                if (zombie.net != null)
                {
                    spawnedZombies.Add(zombie.net.ID.Value);
                    
                    // Store all player items to drop when zombie dies
                    if (allPlayerItems.Count > 0 || wearItems.Count > 0)
                    {
                        var lootList = new List<Item>();
                        lootList.AddRange(allPlayerItems);
                        lootList.AddRange(wearItems);
                        deathZombieLoot[zombie.net.ID.Value] = lootList;
                    }
                }
                
                zombie.displayName = $"<color=#ff4444>Zombie {playerName}</color>";
                
                // Strip default items
                zombie.inventory?.Strip();
                
                // Give pitchfork (will be active weapon)
                if (config.DeathZombie.GivePitchfork)
                {
                    var pitchfork = ItemManager.CreateByName("pitchfork", 1);
                    if (pitchfork != null)
                    {
                        if (!pitchfork.MoveToContainer(zombie.inventory.containerBelt, 0))
                            pitchfork.Remove();
                        else
                            zombie.UpdateActiveItem(pitchfork.uid);
                    }
                }
                
                // Equip player's wear items on zombie (visual only - actual items stored in deathZombieLoot)
                foreach (var item in wearItems)
                {
                    // Create a copy for visual display on zombie
                    var visualCopy = ItemManager.CreateByItemID(item.info.itemid, 1, item.skin);
                    if (visualCopy != null)
                    {
                        if (!visualCopy.MoveToContainer(zombie.inventory.containerWear))
                            visualCopy.Remove();
                    }
                }
                
                // Configure zombie brain to stay within roam radius
                timer.Once(0.5f, () =>
                {
                    if (zombie == null || zombie.IsDestroyed) return;
                    
                    var brain = zombie.GetComponent<ScarecrowBrain>();
                    if (brain != null)
                    {
                        brain.SenseRange = config.DeathZombie.RoamRadius;
                        brain.TargetLostRange = config.DeathZombie.RoamRadius + 5f;
                        
                        if (brain.Navigator != null)
                        {
                            brain.Navigator.MaxRoamDistanceFromHome = config.DeathZombie.RoamRadius;
                            brain.Navigator.BestRoamPointMaxDistance = config.DeathZombie.RoamRadius;
                        }
                        
                        // Set home position to death location
                        brain.Events.Memory.Position.Set(deathPosition, 0);
                    }
                });
                
                // Set lifetime if configured
                if (config.DeathZombie.Lifetime > 0)
                {
                    timer.Once(config.DeathZombie.Lifetime, () =>
                    {
                        if (zombie != null && !zombie.IsDestroyed)
                        {
                            if (zombie.net != null)
                                spawnedZombies.Remove(zombie.net.ID.Value);
                            zombie.Kill();
                        }
                    });
                }
                
                // Announce spawn
                if (config.DeathZombie.AnnounceSpawn)
                {
                    BroadcastToAll($"<color=#ff4444>[DEATH] {playerName} has risen as a ZOMBIE!</color>");
                }
                
                // Play spawn sound
                if (config.ZombieSoundsEnabled)
                {
                    Effect.server.Run("assets/bundled/prefabs/fx/player/howl.prefab", deathPosition);
                }
            });
        }
        #endregion

        #region VIP Vehicle Fuel Reduction
        // VIP players use 50% less fuel on all vehicles
        private object OnFuelConsume(EntityFuelSystem fuelSystem, float fuelUsed, float seconds)
        {
            var container = fuelSystem.GetFuelContainer();
            if (container == null) return null;
            
            var vehicle = container.GetParentEntity() as BaseVehicle;
            if (vehicle == null) return null;

            var driver = vehicle.GetDriver();
            if (driver == null || !driver.userID.IsSteamId()) return null;
            
            // VIP gets half fuel consumption
            if (permission.UserHasPermission(driver.UserIDString, VIP_PERM))
            {
                return fuelUsed * 0.5f;
            }
            
            return null;
        }
        #endregion

        #region Resource Control
        // Note: OnEntitySpawned is defined above in Zombie Respawn region - resource control moved there
        
        // Decay control
        private object OnEntityTakeDamage_Decay(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null || info == null) return null;
            if (!info.damageTypes.Has(Rust.DamageType.Decay)) return null;
            
            if (!config.PVE.AllowDecay)
                return true; // Block all decay
            
            if (config.PVE.DecayMultiplier != 1f)
            {
                info.damageTypes.Scale(Rust.DamageType.Decay, config.PVE.DecayMultiplier);
            }
            
            return null;
        }
        #endregion

        #region Zombie Spawning System
        private void StartZombieSpawning()
        {
            if (!config.ZombieSpawningEnabled) return;
            
            ScheduleNextSpawn();
            
            // Start cleanup timer
            zombieCleanupTimer?.Destroy();
            zombieCleanupTimer = timer.Every(config.CleanupInterval, CleanupDistantZombies);

            // Ambient zombie spawning - keeps a constant low number of zombies around the map
            ambientZombieTimer?.Destroy();
            ambientZombieTimer = timer.Every(30f, () =>
            {
                // Keep a minimum number of zombies always spawned around players
                int targetAmbientZombies = Math.Max(5, BasePlayer.activePlayerList.Count * 2);
                targetAmbientZombies = Math.Min(targetAmbientZombies, config.MaxTotalZombies / 2);
                
                if (spawnedZombies.Count < targetAmbientZombies)
                {
                    int toSpawn = Math.Min(3, targetAmbientZombies - spawnedZombies.Count);
                    
                    foreach (var player in BasePlayer.activePlayerList)
                    {
                        if (toSpawn <= 0) break;
                        if (player == null || !player.IsConnected || player.IsDead()) continue;
                        if (player.IsAdmin || player.IsSleeping()) continue;
                        
                        Vector3 spawnPos = GetSpawnPosition(player.transform.position);
                        if (spawnPos != Vector3.zero)
                        {
                            SpawnZombieAtPosition(spawnPos);
                            toSpawn--;
                        }
                    }
                }
            });
        }
        
        private void DebugLog(string message)
        {
            if (config.Debug.Enabled)
                Puts($"[ZombiePVE] {message}");
        }

        private void ScheduleNextSpawn()
        {
            zombieSpawnTimer?.Destroy();
            
            // Random interval based on config (12-48 seconds by default)
            float nextSpawn = UnityEngine.Random.Range(config.MinSpawnInterval, config.MaxSpawnInterval);
            
            zombieSpawnTimer = timer.Once(nextSpawn, () =>
            {
                DoZombieSpawn();
                ScheduleNextSpawn(); // Schedule next random spawn
            });
            
            DebugLog($"Next zombie spawn in {nextSpawn:F1} seconds ({spawnedZombies.Count}/{config.MaxTotalZombies} active)");
        }

        private void DoZombieSpawn()
        {
            // Clean up dead zombies from tracking
            int beforeCleanup = spawnedZombies.Count;
            spawnedZombies.RemoveWhere(id => 
            {
                var z = BaseNetworkable.serverEntities.Find(new NetworkableId(id));
                return z == null || (z as BaseCombatEntity)?.IsDead() == true;
            });
            
            if (beforeCleanup != spawnedZombies.Count)
                DebugLog($"Cleaned up {beforeCleanup - spawnedZombies.Count} dead zombies");

            int maxZombies = config.MaxTotalZombies;
            
            // Night boost - allow more zombies at night
            if (config.NightBoostEnabled && IsNightTime())
            {
                maxZombies = (int)(maxZombies * config.NightSpawnMultiplier);
            }
            
            if (spawnedZombies.Count >= maxZombies)
            {
                DebugLog($"Max zombies reached ({spawnedZombies.Count}/{maxZombies}), skipping spawn");
                return;
            }

            // Track which players we've already spawned for (to avoid double-spawning for teams)
            HashSet<ulong> processedPlayers = new HashSet<ulong>();
            int playersProcessed = 0;

            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player == null || !player.IsConnected || player.IsDead()) continue;
                if (player.IsSleeping()) continue;
                if (processedPlayers.Contains(player.userID)) continue;
                
                // Check team size
                int teamSize = GetTeamSize(player);
                processedPlayers.Add(player.userID);
                playersProcessed++;
                
                // Mark all teammates as processed
                if (player.currentTeam != 0)
                {
                    foreach (var p in BasePlayer.activePlayerList)
                    {
                        if (p.currentTeam == player.currentTeam)
                            processedPlayers.Add(p.userID);
                    }
                }
                
                SpawnZombiesAroundPlayer(player, teamSize);
            }
            
            DebugLog($"Spawn cycle complete - processed {playersProcessed} players, {spawnedZombies.Count} zombies active");
        }

        private int GetTeamSize(BasePlayer player)
        {
            if (player.currentTeam == 0) return 1;
            
            int count = 0;
            foreach (var p in BasePlayer.activePlayerList)
            {
                if (p.currentTeam == player.currentTeam && p.IsConnected && !p.IsDead())
                    count++;
            }
            return count;
        }

        private void CleanupDistantZombies()
        {
            List<ulong> toRemove = new List<ulong>();
            
            foreach (var zombieId in spawnedZombies)
            {
                // Never despawn death zombies - they have player loot
                if (deathZombieLoot.ContainsKey(zombieId))
                    continue;
                
                var zombie = BaseNetworkable.serverEntities.Find(new NetworkableId(zombieId)) as BasePlayer;
                if (zombie == null || zombie.IsDead())
                {
                    toRemove.Add(zombieId);
                    continue;
                }
                
                // Check if any player is within range
                bool playerNearby = false;
                foreach (var player in BasePlayer.activePlayerList)
                {
                    if (player == null || !player.IsConnected) continue;
                    
                    float distance = Vector3.Distance(zombie.transform.position, player.transform.position);
                    if (distance <= config.ZombieDespawnRange)
                    {
                        playerNearby = true;
                        break;
                    }
                }
                
                // No player nearby - despawn
                if (!playerNearby)
                {
                    zombie.Kill();
                    toRemove.Add(zombieId);
                }
            }
            
            foreach (var id in toRemove)
                spawnedZombies.Remove(id);
        }

        private void SpawnZombiesAroundPlayer(BasePlayer player, int teamSize)
        {
            // Don't spawn zombies for players inside their base
            if (IsPlayerInsideBase(player))
            {
                DebugLog($"Skipping spawn for {player.displayName} - inside base");
                return;
            }
            
            // Check how many zombies are already near this player
            int nearbyZombies = CountZombiesNearPlayer(player);
            int maxNearby = config.ZombieSpawn.MaxZombiesPerPlayer;
            
            if (nearbyZombies >= maxNearby)
            {
                DebugLog($"Skipping spawn for {player.displayName} - already has {nearbyZombies}/{maxNearby} zombies nearby");
                return;
            }
            
            int minCount, maxCount;
            
            // Teams of 2+ get more zombies
            if (teamSize >= 2)
            {
                minCount = config.TeamMinZombies;
                maxCount = config.TeamMaxZombies;
            }
            else
            {
                minCount = config.MinZombiesPerPlayer;
                maxCount = config.MaxZombiesPerPlayer;
            }
            
            // Limit spawn count to not exceed max per player
            int availableSlots = maxNearby - nearbyZombies;
            int count = UnityEngine.Random.Range(minCount, maxCount + 1);
            count = Math.Min(count, availableSlots);
            
            if (count <= 0) return;
            
            int spawned = 0;
            int failedPositions = 0;
            
            for (int i = 0; i < count; i++)
            {
                if (spawnedZombies.Count >= config.MaxTotalZombies) break;
                
                Vector3 spawnPos = GetSpawnPosition(player.transform.position);
                if (spawnPos == Vector3.zero)
                {
                    failedPositions++;
                    continue;
                }
                
                SpawnZombieAtPosition(spawnPos);
                spawned++;
            }
            
            if (spawned > 0 || failedPositions > 0)
                DebugLog($"Spawned {spawned} zombies around {player.displayName} ({failedPositions} failed positions)");
        }
        
        private int CountZombiesNearPlayer(BasePlayer player)
        {
            if (player == null) return 0;
            
            int count = 0;
            float checkRange = config.MaxSpawnDistance + 50f; // Check slightly beyond spawn range
            
            foreach (var zombieId in spawnedZombies)
            {
                var zombie = BaseNetworkable.serverEntities.Find(new NetworkableId(zombieId)) as BasePlayer;
                if (zombie == null || zombie.IsDead()) continue;
                
                float distance = Vector3.Distance(zombie.transform.position, player.transform.position);
                if (distance <= checkRange)
                    count++;
            }
            
            return count;
        }
        
        private bool IsPlayerInsideBase(BasePlayer player)
        {
            if (player == null) return false;
            
            // Check if player has building privilege (is near their TC)
            var priv = player.GetBuildingPrivilege();
            if (priv == null || !priv.IsAuthed(player)) return false;
            
            // Check if there's a roof/floor above them
            Vector3 pos = player.transform.position + Vector3.up * 0.5f;
            RaycastHit hit;
            
            if (Physics.Raycast(pos, Vector3.up, out hit, 20f, LayerMask.GetMask("Construction")))
            {
                return true; // Has ceiling - inside base
            }
            
            // Check for walls around (at least 2 walls = inside)
            int wallCount = 0;
            Vector3[] directions = { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };
            
            foreach (var dir in directions)
            {
                if (Physics.Raycast(pos, dir, out hit, 5f, LayerMask.GetMask("Construction")))
                {
                    wallCount++;
                }
            }
            
            return wallCount >= 2;
        }

        private Vector3 GetSpawnPosition(Vector3 center)
        {
            for (int attempts = 0; attempts < 10; attempts++)
            {
                float distance = UnityEngine.Random.Range(config.MinSpawnDistance, config.MaxSpawnDistance);
                float angle = UnityEngine.Random.Range(0f, 360f) * Mathf.Deg2Rad;
                
                Vector3 offset = new Vector3(Mathf.Cos(angle) * distance, 0, Mathf.Sin(angle) * distance);
                Vector3 pos = center + offset;
                
                // Find ground
                RaycastHit hit;
                if (Physics.Raycast(pos + Vector3.up * 100f, Vector3.down, out hit, 200f, LayerMask.GetMask("Terrain", "World")))
                {
                    pos = hit.point;
                    
                    // Check if valid spawn (not in water, not inside building)
                    if (!WaterLevel.Test(pos, true, true) && !IsInsideBuilding(pos))
                    {
                        return pos;
                    }
                }
            }
            return Vector3.zero;
        }

        private bool IsInsideBuilding(Vector3 pos)
        {
            return Physics.CheckSphere(pos + Vector3.up, 1f, LayerMask.GetMask("Construction"));
        }

        private void SpawnZombieAtPosition(Vector3 position)
        {
            // Use scarecrow prefab - it's the standard zombie NPC in Rust
            string prefab = "assets/prefabs/npc/scarecrow/scarecrow.prefab";
            var entity = GameManager.server.CreateEntity(prefab, position, Quaternion.identity);
            if (entity == null)
            {
                PrintWarning($"Failed to create zombie entity at {position}");
                return;
            }
            
            entity.Spawn();
            
            // Track this zombie (check net exists)
            if (entity.net != null)
                spawnedZombies.Add(entity.net.ID.Value);
            
            // Set display name if it's a ScarecrowNPC
            var scarecrow = entity as ScarecrowNPC;
            if (scarecrow != null)
                scarecrow.displayName = zombieNames[UnityEngine.Random.Range(0, zombieNames.Length)];
            
            // Night boost - zombies are faster and stronger at night
            float speedMult = config.ZombieSpeedMultiplier;
            float aggroRange = config.ZombieAggroRange;
            
            if (config.NightBoostEnabled && IsNightTime())
            {
                speedMult *= 1.3f;
                aggroRange *= 1.5f;
            }
            
            // Blood moon boost
            if (isBloodMoon)
            {
                speedMult *= 1.5f;
                aggroRange *= 2f;
            }
            
            // Delay brain setup to ensure it's initialized
            timer.Once(0.5f, () =>
            {
                if (entity == null || entity.IsDestroyed) return;
                
                var brain = entity.GetComponent<ScarecrowBrain>();
                if (brain != null && brain.Navigator != null)
                {
                    brain.Navigator.Speed = 5f * speedMult;
                    brain.SenseRange = aggroRange;
                    brain.TargetLostRange = aggroRange + 10f;
                }
            });
            
            // Play spawn sound
            if (config.ZombieSoundsEnabled)
            {
                Effect.server.Run("assets/bundled/prefabs/fx/player/howl.prefab", position);
            }
        }

        // Gunshot aggro - zombies hear gunshots (but not arrows - they're silent)
        private void OnWeaponFired(BaseProjectile projectile, BasePlayer player, ItemModProjectile mod, ProtoBuf.ProjectileShoot projectiles)
        {
            if (player == null || !player.userID.IsSteamId()) return;
            if (projectile == null) return;
            
            // Skip silent weapons (bows, crossbows, compound bow, nail gun)
            string weaponName = projectile.ShortPrefabName?.ToLower() ?? "";
            if (weaponName.Contains("bow") || 
                weaponName.Contains("crossbow") || 
                weaponName.Contains("compound") ||
                weaponName.Contains("nailgun"))
            {
                return; // Arrows are silent - don't alert zombies
            }
            
            // Also check ammo type - arrows don't make noise
            string ammoName = mod?.projectileObject?.resourcePath?.ToLower() ?? "";
            if (ammoName.Contains("arrow"))
            {
                return; // Arrow ammo is silent
            }
            
            // Alert nearby zombies to gunshot
            AlertZombiesToPosition(player.transform.position, config.GunshotAggroRange, player);
        }
        
        // Explosives thrown (grenades, etc) - LOUD 300m
        // Explosives thrown (grenades, etc) - zombies hear the EXPLOSION, not the throw
        // We don't alert here - OnEntityTakeDamage handles explosion damage and alerts there
        private void OnExplosiveThrown(BasePlayer player, BaseEntity entity, ThrownWeapon item)
        {
            // Don't alert on throw - wait for actual explosion
            // The OnEntityTakeDamage hook handles explosion alerts when damage occurs
        }
        
        // Rockets - zombies hear the EXPLOSION, not the launch
        // We don't alert here - OnEntityTakeDamage handles explosion damage and alerts there
        private void OnRocketLaunched(BasePlayer player, BaseEntity entity)
        {
            // Don't alert on launch - wait for actual explosion/impact
            // The OnEntityTakeDamage hook handles explosion alerts when damage occurs
        }
        
        // Loud tools - jackhammer, chainsaw, etc. - 130m
        private void OnMeleeAttack(BasePlayer player, HitInfo info)
        {
            if (player == null || !player.userID.IsSteamId()) return;
            
            var heldItem = player.GetActiveItem();
            if (heldItem == null) return;
            
            string itemName = heldItem.info.shortname?.ToLower() ?? "";
            
            // Check for loud power tools
            if (itemName.Contains("jackhammer") || 
                itemName.Contains("chainsaw") ||
                itemName.Contains("pneumatic"))
            {
                AlertZombiesToPosition(player.transform.position, config.GunshotAggroRange, player);
            }
        }
        
        // C4/Satchel placement - zombies hear the EXPLOSION, not the placement
        // We don't alert here - OnEntityTakeDamage handles explosion damage and alerts there
        private void OnExplosivePlaced(BasePlayer player, BaseEntity entity)
        {
            // Don't alert on placement - wait for actual explosion
            // The OnEntityTakeDamage hook handles explosion alerts when damage occurs
        }
        
        // Running footsteps - 40m (checked periodically)
        private Dictionary<ulong, float> lastRunningCheck = new Dictionary<ulong, float>();
        
        private void OnPlayerTick(BasePlayer player)
        {
            if (player == null || !player.userID.IsSteamId()) return;
            if (player.IsSleeping() || player.IsDead()) return;
            
            // Throttle checks to every 2 seconds
            float now = Time.realtimeSinceStartup;
            if (lastRunningCheck.TryGetValue(player.userID, out float lastCheck))
            {
                if (now - lastCheck < 2f) return;
            }
            lastRunningCheck[player.userID] = now;
            
            // If crouching, player is invisible to zombies
            if (config.ZombieCombat.CrouchingHidesPlayer && player.IsDucked())
            {
                return; // Silent and invisible when crouched
            }
            
            // If running (sprinting), make noise
            if (player.IsRunning())
            {
                AlertZombiesToPosition(player.transform.position, config.ZombieCombat.RunningNoiseRange, player);
            }
        }
        
        private void AlertZombiesToPosition(Vector3 position, float range, BasePlayer target)
        {
            // If target is crouching and that setting is enabled, don't alert zombies
            if (target != null && config.ZombieCombat.CrouchingHidesPlayer && target.IsDucked())
            {
                return; // Crouched players are invisible
            }
            
            foreach (var zombieId in spawnedZombies.ToList())
            {
                var zombie = BaseNetworkable.serverEntities.Find(new NetworkableId(zombieId)) as ScarecrowNPC;
                if (zombie == null || zombie.IsDead()) continue;
                
                float distance = Vector3.Distance(zombie.transform.position, position);
                if (distance > range) continue;
                
                // Get the brain and make zombie investigate
                var brain = zombie.GetComponent<ScarecrowBrain>();
                if (brain == null) continue;
                
                // Set the player as the current threat/target
                brain.Events.Memory.Entity.Set(target, 0);
                brain.Events.Memory.Position.Set(position, 0);
                
                // Force the zombie to target this player
                if (brain.Senses != null)
                {
                    if (!brain.Senses.Players.Contains(target))
                        brain.Senses.Players.Add(target);
                }
                
                // Set navigator destination to walk toward the sound
                if (brain.Navigator != null)
                {
                    brain.Navigator.SetDestination(position);
                }
                
                DebugLog($"Zombie alerted to noise at {distance:F0}m (range: {range}m)");
            }
        }

        #endregion

        #region Commands
        [ChatCommand("zdebug")]
        private void CmdDebug(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin) return;
            
            RaycastHit hit;
            if (!Physics.Raycast(player.eyes.HeadRay(), out hit, 100f))
            {
                SendReply(player, "Look at an entity");
                return;
            }
            
            var entity = hit.GetEntity();
            if (entity == null)
            {
                SendReply(player, "No entity found");
                return;
            }
            
            string msg = $"<color=#ffcc00>Entity Debug:</color>\n";
            msg += $"Name: {entity.ShortPrefabName}\n";
            msg += $"OwnerID: {entity.OwnerID}\n";
            msg += $"IsSteamId: {entity.OwnerID.IsSteamId()}\n";
            msg += $"Position: {entity.transform.position}\n";
            msg += $"Your ID: {player.userID}\n";
            
            var raidableBases = plugins.Find("RaidableBases");
            if (raidableBases != null && raidableBases.IsLoaded)
            {
                var result = raidableBases.Call("EventTerritory", entity.transform.position);
                msg += $"RB EventTerritory: {result}\n";
                
                // Get raid owner info
                var raidOwner = raidableBases.Call("GetRaidOwner", entity.transform.position);
                msg += $"RB RaidOwnerID: {raidOwner}\n";
                
                // Check if you are the owner
                if (raidOwner is ulong ownerId)
                {
                    msg += $"You are owner: {(player.userID == ownerId ? "YES" : "NO")}\n";
                }
            }
            else
            {
                msg += "RaidableBases: Not loaded\n";
            }
            
            var zoneManager = plugins.Find("ZoneManager");
            if (zoneManager != null && zoneManager.IsLoaded)
            {
                var zones = zoneManager.Call("GetEntityZoneIDs", entity) as string[];
                msg += $"ZM Zones: {(zones != null && zones.Length > 0 ? string.Join(", ", zones) : "none")}\n";
            }
            
            SendReply(player, msg);
        }
        
        [ChatCommand("zesp")]
        private void CmdZombieESP(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin && !permission.UserHasPermission(player.UserIDString, ADMIN_PERM))
            {
                SendReply(player, "<color=#ff4444>Admin only command.</color>");
                return;
            }
            
            if (espPlayers.Contains(player.userID))
            {
                espPlayers.Remove(player.userID);
                SendReply(player, "<color=#ff4444>[ESP] Zombie ESP DISABLED</color>");
                
                // Stop timer if no one is using ESP
                if (espPlayers.Count == 0)
                {
                    espTimer?.Destroy();
                    espTimer = null;
                }
            }
            else
            {
                espPlayers.Add(player.userID);
                SendReply(player, "<color=#44ff44>[ESP] Zombie ESP ENABLED</color> - Green boxes around zombies");
                
                // Start ESP timer if not running
                if (espTimer == null)
                {
                    espTimer = timer.Every(0.5f, DrawZombieESP);
                }
            }
        }
        
        private void DrawZombieESP()
        {
            if (espPlayers.Count == 0) return;
            
            foreach (var playerId in espPlayers.ToList())
            {
                var player = BasePlayer.FindByID(playerId);
                if (player == null || !player.IsConnected)
                {
                    espPlayers.Remove(playerId);
                    continue;
                }
                
                foreach (var zombieId in spawnedZombies)
                {
                    var zombie = BaseNetworkable.serverEntities.Find(new NetworkableId(zombieId)) as BasePlayer;
                    if (zombie == null || zombie.IsDead()) continue;
                    
                    float distance = Vector3.Distance(player.transform.position, zombie.transform.position);
                    if (distance > 200f) continue; // Only show within 200m
                    
                    // Position above zombie head
                    Vector3 textPos = zombie.transform.position + Vector3.up * 2.2f;
                    
                    // Color based on distance: red = close, yellow = medium, green = far
                    Color textColor = distance < 30f ? Color.red : (distance < 80f ? Color.yellow : Color.green);
                    
                    // Just draw distance text - simple and lightweight
                    player.SendConsoleCommand("ddraw.text", 0.5f, textColor, textPos, $"[Z] {distance:F0}m");
                }
            }
        }
        
        [ChatCommand("zhelp")]
        private void CmdZombieHelp(BasePlayer player, string command, string[] args)
        {
            string msg = "<color=#ff4444>=== ZOMBIE PVE COMMANDS ===</color>\n\n" +
                "<color=#ffcc00>Player Commands:</color>\n" +
                "- <color=#44ff44>/pve</color> - Server status & infection info\n" +
                "- <color=#44ff44>/status</color> - Your infection status\n" +
                "- <color=#44ff44>/zstats</color> - Your zombie kill statistics\n" +
                "- <color=#44ff44>/ztop</color> - Leaderboard of top killers\n";
            
            // Tier1 commands
            if (ZombiePVETier1 != null)
            {
                msg += "\n<color=#ffcc00>Tier1 Features:</color>\n" +
                    "- <color=#44ff44>/zevolution</color> - Zombie evolution status\n" +
                    "- <color=#44ff44>/safehouse</color> - Manage safe house\n" +
                    "- <color=#44ff44>/ztrade</color> - Trade zombie trophies\n" +
                    "- <color=#44ff44>/zalpha</color> - Track alpha zombie\n" +
                    "- <color=#44ff44>/bounty</color> - View bounty board\n" +
                    "- <color=#44ff44>/zcure</color> - View cure recipe\n";
            }
            
            // Tier2 commands
            if (ZombiePVETier2 != null)
            {
                msg += "\n<color=#ffcc00>Tier2 Features:</color>\n" +
                    "- <color=#44ff44>/storm</color> - Storm system status\n";
            }
            
            if (player.IsAdmin || permission.UserHasPermission(player.UserIDString, ADMIN_PERM))
            {
                msg += "\n<color=#ffcc00>Admin Commands:</color>\n" +
                    "- <color=#ff8800>/sz [count]</color> - Spawn zombies\n" +
                    "- <color=#ff8800>/horde [size]</color> - Spawn horde\n" +
                    "- <color=#ff8800>/boss</color> - Spawn boss zombie\n" +
                    "- <color=#ff8800>/bloodmoon</color> - Toggle Blood Moon\n" +
                    "- <color=#ff8800>/killzombies</color> - Kill all zombies\n";
                
                if (ZombiePVETier1 != null)
                    msg += "- <color=#ff8800>/zt1</color> - Tier1 admin\n";
                
                if (ZombiePVETier2 != null)
                    msg += "- <color=#ff8800>/storm start/stop</color> - Control storms\n";
            }
            
            SendReply(player, msg);
        }
        
        [ChatCommand("pve")]
        private void CmdPVEInfo(BasePlayer player, string command, string[] args)
        {
            bool pvpActive = IsPVPModeActive();
            string status = pvpActive
                ? "<color=#ff4444>[PVP] PVP MODE ACTIVE</color>"
                : "<color=#44ff44>[PVE] PVE MODE</color>";
            
            string bloodMoonStatus = isBloodMoon 
                ? "<color=#ff0000>[!!] BLOOD MOON ACTIVE!</color>" 
                : "<color=#888888>No Blood Moon</color>";
            
            string nightStatus = IsNightTime() 
                ? "<color=#4444ff>[NIGHT] Night Time - Zombies Enhanced!</color>" 
                : "<color=#ffff44>[DAY] Day Time</color>";

            SendReply(player,
                $"<color=#ff4444>=== ZOMBIE PVE SERVER ===</color>\n\n" +
                $"{status}\n{bloodMoonStatus}\n{nightStatus}\n\n" +
                $"<color=#ffcc00>Active Zombies:</color> {spawnedZombies.Count}/{config.MaxTotalZombies}\n\n" +
                "<color=#888888>Zombie Infection:</color>\n" +
                $"• Bleed chance: {config.BleedChance}%\n" +
                $"• Poison chance: {config.PoisonChance}% (when bleeding)\n" +
                "• Use <color=#ffcc00>Bandage</color> to stop bleeding\n" +
                "• Use <color=#ffcc00>Medical Syringe</color> to cure poison\n\n" +
                "<color=#888888>Type /zhelp for all commands</color>");
        }

        [ChatCommand("status")]
        private void CmdStatus(BasePlayer player, string command, string[] args)
        {
            if (!playerStatuses.TryGetValue(player.userID, out var status))
            {
                SendReply(player, "<color=#44ff44>You are healthy!</color>");
                return;
            }

            string msg = "<color=#ffcc00>Your Status:</color>\n";
            msg += status.IsBleeding ? "<color=#ff4444>[!] BLEEDING</color>\n" : "<color=#44ff44>[OK] Not bleeding</color>\n";
            msg += status.IsPoisoned ? "<color=#44ff44>[!] POISONED</color>" : "<color=#44ff44>[OK] Not poisoned</color>";
            SendReply(player, msg);
        }

        [ChatCommand("sz")]
        private void CmdSpawnZombies(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin)
            {
                SendReply(player, "<color=#ff4444>Admin only command.</color>");
                return;
            }

            int count = 20;
            if (args.Length > 0 && int.TryParse(args[0], out int customCount))
                count = customCount;

            // Raycast to where player is looking
            RaycastHit hit;
            if (!Physics.Raycast(player.eyes.HeadRay(), out hit, 500f, LayerMask.GetMask("Terrain", "World", "Construction")))
            {
                SendReply(player, "<color=#ff4444>Could not find a valid spawn location. Look at the ground.</color>");
                return;
            }

            Vector3 center = hit.point;
            int spawned = 0;

            for (int i = 0; i < count; i++)
            {
                // Spread zombies in a circle around the target point
                float distance = UnityEngine.Random.Range(2f, 15f);
                float angle = UnityEngine.Random.Range(0f, 360f) * Mathf.Deg2Rad;
                Vector3 offset = new Vector3(Mathf.Cos(angle) * distance, 0, Mathf.Sin(angle) * distance);
                Vector3 spawnPos = center + offset;

                // Find ground
                if (Physics.Raycast(spawnPos + Vector3.up * 50f, Vector3.down, out RaycastHit groundHit, 100f, LayerMask.GetMask("Terrain", "World")))
                {
                    spawnPos = groundHit.point;
                }

                SpawnZombieAtPosition(spawnPos);
                spawned++;
            }

            SendReply(player, $"<color=#44ff44>Spawned {spawned} zombies at your aim point!</color>");
        }

        [ChatCommand("killzombies")]
        private void CmdKillZombies(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin)
            {
                SendReply(player, "<color=#ff4444>Admin only command.</color>");
                return;
            }

            int killed = 0;
            foreach (var zombieId in spawnedZombies)
            {
                var zombie = BaseNetworkable.serverEntities.Find(new NetworkableId(zombieId)) as BasePlayer;
                if (zombie != null && !zombie.IsDead())
                {
                    zombie.Kill();
                    killed++;
                }
            }
            spawnedZombies.Clear();

            SendReply(player, $"<color=#44ff44>Killed {killed} zombies!</color>");
        }

        [ChatCommand("zombiecount")]
        private void CmdZombieCount(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin)
            {
                SendReply(player, "<color=#ff4444>Admin only command.</color>");
                return;
            }

            // Clean up dead ones first
            spawnedZombies.RemoveWhere(id => 
            {
                var z = BaseNetworkable.serverEntities.Find(new NetworkableId(id));
                return z == null || (z as BaseCombatEntity)?.IsDead() == true;
            });

            SendReply(player, $"<color=#ffcc00>Active zombies: {spawnedZombies.Count}</color>");
        }
        
        [ChatCommand("zstats")]
        private void CmdZombieStats(BasePlayer player, string command, string[] args)
        {
            var stats = GetPlayerStats(player.userID);
            float kd = stats.DeathsToZombies > 0 ? (float)stats.ZombiesKilled / stats.DeathsToZombies : stats.ZombiesKilled;
            
            SendReply(player,
                $"<color=#ff4444>=== ZOMBIE STATS ===</color>\n" +
                $"<color=#ffcc00>Kills:</color> {stats.ZombiesKilled}\n" +
                $"<color=#ffcc00>Headshots:</color> {stats.HeadshotKills}\n" +
                $"<color=#ffcc00>Deaths:</color> {stats.DeathsToZombies}\n" +
                $"<color=#ffcc00>K/D Ratio:</color> {kd:F2}\n" +
                $"<color=#ffcc00>Best Streak:</color> {stats.BestKillStreak}\n" +
                $"<color=#ffcc00>Infections Survived:</color> {stats.InfectionsSurvived}");
        }
        
        [ChatCommand("ztop")]
        private void CmdZombieTop(BasePlayer player, string command, string[] args)
        {
            var topKillers = playerStats.OrderByDescending(x => x.Value.ZombiesKilled).Take(10).ToList();
            
            string msg = "<color=#ff4444>=== TOP ZOMBIE KILLERS ===</color>\n";
            int rank = 1;
            foreach (var entry in topKillers)
            {
                string name = GetPlayerName(entry.Key);
                msg += $"<color=#ffcc00>#{rank}</color> {name}: <color=#44ff44>{entry.Value.ZombiesKilled}</color> kills\n";
                rank++;
            }
            
            SendReply(player, msg);
        }
        
        [ChatCommand("bloodmoon")]
        private void CmdBloodMoon(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin && !permission.UserHasPermission(player.UserIDString, ADMIN_PERM))
            {
                SendReply(player, "<color=#ff4444>Admin only command.</color>");
                return;
            }
            
            if (isBloodMoon)
            {
                EndBloodMoon();
                SendReply(player, "<color=#44ff44>Blood Moon ended!</color>");
            }
            else
            {
                StartBloodMoon();
                SendReply(player, "<color=#ff4444>Blood Moon started!</color>");
            }
        }
        
        [ChatCommand("horde")]
        private void CmdSpawnHorde(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin && !permission.UserHasPermission(player.UserIDString, ADMIN_PERM))
            {
                SendReply(player, "<color=#ff4444>Admin only command.</color>");
                return;
            }
            
            int size = 30;
            if (args.Length > 0 && int.TryParse(args[0], out int customSize))
                size = customSize;
            
            SpawnHordeAtPlayer(player, size);
            BroadcastToAll($"<color=#ff4444>[!] A ZOMBIE HORDE has been unleashed near {player.displayName}!</color>");
        }
        
        [ChatCommand("boss")]
        private void CmdSpawnBoss(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin && !permission.UserHasPermission(player.UserIDString, ADMIN_PERM))
            {
                SendReply(player, "<color=#ff4444>Admin only command.</color>");
                return;
            }
            
            RaycastHit hit;
            if (!Physics.Raycast(player.eyes.HeadRay(), out hit, 100f, LayerMask.GetMask("Terrain", "World")))
            {
                SendReply(player, "<color=#ff4444>Look at the ground!</color>");
                return;
            }
            
            SpawnBossZombie(hit.point);
            BroadcastToAll($"<color=#ff0000>[BOSS] A BOSS ZOMBIE has spawned!</color>");
        }
        
        [ChatCommand("zsave")]
        private void CmdSaveStats(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin && !permission.UserHasPermission(player.UserIDString, ADMIN_PERM))
            {
                SendReply(player, "<color=#ff4444>Admin only command.</color>");
                return;
            }
            
            SavePlayerStats();
            SendReply(player, "<color=#44ff44>Player stats saved!</color>");
        }
        
        [ChatCommand("zreload")]
        private void CmdReloadConfig(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin && !permission.UserHasPermission(player.UserIDString, ADMIN_PERM))
            {
                SendReply(player, "<color=#ff4444>Admin only command.</color>");
                return;
            }
            
            LoadConfig();
            SendReply(player, "<color=#44ff44>Config reloaded!</color>");
        }
        #endregion
        
        #region Blood Moon System
        private void StartBloodMoonCheck()
        {
            if (!config.BloodMoonEnabled) return;
            
            // Check every 30 minutes for random blood moon (only at night)
            bloodMoonTimer?.Destroy();
            bloodMoonTimer = timer.Every(1800f, () =>
            {
                // Only trigger blood moon at night
                if (!IsNightTime()) return;
                
                if (!isBloodMoon && UnityEngine.Random.Range(0, 100) < 15) // 15% chance every 30 min
                {
                    StartBloodMoon();
                }
            });
        }
        
        private void StartBloodMoon()
        {
            if (isBloodMoon) return;
            
            // Blood moon only happens at night
            if (!IsNightTime())
            {
                Puts("[BloodMoon] Cannot start - Must be night time");
                return;
            }
            
            // Check if acid rain is active - can't have both
            if (IsAcidRainActive())
            {
                Puts("[BloodMoon] Cannot start - Acid Rain is active");
                return;
            }
            
            isBloodMoon = true;
            currentWave = 0;
            
            BroadcastToAll("<color=#ff0000>!! === BLOOD MOON RISING === !!</color>");
            BroadcastToAll("<color=#ff4444>The dead are restless... Prepare yourself!</color>");
            BroadcastToAll("<color=#ff8800>Zombies drop 3x loot during Blood Moon!</color>");
            
            // Show UI to all players
            foreach (var player in BasePlayer.activePlayerList)
            {
                ShowBloodMoonUI(player);
            }
            
            // Spawn waves of zombies
            timer.Repeat(30f, (int)(config.BloodMoonDuration / 30f), () =>
            {
                if (!isBloodMoon) return;
                currentWave++;
                SpawnBloodMoonWave();
            });
            
            // End blood moon after duration
            timer.Once(config.BloodMoonDuration, EndBloodMoon);
        }
        
        private void EndBloodMoon()
        {
            if (!isBloodMoon) return;
            isBloodMoon = false;
            
            BroadcastToAll("<color=#44ff44>[DAY] The Blood Moon has passed... for now.</color>");
            
            foreach (var player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, HORDE_UI);
            }
        }
        
        private void SpawnBloodMoonWave()
        {
            int baseCount = (int)(config.MaxZombiesPerPlayer * config.BloodMoonZombieMultiplier);
            int waveBonus = currentWave * 2;
            
            BroadcastToAll($"<color=#ff4444>[!] WAVE {currentWave} - {baseCount + waveBonus} zombies incoming!</color>");
            
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player == null || !player.IsConnected || player.IsDead() || player.IsSleeping()) continue;
                
                int count = baseCount + waveBonus;
                for (int i = 0; i < count; i++)
                {
                    Vector3 pos = GetSpawnPosition(player.transform.position);
                    if (pos != Vector3.zero)
                        SpawnZombieAtPosition(pos);
                }
            }
        }
        
        private void ShowBloodMoonUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, HORDE_UI);
            
            var elements = new CuiElementContainer();
            var panel = elements.Add(new CuiPanel
            {
                Image = { Color = "0.5 0.1 0.1 0.8" },
                RectTransform = { AnchorMin = "0.4 0.92", AnchorMax = "0.6 0.97" },
                CursorEnabled = false
            }, "Overlay", HORDE_UI);
            
            elements.Add(new CuiLabel
            {
                Text = { Text = "!! BLOOD MOON !!", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "1 0.3 0.3 1", Font = "robotocondensed-bold.ttf" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
            }, panel);
            
            CuiHelper.AddUi(player, elements);
        }
        
        // Hook for ZombiePVETier2 to check blood moon status
        private bool IsBloodMoonActive()
        {
            return isBloodMoon;
        }
        
        // Check if acid rain is active (from ZombiePVETier2)
        private bool IsAcidRainActive()
        {
            if (ZombiePVETier2 == null) return false;
            
            var result = ZombiePVETier2.Call("IsAcidRainActive");
            if (result is bool)
                return (bool)result;
            
            return false;
        }
        #endregion
        
        #region Boss Zombies
        private void SpawnBossZombie(Vector3 position)
        {
            var boss = GameManager.server.CreateEntity("assets/prefabs/npc/scarecrow/scarecrow.prefab", position, Quaternion.identity) as ScarecrowNPC;
            if (boss == null) return;
            
            // Set scale BEFORE spawning
            float scale = config.Boss.SizeScale;
            boss.transform.localScale = new Vector3(scale, scale, scale);
            
            boss.Spawn();
            
            if (boss.net != null)
                spawnedZombies.Add(boss.net.ID.Value);
            
            // Make it a boss
            boss.displayName = "<color=#ff0000>[BOSS] ZOMBIE</color>";
            boss.InitializeHealth(boss.MaxHealth() * config.BossHealthMultiplier, boss.MaxHealth() * config.BossHealthMultiplier);
            
            // Apply scale again after spawn and send to clients
            timer.Once(0.1f, () =>
            {
                if (boss == null || boss.IsDestroyed) return;
                boss.transform.localScale = new Vector3(scale, scale, scale);
                boss.SendNetworkUpdate();
            });
            
            timer.Once(0.5f, () =>
            {
                if (boss == null || boss.IsDestroyed) return;
                
                var brain = boss.GetComponent<ScarecrowBrain>();
                if (brain != null && brain.Navigator != null)
                {
                    brain.Navigator.Speed = 6f;
                    brain.SenseRange = 50f;
                    brain.TargetLostRange = 60f;
                }
            });
        }
        
        private void TrySpawnBoss(Vector3 position)
        {
            if (!config.BossZombiesEnabled) return;
            if (UnityEngine.Random.Range(0, 100) >= config.BossSpawnChance) return;
            
            SpawnBossZombie(position);
            BroadcastToAll("<color=#ff0000>[BOSS] A BOSS ZOMBIE has emerged from the horde!</color>");
        }
        #endregion
        
        #region Zombie Loot System
        private void DropZombieLoot(Vector3 position, BasePlayer killer)
        {
            if (!config.ZombieLootEnabled) return;
            if (UnityEngine.Random.Range(0, 100) >= config.ZombieLootChance) return;
            
            // Blood Moon = 3x loot ITEMS (not quantity)
            int lootDrops = isBloodMoon ? 3 : 1;
            
            // Check if a Tier plugin wants to handle loot instead
            if (ZombiePVETier3 != null)
            {
                for (int i = 0; i < lootDrops; i++)
                    ZombiePVETier3.Call("OnZombieLootDrop", position, killer);
                return;
            }
            if (ZombiePVETier2 != null)
            {
                for (int i = 0; i < lootDrops; i++)
                    ZombiePVETier2.Call("OnZombieLootDrop", position, killer);
                return;
            }
            if (ZombiePVETier1 != null)
            {
                for (int i = 0; i < lootDrops; i++)
                    ZombiePVETier1.Call("OnZombieLootDrop", position, killer);
                return;
            }
            
            // Default Core loot - vanilla appropriate (basic survival items only, no guns)
            var lootTable = new List<(string shortname, int min, int max, int weight)>
            {
                ("scrap", 1, 2, 35),
                ("metal.fragments", 1, 5, 30),
                ("cloth", 1, 3, 30),
                ("lowgradefuel", 1, 3, 20),
                ("bandage", 1, 1, 15),
                ("bone.fragments", 1, 5, 25),
                ("fat.animal", 1, 2, 20),
                ("leather", 1, 2, 15)
            };
            
            // Calculate total weight
            int totalWeight = 0;
            foreach (var entry in lootTable)
                totalWeight += entry.weight;
            
            // Drop loot (3x items during blood moon)
            for (int drop = 0; drop < lootDrops; drop++)
            {
                // Pick ONE random item based on weight
                int roll = UnityEngine.Random.Range(0, totalWeight);
                int cumulative = 0;
                
                foreach (var (shortname, min, max, weight) in lootTable)
                {
                    cumulative += weight;
                    if (roll < cumulative)
                    {
                        var item = ItemManager.CreateByName(shortname, UnityEngine.Random.Range(min, max + 1));
                        if (item != null)
                        {
                            // Drop item directly on ground with slight offset for multiple drops
                            Vector3 dropPos = position + Vector3.up * 0.5f + UnityEngine.Random.insideUnitSphere * 0.2f * drop;
                            item.Drop(dropPos, Vector3.up * 2f + UnityEngine.Random.insideUnitSphere * 0.3f);
                        }
                        break;
                    }
                }
            }
        }
        #endregion
        
        #region Kill Streak System
        private void ProcessZombieKill(BasePlayer killer, bool wasHeadshot)
        {
            var stats = GetPlayerStats(killer.userID);
            stats.ZombiesKilled++;
            
            if (wasHeadshot)
                stats.HeadshotKills++;
            
            // Kill streak
            if (config.KillStreakEnabled)
            {
                if ((DateTime.Now - stats.LastKillTime).TotalSeconds <= config.KillStreakTimeout)
                {
                    stats.CurrentKillStreak++;
                    
                    if (stats.CurrentKillStreak > stats.BestKillStreak)
                        stats.BestKillStreak = stats.CurrentKillStreak;
                    
                    // Milestone broadcasts
                    if (config.BroadcastKillStreaks && stats.CurrentKillStreak % config.KillStreakMilestone == 0)
                    {
                        BroadcastToAll($"<color=#ffcc00>[STREAK] {killer.displayName} is on a {stats.CurrentKillStreak} KILL STREAK!</color>");
                    }
                    
                    // Streak bonuses
                    if (stats.CurrentKillStreak == 5)
                        SendReply(killer, "<color=#ffcc00>[5x] 5 Kill Streak! +10 Scrap bonus!</color>");
                    else if (stats.CurrentKillStreak == 10)
                        SendReply(killer, "<color=#ff8800>[10x] 10 Kill Streak! +25 Scrap bonus!</color>");
                    else if (stats.CurrentKillStreak == 25)
                        SendReply(killer, "<color=#ff4400>[25x] 25 Kill Streak! LEGENDARY!</color>");
                    
                    GiveStreakBonus(killer, stats.CurrentKillStreak);
                }
                else
                {
                    stats.CurrentKillStreak = 1;
                }
                
                stats.LastKillTime = DateTime.Now;
            }
        }
        
        private void GiveStreakBonus(BasePlayer player, int streak)
        {
            int scrapBonus = 0;
            if (streak == 5) scrapBonus = 10;
            else if (streak == 10) scrapBonus = 25;
            else if (streak == 25) scrapBonus = 100;
            else if (streak % 10 == 0) scrapBonus = streak;
            
            if (scrapBonus > 0)
            {
                var scrap = ItemManager.CreateByName("scrap", scrapBonus);
                player.GiveItem(scrap);
            }
        }
        #endregion
        
        #region Player Stats Data
        private PlayerStats GetPlayerStats(ulong playerId)
        {
            if (!playerStats.TryGetValue(playerId, out var stats))
            {
                stats = new PlayerStats { LastKillTime = DateTime.MinValue };
                playerStats[playerId] = stats;
            }
            return stats;
        }
        
        private void LoadPlayerStats()
        {
            playerStats = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, PlayerStats>>("ZombiePVECore_Stats") 
                ?? new Dictionary<ulong, PlayerStats>();
        }
        
        private void SavePlayerStats()
        {
            Interface.Oxide.DataFileSystem.WriteObject("ZombiePVECore_Stats", playerStats);
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
        
        #region Horde Spawning
        private void SpawnHordeAtPlayer(BasePlayer player, int size)
        {
            Vector3 center = player.transform.position;
            
            for (int i = 0; i < size; i++)
            {
                float distance = UnityEngine.Random.Range(15f, 40f);
                float angle = UnityEngine.Random.Range(0f, 360f) * Mathf.Deg2Rad;
                Vector3 offset = new Vector3(Mathf.Cos(angle) * distance, 0, Mathf.Sin(angle) * distance);
                Vector3 pos = center + offset;
                
                RaycastHit hit;
                if (Physics.Raycast(pos + Vector3.up * 50f, Vector3.down, out hit, 100f, LayerMask.GetMask("Terrain", "World")))
                {
                    pos = hit.point;
                    SpawnZombieAtPosition(pos);
                    
                    // Small chance for boss in horde
                    if (i == size / 2)
                        TrySpawnBoss(pos);
                }
            }
        }
        #endregion
        
        #region Helpers
        private void BroadcastToAll(string message)
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                SendReply(player, message);
            }
        }
        
        private bool IsNightTime()
        {
            var time = TOD_Sky.Instance?.Cycle?.Hour ?? 12f;
            return time < 6f || time > 20f;
        }
        
        private bool IsRaidableBase(BaseEntity entity)
        {
            if (entity == null) return false;
            
            // Check RaidableBases plugin API
            var raidableBases = plugins.Find("RaidableBases");
            if (raidableBases != null && raidableBases.IsLoaded)
            {
                try
                {
                    // Check if position is in a raidable base territory
                    object result = raidableBases.Call("EventTerritory", entity.transform.position);
                    if (result is bool)
                        return (bool)result;
                }
                catch { }
            }
            
            return false;
        }
        #endregion
        
        #region VIP Night Vision System
        private void SetupNightVision()
        {
            if (!config.VIP.NightVisionEnabled) return;
            
            if (TOD_Sky.Instance == null)
            {
                timer.Once(15f, SetupNightVision);
                return;
            }
            
            // Subscribe to day/night events
            TOD_Sky.Instance.Components.Time.OnSunrise += OnSunrise;
            TOD_Sky.Instance.Components.Time.OnSunset += OnSunset;
            
            // Track current VIP players
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (permission.UserHasPermission(player.UserIDString, VIP_PERM))
                    nightVisionPlayers.Add(player.userID);
            }
            
            // Check if currently night
            isCurrentlyNight = TOD_Sky.Instance.IsNight;
            
            if (isCurrentlyNight)
                ApplyNightVisionToAll();
        }
        
        private void OnSunrise()
        {
            isCurrentlyNight = false;
            
            foreach (var player in BasePlayer.activePlayerList.ToList())
            {
                if (player != null && nightVisionPlayers.Contains(player.userID))
                    ResetNightVision(player);
            }
        }
        
        private void OnSunset()
        {
            isCurrentlyNight = true;
            ApplyNightVisionToAll();
        }
        
        private void ApplyNightVisionToAll()
        {
            if (!config.VIP.NightVisionEnabled) return;
            
            foreach (var player in BasePlayer.activePlayerList.ToList())
            {
                if (player != null && nightVisionPlayers.Contains(player.userID))
                    ApplyNightVision(player);
            }
        }
        
        private void ApplyNightVision(BasePlayer player)
        {
            if (player?.net?.connection == null) return;
            if (!config.VIP.NightVisionEnabled) return;
            
            // Enable night light for immediate area
            player.SendConsoleCommand("env.nightlight_enabled", "true");
            player.SendConsoleCommand("env.nightlight_brightness", config.VIP.NightVisionBrightness);
            player.SendConsoleCommand("env.nightlight_distance", config.VIP.NightVisionDistance);
            
            // Boost overall atmosphere brightness for distance visibility
            player.SendConsoleCommand("weather.atmosphere_brightness", config.VIP.AtmosphereBrightness);
        }
        
        private void ResetNightVision(BasePlayer player)
        {
            if (player?.net?.connection == null) return;
            
            player.SendConsoleCommand("env.nightlight_enabled", "false");
            player.SendConsoleCommand("weather.atmosphere_brightness", 1f);
        }
        
        // Handle VIP player connecting - apply night vision if it's night
        private void OnPlayerConnected(BasePlayer player)
        {
            if (player == null) return;
            
            timer.Once(2f, () =>
            {
                if (player == null || !player.IsConnected) return;
                
                if (permission.UserHasPermission(player.UserIDString, VIP_PERM))
                {
                    nightVisionPlayers.Add(player.userID);
                    
                    if (isCurrentlyNight && config.VIP.NightVisionEnabled)
                        ApplyNightVision(player);
                }
            });
        }
        #endregion
        
        #region Smooth Saver System
        private void InitializeSmoothSaver()
        {
            if (!config.SmoothSaver.Enabled) return;
            
            // Disable built-in auto-save
            ConVar.Server.saveinterval = 99999;
            
            // Increase save cache size for large servers
            ConVar.Server.savecachesize = config.SmoothSaver.SaveCacheSize;
            
            // Set server FPS limit
            if (config.SmoothSaver.LimitFPS)
            {
                ConVar.FPS.limit = config.SmoothSaver.FPSLimit;
                Puts($"[SmoothSaver] Server FPS limited to {config.SmoothSaver.FPSLimit}");
            }
            
            Puts($"[SmoothSaver] Initialized - Cache: {config.SmoothSaver.SaveCacheSize:N0}, Interval: {config.SmoothSaver.AutoSaveInterval}s");
            Puts($"[SmoothSaver] Entity count: {BaseNetworkable.serverEntities.Count:N0}");
            
            StartAutoSaveTimer();
            lastSaveTime = DateTime.Now;
            
        }
        
        private void StartAutoSaveTimer()
        {
            autoSaveTimer?.Destroy();
            autoSaveTimer = timer.Every(config.SmoothSaver.AutoSaveInterval, () =>
            {
                if (!isSaving)
                    ServerMgr.Instance.StartCoroutine(DoSaveCoroutine(true));
            });
        }
        
        private object OnServerCommand(ConsoleSystem.Arg arg)
        {
            if (arg.cmd?.FullName == "server.save" && config.SmoothSaver.Enabled)
            {
                if (!isSaving)
                {
                    ServerMgr.Instance.StartCoroutine(DoSaveCoroutine(false));
                    arg.ReplyWith("ZombiePVE SmoothSaver: Save initiated...");
                }
                else
                {
                    arg.ReplyWith("ZombiePVE SmoothSaver: Save already in progress...");
                }
                return false;
            }
            return null;
        }
        
        private int CleanupSaveEntities()
        {
            int before = BaseEntity.saveList.Count;
            BaseEntity.saveList.RemoveWhere(x => x == null || x.IsDestroyed);
            return before - BaseEntity.saveList.Count;
        }
        
        private bool AnyPlayersInCombat()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player == null || player.IsDead()) continue;
                
                if (player.lastAttackedTime > 0 && UnityEngine.Time.realtimeSinceStartup - player.lastAttackedTime < 10f)
                    return true;
                    
                if (player.lastDealtDamageTime > 0 && UnityEngine.Time.realtimeSinceStartup - player.lastDealtDamageTime < 10f)
                    return true;
            }
            return false;
        }
        
        private IEnumerator DoSaveCoroutine(bool isAutoSave)
        {
            if (isSaving) yield break;
            
            // Combat check for auto-saves
            if (isAutoSave && config.SmoothSaver.SkipIfPlayersInCombat)
            {
                float waitedTime = 0f;
                while (AnyPlayersInCombat() && waitedTime < config.SmoothSaver.MaxCombatDelay)
                {
                    DebugLog("Players in combat, delaying save...");
                    yield return new WaitForSeconds(5f);
                    waitedTime += 5f;
                }
            }
            
            // Warning before save
            if (config.SmoothSaver.WarnBeforeSave && config.SmoothSaver.WarnSeconds > 0)
            {
                PrintToChat(string.Format(config.SmoothSaver.WarnMessage, config.SmoothSaver.WarnSeconds));
                yield return new WaitForSeconds(config.SmoothSaver.WarnSeconds);
            }
            
            isSaving = true;
            
            if (config.SmoothSaver.AnnounceStart)
                PrintToChat(config.SmoothSaver.SaveStartMessage);

            // Cleanup null entities first
            if (config.SmoothSaver.CleanupBeforeSave)
            {
                int cleaned = CleanupSaveEntities();
                if (config.Debug.Enabled && cleaned > 0)
                    Puts($"[SmoothSaver] Cleaned {cleaned} null entities before save");
            }

            int entityCount = BaseEntity.saveList.Count;
            
            DebugLog($"Starting save of {entityCount:N0} entities...");

            // Wait a frame to let any pending operations complete
            yield return null;
            
            // Force garbage collection before save to reduce memory pressure
            GC.Collect(0, GCCollectionMode.Optimized, false);
            
            yield return null;

            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                // Use SaveRestore.Save with caching enabled (false = use cache)
                SaveRestore.Save(false);
            }
            catch (Exception ex)
            {
                PrintError($"[SmoothSaver] Save error: {ex.Message}");
            }
            
            stopwatch.Stop();
            lastSaveDuration = stopwatch.ElapsedMilliseconds;
            lastSaveEntityCount = entityCount;
            lastSaveTime = DateTime.Now;

            DebugLog($"Save completed: {entityCount:N0} entities in {lastSaveDuration}ms");

            if (config.SmoothSaver.AnnounceComplete)
                PrintToChat(string.Format(config.SmoothSaver.SaveCompleteMessage, entityCount.ToString("N0"), lastSaveDuration));

            isSaving = false;
        }
        
        [ConsoleCommand("smoothsave")]
        private void CmdSmoothSave(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin) return;
            
            if (!config.SmoothSaver.Enabled)
            {
                arg.ReplyWith("SmoothSaver is disabled in config");
                return;
            }
            
            if (isSaving)
            {
                arg.ReplyWith("Save already in progress...");
                return;
            }

            ServerMgr.Instance.StartCoroutine(DoSaveCoroutine(false));
            arg.ReplyWith("Smooth save initiated...");
        }

        [ConsoleCommand("smoothsave.status")]
        private void CmdSaveStatus(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin) return;
            
            var entityCount = BaseNetworkable.serverEntities.Count;
            var saveListCount = BaseEntity.saveList.Count;
            var timeSinceLastSave = (DateTime.Now - lastSaveTime).TotalSeconds;
            
            arg.ReplyWith($"=== SmoothSaver Status ===\n" +
                         $"Enabled: {config.SmoothSaver.Enabled}\n" +
                         $"Currently saving: {isSaving}\n" +
                         $"Total entities: {entityCount:N0}\n" +
                         $"Saveable entities: {saveListCount:N0}\n" +
                         $"Save cache size: {ConVar.Server.savecachesize:N0}\n" +
                         $"Last save: {timeSinceLastSave:F0}s ago\n" +
                         $"Last save duration: {lastSaveDuration:F0}ms\n" +
                         $"Last save entities: {lastSaveEntityCount:N0}\n" +
                         $"Auto-save interval: {config.SmoothSaver.AutoSaveInterval}s\n" +
                         $"Online players: {BasePlayer.activePlayerList.Count}");
        }

        [ConsoleCommand("smoothsave.cache")]
        private void CmdSaveCache(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin) return;
            
            if (arg.Args == null || arg.Args.Length < 1)
            {
                arg.ReplyWith($"Current save cache size: {ConVar.Server.savecachesize}\nUsage: smoothsave.cache <size>");
                return;
            }

            if (int.TryParse(arg.Args[0], out int size))
            {
                config.SmoothSaver.SaveCacheSize = size;
                ConVar.Server.savecachesize = size;
                SaveConfig();
                arg.ReplyWith($"Save cache size set to {size:N0}");
            }
        }

        [ConsoleCommand("smoothsave.cleanup")]
        private void CmdSaveCleanup(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin) return;
            
            int cleaned = CleanupSaveEntities();
            arg.ReplyWith($"Cleaned up {cleaned:N0} null/destroyed entities from save list");
        }

        [ChatCommand("save")]
        private void CmdChatSave(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin) return;
            
            if (!config.SmoothSaver.Enabled)
            {
                player.ChatMessage("<color=#ff4444>SmoothSaver is disabled</color>");
                return;
            }
            
            if (isSaving)
            {
                player.ChatMessage("<color=#ffaa00>Save already in progress...</color>");
                return;
            }

            ServerMgr.Instance.StartCoroutine(DoSaveCoroutine(false));
            player.ChatMessage("<color=#00ff00>Save initiated...</color>");
        }

        [ChatCommand("savestatus")]
        private void CmdChatSaveStatus(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin) return;
            
            var timeSinceLastSave = (DateTime.Now - lastSaveTime).TotalSeconds;
            player.ChatMessage($"<color=#ffcc00>=== Save Status ===</color>\n" +
                              $"Entities: {BaseEntity.saveList.Count:N0}\n" +
                              $"Last save: {timeSinceLastSave:F0}s ago ({lastSaveDuration:F0}ms)\n" +
                              $"Next save: ~{config.SmoothSaver.AutoSaveInterval - timeSinceLastSave:F0}s");
        }
        #endregion
    }
}
