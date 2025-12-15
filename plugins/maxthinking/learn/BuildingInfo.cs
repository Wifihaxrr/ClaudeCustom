using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Rust;
using UnityEngine;

namespace Oxide.Plugins;

[UsedImplicitly]
[Info("Building Info", "misticos", "1.0.8")]
[Description("Scan buildings and get their owners")]
internal sealed class BuildingInfo : RustPlugin
{
    #region Variables

    private const string PermScan = "buildinginfo.scan";
    private const string PermOwner = "buildinginfo.owner";
    private const string PermAuthed = "buildinginfo.authed";
    private const string PermBypass = "buildinginfo.bypass";

    #endregion

    #region Configuration

    private Configuration _config = new();

    [UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.Members)]
    public class Configuration
    {
        [JsonProperty(PropertyName = "Command Scan")]
        public string CommandScan { get; set; } = "scan";

        [JsonProperty(PropertyName = "Command Scan Owner")]
        public string CommandOwner { get; set; } = "owner";

        [JsonProperty(PropertyName = "Command Scan Authorized Players")]
        public string CommandAuthed { get; set; } = "authed";
    }

    protected override void LoadConfig()
    {
        base.LoadConfig();
        try
        {
            _config = Config.ReadObject<Configuration>();
            if (_config == null) throw new Exception();
        }
        catch
        {
            PrintError("Your configuration file contains an error. Using default configuration values.");
            LoadDefaultConfig();
        }
    }

    protected override void LoadDefaultConfig() => _config = new Configuration();

    protected override void SaveConfig() => Config.WriteObject(_config);

    #endregion

    #region Commands

    private void CommandChatScan(BasePlayer player, string command, string[] args)
    {
        var id = player.UserIDString;
        if (!permission.UserHasPermission(id, PermScan))
        {
            player.ChatMessage(GetMsg("No Permissions", id));
            return;
        }

        var entity = GetBuilding(player);
        if (entity == null)
        {
            player.ChatMessage(GetMsg("Cannot Find", id));
            return;
        }

        var owner = BasePlayer.FindByID(entity.OwnerID);
        if (owner != null && permission.UserHasPermission(owner.UserIDString, PermBypass))
        {
            player.ChatMessage(GetMsg("Scan Unavailable", id));
            return;
        }

        var entities = entity.GetBuildingPrivilege()?.GetBuilding()?.buildingBlocks;
        if (entities == null || entities.Count == 0)
        {
            player.ChatMessage(GetMsg("Cannot Find", id));
            return;
        }

        var dict = new Dictionary<string, int>();
        var entitiesCount = entities.Count;
        for (var i = 0; i < entitiesCount; i++)
        {
            var ent = entities[i];
            if (permission.UserHasPermission(ent.OwnerID.ToString(), PermBypass))
                continue;

            var shortname = ent.ShortPrefabName + $" ({ent.currentGrade.gradeBase.type})";
            dict[shortname] = dict.GetValueOrDefault(shortname) + 1;
        }

        var ex = GetMsg("Scan Info", id);
        var builder = new StringBuilder(GetMsg("Scan Title", id));
        foreach (var el in dict)
        {
            builder.Append(ex);
            builder = builder.Replace("{name}", el.Key).Replace("{amount}", el.Value.ToString());
        }

        player.ChatMessage(builder.ToString());
    }

    private void CommandChatOwner(BasePlayer player, string command, string[] args)
    {
        var id = player.UserIDString;
        if (!permission.UserHasPermission(id, PermOwner))
        {
            player.ChatMessage(GetMsg("No Permissions", id));
            return;
        }

        var entity = GetBuilding(player);
        if (entity == null)
        {
            player.ChatMessage(GetMsg("Cannot Find", id));
            return;
        }

        var owner = covalence.Players.FindPlayerById(entity.OwnerID.ToString());
        if (owner == null)
        {
            player.ChatMessage(GetMsg("Cannot Find Owner", id));
            return;
        }

        if (permission.UserHasPermission(owner.Id, PermBypass))
        {
            player.ChatMessage(GetMsg("Owner Unavailable", id));
            return;
        }

        player.ChatMessage(GetMsg("Owner Info", id).Replace("{name}", owner.Name)
            .Replace("{id}", owner.Id));
    }

    private void CommandChatAuthed(BasePlayer player, string command, string[] args)
    {
        var id = player.UserIDString;
        if (!permission.UserHasPermission(id, PermAuthed))
        {
            player.ChatMessage(GetMsg("No Permissions", id));
            return;
        }

        var entity = GetBuilding(player);
        if (entity == null)
        {
            player.ChatMessage(GetMsg("Cannot Find", id));
            return;
        }

        var privilege = entity.GetBuildingPrivilege();
        if (privilege == null)
        {
            player.ChatMessage(GetMsg("Cannot Find Authed", id));
            return;
        }

        if (!privilege.AnyAuthed())
        {
            player.ChatMessage(GetMsg("Authed Zero", id));
            return;
        }

        var ex = GetMsg("Authed Info", id);
        var builder = new StringBuilder(GetMsg("Authed Title", id));
        var i = 0;
        foreach (var authorizedId in privilege.authorizedPlayers)
        {
            var authorizedName = ServerMgr.Instance.persistance.GetPlayerName(authorizedId);

            builder.Append(ex);
            builder = builder
                .Replace("{number}", $"{++i}")
                .Replace("{name}", authorizedName)
                .Replace("{id}", authorizedId.ToString());
        }

        player.ChatMessage(builder.ToString());
    }

    #endregion

    #region Hooks

    protected override void LoadDefaultMessages()
    {
        lang.RegisterMessages(new Dictionary<string, string>
        {
            { "No Permissions", "You don't have enough permissions." },
            { "Scan Title", "Scan result:" },
            { "Scan Info", "\n{name} x{amount}" },
            { "Scan Unavailable", "Sorry, there was an error. You cannot scan this building." },
            { "Owner Info", "Owner: {name} ({id})" },
            { "Owner Unavailable", "Sorry, there was an error. You cannot get an owner of this building." },
            { "Authed Title", "Authed Players:" },
            { "Authed Info", "\n#{number} - {name} ({id})" },
            { "Authed Unavailable", "" },
            { "Authed Zero", "Nobody is authed here." },
            { "Cannot Find", "Excuse me, where is the building you are looking for?" },
            { "Cannot Find Owner", "Sorry, I don't know who owns this building." },
            { "Cannot Find Authed", "I don't know who is authed there." }
        }, this);
    }

    // ReSharper disable once UnusedMember.Local
    private void Init()
    {
        LoadConfig();

        permission.RegisterPermission(PermScan, this);
        permission.RegisterPermission(PermOwner, this);
        permission.RegisterPermission(PermAuthed, this);
        permission.RegisterPermission(PermBypass, this);

        cmd.AddChatCommand(_config.CommandScan, this, CommandChatScan);
        cmd.AddChatCommand(_config.CommandOwner, this, CommandChatOwner);
        cmd.AddChatCommand(_config.CommandAuthed, this, CommandChatAuthed);
    }

    #endregion

    #region Helpers

    private string GetMsg(string key, string userId = null) => lang.GetMessage(key, this, userId);

    private static BaseEntity GetBuilding(BasePlayer player)
    {
        if (!Physics.Raycast(
                player.eyes.HeadRay(),
                out var info,
                float.PositiveInfinity,
                Layers.Construction
            ))
        {
            return null;
        }

        return info.GetEntity();
    }

    #endregion
}