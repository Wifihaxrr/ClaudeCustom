using System;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Plugins.JetPackExtensionMethods;
using Newtonsoft.Json;
using Rust;
using UnityEngine;
using CompanionServer.Handlers;
using Facepunch;

namespace Oxide.Plugins
{
    [Info("JetPack", "Adem", "1.2.9")]
    class JetPack : RustPlugin
    {
        #region Variables
        const bool en = true;
        private static JetPack ins;
        [PluginReference] private Plugin ZoneManager, Space;
        HashSet<string> subscribeMetods = new HashSet<string>
        {
            "OnItemAddedToContainer",
            "OnItemRemovedFromContainer",
            "CanMoveItem",
            "OnPlayerSleep",
            "OnPlayerSleepEnded",
            "OnPlayerDeath",
            "OnPlayerKicked",
            "OnLootSpawn",
            "CanLootEntity",
            "OnSamSiteTargetScan",
            "OnPlayerCommand",
            "OnEntityEnterZone",
            "OnEntitySpawned"
        };
        Dictionary<ulong, float> unequipTime = new Dictionary<ulong, float>();
        #endregion Variables

        #region Hooks
        void Init()
        {
            Unsubscribes();
        }

        void OnServerInitialized()
        {
            ins = this;
            LoadDefaultMessages();
            UpdateConfig();
            Subscribes();
            RegisterPermissions();

            if (plugins.Exists("Space") && (bool)Space.Call("IsEventActive"))
                OnSpaceEventStart();
        }

        void Unload()
        {
            JetpackComponent.RemoveAllJetpacks();
        }

        void OnItemAddedToContainer(ItemContainer container, Item item)
        {
            if (container == null || item == null)
                return;

            if (LootManager.IsJetpackItem(item))
            {
                BasePlayer player = container.playerOwner;

                if (player.IsRealPlayer() && container == player.inventory.containerWear)
                {
                    if (_config.wearWhenItemAdded)
                        JetpackComponent.TryAttachJetpackToPlayer(player);
                    else
                        NotifyManager.SendMessageToPlayer(player, "ActivateCommand", _config.prefix);
                }
            }
        }

        void OnItemRemovedFromContainer(ItemContainer container, Item item)
        {
            if (container == null || item == null)
                return;

            if (LootManager.IsJetpackItem(item))
            {
                BasePlayer player = container.playerOwner;

                if (player.IsRealPlayer() && container == player.inventory.containerWear)
                    JetpackComponent.RemoveJetpackFromPlayer(player.userID);
            }
        }

        object CanMoveItem(Item item, PlayerInventory playerLoot, ItemContainerId targetContainer, int targetSlot, int amount)
        {
            if (item == null || playerLoot == null)
                return null;

            if (!_config.isAirTakeOffAllowed)
            {
                BasePlayer player = playerLoot.baseEntity;

                if (LootManager.IsJetpackItem(item) && player.IsRealPlayer())
                {
                    JetpackComponent jetPack = JetpackComponent.GetJetpackComponentByUserId(player.userID);

                    if (jetPack != null && !jetPack.IsPlayerCanTakeOffJetpack())
                        return true;
                }
            }
            return null;
        }

        void OnPlayerDeath(BasePlayer player)
        {
            if (player == null)
                return;

            JetpackComponent.RemoveJetpackFromPlayer(player.userID);
        }

        void OnPlayerKicked(BasePlayer player, string reason)
        {
            if (player == null)
                return;

            JetpackComponent.RemoveJetpackFromPlayer(player.userID);
        }

        void OnLootSpawn(LootContainer container)
        {
            if (container == null)
                return;

            if (_config.isSpawnInLootEnabled)
            {
                CrateConfig config = _config.crates.FirstOrDefault(x => x.Prefab == container.PrefabName);

                if (config == null)
                    return;

                LootManager.TrySpawnItemInDefaultCrate(container, _config.itemConfig, config.Chance);
            }
        }

        object CanLootEntity(BasePlayer player, SupplyDrop container)
        {
            if (player == null)
                return null;

            if (!_config.isAllowedLootAirdrop)
            {
                JetpackComponent jetPack = JetpackComponent.GetJetpackComponentByUserId(player.userID);

                if (jetPack != null)
                    return true;
            }

            return null;
        }

        void OnSamSiteTargetScan(SamSite samSite, List<SamSite.ISamSiteTarget> targetList)
        {
            if (samSite == null || targetList == null)
                return;

            if (samSite.IsInDefenderMode())
                return;

            if (_config.isPlayerSamsWiillAttack || _config.isMonumentSamsWillAttack)
                JetpackComponent.AddAllJetpacksToSamTargetList(samSite, targetList);
        }

        object OnPlayerCommand(BasePlayer player, string command, string[] args)
        {
            if (player == null)
                return null;

            if (_config.blockedCommands.Any(x => x.ToLower() == command.ToLower()))
            {
                JetpackComponent jetPackComponent = JetpackComponent.GetJetpackComponentByUserId(player.userID);

                if (jetPackComponent != null)
                {
                    NotifyManager.SendMessageToPlayer(player, "CommandBlock", ins._config.prefix);
                    return true;
                }
            }

            return null;
        }

        void OnEntitySpawned(SupplyDrop supplyDrop)
        {
            if (supplyDrop == null)
                return;

            LootManager.OnSuplyDropSpawned(supplyDrop);
        }
        #region SupportedPlugins
        void OnEntityEnterZone(string ZoneID, MovableDroppedItemContainer entity)
        {
            if (entity == null || entity.net == null)
                return;

            if (_config.supportedPluginsConfig.zoneManager.enable)
            {
                JetpackComponent jetPackComponent = JetpackComponent.GetJetDroppedItemNetId(entity.net.ID.Value);

                if (jetPackComponent != null && plugins.Exists("ZoneManager"))
                {
                    if (_config.supportedPluginsConfig.zoneManager.blockIDs.Contains("ZoneID") || _config.supportedPluginsConfig.zoneManager.blockFlags.Any(x => (bool)ZoneManager.Call("HasFlag", ZoneID, x)))
                        jetPackComponent.RemoveJetpack();
                }
            }
        }

        void OnSpaceEventStart()
        {
            SupportedPluginsController.spaceHeight = (float)Space.Call("GetMinSpaceAltitude");
            SupportedPluginsController.isSpaceEventActive = true;
        }

        void OnSpaceEventStop()
        {
            SupportedPluginsController.isSpaceEventActive = false;
            SupportedPluginsController.spaceHeight = 0;
        }
        #endregion SupportedPlugins
        #endregion Hooks

        #region Commands
        [ChatCommand("jet")]
        void JetChatCommand(BasePlayer player, string command, string[] arg)
        {
            if (player == null)
                return;

            JetpackComponent.OnPlayerEnterJetCommand(player);
        }

        [ConsoleCommand("jet")]
        void ModuleGiveConsoleCommande(ConsoleSystem.Arg arg)
        {
            if (arg == null || arg.Player() == null)
                return;

            JetpackComponent.OnPlayerEnterJetCommand(arg.Player());
        }

        [ChatCommand("givejetpack")]
        void GiveJetpackChatCommand(BasePlayer player, string command, string[] arg)
        {
            if (player == null) return;

            if (!permission.UserHasPermission(player.UserIDString, _config.permissionForGiveSelf))
            {
                PrintToChat(player, "You do not have permission to use this command!");
                return;
            }

            LootManager.GiveJetpackItemToPlayer(player);
            NotifyManager.SendMessageToPlayer(player, "GetJetpack", ins._config.prefix);
        }

        [ConsoleCommand("givejetpack")]
        void GiveJetpackConsoleCommand(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();

            if (player == null)
            {
                if (arg.Args == null || arg.Args.Length < 1)
                    return;

                BasePlayer target = BasePlayer.FindByID(Convert.ToUInt64(arg.Args[0]));

                if (target == null)
                {
                    Puts("Player not found");
                    return;
                }

                LootManager.GiveJetpackItemToPlayer(target);
                NotifyManager.SendMessageToPlayer(target, "GetJetpack", ins._config.prefix);
                Puts($"A jetpack was given to {target.displayName}!");
            }
        }
        #endregion Commands

