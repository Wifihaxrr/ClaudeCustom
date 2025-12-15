using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Facepunch;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
	[Info("ServerPanel Migrations", "Mevent", "1.0.0")]
	public class ServerPanelMigrations : RustPlugin
	{
        #region Fields

		[PluginReference] private Plugin ServerPanel;
		
		private Dictionary<string, IMigration> availableMigrations;
        
        private static ServerPanelMigrations Instance;

        #endregion

        #region Hooks

		private void Init()
		{
            Instance = this;

			InitializeMigrations();
		}

        private void Unload()
        {
            Instance = null;
        }

        #endregion

        #region Commands

        [ConsoleCommand("sp.migrations")]
		private void CmdConsoleMigrations(ConsoleSystem.Arg arg)
		{
            if (!arg.IsServerside) return;

			var command = arg.GetString(0);
			
			switch (command?.ToLower())
			{
				case "list":
					ListMigrations(arg);
					break;
				case "run":
					RunMigration(arg);
					break;
				default:
					ShowHelp(arg);
					break;
			}
		}

		private void ListMigrations(ConsoleSystem.Arg arg)
		{			
			if (availableMigrations.Count == 0)
			{
				SendReply(arg, "No migrations available");
				return;
			}

            var sb = Pool.Get<StringBuilder>();
            try
            {
                sb.Append("Available migrations:");
                sb.AppendLine();

                foreach (var kvp in availableMigrations)
                {
                    var migration = kvp.Value;

                    sb.AppendLine($"{kvp.Key}: {migration.Name}");
                    sb.AppendLine($"  Needed: {(migration.IsNeeded() ? "YES" : "NO")}");
                    sb.AppendLine();
                }

                SendReply(arg, sb.ToString());
            }
            finally
            {
                Pool.FreeUnmanaged(ref sb);
            }
		}

		private void RunMigration(ConsoleSystem.Arg arg)
		{
			var migrationKey = arg.GetString(1);
			if (string.IsNullOrEmpty(migrationKey))
			{
				SendReply(arg, "Specify migration key. Use 'sp.migrations list' to see available migrations");
				return;
			}

			if (!availableMigrations.TryGetValue(migrationKey, out var migration))
			{
				SendReply(arg, $"Migration '{migrationKey}' not found");
				return;
			}

			var force = arg.GetString(2) == "force";
			
			SendReply(arg, $"Running migration: {migration.Name}");
			if (force)
				SendReply(arg, "WARNING: Forced execution!");

			timer.Once(1f, () => ExecuteMigration(migration, force));
		}

		private void ShowHelp(ConsoleSystem.Arg arg)
		{
            var sb = Pool.Get<StringBuilder>();
            try
            {
                sb.Append("Usage: sp.migrations <command> [parameters]");
                sb.AppendLine();
                sb.AppendLine("Commands:");
                sb.AppendLine("  list - show all available migrations");
                sb.AppendLine("  run <key> [force] - execute migration");
                sb.AppendLine("  info - show system information");
			    
                SendReply(arg, sb.ToString());
            }
            finally
            {
                Pool.FreeUnmanaged(ref sb);
            }
		}

        #endregion

        #region Migrations

		public interface IMigration
		{
			string Name { get; }
			
			bool IsNeeded();
			void Execute();
		}

		public class CategoryTitleUiRefactoring : IMigration
		{
			public string Name => "1.3.0";

			public bool IsNeeded()
			{
				try
				{
					var templatePath = Path.Combine(Interface.Oxide.DataDirectory, "ServerPanel", "Template.json");
					if (!File.Exists(templatePath))
						return false;

					var templateJson = File.ReadAllText(templatePath);
					var templateData = JsonConvert.DeserializeObject<JObject>(templateJson);

					var categoryTitleToken = templateData?["UI Settings"]?["Categories"]?["Category Title"];
					if (categoryTitleToken is JObject categoryTitleObj)
					{
						return categoryTitleObj.Property("Background Color") != null ||
						       categoryTitleObj.Property("Selected Background Color") != null ||
						       categoryTitleObj.Property("Background Sprite") != null ||
						       categoryTitleObj.Property("Background Material") != null;
					}
					return false;
				}
				catch
				{
					return false;
				}
			}

			public void Execute()
			{
				var templatePath = Path.Combine(Interface.Oxide.DataDirectory, "ServerPanel", "Template.json");
				
				var templateJson = File.ReadAllText(templatePath);
				var templateData = JsonConvert.DeserializeObject<JObject>(templateJson);

				MigrateCategoryTitleUi(templateData);

				var updatedJson = JsonConvert.SerializeObject(templateData, Formatting.Indented);
				File.WriteAllText(templatePath, updatedJson);
			}

			private void MigrateCategoryTitleUi(JObject templateData)
			{
				var categoryTitleToken = templateData?["UI Settings"]?["Categories"]?["Category Title"];
				if (categoryTitleToken is not JObject categoryTitleObj)
					return;

				var backgroundObj = new JObject();
				var selectedBackgroundObj = new JObject();

				if (categoryTitleObj.TryGetValue("Background Color", out var backgroundColorToken))
				{
					backgroundObj["Color"] = backgroundColorToken;
					categoryTitleObj.Remove("Background Color");
				}

				if (categoryTitleObj.TryGetValue("Selected Background Color", out var selectedBackgroundColorToken))
				{
					selectedBackgroundObj["Color"] = selectedBackgroundColorToken;
					categoryTitleObj.Remove("Selected Background Color");
				}

				if (categoryTitleObj.TryGetValue("Background Sprite", out var backgroundSpriteToken))
				{
					var spriteValue = backgroundSpriteToken.Value<string>();
					if (!string.IsNullOrEmpty(spriteValue))
					{
						backgroundObj["Sprite"] = spriteValue;
						selectedBackgroundObj["Sprite"] = spriteValue;
					}
					categoryTitleObj.Remove("Background Sprite");
				}

				if (categoryTitleObj.TryGetValue("Background Material", out var backgroundMaterialToken))
				{
					var materialValue = backgroundMaterialToken.Value<string>();
					if (!string.IsNullOrEmpty(materialValue))
					{
						backgroundObj["Material"] = materialValue;
						selectedBackgroundObj["Material"] = materialValue;
					}
					categoryTitleObj.Remove("Background Material");
				}

				EnsureUiElementFields(backgroundObj);
				EnsureUiElementFields(selectedBackgroundObj);

				categoryTitleObj["Background"] = backgroundObj;
				categoryTitleObj["Selected Background"] = selectedBackgroundObj;
			}

			private void EnsureUiElementFields(JObject uiElementObj)
			{
				if (uiElementObj.Property("Enabled?") == null)
					uiElementObj["Enabled?"] = true;
					
				if (uiElementObj.Property("Visible") == null)
					uiElementObj["Visible"] = true;
					
				if (uiElementObj.Property("Name") == null)
					uiElementObj["Name"] = "";
					
				if (uiElementObj.Property("Type (Label/Panel/Button/Image)") == null)
					uiElementObj["Type (Label/Panel/Button/Image)"] = "Panel";
					
				if (uiElementObj.Property("Color") == null)
					uiElementObj["Color"] = new JObject
					{
						["HEX"] = "#FFFFFF",
						["Opacity (0 - 100)"] = 100
					};
					
				if (uiElementObj.Property("Text") == null)
					uiElementObj["Text"] = new JArray();
					
				if (uiElementObj.Property("Font Size") == null)
					uiElementObj["Font Size"] = 14;
					
				if (uiElementObj.Property("Font") == null)
					uiElementObj["Font"] = "RobotoCondensedBold";
					
				if (uiElementObj.Property("Align") == null)
					uiElementObj["Align"] = "UpperLeft";
					
				if (uiElementObj.Property("Text Color") == null)
					uiElementObj["Text Color"] = new JObject
					{
						["HEX"] = "#FFFFFF",
						["Opacity (0 - 100)"] = 100
					};
					
				if (uiElementObj.Property("Command ({user} - user steamid)") == null)
					uiElementObj["Command ({user} - user steamid)"] = "";
					
				if (uiElementObj.Property("Image") == null)
					uiElementObj["Image"] = "";
					
				if (uiElementObj.Property("Cursor Enabled") == null)
					uiElementObj["Cursor Enabled"] = false;
					
				if (uiElementObj.Property("Keyboard Enabled") == null)
					uiElementObj["Keyboard Enabled"] = false;
					
				if (uiElementObj.Property("Sprite") == null)
					uiElementObj["Sprite"] = "";
					
				if (uiElementObj.Property("Material") == null)
					uiElementObj["Material"] = "";

				if (uiElementObj.Property("AnchorMin (X)") == null)
					uiElementObj["AnchorMin (X)"] = 0f;
					
				if (uiElementObj.Property("AnchorMin (Y)") == null)
					uiElementObj["AnchorMin (Y)"] = 0f;
					
				if (uiElementObj.Property("AnchorMax (X)") == null)
					uiElementObj["AnchorMax (X)"] = 1f;
					
				if (uiElementObj.Property("AnchorMax (Y)") == null)
					uiElementObj["AnchorMax (Y)"] = 1f;
					
				if (uiElementObj.Property("OffsetMin (X)") == null)
					uiElementObj["OffsetMin (X)"] = 0f;
					
				if (uiElementObj.Property("OffsetMin (Y)") == null)
					uiElementObj["OffsetMin (Y)"] = 0f;
					
				if (uiElementObj.Property("OffsetMax (X)") == null)
					uiElementObj["OffsetMax (X)"] = 0f;
					
				if (uiElementObj.Property("OffsetMax (Y)") == null)
					uiElementObj["OffsetMax (Y)"] = 0f;
			}
		}

		public class LocalizationFormatMigration : IMigration
		{
			public string Name => "1.2.3";

			public bool IsNeeded()
			{
				try
				{
					var localizationPath = Path.Combine(Interface.Oxide.DataDirectory, "ServerPanel", "Localization.json");
					if (!File.Exists(localizationPath))
						return false;

					var localizationJson = File.ReadAllText(localizationPath);
					var localizationData = JObject.Parse(localizationJson);
					
					var localizationSettings = localizationData["Localization Settings"];
					if (localizationSettings == null)
						return false;

					var elements = localizationSettings["UI Elements"] as JObject;
					if (elements == null)
						return false;

					foreach (var kvp in elements)
					{
						var key = kvp.Key;
						if (!key.Contains('_') || key.Split('_').Length < 3)
						{
							return true;
						}
					}

					return false;
				}
				catch (Exception ex)
				{
					Instance?.PrintError($"Error checking localization migration need: {ex.Message}");
					return false;
				}
			}

			public void Execute()
			{
				try
				{
					var localizationPath = Path.Combine(Interface.Oxide.DataDirectory, "ServerPanel", "Localization.json");
					var categoriesPath = Path.Combine(Interface.Oxide.DataDirectory, "ServerPanel", "Categories.json");

					if (!File.Exists(localizationPath) || !File.Exists(categoriesPath))
					{
                        Instance?.PrintWarning("Required files not found for localization migration");
						return;
					}

					var localizationJson = File.ReadAllText(localizationPath);
					var categoriesJson = File.ReadAllText(categoriesPath);

					var localizationData = JObject.Parse(localizationJson);
					var categoriesData = JObject.Parse(categoriesJson);

					MigrateLocalizationFormat(localizationData, categoriesData);

					File.WriteAllText(localizationPath, localizationData.ToString(Formatting.Indented));
					Instance?.Puts("Localization format migration completed");
				}
				catch (Exception ex)
				{
					Instance?.PrintError($"Localization migration failed: {ex.Message}");
					throw;
				}
			}

			private void MigrateLocalizationFormat(JObject localizationData, JObject categoriesData)
			{
				var localizationSettings = localizationData["Localization Settings"];
				if (localizationSettings == null)
					return;

				var elements = localizationSettings["UI Elements"] as JObject;
				if (elements == null)
					return;

				var categories = categoriesData["Categories"] as JArray;
				if (categories == null)
					return;

				var elementsToMigrate = new List<(string oldKey, string newKey, JToken data)>();

				foreach (var kvp in elements)
				{
					var key = kvp.Key;
					var elementData = kvp.Value;

					if (!key.Contains('_') || key.Split('_').Length < 3)
					{
						foreach (var categoryToken in categories)
						{
							var category = categoryToken as JObject;
							if (category == null) continue;

							var categoryId = category["ID"]?.Value<int>() ?? 0;
							var pages = category["Pages"] as JArray;
							if (pages == null) continue;

							for (int pageIndex = 0; pageIndex < pages.Count; pageIndex++)
							{
								var page = pages[pageIndex] as JObject;
								if (page == null) continue;

								var pageElements = page["UI Elements"] as JArray;
								if (pageElements == null) continue;

								foreach (var elementToken in pageElements)
								{
									var element = elementToken as JObject;
									if (element == null) continue;

									var elementName = element["Name"]?.Value<string>();
									if (elementName == key)
									{
										var newKey = $"{categoryId}_{pageIndex}_{key}";
										elementsToMigrate.Add((key, newKey, elementData));
									}
								}
							}
						}
					}
				}

				foreach (var migration in elementsToMigrate)
				{
					if (elements[migration.newKey] == null)
					{
						elements[migration.newKey] = migration.data;
					}
				}
			}
		}
        
        #endregion

        #region Utils

		private void InitializeMigrations()
		{
			availableMigrations = new Dictionary<string, IMigration>();
			
			RegisterMigration(new CategoryTitleUiRefactoring());
			RegisterMigration(new LocalizationFormatMigration());
			
			Puts($"Loaded {availableMigrations.Count} migrations");
		}

		private void RegisterMigration(IMigration migration)
		{
			availableMigrations[migration.Name] = migration;
		}

		private void CreateBackup(string migrationName)
		{
			try
			{
				var backupDir = Path.Combine(Interface.Oxide.DataDirectory, "ServerPanelMigrations", "backup", $"{DateTime.Now:yyyy-MM-dd_HH-mm-ss}_{migrationName}");
				Directory.CreateDirectory(backupDir);

				var templatePath = Path.Combine(Interface.Oxide.DataDirectory, "ServerPanel", "Template.json");
				if (File.Exists(templatePath))
				{
					var backupPath = Path.Combine(backupDir, "Template.json");
					File.Copy(templatePath, backupPath);
				}

				var categoriesPath = Path.Combine(Interface.Oxide.DataDirectory, "ServerPanel", "Categories.json");
				if (File.Exists(categoriesPath))
				{
					var backupPath = Path.Combine(backupDir, "Categories.json");
					File.Copy(categoriesPath, backupPath);
				}

				var headerFieldsPath = Path.Combine(Interface.Oxide.DataDirectory, "ServerPanel", "HeaderFields.json");
				if (File.Exists(headerFieldsPath))
				{
					var backupPath = Path.Combine(backupDir, "HeaderFields.json");
					File.Copy(headerFieldsPath, backupPath);
				}
			}
			catch (Exception ex)
			{
				PrintError($"Failed to create backup: {ex.Message}");
			}
		}

		private void ExecuteMigration(IMigration migration, bool force = false)
		{
			try
			{			
				if (!force && !migration.IsNeeded())
				{
					Puts($"Migration {migration.Name} not needed");
					return;
				}

				Puts($"Starting migration: {migration.Name}");
				if (force)
					Puts("FORCED execution");

				CreateBackup(migration.Name);
				migration.Execute();

				Puts($"Migration completed: {migration.Name}");

				timer.In(0.5f, () =>
				{
					Interface.Oxide.ReloadPlugin("ServerPanel");
				});
			}
			catch (Exception ex)
			{
				PrintError($"Migration failed {migration.Name}: {ex.Message}");
			}
		}

        #endregion
    }
}
