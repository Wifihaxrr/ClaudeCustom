/*
 * Copyright (c) 2025 Bazz3l
 * 
 * Guarded Crate cannot be copied, edited and/or (re)distributed without the express permission of Bazz3l.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 * 
 */

using System.Collections.Generic;
using System.Globalization;
using System.Collections;
using System.Text;
using System.Linq;
using System;
using System.Diagnostics;
using Oxide.Core.Plugins;
using Oxide.Core;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using GuardedCrateExt;
using UnityEngine;
using Rust;

namespace Oxide.Plugins
{
    [Info("Guarded Crate", "Bazz3l", "2.0.13")]
    [Description("Spawn custom locked crate events guarded by scientists, eliminate the threats to gain high value loot before the time runs out.")]
    internal class GuardedCrate : RustPlugin
    {
        [PluginReference] private Plugin NpcSpawn, Clans, ZoneManager, HackableLock, GUIAnnouncements = null;

        #region Fields

        private const string PERM_USE = "guardedcrate.use";
        private const string MARKER_PREFAB = "assets/prefabs/tools/map/genericradiusmarker.prefab";
        private const string CRATE_PREFAB = "assets/prefabs/deployable/chinooklockedcrate/codelockedhackablecrate.prefab";
        private const string PLANE_PREFAB = "assets/prefabs/npc/cargo plane/cargo_plane.prefab";
        
        private readonly object _objFalse = false;
        private readonly object _objTrue = true;
        private StoredData _storedData;
        private static ConfigData _configData;
        private static GuardedCrate _plugin;
        private static Func<string, string, string> _getMessage = null;
        private static Func<ulong, string> _tryFetchTag = null;
        private static Func<Vector3, JObject, ScientistNPC> _trySpawnNpc = null;
        private static Func<BasePlayer, HackableLockedCrate, object> _tryLockHackable = null;
        private static Func<string, string, string, BasePlayer, float, object> _sendGuiNotification = null;

        #endregion

        #region Config

        protected override void LoadDefaultConfig()
        {
            _configData = ConfigData.DefaultConfig();
            _configData.Version = Version;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                _configData = Config.ReadObject<ConfigData>();
                if (_configData == null) throw new JsonException();

                bool hasChanged = false;

                if (string.IsNullOrEmpty(_configData.CommandName))
                {
                    _configData.CommandName = "gcrate";
                    _configData.Version = Version;
                    hasChanged = true;
                }

                if (_configData.AutoStartDuration <= 0.0f)
                {
                    _configData.AutoStartDuration = 1800f;
                    _configData.Version = Version;
                    hasChanged = true;
                }

                if (_configData.MessageSettings == null)
                {
                    _configData.MessageSettings = ConfigData.GetDefaultMessageSettings();
                    _configData.Version = Version;
                    hasChanged = true;
                }

                if (_configData.ZoneManagerSettings == null)
                {
                    _configData.ZoneManagerSettings = ConfigData.GetDefaultZoneManagerSettings();
                    _configData.Version = Version;
                    hasChanged = true;
                }

                if (_configData.BlockedTopologies == null)
                {
                    _configData.BlockedTopologies = ConfigData.GetDefaultTopologies();
                    _configData.Version = Version;
                    hasChanged = true;
                }

                if (hasChanged)
                {
                    SaveConfig();
                    PrintWarning("Config has been updated.");
                }
            }
            catch (Exception e)
            {
                LoadDefaultConfig();
                PrintWarning("Loaded default config.");
            }
        }

        protected override void SaveConfig() => Config.WriteObject(_configData, true);

        private class ConfigData
        {
            [JsonProperty("CommandName: used to manage and setup events.")]
            public string CommandName;
            [JsonProperty("EnableAutoStart: enabling auto start will spawn events at a specified duration.")]
            public bool AutoStartEnabled;
            [JsonProperty("AutoStartDuration: auto start duration specify in seconds.")]
            public float AutoStartDuration;
            [JsonProperty("MessageSettings: message notification settings")]
            public MessageSettings MessageSettings;
            [JsonProperty("ZoneManagerSettings: zone manager settings")]
            public ZoneMangerSettings ZoneManagerSettings;
            [JsonConverter(typeof(EnumCollectionStringConverter<TerrainBiome.Enum>))]
            [JsonProperty("BlockedTopologies: specifying topologies will prevent events spawning in these areas")]
            public List<TerrainBiome.Enum> BlockedTopologies;
            [JsonProperty("VersionNumber: current version of the plugin, do not change this!!!")]
            public VersionNumber? Version;

            public static ConfigData DefaultConfig()
            {
                return new ConfigData
                {
                    CommandName = "gcrate",
                    AutoStartEnabled = true,
                    AutoStartDuration = 3600,
                    MessageSettings = ConfigData.GetDefaultMessageSettings(),
                    ZoneManagerSettings = ConfigData.GetDefaultZoneManagerSettings(),
                    BlockedTopologies = ConfigData.GetDefaultTopologies(),
                };
            }

            public static MessageSettings GetDefaultMessageSettings()
            {
                return new MessageSettings
                {
                    EnableToast = false,
                    EnableChat = true,
                    EnableChatPrefix = true,
                    ChatIcon = 76561199542824781,
                    EnableGuiAnnouncements = false,
                    GuiAnnouncementsBgColor = "Purple",
                    GuiAnnouncementsTextColor = "White"
                };
            }

            public static ZoneMangerSettings GetDefaultZoneManagerSettings()
            {
                return new ZoneMangerSettings
                {
                    EnabledIgnoredZones = false,
                    IgnoredZones = new List<string>()
                };
            }

            public static List<TerrainBiome.Enum> GetDefaultTopologies()
            {
                return new List<TerrainBiome.Enum>
                {
                    TerrainBiome.Enum.Arctic,
                    TerrainBiome.Enum.Jungle
                };
            }
        }

        private class MessageSettings
        {
            [JsonProperty("enable toast message")] 
            public bool EnableToast;
            [JsonProperty("enable chat message")] 
            public bool EnableChat;
            [JsonProperty("enable chat prefix")] 
            public bool EnableChatPrefix;
            [JsonProperty("custom chat message icon (steam64)")]
            public ulong ChatIcon;
            [JsonProperty("enable gui announcements plugin from umod.org")]
            public bool EnableGuiAnnouncements;
            [JsonProperty("gui announcements text color")]
            public string GuiAnnouncementsTextColor;
            [JsonProperty("gui announcements background color")]
            public string GuiAnnouncementsBgColor;
        }

        private class ZoneMangerSettings
        {
            [JsonProperty("enable ignored zones")] 
            public bool EnabledIgnoredZones;
            [JsonProperty("ignore these zone ids, leave empty to exclude all zones")]
            public List<string> IgnoredZones;
        }

        #endregion

        #region Storage

        private static (string Name, float HackSeconds, string MarkerColor, int Guards, string Weapon, float Health)[] DefaultEvents()
        {
            return new (string Name, float HackSeconds, string MarkerColor, int Guards, string Weapon, float Health)[]
            {
                ("Easy", 60f, "#32A844", 8, "smg.mp5", 200f),
                ("Medium", 120f, "#EDDF45", 10, "smg.mp5", 250f),
                ("Hard", 180f, "#3060D9", 12, "rifle.ak", 300f),
                ("Elite", 180f, "#3060D9", 20, "rifle.ak", 300f)
            };
        }

        private void LoadDefaultData()
        {
            _storedData = new StoredData();
            
            foreach (var defaultEvent in DefaultEvents())
            {
                _storedData.CrateEventEntries.Add(CreateDefaultEvent(
                    defaultEvent.Name, 
                    defaultEvent.HackSeconds, 
                    defaultEvent.MarkerColor, 
                    defaultEvent.Guards, 
                    defaultEvent.Weapon,
                    defaultEvent.Health)
                );
            }
            
            SaveData();
        }

