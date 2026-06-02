using Discord;
using Discord.WebSocket;
using CordCastWorker.Models;

namespace CordCastWorker.Commands;

public static class SlashCommandHandler
{
    public static ApplicationCommandProperties[] BuildCommands() =>
    [
        new SlashCommandBuilder()
            .WithName("join")
            .WithDescription("Join a voice or stage channel")
            .AddOption("channel", ApplicationCommandOptionType.Channel, "Channel to join", isRequired: false)
            .Build(),

        new SlashCommandBuilder()
            .WithName("leave")
            .WithDescription("Disconnect from current voice channel")
            .Build(),

        new SlashCommandBuilder()
            .WithName("leave-all")
            .WithDescription("Disconnect from all voice channels across all servers")
            .Build(),

        new SlashCommandBuilder()
            .WithName("autojoin")
            .WithDescription("Configure auto-join channel")
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("set")
                .WithDescription("Set auto-join channel")
                .WithType(ApplicationCommandOptionType.SubCommand)
                .AddOption("channel", ApplicationCommandOptionType.Channel, "Channel", isRequired: true))
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("clear")
                .WithDescription("Clear auto-join")
                .WithType(ApplicationCommandOptionType.SubCommand))
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("show")
                .WithDescription("Show current auto-join channel")
                .WithType(ApplicationCommandOptionType.SubCommand))
            .Build(),

        new SlashCommandBuilder()
            .WithName("follow-audio")
            .WithDescription("Follow a user between voice channels")
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("set")
                .WithDescription("Set user to follow")
                .WithType(ApplicationCommandOptionType.SubCommand)
                .AddOption("user", ApplicationCommandOptionType.User, "User", isRequired: true))
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("clear")
                .WithDescription("Stop following")
                .WithType(ApplicationCommandOptionType.SubCommand))
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("show")
                .WithDescription("Show followed user")
                .WithType(ApplicationCommandOptionType.SubCommand))
            .Build(),

        new SlashCommandBuilder()
            .WithName("bind")
            .WithDescription("Restrict commands to specific channels")
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("add")
                .WithDescription("Add a command channel")
                .WithType(ApplicationCommandOptionType.SubCommand)
                .AddOption("channel", ApplicationCommandOptionType.Channel, "Channel", isRequired: true))
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("remove")
                .WithDescription("Remove a command channel")
                .WithType(ApplicationCommandOptionType.SubCommand)
                .AddOption("channel", ApplicationCommandOptionType.Channel, "Channel", isRequired: true))
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("clear")
                .WithDescription("Allow commands in all channels")
                .WithType(ApplicationCommandOptionType.SubCommand))
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("show")
                .WithDescription("Show bound channels")
                .WithType(ApplicationCommandOptionType.SubCommand))
            .Build(),

        new SlashCommandBuilder()
            .WithName("status")
            .WithDescription("Set bot online status")
            .AddOption("status", ApplicationCommandOptionType.String, "online|idle|dnd|inv", isRequired: true,
                choices:
                [
                    new ApplicationCommandOptionChoiceProperties { Name = "online", Value = "online" },
                    new ApplicationCommandOptionChoiceProperties { Name = "idle", Value = "idle" },
                    new ApplicationCommandOptionChoiceProperties { Name = "dnd", Value = "dnd" },
                    new ApplicationCommandOptionChoiceProperties { Name = "invisible", Value = "inv" },
                ])
            .Build(),

        new SlashCommandBuilder()
            .WithName("activity")
            .WithDescription("Set bot activity/presence")
            .AddOption("type", ApplicationCommandOptionType.String, "playing|streaming|listening|watching|competing", isRequired: true)
            .AddOption("description", ApplicationCommandOptionType.String, "Activity description", isRequired: true)
            .AddOption("url", ApplicationCommandOptionType.String, "Stream URL (streaming only)", isRequired: false)
            .Build(),

        new SlashCommandBuilder()
            .WithName("stage")
            .WithDescription("Manage stage channels")
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("start")
                .WithDescription("Start a stage instance")
                .WithType(ApplicationCommandOptionType.SubCommand)
                .AddOption("topic", ApplicationCommandOptionType.String, "Stage topic", isRequired: false))
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("end")
                .WithDescription("End the stage instance")
                .WithType(ApplicationCommandOptionType.SubCommand))
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("speak")
                .WithDescription("Request to speak")
                .WithType(ApplicationCommandOptionType.SubCommand))
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("withdraw")
                .WithDescription("Withdraw speak request")
                .WithType(ApplicationCommandOptionType.SubCommand))
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("topic")
                .WithDescription("Update stage topic")
                .WithType(ApplicationCommandOptionType.SubCommand)
                .AddOption("value", ApplicationCommandOptionType.String, "New topic", isRequired: true))
            .Build(),

        new SlashCommandBuilder()
            .WithName("about")
            .WithDescription("About cordcast")
            .AddOption("public", ApplicationCommandOptionType.Boolean, "Send publicly?", isRequired: false)
            .Build(),

        new SlashCommandBuilder()
            .WithName("invite")
            .WithDescription("Get bot invite link")
            .AddOption("public", ApplicationCommandOptionType.Boolean, "Send publicly?", isRequired: false)
            .Build(),

        new SlashCommandBuilder()
            .WithName("stop")
            .WithDescription("Disconnect bot from Discord (keeps app running)")
            .Build(),

        new SlashCommandBuilder()
            .WithName("leave-guild")
            .WithDescription("Remove bot from this server")
            .Build(),
    ];

    public static async Task HandleAsync(SocketSlashCommand cmd, BotService bot, Config config)
    {
        // Channel binding check (skip for DMs)
        if (cmd.GuildId.HasValue)
        {
            var guildCfg = config.GetGuildConfig(cmd.GuildId.Value.ToString());
            if (!guildCfg.IsCommandAllowed(cmd.ChannelId?.ToString() ?? ""))
            {
                await cmd.RespondAsync("Commands are restricted to specific channels.", ephemeral: true);
                return;
            }
        }

        try
        {
            await (cmd.CommandName switch
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
                _ => cmd.RespondAsync("Unknown command.", ephemeral: true),
            });
        }
        catch (Exception ex)
        {
            Ipc.Emit("error", new Dictionary<string, object?> { ["message"] = $"/{cmd.CommandName}: {ex.Message}" });
            try { await cmd.RespondAsync($"Error: {ex.Message}", ephemeral: true); } catch { }
        }
    }

    private static async Task HandleJoin(SocketSlashCommand cmd, BotService bot)
    {
        if (cmd.GuildId is null) { await cmd.RespondAsync("Must be used in a server.", ephemeral: true); return; }
        var guild = bot.Client!.GetGuild(cmd.GuildId.Value);

        IVoiceChannel? target = null;
        if (cmd.Data.Options.FirstOrDefault()?.Value is IVoiceChannel ch)
            target = ch;
        else
        {
            var user = guild.GetUser(cmd.User.Id);
            target = user?.VoiceChannel;
        }

        if (target is null) { await cmd.RespondAsync("Specify a channel or join one first.", ephemeral: true); return; }

        await cmd.DeferAsync(ephemeral: true);
        try
        {
            await bot.JoinChannelAsync(guild, target);
            bot.EmitGuildsUpdated();
            await cmd.FollowupAsync($"Joined **{target.Name}**.", ephemeral: true);
        }
        catch (Exception ex)
        {
            Ipc.Emit("error", new Dictionary<string, object?> { ["message"] = $"/join: {ex.Message}" });
            try { await cmd.FollowupAsync($"Error: {ex.Message}", ephemeral: true); } catch { }
        }
    }

    private static async Task HandleLeave(SocketSlashCommand cmd, BotService bot)
    {
        if (cmd.GuildId is null) { await cmd.RespondAsync("Must be used in a server.", ephemeral: true); return; }
        await bot.LeaveGuildAudioAsync(cmd.GuildId.Value);
        bot.EmitGuildsUpdated();
        await cmd.RespondAsync("Left voice channel.", ephemeral: true);
    }

    private static async Task HandleLeaveAll(SocketSlashCommand cmd, BotService bot)
    {
        await bot.LeaveAllAsync();
        bot.EmitGuildsUpdated();
        await cmd.RespondAsync("Left all voice channels.", ephemeral: true);
    }

    private static async Task HandleAutoJoin(SocketSlashCommand cmd, BotService bot, Config config)
    {
        if (cmd.GuildId is null) { await cmd.RespondAsync("Must be used in a server.", ephemeral: true); return; }
        var guildCfg = config.GetGuildConfig(cmd.GuildId.Value.ToString());
        var sub = cmd.Data.Options.First();

        switch (sub.Name)
        {
            case "set":
                var ch = sub.Options.First().Value as IChannel;
                guildCfg.AutoJoinAudioChannelId = ch?.Id.ToString();
                bot.UpdateGuildConfig(guildCfg.GuildId, guildCfg);
                await cmd.RespondAsync($"Auto-join set to <#{ch?.Id}>.", ephemeral: true);
                break;
            case "clear":
                guildCfg.AutoJoinAudioChannelId = null;
                bot.UpdateGuildConfig(guildCfg.GuildId, guildCfg);
                await cmd.RespondAsync("Auto-join cleared.", ephemeral: true);
                break;
            case "show":
                var id = guildCfg.AutoJoinAudioChannelId;
                await cmd.RespondAsync(id is null ? "No auto-join set." : $"Auto-join: <#{id}>", ephemeral: true);
                break;
        }
    }

    private static async Task HandleFollowAudio(SocketSlashCommand cmd, BotService bot, Config config)
    {
        if (cmd.GuildId is null) { await cmd.RespondAsync("Must be used in a server.", ephemeral: true); return; }
        var guildCfg = config.GetGuildConfig(cmd.GuildId.Value.ToString());
        var sub = cmd.Data.Options.First();

        switch (sub.Name)
        {
            case "set":
                var user = sub.Options.First().Value as IUser;
                guildCfg.FollowedUserId = user?.Id.ToString();
                bot.UpdateGuildConfig(guildCfg.GuildId, guildCfg);
                await cmd.RespondAsync($"Now following <@{user?.Id}>.", ephemeral: true);
                break;
            case "clear":
                guildCfg.FollowedUserId = null;
                bot.UpdateGuildConfig(guildCfg.GuildId, guildCfg);
                await cmd.RespondAsync("Follow cleared.", ephemeral: true);
                break;
            case "show":
                var uid = guildCfg.FollowedUserId;
                await cmd.RespondAsync(uid is null ? "Not following anyone." : $"Following: <@{uid}>", ephemeral: true);
                break;
        }
    }

    private static async Task HandleBind(SocketSlashCommand cmd, BotService bot, Config config)
    {
        if (cmd.GuildId is null) { await cmd.RespondAsync("Must be used in a server.", ephemeral: true); return; }
        var guildCfg = config.GetGuildConfig(cmd.GuildId.Value.ToString());
        var sub = cmd.Data.Options.First();

        switch (sub.Name)
        {
            case "add":
                var ch = sub.Options.First().Value as IChannel;
                if (ch is not null) guildCfg.CommandChannelIds.Add(ch.Id.ToString());
                bot.UpdateGuildConfig(guildCfg.GuildId, guildCfg);
                await cmd.RespondAsync($"Bound to <#{ch?.Id}>.", ephemeral: true);
                break;
            case "remove":
                var rch = sub.Options.First().Value as IChannel;
                if (rch is not null) guildCfg.CommandChannelIds.Remove(rch.Id.ToString());
                bot.UpdateGuildConfig(guildCfg.GuildId, guildCfg);
                await cmd.RespondAsync($"Removed <#{rch?.Id}> from bindings.", ephemeral: true);
                break;
            case "clear":
                guildCfg.CommandChannelIds.Clear();
                bot.UpdateGuildConfig(guildCfg.GuildId, guildCfg);
                await cmd.RespondAsync("All channel bindings cleared.", ephemeral: true);
                break;
            case "show":
                var ids = guildCfg.CommandChannelIds;
                var msg = ids.Count == 0
                    ? "All channels (no restriction)."
                    : string.Join(", ", ids.Select(id => $"<#{id}>"));
                await cmd.RespondAsync(msg, ephemeral: true);
                break;
        }
    }

    private static async Task HandleStatus(SocketSlashCommand cmd, BotService bot)
    {
        var val = cmd.Data.Options.First().Value as string;
        var status = val switch
        {
            "online" => UserStatus.Online,
            "idle" => UserStatus.Idle,
            "dnd" => UserStatus.DoNotDisturb,
            "inv" => UserStatus.Invisible,
            _ => UserStatus.Online,
        };
        await bot.Client!.SetStatusAsync(status);
        await cmd.RespondAsync($"Status set to **{val}**.", ephemeral: true);
    }

    private static async Task HandleActivity(SocketSlashCommand cmd, BotService bot)
    {
        var opts = cmd.Data.Options.ToDictionary(o => o.Name, o => o.Value);
        var type = opts["type"] as string ?? "playing";
        var desc = opts["description"] as string ?? "";
        var url = opts.TryGetValue("url", out var u) ? u as string : null;

        ActivityType actType = type switch
        {
            "streaming" => ActivityType.Streaming,
            "listening" => ActivityType.Listening,
            "watching" => ActivityType.Watching,
            "competing" => ActivityType.Competing,
            _ => ActivityType.Playing,
        };

        await bot.Client!.SetGameAsync(desc, url, actType);
        await cmd.RespondAsync($"Activity set.", ephemeral: true);
    }

    private static async Task HandleStage(SocketSlashCommand cmd, BotService bot)
    {
        if (cmd.GuildId is null) { await cmd.RespondAsync("Must be used in a server.", ephemeral: true); return; }
        var guild = bot.Client!.GetGuild(cmd.GuildId.Value);
        var botUser = guild.GetUser(bot.Client.CurrentUser.Id);
        var stageChannel = botUser?.VoiceChannel as SocketStageChannel;

        if (stageChannel is null) { await cmd.RespondAsync("Not in a stage channel.", ephemeral: true); return; }

        var sub = cmd.Data.Options.First();
        switch (sub.Name)
        {
            case "start":
                var topic = sub.Options.FirstOrDefault()?.Value as string ?? "Live";
                await stageChannel.StartStageAsync(topic);
                await cmd.RespondAsync("Stage started.", ephemeral: true);
                break;
            case "end":
                await stageChannel.StopStageAsync();
                await cmd.RespondAsync("Stage ended.", ephemeral: true);
                break;
            case "speak":
                await stageChannel.RequestToSpeakAsync();
                await cmd.RespondAsync("Requested to speak.", ephemeral: true);
                break;
            case "withdraw":
                // Lower hand — move self back to audience via REST voice state
                await stageChannel.MoveToSpeakerAsync((IGuildUser)botUser!);
                await cmd.RespondAsync("Withdrew speak request.", ephemeral: true);
                break;
            case "topic":
                var newTopic = sub.Options.First().Value as string ?? "";
                await stageChannel.ModifyInstanceAsync(p => p.Topic = newTopic);
                await cmd.RespondAsync($"Topic updated to **{newTopic}**.", ephemeral: true);
                break;
        }
    }

    private static async Task HandleAbout(SocketSlashCommand cmd)
    {
        var isPublic = cmd.Data.Options.FirstOrDefault()?.Value as bool? ?? false;
        var embed = new EmbedBuilder()
            .WithTitle("cordcast")
            .WithDescription("Discord audio streaming bot. Streams audio between your system and Discord voice channels.")
            .WithColor(new Color(0x8B5CF6))
            .Build();
        await cmd.RespondAsync(embed: embed, ephemeral: !isPublic);
    }

    private static async Task HandleInvite(SocketSlashCommand cmd, BotService bot)
    {
        var isPublic = cmd.Data.Options.FirstOrDefault()?.Value as bool? ?? false;
        var appId = bot.Client!.CurrentUser.Id;
        var url = $"https://discord.com/oauth2/authorize?client_id={appId}&permissions=36702208&scope=bot%20applications.commands";
        await cmd.RespondAsync($"Invite: {url}", ephemeral: !isPublic);
    }

    private static async Task HandleStop(SocketSlashCommand cmd, BotService bot)
    {
        await cmd.RespondAsync("Stopping bot…", ephemeral: true);
        await bot.StopAsync();
    }

    private static async Task HandleLeaveGuild(SocketSlashCommand cmd, BotService bot)
    {
        if (cmd.GuildId is null) { await cmd.RespondAsync("Must be used in a server.", ephemeral: true); return; }
        var guild = bot.Client!.GetGuild(cmd.GuildId.Value);
        await cmd.RespondAsync("Leaving server.", ephemeral: true);
        await guild.LeaveAsync();
        bot.EmitGuildsUpdated();
    }
}
