# OXIDE PERFORMANCE BENCHMARKING

**Performance measurement and optimization tools for Oxide plugins.**

---

## HOOK EXECUTION TIMING

### Built-in Timing
```csharp
private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
{
    var sw = System.Diagnostics.Stopwatch.StartNew();
    
    // Your hook logic here
    ProcessDamage(entity, info);
    
    sw.Stop();
    if (sw.ElapsedMilliseconds > 1) // Log if > 1ms
        Puts($"OnEntityTakeDamage took {sw.ElapsedMilliseconds}ms");
}
```

### Configurable Performance Logging
```csharp
private class Configuration
{
    [JsonProperty("Enable Performance Logging")]
    public bool LogPerformance { get; set; } = false;
    
    [JsonProperty("Performance Warning Threshold (ms)")]
    public float WarningThreshold { get; set; } = 5f;
}

private void LogTiming(string operation, System.Diagnostics.Stopwatch sw)
{
    if (!config.LogPerformance) return;
    
    var ms = sw.Elapsed.TotalMilliseconds;
    if (ms >= config.WarningThreshold)
        PrintWarning($"[PERF] {operation}: {ms:F2}ms");
}
```

---

## MEMORY USAGE MONITORING

### Track Collection Sizes
```csharp
[ChatCommand("pluginstats")]
private void CmdStats(BasePlayer player, string cmd, string[] args)
{
    if (!player.IsAdmin) return;
    
    SendReply(player, "=== Plugin Memory Stats ===");
    SendReply(player, $"Tracked Players: {playerData.Count}");
    SendReply(player, $"Active Timers: {activeTimers.Count}");
    SendReply(player, $"Spawned Entities: {spawnedEntities.Count}");
    SendReply(player, $"Cached Items: {itemCache.Count}");
    
    // Estimate memory
    long estimated = 0;
    estimated += playerData.Count * 200; // ~200 bytes per player data
    estimated += spawnedEntities.Count * 50; // ~50 bytes per tracked entity
    
    SendReply(player, $"Estimated Memory: {estimated / 1024}KB");
}
```

### GC Allocation Tracking
```csharp
// Track allocations in hot paths
private List<BaseEntity> entityBuffer = new List<BaseEntity>();

// ❌ BAD: Allocates new list every call
private void FindEntitiesBad(Vector3 pos, float radius)
{
    var entities = new List<BaseEntity>(); // GC pressure!
    Vis.Entities(pos, radius, entities);
}

// ✅ GOOD: Reuses buffer
private void FindEntitiesGood(Vector3 pos, float radius)
{
    entityBuffer.Clear();
    Vis.Entities(pos, radius, entityBuffer);
}
```

---

## ENTITY COUNT IMPACT

### Measure Entity Processing
```csharp
[ConsoleCommand("plugin.entitytest")]
private void CmdEntityTest(ConsoleSystem.Arg arg)
{
    var sw = System.Diagnostics.Stopwatch.StartNew();
    
    int total = 0;
    int processed = 0;
    
    foreach (var entity in BaseNetworkable.serverEntities)
    {
        total++;
        if (ShouldProcess(entity))
        {
            processed++;
            ProcessEntity(entity);
        }
    }
    
    sw.Stop();
    
    Puts($"Entity Processing Results:");
    Puts($"  Total Entities: {total}");
    Puts($"  Processed: {processed}");
    Puts($"  Time: {sw.ElapsedMilliseconds}ms");
    Puts($"  Per Entity: {sw.Elapsed.TotalMilliseconds / processed:F4}ms");
}
```

### Entity Type Distribution
```csharp
[ConsoleCommand("plugin.entitydist")]
private void CmdEntityDist(ConsoleSystem.Arg arg)
{
    var counts = new Dictionary<string, int>();
    
    foreach (var entity in BaseNetworkable.serverEntities)
    {
        var typeName = entity.GetType().Name;
        counts.TryGetValue(typeName, out int count);
        counts[typeName] = count + 1;
    }
    
    Puts("Entity Type Distribution:");
    foreach (var kvp in counts.OrderByDescending(x => x.Value).Take(20))
    {
        Puts($"  {kvp.Key}: {kvp.Value}");
    }
}
```

