# OXIDE API VERSION COMPATIBILITY

**API version compatibility, changelog, and migration guides for Rust Oxide plugins.**

---

## API VERSION DETECTION

### Check Rust Version at Runtime
```csharp
private void OnServerInitialized()
{
    var protocol = Rust.Protocol.network;
    var version = BuildInfo.Current.BuildNumber;
    
    Puts($"Rust Protocol: {protocol}");
    Puts($"Build: {version}");
}
```

### Conditional API Usage
```csharp
private void DoAction(BaseEntity entity)
{
    // Check if method exists before calling
    var method = entity.GetType().GetMethod("NewMethod");
    if (method != null)
    {
        method.Invoke(entity, null);
    }
    else
    {
        // Fallback for older versions
        OldMethod(entity);
    }
}
```

---

## COMMON BREAKING CHANGES

### NetworkableId Changes (2022+)
```csharp
// OLD (pre-2022):
uint netId = entity.net.ID;

// NEW (current):
NetworkableId netId = entity.net.ID;
uint netIdValue = entity.net.ID.Value;

// Safe pattern for both:
var id = entity.net.ID;
// Store as NetworkableId, use .Value when needed as uint
```

### ItemId Changes
```csharp
// OLD:
int itemId = item.info.itemid;

// NEW - Use ItemId struct:
ItemId itemId = item.info.itemid;
int itemIdValue = item.info.itemid.GetHashCode();

// Or use uid:
ulong itemUid = item.uid.Value;
```

### BuildingPrivlidge to BuildingPrivilege
```csharp
// Note the spelling change in some versions
var priv = player.GetBuildingPrivilege();  // Current
// Some old code has: GetBuildingPrivlidge (typo)
```

---

## HOOK SIGNATURE CHANGES

### OnEntityTakeDamage Evolution
```csharp
// Current signature:
object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)

// Return values:
// null - Allow damage to proceed
// true - Block damage (info was modified)
// false - On some versions, may block

// Safe modification pattern:
object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
{
    if (ShouldBlockDamage(entity))
    {
        info.damageTypes.ScaleAll(0f);
        return true;
    }
    return null;
}
```

### OnPlayerInput
```csharp
// Current signature (stable):
void OnPlayerInput(BasePlayer player, InputState input)

// InputState methods (all versions):
input.WasJustPressed(BUTTON.X)
input.WasJustReleased(BUTTON.X)
input.IsDown(BUTTON.X)
```

---

## DEPRECATED METHOD MIGRATIONS

### Logging Methods
```csharp
// These NEVER existed in Oxide (common mistake):
LogWarning("msg");   // ❌ Does not exist
LogError("msg");     // ❌ Does not exist
Debug.Log("msg");    // ❌ Unity only, not in plugins

// Use these ALWAYS:
Puts("msg");           // ✅ Standard output
PrintWarning("msg");   // ✅ Warning (yellow)
PrintError("msg");     // ✅ Error (red)
```

### Player Finding
```csharp
// OLD - still works but less efficient:
foreach (var p in BasePlayer.activePlayerList)
    if (p.displayName.Contains(name)) return p;

// BETTER - built-in methods:
BasePlayer.Find(nameOrId);           // Finds by partial name or ID
BasePlayer.FindByID(userId);         // Finds by ulong ID
BasePlayer.FindAwakeOrSleeping(id);  // Includes sleepers

// Covalence (cross-platform):
players.FindPlayer(nameOrId);
```

### Item Giving
```csharp
// All these work, pick based on need:

// Simple give (drops if inventory full):
player.GiveItem(item);

// Check if given successfully:
if (!player.inventory.GiveItem(item))
    item.Drop(player.transform.position, Vector3.up);

// Give to specific container:
player.inventory.GiveItem(item, player.inventory.containerMain);
player.inventory.GiveItem(item, player.inventory.containerBelt);
player.inventory.GiveItem(item, player.inventory.containerWear);
```