        #region Methods
        void UpdateConfig()
        {
            if (_config.version != Version.ToString())
            {
                VersionNumber versionNumber;
                var versionArray = _config.version.Split('.');
                versionNumber.Major = Convert.ToInt32(versionArray[0]);
                versionNumber.Minor = Convert.ToInt32(versionArray[1]);
                versionNumber.Patch = Convert.ToInt32(versionArray[2]);

                if (versionNumber.Minor == 0)
                {
                    if (versionNumber.Patch < 4)
                    {
                        PrintError("Delete the configuration file!");
                        NextTick(() => Server.Command($"o.unload {Name}"));
                        return;
                    }
                    _config.maxHeight = 1000;
                    _config.blockedCommands = new HashSet<string>
                    {
                        "home"
                    };
                    versionNumber = new VersionNumber(1, 1, 0);
                }
                if (versionNumber.Minor == 1)
                {
                    if (versionNumber.Patch <= 5)
                    {
                        _config.notificationConfig = new NotificationConfig
                        {
                            isChat = true,
                        };
                        _config.control.autoCrenHold = true;
                        _config.supportedPluginsConfig = new SupportedPluginsConfig
                        {
                            zoneManager = new ZoneManagerConfig
                            {
                                enable = false,
                                blockFlags = new HashSet<string>
                            {
                                "eject",
                                "pvegod"
                            },
                                blockIDs = new HashSet<string>
                            {
                                "Example"
                            }
                            }
                        };
                        _config.permissionJetpackCommand = "jetpack.command";
                        _config.wearWhenItemAdded = true;
                    }
                    if (versionNumber.Patch <= 9)
                    {
                        _config.isAirEquipfAllowed = true;
                    }
                    versionNumber = new VersionNumber(1, 2, 0);
                }
                if (versionNumber.Minor == 2)
                {
                    if (versionNumber.Patch <= 3)
                    {
                        _config.homingLauncherConfig = new HomingLauncherConfig
                        {
                            isEnable = true,
                            targetCaptureDistance = 1
                        };
                    }

                    if (versionNumber.Patch <= 8)
                    {
                        _config.permissionForNoDelay = "jetpack.nodelay";
                    }
                }

                _config.version = Version.ToString();
                SaveConfig();
            }
        }

        void RegisterPermissions()
        {
            permission.RegisterPermission(_config.permissionForUse, this);
            permission.RegisterPermission(_config.permissionForGiveSelf, this);
            permission.RegisterPermission(_config.permissionForNoFuel, this);
            permission.RegisterPermission(_config.permissionForHalfFuel, this);
            permission.RegisterPermission(_config.permissionJetpackCommand, this);
            permission.RegisterPermission(_config.permissionForNoDelay, this);
        }

        void Unsubscribes()
        {
            foreach (string hook in subscribeMetods)
                Unsubscribe(hook);
        }

        void Subscribes()
        {
            foreach (string hook in subscribeMetods)
            {
                if (hook == "OnSamSiteTargetScan" && !_config.isPlayerSamsWiillAttack && !_config.isMonumentSamsWillAttack)
                    continue;
                if (hook == "CanMoveItem" && _config.isAirTakeOffAllowed)
                    continue;
                if (hook == "OnPlayerCommand" && _config.blockedCommands.Count == 0)
                    continue;
                if (hook == "OnLootSpawn" && !_config.isSpawnInLootEnabled)
                    continue;
                if (hook == "OnEntityEnterZone" && !_config.supportedPluginsConfig.zoneManager.enable)
                    continue;

                Subscribe(hook);
            }
        }

        static void Debug(params object[] arg)
        {
            string result = "";

            foreach (object obj in arg)
                if (obj != null)
                    result += obj.ToString() + " ";

            ins.Puts(result);
        }
        #endregion Methods

        #region Classes
        class JetpackComponent : FacepunchBehaviour
        {
            static HashSet<JetpackComponent> jetpacks = new HashSet<JetpackComponent>();
            static BUTTON ForceButton = BUTTON.JUMP;

            MovableDroppedItem movableDroppedItem;
            Rigidbody rigidbody;
            BaseMountable chair;
            BasePlayer player;
            SAMTargetComponent samSiteComponent;
            SeekerTargetComponent seekerTargetComponent;
            Coroutine soundCorountine;
            List<BaseEntity> flamethrowers = new List<BaseEntity>();

            bool isGrounded = false;
            float lastSpeed = 0;

            bool infinityFuel = false;
            bool halfFuel = false;
            bool haveFuel = true;
            float lastFuelConsumtionTime = 0;
            float targetConsumtionAmount = 0;

            Coroutine weaponHoldBlockCorountine;

            internal static void OnPlayerEnterJetCommand(BasePlayer player)
            {
                JetpackComponent jetPack = JetpackComponent.GetJetpackComponentByUserId(player.userID);

                if (jetPack != null)
                    jetPack.PlayerWantsTakeOffJetpack();
                else
                    JetpackComponent.TryAttachJetpackToPlayer(player);
            }

            internal static void AddAllJetpacksToSamTargetList(SamSite samSite, List<SamSite.ISamSiteTarget> targetList)
            {
                foreach (JetpackComponent jetPackComponent in jetpacks)
                {
                    if (jetPackComponent == null || jetPackComponent.player == null)
                        continue;

                    BuildingPrivlidge buildingPrivlidge = samSite.GetBuildingPrivilege();

                    if (buildingPrivlidge != null && buildingPrivlidge.IsAuthed(jetPackComponent.player))
                        continue;

                    if (samSite.ShortPrefabName == "sam_static")
                    {
                        if (ins._config.isMonumentSamsWillAttack)
                            targetList.Add(jetPackComponent.samSiteComponent);
                        continue;
                    }
                    else if (ins._config.isPlayerSamsWiillAttack)
                    {
                        targetList.Add(jetPackComponent.samSiteComponent);
                    }
                }
            }

            internal static JetpackComponent GetJetpackComponentByUserId(ulong userId)
            {
                return jetpacks.FirstOrDefault(x => x != null && x.player.userID == userId);
            }

            internal static JetpackComponent GetJetDroppedItemNetId(ulong netId)
            {
                return jetpacks.FirstOrDefault(x => x != null && x.movableDroppedItem.net.ID.Value == netId);
            }

            internal static void RemoveJetpackFromPlayer(ulong playerUserId)
            {
                JetpackComponent jetPack = JetpackComponent.GetJetpackComponentByUserId(playerUserId);

                if (jetPack != null)
                    jetPack.RemoveJetpack();
            }

            internal static void TryAttachJetpackToPlayer(BasePlayer player)
            {
                if (IsPlayerCanWearJetpack(player))
                    CreateJetpack(player);
            }

            static bool IsPlayerCanWearJetpack(BasePlayer player)
            {
                if (player.HasPlayerFlag(BasePlayer.PlayerFlags.Wounded))
                    return false;

                if (player.isMounted)
                    return false;

                JetpackComponent jetPackComponent = GetJetpackComponentByUserId(player.userID);

                if (jetPackComponent != null)
                    return false;

                if (!ins._config.isAirEquipfAllowed && !player.IsOnGround())
                {
                    NotifyManager.SendMessageToPlayer(player, "AirEquipBlock", ins._config.prefix);
                    return false;
                }

                if (LootManager.IsPlayerHaveJetItem(player))
                {
                    if (!ins.permission.UserHasPermission(player.UserIDString, ins._config.permissionForUse) && !ins.permission.UserHasPermission(player.UserIDString, ins._config.permissionJetpackCommand))
                    {
                        NotifyManager.SendMessageToPlayer(player, "NoPermission", ins._config.prefix);
                        return false;
                    }
                }
                else if (!ins.permission.UserHasPermission(player.UserIDString, ins._config.permissionJetpackCommand))
                {
                    return false;
                }

                if (ins._config.wearDelay > 0)
                {
                    float lastUnequipTime = 0;
                    
                    if (!ins.permission.UserHasPermission(player.UserIDString, ins._config.permissionForNoDelay) && ins.unequipTime.TryGetValue(player.userID, out lastUnequipTime))
                    {
                        float timeScienceUse = UnityEngine.Time.realtimeSinceStartup - lastUnequipTime;

                        if (timeScienceUse < ins._config.wearDelay)
                        {
                            NotifyManager.SendMessageToPlayer(player, "Delay", ins._config.prefix, (int)(ins._config.wearDelay - timeScienceUse));
                            return false;
                        }
                    }
                }

                if (IsPlayerAtHome(player))
                {
                    NotifyManager.SendMessageToPlayer(player, "AtHome", ins._config.prefix);
                    return false;
                }
                else if (!SupportedPluginsController.IsZoneManagerAllowJetpackUse(player))
                {
                    NotifyManager.SendMessageToPlayer(player, "AreaBlock", ins._config.prefix);
                    return false;
                }

                return true;
            }

