# Insight Window TODO

## Function catalog

- Expand the provider-specific function catalog beyond the initial common functions.
- Add provider tests that assert representative functions for SQL Server, Oracle, PostgreSQL, MySQL, and SQLite.
- Keep function metadata provider-specific when names, signatures, or behavior differ.

## Return types

- Replace broad inferred return types with explicit function metadata where possible.
- Handle argument-dependent return types for functions such as `CAST`, `CONVERT`, `COALESCE`, `MIN`, `MAX`, `NVL`, and `IFNULL`.
- Keep the completion row detail short, for example `returns date/time` or `returns target type`.

## Overloads

- Model multiple signatures per function.
- Update the `IOverloadProvider` implementation to expose and navigate real overloads.
- Add tests for overload selection and active argument highlighting.

## Completion and insight window coordination

- Revisit whether `CompletionWindow` and `InsightWindow` can be displayed at the same time without overlap.
- Prefer public AvaloniaEdit APIs if a future version exposes positioning controls for insight windows.
- Keep the current fallback behavior: close parameter insight while completion is open, then reopen it when completion closes and the caret is still inside a known function call.

## Column and expression types

- Keep showing real table column data types in completion rows using schema metadata.
- Investigate type inference for CTE columns and projected expressions.
- Avoid adding partial type inference unless it works consistently across supported providers.
