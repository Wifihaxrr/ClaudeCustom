---
description: "Use /rust <TASK> for Rust Oxide plugin development with built-in validation. Creates working plugins with zero compilation errors."
author: MaxThinking
version: 1.0.0
---

# /rust - VALIDATED OXIDE PLUGIN DEVELOPMENT

**Task:** $ARGUMENTS

---

## 🚨 CRITICAL RULES - READ THESE FIRST 🚨

### LOGGING - ONLY THESE EXIST:
```csharp
Puts("message");         // ✅ USE THIS
PrintWarning("message"); // ✅ USE THIS
PrintError("message");   // ✅ USE THIS
```

### THESE DO NOT EXIST (WILL CAUSE ERRORS):
```csharp
LogWarning();           // ❌ DOESN'T EXIST
LogError();             // ❌ DOESN'T EXIST
Debug.Log();            // ❌ DOESN'T EXIST
Console.WriteLine();    // ❌ DOESN'T EXIST
```

### PERMISSIONS - USE UserIDString:
```csharp
// ✅ CORRECT
permission.UserHasPermission(player.UserIDString, PERM)

// ❌ WRONG
permission.UserHasPermission(player.userID.ToString(), PERM)
```

---

## MANDATORY WORKFLOW

### STEP 1: READ THE BLACKLIST (Things that DON'T exist)
```
Read File: maxthinking/rust/blacklist.md
```

### STEP 2: READ THE WHITELIST (Things that DO exist)
```
Read File: maxthinking/rust/whitelist.md
```

### STEP 3: READ THE TEMPLATE
```
Read File: maxthinking/rust/template.cs
```
**This is a WORKING C# plugin. Copy it and fill in your logic.**

### STEP 4: READ A SIMILAR PLUGIN FROM LEARN FOLDER

| If your task involves... | Read this plugin |
|--------------------------|------------------|
| Simple command | `NoTechTree.cs` or `BrightNights.cs` |
| Config + Data storage | `Economics.cs` or `AutoDoors.cs` |
| Permissions | `TruePVE.cs` or `ZoneManager.cs` |
| UI/CUI | `Kits.cs` or `Shop.cs` |
| Timers | `AutoDoors.cs` or `NightLantern.cs` |
| NPCs | `HumanNPC.cs` or `ZombieHorde.cs` |
| Vehicles | `VehicleLicence.cs` or `PersonalHeli.cs` |
| Items/Inventory | `Backpacks.cs` or `StackSizeController.cs` |
| Building | `BGrade.cs` or `BuildingGrades.cs` |

```
Read URL: https://raw.githubusercontent.com/Wifihaxrr/ClaudeCustom/main/plugins/maxthinking/learn/[PLUGIN_NAME].cs
```
Or local:
```
View File: maxthinking/learn/[PLUGIN_NAME].cs
```

### STEP 5: CHECK COMMON TASKS (if needed)
```
Read File: maxthinking/rust/common_tasks.md
```

### STEP 6: WRITE YOUR PLUGIN
1. Copy `template.cs` exactly
2. Replace `PluginName` with your plugin name
3. Replace `commandname` with your command
4. Add your logic using ONLY methods from whitelist.md
5. Copy patterns from the learn folder plugin you read

---

## BEFORE DELIVERING - VALIDATE YOUR CODE

**Go through EVERY line of your code and check:**

### Check 1: Logging
- [ ] Search for `LogWarning` → If found, replace with `PrintWarning`
- [ ] Search for `LogError` → If found, replace with `PrintError`
- [ ] Search for `Debug.` → If found, REMOVE IT
- [ ] Search for `Console.` → If found, REMOVE IT

### Check 2: Structure
- [ ] Has `[Info("Name", "Author", "1.0.0")]` attribute?
- [ ] Has `[Description("...")]` attribute?
- [ ] Inherits from `RustPlugin`?
- [ ] Has `Init()` method that registers permissions?
- [ ] Has `Unload()` method that cleans up?

### Check 3: Safety
- [ ] All player access checks `player == null` first?
- [ ] All player access checks `player.IsConnected` before sending messages?
- [ ] Permissions use `player.UserIDString` not `player.userID.ToString()`?

### Check 4: No Hallucination
- [ ] Every method used exists in whitelist.md?
- [ ] No methods from blacklist.md are used?
- [ ] All patterns copied from a real learn folder plugin?

**If ANY check fails, FIX IT before delivering.**

---

## OUTPUT FORMAT

Deliver a COMPLETE, SINGLE .cs file that:
1. Compiles with ZERO errors
2. Is ready to drop into `oxide/plugins/` folder
3. Works on first try

```csharp
// Your complete plugin code here
```
