using Facepunch;
using Facepunch.Math;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Rust;
using Rust.Ai.Gen2;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("TruePVE", "nivex", "2.3.5")]
    [Description("Improvement of the default Rust PVE behavior")]
    // Thanks to the original author, ignignokt84.
    internal class TruePVE : RustPlugin
    {
        #region Variables
        // config/data container
        private Configuration config = new();

        [PluginReference]
        Plugin ZoneManager, LiteZones, Clans, Friends, AbandonedBases, RaidableBases;

        public string usageString;
        public enum Command { def, sched, trace, usage, enable, sleepers };
        public enum DamageResult { None, Allow, Block }

        [Flags]
        public enum RuleFlags : ulong
        {
            None = 0,
            AdminsHurtSleepers = 1uL << 1,
            AdminsHurtPlayers = 1uL << 2,
            AnimalsIgnoreSleepers = 1uL << 3,
            AuthorizedDamage = 1uL << 4,
            AuthorizedDamageRequiresOwnership = 1uL << 5,
            CupboardOwnership = 1uL << 6,
            FriendlyFire = 1uL << 7,
            HeliDamageLocked = 1uL << 8,
            HumanNPCDamage = 1uL << 9,
            LockedBoxesImmortal = 1uL << 10,
            LockedDoorsImmortal = 1uL << 11,
            NoPlayerDamageToCar = 1uL << 12,
            NoPlayerDamageToMini = 1uL << 13,
            NoPlayerDamageToScrap = 1uL << 14,
            NoHeliDamage = 1uL << 15,
            NoHeliDamagePlayer = 1uL << 16,
            NoHeliDamageQuarry = 1uL << 17,
            NoHeliDamageRidableHorses = 1uL << 18,
            NoHeliDamageSleepers = 1uL << 19,
            NoMLRSDamage = 1uL << 20,
            NpcsCanHurtAnything = 1uL << 21,
            PlayerSamSitesIgnorePlayers = 1uL << 22,
            ProtectedSleepers = 1uL << 23,
            TrapsIgnorePlayers = 1uL << 24,
            TrapsIgnoreScientist = 1uL << 25,
            TurretsIgnorePlayers = 1uL << 26,
            TurretsIgnoreScientist = 1uL << 27,
            StaticTurretsIgnoreScientist = 1uL << 28,
            TwigDamage = 1uL << 29,
            TwigDamageRequiresOwnership = 1uL << 30,
            VehiclesTakeCollisionDamageWithoutDriver = 1uL << 31,
            SamSitesIgnoreMLRS = 1uL << 32,
            SelfDamage = 1uL << 33,
            StaticSamSitesIgnorePlayers = 1uL << 34,
            StaticTurretsIgnorePlayers = 1uL << 35,
            SafeZoneTurretsIgnorePlayers = 1uL << 36,
            SuicideBlocked = 1uL << 37,
            NoHeliDamageBuildings = 1uL << 38,
            WoodenDamage = 1uL << 39,
            WoodenDamageRequiresOwnership = 1uL << 40,
            AuthorizedDamageCheckPrivilege = 1uL << 41,
            ExcludeTugboatFromImmortalFlags = 1uL << 42,
            LockedVehiclesImmortal = 1uL << 43,
            TurretsIgnoreBradley = 1uL << 44,
            AuthorizedFarmableDamage = 1uL << 45,
            HopperCannotTargetEnemyLoot = 1uL << 46
        }

        private bool IsUnloading;
        private Timer scheduleUpdateTimer;                              // timer to check for schedule updates
        private bool shareRedirectDudEnabled;                           // undocumented. UAYOR.
        private RuleSet dudRuleSet;                                     // dud ruleset when no locations are shared
        private RuleSet currentRuleSet;                                 // current ruleset
        private string currentBroadcastMessage;                         // current broadcast message
        private bool useZones;                                          // internal useZones flag
        private const string Any = "any";                               // constant "any" string for rules
        private const string AllZones = "allzones";                     // constant "allzones" string for mappings
        private const string PermCanMap = "truepve.canmap";             // permission for mapping command
        private bool animalsIgnoreSleepers;                             // toggle flag to protect sleepers
        private bool trace;                                             // trace flag
        private const string traceFile = "ruletrace";                   // tracefile name
        private const float traceTimeout = 300f;                        // auto-disable trace after 300s (5m)
        private Timer traceTimer;                                       // trace timeout timer
        private bool tpveEnabled = true;                                // toggle flag for damage handling
        private List<DamageType> _damageTypes = new()
        {
            DamageType.Arrow,
            DamageType.Blunt,
            DamageType.Bullet,
            DamageType.Explosion,
            DamageType.Cold,
            DamageType.Heat,
            DamageType.Generic,
            DamageType.Slash,
            DamageType.Stab,
        };

        private uint maincannonshell = 3032863244;
        private uint trainbarricade = 1221760186;
        private uint trainbarricadeheavy = 1363243026;
        private uint loot_trash = 3279100614;
        private uint giftbox_loot = 2216891097;
        private uint campfire = 4160694184;
        private uint oilfireballsmall = 3550347674;
        private uint heli_napalm = 184893264;
        private uint rocket_heli_napalm = 200672762;
        private uint rocket_heli = 129320027;

        private bool excludeAllZones;
        private readonly List<ulong> _waiting = new();
        private readonly HashSet<string> _deployables = new();
        private readonly HashSet<string> exclusionLocationsSet = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<ulong, List<PlayerExclusion>> playerDelayExclusions = new();
        private readonly Dictionary<string, RuleSet> ruleSetByNameDictionary = new(StringComparer.OrdinalIgnoreCase);

        private class PlayerExclusion : Pool.IPooled
        {
            public Plugin plugin;
            public float time;
            public bool IsExpired => Time.time > time;
            public void EnterPool()
            {
                plugin = null;
                time = 0f;
            }
            public void LeavePool()
            {
            }
        }

        #endregion

        #region Loading/Unloading

		protected new static void Puts(string format, params object[] args)
        {
            if (!string.IsNullOrEmpty(format))
            {
                Interface.Oxide.LogInfo("[{0}] {1}", "TruePVE", (args.Length != 0) ? string.Format(format, args) : format);
            }
        }
		
        private void Unload()
        {
            bool save = false;
            if (_removeMappingTimer is { Destroyed: false })
            {
                _removeMappingTimer.Destroy();
                SaveConfig();
                save = true;
            }
            if (_auMappingTimer is { Destroyed: false })
            {
                _auMappingTimer.Destroy();
                if (!save) SaveConfig();
            }
            IsUnloading = true;
            scheduleUpdateTimer?.Destroy();
            SaveData();
        }

        private void OnPluginLoaded(Plugin plugin)
        {
            if (plugin.Name == "ZoneManager")
                ZoneManager = plugin;
            if (plugin.Name == "LiteZones")
                LiteZones = plugin;
            if (ZoneManager != null || LiteZones != null)
                SetUseZones();
        }

        private void OnPluginUnloaded(Plugin plugin)
        {
            if (plugin.Name == "ZoneManager")
                ZoneManager = null;
            if (plugin.Name == "LiteZones")
                LiteZones = null;
            if (ZoneManager == null && LiteZones == null)
                useZones = false;
        }

        private void OnCreatedDynamicPVP() => SetUseZones();

        private void OnDeletedDynamicPVP() => SetUseZones();

        protected void SetUseZones()
        {
            useZones = config != null && config.mappings != null && config.options != null && config.options.useZones && (LiteZones != null || ZoneManager != null);
            if (useZones && config.mappings.Count == 1)
            {
                foreach (var mapping in config.mappings)
                {
                    useZones = !mapping.Key.Equals(config.defaultRuleSet);
                }
            }
        }

        private void Init()
        {
            if (!config.options.Loot.NoShieldDrop)
            {
                Unsubscribe(nameof(OnPlayerActiveShieldDrop));
            }
            if (!config.options.Loot.NoActiveItemDrop)
            {
                Unsubscribe(nameof(OnPlayerDropActiveItem));
            }
            if (!config.options.Loot.NoRustBackpackDrop)
            {
                Unsubscribe(nameof(OnBackpackDrop));
            }
            if (!config.BlockSprayCanInSafeZones)
            {
                Unsubscribe(nameof(OnSprayCreate));
            }
            Unsubscribe(nameof(CanLootEntity));
            Unsubscribe(nameof(OnCodeEntered));
            Unsubscribe(nameof(OnCupboardAuthorize));
            Unsubscribe(nameof(CanHelicopterStrafeTarget));
			Unsubscribe(nameof(CanWaterBallSplash));
            Unsubscribe(nameof(OnEntityMarkHostile));
            Unsubscribe(nameof(OnEntitySpawned));
            Unsubscribe(nameof(OnEntityEnter));
            Unsubscribe(nameof(OnTurretTarget));
            Unsubscribe(nameof(OnTimedExplosiveExplode));
            Unsubscribe(nameof(OnWallpaperRemove));
            Unsubscribe(nameof(OnEntityTakeDamage));
            Unsubscribe(nameof(OnPlayerConnected));
            Unsubscribe(nameof(OnSamSiteTarget));
            Unsubscribe(nameof(OnTrapTrigger));
            Unsubscribe(nameof(OnMlrsFire));
            // register console commands automagically
            foreach (Command command in Enum.GetValues(typeof(Command)))
            {
                AddCovalenceCommand($"tpve.{command}", nameof(CommandDelegator));
            }
            // register chat commands
            AddCovalenceCommand("tpve_prod", nameof(CommandDelegator));
            AddCovalenceCommand("tpve_enable", nameof(CommandDelegator));
            AddCovalenceCommand("tpve", nameof(CommandDelegator));
            permission.RegisterPermission(PermCanMap, this);
            // build usage string for console (without sizing)
            usageString = WrapColor("orange", GetMessage("Header_Usage")) + $" - {Version}{Environment.NewLine}" +
                          WrapColor("cyan", $"tpve.{Command.def}") + $" - {GetMessage("Cmd_Usage_def")}{Environment.NewLine}" +
                          WrapColor("cyan", $"tpve.{Command.trace}") + $" - {GetMessage("Cmd_Usage_trace")}{Environment.NewLine}" +
                          WrapColor("cyan", $"tpve.{Command.sched} [enable|disable]") + $" - {GetMessage("Cmd_Usage_sched")}{Environment.NewLine}" +
                          WrapColor("cyan", $"/tpve_prod") + $" - {GetMessage("Cmd_Usage_prod")}{Environment.NewLine}" +
                          WrapColor("cyan", $"/tpve map") + $" - {GetMessage("Cmd_Usage_map")}";
            LoadData();
        }

        private bool IsAnimalsIgnoringSleepers() => animalsIgnoreSleepers || config.ruleSets.Exists(ruleSet => ruleSet.HasFlag(RuleFlags.AnimalsIgnoreSleepers));
        
        private bool IsAnimalsIgnoringSleepers(RuleSet ruleSet) => animalsIgnoreSleepers || ruleSet.HasFlag(RuleFlags.AnimalsIgnoreSleepers);
        
        private void OnServerInitialized(bool isStartup)
        {
            isServerStartingUp = false;
            // check for server pve setting
            if (ConVar.Server.pve) WarnPve();
            // load configuration
            config.Init(this);
            currentRuleSet = config.GetDefaultRuleSet();
            dudRuleSet = config.GetDudRuleSet();
            if (currentRuleSet == null)
                Puts(GetMessage("Warning_NoRuleSet"), config.defaultRuleSet);
            SetUseZones();
            if (config.schedule.enabled)
            {
                TimerLoop(true);
            }
            if (!IsAnimalsIgnoringSleepers())
            {
                Unsubscribe(nameof(OnNpcTarget));
            }
            if (config.PreventSafeZoneStrafing)
            {
                Subscribe(nameof(CanHelicopterStrafeTarget));
            }
            if (config.PreventThrowingWaterInFreezingBiome || config.BlockRadioactiveWaterDamage)
            {
                Subscribe(nameof(CanWaterBallSplash));
            }
            if (currentRuleSet == null)
            {
                return;
            }
            if (config.ruleSets.Exists(ruleSet => (ruleSet._flags & (RuleFlags.SafeZoneTurretsIgnorePlayers | RuleFlags.StaticTurretsIgnorePlayers | RuleFlags.StaticTurretsIgnoreScientist | RuleFlags.TrapsIgnorePlayers | RuleFlags.TrapsIgnoreScientist | RuleFlags.TurretsIgnorePlayers | RuleFlags.TurretsIgnoreScientist | RuleFlags.TurretsIgnoreBradley)) != 0))
            {
                Subscribe(nameof(OnEntityEnter));
                Subscribe(nameof(OnTurretTarget));
            }
            if (config.ruleSets.Exists(ruleSet => (ruleSet._flags & (RuleFlags.SamSitesIgnoreMLRS | RuleFlags.PlayerSamSitesIgnorePlayers | RuleFlags.StaticSamSitesIgnorePlayers)) != 0))
            {
                Subscribe(nameof(OnSamSiteTarget));
            }
            if (config.ruleSets.Exists(ruleSet => (ruleSet._flags & (RuleFlags.TrapsIgnorePlayers | RuleFlags.TrapsIgnoreScientist)) != 0))
            {
                Subscribe(nameof(OnTrapTrigger));
            }
            if (config.schedule.enabled && config.schedule.broadcast && !string.IsNullOrEmpty(currentBroadcastMessage))
            {
                Subscribe(nameof(OnPlayerConnected));
            }
            if (config.options.disableBaseOvenSplash)
            {
                ServerMgr.Instance.StartCoroutine(OvenCo());
            }
            if (config.options.disableHostility)
            {
                Subscribe(nameof(OnEntityMarkHostile));
            }
            RuleSet ruleSet = currentRuleSet;
            if (config.options.handleDamage && ruleSet != null && !ruleSet.IsEmpty() && ruleSet.enabled)
            {
                Subscribe(nameof(OnEntityTakeDamage));
                tpveEnabled = true;
            }
            if (config.wallpaper)
            {
                Subscribe(nameof(OnWallpaperRemove));
                Subscribe(nameof(OnTimedExplosiveExplode));
            }
            if (config.options.Loot.ProtectTC)
            {
                Subscribe(nameof(OnCupboardAuthorize));
                Subscribe(nameof(CanLootEntity));
            }
            else if (config.options.Loot.Lifts)
            {
                Subscribe(nameof(CanLootEntity));
            }
            Subscribe(nameof(OnEntitySpawned));
            Subscribe(nameof(OnMlrsFire));
            BuildPrefabIds();
            AllowLocksOnContainers();
            RemoveTemporaryZones();
        }

        private IEnumerator OvenCo()
        {
            int checks = 0;
            foreach (var ent in BaseNetworkable.serverEntities)
            {
                if (++checks > 500)
                {
                    checks = 0;
                    yield return null;
                }
                if (ent is BaseOven oven)
                {
                    oven.disabledBySplash = false;
                }
            }
        }

        private void BuildPrefabIds()
        {
            if (StringPool.toNumber.TryGetValue("assets/prefabs/npc/m2bradley/maincannonshell.prefab", out var prefab1)) maincannonshell = prefab1;
            if (StringPool.toNumber.TryGetValue("assets/content/props/train_tunnels/trainbarricade.prefab", out var prefab2)) trainbarricade = prefab2;
            if (StringPool.toNumber.TryGetValue("assets/content/props/train_tunnels/trainbarricadeheavy.prefab", out var prefab3)) trainbarricadeheavy = prefab3;
            if (StringPool.toNumber.TryGetValue("assets/bundled/prefabs/radtown/loot_trash.prefab", out var prefab4)) loot_trash = prefab4;
            if (StringPool.toNumber.TryGetValue("assets/prefabs/misc/xmas/giftbox/giftbox_loot.prefab", out var prefab5)) giftbox_loot = prefab5;
            if (StringPool.toNumber.TryGetValue("assets/prefabs/deployable/campfire/campfire.prefab", out var prefab6)) campfire = prefab6;
            if (StringPool.toNumber.TryGetValue("assets/bundled/prefabs/oilfireballsmall.prefab", out var prefab7)) oilfireballsmall = prefab7;
            if (StringPool.toNumber.TryGetValue("assets/bundled/prefabs/napalm.prefab", out var prefab8)) heli_napalm = prefab8;
            if (StringPool.toNumber.TryGetValue("assets/prefabs/npc/patrol helicopter/rocket_heli_napalm.prefab", out var prefab9)) rocket_heli_napalm = prefab9;
            if (StringPool.toNumber.TryGetValue("assets/prefabs/npc/patrol helicopter/rocket_heli.prefab", out var prefab10)) rocket_heli = prefab10;
        }
        #endregion

        #region Data

        private void OnNewSave()
        {
            data = new();
            SaveData();
        }

        private class StoredData
        {
            public Dictionary<ulong, int> LastSeen = new();
            public DateTime LastRunTime = DateTime.MinValue;
        }

        private StoredData data = new();

        private void LoadData()
        {
            try { data = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name); } catch (Exception ex) { Puts(ex.ToString()); }
            data ??= new();
            data.LastSeen ??= new();
            if (data.LastRunTime != DateTime.MinValue && DateTime.Now.Subtract(data.LastRunTime).TotalHours >= 24)
            {
                if (config.AllowKillingSleepersHoursOffline > 0f && data.LastSeen.Count > 0)
                {
                    Puts("Last seen data wiped due to plugin not being loaded for {0} day(s).", DateTime.Now.Subtract(data.LastRunTime).Days);
                }
                data = new();
                data.LastRunTime = DateTime.Now;
            }
            if (config.AllowKillingSleepersHoursOffline <= 0f)
            {
                if (data.LastSeen.Count > 0)
                {
                    data.LastSeen.Clear();
                    SaveData();
                }
                return;
            }
            timer.Every(60f, UpdateLastSeen);
            UpdateLastSeen();
        }

        private void SaveData()
        {
            data.LastRunTime = DateTime.Now;
            Interface.Oxide.DataFileSystem.WriteObject(Name, data);
        }

        public void UpdateLastSeen()
        {
            bool changed = false;
            foreach (var sleeper in BasePlayer.sleepingPlayerList)
            {
                if (sleeper == null || !sleeper.userID.IsSteamId())
                {
                    continue;
                }
                if (!data.LastSeen.ContainsKey(sleeper.userID) && !sleeper.IsBuildingAuthed(true))
                {
                    data.LastSeen[sleeper.userID] = Epoch.Current;
                    changed = true;
                }
            }
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (data.LastSeen.Remove(player.userID))
                {
                    changed = true;
                }
            }
            if (changed)
            {
                SaveData();
            }
        }

        public bool CanKillOfflinePlayer(BasePlayer player, out double timeLeft)
        {
            timeLeft = 0;
            if (player.IsConnected || !player.IsSleeping())
            {
                data.LastSeen.Remove(player.userID);
                return false;
            }
            if (!data.LastSeen.TryGetValue(player.userID, out var lastSeen))
            {
                return false;
            }
            if (player.IsBuildingAuthed(true))
            {
                data.LastSeen.Remove(player.userID);
                return false;
            }
            double timeOffline = Epoch.Current - lastSeen;
            double allowedOfflineTime = config.AllowKillingSleepersHoursOffline * 3600.0;
            timeLeft = (allowedOfflineTime - timeOffline) / 3600.0;
            return timeOffline > allowedOfflineTime;
        }

        #endregion Data

        #region Command Handling
        // delegation method for commands
        private void CommandDelegator(IPlayer user, string command, string[] args)
        {
            // return if user doesn't have access to run console command
            if (!user.IsAdmin) return;

            if (args.Length > 0 && args[0] == "map")
            {
                if (user.HasPermission(PermCanMap))
                {
                    CommandMap(user, command, args);
                }
                return;
            }

            if (args.Contains("pvp"))
            {
                if (currentRuleSet.rules.Remove("players cannot hurt players"))
                {
                    currentRuleSet.rules.Add("players can hurt players");
                }
                else if (currentRuleSet.rules.Remove("player can hurt players"))
                {
                    currentRuleSet.rules.Add("player cannot hurt players");
                }

                Puts("PVP toggled {0}", currentRuleSet.rules.Contains("players can hurt players") ? "on" : "off");
                SaveConfig();
                return;
            }

            if (command == "tpve_prod")
            {
                HandleProd(user);
                return;
            }

            if (command == "tpve_enable")
            {
				tpveEnabled = !tpveEnabled;
                ValidateCurrentDamageHook();
                Message(user, "Enable", tpveEnabled);
                return;
            }

            if (command == "tpve" && args.Length != 0) command = args[0];
            else command = command.Replace("tpve.", string.Empty);

            if (!Enum.TryParse(command, out Command @enum))
            {
                user.Reply($"Invalid argument: {command}");
                return;
            }

            switch (@enum)
            {
                case Command.sleepers:
                    HandleSleepers(user);
                    return;
                case Command.def:
                    HandleDef(user);
                    return;
                case Command.sched:
                    HandleScheduleSet(user, args);
                    return;
                case Command.trace:
                    HandleTrace(user);
                    return;
                case Command.enable:
					tpveEnabled = !tpveEnabled;
                    ValidateCurrentDamageHook();
                    Message(user, "Enable", tpveEnabled);
                    return;
                case Command.usage:
                default:
                    ShowUsage(user);
                    return;
            }
        }

        protected void HandleTrace(IPlayer user)
        {
            if (!IsTraceEnabled(user))
            {
                return;
            }
            if (user.IsServer)
            {
                traceDistance = 0f;
            }
            else traceDistance = config.options.MaxTraceDistance;
            trace = !trace;
            if (!trace)
            {
                tracePlayer = null;
                traceEntity = null;
            }
            else tracePlayer = user.Object as BasePlayer;
            Message(user, "Notify_TraceToggle", new object[] { trace ? "on" : "off" });
            traceTimer?.Destroy();
            if (trace)
            {
                traceTimer = timer.In(traceTimeout, () => trace = false);
            }
        }

        private bool IsTraceEnabled(IPlayer user)
        {
            if (config.options.PlayerConsole || config.options.ServerConsole)
            {
                return true;
            }
            Message(user, "`Trace To Player Console` or `Trace To Server Console` must be enabled in the config!");
            return false;
        }

        private void HandleSleepers(IPlayer user)
        {
            if (animalsIgnoreSleepers)
            {
                animalsIgnoreSleepers = false;
                if (!IsAnimalsIgnoringSleepers())
                {
                    Unsubscribe(nameof(OnNpcTarget));
                }
                user.Reply("Sleepers are no longer protected from animals.");
            }
            else
            {
                animalsIgnoreSleepers = true;
                Subscribe(nameof(OnNpcTarget));
                user.Reply("Sleepers are now protected from animals.");
            }
        }

        // handle setting defaults
        private void HandleDef(IPlayer user)
        {
            config.options = new();
            Message(user, "Notify_DefConfigLoad");
            LoadDefaultData();
            Message(user, "Notify_DefDataLoad");
            CheckData();
            SaveConfig();
        }

        // handle prod command (raycast to determine what player is looking at)
        private void HandleProd(IPlayer user)
        {
            var player = user.Object as BasePlayer;
            if (player == null || !player.IsAdmin)
            {
                Message(user, "Error_NoPermission");
                return;
            }

            if (!GetRaycastTarget(player, out var entity))
            {
                SendReply(player, WrapSize(12, WrapColor("red", GetMessage("Error_NoEntityFound", player.UserIDString))));
                return;
            }

            Message(player, "Notify_ProdResult", entity.GetType(), entity.ShortPrefabName);
        }

        private void CommandMap(IPlayer user, string command, string[] args)
        {
            // assume args[0] is the command (beyond /tpve)
            if (args.Length > 0) command = args[0];

            // shift arguments
            args = args.Length > 1 ? args[1..] : Array.Empty<string>();

            if (command != "map")
            {
                Message(user, "Error_InvalidCommand");
            }
            else if (args.Length == 0)
            {
                Message(user, "Error_InvalidParamForCmd", command);
            }
            else
            {
                string from = args[0]; // mapping name
                string to = args.Length == 2 ? args[1] : null; // target ruleSet/exclude, otherwise delete mapping
                if (to != null)
                {
                    if (to != "exclude" && !config.ruleSets.Exists(r => r.name == to))
                    {
                        // target ruleset must exist, or be "exclude"
                        Message(user, "Error_InvalidMapping", from, to);
                        return;
                    }
                    if (config.HasMapping(from))
                    {
                        string old = config.mappings[from];
                        Message(user, "Notify_MappingUpdated", from, old, to); // update existing mapping
                    }
                    else Message(user, "Notify_MappingCreated", from, to); // add new mapping
                    config.mappings[from] = to;
                    SaveConfig();
                    TryBuildExclusionMappings();
                }
                else
                {
                    if (config.HasMapping(from))
                    {
                        Message(user, "Notify_MappingDeleted", from, config.mappings[from]);
                        config.mappings.Remove(from); // remove mapping
                        SaveConfig();
                        TryBuildExclusionMappings();
                    }
                    else Message(user, "Error_NoMappingToDelete", from);
                }
            }
        }

        // handles schedule enable/disable
        private void HandleScheduleSet(IPlayer user, string[] args)
        {
            if (args.Length == 0)
            {
                Message(user, "Error_InvalidParamForCmd");
                return;
            }
            if (!config.schedule.valid)
            {
                Message(user, "Notify_InvalidSchedule");
            }
            else if (args[0] == "enable")
            {
                if (config.schedule.enabled) return;
                config.schedule.enabled = true;
                TimerLoop();
                Message(user, "Notify_SchedSetEnabled");
            }
            else if (args[0] == "disable")
            {
                if (!config.schedule.enabled) return;
                config.schedule.enabled = false;
                if (scheduleUpdateTimer != null)
                    scheduleUpdateTimer.Destroy();
                Message(user, "Notify_SchedSetDisabled");
            }
            else
            {
                Message(user, "Error_InvalidParameter", args[0]);
            }
        }
        #endregion

        #region Configuration/Data
        private bool _playersTriggerOption, _playersHurtOption, _canKillOfflinePlayerEnabled, _pvpReflectionEnabled, _allowKillingSleepersEnabled, _blockOutputHandlerEnabled;

        // load config
        protected override void LoadConfig()
        {
            base.LoadConfig();
            canSaveConfig = false;
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) LoadDefaultConfig();
                canSaveConfig = true;
                CheckData();
                SaveConfig();
            }
            catch (Exception ex)
            {
                Puts(ex.ToString());
                LoadDefaultConfig();
            }
        }

        protected override void LoadDefaultConfig()
        {
            config = new()
            {
                configVersion = Version.ToString(),
                options = new()
            };
            LoadDefaultData();
            Puts("Loaded default config.");
        }

        private bool canSaveConfig = true;

        // save data
        protected override void SaveConfig()
        {
            if (canSaveConfig)
            {
                Config.WriteObject(config);
            }
        }

        // check rulesets and groups
        private void CheckData()
        {
            if (string.IsNullOrEmpty(config.defaultRuleSet))
            {
                config.defaultRuleSet = "default";
                Puts("Loaded default ruleset (no default ruleset was configured)");
            }
            if (config.mappings.IsNullOrEmpty())
            {
                BuildDefaultMappings();
                Puts("Loaded default mappings (no mappings were configured)");
            }
            if (config.schedule == null)
            {
                Puts("Loaded default schedule (schedule was null)");
                BuildDefaultSchedule();
            }
            if (config.groups.IsNullOrEmpty())
            {
                BuildDefaultGroups();
                Puts("Loaded default entity groups (no entity groups were configured)");
            }
            if (config.ruleSets.IsNullOrEmpty())
            {
                BuildDefaultRuleset();
                Puts("Loaded default rulesets (no rulesets were configured)");
            }
            if (config._AllowKillingSleepersAuthorization != null)
            {
                config.AllowKillingSleepersAuthorization.Enabled = config._AllowKillingSleepersAuthorization.Value;
                config._AllowKillingSleepersAuthorization = null;
            }
            TryUpdateConfig();
            config.configVersion = Version.ToString();
            CheckMappings();
            BuildRuleSetDictionary();
            BuildExclusionMappings();
            _allowKillingSleepersEnabled = config.AllowKillingSleepersAlly || config.AllowKillingSleepers || config.AllowKillingSleepersAuthorization.Enabled || config.AllowKillingSleepersIds.Exists(x => x.IsSteamId());
            _blockOutputHandlerEnabled = config.options.BlockHandler.Any; 
            _pvpReflectionEnabled = config.options.Reflect.Any;
            _canKillOfflinePlayerEnabled = config.AllowKillingSleepersHoursOffline > 0;
            _playersTriggerOption = config.PlayersTriggerTraps || config.PlayersTriggerTurrets;
            _playersHurtOption = config.PlayersHurtTraps || config.PlayersHurtTurrets;
        }

        private void TryUpdateConfig()
        {
            if (!TryParseVersionNumber(config.configVersion, out var vn) || vn >= Version)
                return;

            Dictionary<string, string> updates = new(StringComparer.OrdinalIgnoreCase)
            {
                ["npcs"] = "SnakeHazard",
                ["dispensers"] = "VineSwingingTree"
            };

            for (int i = 0; i < config.groups.Count; i++)
            {
                var group = config.groups[i];
                if (string.IsNullOrWhiteSpace(group.members))
                {
                    continue;
                }

                if (string.IsNullOrEmpty(group.name))
                {
                    group.name = $"group{i}";
                    continue;
                }

                if (group.name == "ridablehorses" && group.members.Equals("RidableHorse2"))
                {
                    group.members = "RidableHorse";
                    continue;
                }

                if (updates.TryGetValue(group.name, out var update) && !ContainedInGroups(update))
                {
                    group.members = $"{group.members.TrimEnd(',', ' ')}{", "}{update}";
                    continue;
                }
            }
        }

        private bool ContainedInGroups(string member) => config.groups.Exists(g => g.members.Contains(member, CompareOptions.OrdinalIgnoreCase) || g.exclusions.Contains(member, CompareOptions.OrdinalIgnoreCase));

        private bool TryParseVersionNumber(string input, out VersionNumber version)
        {
            version = default;
            if (string.IsNullOrWhiteSpace(input))
                return false;

            var parts = input.Split('.');
            if (parts.Length != 3)
                return false;

            if (int.TryParse(parts[0], out int major) && int.TryParse(parts[1], out int minor) && int.TryParse(parts[2], out int patch))
            {
                version = new(major, minor, patch);
                return true;
            }

            return false;
        }

        // rebuild mappings
        private bool CheckMappings()
        {
            bool dirty = false;
            foreach (RuleSet ruleSet in config.ruleSets)
            {
                if (!config.mappings.ContainsValue(ruleSet.name))
                {
                    config.mappings[ruleSet.name] = ruleSet.name;
                    dirty = true;
                }
            }
            return dirty;
        }

        protected void BuildRuleSetDictionary()
        {
            ruleSetByNameDictionary.Clear();

            foreach (RuleSet ruleSet in config.ruleSets)
            {
                if (ruleSet.enabled)
                {
                    ruleSetByNameDictionary[ruleSet.name] = ruleSet;
                }
            }
        }

        protected void TryBuildExclusionMappings()
        {
            if (!config.mappings.TryGetValue(AllZones, out var val) || !val.Equals("exclude", StringComparison.OrdinalIgnoreCase))
            {
                BuildExclusionMappings();
            }
        }

        protected void BuildExclusionMappings()
        {
            excludeAllZones = false;
            exclusionLocationsSet.Clear();

            if (config.mappings.TryGetValue(AllZones, out var val) && val.Equals("exclude", StringComparison.OrdinalIgnoreCase))
            {
                excludeAllZones = true;
                return;
            }

            foreach (var (key, value) in config.mappings)
            {
                if (!value.Equals("exclude", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var ruleSet in config.ruleSets)
                    {
                        if (ruleSet.name.Equals(value, StringComparison.OrdinalIgnoreCase) && ruleSet.IsEmpty())
                        {
                            exclusionLocationsSet.Add(key);
                        }
                    }
                }
                else
                {
                    exclusionLocationsSet.Add(key);
                }
            }

            foreach (var group in config.groups)
            {
                foreach (var exclusion in group._exclusionSet)
                {
                    if (!string.IsNullOrWhiteSpace(exclusion))
                    {
                        exclusionLocationsSet.Add(exclusion.Trim());
                    }
                }
            }
        }

        // load default data to mappings, rulesets, and groups
        protected bool LoadDefaultData()
        {
            BuildDefaultMappings();

            BuildDefaultSchedule();

            BuildDefaultGroups();

            BuildDefaultRuleset();

            return true;
        }

        protected void BuildDefaultSchedule()
        {
            config.schedule = new();
        }

        protected void BuildDefaultMappings()
        {
            config.mappings ??= new();
            config.mappings.Clear();
            config.defaultRuleSet = "default";
            config.mappings[config.defaultRuleSet] = config.defaultRuleSet; // create mapping for ruleset
        }

        protected void BuildDefaultGroups()
        {
            config.groups ??= new();
            config.groups.Clear();

            config.groups.Add(new("barricades")
            {
                members = "door_barricade_a, door_barricade_a_large, door_barricade_b, door_barricade_dbl_a, door_barricade_dbl_a_large, door_barricade_dbl_b, door_barricade_dbl_b_large, gingerbread_barricades_house, gingerbread_barricades_snowman, gingerbread_barricades_tree, wooden_crate_gingerbread",
                exclusions = "barricade.concrete, barricade.sandbags, barricade.stone"
            });

            config.groups.Add(new("barricades2")
            {
                members = "spikes_static, barricade.metal, barricade.wood, barricade.woodwire, spikes.floor, icewall, GraveyardFence",
            });

            config.groups.Add(new("dispensers")
            {
                members = "BaseCorpse, HelicopterDebris, PlayerCorpse, NPCPlayerCorpse, HorseCorpse, SkyLantern, Pinata"
            });

            config.groups.Add(new("fire")
            {
                members = "FireBall, FlameExplosive, FlameThrower, BaseOven, FlameTurret, napalm, oilfireball2"
            });

            config.groups.Add(new("guards")
            {
                members = "bandit_guard, scientistpeacekeeper, sentry.scientist.static, sentry.bandit.static"
            });

            config.groups.Add(new("heli")
            {
                members = "PatrolHelicopter, oilfireballsmall, heli_napalm, rocket_heli, rocket_heli_napalm"
            });

            config.groups.Add(new("highwalls")
            {
                members = "SimpleBuildingBlock, wall.external.high.ice, gates.external.high.stone, gates.external.high.wood"
            });

            config.groups.Add(new("ridablehorses")
            {
                members = "RidableHorse"
            });

            config.groups.Add(new("cars")
            {
                members = "BasicCar, ModularCar, BaseModularVehicle, BaseVehicleModule, VehicleModuleEngine, VehicleModuleSeating, VehicleModuleStorage, VehicleModuleTaxi, ModularCarSeat, Bike"
            });

            config.groups.Add(new("mini")
            {
                members = "minicopter.entity"
            });

            config.groups.Add(new("scrapheli")
            {
                members = "ScrapTransportHelicopter"
            });

            config.groups.Add(new("ch47")
            {
                members = "ch47.entity"
            });

            config.groups.Add(new("npcs")
            {
                members = "ch47scientists.entity, BradleyAPC, CustomScientistNpc, SnakeHazard, ScarecrowNPC, HumanNPC, NPCPlayer, ScientistNPC, TunnelDweller, SimpleShark, UnderwaterDweller, ZombieNPC"
            });

            config.groups.Add(new("players")
            {
                members = "BasePlayer, FrankensteinPet"
            });

            config.groups.Add(new("resources")
            {
                members = "ResourceEntity, TreeEntity, OreResourceEntity, LootContainer, NaturalBeehive, VineSwingingTree",
                exclusions = "hobobarrel.deployed"
            });

            config.groups.Add(new("snowmobiles")
            {
                members = "snowmobile, tomahasnowmobile"
            });

            config.groups.Add(new("traps")
            {
                members = "AutoTurret, BearTrap, FlameTurret, Landmine, GunTrap, ReactiveTarget, TeslaCoil, spikes.floor"
            });

            config.groups.Add(new("junkyard")
            {
                members = "magnetcrane.entity, carshredder.entity"
            });

            config.groups.Add(new("tugboats")
            {
                members = "Tugboat"
            });

            config.groups.Add(new("heliturrets")
            {
                members = "turret_attackheli"
            });

            config.groups.Add(new("ramhead")
            {
                members = "BatteringRamHead"
            });

            config.groups.Add(new("siege")
            {
                members = "SiegeTower, Catapult, Ballista, BallistaGun, BatteringRam, ConstructableEntity"
            });

            config.groups.Add(new("bees")
            {
                members = "BeeSwarmAI, Beehive, BeeGrenade, BeeSwarmMaster, NaturalBeehive"
            });

            config.groups.Add(new("farm")
            {
                members = "simplechicken.entity, FarmableAnimal, ChickenCoop"
            });
        }

        protected void BuildDefaultRuleset()
        {
            config.ruleSets ??= new();
            config.ruleSets.Clear();

            // create default ruleset
            RuleSet defaultRuleSet = new(config.defaultRuleSet)
            {
                _flags = RuleFlags.HopperCannotTargetEnemyLoot | RuleFlags.AuthorizedFarmableDamage | RuleFlags.HumanNPCDamage | RuleFlags.LockedBoxesImmortal | RuleFlags.LockedDoorsImmortal | RuleFlags.PlayerSamSitesIgnorePlayers | RuleFlags.TrapsIgnorePlayers | RuleFlags.TurretsIgnorePlayers,
                flags = "HopperCannotTargetEnemyLoot, AuthorizedFarmableDamage, HumanNPCDamage, LockedBoxesImmortal, LockedDoorsImmortal, PlayerSamSitesIgnorePlayers, TrapsIgnorePlayers, TurretsIgnorePlayers"
            };

            // create rules and add to ruleset
            defaultRuleSet.AddRule(this, "anything can hurt dispensers");
            defaultRuleSet.AddRule(this, "anything can hurt resources");
            defaultRuleSet.AddRule(this, "anything can hurt barricades");
            defaultRuleSet.AddRule(this, "anything can hurt traps");
            defaultRuleSet.AddRule(this, "anything can hurt heli");
            defaultRuleSet.AddRule(this, "anything can hurt npcs");
            defaultRuleSet.AddRule(this, "anything can hurt players");
            defaultRuleSet.AddRule(this, "nothing can hurt ch47");
            defaultRuleSet.AddRule(this, "nothing can hurt cars");
            defaultRuleSet.AddRule(this, "nothing can hurt mini");
            defaultRuleSet.AddRule(this, "nothing can hurt snowmobiles");
            defaultRuleSet.AddRule(this, "nothing can hurt ridablehorses");
            defaultRuleSet.AddRule(this, "cars cannot hurt anything");
            defaultRuleSet.AddRule(this, "mini cannot hurt anything");
            defaultRuleSet.AddRule(this, "ch47 cannot hurt anything");
            defaultRuleSet.AddRule(this, "scrapheli cannot hurt anything");
            defaultRuleSet.AddRule(this, "players cannot hurt players");
            defaultRuleSet.AddRule(this, "players cannot hurt traps");
            defaultRuleSet.AddRule(this, "guards cannot hurt players");
            defaultRuleSet.AddRule(this, "fire cannot hurt players");
            defaultRuleSet.AddRule(this, "traps cannot hurt players");
            defaultRuleSet.AddRule(this, "highwalls cannot hurt players");
            defaultRuleSet.AddRule(this, "barricades2 cannot hurt players");
            defaultRuleSet.AddRule(this, "mini cannot hurt mini");
            defaultRuleSet.AddRule(this, "npcs can hurt players");
            defaultRuleSet.AddRule(this, "junkyard cannot hurt anything");
            defaultRuleSet.AddRule(this, "junkyard can hurt cars");
            defaultRuleSet.AddRule(this, "players cannot hurt tugboats");
            defaultRuleSet.AddRule(this, "heliturrets cannot hurt players");
            defaultRuleSet.AddRule(this, "ramhead can hurt ramhead");
            defaultRuleSet.AddRule(this, "siege cannot hurt players");
            defaultRuleSet.AddRule(this, "players cannot hurt farm");

            config.ruleSets.Add(defaultRuleSet); // add ruleset to rulesets list
        }

        private bool ResetRules(string key)
        {
            if (string.IsNullOrEmpty(key) || config == null)
            {
                return false;
            }

            string old = config.defaultRuleSet;
            config.defaultRuleSet = key;
            currentRuleSet = config.GetDefaultRuleSet();

            if (currentRuleSet == null)
            {
                config.defaultRuleSet = old;
                currentRuleSet = config.GetDefaultRuleSet();
            }

            ValidateCurrentDamageHook();
            return currentRuleSet != null;
        }
        #endregion

        #region Trace
        private StringBuilder _tsb = new();
        private BaseEntity traceEntity;
        private BasePlayer tracePlayer;
        private float traceDistance;

        private void Trace(string message, int indentation = 0)
        {
            if (traceEntity == null || traceEntity.IsDestroyed)
            {
                return;
            }

            bool playerInRange = tracePlayer != null && !tracePlayer.IsDestroyed && InRange(tracePlayer.transform.position, traceEntity.transform.position, traceDistance);
            bool shouldLogToConsole = (config.options.PlayerConsole && playerInRange) || (config.options.ServerConsole && (traceDistance == 0 || playerInRange));

            if (shouldLogToConsole)
            {
                _tsb.Append(new string(' ', indentation)).AppendLine(message);
            }
        }

        private void LogTrace()
        {
            var text = _tsb.ToString();
            traceEntity = null;
            _tsb.Length = 0;
            try
            {
                if (!string.IsNullOrEmpty(text))
                {
                    if (config.options.ServerConsole)
                    {
                        Puts(text);
                    }
                    if (config.options.PlayerConsole && tracePlayer != null && tracePlayer.IsConnected)
                    {
                        tracePlayer.ConsoleMessage(text);
                    }
                    if (config.options.LogToFile)
                    {
                        LogToFile(traceFile, text, this);
                    }
                }
            }
            catch (IOException)
            {
                timer.Once(1f, () => LogToFile(traceFile, text, this));
            }
        }

        #endregion Trace

        #region Hooks/Handler Procedures
        private void OnPlayerConnected(BasePlayer player)
        {
            if (config.schedule.broadcast && !string.IsNullOrEmpty(currentBroadcastMessage))
            {
                SendReply(player, GetMessage("Prefix") + currentBroadcastMessage);
            }
        }

        private string CurrentRuleSetName() => currentRuleSet?.name;

        private bool IsEnabled() => tpveEnabled;

        private void OnTimedExplosiveExplode(TimedExplosive explosive, Vector3 explosionFxPos)
        {
            if (explosive != null)
            {
                explosive.splashWallpaperThroughWalls = false;
            }
        }

        private object OnWallpaperRemove(BuildingBlock block, int side)
        {
            if (block == null || block.IsDestroyed)
            {
                return null;
            }
            switch (side)
            {
                case 0:
                    {
                        if (block.wallpaperID != 0 && block.wallpaperHealth <= 0f)
                        {
                            using var entityLocations = GetLocationKeys(block);
                            if (CheckExclusion(entityLocations, entityLocations, trace))
                            {
                                return null;
                            }

                            if (trace) Trace("Block Damage Wallpaper1 enabled; block and return", 1);
                            block.wallpaperHealth = block.health;
                            return true;
                        }
                        break;
                    }
                case 1:
                    {
                        if (block.wallpaperID2 != 0 && block.wallpaperHealth2 <= 0f)
                        {
                            using var entityLocations = GetLocationKeys(block);
                            if (CheckExclusion(entityLocations, entityLocations, trace))
                            {
                                return null;
                            }

                            if (trace) Trace("Block Damage Wallpaper2 enabled; block and return", 1);
                            block.wallpaperHealth2 = block.health;
                            return true;
                        }
                        break;
                    }
            }
            return null;
        }

        private object OnEntityTakeDamage(ResourceEntity entity, HitInfo info)
        {
            if (info == null || info.Initiator == null)
            {
                return null;
            }

            RuleSet ruleSet;
            if (useZones)
            {
                // get entity and initiator locations (zones)
                using var entityLocations = GetLocationKeys(entity);
                using var initiatorLocations = GetLocationKeys(info.Initiator);

                // check for exclusion zones (zones with no rules mapped)
                if (CheckExclusion(entityLocations, initiatorLocations, trace))
                {
                    if (trace) Trace("Exclusion found; allow and return", 1);
                    return null;
                }

                if (trace) Trace("No exclusion found - looking up RuleSet...", 1);

                // process location rules
                if (config.PVEZones && initiatorLocations.IsNullOrEmpty())
                {
                    ruleSet = GetRuleSet(entityLocations, entityLocations);
                }
                else ruleSet = GetRuleSet(entityLocations, initiatorLocations);
            }
            else ruleSet = currentRuleSet;

            return EvaluateRules(entity, info.Initiator, ruleSet) != DamageResult.Block ? (object)null : true;
        }

        private object OnEntityTakeDamage(BaseEntity entity, HitInfo info)
        {
            if (info == null || entity == null || entity.IsDestroyed)
            {
                return null;
            }

            if (!AllowDamage(entity, info))
            {
                if (trace) LogTrace();
                if (info.Weapon is BlowPipeWeapon)
                {
                    info.HitEntity = null;
                }
                info.damageTypes?.Clear();
                info.DidHit = false;
                info.DoHitEffects = false;
                return true;
            }

            if (trace) LogTrace();
            return null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private string ConcatenateListOrDefault(List<string> list, string defaultValue)
        {
            return (list == null || list.Count == 0) ? defaultValue : string.Join(", ", list);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private string ConcatenateRuleSetNames(List<RuleSet> sets)
        {
            if (sets == null || sets.Count == 0)
                return string.Empty;
            var sb = Pool.Get<StringBuilder>();
            for (int i = 0; i < sets.Count; i++)
            {
                sb.Append(sets[i].name);
                if (i < sets.Count - 1)
                {
                    sb.Append(", ");
                }
            }
            string text = sb.ToString();
            Pool.FreeUnmanaged(ref sb);
            return text;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DamageResult HandleMetabolismDamage(HitInfo info, BasePlayer victim, DamageType damageType, float damageAmount)
        {
            if (damageType != DamageType.Cold && damageType != DamageType.Heat)
            {
                return DamageResult.None;
            }

            if (victim == null || !victim.userID.IsSteamId())
            {
                return DamageResult.None;
            }

            float temperature = victim.metabolism.temperature.value;
            float multiplier;
            float normalized;

            if (damageType == DamageType.Cold)
            {
                if (temperature > 1f)
                {
                    return DamageResult.None;
                }

                multiplier = temperature < -20f ? 1f :
                             temperature < -10f ? 0.3f : 0.1f;

                normalized = (temperature - 1f) / -51f;
            }
            else // DamageType.Heat
            {
                if (temperature < 60f)
                {
                    return DamageResult.None;
                }

                multiplier = 5f;
                normalized = (temperature - 60f) / 140f;
            }

            normalized = normalized < 0f ? 0f : (normalized > 1f ? 1f : normalized);

            float threshold = normalized * ConVar.Player.serverTickInterval * multiplier;

            if (damageAmount > threshold)
            {
                return DamageResult.None;
            }

            bool option = damageType == DamageType.Cold ? config.options.Cold : config.options.Heat;
            DamageResult damageResult = option ? DamageResult.Allow : DamageResult.Block;

            if (trace)
            {
                string action = damageResult == DamageResult.Allow ? "allow and return" : "block and return";
                Trace($"Initiator is {damageType} metabolism damage; {action}", 1);
                LogTrace();
            }

            if (damageResult == DamageResult.Block)
            {
                info.damageTypes.Clear();
            }

            return damageResult;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool CanPlayerBeHurtFromMonumentTopology(BaseEntity weapon, Vector3 worldPos)
        {
            if (!(config.PlayersTriggerTraps && (weapon is BaseTrap or BaseDetector or GunTrap) || config.PlayersTriggerTurrets && (weapon is FlameTurret or AutoTurret)))
            {
                return false;
            }
            if (!_monumentTopologyTargets.TryGetValue(weapon.net.ID.Value, out bool value))
            {
                _monumentTopologyHurt[weapon.net.ID.Value] = value = (TerrainMeta.TopologyMap.GetTopology(worldPos, 5f) & (int)TerrainTopology.Enum.Monument) != 0;
                if (_monumentTopologyHurt.Count == 1) timer.Once(60f, _monumentTopologyHurt.Clear);
            }
            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool CanPlayerHurtTargetInMonumentTopology(BaseEntity entity, Vector3 worldPos)
        {
            if (!(config.PlayersHurtTraps && (entity is BaseTrap or BaseDetector or GunTrap)) && !(config.PlayersHurtTurrets && (entity is FlameTurret or AutoTurret)))
            {
                return false;
            }
            if (!_monumentTopologyTargets.TryGetValue(entity.net.ID.Value, out bool value))
            {
                _monumentTopologyTargets[entity.net.ID.Value] = value = (TerrainMeta.TopologyMap.GetTopology(worldPos, 5f) & (int)TerrainTopology.Enum.Monument) != 0;
                if (_monumentTopologyTargets.Count == 1) timer.Once(60f, _monumentTopologyTargets.Clear);
            }
            return value;
        }

        private Dictionary<ulong, bool> _monumentTopologyTargets = new(), _monumentTopologyHurt = new();

        private bool AllowKillingSleepers(BaseEntity entity, BaseEntity initiator)
        {
            if (entity is BasePlayer victim && victim.userID.IsSteamId() && victim.IsSleeping())
            {
                if (config.AllowKillingSleepersAuthorization.Enabled && initiator is BasePlayer attacker && AllowAuthorizationDamage(victim, attacker))
                {
                    return true;
                }
                if (config.AllowKillingSleepersIds.Count > 0 && initiator is BasePlayer attacker3 && attacker3.userID.IsSteamId() && config.AllowKillingSleepersIds.Contains(attacker3.userID))
                {
                    return true;
                }
                if (config.AllowKillingSleepersAlly && initiator is BasePlayer attacker2 && attacker2.userID.IsSteamId())
                {
                    return IsAlly(victim.userID, attacker2.userID);
                }
                return config.AllowKillingSleepers;
            }
            return false;
        }

        private bool AllowAuthorizationDamage(BasePlayer victim, BasePlayer attacker)
        {
            if (!attacker.userID.IsSteamId())
            {
                return false;
            }
            Tugboat tugboat = victim.GetParentEntity() as Tugboat;
            if (tugboat != null && IsAuthed(tugboat, attacker.userID))
            {
                return true;
            }
            BuildingPrivlidge priv = victim.GetBuildingPrivilege(true);
            if (priv != null && priv.authorizedPlayers.Contains(attacker.userID))
            {
                return config.AllowKillingSleepersAuthorization.MeetsMinimumRequirements(priv);
            }
            return false;
        }

        private static bool IsAuthed(DroppedItem entity, BaseEntity attacker)
        {
            if (entity == null || entity.IsDestroyed) return false;
            BuildingPrivlidge priv = entity.GetBuildingPrivilege(entity.WorldSpaceBounds(), true);
            return priv != null && priv.authorizedPlayers.Contains(entity.DroppedBy);
        }

        private static bool IsAuthed(PlayerCorpse entity, BaseEntity attacker)
        {
            if (entity == null || entity.IsDestroyed) return false;
            BuildingPrivlidge priv = entity.GetBuildingPrivilege(entity.WorldSpaceBounds(), true);
            return priv != null && priv.authorizedPlayers.Contains(entity.playerSteamID);
        }

        private static bool IsAuthed(DecayEntity entity, BasePlayer attacker)
        {
            if (entity is LegacyShelter || entity is LegacyShelterDoor)
            {
                EntityPrivilege entityPriv = entity.GetEntityBuildingPrivilege();
                return entityPriv != null && entityPriv.authorizedPlayers.Contains(attacker.userID);
            }
            BuildingManager.Building building = entity.GetBuilding();
            if (building != null)
            {
                BuildingPrivlidge priv = building.GetDominatingBuildingPrivilege();
                if (priv != null)
                {
                    return priv.authorizedPlayers.Contains(attacker.userID);
                }
            }
            BuildingPrivlidge priv2 = entity.GetBuildingPrivilege(entity.WorldSpaceBounds(), true);
            return priv2 != null && priv2.authorizedPlayers.Contains(attacker.userID);
        }

        private static bool IsAuthed(Tugboat tugboat, ulong userid)
        {
            if (tugboat.children == null)
            {
                return false;
            }
            foreach (var child in tugboat.children)
            {
                VehiclePrivilege priv = child as VehiclePrivilege;
                if (priv != null && priv.authorizedPlayers.Contains(userid))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool IsAuthed(BaseHelicopter heli, BasePlayer attacker)
        {
            BuildingPrivlidge priv = attacker.GetBuildingPrivilege(heli.WorldSpaceBounds(), true);
            return priv != null && priv.authorizedPlayers.Contains(attacker.userID);
        }

        // determines if an entity is "allowed" to take damage
        private bool AllowDamage(BaseEntity entity, HitInfo info)
        {
            if (trace)
            {
                traceEntity = entity;
                _tsb.Length = 0;
            }

            var initiator = info.Initiator switch
            {
                BasePlayer player => player,
                { creatorEntity: BasePlayer player } => player,
                { parentEntity: EntityRef parentRef } when parentRef.Get(true) is BasePlayer player => player,
                _ => info.Initiator ?? info.WeaponPrefab
            };

            var victim = entity as BasePlayer;
            var attacker = initiator as BasePlayer;
            var isAttacker = attacker != null && !attacker.IsDestroyed;
            var isAtkId = isAttacker && attacker.userID.IsSteamId();
            var isVictim = victim != null && !victim.IsDestroyed;
            var isVicId = isVictim && victim.userID.IsSteamId();

            if (Interface.CallHook("CanEntityTakeDamage", new object[] { entity, info }) is bool val)
            {
                if (val && config.options.ArmorDamage.Enabled && isAttacker && !isAtkId && isVicId)
                {
                    HandleHitArea(victim, info);
                }
                return val;
            }

            var damageAmount = info.damageTypes.Total();

            if (damageAmount <= 0f)
            {
                return true;
            }

            if (config.options.ArmorDamage.Enabled && isAttacker && !isAtkId && isVicId)
            {
                HandleHitArea(victim, info);
            }

            if (_allowKillingSleepersEnabled && AllowKillingSleepers(entity, initiator))
            {
                return true;
            }

            var weapon = initiator ?? info.WeaponPrefab ?? info.Weapon;

            if (entity is BaseNpc || entity is BaseNPC2)
            {
                if (trace) Trace($"Target is animal; allow and return {weapon} -> {entity}", 1);
                return true;
            }

            if (config.igniter && entity.OwnerID != 0 && entity is Igniter)
            {
                info.damageTypes.Clear();
                return true;
            }

            if (weapon != null && IsSkinExclusion(weapon))
            {
                if (trace) Trace($"Target is {entity}; allow and return -> {weapon} skin ID {weapon.skinID}", 1);
                return true;
            }

            if (config.scrap)
            {
                if (victim != null && weapon is ScrapTransportHelicopter)
                {
                    info.damageTypes.Clear();
                    return true;
                }
                if (weapon is BasePlayer driver && (driver.GetMountedVehicle() is ScrapTransportHelicopter || info.WeaponPrefab is ScrapTransportHelicopter))
                {
                    info.damageTypes.Clear();
                    return true;
                }
            }

            // allow damage to door barricades and covers 
            if (entity.prefabID == trainbarricade || entity.prefabID == trainbarricadeheavy || (entity is Barricade && (entity.ShortPrefabName.Contains("door_barricade") || entity.ShortPrefabName.Contains("cover"))))
            {
                if (trace) Trace($"Target is {entity.ShortPrefabName}; allow and return", 1);
                return true;
            }

            // if entity is a barrel, trash can, or giftbox, allow damage (exclude waterbarrel and hobobarrel)
            if (entity is LootContainer && (entity.prefabID == giftbox_loot || entity.prefabID == loot_trash || entity.ShortPrefabName.Contains("barrel")))
            {
                if (trace) Trace($"Target is {entity.ShortPrefabName} ({GetTypeName(entity)}); allow and return", 1);
                return true;
            }

            var damageType = info.damageTypes.GetMajorityDamageType();

            if (damageType == DamageType.Fall || damageType == DamageType.Radiation)
            {
                return true;
            }

            if (damageType == DamageType.Decay)
            {
                if (entity is BaseVehicle v)
                {
                    return entity is HitchTrough.IHitchable && v.healthFraction > 0.9f ? true : !config.BlockDecayDamageToVehicles;
                }
                return true;
            }

            if (damageAmount < 15f && HandleMetabolismDamage(info, victim, damageType, damageAmount) != DamageResult.None)
            {
                return true;
            }

            if (trace)
            {
                // Sometimes the initiator is not the attacker (turrets)
                Trace("======================" + Environment.NewLine +
                  "==  STARTING TRACE  ==" + Environment.NewLine +
                  "==  " + DateTime.Now.ToString("HH:mm:ss.fffff") + "  ==" + Environment.NewLine +
                  "======================");
                //string weaponid = $"{(weapon != null ? weapon.OwnerID : (info.Initiator != null ? $"initiator '{info.Initiator.OwnerID}' & creator: '{info.Initiator.creatorEntity?.OwnerID ?? 0}'" : 0))}";
                //string weaponce = $"{(weapon != null ? weapon.creatorEntity : string.Empty)}";
                //string weaponpr = $"{(weapon != null ? weapon.ShortPrefabName : (info.Initiator != null ? $"initiator '{info.Initiator}' & creator: '{info.Initiator.creatorEntity}'" : "Unknown_Prefab"))}";
                //Trace($"From: {GetTypeName(weapon, "Unknown_Weapon")}, {weaponpr} {weaponce} {weaponid}", 1);
                Trace($"From: {GetTypeName(weapon, "Unknown_Weapon")}, {weapon?.ShortPrefabName ?? "Unknown_Prefab"}", 1);
                Trace($"To: {GetTypeName(entity)}, {entity.ShortPrefabName}", 1);
            }

            var ruleSet = currentRuleSet;
            if (useZones)
            {
                // get entity and initiator locations (zones)
                using var entityLocations = GetLocationKeys(entity);
                using var initiatorLocations = GetLocationKeys(weapon);
                // check for exclusion zones (zones with no rules mapped)
                if (CheckExclusion(entityLocations, initiatorLocations, trace))
                {
                    if (trace) Trace("Exclusion found; allow and return", 1);
                    return true;
                }
                ruleSet = GetRuleSet(entityLocations, initiatorLocations);
            }

            if (trace) Trace("No exclusion found - looking up RuleSet...", 1);

            // process location rules
            RuleFlags _flags = ruleSet._flags;

            if (trace) Trace($"Using RuleSet \"{ruleSet.name}\"", 1);

            var selfDamageFlag = (_flags & RuleFlags.SelfDamage) != 0;
            var mountRulesEvaluated = false;

            if (isVicId)
            {
                if (isAtkId)
                {
                    // allow damage to players by admins if configured
                    if (attacker.IsAdmin && (_flags & RuleFlags.AdminsHurtPlayers) != 0)
                    {
                        if (trace) Trace("Initiator is admin player and target is player, with AdminsHurtPlayers flag set; allow and return", 1);
                        return true;
                    }

                    // allow sleeper damage by admins if configured
                    if (attacker.IsAdmin && (_flags & RuleFlags.AdminsHurtSleepers) != 0 && victim.IsSleeping())
                    {
                        if (trace) Trace("Initiator is admin player and target is sleeping player, with AdminsHurtSleepers flag set; allow and return", 1);
                        return true;
                    }

                    if ((_flags & RuleFlags.FriendlyFire) != 0 && victim.userID != attacker.userID && IsAlly(victim.userID, attacker.userID))
                    {
                        if (trace) Trace("Initiator and target are allied players, with FriendlyFire flag set; allow and return", 1);
                        return true;
                    }

                    if (_canKillOfflinePlayerEnabled && CanKillOfflinePlayer(victim, out _))
                    {
                        if (trace) Trace($"Initiator ({attacker}) and target ({victim} exceeds Allow Killing Sleepers offline time); allow and return", 1);
                        return true;
                    }

                    if (PlayerHasExclusion(attacker, info.PointStart) && PlayerHasExclusion(victim, info.HitPositionWorld))
                    {
                        if (trace) Trace($"Initiator ({attacker}) and target ({victim}) meet exclusion conditions; allow and return", 1);
                        return true;
                    }

                    if (_pvpReflectionEnabled && victim.userID != attacker.userID)
                    {
                        float multiplier = damageType != DamageType.Explosion && info.WeaponPrefab is TimedExplosive ? config.options.Reflect.Get(DamageType.Explosion) : config.options.Reflect.Get(damageType);
                        if (multiplier != 0 && !IsAlly(victim.userID, attacker.userID))
                        {
                            float reflectedDamage = damageAmount * multiplier;
                            DamageType reflectType = selfDamageFlag ? damageType : DamageType.Radiation;
                            if (trace) Trace($"Reflect damage ({reflectedDamage} {reflectType})", 1);
                            attacker.Hurt(reflectedDamage, reflectType, attacker, config.options.Reflect.Protection);
                        }
                    }

                    mountRulesEvaluated = true;
                    var mounted = attacker.GetMounted();
                    if (mounted != null)
                    {
                        var parent = GetParentEntity(mounted);
                        if (parent != null && EvaluateRules(entity, parent, ruleSet, false) == DamageResult.Block)
                        {
                            if (trace) Trace($"Player is mounted; evaluation? block and return", 1);
                            return false;
                        }
                    }
                }

                if (config.options.UnderworldOther > -500f && (!isAttacker || !attacker.userID.IsSteamId()) && info.HitPositionWorld.y <= config.options.UnderworldOther && info.PointStart.y <= config.options.UnderworldOther)
                {
                    if (trace) Trace($"Initiator is {weapon} under world; Target is player; allow and return", 1);
                    return true;
                }

                if (config.options.AboveworldOther < 5000f && (!isAttacker || !attacker.userID.IsSteamId()) && info.HitPositionWorld.y >= config.options.AboveworldOther && info.PointStart.y >= config.options.AboveworldOther)
                {
                    if (trace) Trace($"Initiator is {weapon} above world; Target is player; allow and return", 1);
                    return true;
                }

                if (_playersTriggerOption && weapon != null && weapon.net != null && weapon.OwnerID == 0uL && CanPlayerBeHurtFromMonumentTopology(weapon, info.PointStart))
                {
                    if (trace) Trace($"Initiator is turret or trap in monument topology; Target is player; allow and return", 1);
                    return true;
                }
            }

            if (_playersHurtOption && isAtkId && entity.OwnerID == 0uL && entity.net != null && CanPlayerHurtTargetInMonumentTopology(entity, info.HitPositionWorld))
            {
                if (trace) Trace($"Initiator is player; Target is turret or trap in monument topology; allow and return", 1);
                return true;
            }

            // LockedVehiclesImmortal flag with modular car
            if (((_flags & RuleFlags.LockedVehiclesImmortal) != 0) && entity.PrefabName.Contains("modular"))
            {
                ModularCar car = entity.HasParent() ? entity.GetParentEntity() as ModularCar : entity as ModularCar;
                if (car != null && car.CarLock.HasALock)
                {
                    if (trace) Trace($"Initiator is {weapon}; Target is locked {car}; block and return (LockedVehiclesImmortal)", 1);
                    return false;
                }
            }

            if (isVictim)
            {
                if (config.PreventRagdolling && isVicId && damageType == DamageType.Collision)
                {
                    victim.lastAdminCheatTime = UnityEngine.Time.realtimeSinceStartup + 1.9f;
                }

                double hoursLeft = 0;

                if (isAtkId && isVicId)
                {
                    if (_canKillOfflinePlayerEnabled && CanKillOfflinePlayer(victim, out hoursLeft))
                    {
                        if (trace) Trace($"Initiator ({attacker}) and target ({victim} exceeds Allow Killing Sleepers offline time); allow and return", 1);
                        return true;
                    }

                    if (!useZones)
                    {
                        if (PlayerHasExclusion(attacker, info.PointStart) && PlayerHasExclusion(victim, info.HitPositionWorld))
                        {
                            if (trace) Trace($"Initiator ({attacker}) and target ({victim}) meet exclusion conditions; allow and return", 1);
                            return true;
                        }
                    }
                }

                if (!isAtkId && isVicId && config.options.UnderworldOther > -500f && info.HitPositionWorld.y <= config.options.UnderworldOther)
                {
                    if (trace) Trace($"Initiator is {weapon} under world; Target is player; allow and return", 1);
                    return true;
                }

                if (!isAtkId && isVicId && config.options.AboveworldOther < 5000f && info.HitPositionWorld.y >= config.options.AboveworldOther)
                {
                    if (trace) Trace($"Initiator is {weapon} above world; Target is player; allow and return", 1);
                    return true;
                }

                if (isAtkId && hoursLeft > 0 && damageType != DamageType.Heat)
                {
                    ulong userid = attacker.userID;
                    if (!_waiting.Contains(userid))
                    {
                        timer.Once(1f, () => _waiting.Remove(userid));
                        Message(attacker, "Error_TimeLeft", Math.Round(hoursLeft, 2));
                        _waiting.Add(userid);
                    }
                }
            }

            if (entity is PatrolHelicopter)
            {
                if (isAttacker || weapon is PatrolHelicopter)
                {
                    bool isBlocked = EvaluateRules(entity, weapon, ruleSet, false) == DamageResult.Block;
                    if (trace)
                    {
                        string action = isBlocked ? "block and return" : "allow and return";
                        Trace($"Target is PatrolHelicopter; Initiator is {attacker}; {action}", 1);
                    }
                    return !isBlocked;
                }
                if (trace) Trace($"Target is PatrolHelicopter; Initiator is {GetTypeName(weapon)}; allow and return", 1);
                return true;
            }

            if (weapon != null && (weapon is BradleyAPC || weapon.prefabID == maincannonshell))
            {
                if (trace) Trace("Initiator is BradleyAPC; evaluating RuleSet rules...", 1);
                return EvaluateRules(entity, weapon, ruleSet) != DamageResult.Block;
            }

            if ((_flags & RuleFlags.VehiclesTakeCollisionDamageWithoutDriver) != 0 && entity is BaseVehicle vehicle && weapon == vehicle && !vehicle.GetDriver())
            {
                if (trace) Trace($"Vehicle collision: No driver; allow and return", 1);
                return true;
            }

            // check heli and turret
            DamageResult heli = CheckHeliInitiator(ruleSet, initiator, info.WeaponPrefab);

            if (config.Firework && entity is BaseFirework)
            {
                if (trace)
                {
                    string action = heli == DamageResult.None ? "allow and return" : "block and return";
                    Trace($"Target is firework; {action}", 1);
                }
                return heli == DamageResult.None;
            }

            if (heli != DamageResult.None)
            {
                DamageResult immortalFlag = CheckImmortalFlag(entity, ruleSet, initiator, info.WeaponPrefab);
                if (immortalFlag != DamageResult.None)
                {
                    return immortalFlag == DamageResult.Allow;
                }
                return HandleHelicopter(ruleSet, entity, weapon, victim, isVicId, heli == DamageResult.Allow);
            }

            if ((_flags & RuleFlags.NoMLRSDamage) != 0 && info.WeaponPrefab is MLRSRocket)
            {
                if (trace) Trace("Initiator is MLRS rocket with NoMLRSDamage set; block and return", 1);
                return false;
            }

            // after heli check, return true if initiator is null
            if (initiator == null)
            {
                if (entity is ParachuteUnpacked)
                {
                    if (trace) Trace("Initiator is server hurting parachute; allow and return", 1);
                    return true;
                }
                if (weapon is MLRSRocket)
                {
                    if (trace) Trace($"Initiator empty for MLRS Rocket; block and return", 1);
                    return false;
                }
                if ((damageType == DamageType.Slash || damageType == DamageType.Stab || damageType == DamageType.Cold) && isVictim &&
                   (!(victim.lastAttacker is BasePlayer lastAttacker) || !lastAttacker.userID.IsSteamId() || lastAttacker == entity))
                {
                    if (trace) Trace("Initiator is hurt trigger; allow and return", 1);
                    return true;
                }
                if (entity is FarmableAnimal && damageType == DamageType.Generic)
                {
                    if (trace) Trace($"Initiator is thirst or hunger; {(config.options.FarmableMetabolism ? "allow and return" : "block and return")}", 1);
                    return config.options.FarmableMetabolism;
                }
                foreach (DamageType _damageType in _damageTypes)
                {
                    if (info.damageTypes.Has(_damageType))
                    {
                        if ((entity is NPCPlayerCorpse or NPCPlayer) || (entity is BaseCorpse and not PlayerCorpse))
                        {
                            if (trace) _tsb.Clear(); // reduce useless spam
                            return true;
                        }
                        bool tut = IsTutorialNetworkGroup(entity);
                        if (trace)
                        {
                            string action = tut ? "allow and return (Tutorial Zone)" : ruleSet.defaultAllowDamage ? "allow and return" : "block and return";
                            Trace($"Initiator empty for player damage; {action} (Damage Type: {damageType}, Damage Amount: {damageAmount})", 1);
                        }
                        return tut || ruleSet.defaultAllowDamage;
                    }
                }
                if (trace) Trace($"Initiator empty; allow and return {damageType} {damageAmount}", 1);
                return true;
            }

            DamageResult immortalFlag2 = CheckImmortalFlag(entity, ruleSet, initiator, info.WeaponPrefab);
            if (immortalFlag2 != DamageResult.None)
            {
                return immortalFlag2 == DamageResult.Allow;
            }

            if (initiator is SamSite ss && (isVictim || entity is BaseMountable))
            {
                if (CheckExclusion(ss))
                {
                    if (trace) Trace($"Initiator is samsite, and target is player; exclusion found; allow and return", 1);
                    return true;
                }

                bool isAllowed = ss.staticRespawn
                                 ? ((_flags & RuleFlags.StaticSamSitesIgnorePlayers) == 0)
                                 : ((_flags & RuleFlags.PlayerSamSitesIgnorePlayers) == 0);
                if (trace)
                {
                    string action = isAllowed ? "flag not set; allow and return" : "flag set; block and return";
                    Trace($"Initiator is samsite, and target is player; {action}", 1);
                }
                return isAllowed;
            }

            if ((isAttacker && !isAtkId) || (initiator is BaseNpc or BaseNPC2 or BeeSwarmAI))
            {
                if (isVictim && (_flags & RuleFlags.ProtectedSleepers) != 0 && victim.IsSleeping())
                {
                    if (trace) Trace("Target is sleeping player, with ProtectedSleepers flag set; block and return", 1);
                    return false;
                }

                if ((_flags & RuleFlags.NpcsCanHurtAnything) != 0)
                {
                    if (trace) Trace("Initiator is NPC; flag set; allow damage and return", 1);
                    return true;
                }
            }

            if (isVictim)
            {
                if (isVicId && initiator is AutoTurret)
                {
                    if (initiator.OwnerID == 0)
                    {
                        if (initiator is NPCAutoTurret)
                        {
                            bool safezoneFlag = (_flags & RuleFlags.SafeZoneTurretsIgnorePlayers) == 0;
                            if (trace)
                            {
                                string action = safezoneFlag ? "allow and return" : "block and return";
                                Trace($"Initiator is npc turret; Target is player; {action}", 1);
                            }
                            return safezoneFlag;
                        }
                        bool staticFlag = (_flags & RuleFlags.StaticTurretsIgnorePlayers) == 0;
                        if (trace)
                        {
                            string action = staticFlag ? "allow and return" : "block and return";
                            Trace($"Initiator is static turret; Target is player; {action}", 1);
                        }
                        return staticFlag;
                    }
                    if (initiator.OwnerID.IsSteamId() && (_flags & RuleFlags.TurretsIgnorePlayers) != 0)
                    {
                        if (trace) Trace($"Initiator is RC turret; Target is player; block and return", 1);
                        return false;
                    }
                }

                // handle suicide
                if (isVicId && damageType == DamageType.Suicide)
                {
                    bool isBlocked = (_flags & RuleFlags.SuicideBlocked) != 0;
                    if (trace)
                    {
                        string action = isBlocked ? "block and return" : "allow and return";
                        Trace($"DamageType is suicide; {action}", 1);
                    }
                    if (isBlocked) Message(victim, "Error_NoSuicide");
                    return !isBlocked;
                }

                // allow players to inflict self damage
                if (selfDamageFlag && isVicId && isAtkId && attacker.userID == victim.userID)
                {
                    if (trace) Trace($"SelfDamage flag; player inflicted damage to self; allow and return", 1);
                    return true;
                }
            }

            if (isAttacker)
            {
                if (isAtkId && !mountRulesEvaluated)
                {
                    var mounted = attacker.GetMounted();
                    if (mounted != null)
                    {
                        var parent = GetParentEntity(mounted);
                        if (parent != null && EvaluateRules(entity, parent, ruleSet, false) == DamageResult.Block)
                        {
                            if (trace) Trace($"Player is mounted; evaluation? block and return", 1);
                            return false;
                        }
                    }
                }

                if (isAtkId && entity is BuildingBlock block && block.OwnerID != 0)
                {
                    if (!mountRulesEvaluated && attacker.GetMounted() is Minicopter mini && EvaluateRules(block, mini, ruleSet, false) == DamageResult.Block)
                    {
                        if (trace) Trace("Initiator is player in minicopter, target is building; block and return", 1);
                        return false;
                    }

                    if (block.grade == BuildingGrade.Enum.Twigs && (_flags & RuleFlags.TwigDamage) != 0)
                    {
                        bool isAllowed = HasBlockAccess(block, attacker, (_flags & RuleFlags.TwigDamageRequiresOwnership) != 0);
                        if (!isAllowed && _blockOutputHandlerEnabled) HandleBlockOutput(block, damageType, damageAmount, attacker, selfDamageFlag);
                        if (trace) Trace($"Initiator is player and target is twig block, with TwigDamage flag set; {(isAllowed ? "allow" : "block")} and return", 1);
                        return isAllowed;
                    }

                    if (block.grade == BuildingGrade.Enum.Wood && (_flags & RuleFlags.WoodenDamage) != 0)
                    {
                        bool isAllowed = HasBlockAccess(block, attacker, (_flags & RuleFlags.WoodenDamageRequiresOwnership) != 0);
                        if (!isAllowed && _blockOutputHandlerEnabled) HandleBlockOutput(block, damageType, damageAmount, attacker, selfDamageFlag);
                        if (trace) Trace($"Initiator is player and target is wooden block, with WoodenDamage flag set; {(isAllowed ? "allow" : "block")} and return", 1);
                        return isAllowed;
                    }

                    if (_blockOutputHandlerEnabled)
                    {
                        DamageResult result = HandleBlockGrade(block, attacker, damageType, damageAmount, selfDamageFlag);
                        if (result != DamageResult.None)
                        {
                            if (trace) Trace($"Initiator is player and target is {block.grade} block, with damage option set; {(result == DamageResult.Allow ? "allow" : "block")} and return", 1);
                            return result == DamageResult.Allow;
                        }
                    }
                }

                if ((_flags & RuleFlags.NoPlayerDamageToMini) != 0 && entity is Minicopter)
                {
                    if (trace) Trace("Initiator is player and target is Minicopter, with NoPlayerDamageToMini flag set; block and return", 1);
                    return false;
                }

                if ((_flags & RuleFlags.NoPlayerDamageToScrap) != 0 && entity is ScrapTransportHelicopter)
                {
                    if (trace) Trace("Initiator is player and target is ScrapTransportHelicopter, with NoPlayerDamageToScrap flag set; block and return", 1);
                    return false;
                }

                if ((_flags & RuleFlags.NoPlayerDamageToCar) != 0 && entity.PrefabName.Contains("modularcar"))
                {
                    if (trace) Trace("Initiator is player and target is ModularCar, with NoPlayerDamageToCar flag set; block and return", 1);
                    return false;
                }

                if (entity.OwnerID == 0 && entity is ChristmasLights)
                {
                    if (trace) Trace($"Entity is christmas lights; block and return", 1);
                    return false;
                }

                if (entity is GrowableEntity)
                {
                    bool isAllowed = !(entity.GetParentEntity() is PlanterBox planter) || IsAlly(planter.OwnerID, attacker.userID);
                    if (trace)
                    {
                        string action = isAllowed ? "allow ally" : "block non-ally";
                        Trace($"Entity is growable entity; {action} and return", 1);
                    }
                    return isAllowed;
                }

                if (config.SleepingBags && entity is SleepingBag)
                {
                    if (trace) Trace("Initiator is player and target is sleeping bag; allow and return", 1);
                    return true;
                }

                if (config.Campfires && entity.prefabID == campfire)
                {
                    if (trace) Trace("Initiator is player and target is campfire; allow and return", 1);
                    return true;
                }

                if (config.Ladders && entity is BaseLadder)
                {
                    if (trace) Trace("Initiator is player and target is ladder; allow and return", 1);
                    return true;
                }

                if (isVictim)
                {
                    // allow Human NPC damage if configured
                    if ((_flags & RuleFlags.HumanNPCDamage) != 0 && (!isAtkId || !isVicId))
                    {
                        if (trace) Trace("Initiator or target is HumanNPC, with HumanNPCDamage flag set; allow and return", 1);
                        return true;
                    }
                }
                else if ((_flags & RuleFlags.AuthorizedFarmableDamage) != 0 && isAtkId && entity is FarmableAnimal)
                {
                    var parent = entity.GetParentEntity() as ChickenCoop;
                    bool isAllowed = parent == null || parent.OwnerID == 0 || IsAlly(parent.OwnerID, attacker.userID);
                    if (trace) Trace($"Initiator is player {(isAllowed ? "with farm authorization; allow and return" : "without farm authorization; block and return")}", 1);
                    return isAllowed;
                }
                else if ((_flags & RuleFlags.AuthorizedDamage) != 0 && !isVictim && !entity.IsNpc && isAtkId && !(entity is FarmableAnimal))
                { // ignore checks if authorized damage enabled (except for players and npcs)
                    if ((_flags & RuleFlags.AuthorizedDamageCheckPrivilege) != 0)
                    {
                        if (entity is DecayEntity decayEntity && IsAuthed(decayEntity, attacker))
                        {
                            if (trace) Trace("Initiator is player with building priv over target; allow and return", 1);
                            return true;
                        }
                        if (entity is BaseHelicopter playerHelicopter && !(entity is PatrolHelicopter) && IsAuthed(playerHelicopter, attacker))
                        {
                            if (trace) Trace("Initiator is player with heli priv over target; allow and return", 1);
                            return true;
                        }
                        if (entity is Tugboat tugboat && IsAuthed(tugboat, attacker.userID))
                        {
                            if (trace) Trace("Initiator is player with tugboat priv over target; allow and return", 1);
                            return true;
                        }
                        if (entity.HasParent() && entity.GetParentEntity() is Tugboat tugboat2 && IsAuthed(tugboat2, attacker.userID))
                        {
                            if (trace) Trace("Initiator is player with tugboat priv over target; allow and return", 1);
                            return true;
                        }
                    }

                    if ((_flags & RuleFlags.AuthorizedDamageRequiresOwnership) != 0 && !IsAlly(entity.OwnerID, attacker.userID) && CanAuthorize(entity, attacker, ruleSet))
                    {
                        if (trace) Trace("Initiator is player who does not own the target; block and return", 1);
                        return false;
                    }

                    bool cupboardOwnership = (_flags & RuleFlags.CupboardOwnership) != 0;

                    if (CheckAuthorized(entity, attacker, ruleSet, cupboardOwnership))
                    {
                        if (entity is SamSite || entity is BaseMountable || entity.PrefabName.Contains("modular"))
                        {
                            if (trace) Trace($"Target is {entity.ShortPrefabName}; evaluate and return", 1);
                            return EvaluateRules(entity, attacker, ruleSet) != DamageResult.Block;
                        }
                        if (trace) Trace("Initiator is player with authorization over target; allow and return", 1);
                        return true;
                    }

                    if (cupboardOwnership)
                    {
                        if (trace) Trace("Initiator is player without authorization over target; block and return", 1);
                        return false;
                    }
                }
            }

            if (trace) Trace("No match in pre-checks; evaluating RuleSet rules...", 1);
            return EvaluateRules(entity, weapon, ruleSet) != DamageResult.Block;
        }

        private void HandleHitArea(BasePlayer victim, HitInfo info)
        {
            if (victim == null || victim.inventory == null || victim.inventory.containerWear == null || victim.inventory.containerWear.itemList == null)
            {
                return;
            }
            float relative = (info.HitPositionWorld.y - victim.transform.position.y) / (victim.IsDucked() ? 1.1f : 1.8f);
            HitArea area = victim.IsDucked() switch
            {
                true => relative switch
                {
                    <= 0.07f => HitArea.Foot,
                    >= 0.85f => HitArea.Head,
                    >= 0.65f => HitArea.Chest,
                    >= 0.45f => HitArea.Stomach,
                    _ => HitArea.Leg,
                },
                false => relative switch
                {
                    <= 0.07f => HitArea.Foot,
                    >= 0.8f => HitArea.Head,
                    >= 0.7f => HitArea.Chest,
                    >= 0.5f => HitArea.Stomach,
                    _ => HitArea.Leg,
                }
            };
            if (victim.inventory.containerWear.itemList.Count > 0)
            {
                using var obj = Facepunch.Pool.Get<PooledList<Item>>();
                obj.AddRange(victim.inventory.containerWear.itemList);
                bool serverUpdate = false;
                for (int i = 0; i < obj.Count; i++)
                {
                    Item item = obj[i];
                    if (item != null && !item.isBroken)
                    {
                        if (config.options.ArmorDamage.ImmuneSkins.Contains(item.skin))
                        {
                            info.HitBone = 0u; // prevent immune skins from taking damage from other plugins
                            continue;
                        }
                        ItemModWearable wearable = item.info.ItemModWearable;
                        if (wearable != null && wearable.ProtectsArea(area))
                        {
                            item.OnAttacked(info);
                            serverUpdate = true;
                        }
                    }
                }
                if (serverUpdate)
                {
                    info.HitBone = 0u; // prevent double armor damage
                    victim.inventory.ServerUpdate(0f);
                }
            }
            if (config.options.ArmorDamage.Headshots && area == HitArea.Head)
            {
                Effect.server.Run("assets/bundled/prefabs/fx/headshot.prefab", victim, 0u, new Vector3(0f, 2f, 0f), Vector3.zero, null);
            }
        }

        private static BaseEntity GetParentEntity(BaseEntity m)
        {
            int n = 0;
            while (m != null && m.HasParent() && ++n < 30)
            {
                if (!(m.GetParentEntity() is BaseEntity parent)) break;
                m = parent;
            }

            return m;
        }

        private bool HasBlockAccess(BuildingBlock block, BasePlayer attacker, bool requiresOwner) 
        { 
            if (IsAlly(block.OwnerID, attacker.userID)) return true; 
            return (!requiresOwner || IsAuthed(block, attacker)) && (!config.options.BlockHandler.Online || BasePlayer.FindByID(block.OwnerID) == null); 
        }

        private readonly BuildingGrade.Enum[] GradeEnums = new BuildingGrade.Enum[] { BuildingGrade.Enum.Twigs, BuildingGrade.Enum.Wood, BuildingGrade.Enum.Stone, BuildingGrade.Enum.Metal, BuildingGrade.Enum.TopTier };

        private DamageResult HandleBlockGrade(BuildingBlock block, BasePlayer attacker, DamageType damageType, float damageAmount, bool selfDamageFlag)
        {
            DamageResult result = DamageResult.None;

            foreach (var grade in GradeEnums)
            {
                if (block.grade == grade)
                {
                    if (!(grade switch
                    {
                        BuildingGrade.Enum.Twigs when config.options.BlockHandler.Twig => true,
                        BuildingGrade.Enum.Wood when config.options.BlockHandler.Wood => true,
                        BuildingGrade.Enum.Stone when config.options.BlockHandler.Stone => true,
                        BuildingGrade.Enum.Metal when config.options.BlockHandler.Metal => true,
                        BuildingGrade.Enum.TopTier when config.options.BlockHandler.HQM => true,
                        _ => false
                    }))
                    {
                        continue;
                    }

                    result = HasBlockAccess(block, attacker, true) ? DamageResult.Allow : DamageResult.Block;

                    if (trace)
                    {
                        string action = result == DamageResult.Allow ? $"{grade} continue checks" : "block and return";
                        Trace($"Initiator is player and target is {grade} block, {action}", 1);
                    }

                    if (result == DamageResult.Block)
                    {
                        HandleBlockOutput(block, damageType, damageAmount, attacker, selfDamageFlag);
                        return result;
                    }
                }
            }

            return result;
        }

        private void HandleBlockOutput(BuildingBlock block, DamageType damageType, float damageAmount, BasePlayer attacker, bool selfDamageFlag)
        {
            if (config.options.BlockHandler.Log)
            {
                string grade = block.grade.ToString();
                string ownerDisplayName = BasePlayer.FindAwakeOrSleepingByID(block.OwnerID) is BasePlayer owner ? owner.displayName : "Unknown Owner";
                Puts($"{grade} Damage: Attacker - {attacker.displayName} ({attacker.userID}) | {grade} Owner: {ownerDisplayName} ({block.OwnerID}) at Location: {block.transform.position} | Damage Amount: {damageAmount}");
            }

            if (config.options.BlockHandler.Notify)
            {
                SendReply(attacker, GetMessage("Twig", attacker.UserIDString));
            }

            if (config.options.BlockHandler.ReflectDamageMultiplier > 0f)
            {
                float reflectedDamage = damageAmount * config.options.BlockHandler.ReflectDamageMultiplier;

                if (!selfDamageFlag)
                {
                    damageType = DamageType.Radiation;
                }

                bool t = trace;
                trace = false;
                attacker.Hurt(reflectedDamage, damageType, attacker, config.options.BlockHandler.ReflectDamageProtection);
                trace = t;

                if (config.options.BlockHandler.Log)
                {
                    Puts($"Debug: Attacker {attacker.displayName} ({attacker.userID}) was hurt for {reflectedDamage} damage. New Health: {attacker.health}");
                }
            }
        }

        private bool IsTutorialNetworkGroup(BaseEntity entity)
        {
            if (entity.net == null || entity.net.group == null) return false;
            return TutorialIsland.IsTutorialNetworkGroup(entity.net.group.ID);
        }

        private DamageResult CheckImmortalFlag(BaseEntity entity, RuleSet ruleSet, BaseEntity initiator, BaseEntity weaponPrefab)
        {
            // Check storage containers and doors for locks for player entity only
            if ((ruleSet._flags & RuleFlags.LockedBoxesImmortal) != 0 && entity is StorageContainer c && !(c is LootContainer or ChickenCoop or Beehive) || (ruleSet._flags & RuleFlags.LockedDoorsImmortal) != 0 && entity is Door)
            {
                if ((ruleSet._flags & RuleFlags.ExcludeTugboatFromImmortalFlags) != 0 && entity.GetParentEntity() is Tugboat)
                {
                    if (trace) Trace($"Player Door/StorageContainer detected with immortal flag on tugboat with ImmortalExcludesTugboats flag; allow and return", 1);
                    return DamageResult.Allow;
                }
                DamageResult hurt = CheckLock(ruleSet, entity, initiator, weaponPrefab); // check for lock
                if (trace)
                {
                    string action = hurt == DamageResult.None ? "null (no lock or unlocked); continue checks" : hurt == DamageResult.Allow ? "allow and return" : "block and return";
                    Trace($"Player Door/StorageContainer detected with immortal flag; lock check results: {action}", 1);
                }
                return hurt;
            }
            return DamageResult.None;
        }

        private object CanHelicopterStrafeTarget(PatrolHelicopterAI ai, BasePlayer ply)
        {
            if (ai == null || ai.isDead || ai.isRetiring || ply == null || ply.IsDestroyed || !ply.InSafeZone())
            {
                return null;
            }
            TriggerSafeZone zone = null;
            if (ply.triggers != null)
            {
                for (int i = 0; i < ply.triggers.Count; i++)
                {
                    TriggerSafeZone triggerSafeZone = ply.triggers[i] as TriggerSafeZone;
                    if (triggerSafeZone != null)
                    {
                        zone = triggerSafeZone;
                        break;
                    }
                }
            }
            if (zone == null || zone.triggerCollider == null || InRange(ply.transform.position, zone.transform.position, zone.triggerCollider.bounds.extents.Max() * 0.85f))
            {
                ai.Invoke("ClearTargets", 0f);
                ai.ClearAimTarget();
                ai.leftGun?.ClearTarget();
                ai.rightGun?.ClearTarget();
                ai.ExitCurrentState();
                ai.State_Patrol_Enter();
                return false;
            }
            return null;
        }

        public static bool InRange(Vector3 a, Vector3 b, float distance)
        {
            return (a - b).sqrMagnitude <= distance * distance;
        }

        private bool HandleHelicopter(RuleSet ruleSet, BaseEntity entity, BaseEntity weapon, BasePlayer victim, bool isVicId, bool allow)
        {
            if (entity is FarmableAnimal or ChickenCoop or Beehive)
            {
                if (trace) Trace($"Initiator is heli, target is {entity.ShortPrefabName}; block and return", 1);
                return false;
            }
            var eval = EvaluateRules(entity, weapon, ruleSet, false);
            if (eval != DamageResult.None)
            {
                string action = eval == DamageResult.Allow ? "allow and return" : "block and return";
                Trace($"Initiator is heli, target is {entity.ShortPrefabName}; {action}", 1);
                return eval == DamageResult.Allow;
            }
            if (isVicId)
            {
                if ((ruleSet._flags & RuleFlags.NoHeliDamageSleepers) != 0)
                {
                    if (trace)
                    {
                        string action1 = victim.IsSleeping() ? "victim is sleeping; block and return" : "victim is not sleeping; continue checks";
                        Trace($"Initiator is heli, and target is player; flag check results: {action1}", 1);
                    }
                    if (victim.IsSleeping()) return false;
                }
                bool val = (ruleSet._flags & RuleFlags.NoHeliDamagePlayer) != 0;
                if (trace)
                {
                    string action = val ? "flag set; block and return" : "flag not set; allow and return";
                    Trace($"Initiator is heli, and target is player; flag check results: {action}", 1);
                }
                return !val;
            }
            if (entity is MiningQuarry)
            {
                bool val = (ruleSet._flags & RuleFlags.NoHeliDamageQuarry) != 0;
                if (trace)
                {
                    string action = val ? "flag set; block and return" : "flag not set; allow and return";
                    Trace($"Initiator is heli, and target is quarry; flag check results: {action}", 1);
                }
                return !val;
            }
            if (entity is HitchTrough.IHitchable)
            {
                bool val = (ruleSet._flags & RuleFlags.NoHeliDamageRidableHorses) != 0;
                if (trace)
                {
                    string action = val ? "flag set; block and return" : "flag not set; allow and return";
                    Trace($"Initiator is heli, and target is ridablehorse; flag check results: {action}", 1);
                }
                return !val;
            }
            if ((ruleSet._flags & RuleFlags.NoHeliDamageBuildings) != 0 && IsPlayerEntity(entity))
            {
                if (!entity.HasParent() && entity is DecayEntity decayEntity && !HasBuildingPrivilege(decayEntity))
                {
                    if (trace) Trace($"Initiator is heli, {entity.ShortPrefabName} is not within TC; allow and return", 1);
                    return true;
                }
                if (trace) Trace($"Initiator is heli, {entity.ShortPrefabName} is within TC; block and return", 1);
                return false;
            }
            if (trace)
            {
                string action = allow ? "allow and return" : "block and return";
                Trace($"Initiator is heli, target is {entity.ShortPrefabName}; {action}", 1);
            }
            return allow;
        }

        private bool HasBuildingPrivilege(DecayEntity decayEntity)
        {
            var building = decayEntity.GetBuilding();
            if (building == null) return false;
            return building.GetDominatingBuildingPrivilege() != null;
        }

        private bool IsAlly(ulong vic, ulong atk) => vic switch
        {
            _ when vic == atk => true,
            _ when RelationshipManager.ServerInstance.playerToTeam.TryGetValue(vic, out var team) && team.members.Contains(atk) => true,
            _ when Clans != null && Convert.ToBoolean(Clans?.Call("IsClanMember", vic, atk)) => true,
            _ when Friends != null && Convert.ToBoolean(Friends?.Call("AreFriends", vic, atk)) => true,
            _ => false
        };

        private bool CanAuthorize(BaseEntity entity, BasePlayer attacker, RuleSet ruleSet)
        {
            if (entity is BaseVehicle && EvaluateRules(entity, attacker, ruleSet, false) == DamageResult.Block)
            {
                return false;
            }

            if (entity.OwnerID == 0)
            {
                return entity is Minicopter;
            }

            return IsPlayerEntity(entity);
        }

        private bool IsPlayerEntity(BaseEntity entity)
        {
            if (entity is BaseMountable || entity is LegacyShelter || entity is LegacyShelterDoor || entity is FarmableAnimal)
            {
                return true;
            }

            if (entity.PrefabName.IndexOf("building", StringComparison.Ordinal) >= 0)
            {
                return true;
            }

            if (entity.PrefabName.IndexOf("modular", StringComparison.Ordinal) >= 0)
            {
                return true;
            }

            if (_deployables.Count == 0)
            {
                foreach (var def in ItemManager.GetItemDefinitions())
                {
                    if (def.TryGetComponent<ItemModDeployable>(out var imd))
                    {
                        _deployables.Add(imd.entityPrefab.resourcePath);
                    }
                }
            }

            return _deployables.Contains(entity.PrefabName);
        }

        private void ExcludePlayer(ulong userid, float maxDelayLength, Plugin plugin)
        {
            if (plugin == null)
            {
                return;
            }
            if (!playerDelayExclusions.TryGetValue(userid, out var exclusions))
            {
                playerDelayExclusions[userid] = exclusions = Pool.Get<List<PlayerExclusion>>();
            }
            var exclusion = exclusions.Find(x => x.plugin == plugin);
            if (maxDelayLength <= 0f)
            {
                if (exclusion != null)
                {
                    exclusions.Remove(exclusion);
                    exclusion.plugin = null;
                    exclusion.time = 0f;
                    Pool.Free(ref exclusion);
                }
                if (exclusions.Count == 0)
                {
                    playerDelayExclusions.Remove(userid);
                    Pool.FreeUnmanaged(ref exclusions);
                }
            }
            else
            {
                if (exclusion == null)
                {
                    exclusion = Pool.Get<PlayerExclusion>();
                    exclusions.Add(exclusion);
                }
                exclusion.plugin = plugin;
                exclusion.time = Time.time + maxDelayLength;
            }
        }

        private bool HasDelayExclusion(ulong userid)
        {
            if (playerDelayExclusions.TryGetValue(userid, out var exclusions))
            {
                for (int i = 0; i < exclusions.Count; i++)
                {
                    var exclusion = exclusions[i];
                    if (!exclusion.IsExpired)
                    {
                        return true;
                    }
                    exclusions.RemoveAt(i);
                    exclusion.plugin = null;
                    exclusion.time = 0f;
                    Pool.Free(ref exclusion);
                    i--;
                }
                if (exclusions.Count == 0)
                {
                    playerDelayExclusions.Remove(userid);
                    Pool.Free(ref exclusions);
                }
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool PlayerHasExclusion(BasePlayer player, PooledList<string> locs)
        {
            if (playerDelayExclusions.Count > 0 && HasDelayExclusion(player.userID))
            {
                return true;
            }
            if ((config.options.Aboveworld < 5000f && player.transform.position.y >= config.options.Aboveworld) ||
                (config.options.Underworld > -500f && player.transform.position.y <= config.options.Underworld))
            {
                return true;
            }
            if (locs != null && locs.Count > 0)
            {
                foreach (var loc in locs)
                {
                    if (config.mappings.TryGetValue(loc, out var mapping) && mapping == "exclude")
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private bool PlayerHasExclusion(BasePlayer player) => 
            player != null && !player.IsDestroyed && PlayerHasExclusion(player, player.transform.position);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool PlayerHasExclusion(BasePlayer player, Vector3 worldPos)
        {
            if (playerDelayExclusions.Count > 0 && HasDelayExclusion(player.userID))
            {
                return true;
            }
            if ((config.options.Aboveworld < 5000f && worldPos.y >= config.options.Aboveworld) ||
                (config.options.Underworld > -500f && worldPos.y <= config.options.Underworld))
            {
                return true;
            }
            if (useZones)
            {
                using var locs = GetLocationKeys(player);
                if (locs != null && locs.Count > 0)
                {
                    foreach (var loc in locs)
                    {
                        if (config.mappings.TryGetValue(loc, out var mapping) && mapping == "exclude")
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        [HookMethod("GetPlayerMapping")]
        public string GetPlayerMapping(BasePlayer player)
        {
            var t = trace;
            trace = false;
            RuleSet ruleSet;
            if (useZones)
            {
                using var entityLocations = GetLocationKeys(player);
                ruleSet = GetRuleSet(entityLocations, entityLocations);
                trace = t;
                if (!entityLocations.IsNullOrEmpty())
                {
                    foreach (var loc in entityLocations)
                    {
                        if (config.mappings.TryGetValue(loc, out var locMapping))
                        {
                            return locMapping;
                        }
                    }
                }
            }
            else ruleSet = currentRuleSet;
            trace = t;
            if (ruleSet != null && ruleSet.enabled && !ruleSet.IsEmpty() && config.mappings.TryGetValue(ruleSet.name, out var ruleSetMapping))
            {
                return ruleSetMapping;
            }
            return "default";
        }

        // process rules to determine whether to allow damage
        private DamageResult EvaluateRules(BaseEntity entity, BaseEntity attacker, RuleSet ruleSet, bool returnDefaultValue = true)
        {
            List<string> e0Groups = config.ResolveEntityGroups(attacker);
            List<string> e1Groups = config.ResolveEntityGroups(entity);

            if (trace)
            {
                string action1 = ConcatenateListOrDefault(e0Groups, "none");
                string action2 = ConcatenateListOrDefault(e1Groups, "none");
                Trace($"Initiator EntityGroup matches: {action1}", 2);
                Trace($"Target EntityGroup matches: {action2}", 2);
            }

            return ruleSet.Evaluate(this, attacker, e0Groups, entity, e1Groups, returnDefaultValue);
        }

        // checks an entity to see if it has a lock
        private DamageResult CheckLock(RuleSet ruleSet, BaseEntity entity, BaseEntity initiator, BaseEntity weaponPrefab)
        {
            var slot = entity.GetSlot(BaseEntity.Slot.Lock); // check for lock

            if (slot == null || !slot.IsLocked())
            {
                return DamageResult.None; // no lock or unlocked, continue checks
            }

            // if HeliDamageLocked flag is false or NoHeliDamage flag, all damage is cancelled from immortal flag
            if ((ruleSet._flags & RuleFlags.HeliDamageLocked) == 0 || (ruleSet._flags & RuleFlags.NoHeliDamage) != 0)
            {
                return DamageResult.Block;
            }

            return CheckHeliInitiator(ruleSet, initiator, weaponPrefab); // cancel damage except from heli
        }
        
        private DamageResult CheckHeliInitiator(RuleSet ruleSet, BaseEntity initiator, BaseEntity weaponPrefab)
        {
            // Check for heli initiator
            if (initiator is PatrolHelicopter || (initiator != null && (initiator.prefabID.Equals(oilfireballsmall) || initiator.prefabID.Equals(heli_napalm))))
            {
                return (ruleSet._flags & RuleFlags.NoHeliDamage) == 0 ? DamageResult.Allow : DamageResult.Block;
            }
            else if (weaponPrefab != null && (weaponPrefab.prefabID.Equals(rocket_heli) || weaponPrefab.prefabID.Equals(rocket_heli_napalm)))
            {
                return (ruleSet._flags & RuleFlags.NoHeliDamage) == 0 ? DamageResult.Allow : DamageResult.Block;
            }
            return DamageResult.None;
        }

        // checks if the player is authorized to damage the entity
        private bool CheckAuthorized(BaseEntity entity, BasePlayer player, RuleSet ruleSet, bool cupboardOwnership)
        {
            if (!cupboardOwnership)
            {
                return entity.OwnerID == 0 && !entity.InSafeZone() || IsAlly(entity.OwnerID, player.userID); // allow damage to entities that the player owns or is an ally of
            }

            // treat entities outside of cupboard range as unowned, and entities inside cupboard range require authorization
            if (entity is LegacyShelter || entity is LegacyShelterDoor)
            {
                var entityPriv = entity.GetEntityBuildingPrivilege();

                return entityPriv == null || entityPriv.AnyAuthed() && entityPriv.IsAuthed(player);
            }

            if (entity is ResourceEntity)
            {
                return true;
            }

            BuildingPrivlidge priv = null;
            if (entity is DecayEntity decayEntity)
            {
                BuildingManager.Building building = decayEntity.GetBuilding();
                if (building != null)
                {
                    priv = building.GetDominatingBuildingPrivilege();
                }
            }

            if (priv == null)
            {
                priv = player.GetBuildingPrivilege(entity.WorldSpaceBounds(), true);
            }

            return priv == null || priv.AnyAuthed() && priv.IsAuthed(player);
        }

        private bool IsFunTurret(AutoTurret turret)
        {
            return turret.GetAttachedWeapon() is BaseProjectile projectile && projectile.GetItem() is Item weapon && weapon.info.shortname.StartsWith("fun.");
        }

        private object OnSamSiteTarget(BaseEntity attacker, BaseEntity entity)
        {
            SamSite ss = attacker as SamSite;
            if (Interface.CallHook("CanEntityBeTargeted", new object[] { entity, attacker }) is bool val)
            {
                if (val)
                {
                    if (trace) Trace($"CanEntityBeTargeted allowed {entity.ShortPrefabName} to be targetted by SamSite", 1);
                    return null;
                }

                if (trace) Trace($"CanEntityBeTargeted blocked {entity.ShortPrefabName} from being targetted by SamSite", 1);
                if (ss != null)
                {
                    ss.CancelInvoke(ss.WeaponTick);
                }
                return true;
            }

            if (attacker != null && IsSkinExclusion(attacker))
            {
                if (trace) Trace($"Target is {entity}; allow and return -> {attacker} skin ID {attacker.skinID}", 1);
                return null;
            }

            RuleSet ruleSet = GetRuleSet(entity, attacker);

            if (ruleSet == null)
            {
                if (trace) Trace($"OnSamSiteTarget allowed {entity.ShortPrefabName} to be targetted; no ruleset found.", 1);
                return null;
            }

            if (entity is MLRSRocket)
            {
                if ((ruleSet._flags & RuleFlags.SamSitesIgnoreMLRS) != 0) return SamSiteHelper(ss, entity);
                return null;
            }

            var staticRespawn = ss == null ? attacker.OwnerID == 0 : ss.staticRespawn;
            if (staticRespawn && (ruleSet._flags & RuleFlags.StaticSamSitesIgnorePlayers) != 0) return SamSiteHelper(attacker, entity);
            if (!staticRespawn && (ruleSet._flags & RuleFlags.PlayerSamSitesIgnorePlayers) != 0) return SamSiteHelper(attacker, entity);

            return null;
        }

        private object OnMlrsFire(MLRS mlrs, BasePlayer player)
        {
            if (mlrs == null || player == null)
            {
                return true;
            }

            if (Interface.CallHook("CanMlrsTargetLocation", new object[] { mlrs, player }) is bool val)
            {
                if (val)
                {
                    if (trace) Trace($"CanMlrsTargetLocation allowed {mlrs.TrueHitPos} to be targetted by {player.displayName}", 1);
                    return null;
                }

                if (trace) Trace($"CanMlrsTargetLocation blocked {mlrs.TrueHitPos} from being targetted by {player.displayName}", 1);
                return true;
            }

            //if (IsSkinExclusion(mlrs))
            //{
            //    if (trace) Trace($"MLRS attacker is {player}; allow and return -> {mlrs} skin ID {mlrs.skinID}", 1);
            //    return null;
            //}

            RuleSet ruleSet = GetRuleSet(player, mlrs);

            if (ruleSet == null)
            {
                if (trace) Trace($"CanMlrsTargetLocation allowed {mlrs.TrueHitPos} to be targetted by {player.displayName}; no ruleset found.", 1);
                return null;
            }

            return (ruleSet._flags & RuleFlags.NoMLRSDamage) != 0 ? true : (object)null;
        }

        private object CanWaterBallSplash(ItemDefinition liquidDef, Vector3 position, float radius, int amount)
        {
            if (config.PreventThrowingWaterInFreezingBiome && TerrainMeta.BiomeMap != null)
            {
                TerrainBiome.Enum biome = (TerrainBiome.Enum)TerrainMeta.BiomeMap.GetBiomeMaxType(position);
                if (biome == TerrainBiome.Enum.Arctic || biome == TerrainBiome.Enum.Tundra)
                {
                    return false;
                }
            }
            if (config.BlockRadioactiveWaterDamage && liquidDef == WaterTypes.RadioactiveWaterItemDef)
            {
                return false;
            }
            return null;
        }

        private object OnEntityMarkHostile(BasePlayer player, float duration)
        {
            if (player == null || Interface.CallHook("CanMarkEntityHostile", player, duration) is bool val && val)
            {
                return null;
            }
            return true;
        }

        private void OnExplosiveDropped(BasePlayer player, TimedExplosive te, ThrownWeapon tw)
        {
            if (player != null && te != null && te.creatorPlayer == null)
            {
                te.creatorPlayer = player;
            }
        }

#if OXIDE_PUBLICIZED || CARBON
        private void OnEntitySpawned(RidableHorse horse)
        {
            if (config.PreventRagdolling && horse != null)
            {
                horse.playerRagdollThreshold = float.MaxValue;
            }
        }

        private void CanRagdollDismount(BaseRagdoll ragdoll, BasePlayer player)
        {
            if (config.PreventRagdolling && ragdoll != null)
            {            
                ragdoll.dieOnImpact = false;
            }
        }
#endif

        #region Locks etc

        private object OnPlayerActiveShieldDrop(BasePlayer player, Shield shield) => true; // Shield

        private object OnPlayerDropActiveItem(BasePlayer player, Item item) => true; // Active held item

        private object OnBackpackDrop(Item backpack, PlayerInventory inv) => true; // Rust backpack


        private int wrongCodes;
        private object OnCodeEntered(CodeLock codeLock, BasePlayer player, string code)
        {
            if (codeLock == null || player == null || player.limitNetworking || player.isInvisible) 
                return null;
            var parent = codeLock.GetParentEntity() as BaseEntity;
            if (parent != null && parent.OwnerID.IsSteamId() && !IsAlly(player.userID, parent.OwnerID))
            {
                Effect.server.Run(codeLock.effectDenied.resourcePath, codeLock, 0u, Vector3.zero, Vector3.forward);
                Effect.server.Run(codeLock.effectShock.resourcePath, codeLock, 0u, Vector3.zero, Vector3.forward);
                player.Hurt((float)(wrongCodes + 1) * 5f, DamageType.ElectricShock, codeLock, useProtection: false);
                if (++wrongCodes % 5 == 0)
                {
                    player.ShowToast(GameTip.Styles.Red_Normal, CodeLock.blockwarning);
                }
                return true;
            }
            wrongCodes = 0;
            return null;
        }

        private void AllowLocksOnContainers()
        {
            if (config.options.Loot.Locks)
            {
                ServerMgr.Instance.StartCoroutine(LockCo());
            }
            if (config.options.Loot.Antigrief)
            {
                Subscribe(nameof(OnCodeEntered));
            }
        }

        private IEnumerator LockCo()
        {
            int checks = 0;
            YieldInstruction instruction = CoroutineEx.waitForSeconds(0.05f);
            foreach (var ent in BaseNetworkable.serverEntities)
            {
                if (++checks >= 200)
                {
                    checks = 0;
                    yield return instruction;
                }
                if (IsUnloading)
                {
                    yield break;
                }
                if (ent is StorageContainer c && c != null && !c.isLockable)
                {
                    OnEntitySpawned(c);
                }
            }
        }

        private void CreateKeyLock(BaseEntity entity, ulong userid)
        {
            if (GameManager.server.CreateEntity(StringPool.Get(2106860026)) is KeyLock keyLock && keyLock != null)
            {
                keyLock.gameObject.Identity();
                keyLock.SetParent(entity, entity.GetSlotAnchorName(BaseEntity.Slot.Lock));
                keyLock.Spawn();
                entity.SetSlot(BaseEntity.Slot.Lock, keyLock);
                keyLock.OwnerID = userid;
                keyLock.firstKeyCreated = true;
                keyLock.SetFlag(BaseEntity.Flags.Locked, true);
            }
        }

        private void CreateCodeLock(BaseEntity entity, ulong userid)
        {
            if (GameManager.server.CreateEntity(StringPool.Get(3518824735)) is CodeLock codeLock && codeLock != null)
            {
                codeLock.gameObject.Identity();
                codeLock.SetParent(entity, entity.GetSlotAnchorName(BaseEntity.Slot.Lock));
                codeLock.Spawn();
                entity.SetSlot(BaseEntity.Slot.Lock, codeLock);
                codeLock.code = UnityEngine.Random.Range(1000, 9999).ToString();
                codeLock.hasCode = true;
                codeLock.OwnerID = userid;
                codeLock.guestCode = string.Empty;
                codeLock.hasGuestCode = false;
                codeLock.guestPlayers.Clear();
                codeLock.whitelistPlayers.Clear();
                codeLock.whitelistPlayers.Add(userid);
                codeLock.SetFlag(BaseEntity.Flags.Locked, true);
            }
        }

        private readonly List<string> doors = new() { "shutter.wood.a" };

        private void OnEntitySpawned(Door door)
        {
            if (config.options.Loot.Locks && door != null && !door.canTakeLock && door.OwnerID.IsSteamId() && doors.Contains(door.ShortPrefabName))
            {
                door.canTakeLock = true;
            }
        }

        private void OnEntitySpawned(StorageContainer container)
        {
            if (container == null || !container.OwnerID.IsSteamId())
                return;

            if (config.options.Loot.Locks && !container.isLockable)
            {
                container.isLockable = !config.options.Loot.NoLocks.Contains(container.ShortPrefabName) && !config.options.Loot.NoLocks.Contains(GetTypeName(container));
            }

            if (config.options.Loot.AutoLock.TryGetValue(container.ShortPrefabName, out string type) || config.options.Loot.AutoLock.TryGetValue(GetTypeName(container), out type))
            {
                if (type.Equals("nothing", StringComparison.OrdinalIgnoreCase))
                    return;

                container.Invoke(() => TryCreateLock(container, type), 0.3f);
            }
        }

        private void OnEntitySpawned(ContainerIOEntity container)
        {
            if (config.options.Loot.AutoLock.TryGetValue(container.ShortPrefabName, out string type) || config.options.Loot.AutoLock.TryGetValue(GetTypeName(container), out type))
            {
                if (type.Equals("nothing", StringComparison.OrdinalIgnoreCase))
                    return;

                container.Invoke(() => TryCreateLock(container, type), 0.3f);
            }
        }

        private bool TryCreateLock(BaseEntity container, string type)
        {
            if (container.IsDestroyed)
                return false;

            var slot = container.GetSlot(BaseEntity.Slot.Lock);

            if (slot != null)
                return false;

            if (type == "codelock")
            {
                CreateCodeLock(container, container.OwnerID);
            }
            else if (type == "keylock")
            {
                CreateKeyLock(container, container.OwnerID);
            }

            return true;
        }

        private void OnEntitySpawned(BaseLock baseLock)
        {
            if (!config.options.Loot.Locks || baseLock == null)
            {
                return;
            }

            BaseEntity entity = baseLock.GetParentEntity();
            if (entity == null || !entity.OwnerID.IsSteamId())
            {
                return;
            }

            if (config.options.Loot.NoLocks.Count > 0)
            {
                if (config.options.Loot.NoLocks.Contains(entity.ShortPrefabName))
                {
                    return;
                }
                if (config.options.Loot.NoLocks.Contains(GetTypeName(entity)))
                {
                    return;
                }
            }
            
            if (entity is StashContainer)
            {
                baseLock.transform.localPosition = new Vector3(0, -0.3f, 0f);
                baseLock.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }
            else if (entity is Beehive)
            {
                baseLock.transform.localPosition = new Vector3(0, 0.8f, 0.3f);
                baseLock.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            }
            else if (entity is FishMount or HuntingTrophy or PhotoFrame)
            {
                baseLock.transform.localPosition = new Vector3(0, entity.bounds.extents.y + 0.25f, 0f);
                baseLock.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            }
            else if (entity is WeaponRack)
            {
                baseLock.transform.localPosition = new Vector3(-entity.bounds.extents.x + 0.15f, entity.bounds.extents.y * 1.25f, 0f);
                if (entity.ShortPrefabName == "weaponrack_stand.deployed") baseLock.transform.localPosition += new Vector3(0f, 0.65f);
                baseLock.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            }
            else if (entity.ShortPrefabName == "bbq.deployed")
            {
                baseLock.transform.localPosition = new Vector3(0.3f, 0.75f, 0f);
            }
            else if (entity is CookingWorkbenchBbq)
            {
                baseLock.transform.localPosition = new Vector3(0.3f, -3f, -0.3f);
            }
            else if (entity is ChickenCoop)
            {
                baseLock.transform.localPosition = new Vector3(-0.3f, 0.35f, 1.5f);
                baseLock.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            }
            else if (entity is Composter || entity.ShortPrefabName == "refinery_small_deployed")
            {
                baseLock.transform.localPosition += new Vector3(0.6f, 0.75f, 0f);
            }
            else if (entity.ShortPrefabName == "fireplace.deployed")
            {
                baseLock.transform.localPosition += new Vector3(-1.0f, 0.9f, -0.225f);
            }
            else if (entity is FlameTurret)
            {
                baseLock.transform.localPosition += new Vector3(-0.075f, 0.165f, 0.075f);
                baseLock.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
            }
            else if (entity.ShortPrefabName == "furnace")
            {
                baseLock.transform.localPosition += new Vector3(0f, 1.2f, 0.2f);
                baseLock.transform.localRotation = new Quaternion(0f, -0.7f, 0f, 0.7f);
            }
            else if (entity.ShortPrefabName == "legacy_furnace")
            {
                baseLock.transform.localPosition += new Vector3(0f, 1.2f, 0.275f);
                baseLock.transform.localRotation = new Quaternion(0f, -0.7f, 0f, 0.7f);
            }
            else if (entity.ShortPrefabName == "furnace.large")
            {
                baseLock.transform.localPosition += new Vector3(0.75f, 1f, -0.75f);
                baseLock.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
            }
            else if (entity.ShortPrefabName == "electricfurnace.deployed")
            {
                baseLock.transform.localPosition += new Vector3(0f, 0.215f, 0.275f);
                baseLock.transform.localRotation = new Quaternion(0f, -0.7f, 0f, 0.7f);
            }
            else if (entity is Stocking)
            {
                baseLock.transform.localPosition += new Vector3(-0.1f, 0.25f, 0f);
                baseLock.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            }
            else if (entity is GunTrap or TorchDeployableLightSource or BaseFuelLightSource)
            {
                baseLock.transform.localPosition += new Vector3(0f, 0.4f, 0f);
            }
            else if (entity.ShortPrefabName == "hitchtrough.deployed")
            {
                baseLock.transform.localPosition = new Vector3(-1.115f, 0.503f, 0.1f);
                baseLock.transform.localRotation = Quaternion.Euler(0f, 180f, 10f);
            }
            else if (entity is MixingTable)
            {
                baseLock.transform.localPosition = new Vector3(-0.575f, 0.4f, 0.275f);
            }
            else if (entity is Mailbox)
            {
                baseLock.transform.localPosition = new Vector3(-0.1f, 1.1675f, 0.2f);
            }
            else if (entity.ShortPrefabName == "planter.large.deployed" || entity.ShortPrefabName == "planter.triangle.deployed")
            {
                baseLock.transform.localPosition = new Vector3(0f, 0.45f, 0f);
                baseLock.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }
            else if (entity.ShortPrefabName == "planter.small.deployed")
            {
                baseLock.transform.localPosition = new Vector3(0f, 0.45f, 0f);
                baseLock.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }
            else if (entity.ShortPrefabName == "bathtub.planter.deployed")
            {
                baseLock.transform.localPosition = new Vector3(0f, 0.45f, 0.65f);
                baseLock.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            }
            else if (entity.ShortPrefabName == "minecart.planter.deployed")
            {
                baseLock.transform.localPosition = new Vector3(0f, 0.65f, 0.55f);
                baseLock.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            }
            else if (entity is ResearchTable)
            {
                baseLock.transform.localPosition += new Vector3(0f, 0.3f, 0f);
                baseLock.transform.localRotation = new Quaternion(0f, -0.7f, 0f, 0.7f);
            }
            else if (entity is Workbench)
            {
                if (entity.ShortPrefabName == "io.table.deployed")
                {
                    baseLock.transform.localPosition += new Vector3(0f, 1.1f, 0f);
                    baseLock.transform.localRotation = Quaternion.Euler(0f, 90f, -45f);
                }
                else
                {
                    baseLock.transform.localPosition += new Vector3(0f, 0.9f, 0f);
                    baseLock.transform.localRotation = new Quaternion(0f, 0f, 1f, 1f);
                }
            }
            else if (entity is VendingMachine)
            {
                baseLock.transform.localPosition += new Vector3(-0.5175f, 0.15f, -0.5f);
            }
        }

        private object OnCupboardAuthorize(BuildingPrivlidge priv, BasePlayer player)
        {
            if (player == null || player.limitNetworking || player.isInvisible || priv == null || !priv.OwnerID.IsSteamId()) return null;
            BaseLock baseLock = priv.GetSlot(BaseEntity.Slot.Lock) as BaseLock;
            if (baseLock != null && baseLock.IsLocked()) return null;
            if (IsAlly(priv.OwnerID, player.userID)) return null;
            Message(player, "Error_CannotAccessEntity");
            return true;
        }

        private object CanLootEntity(BasePlayer player, BuildingPrivlidge priv)
        {
            return OnCupboardAuthorize(priv, player);
        }

        private object CanLootEntity(BasePlayer player, ModularCarGarage carLift)
        {
            if (player == null || carLift == null || carLift.OwnerID == player.userID)
                return null;

            if (carLift.carOccupant != null && carLift.carOccupant.HasSlot(BaseEntity.Slot.Lock))
                return null;

            if (carLift.OwnerID.IsSteamId() && !IsAlly(carLift.OwnerID, player.userID))
                return true;

            return null;
        }

        #endregion Locks

        private object OnSprayCreate(SprayCan sc, Vector3 pos, Quaternion rot)
        {
            if (sc == null || sc.IsDestroyed) return null;
            BasePlayer player = sc.GetOwnerPlayer();
            if (player == null || player.IsDestroyed) return null;
            if (player.InSafeZone()) return true;
            return null;
        }

        private void OnEntitySpawned(BaseOven oven)
        {
            if (config.options.disableBaseOvenSplash && oven != null && oven.OwnerID.IsSteamId())
            {
                oven.disabledBySplash = false;
            }
            if (config.options.Loot.Locks && oven != null && oven is StorageContainer c)
            {
                OnEntitySpawned(c);
            }
        }

        private void OnEntitySpawned(MLRSRocket rocket)
        {
            if (rocket == null || rocket.IsDestroyed) return;
            using var systems = Pool.Get<PooledList<MLRS>>();
            Vis.Entities(rocket.transform.position, 15f, systems, -1);
            if (systems.Count == 0 || CheckIsEventTerritory(systems[0].TrueHitPos)) return;
            if (systems[0].rocketOwnerRef.Get(true) is not BasePlayer owner) return;
            rocket.creatorEntity = owner;
            rocket.OwnerID = owner.userID;
        }

        private bool CheckIsEventTerritory(Vector3 position)
        {
            if (AbandonedBases != null && AbandonedBases.IsLoaded && Convert.ToBoolean(AbandonedBases?.Call("EventTerritory", position))) return true;
            if (RaidableBases != null && RaidableBases.IsLoaded && Convert.ToBoolean(RaidableBases?.Call("EventTerritory", position))) return true;
            return false;
        }

        private bool IsSkinExclusion(BaseEntity entity) => entity != null && entity.skinID != 0 && config.options.SkinExclusions.Count > 0 && config.options.SkinExclusions.Contains(entity.skinID);

        private object SamSiteHelper(BaseEntity attacker, BaseEntity entity)
        {
            if (useZones)
            {
                using var entityLocations = GetLocationKeys(entity);
                using var initiatorLocations = GetLocationKeys(attacker);

                // check for exclusion zones (zones with no rules mapped)
                if (CheckExclusion(entityLocations, initiatorLocations, false))
                {
                    if (trace) Trace($"OnSamSiteTarget allowed {entity.ShortPrefabName} to be targetted; exclusion of zone found.", 1);
                    return null;
                }
            }

            // check for exclusions in entity groups
            if (CheckExclusion(attacker))
            {
                if (trace) Trace($"OnSamSiteTarget allowed {entity.ShortPrefabName} to be targetted; exclusion found in entity group.", 1);
                return null;
            }

            if (trace && entity is BasePlayer) Trace($"SamSitesIgnorePlayers blocked {entity.ShortPrefabName} from being targetted.", 1);
            else if (trace && entity is MLRSRocket) Trace($"SamSitesIgnoreMLRS blocked {entity.ShortPrefabName} from being targetted.", 1);
            if (attacker is SamSite ss)
            {
                ss.CancelInvoke(ss.WeaponTick);
            }
            return true;
        }

        // Check if entity can be targeted
        private object OnEntityEnter(TargetTrigger trigger, BasePlayer target)
        {
            if (trigger == null || target == null)
            {
                return null;
            }

            var entity = trigger.gameObject.ToBaseEntity();
            if (!entity.IsValid())
            {
                return null;
            }

            return OnEntityEnterInternal(entity, target);
        }

        private object OnEntityEnter(TriggerEnterTimer trigger, BaseEntity target)
        {
            if (trigger == null || target == null)
            {
                return null;
            }

            var entity = trigger.gameObject.ToBaseEntity();
            if (!entity.IsValid())
            {
                return null;
            }

            if (Interface.CallHook("CanEntityBeTargeted", new object[] { target, entity }) is bool val)
            {
                return val ? (object)null : true;
            }

            if (entity != null && IsSkinExclusion(entity))
            {
                //if (trace) Trace($"Target is {target}; allow and return -> {entity} skin ID {entity.skinID}", 1);
                return null;
            }

            RuleSet ruleSet = GetRuleSet(target, entity);

            if (ruleSet == null)
            {
                return null;
            }

            if ((ruleSet._flags & RuleFlags.HopperCannotTargetEnemyLoot) != 0 && entity is Hopper)
            {
                DroppedItem di = target as DroppedItem;
                if (di != null)
                {
                    if (di.DroppedBy != 0 && !di.DroppedBy.IsSteamId())
                    {
                        if (trace) Trace($"Dropped item does not belong to a player; allow and return", 2);
                        return null;
                    }
                    if (di.DroppedBy == 0 || di.DroppedBy == entity.OwnerID || IsAuthed(di, entity))
                    {
                        if (trace) Trace($"{entity} is authorized to loot the dropped item; allow and return", 2);
                        return null;
                    }
                }

                PlayerCorpse corpse = target as PlayerCorpse;
                if (corpse != null)
                {
                    if (corpse.playerSteamID != 0 && !corpse.playerSteamID.IsSteamId())
                    {
                        if (trace) Trace($"Corpse does not belong to a player; allow and return", 2);
                        return null;
                    }
                    if (corpse.playerSteamID == 0 || corpse.playerSteamID == entity.OwnerID || corpse.playerSteamID.IsSteamId() && IsAuthed(corpse, entity))
                    {
                        if (trace) Trace($"{entity} is authorized to loot the corpse; allow and return", 2);
                        return null;
                    }
                }

                if (useZones)
                {
                    using var entityLocations = GetLocationKeys(target);
                    using var initiatorLocations = GetLocationKeys(entity);

                    // check for exclusion zones (zones with no rules mapped)
                    if (CheckExclusion(entityLocations, initiatorLocations, trace))
                    {
                        return null;
                    }
                }

                if (CheckExclusion(target, entity))
                {
                    if (trace) Trace($"{entity} and {target} are both excluded in entity groups", 2);
                    return null;
                }

                if (CheckExclusion(entity))
                {
                    if (trace) Trace($"{entity} is excluded in entity groups", 2);
                    return null;
                }

                return true;
            }

            return null;
        }

        private object OnEntityEnterInternal(BaseEntity entity, BasePlayer target)
        {
            if (Interface.CallHook("CanEntityBeTargeted", new object[] { target, entity }) is bool val)
            {
                return val ? (object)null : true;
            }

            if (entity != null && IsSkinExclusion(entity))
            {
                //if (trace) Trace($"Target is {target}; allow and return -> {entity} skin ID {entity.skinID}", 1);
                return null;
            }

            RuleSet ruleSet = GetRuleSet(target, entity);

            if (ruleSet == null)
            {
                return null;
            }

            if (config.PlayersTriggerTurrets && entity.OwnerID == 0uL && target.userID.IsSteamId() && (entity is FlameTurret or AutoTurret) && !entity.HasParent())
            {
                if (entity is NPCAutoTurret && (ruleSet._flags & RuleFlags.SafeZoneTurretsIgnorePlayers) != 0 && target.InSafeZone()) return true;
                return null;
            }

            var isAutoTurret = entity is AutoTurret;

            if (!target.userID.IsSteamId())
            {
                if (isAutoTurret)
                {
                    return (ruleSet._flags & (entity.OwnerID == 0 ? RuleFlags.StaticTurretsIgnoreScientist : RuleFlags.TurretsIgnoreScientist)) != 0 ? true : (object)null;
                }
                else
                {
                    return (ruleSet._flags & RuleFlags.TrapsIgnoreScientist) != 0 ? true : (object)null;
                }
            }
            else if (entity is NPCAutoTurret && entity.OwnerID == 0)
            {
                return (ruleSet._flags & RuleFlags.SafeZoneTurretsIgnorePlayers) != 0 ? true : (object)null;
            }
            else if (isAutoTurret && (ruleSet._flags & (entity.OwnerID == 0 ? RuleFlags.StaticTurretsIgnorePlayers : RuleFlags.TurretsIgnorePlayers)) != 0 || !isAutoTurret && (ruleSet._flags & RuleFlags.TrapsIgnorePlayers) != 0)
            {
                if (isAutoTurret && IsFunTurret(entity as AutoTurret))
                {
                    return null;
                }

                if (useZones)
                {
                    using var entityLocations = GetLocationKeys(target);
                    using var initiatorLocations = GetLocationKeys(entity);

                    // check for exclusion zones (zones with no rules mapped)
                    if (CheckExclusion(entityLocations, initiatorLocations, trace))
                    {
                        return null;
                    }
                }

                // check for exclusions in entity group
                if (CheckExclusion(target, entity) || CheckExclusion(entity))
                {
                    return null;
                }

                return true;
            }

            return null;
        }

        private object OnTurretTarget(AutoTurret turret, BradleyAPC target)
        {
            if (turret == null || target == null) return null;
            RuleSet ruleSet = GetRuleSet(target, turret);

            if (ruleSet == null)
            {
                return null;
            }

            if ((ruleSet._flags & RuleFlags.TurretsIgnoreBradley) == 0)
            {
                // flag not set, do nothing
                return null;
            }

            if (useZones)
            {
                using var entityLocations = GetLocationKeys(target);
                using var initiatorLocations = GetLocationKeys(turret);

                // check for exclusion zones (zones with no rules mapped)
                if (CheckExclusion(entityLocations, initiatorLocations, trace))
                {
                    // zone exclusion, do nothing
                    return null;
                }
            }

            // check for exclusions in entity group
            if (CheckExclusion(target, turret))
            {
                // group exclusion, do nothing
                return null;
            }

            // prevent turret from targeting bradley
            return true;
        }

        private object OnTurretTarget(AutoTurret turret, BasePlayer target)
        {
            if (turret == null || target == null) return null;
            return OnEntityEnterInternal(turret, target);
        }

        // ignore players stepping on traps if configured
        private object OnTrapTrigger(BaseTrap trap, GameObject go)
        {
            if (go == null || trap == null || !go.TryGetComponent(out BasePlayer player))
            {
                return null;
            }

            if (Interface.CallHook("CanEntityTrapTrigger", new object[] { trap, player }) is bool val)
            {
                return val ? (object)null : true;
            }

            RuleSet ruleSet = GetRuleSet(player, trap);

            if (ruleSet == null)
            {
                return null;
            }

            if ((player.IsNpc || !player.userID.IsSteamId()) && (ruleSet._flags & RuleFlags.TrapsIgnoreScientist) != 0)
            {
                return true;
            }
            else if (player.userID.IsSteamId() && (ruleSet._flags & RuleFlags.TrapsIgnorePlayers) != 0)
            {
                if (useZones)
                {
                    using var entityLocations = GetLocationKeys(player);
                    using var initiatorLocations = GetLocationKeys(trap);

                    // check for exclusion zones (zones with no rules mapped)
                    if (CheckExclusion(entityLocations, initiatorLocations, false))
                    {
                        return null;
                    }
                }

                if (CheckExclusion(trap))
                {
                    return null;
                }

                if (config.PlayersTriggerTraps && trap.OwnerID == 0uL && !trap.HasParent())
                {
                    return null;
                }

                return true;
            }

            return null;
        }

        private object OnNpcTarget(BaseNpc npc, BasePlayer target) => OnNpcTargetInternal(npc, target);

        private object OnNpcTarget(BaseNPC2 npc, BasePlayer target) => OnNpcTargetInternal(npc, target);

        private bool isServerStartingUp = true;

        private object OnNpcTargetInternal(BaseEntity npc, BasePlayer target)
        {
            if (isServerStartingUp)
            {
                return true;
            }

            if (target == null)
            {
                return true;
            }

            if (!target.userID.IsSteamId() || !target.IsSleeping())
            {
                return null;
            }

            if (npc == null)
            {
                return true;
            }

            RuleSet ruleSet = GetRuleSet(target, npc);

            if (ruleSet == null || !IsAnimalsIgnoringSleepers(ruleSet))
            {
                return null;
            }

            if (useZones)
            {
                using var entityLocations = GetLocationKeys(target);
                using var initiatorLocations = GetLocationKeys(npc);

                // check for exclusion zones (zones with no rules mapped)
                if (CheckExclusion(entityLocations, initiatorLocations, false))
                {
                    return null;
                }
            }

            return true;
        }

        private readonly Dictionary<uint, string> _typeNameLookup = new();
        private string GetTypeName(BaseEntity entity, string defaultValue = "Unknown")
        {
            if (entity == null)
            {
                return defaultValue;
            }

            if (!_typeNameLookup.TryGetValue(entity.prefabID, out string name))
            {
                BaseEntity prefab = entity.LookupPrefab();
                if (prefab == null)
                {
                    prefab = entity;
                }
                _typeNameLookup[entity.prefabID] = name = prefab.GetType().Name;
            }

            return name;
        }

        // Check for exclusions in entity groups (attacker)
        private bool CheckExclusion(BaseEntity attacker)
        {
            string attackerName = GetTypeName(attacker);
            foreach (var group in config.groups)
            {
                if (group.IsExclusion(attacker.ShortPrefabName) || group.IsExclusion(attackerName))
                {
                    return true;
                }
            }

            return false;
        }

        // Check for exclusions in entity groups (target, attacker)
        private bool CheckExclusion(BaseEntity target, BaseEntity attacker)
        {
            string targetName = GetTypeName(target);
            string attackerName = GetTypeName(attacker);

            foreach (var vicGroup in config.groups)
            {
                if (vicGroup.IsMember(target.ShortPrefabName) || vicGroup.IsExclusion(targetName))
                {
                    // Target is in a relevant group; now check attacker exclusions
                    foreach (var atkGroup in config.groups)
                    {
                        if (atkGroup.IsExclusion(attacker.ShortPrefabName) || atkGroup.IsExclusion(attackerName))
                        {
                            return true; // Exclusion found for attacker
                        }
                    }

                    return false; // Target is in a group, but no attacker exclusion found
                }
            }

            return false; // Target is not in any member or exclusion group
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private RuleSet GetRuleSet(PooledList<string> vicLocations, PooledList<string> atkLocations)
        {
            bool hasAtkLocations = !atkLocations.IsNullOrEmpty();
            bool hasVicLocations = !vicLocations.IsNullOrEmpty();

            if (shareRedirectDudEnabled && (hasAtkLocations ^ hasVicLocations))
            {
                return dudRuleSet;
            }

            if (!hasVicLocations)
            {
                if (trace) Trace("No shared locations with attacker (empty locations for victim) - no exclusions", 3);
                return currentRuleSet;
            }

            if (!hasAtkLocations)
            {
                if (trace) Trace("No shared locations with victim (empty locations for attacker) - no exclusions", 3);
                return currentRuleSet;
            }

            if (trace)
            {
                string str1 = ConcatenateListOrDefault(vicLocations, "empty");
                string str2 = ConcatenateListOrDefault(atkLocations, "empty");

                Trace($"Beginning RuleSet lookup for [{str1}] and [{str2}]", 2);
            }

            RuleSet ruleSet = currentRuleSet;

            using var sharedLocations = GetSharedLocations(vicLocations, atkLocations);

            if (trace)
            {
                string str = ConcatenateListOrDefault(sharedLocations, "none");

                Trace($"Shared locations: {str}", 3);
            }

            if (sharedLocations.Count > 0)
            {
                using var names = Pool.Get<PooledList<string>>();
                foreach (var loc in sharedLocations)
                {
                    if (config.mappings.TryGetValue(loc, out string mapping))
                    {
                        names.Add(mapping);
                    }
                }

                using var sets = Pool.Get<PooledList<RuleSet>>();
                foreach (var name in names)
                {
                    if (ruleSetByNameDictionary.TryGetValue(name, out RuleSet set))
                    {
                        sets.Add(set);
                    }
                }

                if (trace)
                {
                    Trace($"Found {names.Count} location names, with {sets.Count} mapped RuleSets", 3);
                }

                if (sets.Count == 0 && config.mappings.TryGetValue(AllZones, out var val) && ruleSetByNameDictionary.TryGetValue(val, out RuleSet all))
                {
                    sets.Add(all);

                    if (trace)
                    {
                        Trace($"Found allzones mapped RuleSet", 3);
                    }
                }

                if (sets.Count > 1)
                {
                    string ruleSetNames = ConcatenateRuleSetNames(sets);

                    if (trace)
                    {
                        Trace($"WARNING: Found multiple RuleSets: {ruleSetNames}", 3);
                    }

                    Puts(ruleSetNames);
                }

                if (sets.Count > 0)
                {
                    ruleSet = sets[0];

                    if (trace)
                    {
                        Trace($"Found RuleSet: {ruleSet?.name ?? "null"}", 3);
                    }
                }
            }

            if (ruleSet == null)
            {
                ruleSet = currentRuleSet;

                if (trace)
                {
                    Trace($"No RuleSet found; assigned current global RuleSet: {ruleSet?.name ?? "null"}", 3);
                }
            }

            return ruleSet;
        }

        private RuleSet GetRuleSet(BaseEntity e0, BaseEntity e1)
        {
            using var vic = GetLocationKeys(e0);
            using var atk = GetLocationKeys(e1);
            return GetRuleSet(vic, atk);
        }

        // get locations shared between the two passed location lists
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        private PooledList<string> GetSharedLocations(PooledList<string> e0Locations, PooledList<string> e1Locations)
        {
            var sharedLocations = Pool.Get<PooledList<string>>();

            if (e0Locations == null || e1Locations == null || e0Locations.Count == 0 || e1Locations.Count == 0)
                return sharedLocations;

            foreach (string loc in e0Locations)
            {
                if (e1Locations.Contains(loc) && config.HasMapping(loc))
                {
                    sharedLocations.Add(loc);
                }
            }

            return sharedLocations;
        }

        // Check exclusion for given entity locations
        private bool CheckExclusion(PooledList<string> e0Locations, PooledList<string> e1Locations, bool trace)
        {
            if (e0Locations == null || e1Locations == null)
            {
                if (trace) Trace("No shared locations (empty location) - no exclusions", 3);
                return false;
            }
            if (excludeAllZones)
            {
                if (trace) Trace("All zones are excluded via 'AllZones' mapping. Exclusion found.", 3);
                return true;
            }
            using var sharedLocations = GetSharedLocations(e0Locations, e1Locations);
            if (trace)
            {
                string action1 = ConcatenateListOrDefault(e0Locations, "empty");
                string action2 = ConcatenateListOrDefault(e1Locations, "empty");
                string action3 = ConcatenateListOrDefault(sharedLocations, "none");
                Trace($"Checking exclusions between [{action1}] and [{action2}]", 2);
                Trace($"Shared locations: {action3}", 3);
            }
            if (sharedLocations.Count > 0)
            {
                foreach (string loc in sharedLocations)
                {
                    if (exclusionLocationsSet.Contains(loc))
                    {
                        if (trace) Trace($"Found exclusion mapping for location: {loc}", 3);
                        return true;
                    }
                }
            }
            if (trace) Trace("No shared locations, or no matching exclusion mapping - no exclusions", 3);
            return false;
        }

        private Dictionary<string, string> _mappings = new();
        private void SetExposedMappings()
        {
            _mappings.Clear();
            GetMappingsDictionaryNoAlloc(_mappings);
        }

        // add or update a mapping
        private Timer _auMappingTimer;
        private bool AddOrUpdateMapping(string key, string ruleset)
        {
            if (string.IsNullOrEmpty(key) || config == null || ruleset == null || (ruleset != "exclude" && !config.ruleSets.Exists(r => r.name == ruleset)))
            {
                return false;
            }

            config.mappings[key] = ruleset;
            TryBuildExclusionMappings();
            SetUseZones();
            
            if (_auMappingTimer is { Destroyed: false }) _auMappingTimer.Reset();
            else _auMappingTimer = timer.Once(1f, () =>
            {
                SaveConfig();
                SetExposedMappings();
                Interface.CallHook("OnUpdatedMappings", _mappings);
            });

            return true;
        }

        // remove a mapping
        private Timer _removeMappingTimer;
        private bool RemoveMapping(string key)
        {
            if (!string.IsNullOrEmpty(key) && config.mappings.Remove(key))
            {
                if (_removeMappingTimer is { Destroyed: false }) _removeMappingTimer.Reset();
                else _removeMappingTimer = timer.Once(1f, () =>
                {
                    SaveConfig();
                    SetExposedMappings();
                    Interface.CallHook("OnRemovedMappings", _mappings);
                });
                SetUseZones();
                return true;
            }
            return false;
        }

        // remove a list of mappings, optionally add removed mappings to results
        private bool RemoveMappings(List<string> keys, List<string> results = null)
        {
            bool ret = false;
            if (!keys.IsNullOrEmpty())
            {
                foreach (var key in keys)
                {
                    if (RemoveMapping(key))
                    {
                        ret = true;
                        results?.Add(key);
                    }
                }
            }
            return ret;
        }

        // get all mappings
        private void GetMappingsDictionaryNoAlloc(Dictionary<string, string> dict)
        {
            foreach (var pair in config.mappings)
            {
                dict[pair.Key] = pair.Value;
            }
        }

        private void GetMappingsListNoAlloc(List<string> list)
        {
            list.AddRange(config.mappings.Keys);
        }

        #endregion

        #region Messaging
        private void Message(BasePlayer player, string key, params object[] args)
        {
            string message = BuildMessage(player, key, args);
            if (string.IsNullOrEmpty(message)) return;
            SendReply(player, message);
        }

        private void Message(IPlayer user, string key, params object[] args)
        {
            string message = BuildMessage(user.Object as BasePlayer, key, args);
            if (string.IsNullOrEmpty(message)) return;
            user.Reply(RemoveFormatting(message));
        }

        // build message string
        private string BuildMessage(BasePlayer player, string key, params object[] args)
        {
            string message = GetMessage(key, player?.UserIDString);
            if (string.IsNullOrEmpty(message)) return string.Empty;
            if (args.Length > 0) message = string.Format(message, args);
            string type = key.Split('_')[0];
            if (player != null)
            {
                string size = GetMessage("Format_" + type + "Size");
                string color = GetMessage("Format_" + type + "Color");
                return WrapSize(size, WrapColor(color, message));
            }
            else
            {
                string color = GetMessage("Format_" + type + "Color");
                return WrapColor(color, message);
            }
        }

        // prints the value of an Option
        private void PrintValue(ConsoleSystem.Arg arg, string text, bool value)
        {
            SendReply(arg, WrapSize(GetMessage("Format_NotifySize"), WrapColor(GetMessage("Format_NotifyColor"), text + ": ") + value));
        }

        // wrap string in <size> tag, handles parsing size string to integer
        private string WrapSize(string size, string input)
        {
            return int.TryParse(size, out var i) ? WrapSize(i, input) : input;
        }

        // wrap a string in a <size> tag with the passed size
        private string WrapSize(int size, string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;
            return "<size=" + size + ">" + input + "</size>";
        }

        // wrap a string in a <color> tag with the passed color
        private string WrapColor(string color, string input)
        {
            if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(color))
                return input;
            return "<color=" + color + ">" + input + "</color>";
        }

        // show usage information
        private void ShowUsage(IPlayer user) => user.Message(RemoveFormatting(usageString));

        public string RemoveFormatting(string source) => source.Contains('>') ? Regex.Replace(source, "<.*?>", string.Empty) : source;

        // warn that the server is set to PVE mode
        private void WarnPve() => Puts(GetMessage("Warning_PveMode"));
        #endregion

        #region Helper Procedures

        private bool RemoveTemporaryZones()
        {
            using var zones = Facepunch.Pool.Get<PooledList<string>>();
            using var mappings = Facepunch.Pool.Get<PooledList<string>>();

            return RemoveTemporaryZones(zones, mappings);
        }

        private bool RemoveTemporaryZones(List<string> zones, List<string> mappings)
        {
            if (ZoneManager == null) 
                return false;

            if (zones.Count == 0)
                ZoneManager.Call("GetZoneIDsNoAlloc", zones);

            if (mappings.Count == 0)
                GetMappingsListNoAlloc(mappings);

            bool any = false;
            foreach (var mapping in mappings)
            {
                if (!zones.Contains(mapping) && mapping.IsNumeric() && RemoveMapping(mapping))
                {
                    any = true;
                }
            }

            return any;
        }

        // get location keys from ZoneManager (zone IDs) or LiteZones (zone names)
        private PooledList<string> GetLocationKeys(BaseEntity entity)
        {
            if (!useZones || entity == null) return null;
            var locations = Pool.Get<PooledList<string>>();
            if (ZoneManager != null && ZoneManager.IsLoaded)
            {
                using var locs = Pool.Get<PooledList<string>>();
                if (entity is BasePlayer player)
                {
                    // BasePlayer fix from chadomat
                    string[] array = (string[])ZoneManager.Call("GetPlayerZoneIDs", new object[] { player });
                    if (array != null && array.Length > 0)
                    {
                        foreach (string loc in array)
                        {
                            if (!string.IsNullOrEmpty(loc) && !locs.Contains(loc))
                            {
                                locs.Add(loc);
                            }
                        }
                    }
                }
                else if (entity.IsValid())
                {
                    string[] array = (string[])ZoneManager.Call("GetEntityZoneIDs", new object[] { entity });
                    if (array != null && array.Length > 0)
                    {
                        foreach (string loc in array)
                        {
                            if (!string.IsNullOrEmpty(loc) && !locs.Contains(loc))
                            {
                                locs.Add(loc);
                            }
                        }
                    }
                }
                if (locs.Count > 0)
                {
                    // Add names into list of ID numbers
                    foreach (string loc in locs)
                    {
                        if (!locations.Contains(loc)) locations.Add(loc);
                        string zname = (string)ZoneManager.Call("GetZoneName", loc);
                        if (!string.IsNullOrEmpty(zname) && !locations.Contains(zname)) locations.Add(zname);
                    }
                }
            }
            if (LiteZones != null && LiteZones.IsLoaded)
            {
                List<string> locs = (List<string>)LiteZones?.Call("GetEntityZones", new object[] { entity });
                if (locs != null && locs.Count > 0)
                {
                    foreach (string loc in locs)
                    {
                        if (!locations.Contains(loc))
                        {
                            locations.Add(loc);
                        }
                    }
                }
            }
            return locations;
        }

        // handle raycast from player (for prodding)
        private bool GetRaycastTarget(BasePlayer player, out BaseEntity closestEntity)
        {
            if (Physics.Raycast(player.eyes.HeadRay(), out var hit, 10f) && hit.GetEntity() is BaseEntity hitEntity)
            {
                closestEntity = hitEntity;
                return closestEntity != null;
            }
            closestEntity = null;
            return false;
        }

        // loop to update current ruleset
        private void TimerLoop(bool firstRun = false)
        {
            config.schedule.ClockUpdate(out var ruleSetName, out currentBroadcastMessage);
            if (firstRun || currentRuleSet.name != ruleSetName)
            {
                if (string.IsNullOrEmpty(ruleSetName))
                {
                    ruleSetName = config.defaultRuleSet;
                }

                RuleSet ruleSet = config.ruleSets.Find(r => r.name == ruleSetName && r.enabled && !r.IsEmpty());

                if (ruleSet != null)
                {
                    currentRuleSet = ruleSet;
                }

                ValidateCurrentDamageHook();
                if (config.schedule.broadcast && !string.IsNullOrEmpty(currentBroadcastMessage))
                {
                    Server.Broadcast(currentBroadcastMessage, GetMessage("Prefix"));
                    Puts(RemoveFormatting(" Schedule Broadcast: " + currentBroadcastMessage));
                }
            }

            if (config.schedule.enabled)
            {
                scheduleUpdateTimer = timer.Once(config.schedule.useRealtime ? 30f : 3f, () => TimerLoop());
            }
        }

        private void ValidateCurrentDamageHook()
        {
            if (!config.options.handleDamage)
            {
                Unsubscribe(nameof(OnEntityTakeDamage));
                tpveEnabled = false;
                return;
            }
            RuleSet ruleSet = currentRuleSet;
            tpveEnabled = ruleSet != null && ruleSet.enabled && !ruleSet.IsEmpty();
            if (tpveEnabled)
            {
                Subscribe(nameof(OnEntityTakeDamage));
            }
            else
            {
                Unsubscribe(nameof(OnEntityTakeDamage));
            }
        }

        #endregion

        #region Subclasses
        // configuration and data storage container

        private class TwigDamageOptions
        {
            [JsonProperty(PropertyName = "Apply To Twig (when TwigDamage flag is not set")]
            public bool Twig;

            [JsonProperty(PropertyName = "Apply To Wood")]
            public bool Wood;

            [JsonProperty(PropertyName = "Apply To Stone")]
            public bool Stone;

            [JsonProperty(PropertyName = "Apply To Metal")]
            public bool Metal;

            [JsonProperty(PropertyName = "Apply To HQM")]
            public bool HQM;

            [JsonProperty(PropertyName = "Require Owner Online")]
            public bool Online = true;

            [JsonProperty(PropertyName = "Log Offenses")]
            public bool Log;

            [JsonProperty(PropertyName = "Notify Offenders")]
            public bool Notify;

            [JsonProperty(PropertyName = "Reflect Damage Multiplier")]
            public float ReflectDamageMultiplier;

            [JsonProperty(PropertyName = "Multiplier Allows Armor Protection")]
            public bool ReflectDamageProtection = true;

            internal bool Any => Log || Notify || ReflectDamageMultiplier > 0f || Twig || Wood || Stone || Metal || HQM;
        }

        private class ConfigurationOptions
        {
            [JsonProperty(PropertyName = "Entities with these skin ID's can hurt anything")]
            public List<ulong> SkinExclusions = new();

            [JsonProperty(PropertyName = "Armor damage (PVE)")]
            public ArmorDamagePVE ArmorDamage = new();

            [JsonProperty(PropertyName = "Loot")]
            public LootSupport Loot = new();

            [JsonProperty(PropertyName = "Reflect PVP Damage Multipliers (0 = disabled, 1 = 100%)")]
            public ReflectDamagePVP Reflect = new();

            [JsonProperty(PropertyName = "TwigDamage (FLAG)")]
            public TwigDamageOptions BlockHandler = new();

            [JsonProperty(PropertyName = "handleDamage")] // (true) enable TruePVE damage handling hooks
            public bool handleDamage = true;

            [JsonProperty(PropertyName = "useZones")] // (true) use ZoneManager/LiteZones for zone-specific damage behavior (requires modification of ZoneManager.cs)
            public bool useZones = true;

            [JsonProperty(PropertyName = "Trace To Player Console")]
            public bool PlayerConsole;

            [JsonProperty(PropertyName = "Trace To Server Console")]
            public bool ServerConsole = true;

            [JsonProperty(PropertyName = "Log Trace To File")]
            public bool LogToFile = true;

            [JsonProperty(PropertyName = "Maximum Distance From Player To Trace")]
            public float MaxTraceDistance = 50f;

            [JsonProperty(PropertyName = "Prevent Water From Extinguishing BaseOven")]
            public bool disableBaseOvenSplash;

            [JsonProperty(PropertyName = "Prevent Players From Being Marked Hostile")]
            public bool disableHostility;

            [JsonProperty(PropertyName = "Allow PVP Below Height")]
            public float Underworld = -500f;

            [JsonProperty(PropertyName = "Allow PVP Above Height")]
            public float Aboveworld = 5000f;

            [JsonProperty(PropertyName = "Allow Other Damage Below Height")]
            public float UnderworldOther = -500f;

            [JsonProperty(PropertyName = "Allow Other Damage Above Height")]
            public float AboveworldOther = 5000f;

            [JsonProperty(PropertyName = "Allow Cold Metabolism Damage")]
            public bool Cold;

            [JsonProperty(PropertyName = "Allow Heat Metabolism Damage")]
            public bool Heat;

            [JsonProperty(PropertyName = "Allow Thirst And Hunger Damage To Farmable Animals")]
            public bool FarmableMetabolism = true;

            [JsonProperty(PropertyName = "Auto remove mappings that no longer exist on server restart")]
            public bool AutoRemove;
        }

        private class LootSupport
        {
            [JsonProperty(PropertyName = "Auto lock (codelock, keylock, nothing)", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, string> AutoLock = new() { ["cupboard.tool.deployed"] = "nothing" };

            [JsonProperty(PropertyName = "Exceptions for locks to various containers option", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> NoLocks = new() { "skulltrophy.deployed", "skull_fire_pit", "bbq.static", "small_refinery_static", "campfire" };

            [JsonProperty(PropertyName = "Enable support to allow adding locks to various containers")]
            public bool Locks;

            [JsonProperty(PropertyName = "Enable codelock anti-raiding (team/clan/friend access only)")]
            public bool Antigrief;

            [JsonProperty(PropertyName = "Protect unlocked TC from being accessed by enemy players")]
            public bool ProtectTC;

            [JsonProperty(PropertyName = "Prevent player shield from dropping on death")]
            public bool NoShieldDrop;

            [JsonProperty(PropertyName = "Prevent player active item from dropping on death")]
            public bool NoActiveItemDrop;

            [JsonProperty(PropertyName = "Prevent player backpack from dropping on death (Rust backpack)")]
            public bool NoRustBackpackDrop;

            [JsonProperty(PropertyName = "Prevent players from using enemy car lifts")]
            public bool Lifts;
        }

        private class ArmorDamagePVE
        {
            [JsonProperty(PropertyName = "Skin IDs which are immune to damage", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<ulong> ImmuneSkins = new();

            [JsonProperty(PropertyName = "Enable support for npcs to cause armor damage on hit")]
            public bool Enabled;

            [JsonProperty(PropertyName = "Play headshot sound when a player is headshot by an npc")]
            public bool Headshots;
        }

        private class ReflectDamagePVP
        {
            [JsonProperty(PropertyName = "Multiplier Allows Armor Protection")]
            public bool Protection = true;

            [JsonProperty(PropertyName = "Arrow Damage")]
            public float Arrow;

            [JsonProperty(PropertyName = "Blunt Damage")]
            public float Blunt;

            [JsonProperty(PropertyName = "Bullet Damage")]
            public float Bullet;

            [JsonProperty(PropertyName = "Slash Damage")]
            public float Slash;

            [JsonProperty(PropertyName = "Stab Damage")]
            public float Stab;

            internal bool Any => Arrow != 0 || Blunt != 0 || Bullet != 0 || Slash != 0 || Stab != 0;

            internal float Get(DamageType type) => type switch
            {
                DamageType.Arrow => Arrow,
                DamageType.Blunt => Blunt,
                DamageType.Bullet => Bullet,
                DamageType.Slash => Slash,
                DamageType.Stab => Stab,
                _ => 0
            };
        }

        private class SleeperAuthorizationOptions
        {
            [JsonProperty(PropertyName = "Enabled")]
            public bool Enabled;

            [JsonProperty(PropertyName = "Distinct Loot Required (performance heavy)")]
            public int BaseLoot;

            [JsonProperty(PropertyName = "Foundations Required (performance heavy)")]
            public int FoundationLimit;

            [JsonProperty(PropertyName = "Walls Required (performance heavy)")]
            public int WallLimit;

            [JsonProperty(PropertyName = "Include Twig Structures (performance heavy)")]
            public bool Twig;

            [JsonProperty(PropertyName = "Include Wood Structures (performance heavy)")]
            public bool Wood;

            [JsonProperty(PropertyName = "Skip above checks when the entity count of a base exceeds X (performance heavy)")]
            public int EntityOverrideLimit;

            internal Dictionary<uint, bool> _cache = new();
            internal List<string> ID_FLOORS = new() { "floor", "floor.frame", "floor.grill", "floor.ladder.hatch", "floor.triangle", "floor.triangle.frame", "floor.triangle.grill", "floor.triangle.ladder.hatch" };

            internal bool Checks => BaseLoot > 0 || FoundationLimit > 0 || WallLimit > 0;

            internal bool Any => Checks || EntityOverrideLimit > 0;

            internal List<string> shortnames = new();
            
            internal List<ItemContainer> buffer = new();

            public bool MeetsMinimumRequirements(BuildingPrivlidge priv)
            {
                if (!Any)
                    return true;

                if (_cache.TryGetValue(priv.buildingID, out bool value))
                    return value;
                
                BuildingManager.Building building = priv.GetBuilding();
                if (building == null || !building.HasDecayEntities()) 
                    return false;

                uint ID = building.ID;
                int count = building.decayEntities.Count;

                if (EntityOverrideLimit > 0 && count > EntityOverrideLimit)
                {
                    _cache[ID] = true;
                    return true;
                }

                if (!Checks)
                    return true;

                shortnames.Clear();
                buffer.Clear();

                int walls = 0, foundations = 0, floors = 0, counted = 0;

                foreach (var e in building.decayEntities)
                {
                    if (e == null || e.IsDestroyed)
                    {
                        continue;
                    }
                    if (++counted % 10 == 0 && Performance.report.frameRate < 15)
                    {
                        return false;
                    }
                    if (BaseLoot <= 0 && !(e is BuildingBlock))
                    {
                        continue;
                    }
                    if (FoundationLimit > 0)
                    {
                        if (e.ShortPrefabName == "foundation" || e.ShortPrefabName == "foundation.triangle")
                        {
                            BuildingBlock block = e as BuildingBlock;
                            if (!Twig && block.grade == BuildingGrade.Enum.Twigs)
                            {
                                continue;
                            }
                            if (!Wood && block.grade == BuildingGrade.Enum.Wood)
                            {
                                continue;
                            }
                            foundations++;
                            continue;
                        }
                    }
                    if (WallLimit > 0)
                    {
                        if (e.ShortPrefabName == "wall" || e.ShortPrefabName == "wall.half" || e.ShortPrefabName == "wall.window")
                        {
                            BuildingBlock block = e as BuildingBlock;
                            if (!Twig && block.grade == BuildingGrade.Enum.Twigs)
                            {
                                continue;
                            }
                            if (!Wood && block.grade == BuildingGrade.Enum.Wood)
                            {
                                continue;
                            }
                            walls++;
                            continue;
                        }
                        if (ID_FLOORS.Contains(e.ShortPrefabName))
                        {
                            if (e.children != null)
                            {
                                foreach (var x in e.children)
                                {
                                    if (x is CollectibleEntity col && col != null && col.itemList == null)
                                    {
                                        foundations++;
                                    }
                                }
                            }
                            floors++;
                            continue;
                        }
                    }
                    if (BaseLoot > 0)
                    {
                        AddDistinctBaseLoot(shortnames, buffer, e);
                    }
                }

                if (foundations == 0)
                {
                    foundations = floors;
                }

                bool wallCheck = WallLimit <= 0 || walls >= WallLimit;
                bool foundationCheck = FoundationLimit <= 0 || FoundationLimit >= foundations;
                bool baseLootCheck = BaseLoot <= 0 || shortnames.Count >= BaseLoot;

                _cache[ID] = value = wallCheck && foundationCheck && baseLootCheck;
                if (!value) priv.Invoke(() => _cache.Remove(ID), 15f);
                return value;
            }

            private static void AddDistinctBaseLoot(List<string> shortnames, List<ItemContainer> containers, DecayEntity ent)
            {
                IInventoryProvider provider = ent as IInventoryProvider;
                if (provider == null)
                {
                    return;
                }

                provider.GetAllInventories(containers);
                if (containers.Count == 0)
                {
                    return;
                }

                foreach (ItemContainer container in containers)
                {
                    if (container == null || container.itemList == null)
                    {
                        continue;
                    }
                    foreach (Item item in container.itemList)
                    {
                        if (item != null && item.info != null && !shortnames.Contains(item.info.shortname))
                        {
                            shortnames.Add(item.info.shortname);
                        }
                    }
                }

                containers.Clear();
            }
        }

        private class Configuration
        {
            [JsonProperty(PropertyName = "Config Version")]
            public string configVersion = null;

            [JsonProperty(PropertyName = "Default RuleSet")]
            public string defaultRuleSet = "default";

            [JsonProperty(PropertyName = "Configuration Options")]
            public ConfigurationOptions options = new();

            [JsonProperty(PropertyName = "Mappings")]
            public Dictionary<string, string> mappings = new();

            [JsonProperty(PropertyName = "Schedule")]
            public Schedule schedule = new();

            [JsonProperty(PropertyName = "RuleSets")]
            public List<RuleSet> ruleSets = new();

            [JsonProperty(PropertyName = "Entity Groups")]
            public List<EntityGroup> groups = new();

            [JsonProperty(PropertyName = "Allow Killing Sleepers")]
            public bool AllowKillingSleepers;

            [JsonProperty(PropertyName = "Allow Killing Sleepers (Ally Only)")]
            public bool AllowKillingSleepersAlly;

            [JsonProperty(PropertyName = "Allow Killing Sleepers (Authorization Only)", NullValueHandling = NullValueHandling.Ignore)]
            public bool? _AllowKillingSleepersAuthorization = null;

            [JsonProperty(PropertyName = "Allow Killing Sleepers (TC Auth Only)")]
            public SleeperAuthorizationOptions AllowKillingSleepersAuthorization = new();

            [JsonProperty(PropertyName = "Allow Killing Sleepers (After X Hours Offline)")]
            public float AllowKillingSleepersHoursOffline;

            [JsonProperty(PropertyName = "Allow Killing Sleepers (Allowed steam ids)", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<ulong> AllowKillingSleepersIds = new() { 0 };

            [JsonProperty(PropertyName = "Ignore Firework Damage")]
            public bool Firework = true;

            [JsonProperty(PropertyName = "Ignore Campfire Damage")]
            public bool Campfires;

            [JsonProperty(PropertyName = "Ignore Ladder Damage")]
            public bool Ladders;

            [JsonProperty(PropertyName = "Ignore Sleeping Bag Damage")]
            public bool SleepingBags;

            [JsonProperty(PropertyName = "Players Can Trigger Traps In Monument Topology")]
            public bool PlayersTriggerTraps = true;

            [JsonProperty(PropertyName = "Players Can Hurt Traps In Monument Topology")]
            public bool PlayersHurtTraps;

            [JsonProperty(PropertyName = "Players Can Trigger Turrets In Monument Topology")]
            public bool PlayersTriggerTurrets = true;

            [JsonProperty(PropertyName = "Players Can Hurt Turrets In Monument Topology")]
            public bool PlayersHurtTurrets;

            [JsonProperty(PropertyName = "Block Scrap Heli Damage")]
            public bool scrap = true;

            [JsonProperty(PropertyName = "Block Igniter Damage")]
            public bool igniter;

            [JsonProperty(PropertyName = "Block Wallpaper Damage")]
            public bool wallpaper = true;

            [JsonProperty(PropertyName = "Block Radioactive Water Damage")]
            public bool BlockRadioactiveWaterDamage = true;

            [JsonProperty(PropertyName = "Block Decay Damage To Vehicles")]
            public bool BlockDecayDamageToVehicles;

            [JsonProperty(PropertyName = "Block Spray Can In Safe Zones")]
            public bool BlockSprayCanInSafeZones;

            [JsonProperty(PropertyName = "Prevent heli from strafing in the inner radius of safe zones")]
            public bool PreventSafeZoneStrafing;

            [JsonProperty(PropertyName = "Prevent players from throwing water in arctic and tundra biome")]
            public bool PreventThrowingWaterInFreezingBiome;

            [JsonProperty(PropertyName = "Prevent ragdolling when struck by another vehicle")]
            public bool PreventRagdolling = true;

            [JsonProperty(PropertyName = "Experimental ZoneManager support for PVE zones")]
            public bool PVEZones;

            internal Dictionary<ulong, List<string>> groupCache = new();
            internal TruePVE instance;

            public void Init(TruePVE instance)
            {
                this.instance = instance;
                schedule.Init(instance);
                foreach (RuleSet rs in ruleSets)
                    rs.Build(instance);
                ruleSets.Remove(null);
            }

            public List<string> ResolveEntityGroups(BaseEntity entity)
            {
                ulong id = entity == null || entity.net == null ? 0 : entity.net.ID.Value;

                if (id > 0 && groupCache.TryGetValue(id, out var cachedGroups))
                {
                    return cachedGroups;
                }

                List<string> currentGroups = new(groups.Count);

                string typeName = instance.GetTypeName(entity);

                foreach (EntityGroup group in groups)
                {
                    if (group.Contains(typeName, entity.ShortPrefabName))
                    {
                        currentGroups.Add(group.name);
                    }
                }

                if (id > 0)
                {
                    groupCache[id] = currentGroups;
                }

                return currentGroups;
            }

            public bool HasMapping(string key)
            {
                return mappings.ContainsKey(key) || mappings.ContainsKey(AllZones);
            }

            public RuleSet GetDefaultRuleSet()
            {
                RuleSet foundRuleSet = null;
                int matchCount = 0;

                foreach (var r in ruleSets)
                {
                    if (r.name == defaultRuleSet)
                    {
                        foundRuleSet = r;
                        matchCount++;
                    }
                }

                if (matchCount > 1)
                {
                    Puts($"Warning - duplicate ruleset found for default RuleSet: '{defaultRuleSet}'");
                }

                return foundRuleSet;
            }

            public RuleSet GetDudRuleSet()
            {
                return new("override")
                {
                    _flags = RuleFlags.HumanNPCDamage,
                    defaultAllowDamage = false,
                    enabled = true
                };
            }
        }

        private class RuleSet
        {
            public string name;
            public bool enabled = true;
            public bool defaultAllowDamage = false;
            public string flags = string.Empty;
            internal RuleFlags _flags = RuleFlags.None;
            internal bool Changed;

            public HashSet<string> rules = new();
            internal HashSet<Rule> parsedRules = new();
            internal Dictionary<string, Rule> ruleDictionary = new();

            public RuleSet() { }
            public RuleSet(string name) { this.name = name; }

            // evaluate the passed lists of entity groups against rules
            public DamageResult Evaluate(TruePVE instance, BaseEntity attacker, List<string> eg1, BaseEntity victim, List<string> eg2, bool returnDefaultValue = true)
            {
                bool trace = instance.trace;

                if (trace) instance.Trace("Evaluating Rules...", 3);

                if (ruleDictionary == null || ruleDictionary.Count == 0)
                {
                    if (trace) instance.Trace($"No rules found; returning default value: {defaultAllowDamage}", 4);
                    return defaultAllowDamage ? DamageResult.Allow : DamageResult.Block;
                }

                bool vg1 = eg1 != null && eg1.Count > 0;
                bool vg2 = eg2 != null && eg2.Count > 0;

                // 1. Check all direct links between eg1 and eg2
                if (vg1 && vg2)
                {
                    if (trace) instance.Trace("Checking direct initiator->target rules...", 4);

                    foreach (string s1 in eg1)
                    {
                        foreach (string s2 in eg2)
                        {
                            string ruleText = s1 + "->" + s2; // Using concatenation for performance

                            if (trace) instance.Trace($"Evaluating \"{ruleText}\"...", 5);

                            if (ruleDictionary.TryGetValue(ruleText, out Rule rule))
                            {
                                if (trace) instance.Trace($"Match found; allow damage? {rule.hurt}", 6);
                                return rule.hurt ? DamageResult.Allow : DamageResult.Block;
                            }

                            if (trace) instance.Trace("No match found", 6);
                        }
                    }
                }

                // 2. If no direct match, check group -> Any
                if (vg1)
                {
                    if (trace) instance.Trace("No direct match rules found; continuing with group->Any...", 4);

                    foreach (string s1 in eg1)
                    {
                        string ruleText = s1 + "->" + Any;

                        if (trace) instance.Trace($"Evaluating \"{ruleText}\"...", 5);

                        if (ruleDictionary.TryGetValue(ruleText, out Rule rule))
                        {
                            if (trace) instance.Trace($"Match found; allow damage? {rule.hurt}", 6);
                            return rule.hurt ? DamageResult.Allow : DamageResult.Block;
                        }

                        if (trace) instance.Trace("No match found", 6);
                    }
                }

                // 3. If still no match, check Any -> group
                if (vg2)
                {
                    if (trace) instance.Trace("No matching group->Any rules found; continuing with Any->group...", 4);

                    foreach (string s2 in eg2)
                    {
                        string ruleText = Any + "->" + s2;

                        if (trace) instance.Trace($"Evaluating \"{ruleText}\"...", 5);

                        if (ruleDictionary.TryGetValue(ruleText, out Rule rule))
                        {
                            if (trace) instance.Trace($"Match found; allow damage? {rule.hurt}", 6);
                            return rule.hurt ? DamageResult.Allow : DamageResult.Block;
                        }

                        if (trace) instance.Trace("No match found", 6);
                    }
                }

                // 4. If no rule was found, return the default value if specified
                if (returnDefaultValue)
                {
                    if (trace) instance.Trace($"No matching rules found; returning default value: {defaultAllowDamage}", 4);

                    return defaultAllowDamage ? DamageResult.Allow : DamageResult.Block;
                }

                // 5. If not returning default, default to None
                return DamageResult.None;
            }

            // build rule strings to rules
            public void Build(TruePVE instance)
            {
                foreach (string ruleText in rules)
                    parsedRules.Add(new(instance, ruleText));
                parsedRules.Remove(null);
                ValidateRules();
                InitializeRuleDictionary();
                if (flags.Length == 0)
                {
                    _flags |= RuleFlags.None;
                    return;
                }
                foreach (string _value in flags.Split(','))
                {
                    string value = _value.Trim();
                    if (!Enum.TryParse(value, out RuleFlags flag))
                    {
                        if (value == "SamSitesIgnorePlayers")
                        {
                            ConvertSamSiteFlag();
                        }
                        else if (value == "TrapsIgnoreScientists")
                        {
                            ConvertTrapsIgnoreScientists();
                        }
                        else if (value == "TurretsIgnoreScientists")
                        {
                            ConvertTurretsIgnoreScientists("TurretsIgnoreScientists", "TurretsIgnoreScientist", RuleFlags.TurretsIgnoreScientist);
                        }
                        else if (value == "StaticTurretsIgnoreScientists")
                        {
                            ConvertTurretsIgnoreScientists("StaticTurretsIgnoreScientists", "StaticTurretsIgnoreScientist", RuleFlags.StaticTurretsIgnoreScientist);
                        }
                        else
                        {
                            Puts("WARNING - invalid flag: '{0}' (does this flag still exist?)", value);
                        }
                    }
                    else if (!HasFlag(flag))
                    {
                        _flags |= flag;
                    }
                }
                if (Changed)
                {
                    instance.SaveConfig();
                    Changed = false;
                }
            }

            public void InitializeRuleDictionary()
            {
                if (parsedRules != null)
                {
                    ruleDictionary = new(StringComparer.OrdinalIgnoreCase);

                    foreach (Rule rule in parsedRules)
                    {
                        if (rule != null && rule.valid && rule.key != null)
                        {
                            ruleDictionary[rule.key] = rule;
                        }
                    }
                }
                else
                {
                    ruleDictionary = null;
                }
            }

            private void ConvertSamSiteFlag()
            {
                flags = flags.Replace("SamSitesIgnorePlayers", "PlayerSamSitesIgnorePlayers, StaticSamSitesIgnorePlayers");
                if (!HasFlag(RuleFlags.PlayerSamSitesIgnorePlayers))
                {
                    _flags |= RuleFlags.PlayerSamSitesIgnorePlayers;
                }
                if (!HasFlag(RuleFlags.StaticSamSitesIgnorePlayers))
                {
                    _flags |= RuleFlags.StaticSamSitesIgnorePlayers;
                }
                Changed = true;
            }

            private void ConvertTrapsIgnoreScientists()
            {
                flags = flags.Replace("TrapsIgnoreScientists", "TrapsIgnoreScientist");
                if (!HasFlag(RuleFlags.TrapsIgnoreScientist))
                {
                    _flags |= RuleFlags.TrapsIgnoreScientist;
                }
                Changed = true;
            }

            private void ConvertTurretsIgnoreScientists(string from, string to, RuleFlags flag)
            {
                flags = flags.Replace(from, to);
                if (!HasFlag(flag))
                {
                    _flags |= flag;
                }
                Changed = true;
            }

            public void ValidateRules()
            {
                foreach (Rule rule in parsedRules)
                    if (!rule.valid)
                        Interface.Oxide.LogWarning($"Warning - invalid rule: {rule.ruleText}");
            }

            // add a rule
            public void AddRule(TruePVE instance, string ruleText)
            {
                rules.Add(ruleText);
                parsedRules.Add(new(instance, ruleText));
            }

            public bool HasAnyFlag(RuleFlags flags) => (_flags | flags) != RuleFlags.None;
            public bool HasFlag(RuleFlags flag) => (_flags & flag) == flag;
            public bool IsEmpty() => rules.IsNullOrEmpty() && _flags == RuleFlags.None;
        }
        
        private class Rule
        {
            public string ruleText;
            internal string key;
            internal bool hurt;
            internal bool valid;

            public Rule() { }
            public Rule(TruePVE instance, string ruleText)
            {
                this.ruleText = ruleText;
                valid = Translate(instance);
            }

            public bool Translate(TruePVE instance)
            {
                if (string.IsNullOrWhiteSpace(ruleText))
                    return false;

                string[] splitStr = instance.regex.Split(ruleText.Trim());
                if (splitStr.Length < 3)
                    return false;

                string rs0 = splitStr[0];
                string rs1 = splitStr[^1]; // Using index from end operator
                string[] mid = splitStr[1..^1]; // Slicing the array

                bool canHurt = !Array.Exists(mid, s => s.Equals("cannot", StringComparison.OrdinalIgnoreCase) || s.Equals("can't", StringComparison.OrdinalIgnoreCase));

                // rs0 and rs1 shouldn't ever be "nothing" simultaneously
                if (rs0.Equals("nothing", StringComparison.OrdinalIgnoreCase) || rs1.Equals("nothing", StringComparison.OrdinalIgnoreCase) || rs0.Equals("none", StringComparison.OrdinalIgnoreCase) || rs1.Equals("none", StringComparison.OrdinalIgnoreCase))
                {
                    canHurt = !canHurt;
                }

                if (instance.synonyms.Contains(rs0)) rs0 = Any;
                if (instance.synonyms.Contains(rs1)) rs1 = Any;

                key = rs0 + "->" + rs1;
                hurt = canHurt;
                return true;
            }

            public override int GetHashCode() => key.GetHashCode();

            public override bool Equals(object obj)
            {
                if (obj == null) return false;
                if (obj == this) return true;
                if (obj is Rule obj2)
                    return key.Equals(obj2.key);
                return false;
            }
        }

        private readonly Regex regex = new(@"\s+", RegexOptions.Compiled);

        private readonly HashSet<string> synonyms = new(StringComparer.OrdinalIgnoreCase)
        {
            "anything", "nothing", "all", "any", "none", "everything"
        };

        // container for mapping entities
        private class EntityGroup
        {
            public string name;

            internal readonly HashSet<string> _memberSet;
            internal readonly HashSet<string> _exclusionSet;

            private string _cachedMembersString = string.Empty;
            private string _cachedExclusionsString = string.Empty;

            private bool _isMembersDirty = true;
            private bool _isExclusionsDirty = true;

            public EntityGroup()
            {
                _memberSet = new(StringComparer.OrdinalIgnoreCase);
                _exclusionSet = new(StringComparer.OrdinalIgnoreCase);
            }

            public EntityGroup(string name) : this()
            {
                this.name = name;
            }

            public string members
            {
                get
                {
                    if (_isMembersDirty)
                    {
                        _cachedMembersString = string.Join(", ", _memberSet);
                        _isMembersDirty = false;
                    }
                    return _cachedMembersString;
                }
                set
                {
                    _memberSet.Clear();
                    if (!string.IsNullOrEmpty(value))
                    {
                        var members = value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var member in members)
                        {
                            var trimmed = member.Trim();
                            if (!string.IsNullOrEmpty(trimmed))
                            {
                                _memberSet.Add(trimmed);
                            }
                        }
                    }

                    _isMembersDirty = true;
                }
            }

            public string exclusions
            {
                get
                {
                    if (_isExclusionsDirty)
                    {
                        _cachedExclusionsString = string.Join(", ", _exclusionSet);
                        _isExclusionsDirty = false;
                    }
                    return _cachedExclusionsString;
                }
                set
                {
                    _exclusionSet.Clear();
                    if (!string.IsNullOrEmpty(value))
                    {
                        var exclusions = value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var exclusion in exclusions)
                        {
                            var trimmed = exclusion.Trim();
                            if (!string.IsNullOrEmpty(trimmed))
                            {
                                _exclusionSet.Add(trimmed);
                            }
                        }
                    }

                    _isExclusionsDirty = true;
                }
            }

            public bool IsMember(string value)
            {
                if (string.IsNullOrEmpty(value)) return false;
                return _memberSet.Contains(value);
            }

            public bool IsExclusion(string value)
            {
                if (string.IsNullOrEmpty(value)) return false;
                return _exclusionSet.Contains(value);
            }

            public bool Contains(string typeName, string prefabName)
            {
                return (_memberSet.Contains(typeName) || _memberSet.Contains(prefabName)) && !(_exclusionSet.Contains(typeName) || _exclusionSet.Contains(prefabName));
            }
        }

        // scheduler
        private class Schedule
        {
            public bool enabled;
            public bool useRealtime;
            public bool broadcast;
            public List<string> entries = new();
            internal List<ScheduleEntry> parsedEntries = new();
            internal bool valid;

            public void Init(TruePVE instance)
            {
                // Add entries to parsedEntries
                foreach (string str in entries)
                {
                    parsedEntries.Add(new(instance, str));
                }

                // Check if parsedEntries is null or empty
                if (parsedEntries == null || parsedEntries.Count == 0)
                {
                    enabled = false;
                    return;
                }

                // Count valid entries
                int validEntriesCount = 0;
                foreach (var entry in parsedEntries)
                {
                    if (entry.valid)
                    {
                        validEntriesCount++;
                    }
                }

                // If there are less than 2 valid entries, disable the schedule
                if (validEntriesCount < 2)
                {
                    enabled = false;
                    return;
                }

                // Collect all distinct ruleSets
                using var distinctRuleSets = Pool.Get<PooledHashSet<string>>();
                foreach (var entry in parsedEntries)
                {
                    distinctRuleSets.Add(entry.ruleSet);
                }

                // If there are less than 2 distinct ruleSets, disable the schedule
                if (distinctRuleSets.Count < 2)
                {
                    enabled = false;
                }
                else
                {
                    valid = true;
                }
            }

            // returns delta between current time and next schedule entry
            public void ClockUpdate(out string ruleSetName, out string message)
            {
                // Determine the current TimeSpan based on useRealtime
                TimeSpan currentTime = default;
                if (useRealtime || TOD_Sky.Instance?.Cycle == null)
                {
                    // Create a TimeSpan representing the total number of days since Sunday
                    currentTime = new TimeSpan((int)DateTime.Now.DayOfWeek, 0, 0, 0).Add(DateTime.Now.TimeOfDay);
                }
                else
                {
                    currentTime = TOD_Sky.Instance.Cycle.DateTime.TimeOfDay;
                }

                ScheduleEntry se = null;

                // Step 1: Check for non-daily entries
                bool hasNonDaily = false;
                foreach (var entry in parsedEntries)
                {
                    if (!entry.isDaily)
                    {
                        hasNonDaily = true;
                        break; // Early exit once a non-daily entry is found
                    }
                }

                // Step 2: Find the most recent valid non-daily entry <= currentTime
                if (hasNonDaily)
                {
                    TimeSpan? maxTime = null;
                    foreach (var entry in parsedEntries)
                    {
                        if (!entry.valid || entry.isDaily) continue; // only non-daily here

                        if (entry.time <= currentTime)
                        {
                            if (!maxTime.HasValue || entry.time > maxTime.Value)
                                maxTime = entry.time;
                        }
                    }

                    if (maxTime.HasValue)
                    {
                        foreach (var entry in parsedEntries)
                        {
                            if (entry.valid && !entry.isDaily && entry.time == maxTime.Value)
                            {
                                se = entry;
                                break; // Exit once the first matching entry is found
                            }
                        }
                    }
                    else
                    {
                        // No non-daily entry in the current week segment (e.g., it's early Sunday).
                        // Fall back to the latest non-daily entry overall (previous week's last).
                        TimeSpan latest = TimeSpan.MinValue;
                        ScheduleEntry latestEntry = null;
                        foreach (var entry in parsedEntries)
                        {
                            if (!entry.valid || entry.isDaily) continue;
                            if (entry.time > latest)
                            {
                                latest = entry.time;
                                latestEntry = entry;
                            }
                        }
                        if (latestEntry != null)
                            se = latestEntry;
                    }
                }

                // Step 3: Handle daily entries if useRealtime is true
                if (useRealtime)
                {
                    ScheduleEntry daily = null;
                    TimeSpan maxDailyTime = TimeSpan.Zero;
                    bool hasValidDaily = false;

                    // Find the maximum time among valid daily entries <= current real-time
                    foreach (var entry in parsedEntries)
                    {
                        if (entry.valid && entry.isDaily && entry.time <= DateTime.Now.TimeOfDay)
                        {
                            if (!hasValidDaily || entry.time > maxDailyTime)
                            {
                                maxDailyTime = entry.time;
                                hasValidDaily = true;
                            }
                        }
                    }

                    if (hasValidDaily)
                    {
                        foreach (var entry in parsedEntries)
                        {
                            if (entry.valid && entry.isDaily && entry.time == maxDailyTime)
                            {
                                daily = entry;
                                break;
                            }
                        }
                    }
                    else
                    {
                        // No daily entry earlier today -> use the last daily of the day
                        TimeSpan lastTime = TimeSpan.Zero;
                        ScheduleEntry lastEntry = null;
                        foreach (var entry in parsedEntries)
                        {
                            if (!entry.valid || !entry.isDaily) continue;
                            if (lastEntry == null || entry.time > lastTime)
                            {
                                lastTime = entry.time;
                                lastEntry = entry;
                            }
                        }
                        daily = lastEntry;
                    }

                    if (daily != null)
                    {
                        // Compare on the same "week clock" axis
                        // Create a TimeSpan representing the day's offset
                        TimeSpan dayOffset = new((int)DateTime.Now.DayOfWeek, 0, 0, 0);
                        TimeSpan dailyAdjustedTime = daily.time.Add(dayOffset);

                        if (se == null || dailyAdjustedTime > se.time)
                        {
                            se = daily;
                        }
                    }
                }

                // Assign the output parameters
                ruleSetName = se?.ruleSet;
                message = se?.message;
            }
        }

        private class ScheduleEntry
        {
            public string ruleSet;
            public string message;
            public string scheduleText;
            public bool valid;
            public TimeSpan time;
            internal bool isDaily = false;

            public ScheduleEntry() { }

            public ScheduleEntry(TruePVE instance, string scheduleText)
            {
                this.scheduleText = scheduleText;
                valid = Translate(instance);
            }

            private bool Translate(TruePVE instance)
            {
                if (string.IsNullOrWhiteSpace(scheduleText))
                    return false;

                // Split the scheduleText into at most 3 parts: TimeSpan, RuleSet, Message
                string[] split = instance.regex.Split(scheduleText.Trim(), 3);
                if (split.Length < 2)
                {
                    return false; // At least TimeSpan and RuleSet are required
                }

                string ts = split[0];
                string rs = split[1];
                string msg = split.Length > 2 ? split[2] : string.Empty;

                // Check if the TimeSpan starts with "*." indicating a daily schedule
                if (ts.Length > 2 && ts.StartsWith("*.", StringComparison.Ordinal))
                {
                    isDaily = true;
                    ts = ts[2..]; // Remove the "*." prefix
                }

                if (!TimeSpan.TryParse(ts, out TimeSpan span))
                {
                    string c = ts[^1].ToString();
                    if (!c.IsNumeric())
                    {
                        Puts("Invalid last character '{0}' in time format '{1}'", c, ts);
                    }
                    else
                    {
                        Puts("Time format is invalid: {0}", ts);
                    }
                    return false;
                }

                time = span;
                ruleSet = rs;
                message = msg;

                return true;
            }

            public override int GetHashCode() => ruleSet != null ? StringComparer.OrdinalIgnoreCase.GetHashCode(ruleSet) : 0;

            public override bool Equals(object obj)
            {
                if (obj is ScheduleEntry other)
                    return string.Equals(ruleSet, other.ruleSet, StringComparison.OrdinalIgnoreCase);
                return false;
            }
        }

#endregion

        #region Lang
        // load default messages to Lang
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new()
            {
                {"Prefix", "<color=#FFA500>[ TruePVE ]</color>" },
                {"Enable", "TruePVE enable set to {0}" },
                {"Twig", "<color=#ff0000>WARNING:</color> It is against server rules to destroy other players' items. Actions logged for admin review." },

                {"Header_Usage", "---- TruePVE usage ----"},
                {"Cmd_Usage_def", "Loads default configuration and data"},
                {"Cmd_Usage_sched", "Enable or disable the schedule" },
                {"Cmd_Usage_prod", "Show the prefab name and type of the entity being looked at"},
                {"Cmd_Usage_map", "Create/remove a mapping entry" },
                {"Cmd_Usage_trace", "Toggle tracing on/off" },

                {"Warning_PveMode", "ConVar server.pve is TRUE!  TruePVE is designed for PVP mode, and may cause unexpected behavior in PVE mode."},
                {"Warning_NoRuleSet", "No RuleSet found for \"{0}\"" },
                {"Warning_DuplicateRuleSet", "Multiple RuleSets found for \"{0}\"" },

                {"Error_InvalidCommand", "Invalid command" },
                {"Error_InvalidParameter", "Invalid parameter: {0}"},
                {"Error_InvalidParamForCmd", "Invalid parameters for command \"{0}\""},
                {"Error_InvalidMapping", "Invalid mapping: {0} => {1}; Target must be a valid RuleSet or \"exclude\"" },
                {"Error_NoMappingToDelete", "Cannot delete mapping: \"{0}\" does not exist" },
                {"Error_NoPermission", "Cannot execute command: No permission"},
                {"Error_NoSuicide", "You are not allowed to commit suicide"},
                {"Error_NoEntityFound", "No entity found"},

                {"Notify_AvailOptions", "Available Options: {0}"},
                {"Notify_DefConfigLoad", "Loaded default configuration"},
                {"Notify_DefDataLoad", "Loaded default mapping data"},
                {"Notify_ProdResult", "Prod results: type={0}, prefab={1}"},
                {"Notify_SchedSetEnabled", "Schedule enabled" },
                {"Notify_SchedSetDisabled", "Schedule disabled" },
                {"Notify_InvalidSchedule", "Schedule is not valid" },
                {"Notify_MappingCreated", "Mapping created for \"{0}\" => \"{1}\"" },
                {"Notify_MappingUpdated", "Mapping for \"{0}\" changed from \"{1}\" to \"{2}\"" },
                {"Notify_MappingDeleted", "Mapping for \"{0}\" => \"{1}\" deleted" },
                {"Notify_TraceToggle", "Trace mode toggled {0}" },

                {"Format_EnableColor", "#00FFFF"}, // cyan
                {"Format_EnableSize", "12"},
                {"Format_NotifyColor", "#00FFFF"}, // cyan
                {"Format_NotifySize", "12"},
                {"Format_HeaderColor", "#FFA500"}, // orange
                {"Format_HeaderSize", "14"},
                {"Format_ErrorColor", "#FF0000"}, // red
                {"Format_ErrorSize", "12"},

                {"Error_TimeLeft", "You must wait another {0} hours to attack this player."},
                {"Error_CannotAccessEntity", "You are not allowed to access this" },
            }, this);
        }

        // get message from Lang
        private string GetMessage(string key, string userId = null) => lang.GetMessage(key, this, userId);
        #endregion
    }
}