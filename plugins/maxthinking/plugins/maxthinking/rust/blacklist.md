# THINGS THAT DO NOT EXIST - WILL CAUSE COMPILATION ERRORS

**If you use ANYTHING from this list, your code WILL NOT COMPILE.**

---

## LOGGING - THESE DON'T EXIST

```csharp
LogWarning("msg");           // ❌ DOES NOT EXIST
LogError("msg");             // ❌ DOES NOT EXIST
Log("msg");                  // ❌ DOES NOT EXIST
Debug.Log("msg");            // ❌ DOES NOT EXIST (Unity, not available)
Debug.LogWarning("msg");     // ❌ DOES NOT EXIST
Debug.LogError("msg");       // ❌ DOES NOT EXIST
Console.WriteLine("msg");    // ❌ DOES NOT EXIST
```

**USE INSTEAD:** `Puts()`, `PrintWarning()`, `PrintError()`

---

## BASEPLAYER - THESE DON'T EXIST

```csharp
// Speed-related (NONE of these exist)
player.moveSpeedMultiplier   // ❌ DOES NOT EXIST
player.speedMultiplier       // ❌ DOES NOT EXIST
player.runSpeed              // ❌ DOES NOT EXIST
player.walkSpeed             // ❌ DOES NOT EXIST
player.jumpHeight            // ❌ DOES NOT EXIST
player.gravity               // ❌ DOES NOT EXIST
player.flySpeed              // ❌ DOES NOT EXIST

// God mode (NONE of these exist)
player.SetGodMode()          // ❌ DOES NOT EXIST
player.EnableGodMode()       // ❌ DOES NOT EXIST
player.godMode               // ❌ DOES NOT EXIST
player.isGod                 // ❌ DOES NOT EXIST
player.SetInvulnerable()     // ❌ DOES NOT EXIST

// PlayerFlags that DON'T exist
PlayerFlags.Speeding         // ❌ DOES NOT EXIST
PlayerFlags.SpeedBoost       // ❌ DOES NOT EXIST
PlayerFlags.Flying           // ❌ DOES NOT EXIST
PlayerFlags.God              // ❌ DOES NOT EXIST
PlayerFlags.GodMode          // ❌ DOES NOT EXIST
PlayerFlags.Invulnerable     // ❌ DOES NOT EXIST
```

**To make a player invulnerable, use:**
```csharp
// Block damage in OnEntityTakeDamage hook
object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
{
    var player = entity as BasePlayer;
    if (player != null && IsGodMode(player))
    {
        info.damageTypes.ScaleAll(0f);
        return true; // Block damage
    }
    return null;
}
```

---

## CUI - THESE DON'T EXIST

```csharp
CuiPanel.ScrollRect          // ❌ DOES NOT EXIST
CuiScrollView                // ❌ DOES NOT EXIST
CuiDropdown                  // ❌ DOES NOT EXIST
CuiCheckbox                  // ❌ DOES NOT EXIST
CuiSlider                    // ❌ DOES NOT EXIST
CuiProgressBar               // ❌ DOES NOT EXIST
CuiToggle                    // ❌ DOES NOT EXIST
```

**OXIDE CUI ONLY HAS:** CuiPanel, CuiLabel, CuiButton, CuiInputField, CuiRawImageComponent

---

## RESOURCE GATHERING - CLASS CONFUSION

```csharp
// ❌ WRONG - ResourceContainer does NOT have these:
resourceContainer.itemList           // DOES NOT EXIST
resourceContainer.containedItems     // DOES NOT EXIST

// ✅ CORRECT - Use ResourceDispenser for mining nodes:
var dispenser = ore.GetComponent<ResourceDispenser>();
foreach (var item in dispenser.containedItems)
{
    item.itemDef.shortname  // Use itemDef for ItemAmount
}

// ✅ CORRECT - Use inventory.itemList for storage:
foreach (var item in container.inventory.itemList)
{
    item.info.shortname     // Use info for Item
}
```

---

## PERMISSIONS - WRONG USAGE

```csharp
// ❌ WRONG - This will compile but won't work correctly
permission.UserHasPermission(player.userID.ToString(), perm);

// ✅ CORRECT - Use UserIDString
permission.UserHasPermission(player.UserIDString, perm);
```

---

## TYPE CONVERSION ERRORS

```csharp
// ❌ WRONG - double to float without cast
float value = someDoubleValue;

// ✅ CORRECT - explicit cast
float value = (float)someDoubleValue;

// ❌ WRONG - int where float expected
timer.Once(5, callback);

// ✅ CORRECT - use float literal
timer.Once(5f, callback);
```