            static bool IsPlayerAtHome(BasePlayer player)
            {
                if (SupportedPluginsController.CheckBySpacePlugin(player))
                    return true;

                Vector3 originPosition = player.transform.position + new Vector3(0, 1.5f, 0);

                if (IsBuildingsInDirection(originPosition, Vector3.up))
                    return true;
                else if (IsBuildingsInDirection(originPosition, player.eyes.BodyForward()) || IsBuildingsInDirection(originPosition, -player.eyes.BodyForward()))
                    return true;
                else if (IsBuildingsInDirection(originPosition, player.eyes.BodyRight()) || IsBuildingsInDirection(originPosition, -player.eyes.BodyRight()))
                    return true;

                return false;
            }

            static bool IsBuildingsInDirection(Vector3 origin, Vector3 direction)
            {
                RaycastHit raycastHit;
                return Physics.Raycast(origin, direction, out raycastHit, 2, 1 << 21 | 1 << 8);
            }

            static void CreateJetpack(BasePlayer player)
            {
                MovableDroppedItem movableDroppedItem = MovableDroppedItem.CreateMovableDroppedItem(player.transform.position + new Vector3(0, 1.3f, 0), Quaternion.Euler(0, player.eyes.GetLookRotation().eulerAngles.y, 0));
                JetpackComponent jetpackComponent = movableDroppedItem.gameObject.AddComponent<JetpackComponent>();
                jetpackComponent.Init(player, movableDroppedItem);
                jetpacks.Add(jetpackComponent);
                jetpacks.RemoveWhere(x => x == null);
            }

            void Init(BasePlayer player, MovableDroppedItem movableDroppedItem)
            {
                this.player = player;
                this.movableDroppedItem = movableDroppedItem;

                GetAndUpdateRigidbody();
                BuildJetpack();

                Interface.CallHook("OnJetpackWear", new object[] { player });

                if (ins.permission.UserHasPermission(player.UserIDString, ins._config.permissionForNoFuel))
                    infinityFuel = true;
                if (ins.permission.UserHasPermission(player.UserIDString, ins._config.permissionForHalfFuel))
                    halfFuel = true;
                if (ins._config.isPlayerSamsWiillAttack || ins._config.isMonumentSamsWillAttack)
                    samSiteComponent = gameObject.AddComponent<SAMTargetComponent>();

                if (ins._config.isSoundEnabled && ins._config.isQuiteSound)
                    soundCorountine = ServerMgr.Instance.StartCoroutine(QuiteSoundCorountine());

                if (ins._config.isWeaponAllowed)
                    weaponHoldBlockCorountine = ServerMgr.Instance.StartCoroutine(WeaponHoldBlockCorountine());

                seekerTargetComponent = SeekerTargetComponent.AttachSeekerTargetComponent(movableDroppedItem);
            }

            void GetAndUpdateRigidbody()
            {
                rigidbody = gameObject.GetComponent<Rigidbody>();
                rigidbody.drag = ins._config.control.drag;
                rigidbody.centerOfMass = new Vector3(0, -1.1f, 0.25f);
                rigidbody.mass = 100;
                rigidbody.angularDrag = 3;
                rigidbody.maxAngularVelocity = 7;
            }

            void BuildJetpack()
            {
                SpawnChair();
                SpawnDecorEntities();
                CreateCollider();
            }

            void SpawnChair()
            {
                string chairPrefabName = ins._config.isWeaponAllowed ? "assets/prefabs/vehicle/seats/testseat.prefab" : "assets/prefabs/vehicle/seats/standingdriver.prefab";
                Vector3 localPosition = new Vector3(0, -1.18f, 0.25f);
                Vector3 localRotation = new Vector3(0, -5f, 0);

                chair = MovableBaseMountable.CreateMovableBaseMountable(movableDroppedItem, chairPrefabName, localPosition, localRotation);
                chair.MountPlayer(player);

                if (ins._config.isThirdPersonView)
                    player.SetPlayerFlag(BasePlayer.PlayerFlags.ThirdPersonViewmode, true);
            }

            void SpawnDecorEntities()
            {
                BuildManager.CreateChildEntity(movableDroppedItem, "assets/prefabs/weapons/rocketlauncher/rocket_launcher.entity.prefab", new Vector3(-0.160f, -0.221f, 0.186f), new Vector3(6.413f, 0.312f, 38.265f));
                BuildManager.CreateChildEntity(movableDroppedItem, "assets/prefabs/weapons/rocketlauncher/rocket_launcher.entity.prefab", new Vector3(0.292f, -0.221f, 0.186f), new Vector3(6.413f, 0.312f, 38.265f));
            }

            void CreateCollider()
            {
                CapsuleCollider capsuleCollider = chair.gameObject.AddComponent<CapsuleCollider>();
                capsuleCollider.gameObject.layer = 12;
                capsuleCollider.direction = 1;

                capsuleCollider.center = new Vector3(0, 0.95f, 0);
                capsuleCollider.height = 2f;
                capsuleCollider.radius = 0.75f;

                capsuleCollider.material.staticFriction = 1;
                capsuleCollider.material.dynamicFriction = 1;
                capsuleCollider.material.frictionCombine = PhysicMaterialCombine.Maximum;
            }

            void Update()
            {
                if (ins._config.isSoundEnabled && !ins._config.isQuiteSound)
                    Sound();
            }

            void OnCollisionExit(Collision collision)
            {
                OnTakeOff();
            }

            void OnTakeOff()
            {
                if (isGrounded)
                {
                    isGrounded = false;
                    rigidbody.maxAngularVelocity = 7;
                }
            }

            void OnCollisionStay(Collision collision)
            {
                OnGrounded();
            }

            void OnGrounded()
            {
                if (!isGrounded)
                {
                    isGrounded = true;
                    rigidbody.maxAngularVelocity = 0.5f;
                }
            }

            void FixedUpdate()
            {
                if (!CheckPasanger())
                {
                    RemoveJetpack();
                    return;
                }

                ControlEngine();
                ControlRotation();
                CheckFall();
                CheckWater();
                SpacePluginController();
            }

            bool CheckPasanger()
            {
                if (!chair.PlayerIsMounted(player) || player.IsSleeping())
                    return false;

                return true;
            }

            void ControlEngine()
            {
                if (!haveFuel)
                {
                    ControlFuelConsumtion();
                    return;
                }

                if (player.serverInput.IsDown(ForceButton))
                {
                    ControlFuelConsumtion();
                    EnableFireEffects();

                    if (transform.position.y < ins._config.maxHeight || SupportedPluginsController.IsPositionInSpace(transform.position))
                        rigidbody.AddForce(transform.up * ins._config.control.force * 100, ForceMode.Force);
                }
                else
                {
                    DisableFireEffects();
                }
            }

            void ControlRotation()
            {
                float rotateSpeedMultiplicator = 1;

                if (player.serverInput.IsDown(BUTTON.FORWARD))
                    transform.RotateAround(transform.position, transform.right, ins._config.control.upDown * rotateSpeedMultiplicator);
                else if (player.serverInput.IsDown(BUTTON.BACKWARD))
                    transform.RotateAround(transform.position, transform.right, -ins._config.control.upDown * rotateSpeedMultiplicator);

                if (player.serverInput.IsDown(BUTTON.RIGHT))
                    transform.RotateAround(transform.position, Vector3.up, ins._config.control.yaw * rotateSpeedMultiplicator);
                else if (player.serverInput.IsDown(BUTTON.LEFT))
                    transform.RotateAround(transform.position, Vector3.up, -ins._config.control.yaw * rotateSpeedMultiplicator);

                if (ins._config.control.autoCrenHold)
                {
                    AutoHorizont();
                }
                else
                {
                    if (player.serverInput.IsDown(BUTTON.DUCK))
                        transform.RotateAround(transform.position, transform.forward, ins._config.control.cren * rotateSpeedMultiplicator);
                    else if (player.serverInput.IsDown(BUTTON.SPRINT))
                        transform.RotateAround(transform.position, transform.forward, -ins._config.control.cren * rotateSpeedMultiplicator);
                }
            }

            void CheckFall()
            {
                if (!ins._config.isFallDamageEnabled)
                    return;

                float deltaSpeed = Math.Abs(rigidbody.velocity.magnitude - lastSpeed);

                if (deltaSpeed > ins._config.fallDamagePoint)
                    player.Hurt((deltaSpeed - ins._config.fallDamagePoint) * ins._config.fallDamageMultiplicator, DamageType.Fall, null, false);

                lastSpeed = rigidbody.velocity.magnitude;
            }

            void CheckWater()
            {
                if (ins._config.isTakeOffJetInWater && WaterLevel.Test(movableDroppedItem.transform.position, true, true))
                    RemoveJetpack();
            }

