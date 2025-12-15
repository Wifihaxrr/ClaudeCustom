using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using System.Numerics;
using Oxide.Core.Libraries.Covalence;
using Oxide.Game.Rust.Libraries;
using System;
using Oxide.Core.Plugins;
using Oxide.Core;
using Oxide.Core.Configuration;
using System.Linq;
using HarmonyLib;

namespace Oxide.Plugins
{
    [Info("Dungeon Bases", "Fruster", "1.1.4")]
    [Description("Dungeon Bases")]

    public class DungeonBases : CovalencePlugin
    {
        private const int layerS = ~(1 << 2 | 1 << 3 | 1 << 4 | 1 << 10 | 1 << 18 | 1 << 28 | 1 << 29);
        private ConfigData Configuration = new();
        private List<Door> hatchPair = new();
        private List<PowerCounter> counterList = new();
        private List<Vector3> blackList = new();
        private List<BasePlayer> dungeonPlayers = new();
        public static List<BaseEntity> entitiesList = new();
        public static List<BaseEntity> npcList = new();
        public static List<Door> doorsList = new();
        private List<BuildingPrivlidge> tcList = new();
        private List<AutoTurret> turretsList = new();
        private string[] doorNameList = {
                                            "assets/prefabs/building/door.double.hinged/door.double.hinged.metal.prefab",
                                            "assets/prefabs/building/door.double.hinged/door.double.hinged.toptier.prefab",
                                            "assets/prefabs/building/door.double.hinged/door.double.hinged.wood.prefab",
                                            "assets/prefabs/misc/permstore/factorydoor/door.hinged.industrial.d.prefab",
                                            "assets/prefabs/building/door.hinged/door.hinged.wood.prefab",
                                            "assets/prefabs/building/door.hinged/door.hinged.metal.prefab",
                                            "assets/prefabs/building/door.hinged/door.hinged.toptier.prefab",
                                            "assets/prefabs/building/wall.frame.garagedoor/wall.frame.garagedoor.prefab",
                                            "assets/prefabs/building/wall.frame.shopfront/wall.frame.shopfront.prefab",
                                            "assets/prefabs/building/wall.frame.cell/wall.frame.cell.gate.prefab",
                                            "assets/prefabs/building/wall.frame.fence/wall.frame.fence.gate.prefab",
                                            "assets/prefabs/misc/decor_dlc/bardoors/door.double.hinged.bardoors.prefab"
                                        };
        private RaycastHit hit;
        private Vector3 basePosition = Vector3.up * 2000;
        private bool isEventActive = false;
        private Timer eventTimer;
        private int remain;
        private int duration;
        private int eventCooldown = 0;
        private int ownerTimerRemain = -1;
        private bool inception;
        private ulong eventOwnerID;
        private ulong superCardSkinID = 1988408422;
        private int entranceIndex = 0;
        private int dungeonIndex = 0;
        private BasePlayer eventOwner;
        private MapMarkerGenericRadius marker;
        private VendingMachineMapMarker vending;
        DynamicConfigFile eventData = null;
        DynamicConfigFile npcData = null;
        DynamicConfigFile playersData = null;
        public class ObjectSettings
        {
            public string name { get; set; }
            public string prefab { get; set; }
            public string objectID { get; set; }
        }

        public class NPCSettings
        {
            public float health { get; set; }
            public string name { get; set; }
            public bool isStatic { get; set; }
            public float x { get; set; }
            public float y { get; set; }
            public float z { get; set; }
            public string prefab { get; set; }
            public string code { get; set; }
            public string weapon { get; set; }
            public string tableName { get; set; }
            public int tableMinItems { get; set; }
            public int tableMaxItems { get; set; }
            public string objectID { get; set; }
            public string kit { get; set; }
            public float dmgScale { get; set; }
        }

        [PluginReference] Plugin CopyPaste, SimpleLootTable, SuperCard, Kits;

        private class CardReaderAdd : FacepunchBehaviour
        {
            public Door door;
        }

        public class SlabNPC : FacepunchBehaviour
        {
            public Timer timer;
            public Timer timerAI;
            public Vector3 moveTarget;
            public List<Vector3> wayPoints = new();
            public BaseEntity mainTarget;
            public bool targetIsVisible;
            public bool contact;
            public bool isStatic;
            public float range;
            public string code;
            public string weapon;
            public string tableName;
            public int tableMinItems;
            public int tableMaxItems;
            public string kit;
            public float dmgScale;
        }

        private class HatchComponent : FacepunchBehaviour
        {
            public Vector3 position;
            public float time;
            public bool entrance;
        }
        private class ConfigData
        {
            [JsonProperty("Allow only the event owner (the one who entered the dungeon first) into the dungeon")]
            public bool onlyOwner = true;
            [JsonProperty("Allow owner's teammates to enter the dungeon")]
            public bool teammates = true;
            [JsonProperty("Time before ownership is lost after leaving the server(in seconds)")]
            public int ownerLeaveTimer = 300;
            [JsonProperty("Event marker on the map")]
            public bool eventMarker = true;
            [JsonProperty("Event marker name")]
            public string markerName = "Dungeon Base";
            [JsonProperty("Event marker transparency(0-1)")]
            public float markerAlpha = 0.55f;
            [JsonProperty("Event marker radius")]
            public float markerRadius = 0.5f;
            [JsonProperty("Event marker color.R(0-1)")]
            public float markerColorR = 1.0f;
            [JsonProperty("Event marker color.G(0-1)")]
            public float markerColorG = 0f;
            [JsonProperty("Event marker color.B(0-1)")]
            public float markerColorB = 0f;
            [JsonProperty("Display event owner name on marker")]
            public bool markerOwnerName = true;
            [JsonProperty("Display the time remaining until the end of the event on the marker")]
            public bool markerTime = true;
            [JsonProperty("Autostart event(disable if you want to trigger the event only manually)")]
            public bool autoStart = true;
            [JsonProperty("Calculate the time until the next event only after the previous one has finished")]
            public bool afterTime = false;
            [JsonProperty("Minimum time to event start(in seconds)")]
            public int minimumRemainToEvent = 3600;
            [JsonProperty("Maximum time to event start(in seconds)")]
            public int maximumRemainToEvent = 7200;
            [JsonProperty("Minimum event duration(in seconds)")]
            public int minimumEventDuration = 2000;
            [JsonProperty("Maximum event duration(in seconds)")]
            public int maximumEventDuration = 3000;
            [JsonProperty("Minimum number of online players to trigger an event")]
            public int minOnline = 1;
            [JsonProperty("List of NPC names")]
            public List<string> npcNamesList = new List<string> { "Dungeon NPC", "Dungeon Keeper", "Dungeon guard" };
            [JsonProperty("Dungeons list")]
            public List<string> dungeonList = new List<string> { "#dung#base1", "#dung#base2", "#dung#base3", "#dung#base4" };
            [JsonProperty("Entrances list")]
            public List<string> entrancesList = new List<string> { "#dung#entrance1", "#dung#entrance2", "#dung#entrance3", "#dung#entrance4" };
            [JsonProperty("Random order of choosing a dungeon from the list (if false, will be selected in turn)")]
            public bool randomDungList = true;
            [JsonProperty("Random order of choosing the entrance to the dungeon from the list (if false, will be selected in turn)")]
            public bool randomEntranceList = true;
            [JsonProperty("Change the time of day when entering the dungeon(from 0 to 23, if -1 - do not change the time)")]
            public float dungTime = 0;
            [JsonProperty("How long before the end of the event does radiation start to affect players inside the dungeon")]
            public int radiationTime = 180;
            [JsonProperty("How long before the event ends will a warning message be displayed to players")]
            public int warningMessageTime = 300;
            [JsonProperty("How long after the event ends should the entrance be destroyed")]
            public int destroyTime = 60;
            [JsonProperty("Close the entrance and exit to the dungeon when the event time is over")]
            public bool closeEvent = true;
            [JsonProperty("Will autoturrets attack NPCs")]
            public bool turretNpcAttack = false;
            [JsonProperty("Will flameturrets and guntraps attack NPCs")]
            public bool guntrapNpcAttack = false;
            [JsonProperty("Save event data (If true, the event will be saved and will continue even if you restart the server or plugin. Disable this if you get lag when saving)")]
            public bool saveEvent = true;
            [JsonProperty("SteamID for chat message icon")]
            public ulong iconID = 0;
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>()
            {
                { "EntryDeniedMessage", "You cannot enter the dungeon without being the owner of the event or its teammate" },
                { "StartMessage", "The dungeon bases event has started, find the entrance to the base and get the loot"},
                { "EndMessage", "The dungeon bases event has ended" },
                { "LocationMessage", "The entrance to the dungeon is located at coordinates {0}" },
                { "WarningMessage", "Attention! Leave the dungeon immediately! The exit will be closed in 5 minutes. In 2 minutes the radiation background will rise to a level dangerous to life!" },
                { "ClosedMessage", "Time is up, the entrance and exit to the dungeon are closed forever!" }

            }, this);

