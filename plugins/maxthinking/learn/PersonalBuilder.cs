using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using System;
using Oxide.Core;
using System.Collections;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;

namespace Oxide.Plugins
{
    [Info("Personal Builder", "walkinrey & Max39ru", "1.0.7")] 
    public class PersonalBuilder : RustPlugin
    {
        [PluginReference] private Plugin ZoneManager;

        private Dictionary<ulong, PlayerBuilderController> _controllersByOwner = new Dictionary<ulong, PlayerBuilderController>();
        private Dictionary<ulong, PlayerBuilderController> _controllersByBot = new Dictionary<ulong, PlayerBuilderController>();

        private Dictionary<ulong, DateTime> _cooldownInfo = new Dictionary<ulong, DateTime>();

        private Configuration _config;
        private List<string> _permissionKeys = new List<string>();

        public PlayerBuilderController GetControllerByBot(ulong id)
        {
            if(_controllersByBot.ContainsKey(id))
            {
                var controller = _controllersByBot[id];

                if(controller == null) _controllersByBot.Remove(id);
                else return controller;
            }

            return null;
        }

        public PlayerBuilderController GetControllerByOwner(ulong id)
        {
            if(_controllersByOwner.ContainsKey(id))
            {
                var controller = _controllersByOwner[id];

                if(controller == null) _controllersByOwner.Remove(id);
                else return controller;
            }

            return null;
        }

        public readonly BuildCostInfo buildCostInfo = new BuildCostInfo();
        public const int WoodItemID = -151838493, StoneItemID = -2099697608, MetalItemID = 69511070, HQMItemID = 317398316, GearsItemID = 479143914;
        public class BuildCostInfo : Dictionary<string, ResourcesFromBuild>
        {
            public BuildCostInfo()
            {
                Add("assets/prefabs/building core/foundation.triangle/foundation.triangle.prefab", new ResourcesFromBuild(25, 0, 0, 0, 0, 100, 150, 100, 13));
                Add("assets/prefabs/building core/foundation/foundation.prefab", new ResourcesFromBuild(50, 0, 0, 0, 0, 200, 300, 200, 25));
                Add("assets/prefabs/building core/foundation.steps/foundation.steps.prefab", new ResourcesFromBuild(50, 0, 0, 0, 0, 200, 300, 200, 25));
                Add("assets/prefabs/building core/floor.triangle/floor.triangle.prefab", new ResourcesFromBuild(13, 0, 0, 0, 0, 50, 75, 50, 7));
                Add("assets/prefabs/building core/floor/floor.prefab", new ResourcesFromBuild(25, 0, 0, 0, 0, 100, 150, 100, 13));
                Add("assets/prefabs/building core/roof/roof.prefab", new ResourcesFromBuild(50, 0, 0, 0, 0, 200, 300, 200, 25));
                Add("assets/prefabs/building core/ramp/ramp.prefab", new ResourcesFromBuild(25, 0, 0, 0, 0, 100, 150, 100, 13));
                Add("assets/prefabs/building core/wall/wall.prefab", new ResourcesFromBuild(50, 0, 0, 0, 0, 200, 300, 200, 25));
                Add("assets/prefabs/building core/wall.half/wall.half.prefab", new ResourcesFromBuild(50, 0, 0, 0, 0, 200, 300, 200, 25));
                Add("assets/prefabs/building core/wall.low/wall.low.prefab", new ResourcesFromBuild(25, 0, 0, 0, 0, 100, 150, 100, 13));
                Add("assets/prefabs/building core/wall.frame/wall.frame.prefab", new ResourcesFromBuild(25, 0, 0, 0, 0, 100, 150, 100, 13));
                Add("assets/prefabs/building core/wall.window/wall.window.prefab", new ResourcesFromBuild(35, 0, 0, 0, 0, 140, 210, 140, 18));
                Add("assets/prefabs/building core/wall.doorway/wall.doorway.prefab", new ResourcesFromBuild(35, 0, 0, 0, 0, 140, 210, 140, 18));
                Add("assets/prefabs/building core/stairs.spiral.triangle/block.stair.spiral.triangle.prefab", new ResourcesFromBuild(25, 0, 0, 0, 0, 100, 150, 100, 13));
                Add("assets/prefabs/building core/stairs.spiral/block.stair.spiral.prefab", new ResourcesFromBuild(25, 0, 0, 0, 0, 100, 150, 100, 13));
                Add("assets/prefabs/building core/stairs.l/block.stair.lshape.prefab", new ResourcesFromBuild(25, 0, 0, 0, 0, 100, 150, 100, 13));
                Add("assets/prefabs/building core/stairs.u/block.stair.ushape.prefab", new ResourcesFromBuild(25, 0, 0, 0, 0, 100, 150, 100, 13));
                
                Add("assets/prefabs/building/door.hinged/door.hinged.wood.prefab", new ResourcesFromBuild(300, 0, 0, 0, 0));
                Add("assets/prefabs/building/door.double.hinged/door.double.hinged.wood.prefab", new ResourcesFromBuild(350, 0, 0, 0, 0));
                Add("assets/prefabs/building/door.hinged/door.hinged.metal.prefab", new ResourcesFromBuild(0, 0, 150, 0, 0));
                Add("assets/prefabs/building/door.double.hinged/door.double.hinged.metal.prefab", new ResourcesFromBuild(0, 0, 200, 0, 0));
                Add("assets/prefabs/building/door.hinged/door.hinged.toptier.prefab", new ResourcesFromBuild(0, 0, 0, 20, 5));
                Add("assets/prefabs/building/door.double.hinged/door.double.hinged.toptier.prefab", new ResourcesFromBuild(0, 0, 0, 25, 5));
                
                Add("assets/prefabs/deployable/tool cupboard/cupboard.tool.deployed.prefab", new ResourcesFromBuild(1000, 0, 0, 0, 0));
                Add("assets/prefabs/deployable/tool cupboard/retro/cupboard.tool.retro.deployed.prefab", new ResourcesFromBuild(1000, 0, 0, 0, 0));
            }

            public ResourcesFromBuild GetResources(string prefab)
            {
                TryGetValue(prefab, out var resources);
                return resources;
            }

        }
        public class ResourcesFromBuild
        {
            public int Wood, Stone, Metal, HQM, Gears;
            private Dictionary<BuildingGrade.Enum, int> gradeResources;

            private ResourcesFromBuild(int wood, int stone, int metal, int hQM, int gears)
            {
                Wood = wood;
                Stone = stone;
                Metal = metal;
                HQM = hQM;
                Gears = gears;
                gradeResources = new Dictionary<BuildingGrade.Enum, int>();
            }
            public ResourcesFromBuild(int wood, int stone, int metal, int hQM, int gears, int grade_wood = 0, int grade_stone = 0, int grade_metal= 0, int grade_top = 0) : this(wood, stone, metal, hQM, gears)
            {
                if(grade_wood == 0 && grade_stone == 0 && grade_metal == 0 && grade_top == 0) return;
                gradeResources.Add(BuildingGrade.Enum.Wood, grade_wood);
                gradeResources.Add(BuildingGrade.Enum.Stone, grade_stone);
                gradeResources.Add(BuildingGrade.Enum.Metal, grade_metal);
                gradeResources.Add(BuildingGrade.Enum.TopTier, grade_top);
            }

            public ResourcesFromBuild()
            {
                gradeResources = new Dictionary<BuildingGrade.Enum, int>();
            }

            public void Add(ResourcesFromBuild resources, BuildingGrade.Enum? grade)
            {
                if(resources == null) return;
                Wood += resources.Wood;
                Stone += resources.Stone;
                Metal += resources.Metal;
                HQM += resources.HQM;
                Gears += resources.Gears;
                int value = 0;
                if(grade != null && resources.gradeResources != null && resources.gradeResources.TryGetValue((BuildingGrade.Enum)grade, out value))
                {
                    switch((BuildingGrade.Enum)grade)
                    {
                        case BuildingGrade.Enum.Wood: Wood += value; break;
                        case BuildingGrade.Enum.Stone: Stone += value; break;
                        case BuildingGrade.Enum.Metal: Metal += value; break;
                        case BuildingGrade.Enum.TopTier: HQM += value; break;
                        default: break;
                    }
                }
            }
            public void AddNew(ResourcesFromBuild resources, BuildingGrade.Enum? grade)
            {
                Clear();
                Add(resources, grade);
            }
            public void Clear()
            {
                Wood = 0;
                Stone = 0;
                Metal = 0;
                HQM = 0;
                Gears = 0;
                gradeResources.Clear();
            }
            public void AddNew(int wood, int stone, int metal, int hQM, int gears)
            {
                Clear();
                Wood = wood;
                Stone = stone;
                Metal = metal;
                HQM = hQM;
                Gears = gears;
            }
            internal void SubtractionResourcesFromPlayer(PlayerInventory inventory, ResourcesFromBuild currentSubtractionResources)
            {
                currentSubtractionResources.Clear();
                if(inventory == null) return;
                int wood = 0, stone = 0, metal = 0, hQM = 0, gears = 0;
                if(Wood > 0)
                {
                    wood = inventory.Take(null, WoodItemID, Wood);
                    Wood -= wood;
                }
                if(Stone > 0)
                {
                    stone = inventory.Take(null, StoneItemID, Stone);
                    Stone -= stone;
                }
                if(Metal > 0)
                {
                    metal = inventory.Take(null, MetalItemID, Metal);
                    Metal -= metal;
                }
                if(HQM > 0)
                {
                    hQM = inventory.Take(null, HQMItemID, HQM);
                    HQM -= hQM;
                }
                if(Gears > 0)
                {
                    gears = inventory.Take(null, GearsItemID, Gears);
                    Gears -= gears;
                }
                currentSubtractionResources.AddNew(wood, stone, metal, hQM, gears);
            }
            public void GetResources(PlayerInventory inventoryOwner, ItemContainer containerBot)
            {
                int value;
                if(Wood > 0) value = inventoryOwner.Take(null, WoodItemID, Wood);
                else value = 0;
                if(value > 0) SetItem(WoodItemID, value, containerBot);
                if(Stone > 0) value = inventoryOwner.Take(null, StoneItemID, Stone);
                else value = 0;
                if(value > 0) SetItem(StoneItemID, value, containerBot);
                if(Metal > 0) value = inventoryOwner.Take(null, MetalItemID, Metal);
                else value = 0;
                if(value > 0) SetItem(MetalItemID, value, containerBot);
                if(HQM > 0) value = inventoryOwner.Take(null, HQMItemID, HQM);
                else value = 0;
                if(value > 0) SetItem(HQMItemID, value, containerBot);
                if(Gears > 0) value = inventoryOwner.Take(null, GearsItemID, Gears);
                else value = 0;
                if(value > 0) SetItem(GearsItemID, value, containerBot);
            }
            private void SetItem(int idItem, int amount, ItemContainer container)
            {
                ItemManager.CreateByItemID(idItem, amount)?.MoveToContainer(container);
            }

