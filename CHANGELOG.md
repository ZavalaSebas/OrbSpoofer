# Changelog

All notable changes to this project will be documented in this file.

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