            lang.RegisterMessages(new Dictionary<string, string>
            {
                { "EntryDeniedMessage", "Вы не можете войти в подземелье, не являясь владельцем события или его напарником" },
                { "StartMessage", "Событие 'Данжи' началось. Найдите вход на базу и получите добычу" },
                { "EndMessage", "Событие 'Данжи' завершилось" },
                { "LocationMessage", "Вход в подземелье находится в координатах {0}" },
                { "WarningMessage", "Внимание! Немедленно покиньте подземелье! Выход будет закрыт через 5 минут. Через 2 минуты радиационный фон поднимется до опасного для жизни уровня!" },
                { "ClosedMessage", "Время истекло, вход и выход в подземелье закрыты навсегда!" }

            }, this, "ru");
        }

        string GetMessage(string key, IPlayer player, params string[] args) => String.Format(lang.GetMessage(key, this, player.Id), args);

        [AutoPatch]
        [HarmonyPatch(typeof(ScientistNPC))]
        [HarmonyPatch("displayName", MethodType.Getter)]
        public static class DisplayNameScientistDBE
        {
            static bool Prefix(ref string __result, ScientistNPC __instance)
            {
                if (__instance != null)
                {
                    BasePlayer bot = __instance.gameObject.GetComponent<BasePlayer>();
                    string npcname = "Scientist";

                    if (bot)
                    {
                        if (bot._lastSetName != "#Scientist1818")
                            return true;
                        npcname = bot._name;
                    }

                    __result = npcname;
                    return false;
                }
                return true;
            }
        }

        private const string PluginID = "DungeonBasesEvent";
        private Harmony harmonyInstance;

        protected override void SaveConfig() => Config.WriteObject(Configuration, true);

        protected override void LoadConfig()
        {
            base.LoadConfig();
            base.Config.Settings.ObjectCreationHandling = ObjectCreationHandling.Replace;
            Configuration = Config.ReadObject<ConfigData>();
            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            Configuration = new ConfigData();
            SaveConfig();
        }

        private void Init()
        {
            UnsubscribeAll();
        }
        private void OnServerInitialized()
        {
            if (!SimpleLootTable)
                PrintWarning("SimpleLootTable plugin not found, if you need to use custom loot tables, use this plugin");

            AddMonuments();

            foreach (var item in BaseNetworkable.serverEntities.OfType<BuildingPrivlidge>())
                blackList.Add(new Vector3(item.transform.position.x, 100, item.transform.position.z));

            foreach (var item in BaseNetworkable.serverEntities.OfType<SphereEntity>())
                if (item._name == "dungSphere")
                    item.Kill();

            CalcTime();
            duration = 999999;

            eventTimer = timer.Every(1f, () =>
                {
                    //Puts(remain.ToString() + " " + duration.ToString());
                    //Puts(ownerTimerRemain.ToString());
                    remain--;
                    ownerTimerRemain--;

                    if (ownerTimerRemain == 0)
                        RemoveOwner();

                    if (Configuration.afterTime && isEventActive)
                        remain++;

                    duration--;
                    eventCooldown--;

                    if (remain == 0)
                        EventStart();


                    if (duration < Configuration.radiationTime)
                    {
                        foreach (var player in dungeonPlayers)
                        {
                            player.metabolism.radiation_poison.value += 400f / Configuration.radiationTime;
                            player.Hurt(player.metabolism.radiation_poison.value / 100f);
                        }

                        if (duration == -Configuration.destroyTime)
                            EventEnd();


                    }

                    if (duration == Configuration.warningMessageTime)
                    {
                        foreach (BasePlayer player in dungeonPlayers)
                            SendMessage(player, GetMessage("WarningMessage", player.IPlayer), Configuration.iconID);
                    }
                });

            if (Configuration.autoStart)
                Puts("The event will be triggered in auto mode");
            else
                Puts("The event will be triggered only in manual mode");

            timer.Every(10f, () =>
                {
                    VendingUpdate(eventOwner);
                    int count = duration;
                    if (duration < 0) count = 0;
                    foreach (var counter in counterList)
                    {
                        counter.counterNumber = count;
                        counter.UpdateOutputs();
                    }
                });

            if (Configuration.saveEvent)
                timer.Every(60f, () =>
                    {
                        eventData["0", "duration"] = duration;
                        eventData.Save();
                        SaveNpcData();
                    });

            LoadSuperCardConfig();
            inception = false;

            if (Configuration.saveEvent)
            {
                LoadData(false);
                LoadTcData();
                LoadTurretsData();
                LoadNpcData(false);
                LoadPlayersData();
                InitEntities();
                InitHatch();
            }
        }

        private void LoadTcData()
        {
            if (entitiesList.Count > 0)
                foreach (var tc in entitiesList)
                    if (tc.name.Contains("/tool cupboard/"))
                        tcList.Add(tc as BuildingPrivlidge);
        }

        private void LoadTurretsData()
        {
            if (entitiesList.Count > 0)
                foreach (var turret in entitiesList)
                    if (turret.name == "assets/prefabs/npc/autoturret/autoturret_deployed.prefab")
                        turretsList.Add(turret as AutoTurret);
        }

        private void AddNPCtoTC(BasePlayer playerNPC)
        {
            if (tcList.Count == 0) return;

            ulong playerNameID = playerNPC.userID;

            foreach (var tc in tcList)
            {
                if (!tc.authorizedPlayers.Contains(playerNameID))
                    tc.authorizedPlayers.Add(playerNameID);
            }
        }

        private void AddNPCtoTurret(BasePlayer playerNPC)
        {
            if (turretsList.Count == 0) return;

            ulong playerNameID = playerNPC.userID;

            foreach (var turret in turretsList)
            {
                if (!turret.authorizedPlayers.Contains(playerNameID))
                    turret.authorizedPlayers.Add(playerNameID);
            }
        }

        private void RemoveOwner()
        {
            if (eventOwner && dungeonPlayers.Contains(eventOwner) && !eventOwner.IsConnected)
            {
                dungeonPlayers.Remove(eventOwner);
                eventOwner.Hurt(999);
            }

            if (dungeonPlayers.Count > 0)
            {
                ChangeOwner(dungeonPlayers[0]);
                if (!eventOwner.IsConnected)
                    ownerTimerRemain = Configuration.ownerLeaveTimer;
            }
            else
                ChangeOwner(null);
        }

        private void SendMessage(BasePlayer player, string message, ulong iconID)
        {
            player.SendConsoleCommand("chat.add", 2, iconID, message);
        }

        private void InitEntities()
        {
            foreach (var item in entitiesList)
            {
                InitDoorList(item);

                switch (item.PrefabName)
                {
                    case "assets/bundled/prefabs/static/door.hinged.bunker_hatch.prefab":
                        hatchPair.Add(item as Door);
                        break;
                    case "assets/prefabs/io/electric/switches/cardreader.prefab":
                        InitCardReaders(item as CardReader);
                        break;
                    case "assets/prefabs/deployable/playerioents/counter/counter.prefab":
                        InitCounter(item as PowerCounter);
                        break;
                }
            }
        }

        private void InitCounter(PowerCounter counter)
        {
            if (counter.targetCounterNumber == 707)
            {
                IOEntity ioEntity = counter as IOEntity;
                ioEntity.UpdateFromInput(99, 0);
                counterList.Add(counter);
            }
        }

        private void InitHatch()
        {
            if (hatchPair.Count < 2) return;

            if (hatchPair[0].transform.position.y > hatchPair[1].transform.position.y)
            {
                Door temp;
                temp = hatchPair[0];
                hatchPair[0] = hatchPair[1];
                hatchPair[1] = temp;
            }

            HatchComponent hatch1 = hatchPair[0].gameObject.AddComponent<HatchComponent>();
            HatchComponent hatch2 = hatchPair[1].gameObject.AddComponent<HatchComponent>();
            hatch1.position = hatchPair[1].transform.position + Vector3.down * 2 - hatchPair[1].transform.forward * 1.5f;
            hatch2.position = hatchPair[0].transform.position - hatchPair[0].transform.forward + Vector3.up * 0.5f;
            hatch1.time = Configuration.dungTime;
            hatch2.time = -1;
            hatch1.entrance = true;
            hatch2.entrance = false;
        }

        private void InitDoorList(BaseEntity item)
        {
            if (item == null) return;
            if (doorNameList == null || doorNameList.Length == 0) return;

            CodeLock codeLock;
            KeyLock keyLock;

            foreach (var itemName in doorNameList)
                if (item.PrefabName == itemName)
                {
                    codeLock = item.gameObject.GetComponentInChildren<CodeLock>();
                    keyLock = item.gameObject.GetComponentInChildren<KeyLock>();

                    if (!codeLock && !keyLock)
                    {
                        doorsList.Add(item as Door);
                        AddTrigger.AddToEntity(item);
                    }
                }
        }

        BaseCorpse OnCorpsePopulate(BasePlayer npcPlayer, BaseCorpse corpse)
        {
            SlabNPC slabNPC = npcPlayer.GetComponent<SlabNPC>();
            if (slabNPC == null) return null;
            LootableCorpse container = corpse.GetComponentInParent<LootableCorpse>();
            BaseEntity entity = corpse.GetComponent<BaseEntity>();

            if (slabNPC.tableName != "")
                timer.Once(2f, () =>
                {
                    SimpleLootTable?.Call("GetSetItems", entity, slabNPC.tableName, slabNPC.tableMinItems, slabNPC.tableMaxItems, 1f);
                });

            if (slabNPC.code != "")
            {
                BaseEntity storage = GameManager.server.CreateEntity("assets/prefabs/deployable/small stash/small_stash_deployed.prefab", corpse.transform.position);
                storage.Spawn();

                Item additem = ItemManager.CreateByName("note", 1);
                additem.text = slabNPC.code;
                StorageContainer inv = storage.GetComponent<StorageContainer>();
                BaseCombatEntity ent = storage.GetComponent<BaseCombatEntity>();
                additem.MoveToContainer(inv.inventory);
                ent.DieInstantly();
            }

            return null;
        }

