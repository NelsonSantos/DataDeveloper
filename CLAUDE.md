# DataDeveloper

## Completion rules
- The `CompletionWindow` should prioritize only database objects and columns; do not list keywords for now.
- After `SELECT`, `WHERE`, `GROUP BY`, `ORDER BY`, `SET`, and `alias.`, completion should focus on columns.
- After `FROM`, `JOIN`, `UPDATE`, `INTO`, and commas in those contexts, completion should focus on database objects.
- In `INSERT INTO table (...)`, `(` and `,` inside the column list should reopen columns from the target table.
- In `INSERT ... VALUES (...)`, completion must not suggest columns just because `(` was typed.
- A space after a popup opened by a valid context may keep/reopen completion; ordinary spaces should not open the popup.

## Tests
- Completion context rules must have unit tests before larger changes.
- Always run `dotnet test` after changes to the completion provider.
- When fixing a completion regression, add a test covering the specific case.
- When running `dotnet build`, `dotnet test`, or other validation commands for `DataDeveloper`, run them sequentially, not in parallel, to avoid wasting time on reruns caused by concurrent build/test interference.

## Provider compatibility
- Every new feature must support all currently supported database providers, unless the limitation is explicitly documented and approved.
- Do not introduce behavior that works for only a subset of supported providers without handling the remaining providers, or without explicitly documenting and approving the limitation.
- When implementing provider-dependent parsing, completion, navigation, execution, or UI behavior, validate the impact on all supported providers.
- When adding database-dependent behavior tests, cover all supported providers whenever applicable.
- For every new provider added in the future, implement provider tests from the start.
- New providers must include, at minimum, unit coverage for provider factory wiring, connection settings serialization, schema SQL, and completion/execution behavior where applicable.
- When feasible, add opt-in integration tests for each supported provider using real configured connections.

## Documentation
- When adding a new database provider or significant provider capability, update `README.md` in the same branch.
- Provider documentation updates must cover supported providers and any local setup needed to validate the new provider during development.
- Keep `README.md` user-focused. Do not add internal workflows, local-only test instructions, release mechanics, or other team-facing technical details there unless the user explicitly asks for that content in `README.md`.
- Put team-facing technical documentation such as local validation steps, integration test setup, and internal workflows in dedicated files like `TESTS.md` unless the user explicitly asks for a different location.

## Git safety
- When asked to delete a branch, delete the local branch only by default.
- Do not delete remote branches on GitHub unless the user explicitly asks to remove the remote branch too.
- Every change must be made on its own branch named `feature/<feature-name>`. Do not commit or push directly to `main`.

## Releases
- Use `scripts/create-release.sh <version>` to cut a release (e.g. `./scripts/create-release.sh 26.0731.0`). It creates and pushes the version tag, waits for the GitHub Release to be published, and updates its notes.
- Do not trigger the `release.yml` workflow via `workflow_dispatch` (`gh workflow run release.yml ...`) directly. Its manual-dispatch path checks out the version tag before that tag exists, so every build fails at checkout. Only the tag-push path (which `create-release.sh` drives) works.
- Must be run from `main` with a clean working tree.

## Parser architecture
- `DataDeveloper.Antlr` is the single owner of ANTLR grammars and checked-in generated parser/lexer code.
- Keep authoritative parser sources under `DataDeveloper.Antlr/Generated` and `DataDeveloper.Antlr/Support`. Do not add or restore parser copies under `DataDeveloper.Data`.
- Do not add `Compile Include` links for generated parser files into `DataDeveloper`, `DataDeveloper.Data`, or test projects. Parser consumers must reference `DataDeveloper.Antlr`.
- Do not make normal solution builds depend on `Antlr4BuildTasks`, Java, or on-the-fly parser generation. Commit the generated parser changes under `DataDeveloper.Antlr/Generated` and keep build consumption purely .NET.
- Do not keep multiple authoritative copies of generated parser code across projects. If support files such as `*Base.cs`, listeners, or helper enums are needed, ensure `DataDeveloper.Antlr` compiles them and other projects consume that assembly.
- Keep provider parser architecture consistent across all supported databases. Do not let SQL Server, MySQL, PostgreSQL, Oracle, and SQLite follow different ownership or build patterns.
- When refactoring parsing logic away from regex/manual code, prefer `DataDeveloper.Antlr` outputs when the grammar already covers the scenario, and add/adjust tests before wider adoption.

## Platform guidelines
- UI behavior must follow the conventions of the operating system currently running the app.
- Keyboard shortcuts, menu gesture labels, context menu behavior, window actions, and similar UX details must use the platform-appropriate conventions instead of hardcoded Windows behavior.
- On macOS, prefer `Command`-based shortcuts and macOS menu expectations; on Windows/Linux, prefer the native conventions for those platforms.

## UI styles
- Reuse shared Avalonia styles for repeated UI patterns instead of duplicating visual properties inline on each control.
- Dialog and action buttons should prefer the shared dialog button styles (for example `dialog-button` and `dialog-button.primary`) unless there is a clear reason to diverge.
- When a visual pattern starts being reused across windows, dialogs, or menus, promote it to a shared style in `App.axaml` or another dedicated theme/style file.
