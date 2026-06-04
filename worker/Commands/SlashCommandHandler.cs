using NetCord;
using NetCord.Gateway;
using NetCord.Rest;
using CordCastWorker.Models;

namespace CordCastWorker.Commands;

public static class SlashCommandHandler
{
    public static SlashCommandProperties[] BuildCommands() =>
    [
        new SlashCommandProperties("join", "Join a voice or stage channel")
            .WithOptions([
                new ApplicationCommandOptionProperties(ApplicationCommandOptionType.Channel, "channel", "Channel to join"),
            ]),

        new SlashCommandProperties("leave", "Disconnect from current voice channel"),

        new SlashCommandProperties("leave-all", "Disconnect from all voice channels across all servers"),

        new SlashCommandProperties("autojoin", "Configure auto-join channel")
            .WithOptions([
                new ApplicationCommandOptionProperties(ApplicationCommandOptionType.SubCommand, "set", "Set auto-join channel")
                    .WithOptions([
                        new ApplicationCommandOptionProperties(ApplicationCommandOptionType.Channel, "channel", "Channel")
                            .WithRequired(true),
                    ]),
                new ApplicationCommandOptionProperties(ApplicationCommandOptionType.SubCommand, "clear", "Clear auto-join"),
                new ApplicationCommandOptionProperties(ApplicationCommandOptionType.SubCommand, "show", "Show current auto-join channel"),
            ]),

        new SlashCommandProperties("follow-audio", "Follow a user between voice channels")
            .WithOptions([
                new ApplicationCommandOptionProperties(ApplicationCommandOptionType.SubCommand, "set", "Set user to follow")
                    .WithOptions([
                        new ApplicationCommandOptionProperties(ApplicationCommandOptionType.User, "user", "User")
                            .WithRequired(true),
                    ]),
                new ApplicationCommandOptionProperties(ApplicationCommandOptionType.SubCommand, "clear", "Stop following"),
                new ApplicationCommandOptionProperties(ApplicationCommandOptionType.SubCommand, "show", "Show followed user"),
            ]),

        new SlashCommandProperties("bind", "Restrict commands to specific channels")
            .WithOptions([
                new ApplicationCommandOptionProperties(ApplicationCommandOptionType.SubCommand, "add", "Add a command channel")
                    .WithOptions([
                        new ApplicationCommandOptionProperties(ApplicationCommandOptionType.Channel, "channel", "Channel")
                            .WithRequired(true),
                    ]),
                new ApplicationCommandOptionProperties(ApplicationCommandOptionType.SubCommand, "remove", "Remove a command channel")
                    .WithOptions([
                        new ApplicationCommandOptionProperties(ApplicationCommandOptionType.Channel, "channel", "Channel")
                            .WithRequired(true),
                    ]),
                new ApplicationCommandOptionProperties(ApplicationCommandOptionType.SubCommand, "clear", "Allow commands in all channels"),
                new ApplicationCommandOptionProperties(ApplicationCommandOptionType.SubCommand, "show", "Show bound channels"),
            ]),

        new SlashCommandProperties("status", "Set bot online status")
            .WithOptions([
                new ApplicationCommandOptionProperties(ApplicationCommandOptionType.String, "status", "online|idle|dnd|inv")
                    .WithRequired(true)
                    .WithChoices([
                        new ApplicationCommandOptionChoiceProperties("online", "online"),
                        new ApplicationCommandOptionChoiceProperties("idle", "idle"),
                        new ApplicationCommandOptionChoiceProperties("dnd", "dnd"),
                        new ApplicationCommandOptionChoiceProperties("invisible", "inv"),
                    ]),
            ]),

        new SlashCommandProperties("activity", "Set bot activity/presence")
            .WithOptions([
                new ApplicationCommandOptionProperties(ApplicationCommandOptionType.String, "type", "playing|streaming|listening|watching|competing")
                    .WithRequired(true),
                new ApplicationCommandOptionProperties(ApplicationCommandOptionType.String, "description", "Activity description")
                    .WithRequired(true),
                new ApplicationCommandOptionProperties(ApplicationCommandOptionType.String, "url", "Stream URL (streaming only)"),
            ]),

        new SlashCommandProperties("stage", "Manage stage channels")
            .WithOptions([
                new ApplicationCommandOptionProperties(ApplicationCommandOptionType.SubCommand, "start", "Start a stage instance")
                    .WithOptions([
                        new ApplicationCommandOptionProperties(ApplicationCommandOptionType.String, "topic", "Stage topic"),
                    ]),
                new ApplicationCommandOptionProperties(ApplicationCommandOptionType.SubCommand, "end", "End the stage instance"),
                new ApplicationCommandOptionProperties(ApplicationCommandOptionType.SubCommand, "speak", "Request to speak"),
                new ApplicationCommandOptionProperties(ApplicationCommandOptionType.SubCommand, "withdraw", "Withdraw speak request"),
                new ApplicationCommandOptionProperties(ApplicationCommandOptionType.SubCommand, "topic", "Update stage topic")
                    .WithOptions([
                        new ApplicationCommandOptionProperties(ApplicationCommandOptionType.String, "value", "New topic")
                            .WithRequired(true),
                    ]),
            ]),

        new SlashCommandProperties("about", "About cordcast")
            .WithOptions([
                new ApplicationCommandOptionProperties(ApplicationCommandOptionType.Boolean, "public", "Send publicly?"),
            ]),

        new SlashCommandProperties("invite", "Get bot invite link")
            .WithOptions([
                new ApplicationCommandOptionProperties(ApplicationCommandOptionType.Boolean, "public", "Send publicly?"),
            ]),

        new SlashCommandProperties("stop", "Disconnect bot from Discord (keeps app running)"),

        new SlashCommandProperties("leave-guild", "Remove bot from this server"),
    ];

