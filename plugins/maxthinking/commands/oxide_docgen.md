# OXIDE DOCUMENTATION GENERATION

**Templates and patterns for automated plugin documentation.**

---

## PLUGIN HEADER TEMPLATE

```csharp
/*
 * ============================================================================
 *                              PLUGIN NAME
 *                           Version 1.0.0
 *                          Author: YourName
 * ============================================================================
 * 
 * Description:
 *   Brief description of what the plugin does and its main features.
 * 
 * Commands:
 *   /command1         - Description of command 1
 *   /command2 <arg>   - Description of command 2
 * 
 * Permissions:
 *   pluginname.use    - Allows basic usage
 *   pluginname.admin  - Allows admin commands
 * 
 * Configuration:
 *   See oxide/config/PluginName.json
 * 
 * Data:
 *   Player data stored in oxide/data/PluginName.json
 * 
 * Dependencies:
 *   Required: None
 *   Optional: Economics, ImageLibrary
 * 
 * Hooks Used:
 *   OnPlayerConnected, OnPlayerDisconnected, OnEntityTakeDamage
 * 
 * API:
 *   Call<bool>("HasFeature", playerId)
 *   Call<void>("GiveReward", playerId, amount)
 * 
 * ============================================================================
 */
```

---

## COMMAND DOCUMENTATION FORMAT

### In-Code Documentation
```csharp
/// <summary>
/// Teleports the player to specified coordinates
/// </summary>
/// <param name="player">The player executing the command</param>
/// <param name="args">Arguments: x y z (or player name)</param>
/// <example>/tp 100 50 -200</example>
/// <permission>teleport.use</permission>
[ChatCommand("tp")]
private void CmdTeleport(BasePlayer player, string cmd, string[] args)
{
    // Implementation
}
```

### Help Command Pattern
```csharp
[ChatCommand("pluginhelp")]
private void CmdHelp(BasePlayer player, string cmd, string[] args)
{
    var help = new StringBuilder();
    help.AppendLine("<color=#ff8800>=== Plugin Name Help ===</color>");
    help.AppendLine();
    help.AppendLine("<color=#888888>Commands:</color>");
    help.AppendLine("  <color=#44ff44>/command1</color> - Description");
    help.AppendLine("  <color=#44ff44>/command2 <arg></color> - Description");
    help.AppendLine();
    help.AppendLine("<color=#888888>Examples:</color>");
    help.AppendLine("  /command2 player1 - Example usage");
    
    SendReply(player, help.ToString());
}
```

---

## PERMISSION DOCUMENTATION FORMAT

### Permission List Generator
```csharp
private void GeneratePermissionDocs()
{
    var perms = new Dictionary<string, string>
    {
        [PERM_USE] = "Allows basic plugin usage",
        [PERM_VIP] = "Grants VIP benefits (faster cooldowns, etc.)",
        [PERM_ADMIN] = "Full admin access to all commands"
    };
    
    var sb = new StringBuilder();
    sb.AppendLine("# Permissions\n");
    
    foreach (var kvp in perms)
    {
        sb.AppendLine($"- `{kvp.Key}` - {kvp.Value}");
    }
    
    // Output to console or file
    Puts(sb.ToString());
}
```

---

## CONFIG DOCUMENTATION FORMAT

### Self-Documenting Config
```csharp
private class Configuration
{
    [JsonProperty("General Settings")]
    public GeneralSettings General { get; set; } = new();
    
    [JsonProperty("Feature Settings")]
    public FeatureSettings Features { get; set; } = new();
}

private class GeneralSettings
{
    [JsonProperty("Enable Debug Mode (logs extra information)")]
    public bool Debug { get; set; } = false;
    
    [JsonProperty("Default Cooldown (seconds, 0 to disable)")]
    public float Cooldown { get; set; } = 60f;
}

private class FeatureSettings
{
    [JsonProperty("Enable VIP Bonuses")]
    public bool VIPEnabled { get; set; } = true;
    
    [JsonProperty("VIP Cooldown Multiplier (0.5 = half cooldown)")]
    public float VIPMultiplier { get; set; } = 0.5f;
}
```

### Config Documentation Generator
```csharp
[ChatCommand("genconfig")]
private void CmdGenConfig(BasePlayer player, string cmd, string[] args)
{
    if (!player.IsAdmin) return;
    
    var sb = new StringBuilder();
    sb.AppendLine("# Configuration Options\n");
    sb.AppendLine("| Setting | Default | Description |");
    sb.AppendLine("|---------|---------|-------------|");
    
    // Reflect over config class
    DocumentConfigClass(typeof(Configuration), "", sb);
    
    Puts(sb.ToString());
}

private void DocumentConfigClass(Type type, string prefix, StringBuilder sb)
{
    foreach (var prop in type.GetProperties())
    {
        var jsonProp = prop.GetCustomAttribute<JsonPropertyAttribute>();
        var name = jsonProp?.PropertyName ?? prop.Name;
        
        var defaultValue = GetDefaultValue(prop);
        
        sb.AppendLine($"| {prefix}{name} | {defaultValue} | See config |");
    }
}
```

