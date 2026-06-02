# cordcast

Discord audio streaming bot with a desktop control panel. Streams your system's microphone (or any recording device) into Discord voice and stage channels, and plays Discord audio back out to your speakers. Built with a Flutter desktop frontend driving a self-contained .NET worker bot.

Runs on **Windows** (Linux build paths exist but are untested).

## Features

- **Speak**: capture a recording device and stream 48 kHz stereo audio into Discord voice/stage channels
- **Listen**: play audio received from Discord back out to a local playback device
- **Noise gate**: optional RMS threshold to suppress audio below a configurable level
- **Voice & stage channels**: join, leave, and manage stage instances (start/end, request/withdraw speak, set topic)
- **Auto-join**: automatically join a configured channel per server
- **Follow audio**: track a specific user as they move between voice channels
- **Channel binding**: restrict slash commands to specific text channels per server
- **Presence control**: set bot online status (online/idle/dnd/invisible) and activity (playing/streaming/listening/watching/competing)
- **Slash commands**: full control from inside Discord: `/join`, `/leave`, `/leave-all`, `/autojoin`, `/follow-audio`, `/bind`, `/status`, `/activity`, `/stage`, `/about`, `/invite`, `/stop`, `/leave-guild`
- **DAVE E2EE**: end-to-end encrypted voice via libdave (mandatory on Discord since Mar 2026)
- **System tray**: minimize to tray, runs in the background
- **Auto-login**: optionally reconnect the bot on app launch

## Architecture

cordcast is two processes:

- **`app/`**: Flutter desktop UI. Manages config, device selection, and per-server settings.
- **`worker/`**: self-contained .NET 9 console executable bundled as a Flutter asset. Runs the Discord.Net bot and handles audio via [BASS](https://www.un4seen.com/).

The app launches the worker as a child process and talks to it over **stdin/stdout using line-delimited JSON** (commands in, events out). Audio capture/playback uses BASS; Discord voice uses Discord.Net with native `opus`, `libsodium`, and `libdave` libraries resolved from the worker's own directory.

## Requirements

- A [Discord bot application](https://discord.com/developers/applications) with a bot token
- The bot needs the **Server Members** and **Voice** related intents/permissions; invite it with the link from `/invite`

Native audio/codec libraries (`bass.dll`, `opus.dll`, `libsodium.dll`, `libdave.dll`) ship in `app/assets/worker/` and are bundled into the build. No separate install needed.

## Developing

### Prerequisites

- [Flutter SDK](https://docs.flutter.dev/get-started/install/windows/desktop) with Windows desktop support enabled
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)

### Build the worker

The worker must be published into `app/assets/worker/` so Flutter bundles it as an asset.

```bash
cd worker
dotnet publish -c Release -r win-x64 --self-contained -o ..\app\assets\worker
```

### Run / build the app

```bash
cd app
flutter pub get
flutter run -d windows
flutter build windows
```

Output: `build\windows\x64\runner\Release\`

The worker is launched at runtime from `data\flutter_assets\assets\worker\CordCastWorker.exe` next to the app executable.

## Logs

The worker writes a log to `%LOCALAPPDATA%\CordCast\worker.log`.