    public static async Task HandleAsync(SlashCommandInteraction cmd, BotService bot, Config config)
    {
        if (cmd.GuildId.HasValue)
        {
            var guildCfg = config.GetGuildConfig(cmd.GuildId.Value.ToString());
            if (!guildCfg.IsCommandAllowed(cmd.Channel.Id.ToString()))
            {
                await Respond(cmd, "Commands are restricted to specific channels.");
                return;
            }
        }

        try
        {
            await (cmd.Data.Name switch
            {
                "join" => HandleJoin(cmd, bot),
                "leave" => HandleLeave(cmd, bot),
                "leave-all" => HandleLeaveAll(cmd, bot),
                "autojoin" => HandleAutoJoin(cmd, bot, config),
                "follow-audio" => HandleFollowAudio(cmd, bot, config),
                "bind" => HandleBind(cmd, bot, config),
                "status" => HandleStatus(cmd, bot),
                "activity" => HandleActivity(cmd, bot),
                "stage" => HandleStage(cmd, bot),
                "about" => HandleAbout(cmd),
                "invite" => HandleInvite(cmd, bot),
                "stop" => HandleStop(cmd, bot),
                "leave-guild" => HandleLeaveGuild(cmd, bot),
                _ => Respond(cmd, "Unknown command."),
            });
        }
        catch (Exception ex)
        {
            Ipc.Emit("error", new Dictionary<string, object?> { ["message"] = $"/{cmd.Data.Name}: {ex.Message}" });
            try { await Respond(cmd, $"Error: {ex.Message}"); } catch { }
        }
    }

    private static Task Respond(SlashCommandInteraction cmd, string text, bool ephemeral = true) =>
        cmd.SendResponseAsync(InteractionCallback.Message(
            new InteractionMessageProperties()
                .WithContent(text)
                .WithFlags(ephemeral ? MessageFlags.Ephemeral : null)));

