# VERIFIED OXIDE API - THESE METHODS EXIST AND WORK

**ONLY use methods and properties from this list. If it's not here, DON'T USE IT.**

---

## LOGGING (from RustPlugin base class)

```csharp
Puts("message");                    // Normal log output
PrintWarning("message");            // Yellow warning
PrintError("message");              // Red error
```

---

## BASEPLAYER PROPERTIES

```csharp
// Identity
player.userID                       // ulong - use for data storage keys
player.UserIDString                 // string - USE THIS FOR PERMISSIONS
player.displayName                  // string - player's name

// State Checks
player.IsConnected                  // bool - is player online
player.IsSleeping()                 // bool
player.IsDead()                     // bool  
player.IsWounded()                  // bool
player.IsSpectating()               // bool
player.isMounted                    // bool
player.IsAdmin                      // bool

// Position/Eyes
player.transform.position           // Vector3
player.eyes.position                // Vector3
player.eyes.HeadRay()               // Ray
player.eyes.HeadForward()           // Vector3

// Health
player.health                       // float
player.MaxHealth()                  // float
player.Heal(float amount)           // void
player.Hurt(float amount)           // void

// Inventory
player.inventory.GiveItem(Item item)              // bool
player.inventory.Take(null, int itemId, int amount) // int
player.inventory.GetAmount(int itemId)            // int

// Teleport
player.Teleport(Vector3 position)   // void

// Active Item  
player.GetActiveItem()              // Item or null

// Commands
player.SendConsoleCommand(string cmd, params object[] args)
```

---

## PERMISSIONS

```csharp
// Register in Init()
permission.RegisterPermission("pluginname.use", this);

// Check permission - ALWAYS use UserIDString
permission.UserHasPermission(player.UserIDString, "pluginname.use")

// Grant/Revoke
permission.GrantUserPermission(player.UserIDString, "perm", this);
permission.RevokeUserPermission(player.UserIDString, "perm", this);
```

---

## CONFIGURATION

```csharp
protected override void LoadDefaultConfig() => config = new Configuration();
protected override void SaveConfig() => Config.WriteObject(config);

// Config class with JsonProperty
private class Configuration
{
    [JsonProperty("Setting Name")]
    public bool Setting = true;
}
```

---

## DATA STORAGE

```csharp
// Save
Interface.Oxide.DataFileSystem.WriteObject(Name, storedData);

// Load  
storedData = Interface.Oxide.DataFileSystem.ReadObject<DataClass>(Name) ?? new DataClass();
```

---

## TIMERS

```csharp
// One-time timer
timer.Once(5f, () => { });

// Repeating timer
Timer myTimer = timer.Every(5f, () => { });

// Destroy timer
myTimer?.Destroy();
```

---

## CUI ELEMENTS (Oxide Custom UI)

```csharp
// Available elements:
CuiElementContainer                 // Container for UI elements
CuiPanel                            // Panel/background
CuiLabel                            // Text
CuiButton                           // Clickable button
CuiInputField                       // Text input
CuiRawImageComponent                // Images
CuiImageComponent                   // Solid colors

// Add UI
CuiHelper.AddUi(player, container);

// Destroy UI
CuiHelper.DestroyUi(player, "PANEL_NAME");
```

---

## ITEM CREATION

```csharp
// Create item by shortname
Item item = ItemManager.CreateByName("rifle.ak", 1, skinId);

// Give to player
player.GiveItem(item);
// or
if (!player.inventory.GiveItem(item))
    item.Drop(player.transform.position + Vector3.up, Vector3.up);
```

---

## ENTITY SPAWNING

```csharp
var entity = GameManager.server.CreateEntity(prefab, position, rotation);
if (entity != null)
{
    entity.OwnerID = player.userID;
    entity.Spawn();
}
```

---

## RAYCASTING

```csharp
if (Physics.Raycast(player.eyes.HeadRay(), out RaycastHit hit, 10f))
{
    BaseEntity entity = hit.GetEntity();
    Vector3 point = hit.point;
}
```

---

## PLUGIN REFERENCES

```csharp
[PluginReference] private Plugin Economics;

// Safe call
Economics?.Call<bool>("Withdraw", player.UserIDString, amount);
```
