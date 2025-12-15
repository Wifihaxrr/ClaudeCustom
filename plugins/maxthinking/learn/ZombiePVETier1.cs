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
    [Info("ZombiePVETier1", "ZombiePVE", "1.0.0")]
    [Description("Advanced zombie PVE features: Safe Houses, Traders, Evolution, Alpha Hunts, Airdrops, Extraction, Bounties & more")]
    public class ZombiePVETier1 : RustPlugin
    {
        #region Fields
        private Configuration config;
        private StoredData storedData;
        
        // Plugin references
        [PluginReference] private Plugin ZombiePVECore, Economics, ServerRewards;
        
        // Tracking
        private Dictionary<ulong, PlayerData> playerData = new Dictionary<ulong, PlayerData>();
        private Dictionary<ulong, SafeHouse> safeHouses = new Dictionary<ulong, SafeHouse>();
        private Dictionary<ulong, AlphaZombie> alphaZombies = new Dictionary<ulong, AlphaZombie>();
        private List<BountyTask> activeBounties = new List<BountyTask>();
        private Dictionary<ulong, ExtractionEvent> activeExtractions = new Dictionary<ulong, ExtractionEvent>();
        
        // Timers
        private Timer evolutionTimer;
        private Timer bountyRefreshTimer;
        private Timer safeHouseTimer;
        private Timer alphaSpawnTimer;
        
        // Constants
        private const string PERM_ADMIN = "zombiepvetier1.admin";
        private const string PERM_VIP = "zombiepvetier1.vip";
        private const string BOUNTY_UI = "ZombieTier1_Bounty";
        private const string SAFEHOUSE_UI = "ZombieTier1_SafeHouse";
        
        // Zombie prefab
        private const string ZOMBIE_PREFAB = "assets/prefabs/npc/scarecrow/scarecrow.prefab";
        #endregion

        #region Classes
        private class PlayerData
        {
            public int ZombieKills;
            public int AlphaKills;
            public int ExtractionsCompleted;
            public int BountiesCompleted;
            public Dictionary<string, int> ZombieTrophies = new Dictionary<string, int>();
            public List<string> CompletedBountyIds = new List<string>();
            public bool HasPermanentCure;
        }
        
        private class SafeHouse
        {
            public ulong TcId;
            public ulong OwnerId;
            public Vector3 Position;
            public float FuelRemaining;
            public bool IsActive;
            public float Radius;
        }
        
        private class AlphaZombie
        {
            public ulong EntityId;
            public string Name;
            public Vector3 SpawnPosition;
            public float SpawnTime;
            public int Tier; // 1-5
            public bool IsAnnounced;
        }
        
        private class BountyTask
        {
            public string Id;
            public string Description;
            public BountyType Type;
            public int Target;
            public int RewardScrap;
            public string RewardItem;
            public int RewardItemAmount;
            public float ExpiresAt;
            public bool IsDaily;
        }
        
        private class ExtractionEvent
        {
            public Vector3 Position;
            public float StartTime;
            public float Duration;
            public List<ulong> Participants;
            public bool IsActive;
        }
        
        public enum BountyType
        {
            KillZombies,
            KillAlpha,
            SurviveBloodMoon,
            CollectTrophies,
            CompleteExtraction
        }
        #endregion

        #region Configuration
        private class Configuration
        {
            [JsonProperty("1. Zombie Evolution System")]
            public EvolutionSettings Evolution { get; set; } = new EvolutionSettings();
            
            [JsonProperty("2. Safe House System")]
            public SafeHouseSettings SafeHouses { get; set; } = new SafeHouseSettings();
            
            [JsonProperty("3. Zombie Trophy System")]
            public TrophySettings Trophies { get; set; } = new TrophySettings();
            
            [JsonProperty("4. Alpha Zombie Hunts")]
            public AlphaSettings Alpha { get; set; } = new AlphaSettings();
            
            [JsonProperty("5. Airdrop Horde System")]
            public AirdropSettings Airdrops { get; set; } = new AirdropSettings();
            
            [JsonProperty("6. Extraction Events")]
            public ExtractionSettings Extraction { get; set; } = new ExtractionSettings();
            
            [JsonProperty("7. Bounty Board System")]
            public BountySettings Bounties { get; set; } = new BountySettings();
            
            [JsonProperty("8. Infection Cure System")]
            public CureSettings Cure { get; set; } = new CureSettings();
            
            [JsonProperty("9. General Settings")]
            public GeneralSettings General { get; set; } = new GeneralSettings();
        }
        
        private class EvolutionSettings
        {
            [JsonProperty("Enable zombie evolution")]
            public bool Enabled { get; set; } = true;
            
            [JsonProperty("Days until max evolution (real days)")]
            public int DaysToMaxEvolution { get; set; } = 7;
            
            [JsonProperty("Base zombie health")]
            public float BaseHealth { get; set; } = 100f;
            
            [JsonProperty("Max evolved health multiplier")]
            public float MaxHealthMultiplier { get; set; } = 3f;
            
            [JsonProperty("Base zombie speed")]
            public float BaseSpeed { get; set; } = 3f;
            
            [JsonProperty("Max evolved speed multiplier")]
            public float MaxSpeedMultiplier { get; set; } = 1.8f;
            
            [JsonProperty("Base zombie damage")]
            public float BaseDamage { get; set; } = 20f;
            
            [JsonProperty("Max evolved damage multiplier")]
            public float MaxDamageMultiplier { get; set; } = 2.5f;
            
            [JsonProperty("Announce evolution milestones")]
            public bool AnnounceEvolution { get; set; } = true;
            
            [JsonProperty("Evolution stage names")]
            public List<string> StageNames { get; set; } = new List<string>
            {
                "Shambler", "Walker", "Runner", "Sprinter", "Nightmare"
            };
        }
        
        private class SafeHouseSettings
        {
            [JsonProperty("Enable safe house system")]
            public bool Enabled { get; set; } = true;
            
            [JsonProperty("Safe house radius (meters)")]
            public float Radius { get; set; } = 30f;
            
            [JsonProperty("Fuel consumption per hour")]
            public float FuelPerHour { get; set; } = 10f;
            
            [JsonProperty("Fuel item shortname")]
            public string FuelItem { get; set; } = "lowgradefuel";
            
            [JsonProperty("Max fuel capacity")]
            public float MaxFuel { get; set; } = 500f;
            
            [JsonProperty("Require generator item in TC")]
            public bool RequireGenerator { get; set; } = false;
            
            [JsonProperty("Generator item shortname")]
            public string GeneratorItem { get; set; } = "generator.small";
            
            [JsonProperty("Show safe house UI")]
            public bool ShowUI { get; set; } = true;
            
            [JsonProperty("Safe house activation command")]
            public string ActivateCommand { get; set; } = "safehouse";
        }
        
        private class TrophySettings
        {
            [JsonProperty("Enable trophy drops")]
            public bool Enabled { get; set; } = true;
            
            [JsonProperty("Trophy drop chance (0-100)")]
            public int DropChance { get; set; } = 25;
            
            [JsonProperty("Trophy types and values")]
            public Dictionary<string, TrophyInfo> TrophyTypes { get; set; } = new Dictionary<string, TrophyInfo>
            {
                { "zombie.ear", new TrophyInfo { DisplayName = "Zombie Ear", Value = 5, DropWeight = 50 } },
                { "zombie.tooth", new TrophyInfo { DisplayName = "Zombie Tooth", Value = 10, DropWeight = 30 } },
                { "zombie.finger", new TrophyInfo { DisplayName = "Zombie Finger", Value = 15, DropWeight = 15 } },
                { "zombie.heart", new TrophyInfo { DisplayName = "Zombie Heart", Value = 50, DropWeight = 5 } }
            };
            
            [JsonProperty("Trader monument names")]
            public List<string> TraderMonuments { get; set; } = new List<string> { "Outpost", "Bandit Camp" };
            
            [JsonProperty("Trade command")]
            public string TradeCommand { get; set; } = "ztrade";
        }
        
        private class TrophyInfo
        {
            public string DisplayName { get; set; }
            public int Value { get; set; }
            public int DropWeight { get; set; }
        }

        
        private class AlphaSettings
        {
            [JsonProperty("Enable alpha zombie hunts")]
            public bool Enabled { get; set; } = true;
            
            [JsonProperty("Alpha spawn interval (minutes)")]
            public float SpawnIntervalMinutes { get; set; } = 60f;
            
            [JsonProperty("Alpha spawn chance per interval (0-100)")]
            public int SpawnChance { get; set; } = 30;
            
            [JsonProperty("Announce alpha spawn")]
            public bool AnnounceSpawn { get; set; } = true;
            
            [JsonProperty("Announce alpha death")]
            public bool AnnounceDeath { get; set; } = true;
            
            [JsonProperty("Alpha health multiplier")]
            public float HealthMultiplier { get; set; } = 10f;
            
            [JsonProperty("Alpha damage multiplier")]
            public float DamageMultiplier { get; set; } = 3f;
            
            [JsonProperty("Alpha size scale")]
            public float SizeScale { get; set; } = 1.5f;
            
            [JsonProperty("Alpha names")]
            public List<string> AlphaNames { get; set; } = new List<string>
            {
                "The Butcher", "Rotting King", "Plague Bearer", "Death Walker", "The Abomination"
            };
            
            [JsonProperty("Alpha loot table")]
            public List<AlphaLoot> LootTable { get; set; } = new List<AlphaLoot>
            {
                new AlphaLoot { Shortname = "scrap", MinAmount = 15, MaxAmount = 30 },
                new AlphaLoot { Shortname = "metal.fragments", MinAmount = 50, MaxAmount = 100 },
                new AlphaLoot { Shortname = "metal.refined", MinAmount = 2, MaxAmount = 5 },
                new AlphaLoot { Shortname = "techparts", MinAmount = 1, MaxAmount = 2, Chance = 30 },
                new AlphaLoot { Shortname = "gears", MinAmount = 1, MaxAmount = 2, Chance = 40 },
                new AlphaLoot { Shortname = "sewingkit", MinAmount = 1, MaxAmount = 2, Chance = 35 },
                new AlphaLoot { Shortname = "roadsigns", MinAmount = 1, MaxAmount = 2, Chance = 25 },
                new AlphaLoot { Shortname = "smg.thompson", MinAmount = 1, MaxAmount = 1, Chance = 2 },
                new AlphaLoot { Shortname = "pistol.python", MinAmount = 1, MaxAmount = 1, Chance = 3 },
                new AlphaLoot { Shortname = "rifle.semiauto", MinAmount = 1, MaxAmount = 1, Chance = 1 }
            };
            
            [JsonProperty("Show alpha on map")]
            public bool ShowOnMap { get; set; } = true;
            
            [JsonProperty("Alpha despawn time (minutes)")]
            public float DespawnMinutes { get; set; } = 30f;
        }
        
        private class AlphaLoot
        {
            public string Shortname { get; set; }
            public int MinAmount { get; set; }
            public int MaxAmount { get; set; }
            public int Chance { get; set; } = 100;
        }
        
        private class AirdropSettings
        {
            [JsonProperty("Enable airdrop hordes")]
            public bool Enabled { get; set; } = true;
            
            [JsonProperty("Zombies per airdrop")]
            public int ZombiesPerDrop { get; set; } = 15;
            
            [JsonProperty("Spawn radius around airdrop")]
            public float SpawnRadius { get; set; } = 30f;
            
            [JsonProperty("Spawn delay after drop lands (seconds)")]
            public float SpawnDelay { get; set; } = 10f;
            
            [JsonProperty("Announce airdrop horde")]
            public bool AnnounceHorde { get; set; } = true;
            
            [JsonProperty("Horde announcement message")]
            public string HordeMessage { get; set; } = "<color=#ff4444>[!] A zombie horde has gathered around the airdrop!</color>";
            
            [JsonProperty("Include alpha chance (0-100)")]
            public int AlphaChance { get; set; } = 10;
        }
        
        private class ExtractionSettings
        {
            [JsonProperty("Enable extraction events")]
            public bool Enabled { get; set; } = true;
            
            [JsonProperty("Event interval (minutes)")]
            public float IntervalMinutes { get; set; } = 120f;
            
            [JsonProperty("Event duration (seconds)")]
            public float DurationSeconds { get; set; } = 300f;
            
            [JsonProperty("Warning time before event (seconds)")]
            public float WarningSeconds { get; set; } = 60f;
            
            [JsonProperty("Zombies during extraction")]
            public int ZombieCount { get; set; } = 25;
            
            [JsonProperty("Extraction radius")]
            public float Radius { get; set; } = 20f;
            
            [JsonProperty("Scrap reward for extraction")]
            public int ScrapReward { get; set; } = 500;
            
            [JsonProperty("Bonus loot items")]
            public List<string> BonusLoot { get; set; } = new List<string>
            {
                "supply.signal", "targeting.computer", "cctv.camera"
            };
            
            [JsonProperty("Announce extraction")]
            public bool Announce { get; set; } = true;
            
            [JsonProperty("Extraction start message")]
            public string StartMessage { get; set; } = "<color=#44ff44>[HELI] EXTRACTION EVENT! A helicopter is landing at {0}. Get there in {1} seconds!</color>";
            
            [JsonProperty("Extraction success message")]
            public string SuccessMessage { get; set; } = "<color=#44ff44>[OK] Extraction successful! {0} survivors extracted.</color>";
        }
        
        private class BountySettings
        {
            [JsonProperty("Enable bounty system")]
            public bool Enabled { get; set; } = true;
            
            [JsonProperty("Daily bounty count")]
            public int DailyBountyCount { get; set; } = 3;
            
            [JsonProperty("Weekly bounty count")]
            public int WeeklyBountyCount { get; set; } = 2;
            
            [JsonProperty("Bounty refresh hour (0-23)")]
            public int RefreshHour { get; set; } = 0;
            
            [JsonProperty("Show bounty UI")]
            public bool ShowUI { get; set; } = true;
            
            [JsonProperty("Bounty command")]
            public string BountyCommand { get; set; } = "bounty";
            
            [JsonProperty("Daily bounty templates")]
            public List<BountyTemplate> DailyTemplates { get; set; } = new List<BountyTemplate>
            {
                new BountyTemplate { Type = BountyType.KillZombies, MinTarget = 20, MaxTarget = 50, ScrapPerTarget = 2 },
                new BountyTemplate { Type = BountyType.CollectTrophies, MinTarget = 5, MaxTarget = 15, ScrapPerTarget = 10 },
                new BountyTemplate { Type = BountyType.KillZombies, MinTarget = 10, MaxTarget = 30, ScrapPerTarget = 3, BonusItem = "bandage", BonusAmount = 5 }
            };
            
            [JsonProperty("Weekly bounty templates")]
            public List<BountyTemplate> WeeklyTemplates { get; set; } = new List<BountyTemplate>
            {
                new BountyTemplate { Type = BountyType.KillZombies, MinTarget = 200, MaxTarget = 500, ScrapPerTarget = 1, BonusItem = "rifle.ak", BonusAmount = 1 },
                new BountyTemplate { Type = BountyType.KillAlpha, MinTarget = 3, MaxTarget = 5, ScrapPerTarget = 100 },
                new BountyTemplate { Type = BountyType.CompleteExtraction, MinTarget = 2, MaxTarget = 3, ScrapPerTarget = 200 }
            };
        }
        
        private class BountyTemplate
        {
            public BountyType Type { get; set; }
            public int MinTarget { get; set; }
            public int MaxTarget { get; set; }
            public int ScrapPerTarget { get; set; }
            public string BonusItem { get; set; }
            public int BonusAmount { get; set; }
        }
        
        private class CureSettings
        {
            [JsonProperty("Enable cure crafting")]
            public bool Enabled { get; set; } = true;
            
            [JsonProperty("Cure recipe ingredients")]
            public Dictionary<string, int> Recipe { get; set; } = new Dictionary<string, int>
            {
                { "syringe.medical", 5 },
                { "blood", 10 },
                { "cloth", 50 },
                { "lowgradefuel", 100 }
            };
            
            [JsonProperty("Cure command")]
            public string CureCommand { get; set; } = "craftcure";
            
            [JsonProperty("Cure grants permanent immunity")]
            public bool PermanentImmunity { get; set; } = true;
            
            [JsonProperty("Cure announcement")]
            public string CureMessage { get; set; } = "<color=#44ff44>[CURE] {0} has crafted the ZOMBIE CURE and is now immune!</color>";
        }
        
        private class GeneralSettings
        {
            [JsonProperty("Enable debug logging")]
            public bool Debug { get; set; } = false;
            
            [JsonProperty("Use Economics for rewards")]
            public bool UseEconomics { get; set; } = false;
            
            [JsonProperty("Use ServerRewards for rewards")]
            public bool UseServerRewards { get; set; } = false;
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
            public Dictionary<ulong, PlayerData> Players = new Dictionary<ulong, PlayerData>();
            public Dictionary<ulong, SafeHouse> SafeHouses = new Dictionary<ulong, SafeHouse>();
            public List<BountyTask> ActiveBounties = new List<BountyTask>();
            public DateTime LastBountyRefresh = DateTime.MinValue;
            public DateTime WipeStart = DateTime.UtcNow;
        }
        
        private void LoadData()
        {
            storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>("ZombiePVETier1");
            if (storedData == null) 
            {
                storedData = new StoredData();
                storedData.WipeStart = DateTime.UtcNow;
            }
            
            playerData = storedData.Players;
            safeHouses = storedData.SafeHouses;
            activeBounties = storedData.ActiveBounties;
        }
        
        private void SaveData()
        {
            storedData.Players = playerData;
            storedData.SafeHouses = safeHouses;
            storedData.ActiveBounties = activeBounties;
            Interface.Oxide.DataFileSystem.WriteObject("ZombiePVETier1", storedData);
        }
        
        private PlayerData GetPlayerData(ulong playerId)
        {
            if (!playerData.ContainsKey(playerId))
                playerData[playerId] = new PlayerData();
            return playerData[playerId];
        }
        #endregion


        #region Oxide Hooks
        private void OnServerInitialized()
        {
            permission.RegisterPermission(PERM_ADMIN, this);
            permission.RegisterPermission(PERM_VIP, this);
            
            LoadData();
            
            // Register commands
            if (config.SafeHouses.Enabled)
                cmd.AddChatCommand(config.SafeHouses.ActivateCommand, this, nameof(CmdSafeHouse));
            
            if (config.Trophies.Enabled)
                cmd.AddChatCommand(config.Trophies.TradeCommand, this, nameof(CmdTrade));
            
            if (config.Bounties.Enabled)
                cmd.AddChatCommand(config.Bounties.BountyCommand, this, nameof(CmdBounty));
            
            if (config.Cure.Enabled)
                cmd.AddChatCommand(config.Cure.CureCommand, this, nameof(CmdCraftCure));
            
            // Start timers
            StartEvolutionTimer();
            StartSafeHouseTimer();
            StartAlphaSpawnTimer();
            StartBountyRefreshTimer();
            
            // Check if bounties need refresh
            CheckBountyRefresh();
            
            PrintStartupMessage();
        }

        private void Unload()
        {
            SaveData();
            
            evolutionTimer?.Destroy();
            bountyRefreshTimer?.Destroy();
            safeHouseTimer?.Destroy();
            alphaSpawnTimer?.Destroy();
            
            // Clean up UI
            foreach (var player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, BOUNTY_UI);
                CuiHelper.DestroyUi(player, SAFEHOUSE_UI);
            }
            
            // Kill alpha zombies
            foreach (var alpha in alphaZombies.Values.ToList())
            {
                var entity = BaseNetworkable.serverEntities.Find(new NetworkableId(alpha.EntityId));
                entity?.Kill();
            }
        }

        private void OnServerSave() => SaveData();

        private void OnNewSave(string filename)
        {
            // Reset wipe data
            storedData = new StoredData();
            storedData.WipeStart = DateTime.UtcNow;
            playerData.Clear();
            safeHouses.Clear();
            activeBounties.Clear();
            SaveData();
            
            Puts("Wipe detected - reset all Tier1 data");
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
            if (player == null) return;
            CuiHelper.DestroyUi(player, BOUNTY_UI);
            CuiHelper.DestroyUi(player, SAFEHOUSE_UI);
        }
        #endregion

        #region Zombie Evolution System
        private void StartEvolutionTimer()
        {
            if (!config.Evolution.Enabled) return;
            
            evolutionTimer?.Destroy();
            evolutionTimer = timer.Every(3600f, CheckEvolutionMilestone); // Check every hour
        }
        
        private float GetEvolutionMultiplier()
        {
            if (!config.Evolution.Enabled) return 1f;
            
            double daysSinceWipe = (DateTime.UtcNow - storedData.WipeStart).TotalDays;
            float progress = Mathf.Clamp01((float)daysSinceWipe / config.Evolution.DaysToMaxEvolution);
            
            return progress;
        }
        
        private int GetEvolutionStage()
        {
            float progress = GetEvolutionMultiplier();
            int stages = config.Evolution.StageNames.Count;
            return Mathf.Clamp(Mathf.FloorToInt(progress * stages), 0, stages - 1);
        }
        
        private string GetEvolutionStageName()
        {
            int stage = GetEvolutionStage();
            if (stage < config.Evolution.StageNames.Count)
                return config.Evolution.StageNames[stage];
            return "Unknown";
        }
        
        public float GetEvolvedHealth()
        {
            float mult = 1f + (GetEvolutionMultiplier() * (config.Evolution.MaxHealthMultiplier - 1f));
            return config.Evolution.BaseHealth * mult;
        }
        
        public float GetEvolvedSpeed()
        {
            float mult = 1f + (GetEvolutionMultiplier() * (config.Evolution.MaxSpeedMultiplier - 1f));
            return config.Evolution.BaseSpeed * mult;
        }
        
        public float GetEvolvedDamage()
        {
            float mult = 1f + (GetEvolutionMultiplier() * (config.Evolution.MaxDamageMultiplier - 1f));
            return config.Evolution.BaseDamage * mult;
        }
        
        private void CheckEvolutionMilestone()
        {
            if (!config.Evolution.AnnounceEvolution) return;
            
            int currentStage = GetEvolutionStage();
            string stageName = GetEvolutionStageName();
            
            // Could track last announced stage and only announce on change
            DebugLog($"Evolution check: Stage {currentStage} ({stageName})");
        }
        
        [ChatCommand("zevolution")]
        private void CmdEvolution(BasePlayer player, string command, string[] args)
        {
            if (!config.Evolution.Enabled)
            {
                SendReply(player, "<color=#ff4444>Evolution system is disabled.</color>");
                return;
            }
            
            int stage = GetEvolutionStage();
            string stageName = GetEvolutionStageName();
            float progress = GetEvolutionMultiplier() * 100f;
            
            SendReply(player, 
                $"<color=#ffcc00>═══ ZOMBIE EVOLUTION ═══</color>\n" +
                $"Current Stage: <color=#ff4444>{stageName}</color> ({stage + 1}/{config.Evolution.StageNames.Count})\n" +
                $"Evolution Progress: <color=#ffaa00>{progress:F1}%</color>\n" +
                $"Zombie Health: <color=#ff4444>{GetEvolvedHealth():F0}</color>\n" +
                $"Zombie Speed: <color=#ff4444>{GetEvolvedSpeed():F1}</color>\n" +
                $"Zombie Damage: <color=#ff4444>{GetEvolvedDamage():F0}</color>");
        }
        #endregion

        #region Safe House System
        private void StartSafeHouseTimer()
        {
            if (!config.SafeHouses.Enabled) return;
            
            safeHouseTimer?.Destroy();
            safeHouseTimer = timer.Every(60f, ProcessSafeHouses); // Every minute
        }
        
        private void ProcessSafeHouses()
        {
            float fuelPerMinute = config.SafeHouses.FuelPerHour / 60f;
            var toRemove = new List<uint>();
            
            foreach (var kvp in safeHouses)
            {
                var sh = kvp.Value;
                if (!sh.IsActive) continue;
                
                sh.FuelRemaining -= fuelPerMinute;
                
                if (sh.FuelRemaining <= 0)
                {
                    sh.FuelRemaining = 0;
                    sh.IsActive = false;
                    
                    var owner = BasePlayer.FindByID(sh.OwnerId);
                    if (owner != null)
                        SendReply(owner, "<color=#ff4444>[!] Your safe house has run out of fuel!</color>");
                }
            }
            
            foreach (var id in toRemove)
                safeHouses.Remove(id);
        }
        
        private void CmdSafeHouse(BasePlayer player, string command, string[] args)
        {
            if (!config.SafeHouses.Enabled)
            {
                SendReply(player, "<color=#ff4444>Safe house system is disabled.</color>");
                return;
            }
            
            // Find player's TC
            var tc = FindPlayerTC(player);
            if (tc == null)
            {
                SendReply(player, "<color=#ff4444>You must be near your Tool Cupboard to manage your safe house.</color>");
                return;
            }
            
            ulong tcId = tc.net.ID.Value;
            
            if (args.Length > 0)
            {
                switch (args[0].ToLower())
                {
                    case "activate":
                    case "on":
                        ActivateSafeHouse(player, tc);
                        return;
                    case "deactivate":
                    case "off":
                        DeactivateSafeHouse(player, tc);
                        return;
                    case "fuel":
                    case "refuel":
                        RefuelSafeHouse(player, tc);
                        return;
                }
            }
            
            // Show status
            ShowSafeHouseStatus(player, tc);
        }
        
        private void ActivateSafeHouse(BasePlayer player, BuildingPrivlidge tc)
        {
            ulong tcId = tc.net.ID.Value;
            
            if (!safeHouses.ContainsKey(tcId))
            {
                safeHouses[tcId] = new SafeHouse
                {
                    TcId = tcId,
                    OwnerId = player.userID,
                    Position = tc.transform.position,
                    FuelRemaining = 0,
                    IsActive = false,
                    Radius = config.SafeHouses.Radius
                };
            }
            
            var sh = safeHouses[tcId];
            
            if (sh.FuelRemaining <= 0)
            {
                SendReply(player, "<color=#ff4444>No fuel! Use /" + config.SafeHouses.ActivateCommand + " fuel to add fuel.</color>");
                return;
            }
            
            sh.IsActive = true;
            SendReply(player, $"<color=#44ff44>[OK] Safe house ACTIVATED! Zombies cannot enter within {sh.Radius}m.</color>");
            SendReply(player, $"<color=#888888>Fuel remaining: {sh.FuelRemaining:F0} ({sh.FuelRemaining / config.SafeHouses.FuelPerHour:F1} hours)</color>");
        }
        
        private void DeactivateSafeHouse(BasePlayer player, BuildingPrivlidge tc)
        {
            ulong tcId = tc.net.ID.Value;
            
            if (!safeHouses.ContainsKey(tcId))
            {
                SendReply(player, "<color=#ff4444>No safe house registered here.</color>");
                return;
            }
            
            safeHouses[tcId].IsActive = false;
            SendReply(player, "<color=#ffaa00>Safe house deactivated. Fuel consumption paused.</color>");
        }
        
        private void RefuelSafeHouse(BasePlayer player, BuildingPrivlidge tc)
        {
            ulong tcId = tc.net.ID.Value;
            
            if (!safeHouses.ContainsKey(tcId))
            {
                safeHouses[tcId] = new SafeHouse
                {
                    TcId = tcId,
                    OwnerId = player.userID,
                    Position = tc.transform.position,
                    FuelRemaining = 0,
                    IsActive = false,
                    Radius = config.SafeHouses.Radius
                };
            }
            
            var sh = safeHouses[tcId];
            string fuelItem = config.SafeHouses.FuelItem;
            
            int fuelInInventory = player.inventory.GetAmount(ItemManager.FindItemDefinition(fuelItem).itemid);
            if (fuelInInventory <= 0)
            {
                SendReply(player, $"<color=#ff4444>You need {fuelItem} to fuel your safe house.</color>");
                return;
            }
            
            float spaceAvailable = config.SafeHouses.MaxFuel - sh.FuelRemaining;
            int fuelToAdd = (int)Mathf.Min(fuelInInventory, spaceAvailable);
            
            if (fuelToAdd <= 0)
            {
                SendReply(player, "<color=#ffaa00>Safe house fuel tank is full!</color>");
                return;
            }
            
            player.inventory.Take(null, ItemManager.FindItemDefinition(fuelItem).itemid, fuelToAdd);
            sh.FuelRemaining += fuelToAdd;
            
            SendReply(player, $"<color=#44ff44>Added {fuelToAdd} fuel. Total: {sh.FuelRemaining:F0}/{config.SafeHouses.MaxFuel}</color>");
        }
        
        private void ShowSafeHouseStatus(BasePlayer player, BuildingPrivlidge tc)
        {
            ulong tcId = tc.net.ID.Value;
            
            if (!safeHouses.ContainsKey(tcId))
            {
                SendReply(player, 
                    $"<color=#ffcc00>═══ SAFE HOUSE ═══</color>\n" +
                    $"Status: <color=#888888>Not Registered</color>\n" +
                    $"Use <color=#44ff44>/{config.SafeHouses.ActivateCommand} fuel</color> to add fuel and register.");
                return;
            }
            
            var sh = safeHouses[tcId];
            string status = sh.IsActive ? "<color=#44ff44>ACTIVE</color>" : "<color=#ff4444>INACTIVE</color>";
            float hoursRemaining = sh.FuelRemaining / config.SafeHouses.FuelPerHour;
            
            SendReply(player, 
                $"<color=#ffcc00>═══ SAFE HOUSE ═══</color>\n" +
                $"Status: {status}\n" +
                $"Fuel: <color=#ffaa00>{sh.FuelRemaining:F0}/{config.SafeHouses.MaxFuel}</color> ({hoursRemaining:F1} hours)\n" +
                $"Radius: <color=#44ff44>{sh.Radius}m</color>\n\n" +
                $"Commands:\n" +
                $"<color=#888888>/{config.SafeHouses.ActivateCommand} on</color> - Activate\n" +
                $"<color=#888888>/{config.SafeHouses.ActivateCommand} off</color> - Deactivate\n" +
                $"<color=#888888>/{config.SafeHouses.ActivateCommand} fuel</color> - Add fuel");
        }
        
        private BuildingPrivlidge FindPlayerTC(BasePlayer player)
        {
            var tcs = UnityEngine.Object.FindObjectsOfType<BuildingPrivlidge>();
            foreach (var tc in tcs)
            {
                if (tc.IsAuthed(player) && Vector3.Distance(tc.transform.position, player.transform.position) < 20f)
                    return tc;
            }
            return null;
        }
        
        // Hook for ZombiePVECore to check if position is in safe house
        public bool IsInSafeHouse(Vector3 position)
        {
            if (!config.SafeHouses.Enabled) return false;
            
            foreach (var sh in safeHouses.Values)
            {
                if (!sh.IsActive) continue;
                if (Vector3.Distance(position, sh.Position) <= sh.Radius)
                    return true;
            }
            return false;
        }
        #endregion


        #region Trophy System
        private void CmdTrade(BasePlayer player, string command, string[] args)
        {
            if (!config.Trophies.Enabled)
            {
                SendReply(player, "<color=#ff4444>Trophy trading is disabled.</color>");
                return;
            }
            
            // Check if player is at a trader monument
            if (!IsAtTraderMonument(player))
            {
                SendReply(player, $"<color=#ff4444>You must be at a trader location ({string.Join(", ", config.Trophies.TraderMonuments)}) to trade.</color>");
                return;
            }
            
            var data = GetPlayerData(player.userID);
            
            if (args.Length > 0 && args[0].ToLower() == "sell")
            {
                SellAllTrophies(player, data);
                return;
            }
            
            // Show trophy inventory
            ShowTrophyInventory(player, data);
        }
        
        private void ShowTrophyInventory(BasePlayer player, PlayerData data)
        {
            string msg = "<color=#ffcc00>═══ ZOMBIE TROPHIES ═══</color>\n";
            int totalValue = 0;
            
            foreach (var trophy in config.Trophies.TrophyTypes)
            {
                int count = data.ZombieTrophies.ContainsKey(trophy.Key) ? data.ZombieTrophies[trophy.Key] : 0;
                int value = count * trophy.Value.Value;
                totalValue += value;
                
                msg += $"<color=#888888>{trophy.Value.DisplayName}:</color> {count} (<color=#44ff44>{value} scrap</color>)\n";
            }
            
            msg += $"\n<color=#ffaa00>Total Value: {totalValue} scrap</color>\n";
            msg += $"<color=#888888>Use /{config.Trophies.TradeCommand} sell to sell all trophies.</color>";
            
            SendReply(player, msg);
        }
        
        private void SellAllTrophies(BasePlayer player, PlayerData data)
        {
            int totalValue = 0;
            int totalTrophies = 0;
            
            foreach (var trophy in config.Trophies.TrophyTypes)
            {
                if (data.ZombieTrophies.ContainsKey(trophy.Key))
                {
                    int count = data.ZombieTrophies[trophy.Key];
                    totalValue += count * trophy.Value.Value;
                    totalTrophies += count;
                    data.ZombieTrophies[trophy.Key] = 0;
                }
            }
            
            if (totalTrophies == 0)
            {
                SendReply(player, "<color=#ff4444>You have no trophies to sell.</color>");
                return;
            }
            
            GiveReward(player, totalValue, null, 0);
            SendReply(player, $"<color=#44ff44>Sold {totalTrophies} trophies for {totalValue} scrap!</color>");
            SaveData();
        }
        
        private bool IsAtTraderMonument(BasePlayer player)
        {
            // Simple check - could be improved with actual monument detection
            foreach (var monumentName in config.Trophies.TraderMonuments)
            {
                // Check if player is in a safe zone (Outpost/Bandit)
                if (player.InSafeZone())
                    return true;
            }
            return false;
        }
        
        private void DropTrophy(Vector3 position, BasePlayer killer)
        {
            if (!config.Trophies.Enabled) return;
            if (UnityEngine.Random.Range(0, 100) >= config.Trophies.DropChance) return;
            
            // Calculate total weight
            int totalWeight = 0;
            foreach (var trophy in config.Trophies.TrophyTypes.Values)
                totalWeight += trophy.DropWeight;
            
            // Pick random trophy
            int roll = UnityEngine.Random.Range(0, totalWeight);
            int cumulative = 0;
            
            foreach (var kvp in config.Trophies.TrophyTypes)
            {
                cumulative += kvp.Value.DropWeight;
                if (roll < cumulative)
                {
                    // Add to player's trophy collection
                    var data = GetPlayerData(killer.userID);
                    if (!data.ZombieTrophies.ContainsKey(kvp.Key))
                        data.ZombieTrophies[kvp.Key] = 0;
                    data.ZombieTrophies[kvp.Key]++;
                    
                    SendReply(killer, $"<color=#ffaa00>[TROPHY] Collected: {kvp.Value.DisplayName}</color>");
                    break;
                }
            }
        }
        #endregion

        #region Alpha Zombie System
        private void StartAlphaSpawnTimer()
        {
            if (!config.Alpha.Enabled) return;
            
            alphaSpawnTimer?.Destroy();
            alphaSpawnTimer = timer.Every(config.Alpha.SpawnIntervalMinutes * 60f, TrySpawnAlpha);
        }
        
        private void TrySpawnAlpha()
        {
            if (alphaZombies.Count > 0) return; // Only one alpha at a time
            if (UnityEngine.Random.Range(0, 100) >= config.Alpha.SpawnChance) return;
            
            // Find spawn position near a random player
            var players = BasePlayer.activePlayerList.Where(p => p != null && !p.IsSleeping() && !p.IsAdmin).ToList();
            if (players.Count == 0) return;
            
            var targetPlayer = players[UnityEngine.Random.Range(0, players.Count)];
            Vector3 spawnPos = GetSpawnPositionNear(targetPlayer.transform.position, 100f, 200f);
            
            if (spawnPos == Vector3.zero) return;
            
            SpawnAlphaZombie(spawnPos);
        }
        
        private void SpawnAlphaZombie(Vector3 position)
        {
            var zombie = GameManager.server.CreateEntity(ZOMBIE_PREFAB, position, Quaternion.identity) as ScarecrowNPC;
            if (zombie == null) return;
            
            zombie.Spawn();
            
            string alphaName = config.Alpha.AlphaNames[UnityEngine.Random.Range(0, config.Alpha.AlphaNames.Count)];
            
            // Apply alpha stats
            float health = GetEvolvedHealth() * config.Alpha.HealthMultiplier;
            zombie.SetMaxHealth(health);
            zombie.SetHealth(health);
            zombie.displayName = $"<color=#ff0000>[ALPHA] {alphaName}</color>";
            
            // Scale size
            zombie.transform.localScale = Vector3.one * config.Alpha.SizeScale;
            
            var alpha = new AlphaZombie
            {
                EntityId = zombie.net.ID.Value,
                Name = alphaName,
                SpawnPosition = position,
                SpawnTime = Time.realtimeSinceStartup,
                Tier = GetEvolutionStage() + 1,
                IsAnnounced = false
            };
            
            alphaZombies[zombie.net.ID.Value] = alpha;
            
            // Announce
            if (config.Alpha.AnnounceSpawn)
            {
                string grid = GetGridPosition(position);
                PrintToChat($"<color=#ff0000>[ALPHA] ALPHA ZOMBIE '{alphaName}' has spawned near {grid}! Hunt it down for rare loot!</color>");
            }
            
            // Set despawn timer
            timer.Once(config.Alpha.DespawnMinutes * 60f, () =>
            {
                if (alphaZombies.ContainsKey(alpha.EntityId))
                {
                    var entity = BaseNetworkable.serverEntities.Find(new NetworkableId(alpha.EntityId));
                    if (entity != null && !entity.IsDestroyed)
                    {
                        entity.Kill();
                        PrintToChat($"<color=#888888>The Alpha Zombie '{alphaName}' has retreated into the darkness...</color>");
                    }
                    alphaZombies.Remove(alpha.EntityId);
                }
            });
        }
        
        private void OnAlphaKilled(ulong alphaId, BasePlayer killer)
        {
            if (!alphaZombies.TryGetValue(alphaId, out var alpha)) return;
            
            alphaZombies.Remove(alphaId);
            
            // Announce
            if (config.Alpha.AnnounceDeath && killer != null)
            {
                PrintToChat($"<color=#44ff44>[VICTORY] {killer.displayName} has slain the Alpha Zombie '{alpha.Name}'!</color>");
            }
            
            // Update player stats
            if (killer != null)
            {
                var data = GetPlayerData(killer.userID);
                data.AlphaKills++;
                
                // Update bounty progress
                UpdateBountyProgress(killer.userID, BountyType.KillAlpha, 1);
            }
            
            // Drop loot
            if (killer != null)
            {
                var entity = BaseNetworkable.serverEntities.Find(new NetworkableId(alphaId));
                if (entity != null)
                    DropAlphaLoot(entity.transform.position, killer);
            }
        }
        
        private void DropAlphaLoot(Vector3 position, BasePlayer killer)
        {
            foreach (var loot in config.Alpha.LootTable)
            {
                if (UnityEngine.Random.Range(0, 100) >= loot.Chance) continue;
                
                int amount = UnityEngine.Random.Range(loot.MinAmount, loot.MaxAmount + 1);
                var item = ItemManager.CreateByName(loot.Shortname, amount);
                if (item != null)
                {
                    Vector3 dropPos = position + Vector3.up * 0.5f + UnityEngine.Random.insideUnitSphere * 0.5f;
                    dropPos.y = position.y + 0.5f;
                    item.Drop(dropPos, Vector3.up * 2f);
                }
            }
        }
        
        [ChatCommand("zalpha")]
        private void CmdAlpha(BasePlayer player, string command, string[] args)
        {
            if (!config.Alpha.Enabled)
            {
                SendReply(player, "<color=#ff4444>Alpha zombie system is disabled.</color>");
                return;
            }
            
            if (alphaZombies.Count == 0)
            {
                SendReply(player, "<color=#888888>No Alpha Zombie is currently active.</color>");
                return;
            }
            
            foreach (var alpha in alphaZombies.Values)
            {
                string grid = GetGridPosition(alpha.SpawnPosition);
                float timeAlive = (Time.realtimeSinceStartup - alpha.SpawnTime) / 60f;
                float timeRemaining = config.Alpha.DespawnMinutes - timeAlive;
                
                SendReply(player, 
                    $"<color=#ff0000>[ALPHA] {alpha.Name}</color>\n" +
                    $"Location: <color=#ffaa00>{grid}</color>\n" +
                    $"Time remaining: <color=#ff4444>{timeRemaining:F0} minutes</color>");
            }
        }
        #endregion

        #region Airdrop Horde System
        private void OnAirdropLanded(SupplyDrop drop)
        {
            if (!config.Airdrops.Enabled) return;
            if (drop == null) return;
            
            timer.Once(config.Airdrops.SpawnDelay, () =>
            {
                if (drop == null || drop.IsDestroyed) return;
                SpawnAirdropHorde(drop.transform.position);
            });
        }
        
        private void SpawnAirdropHorde(Vector3 position)
        {
            if (config.Airdrops.AnnounceHorde)
                PrintToChat(config.Airdrops.HordeMessage);
            
            int zombieCount = config.Airdrops.ZombiesPerDrop;
            bool spawnAlpha = UnityEngine.Random.Range(0, 100) < config.Airdrops.AlphaChance;
            
            for (int i = 0; i < zombieCount; i++)
            {
                Vector3 spawnPos = GetSpawnPositionNear(position, 5f, config.Airdrops.SpawnRadius);
                if (spawnPos == Vector3.zero) continue;
                
                timer.Once(i * 0.3f, () =>
                {
                    var zombie = GameManager.server.CreateEntity(ZOMBIE_PREFAB, spawnPos, Quaternion.identity) as ScarecrowNPC;
                    if (zombie == null) return;
                    
                    zombie.Spawn();
                    
                    // Apply evolved stats
                    zombie.SetMaxHealth(GetEvolvedHealth());
                    zombie.SetHealth(GetEvolvedHealth());
                });
            }
            
            // Spawn alpha if lucky
            if (spawnAlpha && config.Alpha.Enabled)
            {
                timer.Once(zombieCount * 0.3f + 1f, () =>
                {
                    SpawnAlphaZombie(position + Vector3.up * 2f);
                });
            }
        }
        #endregion


        #region Extraction Events
        private Timer extractionTimer;
        
        private void StartExtractionTimer()
        {
            if (!config.Extraction.Enabled) return;
            
            extractionTimer?.Destroy();
            extractionTimer = timer.Every(config.Extraction.IntervalMinutes * 60f, TryStartExtraction);
        }
        
        private void TryStartExtraction()
        {
            if (activeExtractions.Count > 0) return; // Only one at a time
            if (BasePlayer.activePlayerList.Count < 1) return;
            
            // Find random position
            Vector3 position = GetRandomMapPosition();
            if (position == Vector3.zero) return;
            
            StartExtractionEvent(position);
        }
        
        private void StartExtractionEvent(Vector3 position)
        {
            string grid = GetGridPosition(position);
            
            // Announce warning
            if (config.Extraction.Announce)
            {
                string msg = config.Extraction.StartMessage
                    .Replace("{0}", grid)
                    .Replace("{1}", config.Extraction.WarningSeconds.ToString());
                PrintToChat(msg);
            }
            
            // Create extraction event after warning
            timer.Once(config.Extraction.WarningSeconds, () =>
            {
                var extraction = new ExtractionEvent
                {
                    Position = position,
                    StartTime = Time.realtimeSinceStartup,
                    Duration = config.Extraction.DurationSeconds,
                    Participants = new List<ulong>(),
                    IsActive = true
                };
                
                ulong eventId = (ulong)Time.realtimeSinceStartup;
                activeExtractions[eventId] = extraction;
                
                // Spawn zombies
                SpawnExtractionZombies(position);
                
                // Spawn helicopter effect
                SpawnExtractionHelicopter(position);
                
                // End timer
                timer.Once(config.Extraction.DurationSeconds, () =>
                {
                    EndExtractionEvent(eventId);
                });
                
                // Check for players in zone periodically
                timer.Repeat(1f, (int)config.Extraction.DurationSeconds, () =>
                {
                    if (!extraction.IsActive) return;
                    CheckExtractionParticipants(extraction);
                });
            });
        }
        
        private void SpawnExtractionZombies(Vector3 position)
        {
            int waves = 3;
            int zombiesPerWave = config.Extraction.ZombieCount / waves;
            
            for (int wave = 0; wave < waves; wave++)
            {
                float delay = wave * (config.Extraction.DurationSeconds / waves);
                
                timer.Once(delay, () =>
                {
                    for (int i = 0; i < zombiesPerWave; i++)
                    {
                        Vector3 spawnPos = GetSpawnPositionNear(position, config.Extraction.Radius + 10f, config.Extraction.Radius + 30f);
                        if (spawnPos == Vector3.zero) continue;
                        
                        timer.Once(i * 0.2f, () =>
                        {
                            var zombie = GameManager.server.CreateEntity(ZOMBIE_PREFAB, spawnPos, Quaternion.identity) as ScarecrowNPC;
                            if (zombie == null) return;
                            
                            zombie.Spawn();
                            zombie.SetMaxHealth(GetEvolvedHealth());
                            zombie.SetHealth(GetEvolvedHealth());
                        });
                    }
                });
            }
        }
        
        private void SpawnExtractionHelicopter(Vector3 position)
        {
            // Visual effect - spawn a patrol helicopter that hovers (doesn't attack)
            Effect.server.Run("assets/prefabs/npc/patrol helicopter/effects/rocket_fire.prefab", position + Vector3.up * 20f);
        }
        
        private void CheckExtractionParticipants(ExtractionEvent extraction)
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player == null || player.IsDead()) continue;
                
                float distance = Vector3.Distance(player.transform.position, extraction.Position);
                if (distance <= config.Extraction.Radius)
                {
                    if (!extraction.Participants.Contains(player.userID))
                    {
                        extraction.Participants.Add(player.userID);
                        SendReply(player, "<color=#44ff44>You are in the extraction zone! Survive until extraction!</color>");
                    }
                }
            }
        }
        
        private void EndExtractionEvent(ulong eventId)
        {
            if (!activeExtractions.TryGetValue(eventId, out var extraction)) return;
            
            extraction.IsActive = false;
            activeExtractions.Remove(eventId);
            
            // Reward participants still in zone
            int extracted = 0;
            foreach (var playerId in extraction.Participants)
            {
                var player = BasePlayer.FindByID(playerId);
                if (player == null || player.IsDead()) continue;
                
                float distance = Vector3.Distance(player.transform.position, extraction.Position);
                if (distance <= config.Extraction.Radius * 1.5f) // Slightly larger for end check
                {
                    extracted++;
                    
                    // Give rewards
                    GiveReward(player, config.Extraction.ScrapReward, null, 0);
                    
                    // Bonus loot
                    if (config.Extraction.BonusLoot.Count > 0)
                    {
                        string bonusItem = config.Extraction.BonusLoot[UnityEngine.Random.Range(0, config.Extraction.BonusLoot.Count)];
                        var item = ItemManager.CreateByName(bonusItem, 1);
                        if (item != null)
                            player.GiveItem(item);
                    }
                    
                    // Update stats
                    var data = GetPlayerData(playerId);
                    data.ExtractionsCompleted++;
                    UpdateBountyProgress(playerId, BountyType.CompleteExtraction, 1);
                    
                    SendReply(player, $"<color=#44ff44>[OK] EXTRACTED! +{config.Extraction.ScrapReward} scrap</color>");
                }
            }
            
            // Announce
            if (config.Extraction.Announce && extracted > 0)
            {
                string msg = config.Extraction.SuccessMessage.Replace("{0}", extracted.ToString());
                PrintToChat(msg);
            }
            else if (extracted == 0)
            {
                PrintToChat("<color=#ff4444>Extraction failed - no survivors made it.</color>");
            }
        }
        
        [ChatCommand("extraction")]
        private void CmdExtraction(BasePlayer player, string command, string[] args)
        {
            if (!config.Extraction.Enabled)
            {
                SendReply(player, "<color=#ff4444>Extraction events are disabled.</color>");
                return;
            }
            
            if (activeExtractions.Count == 0)
            {
                SendReply(player, "<color=#888888>No extraction event is currently active.</color>");
                return;
            }
            
            foreach (var extraction in activeExtractions.Values)
            {
                string grid = GetGridPosition(extraction.Position);
                float timeRemaining = extraction.Duration - (Time.realtimeSinceStartup - extraction.StartTime);
                float distance = Vector3.Distance(player.transform.position, extraction.Position);
                
                SendReply(player, 
                    $"<color=#44ff44>[HELI] EXTRACTION ACTIVE</color>\n" +
                    $"Location: <color=#ffaa00>{grid}</color>\n" +
                    $"Distance: <color=#ffaa00>{distance:F0}m</color>\n" +
                    $"Time remaining: <color=#ff4444>{timeRemaining:F0}s</color>");
            }
        }
        
        [ConsoleCommand("zt1.extraction")]
        private void CmdAdminExtraction(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin) return;
            
            var player = arg.Player();
            if (player == null)
            {
                arg.ReplyWith("Must be run as a player");
                return;
            }
            
            StartExtractionEvent(player.transform.position + Vector3.forward * 20f);
            arg.ReplyWith("Extraction event started!");
        }
        #endregion

        #region Bounty System
        private void StartBountyRefreshTimer()
        {
            if (!config.Bounties.Enabled) return;
            
            bountyRefreshTimer?.Destroy();
            bountyRefreshTimer = timer.Every(3600f, CheckBountyRefresh); // Check every hour
        }
        
        private void CheckBountyRefresh()
        {
            if (!config.Bounties.Enabled) return;
            
            DateTime now = DateTime.UtcNow;
            DateTime lastRefresh = storedData.LastBountyRefresh;
            
            // Check if we need to refresh (new day)
            if (now.Date > lastRefresh.Date || activeBounties.Count == 0)
            {
                RefreshBounties();
                storedData.LastBountyRefresh = now;
                SaveData();
            }
        }
        
        private void RefreshBounties()
        {
            activeBounties.Clear();
            
            // Generate daily bounties
            for (int i = 0; i < config.Bounties.DailyBountyCount; i++)
            {
                if (config.Bounties.DailyTemplates.Count == 0) break;
                
                var template = config.Bounties.DailyTemplates[UnityEngine.Random.Range(0, config.Bounties.DailyTemplates.Count)];
                var bounty = GenerateBounty(template, true);
                activeBounties.Add(bounty);
            }
            
            // Generate weekly bounties
            for (int i = 0; i < config.Bounties.WeeklyBountyCount; i++)
            {
                if (config.Bounties.WeeklyTemplates.Count == 0) break;
                
                var template = config.Bounties.WeeklyTemplates[UnityEngine.Random.Range(0, config.Bounties.WeeklyTemplates.Count)];
                var bounty = GenerateBounty(template, false);
                activeBounties.Add(bounty);
            }
            
            DebugLog($"Refreshed bounties: {activeBounties.Count} active");
        }
        
        private BountyTask GenerateBounty(BountyTemplate template, bool isDaily)
        {
            int target = UnityEngine.Random.Range(template.MinTarget, template.MaxTarget + 1);
            int reward = target * template.ScrapPerTarget;
            
            string description = GetBountyDescription(template.Type, target);
            
            return new BountyTask
            {
                Id = Guid.NewGuid().ToString(),
                Description = description,
                Type = template.Type,
                Target = target,
                RewardScrap = reward,
                RewardItem = template.BonusItem,
                RewardItemAmount = template.BonusAmount,
                ExpiresAt = isDaily ? Time.realtimeSinceStartup + 86400f : Time.realtimeSinceStartup + 604800f,
                IsDaily = isDaily
            };
        }
        
        private string GetBountyDescription(BountyType type, int target)
        {
            switch (type)
            {
                case BountyType.KillZombies:
                    return $"Kill {target} zombies";
                case BountyType.KillAlpha:
                    return $"Kill {target} Alpha Zombies";
                case BountyType.SurviveBloodMoon:
                    return $"Survive {target} Blood Moon events";
                case BountyType.CollectTrophies:
                    return $"Collect {target} zombie trophies";
                case BountyType.CompleteExtraction:
                    return $"Complete {target} extractions";
                default:
                    return $"Complete {target} objectives";
            }
        }
        
        private void CmdBounty(BasePlayer player, string command, string[] args)
        {
            if (!config.Bounties.Enabled)
            {
                SendReply(player, "<color=#ff4444>Bounty system is disabled.</color>");
                return;
            }
            
            var data = GetPlayerData(player.userID);
            
            string msg = "<color=#ffcc00>═══ BOUNTY BOARD ═══</color>\n\n";
            
            // Daily bounties
            msg += "<color=#44ff44>DAILY:</color>\n";
            foreach (var bounty in activeBounties.Where(b => b.IsDaily))
            {
                bool completed = data.CompletedBountyIds.Contains(bounty.Id);
                string status = completed ? "<color=#44ff44>[X]</color>" : "<color=#888888>[ ]</color>";
                string reward = bounty.RewardItem != null 
                    ? $"{bounty.RewardScrap} scrap + {bounty.RewardItemAmount}x {bounty.RewardItem}"
                    : $"{bounty.RewardScrap} scrap";
                
                msg += $"{status} {bounty.Description} - <color=#ffaa00>{reward}</color>\n";
            }
            
            // Weekly bounties
            msg += "\n<color=#ff4444>WEEKLY:</color>\n";
            foreach (var bounty in activeBounties.Where(b => !b.IsDaily))
            {
                bool completed = data.CompletedBountyIds.Contains(bounty.Id);
                string status = completed ? "<color=#44ff44>[X]</color>" : "<color=#888888>[ ]</color>";
                string reward = bounty.RewardItem != null 
                    ? $"{bounty.RewardScrap} scrap + {bounty.RewardItemAmount}x {bounty.RewardItem}"
                    : $"{bounty.RewardScrap} scrap";
                
                msg += $"{status} {bounty.Description} - <color=#ffaa00>{reward}</color>\n";
            }
            
            SendReply(player, msg);
        }
        
        private void UpdateBountyProgress(ulong playerId, BountyType type, int amount)
        {
            if (!config.Bounties.Enabled) return;
            
            var data = GetPlayerData(playerId);
            var player = BasePlayer.FindByID(playerId);
            
            foreach (var bounty in activeBounties.Where(b => b.Type == type))
            {
                if (data.CompletedBountyIds.Contains(bounty.Id)) continue;
                
                // Track progress (simplified - in real implementation you'd track per-bounty progress)
                // For now, check if player meets the target
                int currentProgress = GetBountyProgress(data, type);
                
                if (currentProgress >= bounty.Target)
                {
                    CompleteBounty(playerId, bounty);
                }
            }
        }
        
        private int GetBountyProgress(PlayerData data, BountyType type)
        {
            switch (type)
            {
                case BountyType.KillZombies:
                    return data.ZombieKills;
                case BountyType.KillAlpha:
                    return data.AlphaKills;
                case BountyType.CompleteExtraction:
                    return data.ExtractionsCompleted;
                case BountyType.CollectTrophies:
                    return data.ZombieTrophies.Values.Sum();
                default:
                    return 0;
            }
        }
        
        private void CompleteBounty(ulong playerId, BountyTask bounty)
        {
            var data = GetPlayerData(playerId);
            if (data.CompletedBountyIds.Contains(bounty.Id)) return;
            
            data.CompletedBountyIds.Add(bounty.Id);
            data.BountiesCompleted++;
            
            var player = BasePlayer.FindByID(playerId);
            if (player != null)
            {
                GiveReward(player, bounty.RewardScrap, bounty.RewardItem, bounty.RewardItemAmount);
                SendReply(player, $"<color=#44ff44>[BOUNTY] COMPLETE: {bounty.Description}</color>");
            }
            
            SaveData();
        }
        #endregion


        #region Cure System
        private void CmdCraftCure(BasePlayer player, string command, string[] args)
        {
            if (!config.Cure.Enabled)
            {
                SendReply(player, "<color=#ff4444>Cure crafting is disabled.</color>");
                return;
            }
            
            var data = GetPlayerData(player.userID);
            
            if (data.HasPermanentCure)
            {
                SendReply(player, "<color=#44ff44>You already have permanent immunity!</color>");
                return;
            }
            
            // Check if player has all ingredients
            bool hasAll = true;
            string missing = "";
            
            foreach (var ingredient in config.Cure.Recipe)
            {
                var itemDef = ItemManager.FindItemDefinition(ingredient.Key);
                if (itemDef == null) continue;
                
                int playerHas = player.inventory.GetAmount(itemDef.itemid);
                if (playerHas < ingredient.Value)
                {
                    hasAll = false;
                    missing += $"\n  {ingredient.Key}: {playerHas}/{ingredient.Value}";
                }
            }
            
            if (!hasAll)
            {
                SendReply(player, $"<color=#ff4444>Missing ingredients:{missing}</color>");
                return;
            }
            
            // Take ingredients
            foreach (var ingredient in config.Cure.Recipe)
            {
                var itemDef = ItemManager.FindItemDefinition(ingredient.Key);
                if (itemDef == null) continue;
                player.inventory.Take(null, itemDef.itemid, ingredient.Value);
            }
            
            // Grant cure
            data.HasPermanentCure = true;
            SaveData();
            
            // Announce
            string msg = config.Cure.CureMessage.Replace("{0}", player.displayName);
            PrintToChat(msg);
            
            SendReply(player, "<color=#44ff44>[CURE] You have crafted the ZOMBIE CURE! You are now permanently immune to infection!</color>");
        }
        
        [ChatCommand("zcure")]
        private void CmdCureStatus(BasePlayer player, string command, string[] args)
        {
            if (!config.Cure.Enabled)
            {
                SendReply(player, "<color=#ff4444>Cure system is disabled.</color>");
                return;
            }
            
            var data = GetPlayerData(player.userID);
            
            if (data.HasPermanentCure)
            {
                SendReply(player, "<color=#44ff44>[CURE] You have permanent zombie immunity!</color>");
                return;
            }
            
            string msg = "<color=#ffcc00>═══ ZOMBIE CURE ═══</color>\n";
            msg += "Craft the cure to gain permanent immunity!\n\n";
            msg += "<color=#888888>Required ingredients:</color>\n";
            
            foreach (var ingredient in config.Cure.Recipe)
            {
                var itemDef = ItemManager.FindItemDefinition(ingredient.Key);
                if (itemDef == null) continue;
                
                int playerHas = player.inventory.GetAmount(itemDef.itemid);
                string color = playerHas >= ingredient.Value ? "#44ff44" : "#ff4444";
                msg += $"  <color={color}>{ingredient.Key}: {playerHas}/{ingredient.Value}</color>\n";
            }
            
            msg += $"\n<color=#888888>Use /{config.Cure.CureCommand} to craft when ready.</color>";
            
            SendReply(player, msg);
        }
        
        // Hook for ZombiePVECore to check immunity
        public bool HasCureImmunity(ulong playerId)
        {
            if (!config.Cure.Enabled || !config.Cure.PermanentImmunity) return false;
            var data = GetPlayerData(playerId);
            return data.HasPermanentCure;
        }
        #endregion

        #region Zombie Kill Tracking
        // Called by ZombiePVECore when a zombie is killed
        public void OnZombieKilled(BasePlayer killer, bool isAlpha = false)
        {
            if (killer == null || !killer.userID.IsSteamId()) return;
            
            var data = GetPlayerData(killer.userID);
            data.ZombieKills++;
            
            // Update bounty progress
            UpdateBountyProgress(killer.userID, BountyType.KillZombies, 1);
            
            // Drop trophy
            // Note: Position would need to be passed from ZombiePVECore
            DropTrophy(killer.transform.position, killer);
        }
        
        // Called by ZombiePVECore to let Tier1 handle loot drops
        public void OnZombieLootDrop(Vector3 position, BasePlayer killer)
        {
            if (killer == null) return;
            
            // Tier1 loot - slightly better than Core but still vanilla appropriate
            var lootTable = new List<(string shortname, int min, int max, int weight)>
            {
                ("scrap", 1, 3, 35),
                ("metal.fragments", 2, 8, 30),
                ("cloth", 1, 4, 28),
                ("lowgradefuel", 1, 4, 22),
                ("bandage", 1, 1, 18),
                ("bone.fragments", 2, 6, 22),
                ("fat.animal", 1, 3, 18),
                ("leather", 1, 3, 15),
                ("syringe.medical", 1, 1, 3),
                ("ammo.pistol", 1, 3, 5),
                ("ammo.shotgun", 1, 2, 4)
            };
            
            // Calculate total weight
            int totalWeight = 0;
            foreach (var entry in lootTable)
                totalWeight += entry.weight;
            
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
                        Vector3 dropPos = position + Vector3.up * 0.5f;
                        item.Drop(dropPos, Vector3.up * 2f + UnityEngine.Random.insideUnitSphere * 0.3f);
                    }
                    break;
                }
            }
        }
        #endregion

        #region Helpers
        private void GiveReward(BasePlayer player, int scrap, string bonusItem, int bonusAmount)
        {
            if (scrap > 0)
            {
                if (config.General.UseEconomics && Economics != null)
                {
                    Economics.Call("Deposit", player.userID, (double)scrap);
                }
                else if (config.General.UseServerRewards && ServerRewards != null)
                {
                    ServerRewards.Call("AddPoints", player.userID, scrap);
                }
                else
                {
                    var scrapItem = ItemManager.CreateByName("scrap", scrap);
                    if (scrapItem != null)
                        player.GiveItem(scrapItem);
                }
            }
            
            if (!string.IsNullOrEmpty(bonusItem) && bonusAmount > 0)
            {
                var item = ItemManager.CreateByName(bonusItem, bonusAmount);
                if (item != null)
                    player.GiveItem(item);
            }
        }
        
        private Vector3 GetSpawnPositionNear(Vector3 center, float minDist, float maxDist)
        {
            for (int attempts = 0; attempts < 20; attempts++)
            {
                float angle = UnityEngine.Random.Range(0f, 360f) * Mathf.Deg2Rad;
                float distance = UnityEngine.Random.Range(minDist, maxDist);
                
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
        
        private Vector3 GetRandomMapPosition()
        {
            float mapSize = TerrainMeta.Size.x / 2f - 100f;
            
            for (int attempts = 0; attempts < 50; attempts++)
            {
                float x = UnityEngine.Random.Range(-mapSize, mapSize);
                float z = UnityEngine.Random.Range(-mapSize, mapSize);
                Vector3 pos = new Vector3(x, 0, z);
                
                RaycastHit hit;
                if (Physics.Raycast(pos + Vector3.up * 500f, Vector3.down, out hit, 600f, LayerMask.GetMask("Terrain", "World")))
                {
                    pos = hit.point;
                    
                    if (!WaterLevel.Test(pos, true, true) && pos.y > 0)
                        return pos;
                }
            }
            return Vector3.zero;
        }
        
        private string GetGridPosition(Vector3 position)
        {
            float mapSize = TerrainMeta.Size.x;
            float gridSize = mapSize / 26f; // A-Z
            
            int x = Mathf.FloorToInt((position.x + mapSize / 2f) / gridSize);
            int z = Mathf.FloorToInt((position.z + mapSize / 2f) / gridSize);
            
            x = Mathf.Clamp(x, 0, 25);
            z = Mathf.Clamp(z, 0, 25);
            
            char letter = (char)('A' + x);
            return $"{letter}{z + 1}";
        }
        
        private void DebugLog(string message)
        {
            if (config.General.Debug)
                Puts($"[Debug] {message}");
        }
        
        private void PrintStartupMessage()
        {
            Puts("===========================================");
            Puts("  ZOMBIE PVE TIER 1 v1.0 - LOADED!");
            Puts("===========================================");
            if (config.Evolution.Enabled) Puts($"  [+] Zombie Evolution (Stage: {GetEvolutionStageName()})");
            if (config.SafeHouses.Enabled) Puts($"  [+] Safe Houses (/{config.SafeHouses.ActivateCommand})");
            if (config.Trophies.Enabled) Puts($"  [+] Trophy Trading (/{config.Trophies.TradeCommand})");
            if (config.Alpha.Enabled) Puts("  [+] Alpha Zombie Hunts");
            if (config.Airdrops.Enabled) Puts("  [+] Airdrop Hordes");
            if (config.Extraction.Enabled) Puts("  [+] Extraction Events");
            if (config.Bounties.Enabled) Puts($"  [+] Bounty Board (/{config.Bounties.BountyCommand})");
            if (config.Cure.Enabled) Puts($"  [+] Cure Crafting (/{config.Cure.CureCommand})");
            Puts("===========================================");
        }
        #endregion

        #region Admin Commands
        [ChatCommand("zt1")]
        private void CmdAdmin(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin && !permission.UserHasPermission(player.UserIDString, PERM_ADMIN))
            {
                SendReply(player, "<color=#ff4444>Admin only.</color>");
                return;
            }
            
            if (args.Length == 0)
            {
                string adminHelp = "<color=#ffcc00>=== ZT1 ADMIN ===</color>\n";
                if (config.Alpha.Enabled)
                    adminHelp += "/zt1 alpha - Spawn alpha zombie\n";
                if (config.Extraction.Enabled)
                    adminHelp += "/zt1 extraction - Start extraction event\n";
                if (config.Bounties.Enabled)
                    adminHelp += "/zt1 bounty refresh - Refresh bounties\n";
                if (config.Cure.Enabled)
                    adminHelp += "/zt1 cure <player> - Give cure to player\n";
                if (config.Trophies.Enabled)
                    adminHelp += "/zt1 trophy <player> <type> <amount> - Give trophies";
                SendReply(player, adminHelp);
                return;
            }
            
            switch (args[0].ToLower())
            {
                case "alpha":
                    if (!config.Alpha.Enabled)
                    {
                        SendReply(player, "<color=#ff4444>Alpha zombie system is disabled.</color>");
                        break;
                    }
                    RaycastHit hit;
                    if (Physics.Raycast(player.eyes.HeadRay(), out hit, 100f))
                    {
                        SpawnAlphaZombie(hit.point);
                        SendReply(player, "<color=#44ff44>Alpha zombie spawned!</color>");
                    }
                    break;
                    
                case "extraction":
                    if (!config.Extraction.Enabled)
                    {
                        SendReply(player, "<color=#ff4444>Extraction events are disabled.</color>");
                        break;
                    }
                    StartExtractionEvent(player.transform.position + Vector3.forward * 30f);
                    SendReply(player, "<color=#44ff44>Extraction event started!</color>");
                    break;
                    
                case "bounty":
                    if (!config.Bounties.Enabled)
                    {
                        SendReply(player, "<color=#ff4444>Bounty system is disabled.</color>");
                        break;
                    }
                    if (args.Length > 1 && args[1].ToLower() == "refresh")
                    {
                        RefreshBounties();
                        SendReply(player, "<color=#44ff44>Bounties refreshed!</color>");
                    }
                    break;
                    
                case "cure":
                    if (!config.Cure.Enabled)
                    {
                        SendReply(player, "<color=#ff4444>Cure system is disabled.</color>");
                        break;
                    }
                    if (args.Length > 1)
                    {
                        var target = BasePlayer.Find(args[1]);
                        if (target != null)
                        {
                            var data = GetPlayerData(target.userID);
                            data.HasPermanentCure = true;
                            SaveData();
                            SendReply(player, $"<color=#44ff44>Gave cure to {target.displayName}</color>");
                        }
                        else
                        {
                            SendReply(player, "<color=#ff4444>Player not found.</color>");
                        }
                    }
                    else
                    {
                        SendReply(player, "<color=#ff4444>Usage: /zt1 cure <player></color>");
                    }
                    break;
                    
                case "trophy":
                    if (!config.Trophies.Enabled)
                    {
                        SendReply(player, "<color=#ff4444>Trophy system is disabled.</color>");
                        break;
                    }
                    if (args.Length > 3)
                    {
                        var target = BasePlayer.Find(args[1]);
                        if (target != null && int.TryParse(args[3], out int amount))
                        {
                            var data = GetPlayerData(target.userID);
                            string trophyType = args[2];
                            if (!data.ZombieTrophies.ContainsKey(trophyType))
                                data.ZombieTrophies[trophyType] = 0;
                            data.ZombieTrophies[trophyType] += amount;
                            SaveData();
                            SendReply(player, $"<color=#44ff44>Gave {amount} {trophyType} to {target.displayName}</color>");
                        }
                        else
                        {
                            SendReply(player, "<color=#ff4444>Player not found or invalid amount.</color>");
                        }
                    }
                    else
                    {
                        SendReply(player, "<color=#ff4444>Usage: /zt1 trophy <player> <type> <amount></color>");
                    }
                    break;
                    
                default:
                    SendReply(player, "<color=#ff4444>Unknown command. Use /zt1 for help.</color>");
                    break;
            }
        }
        
        [ChatCommand("zt1help")]
        private void CmdHelp(BasePlayer player, string command, string[] args)
        {
            string msg = "<color=#ffcc00>=== ZOMBIE PVE TIER 1 ===</color>\n\n";
            
            if (config.Evolution.Enabled)
                msg += "<color=#44ff44>/zevolution</color> - View zombie evolution status\n";
            
            if (config.SafeHouses.Enabled)
                msg += $"<color=#44ff44>/{config.SafeHouses.ActivateCommand}</color> - Manage your safe house\n";
            
            if (config.Trophies.Enabled)
                msg += $"<color=#44ff44>/{config.Trophies.TradeCommand}</color> - Trade zombie trophies\n";
            
            if (config.Alpha.Enabled)
                msg += "<color=#44ff44>/zalpha</color> - Track alpha zombie\n";
            
            if (config.Extraction.Enabled)
                msg += "<color=#44ff44>/extraction</color> - View active extraction\n";
            
            if (config.Bounties.Enabled)
                msg += $"<color=#44ff44>/{config.Bounties.BountyCommand}</color> - View bounty board\n";
            
            if (config.Cure.Enabled)
            {
                msg += $"<color=#44ff44>/zcure</color> - View cure recipe\n";
                msg += $"<color=#44ff44>/{config.Cure.CureCommand}</color> - Craft the cure\n";
            }
            
            if (player.IsAdmin || permission.UserHasPermission(player.UserIDString, PERM_ADMIN))
            {
                msg += "\n<color=#ffcc00>Admin Commands:</color>\n";
                msg += "<color=#ff8800>/zt1 alpha</color> - Spawn alpha zombie\n";
                msg += "<color=#ff8800>/zt1 extraction</color> - Start extraction event\n";
                msg += "<color=#ff8800>/zt1 bounty refresh</color> - Refresh bounties\n";
                msg += "<color=#ff8800>/zt1 cure <player></color> - Give cure to player\n";
            }
            
            SendReply(player, msg);
        }
        #endregion
    }
}
