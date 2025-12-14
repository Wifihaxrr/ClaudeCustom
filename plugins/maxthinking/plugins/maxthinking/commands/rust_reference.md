# RUST OXIDE/UMOD COMPLETE API REFERENCE

**This file contains VERIFIED working patterns extracted from production plugins.**
**READ THIS FILE when you need to look up correct API usage.**

---

## CRITICAL: COMMON MISTAKES TO AVOID

### ResourceDispenser vs ResourceContainer - COMPLETELY DIFFERENT!

```csharp
// ═══════════════════════════════════════════════════════════════════
// FOR MINING NODES (OreResourceEntity) - Use ResourceDispenser
// ═══════════════════════════════════════════════════════════════════
var dispenser = ore.GetComponent<ResourceDispenser>();
foreach (var item in dispenser.containedItems)  // containedItems is CORRECT!
{
    if (item.amount <= 0) continue;
    // NOTE: Use item.itemDef.shortname, NOT item.info.shortname
    var giveItem = ItemManager.CreateByName(item.itemDef.shortname, (int)item.amount);
    player.GiveItem(giveItem);
}

// ═══════════════════════════════════════════════════════════════════
// ResourceContainer - LOOTABLE CONTAINERS (barrels, etc)
// This is a DIFFERENT class! Used only in hooks!
// ═══════════════════════════════════════════════════════════════════
// Hook: object CanLootEntity(ResourceContainer container, BasePlayer player)
// ResourceContainer does NOT have: itemList, resourceSpawnList, containedItems

// ═══════════════════════════════════════════════════════════════════
// FOR STORAGE CONTAINERS (boxes, chests) - Use inventory.itemList
// ═══════════════════════════════════════════════════════════════════
foreach (var item in storageContainer.inventory.itemList)
{
    string shortname = item.info.shortname;  // NOTE: item.info for Item class
    int amount = item.amount;
}
```

### CLASS REFERENCE TABLE:
| Class | Purpose | Get Items Via |
|-------|---------|---------------|
| ResourceDispenser | Mining nodes | `dispenser.containedItems` (ItemAmount) |
| ResourceContainer | Lootable barrels | Hook only - no direct item access |
| StorageContainer | Boxes/chests | `container.inventory.itemList` (Item) |
| ItemContainer | Generic inventory | `container.itemList` (Item) |

### ItemAmount vs Item - DIFFERENT CLASSES!
```csharp
// ItemAmount (from ResourceDispenser.containedItems)
item.itemDef.shortname  // Use itemDef
item.amount             // float

// Item (from inventory.itemList)
item.info.shortname     // Use info
item.amount             // int
```

### Permission Checking
```csharp
// WRONG - userID is ulong, not string
permission.UserHasPermission(player.userID, PERM);
permission.UserHasPermission(player.userID.ToString(), PERM);

// CORRECT - Use UserIDString
permission.UserHasPermission(player.UserIDString, PERM);
```

### Player ID Properties
```csharp
player.userID        // ulong - Use for dictionaries, data storage
player.UserIDString  // string - Use for permissions, lang
player.displayName   // string - Player's display name
player.net.ID        // NetworkableId - Network entity ID
```

---

## ENTITY TYPES AND THEIR PROPERTIES

### OreResourceEntity (Mining Nodes)
```csharp
// Get resource dispenser from ore node
var ore = entity as OreResourceEntity;
var dispenser = ore.GetComponent<ResourceDispenser>();

// Access contained resources
foreach (var item in dispenser.containedItems)
{
    string shortname = item.itemDef.shortname;
    float amount = item.amount;
}

// Damage/destroy ore
ore.Kill();
ore.Hurt(damage);
```

### ResourceDispenser
```csharp
// Gather types
ResourceDispenser.GatherType.Tree
ResourceDispenser.GatherType.Ore
ResourceDispenser.GatherType.Flesh

// Properties
dispenser.containedItems  // List of resources
dispenser.gatherType      // Type of dispenser
```

### BaseOven (Furnaces, Campfires, etc)
```csharp
// Check if on
if (oven.IsOn()) { }

// Start/stop cooking
oven.StartCooking();
oven.StopCooking();

// Access inventory
oven.inventory.itemList
oven.inventory.temperature = oven.cookingTemperature;

// Invoke cooking
oven.InvokeRepeating(oven.Cook, 0.5f, 0.5f);
oven.CancelInvoke(oven.Cook);
```

