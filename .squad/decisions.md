# Squad Decisions

## Active Decisions

### 2026-03-19: Settings.Default deprecated, AppConfiguration is authoritative
**By:** Nova
**What:** Settings.Default (ApplicationSettingsBase) is fully removed. AppConfiguration (JSON at %APPDATA%\WindowsActivityLogger\config.json) is the single source of truth for all user configuration. No code should reference Settings.Default.
**Why:** Settings.Default and AppConfiguration were running in parallel, causing every user settings change to be silently discarded on restart. The JSON-backed AppConfiguration is more testable, portable, and explicit.

---

### 2026-03-19: ILogger/DefaultLogger is the single logging abstraction
**By:** Nova
**What:** AppLogger (static class) is deleted. All logging goes through ILogger (interface) backed by DefaultLogger. DefaultLogger is created in Program.cs, initialized twice (early bootstrap with Debug level, then re-initialized with config values after AppConfiguration loads), and injected everywhere via constructor or SetLogger().
**Why:** Two parallel loggers caused all service-layer log output to be silently dropped in production (DefaultLogger was never initialized). Single abstraction eliminates the split.

## Governance

- All meaningful changes require team consensus
- Document architectural decisions here
- Keep history focused on work, decisions focused on direction
