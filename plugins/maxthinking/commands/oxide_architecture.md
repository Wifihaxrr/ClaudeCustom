# OXIDE PLUGIN ARCHITECTURE PATTERNS

**Expert-level architecture patterns for Rust Oxide/uMod plugin development.**

---

## PLUGIN STRUCTURE PATTERNS

### Pattern 1: Simple Command Plugin
**Use when:** Single-purpose plugins with 1-3 commands, minimal state.

```csharp
namespace Oxide.Plugins
{
    [Info("PluginName", "Author", "1.0.0")]
    [Description("Brief description")]
    class PluginName : RustPlugin
    {
        private const string PERM = "pluginname.use";
        
        private void Init() => permission.RegisterPermission(PERM, this);
        
        [ChatCommand("cmd")]
        private void CmdHandler(BasePlayer player, string cmd, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, PERM)) return;
            // Logic here
        }
    }
}
```

---

### Pattern 2: Configurable Plugin
**Use when:** User-customizable behavior needed.

```csharp
namespace Oxide.Plugins
{
    [Info("ConfigPlugin", "Author", "1.0.0")]
    class ConfigPlugin : RustPlugin
    {
        private Configuration config;
        
        private class Configuration
        {
            [JsonProperty("Setting Name")]
            public float Value { get; set; } = 1.0f;
            
            [JsonProperty("Nested Settings")]
            public NestedConfig Nested { get; set; } = new NestedConfig();
        }
        
        private class NestedConfig
        {
            [JsonProperty("Sub Setting")]
            public bool Enabled { get; set; } = true;
        }
        
        protected override void LoadDefaultConfig() => config = new Configuration();
        
        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) throw new Exception();
            }
            catch { LoadDefaultConfig(); }
            SaveConfig();
        }
        
        protected override void SaveConfig() => Config.WriteObject(config, true);
    }
}
```

---

### Pattern 3: Data-Persistent Plugin
**Use when:** Player data needs to survive server restarts.

```csharp
namespace Oxide.Plugins
{
    [Info("DataPlugin", "Author", "1.0.0")]
    class DataPlugin : RustPlugin
    {
        private StoredData data;
        
        private class StoredData
        {
            public Dictionary<ulong, PlayerData> Players { get; set; } = new();
        }
        
        private class PlayerData
        {
            public int Score { get; set; }
            public float LastPlayed { get; set; }
        }
        
        private void Init() => LoadData();
        private void OnServerSave() => SaveData();
        private void Unload() => SaveData();
        
        private void LoadData()
        {
            data = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name) ?? new StoredData();
        }
        
        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject(Name, data);
        }
        
        private PlayerData GetPlayerData(ulong id)
        {
            if (!data.Players.TryGetValue(id, out var pd))
            {
                pd = new PlayerData();
                data.Players[id] = pd;
            }
            return pd;
        }
    }
}
```

---

### Pattern 4: Entity Tracking Plugin
**Use when:** Managing spawned entities, deployables, or NPCs.

```csharp
namespace Oxide.Plugins
{
    [Info("EntityTracker", "Author", "1.0.0")]
    class EntityTracker : RustPlugin
    {
        private HashSet<NetworkableId> trackedEntities = new();
        
        private void Unload()
        {
            foreach (var id in trackedEntities.ToList())
            {
                var entity = BaseNetworkable.serverEntities.Find(id);
                entity?.Kill();
            }
            trackedEntities.Clear();
        }
        
        private BaseEntity SpawnTrackedEntity(string prefab, Vector3 pos)
        {
            var entity = GameManager.server.CreateEntity(prefab, pos);
            if (entity == null) return null;
            
            entity.Spawn();
            trackedEntities.Add(entity.net.ID);
            return entity;
        }
        
        private void OnEntityKill(BaseNetworkable entity)
        {
            if (entity?.net != null)
                trackedEntities.Remove(entity.net.ID);
        }
    }
}
```

---

### Pattern 5: UI-Heavy Plugin
**Use when:** Complex UI with multiple panels, pages, or states.

```csharp
namespace Oxide.Plugins
{
    [Info("UIPlugin", "Author", "1.0.0")]
    class UIPlugin : RustPlugin
    {
        private const string MAIN_PANEL = "UIPlugin_Main";
        private const string SUB_PANEL = "UIPlugin_Sub";
        
        private HashSet<ulong> openUI = new();
        
        private void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
                DestroyAllUI(player);
        }
        
        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            openUI.Remove(player.userID);
        }
        
        private void DestroyAllUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, MAIN_PANEL);
            CuiHelper.DestroyUi(player, SUB_PANEL);
            openUI.Remove(player.userID);
        }
        
        private void ShowMainUI(BasePlayer player)
        {
            DestroyAllUI(player);
            
            var container = new CuiElementContainer();
            
            // Background panel
            container.Add(new CuiPanel
            {
                Image = { Color = "0.1 0.1 0.1 0.95" },
                RectTransform = { AnchorMin = "0.2 0.2", AnchorMax = "0.8 0.8" },
                CursorEnabled = true
            }, "Overlay", MAIN_PANEL);
            
            // Close button (always include!)
            container.Add(new CuiButton
            {
                Button = { Color = "0.8 0.2 0.2 1", Command = "uiplugin.close" },
                RectTransform = { AnchorMin = "0.9 0.9", AnchorMax = "0.98 0.98" },
                Text = { Text = "X", Align = TextAnchor.MiddleCenter }
            }, MAIN_PANEL);
            
            CuiHelper.AddUi(player, container);
            openUI.Add(player.userID);
        }
        
        [ConsoleCommand("uiplugin.close")]
        private void CmdClose(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player != null) DestroyAllUI(player);
        }
    }
}
```