        private void ChangeWeapon(BasePlayer npcPlayer, string weaponName)
        {
            if (weaponName == "") return;

            Item weapon = ItemManager.CreateByName(weaponName, 1);

            if (weapon == null)
            {
                PrintWarning("Weapon with shortname '" + weaponName + "' not found");
                return;
            }

            npcPlayer.inventory.containerBelt.Clear();
            weapon.MoveToContainer(npcPlayer.inventory.containerBelt, 0);
            npcPlayer.UpdateActiveItem(weapon.uid);
        }

        private void InitNPC(BaseEntity npc, string displayName, string code, bool isStatic, string weaponName, string tableName, int tableMinItems, int tableMaxItems, string kit, float dmgScale)
        {
            npcList.Add(npc);
            npc.name = "DungBaseNPC";

            SlabNPC slabNPC = npc.gameObject.AddComponent<SlabNPC>();
            ScientistBrain brain = npc.gameObject.GetComponent<ScientistBrain>();
            BaseNavigator navigator = npc.GetComponent<BaseNavigator>();
            navigator.CanUseNavMesh = false;

            slabNPC.code = code;
            slabNPC.weapon = weaponName;
            slabNPC.isStatic = isStatic;
            slabNPC.moveTarget = npc.transform.position;
            slabNPC.contact = false;
            slabNPC.tableName = tableName;
            slabNPC.tableMinItems = tableMinItems;
            slabNPC.tableMaxItems = tableMaxItems;
            slabNPC.kit = kit;
            slabNPC.dmgScale = dmgScale;

            RaycastHit check;
            BasePlayer npcPlayer = npc as BasePlayer;
            ScientistNPC scientistNPC = npc.gameObject.GetComponent<ScientistNPC>();

            scientistNPC.damageScale = slabNPC.dmgScale;

            if (kit != "")
            {
                if (Kits != null)
                {
                    npcPlayer.inventory.Strip();
                    Kits?.Call("GiveKit", npcPlayer, slabNPC.kit);
                    Item item = npcPlayer.inventory.containerBelt.GetSlot(0);
                    if (item != null)
                    {
                        npcPlayer.UpdateActiveItem(item.uid);
                    }
                }
                else
                {
                    PrintWarning("Kits plugin not found, kits for your NPCs will not be equipped");
                }
            }


            npcPlayer._name = displayName;
            npcPlayer._lastSetName = "#Scientist1818";

            ChangeWeapon(npcPlayer, weaponName);

            slabNPC.range = 0.5f;
            slabNPC.mainTarget = npc;
            Vector3 vec;
            BaseEntity entity;
            float distance;
            Vector3 offset = Vector3.zero;

            slabNPC.timerAI = timer.Every(1f, () =>
            {
                if (!npc)
                {
                    if (slabNPC.timerAI != null)
                        slabNPC.timerAI.Destroy();
                    return;
                }

                npc.transform.rotation = new Quaternion(0, 0, 0, 1);

                offset = new Vector3(UnityEngine.Random.Range(-0.2f, 0.2f), 0, UnityEngine.Random.Range(-0.2f, 0.2f));

                if (brain.Senses.Players.Count > 0)
                {
                    if (brain.Senses.Players[0] == null) return;

                    if (slabNPC.mainTarget != brain.Senses.Players[0]) slabNPC.contact = false;
                    distance = Vector3.Distance(npc.transform.position, brain.Senses.Players[0].transform.position);

                    vec = new Vector3(brain.Senses.Players[0].transform.position.x - npc.transform.position.x, brain.Senses.Players[0].transform.position.y - npc.transform.position.y, brain.Senses.Players[0].transform.position.z - npc.transform.position.z) + Vector3.up * 0.5f;

                    if (Physics.Raycast(npc.transform.position + Vector3.up * 0.2f, vec, out check, distance, layerS))
                    {
                        entity = check.GetEntity();

                        if (entity)
                            if (entity == brain.Senses.Players[0])
                            {
                                slabNPC.contact = true;
                                slabNPC.targetIsVisible = true;
                                slabNPC.range = 5f;
                                slabNPC.wayPoints.Clear();
                                slabNPC.mainTarget = brain.Senses.Players[0];
                            }
                            else
                            {
                                slabNPC.targetIsVisible = false;
                                if (distance < 1)
                                    slabNPC.range = 1f;
                                else
                                    slabNPC.range = 0.5f;
                            }
                    }
                }
                else
                {
                    slabNPC.mainTarget = npc;
                    slabNPC.contact = false;
                }
            });

            slabNPC.timer = timer.Every(0.2f, () =>
            {
                if (!npc) return;

                if (brain.Senses.Players.Count > 0)
                {
                    if (brain.Senses.Players[0] == null) return;
                    if (slabNPC.isStatic) return;

                    if (slabNPC.contact)
                    {
                        if (Math.Abs(brain.Senses.Players[0].transform.position.y - npc.transform.position.y) > 2.5f)
                        {
                            slabNPC.contact = false;
                            return;
                        }
                        else
                        {
                            vec = brain.Senses.Players[0].transform.position;
                            slabNPC.wayPoints.Add(vec + offset);
                        }

                        if (slabNPC.moveTarget == npc.transform.position)
                            slabNPC.moveTarget = slabNPC.wayPoints[0];

                        if (slabNPC.wayPoints.Count > 99)
                            slabNPC.wayPoints.RemoveAt(0);

                        if (slabNPC.moveTarget != npc.transform.position)
                        {
                            if (Vector3.Distance(npc.transform.position, slabNPC.moveTarget) < slabNPC.range)
                            {
                                if (slabNPC.range < 0.6f)
                                    slabNPC.wayPoints.RemoveAt(0);

                                if (slabNPC.wayPoints.Count > 0)
                                    slabNPC.moveTarget = slabNPC.wayPoints[0];
                                else
                                    slabNPC.moveTarget = npc.transform.position;
                            }

                            if (Vector3.Distance(npc.transform.position, slabNPC.moveTarget) > 0.2f)
                            {
                                Vector3 move = Vector3.Normalize(new Vector3(slabNPC.moveTarget.x - npc.transform.position.x, 0, slabNPC.moveTarget.z - npc.transform.position.z));

                                if (Vector3.Distance(npc.transform.position, slabNPC.mainTarget.transform.position) < 5 && slabNPC.targetIsVisible)
                                    move = Vector3.zero;

                                npc.transform.position += move * 0.6f;
                            }
                        }
                    }
                }
            });
        }

        private void InitCardReaders(CardReader cardReader)
        {
            foreach (var door in entitiesList)
                if (door)
                    if (Vector3.Distance(door.transform.position, cardReader.transform.position) < 1.5f)
                    {
                        if (!door.PrefabName.Contains("door.hinged.security")) continue;

                        CardReaderAdd cardReaderAdd = cardReader.gameObject.AddComponent<CardReaderAdd>();
                        cardReaderAdd.door = door as Door;

                        switch (door.PrefabName)
                        {
                            case "assets/bundled/prefabs/static/door.hinged.security.red.prefab":
                                cardReader.accessLevel = 3;
                                break;
                            case "assets/bundled/prefabs/static/door.hinged.security.blue.prefab":
                                cardReader.accessLevel = 2;
                                break;
                            case "assets/bundled/prefabs/static/door.hinged.security.green.prefab":
                                cardReader.accessLevel = 1;
                                break;
                        }
                    }
        }

        private void LoadSuperCardConfig()
        {
            if (!SuperCard) return;
            DynamicConfigFile sc = SuperCard.Config;
            if (sc == null) return;
            object preloadData = sc["Item settings"] as object;
            Dictionary<string, object> dataItem = preloadData as Dictionary<string, object>;
            superCardSkinID = Convert.ToUInt64(dataItem["SkinID"]);
        }

