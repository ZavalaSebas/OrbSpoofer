# Changelog

All notable changes to this project are documented in this file.

## [2.0.2] — 2026-08-28

### Changed

- **UI polish — darker, sharper, more spacious:** window `1000×700 → 1060×740`, header/sidebar darker (`#0A0A0A`/`#0F0F11`/`#18181C`), card `CornerRadius 10→8` + tighter `Margin 0,0,16,4` + `Padding 13`, compact rows (`52×52` image, `44×44` play) across Quests/Search/Database/Steam/Manual
- **Icons filled:** `Play24` + `Heart24` now `Filled="True"` for solid look
- **Readability:** Active Quests `GameName 13→15 / QuestName 11→13 / Reward 10→12` in `SuccessBrush` (`#57F287`), unified Search/DB/Steam to `14/11` — less dense, easier to scan
- **Background:** content area `Transparent → #08080A` overlay `0.85` to remove Mica gray wash on cards

## [2.0.1] — 2026-08-25

### Fixed

- **Manual mode button not working** — `ManualViewModel` now calls `NotifyCanExecuteChanged` when `ExeName` changes so `SpoofCommand` enables correctly

## [2.0.0] — 2026-08-25

### Added

- **Fluent UI 2.0** — full migration to WPF-UI 4.3: `FluentWindow`, `Mica` backdrop, `TitleBar` on 6 windows, `SymbolIcon`, `ProgressRing`, `InfoBar`, `Orb.*` tokens and `ContentDialogHost` for native dialogs
- **MVVM architecture** — `CommunityToolkit.Mvvm` + `Microsoft.Extensions.DependencyInjection`: `MainViewModel` shell + 6 VMs (`Quests`, `Unified`, `Database`, `Steam`, `Manual`, `FreeGames`), 6 `Views` UserControls, `IDialogService` and `ICollectionView` with `SortDescriptions` without recreating collection
- **Free Games** — section that auto-detects free games on Steam and Epic via GamerPower API, badge with counter, anchored popup and seen persistence (`free-games-seen.json`)
- **Centralized search** — `UnifiedSearch` as main view with badges `Discord`/`Steam`/`Both`/`DLC`/`Manual`, 150ms debounce and `Parallel.ForEachAsync` for images
- **Personalization** — accent picker with 8 presets (Blurple, Red, Green...) in header, persisted in `theme.json` and applied live with `DynamicResource` + `RefreshWindow`
- **Infrastructure** — centralized `Directory.Packages.props`/`Directory.Build.props`, `SettingsStoreBase` for `completed_quests`/`created-manifests`/`free-games-seen`, `ApplicationSingleInstance` (mutex), `PathContainment` to sanitize `bin/helldivers2.exe`, typed `HttpClient` with `IHttpClientFactory`
- **Unified cache** — `Infrastructure/Cache/CacheStore` with 30d TTL, atomic writes and expired cleanup; `GameImageService` now persists `image_urls.json` to disk in addition to `steam_ids.json`

### Changed

- `MainWindow` 1227→66L and 992→252L — shell delegates to VMs, code-behind only for animations and popup positioning
- `DarkTheme.xaml` cleaned: removed dead `PrimaryButton`/`GhostButton`/`SearchBox`, now uses `ui:Button`/`ui:TextBox`
- `ThemeManager` now saves accent and refreshes live windows; `App` migrates `%LOCALAPPDATA%/OrbSpoofer` with `AppDataMigrator` and validates single instance
- `SteamService` sanitizes `installDir`/`executable` (`:` → `_`) for `Call of Duty: Modern Warfare 4` and DLCs, detects `type=="DLC"` and uses `parent` (`Call of Duty HQ`) for manifest
- Free Games price now strikethrough (`$29.99`) + `$0` in green to emphasize free

### Fixed

