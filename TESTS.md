# Tests

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