            void ControlFuelConsumtion()
            {
                if (ins._config.fuel.fuelPerTick <= 0 || infinityFuel)
                    return;

                float timeScienceLastConsumtion = UnityEngine.Time.realtimeSinceStartup - lastFuelConsumtionTime;

                if (timeScienceLastConsumtion >= 2)
                {
                    List<Item> allItems = Pool.Get<List<Item>>();
                    player.inventory.GetAllItems(allItems);
                    Item item = allItems.FirstOrDefault(x => x != null && LootManager.IsFuelItem(x.info.shortname));
                    Pool.FreeUnmanaged(ref allItems);

                    if (item == null)
                    {
                        if (haveFuel)
                        {
                            NotifyManager.SendMessageToPlayer(player, "NoFuel", ins._config.prefix);
                            haveFuel = false;
                            DisableFireEffects();
                        }
                        return;
                    }

                    lastFuelConsumtionTime = UnityEngine.Time.realtimeSinceStartup;
                    float fuelAmount = ins._config.fuel.fuelPerTick;
                    if (halfFuel) fuelAmount *= 0.5f;
                    targetConsumtionAmount += fuelAmount;
                    int consumtionInInt = (int)targetConsumtionAmount;

                    if (consumtionInInt > 0)
                    {
                        targetConsumtionAmount -= consumtionInInt;

                        if (item.amount > consumtionInInt)
                        {
                            item.amount = item.amount - consumtionInInt;
                            item.MarkDirty();
                            haveFuel = true;
                        }
                        else
                        {
                            item.Remove();
                        }
                    }
                }
            }

            void EnableFireEffects()
            {
                if (flamethrowers.Count > 0 || !haveFuel)
                    return;

                CreateFireGun(new Vector3(0.118f, 0.206f, 0f), new Vector3(355.290f, 270.032f, 89.903f));
                CreateFireGun(new Vector3(-0.129f, 0.208f, 0.061f), new Vector3(355.290f, 90.032f, 89.903f));
            }

            void CreateFireGun(Vector3 localPosition, Vector3 localRotation)
            {
                BaseEntity entity = BuildManager.CreateChildEntity(movableDroppedItem, "assets/prefabs/weapons/military flamethrower/militaryflamethrower.entity.prefab", localPosition, localRotation);
                entity.SetFlag(BaseEntity.Flags.Reserved8, true);
                flamethrowers.Add(entity);
            }

            void DisableFireEffects()
            {
                if (flamethrowers.Count == 0)
                    return;

                foreach (BaseEntity entity in flamethrowers)
                    entity.Kill();

                flamethrowers.Clear();
            }

            void AutoHorizont()
            {
                if (isGrounded)
                    return;

                float horizonAngle = Vector3.Angle(transform.right, new Vector3(transform.right.x, 0, transform.right.z));
                bool right = transform.right.y < 0;

                if (right)
                    horizonAngle *= -1;

                rigidbody.AddTorque(-transform.forward * horizonAngle * 5);
            }

            void SpacePluginController()
            {
                if (SupportedPluginsController.isSpaceEventActive)
                {
                    if (SupportedPluginsController.IsPositionInSpace(player.transform.position))
                    {
                        if (rigidbody.useGravity)
                            rigidbody.useGravity = false;
                    }
                    else if (!rigidbody.useGravity)
                        rigidbody.useGravity = true;
                }
                else if (!rigidbody.useGravity)
                    rigidbody.useGravity = true;
            }

            IEnumerator QuiteSoundCorountine()
            {
                while (true)
                {
                    if (flamethrowers.Count > 0)
                        Effect.server.Run("assets/bundled/prefabs/fx/impacts/blunt/sand/sand.prefab", player.transform.position);

                    yield return CoroutineEx.waitForSeconds(0.2f);
                }
            }

            void Sound()
            {
                if (flamethrowers.Count > 0)
                    Effect.server.Run("assets/bundled/prefabs/fx/impacts/blunt/sand/sand.prefab", player.transform.position);
            }

            IEnumerator WeaponHoldBlockCorountine()
            {
                while (true)
                {
                    if (isGrounded && LootManager.IsPositionNearToSupplyDrop(transform.position))
                        RemoveJetpack();

                    yield return CoroutineEx.waitForSeconds(1f);
                }
            }

            internal void PlayerWantsTakeOffJetpack()
            {
                if (IsPlayerCanTakeOffJetpack())
                    RemoveJetpack();
            }

            internal bool IsPlayerCanTakeOffJetpack()
            {
                return ins._config.isAirTakeOffAllowed || IsJetpackOnTheGround() || IsJetpackOnCargoShip() || SupportedPluginsController.IsPositionInSpace(transform.position);
            }

            internal bool IsJetpackOnTheGround()
            {
                return isGrounded;
            }

            internal bool IsJetpackOnCargoShip()
            {
                foreach (Collider collider in UnityEngine.Physics.OverlapSphere(transform.position, 2f))
                {
                    BaseEntity entity = collider.ToBaseEntity();

                    if (entity == null)
                        continue;
                    else if (entity is CargoShip)
                        return true;
                }

                return false;
            }

            internal static void RemoveAllJetpacks()
            {
                foreach (JetpackComponent jetpack in jetpacks)
                    if (jetpack != null)
                        jetpack.RemoveJetpack(true);
            }

            internal void RemoveJetpack(bool removeAllJetpacks = false)
            {
                movableDroppedItem.Kill();

                if (!removeAllJetpacks)
                    jetpacks.Remove(this);

                if (ins._config.wearDelay > 0)
                {
                    if (ins.unequipTime.ContainsKey(player.userID))
                        ins.unequipTime[player.userID] = UnityEngine.Time.realtimeSinceStartup;
                    else
                        ins.unequipTime.Add(player.userID, UnityEngine.Time.realtimeSinceStartup);
                }
            }

            void OnDestroy()
            {
                chair.DismountPlayer(player, true);

                if (soundCorountine != null)
                    ServerMgr.Instance.StopCoroutine(soundCorountine);

                if (weaponHoldBlockCorountine != null)
                    ServerMgr.Instance.StopCoroutine(weaponHoldBlockCorountine);

                if (player != null)
                    player.SetPlayerFlag(BasePlayer.PlayerFlags.ThirdPersonViewmode, false);

                if (seekerTargetComponent != null)
                    seekerTargetComponent.KillComponent();

                Interface.CallHook("OnJetpackRemoved", new object[] { player });
            }

            private class SAMTargetComponent : FacepunchBehaviour, SamSite.ISamSiteTarget
            {
                BaseEntity baseEntity;
                Rigidbody rigidbody;

                private void Awake()
                {
                    baseEntity = GetComponent<BaseEntity>();
                    rigidbody = GetComponent<Rigidbody>();
                }

                public bool IsValidSAMTarget() => true;

                public Vector3 Position => baseEntity.transform.position;

                public SamSite.SamTargetType SAMTargetType => ins._config.isSamsRadiusAndSpeedIncreased ? SamSite.targetTypeMissile : SamSite.targetTypeVehicle;

                public bool isClient => false;

                public bool IsValidSAMTarget(bool isStaticSamSite) => true;

                public Vector3 CenterPoint() => transform.position;

                public Vector3 GetWorldVelocity()
                {
                    return rigidbody.velocity;
                }

                public bool IsVisible(Vector3 position, float distance) => baseEntity.IsVisible(position, distance);
            }
        }

        sealed class MovableDroppedItem : DroppedItem
        {
            internal static MovableDroppedItem CreateMovableDroppedItem(Vector3 position, Quaternion rotation)
            {
                DroppedItem droppedItem = GameManager.server.CreateEntity("assets/prefabs/misc/burlap sack/generic_world.prefab", position, rotation) as DroppedItem;
                droppedItem.enableSaving = false;
                droppedItem.allowPickup = false;
                droppedItem.item = ItemManager.CreateByName("largebackpack");
                MovableDroppedItem movableDroppedItem = droppedItem.gameObject.AddComponent<MovableDroppedItem>();
                BuildManager.CopySerializableFields(droppedItem, movableDroppedItem);
                droppedItem.StopAllCoroutines();
                UnityEngine.GameObject.DestroyImmediate(droppedItem, true);
                movableDroppedItem.Spawn();
                movableDroppedItem.allowPickup = false;
                movableDroppedItem.SetFlag(BaseEntity.Flags.Busy, true);
                movableDroppedItem.SetFlag(BaseEntity.Flags.Locked, true);
                movableDroppedItem.SetFlag(BaseEntity.Flags.Reserved3, true);
                return movableDroppedItem;
            }

            public override float MaxVelocity()
            {
                return 100;
            }

            public override float GetDespawnDuration()
            {
                return float.MaxValue;
            }
        }

        class MovableDroppedItemContainer : DroppedItemContainer
        {
            public override float MaxVelocity()
            {
                return 100;
            }
        }

        class SeekerTargetComponent : FacepunchBehaviour, SeekerTarget.ISeekerTargetOwner
        {
            MovableDroppedItem movableDroppedItem;