    private static async Task HandleJoin(SlashCommandInteraction cmd, BotService bot)
    {
        if (!cmd.GuildId.HasValue) { await Respond(cmd, "Must be used in a server."); return; }
        if (!bot.Client!.Cache.Guilds.TryGetValue(cmd.GuildId.Value, out var guild)) { await Respond(cmd, "Guild not found."); return; }

        VoiceGuildChannel? target = null;
        var channelOpt = cmd.Data.Options.FirstOrDefault(o => o.Name == "channel");
        if (channelOpt is not null)
        {
            if (ulong.TryParse(channelOpt.Value, out var chId))
                target = guild.Channels.GetValueOrDefault(chId) as VoiceGuildChannel;
        }
        else
        {
            var userVs = guild.VoiceStates.GetValueOrDefault(cmd.User.Id);
            if (userVs?.ChannelId is ulong vsChId)
                target = guild.Channels.GetValueOrDefault(vsChId) as VoiceGuildChannel;
        }

        if (target is null) { await Respond(cmd, "Specify a channel or join one first."); return; }

        await cmd.SendResponseAsync(InteractionCallback.DeferredMessage(MessageFlags.Ephemeral));
        try
        {
            await bot.JoinChannelAsync(guild, target.Id);
            bot.EmitGuildsUpdated();
            await bot.Client!.Rest.SendInteractionFollowupMessageAsync(cmd.ApplicationId, cmd.Token,
                new InteractionMessageProperties().WithContent($"Joined **{target.Name}**.").WithFlags(MessageFlags.Ephemeral));
        }
        catch (Exception ex)
        {
            Ipc.Emit("error", new Dictionary<string, object?> { ["message"] = $"/join: {ex.Message}" });
            try
            {
                await bot.Client!.Rest.SendInteractionFollowupMessageAsync(cmd.ApplicationId, cmd.Token,
                    new InteractionMessageProperties().WithContent($"Error: {ex.Message}").WithFlags(MessageFlags.Ephemeral));
            }
            catch { }
        }
    }

    private static async Task HandleLeave(SlashCommandInteraction cmd, BotService bot)
    {
        if (!cmd.GuildId.HasValue) { await Respond(cmd, "Must be used in a server."); return; }
        await bot.LeaveGuildAudioAsync(cmd.GuildId.Value);
        bot.EmitGuildsUpdated();
        await Respond(cmd, "Left voice channel.");
    }

    private static async Task HandleLeaveAll(SlashCommandInteraction cmd, BotService bot)
    {
        await bot.LeaveAllAsync();
        bot.EmitGuildsUpdated();
        await Respond(cmd, "Left all voice channels.");
    }

    private static async Task HandleAutoJoin(SlashCommandInteraction cmd, BotService bot, Config config)
    {
        if (!cmd.GuildId.HasValue) { await Respond(cmd, "Must be used in a server."); return; }
        var guildCfg = config.GetGuildConfig(cmd.GuildId.Value.ToString());
        var sub = cmd.Data.Options[0];

        switch (sub.Name)
        {
            case "set":
                var chIdStr = sub.Options![0].Value!;
                guildCfg.AutoJoinAudioChannelId = chIdStr;
                bot.UpdateGuildConfig(guildCfg.GuildId, guildCfg);
                await Respond(cmd, $"Auto-join set to <#{chIdStr}>.");
                break;
            case "clear":
                guildCfg.AutoJoinAudioChannelId = null;
                bot.UpdateGuildConfig(guildCfg.GuildId, guildCfg);
                await Respond(cmd, "Auto-join cleared.");
                break;
            case "show":
                var id = guildCfg.AutoJoinAudioChannelId;
                await Respond(cmd, id is null ? "No auto-join set." : $"Auto-join: <#{id}>");
                break;
        }
    }

