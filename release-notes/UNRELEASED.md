# Release Notes — Unreleased

## Summary
- **#57 chore: migrate to .NET 10** (https://github.com/NelsonSantos/DataDeveloper/pull/57)
  - Migrates the solution from .NET 8 to .NET 10 (LTS). .NET 8 support ends on 2026-11-10, and .NET 10 is also the recommended target for the upcoming Avalonia 12 migration.
- **#58 chore: migrate to Avalonia 12 and ReactiveUI 24** (https://github.com/NelsonSantos/DataDeveloper/pull/58)
  - Upgrades the UI stack to Avalonia 12.1 (Avalonia 11 is moving to maintenance) together with the ReactiveUI 24 line required by the Avalonia 12 integration package.
- **#59 feat: enable compiled bindings with x:DataType across views** (https://github.com/NelsonSantos/DataDeveloper/pull/59)
  - Enables Avalonia 12 compiled bindings app-wide (deferred from #58). Bindings are now type-checked at build time and no longer resolved through reflection at runtime.
- **#60 feat: adopt Dock for connection and query tabs** (https://github.com/NelsonSantos/DataDeveloper/pull/60)
  - Replaces the connection and query `TabControl`s with [wieslawsoltes/Dock](https://github.com/wieslawsoltes/Dock) 12.1. Dock is used only for the workspace docked in the main window; dialogs and secondary windows (Table Designer, connection selector) keep Avalonia tabs.
- **#61 feat: add Ctrl+Tab query switcher** (https://github.com/NelsonSantos/DataDeveloper/pull/61)
  - Ctrl+Tab now follows recent use, like Rider and VS Code. A quick Ctrl+Tab returns to the previously used query (and again back); holding Ctrl opens a switcher listing the open connections and the highlighted connection's queries, most recent first.
- **#62 fix: keep the app running when the database becomes unreachable** (https://github.com/NelsonSantos/DataDeveloper/pull/62)
  - Losing the database connection while using the app (typically the VPN dropping) could crash it: nothing handled errors escaping ReactiveUI commands, async event handlers or the UI thread. They are now logged and shown to the user, and the app keeps running.

## Included Commits
- d1e3c23 Merge pull request #62 from NelsonSantos/feature/crash-resilience
- cf35ef1 Merge pull request #61 from NelsonSantos/feature/query-switcher
- 44834b8 Merge pull request #60 from NelsonSantos/feature/dock-evaluation
- 00df4a0 Merge pull request #59 from NelsonSantos/feature/compiled-bindings
- 98316e6 Merge pull request #58 from NelsonSantos/feature/avalonia-12
- f195a95 Merge pull request #57 from NelsonSantos/feature/dotnet-10