- **HELLDIVERS 2** (`bin/helldivers2.exe`) not detected due to subdirectory strip in `GameFaker` — now preserves `bin/` via `PathContainment`
- **Call of Duty: Modern Warfare 4** (`4435490` DLC) `Failed to launch Steam spoof` due to `:` in `installdir` — sanitized and resolved via parent
- **GamerPower** `No URL for Bolmn` — `JsonOptions` now `SnakeCaseLower` for `gamerpower_url`/`open_giveaway_url`
- **Claim double open** — removed duplicate `Process.Start` fallback in `MainWindow`
- **Pinned popup** — `StaysOpen`→`False` + `LocationChanged`/`Deactivated` now close and `Claim` uses `Tag` instead of broken `RelativeSource` in `Popup`
- **Timer** `ExitCode` 0/1, `bat` with `timeout`/`rmdir /s /q`, `DeleteTrackedManifests` immediate and on app close (kills orphan timers), `Run All` `AllowConcurrentExecutions` for Stop button and cancels without marking as completed if closed manually
- **Quests** `HasNoQuests` inverted and `IsLoading` collapsed list, DLC badge at end

## [1.2.4] — 2026-08-21

### Added

- Unified Search — one search bar merges Discord Database and Steam Store results
- Source badges on each result: Discord, Steam, or Both (one row per game, never duplicated)
- Advanced sidebar — Database, Steam Quest, and Manual modes are still available when you need a specific source
- Automatic Steam spoof for quests that have a Steam SKU but no Discord executable
- Steam AppID parsed from Discord `third_party_skus` for routing and matching

### Changed

- Search is now the primary fallback when Active Quests are empty (was Discord Database)
- Clicking ▶ on a Steam-only quest goes straight to Steam mode instead of showing a dead-end dialog
- Steam metadata parsing: missing `config` blocks no longer fail, nested install paths are joined correctly, anti-cheat launchers and redistributable depots are skipped

### Fixed

- Steam-only games such as MARVEL Tokon now use the real shipping executable and game depot instead of Easy Anti-Cheat or a redistributable

## [1.2.3] — 2026-07-30

### Fixed

- Quest auto-complete on timer expiry — save was deferred via `Dispatcher.BeginInvoke` causing the quest to not be marked as completed
- Fake executable cleanup on timer close — process cannot delete itself while running; now uses a helper batch file that waits for the process to exit before deleting
- Quest list not reflecting completed state after timer closes — main app now watches `completed_quests.json` for changes via `FileSystemWatcher` and refreshes automatically

### Changed

- README: unified Ko-fi URL to match project config
- DEVELOPMENT.md: removed reusable template section, documented code conventions, added status bar sponsor heart, updated push rule to include local verification
- CHANGELOG.md: fixed future-tense boilerplate

## [1.2.2] — 2026-07-22

### Added

- Quest filtering by game name — quests now match via name when Discord app ID isn't in the database
- `NeedsSteamMode` indicator on quest cards — warns when a quest requires Steam Quest mode
- `InfoDialog` — custom themed dialog shown when a game has no executable in Discord's database
- Manual mode placeholder text and button enable/disable via `TextChanged` event

### Changed

- Steam Quest mode status message now warns about inconsistency and suggests DB mode as fallback

### Fixed

- `InstalledDepots` section in appmanifest now correctly populated (was always empty)
- Steam Quest mode null-check for missing executable before proceeding

## [1.2.1] — 2026-07-22

### Added

- Mark quests as completed — circular toggle next to each quest card, persisted to `completed_quests.json`
- Completed quests sort to the bottom of the list automatically
- Auto-complete: when the quest timer finishes, the quest is automatically marked as completed
- Fade animation when toggling completion (fade out → re-sort → staggered fade in)
- Quests re-fetch from API on every tab click (no more stale data)

### Changed

- Quest list now sorts by completion status (active first, completed last) then by expiry
- `GameFaker.LaunchExecutable` now accepts optional `questId` parameter
- Timer process receives `--quest-id` argument for auto-completion on finish
- `ListBoxItem` style for quests list overrides default selection/hover colors (no blue highlight)

### Fixed

- ListBoxItem hover/selection showing blue highlight on quest cards
- Toggle click event not firing — switched to `PreviewMouseLeftButtonDown`

## [1.2.0] — 2026-07-21

### Added