        private void LoadData(bool kill)
        {
            eventData = Interface.Oxide.DataFileSystem.GetDatafile("DungeonBases/eventData");

            if (eventData == null) return;

            object preloadData1 = eventData["0"] as object;

            if (preloadData1 == null) return;

            Dictionary<string, object> dataItem = preloadData1 as Dictionary<string, object>;

            if (dataItem.Count <= 2) return;

            if (!kill)
                duration = (int)dataItem["duration"];

            basePosition = new Vector3((int)dataItem["basePositionX"], (int)dataItem["basePositionY"], (int)dataItem["basePositionZ"]);
            eventOwnerID = Convert.ToUInt64(dataItem["eventOwnerID"]);

            if (duration > 0 && duration != 999999)
            {
                isEventActive = true;
                SubscribeAll();

                if (eventOwnerID != 0)
                    foreach (var player in BasePlayer.allPlayerList)
                        if (player.userID == eventOwnerID)
                        {
                            eventOwner = player;
                        }
                CreateMarker();
            }

            List<object> objectList = dataItem["zObjects"] as List<object>;

            List<string> idList = new();
            int index = 0;
            Dictionary<string, object> item1;
            Vector3 pos = Vector3.zero;
            string id = "#";

            foreach (Dictionary<string, object> item in objectList)
                idList.Add(item["objectID"].ToString());

            var baseNetworkableList = Facepunch.Pool.Get<List<BaseEntity>>();
            try
            {
                foreach (var item in BaseNetworkable.serverEntities)
                    if (idList.Contains(item.net.ID.ToString()) && item is BaseEntity entity)
                        baseNetworkableList.Add(entity);

                entitiesList.Clear();
                foreach (var item in baseNetworkableList)
                {
                    entitiesList.Add(item);
                }
            }
            finally
            {
                Facepunch.Pool.FreeUnmanaged(ref baseNetworkableList);
            }

            foreach (var item in entitiesList)
            {
                if (item.net == null) continue;
                id = item.net.ID.ToString();

                if (idList.Contains(id))
                {
                    index = idList.FindIndex(x => x == id);
                    item1 = objectList[index] as Dictionary<string, object>;

                    if (item1["name"] != null)
                        item._name = item1["name"].ToString();

                    if (kill)
                        item?.Kill();
                }
            }

            if (kill)
            {
                eventData.Clear();
                eventData.Save();
            }
        }

        private void LoadNpcData(bool kill)
        {
            npcData = Interface.Oxide.DataFileSystem.GetDatafile("DungeonBases/npcData");

            if (npcData == null) return;

            List<object> preloadData = npcData["0"] as List<object>;

            if (preloadData == null) return;
            if (preloadData.Count == 0) return;

            Dictionary<string, object> dataItem = preloadData[0] as Dictionary<string, object>;
            List<string> idList = new();
            Vector3 pos = Vector3.zero;
            List<BaseEntity> _npcList = new();

            foreach (Dictionary<string, object> item in preloadData)
                idList.Add(item["objectID"].ToString());

            var baseNetworkableList = Facepunch.Pool.Get<List<BaseEntity>>();
            try
            {
                foreach (var item in BaseNetworkable.serverEntities)
                    if (idList.Contains(item.net.ID.ToString()) && item is BaseEntity entity)
                        baseNetworkableList.Add(entity);

                foreach (var item in baseNetworkableList)
                    _npcList.Add(item);
            }
            finally
            {
                Facepunch.Pool.FreeUnmanaged(ref baseNetworkableList);
            }

            bool flag;

            if (kill)
            {
                foreach (var item in _npcList)
                    if (item) item?.Kill();

                _npcList.Clear();
                npcList.Clear();
            }
            else
                foreach (Dictionary<string, object> item in preloadData)
                {
                    flag = false;
                    foreach (var npc in _npcList)
                        if (npc.net.ID.ToString() == item["objectID"].ToString())
                        {
                            InitNPC(npc, item["name"].ToString(), item["code"].ToString(), Convert.ToBoolean(item["isStatic"]), item["weapon"].ToString(),
                            item["tableName"].ToString(), Convert.ToInt16(item["tableMinItems"]), Convert.ToInt16(item["tableMaxItems"]), item["kit"].ToString(), Convert.ToSingle(item["dmgScale"]));
                            flag = true;
                        }
                    if (flag) continue;

                    pos = new Vector3(Convert.ToSingle(item["x"]), Convert.ToSingle(item["y"]), Convert.ToSingle(item["z"]));

                    string[] args = { "prefab", item["prefab"].ToString(),
                                      "displayname", item["name"].ToString(),
                                      "health", item["health"].ToString(),
                                      "weapon", item["weapon"].ToString(),
                                      "static", item["isStatic"].ToString(),
                                      "tableName", item["tableName"].ToString(),
                                      "tableMinItems", item["tableMinItems"].ToString(),
                                      "tableMaxItems", item["tableMaxItems"].ToString(),
                                      "kit", item["kit"].ToString(),
                                      "dmgScale", item["dmgScale"].ToString() };

                    ReplaceEntity(null, pos, args, item["code"].ToString());

                }

            SaveNpcData();
        }

        private void LoadPlayersData()
        {
            playersData = Interface.Oxide.DataFileSystem.GetDatafile("DungeonBases/playersData");
            if (playersData == null) return;

            List<string> playersList = new();
            List<object> preloadData = playersData["0"] as List<object>;

            if (preloadData == null || preloadData.Count == 0) return;

            dungeonPlayers.Clear();

            foreach (var item in preloadData)
                playersList.Add(item.ToString());

            foreach (var player in BasePlayer.allPlayerList)
                if (playersList.Contains(player.userID.ToString()))
                    dungeonPlayers.Add(player);
        }

        private void AddMonuments()
        {
            foreach (var monument in TerrainMeta.Path.Monuments)
            {
                if (monument.name.Contains("monument/small"))
                    blackList.Add(new Vector3(monument.transform.position.x, 100, monument.transform.position.z));

                if (monument.name.Contains("monument/medium"))
                    blackList.Add(new Vector3(monument.transform.position.x, 200, monument.transform.position.z));

                if (monument.name.Contains("monument/large"))
                    blackList.Add(new Vector3(monument.transform.position.x, 300, monument.transform.position.z));

                if (monument.name.Contains("monument/xlarge"))
                    blackList.Add(new Vector3(monument.transform.position.x, 400, monument.transform.position.z));

                if (monument.name.Contains("monument/roadside"))
                    blackList.Add(new Vector3(monument.transform.position.x, 100, monument.transform.position.z));

            }
        }

        private void OnEntitySpawned(BuildingPrivlidge entity)
        {
            blackList.Add(new Vector3(entity.transform.position.x, 100, entity.transform.position.z));
        }

        private void OnEntityKill(BuildingPrivlidge entity)
        {
            blackList.Remove(new Vector3(entity.transform.position.x, 100, entity.transform.position.z));
        }

        private void CalcTime()
        {
            remain = UnityEngine.Random.Range(Configuration.minimumRemainToEvent, Configuration.maximumRemainToEvent);
            string message = "Next event will start in " + remain.ToString() + " seconds";

            if (Configuration.afterTime && isEventActive)
                message = "The next event will start " + remain.ToString() + " seconds after the current event ends";

            if (Configuration.autoStart)
                Puts(message);
            else
                remain = -1;
        }

        private void EventStart()
        {
            if (eventCooldown > 0)
            {
                PrintWarning("You can't trigger the event that often");
                return;
            }

            eventCooldown = 10;

            if (!CopyPaste)
            {
                PrintWarning("CopyPaste plugin not found");
                PrintWarning("CopyPaste plugin is required for the plugin to work correctly");
                remain = 600;
                return;
            }

            inception = true;

            timer.Once(10f, () => inception = false);

            EventEnd();
            if (BasePlayer.activePlayerList.Count > Configuration.minOnline - 1)
            {
                Vector3 point = FindEventPoint();
                if (point == Vector3.zero)
                {
                    PrintWarning("At the moment, there is no suitable place on the map for the event, the event will not be launched");
                    if (Configuration.autoStart)
                        CalcTime();
                    return;
                }

                EventStartPos(point);
            }
            else
                NotEnough();
        }

        private void EventEnd()
        {
            AddEventSphere();
            duration = 999999;

            if (Configuration.saveEvent)
            {
                LoadData(true);
                LoadNpcData(true);
            }
            else
                RemoveDungeon();

            List<BasePlayer> killList = new();
            foreach (var player in dungeonPlayers)
            {
                ChangeTime(player, -1);
                killList.Add(player);
            }

            dungeonPlayers.Clear();

            if (Configuration.saveEvent)
                SavePlayersData();

            foreach (var player in killList)
                player.Hurt(999);

            if (isEventActive)
            {
                foreach (BasePlayer player in BasePlayer.activePlayerList)
                    SendMessage(player, GetMessage("EndMessage", player.IPlayer), Configuration.iconID);

                Puts("Event ended");
                Interface.CallHook("DungeonBasesEventEnded");
            }


            if (vending) vending.Kill();
            UnsubscribeAll();

            if (Configuration.saveEvent && isEventActive)
                timer.Once(3, () => server.Command("save"));

            isEventActive = false;
        }

        private void RemoveDungeon()
        {
            foreach (var item in entitiesList)
                if (item)
                    item.Kill();

            foreach (var item in npcList)
                if (item)
                    item.Kill();
        }

        private void NotEnough()
        {
            Puts("Not enough online players on the server, event will not start!");
            if (Configuration.autoStart)
                CalcTime();
        }

        private void OnCardSwipe(CardReader cardReader, Keycard Keycard, BasePlayer BasePlayer)
        {
            CardReaderAdd cardReaderAdd = cardReader.GetComponent<CardReaderAdd>();
            if (!cardReaderAdd) return;

            if (Keycard.accessLevel == cardReader.accessLevel || Keycard.skinID == superCardSkinID)
                if (cardReaderAdd.door)
                    cardReaderAdd.door.SetOpen(true);

        }