### Door
```csharp
// Check state
door.IsOpen()
door.IsLocked()

// Toggle
door.SetFlag(BaseEntity.Flags.Open, true/false);
door.SendNetworkUpdateImmediate();

// Get door from raycast
if (Physics.Raycast(player.eyes.HeadRay(), out RaycastHit hit, 10f, Layers.Mask.Construction))
{
    var door = hit.GetEntity() as Door;
}
```

### BoxStorage / StorageContainer
```csharp
// Access inventory
box.inventory.itemList
box.inventory.capacity

// Check ownership
box.OwnerID  // ulong

// Set skin
box.skinID = skinId;
```

### BuildingBlock
```csharp
// Get grade
BuildingGrade.Enum grade = block.grade;

// Upgrade
block.SetGrade(BuildingGrade.Enum.TopTier);
block.SetHealthToMax();
block.SendNetworkUpdate();
block.UpdateSkin();

// Building grades
BuildingGrade.Enum.Twigs
BuildingGrade.Enum.Wood
BuildingGrade.Enum.Stone
BuildingGrade.Enum.Metal
BuildingGrade.Enum.TopTier
```

### BasePlayer
```csharp
// Identity
player.userID           // ulong
player.UserIDString     // string
player.displayName      // string

// State checks
player.IsConnected
player.IsSleeping()
player.IsWounded()
player.IsDead()
player.IsSpectating()
player.isMounted
player.IsAdmin

// Position/Eyes
player.transform.position
player.eyes.position
player.eyes.HeadRay()
player.eyes.HeadForward()

// Inventory
player.inventory.GiveItem(item)
player.inventory.Take(null, itemId, amount)
player.inventory.GetAmount(itemId)
player.inventory.FindItemByItemName(shortname)

// Health/Metabolism
player.SetHealth(amount)
player.Heal(amount)
player.metabolism.calories.value
player.metabolism.hydration.value
player.metabolism.bleeding.value
player.metabolism.SendChangesToClient()

// Teleport
player.Teleport(position)
player.SendNetworkUpdateImmediate()

// Commands
player.SendConsoleCommand("command", args)

// Active item
player.GetActiveItem()

// Building privilege
player.GetBuildingPrivilege()
player.IsBuildingAuthed()
player.CanBuild()

// Mounting
player.GetMounted()
player.GetMounted().DismountPlayer(player, true)
```

---

## HOOKS REFERENCE (EXACT SIGNATURES)

### Player Lifecycle
```csharp
void OnPlayerConnected(BasePlayer player)
void OnPlayerDisconnected(BasePlayer player, string reason)
void OnPlayerSleep(BasePlayer player)
void OnPlayerSleepEnded(BasePlayer player)
void OnPlayerRespawned(BasePlayer player)
void OnPlayerDeath(BasePlayer player, HitInfo info)
void OnPlayerWound(BasePlayer player, HitInfo info)
void OnPlayerRecover(BasePlayer player)
```

### Player Input
```csharp
void OnPlayerInput(BasePlayer player, InputState input)

// InputState methods
input.WasJustPressed(BUTTON.FIRE_PRIMARY)
input.WasJustReleased(BUTTON.FIRE_PRIMARY)
input.IsDown(BUTTON.SPRINT)

// Button types
BUTTON.FIRE_PRIMARY    // Left click
BUTTON.FIRE_SECONDARY  // Right click
BUTTON.FIRE_THIRD      // Middle click
BUTTON.RELOAD
BUTTON.JUMP
BUTTON.DUCK
BUTTON.SPRINT
BUTTON.USE
BUTTON.SLOT1 - BUTTON.SLOT8
```

### Entity Lifecycle
```csharp
void OnEntitySpawned(BaseNetworkable entity)
void OnEntityKill(BaseNetworkable entity)
void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
void OnEntityBuilt(Planner planner, GameObject go)
```

### Items
```csharp
void OnActiveItemChanged(BasePlayer player, Item oldItem, Item newItem)
void OnItemAddedToContainer(ItemContainer container, Item item)
void OnItemRemovedFromContainer(ItemContainer container, Item item)
object OnItemPickup(Item item, BasePlayer player)
void OnItemDropped(Item item, BaseEntity entity)
```

### Doors
```csharp
void OnDoorOpened(Door door, BasePlayer player)
void OnDoorClosed(Door door, BasePlayer player)
```

### Ovens/Furnaces
```csharp
void OnOvenToggle(BaseOven oven, BasePlayer player)
object OnFuelConsume(BaseOven oven, Item fuel, ItemModBurnable burnable)
```

