using HarmonyLib;
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using System.Reflection;
using Network;
using Newtonsoft.Json;
using System.Collections;
using System.ComponentModel;
using System.Linq;
using UnityEngine;
using Oxide.Game.Rust.Cui;
using UnityEngine.UI;
using Facepunch;
using Facepunch.Math;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using System.IO;
using Rust;
using System.Text.RegularExpressions;
using Oxide.Core.Libraries;
using Oxide.Game.Rust.Libraries;
using System.Xml;
using System.Text;
using System.Globalization;
using Newtonsoft.Json.Linq;

namespace Oxide.Plugins
{
    [Info("AdminMenuEnhanced", "Enhanced  // Original by 0xF", "2.0.0")]
    [Description("Modern, fancy in-game admin menu with animations and premium styling.")]
    public class AdminMenuEnhanced : RustPlugin
    {
        #region Constants & References
        [PluginReference]
        private Plugin ImageLibrary, Economics, ServerRewards, Clans;
        
        private const string PERMISSION_USE = "adminmenuenhanced.use";
        private const string PERMISSION_FULLACCESS = "adminmenuenhanced.fullaccess";
        private const string PERMISSION_CONVARS = "adminmenuenhanced.convars";
        private const string PERMISSION_PERMISSIONMANAGER = "adminmenuenhanced.permissionmanager";
        private const string PERMISSION_PLUGINMANAGER = "adminmenuenhanced.pluginmanager";
        private const string PERMISSION_GIVE = "adminmenuenhanced.give";
        private const string PERMISSION_USERINFO_IP = "adminmenuenhanced.userinfo.ip";
        private const string PERMISSION_USERINFO_STEAMINFO = "adminmenuenhanced.userinfo.steaminfo";
        
        private static AdminMenuEnhanced Instance;
        private static Dictionary<string, Panel> panelList;
        private static Dictionary<ulong, SteamInfo> cachedSteamInfo = new Dictionary<ulong, SteamInfo>();
        
        // ═══════════════════════════════════════════════════════════════════════════════
        // ENHANCED MODERN THEME COLORS - Premium Dark Theme with Cyan/Teal Accents
        // ═══════════════════════════════════════════════════════════════════════════════
        
        // Main Background Colors
        private const string COLOR_BG_DARK = "0.08 0.09 0.11 0.98";           // Deep dark background
        private const string COLOR_BG_MEDIUM = "0.12 0.13 0.16 0.95";         // Medium dark panels
        private const string COLOR_BG_LIGHT = "0.16 0.18 0.22 0.90";          // Lighter panels
        private const string COLOR_BG_CARD = "0.14 0.15 0.19 0.92";           // Card backgrounds
        
        // Accent Colors - Cyan/Teal Theme
        private const string COLOR_ACCENT_PRIMARY = "0.0 0.75 0.85 1";        // Bright cyan
        private const string COLOR_ACCENT_SECONDARY = "0.0 0.60 0.70 0.8";    // Darker cyan
        private const string COLOR_ACCENT_GLOW = "0.0 0.85 0.95 0.3";         // Glow effect
        private const string COLOR_ACCENT_HOVER = "0.0 0.65 0.75 1";          // Hover state
        
        // Button Colors
        private const string COLOR_BTN_DEFAULT = "0.18 0.20 0.25 0.85";       // Default button
        private const string COLOR_BTN_HOVER = "0.22 0.24 0.30 0.95";         // Hover state
        private const string COLOR_BTN_ACTIVE = "0.0 0.55 0.65 0.9";          // Active/pressed
        private const string COLOR_BTN_SUCCESS = "0.20 0.65 0.45 0.9";        // Green success
        private const string COLOR_BTN_DANGER = "0.75 0.25 0.25 0.9";         // Red danger
        private const string COLOR_BTN_WARNING = "0.85 0.60 0.20 0.9";        // Orange warning
        
        // Text Colors
        private const string COLOR_TEXT_PRIMARY = "0.95 0.96 0.98 1";         // Main text
        private const string COLOR_TEXT_SECONDARY = "0.70 0.72 0.78 1";       // Secondary text
        private const string COLOR_TEXT_MUTED = "0.50 0.52 0.58 0.8";         // Muted text
        private const string COLOR_TEXT_ACCENT = "0.0 0.85 0.95 1";           // Accent text
        
        // Border & Divider Colors
        private const string COLOR_BORDER = "0.25 0.27 0.32 0.6";             // Subtle borders
        private const string COLOR_BORDER_ACCENT = "0.0 0.75 0.85 0.5";       // Accent borders
        private const string COLOR_DIVIDER = "0.20 0.22 0.28 0.5";            // Dividers
        
        // Status Colors
        private const string COLOR_ONLINE = "0.30 0.80 0.45 1";               // Online status
        private const string COLOR_OFFLINE = "0.55 0.55 0.60 1";              // Offline status
        private const string COLOR_BANNED = "0.80 0.25 0.25 1";               // Banned status
        private const string COLOR_ADMIN = "0.85 0.55 0.20 1";                // Admin highlight
        private const string COLOR_MODERATOR = "0.60 0.40 0.80 1";            // Moderator highlight
        
        // Animation Timings (in seconds)
        private const float FADE_IN_FAST = 0.15f;
        private const float FADE_IN_NORMAL = 0.25f;
        private const float FADE_IN_SLOW = 0.4f;
        private const float FADE_OUT_FAST = 0.1f;
        private const float FADE_OUT_NORMAL = 0.2f;
        
        // UI Materials & Sprites for Premium Look
        private const string MAT_BLUR = "assets/content/ui/uibackgroundblur-ingamemenu.mat";
        private const string MAT_BLUR_MENU = "assets/content/ui/uibackgroundblur-mainmenu.mat";
        private const string MAT_PANEL = "assets/content/ui/menuui/mainmenu.panel.mat";
        private const string MAT_NAME = "assets/content/ui/namefontmaterial.mat";
        private const string MAT_ICON = "assets/icons/iconmaterial.mat";
        private const string MAT_GREYOUT = "assets/icons/greyout.mat";
        
        private const string SPRITE_BG_TILE = "assets/content/ui/ui.background.tile.psd";
        private const string SPRITE_BG_ROUNDED = "assets/content/ui/ui.background.rounded.png";
        private const string SPRITE_BG_RADIAL = "assets/content/ui/ui.background.transparent.radial.psd";
        private const string SPRITE_BG_LINEAR = "assets/content/ui/ui.background.transparent.linear.psd";
        private const string SPRITE_ROUNDED_TGA = "assets/content/ui/ui.rounded.tga";
        private const string SPRITE_CIRCLE = "assets/icons/circle_closed.png";
        
        private static Dictionary<string, string> HEADERS = new Dictionary<string, string>
        {
            { "Content-Type", "application/json" }
        };

#if !CARBON
        private static FieldInfo PERMISSIONS_DICTIONARY_FIELD = typeof(Oxide.Core.Libraries.Permission).GetField("registeredPermissions", BindingFlags.NonPublic | BindingFlags.Instance);
        private static FieldInfo CONSOLECOMMANDS_DICTIONARY_FIELD = typeof(Oxide.Game.Rust.Libraries.Command).GetField("consoleCommands", BindingFlags.NonPublic | BindingFlags.Instance);
        private static FieldInfo CONSOLECOMMAND_CALLBACK_FIELD = CONSOLECOMMANDS_DICTIONARY_FIELD.FieldType.GetGenericArguments()[1].GetField("Callback", BindingFlags.Public | BindingFlags.Instance);
        private static FieldInfo PLUGINCALLBACK_PLUGIN_FIELD = CONSOLECOMMAND_CALLBACK_FIELD.FieldType.GetField("Plugin", BindingFlags.Public | BindingFlags.Instance);
        private static FieldInfo CHATCOMMANDS_DICTIONARY_FIELD = typeof(Oxide.Game.Rust.Libraries.Command).GetField("chatCommands", BindingFlags.NonPublic | BindingFlags.Instance);
        private static FieldInfo CHATCOMMAND_PLUGIN_FIELD = CHATCOMMANDS_DICTIONARY_FIELD.FieldType.GetGenericArguments()[1].GetField("Plugin", BindingFlags.Public | BindingFlags.Instance);
#endif

        private static string ADMINMENU_IMAGECRC;
        private MainMenu mainMenu;
        private Dictionary<string, string> defaultLang = new Dictionary<string, string>();
        private Dictionary<PlayerLoot, Item> viewingBackpacks = new Dictionary<PlayerLoot, Item>();
        static Configuration config;
        
        public AdminMenuEnhanced()
        {
            Instance = this;
        }
        #endregion

        #region Enhanced Theme System
        public static class ModernTheme
        {
            // ═══════════════════════════════════════════════════════════════════════════════
            // PREMIUM THEME PRESETS
            // ═══════════════════════════════════════════════════════════════════════════════
            
            public static class Cyber
            {
                public const string Primary = "0.0 0.85 0.95 1";      // Bright cyan
                public const string Secondary = "0.0 0.65 0.75 0.8";  // Darker cyan
                public const string Accent = "0.95 0.25 0.55 1";      // Pink accent
                public const string Background = "0.06 0.08 0.12 0.98";
                public const string Surface = "0.10 0.12 0.18 0.95";
                public const string Text = "0.92 0.94 0.98 1";
            }
            
            public static class Neon
            {
                public const string Primary = "0.95 0.30 0.50 1";     // Neon pink
                public const string Secondary = "0.55 0.20 0.85 0.9"; // Purple
                public const string Accent = "0.0 0.95 0.85 1";       // Teal
                public const string Background = "0.05 0.05 0.08 0.98";
                public const string Surface = "0.08 0.08 0.12 0.95";
                public const string Text = "0.98 0.98 1.0 1";
            }
            
            public static class Emerald
            {
                public const string Primary = "0.20 0.80 0.55 1";     // Emerald green
                public const string Secondary = "0.15 0.60 0.45 0.8"; // Darker green
                public const string Accent = "0.85 0.75 0.30 1";      // Gold accent
                public const string Background = "0.08 0.10 0.09 0.98";
                public const string Surface = "0.12 0.15 0.13 0.95";
                public const string Text = "0.90 0.95 0.92 1";
            }
            
            public static class Sunset
            {
                public const string Primary = "0.95 0.50 0.25 1";     // Orange
                public const string Secondary = "0.85 0.35 0.35 0.9"; // Red-orange
                public const string Accent = "0.95 0.80 0.30 1";      // Yellow
                public const string Background = "0.10 0.08 0.08 0.98";
                public const string Surface = "0.15 0.12 0.12 0.95";
                public const string Text = "0.98 0.95 0.92 1";
            }
        }
        #endregion

        #region Button Classes
        public class ButtonArray<T> : List<T> where T : Button
        {
            public ButtonArray() : base() { }
            public ButtonArray(IEnumerable<T> collection) : base(collection) { }

            public IEnumerable<T> GetAllowedButtons(Connection connection) => GetAllowedButtons(connection.userid.ToString());
            public IEnumerable<T> GetAllowedButtons(string userId) => this.Where(b => b == null || b.UserHasPermission(userId));
        }

        public class ButtonGrid<T> : List<ButtonGrid<T>.Item> where T : Button
        {
            public class Item
            {
                public int row;
                public int column;
                public T button;
                public Item(int row, int column, T button)
                {
                    this.row = row;
                    this.column = column;
                    this.button = button;
                }
            }

            public IEnumerable<Item> GetAllowedButtons(Connection connection) => GetAllowedButtons(connection.userid.ToString());
            public IEnumerable<Item> GetAllowedButtons(string userId) => this.Where(b => b.button == null || b.button.UserHasPermission(userId));
        }

        public class ButtonArray : ButtonArray<Button> { }

        public class Button
        {
            public enum State { None, Normal, Pressed, Toggled }

            private string permission = null;
            public string Command { get; set; }
            public string[] Args { get; set; }
            public Label Label { get; set; }
            public virtual int FontSize { get; set; } = 14;
            public ButtonStyle Style { get; set; } = ButtonStyle.Default;

            public string Permission
            {
                get => permission;
                set
                {
                    if (string.IsNullOrEmpty(value)) return;
                    permission = string.Format("adminmenuenhanced.{0}", value);
                    if (!Instance.permission.PermissionExists(permission))
                        Instance.permission.RegisterPermission(permission, Instance);
                }
            }

            public string FullCommand => $"{Command} {string.Join(" ", Args)}";

            public Button() { }

            public Button(string label, string command, params string[] args)
            {
                Label = new Label(label);
                Command = command;
                Args = args;
                if (!all.ContainsKey(FullCommand))
                    all.Add(FullCommand, this);
            }

            public virtual State GetState(ConnectionData connectionData)
            {
                if (connectionData.userData.TryGetValue($"button_{this.GetHashCode()}.state", out object state))
                    return (State)state;
                return State.None;
            }

            public virtual void SetState(ConnectionData connectionData, State state)
            {
                connectionData.userData[$"button_{this.GetHashCode()}.state"] = state;
            }

            public bool UserHasPermission(Connection connection) => UserHasPermission(connection.userid.ToString());
            public bool UserHasPermission(string userId) => Permission == null || Instance.UserHasPermission(userId, Permission);

            public virtual bool IsPressed(ConnectionData connectionData) => GetState(connectionData) == State.Pressed;
            public virtual bool IsHidden(ConnectionData connectionData) => false;

            internal static Dictionary<string, Button> all = new Dictionary<string, Button>();
        }

        // ═══════════════════════════════════════════════════════════════════════════════
        // ENHANCED BUTTON STYLE - Modern Premium Look
        // ═══════════════════════════════════════════════════════════════════════════════
        public class ButtonStyle : ICloneable
        {
            public string BackgroundColor { get; set; }
            public string ActiveBackgroundColor { get; set; }
            public string HoverBackgroundColor { get; set; }
            public string TextColor { get; set; }
            public string BorderColor { get; set; }
            public float BorderWidth { get; set; }
            public bool HasGlow { get; set; }
            public string GlowColor { get; set; }
            public float CornerRadius { get; set; }
            public float FadeIn { get; set; }

            public object Clone() => this.MemberwiseClone();

            public static ButtonStyle Default => new ButtonStyle
            {
                BackgroundColor = COLOR_BTN_DEFAULT,
                ActiveBackgroundColor = COLOR_BTN_ACTIVE,
                HoverBackgroundColor = COLOR_BTN_HOVER,
                TextColor = COLOR_TEXT_PRIMARY,
                BorderColor = COLOR_BORDER,
                BorderWidth = 1f,
                HasGlow = false,
                GlowColor = COLOR_ACCENT_GLOW,
                CornerRadius = 4f,
                FadeIn = FADE_IN_FAST
            };

            public static ButtonStyle Primary => new ButtonStyle
            {
                BackgroundColor = COLOR_ACCENT_PRIMARY,
                ActiveBackgroundColor = COLOR_ACCENT_HOVER,
                HoverBackgroundColor = "0.0 0.70 0.80 1",
                TextColor = "0.05 0.05 0.08 1",
                BorderColor = COLOR_ACCENT_PRIMARY,
                BorderWidth = 0f,
                HasGlow = true,
                GlowColor = COLOR_ACCENT_GLOW,
                CornerRadius = 6f,
                FadeIn = FADE_IN_FAST
            };

            public static ButtonStyle Success => new ButtonStyle
            {
                BackgroundColor = COLOR_BTN_SUCCESS,
                ActiveBackgroundColor = "0.15 0.55 0.35 1",
                HoverBackgroundColor = "0.25 0.70 0.50 1",
                TextColor = COLOR_TEXT_PRIMARY,
                BorderColor = "0.25 0.70 0.50 0.5",
                BorderWidth = 0f,
                HasGlow = false,
                CornerRadius = 6f,
                FadeIn = FADE_IN_FAST
            };

            public static ButtonStyle Danger => new ButtonStyle
            {
                BackgroundColor = COLOR_BTN_DANGER,
                ActiveBackgroundColor = "0.65 0.20 0.20 1",
                HoverBackgroundColor = "0.85 0.30 0.30 1",
                TextColor = COLOR_TEXT_PRIMARY,
                BorderColor = "0.85 0.30 0.30 0.5",
                BorderWidth = 0f,
                HasGlow = false,
                CornerRadius = 6f,
                FadeIn = FADE_IN_FAST
            };