            internal static SeekerTargetComponent AttachSeekerTargetComponent(MovableDroppedItem movableDroppedItem)
            {
                if (!ins._config.homingLauncherConfig.isEnable)
                    return null;

                GameObject gameObject = new GameObject("SeekerTargetComponent");
                gameObject.transform.SetParent(movableDroppedItem.transform, false);
                SeekerTargetComponent seekerTargetComponent = gameObject.AddComponent<SeekerTargetComponent>();
                seekerTargetComponent.Init(movableDroppedItem);
                return seekerTargetComponent;
            }

            void Init(MovableDroppedItem movableDroppedItem)
            {
                this.movableDroppedItem = movableDroppedItem;
                SeekerTarget.SeekerStrength seekerStrength = ins._config.homingLauncherConfig.targetCaptureDistance == 0 ? SeekerTarget.SeekerStrength.LOW : ins._config.homingLauncherConfig.targetCaptureDistance == 1 ? SeekerTarget.SeekerStrength.MEDIUM : SeekerTarget.SeekerStrength.HIGH;
                SeekerTarget.SetSeekerTarget(this, seekerStrength);
            }

            public bool InSafeZone()
            {
                return movableDroppedItem.InSafeZone();
            }

            public bool IsValidHomingTarget()
            {
                return true;
            }

            public void OnEntityMessage(BaseEntity from, string msg)
            {

            }

            public Vector3 CenterPoint()
            {
                return movableDroppedItem.WorldSpaceBounds().position;
            }

            public bool IsVisible(Vector3 position, float maxDistance = float.PositiveInfinity)
            {
                return movableDroppedItem.IsVisibleAndCanSee(position) || movableDroppedItem.children.Any(x => x != null && x.IsVisibleAndCanSee(position));
            }

            internal void KillComponent()
            {
                if (this.gameObject != null)
                    UnityEngine.GameObject.Destroy(this.gameObject);
            }
        }

        sealed class MovableBaseMountable : BaseMountable
        {
            internal static MovableBaseMountable CreateMovableBaseMountable(BaseEntity parentEntity, string seatPrefab, Vector3 localPosition, Vector3 localRotation)
            {
                BaseMountable baseMountable = GameManager.server.CreateEntity(seatPrefab, parentEntity.transform.position) as BaseMountable;
                baseMountable.enableSaving = false;

                MovableBaseMountable movableBaseMountable = baseMountable.gameObject.AddComponent<MovableBaseMountable>();
                BuildManager.CopySerializableFields(baseMountable, movableBaseMountable);

                baseMountable.StopAllCoroutines();
                UnityEngine.GameObject.DestroyImmediate(baseMountable, true);
                BuildManager.SetParent(parentEntity, movableBaseMountable, localPosition, localRotation);
                movableBaseMountable.Spawn();
                return movableBaseMountable;
            }

            public override void DismountAllPlayers()
            {

            }

            public override bool GetDismountPosition(BasePlayer player, out Vector3 res, bool silent = false)
            {
                res = player.transform.position;
                return true;
            }
        }

        static class LootManager
        {
            static HashSet<SupplyDrop> supplyDrops = new HashSet<SupplyDrop>();

            internal static void OnSuplyDropSpawned(SupplyDrop supplyDrop)
            {
                supplyDrops.RemoveWhere(x => !x.IsExists());
                supplyDrops.Add(supplyDrop);
            }

            internal static bool IsPositionNearToSupplyDrop(Vector3 position)
            {
                return supplyDrops.Any(x => x.IsExists() && Vector3.Distance(position, x.transform.position) < 3);
            }

            internal static bool IsFuelItem(string shortname)
            {
                return shortname == ins._config.fuel.itemShortname;
            }

            internal static bool IsJetpackItem(Item item)
            {
                if (item == null || item.info == null)
                    return false;

                return item.info.shortname == ins._config.itemConfig.shortname && item.skin == ins._config.itemConfig.skin;
            }

            internal static bool IsPlayerHaveJetItem(BasePlayer player)
            {
                if (ins._config.allowOpenFromAnyContainer)
                {
                    List<Item> allItems = Pool.Get<List<Item>>();
                    player.inventory.GetAllItems(allItems);

                    if (allItems.Any(x => IsJetpackItem(x)))
                    {
                        Pool.FreeUnmanaged(ref allItems);
                        return true;
                    }
                    else
                    {
                        Pool.FreeUnmanaged(ref allItems);
                        return false;
                    }
                }
                else
                {
                    return player.inventory.containerWear.itemList.Any(x => IsJetpackItem(x));
                }
            }

            internal static void TrySpawnItemInDefaultCrate(LootContainer lootContatiner, ItemConfig itemConfig, float chance, int removeItemIndex = 0)
            {
                ins.NextTick(() =>
                {
                    if (lootContatiner == null)
                        return;

                    if (UnityEngine.Random.Range(0f, 100f) <= chance)
                    {
                        Item item = CreateItem(itemConfig, 1);
                        if (lootContatiner.inventory.itemList.Count > removeItemIndex)
                        {
                            Item removeItem = lootContatiner.inventory.itemList[removeItemIndex];

                            if (removeItem != null)
                                lootContatiner.inventory.Remove(removeItem);
                        }

                        if (!item.MoveToContainer(lootContatiner.inventory))
                            item.Remove();
                    }
                });
            }

            internal static void GiveJetpackItemToPlayer(BasePlayer player)
            {
                Item item = CreateItem(ins._config.itemConfig, 1);
                UpdateJetpackItem(item);

                if (item != null)
                    GiveItemToPLayer(player, item);
            }

            internal static void UpdateJetpackItem(Item item)
            {
                if (item == null || item.info == null)
                    return;
            }

            static void GiveItemToPLayer(BasePlayer player, Item item)
            {
                int spaceCountItem = PLayerInventory.GetSpaceCountItem(player, item.info.shortname, item.MaxStackable(), item.skin);
                int inventoryItemCount;

                if (spaceCountItem > item.amount)
                    inventoryItemCount = item.amount;
                else
                    inventoryItemCount = spaceCountItem;

                if (inventoryItemCount > 0)
                {
                    Item itemInventory = ItemManager.CreateByName(item.info.shortname, inventoryItemCount, item.skin);

                    if (item.skin != 0)
                        itemInventory.name = item.name;

                    item.amount -= inventoryItemCount;
                    PLayerInventory.MoveInventoryItem(player, itemInventory);
                }

                if (item.amount > 0)
                    PLayerInventory.DropExtraItem(player, item);
            }

            internal static Item CreateItem(ItemConfig itemConfig, int amount)
            {
                Item item = ItemManager.CreateByName(itemConfig.shortname, amount, itemConfig.skin);

                if (itemConfig.name != "")
                    item.name = itemConfig.name;

                return item;
            }

            static class PLayerInventory
            {
                internal static int GetSpaceCountItem(BasePlayer player, string shortname, int stack, ulong skinID)
                {
                    int inventoryCapacity = player.inventory.containerMain.capacity + player.inventory.containerBelt.capacity;
                    int taken = player.inventory.containerMain.itemList.Count + player.inventory.containerBelt.itemList.Count;
                    int result = (inventoryCapacity - taken) * stack;

                    List<Item> allItems = Pool.Get<List<Item>>();
                    player.inventory.GetAllItems(allItems);

                    foreach (Item item in allItems)
                        if (item.info.shortname == shortname && item.skin == skinID && item.amount < stack)
                            result += stack - item.amount;

                    Pool.FreeUnmanaged(ref allItems);

                    return result;
                }

                internal static void MoveInventoryItem(BasePlayer player, Item item)
                {
                    if (item.amount <= item.MaxStackable())
                    {
                        List<Item> allItems = Pool.Get<List<Item>>();
                        player.inventory.GetAllItems(allItems);

                        foreach (Item itemInv in allItems)
                        {
                            if (itemInv.info.shortname == item.info.shortname && itemInv.skin == item.skin && itemInv.amount < itemInv.MaxStackable())
                            {
                                if (itemInv.amount + item.amount <= itemInv.MaxStackable())
                                {
                                    itemInv.amount += item.amount;
                                    itemInv.MarkDirty();
                                    Pool.FreeUnmanaged(ref allItems);
                                    return;
                                }
                                else
                                {
                                    item.amount -= itemInv.MaxStackable() - itemInv.amount;
                                    itemInv.amount = itemInv.MaxStackable();
                                }
                            }
                        }

                        Pool.FreeUnmanaged(ref allItems);

                        if (item.amount > 0) player.inventory.GiveItem(item);
                    }
                    else
                    {
                        while (item.amount > item.MaxStackable())
                        {
                            Item thisItem = ItemManager.CreateByName(item.info.shortname, item.MaxStackable(), item.skin);
                            if (item.skin != 0) thisItem.name = item.name;
                            player.inventory.GiveItem(thisItem);
                            item.amount -= item.MaxStackable();
                        }
                        if (item.amount > 0) player.inventory.GiveItem(item);
                    }
                }

