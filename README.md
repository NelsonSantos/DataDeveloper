# DataDeveloper
a SQL Statement manager for DBMS SQL Server (for now)

## macOS release

To generate a self-contained macOS release with `.app`:

```bash
./scripts/build-macos-release.sh
```

For another runtime:

```bash
./scripts/build-macos-release.sh osx-arm64
```

Output:

- `artifacts/macos/<rid>/DataDeveloper.app`
- `artifacts/macos/<rid>/DataDeveloper-<rid>.zip`

## Next steps

- [ ] generate and validate the `osx-arm64` release as well
- [ ] add universal packaging, if it makes sense to distribute a single app for Intel and Apple Silicon
- [ ] configure signing with `Developer ID Application` instead of ad-hoc signing
- [ ] configure Apple notarization to avoid Gatekeeper blocking on other machines
- [ ] validate app launch and execution on a clean macOS machine without the .NET SDK installed
- [ ] decide whether distribution will be via `.zip`, `.dmg`, or both
- [ ] automate the release build in CI, if it makes sense
