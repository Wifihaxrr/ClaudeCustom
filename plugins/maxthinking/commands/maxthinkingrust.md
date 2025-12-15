---
description: "Use /maxthinkingrust <TASK> for Rust Oxide plugin development with built-in validation. Creates working plugins with zero compilation errors."
author: MaxThinking
version: 3.2.0
---

# 🛑 STOP! COPY THIS WORKING CODE! 🛑

## HERE IS A COMPLETE WORKING GATHER PLUGIN - USE THIS AS YOUR BASE:

```csharp
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("TripleGather", "Author", "1.0.0")]
    [Description("Triples all gathering rates")]
    class TripleGather : RustPlugin
    {
        private float multiplier = 3.0f;

        private void OnDispenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item)
        {
            var player = entity.ToPlayer();
            if (player == null) return;
            item.amount = (int)(item.amount * multiplier);
        }

        private void OnDispenserBonus(ResourceDispenser dispenser, BaseEntity entity, Item item)
        {
            OnDispenserGather(dispenser, entity, item);
        }

        private void OnQuarryGather(MiningQuarry quarry, Item item)
        {
            item.amount = (int)(item.amount * multiplier);
        }
    }
}
```

## ⛔ DO NOT:
- ❌ Search the web
- ❌ Search for .cs files
- ❌ Research online
- ❌ Invent API methods

## ✅ JUST:
1. **COPY** the code above
2. **MODIFY** only what's needed for the task
3. **DELIVER** the complete plugin

---

**Task:** $ARGUMENTS

---

## 🚨 CRITICAL RULES - MEMORIZE THESE 🚨

### LOGGING - ONLY THESE EXIST:
```csharp
Puts("message");         // ✅ USE THIS
PrintWarning("message"); // ✅ USE THIS
PrintError("message");   // ✅ USE THIS
```

### THESE DO NOT EXIST (WILL CAUSE COMPILATION ERRORS):
```csharp
LogWarning();           // ❌ DOESN'T EXIST
LogError();             // ❌ DOESN'T EXIST
Log();                  // ❌ DOESN'T EXIST
Debug.Log();            // ❌ DOESN'T EXIST
Debug.LogWarning();     // ❌ DOESN'T EXIST
Console.WriteLine();    // ❌ DOESN'T EXIST
```

### PERMISSIONS - USE UserIDString:
```csharp
// ✅ CORRECT
permission.UserHasPermission(player.UserIDString, PERM)

// ❌ WRONG - will fail
permission.UserHasPermission(player.userID.ToString(), PERM)
```

### PLAYER SPEED - THESE DON'T EXIST:
```csharp
player.moveSpeedMultiplier   // ❌ DOESN'T EXIST
player.speedMultiplier       // ❌ DOESN'T EXIST
player.runSpeed              // ❌ DOESN'T EXIST
PlayerFlags.Speeding         // ❌ DOESN'T EXIST
```

### GOD MODE - THESE DON'T EXIST:
```csharp
player.SetGodMode()          // ❌ DOESN'T EXIST
player.godMode               // ❌ DOESN'T EXIST
PlayerFlags.God              // ❌ DOESN'T EXIST
PlayerFlags.GodMode          // ❌ DOESN'T EXIST
player.SetInvulnerable()     // ❌ DOESN'T EXIST
```
**For god mode, use OnEntityTakeDamage hook to block damage!**

### CUI - THESE DON'T EXIST:
```csharp
CuiPanel.ScrollRect     // ❌ DOESN'T EXIST
CuiScrollView           // ❌ DOESN'T EXIST
CuiDropdown             // ❌ DOESN'T EXIST
CuiCheckbox             // ❌ DOESN'T EXIST
CuiSlider               // ❌ DOESN'T EXIST
```

**CUI ONLY HAS: CuiPanel, CuiLabel, CuiButton, CuiInputField, CuiRawImageComponent**

---

## MANDATORY WORKFLOW

### STEP 1: READ MEGA-TEMPLATES (PRIMARY SOURCE - DO THIS FIRST!)
```
Read File: maxthinking/rust/mega_templates.cs
```
**This file contains 25 COMPLETE WORKING PLUGINS. Find the one closest to your task and COPY IT.**