            public static ButtonStyle Warning => new ButtonStyle
            {
                BackgroundColor = COLOR_BTN_WARNING,
                ActiveBackgroundColor = "0.75 0.50 0.15 1",
                HoverBackgroundColor = "0.95 0.70 0.25 1",
                TextColor = "0.10 0.08 0.05 1",
                BorderColor = "0.95 0.70 0.25 0.5",
                BorderWidth = 0f,
                HasGlow = false,
                CornerRadius = 6f,
                FadeIn = FADE_IN_FAST
            };

            public static ButtonStyle Ghost => new ButtonStyle
            {
                BackgroundColor = "0 0 0 0",
                ActiveBackgroundColor = COLOR_BTN_ACTIVE,
                HoverBackgroundColor = "0.20 0.22 0.28 0.5",
                TextColor = COLOR_TEXT_SECONDARY,
                BorderColor = COLOR_BORDER,
                BorderWidth = 1f,
                HasGlow = false,
                CornerRadius = 4f,
                FadeIn = FADE_IN_FAST
            };

            public static ButtonStyle Sidebar => new ButtonStyle
            {
                BackgroundColor = "0 0 0 0",
                ActiveBackgroundColor = "0.0 0.65 0.75 0.15",
                HoverBackgroundColor = "0.15 0.17 0.22 0.6",
                TextColor = COLOR_TEXT_SECONDARY,
                BorderColor = "0 0 0 0",
                BorderWidth = 0f,
                HasGlow = false,
                CornerRadius = 0f,
                FadeIn = FADE_IN_FAST
            };
        }

        public class CategoryButton : Button
        {
            public override int FontSize { get; set; } = 18;
            public CategoryButton(string label, string command, params string[] args) : base(label, command, args)
            {
                Style = ButtonStyle.Sidebar;
            }
        }

        public class HideButton : Button
        {
            public HideButton(string label, string command, params string[] args) : base(label, command, args) { }
            public override bool IsHidden(ConnectionData connectionData) => connectionData.userData["backcommand"] == null;
        }

        public class ToggleButton : Button
        {
            public ToggleButton(string label, string command, params string[] args) : base(label, command, args) { }
            public virtual void Toggle(ConnectionData connectionData)
            {
                State currentState = GetState(connectionData);
                SetState(connectionData, (currentState == State.Normal || currentState == State.None ? State.Toggled : State.Normal));
            }
        }

        public class ConditionToggleButton : ToggleButton
        {
            public Func<ConnectionData, bool> Condition { get; set; }
            public ConditionToggleButton(string label, string command, params string[] args) : base(label, command, args) { }

            public override State GetState(ConnectionData connectionData)
            {
                if (Condition == null) return State.Normal;
                return Condition(connectionData) ? State.Toggled : State.Normal;
            }

            public override void Toggle(ConnectionData connectionData) { }
        }
        #endregion

        #region Configuration
        [JsonObject(MemberSerialization.OptIn)]
        public class BaseCustomButton
        {
            [JsonProperty] public string Label { get; set; } = string.Empty;
            [JsonProperty("Execution as server")] public bool ExecutionAsServer { get; set; }
            [JsonProperty("Commands")] public string[] Commands { get; set; } = new string[0];
            [JsonProperty("Commands for Toggled state")] public string[] ToggledStateCommands { get; set; } = new string[0];
            [JsonProperty] public string Permission { get; set; } = string.Empty;
            [JsonProperty] public ButtonStyle Style { get; set; } = ButtonStyle.Default;
            [JsonProperty] public int[] Position { get; set; } = new int[2] { 0, 0 };
            protected virtual string BaseCommand => "custombutton";

            private Button _button;
            public Button Button
            {
                get
                {
                    if (_button == null) _button = GetButton();
                    return _button;
                }
            }

            private Button GetButton()
            {
                if (ToggledStateCommands != null && ToggledStateCommands.Length > 0)
                    return new ToggleButton(Label, BaseCommand, "cb.exec", this.GetHashCode().ToString())
                    {
                        Permission = Permission.ToLower(),
                        Style = Style
                    };
                return new Button(Label, BaseCommand, "cb.exec", this.GetHashCode().ToString())
                {
                    Permission = Permission.ToLower(),
                    Style = Style
                };
            }
        }

        public class QMCustomButton : BaseCustomButton
        {
            public enum Recievers { None, Online, Offline, Everyone }
            protected override string BaseCommand => "quickmenu.action";
            [JsonProperty("Bulk sending of command to each player. Available values: None, Online, Offline, Everyone")]
            [JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
            public Recievers PlayerReceivers { get; set; }
        }

        public class UserInfoCustomButton : BaseCustomButton
        {
            protected override string BaseCommand => "userinfo.action";
        }

        public class Configuration
        {
            [JsonProperty(PropertyName = "Text under the ADMIN MENU")]
            public string Subtext { get; set; } = "ENHANCED EDITION";

            [JsonProperty(PropertyName = "Button to hook (X | F | OFF)")]
            [JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
            public ButtonHook ButtonToHook { get; set; } = ButtonHook.X;

            [JsonProperty(PropertyName = "Disable player avatars")]
            public bool DisablePlayerAvatars { get; set; } = false;

            [JsonProperty(PropertyName = "Chat command to show admin menu")]
            public string ChatCommand { get; set; } = "admin";

            [JsonProperty(PropertyName = "Theme (Cyber, Neon, Emerald, Sunset)")]
            public string ThemeName { get; set; } = "Cyber";

            [JsonProperty(PropertyName = "Enable Animations")]
            public bool EnableAnimations { get; set; } = true;

            [JsonProperty(PropertyName = "Animation Speed (0.1 - 1.0)")]
            public float AnimationSpeed { get; set; } = 0.25f;

            [JsonProperty(PropertyName = "Custom Quick Buttons")]
            public List<QMCustomButton> CustomQuickButtons { get; set; } = new List<QMCustomButton>();

            [JsonProperty(PropertyName = "User Custom Buttons")]
            public List<UserInfoCustomButton> UserInfoCustomButtons { get; set; } = new List<UserInfoCustomButton>();

            [JsonProperty(PropertyName = "Give menu item presets")]
            public List<ItemPreset> GiveItemPresets { get; set; } = new List<ItemPreset>();

            [JsonProperty(PropertyName = "Favorite Plugins")]
            public HashSet<string> FavoritePlugins { get; set; } = new HashSet<string>();

            [JsonProperty(PropertyName = "Logs Properties")]
            public LogsProperties Logs { get; set; } = new LogsProperties();

            [JsonIgnore]
            public Dictionary<int, string[]> HashedCommands { get; set; } = new Dictionary<int, string[]>();

            public static Configuration DefaultConfig()
            {
                return new Configuration()
                {
                    CustomQuickButtons = new List<QMCustomButton>
                    {
                        new QMCustomButton
                        {
                            Label = "Custom Button",
                            Commands = new[] { "chat.say \"/custom\"", "adminmenuenhanced openinfopanel custom_buttons" },
                            Permission = "fullaccess",
                            Style = ButtonStyle.Primary,
                            Position = new int[2] { 0, 4 }
                        }
                    },
                    UserInfoCustomButtons = new List<UserInfoCustomButton>
                    {
                        new UserInfoCustomButton
                        {
                            Label = "Custom Button",
                            Commands = new[] { "chat.say \"/custom {steamID}\"", "adminmenuenhanced openinfopanel custom_buttons" },
                            Permission = "fullaccess",
                            Position = new int[2] { 9, 0 }
                        }
                    },
                    GiveItemPresets = new List<ItemPreset>() { ItemPreset.Example }
                };
            }

            public class ItemPreset
            {
                [JsonProperty(PropertyName = "Short Name")] public string ShortName { get; set; }
                [JsonProperty(PropertyName = "Skin Id")] public ulong SkinId { get; set; }
                [JsonProperty(PropertyName = "Name")] public string Name { get; set; }
                [JsonProperty(PropertyName = "Category Name")] public string Category { get; set; }

                [JsonIgnore]
                public static ItemPreset Example => new ItemPreset
                {
                    ShortName = "chocolate",
                    SkinId = 3161523786,
                    Name = "Delicious cookies with chocolate",
                    Category = "Food",
                };
            }

            public class LogsProperties
            {
                [JsonProperty(PropertyName = "Discord Webhook URL")] public string WebhookURL { get; set; } = string.Empty;
                [JsonProperty(PropertyName = "Info")] public string Info { get; } = "You can override the url for each log type.";
                [JsonProperty(PropertyName = "Give")] public object Give { get; set; } = true;
                [JsonProperty(PropertyName = "Admin teleports")] public object AdminTeleport { get; set; } = true;
                [JsonProperty(PropertyName = "Spectate")] public object Spectate { get; set; } = true;
                [JsonProperty(PropertyName = "Heal")] public object Heal { get; set; } = true;
                [JsonProperty(PropertyName = "Kill")] public object Kill { get; set; } = true;
                [JsonProperty(PropertyName = "Look inventory")] public object LookInventory { get; set; } = true;
                [JsonProperty(PropertyName = "Strip inventory")] public object StripInventory { get; set; } = true;
                [JsonProperty(PropertyName = "Blueprints")] public object Blueprints { get; set; } = true;
                [JsonProperty(PropertyName = "Mute/Unmute")] public object MuteUnmute { get; set; } = true;
                [JsonProperty(PropertyName = "Toggle Creative")] public object ToggleCreative { get; set; } = true;
                [JsonProperty(PropertyName = "Cuff")] public object Cuff { get; set; } = true;
                [JsonProperty(PropertyName = "Kick the player")] public object Kick { get; set; } = true;
                [JsonProperty(PropertyName = "Ban the player")] public object Ban { get; set; } = true;
                [JsonProperty(PropertyName = "Using custom buttons")] public object CustomButtons { get; set; } = true;
                [JsonProperty(PropertyName = "Spawn entities")] public object SpawnEntities { get; set; } = true;
                [JsonProperty(PropertyName = "Set time")] public object SetTime { get; set; } = true;
                [JsonProperty(PropertyName = "ConVars")] public object ConVars { get; set; } = true;
                [JsonProperty(PropertyName = "Plugin Manager")] public object PluginManager { get; set; } = true;
                [JsonProperty(PropertyName = "Permission Manager")] public object PermissionManager { get; set; } = true;
            }
        }
        #endregion

        #region Connection Data & UI
        public class ConnectionData
        {
            public Connection connection;
            public MainMenu currentMainMenu;
            public Panel currentPanel;
            public Sidebar currentSidebar;
            public Content currentContent;
            public Translator15 translator;
            public Dictionary<string, object> userData;

            public ConnectionData(BasePlayer player) : this(player.Connection) { }

            public ConnectionData(Connection connection)
            {
                this.connection = connection;
                this.translator = new Translator15(connection.userid);
                this.userData = new Dictionary<string, object>()
                {
                    { "userId", connection.userid },
                    { "userinfo.lastuserid", connection.userid },
                    { "backcommand", null },
                };
                this.UI = new ConnectionUI(this);
                try { Init(); }
                catch (Exception ex) { UnityEngine.Debug.LogException(ex); }
            }

            public ConnectionUI UI { get; private set; }
            public bool IsAdminMenuDisplay { get; set; }
            public bool IsDestroyed { get; set; }

            public void Init()
            {
                UI.RenderMainMenu(Instance.mainMenu);
                this.currentMainMenu = Instance.mainMenu;
            }

            public void ShowAdminMenu()
            {
                UI.ShowAdminMenu();
                IsAdminMenuDisplay = true;
            }

            public void HideAdminMenu()
            {
                UI.HideAdminMenu();
                IsAdminMenuDisplay = false;
            }

            public ConnectionData OpenPanel(string panelName)
            {
                if (panelList.TryGetValue(panelName, out Panel panel))
                {
                    if (currentPanel == panel) return null;
                    if (currentContent != null) currentContent.RestoreUserData(userData);
                    currentContent = null;
                    if (currentPanel != null) currentPanel.OnClose(this);
                    currentPanel = panel;
                    currentSidebar = currentPanel.Sidebar;
                    UI.RenderPanel(currentPanel);
                    currentPanel.OnOpen(this);
                    Content defaultPanelContent = panel.DefaultContent;
                    if (defaultPanelContent != null) ShowPanelContent(defaultPanelContent);
                    if (panel.Sidebar != null && panel.Sidebar.AutoActivateCategoryButtonIndex.HasValue)
                        Instance.HandleCommand(connection, "uipanel.sidebar.button_pressed", 
                            panel.Sidebar.AutoActivateCategoryButtonIndex.Value.ToString(), 
                            panel.Sidebar.CategoryButtons.GetAllowedButtons(connection).Count().ToString());
                    return this;
                }
                else
                {
                    Instance.PrintError($"Panel with name \"{panelName}\" not found!");
                    return null;
                }
            }

            public void SetSidebar(Sidebar sidebar)
            {
                bool needsChangeContentSize = (currentSidebar != sidebar);
                currentSidebar = sidebar;
                CUI.Root root = new CUI.Root("AdminMenuEnhanced_Panel");
                if (sidebar != null) UI.AddSidebar(root, sidebar);
                else root.Add(new CUI.Element { DestroyUi = "AdminMenuEnhanced_Panel_Sidebar" });
                root.Render(connection);
                if (needsChangeContentSize)
                {
                    CUI.Root updateRoot = new CUI.Root();
                    updateRoot.Add(new CUI.Element { 
                        Components = { new CuiRectTransformComponent { OffsetMin = $"{(sidebar != null ? 260 : 0)} 0", } }, 
                        Name = "AdminMenuEnhanced_Panel_Content" 
                    });
                    updateRoot.Update(connection);
                }
            }

            public void ShowPanelContent(Content content)
            {
                if (content == null)
                {
                    CUI.Root root = new CUI.Root();
                    root.Add(new CUI.Element { DestroyUi = "AdminMenuEnhanced_Panel_TempContent" });
                    root.Render(connection);
                    return;
                }
                if (currentContent != null) currentContent.RestoreUserData(userData);
                currentContent = content;
                currentContent.LoadDefaultUserData(userData);
                UI.RenderContent(content);
            }

            public void ShowPanelContent(string contentId) => ShowPanelContent(currentPanel.TryGetContent(contentId));

            public void Dispose() => all.Remove(connection);

            public static Dictionary<Connection, ConnectionData> all = new Dictionary<Connection, ConnectionData>();
            public static ConnectionData Get(Connection connection)
            {
                if (connection == null) return null;
                ConnectionData data;
                if (all.TryGetValue(connection, out data)) return data;
                return null;
            }

            public static ConnectionData Get(BasePlayer player) => Get(player.Connection);
            public static ConnectionData GetOrCreate(Connection connection)
            {
                if (connection == null) return null;
                ConnectionData data = Get(connection);
                if (data == null) data = all[connection] = new ConnectionData(connection);
                return data;
            }

            public static ConnectionData GetOrCreate(BasePlayer player) => GetOrCreate(player.Connection);
        }
        #endregion

        #region Enhanced Connection UI - Modern Premium Rendering
        public class ConnectionUI
        {
            Connection connection;
            ConnectionData connectionData;
            
            public ConnectionUI(ConnectionData connectionData)
            {
                this.connectionData = connectionData;
                this.connection = connectionData.connection;
            }

            public void ShowAdminMenu()
            {
                CUI.Root root = new CUI.Root();
                root.Add(new CUI.Element { 
                    Components = { new CuiRectTransformComponent { AnchorMin = "0 0.00001", AnchorMax = "1 1.00001" } }, 
                    Name = "AdminMenuEnhanced", 
                    Update = true 
                });
                root.Add(new CUI.Element { 
                    Components = { new CuiNeedsCursorComponent() }, 
                    Parent = "AdminMenuEnhanced", 
                    Name = "AdminMenuEnhanced_Cursor" 
                });
                root.Render(connection);
            }

            public void HideAdminMenu()
            {
                CUI.Root root = new CUI.Root();
                root.Add(new CUI.Element { 
                    Components = { new CuiRectTransformComponent { AnchorMin = "1000 1000", AnchorMax = "1001 1001" } }, 
                    DestroyUi = "AdminMenuEnhanced_Cursor", 
                    Name = "AdminMenuEnhanced", 
                    Update = true 
                });
                root.Render(connection);
            }

            public void DestroyAdminMenu()
            {
                CuiHelper.DestroyUi(connection.player as BasePlayer, "AdminMenuEnhanced");
                CuiHelper.DestroyUi(connection.player as BasePlayer, "AdminMenuEnhanced_Cursor");
                connectionData.IsDestroyed = true;
            }