---

## CROSS-PLUGIN COMMUNICATION

### Calling Other Plugins
```csharp
[PluginReference] private Plugin Economics;
[PluginReference] private Plugin ServerRewards;
[PluginReference] private Plugin Clans;

// Safe call pattern - ALWAYS check for null
private double GetBalance(ulong playerId)
{
    return Economics?.Call<double>("Balance", playerId.ToString()) ?? 0;
}

private bool TryWithdraw(ulong playerId, double amount)
{
    if (Economics == null) return false;
    return Economics.Call<bool>("Withdraw", playerId.ToString(), amount);
}
```

### Exposing API for Other Plugins
```csharp
// Other plugins call: MyPlugin.Call<int>("GetPlayerScore", playerId);
private int GetPlayerScore(ulong playerId)
{
    return GetPlayerData(playerId).Score;
}

// Use [HookMethod] for non-private methods
[HookMethod("AddPlayerScore")]
public void AddPlayerScore(ulong playerId, int amount)
{
    GetPlayerData(playerId).Score += amount;
}
```

---

## STATE MANAGEMENT

### Player State Tracking
```csharp
private Dictionary<ulong, PlayerState> playerStates = new();

private class PlayerState
{
    public bool FeatureEnabled { get; set; }
    public Vector3 LastPosition { get; set; }
    public float Cooldown { get; set; }
}

private PlayerState GetState(BasePlayer player)
{
    if (!playerStates.TryGetValue(player.userID, out var state))
    {
        state = new PlayerState();
        playerStates[player.userID] = state;
    }
    return state;
}

private void OnPlayerDisconnected(BasePlayer player, string reason)
{
    playerStates.Remove(player.userID);
}

private void Unload()
{
    playerStates.Clear();
}
```

### Cooldown Management
```csharp
private Dictionary<ulong, float> cooldowns = new();

private bool IsOnCooldown(BasePlayer player, float cooldownSeconds)
{
    if (!cooldowns.TryGetValue(player.userID, out float lastUse))
        return false;
    
    return Time.realtimeSinceStartup - lastUse < cooldownSeconds;
}

private float GetRemainingCooldown(BasePlayer player, float cooldownSeconds)
{
    if (!cooldowns.TryGetValue(player.userID, out float lastUse))
        return 0;
    
    return Mathf.Max(0, cooldownSeconds - (Time.realtimeSinceStartup - lastUse));
}

private void SetCooldown(BasePlayer player)
{
    cooldowns[player.userID] = Time.realtimeSinceStartup;
}
```

---

## MODULAR DESIGN GUIDELINES

1. **Single Responsibility**: Each method does ONE thing
2. **Private by Default**: Only expose what's necessary
3. **Fail Gracefully**: Always handle null/missing data
4. **Clean Unload**: Release ALL resources in Unload()
5. **Defensive Coding**: Check player.IsConnected before delayed actions

---

## ANTI-PATTERNS TO AVOID

```csharp
// ❌ BAD: Storing BasePlayer references
private Dictionary<ulong, BasePlayer> players; // Players can disconnect!

// ✅ GOOD: Store userID, lookup when needed
private HashSet<ulong> enabledPlayers;
var player = BasePlayer.FindByID(userId);
if (player != null && player.IsConnected) { }

// ❌ BAD: Not cleaning up in Unload
private void Unload() { } // Empty unload!

// ✅ GOOD: Clean everything
private void Unload()
{
    foreach (var player in BasePlayer.activePlayerList)
        CuiHelper.DestroyUi(player, PANEL);
    foreach (var timer in timers.Values)
        timer?.Destroy();
    foreach (var entity in trackedEntities)
        entity?.Kill();
}

// ❌ BAD: Blocking operations in hooks
void OnPlayerConnected(BasePlayer player)
{
    Thread.Sleep(1000); // NEVER DO THIS
    WebRequest.SendSync(...); // NEVER DO THIS
}

// ✅ GOOD: Use async patterns
void OnPlayerConnected(BasePlayer player)
{
    timer.Once(1f, () => DelayedSetup(player));
    webrequest.Enqueue(url, null, callback, this);
}
```
