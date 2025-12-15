using Oxide.Core;
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Oxide.Plugins
{
    [Info("Clans Converter", "Mevent", "1.0.0")]
    class ClansConverter : RustPlugin
    {
        #region Fields

        private Coroutine _actionConvert;

        [PluginReference]
        private Plugin Clans;
        
        #endregion Fields

        #region Hooks

        private void OnServerInitialized()
        {
            if (Clans == null)
            {
                PrintError("Clans plugin not found! ClansConverter requires Clans plugin to function.");
                return;
            }

            Puts("ClansConverter initialized successfully with Clans plugin reference.");
        }

        #endregion Hooks

        #region Clans Reborn

        private readonly DateTime _epoch = new(1970, 1, 1);

        private readonly double _maxUnixSeconds = (DateTime.MaxValue - new DateTime(1970, 1, 1)).TotalSeconds;

        [ConsoleCommand("clans.reborn.convert")]
        private void CmdConsoleConvertOldClans(ConsoleSystem.Arg arg)
        {
            if (!arg.IsServerside) return;

            SendReply(arg, "Converting clans...");
            _actionConvert = ServerMgr.Instance.StartCoroutine(ConvertClansReborn());
        }

        private IEnumerator ConvertClansReborn()
        {
            ClansReborn.StoredData oldClans = null;

            try
            {
                oldClans = Interface.Oxide.DataFileSystem.GetFile("Clans")?.ReadObject<ClansReborn.StoredData>();
            }
            catch (Exception e)
            {
                PrintError(e.ToString());
            }

            if (oldClans == null) yield break;

            var converted = 0;
            foreach (var check in oldClans.clans)
            {
                try
                {
                    var newClan = new JObject();
                    newClan["ClanTag"] = check.Key;
                    newClan["LeaderID"] = check.Value.OwnerID;
                    newClan["LeaderName"] = covalence.Players.FindPlayer(check.Value.OwnerID.ToString())?.Name;
                    newClan["Avatar"] = string.Empty;
                    newClan["Members"] = new JArray(check.Value.ClanMembers.Keys.ToList());
                    newClan["Moderators"] = new JArray(check.Value.ClanMembers.Where(x => x.Value.Role == ClansReborn.MemberRole.Moderator).Select(x => x.Key).ToList());
                    newClan["Top"] = converted + 1;
                    newClan["CreationTime"] = ConvertTimeReborn(check.Value.CreationTime);
                    newClan["LastOnlineTime"] = ConvertTimeReborn(check.Value.LastOnlineTime);

                    converted++;

                    Clans?.Call("API_CreateClan", newClan);
                }
                catch (Exception e)
                {
                    PrintError($"Error converting clan {check.Key}: {e.Message}");
                    continue;
                }
                
                if (converted % 100 == 0)
                {
                    yield return null;
                }
            }

            yield return CoroutineEx.waitForFixedUpdate;

            Puts($"{oldClans.clans.Count} clans was converted!");
        }

        private DateTime ConvertTimeReborn(double lastTime)
        {
            return lastTime > _maxUnixSeconds
                ? _epoch.AddMilliseconds(lastTime)
                : _epoch.AddSeconds(lastTime);
        }

        private static class ClansReborn
        {
            public class StoredData
            {
                public Hash<string, Clan> clans = new();

                public int timeSaved;

                public Hash<ulong, List<string>> playerInvites = new();
            }

            public class Clan
            {
                public string Tag;

                public string Description;

                public ulong OwnerID;

                public double CreationTime;

                public double LastOnlineTime;

                public Hash<ulong, Member> ClanMembers = new();

                public HashSet<string> Alliances = new();

                public Hash<string, double> AllianceInvites = new();

                public HashSet<string> IncomingAlliances = new();

                public string TagColor = string.Empty;
            }

            public class Member
            {
                public string DisplayName = string.Empty;

                public MemberRole Role;

                public bool MemberFFEnabled;

                public bool AllyFFEnabled;
            }

            public enum MemberRole
            {
                Owner,
                Council,
                Moderator,
                Member
            }
        }

        #endregion

        #region Clans (uMod)

        [ConsoleCommand("clans.umod.convert")]
        private void CmdConsoleConvertOldClansUMod(ConsoleSystem.Arg arg)
        {
            if (!arg.IsServerside) return;

            SendReply(arg, "Converting clans...");
            _actionConvert = ServerMgr.Instance.StartCoroutine(ConvertClansUMod());
        }

        private IEnumerator ConvertClansUMod()
        {
            if (!Interface.Oxide.DataFileSystem.ExistsDatafile("clan_data"))
            {
                PrintError("Clans plugin data from uMod not found!");
                yield break;
            }

            ClansUmod.StoredData oldClans = null;

            try
            {
                oldClans = Interface.Oxide.DataFileSystem.GetFile("clan_data")?.ReadObject<ClansUmod.StoredData>();
            }
            catch (Exception e)
            {
                PrintError(e.ToString());
            }

            if (oldClans == null) yield break;

            var converted = 0;
            foreach (var (clanTag, clanData) in oldClans.clans)
            {
                try
                {
                    var newClan = new JObject();
                    newClan["ClanTag"] = clanTag;
                    newClan["LeaderID"] = Convert.ToUInt64(clanData.OwnerID);
                    newClan["LeaderName"] = covalence.Players.FindPlayer(clanData.OwnerID)?.Name ?? string.Empty;
                    newClan["Avatar"] = string.Empty;
                    newClan["Members"] = new JArray(clanData.ClanMembers.Keys.ToList());
                    newClan["Moderators"] = new JArray(clanData.ClanMembers.Where(x => x.Value.Role == ClansUmod.Member.MemberRole.Moderator).Select(x => Convert.ToUInt64(x.Key)).ToList());
                    newClan["Top"] = converted + 1;
                    newClan["CreationTime"] = ConvertTimeReborn(clanData.CreationTime);
                    newClan["LastOnlineTime"] = ConvertTimeReborn(clanData.LastOnlineTime);

                    converted++;

                    Clans?.Call("API_CreateClan", newClan);
                }
                catch (Exception e)
                {
                    PrintError($"Error converting clan {clanTag}: {e.Message}");
                    continue;
                }
                
                if (converted % 100 == 0)
                {
                    yield return null;
                }
            }

            yield return CoroutineEx.waitForFixedUpdate;

            Puts($"{oldClans.clans.Count} clans was converted!");
        }

        private static class ClansUmod
        {
            public class StoredData
            {
                public Hash<string, Clan> clans = new();

                public Hash<string, List<string>> playerInvites = new();
            }

            public class Clan
            {
                public string Tag;

                public string Description;

                public string OwnerID;

                public double CreationTime;

                public double LastOnlineTime;

                public Hash<string, Member> ClanMembers = new();

                public Hash<string, MemberInvite> MemberInvites = new();

                public HashSet<string> Alliances = new();

                public Hash<string, double> AllianceInvites = new();

                public HashSet<string> IncomingAlliances = new();

                public string TagColor;
            }

            public class Member
            {
                public string Name;

                public MemberRole Role;

                public enum MemberRole
                {
                    Owner,
                    Moderator,
                    Member
                }
            }

            public class MemberInvite
            {
                public string Name;

                public double ExpiryTime;
            }
        }

        #endregion

        #region Data 2.0

        [ConsoleCommand("clans.convert.olddata")]
        private void CmdConvertOldData(ConsoleSystem.Arg arg)
        {
            if (!arg.IsServerside) return;

            SendReply(arg, "Converting players...");
            StartConvertOldData();
        }

        private void StartConvertOldData()
        {
            var data = LoadOldData();
            if (data != null)
                timer.In(0.3f, () =>
                {
                    CondertOldData(data);

                    PrintWarning($"{data.Count} players was converted!");
                });
        }

        private Dictionary<ulong, OldData> LoadOldData()
        {
            Dictionary<ulong, OldData> players = null;
            try
            {
                players = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, OldData>>($"{Name}/PlayersList");
            }
            catch (Exception e)
            {
                PrintError(e.ToString());
            }

            return players ?? new Dictionary<ulong, OldData>();
        }

        private void CondertOldData(Dictionary<ulong, OldData> players)
        {
            foreach (var (userId, data) in players)
            {
                var newPlayer = new JObject();
                newPlayer["SteamID"] = userId;
                newPlayer["DisplayName"] = data.DisplayName;
                newPlayer["LastLogin"] = data.LastLogin;
                newPlayer["FriendlyFire"] = data.FriendlyFire;
                newPlayer["AllyFriendlyFire"] = data.AllyFriendlyFire;
                newPlayer["ClanSkins"] = data.ClanSkins;
                newPlayer["Stats"] = new JArray(data.Stats.ToList());

                Clans?.Call("API_CreatePlayer", userId, newPlayer);
            }
        }

        private class OldData
        {
            [JsonProperty(PropertyName = "Display Name")]
            public string DisplayName;

            [JsonProperty(PropertyName = "Last Login")]
            public DateTime LastLogin;

            [JsonProperty(PropertyName = "Friendly Fire")]
            public bool FriendlyFire;

            [JsonProperty(PropertyName = "Ally Friendly Fire")]
            public bool AllyFriendlyFire;

            [JsonProperty(PropertyName = "Use Clan Skins")]
            public bool ClanSkins;

            [JsonProperty(PropertyName = "Stats", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, float> Stats = new();

            [JsonProperty(PropertyName = "Invites", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, OldInviteData> Invites = new();
        }

        private class OldInviteData
        {
            [JsonProperty(PropertyName = "Inviter Name")]
            public string InviterName;

            [JsonProperty(PropertyName = "Inviter Id")]
            public ulong InviterId;
        }

        #endregion
    }
}