        private void OnPasteFinished(List<BaseEntity> pastedEntities, string filename, IPlayer player, Vector3 startPos)
        {
            if (!inception) return;
            if (!filename.Contains("#dung#")) return;
            float cooldown = 5f;
            CodeLock codeLock;
            string code = "";
            Item slotItem1;
            Item slotItem2;

            foreach (var item in pastedEntities)
            {
                if (item.name.Contains("/tool cupboard/"))
                    tcList.Add(item as BuildingPrivlidge);

                item._name = "#dung#undestr#";
                item.OwnerID = 0;

                if (item.PrefabName.Contains("modularcar") || item.PrefabName == "modular_car_fuel_storage")
                {
                    item._name = "";
                    continue;
                }

                entitiesList.Add(item);

                InitDoorList(item);

                switch (item.PrefabName)
                {
                    case "assets/prefabs/npc/flame turret/flameturret.deployed.prefab":
                        item._name = "";
                        break;
                    case "assets/prefabs/deployable/planters/planter.large.deployed.prefab":
                        StorageContainer storage = item.gameObject.GetComponent<StorageContainer>();
                        slotItem1 = storage.inventory.GetSlot(0);
                        slotItem2 = storage.inventory.GetSlot(5);
                        if (slotItem1 != null && slotItem2 != null)
                            if (slotItem1.info.shortname == "fertilizer" && slotItem1.amount == 1 && slotItem2.info.shortname == "fertilizer" && slotItem2.amount == 999)
                            {
                                ReplaceHatch(item, item.transform.forward);
                                item.Kill();
                                entitiesList.Remove(item);
                            }
                        break;
                    case "assets/prefabs/building/floor.ladder.hatch/floor.ladder.hatch.prefab":
                        codeLock = item.gameObject.GetComponentInChildren<CodeLock>();
                        if (!codeLock) continue;
                        if (codeLock.code == "0707")
                        {
                            codeLock.code = "18549";
                            ReplaceHatch(item, item.transform.right + Vector3.up * 0.1f);
                        }
                        break;
                    case "assets/prefabs/locks/keypad/lock.code.prefab":
                        codeLock = item.gameObject.GetComponent<CodeLock>();
                        if (codeLock.code == "1818")
                            codeLock.code = "0";
                        break;
                    case "assets/prefabs/building/door.hinged/door.hinged.wood.prefab":
                        ReplaceDoor(item, "assets/bundled/prefabs/static/door.hinged.security.green.prefab");
                        break;
                    case "assets/prefabs/building/door.hinged/door.hinged.metal.prefab":
                        ReplaceDoor(item, "assets/bundled/prefabs/static/door.hinged.security.blue.prefab");
                        break;
                    case "assets/prefabs/building/door.hinged/door.hinged.toptier.prefab":
                        ReplaceDoor(item, "assets/bundled/prefabs/static/door.hinged.security.red.prefab");
                        break;
                    case "assets/prefabs/deployable/playerioents/gates/branch/electrical.branch.deployed.prefab":
                        ElectricalBranch branch = item as ElectricalBranch;
                        if (branch.branchAmount == 1234560)
                            timer.Once(cooldown + 2f, () => ReplaceCardReader(item));
                        if (branch.branchAmount == 1234561)
                            timer.Once(cooldown + 2f, () => ReplaceFuseBox(item));
                        break;
                    case "assets/prefabs/deployable/woodenbox/woodbox_deployed.prefab":
                        storage = item.gameObject.GetComponent<StorageContainer>();
                        var boxItem = storage.inventory.GetSlot(0);
                        if (boxItem == null) break;
                        if (boxItem.text == null) break;
                        char[] separators = new char[] { '=', '\n' };
                        string[] args = boxItem.text.Split(separators);

                        timer.Once(cooldown + 2f, () =>
                        {
                            code = "";
                            if (storage.inventory.GetSlot(17) != null)
                                code = storage.inventory.GetSlot(17).text;

                            ReplaceEntity(item, Vector3.zero, args, code);
                        });
                        break;
                    case "assets/prefabs/npc/autoturret/autoturret_deployed.prefab":
                        item._name = "#dung#turret#";
                        SetupTurret(item);
                        turretsList.Add(item as AutoTurret);
                        break;
                    case "assets/prefabs/deployable/single shot trap/guntrap.deployed.prefab":
                        item._name = "#dung#turret#";
                        StorageContainer container = item.GetComponent<StorageContainer>();
                        if (container.inventory.GetSlot(5) != null && container.inventory.GetSlot(5).amount == 1)
                            item._name = "#dung#undestr#";
                        break;
                    case "assets/prefabs/deployable/playerioents/doormanipulators/doorcontroller.deployed.prefab":
                        Door door = item.gameObject.GetComponentInParent<Door>();
                        IOEntity iOEntity = item.GetComponent<IOEntity>().outputs[0].connectedTo.ioEnt;
                        if (!iOEntity) break;
                        CodeLock codeLockLink = door.gameObject.GetComponentInChildren<CodeLock>();
                        if (!codeLockLink) break;
                        if (iOEntity.PrefabName == "assets/prefabs/deployable/playerioents/gates/rfbroadcaster/rfbroadcaster.prefab")
                            timer.Once(cooldown + 1f, () => codeLockLink.code = LinkCode(item, iOEntity));
                        break;
                    case "assets/prefabs/deployable/playerioents/counter/counter.prefab":
                        InitCounter(item as PowerCounter);
                        break;
                }
            }
        }

        private void SetupTurret(BaseEntity turret)
        {
            IOEntity iOEntity = turret.GetComponent<IOEntity>().inputs[0].connectedTo.ioEnt;
            if (!iOEntity) return;

            if (iOEntity.PrefabName == "assets/prefabs/deployable/playerioents/gates/branch/electrical.branch.deployed.prefab")
            {
                ElectricalBranch branch = iOEntity as ElectricalBranch;
                string str = branch.branchAmount.ToString() + "000000";
                if (str[0].ToString() != "9") return;
                if (str[1].ToString() != "9") return;
                if (str[2].ToString() == "1") turret._name += "undestr#";
                if (str[3].ToString() == "1") turret._name += "autofill#";
                if (str[4].ToString() == "1") turret._name += "nodrop#";
            }

            if (iOEntity.PrefabName == "assets/prefabs/deployable/playerioents/gates/blocker/electrical.blocker.deployed.prefab")
                turret._name = "#dung#turret#undestr#";

            if (turret._name.Contains("#autofill#"))
            {
                var autoTurret = turret as AutoTurret;
                var weapon = autoTurret.GetAttachedWeapon();
                if (!weapon) return;
                Item newItem = ItemManager.CreateByName(weapon.primaryMagazine.ammoType.shortname);
                newItem.amount = 9999999;

                if (autoTurret.inventory.GetSlot(1) != null)
                    autoTurret.inventory.GetSlot(1).Remove();

                timer.Once(2f, () =>
                {
                    if (!autoTurret) return;
                    newItem.MoveToContainer(autoTurret.inventory);
                    autoTurret.UpdateTotalAmmo();
                });
            }
        }

        private void SaveDungeonData()
        {
            List<ObjectSettings> objectsList = new();
            BasePlayer basePlayer;
            foreach (var item in entitiesList)
            {
                basePlayer = item as BasePlayer;

                if (basePlayer) continue;

                if (item)
                    objectsList.Add(new ObjectSettings { objectID = item.net.ID.ToString(), name = item._name, prefab = item.PrefabName });
                else
                    objectsList.Add(new ObjectSettings { objectID = "#", name = item._name, prefab = item.PrefabName });

            }

            eventData.Clear();
            eventData["0", "eventOwnerID"] = eventOwnerID;
            eventData["0", "basePositionX"] = (int)basePosition.x;
            eventData["0", "basePositionY"] = (int)basePosition.y;
            eventData["0", "basePositionZ"] = (int)basePosition.z;
            eventData["0", "duration"] = duration;
            eventData["0", "zObjects"] = objectsList;
            eventData.Save();
        }

        private void SaveNpcData()
        {
            List<NPCSettings> _objectsList = new();
            ScientistBrain brain;
            BaseCombatEntity entity;
            SlabNPC slabNPC;

            foreach (var item in npcList)
            {
                if (item == null) continue;
                brain = item.gameObject.GetComponent<ScientistBrain>();
                entity = item as BaseCombatEntity;
                slabNPC = item.GetComponent<SlabNPC>();

                if (brain)
                    _objectsList.Add(new NPCSettings
                    {
                        weapon = slabNPC.weapon,
                        isStatic = slabNPC.isStatic,
                        code = slabNPC.code,
                        objectID = entity.net.ID.ToString(),
                        health = entity.health,
                        name = item._name,
                        x = item.transform.position.x,
                        y = item.transform.position.y,
                        z = item.transform.position.z,
                        tableName = slabNPC.tableName,
                        tableMinItems = slabNPC.tableMinItems,
                        tableMaxItems = slabNPC.tableMaxItems,
                        prefab = item.PrefabName,
                        kit = slabNPC.kit,
                        dmgScale = slabNPC.dmgScale
                    });
            }

            npcData.Clear();
            npcData["0"] = _objectsList;
            npcData.Save();
        }

