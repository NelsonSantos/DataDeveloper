# DataDeveloper
A desktop SQL workspace focused on database exploration, query authoring, and result analysis.

DataDeveloper currently supports SQL Server, MySQL, and PostgreSQL, with provider-aware behavior across connection management, schema browsing, SQL completion, query execution, and result visualization.

## Features

- Manage saved database connections for SQL Server, MySQL, and PostgreSQL
- Store saved connections in a local app-state database
- Store connection credentials using the operating system secure storage
- Browse database schema from a dedicated explorer panel
- Open multiple query editors per connection
- Execute SQL statements and inspect results in tabbed result views
- Use provider-aware SQL completion for tables and columns
- Navigate query results with the custom `NextGrid` control
- Detect `@parameters` in SQL and fill them through a side panel in the editor
- Select ranges, rows, columns, and copy results to the clipboard
- Use context menus in the schema explorer to copy names and open SQL templates for tables, views, procedures, and functions
- Resize columns and navigate results with keyboard shortcuts
- Track query execution and previous-result cleanup from the status bar
- Run on macOS, Windows, and Linux

## Local PostgreSQL

Run a local PostgreSQL instance with Docker:

```bash
docker run --name datadeveloper-postgres \
  -e POSTGRES_USER=datadeveloper \
  -e POSTGRES_PASSWORD=datadeveloper \
  -e POSTGRES_DB=datadeveloper \
  -p 5432:5432 \
  -v postgres_data:/var/lib/postgresql/data \
  -d postgres:16
```

Connection defaults:

- Server: `localhost`
- Database: `datadeveloper`
- User: `datadeveloper`
- Password: `datadeveloper`
- Port: `5432`

## Releases

- GitHub Releases: https://github.com/NelsonSantos/DataDeveloper/releases
