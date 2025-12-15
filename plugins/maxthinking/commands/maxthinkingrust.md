---
description: "Use /maxthinkingrust <TASK> for Rust Oxide plugin development. Generates working plugins."
author: MaxThinking
version: 4.0.0
---

# RUST OXIDE PLUGIN GENERATOR

**User Request:** $ARGUMENTS

---

## STEP 1: PICK THE MATCHING TEMPLATE BELOW

Read all templates, pick the one that matches the user request, copy it exactly, then modify only the values needed.

---

## TEMPLATE A: GATHER MULTIPLIER
**Use when:** user wants to modify gathering, resource rates, 2x, 3x, 5x, etc.

```csharp
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("GatherMultiplier", "Author", "1.0.0")]
    [Description("Multiplies all gathering rates")]
    public class GatherMultiplier : RustPlugin
    {
        private Configuration config;

        private class Configuration
        {
            [JsonProperty("Multiplier")]
            public float Multiplier = 3.0f;  // CHANGE THIS VALUE
        }

        protected override void LoadDefaultConfig() => config = new Configuration();
        protected override void LoadConfig()
        {
            base.LoadConfig();
            try { config = Config.ReadObject<Configuration>(); if (config == null) throw new System.Exception(); }
            catch { PrintWarning("Config error"); LoadDefaultConfig(); }
            SaveConfig();
        }
        protected override void SaveConfig() => Config.WriteObject(config, true);

        private void OnDispenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item)
        {
            var player = entity.ToPlayer();
            if (player == null) return;
            item.amount = (int)(item.amount * config.Multiplier);
        }

        private void OnDispenserBonus(ResourceDispenser dispenser, BaseEntity entity, Item item)
        {
            var player = entity.ToPlayer();
            if (player == null) return;
            item.amount = (int)(item.amount * config.Multiplier);
        }

        private void OnQuarryGather(MiningQuarry quarry, Item item)
        {
            item.amount = (int)(item.amount * config.Multiplier);
        }
    }
}
```

---

## TEMPLATE B: BLOCK SOMETHING (Tech Tree, Crafting, etc.)
**Use when:** user wants to prevent/block/disable something

```csharp
namespace Oxide.Plugins
{
    [Info("BlockFeature", "Author", "1.0.0")]
    [Description("Blocks a game feature")]
    class BlockFeature : RustPlugin
    {
        // CHANGE "CanSomeHook" to the correct hook name
        // Return false or true to block, null to allow
        private object CanUnlockTechTreeNode(BasePlayer player)
        {
            return false; // Blocks tech tree
        }
    }
}
```

---

## TEMPLATE C: DAMAGE/GOD MODE
**Use when:** user wants invincibility, god mode, damage blocking

```csharp
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("GodMode", "Author", "1.0.0")]
    [Description("Makes players invincible")]
    class GodMode : RustPlugin
    {
        private const string PERM = "godmode.use";
        private HashSet<ulong> godPlayers = new HashSet<ulong>();

        private void Init() => permission.RegisterPermission(PERM, this);

        [ChatCommand("god")]
        private void CmdGod(BasePlayer player, string cmd, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, PERM))
            {
                SendReply(player, "No permission");
                return;
            }
            if (godPlayers.Contains(player.userID))
            {
                godPlayers.Remove(player.userID);
                SendReply(player, "God mode OFF");
            }
            else
            {
                godPlayers.Add(player.userID);
                SendReply(player, "God mode ON");
            }
        }

        private object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            var player = entity as BasePlayer;
            if (player != null && godPlayers.Contains(player.userID))
            {
                info.damageTypes.ScaleAll(0f);
                return true;
            }
            return null;
        }
    }
}
```

---

## TEMPLATE D: GIVE ITEMS
**Use when:** user wants to give items, kits, starter items