        private void SavePlayersData()
        {
            List<ulong> _playersList = new();

            foreach (var player in dungeonPlayers)
                _playersList.Add(player.userID);

            playersData.Clear();
            playersData["0"] = _playersList;
            playersData.Save();
        }

        private string LinkCode(BaseEntity item, IOEntity iOEntity)
        {
            StorageContainer container;
            string secretCode = UnityEngine.Random.Range(1000, 10000).ToString();
            foreach (var entity in entitiesList)
                if (entity && iOEntity)
                    if (Vector3.Distance(iOEntity.transform.position, entity.transform.position) < 1.5f)
                    {
                        container = entity.GetComponent<StorageContainer>();
                        if (container == null) continue;
                        Item newItem = ItemManager.CreateByName("note", 1);
                        newItem.text = secretCode;
                        newItem.MoveToContainer(container.inventory, container.inventory.capacity - 1);
                        break;
                    }
            entitiesList.Remove(item);
            entitiesList.Remove(iOEntity);
            item.Kill();
            iOEntity.Kill();

            return secretCode;

        }

        private void ReplaceEntity(BaseEntity item, Vector3 position, string[] args, string code)
        {
            string prefab = "";
            string displayName = "";
            float height = 0;
            float forward = 0;
            float right = 0;
            float health = 0;
            string weaponName = "";
            string tableName = "";
            int tableMinItems = 0;
            int tableMaxItems = 0;
            bool isStatic = false;
            string kit = "";
            float dmgScale = 1f;
            Vector3 pos;
            Quaternion rot;

            for (int i = 0; i < args.Length; i = i + 2)
            {
                switch (args[i].ToLower())
                {
                    case "prefab":
                        prefab = args[i + 1];
                        break;
                    case "displayname":
                        displayName = args[i + 1];
                        break;
                    case "height":
                        if (float.TryParse(args[i + 1], out float parsedHeight))
                            height = parsedHeight;
                        break;
                    case "forward":
                        forward = Convert.ToSingle(args[i + 1]);
                        break;
                    case "right":
                        right = Convert.ToSingle(args[i + 1]);
                        break;
                    case "health":
                        health = Convert.ToSingle(args[i + 1]);
                        break;
                    case "weapon":
                        weaponName = args[i + 1];
                        break;
                    case "static":
                        isStatic = Convert.ToBoolean(args[i + 1]);
                        break;
                    case "tablename":
                        tableName = args[i + 1];
                        break;
                    case "tableminitems":
                        tableMinItems = Convert.ToInt16(args[i + 1]);
                        break;
                    case "tablemaxitems":
                        tableMaxItems = Convert.ToInt16(args[i + 1]);
                        break;
                    case "kit":
                        kit = args[i + 1];
                        break;
                    case "dmgscale":
                        dmgScale = Convert.ToSingle(args[i + 1]);
                        break;
                }
            }

            if (item == null)
            {
                pos = position;
                rot = new Quaternion(0, 0, 0, 0);
            }
            else
            {
                pos = item.transform.position;
                rot = item.transform.rotation;
                pos += Vector3.up * height + item.transform.forward * forward + item.transform.right * right;
                StorageContainer container = item.GetComponent<StorageContainer>();

                if (prefab == "")
                {
                    if (container)
                        container.inventory.GetSlot(0).Remove();

                    if (tableName != "")
                        SimpleLootTable?.Call("GetSetItems", item, tableName, tableMinItems, tableMaxItems, 1f, false);
                    return;
                }
                entitiesList.Remove(item);
                item.Kill();
            }

            var entity = GameManager.server.CreateEntity(prefab, pos, rot);
            if (!entity)
            {
                PrintWarning("Entity prefab no exist " + prefab);
                return;
            }
            entity.Spawn();
            entity.OwnerID = 0;

            var player = entity as BasePlayer;

            if (!player && tableName != "")
                SimpleLootTable?.Call("GetSetItems", entity, tableName, tableMinItems, tableMaxItems, 1f);

            var bradley = entity.gameObject.GetComponent<BradleyAPC>();

            if (bradley)
                bradley.ClearPath();

            if (player)
            {
                if (health > 0)
                {
                    player.startHealth = health;
                    player.health = health;
                }

                if (displayName == "" && Configuration.npcNamesList.Count > 0)
                {
                    displayName = Configuration.npcNamesList[UnityEngine.Random.Range(0, Configuration.npcNamesList.Count)];
                }

                if (displayName == "") displayName = player.displayName;
                InitNPC(entity, displayName, code, isStatic, weaponName, tableName, tableMinItems, tableMaxItems, kit, dmgScale);

                if (!Configuration.guntrapNpcAttack)
                    AddNPCtoTC(player);

                if (!Configuration.turretNpcAttack)
                    AddNPCtoTurret(player);

                return;
            }

            entitiesList.Add(entity);
        }

        private void AddEventSphere()
        {
            SphereEntity sphereEntity = GameManager.server.CreateEntity(StringPool.Get(3211242734), basePosition) as SphereEntity;
            sphereEntity.EnableSaving(false);
            sphereEntity.EnableGlobalBroadcast(false);
            sphereEntity._name = "dungSphere";
            sphereEntity.Spawn();

            sphereEntity.currentRadius = 200;
            sphereEntity.lerpRadius = 200;
            sphereEntity.UpdateScale();
            sphereEntity.SendNetworkUpdateImmediate();

            AddTriggerKill.AddToEntity(sphereEntity);

            timer.Once(3f, () => { if (sphereEntity) sphereEntity.Kill(); });
        }

        private class AddTrigger : MonoBehaviour
        {
            public static void AddToEntity(BaseEntity entity)
            {
                BoxCollider collider = entity.gameObject.AddComponent<BoxCollider>();
                collider.isTrigger = true;
                entity.gameObject.layer = (int)Rust.Layer.Reserved1;
                entity.gameObject.AddComponent<CollisionListener>();
            }
        }

        private class AddTriggerKill : MonoBehaviour
        {
            public static void AddToEntity(BaseEntity entity)
            {
                BoxCollider collider = entity.gameObject.AddComponent<BoxCollider>();
                collider.isTrigger = true;
                entity.gameObject.layer = (int)Rust.Layer.Reserved1;
                entity.gameObject.AddComponent<CollisionListenerKill>();
            }
        }

        public class CollisionListenerKill : MonoBehaviour
        {
            private void OnTriggerEnter(Collider collider)
            {
                BasePlayer player = collider?.ToBaseEntity()?.ToPlayer();
                if (player) return;

                BaseEntity entity = collider?.ToBaseEntity();
                if (entity) entity.Kill();
            }
        }

        public class CollisionListener : MonoBehaviour
        {
            private void OnTriggerEnter(Collider collider)
            {
                BasePlayer player = collider?.ToBaseEntity()?.ToPlayer();

                if (player == null) return;

                SlabNPC slabNPC = player.gameObject.GetComponent<SlabNPC>();

                if (slabNPC == null) return;

                foreach (var door in doorsList)
                    if (door != null)
                        if (Vector3.Distance(door.transform.position, slabNPC.transform.position) < 2)
                            door.SetOpen(true);

            }
        }
        private void ReplaceFuseBox(BaseEntity item)
        {
            string prefab = "assets/prefabs/io/electric/switches/fusebox/fusebox.prefab";
            var fusebox = GameManager.server.CreateEntity(prefab, item.transform.position + Vector3.down * 1.38f, item.transform.rotation);
            fusebox.Spawn();
            fusebox.OwnerID = 0;
            entitiesList.Add(fusebox);

            IOEntity iOEntity1 = item.GetComponent<IOEntity>().inputs[0].connectedTo.ioEnt;
            IOEntity iOEntity2 = fusebox.GetComponent<IOEntity>();
            IOEntity iOEntity3 = item.GetComponent<IOEntity>().outputs[0].connectedTo.ioEnt;
            ConnectIO(iOEntity1, iOEntity2);
            ConnectIO(iOEntity2, iOEntity3);
            item.Kill();
            entitiesList.Remove(item);
        }

        private void ReplaceCardReader(BaseEntity item)
        {
            if (item == null)
            {
                PrintWarning("ReplaceCardReader: Item is null! Skipping replacement.");
                return;
            }

            string prefab = "assets/prefabs/io/electric/switches/cardreader.prefab";
            var cardReader = GameManager.server.CreateEntity(prefab, item.transform.position + Vector3.down * 1.38f, item.transform.rotation) as CardReader;

            if (cardReader == null)
            {
                PrintWarning("ReplaceCardReader: Failed to create CardReader from prefab!");
                return;
            }

            InitCardReaders(cardReader);
            cardReader.Spawn();
            cardReader.OwnerID = 0;
            entitiesList.Add(cardReader);

            IOEntity itemIO = item.GetComponent<IOEntity>();
            if (itemIO == null)
            {
                PrintWarning($"ReplaceCardReader: {item} does not have IOEntity component! Skipping connection.");
                RemoveItem(item);
                return;
            }

            var input0 = itemIO.inputs[0];
            if (input0 == null || input0.connectedTo == null || input0.connectedTo.ioEnt == null)
            {
                PrintWarning($"ReplaceCardReader: {item}'s input[0] has no connected IOEntity! Skipping connection.");
                RemoveItem(item);
                return;
            }

            IOEntity iOEntity1 = input0.connectedTo.ioEnt;
            IOEntity iOEntity2 = cardReader.GetComponent<IOEntity>();
            ConnectIO(iOEntity1, iOEntity2);
            RemoveItem(item);
        }

