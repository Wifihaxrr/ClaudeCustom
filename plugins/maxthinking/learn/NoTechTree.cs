using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("No Tech Tree", "Sche1sseHund", 1.3)]
    [Description("Completely disables the tech tree - players cannot learn anything from it")]

    class NoTechTree : RustPlugin
    {
        private const string PERMISSION_BYPASS = "notechtree.bypass";

        private Configuration config;

        private class Configuration
        {
            [JsonProperty("Block Tech Tree Unlocking")]
            public bool BlockTechTree { get; set; } = true;

            [JsonProperty("Block Research Table")]
            public bool BlockResearchTable { get; set; } = false;

            [JsonProperty("Block Experimenting at Workbench")]
            public bool BlockExperimenting { get; set; } = false;
        }

        protected override void LoadDefaultConfig()
        {
            config = new Configuration();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) throw new Exception();
                SaveConfig();
            }
            catch
            {
                PrintError("Config file is corrupt, generating new one...");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig() => Config.WriteObject(config);

        private void Init()
        {
            permission.RegisterPermission(PERMISSION_BYPASS, this);

            if (!config.BlockResearchTable)
                Unsubscribe(nameof(CanResearchItem));

            if (!config.BlockExperimenting)
                Unsubscribe(nameof(CanExperiment));
        }

        // Block tech tree node unlocking
        private object CanUnlockTechTreeNode(BasePlayer player, TechTreeData.NodeInstance node, TechTreeData techTree)
        {
            if (!config.BlockTechTree)
                return null;

            if (permission.UserHasPermission(player.UserIDString, PERMISSION_BYPASS))
                return null;

            PrintToChat(player, lang.GetMessage("NoTechTree", this, player.UserIDString));
            return false;
        }

        // Block tech tree node unlocking (alternative hook for some versions)
        private object OnTechTreeNodeUnlock(Workbench workbench, TechTreeData.NodeInstance node, BasePlayer player)
        {
            if (!config.BlockTechTree)
                return null;

            if (permission.UserHasPermission(player.UserIDString, PERMISSION_BYPASS))
                return null;

            PrintToChat(player, lang.GetMessage("NoTechTree", this, player.UserIDString));
            return false;
        }

        // Block research table usage (optional)
        private object CanResearchItem(BasePlayer player, Item item)
        {
            if (permission.UserHasPermission(player.UserIDString, PERMISSION_BYPASS))
                return null;

            PrintToChat(player, lang.GetMessage("NoResearch", this, player.UserIDString));
            return false;
        }

        // Block experimenting at workbench (optional)
        private object CanExperiment(BasePlayer player, Workbench workbench)
        {
            if (permission.UserHasPermission(player.UserIDString, PERMISSION_BYPASS))
                return null;

            PrintToChat(player, lang.GetMessage("NoExperiment", this, player.UserIDString));
            return false;
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NoTechTree"] = "<color=#ff5555>The Tech Tree has been disabled on this server.</color>",
                ["NoResearch"] = "<color=#ff5555>Research Table has been disabled on this server.</color>",
                ["NoExperiment"] = "<color=#ff5555>Experimenting has been disabled on this server.</color>"
            }, this, "en");

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NoTechTree"] = "<color=#ff5555>El árbol tecnológico se ha desactivado.</color>",
                ["NoResearch"] = "<color=#ff5555>La mesa de investigación ha sido desactivada.</color>",
                ["NoExperiment"] = "<color=#ff5555>La experimentación ha sido desactivada.</color>"
            }, this, "es");

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NoTechTree"] = "<color=#ff5555>Der Forschungsbaum wurde deaktiviert.</color>",
                ["NoResearch"] = "<color=#ff5555>Der Forschungstisch wurde deaktiviert.</color>",
                ["NoExperiment"] = "<color=#ff5555>Das Experimentieren wurde deaktiviert.</color>"
            }, this, "de");
        }
    }
}
