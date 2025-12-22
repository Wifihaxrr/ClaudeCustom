# OXIDE PERFORMANCE OPTIMIZATION & MEMORY SAFETY

**Performance optimization techniques and memory safety guidelines for Rust Oxide plugins.**

---

## MEMORY ALLOCATION PATTERNS

### Object Pooling
```csharp
// ✅ GOOD: Reuse lists instead of creating new ones
private List<BaseEntity> entityBuffer = new List<BaseEntity>();

private void FindNearbyEntities(Vector3 pos, float radius)
{
    entityBuffer.Clear();  // Reuse the same list
    Vis.Entities(pos, radius, entityBuffer);
    
    foreach (var entity in entityBuffer)
    {
        // Process
    }
}

// ❌ BAD: Creating new list every call
private void FindNearbyEntitiesBad(Vector3 pos, float radius)
{
    var entities = new List<BaseEntity>(); // Creates garbage every call!
    Vis.Entities(pos, radius, entities);
}
```

### String Caching
```csharp
// ✅ GOOD: Cache frequently used strings
private const string PERM_USE = "myplugin.use";
private const string PERM_VIP = "myplugin.vip";
private const string MSG_NO_PERM = "You don't have permission!";

// Cache lang messages at load
private Dictionary<string, string> cachedMessages = new();

private void OnServerInitialized()
{
    foreach (var player in BasePlayer.activePlayerList)
        CachePlayerMessages(player);
}

// ❌ BAD: String concatenation in hot paths
private void OnPlayerInput(BasePlayer player, InputState input)
{
    var msg = "Player " + player.displayName + " pressed button"; // GC pressure!
}

// ✅ GOOD: Use string interpolation (compiled efficiently) or StringBuilder
private void OnPlayerInput(BasePlayer player, InputState input)
{
    Puts($"Player {player.displayName} pressed button"); // Better
}
```

---

## HOOK SUBSCRIPTION OPTIMIZATION

### Unsubscribe Expensive Hooks When Not Needed
```csharp
private bool featureEnabled = false;

private void Init()
{
    // Don't run expensive hooks until needed
    Unsubscribe(nameof(OnEntityTakeDamage));
    Unsubscribe(nameof(OnPlayerInput));
    Unsubscribe(nameof(OnItemAddedToContainer));
}

[ChatCommand("enablefeature")]
private void CmdEnable(BasePlayer player, string cmd, string[] args)
{
    featureEnabled = true;
    Subscribe(nameof(OnEntityTakeDamage));
    Subscribe(nameof(OnPlayerInput));
}

[ChatCommand("disablefeature")]
private void CmdDisable(BasePlayer player, string cmd, string[] args)
{
    featureEnabled = false;
    Unsubscribe(nameof(OnEntityTakeDamage));
    Unsubscribe(nameof(OnPlayerInput));
}
```

### Hook Performance Tiers
```
TIER 1 (Very Cheap - Always OK):
  OnPlayerConnected, OnPlayerDisconnected, OnServerInitialized
  
TIER 2 (Cheap - Subscribe freely):
  OnPlayerRespawned, OnPlayerDeath, OnEntitySpawned
  
TIER 3 (Moderate - Consider unsubscribing when idle):
  OnActiveItemChanged, OnItemAddedToContainer, OnLootEntity

TIER 4 (Expensive - Unsubscribe when not needed):
  OnEntityTakeDamage (called VERY frequently in combat)
  OnPlayerInput (called every frame per player)
  OnItemPickup, OnCollectiblePickup

TIER 5 (Very Expensive - Use sparingly):
  CanBuild (called per placement attempt)
  OnServerCommand (every console command)
```

---

## COLLECTION ITERATION

### Use ToList() When Modifying During Iteration
```csharp
// ❌ BAD: Will throw "Collection was modified" exception
foreach (var player in BasePlayer.activePlayerList)
{
    if (ShouldKick(player))
        player.Kick("Removed"); // Modifies the collection!
}

// ✅ GOOD: ToList() creates a copy
foreach (var player in BasePlayer.activePlayerList.ToList())
{
    if (ShouldKick(player))
        player.Kick("Removed"); // Safe
}

// ✅ ALSO GOOD: Collect first, then modify
var toKick = BasePlayer.activePlayerList.Where(ShouldKick).ToList();
foreach (var player in toKick)
    player.Kick("Removed");
```

### Efficient Lookups
```csharp
// ❌ BAD: O(n) lookup every time
private List<ulong> vipPlayers = new List<ulong>();
if (vipPlayers.Contains(player.userID)) // Slow for large lists

// ✅ GOOD: O(1) lookup
private HashSet<ulong> vipPlayers = new HashSet<ulong>();
if (vipPlayers.Contains(player.userID)) // Fast!

// ✅ GOOD: Dictionary for data lookup
private Dictionary<ulong, PlayerData> playerData = new();
if (playerData.TryGetValue(player.userID, out var data)) // Fast + data access
```