```csharp
namespace Oxide.Plugins
{
    [Info("GiveItems", "Author", "1.0.0")]
    [Description("Gives items to players")]
    class GiveItems : RustPlugin
    {
        [ChatCommand("kit")]
        private void CmdKit(BasePlayer player, string cmd, string[] args)
        {
            GiveItem(player, "rifle.ak", 1);
            GiveItem(player, "ammo.rifle", 100);
            GiveItem(player, "largemedkit", 5);
            SendReply(player, "Kit given!");
        }

        private void GiveItem(BasePlayer player, string shortname, int amount)
        {
            var item = ItemManager.CreateByName(shortname, amount);
            if (item == null) return;
            if (!player.inventory.GiveItem(item))
            {
                item.Drop(player.transform.position, UnityEngine.Vector3.up);
            }
        }
    }
}
```

---

## TEMPLATE E: TELEPORT
**Use when:** user wants teleportation

```csharp
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Teleport", "Author", "1.0.0")]
    [Description("Teleports players")]
    class Teleport : RustPlugin
    {
        private const string PERM = "teleport.use";
        private void Init() => permission.RegisterPermission(PERM, this);

        [ChatCommand("tp")]
        private void CmdTp(BasePlayer player, string cmd, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, PERM))
            {
                SendReply(player, "No permission");
                return;
            }
            if (args.Length < 1)
            {
                SendReply(player, "Usage: /tp <player>");
                return;
            }
            var target = FindPlayer(args[0]);
            if (target == null)
            {
                SendReply(player, "Player not found");
                return;
            }
            player.Teleport(target.transform.position);
            SendReply(player, $"Teleported to {target.displayName}");
        }

        private BasePlayer FindPlayer(string name)
        {
            foreach (var p in BasePlayer.activePlayerList)
            {
                if (p.displayName.ToLower().Contains(name.ToLower()))
                    return p;
            }
            return null;
        }
    }
}
```

---

## TEMPLATE F: SIMPLE UI
**Use when:** user wants a UI panel

```csharp
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("SimpleUI", "Author", "1.0.0")]
    [Description("Shows a simple UI")]
    class SimpleUI : RustPlugin
    {
        private const string PANEL = "SimpleUIPanel";

        private void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
                CuiHelper.DestroyUi(player, PANEL);
        }

        [ChatCommand("ui")]
        private void CmdUI(BasePlayer player, string cmd, string[] args)
        {
            ShowUI(player);
        }

        [ConsoleCommand("simpleui.close")]
        private void CmdClose(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player != null) CuiHelper.DestroyUi(player, PANEL);
        }

        private void ShowUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, PANEL);
            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                Image = { Color = "0 0 0 0.9" },
                RectTransform = { AnchorMin = "0.3 0.3", AnchorMax = "0.7 0.7" },
                CursorEnabled = true
            }, "Overlay", PANEL);

            container.Add(new CuiLabel
            {
                Text = { Text = "Hello World!", FontSize = 24, Align = TextAnchor.MiddleCenter },
                RectTransform = { AnchorMin = "0 0.5", AnchorMax = "1 0.9" }
            }, PANEL);

            container.Add(new CuiButton
            {
                Button = { Color = "0.8 0.2 0.2 1", Command = "simpleui.close" },
                RectTransform = { AnchorMin = "0.3 0.1", AnchorMax = "0.7 0.3" },
                Text = { Text = "Close", FontSize = 18, Align = TextAnchor.MiddleCenter }
            }, PANEL);

            CuiHelper.AddUi(player, container);
        }
    }
}
```

---

## CRITICAL RULES

### LOGGING - USE THESE:
- `Puts("message");` - normal log
- `PrintWarning("message");` - warning  
- `PrintError("message");` - error

### NEVER USE (WILL BREAK):
- `LogWarning()` - DOES NOT EXIST
- `LogError()` - DOES NOT EXIST
- `Debug.Log()` - DOES NOT EXIST

### PERMISSIONS:
```csharp
permission.UserHasPermission(player.UserIDString, "perm")  // CORRECT
```

---

## YOUR TASK

1. Read the user request above
2. Pick the template that matches (A-F)
3. Copy the template EXACTLY
4. Change ONLY what is needed (plugin name, values, commands)
5. Output the complete .cs file

**IMPORTANT**: Do NOT add features not requested. Keep it simple!
