using System.Runtime.InteropServices;
using CordCastWorker;
using CordCastWorker.Models;
using ManagedBass;

Console.OutputEncoding = System.Text.Encoding.UTF8;

var logDir = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "CordCast");
Directory.CreateDirectory(logDir);
var logPath = Path.Combine(logDir, "worker.log");
Logger.Init(logPath);

// Redirect native library loading to the exe's own directory.
// Required when running as a Flutter asset (AppDomain.BaseDirectory != cwd).
static IntPtr ResolveFromExeDir(string name, System.Reflection.Assembly assembly, DllImportSearchPath? paths)
{
    var dir = AppDomain.CurrentDomain.BaseDirectory;
    var candidates = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
        ? new[] { $"{name}.dll", $"lib{name}.dll" }
        : new[] { $"lib{name}.so", $"{name}.so" };
    foreach (var file in candidates)
    {
        var full = Path.Combine(dir, file);
        if (File.Exists(full)) return NativeLibrary.Load(full);
    }
    return IntPtr.Zero;
}

NativeLibrary.SetDllImportResolver(typeof(Bass).Assembly, ResolveFromExeDir);

// Discord.Net.WebSocket requires opus (audio codec) and libsodium (encryption)
var discordAudioAssembly = typeof(Discord.WebSocket.DiscordSocketClient).Assembly;
NativeLibrary.SetDllImportResolver(discordAudioAssembly, ResolveFromExeDir);

// Discord.Net.Dave requires libdave (DAVE E2EE), mandatory since Mar 2026
NativeLibrary.SetDllImportResolver(typeof(Discord.LibDave.Dave).Assembly, ResolveFromExeDir);

var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

var audio = new AudioService();
BotService? bot = null;

await foreach (var cmd in Ipc.ReadCommands(cts.Token))
{
    var command = cmd.GetString("command");
    try
    {
        switch (command)
        {
            case "list_devices":
                audio.Init();
                var rec = audio.GetRecordingDevices()
                    .Select(d => new { id = d.Id, name = d.Name })
                    .ToList();
                var play = audio.GetPlaybackDevices()
                    .Select(d => new { id = d.Id, name = d.Name })
                    .ToList();
                Ipc.Emit("devices_listed", new Dictionary<string, object?>
                {
                    ["recording"] = rec,
                    ["playback"] = play,
                });
                break;

            case "start":
                var configEl = cmd.Deserialize<Config>("config");
                if (configEl is null)
                {
                    Ipc.Emit("error", new Dictionary<string, object?> { ["message"] = "Invalid config." });
                    break;
                }
                if (bot is not null) await bot.DisposeAsync();
                bot = new BotService(audio);
                await bot.StartAsync(configEl);
                break;

            case "stop":
                if (bot is not null)
                {
                    await bot.StopAsync();
                    await bot.DisposeAsync();
                    bot = null;
                }
                break;

            case "set_speak":
                audio.SetSpeak(
                    enabled: cmd.GetBool("enabled"),
                    deviceName: cmd.GetString("deviceName"),
                    thresholdEnabled: cmd.GetBool("thresholdEnabled"),
                    threshold: cmd.GetDouble("threshold", 0.5));
                break;

            case "set_listen":
                audio.SetListen(
                    enabled: cmd.GetBool("enabled"),
                    deviceName: cmd.GetString("deviceName"));
                break;

            case "update_guild_config":
                if (bot is not null)
                {
                    var guildId = cmd.GetString("guildId") ?? "";
                    var guildCfg = cmd.Deserialize<GuildConfig>("config");
                    if (guildCfg is not null)
                        bot.UpdateGuildConfig(guildId, guildCfg);
                }
                break;

            case "exit":
                cts.Cancel();
                break;
        }
    }
    catch (Exception ex)
    {
        Logger.Write("ERROR", "Program", $"Command '{command}': {ex}");
        Ipc.Emit("error", new Dictionary<string, object?> { ["message"] = ex.Message });
    }
}

if (bot is not null) await bot.DisposeAsync();
audio.Dispose();
Logger.Close();