            internal void Subtraction(ResourcesFromBuild currentSubtractionResources)
            {
                Wood -= currentSubtractionResources.Wood;
                Stone -= currentSubtractionResources.Stone;
                Metal -= currentSubtractionResources.Metal;
                HQM -= currentSubtractionResources.HQM;
                Gears -= currentSubtractionResources.Gears;
            }
            public string ToString(PlayerInventory inventory, PersonalBuilder plugin, string userID)
            {
                if(inventory == null) return "";
                string text = "";
                string color = "";
                int num; 
                if(Wood > 0)
                {
                    num = inventory.GetAmount(WoodItemID);
                    color = num >= Wood ? "green" : "red";
                    text += $"\n{plugin.GetMsg("Resource_Wood", userID)}: {Wood}/<color={color}>{num}</color>";
                }
                if(Stone > 0)
                {
                    num = inventory.GetAmount(StoneItemID);
                    color = num >= Stone ? "green" : "red";
                    text += $"\n{plugin.GetMsg("Resource_Stone", userID)}: {Stone}/<color={color}>{num}</color>";
                }
                if(Metal > 0)
                {
                    num = inventory.GetAmount(MetalItemID);
                    color = num >= Metal ? "green" : "red";
                    text += $"\n{plugin.GetMsg("Resource_MetalFragment", userID)}: {Metal}/<color={color}>{num}</color>";
                }
                if(HQM > 0)
                {
                    num = inventory.GetAmount(HQMItemID);
                    color = num >= HQM ? "green" : "red";
                    text += $"\n{plugin.GetMsg("Resource_HQM", userID)}: {HQM}/<color={color}>{num}</color>";
                }
                if(Gears > 0)
                {
                    num = inventory.GetAmount(GearsItemID);
                    color = num >= Gears ? "green" : "red";
                    text += $"\n{plugin.GetMsg("Resource_Gears", userID)}: {Gears}/<color={color}>{num}</color>";
                }
                return text;
            }
            public bool CanBuild() => Wood == 0 && Stone == 0 && Metal == 0 && HQM == 0 && Gears == 0;
        }

        #region Config

        public class Configuration 
        {
            [JsonProperty("Controls")]
            public ControlsSetup Controls = new ControlsSetup();

            [JsonProperty("Build Limitations")]
            public GlobalLimitationsSetup Limitations = new GlobalLimitationsSetup();

            [JsonProperty("Permissions")]
            public Dictionary<string, List<BotSetup>> permissionBot = new Dictionary<string, List<BotSetup>>();

            [JsonProperty("Builder install by item")]
            public List<ItemInfo> installItem = new List<ItemInfo>();

            public struct ItemInfo 
            {
                [JsonProperty("Item name")]
                public string name;

                [JsonProperty("Item shortname")]
                public string shortname;

                [JsonProperty("Item skin")]
                public ulong skin;

                [JsonProperty("Return item on bot despawn?")]
                public bool returnItem;

                [JsonProperty("Bot info")]
                public BotSetup bot;
            }

            public class ControlsSetup
            {
                [JsonIgnore]
                public BUTTON controlButton = BUTTON.FIRE_THIRD;

                [JsonProperty("Button to assign build position (MIDDLE_MOUSE, SECOND_MOUSE, E, RELOAD, SPRINT)")]
                public string mainControlButton = "MIDDLE_MOUSE"; 

                [JsonProperty("Range of the task assignment button")]
                public float rayLength = 25f;

                [JsonProperty("Display 3D arrows over a build position?")]
                public bool enableArrowView = true;

                [JsonProperty("Arrow display duration")]
                public int arrowViewDuration = 2;

                [JsonProperty("Block bot spawn in safezones")]
                public bool blockBotSpawnSafezone = false;

                [JsonProperty("Block bot spawn in ZoneManager zones (enter zone id belove)")]
                public List<string> blockBotZoneManager = new List<string>();
            }

            public class GlobalLimitationsSetup
            {
                [JsonProperty("Clear limitations data on server wipe?")]
                public bool clearLimitsOnWipe = true;

                [JsonProperty("Clear limitations data on permission revoke?")]
                public bool clearLimitsOnRevoke = true;
            }

            public class BotSetup 
            {
                [JsonProperty("The name of the bot to be selected through the command when spawning")]
                public string spawnName = "bot1";

                [JsonProperty("Bot spawn delay")]
                public float cooldown = 300f;

                [JsonProperty("CopyPaste file name")]
                public string filename = "";

                [JsonProperty("Player (bot)")]
                public PlayerSetup player = new PlayerSetup();

                [JsonProperty("Resources")]
                public ResourcesSetup resources = new ResourcesSetup();

                [JsonProperty("Speed")]
                public SpeedSetup speed = new SpeedSetup();

                [JsonProperty("Build")]
                public BuildSetup build = new BuildSetup();

                [JsonProperty("Effects")]
                public EffectsSetup effects = new EffectsSetup();

                [JsonProperty("Limitations")]
                public LimitationsSetup limitations = new LimitationsSetup();

                [JsonProperty("Clothes")]
                public List<ItemSetup> startKit = new List<ItemSetup>();

                public class LimitationsSetup 
                {
                    [JsonProperty("Enable limit on number of buildings that can be built? (linked to player, doesn't apply to everyone)")]
                    public bool enableLimits = false;

                    [JsonProperty("How many bases can this bot build?")]
                    public int times = 5;
                }

                public class PlayerSetup 
                {
                    [JsonProperty("Bot display name")]
                    public string displayName = "";

                    [JsonProperty("Bot health")]
                    public float health = 1000f;

                    [JsonProperty("Make bot immortal?")]
                    public bool isImmortal = false;

                    [JsonProperty("Lock wear (attire) container?")]
                    public bool lockWearContainer = true;

                    [JsonProperty("Lock belt container?")]
                    public bool lockBeltContainer = true;
                }

                public class EffectsSetup 
                {
                    [JsonProperty("Enable effects on appear & disappear?")]
                    public bool enableSpawnEffects = true;

                    [JsonProperty("Enable building block upgrade effects?")]
                    public bool enableUpgradeEffects = true;
                }

                public class ResourcesSetup 
                {
                    [JsonProperty("Require resources?")]
                    public bool requireResources = false;

                    [JsonProperty("Drop bot's resources on death?")]
                    public bool dropResources = true;
                }

                public class BuildSetup
                {
                    [JsonProperty("Max allowable height of foundation")]
                    public float maxHeightBlockFoundation = 3.1f;

                    [JsonProperty("Start height to ground")]
                    public float startHeightToGround = 0.5f;

                    [JsonProperty("Max depth when building in water")]
                    public float maxDepthInWater = 2.5f;

                    [JsonProperty("Checks")]
                    public ChecksSetup Checks = new ChecksSetup();

                    [JsonProperty("Doors")]
                    public DoorsSetup Doors = new DoorsSetup();

                    // [JsonProperty("BuildingPrivileges")]
                    [JsonIgnore] public CupSetup Cup = new CupSetup();

                    public class DoorsSetup 
                    {
                        [JsonProperty("Deploy doors?")]
                        public bool deployDoors = false;

                        [JsonProperty("Require resources for doors?")]
                        public bool requireResources = false;
                    }
                    public class CupSetup 
                    {
                        [JsonProperty("Deploy Building Privileges?")]
                        public bool deployCup = false;

                        [JsonProperty("Require resources for doors?")]
                        public bool requireResources = false;

                        [JsonProperty("Authorize the owner?")]
                        public bool canAuth = false;
                    }

                    public class ChecksSetup
                    {
                        [JsonProperty("Check is close to road?")]
                        public bool checkIsCloseToRoad = true;

                        [JsonProperty("Check is building blocked?")]
                        public bool checkIsBuildingBlocked = true;

                        [JsonProperty("Check for Prevent Building triggers")]
                        public bool checkForPreventBuilding = true;

                        [JsonProperty("Prevent Building check radius")]
                        public float preventBuildingRadius = 10f;
                    }
                }

                public class SpeedSetup 
                {
                    [JsonProperty("Bot speed multiplier")]
                    public float lerpSpeedMultiplier = 10f;

                    [JsonProperty("Build speed multiplier")]
                    public float buildSpeedMultiplier = 10f;

                    [JsonProperty("Time to check 1 foundation")]
                    public float timeCheckFoundation = 0.001f;

                    [JsonProperty("Time to check 1 building block")]
                    public float timeCheckBuildingBlock = 0.001f;
                }

                public class ItemSetup 
                {
                    [JsonProperty("Item name")]
                    public string name = "";

                    [JsonProperty("Item shortname")]
                    public string shortname = "";

                    [JsonProperty("Item skin")]
                    public ulong skin = 0;

                    [JsonProperty("Item amount")]
                    public int amount = 1;
                }
            }
        }

        public class OldConfiguration 
        {
            [JsonProperty("Controls")]
            public Configuration.ControlsSetup Controls = new Configuration.ControlsSetup();

            [JsonProperty("Build Limitations")]
            public Configuration.GlobalLimitationsSetup Limitations = new Configuration.GlobalLimitationsSetup();

            [JsonProperty("Permissions")]
            public Dictionary<string, Configuration.BotSetup> permissionBot = new Dictionary<string, Configuration.BotSetup>();
        
            [JsonProperty("Builder install by item")]
            public List<Configuration.ItemInfo> installItem = new List<Configuration.ItemInfo>();
        }

        protected override void LoadDefaultConfig() 
        {
            _config = new Configuration
            {
                permissionBot = new Dictionary<string, List<Configuration.BotSetup>>
                {
                    ["personalbuilder.bot1"] = new List<Configuration.BotSetup>
                    {
                        new Configuration.BotSetup
                        {
                            player = new Configuration.BotSetup.PlayerSetup {
                            displayName = "Personal Builder"
                            },
                            filename = "pbuilder_test",
                            startKit = new List<Configuration.BotSetup.ItemSetup>
                            {
                                new Configuration.BotSetup.ItemSetup
                                {
                                    shortname = "shoes.boots",
                                },
                                new Configuration.BotSetup.ItemSetup
                                {
                                    shortname = "pants",
                                },
                                new Configuration.BotSetup.ItemSetup
                                {
                                    shortname = "hoodie",
                                },
                                new Configuration.BotSetup.ItemSetup
                                {
                                    shortname = "mask.bandana",
                                },
                                new Configuration.BotSetup.ItemSetup
                                {
                                    shortname = "hat.boonie",
                                },
                                new Configuration.BotSetup.ItemSetup
                                {
                                    shortname = "sunglasses",
                                }
                            }
                        }
                    }
                }
            };
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null) throw new Exception();

