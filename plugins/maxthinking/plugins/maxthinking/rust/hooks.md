# VERIFIED HOOK SIGNATURES

**These are the exact signatures. Do not modify them.**

---

## PLAYER LIFECYCLE

| Hook | Signature |
|------|-----------|
| OnPlayerConnected | `void OnPlayerConnected(BasePlayer player)` |
| OnPlayerDisconnected | `void OnPlayerDisconnected(BasePlayer player, string reason)` |
| OnPlayerSleep | `void OnPlayerSleep(BasePlayer player)` |
| OnPlayerSleepEnded | `void OnPlayerSleepEnded(BasePlayer player)` |
| OnPlayerRespawned | `void OnPlayerRespawned(BasePlayer player)` |
| OnPlayerDeath | `void OnPlayerDeath(BasePlayer player, HitInfo info)` |
| OnPlayerWound | `void OnPlayerWound(BasePlayer player, HitInfo info)` |
| OnPlayerRecover | `void OnPlayerRecover(BasePlayer player)` |

---

## PLAYER INPUT

| Hook | Signature |
|------|-----------|
| OnPlayerInput | `void OnPlayerInput(BasePlayer player, InputState input)` |

```csharp
// InputState methods:
input.WasJustPressed(BUTTON.FIRE_PRIMARY)
input.WasJustReleased(BUTTON.FIRE_PRIMARY)
input.IsDown(BUTTON.SPRINT)

// Button types:
BUTTON.FIRE_PRIMARY    // Left click
BUTTON.FIRE_SECONDARY  // Right click
BUTTON.RELOAD
BUTTON.JUMP
BUTTON.DUCK
BUTTON.SPRINT
BUTTON.USE
```

---

## ENTITY LIFECYCLE

| Hook | Signature |
|------|-----------|
| OnEntitySpawned | `void OnEntitySpawned(BaseNetworkable entity)` |
| OnEntityKill | `void OnEntityKill(BaseNetworkable entity)` |
| OnEntityDeath | `void OnEntityDeath(BaseCombatEntity entity, HitInfo info)` |
| OnEntityTakeDamage | `object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)` |
| OnEntityBuilt | `void OnEntityBuilt(Planner planner, GameObject go)` |

---

## ITEMS

| Hook | Signature |
|------|-----------|
| OnActiveItemChanged | `void OnActiveItemChanged(BasePlayer player, Item oldItem, Item newItem)` |
| OnItemAddedToContainer | `void OnItemAddedToContainer(ItemContainer container, Item item)` |
| OnItemRemovedFromContainer | `void OnItemRemovedFromContainer(ItemContainer container, Item item)` |
| OnItemPickup | `object OnItemPickup(Item item, BasePlayer player)` |
| OnItemDropped | `void OnItemDropped(Item item, BaseEntity entity)` |

---

## BUILDING

| Hook | Signature |
|------|-----------|
| CanBuild | `object CanBuild(Planner planner, Construction prefab, Construction.Target target)` |
| OnStructureUpgrade | `void OnStructureUpgrade(BuildingBlock block, BasePlayer player, BuildingGrade.Enum grade)` |
| CanChangeGrade | `object CanChangeGrade(BasePlayer player, BuildingBlock block, BuildingGrade.Enum grade)` |
| OnHammerHit | `void OnHammerHit(BasePlayer player, HitInfo info)` |

---

## LOOTING

| Hook | Signature |
|------|-----------|
| OnLootEntity | `void OnLootEntity(BasePlayer player, BaseEntity entity)` |
| OnLootEntityEnd | `void OnLootEntityEnd(BasePlayer player, BaseCombatEntity entity)` |
| CanLootEntity | `object CanLootEntity(BasePlayer player, StorageContainer container)` |

---

## DOORS

| Hook | Signature |
|------|-----------|
| OnDoorOpened | `void OnDoorOpened(Door door, BasePlayer player)` |
| OnDoorClosed | `void OnDoorClosed(Door door, BasePlayer player)` |

---

## CHAT & COMMANDS

| Hook | Signature |
|------|-----------|
| OnPlayerChat | `object OnPlayerChat(BasePlayer player, string message, ConVar.Chat.ChatChannel channel)` |
| OnPlayerCommand | `object OnPlayerCommand(BasePlayer player, string command, string[] args)` |

---

## SERVER

| Hook | Signature |
|------|-----------|
| OnServerInitialized | `void OnServerInitialized()` |
| OnServerSave | `void OnServerSave()` |
| OnNewSave | `void OnNewSave(string filename)` |

---

## GATHERING

| Hook | Signature |
|------|-----------|
| OnDispenserGather | `void OnDispenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item)` |
| OnDispenserBonus | `void OnDispenserBonus(ResourceDispenser dispenser, BaseEntity entity, Item item)` |
| OnCollectiblePickup | `void OnCollectiblePickup(CollectibleEntity collectible, BasePlayer player)` |
