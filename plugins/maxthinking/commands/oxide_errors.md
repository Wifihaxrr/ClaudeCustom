# OXIDE ERROR HANDLING STANDARDS

**Error handling standards and graceful degradation for game mods.**

---

## TRY/CATCH PATTERNS FOR HOOKS

### When to Use Try/Catch
```csharp
// Use try/catch for:
// - External data parsing
// - Plugin integration calls
// - File operations
// - Web request handling

// DON'T wrap every hook - it hides bugs!
// Instead, use null checks and validation
```

### Safe Hook Pattern
```csharp
private void OnDispenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item)
{
    try
    {
        // Core logic that might fail
        var player = entity?.ToPlayer();
        if (player == null) return;
        
        // Process gather...
    }
    catch (Exception ex)
    {
        // Log but don't crash server
        PrintError($"OnDispenserGather error: {ex.Message}");
    }
}
```

### When NOT to Try/Catch
```csharp
// ❌ BAD: Hiding bugs
private void OnPlayerConnected(BasePlayer player)
{
    try
    {
        player.ChatMessage("Welcome!"); // If this crashes, we WANT to know!
    }
    catch { }  // Silent failure - terrible!
}

// ✅ GOOD: Proper null check instead
private void OnPlayerConnected(BasePlayer player)
{
    if (player == null || !player.IsConnected) return;
    player.ChatMessage("Welcome!");
}
```

---

## GRACEFUL DEGRADATION

### Feature Fallbacks
```csharp
[PluginReference] private Plugin Economics;
[PluginReference] private Plugin ServerRewards;

private bool TryPayment(ulong playerId, double amount, out string error)
{
    error = null;
    
    // Try primary economy
    if (Economics != null)
    {
        var balance = Economics.Call<double>("Balance", playerId.ToString());
        if (balance >= amount)
        {
            Economics.Call<bool>("Withdraw", playerId.ToString(), amount);
            return true;
        }
        error = $"Not enough money. Need: {amount}, Have: {balance}";
        return false;
    }
    
    // Fallback to secondary
    if (ServerRewards != null)
    {
        var points = ServerRewards.Call<int>("CheckPoints", playerId);
        if (points >= (int)amount)
        {
            ServerRewards.Call<object>("TakePoints", playerId, (int)amount);
            return true;
        }
        error = $"Not enough points. Need: {amount}, Have: {points}";
        return false;
    }
    
    // No economy - allow free or deny
    if (config.AllowFreeWithoutEconomy)
        return true;
    
    error = "No economy plugin found";
    return false;
}
```

### Optional Feature Warnings
```csharp
private void OnServerInitialized()
{
    // Required dependency
    if (ImageLibrary == null)
    {
        PrintError("ImageLibrary is REQUIRED! Plugin disabled.");
        Interface.Oxide.UnloadPlugin(Name);
        return;
    }
    
    // Optional dependency
    if (Economics == null)
        PrintWarning("Economics not found - shop prices disabled");
    
    // Optional feature based on config
    if (config.EnableWebhooks && string.IsNullOrEmpty(config.WebhookUrl))
        PrintWarning("Webhooks enabled but no URL configured");
}
```

---

## ERROR LOGGING STANDARDS

### Log Levels
```csharp
// INFO - Normal operation
Puts("Plugin loaded successfully");
Puts($"Loaded {data.Players.Count} player records");

// WARNING - Non-critical issues
PrintWarning("Config migration required");
PrintWarning($"Player {playerId} has corrupted data, resetting");

// ERROR - Critical issues
PrintError("Failed to load data file");
PrintError($"Database connection failed: {ex.Message}");
```

### Contextual Error Messages
```csharp
// ❌ BAD: Unhelpful
PrintError("Error");
PrintError("Something went wrong");

// ✅ GOOD: Actionable
PrintError($"Failed to spawn NPC at {position}: prefab not found");
PrintError($"Config error in 'Shop Items': {itemName} is not a valid item shortname");
PrintError($"Permission check failed for {player.displayName}: {ex.Message}");
```

### Debug Mode
```csharp
private class Configuration
{
    [JsonProperty("Debug Mode")]
    public bool Debug { get; set; } = false;
}

private void LogDebug(string message)
{
    if (config.Debug)
        Puts($"[DEBUG] {message}");
}

// Usage
LogDebug($"Processing {items.Count} items for {player.displayName}");
```

---

## PLAYER-FACING ERROR MESSAGES

### User-Friendly Messages
```csharp
// ❌ BAD: Technical error
SendReply(player, "NullReferenceException in ProcessPurchase");

// ✅ GOOD: User-friendly
SendReply(player, "<color=#ff4444>Purchase failed. Please try again.</color>");

// ✅ BETTER: With actionable info
SendReply(player, "<color=#ff4444>Not enough money!</color> Need: <color=#ffaa00>$500</color>, Have: <color=#ffaa00>$350</color>");
```

