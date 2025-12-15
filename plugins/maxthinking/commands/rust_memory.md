# RUST PLUGIN MEMORY - READ AND UPDATE THIS FILE

This file is your persistent memory for Rust Oxide/uMod plugin development. 
**READ this file at the start of every /maxthinkingrust session.**
**UPDATE this file when you encounter new patterns, errors, or solutions.**

---

## VERIFIED WORKING PATTERNS

### 1. Permission Checking
```csharp
// ALWAYS use UserIDString, never userID.ToString()
permission.UserHasPermission(player.UserIDString, PERM_USE)
```

### 2. Null-Safe Player Operations
```csharp
if (player == null || !player.IsConnected) return;
```

### 3. Timer with Null Check
```csharp
timer.Once(2f, () =>
{
    if (player == null || !player.IsConnected) return;
    // Safe to use player
});
```

### 4. Config Loading Pattern
```csharp
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
```

### 5. Data Storage with ProtoStorage (Binary - Faster)
```csharp
// Save
ProtoStorage.Save(data, Name);

// Load
data = ProtoStorage.Load<PluginData>(Name) ?? new PluginData();

// Class must have [ProtoContract] and [ProtoMember] attributes
[ProtoContract]
private class PluginData
{
    [ProtoMember(1)]
    public Hash<ulong, PlayerData> Players { get; set; } = new();
}
```

### 6. Data Storage with JSON (Human Readable)
```csharp
// Save
Interface.Oxide.DataFileSystem.WriteObject(Name, storedData);

// Load
storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name) ?? new StoredData();
```

### 7. MonoBehaviour Component Pattern
```csharp
public class MyComponent : FacepunchBehaviour
{
    private BasePlayer Player { get; set; }
    
    private void Awake()
    {
        Player = GetComponent<BasePlayer>();
        _pluginInstance._cachedComponents[Player.userID] = this;
    }
    
    private void Update()
    {
        // Called every frame - be careful with performance!
    }
    
    public void Destroy()
    {
        _pluginInstance._cachedComponents.Remove(Player.userID);
        DestroyImmediate(this);
    }
}

// Add to player
player.gameObject.AddComponent<MyComponent>();
```

### 8. Player Input Detection
```csharp
private void OnPlayerInput(BasePlayer player, InputState input)
{
    if (input.WasJustPressed(BUTTON.FIRE_PRIMARY)) { }
    if (input.WasJustReleased(BUTTON.FIRE_PRIMARY)) { }
    if (input.WasJustPressed(BUTTON.FIRE_SECONDARY)) { }
    if (input.IsDown(BUTTON.SPRINT)) { }
}
```

### 9. Raycast from Player Eyes
```csharp
if (Physics.Raycast(player.eyes.HeadRay(), out RaycastHit hit, 10f, layerMask))
{
    BaseEntity entity = hit.GetEntity();
    Vector3 hitPoint = hit.point;
}
```

### 10. Entity Flags
```csharp
// Set flag
entity.SetFlag(BaseEntity.Flags.Reserved1, true);

// Check flag
if (entity.HasFlag(BaseEntity.Flags.Reserved1)) { }

// Common flags: On, Open, Locked, Reserved1-8, Busy, etc.
```

### 11. Layer Masks
```csharp
Layers.Mask.Deployed      // Deployables (boxes, etc)
Layers.Mask.Construction  // Building blocks
Layers.Mask.Player_Server // Players
Layers.Mask.Vehicle_Large // Vehicles
Layers.Mask.Default       // Default layer
```

### 12. Overlap Sphere (Find Entities in Radius)
```csharp
List<BaseEntity> entities = new List<BaseEntity>();
Vis.Entities(position, radius, entities, layerMask);
```

### 13. NextTick / NextFrame
```csharp
// NextTick - runs next server tick (safer for collection modification)
NextTick(() => { myList.Remove(item); });

// NextFrame - runs next frame
NextFrame(() => { UpdateUI(player); });
```

### 14. Effect Playing
```csharp
Effect.server.Run("assets/prefabs/fx/impacts/additive/fire.prefab", position);
```

### 15. Item Creation and Giving
```csharp
var item = ItemManager.CreateByName("rifle.ak", 1, skinId);
if (item != null)
{
    item.name = "Custom Name";
    player.GiveItem(item);
}
```

---

## COMMON ERRORS AND FIXES

### Error: CS0103 - 'userId' does not exist
**Cause:** Variable name mismatch
**Fix:** Check spelling - is it `userId`, `userID`, `playerId`?

### Error: CS1061 - 'BasePlayer' does not contain 'userID'
**Fix:** Use `player.userID` (lowercase u, uppercase ID)

### Error: CS0029 - Cannot convert 'ulong' to 'string'
**Fix:** Use `player.UserIDString` for permissions, not `player.userID`

### Error: NullReferenceException at runtime
**Fix:** Add null checks:
```csharp
if (player == null) return;
if (entity == null || entity.IsDestroyed) return;
```

