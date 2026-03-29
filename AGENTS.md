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