---

## COMPATIBILITY SHIMS

### Safe Entity Spawn
```csharp
// Works across versions
private BaseEntity SpawnEntity(string prefab, Vector3 position, Quaternion rotation = default)
{
    var entity = GameManager.server.CreateEntity(prefab, position, rotation);
    if (entity == null)
    {
        PrintError($"Failed to create entity: {prefab}");
        return null;
    }
    entity.Spawn();
    return entity;
}
```

### Safe Item Creation
```csharp
// Works across versions
private Item CreateItem(string shortname, int amount = 1, ulong skin = 0)
{
    var def = ItemManager.FindItemDefinition(shortname);
    if (def == null)
    {
        PrintWarning($"Unknown item: {shortname}");
        return null;
    }
    return ItemManager.Create(def, amount, skin);
}
```

### Safe Permission Check
```csharp
// Works across all Oxide versions
private bool HasPerm(BasePlayer player, string perm)
{
    if (player == null) return false;
    return permission.UserHasPermission(player.UserIDString, perm);
}

private bool HasPerm(string odUserId, string perm)
{
    return permission.UserHasPermission(userId, perm);
}
```

---

## OXIDE/UMOD VERSION NOTES

### Oxide vs uMod
```
- Oxide and uMod use the same API
- uMod is the continuation of Oxide development
- Most plugins work on both without changes
- Namespace is still "Oxide.Plugins"
```

### Extension Dependencies
```csharp
// Check for extension at load
private void Init()
{
    if (!Interface.Oxide.GetLibrary<Permission>().IsLoaded)
    {
        PrintError("Permission library not loaded!");
        return;
    }
}

// Common extensions:
// - Covalence (cross-game support)
// - MySql / SQLite (database)
// - Discord (webhooks)
```

---

## FORWARD COMPATIBILITY

### Defensive Coding
```csharp
// Always check for null
var component = entity?.GetComponent<MyComponent>();
if (component == null) return;

// Use TryGetValue
if (data.TryGetValue(key, out var value))
{
    // Use value
}

// Safe casting
if (entity is BasePlayer player)
{
    // Use player
}
```

### Feature Detection
```csharp
// Check if a prefab exists
private bool PrefabExists(string prefab)
{
    return GameManifest.pathToGuid.ContainsKey(prefab);
}

// Check if item exists
private bool ItemExists(string shortname)
{
    return ItemManager.FindItemDefinition(shortname) != null;
}
```

---

## PREFAB CHANGES TO WATCH

### Vehicle Prefabs (May Change)
```csharp
// Current paths - verify in new Rust versions
"assets/content/vehicles/minicopter/minicopter.entity.prefab"
"assets/content/vehicles/scrap heli carrier/scraptransporthelicopter.prefab"
"assets/content/vehicles/boats/rhib/rhib.prefab"
"assets/rust.ai/nextai/testridablehorse.prefab"
```

### NPC Prefabs
```csharp
// Scientist NPCs
"assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_heavy.prefab"
"assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_roam.prefab"

// Other NPCs
"assets/prefabs/npc/murderer/murderer.prefab"
"assets/prefabs/npc/scarecrow/scarecrow.prefab"
```

---

## VERSION CHECKING TEMPLATE

```csharp
private void OnServerInitialized()
{
    var rustVersion = BuildInfo.Current.BuildNumber;
    var oxideVersion = GetType().Assembly.GetName().Version;
    
    Puts($"Rust Build: {rustVersion}");
    Puts($"Plugin compiled for Oxide: {oxideVersion}");
    
    // Warn if potentially incompatible
    if (rustVersion < MINIMUM_RUST_BUILD)
    {
        PrintWarning($"This plugin requires Rust build {MINIMUM_RUST_BUILD}+");
    }
}

private const int MINIMUM_RUST_BUILD = 0; // Set if needed
```