            public void DestroyAll()
            {
                DestroyAdminMenu();
                CuiHelper.DestroyUi(connection.player as BasePlayer, "AdminMenuEnhanced_OpenButton");
            }

            // ═══════════════════════════════════════════════════════════════════════════════
            // ENHANCED SIDEBAR - Modern Glass Effect with Accent Line
            // ═══════════════════════════════════════════════════════════════════════════════
            public void AddSidebar(CUI.Element element, Sidebar sidebar)
            {
                if (sidebar == null) return;
                
                float fadeTime = config.EnableAnimations ? FADE_IN_NORMAL : 0f;
                
                // Main sidebar container with blur effect
                var sidebarPanel = element.AddPanel(
                    color: COLOR_BG_MEDIUM, 
                    material: MAT_BLUR, 
                    imageType: Image.Type.Tiled, 
                    anchorMin: "0 0", 
                    anchorMax: "0 1", 
                    offsetMin: "0 0", 
                    offsetMax: "260 0", 
                    name: "AdminMenuEnhanced_Panel_Sidebar"
                ).AddDestroySelfAttribute();
                
                // Accent line on the right edge
                sidebarPanel.AddPanel(
                    color: COLOR_ACCENT_PRIMARY, 
                    anchorMin: "1 0", 
                    anchorMax: "1 1", 
                    offsetMin: "-2 0", 
                    offsetMax: "0 0"
                ).WithFade(fadeTime);
                
                // Subtle gradient overlay
                sidebarPanel.AddPanel(
                    color: "0.05 0.06 0.08 0.4", 
                    sprite: SPRITE_BG_LINEAR, 
                    material: MAT_NAME, 
                    anchorMin: "0 0", 
                    anchorMax: "1 1"
                );

                IEnumerable<CategoryButton> categoryButtons = sidebar.CategoryButtons.GetAllowedButtons(connection);
                if (categoryButtons != null)
                {
                    int categoryButtonsCount = categoryButtons.Count();
                    if (categoryButtonsCount == 0) return;
                    
                    var sidebarButtonGroup = sidebarPanel.AddPanel(
                        color: "0 0 0 0", 
                        name: "UIPanel_SideBar_Scrollview"
                    );
                    sidebarButtonGroup.Components.AddScrollView(
                        vertical: true, 
                        anchorMin: "0 1", 
                        offsetMin: $"0 -{categoryButtonsCount * 52}"
                    );
                    
                    for (int i = 0; i < categoryButtonsCount; i++)
                    {
                        CategoryButton categoryButton = categoryButtons.ElementAt(i);
                        float delay = config.EnableAnimations ? i * 0.05f : 0f;
                        
                        // Button container
                        var btnContainer = sidebarButtonGroup.AddButton(
                            command: $"adminmenuenhanced uipanel.sidebar.button_pressed {i} {categoryButtonsCount}", 
                            color: "0 0 0 0", 
                            anchorMin: "0 1", 
                            anchorMax: "1 1", 
                            offsetMin: $"12 -{(i + 1) * 52}", 
                            offsetMax: $"-12 -{i * 52 + 4}", 
                            name: $"UIPanel_SideBar_Button{i}"
                        );
                        
                        // Hover background (rounded)
                        btnContainer.AddPanel(
                            color: "0.15 0.17 0.22 0", 
                            sprite: SPRITE_BG_ROUNDED, 
                            imageType: Image.Type.Sliced, 
                            anchorMin: "0 0", 
                            anchorMax: "1 1"
                        );
                        
                        // Active indicator line
                        btnContainer.AddPanel(
                            color: "0 0 0 0", 
                            anchorMin: "0 0.15", 
                            anchorMax: "0 0.85", 
                            offsetMin: "0 0", 
                            offsetMax: "3 0",
                            name: $"UIPanel_SideBar_Button{i}_Indicator"
                        );
                        
                        // Button text
                        btnContainer.AddText(
                            text: categoryButton.Label.Localize(connection), 
                            color: COLOR_TEXT_SECONDARY, 
                            font: CUI.Font.RobotoCondensedBold, 
                            fontSize: categoryButton.FontSize, 
                            align: TextAnchor.MiddleLeft, 
                            offsetMin: "16 0", 
                            offsetMax: "-16 0"
                        ).WithFade(fadeTime + delay);
                    }
                }
            }

            // ═══════════════════════════════════════════════════════════════════════════════
            // ENHANCED NAVIGATION BUTTONS - Modern Animated Style
            // ═══════════════════════════════════════════════════════════════════════════════
            private void AddNavButtons(CUI.Element element, MainMenu mainMenu)
            {
                IEnumerable<Button> navButtons = mainMenu.NavButtons.GetAllowedButtons(connection);
                int navButtonsCount = navButtons.Count();
                float fadeTime = config.EnableAnimations ? FADE_IN_NORMAL : 0f;
                
                var navButtonGroup = element.AddContainer(
                    anchorMin: "0 0", 
                    anchorMax: "1 0", 
                    offsetMin: "64 64", 
                    offsetMax: $"0 {64 + navButtonsCount * 48}", 
                    name: "Navigation ButtonGroup"
                ).AddDestroySelfAttribute();
                
                for (int i = 0; i < navButtonsCount; i++)
                {
                    Button navButton = navButtons.ElementAtOrDefault(i);
                    if (navButton == null) continue;
                    
                    bool isHidden = navButton.IsHidden(connectionData);
                    bool isPressed = navButton.IsPressed(connectionData);
                    float delay = config.EnableAnimations ? i * 0.08f : 0f;
                    
                    // Button container
                    var btnContainer = navButtonGroup.AddButton(
                        command: isHidden ? null : $"adminmenuenhanced navigation.button_pressed {i} {navButtonsCount}", 
                        color: "0 0 0 0", 
                        anchorMin: "0 1", 
                        anchorMax: "1 1", 
                        offsetMin: $"0 -{(i + 1) * 48}", 
                        offsetMax: $"0 -{i * 48}"
                    );
                    
                    // Active indicator line (left side)
                    if (isPressed)
                    {
                        btnContainer.AddPanel(
                            color: COLOR_ACCENT_PRIMARY, 
                            anchorMin: "0 0.2", 
                            anchorMax: "0 0.8", 
                            offsetMin: "0 0", 
                            offsetMax: "3 0"
                        ).WithFade(fadeTime);
                    }
                    
                    // Button text with dynamic color
                    string textColor = isHidden ? "0 0 0 0" : (isPressed ? COLOR_TEXT_PRIMARY : COLOR_TEXT_MUTED);
                    btnContainer.AddText(
                        text: navButton.Label.Localize(connection).ToUpper(), 
                        color: textColor, 
                        fontSize: 24, 
                        font: CUI.Font.RobotoCondensedBold, 
                        align: TextAnchor.LowerLeft, 
                        overflow: VerticalWrapMode.Truncate, 
                        offsetMin: "12 8", 
                        name: $"NavigationButtonText{i}"
                    ).WithFade(fadeTime + delay);
                    
                    // Underline for active state
                    if (isPressed)
                    {
                        btnContainer.AddPanel(
                            color: COLOR_ACCENT_PRIMARY, 
                            anchorMin: "0 0", 
                            anchorMax: "0.3 0", 
                            offsetMin: "12 4", 
                            offsetMax: "0 6"
                        ).WithFade(fadeTime);
                    }
                }
            }

            public void UpdateNavButtons(MainMenu mainMenu)
            {
                CUI.Root root = new CUI.Root("AdminMenuEnhanced_Navigation");
                AddNavButtons(root, mainMenu);
                root.Render(connection);
            }

            // ═══════════════════════════════════════════════════════════════════════════════
            // ENHANCED OPEN BUTTON - Sleek Floating Button
            // ═══════════════════════════════════════════════════════════════════════════════
            public void RenderOverlayOpenButton()
            {
                CUI.Root root = new CUI.Root("Overlay");
                
                var btn = root.AddButton(
                    command: "adminmenuenhanced", 
                    color: COLOR_BG_CARD, 
                    sprite: SPRITE_BG_ROUNDED,
                    imageType: Image.Type.Sliced,
                    anchorMin: "0 0", 
                    anchorMax: "0 0", 
                    offsetMin: "8 8", 
                    offsetMax: "120 38", 
                    name: "AdminMenuEnhanced_OpenButton"
                ).AddDestroySelfAttribute();
                
                // Accent line on top
                btn.AddPanel(
                    color: COLOR_ACCENT_PRIMARY, 
                    anchorMin: "0 1", 
                    anchorMax: "1 1", 
                    offsetMin: "4 -2", 
                    offsetMax: "-4 0"
                );
                
                // Icon dot
                btn.AddPanel(
                    color: COLOR_ACCENT_PRIMARY, 
                    sprite: SPRITE_CIRCLE,
                    anchorMin: "0 0.5", 
                    anchorMax: "0 0.5", 
                    offsetMin: "10 -4", 
                    offsetMax: "18 4"
                );
                
                btn.AddText(
                    text: "ADMIN", 
                    color: COLOR_TEXT_PRIMARY, 
                    font: CUI.Font.RobotoCondensedBold, 
                    fontSize: 13, 
                    align: TextAnchor.MiddleCenter,
                    offsetMin: "20 0",
                    offsetMax: "0 0"
                );
                
                root.Render(connection);
            }

            // ═══════════════════════════════════════════════════════════════════════════════
            // ENHANCED MAIN MENU - Premium Dark Theme with Animations
            // ═══════════════════════════════════════════════════════════════════════════════
            public void RenderMainMenu(MainMenu mainMenu)
            {
                if (mainMenu == null) return;
                
                float fadeTime = config.EnableAnimations ? FADE_IN_SLOW : 0f;
                CUI.Root root = new CUI.Root("Overall");
                
                // Main container with deep dark background and blur
                var container = root.AddPanel(
                    color: COLOR_BG_DARK, 
                    material: MAT_BLUR_MENU, 
                    imageType: Image.Type.Tiled, 
                    anchorMin: "1000 1000", 
                    anchorMax: "1001 1001", 
                    name: "AdminMenuEnhanced"
                ).AddDestroySelfAttribute();
                
                // Radial gradient overlay for depth
                container.AddPanel(
                    color: "0.08 0.12 0.15 0.6", 
                    sprite: SPRITE_BG_RADIAL, 
                    material: MAT_NAME, 
                    anchorMin: "0 0", 
                    anchorMax: "1 1"
                );
                
                // Subtle vignette effect
                container.AddPanel(
                    color: "0.02 0.03 0.05 0.5", 
                    sprite: SPRITE_BG_RADIAL, 
                    material: MAT_NAME, 
                    anchorMin: "0 0", 
                    anchorMax: "1 1"
                );
                
                // Navigation panel (left side)
                var navigation = container.AddContainer(
                    anchorMin: "0 0", 
                    anchorMax: "0 1", 
                    offsetMin: "0 0", 
                    offsetMax: "360 0", 
                    name: "AdminMenuEnhanced_Navigation"
                );
                
                // Logo/Title area
                var homeButton = navigation.AddContainer(
                    anchorMin: "0 1", 
                    anchorMax: "1 1", 
                    offsetMin: "64 -140", 
                    offsetMax: "0 -32"
                );
                
                // Title text with glow effect
                homeButton.AddText(
                    text: "ADMIN", 
                    color: COLOR_TEXT_PRIMARY, 
                    font: CUI.Font.RobotoCondensedBold, 
                    fontSize: 42, 
                    align: TextAnchor.LowerLeft,
                    anchorMin: "0 0.5",
                    anchorMax: "1 1",
                    offsetMin: "0 0",
                    offsetMax: "0 0"
                ).WithFade(fadeTime);
                
                homeButton.AddText(
                    text: "MENU", 
                    color: COLOR_ACCENT_PRIMARY, 
                    font: CUI.Font.RobotoCondensedBold, 
                    fontSize: 42, 
                    align: TextAnchor.UpperLeft,
                    anchorMin: "0 0",
                    anchorMax: "1 0.5",
                    offsetMin: "0 0",
                    offsetMax: "0 0"
                ).WithFade(fadeTime);
                
                // Subtext
                homeButton.AddText(
                    text: config.Subtext, 
                    color: COLOR_TEXT_MUTED, 
                    font: CUI.Font.RobotoCondensedRegular, 
                    fontSize: 12, 
                    anchorMin: "0 0", 
                    anchorMax: "0 0", 
                    offsetMin: "0 -25", 
                    offsetMax: "200 -8"
                ).WithFade(fadeTime * 1.5f);
                
                // Accent line under title
                homeButton.AddPanel(
                    color: COLOR_ACCENT_PRIMARY, 
                    anchorMin: "0 0", 
                    anchorMax: "0 0", 
                    offsetMin: "0 -4", 
                    offsetMax: "60 -2"
                ).WithFade(fadeTime);
                
                // Version text (bottom right)
                container.AddText(
                    text: $"v{Instance.Version}", 
                    color: COLOR_TEXT_MUTED, 
                    font: CUI.Font.RobotoCondensedRegular, 
                    fontSize: 11, 
                    align: TextAnchor.MiddleRight, 
                    anchorMin: "1 0", 
                    anchorMax: "1 0", 
                    offsetMin: "-100 8", 
                    offsetMax: "-16 28"
                );
                
                // ═══════════════════════════════════════════════════════════════════════════════
                // CLOSE BUTTON - Top Right Corner
                // ═══════════════════════════════════════════════════════════════════════════════
                var closeBtn = container.AddButton(
                    command: "adminmenuenhanced close",
                    color: COLOR_BTN_DEFAULT,
                    sprite: SPRITE_BG_ROUNDED,
                    imageType: Image.Type.Sliced,
                    anchorMin: "1 1",
                    anchorMax: "1 1",
                    offsetMin: "-56 -56",
                    offsetMax: "-16 -16"
                ).WithFade(fadeTime);
                
                // X icon text
                closeBtn.AddText(
                    text: "✕",
                    color: COLOR_TEXT_PRIMARY,
                    font: CUI.Font.RobotoCondensedBold,
                    fontSize: 20,
                    align: TextAnchor.MiddleCenter
                );
                
                // Hover hint - red tint on the button border
                closeBtn.AddPanel(
                    color: COLOR_BTN_DANGER,
                    anchorMin: "0 0",
                    anchorMax: "1 0",
                    offsetMin: "4 0",
                    offsetMax: "-4 2"
                );
                
                // Navigation buttons
                AddNavButtons(navigation, mainMenu);
                
                // Main body area
                var body = container.AddContainer(
                    anchorMin: "0 0", 
                    anchorMax: "1 1", 
                    offsetMin: "360 0", 
                    offsetMax: "-64 0", 
                    name: "AdminMenuEnhanced_Body"
                );
                
                // Right margin
                container.AddContainer(
                    anchorMin: "1 0", 
                    anchorMax: "1 1", 
                    offsetMin: "-64 0", 
                    offsetMax: "0 0"
                );
                
                root.Render(connection);
                connectionData.IsDestroyed = false;
            }

            // ═══════════════════════════════════════════════════════════════════════════════
            // ENHANCED PANEL RENDERING - Modern Card Style
            // ═══════════════════════════════════════════════════════════════════════════════
            public void RenderPanel(Panel panel)
            {
                if (panel == null) return;
                
                float fadeTime = config.EnableAnimations ? FADE_IN_NORMAL : 0f;
                CUI.Root root = new CUI.Root("AdminMenuEnhanced_Body");
                
                var container = root.AddContainer(
                    anchorMin: "0 0", 
                    anchorMax: "1 1", 
                    name: "AdminMenuEnhanced_Panel"
                ).AddDestroySelfAttribute();
                
                Sidebar sidebar = panel.Sidebar;
                if (sidebar != null) AddSidebar(container, sidebar);
                
                // Content area with modern styling
                CUI.Element panelBackground = container.AddContainer(
                    anchorMin: "0 0", 
                    anchorMax: "1 1", 
                    offsetMin: $"{(sidebar != null ? 260 : 0)} 0", 
                    offsetMax: "0 0", 
                    name: "AdminMenuEnhanced_Panel_Content"
                );
                
                // Add subtle background pattern
                panelBackground.AddPanel(
                    color: COLOR_BG_LIGHT, 
                    material: MAT_PANEL, 
                    imageType: Image.Type.Tiled
                ).WithFade(fadeTime);
                
                // Top accent line
                panelBackground.AddPanel(
                    color: COLOR_ACCENT_GLOW, 
                    anchorMin: "0 1", 
                    anchorMax: "1 1", 
                    offsetMin: "0 -1", 
                    offsetMax: "0 0"
                );
                
                root.Render(connection);
            }