---

## PLAYER COUNT SCALING

### Test With Player Load
```csharp
[ConsoleCommand("plugin.playerscale")]
private void CmdPlayerScale(ConsoleSystem.Arg arg)
{
    int playerCount = BasePlayer.activePlayerList.Count;
    
    var sw = System.Diagnostics.Stopwatch.StartNew();
    
    foreach (var player in BasePlayer.activePlayerList)
    {
        ProcessPlayer(player);
    }
    
    sw.Stop();
    
    Puts($"Player Processing ({playerCount} players):");
    Puts($"  Total Time: {sw.ElapsedMilliseconds}ms");
    Puts($"  Per Player: {sw.ElapsedMilliseconds / (float)playerCount:F2}ms");
    
    // Project scaling
    Puts($"  Projected 50 players: {(sw.ElapsedMilliseconds / (float)playerCount) * 50:F0}ms");
    Puts($"  Projected 100 players: {(sw.ElapsedMilliseconds / (float)playerCount) * 100:F0}ms");
}
```

---

## TIMER OVERHEAD MEASUREMENT

### Timer Performance Test
```csharp
private int timerCallCount = 0;
private float timerStartTime;

[ChatCommand("timertest")]
private void CmdTimerTest(BasePlayer player, string cmd, string[] args)
{
    if (!player.IsAdmin) return;
    
    int count = args.Length > 0 ? int.Parse(args[0]) : 100;
    timerCallCount = 0;
    timerStartTime = Time.realtimeSinceStartup;
    
    for (int i = 0; i < count; i++)
    {
        timer.Once(0.1f, () =>
        {
            timerCallCount++;
            if (timerCallCount >= count)
            {
                float elapsed = Time.realtimeSinceStartup - timerStartTime;
                Puts($"Timer Test Complete:");
                Puts($"  Timers: {count}");
                Puts($"  Actual Time: {elapsed:F2}s");
                Puts($"  Expected: ~0.1s");
            }
        });
    }
    
    SendReply(player, $"Scheduled {count} timers");
}
```

---

## UI RENDERING PERFORMANCE

### UI Update Timing
```csharp
private System.Diagnostics.Stopwatch uiStopwatch = new();

private void UpdateUI(BasePlayer player)
{
    uiStopwatch.Restart();
    
    // Build UI
    var container = BuildUIContainer(player);
    
    // Destroy old
    CuiHelper.DestroyUi(player, PANEL);
    
    // Add new
    CuiHelper.AddUi(player, container);
    
    uiStopwatch.Stop();
    
    if (config.LogPerformance && uiStopwatch.ElapsedMilliseconds > 2)
        Puts($"UI Update for {player.displayName}: {uiStopwatch.ElapsedMilliseconds}ms");
}
```

### UI Element Count Impact
```csharp
[ChatCommand("uitest")]
private void CmdUITest(BasePlayer player, string cmd, string[] args)
{
    if (!player.IsAdmin) return;
    
    int elementCount = args.Length > 0 ? int.Parse(args[0]) : 50;
    
    var sw = System.Diagnostics.Stopwatch.StartNew();
    
    var container = new CuiElementContainer();
    container.Add(new CuiPanel
    {
        Image = { Color = "0 0 0 0.8" },
        RectTransform = { AnchorMin = "0.2 0.2", AnchorMax = "0.8 0.8" }
    }, "Overlay", "TestPanel");
    
    for (int i = 0; i < elementCount; i++)
    {
        float y = 0.9f - (i * 0.02f);
        container.Add(new CuiLabel
        {
            Text = { Text = $"Element {i}", FontSize = 10 },
            RectTransform = { AnchorMin = $"0.1 {y - 0.02f}", AnchorMax = $"0.9 {y}" }
        }, "TestPanel");
    }
    
    CuiHelper.AddUi(player, container);
    
    sw.Stop();
    SendReply(player, $"Created UI with {elementCount} elements in {sw.ElapsedMilliseconds}ms");
    
    timer.Once(5f, () => CuiHelper.DestroyUi(player, "TestPanel"));
}
```