---

## COROUTINES FOR LONG OPERATIONS

### Spread Work Across Frames
```csharp
private Coroutine _processCoroutine;

private IEnumerator ProcessAllEntities()
{
    var entities = BaseNetworkable.serverEntities.ToList();
    int processed = 0;
    
    foreach (var entity in entities)
    {
        if (entity == null || entity.IsDestroyed) continue;
        
        ProcessEntity(entity);
        
        // Yield every 50 entities to prevent lag spikes
        if (++processed % 50 == 0)
            yield return CoroutineEx.waitForEndOfFrame;
    }
    
    Puts($"Processed {processed} entities");
}

// Start coroutine
private void StartProcessing()
{
    _processCoroutine = ServerMgr.Instance.StartCoroutine(ProcessAllEntities());
}

// ALWAYS stop in Unload!
private void Unload()
{
    if (_processCoroutine != null)
        ServerMgr.Instance.StopCoroutine(_processCoroutine);
}
```

---

## ENTITY LOOKUP OPTIMIZATION

### Cache Entity References Safely
```csharp
// Store NetworkableId, not BaseEntity reference
private Dictionary<NetworkableId, CachedEntityData> entityCache = new();

private class CachedEntityData
{
    public NetworkableId Id;
    public Vector3 LastPosition;
    public float LastCheck;
}

private BaseEntity GetCachedEntity(NetworkableId id)
{
    var entity = BaseNetworkable.serverEntities.Find(id) as BaseEntity;
    if (entity == null || entity.IsDestroyed)
    {
        entityCache.Remove(id);
        return null;
    }
    return entity;
}
```

### Spatial Queries
```csharp
// ✅ GOOD: Use Vis.Entities for radius searches
private List<BasePlayer> nearbyPlayers = new List<BasePlayer>();

private void FindPlayersNear(Vector3 pos, float radius)
{
    nearbyPlayers.Clear();
    Vis.Entities(pos, radius, nearbyPlayers, Layers.Mask.Player_Server);
}

// ❌ BAD: Iterating all players and checking distance
foreach (var player in BasePlayer.activePlayerList)
{
    if (Vector3.Distance(player.transform.position, pos) < radius) // Slow!
}
```

---

## GC PRESSURE REDUCTION

### Avoid Boxing
```csharp
// ❌ BAD: Boxing int to object
object boxed = 42;

// ✅ GOOD: Use generics
Dictionary<ulong, int> scores = new();
scores[playerId] = 42; // No boxing
```

### Struct vs Class
```csharp
// ✅ GOOD: Use struct for small, immutable data
private struct CooldownData
{
    public float StartTime;
    public float Duration;
}

// Use class for larger objects with methods
private class PlayerState
{
    public bool Enabled { get; set; }
    public List<Item> Items { get; set; }
    public void Reset() { Items.Clear(); }
}
```

---

## TIMER BEST PRACTICES

### Destroy Timers Properly
```csharp
private Timer updateTimer;
private Dictionary<ulong, Timer> playerTimers = new();

private void OnServerInitialized()
{
    updateTimer = timer.Every(60f, UpdateAllPlayers);
}

private void Unload()
{
    updateTimer?.Destroy();
    
    foreach (var t in playerTimers.Values)
        t?.Destroy();
    playerTimers.Clear();
}

private void OnPlayerDisconnected(BasePlayer player, string reason)
{
    if (playerTimers.TryGetValue(player.userID, out var t))
    {
        t?.Destroy();
        playerTimers.Remove(player.userID);
    }
}
```

### Timer Frequency Guidelines
```
Every 0.1s  - VERY EXPENSIVE, avoid unless necessary (real-time UI)
Every 0.5s  - Expensive, use sparingly (active gameplay features)
Every 1s    - Moderate (status updates, decay checks)
Every 5s    - Cheap (periodic saves, cleanup)
Every 60s+  - Very cheap (statistics, announcements)
```

---

## MEMORY SAFETY CHECKLIST

1. **Never store BasePlayer references long-term** - use userID
2. **Never store BaseEntity references long-term** - use NetworkableId
3. **Always null-check entities before use**
4. **Always check IsDestroyed for entities**
5. **Always check IsConnected for players in delayed callbacks**
6. **Clear all collections in Unload()**
7. **Destroy all timers in Unload()**
8. **Stop all coroutines in Unload()**
9. **Destroy all UI elements in Unload()**
10. **Remove components from GameObjects in Unload()**
