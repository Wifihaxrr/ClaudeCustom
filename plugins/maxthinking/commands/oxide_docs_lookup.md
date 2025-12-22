# OXIDE DOCUMENTATION LOOKUP

**Real-time documentation references and community resources for Oxide development.**

---

## OFFICIAL DOCUMENTATION

### uMod Documentation
```
Base URL: https://umod.org/documentation/

Hooks Reference:
https://umod.org/documentation/games/rust

Configuration:
https://umod.org/documentation/oxide/configuration

Data Storage:
https://umod.org/documentation/oxide/data-storage

Permissions:
https://umod.org/documentation/oxide/permissions

Timers:
https://umod.org/documentation/oxide/timers

Web Requests:
https://umod.org/documentation/oxide/web-requests
```

### Facepunch Wiki
```
Entity Reference:
https://wiki.facepunch.com/rust

Prefab Browser (in-game):
Press F1 → type "entity.spawn" to see prefabs
```

---

## COMMUNITY RESOURCES

### Plugin Marketplaces
```
Codefling (Premium Plugins):
https://codefling.com/

Lone.Design:
https://lone.design/

uMod Plugins:
https://umod.org/plugins/

ChaosCode (Free):
https://chaoscode.io/
```

### GitHub Reference Repositories
```
Oxide.Rust (Core):
https://github.com/OxideMod/Oxide.Rust

Popular Plugin Examples:
https://github.com/k1lly0u/Oxide-Plugins
https://github.com/Whispers88/Rust-Plugins

Rust Game API (Decompiled):
Search for "Rust-Game-API" or use dnSpy on Assembly-CSharp.dll
```

---

## HOOK DOCUMENTATION LOOKUP

### Player Hooks
| Hook | When Called | Parameters |
|------|-------------|------------|
| `OnPlayerConnected` | Player joins server | `BasePlayer player` |
| `OnPlayerDisconnected` | Player leaves | `BasePlayer player, string reason` |
| `OnPlayerRespawned` | After respawn | `BasePlayer player` |
| `OnPlayerDeath` | Player dies | `BasePlayer player, HitInfo info` |
| `OnPlayerInput` | Every frame with input | `BasePlayer player, InputState input` |
| `OnPlayerSleep` | Player goes AFK | `BasePlayer player` |
| `OnPlayerSleepEnded` | Player returns | `BasePlayer player` |
| `OnPlayerWound` | Player downed | `BasePlayer player, HitInfo info` |
| `OnPlayerRecover` | Player gets up | `BasePlayer player` |

### Entity Hooks
| Hook | When Called | Parameters |
|------|-------------|------------|
| `OnEntitySpawned` | Entity created | `BaseNetworkable entity` |
| `OnEntityKill` | Entity destroyed | `BaseNetworkable entity` |
| `OnEntityTakeDamage` | Damage dealt | `BaseCombatEntity entity, HitInfo info` |
| `OnEntityBuilt` | Player builds | `Planner planner, GameObject go` |
| `OnEntityDeath` | Entity health → 0 | `BaseCombatEntity entity, HitInfo info` |

### Item Hooks
| Hook | When Called | Parameters |
|------|-------------|------------|
| `OnActiveItemChanged` | Player switches item | `BasePlayer, Item oldItem, Item newItem` |
| `OnItemAddedToContainer` | Item placed | `ItemContainer container, Item item` |
| `OnItemRemovedFromContainer` | Item taken | `ItemContainer container, Item item` |
| `OnItemPickup` | Ground pickup | `Item item, BasePlayer player` |
| `OnItemDropped` | Item dropped | `Item item, BaseEntity entity` |

### Gathering Hooks
| Hook | When Called | Parameters |
|------|-------------|------------|
| `OnDispenserGather` | Node/tree hit | `ResourceDispenser, BaseEntity, Item` |
| `OnDispenserBonus` | Bonus resource | `ResourceDispenser, BaseEntity, Item` |
| `OnCollectiblePickup` | Hemp/mushroom | `CollectibleEntity, BasePlayer` |
| `OnGrowableGather` | Plant harvest | `GrowableEntity, Item, BasePlayer` |

---

## ENTITY TYPE REFERENCE

### Common Entity Classes
```csharp
BasePlayer          // Players
BaseNpc             // All NPCs base
ScientistNPC        // Scientist NPCs
HumanNPC            // Generic human NPCs
BradleyAPC          // Bradley tank
PatrolHelicopter    // Attack helicopter
CH47Helicopter      // Chinook
BaseHelicopter      // All helicopters
MiniCopter          // Minicopter
ScrapTransportHelicopter  // Scrap heli
RHIB                // Rigid Hull Boat
MotorRowboat        // Small boat
ModularCar          // Modular vehicles
BaseVehicle         // All vehicles base
StorageContainer    // Boxes, chests
BoxStorage          // Large/small box
BuildingBlock       // Foundations, walls
Door                // All doors
AutoTurret          // Auto turret
FlameTurret         // Flame turret
GunTrap             // Shotgun trap
Landmine            // Land mine
BearTrap            // Bear trap
BaseOven            // Furnaces, campfires
```