    private static async Task HandleFollowAudio(SlashCommandInteraction cmd, BotService bot, Config config)
    {
        if (!cmd.GuildId.HasValue) { await Respond(cmd, "Must be used in a server."); return; }
        var guildCfg = config.GetGuildConfig(cmd.GuildId.Value.ToString());
        var sub = cmd.Data.Options[0];

        switch (sub.Name)
        {
            case "set":
                var userId = sub.Options![0].Value!;
                guildCfg.FollowedUserId = userId;
                bot.UpdateGuildConfig(guildCfg.GuildId, guildCfg);
                await Respond(cmd, $"Now following <@{userId}>.");
                break;
            case "clear":
                guildCfg.FollowedUserId = null;
                bot.UpdateGuildConfig(guildCfg.GuildId, guildCfg);
                await Respond(cmd, "Follow cleared.");
                break;
            case "show":
                var uid = guildCfg.FollowedUserId;
                await Respond(cmd, uid is null ? "Not following anyone." : $"Following: <@{uid}>");
                break;
        }
    }

    private static async Task HandleBind(SlashCommandInteraction cmd, BotService bot, Config config)
    {
        if (!cmd.GuildId.HasValue) { await Respond(cmd, "Must be used in a server."); return; }
        var guildCfg = config.GetGuildConfig(cmd.GuildId.Value.ToString());
        var sub = cmd.Data.Options[0];

        switch (sub.Name)
        {
            case "add":
                var chId = sub.Options![0].Value!;
                if (chId is not null) guildCfg.CommandChannelIds.Add(chId);
                bot.UpdateGuildConfig(guildCfg.GuildId, guildCfg);
                await Respond(cmd, $"Bound to <#{chId}>.");
                break;
            case "remove":
                var rchId = sub.Options![0].Value!;
                if (rchId is not null) guildCfg.CommandChannelIds.Remove(rchId);
                bot.UpdateGuildConfig(guildCfg.GuildId, guildCfg);
                await Respond(cmd, $"Removed <#{rchId}> from bindings.");
                break;
            case "clear":
                guildCfg.CommandChannelIds.Clear();
                bot.UpdateGuildConfig(guildCfg.GuildId, guildCfg);
                await Respond(cmd, "All channel bindings cleared.");
                break;
            case "show":
                var ids = guildCfg.CommandChannelIds;
                var msg = ids.Count == 0
                    ? "All channels (no restriction)."
                    : string.Join(", ", ids.Select(id => $"<#{id}>"));
                await Respond(cmd, msg);
                break;
        }
    }

    private static async Task HandleStatus(SlashCommandInteraction cmd, BotService bot)
    {
        var val = cmd.Data.Options[0].Value!;
        UserStatusType status = val switch
        {
            "online" => UserStatusType.Online,
            "idle" => UserStatusType.Idle,
            "dnd" => UserStatusType.DoNotDisturb,
            "inv" => UserStatusType.Invisible,
            _ => UserStatusType.Online,
        };
        await bot.Client!.UpdatePresenceAsync(new PresenceProperties(status));
        await Respond(cmd, $"Status set to **{val}**.");
    }

    private static async Task HandleActivity(SlashCommandInteraction cmd, BotService bot)
    {
        var opts = cmd.Data.Options.ToDictionary(o => o.Name, o => o.Value);
        var type = opts.GetValueOrDefault("type") ?? "playing";
        var desc = opts.GetValueOrDefault("description") ?? "";
        var url = opts.GetValueOrDefault("url");

        UserActivityType actType = type switch
        {
            "streaming" => UserActivityType.Streaming,
            "listening" => UserActivityType.Listening,
            "watching" => UserActivityType.Watching,
            "competing" => UserActivityType.Competing,
            _ => UserActivityType.Playing,
        };

        var activity = new UserActivityProperties(desc, actType);
        if (url is not null) activity = activity.WithUrl(url);

        await bot.Client!.UpdatePresenceAsync(
            new PresenceProperties(UserStatusType.Online).WithActivities([activity]));
        await Respond(cmd, "Activity set.");
    }