                internal static void DropExtraItem(BasePlayer player, Item item)
                {
                    if (item.amount <= item.MaxStackable()) item.Drop(player.transform.position, Vector3.up);
                    else
                    {
                        while (item.amount > item.MaxStackable())
                        {
                            Item thisItem = ItemManager.CreateByName(item.info.shortname, item.MaxStackable(), item.skin);
                            if (item.skin != 0) thisItem.name = item.name;
                            thisItem.Drop(player.transform.position, Vector3.up);
                            item.amount -= item.MaxStackable();
                        }
                        if (item.amount > 0) item.Drop(player.transform.position, Vector3.up);
                    }
                }
            }
        }

        static class BuildManager
        {
            internal static BaseEntity CreateChildEntity(BaseEntity parrentEntity, string prefabName, Vector3 localPosition, Vector3 localRotation, ulong skinID = 0)
            {
                BaseEntity entity = CreateEntity(prefabName, parrentEntity.transform.position, Quaternion.identity);
                if (entity == null)
                    return null;

                DestroyUnnessesaryComponents(entity);
                entity.skinID = skinID;
                SetParent(parrentEntity, entity, localPosition, localRotation);
                entity.SetFlag(BaseEntity.Flags.Reserved8, true);
                entity.Spawn();

                return entity;
            }

            internal static void SetParent(BaseEntity parrentEntity, BaseEntity childEntity, Vector3 localPosition, Vector3 localRotation)
            {
                childEntity.SetParent(parrentEntity, true, false);
                childEntity.transform.localPosition = localPosition;
                childEntity.transform.localEulerAngles = localRotation;
            }

            internal static MovableDroppedItemContainer CreateMovableBackpackOnPLayerBack(BasePlayer player)
            {
                Vector3 rotation = player.eyes.GetLookRotation().eulerAngles;
                Vector3 position = player.transform.position + new Vector3(0, 1.3f, 0);

                DroppedItemContainer droppedItemContainer = CreateEntity("assets/prefabs/misc/item drop/item_drop_backpack.prefab", position, Quaternion.Euler(270, rotation.y, rotation.z)) as DroppedItemContainer;
                MovableDroppedItemContainer movableDroppedContainer = droppedItemContainer.gameObject.AddComponent<MovableDroppedItemContainer>();
                CopySerializableFields(droppedItemContainer, movableDroppedContainer);
                UnityEngine.GameObject.DestroyImmediate(droppedItemContainer, true);

                movableDroppedContainer.SetFlag(BaseEntity.Flags.Busy, true);
                movableDroppedContainer.SetFlag(BaseEntity.Flags.Locked, true);
                movableDroppedContainer.SetFlag(BaseEntity.Flags.Reserved8, true);
                movableDroppedContainer.OwnerID = player.userID;
                movableDroppedContainer.Spawn();

                return movableDroppedContainer;
            }

            internal static BaseEntity CreateEntity(string prefabName, Vector3 position, Quaternion rotation, bool enableSaving = false)
            {
                BaseEntity entity = GameManager.server.CreateEntity(prefabName, position, rotation);
                entity.enableSaving = enableSaving;
                return entity;
            }

            static void DestroyUnnessesaryComponents(BaseEntity entity)
            {
                DestroyEntityConponent<Rigidbody>(entity);
                DestroyEntityConponent<GroundWatch>(entity);
                DestroyEntityConponent<DestroyOnGroundMissing>(entity);
            }

            internal static void DestroyEntityConponent<T>(BaseEntity entity)
            {
                T component = entity.GetComponent<T>();
                if (component != null) UnityEngine.GameObject.DestroyImmediate(component as UnityEngine.Object);
            }

            internal static void CopySerializableFields<T>(T src, T dst)
            {
                FieldInfo[] srcFields = typeof(T).GetFields(BindingFlags.Public | BindingFlags.Instance);
                foreach (FieldInfo field in srcFields)
                {
                    object value = field.GetValue(src);
                    field.SetValue(dst, value);
                }
            }
        }

        static class NotifyManager
        {
            internal static void PrintErrorToConsole(string langKey, params object[] args)
            {
                string langMessage = GetMessage(langKey, null, args);
                string consoleMessage = ClearColorAndSize(langMessage);
                ins.PrintError(consoleMessage);
            }

            internal static string ClearColorAndSize(string message)
            {
                message = message.Replace("</color>", string.Empty);
                message = message.Replace("</size>", string.Empty);
                while (message.Contains("<color="))
                {
                    int index = message.IndexOf("<color=");
                    message = message.Remove(index, message.IndexOf(">", index) - index + 1);
                }
                while (message.Contains("<size="))
                {
                    int index = message.IndexOf("<size=");
                    message = message.Remove(index, message.IndexOf(">", index) - index + 1);
                }
                return message;
            }

            internal static void SendMessageToPlayer(BasePlayer player, string langKey, params object[] args)
            {
                if (ins._config.notificationConfig.isChat)
                    ins.PrintToChat(player, GetMessage(langKey, player.UserIDString, args));
            }
        }

        static class SupportedPluginsController
        {
            internal static bool isSpaceEventActive = false;
            internal static float spaceHeight = 0;

            internal static bool IsPositionInSpace(Vector3 position)
            {
                return isSpaceEventActive && spaceHeight > 0 && position.y > spaceHeight;
            }

            internal static bool CheckBySpacePlugin(BasePlayer player)
            {
                if (IsPositionInSpace(player.transform.position))
                {
                    RaycastHit raycastHit;

                    if (Physics.Raycast(player.eyes.transform.position, Vector3.up, out raycastHit, 10, 1 << 21) && Physics.Raycast(player.eyes.transform.position, -Vector3.up, out raycastHit, 10, 1 << 21) && !raycastHit.collider.name.Contains("floor.triangle.twig") && !raycastHit.collider.name.Contains("floor.frame"))
                        return true;

                    if (player.GetMounted() != null && player.GetMounted().PrefabName == "assets/prefabs/vehicle/seats/testseat.prefab")
                        player.GetMounted().Kill();
                }

                return false;
            }

            internal static bool IsZoneManagerAllowJetpackUse(BasePlayer player)
            {
                if (!ins._config.supportedPluginsConfig.zoneManager.enable || !ins.plugins.Exists("ZoneManager"))
                    return true;

                string[] playerZones = (string[])ins.ZoneManager.Call("GetPlayerZoneIDs", player);

                if (playerZones == null || playerZones.Length == 0)
                    return true;

                if (playerZones.Any(zoneName => ins._config.supportedPluginsConfig.zoneManager.blockIDs.Contains(zoneName)))
                    return false;
                else if (playerZones.Any(zoneName => ins._config.supportedPluginsConfig.zoneManager.blockFlags.Any(flagName => (bool)ins.ZoneManager.Call("HasFlag", zoneName, flagName))))
                    return false;

                return true;
            }
        }
        #endregion Classes

