# OXIDE PLUGIN LIFECYCLE MANAGEMENT

**Complete plugin lifecycle patterns from Init to Unload.**

---

## LIFECYCLE FLOW

```
Plugin Load Order:
┌─────────────────────────────────────────────────────────────┐
│  1. Constructor (avoid - use Init instead)                  │
│  2. LoadDefaultConfig() / LoadConfig()                      │
│  3. Init()                     ← Register permissions       │
│  4. Loaded()                   ← Plugin fully loaded        │
│  5. OnServerInitialized()      ← Server ready, players exist│
│  6. [Plugin runs normally]                                  │
│  7. Unload()                   ← Cleanup EVERYTHING         │
└─────────────────────────────────────────────────────────────┘
```

---

## INIT PHASE

### Init() - Early Initialization
```csharp
private void Init()
{
    // Register permissions (ALWAYS do this here)
    permission.RegisterPermission(PERM_USE, this);
    permission.RegisterPermission(PERM_ADMIN, this);
    
    // Load data
    LoadData();
    
    // Unsubscribe from expensive hooks (if conditional)
    Unsubscribe(nameof(OnEntityTakeDamage));
    Unsubscribe(nameof(OnPlayerInput));
    
    // Register lang messages
    lang.RegisterMessages(new Dictionary<string, string>
    {
        ["NoPermission"] = "You don't have permission!",
        ["Success"] = "Action completed successfully."
    }, this);
}
```

### Loaded() - Post-Init (Rarely Needed)
```csharp
private void Loaded()
{
    // Called after Init(), before OnServerInitialized()
    // Use for plugin reference setup
}
```

### OnServerInitialized() - Server Ready
```csharp
private void OnServerInitialized()
{
    // Server is fully initialized
    // All entities exist, players may be connected
    
    // Start repeating timers
    updateTimer = timer.Every(60f, UpdateAllPlayers);
    
    // Initialize existing players
    foreach (var player in BasePlayer.activePlayerList)
        InitializePlayer(player);
    
    // Check for required plugins
    if (Economics == null)
        PrintWarning("Economics not found - shop disabled");
    
    // Subscribe to TOD events
    if (TOD_Sky.Instance != null)
    {
        TOD_Sky.Instance.Components.Time.OnSunrise += OnSunrise;
        TOD_Sky.Instance.Components.Time.OnSunset += OnSunset;
    }
}
```

---

## UNLOAD PHASE (CRITICAL!)

### Complete Unload Template
```csharp
private void Unload()
{
    // 1. Save data
    SaveData();
    
    // 2. Destroy all timers
    updateTimer?.Destroy();
    foreach (var t in playerTimers.Values)
        t?.Destroy();
    playerTimers.Clear();
    
    // 3. Stop all coroutines
    if (_coroutine != null)
        ServerMgr.Instance.StopCoroutine(_coroutine);
    
    // 4. Destroy all UI
    foreach (var player in BasePlayer.activePlayerList)
    {
        CuiHelper.DestroyUi(player, MAIN_PANEL);
        CuiHelper.DestroyUi(player, SUB_PANEL);
    }
    
    // 5. Remove components from players
    foreach (var player in BasePlayer.activePlayerList)
    {
        var component = player.GetComponent<MyComponent>();
        if (component != null)
            UnityEngine.Object.DestroyImmediate(component);
    }
    
    // 6. Kill spawned entities (if appropriate)
    foreach (var id in spawnedEntities.ToList())
    {
        var entity = BaseNetworkable.serverEntities.Find(id);
        entity?.Kill();
    }
    spawnedEntities.Clear();
    
    // 7. Unsubscribe from events
    if (TOD_Sky.Instance != null)
    {
        TOD_Sky.Instance.Components.Time.OnSunrise -= OnSunrise;
        TOD_Sky.Instance.Components.Time.OnSunset -= OnSunset;
    }
    
    // 8. Clear all collections
    playerData.Clear();
    cooldowns.Clear();
    trackedEntities.Clear();
    
    // 9. Null static instance if using singleton
    _instance = null;
}
```

---

## PLAYER CONNECTION LIFECYCLE

