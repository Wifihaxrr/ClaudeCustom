# HOW TO DO COMMON TASKS IN RUST OXIDE

**Copy these patterns exactly. Don't invent your own methods.**

---

## HOW TO: Create a Chat Command

```csharp
// Option 1: Attribute-based
[ChatCommand("mycommand")]
private void CmdMyCommand(BasePlayer player, string command, string[] args)
{
    if (player == null) return;
    if (!permission.UserHasPermission(player.UserIDString, PERM_USE))
    {
        SendReply(player, "No permission!");
        return;
    }
    // Your logic here
}

// Option 2: Config-based (registered in Init)
private void Init()
{
    cmd.AddChatCommand(config.CommandName, this, nameof(CmdMyCommand));
}
```

---

## HOW TO: Teleport a Player

```csharp
private void TeleportPlayer(BasePlayer player, Vector3 position)
{
    if (player == null || !player.IsConnected) return;
    
    // Dismount if mounted
    if (player.isMounted)
        player.GetMounted()?.DismountPlayer(player, true);
    
    // Teleport
    player.Teleport(position);
}
```

---

## HOW TO: Give Item to Player

```csharp
private void GiveItem(BasePlayer player, string shortname, int amount, ulong skin = 0)
{
    var item = ItemManager.CreateByName(shortname, amount, skin);
    if (item == null)
    {
        PrintWarning($"Failed to create item: {shortname}");
        return;
    }
    
    if (!player.inventory.GiveItem(item))
        item.Drop(player.transform.position + Vector3.up, Vector3.up);
}
```

---

## HOW TO: Create Simple UI Panel

```csharp
private const string UI_PANEL = "MyPlugin_Panel";

private void ShowUI(BasePlayer player)
{
    var container = new CuiElementContainer();
    
    // Main panel
    container.Add(new CuiPanel
    {
        Image = { Color = "0.1 0.1 0.1 0.9" },
        RectTransform = { AnchorMin = "0.3 0.3", AnchorMax = "0.7 0.7" },
        CursorEnabled = true
    }, "Overlay", UI_PANEL);
    
    // Add label
    container.Add(new CuiLabel
    {
        Text = { Text = "Hello World!", FontSize = 24, Align = TextAnchor.MiddleCenter },
        RectTransform = { AnchorMin = "0 0.8", AnchorMax = "1 1" }
    }, UI_PANEL);
    
    // Add close button
    container.Add(new CuiButton
    {
        Button = { Color = "0.8 0.2 0.2 1", Command = "myplugin.close" },
        RectTransform = { AnchorMin = "0.4 0.05", AnchorMax = "0.6 0.15" },
        Text = { Text = "Close", FontSize = 14, Align = TextAnchor.MiddleCenter }
    }, UI_PANEL);
    
    CuiHelper.DestroyUi(player, UI_PANEL);
    CuiHelper.AddUi(player, container);
}

private void DestroyUI(BasePlayer player)
{
    CuiHelper.DestroyUi(player, UI_PANEL);
}
```

---

## HOW TO: Use Timers with Cooldown

```csharp
private Dictionary<ulong, float> cooldowns = new Dictionary<ulong, float>();

private bool IsOnCooldown(BasePlayer player)
{
    if (!cooldowns.TryGetValue(player.userID, out float lastUse))
        return false;
    
    return Time.realtimeSinceStartup - lastUse < config.Cooldown;
}

private float GetRemainingCooldown(BasePlayer player)
{
    if (!cooldowns.TryGetValue(player.userID, out float lastUse))
        return 0;
    
    return config.Cooldown - (Time.realtimeSinceStartup - lastUse);
}

private void SetCooldown(BasePlayer player)
{
    cooldowns[player.userID] = Time.realtimeSinceStartup;
}
```

---

## HOW TO: Raycast to Find Entity Player is Looking At

```csharp
private BaseEntity GetLookingAt(BasePlayer player, float distance = 10f)
{
    if (Physics.Raycast(player.eyes.HeadRay(), out RaycastHit hit, distance))
        return hit.GetEntity();
    return null;
}
```

---

## HOW TO: Find Entities Near Position

```csharp
private List<T> FindNearbyEntities<T>(Vector3 position, float radius) where T : BaseEntity
{
    var list = new List<T>();
    Vis.Entities(position, radius, list);
    return list;
}
```

---

## HOW TO: Save and Load Data

```csharp
private StoredData storedData;

private class StoredData
{
    public Dictionary<ulong, PlayerData> Players = new Dictionary<ulong, PlayerData>();
}

private class PlayerData
{
    public int Uses;
    public double LastUsed;
}

private void LoadData()
{
    storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name) ?? new StoredData();
}

private void SaveData()
{
    Interface.Oxide.DataFileSystem.WriteObject(Name, storedData);
}

// Call in Unload and OnServerSave
private void Unload() => SaveData();
private void OnServerSave() => SaveData();
```

---

## HOW TO: Integrate with Economics Plugin

```csharp
[PluginReference] private Plugin Economics;

private double GetBalance(string oderId)
{
    return Economics?.Call<double>("Balance", userId) ?? 0;
}

private bool Withdraw(string userId, double amount)
{
    return Economics?.Call<bool>("Withdraw", userId, amount) ?? false;
}

private bool Deposit(string userId, double amount)
{
    return Economics?.Call<bool>("Deposit", userId, amount) ?? false;
}
```
