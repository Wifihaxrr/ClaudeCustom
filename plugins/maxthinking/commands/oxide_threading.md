# OXIDE THREAD SAFETY FOR MULTIPLAYER

**Thread safety patterns and considerations for Rust Oxide multiplayer environments.**

---

## UNITY MAIN THREAD REQUIREMENTS

### Rule: All Game API Calls Must Be On Main Thread
```csharp
// ❌ BAD: Calling Unity/Rust API from callback thread
webrequest.Enqueue(url, null, (code, response) =>
{
    // This callback may not be on main thread!
    player.ChatMessage("Done"); // UNSAFE
    player.GiveItem(item);      // UNSAFE
}, this);

// ✅ GOOD: Use NextTick to marshal to main thread
webrequest.Enqueue(url, null, (code, response) =>
{
    NextTick(() =>
    {
        if (player == null || !player.IsConnected) return;
        player.ChatMessage("Done"); // Safe on main thread
    });
}, this);
```

---

## NEXTTICK / NEXTFRAME PATTERNS

### NextTick - Runs Next Server Tick (Most Common)
```csharp
// Safe for modifying collections during hooks
private void OnItemAddedToContainer(ItemContainer container, Item item)
{
    NextTick(() =>
    {
        if (item == null) return;
        ProcessItem(item); // Safe - not during enumeration
    });
}

// Safe for entity modifications after spawn
private void OnEntitySpawned(BaseNetworkable entity)
{
    var oven = entity as BaseOven;
    if (oven == null) return;
    
    NextTick(() =>
    {
        if (oven == null || oven.IsDestroyed) return;
        oven.inventory.temperature = 1000f;
    });
}
```

### NextFrame - Runs Next Unity Frame
```csharp
// Use for UI updates
private void OnActiveItemChanged(BasePlayer player, Item oldItem, Item newItem)
{
    NextFrame(() =>
    {
        if (player == null || !player.IsConnected) return;
        UpdatePlayerUI(player);
    });
}
```

### Difference Between NextTick and NextFrame
```
NextTick:  Runs after current hook chain completes
           Use for: Collection mods, entity changes, data updates
           
NextFrame: Runs on next Unity frame (render cycle)
           Use for: UI updates, visual effects, camera operations
```

---

## TIMER CALLBACK SAFETY

### Always Validate Player State in Callbacks
```csharp
// ❌ BAD: No validation
timer.Once(5f, () =>
{
    player.ChatMessage("Hello!"); // Player might be disconnected!
});

// ✅ GOOD: Full validation
timer.Once(5f, () =>
{
    if (player == null) return;           // Player object gone
    if (!player.IsConnected) return;      // Player disconnected
    if (player.IsSleeping()) return;      // Optional: skip sleepers
    
    player.ChatMessage("Hello!");
});

// ✅ ALSO GOOD: Capture userID and look up fresh
var userId = player.userID;
timer.Once(5f, () =>
{
    var p = BasePlayer.FindByID(userId);
    if (p == null || !p.IsConnected) return;
    p.ChatMessage("Hello!");
});
```

### Entity Validation in Timers
```csharp
// ❌ BAD: Entity might be destroyed
timer.Once(2f, () =>
{
    entity.Kill(); // Might throw if already destroyed!
});

// ✅ GOOD: Check validity
timer.Once(2f, () =>
{
    if (entity == null) return;
    if (entity.IsDestroyed) return;
    entity.Kill();
});

// ✅ ALSO GOOD: Store NetworkableId
var entityId = entity.net.ID;
timer.Once(2f, () =>
{
    var e = BaseNetworkable.serverEntities.Find(entityId);
    if (e == null) return;
    e.Kill();
});
```

---

## CONCURRENT COLLECTION ACCESS

### Problem: Multiple Hooks Modifying Same Collection
```csharp
private Dictionary<ulong, int> playerScores = new();

// These could be called from different code paths simultaneously!
void OnPlayerConnected(BasePlayer player)
{
    playerScores[player.userID] = 0;
}

void OnPlayerDisconnected(BasePlayer player, string reason)
{
    playerScores.Remove(player.userID);
}

void AddScore(ulong playerId, int amount)
{
    if (playerScores.ContainsKey(playerId))
        playerScores[playerId] += amount;
}
```

### Solution: Single-Threaded Nature of Oxide Hooks
```csharp
// Good news: Oxide hooks run synchronously on main thread
// So the above code is actually safe in normal use!

// HOWEVER, be careful with:
// 1. Web request callbacks
// 2. Timer callbacks during iteration
// 3. Coroutines modifying collections

// ✅ SAFE: Use ToList() when iterating and potentially modifying
foreach (var kvp in playerScores.ToList())
{
    if (kvp.Value < 0)
        playerScores.Remove(kvp.Key); // Safe with ToList()
}
```