            public void RenderContent(Content content)
            {
                if (content == null) return;
                
                CUI.Root root = new CUI.Root("AdminMenuEnhanced_Panel_Content");
                var container = root.AddContainer(
                    name: "AdminMenuEnhanced_Panel_TempContent"
                ).AddDestroySelfAttribute();
                root.Render(connection);
                content.Render(connectionData);
            }
        }
        #endregion

        #region Helper Classes
        public static class Extensions
        {
            public static string ToCuiString(Color color)
            {
                return string.Format("{0} {1} {2} {3}", color.r, color.g, color.b, color.a);
            }
        }

        public class Label
        {
            private static readonly Regex richTextRegex = new Regex(@"<[^>]*>");
            string label;
            string langKey;
            
            public Label(string label)
            {
                this.label = label;
                if (!string.IsNullOrEmpty(label))
                {
                    this.langKey = richTextRegex.Replace(label.ToPrintable(), string.Empty).Trim();
                    if (!Instance.defaultLang.ContainsKey(this.langKey))
                        Instance.defaultLang.Add(this.langKey, label);
                }
            }

            public override string ToString() => label;
            public string Localize(string userId) => this.langKey != null ? Instance.lang.GetMessage(this.langKey, Instance, userId) : this.label;
            public string Localize(Connection connection) => Localize(connection.userid.ToString());
        }

        public class MainMenu
        {
            public ButtonArray NavButtons { get; set; }
        }

        public class Panel
        {
            public virtual Sidebar Sidebar { get; set; }
            public virtual Dictionary<string, Content> Content { get; set; }
            public Content DefaultContent => TryGetContent("default");

            public Content TryGetContent(string id)
            {
                if (Content == null) return null;
                if (Content.TryGetValue(id, out Content content)) return content;
                return null;
            }

            public virtual void OnOpen(ConnectionData connectionData)
            {
                connectionData.UI.UpdateNavButtons(connectionData.currentMainMenu);
            }

            public virtual void OnClose(ConnectionData connectionData)
            {
                connectionData.userData["backcommand"] = null;
            }
        }

        public class UserInfoPanel : Panel { }

        public class PermissionPanel : Panel
        {
            public override Sidebar Sidebar => null;
            public override void OnClose(ConnectionData connectionData)
            {
                connectionData.userData["backcommand"] = null;
            }
        }

        public class Sidebar
        {
            public ButtonArray<CategoryButton> CategoryButtons { get; set; }
            public int? AutoActivateCategoryButtonIndex { get; set; } = 0;
        }

        public class SteamInfo
        {
            public string Location { get; set; }
            public string[] Avatars { get; set; }
            public string RegistrationDate { get; set; }
            public string RustHours { get; set; }
            public override string ToString() => string.Join(", ", Location, RegistrationDate, RustHours);
        }
        #endregion

        #region Translator
        public class Translator15
        {
            public static void Init(string language)
            {
                if (ignoreLang.Contains(language)) return;

                Dictionary<string, string> lang = new Dictionary<string, string>();
                TextAsset textAsset = FileSystem.Load<TextAsset>($"assets/localization/{language}/engine.json", true);
                if (textAsset == null) { ignoreLang.Add(language); return; }

                Dictionary<string, string> @object = JsonConvert.DeserializeObject<Dictionary<string, string>>(textAsset.text);
                if (@object == null) { ignoreLang.Add(language); return; }

                foreach (ItemDefinition itemDefinition in ItemManager.itemList)
                {
                    string key = itemDefinition.displayName.token;
                    if (@object.ContainsKey(key)) lang[key] = @object[key];
                }
                translations[language] = lang;
            }

            private static HashSet<string> ignoreLang = new HashSet<string>();
            private static Dictionary<string, Dictionary<string, string>> translations = new Dictionary<string, Dictionary<string, string>>();
            
            public class Phrase : Translate.Phrase
            {
                private Translator15 translator;
                public Phrase(Translator15 translator) { this.translator = translator; }
                public override string translated
                {
                    get
                    {
                        if (string.IsNullOrEmpty(this.token)) return this.english;
                        return translator.Get(this.token, this.english);
                    }
                }
            }

            protected ulong userID;
            protected string userIDString;
            public Translator15(ulong userID)
            {
                this.userID = userID;
                this.userIDString = userID.ToString();
            }

            public Translator15.Phrase Convert(Translate.Phrase phrase)
            {
                if (phraseCache.TryGetValue(phrase, out Translator15.Phrase phrase1)) return phrase1;
                else return phraseCache[phrase] = new Translator15.Phrase(this) { english = phrase.english, token = phrase.token };
            }

            private string GetLanguage() => Instance.lang.GetLanguage(userIDString);

            public string Get(string key, string def = null)
            {
                if (def == null) def = "#" + key;
                if (string.IsNullOrEmpty(key)) return def;
                string language = GetLanguage();
                if (!Translator15.translations.ContainsKey(language)) Translator15.Init(language);
                if (Translator15.translations.TryGetValue(language, out var lang) && lang.TryGetValue(key, out string result)) return result;
                return def;
            }

            private Dictionary<Translate.Phrase, Translator15.Phrase> phraseCache = new Dictionary<Translate.Phrase, Phrase>();
        }
        #endregion

        #region Content Base Classes
        public abstract class Content
        {
            public virtual void LoadDefaultUserData(Dictionary<string, object> userData) { }
            public virtual void RestoreUserData(Dictionary<string, object> userData) { }
            
            public void Render(ConnectionData connectionData)
            {
                float fadeTime = config.EnableAnimations ? FADE_IN_NORMAL : 0f;
                CUI.Root root = new CUI.Root("AdminMenuEnhanced_Panel_TempContent");
                var container = root.AddContainer(
                    anchorMin: "0.02 0.02", 
                    anchorMax: "0.98 0.98", 
                    name: "ContentContainer"
                ).AddDestroySelfAttribute();
                Render(container, connectionData, connectionData.userData);
                root.Render(connectionData.connection);
            }

            protected abstract void Render(CUI.Element root, ConnectionData connectionData, Dictionary<string, object> userData);
        }

        public class CenteredTextContent : Content
        {
            public string text;
            protected override void Render(CUI.Element root, ConnectionData connectionData, Dictionary<string, object> userData)
            {
                root.AddText(
                    text: text, 
                    color: COLOR_TEXT_SECONDARY, 
                    font: CUI.Font.RobotoCondensedRegular, 
                    fontSize: 16, 
                    align: TextAnchor.MiddleCenter
                );
            }
        }

        public class TextContent : Content
        {
            public string text;
            public CUI.Font font = CUI.Font.RobotoCondensedRegular;
            public int fontSize = 14;
            
            protected override void Render(CUI.Element root, ConnectionData connectionData, Dictionary<string, object> userData)
            {
                var scrollContainer = root.AddPanel(color: "0 0 0 0");
                scrollContainer.Components.AddScrollView(vertical: true, scrollSensitivity: 30, anchorMin: "0 -5");
                scrollContainer.AddText(
                    text: text, 
                    color: COLOR_TEXT_PRIMARY, 
                    font: font, 
                    fontSize: fontSize,
                    offsetMin: "10 10",
                    offsetMax: "-10 -10"
                );
            }
        }
        #endregion

        #region Enhanced Quick Menu Content
        public class QuickMenuContent : Content
        {
            public ButtonGrid<Button> buttonGrid;
            
            protected override void Render(CUI.Element root, ConnectionData connectionData, Dictionary<string, object> userData)
            {
                float fadeTime = config.EnableAnimations ? FADE_IN_FAST : 0f;
                var allowedButtons = buttonGrid.GetAllowedButtons(connectionData.connection);
                int maxRow = allowedButtons.Max(b => b.row) + 1;
                int maxColumn = allowedButtons.Max(b => b.column) + 1;
                
                float buttonWidth = 180f;
                float buttonHeight = 50f;
                float gapX = 12f;
                float gapY = 10f;
                
                foreach (var item in allowedButtons)
                {
                    Button button = item.button;
                    if (button == null) continue;
                    
                    float delay = config.EnableAnimations ? (item.row * 0.05f + item.column * 0.03f) : 0f;
                    bool isToggled = button.GetState(connectionData) == Button.State.Toggled;
                    
                    string bgColor = isToggled ? button.Style.ActiveBackgroundColor : button.Style.BackgroundColor;
                    string textColor = button.Style.TextColor;
                    
                    // Button container with rounded corners
                    var btn = root.AddButton(
                        command: $"adminmenuenhanced {button.Command} {string.Join(" ", button.Args)}", 
                        color: bgColor, 
                        sprite: SPRITE_BG_ROUNDED,
                        imageType: Image.Type.Sliced,
                        anchorMin: "0 1", 
                        anchorMax: "0 1", 
                        offsetMin: $"{item.column * (buttonWidth + gapX)} -{(item.row + 1) * (buttonHeight + gapY)}", 
                        offsetMax: $"{item.column * (buttonWidth + gapX) + buttonWidth} -{item.row * (buttonHeight + gapY) + gapY}"
                    ).WithFade(fadeTime + delay);
                    
                    // Accent line on left for toggled state
                    if (isToggled)
                    {
                        btn.AddPanel(
                            color: COLOR_ACCENT_PRIMARY, 
                            anchorMin: "0 0.15", 
                            anchorMax: "0 0.85", 
                            offsetMin: "0 0", 
                            offsetMax: "3 0"
                        );
                    }
                    
                    // Button text
                    btn.AddText(
                        text: button.Label?.Localize(connectionData.connection) ?? "", 
                        color: textColor, 
                        font: CUI.Font.RobotoCondensedBold, 
                        fontSize: 13, 
                        align: TextAnchor.MiddleCenter,
                        offsetMin: "8 0",
                        offsetMax: "-8 0"
                    );
                }
            }
        }
        #endregion

        #region Enhanced User Info Content
        public class UserInfoContent : Content
        {
            public ButtonGrid<Button> buttonGrid;
            
            public static string GetDisplayName(IPlayer player)
            {
                if (player == null) return "Unknown";
                return player.Name ?? player.Id;
            }
            
            protected override void Render(CUI.Element root, ConnectionData connectionData, Dictionary<string, object> userData)
            {
                float fadeTime = config.EnableAnimations ? FADE_IN_FAST : 0f;
                
                string targetUserId = userData.ContainsKey("userinfo.userid") ? userData["userinfo.userid"]?.ToString() : null;
                if (string.IsNullOrEmpty(targetUserId)) return;
                
                IPlayer targetPlayer = Instance.covalence.Players.FindPlayerById(targetUserId);
                if (targetPlayer == null) return;
                
                // Player info header card
                var headerCard = root.AddPanel(
                    color: COLOR_BG_CARD, 
                    sprite: SPRITE_BG_ROUNDED,
                    imageType: Image.Type.Sliced,
                    anchorMin: "0 1", 
                    anchorMax: "1 1", 
                    offsetMin: "0 -100", 
                    offsetMax: "0 0"
                ).WithFade(fadeTime);
                
                // Player name
                headerCard.AddText(
                    text: targetPlayer.Name, 
                    color: COLOR_TEXT_PRIMARY, 
                    font: CUI.Font.RobotoCondensedBold, 
                    fontSize: 24, 
                    align: TextAnchor.MiddleLeft,
                    anchorMin: "0 0.5",
                    anchorMax: "0.7 1",
                    offsetMin: "20 0",
                    offsetMax: "-10 -10"
                );
                
                // Steam ID
                headerCard.AddText(
                    text: targetUserId, 
                    color: COLOR_TEXT_MUTED, 
                    font: CUI.Font.RobotoMonoRegular, 
                    fontSize: 12, 
                    align: TextAnchor.MiddleLeft,
                    anchorMin: "0 0",
                    anchorMax: "0.7 0.5",
                    offsetMin: "20 10",
                    offsetMax: "-10 0"
                );
                
                // Online status indicator
                bool isOnline = targetPlayer.IsConnected;
                headerCard.AddPanel(
                    color: isOnline ? COLOR_ONLINE : COLOR_OFFLINE, 
                    sprite: SPRITE_CIRCLE,
                    anchorMin: "1 1", 
                    anchorMax: "1 1", 
                    offsetMin: "-30 -30", 
                    offsetMax: "-14 -14"
                );
                
                headerCard.AddText(
                    text: isOnline ? "ONLINE" : "OFFLINE", 
                    color: isOnline ? COLOR_ONLINE : COLOR_OFFLINE, 
                    font: CUI.Font.RobotoCondensedBold, 
                    fontSize: 11, 
                    align: TextAnchor.MiddleRight,
                    anchorMin: "0.7 0.6",
                    anchorMax: "1 1",
                    offsetMin: "0 0",
                    offsetMax: "-40 -10"
                );
                
                // Action buttons grid
                var allowedButtons = buttonGrid.GetAllowedButtons(connectionData.connection);
                float buttonWidth = 160f;
                float buttonHeight = 42f;
                float gapX = 10f;
                float gapY = 8f;
                float startY = 120f;
                
                foreach (var item in allowedButtons)
                {
                    Button button = item.button;
                    if (button == null) continue;
                    
                    float delay = config.EnableAnimations ? (item.row * 0.04f + item.column * 0.02f) : 0f;
                    bool isToggled = button.GetState(connectionData) == Button.State.Toggled;
                    
                    string bgColor = isToggled ? button.Style.ActiveBackgroundColor : button.Style.BackgroundColor;
                    
                    var btn = root.AddButton(
                        command: $"adminmenuenhanced {button.Command} {string.Join(" ", button.Args)}", 
                        color: bgColor, 
                        sprite: SPRITE_BG_ROUNDED,
                        imageType: Image.Type.Sliced,
                        anchorMin: "0 1", 
                        anchorMax: "0 1", 
                        offsetMin: $"{item.column * (buttonWidth + gapX)} -{startY + (item.row + 1) * (buttonHeight + gapY)}", 
                        offsetMax: $"{item.column * (buttonWidth + gapX) + buttonWidth} -{startY + item.row * (buttonHeight + gapY) + gapY}"
                    ).WithFade(fadeTime + delay);
                    
                    btn.AddText(
                        text: button.Label?.Localize(connectionData.connection) ?? "", 
                        color: button.Style.TextColor, 
                        font: CUI.Font.RobotoCondensedBold, 
                        fontSize: 12, 
                        align: TextAnchor.MiddleCenter,
                        offsetMin: "6 0",
                        offsetMax: "-6 0"
                    );
                }
            }
        }
        #endregion

        #region Enhanced Player List Content
        public class PlayerListContent : Content
        {
            private static readonly Label SEARCH_LABEL = new Label("Search players...");
            private static List<Timer> sequentialLoad = new List<Timer>();
            
            public override void LoadDefaultUserData(Dictionary<string, object> userData)
            {
                userData["playerlist.executeCommand"] = "adminmenuenhanced userinfo.open";
                if (!userData.ContainsKey("playerlist.filter"))
                    userData["playerlist.filter"] = (Func<IPlayer, bool>)((IPlayer player) => true);
                userData["playerlist.searchQuery"] = string.Empty;
            }

            public override void RestoreUserData(Dictionary<string, object> userData)
            {
                userData["playerlist.filter"] = (Func<IPlayer, bool>)((IPlayer player) => true);
                StopSequentialLoad();
            }

            public void StopSequentialLoad()
            {
                foreach (Timer timer in sequentialLoad)
                    if (!timer.Destroyed) timer.Destroy();
                sequentialLoad.Clear();
            }

