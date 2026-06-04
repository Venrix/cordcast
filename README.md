# cordcast

[![Download latest Release](https://img.shields.io/github/v/release/Venrix/cordcast?label=Download%20latest%20Release&logo=windows&style=for-the-badge)](https://github.com/Venrix/cordcast/releases/latest)

Discord audio streaming bot with a desktop control panel. Streams your system's microphone (or any recording device) into Discord voice and stage channels, and plays Discord audio back out to your speakers. A companion VST3 plugin lets you route DAW audio directly into Discord without a virtual cable. Built with a Flutter desktop frontend driving a self-contained .NET worker bot.

Runs on **Windows** (Linux build paths exist but are untested).

<img width="400" alt="image" src="https://github.com/user-attachments/assets/52873230-d948-4133-bf70-0d3400249d42" />
<img width="400" alt="image" src="https://github.com/user-attachments/assets/c6d11d0f-5d34-4b26-88a8-1cf2838e2108" />
<img width="400" alt="image" src="https://github.com/user-attachments/assets/a26f170f-94c5-4374-bde5-1bacad6ab3c6" />

## Features

- **Speak**: capture a recording device and stream 48 kHz stereo audio into Discord voice/stage channels; select **CordCast VST Plugin** to route audio directly from a DAW send channel
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

cordcast has three components:

- **`app/`**: Flutter desktop UI. Manages config, device selection, and per-server settings.
- **`worker/`**: self-contained .NET 10 console executable bundled as a Flutter asset. Runs the [NetCord](https://netcord.dev/) bot and handles audio via [BASS](https://www.un4seen.com/).
- **`vst/`**: optional JUCE-based VST3 send plugin. Routes DAW audio into cordcast via a named pipe (`\\.\pipe\CordCastAudio`). No UI; install and add as a send effect on any DAW track.

The app launches the worker as a child process and talks to it over **stdin/stdout using line-delimited JSON** (commands in, events out). Audio capture/playback uses BASS; Discord voice uses NetCord with native `opus`, `libsodium`, and `libdave` libraries resolved from the worker's own directory.

## Requirements

- A Discord bot application and token (see below)
- The bot invited to your server with voice permissions

Native audio/codec libraries (`bass.dll`, `opus.dll`, `libsodium.dll`, `libdave.dll`) ship in `app/assets/worker/` and are bundled into the build. No separate install needed.

## Setting up a Discord bot

### 1. Create the application

1. Go to the [Discord Developer Portal](https://discord.com/developers/applications) and log in
2. Click **New Application**, enter a name (e.g. `cordcast`), accept the terms, and click **Create**

### 2. Create the bot and copy the token

1. In the left sidebar click **Bot**
2. Under **Token**, click **Reset Token** → confirm (enter your 2FA code if prompted) → copy the token
3. Paste it into the **Bot Token** field in cordcast's Settings tab — you won't be able to view the token again without resetting it

### 3. Privileged Gateway Intents

cordcast only uses the **Guilds** and **Guild Voice States** intents, which are not privileged. No privileged intents need to be enabled; leave all toggles under _Privileged Gateway Intents_ off.

### 4. Invite the bot to your server

1. In the left sidebar go to **OAuth2 → URL Generator**
2. Under **Scopes** tick: `bot`, `applications.commands`
3. Under **Bot Permissions** tick:

   | Permission         | Why                                              |
   | ------------------ | ------------------------------------------------ |
   | Connect            | Join voice and stage channels                    |
   | Speak              | Stream audio into voice channels                 |
   | Use Voice Activity | Send audio without push-to-talk                  |
   | Mute Members       | Manage stage speakers                            |
   | Move Members       | Follow a user between channels (`/follow-audio`) |
   | Request to Speak   | Enter stage channels as a speaker                |

4. Copy the generated URL at the bottom, open it in your browser, select your server, and click **Authorise**

The bot will appear offline until cordcast connects it.

## Developing

### Prerequisites

- [Flutter SDK](https://docs.flutter.dev/get-started/install/windows/desktop) with Windows desktop support enabled
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [CMake 3.22+](https://cmake.org/download/) and Visual Studio 2022 (with C++ workload) — VST plugin only

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

### Build the VST plugin (optional)

Requires CMake 3.22+ and Visual Studio 2022. JUCE is fetched automatically at configure time.

```bash
cmake -B vst/build -S vst
cmake --build vst/build --config Release
```

Output: `vst\build\CordCastSend_artefacts\Release\VST3\CordCastSend.vst3`

Copy `CordCastSend.vst3` to `C:\Program Files\Common Files\VST3\` and rescan plugins in your DAW. Set your DAW project to **48 kHz**. Add **CordCast Send** as a send effect on any track, then select **CordCast VST Plugin** as the recording device in the cordcast app.

## Logs

The worker writes a log to `%LOCALAPPDATA%\CordCast\worker.log`.
