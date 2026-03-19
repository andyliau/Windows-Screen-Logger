# Squad Team

> Windows Screen Logger — periodic desktop screenshot capture for Windows 10+

## Coordinator

| Name | Role | Notes |
|------|------|-------|
| Squad | Coordinator | Routes work, enforces handoffs and reviewer gates. Does not generate domain artifacts. |

## Members

| Name | Role | Charter | Status |
|------|------|---------|--------|
| Nova | Tech Lead | `.squad/agents/nova/charter.md` | ✅ Active |
| Kai | C# Developer | `.squad/agents/kai/charter.md` | ✅ Active |
| Sage | QA Engineer | `.squad/agents/sage/charter.md` | ✅ Active |
| Scribe | Session Logger | `.squad/agents/scribe/charter.md` | 📋 Silent |

## Coding Agent

<!-- copilot-auto-assign: false -->

| Name | Role | Charter | Status |
|------|------|---------|--------|
| @copilot | Coding Agent | — | 🤖 Coding Agent |

### Capabilities

**🟢 Good fit — auto-route when enabled:**
- Bug fixes with clear reproduction steps
- Test coverage (adding missing tests, fixing flaky tests)
- Lint/format fixes and code style cleanup
- Dependency updates and version bumps
- Small isolated features with clear specs
- Boilerplate/scaffolding generation
- Documentation fixes and README updates

**🟡 Needs review — route to @copilot but flag for squad member PR review:**
- Medium features with clear specs and acceptance criteria
- Refactoring with existing test coverage
- New service classes following established patterns
- Migration scripts with well-defined schemas

**🔴 Not suitable — route to squad member instead:**
- Architecture decisions and system design
- Windows API integration requiring deep platform knowledge
- Ambiguous requirements needing clarification
- Security-sensitive changes
- Performance-critical paths (screen capture, memory management)
- Changes requiring cross-agent coordination

## Project Context

- **Owner:** andyliau
- **Stack:** C# 13, .NET 9.0, WinForms, SkiaSharp, System.CommandLine, System.Text.Json, xUnit
- **Platform:** Windows 10+ (22000), self-contained single-file deployment (win-x64)
- **Description:** Lightweight system-tray app that periodically captures desktop screenshots, with configurable interval, quality, size, and automatic cleanup.
- **Created:** 2026-03-19
