using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("QuestSystem", "YourName", "1.0.0")]
    [Description("Comprehensive quest system with dynamic missions, NPC interactions, and rewards")]
    public class QuestSystem : RustPlugin
    {
        #region Fields

        private DataManager _dataManager;
        private ProgressManager _progressManager;
        private QuestManager _questManager;
        private RewardManager _rewardManager;
        private NPCManager _npcManager;
        private UIManager _uiManager;
        private IntegrationManager _integrationManager;
        private MapMarkerManager _mapMarkerManager;

        private Configuration _config;
        private QuestData _questData;

        private const string PermissionUse = "questsystem.use";
        private const string PermissionAdmin = "questsystem.admin";

        #endregion

        #region Oxide Hooks

        private void Init()
        {
            permission.RegisterPermission(PermissionUse, this);
            permission.RegisterPermission(PermissionAdmin, this);
        }

        private void OnServerInitialized()
        {
            _dataManager = new DataManager(this);
            _questData = _dataManager.LoadQuestDefinitions();
            
            _integrationManager = new IntegrationManager(this);
            _progressManager = new ProgressManager(this, _dataManager);
            _rewardManager = new RewardManager(this, _integrationManager);
            _npcManager = new NPCManager(this, _dataManager);
            _mapMarkerManager = new MapMarkerManager(this);
            _questManager = new QuestManager(this, _questData, _progressManager, _rewardManager, _npcManager, _mapMarkerManager);
            _uiManager = new UIManager(this, _questManager, _progressManager);

            foreach (var player in BasePlayer.activePlayerList)
            {
                OnPlayerConnected(player);
            }
        }

        private void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                _uiManager?.DestroyUI(player);
                _progressManager?.SavePlayerData(player.userID);
            }
        }

        private void OnServerSave()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                _progressManager?.SavePlayerData(player.userID);
            }
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            _progressManager?.LoadPlayerData(player.userID);
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            _uiManager?.DestroyUI(player);
            _progressManager?.SavePlayerData(player.userID);
        }

        #endregion

        #region Game Event Hooks

        private void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (info?.InitiatorPlayer == null) return;
            var player = info.InitiatorPlayer;
            var target = entity.ShortPrefabName;
            _questManager?.ProcessObjectiveEvent(player, ObjectiveType.Kill, target, 1);
        }

        private void OnCollectiblePickup(CollectibleEntity collectible, BasePlayer player)
        {
            if (player == null || collectible == null) return;
            var item = collectible.itemList?.FirstOrDefault();
            if (item != null)
            {
                _questManager?.ProcessObjectiveEvent(player, ObjectiveType.Gather, item.itemDef.shortname, (int)item.amount);
            }
        }

        private void OnDispenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item)
        {
            var player = entity as BasePlayer;
            if (player == null || item == null) return;
            _questManager?.ProcessObjectiveEvent(player, ObjectiveType.Gather, item.info.shortname, item.amount);
        }

        private void OnDispenserBonus(ResourceDispenser dispenser, BaseEntity entity, Item item)
        {
            var player = entity as BasePlayer;
            if (player == null || item == null) return;
            _questManager?.ProcessObjectiveEvent(player, ObjectiveType.Gather, item.info.shortname, item.amount);
        }

        private void OnItemCraftFinished(ItemCraftTask task, Item item, ItemCrafter crafter)
        {
            if (crafter == null) return;
            var player = crafter.owner;
            if (player == null || item == null) return;
            _questManager?.ProcessObjectiveEvent(player, ObjectiveType.Craft, item.info.shortname, item.amount);
        }

        private void OnLootEntity(BasePlayer player, BaseEntity entity)
        {
            if (player == null || entity == null) return;
            _questManager?.ProcessObjectiveEvent(player, ObjectiveType.Loot, entity.ShortPrefabName, 1);
        }

        private void OnEntityBuilt(Planner planner, GameObject go)
        {
            var player = planner?.GetOwnerPlayer();
            if (player == null || go == null) return;
            var entity = go.ToBaseEntity();
            if (entity != null)
            {
                _questManager?.ProcessObjectiveEvent(player, ObjectiveType.Build, entity.ShortPrefabName, 1);
            }
        }

        #endregion

        #region Commands

        [ChatCommand("quest")]
        private void QuestCommand(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, PermissionUse))
            {
                SendReply(player, "You don't have permission to use the quest system.");
                return;
            }

            _uiManager?.ShowQuestUI(player);
        }

        [ChatCommand("questadmin")]
        private void QuestAdminCommand(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, PermissionAdmin))
            {
                SendReply(player, "You don't have permission to use admin commands.");
                return;
            }

            if (args.Length < 1)
            {
                SendReply(player, "Usage: /questadmin <assign|complete|edit|reset> [questId] [playerId]");
                return;
            }

            var action = args[0].ToLower();

            // Handle reset command (no additional args needed)
            if (action == "reset")
            {
                _questData = _dataManager.CreateAndSaveDefaultQuestData();
                _questManager.ReloadQuestData(_questData);
                SendReply(player, "Quest data has been reset to defaults! All quests regenerated with new descriptions.");
                return;
            }

            if (action == "edit")
            {
                _uiManager?.ShowAdminUI(player);
                return;
            }

            // Other commands need questId
            if (args.Length < 2)
            {
                SendReply(player, "Usage: /questadmin <assign|complete> <questId> [playerId]");
                return;
            }

            var questId = args[1];
            var targetPlayer = args.Length > 2 ? BasePlayer.Find(args[2]) : player;

            if (targetPlayer == null)
            {
                SendReply(player, "Player not found.");
                return;
            }

            switch (action)
            {
                case "assign":
                    var assignResult = _questManager.AcceptQuest(targetPlayer, questId);
                    SendReply(player, assignResult.Success ? $"Quest '{questId}' assigned to {targetPlayer.displayName}" : $"Failed: {assignResult.ErrorMessage}");
                    break;
                case "complete":
                    var completeResult = _questManager.CompleteQuest(targetPlayer, questId);
                    SendReply(player, completeResult.Success ? $"Quest '{questId}' completed for {targetPlayer.displayName}" : $"Failed: {completeResult.ErrorMessage}");
                    break;
                default:
                    SendReply(player, "Unknown action. Use 'assign', 'complete', 'edit', or 'reset'.");
                    break;
            }
        }

        [ChatCommand("questedit")]
        private void QuestEditCommand(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, PermissionAdmin))
            {
                SendReply(player, "You don't have permission to edit quests.");
                return;
            }
            _uiManager?.ShowAdminUI(player);
        }

        [ConsoleCommand("quest.accept")]
        private void ConsoleAcceptQuest(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || arg.Args == null || arg.Args.Length < 1) return;
            
            var questId = arg.Args[0];
            var result = _questManager.AcceptQuest(player, questId);
            if (result.Success)
            {
                _uiManager?.RefreshQuestUI(player);
            }
            else
            {
                SendReply(player, result.ErrorMessage);
            }
        }

        [ConsoleCommand("quest.complete")]
        private void ConsoleCompleteQuest(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || arg.Args == null || arg.Args.Length < 1) return;
            
            var questId = arg.Args[0];
            var result = _questManager.CompleteQuest(player, questId);
            if (result.Success)
            {
                _uiManager?.RefreshQuestUI(player);
            }
            else
            {
                SendReply(player, result.ErrorMessage);
            }
        }

        [ConsoleCommand("quest.abandon")]
        private void ConsoleAbandonQuest(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || arg.Args == null || arg.Args.Length < 1) return;
            
            var questId = arg.Args[0];
            var result = _questManager.AbandonQuest(player, questId);
            if (result.Success)
            {
                _uiManager?.RefreshQuestUI(player);
            }
            else
            {
                SendReply(player, result.ErrorMessage);
            }
        }

        [ConsoleCommand("quest.ui.close")]
        private void ConsoleCloseUI(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            _uiManager?.DestroyUI(player);
        }

        [ConsoleCommand("quest.ui.tab")]
        private void ConsoleChangeTab(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || arg.Args == null || arg.Args.Length < 1) return;
            _uiManager?.ChangeTab(player, arg.Args[0]);
        }

        [ConsoleCommand("quest.ui.select")]
        private void ConsoleSelectQuest(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || arg.Args == null || arg.Args.Length < 1) return;
            _uiManager?.SelectQuest(player, arg.Args[0]);
        }

        [ConsoleCommand("quest.ui.admin")]
        private void ConsoleOpenAdmin(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || !permission.UserHasPermission(player.UserIDString, PermissionAdmin)) return;
            _uiManager?.DestroyUI(player);
            _uiManager?.ShowAdminUI(player);
        }

        // Admin UI Commands
        [ConsoleCommand("quest.admin.close")]
        private void ConsoleAdminClose(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            _uiManager?.DestroyAdminUI(player);
        }

        [ConsoleCommand("quest.admin.select")]
        private void ConsoleAdminSelect(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || !permission.UserHasPermission(player.UserIDString, PermissionAdmin)) return;
            var questId = arg.Args?.Length > 0 ? arg.Args[0] : null;
            _uiManager?.AdminSelectQuest(player, questId);
        }

        [ConsoleCommand("quest.admin.new")]
        private void ConsoleAdminNew(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || !permission.UserHasPermission(player.UserIDString, PermissionAdmin)) return;
            _uiManager?.AdminNewQuest(player);
        }

        [ConsoleCommand("quest.admin.backtoquest")]
        private void ConsoleAdminBackToQuest(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            _uiManager?.DestroyAdminUI(player);
            _uiManager?.ShowQuestUI(player);
        }

        [ConsoleCommand("quest.admin.resetall")]
        private void ConsoleAdminResetAll(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || !permission.UserHasPermission(player.UserIDString, PermissionAdmin)) return;
            _questData = _dataManager.CreateAndSaveDefaultQuestData();
            _questManager.ReloadQuestData(_questData);
            _uiManager?.ShowAdminUI(player);
            SendReply(player, "All quests have been reset to defaults!");
        }

        [ConsoleCommand("quest.admin.delete")]
        private void ConsoleAdminDelete(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || !permission.UserHasPermission(player.UserIDString, PermissionAdmin)) return;
            var questId = arg.Args?.Length > 0 ? arg.Args[0] : null;
            if (string.IsNullOrEmpty(questId)) return;
            _questManager.DeleteQuest(questId);
            _uiManager?.ShowAdminUI(player);
            SendReply(player, $"Quest '{questId}' deleted.");
        }

        [ConsoleCommand("quest.admin.save")]
        private void ConsoleAdminSave(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || !permission.UserHasPermission(player.UserIDString, PermissionAdmin)) return;
            if (arg.Args == null || arg.Args.Length < 6) return;

            var questId = arg.Args[0];
            var name = arg.Args[1].Replace("_", " ");
            var description = arg.Args[2].Replace("_", " ");
            var category = arg.Args[3];
            var xpReward = int.TryParse(arg.Args[4], out var xp) ? xp : 100;
            var requiredLevel = int.TryParse(arg.Args[5], out var lvl) ? lvl : 0;

            _questManager.SaveQuestBasicInfo(questId, name, description, category, xpReward, requiredLevel);
            _uiManager?.ShowAdminUI(player);
            SendReply(player, $"Quest '{name}' saved.");
        }

        [ConsoleCommand("quest.admin.addobjective")]
        private void ConsoleAdminAddObjective(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || !permission.UserHasPermission(player.UserIDString, PermissionAdmin)) return;
            if (arg.Args == null || arg.Args.Length < 5) return;

            var questId = arg.Args[0];
            var objType = arg.Args[1];
            var target = arg.Args[2];
            var amount = int.TryParse(arg.Args[3], out var amt) ? amt : 1;
            var desc = arg.Args[4].Replace("_", " ");

            _questManager.AddObjectiveToQuest(questId, objType, target, amount, desc);
            _uiManager?.AdminSelectQuest(player, questId);
        }

        [ConsoleCommand("quest.admin.addreward")]
        private void ConsoleAdminAddReward(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || !permission.UserHasPermission(player.UserIDString, PermissionAdmin)) return;
            if (arg.Args == null || arg.Args.Length < 4) return;

            var questId = arg.Args[0];
            var rewardType = arg.Args[1];
            var item = arg.Args[2];
            var amount = int.TryParse(arg.Args[3], out var amt) ? amt : 1;

            _questManager.AddRewardToQuest(questId, rewardType, item, amount);
            _uiManager?.AdminSelectQuest(player, questId);
        }

        #endregion

        #region Configuration

        protected override void LoadDefaultConfig()
        {
            _config = new Configuration();
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
                PrintError("Configuration file is corrupt, loading defaults...");
                LoadDefaultConfig();
            }
            SaveConfig();
        }

        protected override void SaveConfig() => Config.WriteObject(_config);

        private class Configuration
        {
            [JsonProperty("UI Settings")]
            public UISettings UI { get; set; } = new UISettings();

            [JsonProperty("Quest Settings")]
            public QuestSettings Settings { get; set; } = new QuestSettings();

            public class UISettings
            {
                [JsonProperty("Show Mini Quest List")]
                public bool ShowMiniQuestList { get; set; } = true;

                [JsonProperty("Mini Quest List Position X")]
                public float MiniListX { get; set; } = 0.01f;

                [JsonProperty("Mini Quest List Position Y")]
                public float MiniListY { get; set; } = 0.7f;
            }

            public class QuestSettings
            {
                [JsonProperty("Default Cooldown (seconds)")]
                public int DefaultCooldown { get; set; } = 3600;

                [JsonProperty("Max Active Quests")]
                public int MaxActiveQuests { get; set; } = 5;
            }

            [JsonProperty("XP Settings")]
            public XPSettings XP { get; set; } = new XPSettings();

            public class XPSettings
            {
                [JsonProperty("Enable Built-in XP System")]
                public bool Enabled { get; set; } = true;

                [JsonProperty("Base XP Per Quest")]
                public int BaseXPPerQuest { get; set; } = 100;

                [JsonProperty("XP Required Per Level (multiplier)")]
                public float XPPerLevelMultiplier { get; set; } = 1.5f;

                [JsonProperty("Base XP For Level 1")]
                public int BaseXPForLevel { get; set; } = 100;

                [JsonProperty("Max Level")]
                public int MaxLevel { get; set; } = 50;
            }
        }

        #endregion


        #region Data Models

        public enum ObjectiveType
        {
            Gather,
            Kill,
            Craft,
            Loot,
            Deliver,
            Explore,
            Interact,
            Build,
            Destroy,
            Fish,
            Harvest,
            Research
        }

        public enum RewardType
        {
            Item,
            Blueprint,
            Currency,
            Command,
            SkillPoints,
            Experience
        }

        public enum QuestStatus
        {
            Active,
            ReadyToComplete,
            Completed,
            Failed
        }

        public class QuestDefinition
        {
            [JsonProperty("id")]
            public string Id { get; set; }

            [JsonProperty("name")]
            public string Name { get; set; }

            [JsonProperty("description")]
            public string Description { get; set; }

            [JsonProperty("objectives")]
            public List<QuestObjective> Objectives { get; set; } = new List<QuestObjective>();

            [JsonProperty("rewards")]
            public List<QuestReward> Rewards { get; set; } = new List<QuestReward>();

            [JsonProperty("settings")]
            public QuestDefinitionSettings Settings { get; set; } = new QuestDefinitionSettings();

            [JsonProperty("npcId")]
            public string NpcId { get; set; }
        }

        public class QuestObjective
        {
            [JsonProperty("id")]
            public string Id { get; set; }

            [JsonProperty("type")]
            [JsonConverter(typeof(StringEnumConverter))]
            public ObjectiveType Type { get; set; }

            [JsonProperty("target")]
            public string Target { get; set; }

            [JsonProperty("requiredAmount")]
            public int RequiredAmount { get; set; }

            [JsonProperty("description")]
            public string Description { get; set; }

            [JsonProperty("location")]
            public Vector3Data Location { get; set; }
        }

        public class Vector3Data
        {
            [JsonProperty("x")]
            public float X { get; set; }

            [JsonProperty("y")]
            public float Y { get; set; }

            [JsonProperty("z")]
            public float Z { get; set; }

            public Vector3 ToVector3() => new Vector3(X, Y, Z);

            public static Vector3Data FromVector3(Vector3 v) => new Vector3Data { X = v.x, Y = v.y, Z = v.z };
        }

        public class QuestReward
        {
            [JsonProperty("type")]
            [JsonConverter(typeof(StringEnumConverter))]
            public RewardType Type { get; set; }

            [JsonProperty("itemShortname")]
            public string ItemShortname { get; set; }

            [JsonProperty("amount")]
            public int Amount { get; set; }

            [JsonProperty("skinId")]
            public ulong SkinId { get; set; }

            [JsonProperty("command")]
            public string Command { get; set; }

            [JsonProperty("blueprintShortname")]
            public string BlueprintShortname { get; set; }
        }

        public class QuestDefinitionSettings
        {
            [JsonProperty("isRepeatable")]
            public bool IsRepeatable { get; set; }

            [JsonProperty("cooldownSeconds")]
            public int CooldownSeconds { get; set; }

            [JsonProperty("maxCompletions")]
            public int MaxCompletions { get; set; }

            [JsonProperty("prerequisiteQuestIds")]
            public List<string> PrerequisiteQuestIds { get; set; } = new List<string>();

            [JsonProperty("requiredLevel")]
            public int RequiredLevel { get; set; }

            [JsonProperty("category")]
            public string Category { get; set; } = "General";

            [JsonProperty("xpReward")]
            public int XPReward { get; set; } = 100;
        }

        public class PlayerQuestData
        {
            [JsonProperty("playerId")]
            public ulong PlayerId { get; set; }

            [JsonProperty("activeQuests")]
            public Dictionary<string, QuestProgress> ActiveQuests { get; set; } = new Dictionary<string, QuestProgress>();

            [JsonProperty("completedQuests")]
            public Dictionary<string, QuestCompletionRecord> CompletedQuests { get; set; } = new Dictionary<string, QuestCompletionRecord>();

            [JsonProperty("xp")]
            public int XP { get; set; }

            [JsonProperty("level")]
            public int Level { get; set; } = 1;
        }

        public class QuestProgress
        {
            [JsonProperty("questId")]
            public string QuestId { get; set; }

            [JsonProperty("status")]
            [JsonConverter(typeof(StringEnumConverter))]
            public QuestStatus Status { get; set; }

            [JsonProperty("objectiveProgress")]
            public Dictionary<string, int> ObjectiveProgress { get; set; } = new Dictionary<string, int>();

            [JsonProperty("acceptedAt")]
            public DateTime AcceptedAt { get; set; }
        }

        public class QuestCompletionRecord
        {
            [JsonProperty("completionCount")]
            public int CompletionCount { get; set; }

            [JsonProperty("lastCompletedAt")]
            public DateTime LastCompletedAt { get; set; }
        }

        public class QuestData
        {
            [JsonProperty("quests")]
            public List<QuestDefinition> Quests { get; set; } = new List<QuestDefinition>();
        }

        public class NPCConfiguration
        {
            [JsonProperty("id")]
            public string Id { get; set; }

            [JsonProperty("displayName")]
            public string DisplayName { get; set; }

            [JsonProperty("position")]
            public Vector3Data Position { get; set; }

            [JsonProperty("greetingDialogue")]
            public string GreetingDialogue { get; set; }

            [JsonProperty("acceptDialogue")]
            public string AcceptDialogue { get; set; }

            [JsonProperty("completionDialogue")]
            public string CompletionDialogue { get; set; }

            [JsonProperty("voiceFile")]
            public string VoiceFile { get; set; }

            [JsonProperty("assignedQuestIds")]
            public List<string> AssignedQuestIds { get; set; } = new List<string>();
        }

        public class NPCData
        {
            [JsonProperty("npcs")]
            public List<NPCConfiguration> NPCs { get; set; } = new List<NPCConfiguration>();
        }

        public class QuestOperationResult
        {
            public bool Success { get; set; }
            public string ErrorCode { get; set; }
            public string ErrorMessage { get; set; }
            public Dictionary<string, object> Data { get; set; } = new Dictionary<string, object>();

            public static QuestOperationResult Ok(Dictionary<string, object> data = null) => new QuestOperationResult { Success = true, Data = data ?? new Dictionary<string, object>() };
            public static QuestOperationResult Fail(string code, string message) => new QuestOperationResult { Success = false, ErrorCode = code, ErrorMessage = message };
        }

        #endregion


        #region Data Manager

        public class DataManager
        {
            private readonly QuestSystem _plugin;
            private const string QuestDataFile = "QuestSystem/quests";
            private const string NPCDataFile = "QuestSystem/npcs";
            private const string PlayerDataFolder = "QuestSystem/players";

            public DataManager(QuestSystem plugin)
            {
                _plugin = plugin;
            }

            public string Serialize<T>(T data)
            {
                return JsonConvert.SerializeObject(data, Formatting.Indented, new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore,
                    DefaultValueHandling = DefaultValueHandling.Include
                });
            }

            public T Deserialize<T>(string json) where T : new()
            {
                if (string.IsNullOrEmpty(json)) return new T();
                try
                {
                    return JsonConvert.DeserializeObject<T>(json);
                }
                catch (Exception ex)
                {
                    _plugin.PrintError($"Failed to deserialize data: {ex.Message}");
                    return new T();
                }
            }

            public QuestData LoadQuestDefinitions()
            {
                var data = Interface.Oxide.DataFileSystem.ReadObject<QuestData>(QuestDataFile);
                if (data == null || data.Quests == null || data.Quests.Count == 0)
                {
                    data = CreateDefaultQuestData();
                    SaveQuestDefinitions(data);
                }
                return data;
            }

            public void SaveQuestDefinitions(QuestData data)
            {
                Interface.Oxide.DataFileSystem.WriteObject(QuestDataFile, data);
            }

            public QuestData CreateAndSaveDefaultQuestData()
            {
                var data = CreateDefaultQuestData();
                SaveQuestDefinitions(data);
                return data;
            }

            public NPCData LoadNPCData()
            {
                var data = Interface.Oxide.DataFileSystem.ReadObject<NPCData>(NPCDataFile);
                if (data == null)
                {
                    data = new NPCData();
                    SaveNPCData(data);
                }
                return data;
            }

            public void SaveNPCData(NPCData data)
            {
                Interface.Oxide.DataFileSystem.WriteObject(NPCDataFile, data);
            }

            public PlayerQuestData LoadPlayerData(ulong playerId)
            {
                var path = $"{PlayerDataFolder}/{playerId}";
                var data = Interface.Oxide.DataFileSystem.ReadObject<PlayerQuestData>(path);
                if (data == null)
                {
                    data = new PlayerQuestData { PlayerId = playerId };
                }
                return data;
            }

            public void SavePlayerData(PlayerQuestData data)
            {
                var path = $"{PlayerDataFolder}/{data.PlayerId}";
                Interface.Oxide.DataFileSystem.WriteObject(path, data);
            }

            private QuestData CreateDefaultQuestData()
            {
                return new QuestData
                {
                    Quests = new List<QuestDefinition>
                    {
                        // === STARTER QUESTS (Level 1) ===
                        new QuestDefinition
                        {
                            Id = "starter_gather_wood",
                            Name = "The First Harvest",
                            Description = "You've woken up on this island with nothing but the tattered clothes on your back and a rock clutched in your trembling hand. The beach stretches endlessly in both directions, littered with the debris of countless others who washed ashore before you. Some made it. Most didn't. The difference between life and death on this island comes down to one simple truth: those who gather resources survive, and those who don't become food for the wildlife.\n\nWood is the foundation of everything in this harsh world. Without it, you cannot build shelter to protect yourself from the elements and other survivors. Without it, you cannot craft the tools needed to harvest other resources. Without it, you cannot start the fires that cook your food and keep the darkness at bay. Every great fortress, every successful survivor, every legend of this island started with someone just like you, picking up a rock and hitting a tree.\n\nThe forests of this island are plentiful, filled with trees of all sizes waiting to be harvested. Pine trees dot the mountainsides, their wood perfect for building. Palm trees line the beaches, offering easy access for newcomers. Even the fallen logs and driftwood scattered across the landscape can be broken down for precious wood. But be warned - you are not alone in these forests. Other survivors prowl the treelines, some friendly, most not. Animals hunt in the shadows. And the sound of chopping wood carries far, announcing your presence to anyone nearby.\n\n🪓 GATHERING TIPS:\n• Start with the rock in your inventory - it's slow but it works\n• Hit trees repeatedly until they fall, then harvest the fallen log\n• Smaller trees yield less wood but fall faster\n• Driftwood on beaches is easy to gather and relatively safe\n• Upgrade to a stone hatchet as soon as possible for faster gathering\n• Stay alert - the sound of harvesting attracts attention\n\n🏠 WHY THIS MATTERS: With 500 wood, you can craft basic tools, build a small shelter, and start a campfire. This is the first step on your journey from naked survivor to island legend.",
                            Objectives = new List<QuestObjective>
                            {
                                new QuestObjective
                                {
                                    Id = "gather_wood",
                                    Type = ObjectiveType.Gather,
                                    Target = "wood",
                                    RequiredAmount = 500,
                                    Description = "Gather wood from trees"
                                }
                            },
                            Rewards = new List<QuestReward>
                            {
                                new QuestReward { Type = RewardType.Item, ItemShortname = "scrap", Amount = 50 },
                                new QuestReward { Type = RewardType.Item, ItemShortname = "stone.pickaxe", Amount = 1 }
                            },
                            Settings = new QuestDefinitionSettings
                            {
                                IsRepeatable = false,
                                Category = "Starter",
                                XPReward = 50,
                                RequiredLevel = 0
                            }
                        },
                        new QuestDefinition
                        {
                            Id = "starter_gather_stone",
                            Name = "Stone Age Beginnings",
                            Description = "Now that you've gathered some wood, it's time to move beyond the most primitive stage of survival. Stone is the backbone of early progression on this island - the material that separates those who merely survive from those who begin to thrive. With stone, you can craft better tools that harvest faster. With stone, you can build walls that actually provide protection. With stone, you take your first real step toward establishing yourself as a force to be reckoned with.\n\nScattered across the island's terrain, you'll find stone nodes - distinctive grey and brown rocks that jut up from the ground like nature's gift to survivors. These nodes are concentrated in rocky areas, along cliffsides, near mountains, and in the barren patches between biomes. Each node contains a wealth of stone waiting to be extracted, and some even hide bonus resources like metal ore or sulfur within their depths.\n\nMining stone is straightforward but time-consuming with primitive tools. You'll need to strike each node repeatedly, watching as chunks break away with each hit. The sweet spot - a glowing X that appears on the rock - yields bonus resources when struck, so aim carefully. As you mine, you'll notice the distinctive sound echoing across the landscape, a dinner bell for anyone nearby looking for easy prey. Work quickly, stay alert, and always have an escape route planned.\n\n⛏️ MINING STRATEGIES:\n• Look for the glowing 'X' marker on nodes - hitting it gives bonus resources\n• Stone nodes respawn over time, so remember good mining locations\n• Rocky areas and mountains have the highest concentration of nodes\n• Mining is loud - check your surroundings frequently\n• A stone pickaxe mines faster than a rock - craft one as soon as possible\n• Some nodes contain metal ore as a bonus - save it for later!\n\n🏗️ BUILDING YOUR FUTURE: Stone is essential for upgrading your base from flimsy twig to solid stone walls. It's also used in countless crafting recipes. This resource will be your constant companion throughout your time on the island.",
                            Objectives = new List<QuestObjective>
                            {
                                new QuestObjective
                                {
                                    Id = "gather_stone",
                                    Type = ObjectiveType.Gather,
                                    Target = "stones",
                                    RequiredAmount = 300,
                                    Description = "Mine stone from rocks"
                                }
                            },
                            Rewards = new List<QuestReward>
                            {
                                new QuestReward { Type = RewardType.Item, ItemShortname = "scrap", Amount = 50 },
                                new QuestReward { Type = RewardType.Item, ItemShortname = "stone.hatchet", Amount = 1 }
                            },
                            Settings = new QuestDefinitionSettings
                            {
                                IsRepeatable = false,
                                Category = "Starter",
                                XPReward = 50,
                                RequiredLevel = 0
                            }
                        },

                        // === HUNTING QUESTS (Level 1-5) ===
                        new QuestDefinition
                        {
                            Id = "hunter_boar",
                            Name = "Wild Boar Menace",
                            Description = "The wild boars on this island have become increasingly aggressive over the past few weeks. Local survivors at the coastal camps report that a pack of at least a dozen has been destroying vegetable gardens, raiding food storage, and attacking anyone who gets too close to their territory near the southern grasslands.\n\nThese tusked beasts may look like simple pigs, but don't be fooled - they're muscular, fast, and their tusks can gore through flesh with terrifying efficiency. A charging boar can knock a grown man off his feet and trample them before they can react. The old-timers say the boars have been getting bolder since the last wipe, venturing closer to populated areas in search of easy food.\n\nHowever, these dangerous creatures also provide valuable resources for survivors who can take them down. Their meat, when properly cooked over a campfire, provides excellent nutrition for long expeditions. The fat rendered from their bodies can be processed into low grade fuel - essential for furnaces, lanterns, and vehicles. Their leather is thick and durable, perfect for crafting basic armor and clothing.\n\n⚠️ HUNTING TIPS:\n• Boars will charge when threatened - keep your distance and use ranged weapons\n• They have moderate health pools, so a hunting bow with several arrows should suffice\n• Aim for the head for bonus damage\n• If charged, try to sidestep and counterattack while they recover\n• Hunt in pairs if you're new to combat\n\n🎯 REWARD: Successfully culling the boar population will earn you valuable crafting materials and the gratitude of nearby survivors.",
                            Objectives = new List<QuestObjective>
                            {
                                new QuestObjective
                                {
                                    Id = "kill_boars",
                                    Type = ObjectiveType.Kill,
                                    Target = "boar",
                                    RequiredAmount = 5,
                                    Description = "Hunt wild boars"
                                }
                            },
                            Rewards = new List<QuestReward>
                            {
                                new QuestReward { Type = RewardType.Item, ItemShortname = "scrap", Amount = 100 },
                                new QuestReward { Type = RewardType.Item, ItemShortname = "leather", Amount = 50 },
                                new QuestReward { Type = RewardType.Item, ItemShortname = "bone.fragments", Amount = 100 }
                            },
                            Settings = new QuestDefinitionSettings
                            {
                                IsRepeatable = true,
                                CooldownSeconds = 3600,
                                Category = "Hunting",
                                XPReward = 75,
                                RequiredLevel = 1
                            }
                        },
                        new QuestDefinition
                        {
                            Id = "hunter_deer",
                            Name = "The Great Deer Hunt",
                            Description = "The majestic deer of this island are a sight to behold - graceful creatures with impressive antlers that roam the grasslands and forest edges in small herds. Unlike the aggressive boars, deer are peaceful herbivores that pose no direct threat to survivors. However, their skittish nature makes them one of the most challenging prey for inexperienced hunters.\n\nDeer have evolved to be hyper-aware of their surroundings. Their large ears can detect the slightest sound, and their keen eyes spot movement from incredible distances. At the first sign of danger - a snapping twig, a shadow moving wrong, or the scent of a human on the wind - they bolt with surprising speed, bounding away in graceful leaps that can cover vast distances in seconds.\n\nFor those patient and skilled enough to hunt them, deer provide some of the finest resources on the island. Their meat is lean and nutritious, with a delicate flavor that's prized among survivors who've grown tired of the gamey taste of boar. The leather from their hides is softer and more supple than boar leather, making it ideal for comfortable clothing and flexible armor components. Even their antlers and bones can be harvested for crafting.\n\n💡 HUNTING STRATEGIES:\n• Approach slowly and crouch to minimize noise - deer can hear footsteps from far away\n• Use the terrain to your advantage - approach from downwind so they can't smell you\n• A hunting bow is ideal for silent takedowns - firearms will scare away the entire herd\n• Aim carefully - you may only get one shot before they flee\n• Early morning and dusk are the best hunting times when deer are most active\n• Look for them near water sources and in meadows with tall grass\n\n🏹 CHALLENGE: This hunt will test your patience and stealth skills. Only true hunters can consistently bring down these elusive creatures.",
                            Objectives = new List<QuestObjective>
                            {
                                new QuestObjective
                                {
                                    Id = "kill_deer",
                                    Type = ObjectiveType.Kill,
                                    Target = "stag",
                                    RequiredAmount = 3,
                                    Description = "Hunt deer"
                                }
                            },
                            Rewards = new List<QuestReward>
                            {
                                new QuestReward { Type = RewardType.Item, ItemShortname = "scrap", Amount = 75 },
                                new QuestReward { Type = RewardType.Item, ItemShortname = "cloth", Amount = 50 },
                                new QuestReward { Type = RewardType.Item, ItemShortname = "leather", Amount = 30 }
                            },
                            Settings = new QuestDefinitionSettings
                            {
                                IsRepeatable = true,
                                CooldownSeconds = 2400,
                                Category = "Hunting",
                                XPReward = 60,
                                RequiredLevel = 1
                            }
                        },
                        new QuestDefinition
                        {
                            Id = "hunter_wolf",
                            Name = "Alpha Predator",
                            Description = "Wolves are the apex predators of the wilderness, and they've earned that title through millions of years of evolution into perfect killing machines. These cunning beasts hunt in coordinated packs, communicate through haunting howls that echo across the valleys, and show absolutely no fear of humans. In fact, to a hungry wolf pack, you're just another meal.\n\nSeveral survivors have gone missing near the northern forests over the past month. Search parties found only scattered equipment, bloodstains, and wolf tracks circling the areas. The pack responsible has grown bold, venturing closer to established camps and picking off lone travelers. Something needs to be done before more lives are lost.\n\nWolves are incredibly dangerous opponents. They're fast - faster than any human can run - and they attack with savage ferocity. Their teeth can tear through basic clothing like paper, and they often attack in groups, with one wolf distracting the prey while others flank from the sides. A single wolf is manageable for an armed survivor, but facing a pack is a death sentence for the unprepared.\n\nHowever, wolf pelts are highly valued for their warmth and durability. Wolf meat, while tough, is nutritious and filling. And there's a certain prestige among survivors who can claim to have faced down these predators and lived.\n\n⚠️ CRITICAL WARNINGS:\n• Wolves deal significant damage and attack relentlessly - they don't retreat easily\n• They often hunt in pairs or small packs - killing one may attract others\n• Their speed means you cannot outrun them - stand and fight or find high ground\n• Bring medical supplies - bandages and syringes are essential\n• A melee weapon is risky; ranged weapons give you crucial distance\n• Listen for howls - they signal wolf presence in the area\n\n☠️ DANGER LEVEL: HIGH\nThis quest is recommended for survivors with combat experience and proper equipment. Bring armor, weapons, and healing items. Consider hunting with a partner.",
                            Objectives = new List<QuestObjective>
                            {
                                new QuestObjective
                                {
                                    Id = "kill_wolves",
                                    Type = ObjectiveType.Kill,
                                    Target = "wolf",
                                    RequiredAmount = 3,
                                    Description = "Eliminate wolves"
                                }
                            },
                            Rewards = new List<QuestReward>
                            {
                                new QuestReward { Type = RewardType.Item, ItemShortname = "scrap", Amount = 150 },
                                new QuestReward { Type = RewardType.Item, ItemShortname = "wolfmeat.raw", Amount = 10 },
                                new QuestReward { Type = RewardType.Item, ItemShortname = "skull.wolf", Amount = 1 }
                            },
                            Settings = new QuestDefinitionSettings
                            {
                                IsRepeatable = true,
                                CooldownSeconds = 5400,
                                Category = "Hunting",
                                XPReward = 150,
                                RequiredLevel = 3
                            }
                        },
                        new QuestDefinition
                        {
                            Id = "hunter_bear",
                            Name = "The Bear Necessities",
                            Description = "Deep in the eastern mountains, where the trees grow thick and the shadows run deep, there lurks a terror that has claimed more lives than any other creature on this island. The grizzly bears of this region are not the gentle giants of children's stories - they are massive, territorial killing machines that view humans as either threats to be eliminated or prey to be consumed.\n\nOver the past several weeks, a particularly large and aggressive bear has been terrorizing the mountain passes. This beast - survivors have taken to calling it 'Old Scar' due to the distinctive marks across its face - has killed at least seven people, destroyed a small mining outpost, and driven prospectors away from some of the richest ore deposits on the island. The bounty on its head has grown substantial, attracting hunters from across the server.\n\nBears are, without question, the most dangerous animals you will encounter. Standing over 8 feet tall when reared up on their hind legs, they possess incredible strength capable of crushing a human skull with a single swipe. Their thick fur and layers of fat act as natural armor, absorbing damage that would fell lesser creatures. Despite their bulk, they can sprint at terrifying speeds, easily outpacing a running human. And unlike wolves, bears don't need a pack - a single bear is more than capable of killing multiple armed survivors.\n\nThe rewards for bringing down a bear are equally impressive. Bear meat is rich and fatty, providing excellent nutrition. Their fat can be rendered into massive quantities of low grade fuel. The thick hide makes exceptional leather for armor. And the skull? Well, that's a trophy that commands respect from every survivor who sees it.\n\n☠️ EXTREME DANGER - READ CAREFULLY:\n• Bears have MASSIVE health pools - expect to use significant ammunition\n• Their attacks deal devastating damage - even good armor won't save you from multiple hits\n• They are FAST - do not try to outrun a bear, you will fail\n• Elevation is your friend - bears struggle with steep terrain and can't climb\n• Bring your BEST weapons - primitive weapons are nearly useless\n• Stock up on medical supplies - syringes, medkits, bandages\n• Consider hunting with a group - solo bear hunting is extremely risky\n• If a bear charges, don't panic - steady aim and continuous fire is your only hope\n\n🏆 PRESTIGE HUNT: Successfully killing bears marks you as one of the island's elite hunters. This is not a quest for beginners - only attempt this if you're well-equipped and experienced in combat.",
                            Objectives = new List<QuestObjective>
                            {
                                new QuestObjective
                                {
                                    Id = "kill_bears",
                                    Type = ObjectiveType.Kill,
                                    Target = "bear",
                                    RequiredAmount = 2,
                                    Description = "Slay bears"
                                }
                            },
                            Rewards = new List<QuestReward>
                            {
                                new QuestReward { Type = RewardType.Item, ItemShortname = "scrap", Amount = 300 },
                                new QuestReward { Type = RewardType.Item, ItemShortname = "fat.animal", Amount = 100 },
                                new QuestReward { Type = RewardType.Item, ItemShortname = "bearmeat", Amount = 20 },
                                new QuestReward { Type = RewardType.Item, ItemShortname = "skull.human", Amount = 1 }
                            },
                            Settings = new QuestDefinitionSettings
                            {
                                IsRepeatable = true,
                                CooldownSeconds = 7200,
                                Category = "Hunting",
                                XPReward = 250,
                                RequiredLevel = 5
                            }
                        },

                        // === CRAFTING QUESTS (Level 1-3) ===
                        new QuestDefinition
                        {
                            Id = "crafter_tools",
                            Name = "Tools of the Trade",
                            Description = "A survivor without proper tools is as good as dead on this island. Those primitive stone implements you've been using? They're barely better than using your bare hands. They break constantly, harvest resources at a painfully slow rate, and mark you as a fresh spawn to every predator - human and animal alike. It's time to graduate from the stone age and enter the era of metal.\n\nMetal tools represent a quantum leap in efficiency and durability. A metal hatchet will harvest wood nearly twice as fast as its stone counterpart, and it lasts significantly longer before breaking. A metal pickaxe tears through stone and ore nodes with satisfying speed, extracting more resources per swing. These aren't just upgrades - they're necessities for any survivor who wants to progress beyond scraping by on the beach.\n\nCrafting metal tools requires several steps that teach you the fundamentals of the island's crafting system. First, you'll need to gather metal ore from the reddish-brown nodes scattered across the map. Then, you'll need to smelt that ore in a furnace, combining it with wood to produce metal fragments. Finally, with those fragments and some additional wood, you can craft your new tools at a workbench. It's a process that every successful survivor has mastered.\n\n🔧 CRAFTING REQUIREMENTS:\n• Metal Hatchet: 100 metal fragments + 50 wood (requires Workbench Level 1)\n• Metal Pickaxe: 125 metal fragments + 50 wood (requires Workbench Level 1)\n• You'll need a furnace to smelt metal ore into fragments\n• Furnaces require 50 low grade fuel, 200 stone, and 100 wood to craft\n\n⚡ EFFICIENCY GAINS:\n• Metal hatchet gathers ~40% more wood per tree than stone\n• Metal pickaxe extracts ~50% more ore per node than stone\n• Both tools last 3-4 times longer than stone equivalents\n• Faster gathering means less time exposed and vulnerable\n\n📈 PROGRESSION TIP: These tools are your gateway to mid-game content. With efficient gathering, you can stockpile resources for base building, weapon crafting, and research.",
                            Objectives = new List<QuestObjective>
                            {
                                new QuestObjective
                                {
                                    Id = "craft_hatchet",
                                    Type = ObjectiveType.Craft,
                                    Target = "hatchet",
                                    RequiredAmount = 1,
                                    Description = "Craft a metal hatchet"
                                },
                                new QuestObjective
                                {
                                    Id = "craft_pickaxe",
                                    Type = ObjectiveType.Craft,
                                    Target = "pickaxe",
                                    RequiredAmount = 1,
                                    Description = "Craft a metal pickaxe"
                                }
                            },
                            Rewards = new List<QuestReward>
                            {
                                new QuestReward { Type = RewardType.Item, ItemShortname = "metal.fragments", Amount = 200 },
                                new QuestReward { Type = RewardType.Item, ItemShortname = "scrap", Amount = 75 }
                            },
                            Settings = new QuestDefinitionSettings
                            {
                                IsRepeatable = false,
                                Category = "Crafting",
                                XPReward = 100,
                                RequiredLevel = 1
                            }
                        },
                        new QuestDefinition
                        {
                            Id = "crafter_weapons",
                            Name = "Armed and Dangerous",
                            Description = "This island doesn't care about your feelings, your plans, or your desire to live peacefully. Every moment you spend here unarmed is a moment you're gambling with your life. The wildlife sees you as food. Other survivors see you as a resource to be harvested. Without weapons, you're not a survivor - you're a victim waiting to happen. It's time to change that.\n\nThe hunting bow is one of the most versatile weapons available to early-game survivors. Silent, deadly at medium range, and craftable with basic materials, it's been the weapon of choice for hunters since the dawn of humanity. On this island, a skilled archer can take down animals for food, defend against aggressive players, and do it all without announcing their position to everyone within earshot. Unlike firearms, bows don't echo across the landscape, making them perfect for survivors who prefer stealth over confrontation.\n\nThe wooden spear serves a different but equally important role. It's your last line of defense when enemies get too close for arrows, your tool for finishing off wounded prey, and your constant companion in close-quarters combat. A well-timed spear thrust can mean the difference between life and death when a wolf is charging at your throat or a hostile player rounds a corner unexpectedly.\n\n🏹 BOW MECHANICS:\n• Arrows have travel time - lead your targets at distance\n• Arrows drop over distance due to gravity - aim high for long shots\n• Headshots deal bonus damage - practice your aim\n• Craft bone arrows later for increased damage\n• The bow is nearly silent - perfect for stealth hunting\n\n🗡️ SPEAR TACTICS:\n• Right-click to throw the spear for ranged damage\n• Left-click for quick melee jabs\n• Thrown spears can be retrieved from bodies and the ground\n• Keep a backup spear - thrown spears can be lost\n• Effective against animals that get too close\n\n⚔️ COMBAT WISDOM: These primitive weapons may seem basic compared to firearms, but many veteran survivors still carry a bow for silent operations. Master these weapons now, and you'll have skills that serve you throughout your time on the island.",
                            Objectives = new List<QuestObjective>
                            {
                                new QuestObjective
                                {
                                    Id = "craft_bow",
                                    Type = ObjectiveType.Craft,
                                    Target = "bow.hunting",
                                    RequiredAmount = 1,
                                    Description = "Craft a hunting bow"
                                },
                                new QuestObjective
                                {
                                    Id = "craft_arrows",
                                    Type = ObjectiveType.Craft,
                                    Target = "arrow.wooden",
                                    RequiredAmount = 20,
                                    Description = "Craft wooden arrows"
                                },
                                new QuestObjective
                                {
                                    Id = "craft_spear",
                                    Type = ObjectiveType.Craft,
                                    Target = "spear.wooden",
                                    RequiredAmount = 1,
                                    Description = "Craft a wooden spear"
                                }
                            },
                            Rewards = new List<QuestReward>
                            {
                                new QuestReward { Type = RewardType.Item, ItemShortname = "arrow.wooden", Amount = 50 },
                                new QuestReward { Type = RewardType.Item, ItemShortname = "scrap", Amount = 100 }
                            },
                            Settings = new QuestDefinitionSettings
                            {
                                IsRepeatable = false,
                                Category = "Crafting",
                                XPReward = 100,
                                RequiredLevel = 1
                            }
                        },
                        new QuestDefinition
                        {
                            Id = "crafter_armor",
                            Name = "Suit Up",
                            Description = "Let's address the elephant in the room - or rather, the naked survivor running across the beach. You've probably noticed that you spawned on this island wearing nothing but your underwear. While some survivors embrace the 'natural' lifestyle, the smart ones understand that clothing isn't just about modesty - it's about survival. Every piece of armor between your skin and the dangers of this world is another chance to live through an encounter that would otherwise kill you.\n\nThe elements alone can kill an unprotected survivor. Cold nights in the mountains will drain your health as hypothermia sets in. The scorching desert sun will cook you alive without shade or protection. Rain chills you to the bone, and radiation zones will melt your insides without proper gear. But the elements are just the beginning - animal attacks, player weapons, and environmental hazards all deal reduced damage when you're properly armored.\n\nBurlap clothing represents the first step on the armor progression ladder. Crafted from simple cloth harvested from hemp plants, these rough garments won't win any fashion awards, but they provide crucial protection against the cold and minor damage reduction against attacks. They're cheap, easy to craft, and infinitely better than running around naked. Think of them as your training wheels - essential for learning the importance of armor before you graduate to more advanced protection.\n\n👕 ARMOR BASICS:\n• All armor has different protection values for different damage types\n• Projectile protection reduces bullet and arrow damage\n• Melee protection reduces damage from melee weapons and animals\n• Cold protection helps you survive in cold biomes\n• Radiation protection is essential for monument runs\n\n🧵 CLOTH GATHERING:\n• Hemp plants (tall green plants) yield cloth when harvested\n• Found commonly in grasslands and forests\n• Each plant gives 10-20 cloth\n• Cloth is also used for sleeping bags and medical items\n\n📈 ARMOR PROGRESSION:\n• Burlap → Hide/Bone → Road Sign → Metal\n• Each tier offers significantly better protection\n• Higher tier armor requires more resources and workbench levels\n• Always wear the best armor you can afford to lose",
                            Objectives = new List<QuestObjective>
                            {
                                new QuestObjective
                                {
                                    Id = "craft_pants",
                                    Type = ObjectiveType.Craft,
                                    Target = "burlap.trousers",
                                    RequiredAmount = 1,
                                    Description = "Craft burlap trousers"
                                },
                                new QuestObjective
                                {
                                    Id = "craft_shirt",
                                    Type = ObjectiveType.Craft,
                                    Target = "burlap.shirt",
                                    RequiredAmount = 1,
                                    Description = "Craft a burlap shirt"
                                }
                            },
                            Rewards = new List<QuestReward>
                            {
                                new QuestReward { Type = RewardType.Item, ItemShortname = "cloth", Amount = 100 },
                                new QuestReward { Type = RewardType.Item, ItemShortname = "sewingkit", Amount = 2 }
                            },
                            Settings = new QuestDefinitionSettings
                            {
                                IsRepeatable = false,
                                Category = "Crafting",
                                XPReward = 75,
                                RequiredLevel = 1
                            }
                        },

                        // === GATHERING QUESTS (Level 2-5) ===
                        new QuestDefinition
                        {
                            Id = "gather_metal",
                            Name = "Metal Mania",
                            Description = "Metal is the currency of progress on this island. It's the material that separates primitive survivors scraping by with stone tools from established players with fortified bases and deadly weapons. Without a steady supply of metal, you'll be forever stuck in the early game while others around you advance to firearms, armored bases, and vehicles. It's time to get serious about mining.\n\nMetal ore nodes are scattered across the entire island, but they're not distributed evenly. You'll find the highest concentrations in rocky areas, along cliff faces, near mountain bases, and in the barren transitional zones between biomes. The nodes themselves are distinctive - look for rocks with a reddish-brown coloration that stands out from the grey stone nodes. Each metal node contains a substantial amount of ore, and hitting the glowing sweet spot will yield bonus resources.\n\nOnce you've gathered the ore, you'll need to process it in a furnace. Smelting converts raw metal ore into usable metal fragments at a ratio that depends on your furnace efficiency. A small furnace works for beginners, but serious miners eventually upgrade to large furnaces that can process massive quantities simultaneously. The smelting process requires wood as fuel, so plan your resource gathering accordingly.\n\n⛏️ MINING EFFICIENCY:\n• Always hit the glowing 'X' sweet spot for bonus ore\n• Metal pickaxe is minimum recommended - stone is too slow\n• Salvaged pickaxe and jackhammer are significantly faster\n• Mining quarries can passively generate metal ore\n• Night mining is risky but nodes are easier to spot\n\n🔥 SMELTING RATIOS:\n• 1 metal ore = 1 metal fragment (in furnace)\n• Small furnace holds 6 slots, large furnace holds 18\n• Wood burns at consistent rate regardless of ore amount\n• Smelt in bulk to maximize fuel efficiency\n\n⚠️ SAFETY TIPS: Mining is loud and time-consuming, making you vulnerable. Check your surroundings frequently, have an escape route planned, and consider mining with a partner who can watch your back.",
                            Objectives = new List<QuestObjective>
                            {
                                new QuestObjective
                                {
                                    Id = "gather_metal",
                                    Type = ObjectiveType.Gather,
                                    Target = "metal.ore",
                                    RequiredAmount = 500,
                                    Description = "Mine metal ore"
                                }
                            },
                            Rewards = new List<QuestReward>
                            {
                                new QuestReward { Type = RewardType.Item, ItemShortname = "metal.fragments", Amount = 300 },
                                new QuestReward { Type = RewardType.Item, ItemShortname = "scrap", Amount = 100 }
                            },
                            Settings = new QuestDefinitionSettings
                            {
                                IsRepeatable = true,
                                CooldownSeconds = 3600,
                                Category = "Gathering",
                                XPReward = 100,
                                RequiredLevel = 2
                            }
                        },
                        new QuestDefinition
                        {
                            Id = "gather_sulfur",
                            Name = "Explosive Potential",
                            Description = "If metal is the currency of progress, then sulfur is the currency of power. This bright yellow mineral is the key ingredient in gunpowder, which means it's essential for crafting ammunition, explosives, and raiding tools. Without sulfur, your guns are useless paperweights. Without sulfur, you can't breach enemy walls. Without sulfur, you're at the mercy of everyone who has it. On this island, sulfur is power, and power is survival.\n\nSulfur nodes are impossible to miss once you know what to look for. They're bright, almost neon yellow rocks that practically glow against the landscape. You'll find them scattered across the map, but they're particularly abundant in desert biomes and arctic regions. The competition for sulfur is fierce - experienced players know its value and will fight to control sulfur-rich areas. Many of the island's bloodiest conflicts have been fought over sulfur spawns.\n\nThe economics of sulfur are brutal. Crafting a single rocket requires 1,400 sulfur. A stack of 5.56 ammo needs 250 sulfur. Raiding even a small base can consume thousands of sulfur worth of explosives. This means you'll need to mine constantly to maintain any kind of offensive capability. Smart survivors establish bases near sulfur-rich areas and set up mining operations that run around the clock.\n\n💥 SULFUR ECONOMICS:\n• 1 sulfur ore = 1 sulfur (when smelted)\n• Gunpowder: 30 charcoal + 20 sulfur\n• Explosives: 50 gunpowder + 3 sulfur + 10 metal fragments\n• Rocket: 10 explosives + 150 gunpowder (1,400 sulfur total)\n• C4: 20 explosives + 5 cloth + 2 tech trash (2,200 sulfur total)\n\n🎯 STRATEGIC VALUE:\n• Control of sulfur = control of raiding capability\n• Sulfur-rich areas are high-conflict zones\n• Bank your sulfur frequently - dying with a full inventory is devastating\n• Consider trading excess sulfur for other resources\n\n⚠️ HIGH RISK: Carrying large amounts of sulfur makes you a prime target. Other players will kill you just for the chance you might have sulfur. Mine smart, bank often, and never carry more than you can afford to lose.",
                            Objectives = new List<QuestObjective>
                            {
                                new QuestObjective
                                {
                                    Id = "gather_sulfur",
                                    Type = ObjectiveType.Gather,
                                    Target = "sulfur.ore",
                                    RequiredAmount = 500,
                                    Description = "Mine sulfur ore"
                                }
                            },
                            Rewards = new List<QuestReward>
                            {
                                new QuestReward { Type = RewardType.Item, ItemShortname = "sulfur", Amount = 300 },
                                new QuestReward { Type = RewardType.Item, ItemShortname = "gunpowder", Amount = 100 }
                            },
                            Settings = new QuestDefinitionSettings
                            {
                                IsRepeatable = true,
                                CooldownSeconds = 3600,
                                Category = "Gathering",
                                XPReward = 125,
                                RequiredLevel = 3
                            }
                        },
                        new QuestDefinition
                        {
                            Id = "gather_hqm",
                            Name = "High Quality Dreams",
                            Description = "At the top of the resource hierarchy sits high quality metal - the rarest, most valuable ore on the entire island. While regular metal is common enough that any survivor can accumulate stacks of it, HQM is precious enough that veteran players count every single piece. It's the material of endgame gear, the stuff that separates the elite from everyone else. If you want the best weapons, the strongest armor, and the most secure base components, you need HQM. Lots of it.\n\nHigh quality metal ore appears as small, shiny deposits that glint in the light. You'll occasionally find trace amounts while mining regular metal nodes - those little bonus drops that make you smile. But for serious HQM farming, you need to seek out dedicated HQM nodes. These are rarer than regular metal nodes and tend to spawn in specific areas - rocky outcrops, mountain peaks, and certain monument locations. Learning the HQM spawn points on your server's map is knowledge worth its weight in... well, high quality metal.\n\nThe smelting ratio for HQM is punishing compared to regular metal. You'll burn through significant amounts of ore to produce relatively small quantities of refined HQM. This scarcity is intentional - it ensures that top-tier gear remains valuable and that players must invest serious time and effort to acquire it. A full set of metal armor, an AK-47, and a few armored doors can easily consume hundreds of HQM.\n\n💎 HQM APPLICATIONS:\n• Armored building tier (strongest base protection)\n• High-tier weapons (AK-47, LR-300, M249)\n• Metal armor set (best craftable protection)\n• Auto turrets and other advanced deployables\n• High-end tools and equipment\n\n⛏️ FARMING STRATEGIES:\n• Learn HQM node spawn locations on your map\n• Recycling certain components yields HQM\n• Mining quarries can be configured for HQM\n• Some monuments have guaranteed HQM spawns\n• Trading with other players can supplement mining\n\n🏆 ELITE RESOURCE: Accumulating 100 HQM ore demonstrates serious dedication to the grind. This quest marks your transition from mid-game survivor to late-game contender. Guard your HQM jealously - it's worth killing for.",
                            Objectives = new List<QuestObjective>
                            {
                                new QuestObjective
                                {
                                    Id = "gather_hqm",
                                    Type = ObjectiveType.Gather,
                                    Target = "hq.metal.ore",
                                    RequiredAmount = 100,
                                    Description = "Mine high quality metal ore"
                                }
                            },
                            Rewards = new List<QuestReward>
                            {
                                new QuestReward { Type = RewardType.Item, ItemShortname = "metal.refined", Amount = 50 },
                                new QuestReward { Type = RewardType.Item, ItemShortname = "scrap", Amount = 200 }
                            },
                            Settings = new QuestDefinitionSettings
                            {
                                IsRepeatable = true,
                                CooldownSeconds = 7200,
                                Category = "Gathering",
                                XPReward = 200,
                                RequiredLevel = 5
                            }
                        },

                        // === BUILDING QUESTS (Level 2-4) ===
                        new QuestDefinition
                        {
                            Id = "builder_foundation",
                            Name = "Laying the Foundation",
                            Description = "Every survivor eventually faces the same realization: you can't live on the beach forever. The constant threat of other players, the need to store resources safely, the desire for a place to respawn when things go wrong - all of these push you toward the same inevitable conclusion. You need a base. You need walls between yourself and the dangers of this world. You need a home.\n\nBuilding in Rust is both an art and a science. The basics are simple enough - craft a building plan and hammer, gather resources, and start placing structures. But the nuances of base design can take hundreds of hours to master. Where you build matters enormously - too close to a monument and you'll face constant traffic, too far from resources and you'll spend all your time traveling. The shape of your base affects how easily it can be raided. The placement of your doors determines whether you can be door-camped. Every decision has consequences.\n\nFor this quest, you'll learn the fundamentals by constructing a simple starter base. Four foundations give you enough floor space for essential crafting stations and storage. Eight walls provide the enclosure you need for basic security. It's not a fortress - that comes later - but it's a crucial first step toward establishing yourself on the island. A place to call your own, however humble.\n\n🏗️ BUILDING BASICS:\n• Craft a Building Plan (wood) and Hammer (wood)\n• Building Plan places new structures, Hammer upgrades/repairs them\n• Structures start as 'Twig' - very weak, upgrade immediately\n• Wood costs 200 wood per piece, Stone costs 300 stone\n• Place a Tool Cupboard (TC) to claim building privilege\n\n⚠️ CRITICAL WARNINGS:\n• Twig structures can be destroyed by ANYONE with basic tools\n• Upgrade to at least Wood immediately, Stone as soon as possible\n• Without a TC, anyone can build in your base\n• Doors require locks - craft a key lock or code lock\n• Always have a sleeping bag inside for respawning\n\n📐 STARTER BASE TIPS:\n• 2x2 (4 foundations) is the classic starter size\n• Airlock (double doors) prevents easy door camping\n• Place TC in a secure location, not right by the door\n• Honeycomb (extra layers of walls) adds raid cost\n• Start small, expand as you gather more resources",
                            Objectives = new List<QuestObjective>
                            {
                                new QuestObjective
                                {
                                    Id = "build_foundation",
                                    Type = ObjectiveType.Build,
                                    Target = "foundation",
                                    RequiredAmount = 4,
                                    Description = "Place foundations"
                                },
                                new QuestObjective
                                {
                                    Id = "build_wall",
                                    Type = ObjectiveType.Build,
                                    Target = "wall",
                                    RequiredAmount = 8,
                                    Description = "Build walls"
                                }
                            },
                            Rewards = new List<QuestReward>
                            {
                                new QuestReward { Type = RewardType.Item, ItemShortname = "wood", Amount = 2000 },
                                new QuestReward { Type = RewardType.Item, ItemShortname = "metal.fragments", Amount = 500 }
                            },
                            Settings = new QuestDefinitionSettings
                            {
                                IsRepeatable = false,
                                Category = "Building",
                                XPReward = 150,
                                RequiredLevel = 2
                            }
                        },

                        // === EXPLORATION QUESTS (Level 3+) ===
                        new QuestDefinition
                        {
                            Id = "loot_barrels",
                            Name = "Scavenger's Paradise",
                            Description = "Before the island became the lawless survival arena it is today, it was something else. The rusting infrastructure, abandoned vehicles, and scattered debris tell the story of a civilization that once thrived here before everything fell apart. Now, the remnants of that old world are scattered across the landscape in the form of barrels, crates, and containers - each one a potential treasure trove of valuable resources waiting to be claimed by survivors bold enough to seek them out.\n\nBarrels are the most common form of lootable container on the island. These weathered metal drums are found along roadsides, near monuments, floating in the ocean, and scattered throughout the wilderness. Each barrel contains a random assortment of items - scrap metal for researching blueprints, components for crafting advanced items, and occasionally weapons or tools. The contents are randomized, so every barrel is a small gamble. Most will contain modest rewards, but occasionally you'll crack one open to find something truly valuable.\n\nThe art of barrel farming is about efficiency and risk management. Roadsides offer easy access but heavy traffic from other players. Monuments have higher concentrations of barrels but also more danger from scientists, radiation, and PvP. Ocean barrels require a boat but face less competition. Smart scavengers learn the respawn patterns, plan efficient routes, and always stay alert for threats.\n\n🛢️ BARREL LOCATIONS:\n• Roadsides - easy access, moderate density, high player traffic\n• Monuments - high density, dangerous (scientists, radiation, PvP)\n• Ocean - requires boat, low competition, spread out\n• Junk piles - random spawns in wilderness areas\n• Underwater - diving gear required, unique loot tables\n\n♻️ RECYCLING TIP:\n• Components from barrels can be recycled for raw materials\n• Recyclers are found at most monuments\n• Gears, pipes, springs = metal fragments\n• Tech trash = scrap and HQM\n• Prioritize recycling over hoarding components\n\n⚠️ SCAVENGING SAFETY:\n• Barrel farming makes noise - stay alert\n• Other players target barrel farmers for easy kills\n• Don't carry more than you can afford to lose\n• Bank frequently at your base\n• Travel light for faster movement and escape",
                            Objectives = new List<QuestObjective>
                            {
                                new QuestObjective
                                {
                                    Id = "loot_barrels",
                                    Type = ObjectiveType.Loot,
                                    Target = "loot-barrel",
                                    RequiredAmount = 20,
                                    Description = "Loot barrels"
                                }
                            },
                            Rewards = new List<QuestReward>
                            {
                                new QuestReward { Type = RewardType.Item, ItemShortname = "scrap", Amount = 150 },
                                new QuestReward { Type = RewardType.Item, ItemShortname = "metal.fragments", Amount = 200 }
                            },
                            Settings = new QuestDefinitionSettings
                            {
                                IsRepeatable = true,
                                CooldownSeconds = 1800,
                                Category = "Exploration",
                                XPReward = 100,
                                RequiredLevel = 2
                            }
                        }
                    }
                };
            }
        }

        #endregion


        #region Progress Manager

        public class ProgressManager
        {
            private readonly QuestSystem _plugin;
            private readonly DataManager _dataManager;
            private readonly Dictionary<ulong, PlayerQuestData> _playerData = new Dictionary<ulong, PlayerQuestData>();

            public ProgressManager(QuestSystem plugin, DataManager dataManager)
            {
                _plugin = plugin;
                _dataManager = dataManager;
            }

            public void LoadPlayerData(ulong playerId)
            {
                if (!_playerData.ContainsKey(playerId))
                {
                    _playerData[playerId] = _dataManager.LoadPlayerData(playerId);
                }
            }

            public void SavePlayerData(ulong playerId)
            {
                if (_playerData.TryGetValue(playerId, out var data))
                {
                    _dataManager.SavePlayerData(data);
                }
            }

            public PlayerQuestData GetPlayerData(ulong playerId)
            {
                if (!_playerData.TryGetValue(playerId, out var data))
                {
                    data = _dataManager.LoadPlayerData(playerId);
                    _playerData[playerId] = data;
                }
                return data;
            }

            public QuestProgress GetProgress(ulong playerId, string questId)
            {
                var playerData = GetPlayerData(playerId);
                playerData.ActiveQuests.TryGetValue(questId, out var progress);
                return progress;
            }

            public QuestProgress CreateProgress(ulong playerId, QuestDefinition quest)
            {
                var playerData = GetPlayerData(playerId);
                var progress = new QuestProgress
                {
                    QuestId = quest.Id,
                    Status = QuestStatus.Active,
                    AcceptedAt = DateTime.UtcNow,
                    ObjectiveProgress = new Dictionary<string, int>()
                };

                foreach (var objective in quest.Objectives)
                {
                    progress.ObjectiveProgress[objective.Id] = 0;
                }

                playerData.ActiveQuests[quest.Id] = progress;
                return progress;
            }

            public void UpdateProgress(ulong playerId, string questId, string objectiveId, int amount, QuestDefinition quest)
            {
                var progress = GetProgress(playerId, questId);
                if (progress == null || progress.Status != QuestStatus.Active) return;

                if (progress.ObjectiveProgress.ContainsKey(objectiveId))
                {
                    progress.ObjectiveProgress[objectiveId] += amount;

                    var objective = quest.Objectives.FirstOrDefault(o => o.Id == objectiveId);
                    if (objective != null && progress.ObjectiveProgress[objectiveId] >= objective.RequiredAmount)
                    {
                        progress.ObjectiveProgress[objectiveId] = objective.RequiredAmount;
                    }

                    CheckQuestCompletion(playerId, questId, quest);
                }
            }

            public void CheckQuestCompletion(ulong playerId, string questId, QuestDefinition quest)
            {
                var progress = GetProgress(playerId, questId);
                if (progress == null || progress.Status != QuestStatus.Active) return;

                var allComplete = quest.Objectives.All(obj =>
                {
                    var current = progress.ObjectiveProgress.GetValueOrDefault(obj.Id, 0);
                    return current >= obj.RequiredAmount;
                });

                if (allComplete)
                {
                    progress.Status = QuestStatus.ReadyToComplete;
                }
            }

            public bool IsQuestComplete(ulong playerId, string questId, QuestDefinition quest)
            {
                var progress = GetProgress(playerId, questId);
                if (progress == null) return false;

                return quest.Objectives.All(obj =>
                {
                    var current = progress.ObjectiveProgress.GetValueOrDefault(obj.Id, 0);
                    return current >= obj.RequiredAmount;
                });
            }

            public void CompleteQuest(ulong playerId, string questId)
            {
                var playerData = GetPlayerData(playerId);
                if (playerData.ActiveQuests.TryGetValue(questId, out var progress))
                {
                    playerData.ActiveQuests.Remove(questId);

                    if (!playerData.CompletedQuests.TryGetValue(questId, out var record))
                    {
                        record = new QuestCompletionRecord();
                        playerData.CompletedQuests[questId] = record;
                    }

                    record.CompletionCount++;
                    record.LastCompletedAt = DateTime.UtcNow;
                }
            }

            public void RemoveQuest(ulong playerId, string questId)
            {
                var playerData = GetPlayerData(playerId);
                playerData.ActiveQuests.Remove(questId);
            }

            public bool HasCompletedQuest(ulong playerId, string questId)
            {
                var playerData = GetPlayerData(playerId);
                return playerData.CompletedQuests.ContainsKey(questId);
            }

            public int GetCompletionCount(ulong playerId, string questId)
            {
                var playerData = GetPlayerData(playerId);
                if (playerData.CompletedQuests.TryGetValue(questId, out var record))
                {
                    return record.CompletionCount;
                }
                return 0;
            }

            public DateTime? GetLastCompletionTime(ulong playerId, string questId)
            {
                var playerData = GetPlayerData(playerId);
                if (playerData.CompletedQuests.TryGetValue(questId, out var record))
                {
                    return record.LastCompletedAt;
                }
                return null;
            }

            public List<QuestProgress> GetActiveQuests(ulong playerId)
            {
                var playerData = GetPlayerData(playerId);
                return playerData.ActiveQuests.Values.ToList();
            }

            // XP System Methods
            public int GetPlayerLevel(ulong playerId)
            {
                var playerData = GetPlayerData(playerId);
                return playerData.Level;
            }

            public int GetPlayerXP(ulong playerId)
            {
                var playerData = GetPlayerData(playerId);
                return playerData.XP;
            }

            public int GetXPForLevel(int level)
            {
                var baseXP = _plugin._config.XP.BaseXPForLevel;
                var multiplier = _plugin._config.XP.XPPerLevelMultiplier;
                return (int)(baseXP * Math.Pow(multiplier, level - 1));
            }

            public int GetXPToNextLevel(ulong playerId)
            {
                var playerData = GetPlayerData(playerId);
                var xpNeeded = GetXPForLevel(playerData.Level + 1);
                return Math.Max(0, xpNeeded - playerData.XP);
            }

            public void AddXP(ulong playerId, int amount)
            {
                if (!_plugin._config.XP.Enabled) return;
                
                var playerData = GetPlayerData(playerId);
                playerData.XP += amount;

                // Check for level up
                var maxLevel = _plugin._config.XP.MaxLevel;
                while (playerData.Level < maxLevel)
                {
                    var xpNeeded = GetXPForLevel(playerData.Level + 1);
                    if (playerData.XP >= xpNeeded)
                    {
                        playerData.Level++;
                        var player = BasePlayer.FindByID(playerId);
                        if (player != null)
                        {
                            _plugin.SendReply(player, $"<color=#FFD700>Level Up!</color> You are now level {playerData.Level}!");
                        }
                    }
                    else
                    {
                        break;
                    }
                }
            }
        }

        #endregion


        #region Quest Manager

        public class QuestManager
        {
            private readonly QuestSystem _plugin;
            private QuestData _questData;
            private readonly ProgressManager _progressManager;
            private readonly RewardManager _rewardManager;
            private readonly NPCManager _npcManager;
            private readonly MapMarkerManager _mapMarkerManager;

            public QuestManager(QuestSystem plugin, QuestData questData, ProgressManager progressManager, RewardManager rewardManager, NPCManager npcManager, MapMarkerManager mapMarkerManager)
            {
                _plugin = plugin;
                _questData = questData;
                _progressManager = progressManager;
                _rewardManager = rewardManager;
                _npcManager = npcManager;
                _mapMarkerManager = mapMarkerManager;
            }

            public void ReloadQuestData(QuestData newData)
            {
                _questData = newData;
            }

            public QuestDefinition GetQuest(string questId)
            {
                return _questData.Quests.FirstOrDefault(q => q.Id == questId);
            }

            public List<QuestDefinition> GetAllQuests()
            {
                return _questData.Quests;
            }

            public List<QuestDefinition> GetAvailableQuests(BasePlayer player)
            {
                var available = new List<QuestDefinition>();
                var playerData = _progressManager.GetPlayerData(player.userID);

                foreach (var quest in _questData.Quests)
                {
                    if (playerData.ActiveQuests.ContainsKey(quest.Id)) continue;
                    if (!CanAcceptQuest(player, quest).Success) continue;
                    available.Add(quest);
                }

                return available;
            }

            public List<QuestProgress> GetActiveQuests(BasePlayer player)
            {
                return _progressManager.GetActiveQuests(player.userID);
            }

            public QuestOperationResult CanAcceptQuest(BasePlayer player, QuestDefinition quest)
            {
                var playerId = player.userID;

                // Check if already active
                if (_progressManager.GetProgress(playerId, quest.Id) != null)
                {
                    return QuestOperationResult.Fail("ALREADY_ACTIVE", "Quest is already active.");
                }

                // Check max completions
                if (quest.Settings.MaxCompletions > 0)
                {
                    var completions = _progressManager.GetCompletionCount(playerId, quest.Id);
                    if (completions >= quest.Settings.MaxCompletions)
                    {
                        return QuestOperationResult.Fail("MAX_COMPLETIONS", "Maximum completions reached for this quest.");
                    }
                }

                // Check cooldown
                if (quest.Settings.IsRepeatable && quest.Settings.CooldownSeconds > 0)
                {
                    var lastCompletion = _progressManager.GetLastCompletionTime(playerId, quest.Id);
                    if (lastCompletion.HasValue)
                    {
                        var cooldownEnd = lastCompletion.Value.AddSeconds(quest.Settings.CooldownSeconds);
                        if (DateTime.UtcNow < cooldownEnd)
                        {
                            var remaining = cooldownEnd - DateTime.UtcNow;
                            return QuestOperationResult.Fail("ON_COOLDOWN", $"Quest is on cooldown. {FormatTimeSpan(remaining)} remaining.");
                        }
                    }
                }

                // Check if not repeatable and already completed
                if (!quest.Settings.IsRepeatable && _progressManager.HasCompletedQuest(playerId, quest.Id))
                {
                    return QuestOperationResult.Fail("ALREADY_COMPLETED", "Quest has already been completed.");
                }

                // Check prerequisites
                if (quest.Settings.PrerequisiteQuestIds != null && quest.Settings.PrerequisiteQuestIds.Count > 0)
                {
                    foreach (var prereqId in quest.Settings.PrerequisiteQuestIds)
                    {
                        if (!_progressManager.HasCompletedQuest(playerId, prereqId))
                        {
                            var prereqQuest = GetQuest(prereqId);
                            var prereqName = prereqQuest?.Name ?? prereqId;
                            return QuestOperationResult.Fail("MISSING_PREREQUISITE", $"Must complete '{prereqName}' first.");
                        }
                    }
                }

                // Check level requirement (placeholder - integrate with level system)
                if (quest.Settings.RequiredLevel > 0)
                {
                    var playerLevel = GetPlayerLevel(player);
                    if (playerLevel < quest.Settings.RequiredLevel)
                    {
                        return QuestOperationResult.Fail("LEVEL_TOO_LOW", $"Requires level {quest.Settings.RequiredLevel}.");
                    }
                }

                return QuestOperationResult.Ok();
            }

            public QuestOperationResult AcceptQuest(BasePlayer player, string questId)
            {
                var quest = GetQuest(questId);
                if (quest == null)
                {
                    return QuestOperationResult.Fail("QUEST_NOT_FOUND", "Quest not found.");
                }

                var canAccept = CanAcceptQuest(player, quest);
                if (!canAccept.Success)
                {
                    return canAccept;
                }

                _progressManager.CreateProgress(player.userID, quest);
                
                // Create map markers for location-based objectives
                _mapMarkerManager?.CreateMarkersForQuest(player, quest);
                
                // Emit event
                Interface.CallHook("OnQuestAccepted", player, questId);
                
                _plugin.SendReply(player, $"Quest accepted: {quest.Name}");
                return QuestOperationResult.Ok();
            }

            public QuestOperationResult CompleteQuest(BasePlayer player, string questId)
            {
                var quest = GetQuest(questId);
                if (quest == null)
                {
                    return QuestOperationResult.Fail("QUEST_NOT_FOUND", "Quest not found.");
                }

                var progress = _progressManager.GetProgress(player.userID, questId);
                if (progress == null)
                {
                    return QuestOperationResult.Fail("QUEST_NOT_ACTIVE", "Quest is not active.");
                }

                if (progress.Status != QuestStatus.ReadyToComplete)
                {
                    if (!_progressManager.IsQuestComplete(player.userID, questId, quest))
                    {
                        return QuestOperationResult.Fail("QUEST_NOT_COMPLETE", "Quest objectives are not complete.");
                    }
                }

                // Grant rewards
                _rewardManager.GrantRewards(player, quest.Rewards);

                // Grant XP
                if (quest.Settings.XPReward > 0)
                {
                    _progressManager.AddXP(player.userID, quest.Settings.XPReward);
                    _plugin.SendReply(player, $"<color=#00FF00>+{quest.Settings.XPReward} XP</color>");
                }

                // Mark as completed
                _progressManager.CompleteQuest(player.userID, questId);
                
                // Remove map markers
                _mapMarkerManager?.RemoveAllMarkersForQuest(player, questId);

                // Emit event
                Interface.CallHook("OnQuestCompleted", player, questId);

                _plugin.SendReply(player, $"Quest completed: {quest.Name}");
                return QuestOperationResult.Ok();
            }

            public QuestOperationResult AbandonQuest(BasePlayer player, string questId)
            {
                var progress = _progressManager.GetProgress(player.userID, questId);
                if (progress == null)
                {
                    return QuestOperationResult.Fail("QUEST_NOT_ACTIVE", "Quest is not active.");
                }

                _progressManager.RemoveQuest(player.userID, questId);
                
                // Remove map markers
                _mapMarkerManager?.RemoveAllMarkersForQuest(player, questId);

                // Emit event
                Interface.CallHook("OnQuestAbandoned", player, questId);

                var quest = GetQuest(questId);
                _plugin.SendReply(player, $"Quest abandoned: {quest?.Name ?? questId}");
                return QuestOperationResult.Ok();
            }

            public void ProcessObjectiveEvent(BasePlayer player, ObjectiveType type, string target, int amount)
            {
                var activeQuests = _progressManager.GetActiveQuests(player.userID);
                
                foreach (var progress in activeQuests)
                {
                    if (progress.Status != QuestStatus.Active) continue;

                    var quest = GetQuest(progress.QuestId);
                    if (quest == null) continue;

                    foreach (var objective in quest.Objectives)
                    {
                        if (objective.Type != type) continue;
                        if (!MatchesTarget(objective.Target, target)) continue;

                        var currentProgress = progress.ObjectiveProgress.GetValueOrDefault(objective.Id, 0);
                        if (currentProgress >= objective.RequiredAmount) continue;

                        _progressManager.UpdateProgress(player.userID, quest.Id, objective.Id, amount, quest);

                        // Emit progress event
                        Interface.CallHook("OnQuestProgressUpdated", player, quest.Id, objective.Id, currentProgress + amount);
                    }
                }
            }

            private bool MatchesTarget(string objectiveTarget, string actualTarget)
            {
                if (string.IsNullOrEmpty(objectiveTarget)) return false;
                if (string.IsNullOrEmpty(actualTarget)) return false;
                
                return actualTarget.Contains(objectiveTarget, StringComparison.OrdinalIgnoreCase) ||
                       objectiveTarget.Contains(actualTarget, StringComparison.OrdinalIgnoreCase) ||
                       objectiveTarget.Equals(actualTarget, StringComparison.OrdinalIgnoreCase);
            }

            private int GetPlayerLevel(BasePlayer player)
            {
                // Use built-in XP system
                if (_plugin._config.XP.Enabled)
                {
                    return _progressManager.GetPlayerLevel(player.userID);
                }
                return 1;
            }

            // Admin Quest Management Methods
            public void DeleteQuest(string questId)
            {
                var quest = _questData.Quests.FirstOrDefault(q => q.Id == questId);
                if (quest != null)
                {
                    _questData.Quests.Remove(quest);
                    _plugin._dataManager.SaveQuestDefinitions(_questData);
                }
            }

            public void SaveQuestBasicInfo(string questId, string name, string description, string category, int xpReward, int requiredLevel)
            {
                var quest = _questData.Quests.FirstOrDefault(q => q.Id == questId);
                if (quest == null)
                {
                    quest = new QuestDefinition
                    {
                        Id = questId,
                        Objectives = new List<QuestObjective>(),
                        Rewards = new List<QuestReward>(),
                        Settings = new QuestDefinitionSettings()
                    };
                    _questData.Quests.Add(quest);
                }

                quest.Name = name;
                quest.Description = description;
                quest.Settings.Category = category;
                quest.Settings.XPReward = xpReward;
                quest.Settings.RequiredLevel = requiredLevel;

                _plugin._dataManager.SaveQuestDefinitions(_questData);
            }

            public void AddObjectiveToQuest(string questId, string objType, string target, int amount, string description)
            {
                var quest = _questData.Quests.FirstOrDefault(q => q.Id == questId);
                if (quest == null) return;

                if (!Enum.TryParse<ObjectiveType>(objType, true, out var type)) return;

                var objId = $"obj_{quest.Objectives.Count + 1}";
                quest.Objectives.Add(new QuestObjective
                {
                    Id = objId,
                    Type = type,
                    Target = target,
                    RequiredAmount = amount,
                    Description = description
                });

                _plugin._dataManager.SaveQuestDefinitions(_questData);
            }

            public void AddRewardToQuest(string questId, string rewardType, string item, int amount)
            {
                var quest = _questData.Quests.FirstOrDefault(q => q.Id == questId);
                if (quest == null) return;

                if (!Enum.TryParse<RewardType>(rewardType, true, out var type)) return;

                quest.Rewards.Add(new QuestReward
                {
                    Type = type,
                    ItemShortname = item,
                    Amount = amount
                });

                _plugin._dataManager.SaveQuestDefinitions(_questData);
            }

            private string FormatTimeSpan(TimeSpan ts)
            {
                if (ts.TotalHours >= 1)
                    return $"{(int)ts.TotalHours}h {ts.Minutes}m";
                if (ts.TotalMinutes >= 1)
                    return $"{ts.Minutes}m {ts.Seconds}s";
                return $"{ts.Seconds}s";
            }
        }

        #endregion


        #region Reward Manager

        public class RewardManager
        {
            private readonly QuestSystem _plugin;
            private readonly IntegrationManager _integrationManager;

            public RewardManager(QuestSystem plugin, IntegrationManager integrationManager)
            {
                _plugin = plugin;
                _integrationManager = integrationManager;
            }

            public void GrantRewards(BasePlayer player, List<QuestReward> rewards)
            {
                foreach (var reward in rewards)
                {
                    switch (reward.Type)
                    {
                        case RewardType.Item:
                            GrantItemReward(player, reward);
                            break;
                        case RewardType.Blueprint:
                            GrantBlueprintReward(player, reward);
                            break;
                        case RewardType.Currency:
                            GrantCurrencyReward(player, reward);
                            break;
                        case RewardType.Command:
                            ExecuteCommandReward(player, reward);
                            break;
                        case RewardType.SkillPoints:
                            GrantSkillPointsReward(player, reward);
                            break;
                        case RewardType.Experience:
                            GrantExperienceReward(player, reward);
                            break;
                    }
                }
            }

            private void GrantItemReward(BasePlayer player, QuestReward reward)
            {
                var item = ItemManager.CreateByName(reward.ItemShortname, reward.Amount, reward.SkinId);
                if (item == null)
                {
                    _plugin.PrintWarning($"Failed to create item: {reward.ItemShortname}");
                    return;
                }

                if (!player.inventory.GiveItem(item))
                {
                    item.Drop(player.transform.position + Vector3.up, Vector3.zero);
                    _plugin.SendReply(player, "Inventory full! Item dropped at your feet.");
                }
            }

            private void GrantBlueprintReward(BasePlayer player, QuestReward reward)
            {
                var itemDef = ItemManager.FindItemDefinition(reward.BlueprintShortname ?? reward.ItemShortname);
                if (itemDef == null)
                {
                    _plugin.PrintWarning($"Failed to find blueprint item: {reward.BlueprintShortname ?? reward.ItemShortname}");
                    return;
                }

                if (!player.blueprints.HasUnlocked(itemDef))
                {
                    player.blueprints.Unlock(itemDef);
                    _plugin.SendReply(player, $"Blueprint unlocked: {itemDef.displayName.english}");
                }
            }

            private void GrantCurrencyReward(BasePlayer player, QuestReward reward)
            {
                if (_integrationManager.IsEconomicsAvailable())
                {
                    _integrationManager.DepositCurrency(player, reward.Amount);
                }
                else
                {
                    _plugin.PrintWarning("Economics plugin not available for currency reward.");
                }
            }

            private void ExecuteCommandReward(BasePlayer player, QuestReward reward)
            {
                if (string.IsNullOrEmpty(reward.Command)) return;

                var command = reward.Command
                    .Replace("{player.id}", player.UserIDString)
                    .Replace("{player.name}", player.displayName)
                    .Replace("{player.x}", player.transform.position.x.ToString())
                    .Replace("{player.y}", player.transform.position.y.ToString())
                    .Replace("{player.z}", player.transform.position.z.ToString());

                _plugin.Server.Command(command);
            }

            private void GrantSkillPointsReward(BasePlayer player, QuestReward reward)
            {
                if (_integrationManager.IsSkillTreeAvailable())
                {
                    _integrationManager.AddSkillPoints(player, reward.Amount);
                }
                else
                {
                    _plugin.PrintWarning("SkillTree plugin not available for skill points reward.");
                }
            }

            private void GrantExperienceReward(BasePlayer player, QuestReward reward)
            {
                if (_integrationManager.IsZLevelsAvailable())
                {
                    _integrationManager.AddExperience(player, reward.Amount);
                }
                else
                {
                    _plugin.PrintWarning("ZLevels plugin not available for experience reward.");
                }
            }

            public bool CanReceiveRewards(BasePlayer player, List<QuestReward> rewards)
            {
                return player != null && player.IsConnected;
            }
        }

        #endregion

        #region Integration Manager

        public class IntegrationManager
        {
            private readonly QuestSystem _plugin;

            [PluginReference] private Plugin Economics;
            [PluginReference] private Plugin SkillTree;
            [PluginReference] private Plugin ZLevels;

            public IntegrationManager(QuestSystem plugin)
            {
                _plugin = plugin;
                Economics = plugin.plugins.Find("Economics");
                SkillTree = plugin.plugins.Find("SkillTree");
                ZLevels = plugin.plugins.Find("ZLevels");
            }

            public bool IsEconomicsAvailable() => Economics != null;
            public bool IsSkillTreeAvailable() => SkillTree != null;
            public bool IsZLevelsAvailable() => ZLevels != null;

            public void DepositCurrency(BasePlayer player, double amount)
            {
                if (!IsEconomicsAvailable()) return;
                Economics?.Call("Deposit", player.UserIDString, amount);
            }

            public double GetBalance(BasePlayer player)
            {
                if (!IsEconomicsAvailable()) return 0;
                return (double)(Economics?.Call("Balance", player.UserIDString) ?? 0);
            }

            public void AddSkillPoints(BasePlayer player, int amount)
            {
                if (!IsSkillTreeAvailable()) return;
                SkillTree?.Call("GiveSkillPoints", player.userID, amount);
            }

            public void AddExperience(BasePlayer player, int amount)
            {
                if (!IsZLevelsAvailable()) return;
                ZLevels?.Call("AddXP", player.userID, amount);
            }

            public int GetPlayerLevel(BasePlayer player)
            {
                if (IsZLevelsAvailable())
                {
                    return (int)(ZLevels?.Call("GetLevel", player.userID) ?? 1);
                }
                return 1;
            }
        }

        #endregion


        #region NPC Manager

        public class NPCManager
        {
            private readonly QuestSystem _plugin;
            private readonly DataManager _dataManager;
            private NPCData _npcData;

            public NPCManager(QuestSystem plugin, DataManager dataManager)
            {
                _plugin = plugin;
                _dataManager = dataManager;
                _npcData = _dataManager.LoadNPCData();
            }

            public NPCConfiguration GetNPC(string npcId)
            {
                return _npcData.NPCs.FirstOrDefault(n => n.Id == npcId);
            }

            public List<string> GetNPCQuestIds(string npcId)
            {
                var npc = GetNPC(npcId);
                return npc?.AssignedQuestIds ?? new List<string>();
            }

            public string GetGreetingDialogue(string npcId)
            {
                var npc = GetNPC(npcId);
                return npc?.GreetingDialogue ?? "Hello, traveler!";
            }

            public string GetAcceptDialogue(string npcId)
            {
                var npc = GetNPC(npcId);
                return npc?.AcceptDialogue ?? "Good luck on your quest!";
            }

            public string GetCompletionDialogue(string npcId)
            {
                var npc = GetNPC(npcId);
                return npc?.CompletionDialogue ?? "Well done! Here is your reward.";
            }

            public void PlayVoice(BasePlayer player, string npcId, string dialogueType)
            {
                var npc = GetNPC(npcId);
                if (npc == null || string.IsNullOrEmpty(npc.VoiceFile)) return;
                // Voice playback would be implemented here using Rust's audio system
            }

            public List<NPCConfiguration> GetAllNPCs()
            {
                return _npcData.NPCs;
            }

            public void RegisterNPC(NPCConfiguration config)
            {
                var existing = _npcData.NPCs.FirstOrDefault(n => n.Id == config.Id);
                if (existing != null)
                {
                    _npcData.NPCs.Remove(existing);
                }
                _npcData.NPCs.Add(config);
                _dataManager.SaveNPCData(_npcData);
            }
        }

        #endregion


        #region Map Marker Manager

        public class MapMarkerManager
        {
            private readonly QuestSystem _plugin;
            private readonly Dictionary<ulong, Dictionary<string, MapMarker>> _playerMarkers = new Dictionary<ulong, Dictionary<string, MapMarker>>();

            public MapMarkerManager(QuestSystem plugin)
            {
                _plugin = plugin;
            }

            public void CreateMarkersForQuest(BasePlayer player, QuestDefinition quest)
            {
                if (!_playerMarkers.ContainsKey(player.userID))
                {
                    _playerMarkers[player.userID] = new Dictionary<string, MapMarker>();
                }

                foreach (var objective in quest.Objectives)
                {
                    if (objective.Location == null) continue;

                    var markerId = $"{quest.Id}_{objective.Id}";
                    if (_playerMarkers[player.userID].ContainsKey(markerId)) continue;

                    var marker = CreateMapMarker(player, objective.Location.ToVector3(), objective.Description);
                    if (marker != null)
                    {
                        _playerMarkers[player.userID][markerId] = marker;
                    }
                }
            }

            public void RemoveMarkerForObjective(BasePlayer player, string questId, string objectiveId)
            {
                if (!_playerMarkers.TryGetValue(player.userID, out var markers)) return;

                var markerId = $"{questId}_{objectiveId}";
                if (markers.TryGetValue(markerId, out var marker))
                {
                    if (marker != null && !marker.IsDestroyed)
                    {
                        marker.Kill();
                    }
                    markers.Remove(markerId);
                }
            }

            public void RemoveAllMarkersForQuest(BasePlayer player, string questId)
            {
                if (!_playerMarkers.TryGetValue(player.userID, out var markers)) return;

                var keysToRemove = markers.Keys.Where(k => k.StartsWith($"{questId}_")).ToList();
                foreach (var key in keysToRemove)
                {
                    if (markers.TryGetValue(key, out var marker))
                    {
                        if (marker != null && !marker.IsDestroyed)
                        {
                            marker.Kill();
                        }
                    }
                    markers.Remove(key);
                }
            }

            public void RemoveAllMarkersForPlayer(BasePlayer player)
            {
                if (!_playerMarkers.TryGetValue(player.userID, out var markers)) return;

                foreach (var marker in markers.Values)
                {
                    if (marker != null && !marker.IsDestroyed)
                    {
                        marker.Kill();
                    }
                }
                markers.Clear();
            }

            public List<Vector3> GetMarkerLocationsForQuest(string questId, QuestDefinition quest)
            {
                var locations = new List<Vector3>();
                foreach (var objective in quest.Objectives)
                {
                    if (objective.Location != null)
                    {
                        locations.Add(objective.Location.ToVector3());
                    }
                }
                return locations;
            }

            public void CreateNPCMarker(BasePlayer player, NPCConfiguration npc)
            {
                if (npc?.Position == null) return;

                if (!_playerMarkers.ContainsKey(player.userID))
                {
                    _playerMarkers[player.userID] = new Dictionary<string, MapMarker>();
                }

                var markerId = $"npc_{npc.Id}";
                if (_playerMarkers[player.userID].ContainsKey(markerId)) return;

                var marker = CreateMapMarker(player, npc.Position.ToVector3(), $"Quest Giver: {npc.DisplayName}");
                if (marker != null)
                {
                    _playerMarkers[player.userID][markerId] = marker;
                }
            }

            private MapMarker CreateMapMarker(BasePlayer player, Vector3 position, string name)
            {
                try
                {
                    var marker = GameManager.server.CreateEntity("assets/prefabs/tools/map/genericradiusmarker.prefab", position) as MapMarker;
                    if (marker != null)
                    {
                        marker.Spawn();
                        marker.SendNetworkUpdate();
                        return marker;
                    }
                }
                catch (Exception ex)
                {
                    _plugin.PrintWarning($"Failed to create map marker: {ex.Message}");
                }
                return null;
            }
        }

        #endregion


        #region UI Manager

        public class UIManager
        {
            private readonly QuestSystem _plugin;
            private readonly QuestManager _questManager;
            private readonly ProgressManager _progressManager;

            private const string MainPanel = "QuestSystem_Main";
            private const string MiniPanel = "QuestSystem_Mini";

            private readonly Dictionary<ulong, string> _selectedTab = new Dictionary<ulong, string>();
            private readonly Dictionary<ulong, string> _selectedQuest = new Dictionary<ulong, string>();

            public UIManager(QuestSystem plugin, QuestManager questManager, ProgressManager progressManager)
            {
                _plugin = plugin;
                _questManager = questManager;
                _progressManager = progressManager;
            }

            public void ShowQuestUI(BasePlayer player)
            {
                DestroyUI(player);
                
                if (!_selectedTab.ContainsKey(player.userID))
                    _selectedTab[player.userID] = "available";

                var container = new CuiElementContainer();

                // Main background panel
                container.Add(new CuiPanel
                {
                    Image = { Color = "0 0 0 0.95" },
                    RectTransform = { AnchorMin = "0.15 0.15", AnchorMax = "0.85 0.85" },
                    CursorEnabled = true
                }, "Overlay", MainPanel);

                // Header
                container.Add(new CuiPanel
                {
                    Image = { Color = "0.2 0.2 0.2 1" },
                    RectTransform = { AnchorMin = "0 0.92", AnchorMax = "1 1" }
                }, MainPanel, $"{MainPanel}_Header");

                container.Add(new CuiLabel
                {
                    Text = { Text = "QUEST SYSTEM", FontSize = 20, Align = TextAnchor.MiddleCenter, Color = "1 0.8 0.2 1" },
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
                }, $"{MainPanel}_Header");

                // Close button
                container.Add(new CuiButton
                {
                    Button = { Color = "0.8 0.2 0.2 1", Command = "quest.ui.close" },
                    RectTransform = { AnchorMin = "0.95 0.3", AnchorMax = "0.99 0.7" },
                    Text = { Text = "X", FontSize = 14, Align = TextAnchor.MiddleCenter }
                }, $"{MainPanel}_Header");

                // Admin button (only for admins)
                if (_plugin.permission.UserHasPermission(player.UserIDString, "questsystem.admin"))
                {
                    container.Add(new CuiButton
                    {
                        Button = { Color = "0.8 0.5 0.1 1", Command = "quest.ui.admin" },
                        RectTransform = { AnchorMin = "0.01 0.3", AnchorMax = "0.08 0.7" },
                        Text = { Text = "⚙ ADMIN", FontSize = 10, Align = TextAnchor.MiddleCenter }
                    }, $"{MainPanel}_Header");
                }

                // Player Level & XP display
                if (_plugin._config.XP.Enabled)
                {
                    var playerLevel = _progressManager.GetPlayerLevel(player.userID);
                    var playerXP = _progressManager.GetPlayerXP(player.userID);
                    var xpToNext = _progressManager.GetXPToNextLevel(player.userID);
                    var xpForNext = _progressManager.GetXPForLevel(playerLevel + 1);
                    var xpProgress = xpForNext > 0 ? (float)(playerXP - _progressManager.GetXPForLevel(playerLevel)) / (xpForNext - _progressManager.GetXPForLevel(playerLevel)) : 0;
                    xpProgress = Math.Max(0, Math.Min(1, xpProgress));

                    container.Add(new CuiPanel
                    {
                        Image = { Color = "0.15 0.15 0.15 1" },
                        RectTransform = { AnchorMin = "0.7 0.89", AnchorMax = "0.99 0.91" }
                    }, MainPanel, $"{MainPanel}_LevelBar");

                    container.Add(new CuiLabel
                    {
                        Text = { Text = $"Level {playerLevel}", FontSize = 10, Align = TextAnchor.MiddleLeft, Color = "1 0.8 0.2 1" },
                        RectTransform = { AnchorMin = "0.01 0", AnchorMax = "0.25 1" }
                    }, $"{MainPanel}_LevelBar");

                    // XP bar background
                    container.Add(new CuiPanel
                    {
                        Image = { Color = "0.2 0.2 0.2 1" },
                        RectTransform = { AnchorMin = "0.26 0.2", AnchorMax = "0.85 0.8" }
                    }, $"{MainPanel}_LevelBar", $"{MainPanel}_XPBarBg");

                    // XP bar fill
                    if (xpProgress > 0)
                    {
                        container.Add(new CuiPanel
                        {
                            Image = { Color = "0.2 0.6 0.8 1" },
                            RectTransform = { AnchorMin = "0 0", AnchorMax = $"{xpProgress} 1" }
                        }, $"{MainPanel}_XPBarBg");
                    }

                    container.Add(new CuiLabel
                    {
                        Text = { Text = $"{xpToNext} XP", FontSize = 8, Align = TextAnchor.MiddleRight, Color = "0.7 0.7 0.7 1" },
                        RectTransform = { AnchorMin = "0.86 0", AnchorMax = "0.99 1" }
                    }, $"{MainPanel}_LevelBar");
                }

                // Tab buttons
                var currentTab = _selectedTab.GetValueOrDefault(player.userID, "available");
                AddTabButton(container, "Available", "available", currentTab == "available", 0);
                AddTabButton(container, "Active", "active", currentTab == "active", 1);

                // Left panel - Quest list
                container.Add(new CuiPanel
                {
                    Image = { Color = "0.1 0.1 0.1 1" },
                    RectTransform = { AnchorMin = "0.01 0.02", AnchorMax = "0.35 0.88" }
                }, MainPanel, $"{MainPanel}_QuestList");

                container.Add(new CuiLabel
                {
                    Text = { Text = "Quest List", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0.8 0.8 0.8 1" },
                    RectTransform = { AnchorMin = "0 0.95", AnchorMax = "1 1" }
                }, $"{MainPanel}_QuestList");

                // Populate quest list
                var quests = currentTab == "available" 
                    ? _questManager.GetAvailableQuests(player).Select(q => (q, (QuestProgress)null)).ToList()
                    : _questManager.GetActiveQuests(player).Select(p => (_questManager.GetQuest(p.QuestId), p)).Where(x => x.Item1 != null).ToList();

                var yOffset = 0.9f;
                foreach (var (quest, progress) in quests.Take(10))
                {
                    var statusColor = GetStatusColor(progress);
                    var isSelected = _selectedQuest.GetValueOrDefault(player.userID) == quest.Id;
                    
                    container.Add(new CuiButton
                    {
                        Button = { Color = isSelected ? "0.3 0.5 0.3 1" : "0.15 0.15 0.15 1", Command = $"quest.ui.select {quest.Id}" },
                        RectTransform = { AnchorMin = $"0.02 {yOffset - 0.08f}", AnchorMax = $"0.98 {yOffset}" },
                        Text = { Text = "", FontSize = 12, Align = TextAnchor.MiddleLeft }
                    }, $"{MainPanel}_QuestList", $"{MainPanel}_Quest_{quest.Id}");

                    // Quest name with level indicator
                    var levelText = quest.Settings.RequiredLevel > 0 ? $" [Lv.{quest.Settings.RequiredLevel}]" : "";
                    var nameColor = quest.Settings.RequiredLevel > _progressManager.GetPlayerLevel(player.userID) ? "0.6 0.6 0.6 1" : "1 1 1 1";
                    
                    container.Add(new CuiLabel
                    {
                        Text = { Text = quest.Name + levelText, FontSize = 11, Align = TextAnchor.MiddleLeft, Color = nameColor },
                        RectTransform = { AnchorMin = "0.05 0.5", AnchorMax = "0.85 1" }
                    }, $"{MainPanel}_Quest_{quest.Id}");

                    var statusText = progress == null ? "Available" : (progress.Status == QuestStatus.ReadyToComplete ? "Complete!" : "In Progress");
                    var xpText = quest.Settings.XPReward > 0 ? $" +{quest.Settings.XPReward}XP" : "";
                    container.Add(new CuiLabel
                    {
                        Text = { Text = statusText + xpText, FontSize = 9, Align = TextAnchor.MiddleLeft, Color = statusColor },
                        RectTransform = { AnchorMin = "0.05 0", AnchorMax = "0.85 0.5" }
                    }, $"{MainPanel}_Quest_{quest.Id}");

                    yOffset -= 0.09f;
                }

                // Right panel - Quest details
                container.Add(new CuiPanel
                {
                    Image = { Color = "0.1 0.1 0.1 1" },
                    RectTransform = { AnchorMin = "0.36 0.02", AnchorMax = "0.99 0.88" }
                }, MainPanel, $"{MainPanel}_Details");

                var selectedQuestId = _selectedQuest.GetValueOrDefault(player.userID);
                if (!string.IsNullOrEmpty(selectedQuestId))
                {
                    AddQuestDetails(container, player, selectedQuestId);
                }
                else
                {
                    container.Add(new CuiLabel
                    {
                        Text = { Text = "Select a quest to view details", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0.5 0.5 0.5 1" },
                        RectTransform = { AnchorMin = "0 0.4", AnchorMax = "1 0.6" }
                    }, $"{MainPanel}_Details");
                }

                CuiHelper.AddUi(player, container);
            }

            private void AddTabButton(CuiElementContainer container, string label, string tabId, bool isActive, int index)
            {
                var xMin = 0.01f + (index * 0.12f);
                var xMax = xMin + 0.11f;
                
                container.Add(new CuiButton
                {
                    Button = { Color = isActive ? "0.3 0.6 0.3 1" : "0.2 0.2 0.2 1", Command = $"quest.ui.tab {tabId}" },
                    RectTransform = { AnchorMin = $"{xMin} 0.89", AnchorMax = $"{xMax} 0.91" },
                    Text = { Text = label, FontSize = 11, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
                }, MainPanel);
            }

            private void AddQuestDetails(CuiElementContainer container, BasePlayer player, string questId)
            {
                var quest = _questManager.GetQuest(questId);
                if (quest == null) return;

                var progress = _progressManager.GetProgress(player.userID, questId);
                var isActive = progress != null;

                // Quest name
                container.Add(new CuiLabel
                {
                    Text = { Text = quest.Name, FontSize = 18, Align = TextAnchor.MiddleLeft, Color = "1 0.8 0.2 1" },
                    RectTransform = { AnchorMin = "0.02 0.9", AnchorMax = "0.98 0.98" }
                }, $"{MainPanel}_Details");

                // Category and settings
                var settingsText = $"Category: {quest.Settings.Category}";
                if (quest.Settings.IsRepeatable) settingsText += " | Repeatable";
                if (quest.Settings.CooldownSeconds > 0) settingsText += $" | Cooldown: {FormatSeconds(quest.Settings.CooldownSeconds)}";

                container.Add(new CuiLabel
                {
                    Text = { Text = settingsText, FontSize = 10, Align = TextAnchor.MiddleLeft, Color = "0.6 0.6 0.6 1" },
                    RectTransform = { AnchorMin = "0.02 0.85", AnchorMax = "0.98 0.9" }
                }, $"{MainPanel}_Details");

                // Description
                container.Add(new CuiLabel
                {
                    Text = { Text = quest.Description, FontSize = 12, Align = TextAnchor.UpperLeft, Color = "0.9 0.9 0.9 1" },
                    RectTransform = { AnchorMin = "0.02 0.65", AnchorMax = "0.98 0.84" }
                }, $"{MainPanel}_Details");

                // Objectives header
                container.Add(new CuiLabel
                {
                    Text = { Text = "Objectives", FontSize = 14, Align = TextAnchor.MiddleLeft, Color = "0.8 0.8 0.8 1" },
                    RectTransform = { AnchorMin = "0.02 0.58", AnchorMax = "0.98 0.64" }
                }, $"{MainPanel}_Details");

                // Objectives list
                var objY = 0.55f;
                foreach (var objective in quest.Objectives)
                {
                    var currentAmount = progress?.ObjectiveProgress.GetValueOrDefault(objective.Id, 0) ?? 0;
                    var isComplete = currentAmount >= objective.RequiredAmount;
                    var checkmark = isComplete ? "✓ " : "○ ";
                    var objColor = isComplete ? "0.3 0.8 0.3 1" : "0.9 0.9 0.9 1";

                    // Objective icon - use item icons (works reliably with Rust's native sprites)
                    var itemDef = ItemManager.FindItemDefinition(objective.Target);
                    var entityItemId = GetEntityItemId(objective.Target);
                    
                    if (itemDef != null)
                    {
                        // Use item icon for gather/craft objectives
                        AddItemIcon(container, $"{MainPanel}_Details", $"{MainPanel}_ObjIcon_{objective.Id}", 
                            itemDef.itemid, $"0.02 {objY - 0.05f}", $"0.06 {objY + 0.01f}");

                        container.Add(new CuiLabel
                        {
                            Text = { Text = $"{checkmark}{objective.Description}", FontSize = 11, Align = TextAnchor.MiddleLeft, Color = objColor },
                            RectTransform = { AnchorMin = $"0.07 {objY - 0.04f}", AnchorMax = $"0.7 {objY}" }
                        }, $"{MainPanel}_Details");
                    }
                    else if (entityItemId > 0)
                    {
                        // Use related item icon for entities (meat for animals, etc.)
                        AddItemIcon(container, $"{MainPanel}_Details", $"{MainPanel}_ObjIcon_{objective.Id}", 
                            entityItemId, $"0.02 {objY - 0.05f}", $"0.06 {objY + 0.01f}");

                        container.Add(new CuiLabel
                        {
                            Text = { Text = $"{checkmark}{objective.Description}", FontSize = 11, Align = TextAnchor.MiddleLeft, Color = objColor },
                            RectTransform = { AnchorMin = $"0.07 {objY - 0.04f}", AnchorMax = $"0.7 {objY}" }
                        }, $"{MainPanel}_Details");
                    }
                    else
                    {
                        container.Add(new CuiLabel
                        {
                            Text = { Text = $"{checkmark}{objective.Description}", FontSize = 11, Align = TextAnchor.MiddleLeft, Color = objColor },
                            RectTransform = { AnchorMin = $"0.04 {objY - 0.04f}", AnchorMax = $"0.7 {objY}" }
                        }, $"{MainPanel}_Details");
                    }

                    // Progress bar background
                    container.Add(new CuiPanel
                    {
                        Image = { Color = "0.2 0.2 0.2 1" },
                        RectTransform = { AnchorMin = $"0.72 {objY - 0.03f}", AnchorMax = $"0.98 {objY - 0.01f}" }
                    }, $"{MainPanel}_Details", $"{MainPanel}_ObjBar_{objective.Id}");

                    // Progress bar fill
                    var fillPercent = Math.Min(1f, (float)currentAmount / objective.RequiredAmount);
                    if (fillPercent > 0)
                    {
                        container.Add(new CuiPanel
                        {
                            Image = { Color = isComplete ? "0.3 0.7 0.3 1" : "0.4 0.6 0.8 1" },
                            RectTransform = { AnchorMin = "0 0", AnchorMax = $"{fillPercent} 1" }
                        }, $"{MainPanel}_ObjBar_{objective.Id}");
                    }

                    // Progress text
                    container.Add(new CuiLabel
                    {
                        Text = { Text = $"{currentAmount}/{objective.RequiredAmount}", FontSize = 9, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
                    }, $"{MainPanel}_ObjBar_{objective.Id}");

                    objY -= 0.06f;
                }

                // Rewards header
                container.Add(new CuiLabel
                {
                    Text = { Text = "Rewards", FontSize = 14, Align = TextAnchor.MiddleLeft, Color = "0.8 0.8 0.8 1" },
                    RectTransform = { AnchorMin = "0.02 0.22", AnchorMax = "0.98 0.28" }
                }, $"{MainPanel}_Details");

                // Rewards list
                var rewardX = 0.04f;
                var rewardIndex = 0;
                foreach (var reward in quest.Rewards.Take(5))
                {
                    var panelName = $"{MainPanel}_Reward_{rewardIndex}";
                    
                    // Reward container
                    container.Add(new CuiPanel
                    {
                        Image = { Color = "0.15 0.15 0.15 1" },
                        RectTransform = { AnchorMin = $"{rewardX} 0.08", AnchorMax = $"{rewardX + 0.12f} 0.21" }
                    }, $"{MainPanel}_Details", panelName);

                    // Item icon
                    var itemShortname = reward.ItemShortname ?? reward.BlueprintShortname;
                    var itemDef = !string.IsNullOrEmpty(itemShortname) ? ItemManager.FindItemDefinition(itemShortname) : null;
                    
                    if (itemDef != null)
                    {
                        AddItemIcon(container, panelName, $"{panelName}_Icon", itemDef.itemid, "0.15 0.35", "0.85 0.95");

                        // Blueprint indicator
                        if (reward.Type == RewardType.Blueprint)
                        {
                            container.Add(new CuiLabel
                            {
                                Text = { Text = "BP", FontSize = 8, Align = TextAnchor.UpperRight, Color = "0.2 0.8 1 1" },
                                RectTransform = { AnchorMin = "0.6 0.7", AnchorMax = "0.95 0.95" }
                            }, panelName);
                        }
                    }
                    else
                    {
                        // Non-item reward icon placeholder
                        var iconText = reward.Type switch
                        {
                            RewardType.Currency => "$",
                            RewardType.Command => "⚡",
                            RewardType.SkillPoints => "SP",
                            RewardType.Experience => "XP",
                            _ => "?"
                        };
                        container.Add(new CuiLabel
                        {
                            Text = { Text = iconText, FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "1 0.8 0.2 1" },
                            RectTransform = { AnchorMin = "0.1 0.4", AnchorMax = "0.9 0.95" }
                        }, panelName);
                    }

                    // Amount text
                    var amountText = reward.Amount > 1 ? $"x{reward.Amount}" : "";
                    if (reward.Type == RewardType.Currency) amountText = $"${reward.Amount}";
                    if (reward.Type == RewardType.SkillPoints) amountText = $"{reward.Amount} SP";
                    if (reward.Type == RewardType.Experience) amountText = $"{reward.Amount} XP";
                    
                    container.Add(new CuiLabel
                    {
                        Text = { Text = amountText, FontSize = 9, Align = TextAnchor.LowerCenter, Color = "1 1 1 1" },
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0.35" }
                    }, panelName);

                    rewardX += 0.13f;
                    rewardIndex++;
                }

                // Action button
                if (isActive)
                {
                    if (progress.Status == QuestStatus.ReadyToComplete)
                    {
                        container.Add(new CuiButton
                        {
                            Button = { Color = "0.2 0.7 0.2 1", Command = $"quest.complete {questId}" },
                            RectTransform = { AnchorMin = "0.35 0.02", AnchorMax = "0.65 0.09" },
                            Text = { Text = "COMPLETE", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
                        }, $"{MainPanel}_Details");
                    }
                    else
                    {
                        container.Add(new CuiButton
                        {
                            Button = { Color = "0.6 0.2 0.2 1", Command = $"quest.abandon {questId}" },
                            RectTransform = { AnchorMin = "0.35 0.02", AnchorMax = "0.65 0.09" },
                            Text = { Text = "ABANDON", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
                        }, $"{MainPanel}_Details");
                    }
                }
                else
                {
                    container.Add(new CuiButton
                    {
                        Button = { Color = "0.2 0.5 0.7 1", Command = $"quest.accept {questId}" },
                        RectTransform = { AnchorMin = "0.35 0.02", AnchorMax = "0.65 0.09" },
                        Text = { Text = "ACCEPT", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
                    }, $"{MainPanel}_Details");
                }
            }

            public void ShowMiniQuestList(BasePlayer player)
            {
                DestroyMiniUI(player);

                var activeQuests = _questManager.GetActiveQuests(player);
                if (activeQuests.Count == 0) return;

                var container = new CuiElementContainer();

                // Smaller mini panel - about 30% of original size
                container.Add(new CuiPanel
                {
                    Image = { Color = "0 0 0 0.75" },
                    RectTransform = { AnchorMin = "0.005 0.78", AnchorMax = "0.12 0.92" }
                }, "Hud", MiniPanel);

                container.Add(new CuiLabel
                {
                    Text = { Text = "Quests", FontSize = 9, Align = TextAnchor.MiddleCenter, Color = "1 0.8 0.2 1" },
                    RectTransform = { AnchorMin = "0 0.85", AnchorMax = "1 1" }
                }, MiniPanel);

                var yOffset = 0.82f;
                foreach (var progress in activeQuests.Take(2))
                {
                    var quest = _questManager.GetQuest(progress.QuestId);
                    if (quest == null) continue;

                    container.Add(new CuiLabel
                    {
                        Text = { Text = quest.Name, FontSize = 8, Align = TextAnchor.MiddleLeft, Color = "0.9 0.9 0.9 1" },
                        RectTransform = { AnchorMin = $"0.05 {yOffset - 0.12f}", AnchorMax = $"0.95 {yOffset}" }
                    }, MiniPanel);

                    yOffset -= 0.14f;

                    foreach (var objective in quest.Objectives.Take(2))
                    {
                        var current = progress.ObjectiveProgress.GetValueOrDefault(objective.Id, 0);
                        var isComplete = current >= objective.RequiredAmount;
                        var checkmark = isComplete ? "✓" : "○";
                        var color = isComplete ? "0.3 0.8 0.3 1" : "0.7 0.7 0.7 1";

                        container.Add(new CuiLabel
                        {
                            Text = { Text = $" {checkmark} {current}/{objective.RequiredAmount}", FontSize = 7, Align = TextAnchor.MiddleLeft, Color = color },
                            RectTransform = { AnchorMin = $"0.05 {yOffset - 0.1f}", AnchorMax = $"0.95 {yOffset}" }
                        }, MiniPanel);

                        yOffset -= 0.12f;
                    }

                    yOffset -= 0.03f;
                }

                CuiHelper.AddUi(player, container);
            }

            public void RefreshQuestUI(BasePlayer player)
            {
                ShowQuestUI(player);
                ShowMiniQuestList(player);
            }

            public void ChangeTab(BasePlayer player, string tab)
            {
                _selectedTab[player.userID] = tab;
                _selectedQuest.Remove(player.userID);
                ShowQuestUI(player);
            }

            public void SelectQuest(BasePlayer player, string questId)
            {
                _selectedQuest[player.userID] = questId;
                ShowQuestUI(player);
            }

            public void DestroyUI(BasePlayer player)
            {
                CuiHelper.DestroyUi(player, MainPanel);
            }

            public void DestroyMiniUI(BasePlayer player)
            {
                CuiHelper.DestroyUi(player, MiniPanel);
            }

            private string GetStatusColor(QuestProgress progress)
            {
                if (progress == null) return "0.5 0.8 0.5 1";
                return progress.Status switch
                {
                    QuestStatus.ReadyToComplete => "0.2 0.9 0.2 1",
                    QuestStatus.Active => "0.8 0.8 0.2 1",
                    _ => "0.5 0.5 0.5 1"
                };
            }

            private string GetRewardText(QuestReward reward)
            {
                return reward.Type switch
                {
                    RewardType.Item => $"{reward.ItemShortname}\nx{reward.Amount}",
                    RewardType.Blueprint => $"BP: {reward.BlueprintShortname ?? reward.ItemShortname}",
                    RewardType.Currency => $"${reward.Amount}",
                    RewardType.Command => "Command",
                    RewardType.SkillPoints => $"SP: {reward.Amount}",
                    RewardType.Experience => $"XP: {reward.Amount}",
                    _ => "Reward"
                };
            }

            private string FormatSeconds(int seconds)
            {
                var ts = TimeSpan.FromSeconds(seconds);
                if (ts.TotalHours >= 1) return $"{(int)ts.TotalHours}h";
                if (ts.TotalMinutes >= 1) return $"{(int)ts.TotalMinutes}m";
                return $"{seconds}s";
            }

            private void AddItemIcon(CuiElementContainer container, string parent, string name, int itemId, string anchorMin, string anchorMax)
            {
                var itemDef = ItemManager.FindItemDefinition(itemId);
                if (itemDef == null) return;

                // Try ImageLibrary first (if installed)
                var imageLib = _plugin.plugins.Find("ImageLibrary");
                if (imageLib != null)
                {
                    var image = imageLib.Call("GetImage", itemDef.shortname) as string;
                    if (!string.IsNullOrEmpty(image))
                    {
                        container.Add(new CuiElement
                        {
                            Parent = parent,
                            Name = name,
                            Components =
                            {
                                new CuiRawImageComponent { Png = image, Color = "1 1 1 1" },
                                new CuiRectTransformComponent { AnchorMin = anchorMin, AnchorMax = anchorMax }
                            }
                        });
                        return;
                    }
                }
                
                // Use Rust's native item icon sprite (works without ImageLibrary)
                container.Add(new CuiElement
                {
                    Parent = parent,
                    Name = name,
                    Components =
                    {
                        new CuiImageComponent { ItemId = itemId, Color = "1 1 1 1" },
                        new CuiRectTransformComponent { AnchorMin = anchorMin, AnchorMax = anchorMax }
                    }
                });
            }

            // Maps entity/animal names to related item icons (meat for animals, etc.)
            private int GetEntityItemId(string entityName)
            {
                if (string.IsNullOrEmpty(entityName)) return 0;
                
                var name = entityName.ToLower();
                
                // Fallback item icons
                var entityItemMap = new Dictionary<string, string>
                {
                    { "boar", "meat.boar" },
                    { "bear", "bearmeat" },
                    { "wolf", "wolfmeat.raw" },
                    { "stag", "deermeat.raw" },
                    { "deer", "deermeat.raw" },
                    { "chicken", "chicken.raw" },
                    { "horse", "horsemeat.raw" },
                    { "shark", "fish.raw" },
                    { "scientist", "hazmatsuit" },
                    { "heavyscientist", "heavy.plate.helmet" },
                    { "scarecrow", "skull.human" },
                    { "zombie", "skull.human" },
                    { "murderer", "machete" },
                    { "patrolhelicopter", "targeting.computer" },
                    { "bradleyapc", "targeting.computer" },
                    { "barrel", "scrap" },
                    { "loot-barrel", "scrap" },
                    { "crate", "scrap" }
                };

                foreach (var kvp in entityItemMap)
                {
                    if (name.Contains(kvp.Key))
                    {
                        var itemDef = ItemManager.FindItemDefinition(kvp.Value);
                        if (itemDef != null) return itemDef.itemid;
                    }
                }

                return 0;
            }

            // Admin UI
            private const string AdminPanel = "QuestSystem_Admin";
            private readonly Dictionary<ulong, string> _adminSelectedQuest = new Dictionary<ulong, string>();

            public void ShowAdminUI(BasePlayer player)
            {
                DestroyAdminUI(player);

                var container = new CuiElementContainer();

                // Main background
                container.Add(new CuiPanel
                {
                    Image = { Color = "0 0 0 0.97" },
                    RectTransform = { AnchorMin = "0.1 0.1", AnchorMax = "0.9 0.9" },
                    CursorEnabled = true
                }, "Overlay", AdminPanel);

                // Header
                container.Add(new CuiPanel
                {
                    Image = { Color = "0.8 0.4 0.1 1" },
                    RectTransform = { AnchorMin = "0 0.93", AnchorMax = "1 1" }
                }, AdminPanel, $"{AdminPanel}_Header");

                container.Add(new CuiLabel
                {
                    Text = { Text = "QUEST EDITOR (Admin)", FontSize = 18, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
                }, $"{AdminPanel}_Header");

                // Close button
                container.Add(new CuiButton
                {
                    Button = { Color = "0.8 0.2 0.2 1", Command = "quest.admin.close" },
                    RectTransform = { AnchorMin = "0.96 0.2", AnchorMax = "0.99 0.8" },
                    Text = { Text = "X", FontSize = 14, Align = TextAnchor.MiddleCenter }
                }, $"{AdminPanel}_Header");

                // Back to Quest UI button
                container.Add(new CuiButton
                {
                    Button = { Color = "0.2 0.5 0.7 1", Command = "quest.admin.backtoquest" },
                    RectTransform = { AnchorMin = "0.01 0.2", AnchorMax = "0.12 0.8" },
                    Text = { Text = "← Back to Quests", FontSize = 9, Align = TextAnchor.MiddleCenter }
                }, $"{AdminPanel}_Header");

                // Reset Quests button
                container.Add(new CuiButton
                {
                    Button = { Color = "0.7 0.3 0.1 1", Command = "quest.admin.resetall" },
                    RectTransform = { AnchorMin = "0.13 0.2", AnchorMax = "0.24 0.8" },
                    Text = { Text = "Reset All Quests", FontSize = 9, Align = TextAnchor.MiddleCenter }
                }, $"{AdminPanel}_Header");

                // Left panel - Quest list
                container.Add(new CuiPanel
                {
                    Image = { Color = "0.1 0.1 0.1 1" },
                    RectTransform = { AnchorMin = "0.01 0.02", AnchorMax = "0.3 0.92" }
                }, AdminPanel, $"{AdminPanel}_List");

                container.Add(new CuiLabel
                {
                    Text = { Text = "Quests", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0.8 0.8 0.8 1" },
                    RectTransform = { AnchorMin = "0 0.95", AnchorMax = "1 1" }
                }, $"{AdminPanel}_List");

                // New Quest button
                container.Add(new CuiButton
                {
                    Button = { Color = "0.2 0.6 0.2 1", Command = "quest.admin.new" },
                    RectTransform = { AnchorMin = "0.05 0.9", AnchorMax = "0.95 0.94" },
                    Text = { Text = "+ New Quest", FontSize = 11, Align = TextAnchor.MiddleCenter }
                }, $"{AdminPanel}_List");

                // Quest list
                var quests = _questManager.GetAllQuests();
                var yOffset = 0.88f;
                foreach (var quest in quests.Take(15))
                {
                    var isSelected = _adminSelectedQuest.GetValueOrDefault(player.userID) == quest.Id;
                    container.Add(new CuiButton
                    {
                        Button = { Color = isSelected ? "0.4 0.5 0.6 1" : "0.2 0.2 0.2 1", Command = $"quest.admin.select {quest.Id}" },
                        RectTransform = { AnchorMin = $"0.02 {yOffset - 0.05f}", AnchorMax = $"0.98 {yOffset}" },
                        Text = { Text = quest.Name, FontSize = 10, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" }
                    }, $"{AdminPanel}_List");
                    yOffset -= 0.055f;
                }

                // Right panel - Quest editor
                container.Add(new CuiPanel
                {
                    Image = { Color = "0.1 0.1 0.1 1" },
                    RectTransform = { AnchorMin = "0.31 0.02", AnchorMax = "0.99 0.92" }
                }, AdminPanel, $"{AdminPanel}_Editor");

                var selectedQuestId = _adminSelectedQuest.GetValueOrDefault(player.userID);
                if (!string.IsNullOrEmpty(selectedQuestId))
                {
                    AddAdminQuestEditor(container, player, selectedQuestId);
                }
                else
                {
                    container.Add(new CuiLabel
                    {
                        Text = { Text = "Select a quest to edit or create a new one", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0.5 0.5 0.5 1" },
                        RectTransform = { AnchorMin = "0 0.4", AnchorMax = "1 0.6" }
                    }, $"{AdminPanel}_Editor");
                }

                CuiHelper.AddUi(player, container);
            }

            private void AddAdminQuestEditor(CuiElementContainer container, BasePlayer player, string questId)
            {
                var quest = _questManager.GetQuest(questId);
                if (quest == null) return;

                // Quest ID
                container.Add(new CuiLabel
                {
                    Text = { Text = $"Quest ID: {quest.Id}", FontSize = 12, Align = TextAnchor.MiddleLeft, Color = "0.6 0.6 0.6 1" },
                    RectTransform = { AnchorMin = "0.02 0.93", AnchorMax = "0.5 0.98" }
                }, $"{AdminPanel}_Editor");

                // Delete button
                container.Add(new CuiButton
                {
                    Button = { Color = "0.7 0.2 0.2 1", Command = $"quest.admin.delete {quest.Id}" },
                    RectTransform = { AnchorMin = "0.85 0.93", AnchorMax = "0.98 0.98" },
                    Text = { Text = "Delete", FontSize = 10, Align = TextAnchor.MiddleCenter }
                }, $"{AdminPanel}_Editor");

                // Quest Name
                container.Add(new CuiLabel
                {
                    Text = { Text = "Name:", FontSize = 11, Align = TextAnchor.MiddleLeft, Color = "0.8 0.8 0.8 1" },
                    RectTransform = { AnchorMin = "0.02 0.86", AnchorMax = "0.1 0.91" }
                }, $"{AdminPanel}_Editor");

                container.Add(new CuiLabel
                {
                    Text = { Text = quest.Name, FontSize = 12, Align = TextAnchor.MiddleLeft, Color = "1 0.8 0.2 1" },
                    RectTransform = { AnchorMin = "0.11 0.86", AnchorMax = "0.6 0.91" }
                }, $"{AdminPanel}_Editor");

                // Category
                container.Add(new CuiLabel
                {
                    Text = { Text = $"Category: {quest.Settings.Category}", FontSize = 10, Align = TextAnchor.MiddleLeft, Color = "0.7 0.7 0.7 1" },
                    RectTransform = { AnchorMin = "0.62 0.86", AnchorMax = "0.98 0.91" }
                }, $"{AdminPanel}_Editor");

                // XP Reward
                container.Add(new CuiLabel
                {
                    Text = { Text = $"XP Reward: {quest.Settings.XPReward}", FontSize = 10, Align = TextAnchor.MiddleLeft, Color = "0.5 0.8 0.5 1" },
                    RectTransform = { AnchorMin = "0.02 0.81", AnchorMax = "0.25 0.86" }
                }, $"{AdminPanel}_Editor");

                // Required Level
                container.Add(new CuiLabel
                {
                    Text = { Text = $"Required Level: {quest.Settings.RequiredLevel}", FontSize = 10, Align = TextAnchor.MiddleLeft, Color = "0.8 0.6 0.2 1" },
                    RectTransform = { AnchorMin = "0.26 0.81", AnchorMax = "0.5 0.86" }
                }, $"{AdminPanel}_Editor");

                // Description
                container.Add(new CuiLabel
                {
                    Text = { Text = "Description:", FontSize = 11, Align = TextAnchor.MiddleLeft, Color = "0.8 0.8 0.8 1" },
                    RectTransform = { AnchorMin = "0.02 0.75", AnchorMax = "0.98 0.8" }
                }, $"{AdminPanel}_Editor");

                container.Add(new CuiLabel
                {
                    Text = { Text = quest.Description ?? "(No description)", FontSize = 10, Align = TextAnchor.UpperLeft, Color = "0.9 0.9 0.9 1" },
                    RectTransform = { AnchorMin = "0.02 0.65", AnchorMax = "0.98 0.75" }
                }, $"{AdminPanel}_Editor");

                // Objectives section
                container.Add(new CuiLabel
                {
                    Text = { Text = $"Objectives ({quest.Objectives.Count})", FontSize = 12, Align = TextAnchor.MiddleLeft, Color = "0.8 0.8 0.8 1" },
                    RectTransform = { AnchorMin = "0.02 0.58", AnchorMax = "0.5 0.64" }
                }, $"{AdminPanel}_Editor");

                var objY = 0.55f;
                foreach (var obj in quest.Objectives.Take(5))
                {
                    container.Add(new CuiLabel
                    {
                        Text = { Text = $"• [{obj.Type}] {obj.Description} - {obj.Target} x{obj.RequiredAmount}", FontSize = 9, Align = TextAnchor.MiddleLeft, Color = "0.7 0.7 0.7 1" },
                        RectTransform = { AnchorMin = $"0.04 {objY - 0.04f}", AnchorMax = $"0.98 {objY}" }
                    }, $"{AdminPanel}_Editor");
                    objY -= 0.045f;
                }

                // Rewards section
                container.Add(new CuiLabel
                {
                    Text = { Text = $"Rewards ({quest.Rewards.Count})", FontSize = 12, Align = TextAnchor.MiddleLeft, Color = "0.8 0.8 0.8 1" },
                    RectTransform = { AnchorMin = "0.02 0.3", AnchorMax = "0.5 0.36" }
                }, $"{AdminPanel}_Editor");

                var rewY = 0.27f;
                foreach (var rew in quest.Rewards.Take(5))
                {
                    var rewText = rew.Type == RewardType.Item ? $"• {rew.ItemShortname} x{rew.Amount}" : $"• [{rew.Type}] {rew.Amount}";
                    container.Add(new CuiLabel
                    {
                        Text = { Text = rewText, FontSize = 9, Align = TextAnchor.MiddleLeft, Color = "0.7 0.7 0.7 1" },
                        RectTransform = { AnchorMin = $"0.04 {rewY - 0.04f}", AnchorMax = $"0.98 {rewY}" }
                    }, $"{AdminPanel}_Editor");
                    rewY -= 0.045f;
                }

                // Info text
                container.Add(new CuiLabel
                {
                    Text = { Text = "Use console commands to edit:\nquest.admin.save <id> <name> <desc> <category> <xp> <level>\nquest.admin.addobjective <questId> <type> <target> <amount> <desc>\nquest.admin.addreward <questId> <type> <item> <amount>", FontSize = 9, Align = TextAnchor.UpperLeft, Color = "0.5 0.5 0.5 1" },
                    RectTransform = { AnchorMin = "0.02 0.02", AnchorMax = "0.98 0.15" }
                }, $"{AdminPanel}_Editor");
            }

            public void AdminSelectQuest(BasePlayer player, string questId)
            {
                _adminSelectedQuest[player.userID] = questId;
                ShowAdminUI(player);
            }

            public void AdminNewQuest(BasePlayer player)
            {
                var newId = $"quest_{DateTime.UtcNow.Ticks}";
                _questManager.SaveQuestBasicInfo(newId, "New Quest", "Quest description here", "General", 100, 0);
                _adminSelectedQuest[player.userID] = newId;
                ShowAdminUI(player);
            }

            public void DestroyAdminUI(BasePlayer player)
            {
                CuiHelper.DestroyUi(player, AdminPanel);
            }
        }

        #endregion

    }
}