        #region Lang
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["GetJetpack"] = "{0} You <color=#738d43>got</color> a jetpack!",
                ["NoPermission"] = "{0} You <color=#b03b1e>do not have permission</color> to use a jetpack!",
                ["AtHome"] = "{0} You <color=#b03b1e>cannot</color> use a jetpack indoors!",
                ["AirEquipBlock"] = "{0} It is <color=#b03b1e>forbidden</color> to equip a jetpack in the air!",
                ["NoFuel"] = "{0} To use the jetpack, put fuel to <color=#738d43>inventory</color>!",
                ["CommandBlock"] = "{0} This command is <color=#b03b1e>not allowed</color> to be used with a jetpack!",
                ["AreaBlock"] = "{0} It is <color=#b03b1e>forbidden</color> to use a jetpack in this area!",
                ["ActivateCommand"] = "{0} To activate the jetpack, use the command <color=#738d43>/jet</color>",
                ["Delay"] = "{0} You will be able to use the jetpack in <color=#738d43>{1}</color> s.",
            }, this);

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["GetJetpack"] = "{0} Вы <color=#738d43>получили</color> реактивный ранец!",
                ["NoPermission"] = "{0} У вас <color=#b03b1e>нет разрешения</color> использовать реактивный ранец!",
                ["AtHome"] = "{0} <color=#b03b1e>Запрещено</color> использовать реактивный ранец в помещении!",
                ["AirEquipBlock"] = "{0} <color=#b03b1e>Запрещено</color> надевать джетпак в воздухе!",
                ["NoFuel"] = "{0} Для использования реактивного ранца положите топливо в <color=#2257b3>инвентарь</color>!",
                ["CommandBlock"] = "{0} Эту команду <color=#b03b1e>запрещено</color> использовать на джетпаке!",
                ["AreaBlock"] = "{0} <color=#b03b1e>Запрещено</color> использовать джетпак в этой области!",
                ["ActivateCommand"] = "{0} Для активации джетпака используйте команду <color=#738d43>/jet</color>!",
                ["Delay"] = "{0} Вы сможете использовать джетпак через <color=#738d43>{1}</color> с.",
            }, this, "ru");
        }

        static string GetMessage(string langKey, string userID) => ins.lang.GetMessage(langKey, ins, userID);

        static string GetMessage(string langKey, string userID, params object[] args) => (args.Length == 0) ? GetMessage(langKey, userID) : string.Format(GetMessage(langKey, userID), args);
        #endregion Lang

        #region Config  

        private PluginConfig _config;

        protected override void LoadDefaultConfig() => _config = PluginConfig.DefaultConfig();

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<PluginConfig>();
            Config.WriteObject(_config, true);
        }

        protected override void SaveConfig() => Config.WriteObject(_config);

        public class ItemConfig
        {
            [JsonProperty(en ? "Shortname" : "Shortname")] public string shortname { get; set; }
            [JsonProperty(en ? "Skin" : "Skin")] public ulong skin { get; set; }
            [JsonProperty(en ? "Name" : "Name")] public string name { get; set; }
        }

        public class FuelConfig
        {
            [JsonProperty(en ? "Use fuel" : "Использовать топливо?")] public bool fuel { get; set; }
            [JsonProperty(en ? "Fuel period" : "Период забора топлива")] public float periodFuel { get; set; }
            [JsonProperty(en ? "Fuel consumption" : "Потребление топлива за период")] public int fuelPerTick { get; set; }
            [JsonProperty(en ? "Item" : "Предмет")] public string itemShortname { get; set; }
        }

        public class HomingLauncherConfig
        {
            [JsonProperty(en ? "Allow the Homing Launcher to attack the biplane? [true/false]" : "Разрешить ПЗРК атаковать биплан? [true/false]")] public bool isEnable { get; set; }
            [JsonProperty(en ? "Target capture distance (0 - 100m, 1 - 200m, 2 - 1000m)" : "Дистанция для захвата биплана (0 - 100м, 1 - 200м, 2 - 1000м)")] public int targetCaptureDistance { get; set; }
        }

        public class ControlConfig
        {
            [JsonProperty(en ? "Air resistance" : "Сопротивление воздуха")] public float drag { get; set; }
            [JsonProperty(en ? "Thrust" : "Тяга")] public float force { get; set; }
            [JsonProperty(en ? "Pitch" : "Тангаж")] public float upDown { get; set; }
            [JsonProperty(en ? "Roll" : "Крен")] public float cren { get; set; }
            [JsonProperty(en ? "Yaw" : "Рыскание")] public float yaw { get; set; }
            [JsonProperty(en ? "Control assistance" : "Помощь в управлении")] public bool autoCrenHold { get; set; }
        }

        public class CrateConfig
        {
            [JsonProperty(en ? "Prefab" : "Префаб ящика")] public string Prefab { get; set; }
            [JsonProperty(en ? "Chance" : "Шанс")] public float Chance { get; set; }
        }

        public class NotificationConfig
        {
            [JsonProperty(en ? "Use Chat Notifications? [true/false]" : "Использовать ли чат? [true/false]")] public bool isChat { get; set; }
        }

        public class SupportedPluginsConfig
        {
            [JsonProperty(en ? "ZoneManager setting" : "Настройка ZoneManager")] public ZoneManagerConfig zoneManager { get; set; }
        }

        public class ZoneManagerConfig
        {
            [JsonProperty(en ? "Do you use the ZoneManager? [true/false]" : "Использовать ли ZoneManager? [true/false]")] public bool enable { get; set; }
            [JsonProperty(en ? "List of zone flags that block spawn" : "Список флагов, при наличии в зоне которого спутник не будет спавниться")] public HashSet<string> blockFlags { get; set; }
            [JsonProperty(en ? "List of zone IDs that block spawn" : "Список ID зон, которые запретят спавн спутника")] public HashSet<string> blockIDs { get; set; }
        }

        private class PluginConfig
        {
            [JsonProperty(en ? "Version" : "Версия")] public string version { get; set; }
            [JsonProperty(en ? "Prefix of chat messages" : "Префикс в чате")] public string prefix { get; set; }
            [JsonProperty(en ? "Permission to use" : "Разрешение для использования")] public string permissionForUse { get; set; }
            [JsonProperty(en ? "Permission to use a Jetpack without a Jetpack item in the inventory (chat command - /jet)" : "Разрешение для использования Джетпака без соответствующего предмета в инвентаре (команда - /jet)")] public string permissionJetpackCommand { get; set; }
            [JsonProperty(en ? "Permission to give to yourself" : "Разрешение для выдачи себе")] public string permissionForGiveSelf { get; set; }
            [JsonProperty(en ? "Permission to turn off fuel" : "Разрешение для отключения топлива")] public string permissionForNoFuel { get; set; }
            [JsonProperty(en ? "Permission for half fuel consumption (VIP)" : "Разрешение для половинного расхода топлива (VIP)")] public string permissionForHalfFuel { get; set; }
            [JsonProperty(en ? "Permission to disable the delay between uses" : "Разрешение для отключения задержки между использованиями")] public string permissionForNoDelay { get; set; }
            [JsonProperty(en ? "A cooldown period during which the player cannot equip the jetpack after unequipping it [sec]" : "Задержка, в течение которой игрок не может надеть джетпак после его снятия [sec]")] public float wearDelay { get; set; }
            [JsonProperty(en ? "Activate the jetpack when moving an item into a clothing container?" : "Активировать джетпак при перемещении предмета в контейнер одежды? ")] public bool wearWhenItemAdded { get; set; }
            [JsonProperty(en ? "Allow the jetpack to be activated with the /jet command from any inventory container? (false - only from the clothing container)" : "Разрешить активировать джетпак командой /jet из любого контейнера инвентаря? (false - только из контейнера одежды)")] public bool allowOpenFromAnyContainer { get; set; }
            [JsonProperty(en ? "Maximum flight altitude" : "Максимальная высота полета")] public float maxHeight { get; set; }
            [JsonProperty(en ? "Allow the use of weapons on the jetpack?" : "Разрешить использование оружия на джетпаке?")] public bool isWeaponAllowed { get; set; }
            [JsonProperty(en ? "Take off a jetpack in the water?" : "Снимать джетпак при попадании в воду")] public bool isTakeOffJetInWater { get; set; }
            [JsonProperty(en ? "Allow to take off the jetpack in the air" : "Разрешить снимать джетпак в воздухе")] public bool isAirTakeOffAllowed { get; set; }
            [JsonProperty(en ? "Allow to equip a jetpack in the air" : "Разрешить одевать джетпак в воздухе")] public bool isAirEquipfAllowed { get; set; }
            [JsonProperty(en ? "Third-person view" : "Вид от третьего лица")] public bool isThirdPersonView { get; set; }
            [JsonProperty(en ? "Turn on the sound?" : "Включить звук при полете")] public bool isSoundEnabled { get; set; }
            [JsonProperty(en ? "Use quiet sound" : "Уменьшить громкость звука")] public bool isQuiteSound { get; set; }
            [JsonProperty(en ? "Enable collision damage" : "Включить урон от столкновений")] public bool isFallDamageEnabled { get; set; }
            [JsonProperty(en ? "Collision Damage Multiplier" : "Множитель урона от столкновений")] public float fallDamageMultiplicator { get; set; }
            [JsonProperty(en ? "Collision acceleration threshold" : "Порог урона от столкновений")] public float fallDamagePoint { get; set; }
            [JsonProperty(en ? "The SamSite will attack the jetpack" : "ПВО будет атаковать джетпак")] public bool isPlayerSamsWiillAttack { get; set; }
            [JsonProperty(en ? "The SamSite on the monuments will attack the jetpack" : "ПВО на рт будет атаковать джетпак")] public bool isMonumentSamsWillAttack { get; set; }
            [JsonProperty(en ? "Increased missile speed and SamSite attack radius by jetpack" : "Увеличеные скорость ракет и радиус атаки ПВО по джетпаку")] public bool isSamsRadiusAndSpeedIncreased { get; set; }
            [JsonProperty(en ? "Homing Launcher Config" : "Настройки ПЗРК")] public HomingLauncherConfig homingLauncherConfig { get; set; }
            [JsonProperty(en ? "Allow loot supply drop when using a jetpack" : "Разрешить лутать аирдроп при использовании джетпака")] public bool isAllowedLootAirdrop { get; set; }
            [JsonProperty(en ? "List of commands that are prohibited while using the jetpack" : "Список команд, которые запрещены во время использования джетпака")] public HashSet<string> blockedCommands { get; set; }
            [JsonProperty(en ? "Control" : "Управление")] public ControlConfig control { get; set; }
            [JsonProperty(en ? "Fuel" : "Топливо")] public FuelConfig fuel { get; set; }
            [JsonProperty(en ? "Item" : "Предмет")] public ItemConfig itemConfig { get; set; }
            [JsonProperty(en ? "Enable spawn in crates" : "Включить спавн в ящиках")] public bool isSpawnInLootEnabled { get; set; }
            [JsonProperty(en ? "Spawn setting" : "Настройка спавна в ящиках")] public List<CrateConfig> crates { get; set; }
            [JsonProperty(en ? "Notification Settings" : "Настройкa уведомлений")] public NotificationConfig notificationConfig { get; set; }
            [JsonProperty(en ? "Supported Plugins" : "Поддерживаемые плагины")] public SupportedPluginsConfig supportedPluginsConfig { get; set; }

            public static PluginConfig DefaultConfig()
            {
                return new PluginConfig()
                {
                    version = "1.2.9",
                    prefix = "[JetPack]",
                    permissionForUse = "jetpack.use",
                    permissionJetpackCommand = "jetpack.command",
                    permissionForGiveSelf = "jetpack.giveself",
                    permissionForNoFuel = "jetpack.fuel",
                    permissionForHalfFuel = "jetpack.vip",
                    permissionForNoDelay = "jetpack.nodelay",
                    wearDelay = 10,
                    wearWhenItemAdded = false,
                    allowOpenFromAnyContainer = false,
                    maxHeight = 1000,
                    isWeaponAllowed = true,
                    isTakeOffJetInWater = true,
                    isAirTakeOffAllowed = false,
                    isAirEquipfAllowed = false,
                    isThirdPersonView = false,
                    isSoundEnabled = true,
                    isQuiteSound = false,
                    isFallDamageEnabled = true,
                    fallDamageMultiplicator = 5f,
                    fallDamagePoint = 2.5f,
                    isPlayerSamsWiillAttack = true,
                    isSamsRadiusAndSpeedIncreased = true,
                    homingLauncherConfig = new HomingLauncherConfig
                    {
                        isEnable = true,
                        targetCaptureDistance = 1
                    },
                    blockedCommands = new HashSet<string>
                    {
                        "home"
                    },
                    control = new ControlConfig
                    {
                        force = 20f,
                        cren = 1.5f,
                        upDown = 1.5f,
                        yaw = 1.5f,
                        drag = 0.7f,
                        autoCrenHold = true,
                    },
                    fuel = new FuelConfig
                    {
                        fuel = true,
                        periodFuel = 1,
                        fuelPerTick = 1,
                        itemShortname = "lowgradefuel",

                    },
                    itemConfig = new ItemConfig
                    {
                        shortname = "burlap.gloves.new",
                        skin = 2632956407,
                        name = "Jetpack"
                    },
                    isSpawnInLootEnabled = false,
                    crates = new List<CrateConfig>
                    {
                        new CrateConfig
                        {
                            Prefab = "assets/bundled/prefabs/radtown/crate_elite.prefab",
                            Chance = 5f
                        }
                    },
                    notificationConfig = new NotificationConfig
                    {
                        isChat = true,
                    },
                    supportedPluginsConfig = new SupportedPluginsConfig
                    {
                        zoneManager = new ZoneManagerConfig
                        {
                            enable = false,
                            blockFlags = new HashSet<string>
                            {
                                "eject",
                                "pvegod"
                            },
                            blockIDs = new HashSet<string>
                            {
                                "Example"
                            }
                        }
                    }
                };
            }
        }
        #endregion Config
    }
}