---

## NETWORK UPDATE FREQUENCY

### Track Network Updates
```csharp
private Dictionary<NetworkableId, int> updateCounts = new();
private float trackingStartTime;

[ChatCommand("trackupdates")]
private void CmdTrackUpdates(BasePlayer player, string cmd, string[] args)
{
    if (!player.IsAdmin) return;
    
    if (args.Length > 0 && args[0] == "stop")
    {
        float elapsed = Time.realtimeSinceStartup - trackingStartTime;
        int total = updateCounts.Values.Sum();
        
        SendReply(player, $"Network Update Stats ({elapsed:F0}s):");
        SendReply(player, $"  Total Updates: {total}");
        SendReply(player, $"  Updates/sec: {total / elapsed:F1}");
        
        updateCounts.Clear();
        Unsubscribe(nameof(OnNetworkMessage));
        return;
    }
    
    updateCounts.Clear();
    trackingStartTime = Time.realtimeSinceStartup;
    Subscribe(nameof(OnNetworkMessage));
    
    SendReply(player, "Tracking network updates. Use /trackupdates stop to see results.");
}
```

---

## PERFORMANCE TARGETS

### Acceptable Thresholds
```
Hook Execution:
  OnPlayerConnected: < 10ms
  OnEntitySpawned: < 5ms
  OnEntityTakeDamage: < 1ms (called very frequently)
  OnPlayerInput: < 0.5ms (called every frame)

UI Operations:
  Simple UI: < 5ms
  Complex UI (50+ elements): < 20ms
  UI Destroy: < 1ms

Data Operations:
  Save (100 players): < 50ms
  Load (100 players): < 100ms

Correlation Guidelines:
  100 players → target < 100ms total per tick
  Server tick = 32/sec → 31.25ms per tick
  Plugin should use < 10% of tick time = ~3ms
```

### Performance Checklist
```
□ All hooks complete in < 1ms normally
□ OnPlayerInput completes in < 0.5ms
□ UI updates complete in < 10ms
□ No GC allocations in hot paths
□ Collections reused, not recreated
□ Heavy operations spread across frames (coroutines)
□ Expensive hooks unsubscribed when not needed
□ Entity lookups use spatial queries, not iteration
□ Data saves batched, not per-change
```

---

## BENCHMARK COMMAND TEMPLATE

```csharp
[ConsoleCommand("plugin.benchmark")]
private void CmdBenchmark(ConsoleSystem.Arg arg)
{
    Puts("=== Plugin Benchmark ===");
    
    // Memory
    Puts($"[Memory]");
    Puts($"  Player Data: {playerData.Count} entries");
    Puts($"  Entity Cache: {entityCache.Count} entries");
    Puts($"  Active Timers: {activeTimers.Count}");
    
    // Performance
    Puts($"[Performance]");
    var sw = System.Diagnostics.Stopwatch.StartNew();
    
    // Test critical operation
    for (int i = 0; i < 1000; i++)
        TestOperation();
    
    sw.Stop();
    Puts($"  1000 operations: {sw.ElapsedMilliseconds}ms");
    Puts($"  Per operation: {sw.Elapsed.TotalMicroseconds / 1000:F2}μs");
    
    // Entity stats
    Puts($"[Entities]");
    Puts($"  Total Server Entities: {BaseNetworkable.serverEntities.Count}");
    Puts($"  Active Players: {BasePlayer.activePlayerList.Count}");
    Puts($"  Sleeping Players: {BasePlayer.sleepingPlayerList.Count}");
}
```
