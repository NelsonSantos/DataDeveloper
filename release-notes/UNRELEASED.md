# Release Notes — Unreleased

## Summary
- **#57 chore: migrate to .NET 10** (https://github.com/NelsonSantos/DataDeveloper/pull/57)
  - Migrates the solution from .NET 8 to .NET 10 (LTS). .NET 8 support ends on 2026-11-10, and .NET 10 is also the recommended target for the upcoming Avalonia 12 migration.
- **#58 chore: migrate to Avalonia 12 and ReactiveUI 24** (https://github.com/NelsonSantos/DataDeveloper/pull/58)
  - Upgrades the UI stack to Avalonia 12.1 (Avalonia 11 is moving to maintenance) together with the ReactiveUI 24 line required by the Avalonia 12 integration package.
- **#59 feat: enable compiled bindings with x:DataType across views** (https://github.com/NelsonSantos/DataDeveloper/pull/59)
  - Enables Avalonia 12 compiled bindings app-wide (deferred from #58). Bindings are now type-checked at build time and no longer resolved through reflection at runtime.

## Included Commits
- 00df4a0 Merge pull request #59 from NelsonSantos/feature/compiled-bindings
- 98316e6 Merge pull request #58 from NelsonSantos/feature/avalonia-12
- f195a95 Merge pull request #57 from NelsonSantos/feature/dotnet-10
