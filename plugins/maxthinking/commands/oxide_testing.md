# OXIDE TESTING FRAMEWORK

**Testing patterns and verification approaches for Oxide plugins.**

---

## MENTAL COMPILATION CHECKLIST

Before delivering any plugin, verify:

### Syntax Check
```
□ All brackets balanced { } [ ] ( )
□ All statements end with semicolons
□ All strings properly quoted
□ No missing commas in collections/parameters
□ Class names match file names
```

### API Check
```
□ No LogWarning() - use PrintWarning()
□ No LogError() - use PrintError()
□ No Debug.Log() - use Puts()
□ Permissions use player.UserIDString
□ All hook signatures are correct
```

### Structure Check
```
□ Has [Info] and [Description] attributes
□ Inherits from RustPlugin
□ Init() registers permissions
□ Unload() cleans up everything
□ Proper namespace: Oxide.Plugins
```

---

## RCON COMMAND TESTING

### Test Commands via RCON
```
# Load/Reload plugin
oxide.reload PluginName

# Check for errors
oxide.plugins

# Grant permission for testing
oxide.grant user PlayerName pluginname.use
oxide.grant group default pluginname.use

# Revoke permission
oxide.revoke user PlayerName pluginname.use

# Check permissions
oxide.show user PlayerName
oxide.show group default
```

### Console Command Simulation
```csharp
// Test console commands programmatically
[ConsoleCommand("myplugin.test")]
private void CmdTest(ConsoleSystem.Arg arg)
{
    // From RCON: myplugin.test arg1 arg2
    var args = arg.Args ?? new string[0];
    
    // arg.Player() returns null from RCON
    var player = arg.Player();
    
    if (player == null)
        Puts($"Called from RCON with args: {string.Join(", ", args)}");
    else
        SendReply(player, $"Called from console");
}
```

---

## PERMISSION TESTING

### Permission Test Sequence
```
1. Test WITHOUT permission:
   - Run /command as non-admin
   - Verify "No permission" message
   
2. Grant permission:
   - oxide.grant user TestPlayer pluginname.use
   
3. Test WITH permission:
   - Run /command as TestPlayer
   - Verify command works
   
4. Test admin bypass (if applicable):
   - Run /command as admin
   - Verify works without explicit permission
```

### Permission Test Code
```csharp
[ChatCommand("testperm")]
private void CmdTestPerm(BasePlayer player, string cmd, string[] args)
{
    SendReply(player, $"IsAdmin: {player.IsAdmin}");
    SendReply(player, $"HasPerm: {permission.UserHasPermission(player.UserIDString, PERM_USE)}");
    
    foreach (var perm in permission.GetUserPermissions(player.UserIDString))
        SendReply(player, $"  Permission: {perm}");
}
```

---

## CONFIG TESTING

### Config Reload Test
```csharp
[ChatCommand("reloadconfig")]
private void CmdReloadConfig(BasePlayer player, string cmd, string[] args)
{
    if (!player.IsAdmin) return;
    
    LoadConfig();
    SendReply(player, "Config reloaded!");
    SendReply(player, $"Test value: {config.TestValue}");
}
```

### Config Generation Test
```
1. Delete oxide/config/PluginName.json
2. Load plugin: oxide.reload PluginName
3. Verify config file created with defaults
4. Modify config values
5. Reload plugin
6. Verify changes applied
```

---

## DATA PERSISTENCE TESTING

### Data Round-Trip Test
```csharp
[ChatCommand("testdata")]
private void CmdTestData(BasePlayer player, string cmd, string[] args)
{
    if (!player.IsAdmin) return;
    
    // Write test value
    var testValue = UnityEngine.Random.Range(0, 1000);
    data.TestValue = testValue;
    SaveData();
    
    SendReply(player, $"Saved: {testValue}");
    
    // Clear and reload
    data = null;
    LoadData();
    
    SendReply(player, $"Loaded: {data.TestValue}");
    SendReply(player, $"Match: {data.TestValue == testValue}");
}
```

### Data Corruption Recovery Test
```
1. Save valid data
2. Manually corrupt oxide/data/PluginName.json
3. Reload plugin
4. Verify graceful recovery or fresh start
5. Verify error logged
```

