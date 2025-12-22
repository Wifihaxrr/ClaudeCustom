# OXIDE LINTING RULES & VALIDATION

**Oxide-specific linting rules and code validation checklist.**

---

## METHOD EXISTENCE RULES

### ✅ WHITELIST - Methods That EXIST in Oxide

```csharp
// Logging (ONLY THESE EXIST)
Puts("message");
PrintWarning("message");
PrintError("message");

// Permissions
permission.RegisterPermission(perm, this);
permission.UserHasPermission(UserIDString, perm);  // Must use UserIDString!
permission.GroupHasPermission(group, perm);
permission.GrantUserPermission(UserIDString, perm, this);
permission.RevokeUserPermission(UserIDString, perm);
permission.GetUserPermissions(UserIDString);

// Config
Config.ReadObject<T>();
Config.WriteObject(obj, true);

// Data
Interface.Oxide.DataFileSystem.ReadObject<T>(filename);
Interface.Oxide.DataFileSystem.WriteObject(filename, obj);

// Timers
timer.Once(seconds, callback);
timer.Every(seconds, callback);
timer.In(seconds, callback);

// Lang
lang.RegisterMessages(messages, this);
lang.GetMessage(key, this, UserIDString);

// Commands
cmd.AddChatCommand(command, this, methodName);
cmd.AddConsoleCommand(command, this, methodName);
```

### ❌ BLACKLIST - Methods That DO NOT EXIST

```csharp
// THESE WILL CAUSE COMPILE ERRORS:
LogWarning("msg");           // ❌ Use PrintWarning()
LogError("msg");             // ❌ Use PrintError()
Log("msg");                  // ❌ Use Puts()
Debug.Log("msg");            // ❌ Use Puts()
Debug.LogWarning("msg");     // ❌ Use PrintWarning()
Debug.LogError("msg");       // ❌ Use PrintError()
Console.WriteLine("msg");    // ❌ Use Puts()

// WRONG PERMISSION USAGE:
permission.UserHasPermission(player.userID, perm);           // ❌ userID is ulong
permission.UserHasPermission(player.userID.ToString(), perm);// ❌ Use UserIDString

// WRONG PLAYER PROPERTIES:
player.UserId;               // ❌ Use player.userID (lowercase u)
player.UserID;               // ❌ Use player.userID
player.SteamId;              // ❌ Use player.userID or UserIDString
```

---

## PRE-DELIVERY VALIDATION CHECKLIST

### 1. Structure Check
```
□ Has [Info("Name", "Author", "Version")] attribute
□ Has [Description("...")] attribute
□ Class inherits from RustPlugin (or CovalencePlugin)
□ Namespace is Oxide.Plugins
□ Plugin name matches filename (MyPlugin.cs → class MyPlugin)
```

### 2. Logging Check
```
□ No LogWarning() calls
□ No LogError() calls
□ No Debug.Log() calls
□ No Console.WriteLine() calls
□ All logging uses Puts(), PrintWarning(), or PrintError()
```

### 3. Permission Check
```
□ All permissions registered in Init() or OnServerInitialized()
□ All permission checks use player.UserIDString (not userID.ToString())
□ Permission strings are lowercase with dots (myplugin.use)
□ Permissions follow naming: pluginname.feature
```

### 4. Null Safety Check
```
□ All player references checked: if (player == null) return;
□ Delayed player access checks: if (!player.IsConnected) return;
□ Entity references checked: if (entity == null || entity.IsDestroyed) return;
□ Item references checked: if (item == null) return;
□ Collections use TryGetValue or null-conditional
```

### 5. Cleanup Check
```
□ Unload() method exists
□ All timers destroyed in Unload()
□ All UI elements destroyed in Unload()
□ All tracked entities cleaned up
□ All components removed from GameObjects
□ All event subscriptions unsubscribed
```

### 6. Timer Safety Check
```
□ Timer callbacks check player validity
□ Timer callbacks check entity validity
□ Timers stored in fields if need to be destroyed
□ Repeating timers have proper destroy logic
```

---

## HOOK SIGNATURE VALIDATION

### Player Hooks - EXACT Signatures Required
```csharp
void OnPlayerConnected(BasePlayer player)                    // ✅
void OnPlayerDisconnected(BasePlayer player, string reason)  // ✅
void OnPlayerRespawned(BasePlayer player)                    // ✅
void OnPlayerDeath(BasePlayer player, HitInfo info)          // ✅
void OnPlayerInput(BasePlayer player, InputState input)      // ✅
void OnPlayerSleep(BasePlayer player)                        // ✅
void OnPlayerSleepEnded(BasePlayer player)                   // ✅

// WRONG:
void OnPlayerConnect(BasePlayer player)     // ❌ Should be OnPlayerConnected
void OnPlayerJoin(BasePlayer player)        // ❌ Should be OnPlayerConnected
void OnPlayerLeave(BasePlayer player)       // ❌ Should be OnPlayerDisconnected
```