---

## RACE CONDITION PREVENTION

### Problem: Action Based on Stale State
```csharp
// ❌ BAD: Player state can change between check and action
[ChatCommand("heal")]
private void CmdHeal(BasePlayer player, string cmd, string[] args)
{
    if (player.health < 50) // Check
    {
        timer.Once(1f, () =>
        {
            player.Heal(50); // Action - but health might be different now!
        });
    }
}

// ✅ GOOD: Re-validate in timer
[ChatCommand("heal")]
private void CmdHeal(BasePlayer player, string cmd, string[] args)
{
    if (player.health < 50)
    {
        timer.Once(1f, () =>
        {
            if (player == null || !player.IsConnected) return;
            if (player.health >= 50) return; // Re-validate!
            player.Heal(50);
        });
    }
}
```

### Store Intent, Not State
```csharp
// ❌ BAD: Storing computed value
bool shouldHeal = player.health < 50;
timer.Once(1f, () =>
{
    if (shouldHeal) // Stale data!
        player.Heal(50);
});

// ✅ GOOD: Compute fresh
var userId = player.userID;
timer.Once(1f, () =>
{
    var p = BasePlayer.FindByID(userId);
    if (p == null || !p.IsConnected) return;
    if (p.health < 50) // Fresh check
        p.Heal(50);
});
```

---

## NETWORK UPDATE SYNCHRONIZATION

### SendNetworkUpdate vs SendNetworkUpdateImmediate
```csharp
// SendNetworkUpdate - Batched, more efficient
entity.skinID = newSkin;
entity.SendNetworkUpdate(); // Queued for next network tick

// SendNetworkUpdateImmediate - Instant, use sparingly
entity.SetFlag(BaseEntity.Flags.On, true);
entity.SendNetworkUpdateImmediate(); // Sent immediately

// When to use Immediate:
// - UI-critical state changes (doors, lights)
// - Time-sensitive gameplay (weapon state)
// - After position changes

// When regular is fine:
// - Cosmetic changes (skins)
// - Batch operations
// - Non-urgent state
```

### Player SendNetworkUpdateImmediate
```csharp
// After teleport - REQUIRED
player.Teleport(position);
player.SendNetworkUpdateImmediate();

// After inventory changes - recommended
player.inventory.GiveItem(item);
player.SendNetworkUpdate(); // Regular is usually fine

// After metabolism changes
player.metabolism.calories.value = 500;
player.metabolism.SendChangesToClient();
```

---

## PLAYER CONNECTION STATE HANDLING

### Connection States to Check
```csharp
player.IsConnected     // Has active network connection
player.IsSleeping()    // Player is sleeping (logged out but body exists)
player.IsDead()        // Player is dead
player.IsSpectating()  // Player is spectating
player.IsWounded()     // Player is downed

// Full safety check
private bool IsValidPlayer(BasePlayer player)
{
    if (player == null) return false;
    if (!player.IsConnected) return false;
    if (player.IsDead()) return false;
    return true;
}

// For UI/messages, usually just need:
if (player == null || !player.IsConnected) return;
```

### Handle Disconnect During Operations
```csharp
private Dictionary<ulong, List<Item>> pendingItems = new();

private void GiveItemsAsync(BasePlayer player, List<Item> items)
{
    var userId = player.userID;
    pendingItems[userId] = items;
    
    timer.Once(2f, () =>
    {
        if (!pendingItems.TryGetValue(userId, out var pending))
            return;
        
        pendingItems.Remove(userId);
        
        var p = BasePlayer.FindByID(userId);
        if (p == null || !p.IsConnected)
        {
            // Player disconnected - save items for later or drop
            foreach (var item in pending)
                item.Remove(); // or save to database
            return;
        }
        
        foreach (var item in pending)
            p.GiveItem(item);
    });
}
```

---

## THREAD SAFETY CHECKLIST

1. **Use NextTick() for web request callbacks**
2. **Validate player.IsConnected in all timer callbacks**
3. **Check entity.IsDestroyed before entity operations**
4. **Use ToList() when iterating and modifying collections**
5. **Store userID/NetworkableId instead of object references**
6. **Re-validate conditions in delayed callbacks**
7. **Use SendNetworkUpdateImmediate after position changes**
8. **Handle player disconnect gracefully for async operations**