        private void RemoveItem(BaseEntity item)
        {
            item.Kill();
            entitiesList.Remove(item);
        }

        private void ReplaceHatch(BaseEntity item, Vector3 offset)
        {
            BaseEntity entity = SpawnReplace("assets/bundled/prefabs/static/door.hinged.bunker_hatch.prefab", item, offset);
            entity.Spawn();
            if (offset == item.transform.right + Vector3.up * 0.1f)
                entity.transform.rotation *= Quaternion.Euler(0f, 90f, 0f);

            entitiesList.Add(entity);
            hatchPair.Add(entity as Door);
        }

        private void ReplaceDoor(BaseEntity item, string prefab)
        {
            CodeLock codeLock = item.gameObject.GetComponentInChildren<CodeLock>();
            if (!codeLock) return;

            if (codeLock.code == "0707")
            {
                BaseEntity entity = SpawnReplace(prefab, item, Vector3.zero);
                entity.Spawn();
                entity.OwnerID = 0;
                entitiesList.Add(entity);
                item.Kill();
                entitiesList.Remove(item);
                entitiesList.Remove(codeLock);
            }

        }
        private BaseEntity SpawnReplace(string prefab, BaseEntity entity, Vector3 offset)
        {
            var replace = GameManager.server.CreateEntity(prefab, entity.transform.position + offset, entity.transform.rotation);
            return replace;
        }