### Gathering
```csharp
void OnDispenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item)
void OnDispenserBonus(ResourceDispenser dispenser, BaseEntity entity, Item item)
void OnCollectiblePickup(CollectibleEntity collectible, BasePlayer player)
object OnGrowableGather(GrowableEntity plant, Item item, BasePlayer player)
```

### Looting
```csharp
void OnLootEntity(BasePlayer player, BaseEntity entity)
void OnLootEntityEnd(BasePlayer player, BaseCombatEntity entity)
object CanLootEntity(BasePlayer player, StorageContainer container)
object CanLootEntity(BasePlayer player, LootableCorpse corpse)
```

### Building
```csharp
object CanBuild(Planner planner, Construction prefab, Construction.Target target)
void OnStructureUpgrade(BuildingBlock block, BasePlayer player, BuildingGrade.Enum grade)
object CanChangeGrade(BasePlayer player, BuildingBlock block, BuildingGrade.Enum grade)
void OnHammerHit(BasePlayer player, HitInfo info)
```

### Server
```csharp
void OnServerInitialized()
void OnServerSave()
void OnNewSave(string filename)
void OnPluginLoaded(Plugin plugin)
void OnPluginUnloaded(Plugin plugin)
```

---

## LAYER MASKS

```csharp
Layers.Mask.Deployed        // Deployables (boxes, etc)
Layers.Mask.Construction    // Building blocks
Layers.Mask.Player_Server   // Players
Layers.Mask.Vehicle_Large   // Vehicles
Layers.Mask.Default         // Default layer
Layers.Mask.Terrain         // Ground/terrain
Layers.Mask.World           // World objects
Layers.Mask.Water           // Water
Layers.Mask.AI              // AI entities
```

---

## ENTITY FLAGS

```csharp
BaseEntity.Flags.On
BaseEntity.Flags.Open
BaseEntity.Flags.Locked
BaseEntity.Flags.Busy
BaseEntity.Flags.Reserved1  // Custom use
BaseEntity.Flags.Reserved2  // Custom use
BaseEntity.Flags.Reserved3  // Custom use
BaseEntity.Flags.Reserved4  // Custom use
BaseEntity.Flags.Reserved5  // Custom use
BaseEntity.Flags.Reserved6  // Custom use
BaseEntity.Flags.Reserved7  // Custom use
BaseEntity.Flags.Reserved8  // Custom use

// Set flag
entity.SetFlag(BaseEntity.Flags.On, true);

// Check flag
if (entity.HasFlag(BaseEntity.Flags.On)) { }
```

---

## ITEM CREATION

```csharp
// Create by shortname
var item = ItemManager.CreateByName("rifle.ak", 1, skinId);

// Create by item ID
var item = ItemManager.CreateByItemID(itemId, amount);

// Create by definition
var itemDef = ItemManager.FindItemDefinition("rifle.ak");
var item = ItemManager.Create(itemDef, amount, skinId);

// Item properties
item.name = "Custom Name";
item.text = "Description";
item.skin = skinId;
item.condition = 100f;
item.amount = 10;

// Give to player
player.GiveItem(item);
// or
if (!player.inventory.GiveItem(item))
    item.Drop(player.transform.position + Vector3.up, Vector3.up);

// Move to container
item.MoveToContainer(container);

// Use/consume
item.UseItem(amount);

// Remove
item.Remove();
```

---

## ENTITY SPAWNING

```csharp
// Basic spawn
var entity = GameManager.server.CreateEntity(prefab, position, rotation);
if (entity != null)
{
    entity.Spawn();
}

// With owner
entity.OwnerID = player.userID;

// With skin
if (entity is BaseEntity baseEntity)
    baseEntity.skinID = skinId;

// With parent
entity.SetParent(parentEntity, true);

// Network update
entity.SendNetworkUpdateImmediate();
```

---

## EFFECTS

```csharp
// Play at position
Effect.server.Run("assets/bundled/prefabs/fx/survey_explosion.prefab", position);

// Play for specific player
var effect = new Effect("prefab", position, Vector3.up);
EffectNetwork.Send(effect, player.net.connection);

// Common effect prefabs
"assets/bundled/prefabs/fx/impacts/additive/fire.prefab"
"assets/bundled/prefabs/fx/survey_explosion.prefab"
"assets/bundled/prefabs/fx/ore_break.prefab"
"assets/bundled/prefabs/fx/build/promote_wood.prefab"
"assets/bundled/prefabs/fx/build/promote_stone.prefab"
"assets/bundled/prefabs/fx/build/promote_metal.prefab"
"assets/bundled/prefabs/fx/build/promote_toptier.prefab"
```