### Error Message Templates
```csharp
private static class Messages
{
    public const string NO_PERMISSION = "<color=#ff4444>You don't have permission to use this command.</color>";
    public const string PLAYER_NOT_FOUND = "<color=#ff4444>Player not found.</color>";
    public const string ITEM_NOT_FOUND = "<color=#ff4444>Unknown item: {0}</color>";
    public const string COOLDOWN = "<color=#ffaa00>Please wait {0} seconds before using this again.</color>";
    public const string SUCCESS = "<color=#44ff44>{0}</color>";
    public const string ERROR = "<color=#ff4444>{0}</color>";
}

// Usage
SendReply(player, string.Format(Messages.COOLDOWN, remaining));
SendReply(player, string.Format(Messages.SUCCESS, "Item purchased!"));
```

---

## RECOVERY FROM CORRUPTED DATA

### Safe Data Loading
```csharp
private void LoadData()
{
    try
    {
        data = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name);
    }
    catch (Exception ex)
    {
        PrintError($"Failed to load data: {ex.Message}");
        
        // Backup corrupted file
        try
        {
            var corruptPath = $"oxide/data/{Name}_corrupted_{DateTime.Now:yyyyMMdd_HHmmss}.json";
            // File operations here
            PrintWarning($"Corrupted data backed up to: {corruptPath}");
        }
        catch { }
        
        // Start fresh
        data = new StoredData();
        PrintWarning("Starting with fresh data");
    }
    
    if (data == null)
        data = new StoredData();
    
    // Validate and repair data
    RepairData();
}

private void RepairData()
{
    int repaired = 0;
    
    foreach (var kvp in data.Players.ToList())
    {
        var pd = kvp.Value;
        
        // Fix negative values
        if (pd.Score < 0)
        {
            pd.Score = 0;
            repaired++;
        }
        
        // Remove null entries from lists
        pd.Items?.RemoveAll(x => x == null);
    }
    
    if (repaired > 0)
        PrintWarning($"Repaired {repaired} data entries");
}
```

### Versioned Data Migration
```csharp
private void LoadData()
{
    data = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name) ?? new StoredData();
    
    if (data.Version < CURRENT_DATA_VERSION)
    {
        MigrateData(data.Version);
        data.Version = CURRENT_DATA_VERSION;
        SaveData();
    }
}

private void MigrateData(int fromVersion)
{
    Puts($"Migrating data from v{fromVersion} to v{CURRENT_DATA_VERSION}");
    
    if (fromVersion < 2)
    {
        // Migration logic for v1 → v2
        foreach (var pd in data.Players.Values)
        {
            pd.NewField = ConvertOldField(pd.OldField);
        }
    }
    
    if (fromVersion < 3)
    {
        // Migration logic for v2 → v3
    }
}
```

---

## CONNECTION STATE ERROR HANDLING

### Validate Before Network Operations
```csharp
private void SendToPlayer(BasePlayer player, string message)
{
    if (player == null)
    {
        LogDebug("SendToPlayer: player is null");
        return;
    }
    
    if (!player.IsConnected)
    {
        LogDebug($"SendToPlayer: {player.displayName} not connected");
        return;
    }
    
    if (player.net?.connection == null)
    {
        LogDebug($"SendToPlayer: {player.displayName} has no connection");
        return;
    }
    
    player.ChatMessage(message);
}
```

### Delayed Operation Safety
```csharp
private void DoDelayedAction(BasePlayer player, Action<BasePlayer> action)
{
    var userId = player.userID;
    
    timer.Once(2f, () =>
    {
        // Re-fetch player
        var p = BasePlayer.FindByID(userId);
        
        if (p == null)
        {
            LogDebug($"Delayed action: player {userId} no longer exists");
            return;
        }
        
        if (!p.IsConnected)
        {
            LogDebug($"Delayed action: player {p.displayName} disconnected");
            return;
        }
        
        try
        {
            action(p);
        }
        catch (Exception ex)
        {
            PrintError($"Delayed action failed for {p.displayName}: {ex.Message}");
        }
    });
}
```

---

## ENTITY VALIDITY CHECKS

### Complete Entity Validation
```csharp
private bool IsValidEntity(BaseEntity entity)
{
    if (entity == null) return false;
    if (entity.IsDestroyed) return false;
    if (entity.net == null) return false;
    return true;
}

private bool IsValidPlayer(BasePlayer player)
{
    if (player == null) return false;
    if (!player.IsConnected) return false;
    if (player.IsDead()) return false;
    return true;
}

// Usage
private void ProcessEntity(BaseEntity entity)
{
    if (!IsValidEntity(entity)) return;
    // Safe to use entity
}
```

---

## ERROR HANDLING CHECKLIST

```
□ External calls wrapped in try/catch with logging
□ Null checks before all pointer access
□ IsConnected checked before player operations
□ IsDestroyed checked before entity operations
□ Data loading has corruption recovery
□ Error messages are user-friendly
□ Debug mode available for troubleshooting
□ Graceful degradation for optional features
□ Required dependencies checked at startup
□ Timer callbacks validate all state
```
