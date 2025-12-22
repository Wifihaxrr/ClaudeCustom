# OXIDE SECURITY GUIDELINES

**Security audit patterns and vulnerability prevention for Oxide plugins.**

---

## PERMISSION BYPASS PREVENTION

### Always Check Permissions
```csharp
// ❌ BAD: Console command without permission check
[ConsoleCommand("plugin.giveall")]
private void CmdGiveAll(ConsoleSystem.Arg arg)
{
    GiveItemsToAll(); // Anyone from RCON can call!
}

// ✅ GOOD: Validate caller
[ConsoleCommand("plugin.giveall")]
private void CmdGiveAll(ConsoleSystem.Arg arg)
{
    var player = arg.Player();
    
    // From RCON (no player) - require admin connection
    if (player == null)
    {
        if (!arg.IsAdmin)
        {
            arg.ReplyWith("Admin only");
            return;
        }
    }
    // From player console
    else if (!player.IsAdmin && !HasPerm(player, PERM_ADMIN))
    {
        arg.ReplyWith("No permission");
        return;
    }
    
    GiveItemsToAll();
}
```

### UI Command Protection
```csharp
// ❌ BAD: UI button command without verification
[ConsoleCommand("shop.buy")]
private void CmdBuy(ConsoleSystem.Arg arg)
{
    var itemId = arg.GetInt(0);
    var player = arg.Player();
    PurchaseItem(player, itemId); // No validation!
}

// ✅ GOOD: Validate everything
[ConsoleCommand("shop.buy")]
private void CmdBuy(ConsoleSystem.Arg arg)
{
    var player = arg.Player();
    if (player == null) return;  // Must be from player
    
    if (!HasPerm(player, PERM_USE))
    {
        SendReply(player, "No permission");
        return;
    }
    
    // Validate shop is open for this player
    if (!openShops.Contains(player.userID))
    {
        PrintWarning($"{player.displayName} tried to buy without shop open");
        return;
    }
    
    if (!arg.HasArgs(1))
    {
        PrintWarning($"{player.displayName} sent shop.buy without item ID");
        return;
    }
    
    var itemId = arg.GetInt(0);
    if (!IsValidShopItem(itemId))
    {
        PrintWarning($"{player.displayName} tried to buy invalid item {itemId}");
        return;
    }
    
    PurchaseItem(player, itemId);
}
```

---

## ADMIN COMMAND PROTECTION

### Verify Admin Status
```csharp
private bool IsAdmin(BasePlayer player)
{
    if (player == null) return false;
    
    // Check built-in admin
    if (player.IsAdmin) return true;
    
    // Check permission-based admin
    if (HasPerm(player, PERM_ADMIN)) return true;
    
    return false;
}

// Always use for admin commands
[ChatCommand("admin")]
private void CmdAdmin(BasePlayer player, string cmd, string[] args)
{
    if (!IsAdmin(player))
    {
        SendReply(player, "Admin only");
        return;
    }
    
    // Admin action
}
```

### Log Admin Actions
```csharp
private void LogAdminAction(BasePlayer admin, string action, string details)
{
    var logEntry = $"[ADMIN] {admin.displayName} ({admin.userID}): {action} - {details}";
    
    // Console log
    Puts(logEntry);
    
    // Optional: File log
    LogToFile("admin_log", logEntry, this);
    
    // Optional: Discord webhook
    if (!string.IsNullOrEmpty(config.DiscordWebhook))
        SendDiscordLog(logEntry);
}

// Usage
[ChatCommand("give")]
private void CmdGive(BasePlayer player, string cmd, string[] args)
{
    if (!IsAdmin(player)) return;
    
    var target = FindPlayer(args[0]);
    var item = args[1];
    var amount = int.Parse(args[2]);
    
    GiveItem(target, item, amount);
    
    LogAdminAction(player, "GIVE", $"{amount}x {item} to {target.displayName}");
}
```

---

## PLAYER IMPERSONATION PREVENTION

### Validate Player Identity
```csharp
// ❌ BAD: Trust player-provided ID
[ConsoleCommand("plugin.setdata")]
private void CmdSetData(ConsoleSystem.Arg arg)
{
    var targetId = arg.GetUInt64(0);  // Player can set anyone's data!
    var value = arg.GetInt(1);
    data.Players[targetId] = value;
}

// ✅ GOOD: Use actual player identity
[ConsoleCommand("plugin.setdata")]
private void CmdSetData(ConsoleSystem.Arg arg)
{
    var player = arg.Player();
    if (player == null) return;
    
    var value = arg.GetInt(0);
    data.Players[player.userID] = value;  // Use real ID
}

// ✅ GOOD: Admin-only for setting other player data
[ConsoleCommand("plugin.setdataadmin")]
private void CmdSetDataAdmin(ConsoleSystem.Arg arg)
{
    var player = arg.Player();
    if (!IsAdmin(player)) return;
    
    var targetId = arg.GetUInt64(0);
    var value = arg.GetInt(1);
    
    LogAdminAction(player, "SET_DATA", $"{targetId} = {value}");
    data.Players[targetId] = value;
}
```

---

## DATA INJECTION PREVENTION

### Sanitize User Input
```csharp
// ❌ BAD: Using raw user input
[ChatCommand("setname")]
private void CmdSetName(BasePlayer player, string cmd, string[] args)
{
    data.Players[player.userID].CustomName = string.Join(" ", args);
}

// ✅ GOOD: Sanitize input
private static readonly Regex SafeTextRegex = new Regex(@"^[a-zA-Z0-9\s\-_]+$");

[ChatCommand("setname")]
private void CmdSetName(BasePlayer player, string cmd, string[] args)
{
    var name = string.Join(" ", args);
    
    // Length check
    if (name.Length > 32)
    {
        SendReply(player, "Name too long (max 32 characters)");
        return;
    }
    
    // Character check
    if (!SafeTextRegex.IsMatch(name))
    {
        SendReply(player, "Name contains invalid characters");
        return;
    }
    
    data.Players[player.userID].CustomName = name;
}
```