---

## RAYCASTING

```csharp
// From player eyes
if (Physics.Raycast(player.eyes.HeadRay(), out RaycastHit hit, distance, layerMask))
{
    BaseEntity entity = hit.GetEntity();
    Vector3 hitPoint = hit.point;
    Vector3 hitNormal = hit.normal;
}

// From position in direction
if (Physics.Raycast(position, direction, out RaycastHit hit, distance))
{
    // ...
}

// Sphere overlap
var entities = new List<BaseEntity>();
Vis.Entities(position, radius, entities, layerMask);
```

---

## TIMERS

```csharp
// Once
timer.Once(seconds, () => { });

// Repeating
Timer myTimer = timer.Every(seconds, () => { });

// With null check in callback
timer.Once(2f, () =>
{
    if (player == null || !player.IsConnected) return;
    // Safe to use player
});

// Destroy timer
myTimer?.Destroy();

// In Unload - ALWAYS destroy timers
private void Unload()
{
    myTimer?.Destroy();
    foreach (var t in playerTimers.Values)
        t?.Destroy();
}
```

---

## DATA STORAGE

### JSON (Human Readable)
```csharp
// Save
Interface.Oxide.DataFileSystem.WriteObject(Name, data);

// Load
data = Interface.Oxide.DataFileSystem.ReadObject<DataClass>(Name) ?? new DataClass();
```

### ProtoStorage (Binary - Faster)
```csharp
// Save
ProtoStorage.Save(data, Name);

// Load
data = ProtoStorage.Load<DataClass>(Name) ?? new DataClass();

// Class must have attributes
[ProtoContract]
private class DataClass
{
    [ProtoMember(1)]
    public Dictionary<ulong, PlayerData> Players { get; set; } = new();
}
```

---

## MONOBEHAVIOUR COMPONENTS

```csharp
public class MyComponent : FacepunchBehaviour
{
    private BasePlayer Player { get; set; }
    
    private void Awake()
    {
        Player = GetComponent<BasePlayer>();
    }
    
    private void Update()
    {
        // Called every frame - use sparingly!
    }
    
    private void OnDestroy()
    {
        // Cleanup
    }
    
    public void Destroy()
    {
        DestroyImmediate(this);
    }
}

// Add to player
player.gameObject.AddComponent<MyComponent>();

// Get existing
var component = player.GetComponent<MyComponent>();

// Get or add
var component = player.GetOrAddComponent<MyComponent>();
```

---

## COROUTINES

```csharp
private Coroutine _coroutine;

private IEnumerator MyCoroutine()
{
    int count = 0;
    foreach (var item in largeCollection)
    {
        // Process item
        
        // Yield every 100 to prevent lag
        if (++count % 100 == 0)
            yield return CoroutineEx.waitForEndOfFrame;
    }
}

// Start
_coroutine = ServerMgr.Instance.StartCoroutine(MyCoroutine());

// Stop
if (_coroutine != null)
    ServerMgr.Instance.StopCoroutine(_coroutine);
```

---

## SUBSCRIBE/UNSUBSCRIBE HOOKS

```csharp
private void Init()
{
    // Unsubscribe from expensive hooks until needed
    Unsubscribe(nameof(OnEntityTakeDamage));
    Unsubscribe(nameof(OnPlayerInput));
}

private void EnableFeature()
{
    Subscribe(nameof(OnEntityTakeDamage));
}

private void DisableFeature()
{
    Unsubscribe(nameof(OnEntityTakeDamage));
}
```

---

## TOD_SKY (Time of Day)

```csharp
// Get current time
float currentHour = TOD_Sky.Instance.Cycle.Hour;

// Check day/night
bool isNight = TOD_Sky.Instance.IsNight;
bool isDay = TOD_Sky.Instance.IsDay;

// Subscribe to events
TOD_Sky.Instance.Components.Time.OnSunrise += OnSunrise;
TOD_Sky.Instance.Components.Time.OnSunset += OnSunset;

// Unsubscribe in Unload
private void Unload()
{
    if (TOD_Sky.Instance != null)
    {
        TOD_Sky.Instance.Components.Time.OnSunrise -= OnSunrise;
        TOD_Sky.Instance.Components.Time.OnSunset -= OnSunset;
    }
}
```



---

## AUTOTURRET

