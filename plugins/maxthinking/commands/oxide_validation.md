# OXIDE PLUGIN VALIDATION SUITE

**Pre-release validation checklist and automated verification patterns.**

---

## PRE-RELEASE VALIDATION CHECKLIST

### 1. Compilation Validation
```
□ No compiler errors
□ No compiler warnings (or all warnings understood)
□ Plugin class name matches filename
□ Namespace is Oxide.Plugins
□ Has [Info] and [Description] attributes
```

### 2. Structure Validation
```
□ Inherits from RustPlugin (or CovalencePlugin)
□ Init() method exists and registers permissions
□ Unload() method exists and cleans up everything
□ OnServerInitialized() for post-init setup
□ No constructor logic (use Init instead)
```

### 3. API Usage Validation
```
□ No LogWarning() - must use PrintWarning()
□ No LogError() - must use PrintError()
□ No Debug.Log() - must use Puts()
□ Permissions use player.UserIDString
□ All hooks have correct signatures
```

### 4. Safety Validation
```
□ All player references null-checked
□ All entity references check IsDestroyed
□ Timer callbacks validate player.IsConnected
□ Collections use ToList() when modifying during iteration
□ No storing BasePlayer/BaseEntity references long-term
```

### 5. Resource Cleanup Validation
```
□ All timers destroyed in Unload()
□ All UI destroyed in Unload()
□ All components removed in Unload()
□ All spawned entities handled in Unload()
□ All event subscriptions removed in Unload()
□ All collections cleared in Unload()
```

---

## AUTOMATED VALIDATION SCRIPT

### Quick Grep Checks
```powershell
# Run from commands folder or plugin directory

# Check for forbidden logging methods
Select-String -Path "*.cs" -Pattern "LogWarning|LogError|Debug\.Log" -List

# Check for wrong permission pattern
Select-String -Path "*.cs" -Pattern "\.userID\s*," -List

# Check for missing Unload
Select-String -Path "*.cs" -Pattern "void Unload\(" -List

# Check for timer creation without storage
Select-String -Path "*.cs" -Pattern "timer\.(Once|Every|In)\(" -Context 0,2
```

### Validation Helper Class
```csharp
// Add to plugin for development, remove for release
#if DEBUG
public class PluginValidator
{
    public static void Validate(RustPlugin plugin)
    {
        var type = plugin.GetType();
        var issues = new List<string>();
        
        // Check Unload exists
        if (type.GetMethod("Unload", 
            System.Reflection.BindingFlags.NonPublic | 
            System.Reflection.BindingFlags.Instance) == null)
        {
            issues.Add("Missing Unload() method");
        }
        
        // Check for timer fields
        var timerFields = type.GetFields(
            System.Reflection.BindingFlags.NonPublic | 
            System.Reflection.BindingFlags.Instance)
            .Where(f => f.FieldType == typeof(Timer));
        
        if (!timerFields.Any())
        {
            // Check if timer.Once is used - might be leak
            // Would need source analysis
        }
        
        foreach (var issue in issues)
            plugin.PrintWarning($"[VALIDATION] {issue}");
    }
}
#endif
```

---

## UI ELEMENT VALIDATION

### Panel Name Uniqueness
```csharp
// Validate unique panel names at plugin level
private void ValidateUINames()
{
    var panels = new HashSet<string>
    {
        MAIN_PANEL,
        SUB_PANEL,
        HEADER_PANEL
    };
    
    // If count differs, we have duplicates
    var list = new List<string> { MAIN_PANEL, SUB_PANEL, HEADER_PANEL };
    if (panels.Count != list.Count)
    {
        PrintError("Duplicate UI panel names detected!");
    }
}
```

### UI Cleanup Verification
```csharp
[ChatCommand("validateui")]
private void CmdValidateUI(BasePlayer player, string cmd, string[] args)
{
    if (!player.IsAdmin) return;
    
    // Show all UIs
    ShowMainUI(player);
    ShowSubPanel(player);
    
    SendReply(player, "UI shown. Reloading plugin in 3s...");
    
    timer.Once(3f, () =>
    {
        // Trigger plugin reload
        Interface.Oxide.ReloadPlugin(Name);
        
        // After reload, UI should be gone
        timer.Once(1f, () =>
        {
            SendReply(player, "Plugin reloaded. Check if UI persists (it shouldn't).");
        });
    });
}
```

---

## CONFIG VALIDATION

