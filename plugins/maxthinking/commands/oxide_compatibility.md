# OXIDE VERSION COMPATIBILITY

**Version compatibility checks and cross-version support patterns.**

---

## COMPATIBILITY MATRIX

### Rust Game Updates
```
Rust updates frequently with breaking changes.
Key areas that change:
- Prefab paths (vehicles, NPCs, deployables)
- Entity class names and hierarchies
- Hook parameters
- Method signatures
- Property names
```

### Check Current Version
```csharp
private void OnServerInitialized()
{
    // Get Rust build info
    var buildNumber = BuildInfo.Current.BuildNumber;
    var scmBranch = BuildInfo.Current.Scm.Branch;
    var scmDate = BuildInfo.Current.Scm.Date;
    
    Puts($"Rust Build: {buildNumber}");
    Puts($"Branch: {scmBranch}");
    Puts($"Date: {scmDate}");
    
    // Get Oxide version
    var oxideVersion = typeof(RustPlugin).Assembly.GetName().Version;
    Puts($"Oxide: {oxideVersion}");
    
    // Check protocol
    var protocol = Rust.Protocol.network;
    Puts($"Protocol: {protocol}");
}
```

---

## BREAKING CHANGE DETECTION

### Safe Method Check
```csharp
private bool MethodExists(Type type, string methodName)
{
    return type.GetMethod(methodName) != null;
}

private void OnServerInitialized()
{
    // Check if new API method exists
    if (!MethodExists(typeof(BasePlayer), "GetBuildingPrivilege"))
    {
        PrintError("This version of Rust doesn't support GetBuildingPrivilege!");
        Interface.Oxide.UnloadPlugin(Name);
        return;
    }
}
```

### Prefab Existence Check
```csharp
private bool PrefabExists(string prefab)
{
    return GameManifest.pathToGuid.ContainsKey(prefab);
}

private string GetVehiclePrefab(string type)
{
    var prefabs = new Dictionary<string, string[]>
    {
        ["minicopter"] = new[]
        {
            "assets/content/vehicles/minicopter/minicopter.entity.prefab",
            "assets/prefabs/vehicles/minicopter/minicopter.entity.prefab" // Old path
        },
        ["scrapheli"] = new[]
        {
            "assets/content/vehicles/scrap heli carrier/scraptransporthelicopter.prefab"
        }
    };
    
    if (!prefabs.TryGetValue(type, out var paths))
        return null;
    
    foreach (var path in paths)
    {
        if (PrefabExists(path))
            return path;
    }
    
    PrintWarning($"No valid prefab found for vehicle type: {type}");
    return null;
}
```

---

## FALLBACK PATTERNS

### Item Definition Fallback
```csharp
private ItemDefinition FindItem(params string[] shortnames)
{
    foreach (var name in shortnames)
    {
        var def = ItemManager.FindItemDefinition(name);
        if (def != null)
            return def;
    }
    return null;
}

// Usage - handles renamed items
var ammo = FindItem("ammo.rifle", "ammo.rifle.basic"); // Try new name first
```

### Method Fallback
```csharp
private void TeleportPlayer(BasePlayer player, Vector3 position)
{
    // Try new method first
    try
    {
        player.Teleport(position);
    }
    catch
    {
        // Fallback to old method
        player.transform.position = position;
        player.SendNetworkUpdateImmediate();
    }
}
```

### Property Fallback
```csharp
private ulong GetEntityOwner(BaseEntity entity)
{
    // Modern property
    if (entity is IOwned owned)
        return owned.OwnerID;
    
    // Legacy - direct property
    try
    {
        return entity.OwnerID;
    }
    catch
    {
        return 0;
    }
}
```

---

## VERSION-CONDITIONAL CODE

### Compile-Time Conditions
```csharp
// Use preprocessor directives for major version differences
#if RUST_2023
    player.NewMethod();
#else
    LegacyMethod(player);
#endif
```

### Runtime Version Check
```csharp
private static bool IsRustVersionAtLeast(int minBuild)
{
    return BuildInfo.Current.BuildNumber >= minBuild;
}

private void DoVersionSpecificAction(BasePlayer player)
{
    if (IsRustVersionAtLeast(12345))
    {
        // New behavior for recent builds
        UseNewFeature(player);
    }
    else
    {
        // Legacy behavior for older builds
        UseLegacyFeature(player);
    }
}
```

