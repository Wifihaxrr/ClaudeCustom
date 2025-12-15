using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("PluginName", "YourName", "1.0.0")]
    [Description("Description of your plugin")]
    public class PluginName : RustPlugin
    {
        #region Fields
        
        private static PluginName Instance;
        private Configuration config;
        
        private const string PERM_USE = "pluginname.use";
        private const string PERM_ADMIN = "pluginname.admin";
        
        #endregion

        #region Configuration
        
        private class Configuration
        {
            [JsonProperty("Enable Plugin")]
            public bool Enabled = true;
            
            [JsonProperty("Cooldown Seconds")]
            public float Cooldown = 60f;
            
            // TODO: Add your config options here with [JsonProperty]
        }
        
        protected override void LoadDefaultConfig()
        {
            config = new Configuration();
            SaveConfig();
        }
        
        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null)
                {
                    LoadDefaultConfig();
                }
            }
            catch
            {
                PrintWarning("Config file is corrupt! Loading defaults...");
                LoadDefaultConfig();
            }
            SaveConfig();
        }
        
        protected override void SaveConfig() => Config.WriteObject(config);
        
        #endregion

        #region Localization
        
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NoPermission"] = "You don't have permission to use this command.",
                ["Cooldown"] = "Please wait {0} seconds before using this again.",
                ["Success"] = "Command executed successfully!",
                // TODO: Add your messages here
            }, this);
        }
        
        private string Lang(string key, string userId = null, params object[] args)
        {
            return string.Format(lang.GetMessage(key, this, userId), args);
        }
        
        private void Message(BasePlayer player, string key, params object[] args)
        {
            if (player != null && player.IsConnected)
                SendReply(player, Lang(key, player.UserIDString, args));
        }
        
        #endregion

        #region Oxide Hooks
        
        private void Init()
        {
            Instance = this;
            
            permission.RegisterPermission(PERM_USE, this);
            permission.RegisterPermission(PERM_ADMIN, this);
            
            // TODO: Register commands if using config-based command names
            // cmd.AddChatCommand(config.CommandName, this, nameof(CmdMain));
        }
        
        private void OnServerInitialized()
        {
            Puts("Plugin loaded successfully!");
            // TODO: Add initialization logic here
        }
        
        private void Unload()
        {
            // CRITICAL: Clean up everything!
            
            // TODO: Destroy UI for all players
            // foreach (var player in BasePlayer.activePlayerList)
            //     CuiHelper.DestroyUi(player, "UI_NAME");
            
            // TODO: Destroy any timers
            // myTimer?.Destroy();
            
            Instance = null;
        }
        
        #endregion

        #region Commands
        
        [ChatCommand("commandname")]
        private void CmdMain(BasePlayer player, string command, string[] args)
        {
            if (player == null) return;
            
            if (!permission.UserHasPermission(player.UserIDString, PERM_USE))
            {
                Message(player, "NoPermission");
                return;
            }
            
            // TODO: Add your command logic here
            
            Message(player, "Success");
        }
        
        #endregion

        #region Helpers
        
        // TODO: Add helper methods here
        
        private BasePlayer FindPlayer(string nameOrId)
        {
            return BasePlayer.activePlayerList.FirstOrDefault(x =>
                x.UserIDString == nameOrId ||
                x.displayName.Contains(nameOrId, StringComparison.OrdinalIgnoreCase));
        }
        
        #endregion
    }
}