namespace Oxide.Plugins.JetPackExtensionMethods
{
    public static class ExtensionMethods
    {
        public static bool Any<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate)
        {
            using (var enumerator = source.GetEnumerator()) while (enumerator.MoveNext()) if (predicate(enumerator.Current)) return true;
            return false;
        }

        public static HashSet<TSource> Where<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate)
        {
            HashSet<TSource> result = new HashSet<TSource>();
            using (var enumerator = source.GetEnumerator()) while (enumerator.MoveNext()) if (predicate(enumerator.Current)) result.Add(enumerator.Current);
            return result;
        }

        public static TSource FirstOrDefault<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate)
        {
            using (var enumerator = source.GetEnumerator()) while (enumerator.MoveNext()) if (predicate(enumerator.Current)) return enumerator.Current;
            return default(TSource);
        }

        public static HashSet<TResult> Select<TSource, TResult>(this IEnumerable<TSource> source, Func<TSource, TResult> predicate)
        {
            HashSet<TResult> result = new HashSet<TResult>();
            using (var enumerator = source.GetEnumerator()) while (enumerator.MoveNext()) result.Add(predicate(enumerator.Current));
            return result;
        }

        public static List<TResult> Select<TSource, TResult>(this IList<TSource> source, Func<TSource, TResult> predicate)
        {
            List<TResult> result = new List<TResult>();
            for (int i = 0; i < source.Count; i++)
            {
                TSource element = source[i];
                result.Add(predicate(element));
            }
            return result;
        }

        public static HashSet<T> OfType<T>(this IEnumerable<BaseNetworkable> source)
        {
            HashSet<T> result = new HashSet<T>();
            using (var enumerator = source.GetEnumerator()) while (enumerator.MoveNext()) if (enumerator.Current is T) result.Add((T)(object)enumerator.Current);
            return result;
        }

        public static bool IsExists(this BaseNetworkable entity) => entity != null && !entity.IsDestroyed;

        public static void ClearItemsContainer(this ItemContainer container)
        {
            for (int i = container.itemList.Count - 1; i >= 0; i--)
            {
                Item item = container.itemList[i];
                item.RemoveFromContainer();
                item.Remove();
            }
        }

        public static bool IsRealPlayer(this BasePlayer player) => player != null && player.userID.IsSteamId();

        public static List<TSource> OrderBy<TSource>(this IEnumerable<TSource> source, Func<TSource, float> predicate)
        {
            List<TSource> result = source.ToList();
            for (int i = 0; i < result.Count; i++)
            {
                for (int j = 0; j < result.Count - 1; j++)
                {
                    if (predicate(result[j]) > predicate(result[j + 1]))
                    {
                        TSource z = result[j];
                        result[j] = result[j + 1];
                        result[j + 1] = z;
                    }
                }
            }
            return result;
        }

        public static List<TSource> ToList<TSource>(this IEnumerable<TSource> source)
        {
            List<TSource> result = new List<TSource>();
            using (var enumerator = source.GetEnumerator()) while (enumerator.MoveNext()) result.Add(enumerator.Current);
            return result;
        }

        public static HashSet<TSource> ToHashSet<TSource>(this IEnumerable<TSource> source)
        {
            HashSet<TSource> result = new HashSet<TSource>();
            using (var enumerator = source.GetEnumerator()) while (enumerator.MoveNext()) result.Add(enumerator.Current);
            return result;
        }

        public static TSource Max<TSource>(this IEnumerable<TSource> source, Func<TSource, float> predicate)
        {
            TSource result = source.ElementAt(0);
            float resultValue = predicate(result);
            using (var enumerator = source.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    TSource element = enumerator.Current;
                    float elementValue = predicate(element);
                    if (elementValue > resultValue)
                    {
                        result = element;
                        resultValue = elementValue;
                    }
                }
            }
            return result;
        }

        public static TSource Min<TSource>(this IEnumerable<TSource> source, Func<TSource, float> predicate)
        {
            TSource result = source.ElementAt(0);
            float resultValue = predicate(result);
            using (var enumerator = source.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    TSource element = enumerator.Current;
                    float elementValue = predicate(element);
                    if (elementValue < resultValue)
                    {
                        result = element;
                        resultValue = elementValue;
                    }
                }
            }
            return result;
        }

        public static TSource ElementAt<TSource>(this IEnumerable<TSource> source, int index)
        {
            int movements = 0;
            using (var enumerator = source.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    if (movements == index) return enumerator.Current;
                    movements++;
                }
            }
            return default(TSource);
        }

        public static TSource First<TSource>(this IList<TSource> source) => source[0];

        public static TSource Last<TSource>(this IList<TSource> source) => source[source.Count - 1];
    }
}