---

## COMMON PLUGIN DEPENDENCIES

### Required Versions
```csharp
private void OnServerInitialized()
{
    // Check ImageLibrary version
    if (ImageLibrary != null)
    {
        var version = ImageLibrary.Version;
        if (version < new VersionNumber(2, 0, 0))
        {
            PrintWarning("ImageLibrary 2.0.0+ required for skin support");
        }
    }
    
    // Check Economics version
    if (Economics != null)
    {
        // Test if new API exists
        try
        {
            var balance = Economics.Call<double>("Balance", "0");
        }
        catch
        {
            PrintWarning("Incompatible Economics version");
            Economics = null;
        }
    }
}
```

### Soft Dependencies
```csharp
[PluginReference] private Plugin ZoneManager;
[PluginReference] private Plugin Clans;
[PluginReference] private Plugin Friends;

private bool AreFriends(ulong player1, ulong player2)
{
    // Try Clans first
    if (Clans != null)
    {
        var clan1 = Clans.Call<string>("GetClanOf", player1);
        var clan2 = Clans.Call<string>("GetClanOf", player2);
        if (!string.IsNullOrEmpty(clan1) && clan1 == clan2)
            return true;
    }
    
    // Try Friends
    if (Friends != null)
    {
        if (Friends.Call<bool>("AreFriends", player1, player2))
            return true;
    }
    
    // No friend system available or not friends
    return false;
}
```

---

## FORWARD COMPATIBILITY

### Defensive Null Checks
```csharp
// Always use null-conditional for new properties
var value = entity?.NewProperty?.SubProperty ?? defaultValue;

// Use pattern matching for type checks
if (entity is NewEntityType newEntity)
{
    newEntity.NewMethod();
}
else if (entity is BaseEntity baseEntity)
{
    // Fallback for older types
}
```

### Interface-Based Programming
```csharp
// Check for interface implementation rather than concrete type
if (entity is IContainerEntity container)
{
    // Works with any container type, present or future
    var items = container.inventory.itemList;
}
```

### Optional Features
```csharp
private class Configuration
{
    [JsonProperty("Enable New Feature (requires Rust 2024+)")]
    public bool EnableNewFeature { get; set; } = false;
}

private void UseOptionalFeature(BasePlayer player)
{
    if (!config.EnableNewFeature)
        return;
    
    if (!IsRustVersionAtLeast(REQUIRED_BUILD))
    {
        PrintWarning("New feature requires Rust 2024+ - disabled");
        config.EnableNewFeature = false;
        SaveConfig();
        return;
    }
    
    // Use new feature
}
```

---

## COMPATIBILITY CHECKLIST

### Before Release
```
□ Tested on current Rust version
□ Checked for deprecated method warnings
□ Verified all prefabs exist
□ Verified all item shortnames valid
□ Tested with/without optional dependencies
□ Added fallbacks for new features
□ Documented minimum Rust version
□ Documented plugin dependencies
```

### Version Documentation Template
```csharp
// Plugin header
/*
 * Compatibility:
 * - Minimum Rust Build: 12345
 * - Minimum Oxide Version: 2.0.5935
 * 
 * Dependencies:
 * - Required: ImageLibrary 2.0.0+
 * - Optional: Economics, ServerRewards, Clans
 * 
 * Known Issues:
 * - Rust build < 12300: Vehicle spawning may fail
 * - Without Economics: Shop features disabled
 */
```

---

## DEPRECATION HANDLING

### Log Deprecation Warnings
```csharp
private Dictionary<string, bool> deprecationWarnings = new();

private void WarnDeprecated(string feature, string replacement)
{
    if (deprecationWarnings.ContainsKey(feature))
        return;
    
    deprecationWarnings[feature] = true;
    PrintWarning($"[DEPRECATED] {feature} - Use {replacement} instead");
}

// Usage in old config migration
if (config.OldSetting != default)
{
    WarnDeprecated("OldSetting", "NewSetting");
    config.NewSetting = config.OldSetting;
}
```
