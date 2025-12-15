using System;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("TurretProtection", "ZombiePVE", "1.0.0")]
    [Description("Prevents all turrets from targeting players - they only attack NPCs and zombies")]
    public class TurretProtection : RustPlugin
    {
        #region Oxide Hooks

        private void OnServerInitialized()
        {
            Puts("Turret Protection loaded - Turrets will NOT target players");
        }

        // Block turrets from targeting players
        private object CanBeTargeted(BasePlayer player, MonoBehaviour turret)
        {
            if (player == null) return null;

            // Block ALL turret types from targeting players
            if (turret is AutoTurret || turret is FlameTurret || turret is GunTrap || turret is SamSite)
            {
                return false;
            }

            return null;
        }

        // Block turret damage to players
        private object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null || info == null) return null;

            // Check if victim is a player
            BasePlayer victim = entity as BasePlayer;
            if (victim == null) return null;

            // Check if damage is from a turret
            if (info.Initiator is AutoTurret ||
                info.Initiator is FlameTurret ||
                info.Initiator is GunTrap ||
                info.Initiator is SamSite)
            {
                // Block the damage
                return true;
            }

            return null;
        }

        // When turret is placed, inform the player
        private void OnEntitySpawned(AutoTurret turret)
        {
            if (turret == null) return;

            // Find nearby player who placed it
            var players = BasePlayer.activePlayerList;
            foreach (var player in players)
            {
                if (Vector3.Distance(player.transform.position, turret.transform.position) < 10f)
                {
                    SendReply(player, "<color=#00ff00>Turret Placed!</color>\n<color=#888888>This turret will only attack NPCs and zombies, never players.</color>");
                    break;
                }
            }
        }

        #endregion

        #region Commands

        [ChatCommand("turretinfo")]
        private void CmdTurretInfo(BasePlayer player, string command, string[] args)
        {
            SendReply(player,
                "<color=#ffcc00>=== Turret Information ===</color>\n" +
                "<color=#44ff44>[OK] Auto Turrets</color> - Attack NPCs & zombies only\n" +
                "<color=#44ff44>[OK] Flame Turrets</color> - Attack NPCs & zombies only\n" +
                "<color=#44ff44>[OK] Shotgun Traps</color> - Attack NPCs & zombies only\n" +
                "<color=#44ff44>[OK] SAM Sites</color> - Attack NPCs only\n\n" +
                "<color=#888888>Turrets will NEVER damage players on this server.</color>");
        }

        #endregion
    }
}