---

## API HOOK DOCUMENTATION

### Document Plugin API
```csharp
/*
 * ============================================================================
 *                              DEVELOPER API
 * ============================================================================
 * 
 * This plugin exposes the following API methods for other plugins:
 * 
 * --------------------------------------------------------------------------
 * bool IsPlayerEnrolled(ulong playerId)
 * --------------------------------------------------------------------------
 * Checks if a player is enrolled in the system.
 * 
 * Parameters:
 *   playerId (ulong) - The player's Steam ID
 * 
 * Returns:
 *   true if enrolled, false otherwise
 * 
 * Example:
 *   var enrolled = MyPlugin.Call<bool>("IsPlayerEnrolled", player.userID);
 * 
 * --------------------------------------------------------------------------
 * void AddPoints(ulong playerId, int amount)
 * --------------------------------------------------------------------------
 * Adds points to a player's account.
 * 
 * Parameters:
 *   playerId (ulong) - The player's Steam ID
 *   amount (int) - Points to add (can be negative)
 * 
 * Example:
 *   MyPlugin.Call("AddPoints", player.userID, 100);
 * 
 * ============================================================================
 */

// Actual implementation
private bool IsPlayerEnrolled(ulong playerId)
{
    return data.Players.ContainsKey(playerId);
}

[HookMethod("AddPoints")]
public void AddPoints(ulong playerId, int amount)
{
    var pd = GetPlayerData(playerId);
    pd.Points += amount;
    SaveData();
}
```

---

## CHANGELOG TEMPLATE

```markdown
# Changelog

## [1.1.0] - 2024-01-15

### Added
- New `/stats` command to view player statistics
- VIP support with configurable bonuses
- Discord webhook integration

### Changed
- Improved UI performance by 50%
- Reduced memory usage for large servers

### Fixed
- Fixed crash when player disconnects during action
- Fixed UI not closing on plugin reload

### Deprecated
- Old config format will be removed in v2.0

## [1.0.1] - 2024-01-10

### Fixed
- Hotfix for permission check bypass

## [1.0.0] - 2024-01-05

### Added
- Initial release
- Core functionality
- Basic UI
```

---

## README TEMPLATE

```markdown
# Plugin Name

Brief description of what this plugin does.

## Features

- Feature 1
- Feature 2
- Feature 3

## Installation

1. Download `PluginName.cs`
2. Place in `oxide/plugins/`
3. Reload: `oxide.reload PluginName`
4. Configure: `oxide/config/PluginName.json`

## Commands

| Command | Permission | Description |
|---------|------------|-------------|
| `/cmd1` | plugin.use | Does something |
| `/cmd2 <arg>` | plugin.admin | Admin command |

## Permissions

| Permission | Description |
|------------|-------------|
| `plugin.use` | Basic usage |
| `plugin.vip` | VIP features |
| `plugin.admin` | Admin access |

## Configuration

```json
{
  "Enable Feature": true,
  "Cooldown (seconds)": 60,
  "VIP Multiplier": 0.5
}
```

### Configuration Options

| Option | Default | Description |
|--------|---------|-------------|
| Enable Feature | true | Enables the main feature |
| Cooldown | 60 | Time between uses |

## Dependencies

### Required
- None

### Optional
- Economics - Enables shop features
- ImageLibrary - Enables custom icons

## API

```csharp
// Check if player has feature
bool hasFeature = PluginName.Call<bool>("HasFeature", playerId);

// Add points to player
PluginName.Call("AddPoints", playerId, 100);
```

## Support

- Discord: [Your Discord]
- Website: [Your Website]

## Credits

- Author: Your Name
- Special thanks: Contributors
```

---

## AUTO-GENERATION COMMAND

```csharp
[ChatCommand("gendocs")]
private void CmdGenDocs(BasePlayer player, string cmd, string[] args)
{
    if (!player.IsAdmin) return;
    
    var docs = new StringBuilder();
    
    // Header
    docs.AppendLine($"# {Title}");
    docs.AppendLine($"Version: {Version}");
    docs.AppendLine($"Author: {Author}");
    docs.AppendLine();
    
    // Description
    docs.AppendLine($"## Description");
    docs.AppendLine(Description);
    docs.AppendLine();
    
    // Permissions
    docs.AppendLine("## Permissions");
    foreach (var perm in registeredPerms)
    {
        docs.AppendLine($"- `{perm.Key}` - {perm.Value}");
    }
    docs.AppendLine();
    
    // Commands
    docs.AppendLine("## Commands");
    foreach (var cmd in registeredCommands)
    {
        docs.AppendLine($"- `/{cmd.Key}` - {cmd.Value}");
    }
    
    // Output
    LogToFile("documentation", docs.ToString(), this);
    SendReply(player, "Documentation generated: oxide/logs/documentation.txt");
}
```