- Active Quests panel — fetches live quests from api.discordquest.com, filters PLAY_ON_DESKTOP, deduplicates by GameName|QuestName, cross-references detectable games DB
- GameImageService — resolves game images from Discord CDN (icon hash) or Steam Store search (header.jpg)
- Publisher-filter: promotional quests (published by Discord) are automatically excluded
- Welcome window now version-aware — re-appears after each app update
- **Local cache system** — app works offline when APIs are unavailable:
  - Database cache: saves Discord API JSON response to `db_cache.json` after each successful load
  - Steam search cache: saves per-query results to `steam_search/{query}.json`
  - Steam AppID cache: persists `steam_ids.json` to avoid re-resolving Steam AppIDs
  - Cache expires after 30 days (`Config.MaxCacheAgeDays`)
  - Status bar shows "Local Cache (X games, Yd old)" in warning color when using cache
- Search debounce (150ms) for both Database and Steam search to reduce per-keystroke allocations
- CancellationToken support for image resolution — stale requests cancelled on new search
- Website auto-updates CTA download button version from GitHub Releases API

### Changed

- Active Quests is now the default startup view; auto-fallback to Discord Database if the API fails
- Database and Steam search results now show 92×48 game images (header art / icons)
- Animation: staggered fade-in (60ms stagger, 0.35s duration), hidden before render to eliminate flash
- TimerWindow displays the spoofed game name via --game-name argument
- TimerWindow now centers on screen instead of top-right corner
- Credits XAML: cleaned up {"\n"} artifacts, proper Run + LineBreak
- Sidebar: Active Quests moved to top of the mode list
- `DiscordDatabase.LoadAsync()` now has 3 fallbacks: Discord API → GitHub Gist → local cache
- `SteamService.SearchGamesAsync()` falls back to cached search results when API fails
- `GameImageService.SteamIdCache` now persists to disk across sessions with debounced writes
- `DiscordGame` pre-computes `NameLower`/`AliasesLower` for faster search
- `ResolveGameImagesAsync` now runs in parallel (max 5 concurrent)
- `PreloadAsync` now limited to 5 concurrent requests
- Website How it works section updated for Active Quests flow
- Website features, structured data, and meta description updated for v1.2.0

### Fixed

- Credits animation: cards now start at Opacity=0 before appearing
- InvalidCastException in animation RenderTransform — now creates fresh transforms per item
- CS4014 warnings — discard Dispatcher.BeginInvoke returns
- Status bar color not resetting after cache warning when clearing search
- Steam search not clearing results when input is emptied
- Quest matching O(n*m) → HashSet O(1) for spoofable game lookup
- SaveSteamIdCache race condition — lock + dirty flag + DispatcherTimer 2s debounce prevents file corruption
- PickWindowsExe missing TryGetProperty checks for malformed JSON safety
- CancellationTokenSource properly disposed before creating new one to prevent leak
- GameImageService flushes SteamIdCache on app exit via Application.Current.Exit
- Duplicated Process.Start code extracted into OpenUrl helper

## [1.1.0] — 2026-07-21

### Added

- Auto-update system with custom UpdateWindow UI and persistent sidebar reminder
- Welcome popup on first launch with app info, what's new, and support link
- Staggered entry animations for search results and credits view
- Timer completion sound and window activation on finish
- Startup cleanup for orphaned fake executables
- Timer includes a 30-second grace period after the visible countdown
- GitHub Sponsors support link
- Debug logging to all silent catch blocks

### Changed

- Moved display models (GameDisplayItem, SteamGameDisplayItem) to Models/
- Progress reporting convention changed from 0-100 to 0.0-1.0 in NetworkHelper
- SteamService class marked as static
- Prevented double Cleanup execution in TimerWindow

### Fixed

- Empty catch blocks now log with Debug.WriteLine instead of swallowing exceptions

## [1.0.0] — 2026-07-21

### Added

- Initial release
- Discord Database mode with smart search
- Manual mode for custom executable names
- Steam Quest mode with appmanifest generation
- 15-minute quest timer window
- Dark native WPF UI
- GitHub-based update checker
- Ko-fi support link