---

## MULTI-PLAYER SCENARIO TESTING

### Player Interaction Tests
```
Test: Player A and Player B interaction
1. Player A performs action
2. Verify Player B sees/receives effect
3. Player B performs counter-action
4. Verify states update correctly for both
```

### Disconnection Tests
```
Test: Player disconnects mid-action
1. Player starts action with timer
2. Player disconnects
3. Verify timer callback handles null/disconnected player
4. Verify no errors in console
5. Player reconnects
6. Verify state is correct
```

---

## EDGE CASE TEST PATTERNS

### Null Entity Tests
```csharp
// Test hook with destroyed entity
private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
{
    if (entity == null) return;           // Test this path
    if (entity.IsDestroyed) return;       // Test this path
    
    var player = entity.ToPlayer();
    if (player == null) return;           // Test with non-player entities
    
    // Main logic
}
```

### Empty Collection Tests
```csharp
[ChatCommand("emptytest")]
private void CmdEmptyTest(BasePlayer player, string cmd, string[] args)
{
    // Test with no args
    if (args.Length == 0)
    {
        SendReply(player, "No args provided");
        return;
    }
    
    // Test with empty storage
    var box = FindNearbyBox(player);
    if (box == null || box.inventory.itemList.Count == 0)
    {
        SendReply(player, "No items in box");
        return;
    }
}
```

### Boundary Value Tests
```csharp
// Test with minimum values
config.Multiplier = 0.0f;  // Does plugin handle 0?
config.Multiplier = -1.0f; // Does plugin handle negative?

// Test with maximum values
config.MaxItems = int.MaxValue;  // Overflow?
config.Range = float.MaxValue;   // Infinite range?

// Test with special values
config.PlayerID = 0;        // Invalid ID
config.ItemName = "";       // Empty string
config.ItemName = null;     // Null string
```

---

## AUTOMATED TEST HELPERS

### Debug Command Template
```csharp
#if DEBUG
[ChatCommand("debugtest")]
private void CmdDebug(BasePlayer player, string cmd, string[] args)
{
    if (!player.IsAdmin) return;
    
    switch (args.Length > 0 ? args[0] : "")
    {
        case "spawn":
            TestSpawn(player);
            break;
        case "ui":
            TestUI(player);
            break;
        case "data":
            TestData(player);
            break;
        case "cleanup":
            TestCleanup(player);
            break;
        default:
            SendReply(player, "Usage: /debugtest <spawn|ui|data|cleanup>");
            break;
    }
}

private void TestSpawn(BasePlayer player)
{
    var entity = SpawnTestEntity(player.transform.position);
    SendReply(player, $"Spawned: {entity?.net.ID}");
}

private void TestUI(BasePlayer player)
{
    ShowTestUI(player);
    timer.Once(3f, () => DestroyTestUI(player));
    SendReply(player, "UI shown for 3 seconds");
}
#endif
```

### Stress Test Pattern
```csharp
[ChatCommand("stresstest")]
private void CmdStress(BasePlayer player, string cmd, string[] args)
{
    if (!player.IsAdmin) return;
    
    int count = args.Length > 0 ? int.Parse(args[0]) : 100;
    
    var sw = System.Diagnostics.Stopwatch.StartNew();
    
    for (int i = 0; i < count; i++)
    {
        // Your operation here
        ProcessItem(i);
    }
    
    sw.Stop();
    SendReply(player, $"Completed {count} operations in {sw.ElapsedMilliseconds}ms");
    SendReply(player, $"Average: {sw.ElapsedMilliseconds / (float)count:F3}ms per operation");
}
```

---

## TEST VERIFICATION CHECKLIST

### Before Release
```
□ Plugin loads without errors
□ Plugin unloads without errors
□ Hot reload (oxide.reload) works
□ Config generates correctly
□ Config changes apply after reload
□ Data saves and loads correctly
□ All chat commands work
□ All console commands work
□ Permissions block unauthorized users
□ Permissions allow authorized users
□ Player disconnect doesn't cause errors
□ Timer callbacks are null-safe
□ UI shows and destroys properly
□ No memory leaks after extended use
```