### Entity Inheritance
```
BaseNetworkable
  └─ BaseEntity
       └─ BaseCombatEntity
            └─ BasePlayer
            └─ BaseNpc
            └─ BuildingBlock
            └─ BaseVehicle
                 └─ MiniCopter
                 └─ ModularCar
       └─ BaseOven
       └─ StorageContainer
       └─ Door
```

---

## PREFAB REFERENCE LOOKUP

### NPC Prefabs
```csharp
// Scientists
"assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_heavy.prefab"
"assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_roam.prefab"
"assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_peacekeeper.prefab"
"assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_junkpile_pistol.prefab"

// Special NPCs
"assets/prefabs/npc/murderer/murderer.prefab"
"assets/prefabs/npc/scarecrow/scarecrow.prefab"
"assets/rust.ai/nextai/testridablehorse.prefab"
```

### Vehicle Prefabs
```csharp
// Air
"assets/content/vehicles/minicopter/minicopter.entity.prefab"
"assets/content/vehicles/scrap heli carrier/scraptransporthelicopter.prefab"
"assets/prefabs/npc/ch47/ch47scientists.entity.prefab"

// Land
"assets/content/vehicles/modularcar/car_chassis_2module.entity.prefab"
"assets/content/vehicles/modularcar/car_chassis_3module.entity.prefab"
"assets/content/vehicles/modularcar/car_chassis_4module.entity.prefab"

// Water
"assets/content/vehicles/boats/rhib/rhib.prefab"
"assets/content/vehicles/boats/rowboat/rowboat.prefab"
```

### Deployable Prefabs
```csharp
// Storage
"assets/prefabs/deployable/large wood storage/box.wooden.large.prefab"
"assets/prefabs/deployable/woodenbox/woodbox_deployed.prefab"

// Defenses
"assets/prefabs/npc/autoturret/autoturret_deployed.prefab"
"assets/prefabs/npc/flame turret/flameturret.deployed.prefab"

// Utility
"assets/prefabs/deployable/sleeping bag/sleepingbag_leather_deployed.prefab"
"assets/prefabs/deployable/tool cupboard/cupboard.tool.deployed.prefab"
```

### Effect Prefabs
```csharp
// Explosions
"assets/bundled/prefabs/fx/survey_explosion.prefab"
"assets/bundled/prefabs/fx/explosions/explosion_01.prefab"

// Impacts
"assets/bundled/prefabs/fx/impacts/additive/fire.prefab"
"assets/bundled/prefabs/fx/ore_break.prefab"
"assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"

// Building
"assets/bundled/prefabs/fx/build/promote_wood.prefab"
"assets/bundled/prefabs/fx/build/promote_stone.prefab"
"assets/bundled/prefabs/fx/build/promote_metal.prefab"
"assets/bundled/prefabs/fx/build/promote_toptier.prefab"
```

---

## ITEM SHORTNAME LOOKUP

### Weapons
```
rifle.ak, rifle.lr300, rifle.bolt, rifle.m39, rifle.semiauto
smg.mp5, smg.thompson, smg.2
pistol.m92, pistol.python, pistol.revolver, pistol.semiauto, pistol.nailgun
shotgun.pump, shotgun.spas12, shotgun.waterpipe, shotgun.double
bow.hunting, bow.compound, crossbow
```

### Ammo
```
ammo.rifle, ammo.rifle.hv, ammo.rifle.incendiary, ammo.rifle.explosive
ammo.pistol, ammo.pistol.hv, ammo.pistol.fire
ammo.shotgun, ammo.shotgun.slug, ammo.shotgun.fire
arrow.wooden, arrow.hv, arrow.fire, arrow.bone
```

### Medical
```
syringe.medical, largemedkit, bandage
```

### Resources
```
wood, stones, metal.ore, sulfur.ore, metal.fragments, metal.refined
sulfur, gunpowder, lowgradefuel, cloth, leather
```

---

## COMMON API PATTERNS

### Find Item Definition
```csharp
var itemDef = ItemManager.FindItemDefinition("rifle.ak");
if (itemDef == null)
    PrintWarning("Item not found!");
```

### Find Prefab
```csharp
if (!GameManifest.pathToGuid.ContainsKey(prefabPath))
    PrintWarning("Prefab not found!");
```

### Validate Item Shortname
```csharp
private bool IsValidItem(string shortname)
{
    return ItemManager.FindItemDefinition(shortname) != null;
}
```

---

## RESEARCH WORKFLOW

1. **Check Hook Exists**: Search `rust_reference.md` first
2. **Check Examples**: Look in `learn/` folder plugins
3. **Check uMod Docs**: https://umod.org/documentation/games/rust
4. **Check Community**: Search Codefling/uMod plugins
5. **Last Resort**: Decompile Assembly-CSharp.dll with dnSpy