```csharp
// Properties
turret.sightRange       // Detection range
turret.bulletSpeed      // Projectile speed
turret.aimCone          // Accuracy (lower = more accurate)
turret.OwnerID          // Owner's Steam ID

// State checks
turret.IsOnline()       // Is turret active
turret.IsPowered()      // Has power
turret.IsOn()           // Is turned on

// Methods
turret.Reload();
turret.GetAttachedWeapon();
turret.GetTotalAmmo();
turret.SendNetworkUpdateImmediate();

// Get weapon magazine
var weapon = turret.GetAttachedWeapon();
if (weapon != null)
{
    weapon.primaryMagazine.contents = 30;
    weapon.primaryMagazine.capacity = 30;
    weapon.primaryMagazine.ammoType = ItemManager.FindItemDefinition("ammo.rifle");
}
```

---

## FLAMETURRET

```csharp
// Properties
turret.arc              // Fire arc in degrees
turret.triggeredDuration // How long it fires
turret.flameRange       // Range of flames
turret.flameRadius      // Radius of damage
turret.fuelPerSec       // Fuel consumption rate

// Methods
turret.HasFuel()
turret.SendNetworkUpdateImmediate()
```

---

## PLUGIN REFERENCES (Calling Other Plugins)

```csharp
[PluginReference] private Plugin Economics;
[PluginReference] private Plugin ServerRewards;
[PluginReference] private Plugin Clans;
[PluginReference] private Plugin Friends;
[PluginReference] private Plugin ZoneManager;

// Safe call pattern
private double GetBalance(string playerId)
{
    if (Economics == null) return 0;
    return Economics.Call<double>("Balance", playerId);
}

// With null-conditional
var balance = Economics?.Call<double>("Balance", playerId) ?? 0;

// Common Economics API
Economics.Call<double>("Balance", playerId);
Economics.Call<bool>("Deposit", playerId, amount);
Economics.Call<bool>("Withdraw", playerId, amount);
Economics.Call<bool>("Transfer", fromId, toId, amount);

// Common ServerRewards API
ServerRewards.Call<int>("CheckPoints", playerId);
ServerRewards.Call<bool>("AddPoints", playerId, amount);
ServerRewards.Call<bool>("TakePoints", playerId, amount);

// Common Clans API
Clans.Call<string>("GetClanOf", playerId);
Clans.Call<bool>("IsClanMember", playerId, clanTag);

// Common Friends API
Friends.Call<bool>("AreFriends", playerId1, playerId2);
Friends.Call<bool>("IsFriend", playerId, friendId);
```

---

## COVALENCEPLUGIN (Cross-Platform)

```csharp
// Inherit from CovalencePlugin instead of RustPlugin
public class MyPlugin : CovalencePlugin
{
    // Use IPlayer instead of BasePlayer for commands
    private void MyCommand(IPlayer player, string command, string[] args)
    {
        // IPlayer properties
        player.Id           // Steam ID string
        player.Name         // Display name
        player.IsConnected
        player.IsAdmin
        player.IsServer
        
        // Reply to player
        player.Reply("Message");
        
        // Get BasePlayer from IPlayer
        var basePlayer = player.Object as BasePlayer;
        
        // Check permission
        if (!player.HasPermission("myplugin.use"))
            return;
    }
    
    // Find players
    IPlayer target = players.FindPlayer(nameOrId);
    IEnumerable<IPlayer> found = players.FindPlayers(partialName);
    IEnumerable<IPlayer> connected = players.Connected;
}
```

---

## CONSOLE COMMANDS

```csharp
// Register console command
cmd.AddConsoleCommand("mycommand", this, nameof(ConsoleCommandHandler));

// Handler
private void ConsoleCommandHandler(ConsoleSystem.Arg arg)
{
    // Get player (null if from server console)
    var player = arg.Player();
    
    // Get arguments
    string[] args = arg.Args;
    
    // Check permission
    if (arg.Connection != null && arg.Connection.authLevel < 1)
    {
        arg.ReplyWith("No permission");
        return;
    }
    
    // Reply
    arg.ReplyWith("Success!");
}

// Send console command to player
player.SendConsoleCommand("chat.say", "Hello!");
player.SendConsoleCommand("inventory.give", "rifle.ak", "1");
```

---

## CHAT COMMANDS