        private void ChangeTime(BasePlayer player, float timevalue)
        {
            if (player.IsAdmin)
                player.SendConsoleCommand("admintime", timevalue);
            else
            {
                player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, true);
                player.SendNetworkUpdateImmediate();
                player.SendConsoleCommand("admintime", timevalue);
                player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, false);
                player.SendNetworkUpdateImmediate();
            }
        }

        private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitinfo)
        {
            if (entity._name == null) return;

            if (entity._name == "#dung#undestr#")
                hitinfo.damageTypes.ScaleAll(0);
        }

        private void OnEntityTakeDamage(AutoTurret turret, HitInfo thitinfo)
        {
            if (turret._name == null) return;

            if (turret._name.Contains("#undestr#"))
                thitinfo.damageTypes.ScaleAll(0);
        }

        private void OnEntityDeath(AutoTurret turret, HitInfo dhitinfo)
        {
            if (turret._name == null) return;
            
            if (turret._name.Contains("#nodrop#"))
                turret.inventory.Clear();

            if (turret._name.Contains("#autofill#") && turret.inventory.GetSlot(1) != null)
                turret.inventory.GetSlot(1).Remove();
        }

        private void OnEntityTakeDamage(NPCPlayer npc, HitInfo nhitinfo)
        {
            if (!nhitinfo.ProjectilePrefab) return;
            var brain = npc.GetComponent<SlabNPC>();
            if (!brain) return;
            if (brain.contact) return;
            if (!nhitinfo.InitiatorPlayer) return;

            brain.contact = true;
            brain.mainTarget = nhitinfo.InitiatorPlayer;
            brain.wayPoints.Add(brain.mainTarget.transform.position);
        }

        private Vector3 FindEventPoint()
        {
            int mapSize = (int)TerrainMeta.Size.x;
            float x = 0;
            float y = 0;
            float z = 0;
            Vector3 startPoint;
            Vector3 findPoint = Vector3.zero;
            List<Vector3> pointsList = new();
            bool flag = true;
            float radius = 8f;
            float step = 2.2f;
            float height = 1.6f;

            for (int i = 0; i < 9999; i++)
            {
                pointsList.Clear();
                x = UnityEngine.Random.Range(-mapSize / 2, mapSize / 2);
                z = UnityEngine.Random.Range(-mapSize / 2, mapSize / 2);
                startPoint = new Vector3(x, 500, z);
                flag = false;
                findPoint = FindPoint(startPoint);

                if (findPoint != Vector3.zero)
                {
                    flag = true;
                    y = -999f;
                    pointsList.Add(findPoint);

                    for (x = startPoint.x - radius; x < startPoint.x + radius; x += step)
                        for (z = startPoint.z - radius; z < startPoint.z + radius; z += step)
                        {
                            pointsList.Add(FindPoint(new Vector3(x, 500, z)));
                        }

                    foreach (var item in pointsList)
                    {
                        if (y < item.y) y = item.y;
                        if (item == Vector3.zero) flag = false;
                        if (Math.Abs(item.y - findPoint.y) > height) flag = false;
                    }

                    if (flag)
                    {
                        //Puts(i.ToString());
                        //foreach (var item in pointsList)
                        //    BasePlayer.activePlayerList[0].SendConsoleCommand("ddraw.text", 5f, Color.red, item, "<size=>" + i.ToString() + "</size>");
                        //BasePlayer.activePlayerList[0].SendConsoleCommand("ddraw.text", 5f, Color.red, findPoint, "<size=>" + "center" + "</size>");
                        //BasePlayer.activePlayerList[0].Teleport(findPoint + Vector3.up * 10);
                        break;
                    }
                }
            }

            if (flag)
                return new Vector3(findPoint.x, y, findPoint.z);
            else
                return Vector3.zero;
        }

        private Vector3 FindPoint(Vector3 point)
        {
            foreach (var item in blackList)
                if (Vector3.Distance(point, new Vector3(item.x, point.y, item.z)) < item.y)
                    return Vector3.zero;

            if (Physics.Raycast(point, Vector3.down, out hit, 999, layerS))
                if ((hit.collider.name == "Terrain" || hit.collider.name.Contains("ice_lake_")) && WaterLevel.GetWaterDepth(hit.point, false, false) < 0.1f)
                    point = hit.point;
                else
                    point = Vector3.zero;

            return point;
        }


        [Command("dungbase_start")]
        private void dungbase_start(IPlayer iplayer)
        {
            if (iplayer.IsAdmin)
                timer.Once(3, () =>
                    {
                        remain = 0;
                        EventStart();
                    });

        }

        [Command("dungbase_stop")]
        private void dungbase_stop(IPlayer iplayer)
        {
            if (iplayer.IsAdmin)
                duration = 3 - Configuration.destroyTime;

        }

        private void ConnectIO(IOEntity ioEntity1, IOEntity ioEntity2)
        {
            ioEntity1.outputs[0].connectedTo = new IOEntity.IORef();
            ioEntity1.outputs[0].connectedTo.Set(ioEntity2);
            ioEntity1.outputs[0].connectedToSlot = 0;
            ioEntity1.outputs[0].connectedTo.Init();

            ioEntity2.inputs[0].connectedTo = new IOEntity.IORef();
            ioEntity2.inputs[0].connectedTo.Set(ioEntity1);
            ioEntity2.inputs[0].connectedToSlot = 0;
            ioEntity2.inputs[0].connectedTo.Init();

            ioEntity1.MarkDirtyForceUpdateOutputs();
            ioEntity1.SendNetworkUpdate();

            ioEntity2.MarkDirtyForceUpdateOutputs();
            ioEntity2.SendNetworkUpdate();
        }

        private void EventStartPos(Vector3 position)
        {
            EventEnd();
            entitiesList.Clear();
            npcList.Clear();
            hatchPair.Clear();
            tcList.Clear();
            turretsList.Clear();

            if (Configuration.randomEntranceList)
                entranceIndex = UnityEngine.Random.Range(0, Configuration.entrancesList.Count);

            if (Configuration.randomDungList)
                dungeonIndex = UnityEngine.Random.Range(0, Configuration.dungeonList.Count);

            basePosition = position + Vector3.up * 3000;

            string[] options = new string[] { "stability", "true", "autoheight", "false" };
            var base1 = CopyPaste?.Call("TryPasteFromVector3", position, 0.75f, Configuration.entrancesList[entranceIndex], options);
            var base2 = CopyPaste?.Call("TryPasteFromVector3", basePosition, 90f, Configuration.dungeonList[dungeonIndex], options);

            bool flag = true;

            timer.Once(10f, () =>
            {
                if (base1.ToString() == "File does not exist")
                {
                    PrintWarning("File " + Configuration.entrancesList[entranceIndex].ToString() + " does not exist");
                    flag = false;
                }
                if (base2.ToString() == "File does not exist")
                {
                    PrintWarning("File " + Configuration.dungeonList[dungeonIndex].ToString() + " does not exist");
                    flag = false;
                }

                if (base1.ToString() == "True" && base2.ToString() == "True" && hatchPair.Count != 2)
                {
                    PrintWarning("No hatch found, or both hatches not found");
                    flag = false;
                }

                if (flag)
                {
                    eventOwner = null;
                    eventOwnerID = 0;
                    Puts("Event started");
                    Interface.CallHook("DungeonBasesEventStarted");
                    duration = UnityEngine.Random.Range(Configuration.minimumEventDuration, Configuration.maximumEventDuration);

                    InitHatch();

                    if (Configuration.saveEvent)
                    {
                        SaveDungeonData();
                        SaveNpcData();
                    }

                    isEventActive = true;
                    SubscribeAll();
                    dungeonPlayers.Clear();
                    CreateMarker();
                    CalcTime();

                    foreach (var tree in BaseNetworkable.serverEntities.OfType<TreeEntity>())
                        if (Vector3.Distance(tree.transform.position, position) < 10)
                            tree.Kill();

                    IPlayer iplayer;
                    string msg;
                    foreach (BasePlayer player in BasePlayer.activePlayerList)
                    {
                        iplayer = player.IPlayer;
                        msg = GetMessage("LocationMessage", iplayer, MapHelper.PositionToString(position));

                        SendMessage(player, GetMessage("StartMessage", iplayer), Configuration.iconID);

                        if (msg != "")
                            SendMessage(player, msg, Configuration.iconID);
                    }
                }
                else
                {
                    PrintWarning("The event will not start");

                    foreach (var item in entitiesList)
                        item.Kill();

                    foreach (var item in npcList)
                        item.Kill();

                    EventEnd();
                }

                dungeonIndex++;
                if (dungeonIndex > Configuration.dungeonList.Count - 1)
                    dungeonIndex = 0;

                entranceIndex++;
                if (entranceIndex > Configuration.entrancesList.Count - 1)
                    entranceIndex = 0;

                if (Configuration.saveEvent)
                    server.Command("save");
            });
        }

        private void OnDoorOpened(Door door, BasePlayer player)
        {
            if (hatchPair.Contains(door))
            {
                door.SetOpen(false);

                if (Configuration.closeEvent && duration < 1)
                {
                    string msg = GetMessage("ClosedMessage", player.IPlayer);
                    if (msg != "")
                        SendMessage(player, msg, Configuration.iconID);
                    return;
                }

                HatchComponent hatch = door.gameObject.GetComponent<HatchComponent>();

                if (hatch)
                {
                    if (hatch.entrance)
                    {
                        if (!Configuration.onlyOwner)
                        {
                            DungTeleportPlayer(player, hatch);
                            return;
                        }
                        if (eventOwnerID == 0)
                        {
                            ChangeOwner(player);
                            DungTeleportPlayer(player, hatch);
                        }
                        else
                        {
                            string msg = GetMessage("EntryDeniedMessage", player.IPlayer);
                            if (!Configuration.teammates)
                            {
                                SendMessage(player, msg, Configuration.iconID);
                                return;
                            }
                            else
                            {
                                if (eventOwner.Team == null)
                                {
                                    SendMessage(player, msg, Configuration.iconID);
                                    return;
                                }

                                if (eventOwner.Team.members.Contains(player.userID))
                                {
                                    DungTeleportPlayer(player, hatch);
                                }
                                else
                                {
                                    SendMessage(player, msg, Configuration.iconID);
                                    return;
                                }
                            }
                        }
                    }
                    else
                        DungTeleportPlayer(player, hatch);
                }

                return;
            }
        }

        private bool CanWake(BasePlayer player)
        {
            return player.IsOnGround() || player.limitNetworking || player.IsFlying || player.IsAdmin;
        }

        private void DungTeleportPlayer(BasePlayer player, HatchComponent hatch)
        {
            player.PauseFlyHackDetection(5f);
            player.PauseSpeedHackDetection(5f);
            player.ApplyStallProtection(4f);
            player.UpdateActiveItem(default);
            player.EnsureDismounted();
            player.Server_CancelGesture();
            player.StartSleeping();
            player.ClientRPC(RpcTarget.Player(false ? "StartLoading_Quick" : "StartLoading", player), arg1: true);
            player.Teleport(hatch.position);


            if (player.IsConnected)
            {
                player.SetPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot, true);
                player.UpdateNetworkGroup();
                player.SendNetworkUpdateImmediate();
                player.ClearEntityQueue(null);
                player.SendFullSnapshot();

                if (CanWake(player)) player.Invoke(() =>
                    {
                        if (player && player.IsConnected)
                        {
                            if (player.limitNetworking) player.EndSleeping();
                            else player.EndSleeping();
                        }
                    }, 0.5f);
            }
        }

        private void ChangeOwner(BasePlayer player)
        {
            if (player)
            {
                eventOwner = player;
                eventOwnerID = player.userID;
            }
            else
            {
                eventOwner = null;
                eventOwnerID = 0;
            }

            if (Configuration.saveEvent)
            {
                eventData["0", "eventOwnerID"] = eventOwnerID;
                eventData.Save();
            }
        }

        private void ChangeDungeonPlayers(BasePlayer player, bool addPlayer, float dungTime)
        {
            if (addPlayer)
            {
                if (!dungeonPlayers.Contains(player))
                    dungeonPlayers.Add(player);
            }
            else
                dungeonPlayers.Remove(player);

            if (player.IsConnected)
                ChangeTime(player, dungTime);

            if (Configuration.saveEvent)
                SavePlayersData();

            if (dungeonPlayers.Count == 0)
                ChangeOwner(null);

            if (!addPlayer && player == eventOwner && dungeonPlayers.Count > 0)
                ChangeOwner(dungeonPlayers[0]);

        }

        private void OnPlayerDeath(BasePlayer player)
        {
            NextFrame(() => ChangeDungeonPlayers(player, false, -1));
        }

        private void OnPlayerDeath(NPCPlayer player)
        {
            if (npcList.Contains(player) && Configuration.saveEvent)
                timer.Once(1, () => SaveNpcData());
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (dungeonPlayers.Contains(player))
                ChangeDungeonPlayers(player, true, Configuration.dungTime);

        }

        private void Unload()
        {
            if (Configuration.saveEvent)
            {
                if (eventData == null)
                {
                    PrintWarning("Event data is not initialized. Cannot save changes.");
                }
                else
                {
                    try
                    {
                        eventData["0", "duration"] = duration;
                        eventData.Save();
                    }
                    finally { }
                }
            }
            else
                EventEnd();

            if (vending) vending.Kill();
        }

        private void CreateMarker()
        {
            if (!Configuration.eventMarker) return;

            string markerPrefab = "assets/prefabs/tools/map/genericradiusmarker.prefab";
            marker = GameManager.server.CreateEntity(markerPrefab, basePosition).GetComponent<MapMarkerGenericRadius>();
            markerPrefab = "assets/prefabs/deployable/vendingmachine/vending_mapmarker.prefab";
            vending = GameManager.server.CreateEntity(markerPrefab, basePosition).GetComponent<VendingMachineMapMarker>();
            vending.markerShopName = Configuration.markerName;
            vending.enableSaving = false;
            vending.Spawn();
            marker.radius = Configuration.markerRadius;
            marker.alpha = Configuration.markerAlpha;
            Color markerColor = Color.green;
            markerColor.r = Configuration.markerColorR;
            markerColor.g = Configuration.markerColorG;
            markerColor.b = Configuration.markerColorB;
            marker.color1 = markerColor;
            marker.enableSaving = false;
            marker.Spawn();
            marker.SetParent(vending);
            marker.transform.localPosition = new Vector3(0, 0, 0);
            marker.SendUpdate();
            vending.SendNetworkUpdate();
        }

        private void VendingUpdate(BasePlayer player)
        {
            if (vending && isEventActive)
            {
                vending.markerShopName = Configuration.markerName;

                if (Configuration.markerTime)
                    if (duration > 0)
                        vending.markerShopName += "(" + (int)duration / 60 + "m" + duration % 60 + "s)";
                    else
                        vending.markerShopName += "(the event has ended)";

                if (player != null && Configuration.markerOwnerName)
                    vending.markerShopName += "(" + eventOwner.displayName + ")";

                vending.SendNetworkUpdate();
            }

        }

        private object OnPlayerSleep(BasePlayer player)
        {
            int radius = 200;

            timer.Once(0.1f, () =>
                {
                    if (Vector3.Distance(basePosition, player.transform.position) > radius)
                        ChangeDungeonPlayers(player, false, -1);

                    if (Vector3.Distance(basePosition, player.transform.position) <= radius)
                        ChangeDungeonPlayers(player, true, Configuration.dungTime);
                });

            return null;
        }

        private void OnPlayerSleepEnded(BasePlayer player)
        {
            if (vending)
            {
                marker.SendUpdate();
                vending.SendNetworkUpdate();
            }
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
            if (eventOwner && player == eventOwner)
                ownerTimerRemain = Configuration.ownerLeaveTimer;
        }

        private void SubscribeAll()
        {
            Subscribe("OnPlayerConnected");
            Subscribe("OnPlayerDeath");
            Subscribe("OnDoorOpened");
            Subscribe("OnEntityTakeDamage");
            Subscribe("OnCorpsePopulate");
            Subscribe("OnPlayerSleepEnded");
            Subscribe("OnPlayerSleep");
            Subscribe("OnPlayerDisconnected");
            Subscribe("OnEntityDeath");

        }

        private void UnsubscribeAll()
        {
            Unsubscribe("OnPlayerConnected");
            Unsubscribe("OnPlayerDeath");
            Unsubscribe("OnDoorOpened");
            Unsubscribe("OnEntityTakeDamage");
            Unsubscribe("OnCorpsePopulate");
            Unsubscribe("OnPlayerSleepEnded");
            Unsubscribe("OnPlayerSleep");
            Unsubscribe("OnPlayerDisconnected");
            Unsubscribe("OnEntityDeath");
        }
    }
}
