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

## Provider compatibility
- Starting from the `feature/mysql` branch, every new feature must support both SQL Server and MySQL.
- Do not introduce new SQL Server-only behavior without handling the MySQL equivalent, or without explicitly documenting and approving the limitation.
- When implementing provider-dependent parsing, completion, navigation, execution, or UI behavior, validate the impact on both providers.
- When adding database-dependent behavior tests, cover both SQL Server and MySQL whenever applicable.
- For every new provider added in the future, implement provider tests from the start.
- New providers must include, at minimum, unit coverage for provider factory wiring, connection settings serialization, schema SQL, and completion/execution behavior where applicable.
- When feasible, add opt-in integration tests for each new provider using real configured connections, following the same pattern used for SQL Server and MySQL.

## Documentation
- When adding a new database provider or significant provider capability, update `README.md` in the same branch.
- Provider documentation updates must cover supported providers and any local setup needed to validate the new provider during development.
- Keep `README.md` user-focused. Do not add internal workflows, local-only test instructions, release mechanics, or other team-facing technical details there unless the user explicitly asks for that content in `README.md`.
- Put team-facing technical documentation such as local validation steps, integration test setup, and internal workflows in dedicated files like `TESTS.md` unless the user explicitly asks for a different location.