    private static async Task HandleStage(SlashCommandInteraction cmd, BotService bot)
    {
        if (!cmd.GuildId.HasValue) { await Respond(cmd, "Must be used in a server."); return; }
        if (!bot.Client!.Cache.Guilds.TryGetValue(cmd.GuildId.Value, out var guild)) { await Respond(cmd, "Guild not found."); return; }

        var chId = bot.GetCurrentChannelId(cmd.GuildId.Value);
        var stageChannel = guild.Channels.GetValueOrDefault(chId) as StageGuildChannel;
        if (stageChannel is null) { await Respond(cmd, "Not in a stage channel."); return; }

        var sub = cmd.Data.Options[0];
        switch (sub.Name)
        {
            case "start":
                var topic = sub.Options?.Count > 0 ? sub.Options[0].Value ?? "Live" : "Live";
                await bot.Client!.Rest.CreateStageInstanceAsync(new StageInstanceProperties(stageChannel.Id, topic ?? "Live"));
                await Respond(cmd, "Stage started.");
                break;
            case "end":
                await bot.Client!.Rest.DeleteStageInstanceAsync(stageChannel.Id);
                await Respond(cmd, "Stage ended.");
                break;
            case "speak":
                await bot.Client!.Rest.ModifyCurrentGuildUserVoiceStateAsync(cmd.GuildId.Value,
                    x => x.WithRequestToSpeakTimestamp(DateTimeOffset.UtcNow).WithSuppress(false));
                await Respond(cmd, "Requested to speak.");
                break;
            case "withdraw":
                await bot.Client!.Rest.ModifyCurrentGuildUserVoiceStateAsync(cmd.GuildId.Value,
                    x => x.WithRequestToSpeakTimestamp(null).WithSuppress(true));
                await Respond(cmd, "Withdrew speak request.");
                break;
            case "topic":
                var newTopic = sub.Options![0].Value ?? "";
                await bot.Client!.Rest.ModifyStageInstanceAsync(stageChannel.Id, x => x.WithTopic(newTopic));
                await Respond(cmd, $"Topic updated to **{newTopic}**.");
                break;
        }
    }

    private static async Task HandleAbout(SlashCommandInteraction cmd)
    {
        var isPublic = cmd.Data.Options.FirstOrDefault()?.Value == "true";
        var embed = new EmbedProperties()
            .WithTitle("cordcast")
            .WithDescription("Discord audio streaming bot. Streams audio between your system and Discord voice channels.")
            .WithColor(new Color(0x8B5CF6));
        await cmd.SendResponseAsync(InteractionCallback.Message(
            new InteractionMessageProperties()
                .WithEmbeds([embed])
                .WithFlags(isPublic ? null : MessageFlags.Ephemeral)));
    }

    private static async Task HandleInvite(SlashCommandInteraction cmd, BotService bot)
    {
        var isPublic = cmd.Data.Options.FirstOrDefault()?.Value == "true";
        var appId = bot.Client!.Id;
        var url = $"https://discord.com/oauth2/authorize?client_id={appId}&permissions=36702208&scope=bot%20applications.commands";
        await cmd.SendResponseAsync(InteractionCallback.Message(
            new InteractionMessageProperties()
                .WithContent($"Invite: {url}")
                .WithFlags(isPublic ? null : MessageFlags.Ephemeral)));
    }

    private static async Task HandleStop(SlashCommandInteraction cmd, BotService bot)
    {
        await Respond(cmd, "Stopping bot…");
        await bot.StopAsync();
    }

    private static async Task HandleLeaveGuild(SlashCommandInteraction cmd, BotService bot)
    {
        if (!cmd.GuildId.HasValue) { await Respond(cmd, "Must be used in a server."); return; }
        await Respond(cmd, "Leaving server.");
        await bot.Client!.Rest.LeaveGuildAsync(cmd.GuildId.Value);
        bot.EmitGuildsUpdated();
    }
}