            protected override void Render(CUI.Element root, ConnectionData connectionData, Dictionary<string, object> userData)
            {
                float fadeTime = config.EnableAnimations ? FADE_IN_FAST : 0f;
                Func<IPlayer, bool> filter = (Func<IPlayer, bool>)userData["playerlist.filter"];
                string searchQuery = (string)userData["playerlist.searchQuery"];
                
                int columns = 4;
                int rows = 14;
                int playersPerPage = columns * rows;
                
                List<IPlayer> players = Instance.covalence.Players.All.Where(filter).OrderBy(p => p.Name).ToList();
                if (!string.IsNullOrEmpty(searchQuery))
                    players = players.Where(player => player.Name.ToLower().Contains(searchQuery.ToLower()) || player.Id == searchQuery).ToList();
                
                int pageCount = Mathf.CeilToInt(players.Count / (float)playersPerPage);
                if (pageCount == 0) pageCount = 1;
                
                // Search bar at bottom
                var bottom = root.AddContainer(
                    anchorMin: "0 0", 
                    anchorMax: "1 0", 
                    offsetMin: "0 0", 
                    offsetMax: "0 45", 
                    name: "AdminMenuEnhanced_PlayerList_Bottom"
                ).AddDestroySelfAttribute();
                
                var searchPanel = bottom.AddButton(
                    command: "adminmenuenhanced playerlist.opensearch", 
                    color: COLOR_BG_CARD, 
                    sprite: SPRITE_BG_ROUNDED,
                    imageType: Image.Type.Sliced,
                    anchorMin: "0 0", 
                    anchorMax: "0 1", 
                    offsetMin: "0 8", 
                    offsetMax: "280 -8", 
                    name: "Search"
                );
                
                // Search icon indicator
                searchPanel.AddPanel(
                    color: COLOR_ACCENT_PRIMARY, 
                    anchorMin: "0 0.3", 
                    anchorMax: "0 0.7", 
                    offsetMin: "12 0", 
                    offsetMax: "14 0"
                );
                
                if (string.IsNullOrEmpty(searchQuery))
                {
                    searchPanel.AddText(
                        text: SEARCH_LABEL.Localize(connectionData.connection), 
                        color: COLOR_TEXT_MUTED,
                        font: CUI.Font.RobotoCondensedRegular, 
                        fontSize: 14,
                        align: TextAnchor.MiddleLeft, 
                        offsetMin: "24 0",
                        offsetMax: "-10 0",
                        name: "Search_Placeholder"
                    );
                }
                else
                {
                    searchPanel.AddInputfield(
                        command: "adminmenuenhanced playerlist.search.input", 
                        text: searchQuery, 
                        color: COLOR_TEXT_PRIMARY,
                        align: TextAnchor.MiddleLeft, 
                        offsetMin: "24 0",
                        offsetMax: "-10 0",
                        name: "Search_Inputfield"
                    );
                }
                
                // Player count
                bottom.AddText(
                    text: $"{players.Count} players", 
                    color: COLOR_TEXT_MUTED, 
                    font: CUI.Font.RobotoCondensedRegular, 
                    fontSize: 12, 
                    align: TextAnchor.MiddleRight,
                    anchorMin: "1 0",
                    anchorMax: "1 1",
                    offsetMin: "-150 0",
                    offsetMax: "0 0"
                );
                
                // Player grid
                var layout = root.AddPanel(
                    color: "0 0 0 0", 
                    anchorMin: "0 0", 
                    anchorMax: "1 1", 
                    offsetMin: "0 55", 
                    offsetMax: "0 0", 
                    name: "AdminMenuEnhanced_PlayerList_Layout"
                ).AddDestroySelfAttribute();
                
                layout.Components.AddScrollView(vertical: true, scrollSensitivity: 25, anchorMin: $"0 -{pageCount - 1}", anchorMax: "1 1");
                
                float width = 1f / columns;
                float height = 1f / rows;
                
                StopSequentialLoad();
                
                for (int i = 0; i < pageCount; i++)
                {
                    CUI.Root layoutRoot = new CUI.Root(layout.Name);
                    var screenContainer = layoutRoot.AddContainer(
                        anchorMin: $"0 {(pageCount - (i + 1)) / (float)pageCount}", 
                        anchorMax: $"1 {(pageCount - i) / (float)pageCount}", 
                        name: $"{layoutRoot.Name}_Screen_{i}"
                    ).AddDestroySelfAttribute();
                    
                    for (int a = 0; a < rows; a++)
                    {
                        for (int b = 0; b < columns; b++)
                        {
                            int index = i * playersPerPage + a * columns + b;
                            IPlayer player = players.ElementAtOrDefault(index);
                            if (player == null) break;
                            
                            float delay = config.EnableAnimations ? (a * 0.02f + b * 0.01f) : 0f;
                            
                            var container = screenContainer.AddContainer(
                                anchorMin: $"{b * width} {1 - (a + 1) * height}", 
                                anchorMax: $"{(b + 1) * width} {1 - a * height}"
                            );
                            
                            // Player card button
                            var button = container.AddButton(
                                command: $"{userData["playerlist.executeCommand"]} {player.Id}", 
                                color: COLOR_BG_CARD, 
                                sprite: SPRITE_BG_ROUNDED,
                                imageType: Image.Type.Sliced,
                                anchorMin: "0.5 0.5", 
                                anchorMax: "0.5 0.5", 
                                offsetMin: "-85 -22", 
                                offsetMax: "85 22"
                            ).WithFade(fadeTime + delay);
                            
                            // Status indicator and frame color
                            string frameColor = COLOR_BORDER;
                            var serverUser = ServerUsers.Get(ulong.Parse(player.Id));
                            if (serverUser != null)
                            {
                                switch (serverUser.group)
                                {
                                    case ServerUsers.UserGroup.Owner:
                                        frameColor = COLOR_ADMIN;
                                        break;
                                    case ServerUsers.UserGroup.Moderator:
                                        frameColor = COLOR_MODERATOR;
                                        break;
                                    case ServerUsers.UserGroup.Banned:
                                        frameColor = COLOR_BANNED;
                                        break;
                                }
                            }
                            
                            // Left accent line
                            button.AddPanel(
                                color: frameColor, 
                                anchorMin: "0 0.15", 
                                anchorMax: "0 0.85", 
                                offsetMin: "0 0", 
                                offsetMax: "2 0"
                            );
                            
                            // Online indicator dot
                            button.AddPanel(
                                color: player.IsConnected ? COLOR_ONLINE : COLOR_OFFLINE, 
                                sprite: SPRITE_CIRCLE,
                                anchorMin: "0 0.5", 
                                anchorMax: "0 0.5", 
                                offsetMin: "8 -3", 
                                offsetMax: "14 3"
                            );
                            
                            // Player name
                            button.AddText(
                                text: player.Name, 
                                color: COLOR_TEXT_PRIMARY,
                                fontSize: 12, 
                                font: CUI.Font.RobotoCondensedRegular,
                                align: TextAnchor.MiddleLeft, 
                                overflow: VerticalWrapMode.Truncate,
                                offsetMin: "20 0", 
                                offsetMax: "-8 0"
                            );
                        }
                    }
                    
                    sequentialLoad.Add(Instance.timer.Once(i * 0.08f, () => layoutRoot.Render(connectionData.connection)));
                }
            }

            public void OpenSearch(Connection connection)
            {
                CUI.Root root = new CUI.Root("Search");
                root.AddInputfield(
                    command: "adminmenuenhanced playerlist.search.input", 
                    text: "", 
                    color: COLOR_TEXT_PRIMARY,
                    align: TextAnchor.MiddleLeft, 
                    autoFocus: true, 
                    offsetMin: "24 0",
                    offsetMax: "-10 0",
                    name: "Search_Inputfield"
                ).DestroyUi = "Search_Placeholder";
                root.Render(connection);
            }
        }
        #endregion

        #region Group List Content
        public class GroupListContent : Content
        {
            private static readonly Label CREATE_GROUP_LABEL = new Label("+ Create Group");
            
            protected override void Render(CUI.Element root, ConnectionData connectionData, Dictionary<string, object> userData)
            {
                float fadeTime = config.EnableAnimations ? FADE_IN_FAST : 0f;
                string[] groups = Instance.permission.GetGroups();
                
                // Header with create button
                var header = root.AddContainer(
                    anchorMin: "0 1", 
                    anchorMax: "1 1", 
                    offsetMin: "0 -50", 
                    offsetMax: "0 0"
                );
                
                header.AddButton(
                    command: "adminmenuenhanced grouplist[popup:creategroup] show", 
                    color: COLOR_BTN_SUCCESS, 
                    sprite: SPRITE_BG_ROUNDED,
                    imageType: Image.Type.Sliced,
                    anchorMin: "1 0.5", 
                    anchorMax: "1 0.5", 
                    offsetMin: "-160 -18", 
                    offsetMax: "0 18"
                ).AddText(
                    text: CREATE_GROUP_LABEL.Localize(connectionData.connection), 
                    color: COLOR_TEXT_PRIMARY, 
                    font: CUI.Font.RobotoCondensedBold, 
                    fontSize: 14, 
                    align: TextAnchor.MiddleCenter
                );
                
                // Groups list
                var listContainer = root.AddPanel(
                    color: "0 0 0 0", 
                    anchorMin: "0 0", 
                    anchorMax: "1 1", 
                    offsetMin: "0 0", 
                    offsetMax: "0 -60"
                );
                
                int columns = 3;
                float cardWidth = 200f;
                float cardHeight = 45f;
                float gapX = 12f;
                float gapY = 10f;
                
                for (int i = 0; i < groups.Length; i++)
                {
                    string groupName = groups[i];
                    int row = i / columns;
                    int col = i % columns;
                    float delay = config.EnableAnimations ? i * 0.03f : 0f;
                    
                    var card = listContainer.AddButton(
                        command: $"adminmenuenhanced permissionmanager.select_group {groupName}", 
                        color: COLOR_BG_CARD, 
                        sprite: SPRITE_BG_ROUNDED,
                        imageType: Image.Type.Sliced,
                        anchorMin: "0 1", 
                        anchorMax: "0 1", 
                        offsetMin: $"{col * (cardWidth + gapX)} -{(row + 1) * (cardHeight + gapY)}", 
                        offsetMax: $"{col * (cardWidth + gapX) + cardWidth} -{row * (cardHeight + gapY) + gapY}"
                    ).WithFade(fadeTime + delay);
                    
                    card.AddText(
                        text: groupName, 
                        color: COLOR_TEXT_PRIMARY, 
                        font: CUI.Font.RobotoCondensedBold, 
                        fontSize: 14, 
                        align: TextAnchor.MiddleCenter
                    );
                }
            }
        }
        #endregion

        #region Group Info Content
        public class GroupInfoContent : Content
        {
            public ButtonArray[] buttons;
            
            protected override void Render(CUI.Element root, ConnectionData connectionData, Dictionary<string, object> userData)
            {
                float fadeTime = config.EnableAnimations ? FADE_IN_FAST : 0f;
                string groupName = userData.ContainsKey("groupinfo.groupName") ? userData["groupinfo.groupName"]?.ToString() : null;
                if (string.IsNullOrEmpty(groupName)) return;
                
                // Group info header
                var header = root.AddPanel(
                    color: COLOR_BG_CARD, 
                    sprite: SPRITE_BG_ROUNDED,
                    imageType: Image.Type.Sliced,
                    anchorMin: "0 1", 
                    anchorMax: "1 1", 
                    offsetMin: "0 -80", 
                    offsetMax: "0 0"
                ).WithFade(fadeTime);
                
                header.AddText(
                    text: groupName.ToUpper(), 
                    color: COLOR_TEXT_PRIMARY, 
                    font: CUI.Font.RobotoCondensedBold, 
                    fontSize: 28, 
                    align: TextAnchor.MiddleLeft,
                    offsetMin: "20 0",
                    offsetMax: "-20 0"
                );
                
                // Action buttons
                if (buttons != null && buttons.Length > 0)
                {
                    float buttonWidth = 160f;
                    float buttonHeight = 40f;
                    float gapX = 10f;
                    float startY = 100f;
                    
                    for (int i = 0; i < buttons[0].Count; i++)
                    {
                        Button button = buttons[0][i];
                        if (button == null || !button.UserHasPermission(connectionData.connection)) continue;
                        
                        float delay = config.EnableAnimations ? i * 0.05f : 0f;
                        
                        root.AddButton(
                            command: $"adminmenuenhanced {button.Command} {string.Join(" ", button.Args)}", 
                            color: button.Style.BackgroundColor, 
                            sprite: SPRITE_BG_ROUNDED,
                            imageType: Image.Type.Sliced,
                            anchorMin: "0 1", 
                            anchorMax: "0 1", 
                            offsetMin: $"{i * (buttonWidth + gapX)} -{startY + buttonHeight}", 
                            offsetMax: $"{i * (buttonWidth + gapX) + buttonWidth} -{startY}"
                        ).WithFade(fadeTime + delay).AddText(
                            text: button.Label?.Localize(connectionData.connection) ?? "", 
                            color: button.Style.TextColor, 
                            font: CUI.Font.RobotoCondensedBold, 
                            fontSize: 13, 
                            align: TextAnchor.MiddleCenter
                        );
                    }
                }
            }
        }
        #endregion

        #region CUI System
        public class CUI
        {
            public enum Font
            {
                RobotoCondensedBold,
                RobotoCondensedRegular,
                RobotoMonoRegular,
                DroidSansMono,
                PermanentMarker,
                PressStart2PRegular,
                LSD,
                NotoSansArabicBold,
                NotoSansArabicRegular,
                NotoSansHebrewBold,
            }

            private static readonly Dictionary<Font, string> FontToString = new Dictionary<Font, string>
            {
                { Font.RobotoCondensedBold, "RobotoCondensed-Bold.ttf" },
                { Font.RobotoCondensedRegular, "RobotoCondensed-Regular.ttf" },
                { Font.RobotoMonoRegular, "RobotoMono-Regular.ttf" },
                { Font.DroidSansMono, "DroidSansMono.ttf" },
                { Font.PermanentMarker, "PermanentMarker.ttf" },
                { Font.PressStart2PRegular, "PressStart2P-Regular.ttf" },
                { Font.LSD, "lcd.ttf" },
                { Font.NotoSansArabicBold, "_nonenglish/arabic/notosansarabic-bold.ttf" },
                { Font.NotoSansArabicRegular, "_nonenglish/arabic/notosansarabic-regular.ttf" },
                { Font.NotoSansHebrewBold, "_nonenglish/notosanshebrew-bold.ttf" },
            };

            public enum InputType { None, Default, HudMenuInput }

            private static readonly Dictionary<TextAnchor, string> TextAnchorToString = new Dictionary<TextAnchor, string>
            {
                { TextAnchor.UpperLeft, "UpperLeft" }, { TextAnchor.UpperCenter, "UpperCenter" }, { TextAnchor.UpperRight, "UpperRight" },
                { TextAnchor.MiddleLeft, "MiddleLeft" }, { TextAnchor.MiddleCenter, "MiddleCenter" }, { TextAnchor.MiddleRight, "MiddleRight" },
                { TextAnchor.LowerLeft, "LowerLeft" }, { TextAnchor.LowerCenter, "LowerCenter" }, { TextAnchor.LowerRight, "LowerRight" }
            };

            private static readonly Dictionary<VerticalWrapMode, string> VWMToString = new Dictionary<VerticalWrapMode, string>
            {
                { VerticalWrapMode.Truncate, "Truncate" }, { VerticalWrapMode.Overflow, "Overflow" }
            };

            private static readonly Dictionary<Image.Type, string> ImageTypeToString = new Dictionary<Image.Type, string>
            {
                { Image.Type.Simple, "Simple" }, { Image.Type.Sliced, "Sliced" }, { Image.Type.Tiled, "Tiled" }, { Image.Type.Filled, "Filled" }
            };

            private static readonly Dictionary<InputField.LineType, string> LineTypeToString = new Dictionary<InputField.LineType, string>
            {
                { InputField.LineType.MultiLineNewline, "MultiLineNewline" }, { InputField.LineType.MultiLineSubmit, "MultiLineSubmit" }, { InputField.LineType.SingleLine, "SingleLine" }
            };

            private static readonly Dictionary<ScrollRect.MovementType, string> MovementTypeToString = new Dictionary<ScrollRect.MovementType, string>
            {
                { ScrollRect.MovementType.Unrestricted, "Unrestricted" }, { ScrollRect.MovementType.Elastic, "Elastic" }, { ScrollRect.MovementType.Clamped, "Clamped" }
            };

            public static class Defaults
            {
                public const string VectorZero = "0 0";
                public const string VectorOne = "1 1";
                public const string Color = "1 1 1 1";
                public const string OutlineColor = "0 0 0 1";
                public const string Sprite = "assets/content/ui/ui.background.tile.psd";
                public const string Material = "assets/content/ui/namefontmaterial.mat";
                public const string IconMaterial = "assets/icons/iconmaterial.mat";
                public const Image.Type ImageType = Image.Type.Simple;
                public const CUI.Font Font = CUI.Font.RobotoCondensedRegular;
                public const int FontSize = 14;
                public const TextAnchor Align = TextAnchor.UpperLeft;
                public const VerticalWrapMode VerticalOverflow = VerticalWrapMode.Overflow;
                public const InputField.LineType LineType = InputField.LineType.SingleLine;
            }