```csharp
// In Init()
cmd.AddChatCommand("mycommand", this, nameof(ChatCommandHandler));

// Handler
private void ChatCommandHandler(BasePlayer player, string command, string[] args)
{
    if (args.Length == 0)
    {
        SendReply(player, "Usage: /mycommand <arg>");
        return;
    }
    
    // Process command
}

// Multiple commands
private void Init()
{
    foreach (var cmd in new[] { "cmd1", "cmd2", "cmd3" })
        cmd.AddChatCommand(cmd, this, nameof(MultiCommandHandler));
}
```

---

## LOCALIZATION

```csharp
protected override void LoadDefaultMessages()
{
    lang.RegisterMessages(new Dictionary<string, string>
    {
        ["NoPermission"] = "You don't have permission!",
        ["Success"] = "Operation completed successfully!",
        ["Error"] = "An error occurred: {0}",
        ["Balance"] = "Your balance: {0:C}"
    }, this, "en");
    
    // Additional languages
    lang.RegisterMessages(new Dictionary<string, string>
    {
        ["NoPermission"] = "¡No tienes permiso!",
        ["Success"] = "¡Operación completada!"
    }, this, "es");
}

// Get message
private string GetMessage(string key, string playerId = null, params object[] args)
{
    return string.Format(lang.GetMessage(key, this, playerId), args);
}

// Usage
SendReply(player, GetMessage("Balance", player.UserIDString, 1000));
```

---

## COMMON PREFABS

```csharp
// Storage
"assets/prefabs/deployable/large wood storage/box.wooden.large.prefab"
"assets/prefabs/deployable/woodenbox/woodbox_deployed.prefab"
"assets/prefabs/deployable/fridge/fridge.deployed.prefab"

// Furnaces
"assets/prefabs/deployable/furnace/furnace.prefab"
"assets/prefabs/deployable/furnace.large/furnace.large.prefab"
"assets/prefabs/deployable/campfire/campfire.prefab"

// Turrets
"assets/prefabs/npc/autoturret/autoturret_deployed.prefab"
"assets/prefabs/npc/flame turret/flameturret.deployed.prefab"
"assets/prefabs/npc/sam_site_turret/sam_site_turret_deployed.prefab"

// Vehicles
"assets/content/vehicles/minicopter/minicopter.entity.prefab"
"assets/content/vehicles/scrap heli carrier/scraptransporthelicopter.prefab"
"assets/content/vehicles/boats/rhib/rhib.prefab"
"assets/content/vehicles/boats/rowboat/rowboat.prefab"

// NPCs
"assets/prefabs/npc/scientist/scientist.prefab"
"assets/prefabs/npc/murderer/murderer.prefab"
"assets/prefabs/npc/scarecrow/scarecrow.prefab"

// Loot
"assets/bundled/prefabs/radtown/crate_normal.prefab"
"assets/bundled/prefabs/radtown/crate_elite.prefab"
"assets/bundled/prefabs/radtown/loot_barrel_1.prefab"
"assets/bundled/prefabs/radtown/loot_barrel_2.prefab"
```

---

## NETWORK IDS

```csharp
// Entity network ID
uint netId = entity.net.ID.Value;

// Find entity by network ID
BaseNetworkable entity = BaseNetworkable.serverEntities.Find(new NetworkableId(netId));

// Store in data
data.EntityId = entity.net.ID.Value;

// Retrieve later
var entity = BaseNetworkable.serverEntities.Find(new NetworkableId(data.EntityId));
```

---

## POOL USAGE (Memory Optimization)

```csharp
// Get list from pool
List<BasePlayer> players = Pool.Get<List<BasePlayer>>();

// Use the list
Vis.Entities(position, radius, players);
foreach (var player in players)
{
    // Process
}

// Return to pool (IMPORTANT!)
Pool.FreeUnmanaged(ref players);

// Alternative pattern
using (var players = Pool.Get<List<BasePlayer>>())
{
    // Use players
} // Automatically returned to pool
```

---

## BUILDING SYSTEM

```csharp
// Get building from block
Building building = block.GetBuilding();

// Get all blocks in building
foreach (var b in building.buildingBlocks)
{
    // Process block
}

// Get building privilege
BuildingPrivlidge tc = building.GetDominatingBuildingPrivilege();

// Check if player is authed
bool isAuthed = tc.IsAuthed(player);

// Get all authed players
foreach (var auth in tc.authorizedPlayers)
{
    ulong playerId = auth.userid;
    string playerName = auth.username;
}

// Building grades cost
List<ItemAmount> cost = block.blockDefinition.grades[(int)grade].CostToBuild();
foreach (var item in cost)
{
    int itemId = item.itemid;
    float amount = item.amount;
}
```


