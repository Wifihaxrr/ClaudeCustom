using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("ZombieRocketBehavior", "ZombiePVE", "1.0.0")]
    [Description("Makes ZombieHorde zombies use rocket launchers when players hide in bases - rockets damage buildings only")]
    public class ZombieRocketBehavior : RustPlugin
    {
        #region Fields

        [PluginReference] private Plugin ZombieHorde;

        private Configuration config;
        private Dictionary<ulong, ZombieState> zombieStates = new Dictionary<ulong, ZombieState>();
        private HashSet<ulong> zombieRockets = new HashSet<ulong>();
        private Timer updateTimer;

        private const string ROCKET_PREFAB = "assets/prefabs/ammo/rocket/rocket_basic.prefab";

        #endregion

        #region Classes

        private class ZombieState
        {
            public NPCPlayer Zombie;
            public BasePlayer Target;
            public bool HasRocketOut;
            public float LastRocketTime;
            public float LastCheckTime;
        }

        private class Configuration
        {
            [JsonProperty("Distance to switch to rocket (when player in base)")]
            public float RocketSwitchDistance { get; set; } = 15.0f;

            [JsonProperty("Rocket cooldown (seconds)")]
            public float RocketCooldown { get; set; } = 8.0f;

            [JsonProperty("Rocket damage to structures")]
            public float RocketStructureDamage { get; set; } = 75.0f;

            [JsonProperty("Rocket damage to players (should be 0)")]
            public float RocketPlayerDamage { get; set; } = 0.0f;

            [JsonProperty("Check interval (seconds)")]
            public float CheckInterval { get; set; } = 0.5f;

            [JsonProperty("Announce rocket warning to player in chat")]
            public bool AnnounceRocketInChat { get; set; } = false;

            [JsonProperty("Max range for rocket attacks")]
            public float MaxRocketRange { get; set; } = 50.0f;

            [JsonProperty("Rocket speed")]
            public float RocketSpeed { get; set; } = 40.0f;
            
            [JsonProperty("Enable debug logging")]
            public bool DebugLogging { get; set; } = false;
        }

        #endregion

        #region Configuration

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

        #region Oxide Hooks

        private void OnServerInitialized()
        {
            updateTimer = timer.Every(config.CheckInterval, UpdateZombies);
            Puts("Zombie Rocket Behavior loaded - Zombies will use rockets when players hide in bases!");
        }

        private void Unload()
        {
            updateTimer?.Destroy();
            zombieStates.Clear();
            zombieRockets.Clear();
        }

        // Track when ZombieHorde spawns a zombie
        private void OnEntitySpawned(BaseNetworkable entity)
        {
            if (entity == null) return;

            var npc = entity as NPCPlayer;
            if (npc == null) return;

            // Check if this is a ZombieHorde zombie (scarecrow or has zombie in name)
            if (IsZombieHordeNPC(npc))
            {
                timer.Once(0.5f, () =>
                {
                    if (npc != null && !npc.IsDestroyed)
                    {
                        TrackZombie(npc);
                    }
                });
            }
        }

        // When zombie dies, clean up
        private void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null) return;

            var npc = entity as NPCPlayer;
            if (npc != null && npc.net != null && zombieStates.ContainsKey(npc.net.ID.Value))
            {
                zombieStates.Remove(npc.net.ID.Value);
            }
        }

        // Block rocket damage to players
        private object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null || info == null) return null;

            // Check if this is from a zombie rocket
            if (info.Initiator != null && zombieRockets.Contains(info.Initiator.net?.ID.Value ?? 0))
            {
                // If target is a player, block damage
                if (entity is BasePlayer)
                {
                    return true; // Block damage
                }

                // If target is a building, apply custom damage
                if (entity is BuildingBlock || entity is Door || entity is DecayEntity)
                {
                    // Allow damage but could scale it here if needed
                    return null;
                }
            }

            // Also check by weapon prefab
            if (info.WeaponPrefab != null && info.WeaponPrefab.ShortPrefabName.Contains("rocket"))
            {
                var attacker = info.Initiator?.GetComponent<NPCPlayer>();
                if (attacker != null && zombieStates.ContainsKey(attacker.net.ID.Value))
                {
                    if (entity is BasePlayer)
                    {
                        return true; // Block player damage
                    }
                }
            }

            return null;
        }

        // Track rocket projectiles
        private void OnExplosiveThrown(BasePlayer player, BaseEntity entity, ThrownWeapon item)
        {
            TrackZombieProjectile(player, entity);
        }

        private void OnRocketLaunched(BasePlayer player, BaseEntity entity)
        {
            TrackZombieProjectile(player, entity);
        }

        private void TrackZombieProjectile(BasePlayer player, BaseEntity entity)
        {
            if (player == null || entity == null) return;

            var npc = player as NPCPlayer;
            if (npc != null && zombieStates.ContainsKey(npc.net.ID.Value))
            {
                zombieRockets.Add(entity.net.ID.Value);
                
                // Clean up after 30 seconds
                timer.Once(30f, () =>
                {
                    if (entity != null)
                        zombieRockets.Remove(entity.net.ID.Value);
                });
            }
        }

        #endregion

        #region Core Logic

        private void TrackZombie(NPCPlayer npc)
        {
            if (npc == null || npc.IsDestroyed) return;
            if (zombieStates.ContainsKey(npc.net.ID.Value)) return;

            zombieStates[npc.net.ID.Value] = new ZombieState
            {
                Zombie = npc,
                Target = null,
                HasRocketOut = false,
                LastRocketTime = 0f,
                LastCheckTime = 0f
            };
        }

        private void UpdateZombies()
        {
            var toRemove = new List<ulong>();

            foreach (var kvp in zombieStates)
            {
                var state = kvp.Value;

                if (state.Zombie == null || state.Zombie.IsDestroyed)
                {
                    toRemove.Add(kvp.Key);
                    continue;
                }

                UpdateZombieBehavior(state);
            }

            foreach (var id in toRemove)
            {
                zombieStates.Remove(id);
            }
        }

        private void UpdateZombieBehavior(ZombieState state)
        {
            var zombie = state.Zombie;
            if (zombie == null) return;

            // Find target
            var target = FindNearestVisiblePlayer(zombie);
            state.Target = target;

            if (target == null)
            {
                // No target, ensure pitchfork
                if (state.HasRocketOut)
                {
                    SwitchToPitchfork(state);
                }
                return;
            }

            float distance = Vector3.Distance(zombie.transform.position, target.transform.position);
            bool playerInBase = IsPlayerInBase(target);

            // Decision: Rocket or Pitchfork?
            if (playerInBase && distance <= config.MaxRocketRange && distance > config.RocketSwitchDistance)
            {
                // Player hiding in base - use rocket!
                if (!state.HasRocketOut)
                {
                    SwitchToRocket(state);
                }

                // Fire rocket if cooldown ready
                if (Time.time - state.LastRocketTime >= config.RocketCooldown)
                {
                    FireRocket(state, target);
                    state.LastRocketTime = Time.time;
                }
            }
            else
            {
                // Close enough or player not in base - use pitchfork
                if (state.HasRocketOut)
                {
                    SwitchToPitchfork(state);
                }
            }
        }

        private void SwitchToRocket(ZombieState state)
        {
            var zombie = state.Zombie;
            if (zombie == null) return;

            // Only clear belt items (weapons), NOT wear items (keeps zombie appearance)
            if (zombie.inventory?.containerBelt != null)
            {
                foreach (var item in zombie.inventory.containerBelt.itemList.ToList())
                    item?.Remove();
            }

            var launcher = ItemManager.CreateByName("rocket.launcher", 1);
            var ammo = ItemManager.CreateByName("ammo.rocket.basic", 100);

            if (launcher != null)
            {
                if (!launcher.MoveToContainer(zombie.inventory.containerBelt, 0))
                    launcher.Remove();
            }
            if (ammo != null)
            {
                if (!ammo.MoveToContainer(zombie.inventory.containerMain))
                    ammo.Remove();
            }

            // Equip it
            zombie.UpdateActiveItem(zombie.inventory.containerBelt.GetSlot(0)?.uid ?? default);
            state.HasRocketOut = true;

            // Announce (configurable)
            if (config.AnnounceRocketInChat && state.Target != null)
            {
                SendReply(state.Target, 
                    "<color=#ff4444>WARNING!</color> A zombie pulled out a <color=#ffcc00>ROCKET LAUNCHER</color>!\n" +
                    "<color=#888888>Get out of your base or it will be destroyed!</color>\n" +
                    "<color=#44ff44>Rockets can't hurt you - only the pitchfork can!</color>");
            }
        }

        private void SwitchToPitchfork(ZombieState state)
        {
            var zombie = state.Zombie;
            if (zombie == null) return;

            // Only clear belt items (weapons), NOT wear items (keeps zombie appearance)
            if (zombie.inventory?.containerBelt != null)
            {
                foreach (var item in zombie.inventory.containerBelt.itemList.ToList())
                    item?.Remove();
            }

            // Give pitchfork (or sword as fallback)
            var weapon = ItemManager.CreateByName("pitchfork", 1);
            if (weapon == null)
                weapon = ItemManager.CreateByName("salvaged.sword", 1);
            if (weapon == null)
                weapon = ItemManager.CreateByName("machete", 1);
            if (weapon == null)
                weapon = ItemManager.CreateByName("bone.club", 1);

            if (weapon != null)
            {
                if (!weapon.MoveToContainer(zombie.inventory.containerBelt, 0))
                    weapon.Remove();
                else
                    zombie.UpdateActiveItem(weapon.uid);
            }

            state.HasRocketOut = false;
        }

        private void FireRocket(ZombieState state, BasePlayer target)
        {
            var zombie = state.Zombie;
            if (zombie == null || zombie.IsDestroyed || target == null || target.IsDestroyed) return;

            // Calculate aim position (aim at base/building near player)
            Vector3 targetPos = target.transform.position;
            
            // Try to find a building block near the player to aim at
            var buildingTarget = FindBuildingNearPlayer(target);
            if (buildingTarget != Vector3.zero)
            {
                targetPos = buildingTarget;
            }

            // Safe eye position - fallback if eyes is null
            Vector3 eyePos = zombie.eyes?.position ?? (zombie.transform.position + Vector3.up * 1.6f);
            
            Vector3 direction = (targetPos - eyePos).normalized;
            Vector3 spawnPos = eyePos + direction * 1.5f;

            // Create rocket
            var rocket = GameManager.server.CreateEntity(ROCKET_PREFAB, spawnPos, Quaternion.LookRotation(direction)) as TimedExplosive;
            if (rocket == null) return;

            // Set up rocket
            rocket.creatorEntity = zombie;
            rocket.SetFlag(BaseEntity.Flags.On, true);
            
            var projectile = rocket.GetComponent<ServerProjectile>();
            if (projectile != null)
            {
                projectile.InitializeVelocity(direction * config.RocketSpeed);
            }

            rocket.Spawn();

            // Track this rocket (with null check)
            if (rocket.net != null)
            {
                zombieRockets.Add(rocket.net.ID.Value);
                var rocketId = rocket.net.ID.Value;
                timer.Once(30f, () => zombieRockets.Remove(rocketId));
            }

            // Play sound effect (valid rocket launch sound)
            Effect.server.Run("assets/prefabs/npc/patrol helicopter/effects/rocket_fire.prefab", zombie.transform.position);
            
            if (config.DebugLogging)
                Puts($"[Debug] Zombie fired rocket at {target.displayName}'s base");
        }

        #endregion

        #region Helpers

        private bool IsZombieHordeNPC(NPCPlayer npc)
        {
            if (npc == null) return false;

            string prefabName = npc.PrefabName?.ToLower() ?? "";
            
            // Only track scarecrow and murderer prefabs (the zombie types)
            return prefabName.Contains("assets/prefabs/npc/scarecrow/scarecrow.prefab") || 
                   prefabName.Contains("assets/prefabs/npc/murderer/murderer.prefab");
        }

        private BasePlayer FindNearestVisiblePlayer(NPCPlayer zombie)
        {
            if (zombie == null) return null;

            BasePlayer nearest = null;
            float nearestDist = 100f; // Max search range

            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player == null || player.IsSleeping() || player.IsAdmin && player.IsFlying)
                    continue;

                float dist = Vector3.Distance(zombie.transform.position, player.transform.position);
                if (dist > nearestDist) continue;

                // Check line of sight
                if (CanSeePlayer(zombie, player))
                {
                    nearestDist = dist;
                    nearest = player;
                }
            }

            return nearest;
        }

        private bool CanSeePlayer(NPCPlayer zombie, BasePlayer player)
        {
            if (zombie == null || player == null) return false;

            Vector3 eyePos = zombie.eyes?.position ?? zombie.transform.position + Vector3.up * 1.6f;
            Vector3 targetPos = player.eyes?.position ?? player.transform.position + Vector3.up * 1.6f;

            // Raycast to check line of sight
            RaycastHit hit;
            Vector3 direction = (targetPos - eyePos).normalized;
            float distance = Vector3.Distance(eyePos, targetPos);

            if (Physics.Raycast(eyePos, direction, out hit, distance, LayerMask.GetMask("Construction", "Terrain", "World", "Default")))
            {
                // Hit something before reaching player
                if (hit.GetEntity() != player)
                {
                    return false;
                }
            }

            return true;
        }

        private bool IsPlayerInBase(BasePlayer player)
        {
            if (player == null) return false;

            // Check if player has building privilege
            var priv = player.GetBuildingPrivilege();
            if (priv == null || !priv.IsAuthed(player)) return false;

            // Check if there's a structure above them (roof/floor)
            RaycastHit hit;
            Vector3 pos = player.transform.position + Vector3.up * 0.5f;
            
            if (Physics.Raycast(pos, Vector3.up, out hit, 20f, LayerMask.GetMask("Construction")))
            {
                return true; // Has ceiling
            }

            // Also check for walls around
            int wallCount = 0;
            Vector3[] directions = { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };
            
            foreach (var dir in directions)
            {
                if (Physics.Raycast(pos, dir, out hit, 5f, LayerMask.GetMask("Construction")))
                {
                    wallCount++;
                }
            }

            return wallCount >= 2; // At least 2 walls nearby
        }

        private Vector3 FindBuildingNearPlayer(BasePlayer player)
        {
            if (player == null) return Vector3.zero;

            // Find nearest building block
            var blocks = new List<BuildingBlock>();
            Vis.Entities(player.transform.position, 10f, blocks, LayerMask.GetMask("Construction"));

            if (blocks.Count > 0)
            {
                // Find the one closest to player
                var nearest = blocks.OrderBy(b => Vector3.Distance(b.transform.position, player.transform.position)).First();
                return nearest.transform.position;
            }

            return player.transform.position;
        }

        #endregion

        #region Commands

        [ChatCommand("zombieinfo")]
        private void CmdZombieInfo(BasePlayer player, string command, string[] args)
        {
            int total = zombieStates.Count;
            int withRockets = zombieStates.Values.Count(z => z.HasRocketOut);

            SendReply(player,
                "<color=#ff4444>=== ZOMBIE INFO ===</color>\n" +
                $"<color=#ffffff>Tracked zombies: {total}</color>\n" +
                $"<color=#ffcc00>With rocket launchers: {withRockets}</color>\n\n" +
                "<color=#888888>• Zombies attack with pitchforks (melee only)</color>\n" +
                "<color=#888888>• If you hide in your base, they use rockets</color>\n" +
                "<color=#888888>• Rockets damage structures, NOT players</color>\n" +
                "<color=#888888>• Only the pitchfork can kill you!</color>");
        }

        #endregion
    }
}

