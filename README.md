<div align="center">

# ✦ OrbSpoofer

### Discord Orb Quest Spoofer — Built in C# / .NET 10

[![.NET](https://img.shields.io/badge/.NET-10-512bd4?style=flat-square&logo=dotnet&logoColor=white&labelColor=1a1a2e)](https://dotnet.microsoft.com)
[![WPF](https://img.shields.io/badge/WPF-Desktop-5865f2?style=flat-square&logo=windows&logoColor=white&labelColor=1a1a2e)](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/)
[![Platform](https://img.shields.io/badge/Windows-10%2F11-00a4ef?style=flat-square&logo=windows&logoColor=white&labelColor=1a1a2e)](https://github.com/ZavalaSebas/OrbSpoofer)
[![License](https://img.shields.io/badge/License-GPL%20v3-ff4444?style=flat-square&logo=opensourceinitiative&logoColor=white&labelColor=1a1a2e)](./LICENSE)
[![Version](https://img.shields.io/badge/Version-1.0.0-57F287?style=flat-square&labelColor=1a1a2e)](https://github.com/ZavalaSebas/OrbSpoofer/releases)

<br/>

Earn Discord orb quests without installing games.  
EZ :P

[Get Started](#-get-started) · [How It Works](#how-it-works) · [Usage](#usage) · [Features](#features) · [Steam Quest](#steam-quest-mode)

</div>

<br/>

## What is OrbSpoofer?

A Windows desktop app built in **C# / .NET 10** that earns Discord orb quests by creating fake game processes — no installs, no modifications, no drama.

It pulls Discord's own detectable games list from their public API, copies a base executable, renames it to the exact process name Discord expects, and launches it in the background. Discord sees the name, checks the box, you get your orbs.

**Zero code injection. Zero client modification. Zero suspicious traffic.** Just a renamed process sitting in your task list — which is literally all Discord ever checks.

Born from the idea behind [orbshacker](https://github.com/strykey/orbshacker) by [Strykey](https://github.com/strykey), rebuilt from scratch in .NET with a native WPF interface.

> **Educational purposes only.** You are responsible for following Discord's ToS. Use at your own risk.

<br/>

## How It Works

The way Discord detects what you're playing is by reading your Windows process list. Lets say `roblox.exe` running? Must be Roblox!. There's no deeper verification — no hash check, no memory scan, nothing. The name is all it checks.

1. Launch OrbSpoofer
2. It fetches Discord's game database via their public API
3. You search for a game
4. Click **Spoof**
5. Done — Discord thinks you're playing

The fake process runs until you close it. Discord keeps detecting it the entire time. No kernel-level anti-cheat is involved in orb quests, so there's nothing watching for renamed executables.

<br/>

## ⚡ Get Started

<div align="center">
<table>
<tr>
<td align="center" width="50%">

**📦 Download a Release**

Grab the latest `OrbSpoofer.exe` from  
[Releases](https://github.com/ZavalaSebas/OrbSpoofer/releases)  
Self-contained — no .NET required. Just run it.

</td>
<td align="center" width="50%">

**🔧 Build from Source**

```bash
git clone https://github.com/ZavalaSebas/OrbSpoofer.git
cd OrbSpoofer
dotnet publish -c Release -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true
```

</td>
</tr>
</table>
</div>

<br/>

## Requirements

- Windows 10 or 11 (x64)
- .NET 10 Runtime (or just use the self-contained publish)
- Internet connection for the game database
- Discord must be running — it only works while Discord is actively scanning processes

<br/>

## Usage

<div align="center">
<table>
<tr>
<td align="center">
<img width="979" height="689" alt="image" src="https://github.com/user-attachments/assets/46a0adda-4713-420a-9907-3df9abace9aa" />
<br/><sub><i>Database Mode — search & spoof</i></sub>
</td>
<td align="center">
<img width="978" height="700" alt="image" src="https://github.com/user-attachments/assets/44d517a9-b860-4bd6-92c0-901b162af2bc" />
<br/><sub><i>Let 15 Mins pass</i></sub>
</td>
</tr>
</table>
</div>

### Database Mode (recommended)

1. Open `OrbSpoofer.exe`
2. Wait for the Discord game database to load
3. Use the search bar to find a game — works with full names, abbreviations, whatever
4. Hit **Spoof** next to the game you want
5. A timer window opens — keep it running until the quest is done
6. Discord detects the fake process and shows "Playing [Game]"

### Manual Mode

1. Go to the **Manual** tab in the sidebar
2. Type the exact executable name (e.g. `TslGame.exe`)
3. Click **Spoof**
4. The process launches in the background

### Completing Multiple Quests

Select a game, hit Spoof, go back to the menu, pick another one, repeat. All processes run in parallel — Discord sees all of them at once. Wait 15 minutes, close everything, done.

<br/>

## Features

**Dark Native UI** — Built entirely in WPF with a dark theme, sidebar navigation, styled cards, and smooth interactions. No terminal, no ugly text.

**Discord Game Database** — Pulls the official detectable games list live from Discord's API, with a GitHub-hosted backup if the API is down.

**Smart Search** — Find games by name, abbreviation, or alias. Results are filtered and deduplicated.

**Manual Mode** — Spoof any executable name directly for games not in the database.

**Process Counter** — See how many fake processes are currently active in the status bar.

**Quest Timer** — A 15-minute countdown window that opens with each fake process so you know exactly how long to wait.

<br/>

## Steam Quest Mode

Some games need more than a process name. Discord also checks that Steam shows the game as downloading. Standard spoofing won't cut it — Steam Quest Mode handles it.

1. Go to the **Steam** tab
2. Search for the game by name
3. Click **Spoof**
4. OrbSpoofer fetches game metadata from SteamCMD's API, reads your Steam ID from the registry, generates a fake `appmanifest_<appid>.acf`, and places the executable in the correct Steam directory
5. Wait 15 minutes, close when done, auto-cleanup on exit

<br/>

## Project Structure

```
OrbSpoofer/
├── OrbSpoofer.slnx
├── .github/
│   └── FUNDING.yml                  Ko-fi & GitHub Sponsors
├── OrbSpoofer/
│   ├── OrbSpoofer.csproj            .NET 10 WPF project config
│   ├── App.xaml / .cs              Entry point, theme merge, dual-mode launch
│   ├── Config.cs                    Constants & centralized configuration
│   ├── MainWindow.xaml / .cs        Main UI with 4 navigable views
│   ├── AssemblyInfo.cs              Assembly metadata
│   ├── Models/
│   │   ├── DiscordGame.cs           Discord game data models
│   │   └── SteamGameInfo.cs         Steam data models
│   ├── Services/
│   │   ├── DiscordDatabase.cs       Fetch & search Discord's game list
│   │   ├── GameFaker.cs             Create & launch fake processes
│   │   ├── SteamService.cs          Steam detection, API, appmanifest generation
│   │   ├── NetworkHelper.cs         Centralized HTTP client
│   │   └── Updater.cs               GitHub-based update checker
│   ├── Exceptions/
│   │   └── OrbSpooferExceptions.cs  Custom exception hierarchy
│   ├── Themes/
│   │   └── DarkTheme.xaml           Full dark theme with custom controls
│   └── UI/
│       └── Windows/
│           └── TimerWindow.xaml/.cs  15-minute quest countdown
└── README.md
```

<br/>

## Architecture

No MVVM frameworks, no NuGet dependencies — pure .NET base class library.

- **Dual-mode launch**: The same exe runs as either the main UI or a hidden timer process via `--timer-mode`. When you spoof a game, OrbSpoofer copies itself, renames the copy, and relaunches it as the fake process.
- **DiscordDatabase**: Tries the official API first, falls back to a GitHub-hosted Gist. Parses executables per platform and filters out anti-cheat launchers and uninstallers.
- **GameFaker**: Copies the OrbSpoofer executable to `~/Desktop/Win64/<GameExe>.exe`, renames it, and launches it hidden.
- **SteamService**: Reads the Steam registry for your install path and user ID, searches the SteamCMD API for game metadata, and generates realistic VDF-format appmanifest files.
- **DarkTheme**: 13 named colors, 4 button styles, custom scrollbars, styled list items — all in XAML, no code.

<br/>

## Legal

**Educational purposes only. No commercial use.**

This tool exists to study how Discord's game detection works and to explore process manipulation techniques. Commercial use, redistribution, or sale is strictly prohibited.

You are solely responsible for complying with Discord's Terms of Service and all applicable laws. No warranties. No guarantees. Use at your own risk.

<br/>

<div align="center">

Made with ❤ by **ZavalaSebas** :D

<br/>

[![Ko-fi](https://img.shields.io/badge/Ko--fi-Support%20Me-ff5e5b?style=for-the-badge&logo=ko-fi&logoColor=white&labelColor=1a1a2e)](https://ko-fi.com/sebastianzavala82573)
[![GitHub Sponsors](https://img.shields.io/badge/GitHub-Sponsor%20Me-ea4aaa?style=for-the-badge&logo=githubsponsors&logoColor=white&labelColor=1a1a2e)](https://github.com/sponsors/ZavalaSebas)

<br/>

[![GitHub stars](https://img.shields.io/github/stars/ZavalaSebas/OrbSpoofer?style=for-the-badge&color=5865f2&labelColor=1a1a2e)](https://github.com/ZavalaSebas/OrbSpoofer/stargazers)
[![GitHub forks](https://img.shields.io/github/forks/ZavalaSebas/OrbSpoofer?style=for-the-badge&color=5865f2&labelColor=1a1a2e)](https://github.com/ZavalaSebas/OrbSpoofer/network)

</div>
