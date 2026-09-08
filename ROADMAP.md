# Roadmap

Direction for OrbSpoofer. Checked = shipped, unchecked = planned.

## Shipped ✅

- [x] Hall of Fame / Special Thanks + Thanks Trang! pill (2.1.3)
- [x] Landing page visual overhaul (2.1.3)
- [x] All quest types (play/stream/video/activity) + region badges + Discord deep link
- [x] Accent color applies app-wide (theme token migration)
- [x] **Light / Dark theme toggle (☀️/🌙)** — full palette swap, persisted, all views migrated
- [x] Sidebar collapse fix (single-source column animation, no jumps or stuck states)

## Planned

### Settings (big)
- [x] Settings view in sidebar (`settings.json`): preferred region (filters quests live), quest-link target (app vs browser), token storage + official-API toggle (reserved), alert preferences (reserved). Theme/accent stay in the header
- [ ] Base for token / region / notification features below

### Quests
- [x] Token mode (official Discord API): personalized list via `GET /quests/@me` (fixes mirror lag like the Roblox / Dragon's Dogma 2 cases), with token check + mirror fallback
- [x] Video auto-accept + auto-watch (per-quest chip + bulk run, view-paced progress, 429 backoff, local completion marking)
- [x] New-quest watcher (polling + sidebar badge + auto-reload + orbs-only filter)
- [x] Claim button with done/left counts (no interrupting dialogs)
- [x] Quest list filters (type chips with counts, counts follow visible set)
- [x] Quest list grouped by type with dividers
- [x] Open Discord button with play done/left counts (replaces interrupting dialogs)
- [x] Danger zone: informed consent (5s read + 4 confirmations), Auto UI gated behind it
- [x] Compact quests header (info banner folded into tooltip, empty TitleBar text)
- [ ] Orb reward badge with Nitro 1.2x multiplier note (paused)

### Polish
- [x] Release 2.2.0 (version bump + WhatsNew + push)
- [x] 3.0 visual foundation (tokens, shell, section headings, mission radar, contextual pills, empty states)

### Epic (future major)
- [ ] Full 3.0 redesign / rebrand (name, logo, palette, landing) + separate announcement if it grows
- [ ] No embedded VPN — region viewing is already solved via the global mirror list; enrollment is server-side