---

## COMPLETE HOOK SIGNATURES (EXACT - DO NOT GUESS!)

### Player Hooks
```csharp
void OnPlayerConnected(BasePlayer player)
void OnPlayerDisconnected(BasePlayer player, string reason)
void OnPlayerSleep(BasePlayer player)
void OnPlayerSleepEnded(BasePlayer player)
void OnPlayerRespawned(BasePlayer player)
void OnPlayerDeath(BasePlayer player, HitInfo info)
void OnPlayerWound(BasePlayer player, HitInfo info)
void OnPlayerRecover(BasePlayer player)
object OnPlayerChat(BasePlayer player, string message, ConVar.Chat.ChatChannel channel)
object OnPlayerCommand(BasePlayer player, string command, string[] args)
void OnPlayerInput(BasePlayer player, InputState input)
```

### Entity Hooks
```csharp
void OnEntitySpawned(BaseNetworkable entity)
void OnEntityKill(BaseNetworkable entity)
void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
void OnEntityBuilt(Planner planner, GameObject go)
```

### Building Hooks
```csharp
object CanBuild(Planner planner, Construction prefab, Construction.Target target)
void OnStructureUpgrade(BuildingBlock block, BasePlayer player, BuildingGrade.Enum grade)
object CanChangeGrade(BasePlayer player, BuildingBlock block, BuildingGrade.Enum grade)
void OnStructureRepair(BaseCombatEntity entity, BasePlayer player)
void OnStructureRotate(BuildingBlock block, BasePlayer player)
void OnHammerHit(BasePlayer player, HitInfo info)
```

### Item Hooks
```csharp
void OnItemAddedToContainer(ItemContainer container, Item item)
void OnItemRemovedFromContainer(ItemContainer container, Item item)
object CanMoveItem(Item item, PlayerInventory inventory, ItemContainerId targetContainerId, int targetSlot, int amount)
void OnItemDropped(Item item, BaseEntity entity)
object OnItemPickup(Item item, BasePlayer player)
void OnItemCraftFinished(ItemCraftTask task, Item item, ItemCrafter crafter)
object CanCraft(ItemCrafter crafter, ItemBlueprint bp, int amount)
```

### Loot Hooks
```csharp
void OnLootEntity(BasePlayer player, BaseEntity entity)
void OnLootEntityEnd(BasePlayer player, BaseCombatEntity entity)
object CanLootEntity(BasePlayer player, StorageContainer container)
object CanLootEntity(BasePlayer player, LootableCorpse corpse)
object CanLootEntity(BasePlayer player, DroppedItemContainer container)
object CanLootEntity(ResourceContainer container, BasePlayer player)
void OnLootSpawn(LootContainer container)
```

### Gather Hooks
```csharp
object OnDispenserGather(ResourceDispenser dispenser, BasePlayer player, Item item)
object OnDispenserBonus(ResourceDispenser dispenser, BasePlayer player, Item item)
object OnCollectiblePickup(CollectibleEntity collectible, BasePlayer player)
void OnCollectiblePickup(CollectibleEntity collectible, BasePlayer player, Item item)
object OnGrowableGather(GrowableEntity plant, Item item, BasePlayer player)
```

### Vehicle Hooks
```csharp
void OnEntityMounted(BaseMountable mountable, BasePlayer player)
void OnEntityDismounted(BaseMountable mountable, BasePlayer player)
object CanMountEntity(BasePlayer player, BaseMountable mountable)
object CanDismountEntity(BasePlayer player, BaseMountable mountable)
```

### Combat Hooks
```csharp
object OnPlayerAttack(BasePlayer attacker, HitInfo info)
void OnWeaponFired(BaseProjectile projectile, BasePlayer player, ItemModProjectile mod, ProtoBuf.ProjectileShoot projectiles)
object CanBeWounded(BasePlayer player, HitInfo info)
object OnNpcTarget(BaseNpc npc, BaseEntity target)
object OnTurretTarget(AutoTurret turret, BaseCombatEntity target)
```

### Server Hooks
```csharp
void OnServerInitialized()
void OnServerSave()
void OnNewSave(string filename)
void OnPluginLoaded(Plugin plugin)
void OnPluginUnloaded(Plugin plugin)
```

---

## MORE COMMON PREFABS