### Entity Hooks - EXACT Signatures Required
```csharp
void OnEntitySpawned(BaseNetworkable entity)                       // ✅
void OnEntityKill(BaseNetworkable entity)                          // ✅
object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)   // ✅
void OnEntityBuilt(Planner planner, GameObject go)                 // ✅

// WRONG:
void OnEntitySpawn(BaseNetworkable entity)  // ❌ Should be OnEntitySpawned
void OnEntityDeath(BaseEntity entity)       // ❌ Wrong signature
```

### Item Hooks
```csharp
void OnActiveItemChanged(BasePlayer player, Item oldItem, Item newItem)  // ✅
void OnItemAddedToContainer(ItemContainer container, Item item)          // ✅
void OnItemRemovedFromContainer(ItemContainer container, Item item)      // ✅
```

---

## ANTI-PATTERN DETECTION

### Pattern 1: Storing Player References
```csharp
// ❌ BAD: Player can disconnect, reference becomes stale
private Dictionary<ulong, BasePlayer> cachedPlayers = new();

// ✅ GOOD: Store userID, look up when needed
private HashSet<ulong> activePlayers = new();
var player = BasePlayer.FindByID(userId);
```

### Pattern 2: Missing Null Checks
```csharp
// ❌ BAD: Can throw NullReferenceException
void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
{
    var player = entity.ToPlayer();
    player.ChatMessage("Hit!"); // Crash if entity isn't a player!
}

// ✅ GOOD: Null check before use
void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
{
    var player = entity.ToPlayer();
    if (player == null) return;
    player.ChatMessage("Hit!");
}
```

### Pattern 3: Timer Without Cleanup
```csharp
// ❌ BAD: Timer keeps running after unload
void OnServerInitialized()
{
    timer.Every(60f, DoSomething); // Lost reference!
}

// ✅ GOOD: Store timer, destroy in Unload
private Timer updateTimer;

void OnServerInitialized()
{
    updateTimer = timer.Every(60f, DoSomething);
}

void Unload()
{
    updateTimer?.Destroy();
}
```

### Pattern 4: Unsafe Timer Callback
```csharp
// ❌ BAD: Player might disconnect before timer fires
timer.Once(5f, () => player.ChatMessage("Hello"));

// ✅ GOOD: Validate in callback
timer.Once(5f, () =>
{
    if (player == null || !player.IsConnected) return;
    player.ChatMessage("Hello");
});
```

### Pattern 5: UI Without Cleanup
```csharp
// ❌ BAD: UI stays after plugin unload
void ShowUI(BasePlayer player)
{
    CuiHelper.AddUi(player, container);
}

// ✅ GOOD: Track and clean up
private const string PANEL = "MyPanel";

void Unload()
{
    foreach (var player in BasePlayer.activePlayerList)
        CuiHelper.DestroyUi(player, PANEL);
}
```

---

## CONFIG VALIDATION

### Required JsonProperty Attributes
```csharp
// ✅ GOOD: Properly attributed config
private class Configuration
{
    [JsonProperty("Enable Feature")]
    public bool Enabled { get; set; } = true;
    
    [JsonProperty("Cooldown (seconds)")]
    public float Cooldown { get; set; } = 5f;
}

// ❌ BAD: Missing attributes (won't serialize nicely)
private class Configuration
{
    public bool Enabled = true;
    public float Cooldown = 5f;
}
```

### Nested Config Classes
```csharp
// ✅ GOOD: Nested classes with defaults
private class Configuration
{
    [JsonProperty("Feature Settings")]
    public FeatureSettings Feature { get; set; } = new();
}

private class FeatureSettings
{
    [JsonProperty("Enabled")]
    public bool Enabled { get; set; } = true;
}
```

---

## QUICK VALIDATION COMMANDS

Before delivering a plugin, mentally run through:

```
1. SEARCH: "LogWarning" - Should find 0 matches
2. SEARCH: "LogError" - Should find 0 matches  
3. SEARCH: "Debug.Log" - Should find 0 matches
4. SEARCH: ".userID," (with comma) - Check if used with permissions
5. CHECK: Does Unload() exist and clean up everything?
6. CHECK: Do all timer callbacks validate player/entity state?
7. CHECK: Is there proper null checking throughout?
```
