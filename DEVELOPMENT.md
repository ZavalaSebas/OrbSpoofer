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
│   │   └── GameFaker.cs           # Fake game process spawning
│   ├── Models/
│   │   └── GameDisplayItem.cs     # Display models for game list
│   └── UI/Windows/
│       ├── UpdateWindow.xaml.cs   # Download progress UI
│       └── WelcomeWindow.xaml.cs  # First-launch welcome + What's New
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
<Version>1.1.0</Version>
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

## GitHub Pages

The project website lives at `docs/index.html` and is deployed automatically by GitHub Pages on push to `main`. Configured in repo Settings > Pages > Source: "GitHub Actions". The site shows version, download link, and release info.

When bumping the version, update `docs/index.html` with the new version number and release URL.

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
| `OrbSpoofer/Config.cs` | All constants: URLs, paths, timeouts |
| `OrbSpoofer/Services/Updater.cs` | Update check, download, swap, cleanup |
| `OrbSpoofer/Services/NetworkHelper.cs` | HTTP client with User-Agent, JSON fetching, file download |
| `.github/workflows/release.yml` | CI/CD: build → test → release on version bump |
| `OrbSpoofer.Tests/` | 21 xUnit tests |
