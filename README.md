# DataDeveloper
A desktop SQL workspace focused on database exploration, query authoring, and result analysis.

DataDeveloper currently supports SQL Server, Oracle, MySQL, PostgreSQL, and SQLite, with provider-aware behavior across connection management, schema browsing, SQL completion, query execution, and result visualization.

## Features

- Manage saved database connections for SQL Server, Oracle, MySQL, PostgreSQL, and SQLite
- Configure Oracle connections with server, port, service name, and credentials, or point SQLite connections to a local database file
- Store saved connections in a local app-state database
- Store connection credentials using the operating system secure storage
- Browse database schema from a dedicated explorer panel
- Open multiple query editors per connection
- Execute SQL statements and inspect results in tabbed result views
- Use provider-aware SQL completion for tables and columns
- Navigate query results with the custom `NextGrid` control
- Detect named SQL parameters and fill them through a side panel in the editor
- Select ranges, rows, columns, and copy results to the clipboard
- Use context menus in the schema explorer to copy names and open SQL templates for tables, views, procedures, and functions
- Resize columns and navigate results with keyboard shortcuts
- Track query execution and previous-result cleanup from the status bar
- Run on macOS, Windows, and Linux

## Releases

- GitHub Releases: https://github.com/NelsonSantos/DataDeveloper/releases
