using System.Collections.Generic;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("Triple Gather", "MaxThinking", "1.0.0")]
    [Description("Triples all gathering rates")]
    public class TripleGather : RustPlugin
    {
        private Configuration config;

        private class Configuration
        {
            [JsonProperty("Global Multiplier")]
            public float Multiplier = 3.0f;
        }

        protected override void LoadDefaultConfig() => config = new Configuration();

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) throw new System.Exception();
                SaveConfig();
            }
            catch
            {
                PrintWarning("Config invalid, using defaults");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig() => Config.WriteObject(config, true);

        // Main gather hook - trees, ores, animals
        private void OnDispenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item)
        {
            var player = entity.ToPlayer();
            if (player == null) return;
            
            item.amount = (int)(item.amount * config.Multiplier);
        }

        // Bonus resources from hitting nodes
        private void OnDispenserBonus(ResourceDispenser dispenser, BaseEntity entity, Item item)
        {
            OnDispenserGather(dispenser, entity, item);
        }

        // Quarries
        private void OnQuarryGather(MiningQuarry quarry, Item item)
        {
            item.amount = (int)(item.amount * config.Multiplier);
        }

        // Collectibles (hemp, mushrooms, stone/wood piles on ground)
        private void OnCollectiblePickup(CollectibleEntity collectible, BasePlayer player)
        {
            if (collectible?.itemList == null) return;
            
            foreach (ItemAmount amount in collectible.itemList)
            {
                amount.amount *= config.Multiplier;
            }
        }
    }
}