        private void LoadData()
        {
            try
            {
                _storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name);
                if (_storedData is not { IsValid: true }) throw new Exception();

                SaveData();
            }
            catch
            {
                LoadDefaultData();
                PrintWarning("Loaded default data.");
            }
        }

        private void SaveData()
        {
            if (_storedData is not { IsValid: true }) return;
            Interface.Oxide.DataFileSystem.WriteObject(Name, _storedData);
        }

        private class StoredData
        {
            public List<EventEntry> CrateEventEntries = new();

            [JsonIgnore] 
            private string[] _eventNames;

            [JsonIgnore]
            public string[] EventNames
            {
                get
                {
                    return _eventNames ??= CrateEventEntries
                        .Select(x => x.EventName)
                        .ToArray();
                }
            }

            [JsonIgnore] public bool IsValid => CrateEventEntries != null && CrateEventEntries.Count > 0;

            public EventEntry FindEventByName(string eventName)
            {
                return CrateEventEntries.Find(x => x.EventName.Equals(eventName, StringComparison.OrdinalIgnoreCase));
            }
        }

        private class EventEntry
        {
            [JsonProperty("event display name)")] 
            public string EventName;
            [JsonProperty("event duration")] 
            public float EventDuration;

            [JsonProperty("enable lock to player when completing the event")]
            public bool EnableLockToPlayer;

            [JsonProperty("enable clan tag")] 
            public bool EnableClanTag;

            [JsonProperty("enable auto hacking of crate when an event is finished")]
            public bool EnableAutoHack;

            [JsonProperty("hackable locked crate")]
            public float HackableCrateHackSeconds;

            [JsonProperty("hackable crate fall drag")]
            public float HackableCrateFallDrag;

            [JsonProperty("enable marker")] 
            public bool EnableMarker;
            [JsonProperty("marker color 1")] 
            public string MapMarkerColor1;
            [JsonProperty("marker color 2")] 
            public string MapMarkerColor2;
            [JsonProperty("marker radius")] 
            public float MapMarkerRadius;
            [JsonProperty("marker opacity")] 
            public float MapMarkerOpacity;

            [JsonProperty("enable loot table")] 
            public bool EnableLootTable;
            [JsonProperty("min loot items")] 
            public int LootMinAmount;
            [JsonProperty("max loot items")]
            public int LootMaxAmount;

            [JsonProperty("enable eliminate all guards before looting")]
            public bool EnableEliminateGuards;

            [JsonProperty("guard spawn amount")] 
            public int GuardAmount;
            [JsonProperty("guard spawn config")] 
            public GuardConfig GuardConfig;

            [JsonProperty("create loot items")] 
            public List<ItemEntry> LootTable;
        }

        private class ItemEntry
        {
            public string DisplayName;
            public string Shortname;
            public ulong SkinID = 0UL;
            public int MinAmount;
            public int MaxAmount;
            
            public static List<ItemEntry> SaveItems(ItemContainer container)
            {
                List<ItemEntry> items = new List<ItemEntry>();

                foreach (Item item in container.itemList)
                {
                    items.Add(new ItemEntry
                    {
                        DisplayName = item.name,
                        Shortname = item.info.shortname,
                        SkinID = item.skin,
                        MinAmount = item.amount,
                        MaxAmount = item.amount,
                    });
                }

                return items;
            }

            public Item CreateItem()
            {
                Item item = ItemManager.CreateByName(Shortname, UnityEngine.Random.Range(MinAmount, MaxAmount), SkinID);
                item.name = DisplayName;
                item.MarkDirty();
                return item;
            }
        }

        private class GuardConfig
        {
            public string Name;
            public List<WearEntry> WearItems;
            public List<BeltEntry> BeltItems;
            public string Kit;
            public float Health;
            public float RoamRange;
            public float ChaseRange;
            public float SenseRange;
            public float AttackRangeMultiplier;
            public bool CheckVisionCone;
            public float VisionCone;
            public float DamageScale;
            public float TurretDamageScale;
            public float AimConeScale;
            public float Speed;
            public float SleepDistance;
            public float MemoryDuration;
            public bool HostileTargetsOnly;
            public bool DisableRadio;
            public bool CanRunAwayWater;
            public bool CanSleep;
            public bool CanRaid;
            public bool Stationary;
            public bool AboveOrUnderGround;
            
            [JsonIgnore]
            public string[] Kits;

            [JsonIgnore] 
            public JObject Parsed;
            
            public class BeltEntry
            {
                public string ShortName;
                public ulong SkinID;
                public int Amount;
                public string Ammo;
                public List<string> Mods;

                public static List<BeltEntry> SaveItems(ItemContainer container)
                {
                    List<BeltEntry> items = new List<BeltEntry>();

                    foreach (Item item in container.itemList)
                    {
                        BeltEntry beltEntry = new BeltEntry
                        {
                            ShortName = item.info.shortname,
                            SkinID = item.skin,
                            Amount = item.amount,
                            Mods = new List<string>()
                        };

                        if (item.GetHeldEntity() is BaseProjectile projectile && projectile?.primaryMagazine != null &&
                            projectile.primaryMagazine.ammoType != null)
                            beltEntry.Ammo = projectile.primaryMagazine.ammoType.shortname;

                        if (item?.contents?.itemList != null)
                        {
                            foreach (Item itemContent in item.contents.itemList)
                                beltEntry.Mods.Add(itemContent.info.shortname);
                        }

                        items.Add(beltEntry);
                    }

                    return items;
                }
            }

            public class WearEntry
            {
                public string ShortName;
                public ulong SkinID;

                public static List<WearEntry> SaveItems(ItemContainer container)
                {
                    List<WearEntry> items = new List<WearEntry>();

                    foreach (Item item in container.itemList)
                    {
                        items.Add(new WearEntry
                        {
                            ShortName = item.info.shortname,
                            SkinID = item.skin
                        });
                    }

                    return items;
                }
            }

            public void CacheConfig()
            {
                Kits = Kit?.Split(',') ?? Array.Empty<string>();
                
                Parsed = new JObject
                {
                    ["Name"] = Name,
                    ["WearItems"] = new JArray
                    {
                        WearItems.Select(x => new JObject
                        {
                            ["ShortName"] = x.ShortName,
                            ["SkinID"] = x.SkinID
                        })
                    },
                    ["BeltItems"] = new JArray
                    {
                        BeltItems.Select(x => new JObject
                        {
                            ["ShortName"] = x.ShortName,
                            ["Amount"] = x.Amount,
                            ["SkinID"] = x.SkinID,
                            ["Mods"] = new JArray { x.Mods },
                            ["Ammo"] = x.Ammo
                        })
                    },
                    ["Kit"] = Kit,
                    ["Health"] = Health,
                    ["RoamRange"] = RoamRange,
                    ["ChaseRange"] = ChaseRange,
                    ["SenseRange"] = SenseRange,
                    ["ListenRange"] = SenseRange / 2f,
                    ["AttackRangeMultiplier"] = AttackRangeMultiplier,
                    ["CheckVisionCone"] = CheckVisionCone,
                    ["VisionCone"] = VisionCone,
                    ["HostileTargetsOnly"] = HostileTargetsOnly,
                    ["DamageScale"] = DamageScale,
                    ["TurretDamageScale"] = TurretDamageScale,
                    ["AimConeScale"] = AimConeScale,
                    ["DisableRadio"] = DisableRadio,
                    ["CanRunAwayWater"] = CanRunAwayWater,
                    ["CanSleep"] = CanSleep,
                    ["SleepDistance"] = SleepDistance,
                    ["Speed"] = Speed,
                    ["AreaMask"] = !AboveOrUnderGround ? 1 : 25,
                    ["AgentTypeID"] = !AboveOrUnderGround ? -1372625422 : 0,
                    ["HomePosition"] = string.Empty,
                    ["MemoryDuration"] = MemoryDuration,
                    ["States"] = new JArray
                    {
                        Stationary
                            ? new HashSet<string> { "IdleState", "CombatStationaryState" }
                            : CanRaid
                                ? new HashSet<string> { "RoamState", "CombatState", "ChaseState", "RaidState" }
                                : new HashSet<string> { "RoamState", "CombatState", "ChaseState" }
                    }
                };
            }
        }

        private static EventEntry CreateDefaultEvent(string name, float hackSeconds, string markerColor, int guardAmount, string weaponShortName, float guardHealth)
        {
            return new EventEntry
            {
                EventName = name,
                EventDuration = 1800f,
                EnableAutoHack = true,
                HackableCrateHackSeconds = hackSeconds,
                HackableCrateFallDrag = 1f,
                EnableLockToPlayer = true,
                EnableClanTag = true,
                EnableMarker = true,
                MapMarkerColor1 = markerColor,
                MapMarkerColor2 = "#000000",
                MapMarkerOpacity = 0.6f,
                MapMarkerRadius = 0.7f,
                EnableLootTable = false,
                LootMinAmount = 6,
                LootMaxAmount = 10,
                LootTable = new List<ItemEntry>(),
                EnableEliminateGuards = true,
                GuardAmount = guardAmount,
                GuardConfig = CreateDefaultGuard($"{name} Guard", weaponShortName, guardHealth)
            };
        }

        private static GuardConfig CreateDefaultGuard(string name, string shortname, float health)
        {
            return new GuardConfig
            {
                Name = name,
                WearItems = new List<GuardConfig.WearEntry>
                {
                    new() { ShortName = "hazmatsuit_scientist_peacekeeper", SkinID = 0UL }
                },
                BeltItems = new List<GuardConfig.BeltEntry>
                {
                    new() { ShortName = shortname, Amount = 1, SkinID = 0UL, Mods = new List<string>() },
                    new() { ShortName = "syringe.medical", Amount = 10, SkinID = 0UL, Mods = new List<string>() }
                },
                Kit = "",
                Health = health,
                RoamRange = 5f,
                ChaseRange = 40f,
                SenseRange = 80f,
                AttackRangeMultiplier = 8f,
                VisionCone = 180f,
                CheckVisionCone = false,
                DamageScale = 1f,
                TurretDamageScale = 0.25f,
                AimConeScale = shortname == "rifle.ak" ? 0.15f : 0.25f,
                SleepDistance = 100f,
                Speed = 8.5f,
                MemoryDuration = 30f
            };
        }

        #endregion

        #region Lang

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                { LangKeys.NoPermission, "Sorry you don't have permission to do that." },
                { LangKeys.Prefix, "<color=#8a916f>Guarded Crate</color>:\n" },

                { LangKeys.FailedToStartEvent, "Failed to start event." },
                { LangKeys.StartEvent, "Event starting." },
                { LangKeys.ClearEvents, "Cleaning up all running events." },

                { LangKeys.EventStart, "Special delivery is on its way to <color=#e7cf85>{0}</color> watch out it is heavily contested by guards, severity level <color=#e7cf85>{1}</color>.\nBe fast before the event ends in <color=#e7cf85>{2}</color>." },
                { LangKeys.EventCompleted, "<color=#e7cf85>{1}</color> has cleared the event at <color=#e7cf85>{0}</color>." },
                { LangKeys.EventEnded, "Event ended at <color=#e7cf85>{0}</color>; You were not fast enough; better luck next time!" },
                { LangKeys.EventNotFound, "Event not found, please make sure you have typed the correct name." },
                { LangKeys.EventIntersecting, "Another event is intersecting this position." },
                { LangKeys.EventPositionInvalid, "Event position invalid." },
                { LangKeys.EliminateGuards, "The crate is still contested eliminate all guards to gain access to high-valued loot." },
                { LangKeys.EventUpdated, "Event updated, please reload the plugin to take effect." },
                { LangKeys.InvalidGuardAmount, "Invalid guard amount must be between {0} - {1}." },

                { LangKeys.HelpStartEvent, "<color=#e7cf85>/{0}</color> start \"<color=#e7cf85><{1}></color>\", start an event of a specified type." },
                { LangKeys.HelpStopEvent, "<color=#e7cf85>/{0}</color> stop, stop all currently running events.\n\n" },
                { LangKeys.HelpHereEvent, "<color=#e7cf85>/{0}</color> here \"<color=#e7cf85><event-name></color>\", start an event at your position\n\n" },
                { LangKeys.HelpPositionEvent, "<color=#e7cf85>/{0}</color> position \"<color=#e7cf85><event-name></color>\" \"<color=#e7cf85>x y z</color>\", start an event at a specified position.\n\n" },
                { LangKeys.HelpLootEvent, "<color=#e7cf85>/{0}</color> loot \"<color=#e7cf85><event-name></color>\", create loot items that you wish to spawn in the crate, add the items to your inventory and run the command.\n\n" },
                { LangKeys.HelpDragEvent, "<color=#e7cf85>/{0}</color> drag \"<color=#e7cf85><event-name></color>\", specify the amount of drag the crate should have while falling.\n\n" },
                { LangKeys.HelpGuardAmount, "<color=#e7cf85>/{0}</color> amount \"<color=#e7cf85><event-name></color>\", specify the guard amount to spawn.\n\n" },
                { LangKeys.HelpGuardLoadout, "<color=#e7cf85>/{0}</color> loadout \"<color=#e7cf85><event-name></color>\", set guard loadout using items in your inventory." }
            }, this);
        }

        private static class LangKeys
        {
            public const string Prefix = "Prefix";
            public const string NoPermission = "NoPermission";

            public const string FailedToStartEvent = "FailedToStartEvent";
            public const string ClearEvents = "ClearEvents";
            public const string StartEvent = "StartEvent";

            public const string EventCompleted = "EventCompleted";
            public const string EventStart = "EventStart";
            public const string EventEnded = "EventClear";
            public const string EventNotFound = "EventNotFound";
            public const string EventPositionInvalid = "EventPosInvalid";
            public const string EventIntersecting = "EventNearby";
            public const string EventUpdated = "EventUpdated";
            public const string EliminateGuards = "EliminateGuards";
            public const string InvalidGuardAmount = "InvalidGuardAmount";

            public const string HelpStartEvent = "HelpStartEvent";
            public const string HelpStopEvent = "HelpStopEvent";
            public const string HelpHereEvent = "HelpHereEvent";
            public const string HelpPositionEvent = "HelpPositionEvent";
            public const string HelpLootEvent = "HelpLootEvent";
            public const string HelpDragEvent = "HelpDragEvent";
            public const string HelpGuardAmount = "HelpGuardAmount";
            public const string HelpGuardLoadout = "HelpGuardLoadout";
        }

        #endregion

        #region Oxide Hooks

        private void OnServerInitialized()
        {
            SpawnManager.Instance.FindSpawnPoints();
            
            if (!string.IsNullOrEmpty(_configData.CommandName))
            {
                cmd.AddConsoleCommand(_configData.CommandName, this, nameof(EventConsoleCommands));
                cmd.AddChatCommand(_configData.CommandName, this, nameof(EventCommandCommands));
            }

            if (_configData.AutoStartEnabled && _configData.AutoStartDuration > 0)
                timer.Every(_configData.AutoStartDuration, () => TryStartEvent(null));
        }

        private void Init()
        {
            permission.RegisterPermission(PERM_USE, this);
            
            _plugin = this;
            _getMessage = (key, userId) => lang.GetMessage(key, this, userId);
            _tryFetchTag = TryFetchTag;
            _trySpawnNpc = TrySpawnNpc;
            _tryLockHackable = TryLockHackable;
            _sendGuiNotification = SendGuiNotification;
            
            LoadData();
            
            SpawnManager.Initialize(_configData.BlockedTopologies);
        }

        private void Unload()
        {
            try
            {
                EventManager.OnUnload();
                SpawnManager.OnUnload();
                EntityCache.OnUnload();
            }
            finally
            {
                _plugin = null;
                _configData = null;
            }
        }

        private void OnEntityDeath(ScientistNPC scientist, HitInfo info)
        {
            NetworkableId id = scientist.net?.ID ?? default;
            EntityCache.FindEntityEventInstance(id)?.OnGuardKilled(scientist, info?.InitiatorPlayer);
            EntityCache.RemoveEntity(id);
        }
        
        private void OnEntityKill(ScientistNPC scientist)
        {
            NetworkableId id = scientist.net?.ID ?? default;
            EntityCache.FindEntityEventInstance(id)?.OnGuardKilled(scientist, null);
            EntityCache.RemoveEntity(id);
        }

        private void OnEntityKill(LootContainer container)
        {
            NetworkableId id = container.net?.ID ?? default;
            EntityCache.FindEntityEventInstance(id)?.OnCrateKilled(container);
            EntityCache.RemoveEntity(id);
        }

        private void OnEntityKill(CargoPlane plane)
        {
            NetworkableId id = plane.net?.ID ?? default;
            EntityCache.FindEntityEventInstance(id)?.OnPlaneKilled(plane);
            EntityCache.RemoveEntity(id);
        }

        private object CanHackCrate(BasePlayer player, HackableLockedCrate crate)
        {
            NetworkableId id = crate.net?.ID ?? default;
            return EntityCache.FindEntityEventInstance(id)?.CanHackCrate(player);
        }

        #endregion

        #region Spawn Manager

        private class SpawnManager : IDisposable
        {
            public static SpawnManager Instance { get; private set; }
            
            private const int BLOCKED_TERRAIN_TOPOLOGIES = (int)(TerrainTopology.Enum.Cliff | TerrainTopology.Enum.Cliffside | TerrainTopology.Enum.Beach | TerrainTopology.Enum.Beachside | TerrainTopology.Enum.Ocean | TerrainTopology.Enum.Oceanside | TerrainTopology.Enum.Monument | TerrainTopology.Enum.Building | TerrainTopology.Enum.River | TerrainTopology.Enum.Riverside | TerrainTopology.Enum.Lake | TerrainTopology.Enum.Lakeside);
            private readonly int _blockedLayerMasks = LayerMask.GetMask("Tree", "Construction", "Prevent_Building", "Vehicle_World", "Vehicle_Large", "Vehicle_Detailed");
            private readonly List<ZoneInfo> _ignoreZones = new();
            private readonly Queue<Vector3> _spawnPoints = new();            
            private readonly Stopwatch _stopwatch = new();
            private TerrainBiome.Enum _blockedTerrainBiomes = 0;
            private bool _hasBlockedTerrainBiomes;
            private Coroutine _routine;
            private int _spawnPointAttempts;
            private int _spawnPointCount;
            
            public static void Initialize(List<TerrainBiome.Enum> blockedTerrainBiomes)
            {
                Instance = new SpawnManager();
                Instance._hasBlockedTerrainBiomes = blockedTerrainBiomes.Count > 0;
                
                foreach (TerrainBiome.Enum blockedTopology in blockedTerrainBiomes)
                    Instance._blockedTerrainBiomes |= blockedTopology;
                
                Instance.GetIgnoredZones();
            }

            public static void OnUnload()
            {
                Instance.Dispose();
                Instance = null;
            }

            public void Dispose()
            {
                CleanupSpawnsRoutine();
                
                _ignoreZones.Clear();
                _spawnPoints.Clear();
            }

            private IEnumerator GenerateSpawnsRoutine(int maxSpawnPoints = 100, int maxSpawnAttempts = 5000, float frameBudget = 0.10f)
            {
                yield return CoroutineEx.waitForEndOfFrame;
                
                Interface.Oxide.LogDebug("GuardedCrate: generating spawn points.");
                
                float mapSizeX = TerrainMeta.Size.x / 2;
                float mapSizeZ = TerrainMeta.Size.z / 2;
                Vector3 spawnPoint = Vector3.zero;
                
                _spawnPointAttempts = 0;
                _spawnPointCount = 0;
                
                while (_spawnPointCount < maxSpawnPoints)
                {
                    _stopwatch.Reset();
                    _stopwatch.Start();
                    
                    for (int index = _spawnPointCount; index < maxSpawnPoints; ++index)
                    {
                        spawnPoint.x = UnityEngine.Random.Range(-mapSizeX, mapSizeX);
                        spawnPoint.z = UnityEngine.Random.Range(-mapSizeZ, mapSizeZ);
                        spawnPoint.y = WaterLevel.GetWaterOrTerrainSurface(spawnPoint, false, false);
                        
                        ++_spawnPointAttempts;
                        
                        if (TestSpawnPoint(ref spawnPoint))
                        {
                            _spawnPoints.Enqueue(spawnPoint);
                            
                            ++_spawnPointCount;
                        }

                        if (_stopwatch.Elapsed.TotalMilliseconds >= frameBudget)
                            break;
                    }
                    
                    if (_spawnPointAttempts >= maxSpawnAttempts)
                        break;
                    
                    yield return CoroutineEx.waitForEndOfFrame;
                    yield return CoroutineEx.waitForEndOfFrame;
                }
                
                _stopwatch.Stop();

                Interface.Oxide.LogDebug("GuardedCrate: successfully found {0} spawn points.", _spawnPoints.Count);
                
                _routine = null;
            }

            private void CleanupSpawnsRoutine()
            {
                if (_routine == null) 
                    return;
                
                ServerMgr.Instance.StopCoroutine(_routine);
                _routine = null;
            }
            
            public void FindSpawnPoints()
            {
                if (_routine != null)
                    return;

                _routine = ServerMgr.Instance.StartCoroutine(GenerateSpawnsRoutine());
            }

            public Vector3 GetSpawnPoint()
            {
                Vector3 spawnPoint = Vector3.zero;

                while (_spawnPoints.Count > 0)
                {
                    spawnPoint = _spawnPoints.Dequeue();
                    if (IsSpawnPointValid(spawnPoint))
                        break;
                }

                if (_routine == null && _spawnPoints.Count <= 2)
                {
                    FindSpawnPoints();
                    spawnPoint = Vector3.zero;
                }

                return spawnPoint;
            }

            private void GetIgnoredZones()
            {
                if (!_configData.ZoneManagerSettings.EnabledIgnoredZones)
                    return;
                
                if (_plugin?.ZoneManager == null)
                    return;

                ZoneMangerSettings zoneMangerSettings = _configData?.ZoneManagerSettings;
                if (!(zoneMangerSettings?.IgnoredZones?.Count > 0))
                    return;
                
                if (_plugin.ZoneManager.Call("GetZoneIDs") is not string[] zoneIds || zoneIds.Length == 0)
                    return;
                
                foreach (string zoneId in zoneIds)
                {
                    if (!zoneMangerSettings.IgnoredZones.Contains(zoneId)) 
                        continue;
                    
                    if (_plugin.ZoneManager.Call("GetZoneLocation", zoneId) is not Vector3 position) 
                        continue;
                    
                    if (_plugin.ZoneManager.Call("GetZoneRadius", zoneId) is not float radius)
                        continue;
                    
                    _ignoreZones.Add(new ZoneInfo(position, radius));
                }
            }

            private bool TestSpawnPoint(ref Vector3 spawnPoint)
            {
                if (!Physics.Raycast(spawnPoint.WithY(spawnPoint.y + 100f), Vector3.down, out RaycastHit hit, 100f, 1218652417, QueryTriggerInteraction.Ignore))
                    return false;

                spawnPoint.y = hit.point.y;

                if (AntiHack.TestInsideTerrain(spawnPoint)) 
                    return false;
                
                if (AntiHack.IsInsideMesh(spawnPoint)) 
                    return false;
                
                if (IsInterceptingPosition(spawnPoint)) 
                    return false;
                
                if (IsInterceptingIgnored(spawnPoint)) 
                    return false;
                
                if (IsBlockedTerrainTopology(spawnPoint)) 
                    return false;
                
                if (IsBlockedTerrainBiome(spawnPoint)) 
                    return false;

                return IsSpawnPointValid(spawnPoint);
            }

            private bool IsSpawnPointValid(Vector3 spawnPoint)
            {
                if (WaterLevel.Test(spawnPoint, false, false))
                    return false;
                
                if (!IsValidCollider(spawnPoint))
                    return false;
                
                if (HasPlayersNearby(spawnPoint)) 
                    return false;
                
                return !HasBuildingNearby(spawnPoint);
            }

            private bool IsBlockedTerrainTopology(Vector3 spawnPoint)
            {
                return (TerrainMeta.TopologyMap.GetTopology(spawnPoint) & (int)BLOCKED_TERRAIN_TOPOLOGIES) != 0;
            }

            private bool IsBlockedTerrainBiome(Vector3 spawnPoint)
            {
                return _hasBlockedTerrainBiomes && TerrainMeta.BiomeMap.GetBiome(spawnPoint, (int)_blockedTerrainBiomes) != 0.0f;
            }

            private bool IsInterceptingIgnored(Vector3 spawnPoint)
            {
                foreach (ZoneInfo zoneInfo in _ignoreZones)
                {
                    if (zoneInfo.IsInBounds(spawnPoint))
                        return true;
                }

                return false;
            }

            private bool IsInterceptingPosition(Vector3 spawnPoint)
            {
                const float distance = 100f;
                
                foreach (Vector3 pos in _spawnPoints)
                {
                    if (Vector3.Distance(pos, spawnPoint) <= distance)
                        return true;
                }

                return false;
            }

            private bool IsValidCollider(Vector3 spawnPoint)
            {
                List<Collider> list = Facepunch.Pool.Get<List<Collider>>();
                bool isValid = true;

                try
                {
                    Vis.Colliders<Collider>(spawnPoint, 5f, list);

                    foreach (Collider collider in list)
                    {
                        if ((1 << collider.gameObject.layer & _blockedLayerMasks) > 0)
                        {
                            isValid = false;
                            break;
                        }

                        if (collider.name.Contains("radiation", CompareOptions.IgnoreCase) ||
                            collider.name.Contains("rock", CompareOptions.IgnoreCase) ||
                            collider.name.Contains("cliff", CompareOptions.IgnoreCase) ||
                            collider.name.Contains("road", CompareOptions.IgnoreCase) || 
                            collider.name.Contains("train", CompareOptions.IgnoreCase))
                        {
                            isValid = false;
                            break;
                        }

                        if (collider.name.Contains("fireball", CompareOptions.IgnoreCase) ||
                            collider.name.Contains("iceberg", CompareOptions.IgnoreCase) ||
                            collider.name.Contains("ice_sheet", CompareOptions.IgnoreCase))
                        {
                            isValid = false;
                            break;
                        }

                        if (collider.HasComponent<TriggerSafeZone>())
                        {
                            isValid = false;
                            break;
                        }
                    }
                }
                catch (Exception e)
                {
                    //
                }

                Facepunch.Pool.FreeUnmanaged<Collider>(ref list);
                return isValid;
            }

            private static bool HasPlayersNearby(Vector3 spawnPoint, float radius = 100f)
            {
                List<BasePlayer> list = Facepunch.Pool.Get<List<BasePlayer>>();
                bool isValid = false;

                try
                {
                    Vis.Entities(spawnPoint, radius, list, Layers.Mask.Player_Server);

                    foreach (BasePlayer player in list)
                    {
                        if (player.userID.IsSteamId() && !player.IsSleeping())
                        {
                            isValid = true;
                            break;
                        }
                    }
                }
                catch (Exception e)
                {
                    //
                }

                Facepunch.Pool.FreeUnmanaged<BasePlayer>(ref list);
                return isValid;
            }

            private static bool HasBuildingNearby(Vector3 spawnPoint, float radius = 100f)
            {
                List<BasePlayer> list = Facepunch.Pool.Get<List<BasePlayer>>();
                bool isValid = true;

                try
                {
                    Vis.Entities(spawnPoint, radius, list, Layers.Mask.Prevent_Building);
                    
                    isValid = list.Count > 0;
                }
                catch (Exception e)
                {
                    //
                }

                Facepunch.Pool.FreeUnmanaged<BasePlayer>(ref list);
                return isValid;
            }
        }

        private struct ZoneInfo
        {
            public Vector3 Position;
            public float RadiusSquared;

            public ZoneInfo(Vector3 position, float radius)
            {
                Position = position;
                RadiusSquared = radius;
            }

            public bool IsInBounds(Vector3 position)
            {
                float dx = position.x - Position.x;
                float dy = position.y - Position.y;
                return (dx * dx + dy * dy) <= RadiusSquared;
            }
        }

        #endregion

        #region Event Manager

        private static class EventManager
        {
            private static readonly List<GuardedCrateInstance> GuardedCrateInstances = new();

            public static void OnUnload() => CleanupInstances();

            public static bool HasIntersectingEvent(Vector3 position)
            {
                for (int i = 0; i < GuardedCrateInstances.Count; i++)
                {
                    GuardedCrateInstance eventInstance = GuardedCrateInstances[i];
                    if (eventInstance != null && Vector3Ex.Distance2D(eventInstance.transform.position, position) < 80f)
                        return true;
                }

                return false;
            }

            public static void CleanupInstances()
            {
                for (int i = GuardedCrateInstances.Count - 1; i >= 0; i--)
                    GuardedCrateInstances[i]?.EventEnded();

                GuardedCrateInstances.Clear();
            }

            public static void RegisterInstance(GuardedCrateInstance eventInstance)
            {
                GuardedCrateInstances.Add(eventInstance);
                _plugin?.SubscribeToHooks(GuardedCrateInstances.Count);
            }

            public static void UnregisterInstance(GuardedCrateInstance eventInstance)
            {
                GuardedCrateInstances.Remove(eventInstance);
                _plugin?.SubscribeToHooks(GuardedCrateInstances.Count);
            }
        }

        #endregion

        #region Event
        
        private enum EventState { Initialize, Running, Ended, Failed, Complete, }

        private bool TryStartEvent(string eventName = null)
        {
            if (!NpcSpawn.IsPluginReady())
            {
                PrintWarning("Failed to find NpcSpawn plugin, please download from https://codefling.com/extensions/npc-spawn");
                Interface.Oxide.UnloadPlugin(Name);
                return false;
            }
            
            Vector3 position = SpawnManager.Instance.GetSpawnPoint();
            if (position == Vector3.zero)
            {
                PrintWarning("Failed to find a valid spawn point.");
                return false;
            }

            EventEntry eventEntry = _storedData.FindEventByName(eventName) ?? _storedData.CrateEventEntries.GetRandom();
            if (eventEntry == null)
            {
                PrintWarning("Failed to find a valid event entry, please check your configuration.");
                return false;
            }

            GuardedCrateInstance.CreateInstance(eventEntry, position);
            return true;
        }

        private class GuardedCrateInstance : FacepunchBehaviour, IEventSpawnInstance
        {
            public List<BaseEntity> guardSpawnInstances = new();
            public MapMarkerGenericRadius markerSpawnInstance;
            public HackableLockedCrate crateSpawnInstance;
            public CargoPlane cargoPlaneInstance;
            public List<ItemEntry> LootTable;
            public GuardConfig GuardConfig;
            public int GuardSpawnInstanceCount => guardSpawnInstances.Count;

            public EventState currentState = EventState.Initialize;
            public float thinkEvery = 1f;
            public float lastThinkTime;
            public float timePassed;
            public bool timeEnded;

            public string eventName;
            public float eventSeconds = 120f;

            public bool enableHackableLock;
            public bool enableClanTag;

            public bool enableAutoHack;
            public float hackSeconds;
            public float hackableCrateFallDrag;

            public bool enableMarker;
            public Color markerColor1;
            public Color markerColor2;
            public float markerRadius;
            public float markerOpacity;

            public bool enableEliminateGuards;
            public int guardAmount;

            public bool enableLootTable;
            public int minLootAmount;
            public int maxLootAmount;

            public static void CreateInstance(EventEntry eventEntry, Vector3 position)
            {
                GuardedCrateInstance crateInstance = CustomUtils.CreateObjectWithComponent<GuardedCrateInstance>(position, Quaternion.identity, "Guarded_Create_Event");
                crateInstance.ConfigureEvent(eventEntry);
                crateInstance.EventStart();
                EventManager.RegisterInstance(crateInstance);
            }

            private static void RemoveInstance(GuardedCrateInstance crateInstance)
            {
                EventManager.UnregisterInstance(crateInstance);
                UnityEngine.Object.Destroy(crateInstance?.gameObject);
            }

            private void ConfigureEvent(EventEntry eventEntry)
            {
                if (eventEntry.GuardConfig.Parsed == null)
                    eventEntry.GuardConfig.CacheConfig();

                eventName = eventEntry.EventName;
                eventSeconds = eventEntry.EventDuration;

                enableHackableLock = eventEntry.EnableLockToPlayer;
                enableClanTag = eventEntry.EnableClanTag;

                enableAutoHack = eventEntry.EnableAutoHack;
                hackSeconds = eventEntry.HackableCrateHackSeconds;
                hackableCrateFallDrag = eventEntry.HackableCrateFallDrag;

                enableMarker = eventEntry.EnableMarker;
                markerColor1 = CustomUtils.GetColor(eventEntry.MapMarkerColor1);
                markerColor2 = CustomUtils.GetColor(eventEntry.MapMarkerColor2);
                markerRadius = eventEntry.MapMarkerRadius;
                markerOpacity = eventEntry.MapMarkerOpacity;

                enableLootTable = eventEntry.EnableLootTable;
                minLootAmount = eventEntry.LootMinAmount;
                maxLootAmount = eventEntry.LootMaxAmount;
                LootTable = eventEntry.LootTable;

                enableEliminateGuards = eventEntry.EnableEliminateGuards;
                GuardConfig = eventEntry.GuardConfig;
                guardAmount = eventEntry.GuardAmount;
            }

            private void FixedUpdate()
            {
                if (lastThinkTime < thinkEvery)
                {
                    lastThinkTime += UnityEngine.Time.deltaTime;
                }
                else
                {
                    if (timeEnded)
                        return;

                    timePassed += lastThinkTime;

                    if (timePassed >= eventSeconds)
                    {
                        timeEnded = true;
                        EventFailed();
                        return;
                    }

                    lastThinkTime = 0.0f;
                }
            }

            #region Management

            private void SwitchState(EventState state)
            {
                currentState = state;
            }

            public void EventStart()
            {
                SwitchState(EventState.Running);
                SpawnPlane();
                Interface.Oxide.CallHook("OnGuardedCrateEventStart", transform.position);
                Notification.MessagePlayers(LangKeys.EventStart, MapHelper.PositionToString(transform.position), eventName, eventSeconds.ToStringTime());
            }
            
            public void EventEnded()
            {
                if (currentState == EventState.Complete)
                    crateSpawnInstance = null;
                
                SwitchState(EventState.Ended);
                StopAllCoroutines();
                CancelInvoke();
                ClearEntities();
                crateSpawnInstance = null;
                cargoPlaneInstance = null;
                GuardedCrateInstance.RemoveInstance(this);
            }
            
            public void EventComplete(BasePlayer player)
            {
                SwitchState(EventState.Complete);
                TryLockCrate(player);
                TryHackCrate();
                Interface.CallHook("OnGuardedCrateEventEnded", player, crateSpawnInstance);
                Notification.MessagePlayers(LangKeys.EventCompleted, MapHelper.PositionToString(transform.position), GetWinnerName(player));
                EventEnded();
            }
            
            public void EventFailed()
            {
                SwitchState(EventState.Failed);
                Interface.Oxide.CallHook("OnGuardedCrateEventFailed", transform.position);
                Notification.MessagePlayers(LangKeys.EventEnded, MapHelper.PositionToString(transform.position));
                EventEnded();
            }

            private void TryLockCrate(BasePlayer player)
            {
                if (!enableHackableLock)
                    return;

                _tryLockHackable(player, crateSpawnInstance);
            }

            private void TryHackCrate()
            {
                if (crateSpawnInstance == null || crateSpawnInstance.IsDestroyed)
                    return;

                crateSpawnInstance.shouldDecay = true;
                
                if (enableAutoHack) crateSpawnInstance.StartHacking();
                else crateSpawnInstance.RefreshDecay();
            }

            private string GetWinnerName(BasePlayer player)
            {
                if (player == null)
                    return "Unknown";

                string displayName = player.displayName;
                if (string.IsNullOrEmpty(displayName))
                    return player.UserIDString;

                if (enableClanTag)
                {
                    string clanTag = _tryFetchTag(player.userID);
                    if (!string.IsNullOrEmpty(clanTag)) return $"[{clanTag}]{displayName}";
                }

                return displayName;
            }

            #endregion

            #region Spawning

            public void InitSpawning() => StartCoroutine(SpawnEntities());

            private IEnumerator SpawnEntities()
            {
                yield return CoroutineEx.waitForEndOfFrame;
                yield return SpawnMarker();
                yield return SpawnCrate();
                yield return SpawnGuards();
            }

            private void ClearEntities()
            {
                ClearCrate(currentState == EventState.Complete);
                ClearPlane();
                ClearMarker();
                ClearGuards();
            }

            #region Cargo Plane

            private void SpawnPlane()
            {
                cargoPlaneInstance = (CargoPlane)GameManager.server.CreateEntity(PLANE_PREFAB);
                cargoPlaneInstance.InitDropPosition(transform.position);
                cargoPlaneInstance.dropped = true;
                cargoPlaneInstance.Spawn();
                cargoPlaneInstance.secondsTaken = 0f;
                cargoPlaneInstance.secondsToTake = 30f;
                
                EventCargoPlane eventCargoPlane = cargoPlaneInstance.gameObject.AddComponent<EventCargoPlane>();
                eventCargoPlane.EventSpawnInstance = this;
                
                EntityCache.CreateEntity(cargoPlaneInstance.net.ID, this);
            }

            private void ClearPlane()
            {
                cargoPlaneInstance?.SafeKill();
                cargoPlaneInstance = null;
            }

            #endregion

            #region Guards

            private IEnumerator SpawnGuards()
            {
                yield return CoroutineEx.waitForEndOfFrame;
                
                for (int i = 0; i < guardAmount; i++)
                {
                    float angle = (360f / guardAmount) * i;
                    Vector3 position = transform.position.GetPointAround(10551297, 5f, angle);
                    yield return SpawnGuard(position);
                }
            }

            private IEnumerator SpawnGuard(Vector3 position)
            {
                yield return CoroutineEx.waitForEndOfFrame;
                
                GuardConfig.Parsed["HomePosition"] = position.ToString();
                GuardConfig.Parsed["Kit"] = GuardConfig.Kits.Length > 0 ? GuardConfig.Kits.GetRandom() : string.Empty;
                
                ScientistNPC entity = _trySpawnNpc(position, GuardConfig.Parsed);
                if (entity == null)
                    yield break;
                
                guardSpawnInstances.Add(entity);
                EntityCache.CreateEntity(entity.net.ID, this);
            }

            private void ClearGuards()
            {
                for (int i = guardSpawnInstances.Count - 1; i >= 0; i--)
                    guardSpawnInstances[i].SafeKill();

                guardSpawnInstances.Clear();
            }

            #endregion

            #region Marker

            private IEnumerator SpawnMarker()
            {
                yield return CoroutineEx.waitForEndOfFrame;

                if (!enableMarker)
                    yield break;

                markerSpawnInstance = CustomUtils.CreateEntity<MapMarkerGenericRadius>(MARKER_PREFAB, transform.position, Quaternion.identity);
                markerSpawnInstance.EnableSaving(false);
                markerSpawnInstance.color1 = markerColor1;
                markerSpawnInstance.color2 = markerColor2;
                markerSpawnInstance.radius = markerRadius;
                markerSpawnInstance.alpha = markerOpacity;
                markerSpawnInstance.Spawn();
                markerSpawnInstance.SendUpdate();
                markerSpawnInstance.InvokeRepeating(nameof(MapMarkerGenericRadius.SendUpdate), 10.0f, 10.0f);
            }

            private void ClearMarker()
            {
                markerSpawnInstance.SafeKill();
                markerSpawnInstance = null;
            }

            #endregion

            #region Loot Crate

            private IEnumerator SpawnCrate()
            {
                yield return CoroutineEx.waitForEndOfFrame;

                crateSpawnInstance = CustomUtils.CreateEntity<HackableLockedCrate>(CRATE_PREFAB, transform.position + (Vector3.up * 100f), Quaternion.identity);
                crateSpawnInstance.EnableSaving(false);
                crateSpawnInstance.shouldDecay = false;
                crateSpawnInstance.hackSeconds = HackableLockedCrate.requiredHackSeconds - hackSeconds;
                crateSpawnInstance.Spawn();
                
                PostCrateSpawn(crateSpawnInstance);
                EntityCache.CreateEntity(crateSpawnInstance.net.ID, this);
            }

            private void PostCrateSpawn(HackableLockedCrate hackableCrate)
            {
                if (hackableCrate.TryGetComponent(out Rigidbody rigidbody))
                    rigidbody.drag = hackableCrateFallDrag;
                
                if (!enableLootTable)
                    return;
                
                hackableCrate.Invoke(() => TryPopulateCrate(hackableCrate), 2f);
            }

            private void ClearCrate(bool completed)
            {
                if (completed)
                    return;

                crateSpawnInstance.SafeKill();
                crateSpawnInstance = null;
            }

            #endregion

            #endregion

            #region Hackable

            public object CanPopulateCrate()
            {
                return (!enableLootTable || LootTable == null || LootTable.Count <= 0) ? null : (object)false;
            }

            private void TryPopulateCrate(LootContainer container)
            {
                if (LootTable == null || LootTable.Count == 0)
                    return;

                List<ItemEntry> itemEntries = Facepunch.Pool.Get<List<ItemEntry>>();

                try
                {
                    if (container != null && !container.IsDestroyed)
                    {
                        container.inventory.onItemAddedRemoved = null;
                        container.inventory.SafeClear();
                        GenerateLootItems(ref itemEntries, Mathf.Clamp(UnityEngine.Random.Range(minLootAmount, maxLootAmount), 1, 24));
                        container.inventory.capacity = itemEntries.Count;

                        foreach (ItemEntry lootItem in itemEntries)
                        {
                            Item item = lootItem?.CreateItem();
                            if (item != null && !item.MoveToContainer(container.inventory))
                                item.Remove();
                        }
                    }
                }
                catch (Exception ex)
                {
                    //
                }
                finally
                {
                    Facepunch.Pool.FreeUnmanaged(ref itemEntries);
                }
            }

            private void GenerateLootItems(ref List<ItemEntry> items, int maxItemCount)
            {
                List<ItemEntry> itemEntries = Facepunch.Pool.Get<List<ItemEntry>>();

                try
                {
                    itemEntries.AddRange(LootTable);

                    for (int i = itemEntries.Count - 1; i > 0; i--)
                    {
                        int j = UnityEngine.Random.Range(0, i + 1);
                        (itemEntries[i], itemEntries[j]) = (itemEntries[j], itemEntries[i]);
                    }

                    foreach (ItemEntry itemEntry in itemEntries)
                    {
                        if (!items.Contains(itemEntry))
                        {
                            items.Add(itemEntry);

                            if (items.Count >= maxItemCount)
                                break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    //
                }
                finally
                {
                    Facepunch.Pool.FreeUnmanaged(ref itemEntries);
                }
            }

            #endregion

            #region Oxide Hooks

            public object CanHackCrate(BasePlayer player)
            {
                if (!enableEliminateGuards)
                    return null;

                if (GuardSpawnInstanceCount > 0)
                {
                    Notification.MessagePlayer(player, LangKeys.EliminateGuards);
                    return true;
                }

                return null;
            }

            public void OnGuardKilled(ScientistNPC scientist, BasePlayer player)
            {
                if (currentState != EventState.Running)
                    return;
                
                guardSpawnInstances.Remove(scientist);
                
                timePassed = 0f;

                if (guardSpawnInstances.Count > 0)
                    return;
                
                EventComplete(player);
            }

            public void OnPlaneKilled(CargoPlane plane) => cargoPlaneInstance = null;

            public void OnCrateKilled(LootContainer container) => crateSpawnInstance = null;

            #endregion
        }

        private interface IEventSpawnInstance
        {
            void InitSpawning();
        }

        private class EventCargoPlane : MonoBehaviour
        {
            public IEventSpawnInstance EventSpawnInstance;
            public CargoPlane cargoPlane;
            public bool hasDropped;

            private void Awake()
            {
                cargoPlane = GetComponent<CargoPlane>();
                cargoPlane.dropped = true;
            }

            private void Update()
            {
                if (hasDropped) 
                    return;
                
                float time = Mathf.InverseLerp(0.0f, cargoPlane.secondsToTake, cargoPlane.secondsTaken);
                if (!(time >= 0.5)) 
                    return;
                
                hasDropped = true;
                
                if (!EventSpawnInstance.IsUnityNull())
                    EventSpawnInstance.InitSpawning();
            }
        }
        
        #endregion

        #region Entity Cache

        private static class EntityCache
        {
            private static readonly Dictionary<NetworkableId, GuardedCrateInstance> Entities = new();

            public static void OnUnload() => Entities.Clear();

            public static GuardedCrateInstance FindEntityEventInstance(NetworkableId id)
            {
                Entities.TryGetValue(id, out GuardedCrateInstance instance);
                return instance;
            }
            
            public static bool HasEntity(ulong id)
            {
                foreach (NetworkableId networkableId in Entities.Keys)
                {
                    if (networkableId.Value == id)
                        return true;
                }
                
                return false;
            }

            public static void CreateEntity(NetworkableId id, GuardedCrateInstance component) => Entities.Add(id, component);

            public static void RemoveEntity(NetworkableId id) => Entities.Remove(id);
        }

        #endregion

        #region Notification

        private static class Notification
        {
            private static string GetMessage(string langKey, string userId = null, params object[] args)
            {
                string message = userId != null ? _getMessage(langKey, userId) : _getMessage(langKey, null);
                return args?.Length > 0 ? string.Format(message, args) : message;
            }

            public static void MessagePlayer(ConsoleSystem.Arg arg, string langKey, params object[] args)
            {
                if (arg == null) return;
                arg.ReplyWith(GetMessage(langKey, null, args));
            }

            public static void MessagePlayer(BasePlayer player, string langKey, params object[] args)
            {
                if (player == null) return;
                player.ChatMessage(GetMessage(langKey, player.UserIDString, args));
            }

            public static void MessagePlayers(string langKey, params object[] args)
            {
                MessageSettings messageSettings = _configData.MessageSettings;
                string prefix = messageSettings.EnableChatPrefix ? GetMessage(LangKeys.Prefix) : string.Empty;
                string message = GetMessage(langKey, null, args);
                
                if (messageSettings.EnableChat) ConsoleNetwork.BroadcastToAllClients("chat.add", 2, messageSettings.ChatIcon, prefix + message);
                if (messageSettings.EnableToast) ConsoleNetwork.BroadcastToAllClients("gametip.showtoast_translated", 2, null, message, false);
                if (messageSettings.EnableGuiAnnouncements) _sendGuiNotification(message, messageSettings.GuiAnnouncementsBgColor, messageSettings.GuiAnnouncementsTextColor, null, 0.03f);
            }
        }

        #endregion

        #region Console Command

        private void EventConsoleCommands(ConsoleSystem.Arg arg)
        {
            if (arg == null || !arg.IsRcon)
                return;

            if (!arg.HasArgs(2))
            {
                DisplayHelpText(arg.Player());
                return;
            }

            string option = arg.GetString(0);
            if (option.Equals("start", StringComparison.OrdinalIgnoreCase))
            {
                bool started = TryStartEvent(arg.Args?.Length > 1 ? string.Join(" ", arg.Args.Skip(1)) : string.Empty);
                Notification.MessagePlayer(arg, (started ? LangKeys.StartEvent : LangKeys.FailedToStartEvent));
                return;
            }

            if (option.Equals("stop", StringComparison.OrdinalIgnoreCase))
            {
                EventManager.CleanupInstances();
                Notification.MessagePlayer(arg, LangKeys.ClearEvents);
                return;
            }

            if (option.Equals("position", StringComparison.OrdinalIgnoreCase))
            {
                if (!arg.HasArgs(3))
                {
                    DisplayHelpText(arg.Player());
                    return;
                }

                EventEntry eventEntry = _storedData.FindEventByName(arg.GetString(1));
                if (eventEntry == null)
                {
                    Notification.MessagePlayer(arg, LangKeys.EventNotFound);
                    return;
                }

                Vector3 position = arg.GetVector3(2);
                if (position == Vector3.zero)
                {
                    Notification.MessagePlayer(arg, LangKeys.EventPositionInvalid);
                    return;
                }

                if (EventManager.HasIntersectingEvent(position))
                {
                    Notification.MessagePlayer(arg, LangKeys.EventIntersecting);
                    return;
                }

                GuardedCrateInstance.CreateInstance(eventEntry, position);
                return;
            }

            DisplayHelpText(arg.Player());
        }

        #endregion

        #region Chat Command

        private void EventCommandCommands(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, PERM_USE))
            {
                Notification.MessagePlayer(player, LangKeys.NoPermission);
                return;
            }

            if (args.Length < 1)
            {
                DisplayHelpText(player);
                return;
            }

            string option = args[0];
            if (option.Equals("start", StringComparison.OrdinalIgnoreCase))
            {
                bool started = TryStartEvent(string.Join(" ", args.Skip(1).ToArray()));
                Notification.MessagePlayer(player, (started ? LangKeys.StartEvent : LangKeys.FailedToStartEvent));
                return;
            }

            if (option.Equals("stop", StringComparison.OrdinalIgnoreCase))
            {
                EventManager.CleanupInstances();
                Notification.MessagePlayer(player, LangKeys.ClearEvents);
                return;
            }

            if (option.Equals("here", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 2)
                {
                    DisplayHelpText(player);
                    return;
                }

                EventEntry eventEntry = _storedData.FindEventByName(args[1]);
                if (eventEntry == null)
                {
                    Notification.MessagePlayer(player, LangKeys.EventNotFound);
                    return;
                }

                GuardedCrateInstance.CreateInstance(eventEntry, player.transform.position);
                return;
            }

            if (option.Equals("position", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 3)
                {
                    DisplayHelpText(player);
                    return;
                }

                EventEntry eventEntry = _storedData.FindEventByName(args[1]);
                if (eventEntry == null)
                {
                    Notification.MessagePlayer(player, LangKeys.EventNotFound);
                    return;
                }

                Vector3 position = args[2].ToVector3();
                if (position == Vector3.zero)
                {
                    Notification.MessagePlayer(player, LangKeys.EventPositionInvalid);
                    return;
                }

                if (EventManager.HasIntersectingEvent(position))
                {
                    Notification.MessagePlayer(player, LangKeys.EventIntersecting);
                    return;
                }

                GuardedCrateInstance.CreateInstance(eventEntry, position);
                return;
            }

            if (option.Equals("loot", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 2)
                {
                    DisplayHelpText(player);
                    return;
                }

                EventEntry eventEntry = _storedData.FindEventByName(args[1]);
                if (eventEntry == null)
                {
                    Notification.MessagePlayer(player, LangKeys.EventNotFound);
                    return;
                }

                if (player.inventory == null)
                    return;

                eventEntry.LootTable.Clear();
                eventEntry.LootTable.AddRange(ItemEntry.SaveItems(player.inventory.containerMain));
                eventEntry.LootTable.AddRange(ItemEntry.SaveItems(player.inventory.containerBelt));
                eventEntry.EnableLootTable = eventEntry.LootTable.Count > 0;
                SaveData();

                Notification.MessagePlayer(player, LangKeys.EventUpdated);
                return;
            }

            if (option.Equals("amount", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 3)
                {
                    DisplayHelpText(player);
                    return;
                }

                EventEntry eventEntry = _storedData.FindEventByName(args[1]);
                if (eventEntry == null)
                {
                    Notification.MessagePlayer(player, LangKeys.EventNotFound);
                    return;
                }

                int.TryParse(args[2], out int amount);

                if (amount < 1)
                {
                    Notification.MessagePlayer(player, LangKeys.InvalidGuardAmount);
                    return;
                }

                eventEntry.GuardAmount = amount;
                SaveData();

                Notification.MessagePlayer(player, LangKeys.EventUpdated);
                return;
            }

            if (option.Equals("drag", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 3)
                {
                    DisplayHelpText(player);
                    return;
                }

                EventEntry eventEntry = _storedData.FindEventByName(args[1]);
                if (eventEntry == null)
                {
                    Notification.MessagePlayer(player, LangKeys.EventNotFound);
                    return;
                }

                float.TryParse(args[2], out float amount);
                eventEntry.HackableCrateFallDrag = amount;
                SaveData();

                Notification.MessagePlayer(player, LangKeys.EventUpdated);
                return;
            }

            if (option.Equals("loadout", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 2)
                {
                    DisplayHelpText(player);
                    return;
                }

                EventEntry eventEntry = _storedData.FindEventByName(args[1]);
                if (eventEntry == null)
                {
                    Notification.MessagePlayer(player, LangKeys.EventNotFound);
                    return;
                }

                if (player.inventory == null)
                    return;

                eventEntry.GuardConfig.BeltItems.Clear();
                eventEntry.GuardConfig.WearItems.Clear();
                eventEntry.GuardConfig.BeltItems = GuardConfig.BeltEntry.SaveItems(player.inventory.containerBelt);
                eventEntry.GuardConfig.WearItems = GuardConfig.WearEntry.SaveItems(player.inventory.containerWear);
                eventEntry.GuardConfig.CacheConfig();
                SaveData();

                Notification.MessagePlayer(player, LangKeys.EventUpdated);
                return;
            }

            DisplayHelpText(player);
        }

        private void DisplayHelpText(BasePlayer player)
        {
            StringBuilder sb = Facepunch.Pool.Get<StringBuilder>();

            try
            {
                sb.Clear();
                sb.AppendFormat(lang.GetMessage(LangKeys.Prefix, this, player.UserIDString))
                    .AppendFormat(lang.GetMessage(LangKeys.HelpStartEvent, this, player.UserIDString), _configData.CommandName, string.Join("|", _storedData.EventNames))
                    .AppendFormat(lang.GetMessage(LangKeys.HelpStopEvent, this, player.UserIDString), _configData.CommandName)
                    .AppendFormat(lang.GetMessage(LangKeys.HelpHereEvent, this, player.UserIDString), _configData.CommandName)
                    .AppendFormat(lang.GetMessage(LangKeys.HelpPositionEvent, this, player.UserIDString), _configData.CommandName)
                    .AppendFormat(lang.GetMessage(LangKeys.HelpLootEvent, this, player.UserIDString), _configData.CommandName)
                    .AppendFormat(lang.GetMessage(LangKeys.HelpDragEvent, this, player.UserIDString), _configData.CommandName)
                    .AppendFormat(lang.GetMessage(LangKeys.HelpGuardAmount, this, player.UserIDString), _configData.CommandName)
                    .AppendFormat(lang.GetMessage(LangKeys.HelpGuardLoadout, this, player.UserIDString), _configData.CommandName);
                
                Notification.MessagePlayer(player, sb.ToString());
            }
            finally
            {
                sb.Clear();
                Facepunch.Pool.FreeUnmanaged(ref sb);
            }
        }

        #endregion

        #region Hook Subscribing

        private readonly HashSet<string> _hooks = new()
        {
            "OnEntityDeath",
            "OnEntityKill",
            "CanHackCrate"
        };

        private void SubscribeToHooks(int count)
        {
            if (count == 1)
            {
                foreach (string hook in _hooks)
                    Subscribe(hook);

                return;
            }

            if (count == 0)
            {
                foreach (string hook in _hooks)
                    Unsubscribe(hook);
            }
        }

        #endregion
        
        #region API Hooks

        private bool API_IsGuardedCrateCargoPlane(CargoPlane entity)
        {
            return EntityCache.FindEntityEventInstance(entity.net?.ID ?? default) != null;
        }

        private bool API_IsGuardedCrateEntity(BaseEntity entity)
        {
            return EntityCache.FindEntityEventInstance(entity.net?.ID ?? default) != null;
        }

        #endregion

        #region 3rd Party Hooks
        
        #region Alpha Loot

        private object CanPopulateLoot(HackableLockedCrate crate)
        {
            return EntityCache.FindEntityEventInstance(crate.net?.ID ?? default)
                ?.CanPopulateCrate();
        }

        #endregion

        #region Better Loot

        private object ShouldBLPopulate_Container(ulong lootContainerId)
        {
            return EntityCache.HasEntity(lootContainerId) ? _objTrue : null;
        }

        #endregion

        #region Rust Edit

        private object OnNpcRustEdit(ScientistNPC npc)
        {
            return EntityCache.FindEntityEventInstance(npc.net?.ID ?? default) != null ? _objTrue : null;
        }

        #endregion
        
        #region Clans

        private string TryFetchTag(ulong userID)
        {
            return Clans?.Call<string>("GetClanOf", userID);
        }

        #endregion
        
        #region Hackable Lock

        private object TryLockHackable(BasePlayer player, HackableLockedCrate lockedCrate)
        {
            return HackableLock?.Call("LockCrateToPlayer", player, lockedCrate);
        }

        #endregion

        #region Npc Spawn

        private ScientistNPC TrySpawnNpc(Vector3 position, JObject config)
        {
            return (ScientistNPC)NpcSpawn?.Call("SpawnNpc", position, config);
        }

        #endregion

        #region Gui Announcements

        private object SendGuiNotification(string message, string bgColor, string textColor, BasePlayer player, float duration)
        {
            return GUIAnnouncements?.Call("CreateAnnouncement", message, bgColor, textColor, player, duration);
        }        

        #endregion
        
        #endregion
        
        #region Json Converters

        private class EnumCollectionStringConverter<TEnum> : JsonConverter where TEnum : struct, Enum
        {
            public override bool CanConvert(Type objectType)
            {
                if (objectType == typeof(List<TEnum>)) return true;
                if (objectType == typeof(TEnum[])) return true;
                return false;
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                if (reader.TokenType == JsonToken.Null) 
                    return null;
                
                List<string> list = serializer.Deserialize<List<string>>(reader);
                if (list == null) 
                    return null;
                
                if (objectType == typeof(TEnum[]))
                {
                    TEnum[] result = new TEnum[list.Count];
                    int idx = 0;
                    
                    for (int i = 0; i < list.Count; i++)
                    {
                        if (Enum.TryParse(list[i], true, out TEnum parsed))
                            result[idx++] = parsed;
                    }
                    
                    if (idx != list.Count)
                    {
                        TEnum[] trimmed = new TEnum[idx];
                        Array.Copy(result, trimmed, idx);
                        return trimmed;
                    }
                    
                    return result;
                }
                
                if (objectType == typeof(List<TEnum>))
                {
                    List<TEnum> result = new List<TEnum>(list.Count);
                    for (int i = 0; i < list.Count; i++)
                    {
                        if (Enum.TryParse(list[i], true, out TEnum parsed))
                            result.Add(parsed);
                    }
                    
                    return result;
                }

                throw new JsonSerializationException($"Error converting value type '{objectType}'.");
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                switch (value)
                {
                    case IEnumerable<TEnum> collection:
                    {
                        List<string> stringList = new List<string>();
                    
                        foreach (TEnum item in collection)
                            stringList.Add(item.ToString());

                        serializer.Serialize(writer, stringList);
                        return;
                    }
                    case null:
                        serializer.Serialize(writer, new List<string>());
                        return;
                    default: throw new JsonSerializationException($"Unexpected value type: {value.GetType()}");
                }
            }
        }
        
        #endregion
    }
}

