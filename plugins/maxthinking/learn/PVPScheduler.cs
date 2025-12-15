using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("PVPScheduler", "ZombiePVE", "1.0.0")]
    [Description("Automatically enables PVP during the last week of every month")]
    public class PVPScheduler : RustPlugin
    {
        #region Fields
        
        private Configuration config;
        private bool isPVPActive = false;
        private Timer checkTimer;
        private const string UI_NAME = "PVPScheduler_Status";
        
        #endregion

        #region Configuration

        private class Configuration
        {
            [JsonProperty("Days before month end to enable PVP")]
            public int DaysBeforeMonthEnd { get; set; } = 7;

            [JsonProperty("Check interval (seconds)")]
            public float CheckInterval { get; set; } = 60f;

            [JsonProperty("PVP start message")]
            public string PVPStartMessage { get; set; } = "<size=18><color=#ff4444>[!] PVP MODE ACTIVATED! [!]</color></size>\n<color=#ffcc00>It's the last week of the month!</color>\n<color=#ffffff>Player vs Player combat is now ENABLED!</color>";

            [JsonProperty("PVP end message")]
            public string PVPEndMessage { get; set; } = "<size=18><color=#44ff44>[OK] PVE MODE RESTORED! [OK]</color></size>\n<color=#ffffff>The new month has begun.</color>\n<color=#88ff88>You are now safe from other players.</color>";

            [JsonProperty("Warning message (1 day before)")]
            public string WarningMessage { get; set; } = "<color=#ffcc00>[!] WARNING:</color> <color=#ffffff>PVP mode will activate tomorrow! Prepare your defenses!</color>";

            [JsonProperty("Enable raiding during PVP")]
            public bool EnableRaidingDuringPVP { get; set; } = true;

            [JsonProperty("Turrets target players during PVP")]
            public bool TurretsTargetPlayersDuringPVP { get; set; } = false;

            [JsonProperty("Show UI status indicator")]
            public bool ShowUIStatus { get; set; } = true;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null)
                    LoadDefaultConfig();
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

        private void Init()
        {
            // Register permissions
            permission.RegisterPermission("pvpscheduler.admin", this);
        }

        private void OnServerInitialized()
        {
            // Check PVP status immediately
            CheckPVPStatus();

            // Start periodic checking
            checkTimer = timer.Every(config.CheckInterval, () => CheckPVPStatus());

            // Update UI for all online players
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (config.ShowUIStatus)
                    CreateStatusUI(player);
            }

            Puts($"PVP Scheduler initialized. Current mode: {(isPVPActive ? "PVP" : "PVE")}");
        }

        private void Unload()
        {
            checkTimer?.Destroy();

            // Remove UI from all players
            foreach (var player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, UI_NAME);
            }
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (player == null) return;

            timer.Once(2f, () =>
            {
                if (player == null || !player.IsConnected) return;

                // Show status UI
                if (config.ShowUIStatus)
                    CreateStatusUI(player);

                // Send welcome message with current status
                string statusMsg = isPVPActive
                    ? "<color=#ff4444>[PVP] PVP MODE IS ACTIVE!</color>\n<color=#ffffff>Be careful - other players can attack you!</color>"
                    : "<color=#44ff44>[PVE] Server is in PVE mode.</color>\n<color=#ffffff>You are safe from other players.</color>";

                SendReply(player, statusMsg);

                // Show days until PVP/PVE changes
                int daysUntil = GetDaysUntilChange();
                if (daysUntil > 0)
                {
                    string changeType = isPVPActive ? "PVE" : "PVP";
                    SendReply(player, $"<color=#888888>{changeType} mode in {daysUntil} day(s)</color>");
                }
            });
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
            if (player != null)
                CuiHelper.DestroyUi(player, UI_NAME);
        }

        #endregion

        #region PVP Logic

        private void CheckPVPStatus()
        {
            DateTime now = DateTime.Now;
            int daysInMonth = DateTime.DaysInMonth(now.Year, now.Month);
            int daysUntilEnd = daysInMonth - now.Day;

            bool shouldBePVP = daysUntilEnd < config.DaysBeforeMonthEnd;

            // Check for warning (1 day before PVP)
            if (!isPVPActive && daysUntilEnd == config.DaysBeforeMonthEnd)
            {
                BroadcastToAll(config.WarningMessage);
            }

            // State change
            if (shouldBePVP != isPVPActive)
            {
                isPVPActive = shouldBePVP;

                if (isPVPActive)
                {
                    OnPVPEnabled();
                }
                else
                {
                    OnPVPDisabled();
                }

                // Update UI for all players
                foreach (var player in BasePlayer.activePlayerList)
                {
                    if (config.ShowUIStatus)
                        CreateStatusUI(player);
                }
            }
        }

        private void OnPVPEnabled()
        {
            Puts("PVP Mode ENABLED - Last week of month");
            BroadcastToAll(config.PVPStartMessage);

            // Notify other plugins
            Interface.CallHook("OnPVPModeEnabled");

            // Try to interact with TruePVE
            var truePVE = plugins.Find("TruePVE");
            if (truePVE != null)
            {
                Puts("Notifying TruePVE of PVP mode");
            }

            var nextGenPVE = plugins.Find("NextGenPVE");
            if (nextGenPVE != null)
            {
                Puts("Notifying NextGenPVE of PVP mode");
            }
        }

        private void OnPVPDisabled()
        {
            Puts("PVP Mode DISABLED - New month started");
            BroadcastToAll(config.PVPEndMessage);

            // Notify other plugins
            Interface.CallHook("OnPVPModeDisabled");
        }

        private int GetDaysUntilChange()
        {
            DateTime now = DateTime.Now;
            int daysInMonth = DateTime.DaysInMonth(now.Year, now.Month);
            int daysUntilEnd = daysInMonth - now.Day;

            if (isPVPActive)
            {
                // Days until PVE (end of month + 1)
                return daysUntilEnd + 1;
            }
            else
            {
                // Days until PVP
                return daysUntilEnd - config.DaysBeforeMonthEnd + 1;
            }
        }

        #endregion

        #region Damage Handling

        private object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null || info == null) return null;

            // Get victim and attacker
            BasePlayer victim = entity as BasePlayer;
            BasePlayer attacker = info?.InitiatorPlayer;

            // Allow damage to/from NPCs (scientists, zombies, etc.)
            // NPCs don't have Steam IDs
            bool victimIsNPC = victim != null && !victim.userID.IsSteamId();
            bool attackerIsNPC = attacker != null && !attacker.userID.IsSteamId();
            
            // Always allow: Player vs NPC, NPC vs Player, NPC vs NPC
            if (victimIsNPC || attackerIsNPC)
            {
                return null; // Allow NPC combat
            }

            // Player vs Player damage (both have Steam IDs)
            if (victim != null && attacker != null && victim != attacker)
            {
                // Check if they're on the same team
                if (victim.currentTeam != 0 && victim.currentTeam == attacker.currentTeam)
                    return null; // Allow team damage

                if (!isPVPActive)
                {
                    // PVE mode - block PVP damage
                    return true;
                }
            }

            // Turret damage to real players only
            if (victim != null && victim.userID.IsSteamId() && !config.TurretsTargetPlayersDuringPVP)
            {
                if (info.Initiator is AutoTurret || info.Initiator is FlameTurret || info.Initiator is GunTrap)
                {
                    return true; // Block turret damage to players
                }
            }

            // Building damage (raiding)
            if (!isPVPActive || !config.EnableRaidingDuringPVP)
            {
                var buildingBlock = entity as BuildingBlock;
                var deployable = entity as DecayEntity;

                if ((buildingBlock != null || deployable != null) && attacker != null)
                {
                    // Check if attacker owns this
                    var privilege = entity.GetBuildingPrivilege();
                    if (privilege != null && !privilege.IsAuthed(attacker))
                    {
                        if (!isPVPActive)
                            return true; // Block raid damage in PVE
                    }
                }
            }

            return null;
        }

        // Prevent turret targeting players
        private object CanBeTargeted(BasePlayer player, MonoBehaviour turret)
        {
            if (player == null || turret == null) return null;

            // Always block turret targeting players
            if (!config.TurretsTargetPlayersDuringPVP)
            {
                if (turret is AutoTurret || turret is FlameTurret || turret is GunTrap)
                {
                    return false;
                }
            }

            return null;
        }

        #endregion

        #region UI

        private void CreateStatusUI(BasePlayer player)
        {
            if (player == null) return;

            CuiHelper.DestroyUi(player, UI_NAME);

            var elements = new CuiElementContainer();

            string statusText = isPVPActive ? "PVP" : "PVE";
            string bgColor = isPVPActive ? "0.6 0.1 0.1 0.85" : "0.1 0.4 0.1 0.85";
            string textColor = "1 1 1 1";

            // Background - top left corner
            elements.Add(new CuiPanel
            {
                Image = { Color = bgColor, Material = "assets/content/ui/uibackgroundblur.mat" },
                RectTransform = { AnchorMin = "0.005 0.955", AnchorMax = "0.045 0.99" },
                CursorEnabled = false
            }, "Overlay", UI_NAME);

            // Status text
            elements.Add(new CuiLabel
            {
                Text = { Text = statusText, FontSize = 14, Align = TextAnchor.MiddleCenter, Color = textColor },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
            }, UI_NAME);

            CuiHelper.AddUi(player, elements);
        }

        #endregion

        #region Commands

        [ChatCommand("pvp")]
        private void CmdPVPStatus(BasePlayer player, string command, string[] args)
        {
            DateTime now = DateTime.Now;
            int daysInMonth = DateTime.DaysInMonth(now.Year, now.Month);
            int pvpStartDay = daysInMonth - config.DaysBeforeMonthEnd + 1;
            int daysUntil = GetDaysUntilChange();

            string status;
            if (isPVPActive)
            {
                status = $"<color=#ff4444>[PVP] PVP is currently ACTIVE!</color>\n" +
                         $"<color=#ffffff>PVE mode returns on the 1st of next month.</color>\n" +
                         $"<color=#888888>({daysUntil} day(s) remaining)</color>";
            }
            else
            {
                status = $"<color=#44ff44>[PVE] Server is currently in PVE mode.</color>\n" +
                         $"<color=#ffffff>PVP will activate on day {pvpStartDay} of this month.</color>\n" +
                         $"<color=#888888>({daysUntil} day(s) until PVP)</color>";
            }

            SendReply(player, $"<size=14><color=#ffcc00>═══ PVP Status ═══</color></size>\n{status}\n<color=#666666>Current date: {now:MMMM d, yyyy}</color>");
        }

        [ConsoleCommand("pvp.toggle")]
        private void CmdToggle(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin)
            {
                arg.ReplyWith("Admin only command.");
                return;
            }

            isPVPActive = !isPVPActive;

            if (isPVPActive)
                OnPVPEnabled();
            else
                OnPVPDisabled();

            foreach (var player in BasePlayer.activePlayerList)
            {
                if (config.ShowUIStatus)
                    CreateStatusUI(player);
            }

            arg.ReplyWith($"PVP mode manually set to: {(isPVPActive ? "ENABLED" : "DISABLED")}");
        }

        [ConsoleCommand("pvp.status")]
        private void CmdStatus(ConsoleSystem.Arg arg)
        {
            DateTime now = DateTime.Now;
            int daysInMonth = DateTime.DaysInMonth(now.Year, now.Month);
            int pvpStartDay = daysInMonth - config.DaysBeforeMonthEnd + 1;

            arg.ReplyWith($"PVP Status: {(isPVPActive ? "ACTIVE" : "INACTIVE")}\n" +
                          $"Date: {now:yyyy-MM-dd}\n" +
                          $"Days in month: {daysInMonth}\n" +
                          $"PVP starts day: {pvpStartDay}\n" +
                          $"Config days before end: {config.DaysBeforeMonthEnd}");
        }

        #endregion

        #region API

        private bool IsPVPActive() => isPVPActive;
        private int GetDaysUntilPVP() => isPVPActive ? 0 : GetDaysUntilChange();
        private int GetDaysUntilPVE() => isPVPActive ? GetDaysUntilChange() : 0;

        #endregion

        #region Helpers

        private void BroadcastToAll(string message)
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                SendReply(player, message);
            }
            Puts(message.Replace("<color=#", "").Replace("</color>", "").Replace("<size=18>", "").Replace("</size>", ""));
        }

        #endregion
    }
}
