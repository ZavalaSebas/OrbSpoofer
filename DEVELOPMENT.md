# OrbSpoofer — Project Guide

This document serves both as a guide to this specific project AND as a reusable template structure for future .NET projects. The architecture, workflow, versioning, testing, and release patterns documented here can be replicated in any new project.

## Architecture Overview (Reusable Template)

```
solution.slnx                    # Solution file (.slnx format)
src/ProjectName/                  # Main application
├── ProjectName.csproj            # <Version> + <AssemblyVersion> single source of truth
├── Config.cs                     # Constants: URLs, paths, timeouts
├── Services/                     # Business logic (Updater, NetworkHelper, etc.)
├── Models/                       # Display/DTO models
└── UI/                           # UI layer (Windows, Controls, etc.)
tests/ProjectName.Tests/          # xUnit test project mirroring src structure
├── ProjectName.Tests.csproj
├── ServiceATests.cs
└── ConfigTests.cs                # AssemblyVersion validation against csproj
.github/workflows/release.yml     # CI/CD: build → test → release on version bump
docs/index.html                   # GitHub Pages landing page
README.md
DEVELOPMENT.md                    # This file
```

Key patterns in this template:
- **Single-file publish** (self-contained, compressed)
- **xUnit tests with build-time csproj validation**
- **CI/CD workflow that only releases on version change**
- **Auto-update via GitHub Releases API**
- **Commit body = Release notes** (categorized with ### Added/Fixed/Changed)
- **GitHub Pages** for project website

## Project Structure (OrbSpoofer)

```
OrbSpoofer/
├── OrbSpoofer/                    # Main WPF app
│   ├── OrbSpoofer.csproj          # Version lives here
│   ├── Config.cs                  # App constants (repo URLs, timeouts, etc.)
│   ├── MainWindow.xaml.cs         # Main UI logic, calls CheckForUpdateAsync on load
│   ├── Services/
│   │   ├── Updater.cs             # GitHub release check, download, swap, cleanup old .exe
│   │   ├── NetworkHelper.cs       # HTTP client (User-Agent set here), FetchJson, DownloadFile
│   │   ├── DiscordDatabase.cs     # Discord game database + GitHub backup
│   │   ├── SteamService.cs        # Steam manifest generation
│   │   ├── GameFaker.cs           # Fake game process spawning (--timer-mode, --quest-id)
│   │   ├── QuestService.cs        # Active quests from api.discordquest.com
│   │   └── GameImageService.cs    # Resolves game images (Discord CDN / Steam store)
│   ├── Models/
│   │   ├── GameDisplayItem.cs     # Display model for DB search results (INotifyPropertyChanged)
│   │   ├── SteamGameDisplayItem.cs# Display model for Steam search + CDN image URL
│   │   ├── QuestItem.cs           # Display model for active quests
│   │   └── DiscordGame.cs         # Discord detectable game entry (+ optional IconHash)
│   └── UI/Windows/
│       ├── TimerWindow.xaml.cs   # Countdown timer, auto-saves quest completion on finish
│       ├── UpdateWindow.xaml.cs   # Download progress UI
│       └── WelcomeWindow.xaml.cs  # First-launch welcome + version-checked sentinel
├── OrbSpoofer.Tests/              # xUnit test project (21 tests)
│   ├── DiscordDatabaseTests.cs    # Search, filtering, exe matching
│   ├── SteamServiceTests.cs       # Manifest generation, depots
│   └── ConfigTests.cs             # AssemblyVersion format & consistency
├── .github/workflows/release.yml  # CI/CD pipeline
├── OrbSpoofer.slnx                # Solution file linking app + tests
└── docs/index.html                # GitHub Pages landing page
```

## Version Management

**Single source of truth**: `<Version>` in `OrbSpoofer/OrbSpoofer.csproj:11`

```xml
<Version>1.2.1</Version>
<AssemblyVersion>$(Version).0</AssemblyVersion>
```

- `AssemblyVersion` is derived from `$(Version)` so `Assembly.GetName().Version` returns the correct value (e.g., `1.1.0.0`).
- `Config.AssemblyVersion` reads from `typeof(Config).Assembly.GetName().Version?.ToString(3)`.
- The Updater parses both local and remote versions via `Version.TryParse` for comparison.

**To bump the version**: edit the `<Version>` tag in the csproj, commit with a descriptive message (the commit body becomes the release notes), and push to `main`.

## Release Process (CI/CD)

On push to `main`, `.github/workflows/release.yml` runs:

1. **Check version change** — compares `<Version>` in HEAD vs HEAD~1
2. **Build** — `dotnet build OrbSpoofer.slnx -c Release`
3. **Test** — `dotnet test OrbSpoofer.slnx -c Release --no-build`
4. **Release** (only if version changed):
   - `dotnet publish` the app as self-contained single-file
   - Generate body from `git show -s --format=%B HEAD` (the commit body becomes release notes)
   - `softprops/action-gh-release@v2` creates tag + release with the .exe

### Critical workflow details
- `fetch-depth: 0` — required so `git show HEAD~1:path` can access the parent commit's tree
- `permissions: contents: write` — required for `softprops/action-gh-release` to create releases
- Csproj path in git/commands: `OrbSpoofer/OrbSpoofer.csproj` (NOT `OrbSpoofer/OrbSpoofer/OrbSpoofer.csproj`)
- Release body comes from the **commit body**, not auto-generated notes. Write a proper commit body when bumping.

## Solution File (.slnx)

This project uses the new `.slnx` format (VS2022+ / .NET 8+). It's a clean XML format:

```xml
<Solution>
  <Project Path="src/ProjectName/ProjectName.csproj" />
  <Project Path="tests/ProjectName.Tests/ProjectName.Tests.csproj" />
</Solution>
```

Benefits: human-readable, merge-friendly, no VS-generated garbage.

## Auto-Update System

1. On launch, `MainWindow_Loaded` calls `Updater.CheckForUpdateAsync()`
2. `CheckForUpdateAsync` hits `https://api.github.com/repos/ZavalaSebas/OrbSpoofer/releases/latest`
3. Compares remote tag version vs `Config.AssemblyVersion`
4. If newer, finds the first `.exe` asset and returns download URL
5. `UpdateWindow` shows progress, calls `Updater.DownloadAndApplyUpdateAsync`
6. Download swaps: current.exe → current.exe.old, new → current.exe, starts new process, exits
7. On next launch, `Updater.CleanupOldExe()` deletes the `.old` file

### Important
- `NetworkHelper` **must** set `User-Agent` header — GitHub API returns 403 without it
- The HTTP client timeout is `Config.RequestTimeout` (10 seconds)
- Assembly version must match csproj `<Version>` or the update check will compare wrong values

## Tests (21 xUnit tests)

Run with: `dotnet test OrbSpoofer.slnx -c Release`

### DiscordDatabaseTests
- `SearchGames_EmptyQuery_ReturnsAllGames`
- `SearchGames_ValidQuery_ReturnsMatchingGames`
- `SearchGames_NoMatch_ReturnsEmptyList`
- `FilterWin32Exes_ExcludesKnownNonGameExes`
- Various edge cases for executable filtering

### SteamServiceTests
- `GenerateAppManifest_HasRequiredFields`
- `GenerateAppManifest_DepotsNotEmpty`
- `GenerateAppManifest_NoInvalidDepots`
- Manifests contain all expected keys (AppName, AppVersion, etc.)

### ConfigTests
- `AssemblyVersion_IsValidSemVer` — verifies format matches `x.y.z`
- `AssemblyVersion_MatchesCsprojVersion` — reads csproj and compares at build time

## Quest System

Quests are fetched from `api.discordquest.com/api/quests` (public, no auth required). Images are served from `cdn.discordapp.com`.

### Flow
1. `BtnQuests_Click` calls `LoadQuestsAsync()` which invokes `QuestService.GetActivePlayQuestsAsync()`
2. Response is filtered to `PLAY_ON_DESKTOP` type only; expired quests are excluded
3. Duplicates are removed by `GameName|QuestName` key
4. Each quest is cross-referenced against `DiscordDatabase.Games` by `application.id` — only spoofable games (those with a win32 executable) are kept
5. Promotional quests (`game_publisher = "Discord"`) are filtered out
6. Completed quest IDs are loaded from `completed_quests.json` and matched against loaded quests
7. Quests are sorted: active first (by expiry), completed last

### Completed Quests
- Toggle via circular button (32×32, `CornerRadius="16"`) next to the spoof button
- State persisted to `%LOCALAPPDATA%\OrbSpoofer/completed_quests.json` as a `HashSet<string>` of quest IDs
- Auto-completed when the quest timer finishes — `--quest-id` is passed through command-line args to the timer process, which saves the ID before shutdown
- Each tab click re-fetches from the API (quests are time-sensitive), re-applies completed state, and re-sorts

### UI
- QuestsView is the **default startup view** since v1.2.0
- If the quest API fails on first launch, the app silently falls back to Discord Database view
- If the user manually clicks Active Quests later and it fails, a "no quests found" message is shown
- Each quest card shows: game image (64×64), game name, quest name, reward, task minutes, expiry date, spoof button, and completion toggle
- Completed quests: card opacity 0.45, strikethrough on game/quest names, green filled circle with ✓
- Toggle animation: fade out → re-sort → staggered fade in
- `ListBoxItem` style for quests list overrides default selection/hover colors (no blue highlight)

### Config keys
- `QuestApiUrl` — `https://api.discordquest.com/api/quests`
- `DiscordCdnBase` — `https://cdn.discordapp.com/`
- `CompletedQuestsFile` — `"completed_quests.json"` (in `AppDataPath`)

## Game Image Resolution

`GameImageService` resolves game images for the Discord Database search results. It uses two sources in order:

1. **Discord CDN** — if the detectable games API returns an `icon` hash, constructs `cdn.discordapp.com/app-icons/{id}/{hash}.png`
2. **Steam Store fallback** — searches the Steam store API by game name, finds a matching app ID, and uses `steamcdn-a.akamaihd.net/steam/apps/{appid}/header.jpg`

Results are cached in a `ConcurrentDictionary` (memory-only, per session). Steam search results use their own `SteamGameDisplayItem.ImageUrl` pointing directly to Steam's CDN (`header.jpg` in a 92×48 container).

### Search debounce

Both Database and Steam search use a 150ms `DispatcherTimer` debounce. `SearchBox_TextChanged` and `SteamSearchBox_TextChanged` restart the timer on each keystroke. The actual search fires only after 150ms of inactivity, reducing per-keystroke allocations (list creation, LINQ queries, image resolution).

### CancellationToken for image resolution

Each call to `PerformDatabaseSearch` creates a new `CancellationTokenSource`. The previous one is cancelled and disposed before the new search starts. `ResolveGameImagesAsync` passes the token to `ParallelForEachAsync` via `ParallelOptions.CancellationToken`, so stale image resolution tasks stop immediately when a new search begins.

## Local Cache System

When both APIs (Discord and GitHub) are unavailable, OrbSpoofer falls back to locally cached data. This ensures the Database and Steam modes work offline.

### Cache files

All cache files are stored in `%LOCALAPPDATA%\OrbSpoofer/`:

| File | Source | Max size | TTL |
|------|--------|----------|-----|
| `db_cache.json` | Discord API / GitHub Gist | ~1-2MB | 30 days |
| `steam_ids.json` | GameImageService Steam lookups | ~5KB | 30 days |
| `steam_search/{query}.json` | Steam Store search results | ~2-5KB each | 30 days |
| `completed_quests.json` | User quest completion toggle | ~1KB | Never expires |

### Database cache flow

```
LoadAsync():
  1. Try Discord API → if OK, save JSON to db_cache.json
  2. Try GitHub Gist → if OK, save JSON to db_cache.json
  3. Read db_cache.json → if exists and < 30 days old, use it
  4. No cache → throw DatabaseLoadError
```

The cached JSON is the raw API response, re-parsed by `ParseGames()` on load. Cache age is checked via `FileInfo.LastWriteTime`.

### Steam search cache flow

```
SearchGamesAsync(query):
  1. Try Steam Store API → if OK, save results to steam_search/{query}.json
  2. On NetworkError → try reading steam_search/{query}.json
  3. No cache or expired → return empty list
```

Query names are sanitized (only letters, digits, spaces → underscores) for safe filenames.

### Steam AppID cache

`GameImageService` resolves Steam AppIDs for games without Discord icon hashes. This requires a Steam Store API call per game. The `SteamIdCache` (`ConcurrentDictionary<string, int?>`) persists to `steam_ids.json`:

- Loaded once via static constructor
- Protected by `lock (_steamIdLock)` for thread safety during parallel resolution
- Dirty flag (`_steamIdCacheDirty`) tracks unsaved changes
- Debounced save via `DispatcherTimer` (2s) — batches multiple rapid resolutions into a single disk write
- Flushes on app exit via `Application.Current.Exit` event to prevent data loss
- Avoids repeated API calls for the same game across sessions

### Config keys

```csharp
Config.DbCacheFile          // "db_cache.json"
Config.SteamIdCacheFile     // "steam_ids.json"
Config.SteamSearchCacheDir  // "steam_search"
Config.CompletedQuestsFile  // "completed_quests.json"
Config.MaxCacheAgeDays      // 30
```

### UI behavior

When using cache, the status bar shows a warning-colored message:
```
Database: Local Cache (30,412 games, 5d old)
```
When loaded from API, normal status:
```
Ready — 30,412 games loaded from Discord Official API
```

### What is NOT cached

- **Quest API** (`api.discordquest.com`) — quests are time-sensitive and change frequently, caching would show stale data
- **Image URLs** — Discord CDN URLs are deterministic (constructed from icon hash), no benefit from caching
- **Game images themselves** — only the URL strings are cached (in SteamIdCache), not the actual image files
- **Completed quests** — persisted permanently (never expires), not subject to cache TTL

## Welcome Sentinel

The welcome window respects a per-version flag. The sentinel file (`welcome.flag` in `%LOCALAPPDATA%\OrbSpoofer`) stores the app version that last dismissed it. `ShouldShow()` reads the file and compares against `Config.AssemblyVersion` — if the version differs, the welcome re-appears.

## GitHub Pages

The project website lives at `docs/index.html` and is deployed automatically by GitHub Pages on push to `main`. Configured in repo Settings > Pages > Source: "GitHub Actions". The site shows version, download link, and release info.

The CTA download button auto-updates its version text from the GitHub Releases API on page load. A hardcoded fallback (`v1.2.1`) is used if the API is unavailable. No manual version updates needed in the HTML.

## Known Issues & Resolutions

| Issue | Resolution |
|-------|------------|
| `Resource not accessible by integration` in release step | Add `permissions: contents: write` to workflow |
| `path does not exist in HEAD~1` | Use `fetch-depth: 0` (not 2) |
| Wrong csproj path (`OrbSpoofer/OrbSpoofer/...`) | Use `OrbSpoofer/OrbSpoofer.csproj` (one level) |
| GitHub API returns 403 on update check | Set `User-Agent` header on `HttpClient` |
| AssemblyVersion returns 1.0.0.0 | Add `<AssemblyVersion>$(Version).0</AssemblyVersion>` to csproj |
| Release body has no newlines or weird chars | Use `Out-String` or pipe directly to `Out-File` without byte conversion; avoid em dashes in commit message (use hyphen `-`) |

## Workflow Rules

These are strict rules that must always be followed:

1. **Never commit without asking first** — always confirm with the user before staging or committing any change.
2. **Never push without asking first** — pushing to remote requires explicit permission.
3. **Multiple commits are fine for progress**, but group them meaningfully when pushing. Don't push 100 tiny commits. Squash related work into logical commits with clear descriptions.
4. **Commit messages matter** — subject line ≤72 chars, body should describe exactly what was done and why. For version bumps, the body becomes the release notes, so write it with `### Added / Fixed / Changed` sections.
5. **Force push only for cleanup** — when squashing test commits or fixing history. Never force push over someone else's work. Always ask first.

Good commit structure:
```
bump v1.2.0

### Added
- New feature here

### Fixed
- Bug fix here

### Changed
- Breaking change here
```

## Key Files Quick Reference

| File | Purpose |
|------|---------|
| `OrbSpoofer/OrbSpoofer.csproj` | Version, target framework, assembly info |
| `OrbSpoofer/Config.cs` | All constants: URLs, paths, timeouts, cache settings |
| `OrbSpoofer/Services/Updater.cs` | Update check, download, swap, cleanup |
| `OrbSpoofer/Services/NetworkHelper.cs` | HTTP client with User-Agent, JSON fetching, file download |
| `.github/workflows/release.yml` | CI/CD: build → test → release on version bump |
| `OrbSpoofer.Tests/` | 21 xUnit tests |