            public static Color GetColor(string colorStr) => ColorEx.Parse(colorStr);
            public static string GetColorString(Color color) => string.Format("{0} {1} {2} {3}", color.r, color.g, color.b, color.a);

            public static void AddUI(Connection connection, string json)
            {
                CommunityEntity.ServerInstance.ClientRPCEx<string>(new SendInfo { connection = connection }, null, "AddUI", json);
            }

            private static void SerializeComponent(ICuiComponent IComponent, JsonWriter jsonWriter)
            {
                string colorWhite = "1 1 1 1";
                string defaultOutlineDistance = "1 -1";
                
                void SerializeType() { jsonWriter.WritePropertyName("type"); jsonWriter.WriteValue(IComponent.Type); }
                void SerializeField(string key, object value, object defaultValue)
                {
                    if (value != null && !value.Equals(defaultValue))
                    {
                        if (value is string && defaultValue != null && string.IsNullOrEmpty(value as string)) return;
                        jsonWriter.WritePropertyName(key);
                        jsonWriter.WriteValue(value ?? defaultValue);
                    }
                }

                switch (IComponent.Type)
                {
                    case "RectTransform":
                    {
                        CuiRectTransformComponent component = IComponent as CuiRectTransformComponent;
                        jsonWriter.WriteStartObject();
                        SerializeType();
                        SerializeField("anchormin", component.AnchorMin, Defaults.VectorZero);
                        SerializeField("anchormax", component.AnchorMax, Defaults.VectorOne);
                        SerializeField("offsetmin", component.OffsetMin, Defaults.VectorZero);
                        SerializeField("offsetmax", component.OffsetMax, Defaults.VectorZero);
                        jsonWriter.WriteEndObject();
                        break;
                    }
                    case "UnityEngine.UI.RawImage":
                    {
                        CuiRawImageComponent component = IComponent as CuiRawImageComponent;
                        jsonWriter.WriteStartObject();
                        SerializeType();
                        SerializeField("sprite", component.Sprite, null);
                        SerializeField("color", component.Color, colorWhite);
                        SerializeField("material", component.Material, null);
                        SerializeField("url", component.Url, null);
                        SerializeField("png", component.Png, null);
                        SerializeField("steamid", component.SteamId, null);
                        SerializeField("fadeIn", component.FadeIn, 0f);
                        jsonWriter.WriteEndObject();
                        break;
                    }
                    case "UnityEngine.UI.Image":
                    {
                        CuiImageComponent component = IComponent as CuiImageComponent;
                        jsonWriter.WriteStartObject();
                        SerializeType();
                        SerializeField("sprite", component.Sprite, Defaults.Sprite);
                        SerializeField("material", component.Material, Defaults.Material);
                        SerializeField("color", component.Color, colorWhite);
                        SerializeField("imagetype", ImageTypeToString[component.ImageType], ImageTypeToString[Defaults.ImageType]);
                        SerializeField("png", component.Png, null);
                        SerializeField("itemid", component.ItemId, 0);
                        SerializeField("skinid", component.SkinId, 0UL);
                        SerializeField("fadeIn", component.FadeIn, 0f);
                        jsonWriter.WriteEndObject();
                        break;
                    }
                    case "UnityEngine.UI.Text":
                    {
                        CuiTextComponent component = IComponent as CuiTextComponent;
                        jsonWriter.WriteStartObject();
                        SerializeType();
                        SerializeField("text", component.Text, null);
                        SerializeField("fontSize", component.FontSize, Defaults.FontSize);
                        SerializeField("font", component.Font, FontToString[Defaults.Font]);
                        SerializeField("align", TextAnchorToString[component.Align], TextAnchorToString[Defaults.Align]);
                        SerializeField("color", component.Color, colorWhite);
                        SerializeField("verticalOverflow", VWMToString[component.VerticalOverflow], VWMToString[Defaults.VerticalOverflow]);
                        SerializeField("fadeIn", component.FadeIn, 0f);
                        jsonWriter.WriteEndObject();
                        break;
                    }
                    case "UnityEngine.UI.Button":
                    {
                        CuiButtonComponent component = IComponent as CuiButtonComponent;
                        jsonWriter.WriteStartObject();
                        SerializeType();
                        SerializeField("command", component.Command, null);
                        SerializeField("close", component.Close, null);
                        SerializeField("sprite", component.Sprite, Defaults.Sprite);
                        SerializeField("material", component.Material, Defaults.Material);
                        SerializeField("color", component.Color, colorWhite);
                        SerializeField("imagetype", ImageTypeToString[component.ImageType], ImageTypeToString[Defaults.ImageType]);
                        SerializeField("fadeIn", component.FadeIn, 0f);
                        jsonWriter.WriteEndObject();
                        break;
                    }
                    case "UnityEngine.UI.InputField":
                    {
                        CuiInputFieldComponent component = IComponent as CuiInputFieldComponent;
                        jsonWriter.WriteStartObject();
                        SerializeType();
                        SerializeField("text", component.Text, null);
                        SerializeField("fontSize", component.FontSize, Defaults.FontSize);
                        SerializeField("font", component.Font, FontToString[Defaults.Font]);
                        SerializeField("align", TextAnchorToString[component.Align], TextAnchorToString[Defaults.Align]);
                        SerializeField("color", component.Color, colorWhite);
                        SerializeField("characterLimit", component.CharsLimit, 0);
                        SerializeField("command", component.Command, null);
                        SerializeField("lineType", LineTypeToString[component.LineType], LineTypeToString[Defaults.LineType]);
                        SerializeField("readOnly", component.ReadOnly, false);
                        SerializeField("password", component.IsPassword, false);
                        SerializeField("needsKeyboard", component.NeedsKeyboard, false);
                        SerializeField("hudMenuInput", component.HudMenuInput, false);
                        SerializeField("autofocus", component.Autofocus, false);
                        jsonWriter.WriteEndObject();
                        break;
                    }
                    case "UnityEngine.UI.ScrollView":
                    {
                        CuiScrollViewComponent component = IComponent as CuiScrollViewComponent;
                        jsonWriter.WriteStartObject();
                        SerializeType();
                        if (component.ContentTransform != null)
                        {
                            jsonWriter.WritePropertyName("contentTransform");
                            jsonWriter.WriteStartObject();
                            jsonWriter.WritePropertyName("type");
                            jsonWriter.WriteValue("RectTransform");
                            SerializeField("anchormin", component.ContentTransform.AnchorMin, Defaults.VectorZero);
                            SerializeField("anchormax", component.ContentTransform.AnchorMax, Defaults.VectorOne);
                            SerializeField("offsetmin", component.ContentTransform.OffsetMin, Defaults.VectorZero);
                            SerializeField("offsetmax", component.ContentTransform.OffsetMax, Defaults.VectorZero);
                            jsonWriter.WriteEndObject();
                        }
                        SerializeField("horizontal", component.Horizontal, false);
                        SerializeField("vertical", component.Vertical, false);
                        SerializeField("movementType", MovementTypeToString[component.MovementType], MovementTypeToString[ScrollRect.MovementType.Clamped]);
                        SerializeField("elasticity", component.Elasticity, 0.1f);
                        SerializeField("inertia", component.Inertia, false);
                        SerializeField("decelerationRate", component.DecelerationRate, 0.135f);
                        SerializeField("scrollSensitivity", component.ScrollSensitivity, 1f);
                        jsonWriter.WriteEndObject();
                        break;
                    }
                    case "UnityEngine.UI.Outline":
                    {
                        CuiOutlineComponent component = IComponent as CuiOutlineComponent;
                        jsonWriter.WriteStartObject();
                        SerializeType();
                        SerializeField("color", component.Color, colorWhite);
                        SerializeField("distance", component.Distance, defaultOutlineDistance);
                        SerializeField("useGraphicAlpha", component.UseGraphicAlpha, false);
                        jsonWriter.WriteEndObject();
                        break;
                    }
                    case "NeedsKeyboard":
                    case "NeedsCursor":
                    {
                        jsonWriter.WriteStartObject();
                        SerializeType();
                        jsonWriter.WriteEndObject();
                        break;
                    }
                }
            }

            [JsonObject(MemberSerialization.OptIn)]
            public class Element : CuiElement
            {
                public new string Name { get; set; } = null;
                public Element ParentElement { get; set; }
                public virtual List<Element> Container => ParentElement?.Container;
                public ComponentList Components { get; set; } = new ComponentList();

                [JsonProperty("name")]
                public string JsonName
                {
                    get
                    {
                        if (Name == null)
                        {
                            string result = this.GetHashCode().ToString();
                            if (ParentElement != null) result.Insert(0, ParentElement.JsonName);
                            return result.GetHashCode().ToString();
                        }
                        return Name;
                    }
                }

                public Element() { }
                public Element(Element parent) { AssignParent(parent); }

                public CUI.Element AssignParent(Element parent)
                {
                    if (parent == null) return this;
                    ParentElement = parent;
                    Parent = ParentElement.JsonName;
                    return this;
                }

                public Element AddDestroy(string elementName) { this.DestroyUi = elementName; return this; }
                public Element AddDestroySelfAttribute() => AddDestroy(this.Name);

                public virtual void WriteJson(JsonWriter jsonWriter)
                {
                    jsonWriter.WriteStartObject();
                    jsonWriter.WritePropertyName("name");
                    jsonWriter.WriteValue(this.JsonName);
                    if (!string.IsNullOrEmpty(Parent)) { jsonWriter.WritePropertyName("parent"); jsonWriter.WriteValue(this.Parent); }
                    if (!string.IsNullOrEmpty(this.DestroyUi)) { jsonWriter.WritePropertyName("destroyUi"); jsonWriter.WriteValue(this.DestroyUi); }
                    if (this.Update) { jsonWriter.WritePropertyName("update"); jsonWriter.WriteValue(this.Update); }
                    if (this.FadeOut > 0f) { jsonWriter.WritePropertyName("fadeOut"); jsonWriter.WriteValue(this.FadeOut); }
                    jsonWriter.WritePropertyName("components");
                    jsonWriter.WriteStartArray();
                    for (int i = 0; i < this.Components.Count; i++) SerializeComponent(this.Components[i], jsonWriter);
                    jsonWriter.WriteEndArray();
                    jsonWriter.WriteEndObject();
                }

                public Element Add(Element element)
                {
                    if (element.ParentElement == null) element.AssignParent(this);
                    Container.Add(element);
                    return element;
                }

                public Element AddEmpty(string name = null) => Add(new Element(this) { Name = name });
                public Element AddUpdateElement(string name = null)
                {
                    Element element = AddEmpty(name);
                    element.Parent = null;
                    element.Update = true;
                    return element;
                }

                public Element AddText(string text, string color = Defaults.Color, CUI.Font font = Defaults.Font, int fontSize = Defaults.FontSize, TextAnchor align = Defaults.Align, VerticalWrapMode overflow = Defaults.VerticalOverflow, string anchorMin = Defaults.VectorZero, string anchorMax = Defaults.VectorOne, string offsetMin = Defaults.VectorZero, string offsetMax = Defaults.VectorZero, string name = null)
                    => Add(ElementContructor.CreateText(text, color, font, fontSize, align, overflow, anchorMin, anchorMax, offsetMin, offsetMax, name));

                public Element AddInputfield(string command = null, string text = "", string color = Defaults.Color, CUI.Font font = Defaults.Font, int fontSize = Defaults.FontSize, TextAnchor align = Defaults.Align, InputField.LineType lineType = Defaults.LineType, CUI.InputType inputType = CUI.InputType.Default, bool @readonly = false, bool autoFocus = false, bool isPassword = false, int charsLimit = 0, string anchorMin = Defaults.VectorZero, string anchorMax = Defaults.VectorOne, string offsetMin = Defaults.VectorZero, string offsetMax = Defaults.VectorZero, string name = null)
                    => Add(ElementContructor.CreateInputfield(command, text, color, font, fontSize, align, lineType, inputType, @readonly, autoFocus, isPassword, charsLimit, anchorMin, anchorMax, offsetMin, offsetMax, name));

                public Element AddPanel(string color = Defaults.Color, string sprite = Defaults.Sprite, string material = Defaults.Material, Image.Type imageType = Defaults.ImageType, string anchorMin = Defaults.VectorZero, string anchorMax = Defaults.VectorOne, string offsetMin = Defaults.VectorZero, string offsetMax = Defaults.VectorZero, bool cursorEnabled = false, bool keyboardEnabled = false, string name = null)
                    => Add(ElementContructor.CreatePanel(color, sprite, material, imageType, anchorMin, anchorMax, offsetMin, offsetMax, cursorEnabled, keyboardEnabled, name));

                public Element AddButton(string command = null, string close = null, string color = Defaults.Color, string sprite = Defaults.Sprite, string material = Defaults.Material, Image.Type imageType = Defaults.ImageType, string anchorMin = Defaults.VectorZero, string anchorMax = Defaults.VectorOne, string offsetMin = Defaults.VectorZero, string offsetMax = Defaults.VectorZero, string name = null)
                    => Add(ElementContructor.CreateButton(command, close, color, sprite, material, imageType, anchorMin, anchorMax, offsetMin, offsetMax, name));

                public Element AddImage(string content, string color = Defaults.Color, string material = null, string anchorMin = Defaults.VectorZero, string anchorMax = Defaults.VectorOne, string offsetMin = Defaults.VectorZero, string offsetMax = Defaults.VectorZero, string name = null)
                    => Add(ElementContructor.CreateImage(content, color, material, anchorMin, anchorMax, offsetMin, offsetMax, name));

                public Element AddIcon(int itemId, ulong skin = 0, string color = Defaults.Color, string sprite = Defaults.Sprite, string material = Defaults.IconMaterial, Image.Type imageType = Defaults.ImageType, string anchorMin = Defaults.VectorZero, string anchorMax = Defaults.VectorOne, string offsetMin = Defaults.VectorZero, string offsetMax = Defaults.VectorZero, string name = null)
                    => Add(ElementContructor.CreateIcon(itemId, skin, color, sprite, material, imageType, anchorMin, anchorMax, offsetMin, offsetMax, name));

                public Element AddContainer(string anchorMin = Defaults.VectorZero, string anchorMax = Defaults.VectorOne, string offsetMin = Defaults.VectorZero, string offsetMax = Defaults.VectorZero, string name = null)
                    => Add(ElementContructor.CreateContainer(anchorMin, anchorMax, offsetMin, offsetMax, name));

                public CUI.Element WithRect(string anchorMin = Defaults.VectorZero, string anchorMax = Defaults.VectorOne, string offsetMin = Defaults.VectorZero, string offsetMax = Defaults.VectorZero)
                {
                    if (this.Components.Count > 0) this.Components.RemoveAll(c => c is CuiRectTransformComponent);
                    this.Components.Add(new CuiRectTransformComponent() { AnchorMin = anchorMin, AnchorMax = anchorMax, OffsetMin = offsetMin, OffsetMax = offsetMax });
                    return this;
                }

                public CUI.Element WithFade(float @in = 0f, float @out = 0f)
                {
                    this.FadeOut = @out;
                    foreach (ICuiComponent component in this.Components)
                    {
                        if (component is CuiRawImageComponent rawImage) rawImage.FadeIn = @in;
                        else if (component is CuiImageComponent image) image.FadeIn = @in;
                        else if (component is CuiButtonComponent button) button.FadeIn = @in;
                        else if (component is CuiTextComponent text) text.FadeIn = @in;
                        else if (component is CuiCountdownComponent countdown) countdown.FadeIn = @in;
                    }
                    return this;
                }

                public void AddComponents(params ICuiComponent[] components) => this.Components.AddRange(components);
                public CUI.Element WithComponents(params ICuiComponent[] components) { AddComponents(components); return this; }
                public static CUI.Element Create(string name = null, params ICuiComponent[] components) => new CUI.Element() { Name = name }.WithComponents(components);

                public class ComponentList : List<ICuiComponent>
                {
                    private Dictionary<Type, ICuiComponent> typeToComponent = new Dictionary<Type, ICuiComponent>();
                    public T Get<T>() where T : ICuiComponent
                    {
                        if (typeToComponent.TryGetValue(typeof(T), out ICuiComponent component)) return (T)component;
                        return default(T);
                    }

