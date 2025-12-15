using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("MonumentOwner", "ZombiePVE", "1.0.0")]
    [Description("Creates ownership zones around monuments with customizable rules Free")]
    public class MonumentOwner : RustPlugin
    {
        #region Fields
        private Dictionary<Vector3, ZoneData> zones = new Dictionary<Vector3, ZoneData>();
        private Dictionary<Vector3, List<SphereEntity>> zoneSpheres = new Dictionary<Vector3, List<SphereEntity>>();
        private Dictionary<ulong, PlayerCooldowns> playerCooldowns = new Dictionary<ulong, PlayerCooldowns>();
        private Dictionary<ulong, HashSet<Vector3>> playerInZones = new Dictionary<ulong, HashSet<Vector3>>();
        private const string ADMIN_PERM = "monumentowner.admin";
        private Timer zoneCheckTimer;
        #endregion

        #region Classes
        private class ZoneData
        {
            public string Name;
            public Vector3 Position;
            public float Radius;
            public ulong OwnerId;
            public string OwnerName;
            public DateTime OwnershipStart;
            public bool IsCustomZone;
            public ZoneRules Rules = new ZoneRules();
        }

        private class ZoneRules
        {
            public bool PvpEnabled = false;
            public bool LootProtection = true;
            public bool BuildingBlocked = true;
            public float DamageMultiplier = 1.0f;
            public int MaxOwnershipMinutes = 30;
            public int CooldownMinutes = 10;
        }

        private class PlayerCooldowns
        {
            public Dictionary<string, DateTime> ZoneCooldowns = new Dictionary<string, DateTime>();
        }
        #endregion

        #region Configuration
        private Configuration config;

        private class Configuration
        {
            [JsonProperty("Zone check interval (seconds)")]
            public float ZoneCheckInterval = 2f;

            [JsonProperty("Default zone radius")]
            public float DefaultRadius = 50f;

            [JsonProperty("Default ownership duration (minutes)")]
            public int DefaultOwnershipMinutes = 30;

            [JsonProperty("Default cooldown (minutes)")]
            public int DefaultCooldownMinutes = 10;

            [JsonProperty("Enabled monuments")]
            public Dictionary<string, bool> EnabledMonuments = new Dictionary<string, bool>
            {
                ["airfield"] = true,
                ["bandit_town"] = false,
                ["compound"] = false,
                ["dome"] = true,
                ["excavator"] = true,
                ["fishing_village"] = false,
                ["gas_station"] = false,
                ["harbor"] = false,
                ["junkyard"] = false,
                ["launch_site"] = false,
                ["lighthouse"] = false,
                ["military_tunnel"] = false,
                ["mining_outpost"] = false,
                ["oilrig"] = true,
                ["oilrig_large"] = true,
                ["power_plant"] = false,
                ["satellite_dish"] = false,
                ["sewer_branch"] = false,
                ["sphere_tank"] = false,
                ["supermarket"] = false,
                ["trainyard"] = false,
                ["warehouse"] = false,
                ["water_treatment"] = false
            };

            [JsonProperty("Monument radius overrides")]
            public Dictionary<string, float> RadiusOverrides = new Dictionary<string, float>
            {
                ["launch_site"] = 150f,
                ["oilrig_large"] = 80f,
                ["excavator"] = 120f
            };

            [JsonProperty("Default zone rules")]
            public ZoneRules DefaultRules = new ZoneRules();

            [JsonProperty("Show protection sphere (bubble)")]
            public bool ShowSphere = true;

            [JsonProperty("Sphere color when unowned (R,G,B,A)")]
            public string SphereColorUnowned = "0.2,0.5,0.8,0.3";

            [JsonProperty("Sphere color when owned (R,G,B,A)")]
            public string SphereColorOwned = "0.2,0.8,0.2,0.3";
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) LoadDefaultConfig();
            }
            catch
            {
                LoadDefaultConfig();
            }
            SaveConfig();
        }

        protected override void LoadDefaultConfig() => config = new Configuration();
        protected override void SaveConfig() => Config.WriteObject(config);
        #endregion

        #region Data
        private void SaveData()
        {
            var saveData = new Dictionary<string, object>
            {
                ["zones"] = zones.Values.ToList(),
                ["cooldowns"] = playerCooldowns
            };
            Interface.Oxide.DataFileSystem.WriteObject("MonumentOwner", saveData);
        }

        private void LoadData()
        {
            try
            {
                var data = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, object>>("MonumentOwner");
                if (data != null)
                {
                    if (data.ContainsKey("cooldowns"))
                    {
                        playerCooldowns = JsonConvert.DeserializeObject<Dictionary<ulong, PlayerCooldowns>>(
                            JsonConvert.SerializeObject(data["cooldowns"]));
                    }
                }
            }
            catch { }
        }
        #endregion

        #region Oxide Hooks
        private void OnServerInitialized()
        {
            permission.RegisterPermission(ADMIN_PERM, this);
            LoadData();
            InitializeMonumentZones();
            SpawnAllSpheres();
            StartZoneChecking();
            Puts($"Monument Owner loaded - {zones.Count} zones created");
        }

        private void Unload()
        {
            zoneCheckTimer?.Destroy();
            DestroyAllSpheres();
            SaveData();
        }

        private void OnServerSave() => SaveData();
        #endregion

        #region Sphere Management
        private void SpawnAllSpheres()
        {
            if (!config.ShowSphere) return;

            foreach (var zone in zones.Values)
            {
                SpawnSphere(zone);
            }
        }

        private void SpawnSphere(ZoneData zone)
        {
            if (!config.ShowSphere) return;
            if (zoneSpheres.ContainsKey(zone.Position)) return;

            var sphereList = new List<SphereEntity>();

            // Spawn multiple overlapping spheres for darker/more visible effect
            for (int i = 0; i < 3; i++)
            {
                var sphere = GameManager.server.CreateEntity("assets/prefabs/visualization/sphere.prefab", zone.Position) as SphereEntity;
                if (sphere == null) continue;

                // 0.1 meter difference between each sphere
                sphere.currentRadius = (zone.Radius * 2f) - (i * 0.1f);
                sphere.lerpSpeed = 0f;
                sphere.enableSaving = false;
                sphere.Spawn();
                sphereList.Add(sphere);
            }

            zoneSpheres[zone.Position] = sphereList;
        }

        private void UpdateZoneSphereColor(ZoneData zone)
        {
            // Spheres don't support color changes, but we track ownership state
        }

        private void DestroyAllSpheres()
        {
            foreach (var sphereList in zoneSpheres.Values)
            {
                foreach (var sphere in sphereList)
                {
                    if (sphere != null && !sphere.IsDestroyed)
                        sphere.Kill();
                }
            }
            zoneSpheres.Clear();
        }

        private void DestroySphere(Vector3 position)
        {
            if (zoneSpheres.TryGetValue(position, out var sphereList))
            {
                foreach (var sphere in sphereList)
                {
                    if (sphere != null && !sphere.IsDestroyed)
                        sphere.Kill();
                }
                zoneSpheres.Remove(position);
            }
        }
        #endregion

        #region Zone Initialization
        private void InitializeMonumentZones()
        {
            zones.Clear();

            foreach (var monument in TerrainMeta.Path.Monuments)
            {
                if (monument == null) continue;

                string name = GetMonumentName(monument.name);
                if (string.IsNullOrEmpty(name)) continue;

                if (!config.EnabledMonuments.TryGetValue(name.ToLower(), out bool enabled) || !enabled)
                    continue;

                float radius = config.DefaultRadius;
                if (config.RadiusOverrides.TryGetValue(name.ToLower(), out float overrideRadius))
                    radius = overrideRadius;

                var zone = new ZoneData
                {
                    Name = name,
                    Position = monument.transform.position,
                    Radius = radius,
                    Rules = CloneRules(config.DefaultRules)
                };

                zones[monument.transform.position] = zone;
            }

            // Load custom zones
            LoadCustomZones();
        }

        private void LoadCustomZones()
        {
            var customZonesPath = "MonumentOwner/CustomZones";
            if (!Interface.Oxide.DataFileSystem.ExistsDatafile(customZonesPath)) return;

            try
            {
                var customZones = Interface.Oxide.DataFileSystem.ReadObject<List<ZoneData>>(customZonesPath);
                if (customZones != null)
                {
                    foreach (var zone in customZones)
                    {
                        zone.IsCustomZone = true;
                        zones[zone.Position] = zone;
                    }
                }
            }
            catch { }
        }

        private string GetMonumentName(string prefabName)
        {
            if (string.IsNullOrEmpty(prefabName)) return null;

            prefabName = prefabName.ToLower();

            if (prefabName.Contains("airfield")) return "Airfield";
            if (prefabName.Contains("bandit")) return "Bandit_Town";
            if (prefabName.Contains("compound")) return "Compound";
            if (prefabName.Contains("sphere")) return "Sphere_Tank";
            if (prefabName.Contains("dome")) return "Dome";
            if (prefabName.Contains("excavator")) return "Excavator";
            if (prefabName.Contains("fishing")) return "Fishing_Village";
            if (prefabName.Contains("gas_station")) return "Gas_Station";
            if (prefabName.Contains("harbor")) return "Harbor";
            if (prefabName.Contains("junkyard")) return "Junkyard";
            if (prefabName.Contains("launch")) return "Launch_Site";
            if (prefabName.Contains("lighthouse")) return "Lighthouse";
            if (prefabName.Contains("military")) return "Military_Tunnel";
            if (prefabName.Contains("mining")) return "Mining_Outpost";
            if (prefabName.Contains("oilrig_2") || prefabName.Contains("large_oilrig")) return "Oilrig_Large";
            if (prefabName.Contains("oilrig")) return "Oilrig";
            if (prefabName.Contains("power")) return "Power_Plant";
            if (prefabName.Contains("satellite")) return "Satellite_Dish";
            if (prefabName.Contains("sewer")) return "Sewer_Branch";
            if (prefabName.Contains("supermarket")) return "Supermarket";
            if (prefabName.Contains("trainyard")) return "Trainyard";
            if (prefabName.Contains("warehouse")) return "Warehouse";
            if (prefabName.Contains("water_treatment")) return "Water_Treatment";

            return null;
        }

        private ZoneRules CloneRules(ZoneRules source)
        {
            return new ZoneRules
            {
                PvpEnabled = source.PvpEnabled,
                LootProtection = source.LootProtection,
                BuildingBlocked = source.BuildingBlocked,
                DamageMultiplier = source.DamageMultiplier,
                MaxOwnershipMinutes = source.MaxOwnershipMinutes,
                CooldownMinutes = source.CooldownMinutes
            };
        }
        #endregion

        #region Zone Checking
        private void StartZoneChecking()
        {
            zoneCheckTimer?.Destroy();
            zoneCheckTimer = timer.Every(config.ZoneCheckInterval, CheckAllPlayers);
        }

        private void CheckAllPlayers()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player == null || !player.IsConnected) continue;
                CheckPlayerZones(player);
            }

            // Check ownership expiration
            CheckOwnershipExpiration();
        }

        private void CheckPlayerZones(BasePlayer player)
        {
            if (!playerInZones.ContainsKey(player.userID))
                playerInZones[player.userID] = new HashSet<Vector3>();

            var currentZones = new HashSet<Vector3>();

            foreach (var kvp in zones)
            {
                var zone = kvp.Value;
                float distance = Vector3.Distance(player.transform.position, zone.Position);

                if (distance <= zone.Radius)
                {
                    // Check if zone is owned and player is NOT allowed
                    if (zone.OwnerId != 0 && !IsPlayerAllowedInZone(player, zone))
                    {
                        // Push player out of zone
                        PushPlayerOutOfZone(player, zone);
                        continue;
                    }

                    currentZones.Add(zone.Position);

                    // Player entered zone
                    if (!playerInZones[player.userID].Contains(zone.Position))
                    {
                        OnPlayerEnterZone(player, zone);
                    }
                }
            }

            // Check for zones player left
            foreach (var zonePos in playerInZones[player.userID].ToList())
            {
                if (!currentZones.Contains(zonePos))
                {
                    if (zones.TryGetValue(zonePos, out var zone))
                    {
                        OnPlayerExitZone(player, zone);
                    }
                }
            }

            playerInZones[player.userID] = currentZones;
        }

        private bool IsPlayerAllowedInZone(BasePlayer player, ZoneData zone)
        {
            if (player.IsAdmin) return true;
            if (zone.OwnerId == 0) return true;
            if (zone.OwnerId == player.userID) return true;

            // Check if on same team as owner
            var owner = BasePlayer.FindByID(zone.OwnerId);
            if (owner != null && owner.currentTeam != 0 && owner.currentTeam == player.currentTeam)
                return true;

            return false;
        }

        private void PushPlayerOutOfZone(BasePlayer player, ZoneData zone)
        {
            // Calculate direction away from zone center
            Vector3 direction = (player.transform.position - zone.Position).normalized;
            if (direction == Vector3.zero)
                direction = Vector3.forward;

            // Push player just outside the zone
            Vector3 pushPosition = zone.Position + direction * (zone.Radius + 5f);

            // Find ground level
            RaycastHit hit;
            if (Physics.Raycast(pushPosition + Vector3.up * 50f, Vector3.down, out hit, 100f, LayerMask.GetMask("Terrain", "World")))
            {
                pushPosition = hit.point + Vector3.up * 0.5f;
            }

            player.Teleport(pushPosition);
            SendReply(player, $"<color=#ff4444>You cannot enter {zone.Name} - it's owned by {zone.OwnerName}!</color>");
        }

        private void OnPlayerEnterZone(BasePlayer player, ZoneData zone)
        {
            // Try to claim ownership if no owner
            if (zone.OwnerId == 0 && CanPlayerBecomeOwner(zone.Position, player))
            {
                SetOwnerInternal(zone, player);
                SendReply(player, $"<color=#44ff44>You are now the owner of {zone.Name}!</color>");
            }
            else if (zone.OwnerId == player.userID)
            {
                SendReply(player, $"<color=#44ff44>Welcome back to your zone: {zone.Name}</color>");
            }
            else if (zone.OwnerId != 0 && IsPlayerAllowedInZone(player, zone))
            {
                SendReply(player, $"<color=#44ff44>Entered {zone.Name} (owned by teammate {zone.OwnerName})</color>");
            }
            else
            {
                SendReply(player, $"<color=#ffcc00>Entered {zone.Name}</color>");
            }

            // Call hook
            Interface.CallHook("OnPlayerEnteredMonument", player, zone.Position, zone.Radius);
        }

        private void OnPlayerExitZone(BasePlayer player, ZoneData zone)
        {
            SendReply(player, $"<color=#888888>Left {zone.Name}</color>");

            // If owner leaves, start countdown or release
            if (zone.OwnerId == player.userID)
            {
                ReleaseOwnership(zone);
                SendReply(player, $"<color=#ff4444>You lost ownership of {zone.Name}</color>");
            }

            // Call hook
            Interface.CallHook("OnPlayerExitedMonument", player, zone.Position, zone.Radius);
        }

        private void CheckOwnershipExpiration()
        {
            foreach (var zone in zones.Values)
            {
                if (zone.OwnerId == 0) continue;

                var elapsed = DateTime.Now - zone.OwnershipStart;
                if (elapsed.TotalMinutes >= zone.Rules.MaxOwnershipMinutes)
                {
                    var owner = BasePlayer.FindByID(zone.OwnerId);
                    if (owner != null)
                        SendReply(owner, $"<color=#ff4444>Your ownership of {zone.Name} has expired!</color>");

                    ReleaseOwnership(zone);
                }
            }
        }

        private void SetOwnerInternal(ZoneData zone, BasePlayer player)
        {
            zone.OwnerId = player.userID;
            zone.OwnerName = player.displayName;
            zone.OwnershipStart = DateTime.Now;
            UpdateZoneSphereColor(zone);
        }

        private void ReleaseOwnership(ZoneData zone)
        {
            // Set cooldown for previous owner
            if (zone.OwnerId != 0)
            {
                if (!playerCooldowns.ContainsKey(zone.OwnerId))
                    playerCooldowns[zone.OwnerId] = new PlayerCooldowns();

                playerCooldowns[zone.OwnerId].ZoneCooldowns[zone.Name] = DateTime.Now.AddMinutes(zone.Rules.CooldownMinutes);
            }

            zone.OwnerId = 0;
            zone.OwnerName = null;
            UpdateZoneSphereColor(zone);
        }
        #endregion

        #region Zone Rules Enforcement
        private object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null || info == null) return null;

            var victim = entity as BasePlayer;
            var attacker = info?.InitiatorPlayer;

            if (victim == null || attacker == null) return null;
            if (!victim.userID.IsSteamId() || !attacker.userID.IsSteamId()) return null;

            // Check if in a zone
            foreach (var zone in zones.Values)
            {
                float victimDist = Vector3.Distance(victim.transform.position, zone.Position);
                if (victimDist <= zone.Radius)
                {
                    // PvP disabled in zone
                    if (!zone.Rules.PvpEnabled)
                    {
                        // Allow owner to defend
                        if (zone.OwnerId != 0 && zone.OwnerId == victim.userID)
                            return null;

                        SendReply(attacker, $"<color=#ff4444>PvP is disabled in {zone.Name}</color>");
                        return true;
                    }

                    // Apply damage multiplier
                    if (zone.Rules.DamageMultiplier != 1.0f)
                    {
                        info.damageTypes.ScaleAll(zone.Rules.DamageMultiplier);
                    }
                }
            }

            return null;
        }

        private object CanLootEntity(BasePlayer player, StorageContainer container)
        {
            if (player == null || container == null) return null;

            foreach (var zone in zones.Values)
            {
                float dist = Vector3.Distance(container.transform.position, zone.Position);
                if (dist <= zone.Radius && zone.Rules.LootProtection && zone.OwnerId != 0 && zone.OwnerId != player.userID)
                {
                    SendReply(player, $"<color=#ff4444>This container is protected by {zone.OwnerName}</color>");
                    return false;
                }
            }

            return null;
        }

        private object CanBuild(Planner planner, Construction prefab, Construction.Target target)
        {
            var player = planner?.GetOwnerPlayer();
            if (player == null) return null;

            foreach (var zone in zones.Values)
            {
                float dist = Vector3.Distance(target.position, zone.Position);
                if (dist <= zone.Radius && zone.Rules.BuildingBlocked)
                {
                    SendReply(player, $"<color=#ff4444>Building is blocked in {zone.Name}</color>");
                    return false;
                }
            }

            return null;
        }
        #endregion


        #region Commands
        [ChatCommand("mocd")]
        private void CmdCooldowns(BasePlayer player, string command, string[] args)
        {
            if (!playerCooldowns.TryGetValue(player.userID, out var cooldowns) || cooldowns.ZoneCooldowns.Count == 0)
            {
                SendReply(player, "<color=#44ff44>You have no active cooldowns.</color>");
                return;
            }

            string msg = "<color=#ffcc00>Your Cooldowns:</color>\n";
            foreach (var cd in cooldowns.ZoneCooldowns)
            {
                var remaining = cd.Value - DateTime.Now;
                if (remaining.TotalSeconds > 0)
                    msg += $"• {cd.Key}: {remaining.Minutes}m {remaining.Seconds}s\n";
            }
            SendReply(player, msg);
        }

        [ChatCommand("mocreatecustomzone")]
        private void CmdCreateCustomZone(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, ADMIN_PERM))
            {
                SendReply(player, "<color=#ff4444>No permission.</color>");
                return;
            }

            if (args.Length < 1)
            {
                SendReply(player, "<color=#ff4444>Usage: /mocreatecustomzone <name></color>");
                return;
            }

            string zoneName = string.Join(" ", args);
            var zone = new ZoneData
            {
                Name = zoneName,
                Position = player.transform.position,
                Radius = config.DefaultRadius,
                IsCustomZone = true,
                Rules = CloneRules(config.DefaultRules)
            };

            zones[zone.Position] = zone;
            SaveCustomZones();

            SendReply(player, $"<color=#44ff44>Created custom zone '{zoneName}' at your position!</color>");
        }

        [ChatCommand("moshowid")]
        private void CmdShowId(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, ADMIN_PERM))
            {
                SendReply(player, "<color=#ff4444>No permission.</color>");
                return;
            }

            int i = 0;
            foreach (var zone in zones.Values)
            {
                player.SendConsoleCommand("ddraw.text", 30f, Color.green, zone.Position + Vector3.up * 5f, $"<size=20>{zone.Name}\nID: {i}</size>");
                i++;
            }
            SendReply(player, $"<color=#44ff44>Showing {zones.Count} zone IDs for 30 seconds.</color>");
        }

        [ChatCommand("modrawedges")]
        private void CmdDrawEdges(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, ADMIN_PERM))
            {
                SendReply(player, "<color=#ff4444>No permission.</color>");
                return;
            }

            foreach (var zone in zones.Values)
            {
                DrawZoneCircle(player, zone.Position, zone.Radius, 30f);
            }
            SendReply(player, $"<color=#44ff44>Drawing zone boundaries for 30 seconds.</color>");
        }

        private void DrawZoneCircle(BasePlayer player, Vector3 center, float radius, float duration)
        {
            int segments = 36;
            for (int i = 0; i < segments; i++)
            {
                float angle1 = (i * 360f / segments) * Mathf.Deg2Rad;
                float angle2 = ((i + 1) * 360f / segments) * Mathf.Deg2Rad;

                Vector3 p1 = center + new Vector3(Mathf.Cos(angle1) * radius, 0, Mathf.Sin(angle1) * radius);
                Vector3 p2 = center + new Vector3(Mathf.Cos(angle2) * radius, 0, Mathf.Sin(angle2) * radius);

                player.SendConsoleCommand("ddraw.line", duration, Color.cyan, p1, p2);
            }
        }

        [ConsoleCommand("mocdreset")]
        private void ConsoleCdReset(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null && !permission.UserHasPermission(arg.Connection.userid.ToString(), ADMIN_PERM))
                return;

            if (arg.Args == null || arg.Args.Length < 1)
            {
                arg.ReplyWith("Usage: mocdreset <SteamID64>");
                return;
            }

            if (!ulong.TryParse(arg.Args[0], out ulong steamId))
            {
                arg.ReplyWith("Invalid SteamID64");
                return;
            }

            if (playerCooldowns.ContainsKey(steamId))
            {
                playerCooldowns[steamId].ZoneCooldowns.Clear();
                arg.ReplyWith($"Cooldowns reset for {steamId}");
            }
            else
            {
                arg.ReplyWith("Player has no cooldowns");
            }
        }

        [ConsoleCommand("mogetcd")]
        private void ConsoleGetCd(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null && !permission.UserHasPermission(arg.Connection.userid.ToString(), ADMIN_PERM))
                return;

            if (arg.Args == null || arg.Args.Length < 1)
            {
                arg.ReplyWith("Usage: mogetcd <SteamID64>");
                return;
            }

            if (!ulong.TryParse(arg.Args[0], out ulong steamId))
            {
                arg.ReplyWith("Invalid SteamID64");
                return;
            }

            if (!playerCooldowns.TryGetValue(steamId, out var cooldowns) || cooldowns.ZoneCooldowns.Count == 0)
            {
                arg.ReplyWith("Player has no cooldowns");
                return;
            }

            string msg = $"Cooldowns for {steamId}:\n";
            foreach (var cd in cooldowns.ZoneCooldowns)
            {
                var remaining = cd.Value - DateTime.Now;
                msg += $"  {cd.Key}: {(remaining.TotalSeconds > 0 ? $"{remaining.Minutes}m {remaining.Seconds}s" : "expired")}\n";
            }
            arg.ReplyWith(msg);
        }

        private void SaveCustomZones()
        {
            var customZones = zones.Values.Where(z => z.IsCustomZone).ToList();
            Interface.Oxide.DataFileSystem.WriteObject("MonumentOwner/CustomZones", customZones);
        }
        #endregion

        #region API
        private bool HasZone(Vector3 posMonument)
        {
            foreach (var zone in zones.Values)
            {
                if (Vector3.Distance(zone.Position, posMonument) < 10f)
                    return true;
            }
            return false;
        }

        private bool HasOwner(Vector3 posMonument)
        {
            foreach (var zone in zones.Values)
            {
                if (Vector3.Distance(zone.Position, posMonument) < zone.Radius)
                    return zone.OwnerId != 0;
            }
            return false;
        }

        private BasePlayer GetOwner(Vector3 posMonument)
        {
            foreach (var zone in zones.Values)
            {
                if (Vector3.Distance(zone.Position, posMonument) < zone.Radius && zone.OwnerId != 0)
                    return BasePlayer.FindByID(zone.OwnerId);
            }
            return null;
        }

        private bool CanPlayerBecomeOwner(Vector3 posMonument, BasePlayer player)
        {
            foreach (var zone in zones.Values)
            {
                if (Vector3.Distance(zone.Position, posMonument) < zone.Radius)
                {
                    // Already has owner
                    if (zone.OwnerId != 0) return false;

                    // Check cooldown
                    if (playerCooldowns.TryGetValue(player.userID, out var cooldowns))
                    {
                        if (cooldowns.ZoneCooldowns.TryGetValue(zone.Name, out var cdEnd))
                        {
                            if (DateTime.Now < cdEnd) return false;
                        }
                    }

                    return true;
                }
            }
            return false;
        }

        private bool SetOwner(Vector3 posMonument, BasePlayer player)
        {
            foreach (var zone in zones.Values)
            {
                if (Vector3.Distance(zone.Position, posMonument) < zone.Radius)
                {
                    if (zone.OwnerId != 0) return false;

                    SetOwnerInternal(zone, player);
                    return true;
                }
            }
            return false;
        }

        private bool RemoveZone(MonumentInfo monument)
        {
            var toRemove = zones.Keys.FirstOrDefault(k => Vector3.Distance(k, monument.transform.position) < 10f);
            if (toRemove != default)
            {
                zones.Remove(toRemove);
                return true;
            }
            return false;
        }

        private bool CreateZone(MonumentInfo monument)
        {
            if (HasZone(monument.transform.position)) return false;

            string name = GetMonumentName(monument.name) ?? "Custom";
            var zone = new ZoneData
            {
                Name = name,
                Position = monument.transform.position,
                Radius = config.DefaultRadius,
                Rules = CloneRules(config.DefaultRules)
            };

            zones[monument.transform.position] = zone;
            return true;
        }
        #endregion
    }
}