### Validate Config Values
```csharp
private void ValidateConfig()
{
    bool changed = false;
    
    // Validate ranges
    if (config.Multiplier < 0 || config.Multiplier > 100)
    {
        PrintWarning($"Invalid Multiplier {config.Multiplier}, resetting to 1.0");
        config.Multiplier = 1.0f;
        changed = true;
    }
    
    if (config.Cooldown < 0)
    {
        PrintWarning($"Invalid Cooldown {config.Cooldown}, resetting to 0");
        config.Cooldown = 0;
        changed = true;
    }
    
    // Validate item shortnames
    if (!IsValidItem(config.RewardItem))
    {
        PrintWarning($"Invalid item '{config.RewardItem}', resetting to 'scrap'");
        config.RewardItem = "scrap";
        changed = true;
    }
    
    if (changed)
        SaveConfig();
}

private bool IsValidItem(string shortname)
{
    return ItemManager.FindItemDefinition(shortname) != null;
}
```

### Config Schema Validation
```csharp
protected override void LoadConfig()
{
    base.LoadConfig();
    
    try
    {
        config = Config.ReadObject<Configuration>();
        
        if (config == null)
            throw new Exception("Config is null");
        
        // Validate required fields
        if (config.RequiredSection == null)
        {
            PrintWarning("Config missing RequiredSection, using defaults");
            config.RequiredSection = new RequiredSection();
        }
        
        ValidateConfig();
    }
    catch (Exception ex)
    {
        PrintError($"Config error: {ex.Message}");
        PrintWarning("Using default configuration");
        LoadDefaultConfig();
    }
    
    SaveConfig();
}
```

---

## DATA VALIDATION

### Data Integrity Check
```csharp
private void ValidateData()
{
    int removed = 0;
    int repaired = 0;
    
    foreach (var kvp in data.Players.ToList())
    {
        var pd = kvp.Value;
        
        // Remove invalid entries
        if (pd == null)
        {
            data.Players.Remove(kvp.Key);
            removed++;
            continue;
        }
        
        // Fix negative values
        if (pd.Score < 0)
        {
            pd.Score = 0;
            repaired++;
        }
        
        // Remove null items from lists
        if (pd.Items != null)
        {
            int before = pd.Items.Count;
            pd.Items.RemoveAll(x => x == null);
            if (pd.Items.Count < before)
                repaired++;
        }
    }
    
    if (removed > 0 || repaired > 0)
    {
        Puts($"Data validation: removed {removed}, repaired {repaired}");
        SaveData();
    }
}
```

---

## PERMISSION VALIDATION

### Permission Format Check
```csharp
private void ValidatePermissions()
{
    var permissions = new[]
    {
        PERM_USE,
        PERM_ADMIN,
        PERM_VIP
    };
    
    foreach (var perm in permissions)
    {
        // Check format: pluginname.permname
        if (!perm.Contains("."))
        {
            PrintWarning($"Permission '{perm}' should contain a dot separator");
        }
        
        // Check lowercase
        if (perm != perm.ToLowerInvariant())
        {
            PrintWarning($"Permission '{perm}' should be lowercase");
        }
        
        // Check it's registered
        if (!permission.PermissionExists(perm))
        {
            PrintError($"Permission '{perm}' not registered!");
        }
    }
}
```

---

## RELEASE CHECKLIST

### Final Validation Before Release
```
□ Plugin loads without errors
□ Plugin unloads without errors
□ Config generates correctly
□ Config reloads without errors
□ Data saves and loads correctly
□ All commands respond appropriately
□ All permissions work correctly
□ Hot reload works (oxide.reload)
□ No memory leaks after 10 min usage
□ Works with 0 players online
□ Works with 50+ players (if applicable)
□ All UI elements clean up properly
□ No console spam during normal operation
□ Plugin doesn't break other plugins
□ Documentation is complete
□ Version number is updated
```

### Release Notes Template
```markdown
## v1.0.0 Release

### Features
- Feature 1 description
- Feature 2 description

### Commands
- `/command1` - Description
- `/command2 <arg>` - Description

### Permissions
- `plugin.use` - Basic usage
- `plugin.admin` - Admin commands

### Configuration
- `Setting Name` - Description (default: value)

### Known Issues
- Issue 1 and workaround

### Dependencies
- Required: Plugin1, Plugin2
- Optional: Plugin3

### Compatibility
- Minimum Rust Build: 12345
- Tested with Oxide 2.0.5935
```
