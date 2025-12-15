using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Vehicle Crafting", "Custom", "1.0.0")]
    [Description("Allows players to craft vehicles like boats, helicopters, and more")]
    class VehicleCrafting : RustPlugin
    {
        #region Configuration
        private Configuration config;

        private class CraftingRecipe
        {
            [JsonProperty("Enabled")]
            public bool Enabled { get; set; } = true;

            [JsonProperty("Display Name")]
            public string DisplayName { get; set; }

            [JsonProperty("Prefab")]
            public string Prefab { get; set; }

            [JsonProperty("Permission")]
            public string Permission { get; set; }

            [JsonProperty("Cooldown Seconds")]
            public int Cooldown { get; set; } = 300;

            [JsonProperty("Ingredients (shortname: amount)")]
            public Dictionary<string, int> Ingredients { get; set; } = new Dictionary<string, int>();
        }

        private class Configuration
        {
            [JsonProperty("Chat Command")]
            public string ChatCommand { get; set; } = "craft";

            [JsonProperty("Require Building Privilege")]
            public bool RequireBuildingPrivilege { get; set; } = true;

            [JsonProperty("Spawn Distance From Player")]
            public float SpawnDistance { get; set; } = 3f;

            [JsonProperty("Vehicles")]
            public Dictionary<string, CraftingRecipe> Vehicles { get; set; }
        }

        protected override void LoadDefaultConfig()
        {
            config = new Configuration
            {
                Vehicles = new Dictionary<string, CraftingRecipe>
                {
                    ["rowboat"] = new CraftingRecipe
                    {
                        DisplayName = "Rowboat",
                        Prefab = "assets/content/vehicles/boats/rowboat/rowboat.prefab",
                        Permission = "vehiclecrafting.rowboat",
                        Cooldown = 300,
                        Ingredients = new Dictionary<string, int>
                        {
                            ["wood"] = 300,
                            ["metal.fragments"] = 100,
                            ["rope"] = 1
                        }
                    },
                    ["rhib"] = new CraftingRecipe
                    {
                        DisplayName = "RHIB",
                        Prefab = "assets/content/vehicles/boats/rhib/rhib.prefab",
                        Permission = "vehiclecrafting.rhib",
                        Cooldown = 600,
                        Ingredients = new Dictionary<string, int>
                        {
                            ["wood"] = 500,
                            ["metal.fragments"] = 300,
                            ["metal.refined"] = 5,
                            ["rope"] = 2
                        }
                    },
                    ["kayak"] = new CraftingRecipe
                    {
                        DisplayName = "Kayak",
                        Prefab = "assets/content/vehicles/boats/kayak/kayak.prefab",
                        Permission = "vehiclecrafting.kayak",
                        Cooldown = 180,
                        Ingredients = new Dictionary<string, int>
                        {
                            ["wood"] = 150,
                            ["cloth"] = 50
                        }
                    },
                    ["tugboat"] = new CraftingRecipe
                    {
                        DisplayName = "Tugboat",
                        Prefab = "assets/content/vehicles/boats/tugboat/tugboat.prefab",
                        Permission = "vehiclecrafting.tugboat",
                        Cooldown = 900,
                        Ingredients = new Dictionary<string, int>
                        {
                            ["wood"] = 1000,
                            ["metal.fragments"] = 500,
                            ["metal.refined"] = 25,
                            ["gears"] = 3
                        }
                    },
                    ["minicopter"] = new CraftingRecipe
                    {
                        DisplayName = "Minicopter",
                        Prefab = "assets/content/vehicles/minicopter/minicopter.entity.prefab",
                        Permission = "vehiclecrafting.minicopter",
                        Cooldown = 600,
                        Ingredients = new Dictionary<string, int>
                        {
                            ["metal.fragments"] = 500,
                            ["metal.refined"] = 10,
                            ["gears"] = 2,
                            ["roadsigns"] = 3
                        }
                    },
                    ["scraptransport"] = new CraftingRecipe
                    {
                        DisplayName = "Scrap Transport Helicopter",
                        Prefab = "assets/content/vehicles/scrap heli carrier/scraptransporthelicopter.prefab",
                        Permission = "vehiclecrafting.scraptransport",
                        Cooldown = 900,
                        Ingredients = new Dictionary<string, int>
                        {
                            ["metal.fragments"] = 1000,
                            ["metal.refined"] = 25,
                            ["gears"] = 5,
                            ["roadsigns"] = 5,
                            ["sewingkit"] = 3
                        }
                    },
                    ["attackheli"] = new CraftingRecipe
                    {
                        DisplayName = "Attack Helicopter",
                        Prefab = "assets/content/vehicles/attackhelicopter/attackhelicopter.entity.prefab",
                        Permission = "vehiclecrafting.attackheli",
                        Cooldown = 1200,
                        Ingredients = new Dictionary<string, int>
                        {
                            ["metal.fragments"] = 1500,
                            ["metal.refined"] = 50,
                            ["gears"] = 8,
                            ["roadsigns"] = 8,
                            ["techparts"] = 3
                        }
                    },
                    ["hotairballoon"] = new CraftingRecipe
                    {
                        DisplayName = "Hot Air Balloon",
                        Prefab = "assets/prefabs/deployable/hot air balloon/hotairballoon.prefab",
                        Permission = "vehiclecrafting.hotairballoon",
                        Cooldown = 600,
                        Ingredients = new Dictionary<string, int>
                        {
                            ["cloth"] = 300,
                            ["wood"] = 200,
                            ["rope"] = 5,
                            ["metal.fragments"] = 100
                        }
                    },
                    ["sedan"] = new CraftingRecipe
                    {
                        DisplayName = "Sedan",
                        Prefab = "assets/content/vehicles/sedan_a/sedantest.entity.prefab",
                        Permission = "vehiclecrafting.sedan",
                        Cooldown = 600,
                        Ingredients = new Dictionary<string, int>
                        {
                            ["metal.fragments"] = 600,
                            ["metal.refined"] = 15,
                            ["gears"] = 3,
                            ["roadsigns"] = 4
                        }
                    },
                    ["snowmobile"] = new CraftingRecipe
                    {
                        DisplayName = "Snowmobile",
                        Prefab = "assets/content/vehicles/snowmobiles/snowmobile.prefab",
                        Permission = "vehiclecrafting.snowmobile",
                        Cooldown = 450,
                        Ingredients = new Dictionary<string, int>
                        {
                            ["metal.fragments"] = 400,
                            ["metal.refined"] = 8,
                            ["gears"] = 2
                        }
                    },
                    ["motorbike"] = new CraftingRecipe
                    {
                        DisplayName = "Motorbike",
                        Prefab = "assets/content/vehicles/bikes/motorbike.prefab",
                        Permission = "vehiclecrafting.motorbike",
                        Cooldown = 300,
                        Ingredients = new Dictionary<string, int>
                        {
                            ["metal.fragments"] = 300,
                            ["metal.refined"] = 5,
                            ["gears"] = 1
                        }
                    },
                    ["pedalbike"] = new CraftingRecipe
                    {
                        DisplayName = "Pedal Bike",
                        Prefab = "assets/content/vehicles/bikes/pedalbike.prefab",
                        Permission = "vehiclecrafting.pedalbike",
                        Cooldown = 120,
                        Ingredients = new Dictionary<string, int>
                        {
                            ["metal.fragments"] = 150,
                            ["gears"] = 1
                        }
                    }
                }
            };
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
                PrintError("Configuration file is corrupt, generating new one...");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig() => Config.WriteObject(config);
        #endregion

        #region Data
        private Dictionary<ulong, Dictionary<string, double>> cooldowns = new Dictionary<ulong, Dictionary<string, double>>();
        #endregion

        #region Hooks
        private void Init()
        {
            foreach (var vehicle in config.Vehicles)
            {
                if (!string.IsNullOrEmpty(vehicle.Value.Permission))
                    permission.RegisterPermission(vehicle.Value.Permission, this);
            }

            cmd.AddChatCommand(config.ChatCommand, this, nameof(CmdCraftVehicle));
        }

        private void OnServerSave() => SaveCooldowns();
        private void Unload() => SaveCooldowns();

        private void OnServerInitialized()
        {
            LoadCooldowns();
        }
        #endregion

        #region Commands
        private void CmdCraftVehicle(BasePlayer player, string command, string[] args)
        {
            if (args.Length == 0)
            {
                ShowAvailableVehicles(player);
                return;
            }

            string vehicleKey = args[0].ToLower();
            
            if (!config.Vehicles.TryGetValue(vehicleKey, out CraftingRecipe recipe))
            {
                SendReply(player, lang.GetMessage("VehicleNotFound", this, player.UserIDString));
                ShowAvailableVehicles(player);
                return;
            }

            if (!recipe.Enabled)
            {
                SendReply(player, lang.GetMessage("VehicleDisabled", this, player.UserIDString));
                return;
            }

            if (!string.IsNullOrEmpty(recipe.Permission) && !permission.UserHasPermission(player.UserIDString, recipe.Permission))
            {
                SendReply(player, lang.GetMessage("NoPermission", this, player.UserIDString));
                return;
            }

            if (config.RequireBuildingPrivilege && !player.IsBuildingAuthed())
            {
                SendReply(player, lang.GetMessage("NoBuildingPrivilege", this, player.UserIDString));
                return;
            }

            double remainingCooldown = GetRemainingCooldown(player.userID, vehicleKey, recipe.Cooldown);
            if (remainingCooldown > 0)
            {
                SendReply(player, string.Format(lang.GetMessage("OnCooldown", this, player.UserIDString), FormatTime(remainingCooldown)));
                return;
            }

            if (!HasIngredients(player, recipe.Ingredients))
            {
                SendReply(player, lang.GetMessage("MissingIngredients", this, player.UserIDString));
                ShowRequiredIngredients(player, recipe);
                return;
            }

            TakeIngredients(player, recipe.Ingredients);
            SpawnVehicle(player, recipe, vehicleKey);
        }
        #endregion

        #region Core Methods
        private void ShowAvailableVehicles(BasePlayer player)
        {
            string msg = lang.GetMessage("AvailableVehicles", this, player.UserIDString) + "\n";
            
            foreach (var kvp in config.Vehicles)
            {
                if (!kvp.Value.Enabled) continue;
                if (!string.IsNullOrEmpty(kvp.Value.Permission) && !permission.UserHasPermission(player.UserIDString, kvp.Value.Permission))
                    continue;

                msg += $"<color=#55aaff>/{config.ChatCommand} {kvp.Key}</color> - {kvp.Value.DisplayName}\n";
            }

            SendReply(player, msg);
        }

        private void ShowRequiredIngredients(BasePlayer player, CraftingRecipe recipe)
        {
            string msg = lang.GetMessage("RequiredIngredients", this, player.UserIDString) + "\n";
            
            foreach (var ingredient in recipe.Ingredients)
            {
                var itemDef = ItemManager.FindItemDefinition(ingredient.Key);
                string itemName = itemDef != null ? itemDef.displayName.english : ingredient.Key;
                int playerHas = GetPlayerItemCount(player, ingredient.Key);
                string color = playerHas >= ingredient.Value ? "#55ff55" : "#ff5555";
                msg += $"<color={color}>{itemName}: {playerHas}/{ingredient.Value}</color>\n";
            }

            SendReply(player, msg);
        }

        private bool HasIngredients(BasePlayer player, Dictionary<string, int> ingredients)
        {
            foreach (var ingredient in ingredients)
            {
                if (GetPlayerItemCount(player, ingredient.Key) < ingredient.Value)
                    return false;
            }
            return true;
        }

        private int GetPlayerItemCount(BasePlayer player, string shortname)
        {
            var itemDef = ItemManager.FindItemDefinition(shortname);
            if (itemDef == null) return 0;
            return player.inventory.GetAmount(itemDef.itemid);
        }

        private void TakeIngredients(BasePlayer player, Dictionary<string, int> ingredients)
        {
            foreach (var ingredient in ingredients)
            {
                var itemDef = ItemManager.FindItemDefinition(ingredient.Key);
                if (itemDef != null)
                    player.inventory.Take(null, itemDef.itemid, ingredient.Value);
            }
        }

        private void SpawnVehicle(BasePlayer player, CraftingRecipe recipe, string vehicleKey)
        {
            Vector3 spawnPos = GetSpawnPosition(player, recipe.Prefab);
            
            BaseEntity vehicle = GameManager.server.CreateEntity(recipe.Prefab, spawnPos, player.transform.rotation);
            if (vehicle == null)
            {
                SendReply(player, lang.GetMessage("SpawnFailed", this, player.UserIDString));
                RefundIngredients(player, recipe.Ingredients);
                return;
            }

            vehicle.OwnerID = player.userID;
            vehicle.Spawn();

            SetCooldown(player.userID, vehicleKey);
            SendReply(player, string.Format(lang.GetMessage("VehicleCrafted", this, player.UserIDString), recipe.DisplayName));
        }

        private Vector3 GetSpawnPosition(BasePlayer player, string prefab)
        {
            Vector3 forward = player.eyes.HeadForward();
            forward.y = 0;
            forward.Normalize();

            Vector3 spawnPos = player.transform.position + (forward * config.SpawnDistance);

            // Check if it's a water vehicle
            if (prefab.Contains("boat") || prefab.Contains("kayak") || prefab.Contains("rhib") || prefab.Contains("tugboat"))
            {
                // Try to find water level
                RaycastHit hit;
                if (Physics.Raycast(spawnPos + Vector3.up * 100f, Vector3.down, out hit, 200f, LayerMask.GetMask("Water")))
                {
                    spawnPos.y = hit.point.y + 0.5f;
                }
                else
                {
                    spawnPos.y = TerrainMeta.WaterMap.GetHeight(spawnPos) + 0.5f;
                }
            }
            else if (prefab.Contains("minicopter") || prefab.Contains("helicopter") || prefab.Contains("balloon"))
            {
                // Air vehicles spawn slightly above ground
                spawnPos.y = TerrainMeta.HeightMap.GetHeight(spawnPos) + 1f;
            }
            else
            {
                // Ground vehicles
                spawnPos.y = TerrainMeta.HeightMap.GetHeight(spawnPos) + 0.5f;
            }

            return spawnPos;
        }

        private void RefundIngredients(BasePlayer player, Dictionary<string, int> ingredients)
        {
            foreach (var ingredient in ingredients)
            {
                var itemDef = ItemManager.FindItemDefinition(ingredient.Key);
                if (itemDef != null)
                {
                    Item item = ItemManager.Create(itemDef, ingredient.Value);
                    player.GiveItem(item);
                }
            }
        }
        #endregion

        #region Cooldown Management
        private double GetRemainingCooldown(ulong playerId, string vehicleKey, int cooldownSeconds)
        {
            if (!cooldowns.TryGetValue(playerId, out var playerCooldowns))
                return 0;

            if (!playerCooldowns.TryGetValue(vehicleKey, out double lastUsed))
                return 0;

            double elapsed = GetCurrentTime() - lastUsed;
            return Math.Max(0, cooldownSeconds - elapsed);
        }

        private void SetCooldown(ulong playerId, string vehicleKey)
        {
            if (!cooldowns.ContainsKey(playerId))
                cooldowns[playerId] = new Dictionary<string, double>();

            cooldowns[playerId][vehicleKey] = GetCurrentTime();
        }

        private double GetCurrentTime() => DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;

        private string FormatTime(double seconds)
        {
            TimeSpan ts = TimeSpan.FromSeconds(seconds);
            if (ts.TotalHours >= 1)
                return $"{(int)ts.TotalHours}h {ts.Minutes}m";
            if (ts.TotalMinutes >= 1)
                return $"{ts.Minutes}m {ts.Seconds}s";
            return $"{ts.Seconds}s";
        }

        private void SaveCooldowns()
        {
            Interface.Oxide.DataFileSystem.WriteObject("VehicleCrafting_Cooldowns", cooldowns);
        }

        private void LoadCooldowns()
        {
            try
            {
                cooldowns = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, Dictionary<string, double>>>("VehicleCrafting_Cooldowns") 
                    ?? new Dictionary<ulong, Dictionary<string, double>>();
            }
            catch
            {
                cooldowns = new Dictionary<ulong, Dictionary<string, double>>();
            }
        }
        #endregion

        #region Localization
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["AvailableVehicles"] = "<color=#ffaa00>Available Vehicles to Craft:</color>",
                ["VehicleNotFound"] = "<color=#ff5555>Vehicle not found!</color>",
                ["VehicleDisabled"] = "<color=#ff5555>This vehicle is currently disabled.</color>",
                ["NoPermission"] = "<color=#ff5555>You don't have permission to craft this vehicle.</color>",
                ["NoBuildingPrivilege"] = "<color=#ff5555>You must be in a building privileged area to craft vehicles.</color>",
                ["OnCooldown"] = "<color=#ff5555>You must wait {0} before crafting this vehicle again.</color>",
                ["MissingIngredients"] = "<color=#ff5555>You don't have the required materials!</color>",
                ["RequiredIngredients"] = "<color=#ffaa00>Required Materials:</color>",
                ["VehicleCrafted"] = "<color=#55ff55>You have crafted a {0}!</color>",
                ["SpawnFailed"] = "<color=#ff5555>Failed to spawn vehicle. Materials refunded.</color>"
            }, this);
        }
        #endregion
    }
}