| # | Template | Use For |
|---|----------|---------|
| 1 | Minimal | Hook blocking (18 lines) |
| 2 | Simple+Config | Config, permissions, blocking |
| 3 | DataStorage | Persistent player data |
| 4 | Timers+TOD | Day/night, TOD_Sky |
| 5 | Cooldowns | Per-player cooldowns |
| 6 | EntitySpawning | Helis, vehicles |
| 7 | Teleport | Player teleportation |
| 8 | SimpleUI | CUI panels, buttons |
| 9 | GodMode | CORRECT damage blocking |
| 10 | ItemGiving | Create/give items |
| 11 | Gathering | OnDispenserGather, quarries |
| 12 | DoorAutoClose | Door hooks, timers per entity |
| 13 | Components | FacepunchBehaviour on entities |
| 14 | Raycast | GetLookingAtEntity, doors |
| 15 | PluginReferences | Friends, Clans, Economics |
| 16 | CovalencePlugin | IPlayer, console+chat commands |
| 17 | WebRequests | HTTP GET/POST, JSON |
| 18 | StackSizes | OnMaxStackable, item mods |
| 19 | EntityFlags | SetFlag, On/Off/Locked/Open |
| 20 | BuildingPrivilege | Cupboard auth, CanBuild |
| 21 | ConsoleCommands | Admin commands, arg parsing |
| 22 | Coroutines | Batch processing, IEnumerator |
| 23 | ChatColors | Formatting, Player.Message |
| 24 | Protection | Block damage, ownership |
| 25 | LootHooks | Container hooks, custom loot |

### STEP 2: READ THE BLACKLIST (Things that DON'T exist)
```
Read File: maxthinking/rust/blacklist.md
```

### STEP 3: READ THE WHITELIST (Verified API)
```
Read File: maxthinking/rust/whitelist.md
```

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
| Gathering/Resources | `GatherManager.cs` or `QuickSmelt.cs` |

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
1. **Copy template.cs exactly**
2. Replace `PluginName` with your plugin name
3. Replace `commandname` with your command
4. Add your logic using **ONLY methods from whitelist.md**
5. Copy patterns from the learn folder plugin you read

---

## BEFORE DELIVERING - RUN THESE CHECKS

### Check 1: Logging (CRITICAL!)
- [ ] Search for `LogWarning` → **REPLACE with `PrintWarning`**
- [ ] Search for `LogError` → **REPLACE with `PrintError`**
- [ ] Search for `Debug.` → **REMOVE IT**
- [ ] Search for `Console.` → **REMOVE IT**

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

**⚠️ IF ANY CHECK FAILS, FIX IT BEFORE DELIVERING ⚠️**

---

## OUTPUT FORMAT

Deliver a COMPLETE, SINGLE .cs file that:
1. Compiles with ZERO errors on first try
2. Is ready to drop into `oxide/plugins/` folder
3. Works immediately

```csharp
// Your complete plugin code here
```

---

## VERIFIED HOOK SIGNATURES (do not guess!)

### Player Hooks
```csharp
void OnPlayerConnected(BasePlayer player)
void OnPlayerDisconnected(BasePlayer player, string reason)
void OnPlayerRespawned(BasePlayer player)
void OnPlayerDeath(BasePlayer player, HitInfo info)
void OnPlayerSleep(BasePlayer player)
void OnPlayerSleepEnded(BasePlayer player)
```

### Entity Hooks
```csharp
void OnEntitySpawned(BaseNetworkable entity)
void OnEntityKill(BaseNetworkable entity)
void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
```

### Building Hooks
```csharp
void OnEntityBuilt(Planner planner, GameObject go)
object CanBuild(Planner planner, Construction prefab, Construction.Target target)
void OnStructureUpgrade(BuildingBlock block, BasePlayer player, BuildingGrade.Enum grade)
```

### Item Hooks
```csharp
void OnItemAddedToContainer(ItemContainer container, Item item)
void OnItemRemovedFromContainer(ItemContainer container, Item item)
object OnItemPickup(Item item, BasePlayer player)
```

### Server Hooks
```csharp
void OnServerInitialized()
void OnServerSave()
void OnNewSave(string filename)
```

---

## QUICK REFERENCE - VERIFIED API

### Logging
```csharp
Puts("message");           // Normal log
PrintWarning("message");   // Warning (yellow)
PrintError("message");     // Error (red)
```

### Permissions
```csharp
permission.RegisterPermission("plugin.use", this);
permission.UserHasPermission(player.UserIDString, "plugin.use");
```

### Config
```csharp
private class Configuration
{
    [JsonProperty("Setting Name")]
    public bool Setting = true;
}
```

### Data Storage
```csharp
Interface.Oxide.DataFileSystem.WriteObject(Name, storedData);
storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name);
```

### Timers
```csharp
timer.Once(5f, () => { });
Timer myTimer = timer.Every(5f, () => { });
myTimer?.Destroy();
```

### Find Player
```csharp
BasePlayer.FindByID(steamId);
BasePlayer.activePlayerList.FirstOrDefault(x => x.UserIDString == id);
```

### Give Item
```csharp
var item = ItemManager.CreateByName("rifle.ak", 1);
player.inventory.GiveItem(item);
```

### CUI
```csharp
var container = new CuiElementContainer();
container.Add(new CuiPanel { ... }, "Overlay", "PanelName");
CuiHelper.AddUi(player, container);
CuiHelper.DestroyUi(player, "PanelName");
```

---

**REMEMBER: If you use a method that doesn't exist in the whitelist, it will FAIL TO COMPILE. When in doubt, check the learn folder plugins for how they do it.**