### Deployables
```csharp
// Sleeping bags
"assets/prefabs/deployable/sleeping bag/sleepingbag_leather_deployed.prefab"
"assets/prefabs/deployable/bed/bed_deployed.prefab"

// Tool cupboard
"assets/prefabs/deployable/tool cupboard/cupboard.tool.deployed.prefab"

// Workbenches
"assets/prefabs/deployable/workbench/workbench1.deployed.prefab"
"assets/prefabs/deployable/workbench/workbench2.deployed.prefab"
"assets/prefabs/deployable/workbench/workbench3.deployed.prefab"

// Repair bench
"assets/prefabs/deployable/repair bench/repairbench_deployed.prefab"

// Research table
"assets/prefabs/deployable/research table/researchtable_deployed.prefab"

// Mixing table
"assets/prefabs/deployable/mixingtable/mixingtable.deployed.prefab"

// Vending machine
"assets/prefabs/deployable/vendingmachine/vendingmachine.deployed.prefab"

// Recycler
"assets/bundled/prefabs/static/recycler_static.prefab"
```

### Effects
```csharp
// Explosions
"assets/bundled/prefabs/fx/explosions/explosion_01.prefab"
"assets/bundled/prefabs/fx/survey_explosion.prefab"

// Fire
"assets/bundled/prefabs/fx/impacts/additive/fire.prefab"
"assets/bundled/prefabs/fx/fire/fire_v3.prefab"

// Sparks
"assets/bundled/prefabs/fx/impacts/spark_metal.prefab"
"assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"

// Mining
"assets/bundled/prefabs/fx/ore_break.prefab"
"assets/bundled/prefabs/fx/impacts/additive/explosion.prefab"

// UI sounds
"assets/prefabs/locks/keypad/effects/lock.code.lock.prefab"
"assets/prefabs/locks/keypad/effects/lock.code.unlock.prefab"
"assets/prefabs/locks/keypad/effects/lock.code.denied.prefab"
"assets/bundled/prefabs/fx/notice/item.select.fx.prefab"
"assets/bundled/prefabs/fx/notice/loot.drag.grab.fx.prefab"
```

### Vehicles (Complete List)
```csharp
// Helicopters
"assets/content/vehicles/minicopter/minicopter.entity.prefab"
"assets/content/vehicles/scrap heli carrier/scraptransporthelicopter.prefab"
"assets/content/vehicles/attackhelicopter/attackhelicopter.entity.prefab"

// Boats
"assets/content/vehicles/boats/rowboat/rowboat.prefab"
"assets/content/vehicles/boats/rhib/rhib.prefab"
"assets/content/vehicles/boats/kayak/kayak.prefab"
"assets/content/vehicles/boats/tugboat/tugboat.prefab"

// Ground
"assets/content/vehicles/sedan_a/sedantest.entity.prefab"
"assets/content/vehicles/snowmobiles/snowmobile.prefab"
"assets/content/vehicles/snowmobiles/tomahasnowmobile.prefab"
"assets/content/vehicles/bikes/pedalbike.prefab"
"assets/content/vehicles/bikes/motorbike.prefab"

// Submarines
"assets/content/vehicles/submarine/submarinesolo.entity.prefab"
"assets/content/vehicles/submarine/submarineduo.entity.prefab"

// Horse
"assets/rust.ai/agents/horse/ridablehorse.prefab"

// Hot air balloon
"assets/prefabs/deployable/hot air balloon/hotairballoon.prefab"

// Trains
"assets/content/vehicles/trains/workcart/workcart.entity.prefab"
"assets/content/vehicles/trains/locomotive/locomotive.entity.prefab"
```

---

## ITEM IDS (Common Items)

```csharp
// Resources
-932201673   // scrap
-151838493   // wood
-2099697608  // stone
374890416    // metal.fragments
317398316    // metal.ore
-1581843485  // sulfur
-1157596551  // sulfur.ore
-1779180711  // cloth
-946369541   // lowgradefuel
69511070     // crude.oil
-1779183908  // leather
-2027793839  // fat.animal

// Weapons
1545779598   // rifle.ak
-1812555177  // rifle.lr300
-904863145   // rifle.bolt
-1335497659  // smg.mp5
-1367281941  // smg.thompson
1318558775   // pistol.m92
-852563019   // pistol.python
-778367295   // shotgun.pump
-1009492144  // shotgun.spas12

// Tools
-1302129395  // pickaxe
-1440143841  // hatchet
200773292    // hammer
-1370759135  // building.planner

// Medical
254522515    // syringe.medical
-1432674913  // bandage
-789202811   // largemedkit
```