                SaveConfig();
            }
            catch (Exception ex)
            {
                var oldConfig = Config.ReadObject<OldConfiguration>();
                if(oldConfig == null) {
                    PrintError("{0}", ex);
                    LoadDefaultConfig();
                }
                else {
                    _config = new Configuration();

                    _config.Controls = oldConfig.Controls;
                    _config.Limitations = oldConfig.Limitations;
                    _config.installItem = oldConfig.installItem;

                    foreach(var pair in oldConfig.permissionBot)
                    {
                        _config.permissionBot.Add(pair.Key, new List<Configuration.BotSetup> {pair.Value});
                    }

                    SaveConfig();
                }
            }

            switch(_config.Controls.mainControlButton)
            {
                case "E":
                    _config.Controls.controlButton = BUTTON.USE;
                    break;

                case "MIDDLE_MOUSE":
                    _config.Controls.controlButton = BUTTON.FIRE_THIRD;
                    break;

                case "RELOAD":
                    _config.Controls.controlButton = BUTTON.RELOAD;
                    break;
                
                case "SPRINT":
                    _config.Controls.controlButton = BUTTON.SPRINT;
                    break;

                case "SECOND_MOUSE":
                    _config.Controls.controlButton = BUTTON.FIRE_SECONDARY;
                    break;
            }
        }

        protected override void SaveConfig() => Config.WriteObject(_config);

        #endregion

        #region Data

        private DataFile _data;

        public class DataFile 
        {
            public Dictionary<ulong, LimitInfo> LimitsInfo = new Dictionary<ulong, LimitInfo>();

            public class LimitInfo 
            {
                public Dictionary<string, int> LimitsByPermission = new Dictionary<string, int>();
            }
        }

        private void LoadData()
        {
            _data = Interface.Oxide.DataFileSystem.ReadObject<DataFile>("PersonalBuilder_LimitsData");
        }

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject("PersonalBuilder_LimitsData", _data);
        }

        private void ClearAllData()
        {
            _data = new DataFile();
            SaveData();
        }

        #endregion

        #region Loc

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["ChatCommand_Error_NoPermission"] = "You don't have a permission to spawn a personal builder",
                ["ChatCommand_Error_NotFound"] = "Bot not found",
                ["ChatCommand_Error_NoResources"] = "The bot doesn't have enough resources!\nFor construction, it is necessary:\n",
                ["ChatCommand_Error_Abort"] = "Construction stopped, bot is killed!",
                ["ChatCommand_Error_Limit"] = "Your limit for this building has been reached!",
                ["ChatCommand_Error_NoSpawnHere"] = "You can't spawn personal builder here!",

                ["ChatCommand_Success_Despawn"] = "Your personal builder was sucessfully despawned",
                ["ChatCommand_Success_Built"] = "Your building is ready!",
                ["ChatCommand_Success_Start"] = "Construction has started!",

                ["ChatCommand_Notice_Cooldown"] = "You need to wait {0} seconds to spawn a bot again",
                ["ChatCommand_Notice_AvailableBots"] = "<size=16>Available bots:</size>\n{BOTS}\n\nEnter /pbuilder [short bot name] to spawn!",
                ["ChatCommand_Notice_Check"] = "Checking the place to build...",
                ["open_loot_builder"] = "Open Inventory(USE)",
                ["Calculation_of_the_drawing"] = "Calculation of the building drawing...",
                ["Calculation_resources"] = "List of resources for construction:\n",

                ["CopyPaste_Error_NoFile"] = "Contact server admin, building file doesn't exist",
                ["CopyPaste_Error_FileCorrupt"] = "Contact server admin, build file is broken/corrupted.",
                
                ["CopyPaste_Error_BuildBlocked"] = "Building is blocked here!",
                ["CopyPaste_Error_CloseToRoad"] = "Close to road, can't build here!",
                ["CopyPaste_Error_NotSuitablePlace"] = "Not suitable landscape for construction!",
                ["CopyPaste_Error_WallBlocked"] = "Obstacle to wall construction detected!",
                ["CopyPaste_Error_RoofBlocked"] = "Obstacle to roof construction detected!",

                ["Resource_Wood"] = "Wood",
                ["Resource_Stone"] = "Stones",
                ["Resource_MetalFragment"] = "Metal Fragments",
                ["Resource_HQM"] = "High Quality Metal",
                ["Resource_Gears"] = "Gears",

                ["CommandPaused"] = "Construction is paused!",
                ["BotCalculated"] = "Checking the place to build...",
                ["BotCheckedTerritory"] = "Checking the place to build...",
                ["BotBuilding"] = "Construction resumed!",
            }, this, "en");

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["ChatCommand_Error_NoPermission"] = "У вас нет разрешения на спавн персонального строителя",
                ["ChatCommand_Error_NotFound"] = "Бот не найден",
                ["ChatCommand_Error_NoResources"] = "У Бота недостаточно ресурсов!\nДля строительства необходимо:\n",
                ["ChatCommand_Error_Abort"] = "Строительство прекращено, бот мертв!",
                ["ChatCommand_Error_Limit"] = "Ваш лимит на данную постройку был исчерпан!",
                ["ChatCommand_Error_NoSpawnHere"] = "Вы не можете заспавнить персонального строителя здесь!",

                ["ChatCommand_Success_Despawn"] = "Вас персональный строитель был успешно задеспавнен",
                ["ChatCommand_Success_Built"] = "Ваше здание готово!",
                ["ChatCommand_Success_Start"] = "Строительство начато!",

                ["ChatCommand_Notice_Cooldown"] = "Вам нужно подождать {0} секунд, прежде чем повторно заспавнить бота",
                ["ChatCommand_Notice_AvailableBots"] = "<size=16>Доступные боты:</size>\n{BOTS}\n\nВведите /pbuilder [короткое название бота], чтобы заспавнить!",
                ["ChatCommand_Notice_Check"] = "Проверка территории под строительство...",
                ["open_loot_builder"] = "Открыть Инвентарь(USE)",
                ["Calculation_of_the_drawing"] = "Расчет чертежа здания...",
                ["Calculation_resources"] = "Список ресурсов для строительства:\n",

                ["CopyPaste_Error_NoFile"] = "Свяжитесь с администратором сервера, файл постройки отсутствует",
                ["CopyPaste_Error_FileCorrupt"] = "Свяжитесь с администратором сервера, файл постройки поврежден",

                ["CopyPaste_Error_BuildBlocked"] = "Строительство недоступно здесь!",
                ["CopyPaste_Error_CloseToRoad"] = "Близко к дороге, строительство недоступно!",
                ["CopyPaste_Error_NotSuitablePlace"] = "Не подходящий ландшафт для строительства!",
                ["CopyPaste_Error_WallBlocked"] = "Обнаружено препятствие для строительства стены!",
                ["CopyPaste_Error_RoofBlocked"] = "Обнаружено препятствие для строительства крыши!",

                ["Resource_Wood"] = "Дерево",
                ["Resource_Stone"] = "Камни",
                ["Resource_MetalFragment"] = "Метал. фрагменты",
                ["Resource_HQM"] = "МВК",
                ["Resource_Gears"] = "Шестерни",

                ["CommandPaused"] = "Строительство приостановлено!",
                ["BotCalculated"] = "Бот рассчитывает чертеж здания...",
                ["BotCheckedTerritory"] = "Бот проверяет территорию под строительство...",
                ["BotBuilding"] = "Строительство возобновлено!",
            }, this, "ru");
        }

        #endregion

        #region Methods

        public void DropLoot(BasePlayer player, PlayerBuilderController controller, HitInfo info) 
        {
            if(!controller.BotSetup.resources.dropResources || player.inventory.containerMain.itemList.Count == 0) return;

            List<ItemContainer> containers = new List<ItemContainer>();

            if(!player.inventory.containerMain.IsLocked()) containers.Add(player.inventory.containerMain);

            DroppedItemContainer droppedContainer = GameManager.server.CreateEntity("assets/prefabs/misc/item drop/item_drop_backpack.prefab", player.transform.position, Quaternion.identity) as DroppedItemContainer;

            droppedContainer.TakeFrom(containers.ToArray(), 0f);
            droppedContainer.playerName = player.displayName;
            droppedContainer.playerSteamID = player.userID;
            droppedContainer.Spawn();
        }

        public List<Configuration.BotSetup> GetBotSetup(BasePlayer player) 
        {
            List<Configuration.BotSetup> setups = new List<Configuration.BotSetup>();

            foreach(var key in _permissionKeys)
            {
                if(permission.UserHasPermission(player.UserIDString, key))
                {
                    setups.AddRange(_config.permissionBot[key]);
                }
            }

            return setups;
        }

        public string GetMsg(string key, string id) => lang.GetMessage(key, this, id);

        public void SendMsg(BasePlayer player, string key, string[] args = null) 
        {
            if(args != null) player.ChatMessage(string.Format(lang.GetMessage(key, this, player.UserIDString), args));
            else 
            {
                player.ChatMessage(lang.GetMessage(key, this, player.UserIDString));
            }
        }

        [ChatCommand("pbuilder")]
        private void chatMsg(BasePlayer player, string command, string[] args)
        {
            var controller = GetControllerByOwner(player.userID);
            
            if(args == null || args?.Length == 0) 
            {
                if(controller != null) 
                {
                    if(controller.ItemInfo != null)
                    {
                        if(controller.ItemInfo.HasValue)
                        {
                            if(controller.ItemInfo.Value.returnItem)
                            {
                                var item = ItemManager.CreateByName(controller.ItemInfo.Value.shortname, 1, controller.ItemInfo.Value.skin);
                                if(!string.IsNullOrEmpty(controller.ItemInfo.Value.name)) item.name = controller.ItemInfo.Value.name;

                                player.GiveItem(item);
                            }
                        }
                    }

                    UnityEngine.Object.Destroy(controller);
                    SendMsg(player, "ChatCommand_Success_Despawn");
                }
                else 
                {
                    var botSetup = GetBotSetup(player);

                    if(botSetup != null)
                    {
                        if(botSetup.Count == 0) SendMsg(player, "ChatCommand_Error_NoPermission");
                        else 
                        {
                            if(botSetup.Count == 1)
                            {
                                chatMsg(player, command, new string[] {botSetup[0].spawnName});
                                return;
                            }

                            string msg = lang.GetMessage("ChatCommand_Notice_AvailableBots", this, player.UserIDString);
                            string availableBots = "";

                            foreach(var bot in botSetup) availableBots = availableBots + $"\n{botSetup.IndexOf(bot) + 1}. {bot.spawnName}";

                            msg = msg.Replace("{BOTS}", availableBots);
                            player.ChatMessage(msg);
                        }
                    }
                }

                return;
            }

            if(controller != null && args != null && args.Length == 1 && args[0] == "pause")
            {
                if(controller.Status == PlayerBuilderController.StatusBot.Building && !controller.Command_paused)
                {
                    controller.Command_paused = true;
                }
                controller.PrintStatus();
                return;
            }
            if(controller != null && args != null && args.Length == 1 && args[0] == "resume")
            {
                if(controller.Status == PlayerBuilderController.StatusBot.Building && controller.Command_paused)
                {
                    controller.Command_paused = false;
                }
                controller.PrintStatus();
                return;
            }

            var bots = GetBotSetup(player);

            if(bots.Count > 0)
            {
                List<Configuration.BotSetup> botsFinded = new List<Configuration.BotSetup>();
                foreach(var botSetup in bots) if(botSetup.spawnName == args[0]) botsFinded.Add(botSetup);

                Configuration.BotSetup bot = null;

                if(botsFinded.Count != 0) bot = botsFinded[0];
                else 
                {
                    SendMsg(player, "ChatCommand_Error_NotFound");
                    return;
                }

                if(bot != null)
                {
                    string perm = string.Empty;

                    foreach(var pair in _config.permissionBot)
                    {
                        if(pair.Value.Contains(bot))
                        {
                            perm = pair.Key;
                            break;
                        }
                    }

                    if(permission.UserHasPermission(player.UserIDString, perm))
                    {
                        if(controller != null) 
                        {
                            if(controller.ItemInfo != null)
                            {
                                if(controller.ItemInfo.HasValue)
                                {
                                    if(controller.ItemInfo.Value.returnItem)
                                    {
                                        var item = ItemManager.CreateByName(controller.ItemInfo.Value.shortname, 1, controller.ItemInfo.Value.skin);
                                        if(!string.IsNullOrEmpty(controller.ItemInfo.Value.name)) item.name = controller.ItemInfo.Value.name;

                                        player.GiveItem(item);
                                    }
                                }
                            }

                            UnityEngine.Object.Destroy(controller);
                            SendMsg(player, "ChatCommand_Success_Despawn");

                            return;
                        }

                        if(bot.limitations.enableLimits)
                        {
                            if(_data.LimitsInfo.ContainsKey(player.userID))
                            {
                                var playerInfo = _data.LimitsInfo[player.userID];

                                if(playerInfo != null)
                                {
                                    if(playerInfo.LimitsByPermission.ContainsKey(perm))
                                    {
                                        int currentBuilt = playerInfo.LimitsByPermission[perm];

                                        if(currentBuilt >= bot.limitations.times)
                                        {
                                            SendMsg(player, "ChatCommand_Error_Limit");
                                            return;
                                        }
                                    }
                                }
                            }
                        }
                        

                        if(_cooldownInfo.ContainsKey(player.userID))
                        {
                            var lastTimeSpawn = _cooldownInfo[player.userID];

                            if(DateTime.Now > lastTimeSpawn.AddSeconds(bot.cooldown))
                            {
                                _cooldownInfo.Remove(player.userID);
                                _cooldownInfo.Add(player.userID, DateTime.Now);
                            }
                            else 
                            {
                                SendMsg(player, "ChatCommand_Notice_Cooldown", new string[] { Mathf.RoundToInt((float)(lastTimeSpawn.AddSeconds(bot.cooldown) - DateTime.Now).TotalSeconds).ToString() });
                                return;
                            }
                        }
                        else _cooldownInfo.Add(player.userID, DateTime.Now);
                        
                        controller = PlayerBuilderController.CreateBot(player, bot, null, buildCostInfo, this);

                        _controllersByOwner.Add(player.userID, controller);
                        _controllersByBot.Add(controller.BotPlayer.userID, controller);
                    }
                    else SendMsg(player, "ChatCommand_Error_NoPermission");
                }
                else SendMsg(player, "ChatCommand_Error_NotFound");
            }
            else SendMsg(player, "ChatCommand_Error_NoPermission");
        }

        [ConsoleCommand("pbuilder.item")]
        private void cnslCommandItem(ConsoleSystem.Arg arg)
        {
            if(arg.Player() != null) return;

            if(!arg.HasArgs(2))
            {
                PrintError("Please enter Steam ID and item skin!");
                return;
            }

            ulong id, skin;

            if(!ulong.TryParse(arg.Args[0], out id))
            {
                PrintError("Steam ID is incorrect");
                return;
            }

            if(!ulong.TryParse(arg.Args[1], out skin))
            {
                PrintError("Skin is incorrect");
                return;
            }

            BasePlayer reciver = BasePlayer.FindByID(id);
            
            if(reciver == null)
            {
                PrintError("Player not found");
                return;
            }

            Configuration.ItemInfo info = new Configuration.ItemInfo();

            foreach(var loopInfo in _config.installItem)
            {
                if(loopInfo.skin == skin)
                {
                    info = loopInfo;
                    break;
                }
            }

            if(info.bot == null)
            {
                PrintError("Item is not found");
                return;
            }

            Item pnpc = ItemManager.CreateByName(info.shortname, 1, info.skin);
            if(!string.IsNullOrEmpty(info.name)) pnpc.name = info.name;

            reciver.GiveItem(pnpc);
            Puts($"Item was successfully given to player {reciver.displayName}");
        }

        #endregion

        #region Hooks

        private void OnEntityBuilt(Planner plan, GameObject go)
        {
            if(_config.installItem.Count == 0) return;

            var player = plan.GetOwnerPlayer();
            if(player == null) return;

            var item = player.GetActiveItem();
            if(item == null) return;

            if(_config.Controls.blockBotZoneManager.Count != 0 && ZoneManager != null)
            {
                foreach(var zone in _config.Controls.blockBotZoneManager)
                {
                    if(ZoneManager.Call<bool>("IsPlayerInZone", zone, player))
                    {
                        SendMsg(player, "ChatCommand_Error_NoSpawnHere");

                        int itemAmount = item.amount;

                        this.NextTick(() =>
                        {
                            var ent = go.ToBaseEntity();
                            if(ent != null) ent.Kill();

                            var newItem = ItemManager.CreateByName(item.info.shortname, itemAmount, item.skin);
                            player.GiveItem(newItem);
                        });

                        return;
                    }
                }
            }

            foreach(var loopInfo in _config.installItem)
            {
                if(loopInfo.skin == item.skin)
                {
                    var controller = GetControllerByOwner(player.userID);
                    var bot = loopInfo.bot;
                    
                    if(controller != null) 
                    {
                        if(controller.ItemInfo != null)
                        {
                            if(controller.ItemInfo.HasValue)
                            {
                                if(controller.ItemInfo.Value.returnItem)
                                {
                                    var item2 = ItemManager.CreateByName(controller.ItemInfo.Value.shortname, 1, controller.ItemInfo.Value.skin);
                                    if(!string.IsNullOrEmpty(controller.ItemInfo.Value.name)) item2.name = controller.ItemInfo.Value.name;

                                    player.GiveItem(item2);
                                }
                            }
                        }

                        UnityEngine.Object.Destroy(controller);
                    }

                    controller = PlayerBuilderController.CreateBot(player, bot, loopInfo, buildCostInfo, this, go.transform.position);

                    _controllersByOwner.Remove(player.userID);
                    _controllersByBot.Remove(controller.BotPlayer.userID);

                    _controllersByOwner.Add(player.userID, controller);
                    _controllersByBot.Add(controller.BotPlayer.userID, controller);

                    this.NextTick(() =>
                    {
                        var ent = go.ToBaseEntity();
                        if(ent != null) ent.Kill();
                    });

                    break;
                }
            }
        }

        private void Unload()
        {
            foreach(var controller in _controllersByOwner.Values)
            {
                if(controller == null) continue;
                UnityEngine.Object.Destroy(controller);
            }
        }

        private void Init()
        {
            LoadData();
        }

        private void OnNewSave(string filename)
        {
            if(_config.Limitations.clearLimitsOnWipe) ClearAllData();
        }

        private void OnUserPermissionRevoked(string id, string permName)
        {
            if(_config.Limitations.clearLimitsOnRevoke) 
            {
                ulong result;

                if(ulong.TryParse(id, out result))
                {
                    if(_data.LimitsInfo.ContainsKey(result))
                    {
                        var info = _data.LimitsInfo[result];
                         
                        if(info.LimitsByPermission.ContainsKey(permName))
                        {
                            info.LimitsByPermission.Remove(permName);
                            _data.LimitsInfo[result] = info;

                            SaveData();
                        }
                    }
                }
            }
        }

        private object OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            if(player == null || player?.net == null) return null;

            if(player.IsNpc)
            {
                var controller = GetControllerByBot(player.userID);

                if(controller != null)
                {
                    DropLoot(player, controller, info);
                    player.Teleport(new Vector3(0, -1000, 0));

                    NextTick(() => 
                    {
                        if(player != null && !player.IsDestroyed) player.Kill();
                    });

                    return false;
                }
            }

            return null;
        }

        private void OnServerInitialized()
        {
            _permissionKeys = new List<string>(_config.permissionBot.Keys);
            _permissionKeys.ForEach(x => permission.RegisterPermission(x, this));
        }

        private void OnPlayerInput(BasePlayer player, InputState input)
        {
            if(player == null || input == null) return;
            if(!input.WasJustPressed(_config.Controls.controlButton)) return;

            var controller = GetControllerByOwner(player.userID);
            if(controller == null) return;
            
            controller.OnInput(input);
        }

        private object OnEntityTakeDamage(BaseCombatEntity ent, HitInfo info)
        {
            if(ent == null || info == null) return null;

            if(ent is BasePlayer)
            {
                var player = ent.ToPlayer();

                if(player != null)
                {
                    if(_controllersByBot.ContainsKey(player.userID))
                    {
                        var controller = _controllersByBot[player.userID];

                        if(controller != null)
                        {
                            if(controller.BotSetup.player.isImmortal)
                            {
                                return false;
                            }
                        }
                    }
                }
            }
            else 
            {
                if(!info.damageTypes.Has(Rust.DamageType.Decay) || ent.OwnerID == 0) return null;
                if(!ent.OwnerID.IsSteamId()) return null;

                var controller = GetControllerByOwner(ent.OwnerID);
                if(controller != null) return false;
            }

            return null;
        }

        private object OnStructureUpgrade(BaseCombatEntity entity, BasePlayer player, BuildingGrade.Enum grade)
        {
            if(entity == null || player == null) return null;
            if(player.IsNpc || !player.userID.IsSteamId() || entity.OwnerID == 0 || !entity.OwnerID.IsSteamId()) return null;
            
            var controller = GetControllerByOwner(entity.OwnerID);
            if(controller != null) return false;

            return null;
        }
        private void OnPlayerLootEnd(PlayerLoot inventory)
        {
            if(inventory == null) return;
            if(inventory.entitySource == null) return;
            if(inventory._baseEntity == null) return;

            if(inventory.entitySource.TryGetComponent<BuilderInventory>(out var _inventory))
            {
                if(_inventory.IsOwner(inventory._baseEntity)) _inventory.Kill();
            }
        }

        #endregion

        #region Behaviour

        public class PlayerBuilderController : FacepunchBehaviour
        {
            public float maxHeightBlockFoundation = 3.1f;
            public float startHeightToGround = 0.5f;

            private Configuration.BotSetup _botSetup;
            private Configuration.ItemInfo? _botItemInfo;

            private BasePlayer _owner, _botPlayer;
            private BaseNavigator _bot;
            private NPCPlayer _npcPlayer;

            private List<StabilityEntity> _stabilityEntities = new List<StabilityEntity>();
            private Vector3 _buildPoint = Vector3.zero;

            private Coroutine _buildCoroutine;
            private ItemId _planID, _hammerID;

            private float _myHeight;
            private bool _isNullGround, _canBuildBlocks;

            public PersonalBuilder BuilderInstance;
            public BuildCostInfo BuildCost;

            public Configuration.BotSetup BotSetup => _botSetup;
            public Configuration.ItemInfo? ItemInfo => _botItemInfo;

            public BasePlayer BotPlayer => _botPlayer;

            #region Unity Callbacks
            
            private void FixedUpdate()
            {
                if(_owner == null || _botPlayer == null || BuilderInstance == null) return;
                if(isInitBuilders && Physics.Raycast(_owner.eyes.HeadRay(), out var hitInfo, 2f, LayerMask.GetMask("AI")) && hitInfo.GetEntity() == _botPlayer)
                {
                    if(!guiOpen)
                    {
                        List<CuiElement> list = Facepunch.Pool.GetList<CuiElement>();
                        list.Add(new CuiElement
                        {
                            Parent = "Hud", Name = "open_loot_builder", DestroyUi = "open_loot_builder",
                            Components =
                            {
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
                                    OffsetMin = "-200 50", OffsetMax = "200 100",
                                },
                                new CuiTextComponent
                                {
                                    Align = TextAnchor.MiddleCenter, FontSize = 18,
                                    Text = BuilderInstance.GetMsg("open_loot_builder", _owner.UserIDString)
                                }
                            }
                        });
                        guiOpen = CuiHelper.AddUi(_owner, list);
                        Facepunch.Pool.FreeList(ref list);
                    }
                    
                    if(!Inventory && _owner.serverInput.IsDown(BUTTON.USE))
                    {
                        LootableCorpse corpse = GameManager.server.CreateEntity(StringPool.Get(2604534927), Vector3.zero) as LootableCorpse;
                        corpse.CancelInvoke("RemoveCorpse");

                        corpse.syncPosition = false;
                        corpse.limitNetworking = true;
                        corpse.enableSaving = false;

                        corpse.playerName = _botPlayer.displayName;
                        corpse.playerSteamID = 0;

                        corpse.Spawn();
                        corpse.SetFlag(BaseEntity.Flags.Locked, true);
                        
                        if (corpse.TryGetComponent<Buoyancy>(out var bouyancy)) UnityEngine.Object.Destroy(bouyancy);
                        
                        if (corpse.TryGetComponent<Rigidbody>(out var rb)) UnityEngine.Object.Destroy(rb);

                        corpse.SendAsSnapshot(_owner.Connection);

                        if(!_owner.inventory.loot.StartLootingEntity(corpse, false))
                        {
                            corpse.Kill();
                            return;
                        }
                        
                        _owner.EndLooting();
                        _owner.inventory.loot.Clear();

                        Inventory = corpse.gameObject.AddComponent<BuilderInventory>().Init(_npcPlayer, _owner, this);
                        Inventory.Loot();
                    }

                }
                else
                {
                    if(guiOpen)
                    {
                        guiOpen = !CuiHelper.DestroyUi(_owner, "open_loot_builder");
                    }
                }
                if(bot_paused || Command_paused)
                {
                    float distance = _owner.Distance(_botPlayer.GetNetworkPosition());
                    var pos = (Vector3)current["position"];
                    if(distance > 5f && distance < 50f && (_botPlayer.Distance(pos) < 50f || _owner.Distance(pos) < 50f))
                    {
                        _botPlayer.Teleport(_owner);
                        if(_botSetup.effects.enableSpawnEffects) Effect.server.Run("assets/prefabs/npc/patrol helicopter/effects/rocket_fire.prefab", _botPlayer.transform.position);
                    }
                }
            }

            private void Start()
            {
                if(_botSetup == null) return;
                
                maxHeightBlockFoundation = _botSetup.build.maxHeightBlockFoundation;
                startHeightToGround = _botSetup.build.startHeightToGround;

                StartCoroutine(InitBuilders());
            }

            private void Update()
            {
                if(_bot == null || BuilderInstance == null) return;
                if(!isInitBuilders || _buildCoroutine != null) return;

                if(_buildPoint != Vector3.zero)
                {
                    _bot.SetDestination(_buildPoint);

                    if(Vector3.Distance(_buildPoint, _bot.transform.position) < BuilderInstance._config.Controls.rayLength)
                    {
                        if(_buildCoroutine == null)
                        {
                            _buildCoroutine = StartCoroutine(BuildCoroutine());
                        }
                    }
                }
            }

            private void OnDestroy() 
            {
                if(_stabilityEntities != null) 
                {
                    foreach (var entity in _stabilityEntities)
                    {
                        entity.grounded = false;
                        entity.InitializeSupports();
                        entity.UpdateStability();
                    }
                }

                if(_botPlayer != null && BuilderInstance != null) BuilderInstance.OnPlayerDeath(_botPlayer, null);
                if(_owner != null && BuilderInstance != null && _buildCoroutine != null) BuilderInstance.SendMsg(_owner, "ChatCommand_Error_Abort");
                if(_owner != null) CuiHelper.DestroyUi(_owner, "open_loot_builder");
            }

            #endregion

            #region Max39ru

            public StatusBot Status;
            public BuilderInventory Inventory;
            private ResourcesFromBuild resources = new ResourcesFromBuild();
            private ResourcesFromBuild currentResources = new ResourcesFromBuild();
            private ResourcesFromBuild currentSubtractionResources = new ResourcesFromBuild();
            private bool guiOpen;
            private string path;
            private Core.Configuration.DynamicConfigFile datafile;
            private bool isInitBuilders;
            private List<object> ents;
            private float rotationCorrection = 0f;
            Quaternion quaternionRotation = new Quaternion(0, 0, 0, 1);
            Vector3 eulerRotation;
            private List<Dictionary<string, object>> foundationsObjects = new(), floorObjects = new(), rampObjects = new(),
                roofObjects = new(), stairsObjects = new(), wallObjects = new(), doorObjects = new(), cupObjects = new();
            List<Dictionary<string, object>> todoSorted;
            bool bot_paused = false;
            public bool Command_paused = false;
            Dictionary<string, object> current;
            private IEnumerator InitBuilders()
            {
                if(_botSetup == null || _owner == null || BuilderInstance == null || _npcPlayer == null)
                {
                    yield break;
                }
                
                if(string.IsNullOrEmpty(_botSetup.filename))
                {
                    _owner.ChatMessage("Error: No CopyPaste file configured for this builder!");
                    yield break;
                }
                
                path = "copypaste/" + _botSetup.filename;
                datafile = Interface.Oxide.DataFileSystem.GetDatafile(path);
                
                if(datafile == null || datafile["entities"] == null)
                {
                    _owner.ChatMessage($"Error: CopyPaste file '{_botSetup.filename}' not found!");
                    yield break;
                }
                
                todoSorted = new List<Dictionary<string, object>>();
                BuilderInstance.SendMsg(_owner, "Calculation_of_the_drawing");
                Status = StatusBot.CalculationConstruction;
                yield return InitializationFile();
                isInitBuilders = true;
                _owner.ChatMessage(BuilderInstance.GetMsg("Calculation_resources", _owner.UserIDString) + resources.ToString(_npcPlayer.inventory, BuilderInstance, _owner.UserIDString));
                Status = StatusBot.WaitPoint;
            }

            private IEnumerator InitializationFile()
            {
                ents = datafile["entities"] as List<object>;
                eulerRotation = new Vector3(0f, rotationCorrection, 0f);
                // var preloaddata = new HashSet<Dictionary<string, object>>();
                string prefabname;
                foreach (Dictionary<string, object> data in ents)
                {
                    prefabname = (string)data["prefabname"];
                    if (!data.ContainsKey("grade") && !prefabname.Contains("assets/prefabs/building/door.hinged") &&
                        !prefabname.Contains("assets/prefabs/building/door.double.hinged") &&
                        !prefabname.Contains("assets/prefabs/deployable/tool cupboard/cupboard.tool") &&
                        !prefabname.Contains("assets/prefabs/deployable/tool cupboard/retro/cupboard.tool"))
                        continue;

                    var pos = (Dictionary<string, object>)data["pos"];
                    var rot = (Dictionary<string, object>)data["rot"];

                    data.Add("position", pos);

                    data.Add("rotation", rot);
                    
                    if (data.ContainsKey("items"))
                        data["items"] = new List<object>();
                        
                    bool isDoor = _botSetup.build.Doors.deployDoors && (prefabname.Contains("assets/prefabs/building/door.hinged") || prefabname.Contains("assets/prefabs/building/door.double.hinged"));
                    bool isCup = _botSetup.build.Cup.deployCup && (prefabname.Contains("assets/prefabs/deployable/tool cupboard/cupboard.tool") || prefabname.Contains("assets/prefabs/deployable/tool cupboard/retro/cupboard.tool"));
                    BuildingGrade.Enum? grade = !data.ContainsKey("grade") ? null : (BuildingGrade.Enum)Convert.ToInt32(data["grade"]);

                    if(prefabname.Contains("assets/prefabs/building core/foundation")) 
                    {
                        foundationsObjects.Add(data);
                        resources.Add(BuildCost.GetResources(prefabname), grade);
                        goto Continue;
                    }

                    if(prefabname.Contains("assets/prefabs/building core/floor")) 
                    {
                        floorObjects.Add(data);
                        resources.Add(BuildCost.GetResources(prefabname), grade);
                        goto Continue;
                    }

                    if(prefabname.Contains("assets/prefabs/building core/ramp")) 
                    {
                        rampObjects.Add(data);
                        resources.Add(BuildCost.GetResources(prefabname), grade);
                        goto Continue;
                    }

                    if(prefabname.Contains("assets/prefabs/building core/roof")) 
                    {
                        roofObjects.Add(data);
                        resources.Add(BuildCost.GetResources(prefabname), grade);
                        goto Continue;
                    }

                    if(prefabname.Contains("assets/prefabs/building core/wall")) 
                    {
                        wallObjects.Add(data);
                        resources.Add(BuildCost.GetResources(prefabname), grade);
                        goto Continue;
                    }

                    if(prefabname.Contains("assets/prefabs/building core/stairs")) 
                    {
                        stairsObjects.Add(data);
                        resources.Add(BuildCost.GetResources(prefabname), grade);
                        goto Continue;
                    }

                    if(isDoor)
                    {
                        doorObjects.Add(data);
                        resources.Add(BuildCost.GetResources(prefabname), grade);
                        goto Continue;
                    }
                    if(isCup)
                    {
                        cupObjects.Add(data);
                        resources.Add(BuildCost.GetResources(prefabname), grade);
                        goto Continue;
                    }
                    Continue:
                    yield return CoroutineEx.waitForEndOfFrame;
                }
                
                yield return CoroutineEx.waitForEndOfFrame;
                
                resources.GetResources(_owner.inventory, _botPlayer.inventory.containerMain);
            }
            private IEnumerator InitPosition()
            {
                yield return CoroutineEx.waitForEndOfFrame;

                SetPosition(foundationsObjects);

                yield return CoroutineEx.waitForEndOfFrame;

                SetPosition(floorObjects);

                yield return CoroutineEx.waitForEndOfFrame;

                SetPosition(rampObjects);

                yield return CoroutineEx.waitForEndOfFrame;

                SetPosition(roofObjects);

                yield return CoroutineEx.waitForEndOfFrame;

                SetPosition(wallObjects);

                yield return CoroutineEx.waitForEndOfFrame;

                SetPosition(stairsObjects);

                yield return CoroutineEx.waitForEndOfFrame;

                SetPosition(doorObjects);

                yield return CoroutineEx.waitForEndOfFrame;

                SetPosition(cupObjects);
            }
            private IEnumerator Sorted()
            {
                yield return CoroutineEx.waitForEndOfFrame;

                todoSorted.Clear();

                foundationsObjects = OrderBy(foundationsObjects, (d) => ((Vector3)d["position"] - _npcPlayer.transform.position).sqrMagnitude);
                todoSorted.AddRange(foundationsObjects);

                rampObjects = OrderBy(rampObjects, (d) => ((Vector3)d["position"] - _npcPlayer.transform.position).sqrMagnitude);
                todoSorted.AddRange(rampObjects);

                stairsObjects = OrderBy(stairsObjects, (d) => ((Vector3)d["position"] - _npcPlayer.transform.position).sqrMagnitude);
                todoSorted.AddRange(stairsObjects);
                
                wallObjects = OrderBy(wallObjects, (d) => ((Vector3)d["position"] - _npcPlayer.transform.position).sqrMagnitude);
                todoSorted.AddRange(wallObjects);

                floorObjects = OrderBy(floorObjects, (d) => ((Vector3)d["position"] - _npcPlayer.transform.position).sqrMagnitude);
                todoSorted.AddRange(floorObjects);

                roofObjects = OrderBy(roofObjects, (d) => ((Vector3)d["position"] - _npcPlayer.transform.position).sqrMagnitude);
                todoSorted.AddRange(roofObjects);

                doorObjects = OrderBy(doorObjects, (d) => ((Vector3)d["position"] - _npcPlayer.transform.position).sqrMagnitude);
                cupObjects = OrderBy(cupObjects, (d) => ((Vector3)d["position"] - _npcPlayer.transform.position).sqrMagnitude);
            }
            
            private void SetPosition(List<Dictionary<string, object>> entities)
            {
                foreach (Dictionary<string, object> entity in entities)
                {
                    var pos = (Dictionary<string, object>)entity["pos"];
                    var rot = (Dictionary<string, object>)entity["rot"];
                    
                    entity["position"] = 
                        quaternionRotation * new Vector3(Convert.ToSingle(pos["x"]), Convert.ToSingle(pos["y"]),
                        Convert.ToSingle(pos["z"])) + _buildPoint;
                        

                    entity["rotation"] = 
                        Quaternion.Euler(eulerRotation + new Vector3(Convert.ToSingle(rot["x"]),
                        Convert.ToSingle(rot["y"]), Convert.ToSingle(rot["z"])) * 57.2958f);
                }
            }
            private void RemoveBuild(Dictionary<string, object> remove)
            {
                doorObjects.Remove(remove);
                cupObjects.Remove(remove);
                todoSorted.Remove(remove);
            }
            private bool CheckCost()
            {
                if(!_botSetup.resources.requireResources) return true;

                currentResources.SubtractionResourcesFromPlayer(_npcPlayer.inventory, currentSubtractionResources);
                resources.Subtraction(currentSubtractionResources);

                return currentResources.CanBuild();
            }
            public void EndLoot()
            {
                bot_paused = false;
                Command_paused = false;
            }


            #endregion Max39ru

            #region CopyPaste Methods

            private static bool HasGrade(BuildingBlock block, BuildingGrade.Enum grade, ulong skin) // CopyPaste
            {
                foreach (var constructionGrade in block.blockDefinition.grades)
                {
                    var baseGrade = constructionGrade.gradeBase;
                    if (baseGrade.type == grade && baseGrade.skin == skin)
                        return true;
                }

                return false;
            }

            private bool CheckPlaced(string prefabname, Vector3 pos, Quaternion rot) // CopyPaste
            {
                var ents = Facepunch.Pool.GetList<BaseEntity>();

                try
                {
                    Vis.Entities(pos, 0.01f, ents);

                    foreach (var ent in ents)
                    {
                        if (ent.PrefabName != prefabname 
                            || Vector3.Distance(ent.transform.position, pos) > 0.01f 
                                || Vector3.Distance(ent.transform.rotation.eulerAngles, rot.eulerAngles) > 0.01f) continue;

                        return true;
                    }

                    return false;
                }
                finally
                {
                    Facepunch.Pool.FreeList(ref ents);
                }
            }

            #endregion

            #region Rust Check Methods

            private bool TestPlacingCloseToRoad(Vector3 pos, Quaternion rot)
            {
                if (TerrainMeta.HeightMap == null || TerrainMeta.TopologyMap == null) return true;

                OBB obb = new OBB(pos, Vector3.one, rot);
                float num = Mathf.Abs(TerrainMeta.HeightMap.GetHeight(obb.position) - obb.position.y);

                if (num > 9.0) return true;

                float radius = Mathf.Lerp(3f, 0.0f, num / 9f);
                Vector3 position = obb.position, point1 = obb.GetPoint(-1f, 0.0f, -1f), point2 = obb.GetPoint(-1f, 0.0f, 1f), point3 = obb.GetPoint(1f, 0.0f, -1f), point4 = obb.GetPoint(1f, 0.0f, 1f);

                return ((TerrainMeta.TopologyMap.GetTopology(position, radius) | TerrainMeta.TopologyMap.GetTopology(point1, radius) | TerrainMeta.TopologyMap.GetTopology(point2, radius) | TerrainMeta.TopologyMap.GetTopology(point3, radius) | TerrainMeta.TopologyMap.GetTopology(point4, radius)) & 526336) == 0;
            }
            
            #endregion

            #region List Methods

            public static List<T> OrderBy<T, TKey>(List<T> collection, Func<T, TKey> keySelector)
            {
                // Convert the list to an array for in-place sorting
                T[] array = collection.ToArray();

                // Implement a simple sorting algorithm (e.g., Bubble Sort)
                for (int i = 0; i < array.Length - 1; i++)
                {
                    for (int j = 0; j < array.Length - i - 1; j++)
                    {
                        TKey key1 = keySelector(array[j]);
                        TKey key2 = keySelector(array[j + 1]);

                        // Compare keys and swap elements if needed
                        if (Comparer<TKey>.Default.Compare(key1, key2) > 0)
                        {
                            T temp = array[j];
                            array[j] = array[j + 1];
                            array[j + 1] = temp;
                        }
                    }
                }

                // Convert the sorted array back to a list
                List<T> result = new List<T>(array);
                return result;
            }

            #endregion

            #region Build Check Methods

            protected IEnumerator CheckFoundationsGround(List<Dictionary<string, object>> foundationsData)
            {
                if(foundationsData == null || foundationsData.Count == 0) yield break;
                yield return CoroutineEx.waitForSeconds(_botSetup.speed.timeCheckFoundation);

                _myHeight = 0f;

                for(int i = 0; i < foundationsData.Count; i++)
                {
                    try
                    {
                        var lowestPos = (Dictionary<string, object>)foundationsData[i]["position"];
                        
                        Vector3 lowestNewPos = quaternionRotation * new Vector3(Convert.ToSingle(lowestPos["x"]), Convert.ToSingle(lowestPos["y"]),
                                Convert.ToSingle(lowestPos["z"])) + _buildPoint + new Vector3(0, _myHeight + startHeightToGround, 0);
                        
                        var lowestHeight = TerrainMeta.HeightMap.GetHeight(lowestNewPos);
                        
                        if(lowestNewPos.y < lowestHeight)
                        {
                            _myHeight += lowestHeight - lowestNewPos.y;
                            i = -1;

                            continue;
                        }

                        if(lowestNewPos.y > lowestHeight && lowestNewPos.y - lowestHeight > maxHeightBlockFoundation) yield break;
                        if(lowestHeight < (BotSetup.build.maxDepthInWater * -1f)) yield break;

                        Vector3 p2 = lowestNewPos - new Vector3(0, 3f, 0);
                        var raycastHits = Physics.CapsuleCastAll(lowestNewPos, p2, 4f, (p2 - lowestNewPos).normalized, 3f, LayerMask.GetMask("Construction"));

                        if(raycastHits.Length > 0) yield break;

                        if(_botSetup.build.Checks.checkIsCloseToRoad)
                        {
                            if(!TestPlacingCloseToRoad(lowestNewPos, quaternionRotation))
                            {
                                BuilderInstance.SendMsg(_owner, "CopyPaste_Error_CloseToRoad");
                                _buildPoint = Vector3.zero;
                                _buildCoroutine = null;
                                _isNullGround = false;

                                yield break;
                            }
                        }

                        if(_botSetup.build.Checks.checkIsBuildingBlocked)
                        {
                            if(_owner.IsBuildingBlocked(lowestNewPos, quaternionRotation, new Bounds()))
                            {
                                BuilderInstance.SendMsg(_owner, "CopyPaste_Error_BuildBlocked");
                                
                                _buildPoint = Vector3.zero;
                                _buildCoroutine = null;
                                _isNullGround = false;

                                yield break;
                            }
                        }
                    }
                    catch
                    {
                        continue;
                    }

                    yield return CoroutineEx.waitForSeconds(_botSetup.speed.timeCheckFoundation);
                }

                _isNullGround = true;

                yield break;
            }

            protected IEnumerator CheckedBuildBlocksCoroutine(List<Dictionary<string, object>> blocksData, int? layers)
            {
                yield return CoroutineEx.waitForSeconds(_botSetup.speed.timeCheckBuildingBlock);

                if(blocksData == null || blocksData.Count == 0) yield break;

                bool isLayers = layers != null;
                int _layers = !isLayers ? 0 : (int)layers;

                Vector3? lastPosition = null;
                _canBuildBlocks = true;
                
                foreach(Dictionary<string, object> entity in blocksData)
                {
                    RaycastHit hitInfo = new();

                    try
                    {
                        Vector3 lowestNewPos = (Vector3)entity["position"];
                        
                        if(lastPosition == null)
                        {
                            lastPosition = lowestNewPos;
                            continue;
                        }

                        bool layersResult = isLayers ? !Physics.Linecast((Vector3)lastPosition, lowestNewPos, out hitInfo, _layers) : true;

                        if(isLayers && _botSetup.build.Checks.checkForPreventBuilding) 
                        {
                            if(hitInfo.collider?.transform?.parent != null)
                            {
                                if(hitInfo.collider.transform.parent.name.Contains("cave"))
                                {
                                    layersResult = true;
                                }
                            }
                        }

                        if(_botSetup.build.Checks.checkIsCloseToRoad)
                        {
                            if(!TestPlacingCloseToRoad(lowestNewPos, quaternionRotation))
                            {
                                BuilderInstance.SendMsg(_owner, "CopyPaste_Error_CloseToRoad");
                                _buildPoint = Vector3.zero;
                                _buildCoroutine = null;

                                _canBuildBlocks = false;

                                yield break;
                            }
                        }

                        if(_botSetup.build.Checks.checkIsBuildingBlocked)
                        {
                            if(_owner.IsBuildingBlocked(lowestNewPos, quaternionRotation, new Bounds()))
                            {
                                BuilderInstance.SendMsg(_owner, "CopyPaste_Error_BuildBlocked");
                                
                                _buildPoint = Vector3.zero;
                                _buildCoroutine = null;

                                _canBuildBlocks = false;

                                yield break;
                            }
                        }

                        _canBuildBlocks = isLayers ? layersResult : !Physics.Linecast((Vector3)lastPosition, lowestNewPos, out hitInfo);
                        
                        lastPosition = lowestNewPos;

                        if(_botSetup.build.Checks.checkForPreventBuilding)
                        {
                            var physResults = new List<Collider>(Physics.OverlapSphere(lowestNewPos, _botSetup.build.Checks.preventBuildingRadius, LayerMask.GetMask("Prevent Building")));

                            physResults.RemoveAll((col) =>
                            {
                                if(col.transform.parent != null)
                                {
                                    if(col.transform.parent.name.Contains("cave"))
                                    {
                                        return true;
                                    }
                                }

                                return false;
                            });

                            if(physResults.Count != 0)
                            {
                                BuilderInstance.SendMsg(_owner, "CopyPaste_Error_BuildBlocked");
                                _buildPoint = Vector3.zero;
                                _buildCoroutine = null;
                                _canBuildBlocks = false;
    
                                yield break;
                            }
                        }
                    }
                    catch
                    {
                        continue;
                    }

                    if(!_canBuildBlocks) yield break;
                    
                    yield return CoroutineEx.waitForSeconds(_botSetup.speed.timeCheckBuildingBlock);
                }

                yield break;
            }

            #endregion

            #region Main Stack

            private IEnumerator BuildCoroutine()
            {
                eulerRotation = new Vector3(0f, rotationCorrection, 0f);
                quaternionRotation = Quaternion.Euler(eulerRotation);
                
                _stabilityEntities = new List<StabilityEntity>();

                BuilderInstance.SendMsg(_owner, "ChatCommand_Notice_Check");
                Status = StatusBot.CheckedTerritory;
                
                yield return CheckFoundationsGround(foundationsObjects);
                
                if(!_isNullGround)
                {
                    BuilderInstance.SendMsg(_owner, "CopyPaste_Error_NotSuitablePlace");

                    _buildPoint = Vector3.zero;
                    _buildCoroutine = null;
                    Status = StatusBot.WaitPoint;

                    yield break;
                }
                
                _buildPoint += new Vector3(0, _myHeight + startHeightToGround, 0);
                yield return InitPosition();

                List<string> layerMasks = new List<string> {
                    "Construction", "World", "Prevent Movement"
                };

                if(_botSetup.build.Checks.checkForPreventBuilding) layerMasks.Add("Prevent Building");

                int layersMask = LayerMask.GetMask(layerMasks.ToArray());
                
                yield return CheckedBuildBlocksCoroutine(wallObjects, layersMask);

                if(!_canBuildBlocks)
                {
                    BuilderInstance.SendMsg(_owner, "CopyPaste_Error_WallBlocked");
                    _buildPoint = Vector3.zero;
                    _buildCoroutine = null;
                    Status = StatusBot.WaitPoint;

                    yield break;
                }
                
                yield return CheckedBuildBlocksCoroutine(roofObjects, layersMask);

                if(!_canBuildBlocks)
                {
                    BuilderInstance.SendMsg(_owner, "CopyPaste_Error_RoofBlocked");
                    _buildPoint = Vector3.zero;
                    _buildCoroutine = null;
                    Status = StatusBot.WaitPoint;

                    yield break;
                }

                yield return Sorted();
                
                BuilderInstance.SendMsg(_owner, "ChatCommand_Success_Start");
                Status = StatusBot.Building;

                _bot.SetNavMeshEnabled(false);
                _bot.Pause();

                _bot.CanUseBaseNav = false;
                _bot.CanUseCustomNav = false;
                _bot.CanUseNavMesh = false;
                _bot.CanUseAStar = false;

                _bot.DefaultArea = "Not Walkable";  

                _bot.Speed = 0f;
                _bot.Agent.enabled = false;
                _bot.enabled = false;

                List<Dictionary<string, object>> _todoSorted = Facepunch.Pool.GetList<Dictionary<string, object>>();
                _todoSorted.AddRange(todoSorted);

                yield return SpawnObjects(_todoSorted, true);

                Facepunch.Pool.FreeList(ref _todoSorted);

                if(_botSetup.build.Doors.deployDoors)
                {
                    List<Dictionary<string, object>> _doorObjects = Facepunch.Pool.GetList<Dictionary<string, object>>();
                    _doorObjects.AddRange(doorObjects);

                    yield return SpawnObjects(_doorObjects);
                    
                    Facepunch.Pool.FreeList(ref _doorObjects);
                }

                if(_botSetup.build.Cup.deployCup)
                {
                    List<Dictionary<string, object>> _cupObjects = Facepunch.Pool.GetList<Dictionary<string, object>>();
                    _cupObjects.AddRange(cupObjects);

                    yield return SpawnObjects(_cupObjects);
                    
                    Facepunch.Pool.FreeList(ref _cupObjects);
                }

                foreach (var entity in _stabilityEntities)
                {
                    if(entity == null) continue;

                    entity.grounded = false;
                    entity.InitializeSupports();
                    entity.UpdateStability();
                }


                _stabilityEntities = new List<StabilityEntity>();
                _buildPoint = Vector3.zero;

                _npcPlayer.SetAimDirection(_owner.transform.position - _npcPlayer.transform.position);
                _npcPlayer.UpdateActiveItem(new ItemId());

                yield return null;

                if(_botSetup.limitations.enableLimits)
                {
                    if(!BuilderInstance._data.LimitsInfo.ContainsKey(_owner.userID)) BuilderInstance._data.LimitsInfo.Add(_owner.userID, new DataFile.LimitInfo());

                    var playerInfo = BuilderInstance._data.LimitsInfo[_owner.userID];

                    if(playerInfo != null)
                    {
                        string perm = "";

                        foreach(var pair in BuilderInstance._config.permissionBot)
                        {
                            if(pair.Value.Contains(_botSetup)) 
                            {
                                perm = pair.Key;
                                break;
                            }
                        }

                        if(!playerInfo.LimitsByPermission.ContainsKey(perm)) playerInfo.LimitsByPermission.Add(perm, 0);

                        int currentBuilt = playerInfo.LimitsByPermission[perm];
                        currentBuilt++;

                        playerInfo.LimitsByPermission[perm] = currentBuilt;
                        BuilderInstance._data.LimitsInfo[_owner.userID] = playerInfo;

                        BuilderInstance.SaveData();
                    }
                }

                BuilderInstance.SendMsg(_owner, "ChatCommand_Success_Built");
                BuilderInstance.DropLoot(_npcPlayer, this, null);

                if(_botSetup.effects.enableSpawnEffects) Effect.server.Run("assets/prefabs/npc/patrol helicopter/effects/rocket_fire.prefab", _npcPlayer.transform.position);

                _buildPoint = Vector3.zero;
                _buildCoroutine = null;

                _npcPlayer.Kill();

                yield break;
            }
            private IEnumerator SpawnObjects(List<Dictionary<string, object>> objects, bool isBuildBlock = false)
            {
                foreach (var data in objects)
                {
                    uint buildingID = 0;
                    var prefabname = (string)data["prefabname"];
                    var skinid = ulong.Parse(data["skinid"].ToString());
                    var pos = (Vector3)data["position"];
                    var rot = (Quaternion)data["rotation"];

                    if(isBuildBlock)
                    {
                        if (CheckPlaced(prefabname, pos, rot)) continue;

                        if (prefabname.Contains("pillar") || prefabname.Contains("locks")) continue;
                    }
                    BuildingGrade.Enum? grade = isBuildBlock && data.ContainsKey("grade") ? (BuildingGrade.Enum)Convert.ToInt32(data["grade"]) : null;
                    currentResources.AddNew(BuildCost.GetResources(prefabname), grade);
                    current = data;

                    checkCost:
                    if(!CheckCost())
                    {
                        bot_paused = true;
                        _owner.ChatMessage(BuilderInstance.GetMsg("ChatCommand_Error_NoResources", _owner.UserIDString) + resources.ToString(_npcPlayer.inventory, BuilderInstance, _owner.UserIDString));
                    }
                    else
                    {
                        bot_paused = false;
                        if(!Command_paused) goto create;
                    }

                    while(bot_paused || Command_paused) yield return CoroutineEx.waitForSeconds(0.5f);
                    goto checkCost;

                    create:

                    var entity = GameManager.server.CreateEntity(prefabname, pos, rot);

                    if (entity == null)
                        continue;

                    _npcPlayer.UpdateActiveItem(_planID);

                    RemoveBuild(data);

                    var transform = entity.transform;

                    transform.position = pos;
                    transform.rotation = rot;

                    entity.SendMessage("SetDeployedBy", _owner, SendMessageOptions.DontRequireReceiver);
                    
                    entity.OwnerID = _owner.userID;

                    BuildingBlock buildingBlock = entity as BuildingBlock;
                    if(buildingBlock != null)
                    {
                        buildingBlock.blockDefinition = PrefabAttribute.server.Find<Construction>(buildingBlock.prefabID);
                    }

                    if (entity is DecayEntity decayEntity)
                    {
                        if (buildingID == 0)
                            buildingID = BuildingManager.server.NewBuildingID();

                        decayEntity.AttachToBuilding(buildingID);
                    }
                    if (entity is StabilityEntity stabilityEntity)
                    {
                        if (!stabilityEntity.grounded)
                        {
                            stabilityEntity.grounded = true;
                            _stabilityEntities.Add(stabilityEntity);
                        }
                    }

                    // Teleport directly to build position instead of walking
                    if(_bot != null)
                        _bot.transform.position = entity.transform.position;
                    else if(_npcPlayer != null)
                        _npcPlayer.transform.position = entity.transform.position;
                    
                    if(_npcPlayer != null)
                        _npcPlayer.SetAimDirection(entity.transform.position - _npcPlayer.transform.position);

                    entity.skinID = skinid;
                    entity.Spawn();

                    ShowArrow(entity.transform.position, Color.red);
                    
                    if (buildingBlock != null)
                    {
                        if(_botSetup.effects.enableUpgradeEffects) Effect.server.Run("assets/bundled/prefabs/fx/build/frame_place.prefab", entity.transform.position);
                   
                        var _grade = grade == null ? BuildingGrade.Enum.Twigs : (BuildingGrade.Enum)grade;
                        if (skinid != 0ul && !HasGrade(buildingBlock, _grade, skinid))
                            skinid = 0ul;
                            entity.skinID = skinid;

                        _npcPlayer.UpdateActiveItem(_hammerID);
                        yield return null;

                        if(buildingBlock != null)
                        {
                            buildingBlock.ChangeGrade(_grade); 
                            
                            if(_botSetup.effects.enableUpgradeEffects)
                            {
                                if(_grade == BuildingGrade.Enum.Wood) 
                                {
                                    Effect.server.Run("assets/bundled/prefabs/fx/build/promote_wood.prefab", _npcPlayer.transform.position);
                                    Effect.server.Run("assets/bundled/prefabs/fx/build/construction_upgrade_wood.prefab", _npcPlayer.transform.position);
                                }
                                else if(_grade == BuildingGrade.Enum.Metal) 
                                {
                                    Effect.server.Run("assets/bundled/prefabs/fx/build/promote_metal.prefab", _npcPlayer.transform.position);
                                    Effect.server.Run("assets/bundled/prefabs/fx/build/construction_upgrade_metal.prefab", _npcPlayer.transform.position);
                                }
                                else if(_grade == BuildingGrade.Enum.Stone) 
                                {
                                    Effect.server.Run("assets/bundled/prefabs/fx/build/promote_stone.prefab", _npcPlayer.transform.position);
                                    Effect.server.Run("assets/bundled/prefabs/fx/build/construction_upgrade_stone.prefab", _npcPlayer.transform.position);
                                }
                                else if(_grade == BuildingGrade.Enum.TopTier) 
                                {
                                    Effect.server.Run("assets/bundled/prefabs/fx/build/construction_upgrade_toptier.prefab", _npcPlayer.transform.position);
                                    Effect.server.Run("assets/bundled/prefabs/fx/build/promote_toptier.prefab", buildingBlock.transform.position);
                                }
                            }

                            buildingBlock.SetHealthToMax();
                            buildingBlock.UpdateSkin();
                            buildingBlock.SendNetworkUpdate();
                            buildingBlock.ResetUpkeepTime();
                            
                            if (data.TryGetValue("customColour", out object customColour))
                                buildingBlock.SetCustomColour(Convert.ToUInt32(customColour));
                        }
                    }
                    else if (entity is BaseCombatEntity baseCombat) baseCombat.SetHealth(baseCombat.MaxHealth());

                    if(entity != null)
                    {
                        if(entity is BuildingPrivlidge cup)
                        {
                            if(_botSetup.build.Cup.canAuth && !cup.IsAuthed(_owner)) cup.AddPlayer(_owner, _owner.userID);
                            cup.SendNetworkUpdate();
                        }

                        var flagsData = new Dictionary<string, object>();

                        if (data.ContainsKey("flags"))
                            flagsData = data["flags"] as Dictionary<string, object>;

                        var flags = new Dictionary<BaseEntity.Flags, bool>();

                        foreach (var flagData in flagsData)
                        {
                            BaseEntity.Flags baseFlag;
                            if (Enum.TryParse(flagData.Key, out baseFlag))
                                flags.Add(baseFlag, Convert.ToBoolean(flagData.Value));
                        }

                        foreach (var flag in flags) entity.SetFlag(flag.Key, flag.Value);
                    }

                    yield return null;
                }
            }
            public void OnInput(InputState state)
            {
                if(Status == StatusBot.WaitPoint)
                {
                    RaycastHit hit;

                    if(Physics.Raycast(_owner.eyes.HeadRay(), out hit, BuilderInstance._config.Controls.rayLength)) 
                    {
                        ShowArrow(hit.point);
                        _buildPoint = hit.point;
                        rotationCorrection = _owner.viewAngles.y;
                        Status = StatusBot.CheckedTerritory;
                        if(_botPlayer.Distance(_buildPoint) > BuilderInstance._config.Controls.rayLength)
                        {
                            _botPlayer.Teleport(_owner);
                    
                            if(_botSetup.effects.enableSpawnEffects) Effect.server.Run("assets/prefabs/npc/patrol helicopter/effects/rocket_fire.prefab", _botPlayer.transform.position);
                        }
                    }
                }
                else
                {
                    PrintStatus();
                }
                
            }
            
            #endregion

            #region Methods

            public void ShowArrow(Vector3 pos, Color color = new Color())
            {
                if(!BuilderInstance._config.Controls.enableArrowView) return;
                if(color == new Color()) color = Color.white;

                if(!_owner.IsAdmin) 
                {
                    _owner.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, true);
                    _owner.SendNetworkUpdateImmediate();

                    _owner.SendConsoleCommand("ddraw.arrow", BuilderInstance._config.Controls.arrowViewDuration, color, pos + new Vector3(0f, pos.y + 5), pos, 1.5f);
                   
                    _owner.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, false);
                    _owner.SendNetworkUpdateImmediate();
                }
                else 
                {
                    _owner.SendConsoleCommand("ddraw.arrow", BuilderInstance._config.Controls.arrowViewDuration, color, pos + new Vector3(0f, pos.y + 5), pos, 1.5f);
                }
            }

            public void PrintStatus()
            {
                if(bot_paused)
                {
                    _owner.ChatMessage(BuilderInstance.GetMsg("ChatCommand_Error_NoResources", _owner.UserIDString) + resources.ToString(_npcPlayer.inventory, BuilderInstance, _owner.UserIDString));
                    return;
                }
                if(Command_paused)
                {
                    BuilderInstance.SendMsg(_owner, "CommandPaused");
                    return;
                }
                switch(Status)
                {
                    case StatusBot.CalculationConstruction: BuilderInstance.SendMsg(_owner, "BotCalculated"); return;
                    case StatusBot.CheckedTerritory: BuilderInstance.SendMsg(_owner, "BotCheckedTerritory"); return;
                    case StatusBot.Building: BuilderInstance.SendMsg(_owner, "BotBuilding"); return;
                }
            }

            #endregion

            #region Static

            public static PlayerBuilderController CreateBot(BasePlayer caller, Configuration.BotSetup botSetup, Configuration.ItemInfo? itemInfo, BuildCostInfo info, PersonalBuilder pluginInstance, Vector3? pos = null)
            {
                NPCPlayer bot = GameManager.server.CreateEntity("assets/rust.ai/agents/npcplayer/pet/frankensteinpet.prefab", pos != null ? (Vector3)pos : caller.transform.position) as NPCPlayer;

                bot.userID = (ulong)UnityEngine.Random.Range(1, 100000);
                bot.UserIDString = bot.userID.ToString();

                bot.Spawn();
                if(botSetup.effects.enableSpawnEffects) Effect.server.Run("assets/prefabs/npc/patrol helicopter/effects/rocket_fire.prefab", bot.transform.position);

                bot.InitializeHealth(botSetup.player.health, botSetup.player.health);
                bot.inventory.Strip();

                if(botSetup.player.lockBeltContainer) bot.inventory.containerBelt.SetLocked(true);
                if(botSetup.player.lockWearContainer) bot.inventory.containerWear.SetLocked(true);

                if(botSetup.startKit.Count != 0)
                {
                    foreach(var item in botSetup.startKit)
                    {
                        var cloth = ItemManager.CreateByName(item.shortname, item.amount, item.skin);
                        if(!string.IsNullOrEmpty(item.name)) cloth.name = item.name;

                        cloth.MoveToContainer(bot.inventory.containerWear);
                    }
                }

                var plannerItem = ItemManager.CreateByName("building.planner");
                plannerItem.MoveToContainer(bot.inventory.containerBelt);

                var hammerItem = ItemManager.CreateByName("hammer");
                hammerItem.MoveToContainer(bot.inventory.containerBelt);

                bot.UpdateActiveItem(plannerItem.uid);
                bot.displayName = botSetup.player.displayName;
                
                var controller = bot.gameObject.AddComponent<PlayerBuilderController>();
                
                controller._botSetup = botSetup;
                controller._botItemInfo = itemInfo;

                controller._owner = caller;
                controller._botPlayer = bot;
                controller._npcPlayer = bot;
                
                controller._planID = plannerItem.uid;
                controller._hammerID = hammerItem.uid;

                controller.BuilderInstance = pluginInstance;
                controller.BuildCost = info;

                controller._bot = bot.GetComponent<BaseNavigator>();
                controller._bot.StoppingDistance = 0.01f;

                return controller;
            }
        
            #endregion

            public enum StatusBot
            {
                CalculationConstruction,
                CheckedTerritory,
                WaitPoint,
                Building
            }
        }

        public class BuilderInventory : FacepunchBehaviour
        {
            private NPCPlayer bot;
            private BasePlayer owner;
            private LootableCorpse corpse;
            private PlayerBuilderController controller;

            #region UnityCallbacks

            private void Awake()
            {
                corpse = GetComponent<LootableCorpse>();
            }
                
            #endregion UnityCallbacks

            #region Methods

            public BuilderInventory Init(NPCPlayer bot, BasePlayer owner, PlayerBuilderController _controller)
            {
                this.bot = bot;
                this.owner = owner;
                controller = _controller;
                return this;
            }

            public bool IsOwner(BasePlayer player) => player == owner;
            public void Kill()
            {
                controller.EndLoot();
                corpse.Kill();
            }

            public void Loot()
            {
                owner.inventory.loot.AddContainer(bot.inventory.containerMain);
                owner.inventory.loot.AddContainer(bot.inventory.containerWear);
                owner.inventory.loot.AddContainer(bot.inventory.containerBelt);
                
                owner.inventory.loot.SendImmediate();
                owner.inventory.loot.MarkDirty();
                
                owner.ClientRPC(RpcTarget.Player("RPC_OpenLootPanel", owner), "player_corpse");
            }
                
            #endregion Methods

        }

        #endregion
    }
}