### Sanitize UI Display
```csharp
// ❌ BAD: Display raw user text in UI
container.Add(new CuiLabel
{
    Text = { Text = userData.CustomName }  // Could contain exploits
});

// ✅ GOOD: Escape or sanitize
private string SanitizeForUI(string text)
{
    if (string.IsNullOrEmpty(text)) return "";
    
    // Remove potential exploit characters
    return text
        .Replace("<", "")
        .Replace(">", "")
        .Replace("{", "")
        .Replace("}", "");
}

container.Add(new CuiLabel
{
    Text = { Text = SanitizeForUI(userData.CustomName) }
});
```

---

## CONSOLE COMMAND INJECTION

### Validate Command Arguments
```csharp
// ❌ BAD: Passing raw args to console command
[ChatCommand("runcmd")]
private void CmdRunCmd(BasePlayer player, string cmd, string[] args)
{
    if (!player.IsAdmin) return;
    player.SendConsoleCommand(string.Join(" ", args));  // Dangerous!
}

// ✅ GOOD: Whitelist allowed commands
private readonly HashSet<string> allowedCommands = new()
{
    "inventory.give",
    "spawn",
    "teleport"
};

[ChatCommand("runcmd")]
private void CmdRunCmd(BasePlayer player, string cmd, string[] args)
{
    if (!player.IsAdmin) return;
    if (args.Length == 0) return;
    
    var command = args[0].ToLower();
    if (!allowedCommands.Contains(command))
    {
        SendReply(player, "Command not allowed");
        return;
    }
    
    player.SendConsoleCommand(string.Join(" ", args));
}
```

---

## FILE PATH TRAVERSAL

### Validate File Paths
```csharp
// ❌ BAD: Using raw user input for file operations
[ChatCommand("loaddata")]
private void CmdLoadData(BasePlayer player, string cmd, string[] args)
{
    var filename = args[0];
    var data = Interface.Oxide.DataFileSystem.ReadObject<object>(filename);
}

// ✅ GOOD: Validate and sanitize path
private static readonly Regex SafeFilenameRegex = new Regex(@"^[a-zA-Z0-9\-_]+$");

[ChatCommand("loaddata")]
private void CmdLoadData(BasePlayer player, string cmd, string[] args)
{
    if (!player.IsAdmin) return;
    
    var filename = args[0];
    
    // Prevent path traversal
    if (filename.Contains("..") || filename.Contains("/") || filename.Contains("\\"))
    {
        SendReply(player, "Invalid filename");
        return;
    }
    
    // Only allow alphanumeric
    if (!SafeFilenameRegex.IsMatch(filename))
    {
        SendReply(player, "Invalid filename");
        return;
    }
    
    // Prefix with plugin name to sandbox
    var safePath = $"{Name}_{filename}";
    var data = Interface.Oxide.DataFileSystem.ReadObject<object>(safePath);
}
```

---

## RESOURCE EXHAUSTION PREVENTION

### Rate Limiting
```csharp
private Dictionary<ulong, float> lastCommandTime = new();
private const float COMMAND_COOLDOWN = 1f;

private bool IsRateLimited(BasePlayer player)
{
    if (player.IsAdmin) return false;  // Admins exempt
    
    if (lastCommandTime.TryGetValue(player.userID, out float lastTime))
    {
        if (Time.realtimeSinceStartup - lastTime < COMMAND_COOLDOWN)
            return true;
    }
    
    lastCommandTime[player.userID] = Time.realtimeSinceStartup;
    return false;
}

[ChatCommand("action")]
private void CmdAction(BasePlayer player, string cmd, string[] args)
{
    if (IsRateLimited(player))
    {
        SendReply(player, "Please wait before using this command again");
        return;
    }
    
    DoAction(player);
}
```

### Limit Resource Creation
```csharp
private Dictionary<ulong, int> entityCount = new();
private const int MAX_ENTITIES_PER_PLAYER = 10;

private bool CanSpawnEntity(BasePlayer player)
{
    entityCount.TryGetValue(player.userID, out int count);
    
    if (count >= MAX_ENTITIES_PER_PLAYER)
    {
        SendReply(player, $"Maximum entities reached ({MAX_ENTITIES_PER_PLAYER})");
        return false;
    }
    
    return true;
}

private void OnEntitySpawned(BaseEntity entity)
{
    if (ownedEntities.Contains(entity.net.ID))
    {
        entityCount.TryGetValue(entity.OwnerID, out int count);
        entityCount[entity.OwnerID] = count + 1;
    }
}

private void OnEntityKill(BaseNetworkable entity)
{
    if (entity is BaseEntity be && ownedEntities.Contains(be.net.ID))
    {
        if (entityCount.TryGetValue(be.OwnerID, out int count))
            entityCount[be.OwnerID] = Math.Max(0, count - 1);
    }
}
```

---

## SECURITY CHECKLIST

```
□ All admin commands check IsAdmin or permission
□ All console commands verify arg.Player() when needed
□ UI commands verify player has UI open
□ User input is sanitized
□ File paths are validated
□ Commands are rate-limited
□ Admin actions are logged
□ Player identity is verified (use userID, not provided values)
□ Resource creation is limited
□ No sensitive data in client-visible UI
□ Config doesn't contain exploitable defaults
```
