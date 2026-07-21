# Changelog

All notable changes to this project will be documented in this file.

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