### Error: Collection modified during enumeration
**Fix:** Use `.ToList()` when iterating and modifying:
```csharp
foreach (var player in BasePlayer.activePlayerList.ToList())
```

### Error: Timer keeps running after unload
**Fix:** Destroy timers in Unload():
```csharp
private void Unload()
{
    myTimer?.Destroy();
}
```

### Error: UI not showing / UI stuck
**Fix:** Always destroy before creating:
```csharp
CuiHelper.DestroyUi(player, UI_PANEL);
CuiHelper.AddUi(player, container);
```

### Error: Config not saving properly
**Fix:** Ensure [JsonProperty] attributes on all config fields

### Error: CS0103 - 'LogWarning' does not exist in current context
**Cause:** Using Unity/generic C# logging instead of Oxide methods
**Fix:** Use Oxide's built-in logging methods:
```csharp
// WRONG - These will cause compilation errors
LogWarning("msg");
LogError("msg");
Debug.Log("msg");

// CORRECT - Use these Oxide methods
Puts("Info message");           // Console output
PrintWarning("Warning message"); // Yellow warning  
PrintError("Error message");     // Red error
```

---

## HOOK SIGNATURES (MUST BE EXACT)

```csharp
// Player hooks
void OnPlayerConnected(BasePlayer player)
void OnPlayerDisconnected(BasePlayer player, string reason)
void OnPlayerRespawned(BasePlayer player)
void OnPlayerDeath(BasePlayer player, HitInfo info)
void OnPlayerInput(BasePlayer player, InputState input)

// Entity hooks
void OnEntitySpawned(BaseNetworkable entity)
void OnEntityKill(BaseNetworkable entity)
void OnEntityBuilt(Planner planner, GameObject go)
object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)

// Item hooks
void OnActiveItemChanged(BasePlayer player, Item oldItem, Item newItem)
void OnItemAddedToContainer(ItemContainer container, Item item)

// Server hooks
void OnServerInitialized()
void OnServerSave()
void OnNewSave(string filename)
```

---

## USEFUL PREFABS

```csharp
// Effects
"assets/bundled/prefabs/fx/impacts/additive/fire.prefab"
"assets/bundled/prefabs/fx/survey_explosion.prefab"
"assets/bundled/prefabs/fx/ore_break.prefab"
"assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
"assets/bundled/prefabs/fx/gestures/drink_vomit.prefab"
"assets/bundled/prefabs/fx/player/howl.prefab"
"assets/prefabs/misc/xmas/presents/effects/unwrap.prefab"

// Entities
"assets/prefabs/deployable/large wood storage/box.wooden.large.prefab"
"assets/prefabs/deployable/woodenbox/woodbox_deployed.prefab"
"assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_heavy.prefab"
"assets/bundled/prefabs/radtown/crate_normal.prefab"
"assets/prefabs/npc/murderer/murderer.prefab"
"assets/prefabs/npc/scarecrow/scarecrow.prefab"

// Vehicles
"assets/content/vehicles/minicopter/minicopter.entity.prefab"
"assets/content/vehicles/scrap heli carrier/scraptransporthelicopter.prefab"
"assets/content/vehicles/boats/rhib/rhib.prefab"
"assets/content/vehicles/modularcar/car_chassis_2module.entity.prefab"
```

---

## ADVANCED PATTERNS

### Coroutines (Long Operations)
```csharp
private Coroutine _coroutine;

private IEnumerator MyCoroutine()
{
    int count = 0;
    foreach (var item in largeCollection)
    {
        // Process
        if (++count % 100 == 0)
            yield return CoroutineEx.waitForEndOfFrame;
    }
}

// Start: _coroutine = ServerMgr.Instance.StartCoroutine(MyCoroutine());
// Stop in Unload: ServerMgr.Instance.StopCoroutine(_coroutine);
```

### Subscribe/Unsubscribe Hooks
```csharp
private void Init()
{
    Unsubscribe(nameof(OnEntityTakeDamage)); // Expensive hook
}

private void EnableFeature()
{
    Subscribe(nameof(OnEntityTakeDamage));
}
```

### Plugin Integration Patterns
```csharp
// Economics
Economics?.Call<bool>("Withdraw", playerId, amount);
Economics?.Call<double>("Balance", playerId);

// ServerRewards
ServerRewards?.Call<int>("CheckPoints", playerId);

// ImageLibrary
ImageLibrary?.Call<string>("GetImage", imageName);

// ZoneManager
ZoneManager?.Call<bool>("IsPlayerInZone", zoneId, player);

// Clans
Clans?.Call<string>("GetClanOf", playerId);
```

### Safe Teleport
```csharp
if (player.isMounted)
    player.GetMounted().DismountPlayer(player, true);
player.Teleport(position);
```

---

## SESSION LOG

*Add notes here during development sessions:*

- [2024-12-14] - Fixed ResourceContainer.itemList error - should be resourceSpawnList
- [2024-12-14] - For OreResourceEntity, use GetComponent<ResourceDispenser>().containedItems

---

## PLUGIN-SPECIFIC NOTES

*Add notes about specific plugins you're working on:*