namespace GuardedCrateExt
{
    internal static class CustomUtils
    {
        public static T CreateObjectWithComponent<T>(Vector3 position, Quaternion rotation, string name) where T : MonoBehaviour
        {
            GameObject gameObject = new GameObject(name);
            gameObject.transform.SetPositionAndRotation(position, rotation);
            return gameObject.AddComponent<T>();
        }
        
        public static T CreateEntity<T>(string prefab, Vector3 position, Quaternion rotation) where T : BaseEntity
        {
            T baseEntity = (T)GameManager.server.CreateEntity(prefab, position, rotation);
            baseEntity.enableSaving = false;
            return baseEntity;
        }
        
        public static Color GetColor(string hex)
        {
            return ColorUtility.TryParseHtmlString(hex, out Color color) ? color : Color.yellow;
        }
    }

    internal static class ExtensionMethods
    {
        public static bool IsPluginReady(this Plugin plugin) => plugin != null && plugin.IsLoaded;
        
        public static Vector3 GetPointAround(this Vector3 origin, int layers, float radius, float angle)
        {
            Vector3 pointAround = Vector3.zero;
            pointAround.x = origin.x + radius * Mathf.Sin(angle * Mathf.Deg2Rad);
            pointAround.z = origin.z + radius * Mathf.Cos(angle * Mathf.Deg2Rad);
            pointAround.y = TerrainMeta.HeightMap.GetHeight(origin) + 100f;
            
            if (Physics.Raycast(pointAround, Vector3.down, out RaycastHit hit, 200f, layers, QueryTriggerInteraction.Ignore))
                pointAround.y = hit.point.y;
            
            pointAround.y += 0.25f;
            return pointAround;
        }
        
        public static void SafeKill(this BaseEntity entity)
        {
            if (entity != null && !entity.IsDestroyed)
                entity.Kill();
        }

        public static void SafeClear(this ItemContainer container)
        {
            for (int i = container.itemList.Count - 1; i >= 0; i--)
            {
                Item item = container.itemList[i];
                item.RemoveFromContainer();
                item.Remove();
            }
        }
        
        public static string ToStringTime(this float seconds) 
        {
            TimeSpan timeSpan = TimeSpan.FromSeconds(seconds);
            if (timeSpan.Days >= 1) return $"{timeSpan.Days} day{(timeSpan.Days != 1 ? "(s)" : "")}";
            if (timeSpan.Hours >= 1) return $"{timeSpan.Hours} hour{(timeSpan.Hours != 1 ? "(s)" : "")}";
            return timeSpan.Minutes >= 1 ? $"{timeSpan.Minutes} minute{(timeSpan.Minutes != 1 ? "(s)" : "")}" : $"{timeSpan.Seconds} second{(timeSpan.Seconds == 1 ? "(s)" : "")}";
        }
    }
}