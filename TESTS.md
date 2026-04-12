# Tests

## Parser Architecture

- `DataDeveloper.Antlr` is the only project that may own ANTLR grammars and checked-in generated parser/lexer code.
- Authoritative parser sources live under `DataDeveloper.Antlr/Generated` and `DataDeveloper.Antlr/Support`. Do not reintroduce parser copies under `DataDeveloper.Data`.
- Do not compile parser source files from other projects via `Compile Include` links. Consumers must reference `DataDeveloper.Antlr`.
- If a grammar needs manual support files such as `*Base.cs`, listeners, or helper enums, keep a single authoritative copy in `DataDeveloper.Antlr/Support`.
- Do not introduce provider-specific parser ownership patterns. All supported providers must follow the same pipeline: grammar/support in `DataDeveloper.Antlr`, consumption via project reference.
- Normal solution builds must not depend on `Antlr4BuildTasks`, Java, or on-the-fly parser generation. If grammar output changes, update the checked-in files under `DataDeveloper.Antlr/Generated` in the same branch.
- When changing grammars or parser support files, run focused tests for all affected parsing consumers before larger changes.

Current parser layout:

- Grammars: `DataDeveloper.Antlr/Antlr`
- Checked-in generated parser sources: `DataDeveloper.Antlr/Generated`
- Manual parser support files: `DataDeveloper.Antlr/Support`
- Consumer projects: reference `DataDeveloper.Antlr`; they must not compile parser source files directly

Recommended validation after parser pipeline changes:

```bash
dotnet build DataDeveloper.Antlr/DataDeveloper.Antlr.csproj
dotnet build DataDeveloper/DataDeveloper.csproj
dotnet test DataDeveloper.Tests/DataDeveloper.Tests.csproj --filter "StatementSplitterTests|SqlParameterDetectorTests"
dotnet test DataDeveloper.Tests/DataDeveloper.Tests.csproj --filter "SqlCompletionProviderTests|ProviderSqlAnalyzerTests"
```

## Integration Tests

The repository includes opt-in integration tests that exercise the supported providers against real local databases.

Start the test databases:

```bash
docker compose -f docker/integration/docker-compose.integration.yml up -d
```

Run the integration suite:

```bash
RUN_DB_INTEGRATION_TESTS=1 dotnet test DataDeveloper.Tests/DataDeveloper.Tests.csproj
```

SQLite integration coverage uses a temporary local database file and does not require Docker.