```csharp
private void OnPlayerConnected(BasePlayer player)
{
    // Player just connected, may not be fully spawned
    // Use timer for delayed setup
    
    timer.Once(1f, () =>
    {
        if (player == null || !player.IsConnected) return;
        InitializePlayer(player);
    });
}

private void OnPlayerRespawned(BasePlayer player)
{
    // Player respawned after death
    // Good for applying spawn effects
    
    if (player == null) return;
    ApplySpawnEffects(player);
}

private void OnPlayerSleepEnded(BasePlayer player)
{
    // Player woke up (logged in while body was sleeping)
    
    if (player == null) return;
    RefreshPlayerUI(player);
}

private void OnPlayerDisconnected(BasePlayer player, string reason)
{
    // Player disconnected
    // Clean up player-specific resources
    
    if (player == null) return;
    
    // Remove from tracking
    activePlayers.Remove(player.userID);
    
    // Destroy player timer
    if (playerTimers.TryGetValue(player.userID, out var t))
    {
        t?.Destroy();
        playerTimers.Remove(player.userID);
    }
    
    // Save player data if needed
    SavePlayerData(player.userID);
}
```

---

## DATA PERSISTENCE TIMING

### When to Save
```csharp
// OnServerSave - Called periodically by server
private void OnServerSave()
{
    SaveData();
}

// OnNewSave - Called when server wipes
private void OnNewSave(string filename)
{
    // Clear wipe-specific data
    data.Players.Clear();
    SaveData();
    
    Puts("Data wiped for new save");
}

// Unload - Always save on unload
private void Unload()
{
    SaveData();
    // ... rest of cleanup
}

// Important player actions (optional, for safety)
private void OnPlayerDisconnected(BasePlayer player, string reason)
{
    SavePlayerData(player.userID);
}
```

### Efficient Saving Pattern
```csharp
private float lastSaveTime;
private bool dataDirty;

private void MarkDataDirty()
{
    dataDirty = true;
}

private void OnServerSave()
{
    if (dataDirty)
    {
        SaveData();
        dataDirty = false;
    }
}

// Only save when actually modified
private void AddPlayerScore(ulong playerId, int amount)
{
    GetPlayerData(playerId).Score += amount;
    MarkDataDirty();
}
```

---

## HOT RELOAD CONSIDERATIONS

### State Persistence Across Reloads
```csharp
// Problem: Plugin reloads lose runtime state
// Solution: Save important state to data file

private void Unload()
{
    // Save temporary state
    Interface.Oxide.DataFileSystem.WriteObject($"{Name}_temp", tempData);
}

private void OnServerInitialized()
{
    // Restore temporary state
    tempData = Interface.Oxide.DataFileSystem.ReadObject<TempData>($"{Name}_temp");
    if (tempData != null)
    {
        Puts("Restored state from previous session");
        // Delete temp file
        Interface.Oxide.DataFileSystem.DeleteDataFile($"{Name}_temp");
    }
}
```

### Handling Existing Players on Reload
```csharp
private void OnServerInitialized()
{
    // Apply plugin state to already-connected players
    foreach (var player in BasePlayer.activePlayerList)
    {
        if (player == null || !player.IsConnected) continue;
        
        // Remove stale UI from previous load
        CuiHelper.DestroyUi(player, PANEL);
        
        // Re-initialize player
        InitializePlayer(player);
    }
}
```

---

## WIPE DETECTION AND HANDLING

```csharp
private void OnNewSave(string filename)
{
    // Called when world wipes
    // filename is the new save file name
    
    Puts($"New save detected: {filename}");
    
    // Reset wipe-specific data
    foreach (var pd in data.Players.Values)
    {
        pd.WipeData = new WipeData(); // Reset wipe-specific
        // Keep pd.PermanentData; // Preserve permanent
    }
    
    SaveData();
}

private class PlayerData
{
    // Resets each wipe
    public WipeData WipeData { get; set; } = new();
    
    // Persists across wipes
    public PermanentData Stats { get; set; } = new();
}
```

---

## SERVER EVENTS

```csharp
// Server is saving
void OnServerSave()
{
    SaveData();
}

// Another plugin loaded
void OnPluginLoaded(Plugin plugin)
{
    if (plugin.Name == "Economics")
    {
        Economics = plugin;
        Puts("Economics detected - enabling shop");
    }
}

// Another plugin unloaded
void OnPluginUnloaded(Plugin plugin)
{
    if (plugin.Name == "Economics")
    {
        Economics = null;
        PrintWarning("Economics unloaded - shop disabled");
    }
}
```

---

## LIFECYCLE CHECKLIST

### Before Release
```
□ Init() registers all permissions
□ Init() loads data and registers lang
□ OnServerInitialized() starts timers
□ OnServerInitialized() initializes existing players
□ Unload() saves data
□ Unload() destroys all timers
□ Unload() destroys all UI
□ Unload() removes all components
□ Unload() cleans up spawned entities
□ Unload() unsubscribes from events
□ Unload() clears all collections
□ OnPlayerDisconnected() cleans up player resources
□ OnNewSave() handles wipe appropriately
```