                    public new void Add(ICuiComponent item) { base.Add(item); typeToComponent.Add(item.GetType(), item); }
                    public new void Remove(ICuiComponent item) { base.Remove(item); typeToComponent.Remove(item.GetType()); }
                    public new void Clear() { base.Clear(); typeToComponent.Clear(); }

                    public ComponentList AddImage(string color = Defaults.Color, string sprite = Defaults.Sprite, string material = Defaults.Material, Image.Type imageType = Defaults.ImageType, int itemId = 0, ulong skinId = 0UL)
                    {
                        Add(new CuiImageComponent { Color = color, Sprite = sprite, Material = material, ImageType = imageType, ItemId = itemId, SkinId = skinId });
                        return this;
                    }

                    public ComponentList AddRawImage(string content, string color = Defaults.Color, string sprite = Defaults.Sprite, string material = Defaults.IconMaterial)
                    {
                        CuiRawImageComponent rawImageComponent = new CuiRawImageComponent { Color = color, Sprite = sprite, Material = material };
                        if (!string.IsNullOrEmpty(content))
                        {
                            if (content.Contains("://")) rawImageComponent.Url = content;
                            else if (content.IsNumeric())
                            {
                                if (content.IsSteamId()) rawImageComponent.SteamId = content;
                                else rawImageComponent.Png = content;
                            }
                        }
                        Add(rawImageComponent);
                        return this;
                    }

                    public ComponentList AddButton(string command = null, string close = null, string color = Defaults.Color, string sprite = Defaults.Sprite, string material = Defaults.Material, Image.Type imageType = Defaults.ImageType)
                    {
                        Add(new CuiButtonComponent { Command = command, Close = close, Color = color, Sprite = sprite, Material = material, ImageType = imageType });
                        return this;
                    }

                    public ComponentList AddText(string text, string color = Defaults.Color, CUI.Font font = Defaults.Font, int fontSize = Defaults.FontSize, TextAnchor align = Defaults.Align, VerticalWrapMode overflow = Defaults.VerticalOverflow)
                    {
                        Add(new CuiTextComponent { Text = text, Color = color, Font = FontToString[font], FontSize = fontSize, Align = align, VerticalOverflow = overflow });
                        return this;
                    }

                    public ComponentList AddInputfield(string command = null, string text = "", string color = Defaults.Color, CUI.Font font = Defaults.Font, int fontSize = Defaults.FontSize, TextAnchor align = Defaults.Align, InputField.LineType lineType = Defaults.LineType, CUI.InputType inputType = CUI.InputType.Default, bool @readonly = false, bool autoFocus = false, bool isPassword = false, int charsLimit = 0)
                    {
                        Add(new CuiInputFieldComponent { Command = command, Text = text, Color = color, Font = FontToString[font], FontSize = fontSize, Align = align, NeedsKeyboard = inputType == InputType.Default, HudMenuInput = inputType == InputType.HudMenuInput, Autofocus = autoFocus, ReadOnly = @readonly, CharsLimit = charsLimit, IsPassword = isPassword, LineType = lineType });
                        return this;
                    }

                    public ComponentList AddScrollView(bool horizontal = false, CuiScrollbar horizonalScrollbar = null, bool vertical = false, CuiScrollbar verticalScrollbar = null, bool inertia = false, ScrollRect.MovementType movementType = ScrollRect.MovementType.Clamped, float decelerationRate = 0.135f, float elasticity = 0.1f, float scrollSensitivity = 1f, string anchorMin = "0 0", string anchorMax = "1 1", string offsetMin = "0 0", string offsetMax = "0 0")
                    {
                        Add(new CuiScrollViewComponent() { ContentTransform = new CuiRectTransformComponent() { AnchorMin = anchorMin, AnchorMax = anchorMax, OffsetMin = offsetMin, OffsetMax = offsetMax }, Horizontal = horizontal, HorizontalScrollbar = horizonalScrollbar, Vertical = vertical, VerticalScrollbar = verticalScrollbar, Inertia = inertia, DecelerationRate = decelerationRate, Elasticity = elasticity, ScrollSensitivity = scrollSensitivity, MovementType = movementType });
                        return this;
                    }

                    public ComponentList AddOutline(string color = Defaults.OutlineColor, int width = 1)
                    {
                        Add(new CuiOutlineComponent { Color = color, Distance = string.Format("{0} -{0}", width) });
                        return this;
                    }

                    public ComponentList AddNeedsKeyboard() { Add(new CuiNeedsKeyboardComponent()); return this; }
                    public ComponentList AddNeedsCursor() { Add(new CuiNeedsCursorComponent()); return this; }
                }
            }

            public static class ElementContructor
            {
                public static CUI.Element CreateText(string text, string color = Defaults.Color, CUI.Font font = Defaults.Font, int fontSize = Defaults.FontSize, TextAnchor align = Defaults.Align, VerticalWrapMode overflow = Defaults.VerticalOverflow, string anchorMin = Defaults.VectorZero, string anchorMax = Defaults.VectorOne, string offsetMin = Defaults.VectorZero, string offsetMax = Defaults.VectorZero, string name = null)
                {
                    CUI.Element element = CreateContainer(anchorMin, anchorMax, offsetMin, offsetMax, name);
                    element.Components.AddText(text, color, font, fontSize, align, overflow);
                    return element;
                }

                public static CUI.Element CreateInputfield(string command = null, string text = "", string color = Defaults.Color, CUI.Font font = Defaults.Font, int fontSize = Defaults.FontSize, TextAnchor align = Defaults.Align, InputField.LineType lineType = Defaults.LineType, CUI.InputType inputType = CUI.InputType.Default, bool @readonly = false, bool autoFocus = false, bool isPassword = false, int charsLimit = 0, string anchorMin = Defaults.VectorZero, string anchorMax = Defaults.VectorOne, string offsetMin = Defaults.VectorZero, string offsetMax = Defaults.VectorZero, string name = null)
                {
                    CUI.Element element = CreateContainer(anchorMin, anchorMax, offsetMin, offsetMax, name);
                    element.Components.AddInputfield(command, text, color, font, fontSize, align, lineType, inputType, @readonly, autoFocus, isPassword, charsLimit);
                    return element;
                }

                public static CUI.Element CreateButton(string command = null, string close = null, string color = Defaults.Color, string sprite = Defaults.Sprite, string material = Defaults.Material, Image.Type imageType = Defaults.ImageType, string anchorMin = Defaults.VectorZero, string anchorMax = Defaults.VectorOne, string offsetMin = Defaults.VectorZero, string offsetMax = Defaults.VectorZero, string name = null)
                {
                    CUI.Element element = CreateContainer(anchorMin, anchorMax, offsetMin, offsetMax, name);
                    element.Components.AddButton(command, close, color, sprite, material, imageType);
                    return element;
                }

                public static CUI.Element CreatePanel(string color = Defaults.Color, string sprite = Defaults.Sprite, string material = Defaults.Material, Image.Type imageType = Defaults.ImageType, string anchorMin = Defaults.VectorZero, string anchorMax = Defaults.VectorOne, string offsetMin = Defaults.VectorZero, string offsetMax = Defaults.VectorZero, bool cursorEnabled = false, bool keyboardEnabled = false, string name = null)
                {
                    Element element = CreateContainer(anchorMin, anchorMax, offsetMin, offsetMax, name);
                    element.Components.AddImage(color, sprite, material, imageType);
                    if (cursorEnabled) element.Components.AddNeedsCursor();
                    if (keyboardEnabled) element.Components.AddNeedsKeyboard();
                    return element;
                }

                public static CUI.Element CreateImage(string content, string color = Defaults.Color, string material = null, string anchorMin = Defaults.VectorZero, string anchorMax = Defaults.VectorOne, string offsetMin = Defaults.VectorZero, string offsetMax = Defaults.VectorZero, string name = null)
                {
                    Element element = CreateContainer(anchorMin, anchorMax, offsetMin, offsetMax, name);
                    element.Components.AddRawImage(content, color, material: material);
                    return element;
                }

                public static CUI.Element CreateIcon(int itemId, ulong skin = 0, string color = Defaults.Color, string sprite = Defaults.Sprite, string material = Defaults.IconMaterial, Image.Type imageType = Defaults.ImageType, string anchorMin = Defaults.VectorZero, string anchorMax = Defaults.VectorOne, string offsetMin = Defaults.VectorZero, string offsetMax = Defaults.VectorZero, string name = null)
                {
                    Element element = CreateContainer(anchorMin, anchorMax, offsetMin, offsetMax, name);
                    element.Components.AddImage(color, sprite, material, imageType, itemId, skin);
                    return element;
                }

                public static Element CreateContainer(string anchorMin = Defaults.VectorZero, string anchorMax = Defaults.VectorOne, string offsetMin = Defaults.VectorZero, string offsetMax = Defaults.VectorZero, string name = null)
                {
                    return Element.Create(name).WithRect(anchorMin, anchorMax, offsetMin, offsetMax);
                }
            }

            public class Root : Element
            {
                public bool wasRendered = false;
                private static StringBuilder stringBuilder = new StringBuilder();
                
                public Root() { Name = string.Empty; }
                public Root(string rootObjectName = "Overlay") { Name = rootObjectName; }

                public override List<Element> Container { get; } = new List<Element>();

                public string ToJson(List<Element> elements)
                {
                    stringBuilder.Clear();
                    try
                    {
                        using (StringWriter stringWriter = new StringWriter(stringBuilder))
                        using (JsonWriter jsonWriter = new JsonTextWriter(stringWriter))
                        {
                            jsonWriter.WriteStartArray();
                            foreach (Element element in elements) element.WriteJson(jsonWriter);
                            jsonWriter.WriteEndArray();
                        }
                    }
                    catch (Exception ex) { UnityEngine.Debug.LogException(ex); }
                    return stringBuilder.ToString();
                }

                public void Render(Connection connection)
                {
                    if (wasRendered) return;
                    wasRendered = true;
                    string json = ToJson(Container);
                    AddUI(connection, json);
                }

                public void Update(Connection connection)
                {
                    foreach (Element element in Container) element.Update = true;
                    Render(connection);
                }
            }
        }
        #endregion

        #region Plugin Hooks & Commands
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Quick Menu"] = "Quick Menu",
                ["Players"] = "Players",
                ["Permissions"] = "Permissions",
                ["Plugins"] = "Plugins",
                ["Give"] = "Give",
                ["← Back"] = "← Back",
                ["ONLINE"] = "ONLINE",
                ["OFFLINE"] = "OFFLINE",
                ["BANNED"] = "BANNED",
                ["ADMINS"] = "ADMINS",
                ["MODERS"] = "MODERS",
                ["ALL"] = "ALL",
                ["INFO"] = "INFO",
                ["USERS"] = "USERS",
                ["Search players..."] = "Search players...",
                ["+ Create Group"] = "+ Create Group",
                ["Teleport to 0 0 0"] = "Teleport to 0 0 0",
                ["Teleport to\nDeathpoint"] = "Teleport to\nDeathpoint",
                ["Teleport to\nSpawn point"] = "Teleport to\nSpawn point",
                ["Kill Self"] = "Kill Self",
                ["Heal Self"] = "Heal Self",
                ["Time to 12"] = "Time to 12",
                ["Time to 0"] = "Time to 0",
                ["Giveaway\nto online"] = "Giveaway\nto online",
                ["Giveaway\nto everyone"] = "Giveaway\nto everyone",
                ["Call Heli"] = "Call Heli",
                ["Spawn Bradley"] = "Spawn Bradley",
                ["Spawn Cargo"] = "Spawn Cargo",
                ["Teleport Self To"] = "Teleport Self To",
                ["Teleport To Self"] = "Teleport To Self",
                ["Teleport To Auth"] = "Teleport To Auth",
                ["Heal"] = "Heal",
                ["Heal 50%"] = "Heal 50%",
                ["Spectate"] = "Spectate",
                ["View Inventory"] = "View Inventory",
                ["Unlock Blueprints"] = "Unlock Blueprints",
                ["Toggle Creative"] = "Toggle Creative",
                ["Mute"] = "Mute",
                ["Unmute"] = "Unmute",
                ["Cuff"] = "Cuff",
                ["Kill"] = "Kill",
                ["Kick"] = "Kick",
                ["Ban"] = "Ban",
                ["Remove Group"] = "Remove Group",
                ["Clone Group"] = "Clone Group",
            }, this);
        }

        private void Init()
        {
            permission.RegisterPermission(PERMISSION_USE, this);
            permission.RegisterPermission(PERMISSION_FULLACCESS, this);
            permission.RegisterPermission(PERMISSION_CONVARS, this);
            permission.RegisterPermission(PERMISSION_PERMISSIONMANAGER, this);
            permission.RegisterPermission(PERMISSION_PLUGINMANAGER, this);
            permission.RegisterPermission(PERMISSION_GIVE, this);
            permission.RegisterPermission(PERMISSION_USERINFO_IP, this);
            permission.RegisterPermission(PERMISSION_USERINFO_STEAMINFO, this);
            
            cmd.AddChatCommand(config.ChatCommand, this, nameof(adminmenu_chatcmd));
        }

