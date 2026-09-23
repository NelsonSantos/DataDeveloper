# Release Notes — Unreleased

## Summary
- **#44 fix: keep routine bodies intact when they contain CASE expressions** (https://github.com/NelsonSantos/DataDeveloper/pull/44)
  - `StatementSplitter` tracked `BEGIN`/`END` to avoid splitting routine bodies on `;`, but the `END` of a `CASE ... END` expression also closed the block. The body was then cut at the next `;`, so SQL Server received a truncated `CREATE PROCEDURE` (e.g. `Incorrect syntax near '@pointOfSaleId'`).
  - `CASE` now opens a block closed by its `END` (including `END CASE`).
  - `END IF` / `END LOOP` / `END WHILE` / `END REPEAT` no longer close an outer `BEGIN` (MySQL/Oracle).
  - `BEGIN TRY/CATCH ... END TRY/CATCH` keep working.

## Included Commits
- c14c5a8 Merge pull request #44 from NelsonSantos/feature/splitter-case-end
