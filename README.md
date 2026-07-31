# DataDeveloper
A desktop SQL workspace focused on database exploration, query authoring, and result analysis.

DataDeveloper currently supports SQL Server, Oracle, MySQL, PostgreSQL, and SQLite, with provider-aware behavior across connection management, schema browsing, SQL completion, query execution, and result visualization.

## Features

- Manage saved database connections for SQL Server, Oracle, MySQL, PostgreSQL, and SQLite
- Connect to SQL Server with SQL login or Windows Authentication, including LocalDB instances such as `(localdb)\MSSQLLocalDB`
- Load visible database names for SQL Server, MySQL, and PostgreSQL directly from the connection dialog while still allowing manual entry
- Configure Oracle connections with server, port, service name, and credentials, or point SQLite connections to a local database file
- Store saved connections in a local app-state database
- Store connection credentials using the operating system secure storage
- Browse database schema from a dedicated explorer panel
- Open multiple query editors per connection
- Execute SQL statements and inspect results in tabbed result views
- Configure DML transaction mode per connection, using either auto commit or manual commit/rollback
- Use provider-aware SQL completion for tables, columns, and common SQL functions
- View column data types and function return details directly in completion suggestions
- See function parameter hints while writing function calls in the SQL editor
- Navigate query results with the custom `NextGrid` control
- Detect named SQL parameters and fill them through a side panel in the editor
- Select ranges, rows, columns, and copy results to the clipboard
- Use context menus in the schema explorer to copy names and open SQL templates for tables, views, procedures, and functions
- Import data from CSV, XLS, and XLSX files through a step-by-step wizard, either into a new table or mapped into an existing one
- Export a query result grid to CSV or XLSX, remembering the last format used per connection
- Resize columns and navigate results with keyboard shortcuts
- Track query execution and previous-result cleanup from the status bar
- Run on macOS, Windows, and Linux

## Releases

- GitHub Releases: https://github.com/NelsonSantos/DataDeveloper/releases

## License

DataDeveloper is licensed under the GNU General Public License v3.0. See [LICENSE](LICENSE).

## Installation

Download the latest package for your operating system from the GitHub Releases page.

### macOS

- Download the `.zip` file for your Mac (the file name includes the release version, for example `DataDeveloper-26.0731.0-osx-arm64.zip`):
  - `DataDeveloper-<version>-osx-arm64.zip` for Apple Silicon
  - `DataDeveloper-<version>-osx-x64.zip` for Intel
- Extract the archive.
- Move `DataDeveloper.app` to `Applications`.
- Open the app from `Applications`.

If macOS blocks the app because it was downloaded from the internet, open it from Finder with `Open` and confirm the prompt.
If needed, allow it in `System Settings > Privacy & Security`.

### Windows

- Download `DataDeveloper-<version>-win-x64-setup.exe` (the file name includes the release version, for example `DataDeveloper-26.0731.0-win-x64-setup.exe`).
- Run the installer.
- Complete the setup wizard.
- Launch DataDeveloper from the Start menu or desktop shortcut, depending on the installer options you selected.

### Linux

- Download `DataDeveloper-<version>-linux-x64.AppImage` (the file name includes the release version, for example `DataDeveloper-26.0731.0-linux-x64.AppImage`).
- Make the file executable:

```bash
chmod +x DataDeveloper-<version>-linux-x64.AppImage
```

- Run the AppImage:

```bash
./DataDeveloper-<version>-linux-x64.AppImage
```

If your distribution requires FUSE support for AppImage, install the appropriate package first.

## Disclaimer

DataDeveloper is free and open source software.

It is provided "as is", without warranties or guarantees of any kind, express or implied, including but not limited to merchantability, fitness for a particular purpose, and noninfringement.

You are responsible for reviewing, validating, and safely using the software in your own environment, including any interaction with databases, credentials, schema changes, and executed SQL statements.