        private void OnServerInitialized()
        {
            FormatPanelList();
            
            mainMenu = new MainMenu
            {
                NavButtons = new ButtonArray
                {
                    new HideButton("← Back", "back"),
                    new Button("Quick Menu", "openpanel", "quickmenu"),
                    new Button("Players", "openpanel", "playerlist"),
                    new Button("Permissions", "openpanel", "permissionmanager") { Permission = "permissionmanager" },
                    new Button("Plugins", "openpanel", "pluginmanager") { Permission = "pluginmanager" },
                    new Button("Give", "givemenu.open", "self") { Permission = "give" },
                }
            };
            
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                if (CanUseAdminMenu(player.Connection))
                {
                    ConnectionData.GetOrCreate(player.Connection).UI.RenderOverlayOpenButton();
                }
            }
        }

        private void Unload()
        {
            foreach (var pair in ConnectionData.all.ToList())
            {
                pair.Value.UI.DestroyAll();
                pair.Value.Dispose();
            }
            ConnectionData.all.Clear();
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (player == null) return;
            timer.Once(1f, () =>
            {
                if (player != null && player.IsConnected && CanUseAdminMenu(player.Connection))
                {
                    ConnectionData.GetOrCreate(player.Connection).UI.RenderOverlayOpenButton();
                }
            });
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
            if (player == null) return;
            ConnectionData data = ConnectionData.Get(player.Connection);
            if (data != null) data.Dispose();
        }

        private bool CanUseAdminMenu(Connection connection)
        {
            if (connection == null) return false;
            string userId = connection.userid.ToString();
            return permission.UserHasPermission(userId, PERMISSION_USE) || permission.UserHasPermission(userId, PERMISSION_FULLACCESS);
        }

        private bool UserHasPermission(string userId, string perm)
        {
            if (!perm.StartsWith("adminmenuenhanced.")) perm = $"adminmenuenhanced.{perm}";
            return permission.UserHasPermission(userId, PERMISSION_FULLACCESS) || permission.UserHasPermission(userId, perm);
        }

        private void adminmenu_chatcmd(BasePlayer player)
        {
            HandleCommand(player.Connection, "");
        }

        [ConsoleCommand("adminmenuenhanced")]
        private void adminmenu_cmd(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null) return;
            HandleCommand(player.Connection, arg.GetString(0), arg.Args?.Skip(1).ToArray());
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) LoadDefaultConfig();
                if (config.GiveItemPresets.Count == 0) config.GiveItemPresets.Add(Configuration.ItemPreset.Example);
                SaveConfig();
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogException(e);
                PrintWarning("Creating new config file.");
                LoadDefaultConfig();
            }
        }

        protected override void LoadDefaultConfig() => config = Configuration.DefaultConfig();
        protected override void SaveConfig() => Config.WriteObject(config);

        public enum ButtonHook { OFF, X, F }
        #endregion

        #region Command Handler
        private void HandleCommand(Connection connection, string command, params string[] args)
        {
            if (connection == null || !CanUseAdminMenu(connection)) return;
            if (args == null) args = new string[0];
            
            ConnectionData connectionData;
            switch (command)
            {
                case "":
                case "True":
                    connectionData = ConnectionData.GetOrCreate(connection);
                    if (!connectionData.IsAdminMenuDisplay)
                    {
                        connectionData.ShowAdminMenu();
                        connectionData.OpenPanel("quickmenu");
                    }
                    else
                    {
                        HandleCommand(connection, "close");
                    }
                    break;

                case "show":
                    connectionData = ConnectionData.GetOrCreate(connection);
                    connectionData.ShowAdminMenu();
                    break;

                case "close":
                    connectionData = ConnectionData.Get(connection);
                    if (connectionData != null)
                    {
                        foreach (Button navButton in connectionData.currentMainMenu.NavButtons)
                            if (navButton != null) navButton.SetState(connectionData, Button.State.Normal);
                        connectionData.HideAdminMenu();
                    }
                    break;

                case "openpanel":
                    ConnectionData.GetOrCreate(connection).OpenPanel(args[0]);
                    break;

                case "openinfopanel":
                    ConnectionData.GetOrCreate(connection).OpenPanel("info")?.ShowPanelContent(args[0]);
                    break;

                case "uipanel.sidebar.button_pressed":
                    connectionData = ConnectionData.Get(connection);
                    if (connectionData != null)
                    {
                        int buttonIndex = int.Parse(args[0]);
                        int buttonCount = int.Parse(args[1]);
                        UpdateSidebarSelection(connection, buttonIndex, buttonCount);
                        if (buttonIndex >= 0)
                        {
                            Button button = connectionData.currentSidebar.CategoryButtons.GetAllowedButtons(connection).ElementAt(buttonIndex);
                            if (button.UserHasPermission(connection))
                                HandleCommand(connection, button.Command, button.Args);
                        }
                    }
                    break;

                case "navigation.button_pressed":
                    connectionData = ConnectionData.Get(connection);
                    if (connectionData != null)
                    {
                        int buttonIndex2 = int.Parse(args[0]);
                        int buttonCount2 = int.Parse(args[1]);
                        IEnumerable<Button> navButtons = connectionData.currentMainMenu.NavButtons.GetAllowedButtons(connection);
                        if (buttonIndex2 != 0)
                        {
                            for (int i = 0; i < navButtons.Count(); i++)
                            {
                                Button navButton = navButtons.ElementAtOrDefault(i);
                                if (navButton == null) continue;
                                navButton.SetState(connectionData, i == buttonIndex2 ? Button.State.Pressed : Button.State.Normal);
                            }
                        }
                        if (buttonIndex2 >= 0)
                        {
                            Button navButton = navButtons.ElementAt(buttonIndex2);
                            if (navButton != null && navButton.UserHasPermission(connection))
                                HandleCommand(connection, navButton.Command, navButton.Args);
                        }
                        connectionData.UI.UpdateNavButtons(connectionData.currentMainMenu);
                    }
                    break;

                case "showcontent":
                    ConnectionData.GetOrCreate(connection).ShowPanelContent(args[0]);
                    break;

                case "back":
                    connectionData = ConnectionData.Get(connection);
                    if (connectionData != null)
                    {
                        string backcommand = (string)connectionData.userData["backcommand"];
                        if (backcommand != null)
                        {
                            string[] a = backcommand.Split(' ');
                            HandleCommand(connection, a[0], a.Skip(1).ToArray());
                            connectionData.userData["backcommand"] = null;
                        }
                    }
                    break;

                case "playerlist.opensearch":
                    connectionData = ConnectionData.Get(connection);
                    if (connectionData != null)
                        (connectionData.currentContent as PlayerListContent)?.OpenSearch(connection);
                    break;

                case "playerlist.search.input":
                    connectionData = ConnectionData.Get(connection);
                    if (connectionData != null)
                    {
                        string searchQuery = args.Length > 0 ? string.Join(" ", args) : string.Empty;
                        connectionData.userData["playerlist.searchQuery"] = searchQuery;
                        connectionData.currentContent.Render(connectionData);
                    }
                    break;

                case "playerlist.filter":
                    connectionData = ConnectionData.Get(connection);
                    if (connectionData != null)
                    {
                        Func<IPlayer, bool> filterFunc;
                        switch (args[0])
                        {
                            case "online": filterFunc = (IPlayer player) => player.IsConnected; break;
                            case "offline": filterFunc = (IPlayer player) => !player.IsConnected && BasePlayer.FindSleeping(player.Id); break;
                            case "banned": filterFunc = (IPlayer player) => player.IsBanned; break;
                            case "admins": filterFunc = (IPlayer player) => ServerUsers.Get(ulong.Parse(player.Id))?.group == ServerUsers.UserGroup.Owner; break;
                            case "moders": filterFunc = (IPlayer player) => ServerUsers.Get(ulong.Parse(player.Id))?.group == ServerUsers.UserGroup.Moderator; break;
                            default: filterFunc = (IPlayer player) => true; break;
                        }
                        connectionData.userData["playerlist.filter"] = filterFunc;
                        connectionData.currentContent.Render(connectionData);
                    }
                    break;

                case "userinfo.open":
                    connectionData = ConnectionData.Get(connection);
                    if (connectionData != null)
                    {
                        connectionData.userData["userinfo.userid"] = args[0];
                        connectionData.OpenPanel("userinfo");
                    }
                    break;

                case "quickmenu.action":
                    connectionData = ConnectionData.Get(connection);
                    if (connectionData != null)
                    {
                        BasePlayer admin = connection.player as BasePlayer;
                        if (admin == null) return;
                        
                        string action = args[0];
                        switch (action)
                        {
                            case "teleportto_000":
                                admin.Teleport(Vector3.zero);
                                break;
                            case "healself":
                                if (admin.IsWounded()) admin.StopWounded();
                                admin.Heal(admin.MaxHealth());
                                admin.metabolism.calories.value = admin.metabolism.calories.max;
                                admin.metabolism.hydration.value = admin.metabolism.hydration.max;
                                admin.metabolism.radiation_level.value = 0;
                                admin.metabolism.radiation_poison.value = 0;
                                break;
                            case "killself":
                                admin.DieInstantly();
                                break;
                            case "settime":
                                float time = float.Parse(args[1]);
                                ConVar.Env.time = time;
                                break;
                            case "helicall":
                                BaseEntity heliEntity = GameManager.server.CreateEntity("assets/prefabs/npc/patrol helicopter/patrolhelicopter.prefab", default(Vector3), default(Quaternion), true);
                                if (heliEntity)
                                {
                                    heliEntity.GetComponent<PatrolHelicopterAI>().SetInitialDestination(admin.transform.position + new Vector3(0f, 10f, 0f), 0.25f);
                                    heliEntity.Spawn();
                                }
                                break;
                            case "spawnbradley":
                                GameManager.server.CreateEntity("assets/prefabs/npc/m2bradley/bradleyapc.prefab", admin.CenterPoint(), default(Quaternion), true).Spawn();
                                break;
                        }
                    }
                    break;

                case "permissionmanager.select_group":
                    connectionData = ConnectionData.Get(connection);
                    if (connectionData != null)
                    {
                        connectionData.ShowPanelContent("default");
                        connectionData.userData["permissions.target_type"] = "group";
                        connectionData.userData["permissions.target"] = string.Join(" ", args);
                        connectionData.currentContent.Render(connectionData);
                    }
                    break;
            }
        }

        private void UpdateSidebarSelection(Connection connection, int activeIndex, int buttonCount)
        {
            CUI.Root root = new CUI.Root();
            for (int i = 0; i < buttonCount; i++)
            {
                // Update indicator color
                root.AddUpdateElement($"UIPanel_SideBar_Button{i}_Indicator").Components.AddImage(
                    color: i == activeIndex ? COLOR_ACCENT_PRIMARY : "0 0 0 0"
                );
            }
            root.Update(connection);
        }
        #endregion

        #region Panel List Setup
        void FormatPanelList()
        {
            Button.all.Clear();
            
            // Quick Menu Content with modern buttons
            QuickMenuContent quickMenuContent = new QuickMenuContent()
            {
                buttonGrid = new ButtonGrid<Button>()
                {
                    new ButtonGrid<Button>.Item(0, 0, new Button("Teleport to 0 0 0", "quickmenu.action", "teleportto_000") { Permission = "quickmenu.teleportto000", Style = ButtonStyle.Default }),
                    new ButtonGrid<Button>.Item(0, 1, new Button("Teleport to\nDeathpoint", "quickmenu.action", "teleportto_deathpoint") { Permission = "quickmenu.teleporttodeath", Style = ButtonStyle.Default }),
                    new ButtonGrid<Button>.Item(0, 2, new Button("Teleport to\nSpawn point", "quickmenu.action", "teleportto_randomspawnpoint") { Permission = "quickmenu.teleporttospawnpoint", Style = ButtonStyle.Default }),
                    new ButtonGrid<Button>.Item(1, 0, new Button("Kill Self", "quickmenu.action", "killself") { Style = ButtonStyle.Danger }),
                    new ButtonGrid<Button>.Item(1, 1, new Button("Heal Self", "quickmenu.action", "healself") { Permission = "quickmenu.healself", Style = ButtonStyle.Success }),
                    new ButtonGrid<Button>.Item(1, 2, new Button("Time to 12", "quickmenu.action", "settime", "12") { Permission = "quickmenu.settime", Style = ButtonStyle.Warning }),
                    new ButtonGrid<Button>.Item(2, 0, new Button("Giveaway\nto online", "quickmenu.action", "giveaway_online") { Permission = "quickmenu.giveaway", Style = ButtonStyle.Primary }),
                    new ButtonGrid<Button>.Item(2, 1, new Button("Giveaway\nto everyone", "quickmenu.action", "giveaway_everyone") { Permission = "quickmenu.giveaway", Style = ButtonStyle.Primary }),
                    new ButtonGrid<Button>.Item(2, 2, new Button("Time to 0", "quickmenu.action", "settime", "0") { Permission = "quickmenu.settime", Style = ButtonStyle.Warning }),
                    new ButtonGrid<Button>.Item(3, 0, new Button("Call Heli", "quickmenu.action", "helicall") { Permission = "quickmenu.helicall", Style = ButtonStyle.Default }),
                    new ButtonGrid<Button>.Item(3, 1, new Button("Spawn Bradley", "quickmenu.action", "spawnbradley") { Permission = "quickmenu.spawnbradley", Style = ButtonStyle.Default }),
                    new ButtonGrid<Button>.Item(3, 2, new Button("Spawn Cargo", "quickmenu.action", "spawncargo") { Permission = "quickmenu.spawncargo", Style = ButtonStyle.Default }),
                }
            };

            // User Info Content
            UserInfoContent userInfoContent = new UserInfoContent()
            {
                buttonGrid = new ButtonGrid<Button>()
                {
                    new ButtonGrid<Button>.Item(0, 0, new Button("Teleport Self To", "userinfo.action", "teleportselfto") { Permission = "userinfo.teleportselfto", Style = ButtonStyle.Default }),
                    new ButtonGrid<Button>.Item(0, 1, new Button("Teleport To Self", "userinfo.action", "teleporttoself") { Permission = "userinfo.teleporttoself", Style = ButtonStyle.Default }),
                    new ButtonGrid<Button>.Item(0, 2, new Button("Teleport To Auth", "userinfo.action", "teleporttoauth") { Permission = "userinfo.teleporttoauth", Style = ButtonStyle.Default }),
                    new ButtonGrid<Button>.Item(1, 0, new Button("Heal", "userinfo.action", "heal") { Permission = "userinfo.fullheal", Style = ButtonStyle.Success }),
                    new ButtonGrid<Button>.Item(1, 1, new Button("Heal 50%", "userinfo.action", "heal50") { Permission = "userinfo.halfheal", Style = ButtonStyle.Success }),
                    new ButtonGrid<Button>.Item(1, 2, new Button("Spectate", "userinfo.action", "spectate") { Permission = "userinfo.spectate", Style = ButtonStyle.Default }),
                    new ButtonGrid<Button>.Item(2, 0, new Button("View Inventory", "userinfo.action", "viewinv") { Permission = "userinfo.viewinv", Style = ButtonStyle.Default }),
                    new ButtonGrid<Button>.Item(2, 1, new Button("Unlock Blueprints", "userinfo.action", "unlockblueprints") { Permission = "userinfo.unlockblueprints", Style = ButtonStyle.Default }),
                    new ButtonGrid<Button>.Item(2, 2, new Button("Toggle Creative", "userinfo.action", "creative") { Permission = "userinfo.creative", Style = ButtonStyle.Warning }),
                    new ButtonGrid<Button>.Item(3, 0, new Button("Mute", "userinfo.action", "mute") { Permission = "userinfo.mute", Style = ButtonStyle.Warning }),
                    new ButtonGrid<Button>.Item(3, 1, new Button("Unmute", "userinfo.action", "unmute") { Permission = "userinfo.unmute", Style = ButtonStyle.Default }),
                    new ButtonGrid<Button>.Item(3, 2, new Button("Cuff", "userinfo.action", "cuff") { Permission = "userinfo.cuff", Style = ButtonStyle.Warning }),
                    new ButtonGrid<Button>.Item(4, 0, new Button("Kill", "userinfo.action", "kill") { Permission = "userinfo.kill", Style = ButtonStyle.Danger }),
                    new ButtonGrid<Button>.Item(4, 1, new Button("Kick", "userinfo.action", "kick", "showpopup") { Permission = "userinfo.kick", Style = ButtonStyle.Danger }),
                    new ButtonGrid<Button>.Item(4, 2, new Button("Ban", "userinfo.action", "ban", "showpopup") { Permission = "userinfo.ban", Style = ButtonStyle.Danger }),
                }
            };

            // Group Info Content
            GroupInfoContent groupInfoContent = new GroupInfoContent
            {
                buttons = new ButtonArray[1]
                {
                    new ButtonArray
                    {
                        new Button("Remove Group", "groupinfo.action", "remove") { Permission = "groupinfo.removegroup", Style = ButtonStyle.Danger },
                        new Button("Clone Group", "groupinfo[popup:clonegroup]", "show") { Permission = "groupinfo.clonegroup", Style = ButtonStyle.Warning },
                    }
                }
            };

            // Panel definitions
            panelList = new Dictionary<string, Panel>()
            {
                { "empty", new Panel { Sidebar = null, Content = null } },
                { "quickmenu", new Panel { Sidebar = null, Content = new Dictionary<string, Content>() { { "default", quickMenuContent } } } },
                { "permissionmanager", new Panel { Sidebar = null, Content = new Dictionary<string, Content> { { "default", new GroupListContent() }, { "groups", new GroupListContent() } } } },
                { "pluginmanager", new Panel { Sidebar = null, Content = new Dictionary<string, Content> { { "default", new CenteredTextContent() { text = "Plugin Manager\n\nComing soon..." } } } } },
                { "userinfo", new Panel
                    {
                        Sidebar = new Sidebar()
                        {
                            CategoryButtons = new ButtonArray<CategoryButton>
                            {
                                new CategoryButton("INFO", "showcontent", "info"),
                                new CategoryButton("GIVE", "userinfo.givemenu.open") { Permission = "give" },
                                new CategoryButton("PERMISSIONS", "userinfo.permissions") { Permission = "permissionmanager" }
                            }
                        },
                        Content = new Dictionary<string, Content>() { { "info", userInfoContent } }
                    }
                },
                { "groupinfo", new Panel
                    {
                        Sidebar = new Sidebar()
                        {
                            CategoryButtons = new ButtonArray<CategoryButton>
                            {
                                new CategoryButton("INFO", "showcontent", "info"),
                                new CategoryButton("USERS", "groupinfo.users.open"),
                                new CategoryButton("PERMISSIONS", "groupinfo.permissions") { Permission = "permissionmanager" }
                            }
                        },
                        Content = new Dictionary<string, Content>() { { "info", groupInfoContent } }
                    }
                },
                { "playerlist", new Panel
                    {
                        Sidebar = new Sidebar
                        {
                            CategoryButtons = new ButtonArray<CategoryButton>
                            {
                                new CategoryButton("ONLINE", "playerlist.filter", "online"),
                                new CategoryButton("OFFLINE", "playerlist.filter", "offline"),
                                new CategoryButton("BANNED", "playerlist.filter", "banned"),
                                new CategoryButton("ADMINS", "playerlist.filter", "admins"),
                                new CategoryButton("MODERS", "playerlist.filter", "moders"),
                                new CategoryButton("ALL", "playerlist.filter", "all"),
                            },
                        },
                        Content = new Dictionary<string, Content> { { "default", new PlayerListContent() } }
                    }
                },
            };
        }
        #endregion
    }
}
