using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ReactiveUI;
using System.Reactive;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia;
using DataDeveloper.Core;
using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Interfaces;
using DataDeveloper.Data.Models;
using DataDeveloper.Data.Providers.Oracle;
using DataDeveloper.Data.Providers.MySql;
using DataDeveloper.Data.Providers.PostgresSql;
using DataDeveloper.Data.Providers.SqLite;
using DataDeveloper.Data.Providers.SqlServer;
using DataDeveloper.Data.Services;
using DataDeveloper.Enums;
using DataDeveloper.Interfaces;
using DataDeveloper.Services;
using DynamicData;

namespace DataDeveloper.ViewModels;

public sealed class ConnectionTypeFilterOption
{
    public ConnectionTypeFilterOption(string displayName, DatabaseType? databaseType)
    {
        DisplayName = displayName;
        DatabaseType = databaseType;
    }

    public string DisplayName { get; }
    public DatabaseType? DatabaseType { get; }
    public bool HasDatabaseType => DatabaseType is not null;
}

public sealed class SqlServerAuthenticationOption
{
    public SqlServerAuthenticationOption(string displayName, SqlServerAuthenticationMode value)
    {
        DisplayName = displayName;
        Value = value;
    }

    public string DisplayName { get; }
    public SqlServerAuthenticationMode Value { get; }
}

public class ConnectionSelectorViewModel : ViewModelBase
{
    private readonly IConnectionSettingsRepository _connectionSettingsRepository;
    private readonly DatabaseProviderFactoryService _databaseProviderFactoryService;
    private readonly ISecretStore _secretStore;
    private readonly IDialogService _dialogService;
    private readonly ObservableCollection<ConnectionSettings> _allConnections = new();
    private readonly ObservableCollection<string> _availableDatabaseNames = new();

    public ConnectionSelectorViewModel(IConnectionSettingsRepository connectionSettingsRepository, DatabaseProviderFactoryService databaseProviderFactoryService, ISecretStore secretStore, IDialogService dialogService)
    {
        _connectionSettingsRepository = connectionSettingsRepository;
        _databaseProviderFactoryService = databaseProviderFactoryService;
        _secretStore = secretStore;
        _dialogService = dialogService;
        AvailableConnectionFilters =
        [
            new ConnectionTypeFilterOption("(All)", null),
            new ConnectionTypeFilterOption("SQL Server", DatabaseType.SqlServer),
            new ConnectionTypeFilterOption("Oracle", DatabaseType.Oracle),
            new ConnectionTypeFilterOption("PostgreSQL", DatabaseType.PostgresSql),
            new ConnectionTypeFilterOption("MySQL", DatabaseType.MySql),
            new ConnectionTypeFilterOption("SQLite", DatabaseType.SqLite)
        ];
        SqlServerAuthenticationModes =
        [
            new SqlServerAuthenticationOption("SQL Server Authentication", SqlServerAuthenticationMode.SqlLogin),
            new SqlServerAuthenticationOption("Windows Authentication", SqlServerAuthenticationMode.WindowsIntegrated)
        ];
        AvailableDatabaseNames = new ReadOnlyObservableCollection<string>(_availableDatabaseNames);
        LoadConnections();
        SelectedConnectionFilter = AvailableConnectionFilters[0];
        
        AddCommand = ReactiveCommand.Create(() =>
        {
            if (SelectedConnectionFilter?.DatabaseType is not DatabaseType databaseType)
                return;

            var newConn = CreateConnectionSettings(databaseType);
            _allConnections.Add(newConn);
            RefreshFilteredConnections();
            SelectedConnection = newConn;
            IsEditing = true;
        });

        ApplyCommand = ReactiveCommand.CreateFromTask<StyledElement>(ApplyOnlyAsync,
            this.WhenAnyValue(x => x.IsEditing)
        );

        EditCommand = ReactiveCommand.CreateFromTask<ConnectionSettings>(
            async connectionModel =>
            {
                IsEditing = true;
                if (connectionModel is not null)
                    await Task.Run(() => _connectionSettingsRepository.LoadPassword(connectionModel));
                SelectedConnection = connectionModel;
            }
        );

        DeleteCommand = ReactiveCommand.CreateFromTask<StyledElement>(DeleteAsync);
        
        TestCommand = ReactiveCommand.CreateFromTask<StyledElement>(TestConnection,
            this.WhenAnyValue(x => x.SelectedConnection).Select(conn => conn is not null)
        );

        OkCommand = ReactiveCommand.CreateFromTask<StyledElement>(OkAsync,
            this.WhenAnyValue(x => x.SelectedConnection).Select(conn => conn is not null)
        );

        CancelCommand = ReactiveCommand.Create<StyledElement>(CancelAsync);
        DuplicateConnectionCommand = ReactiveCommand.CreateFromTask(DuplicateConnectionAsync,
            this.WhenAnyValue(x => x.SelectedConnection).Select(conn => conn is not null)
        );
        SelectSqLiteFileCommand = ReactiveCommand.CreateFromTask(
            SelectSqLiteFileAsync,
            this.WhenAnyValue(x => x.SelectedConnection, x => x.IsSqLiteConnectionSelected, x => x.IsEditing,
                (connection, isSqLite, isEditing) => connection is not null && isSqLite && isEditing));
        CreateSqLiteFileCommand = ReactiveCommand.CreateFromTask(
            CreateSqLiteFileAsync,
            this.WhenAnyValue(x => x.SelectedConnection, x => x.IsSqLiteConnectionSelected, x => x.IsEditing,
                (connection, isSqLite, isEditing) => connection is not null && isSqLite && isEditing));
        RefreshDatabaseNamesCommand = ReactiveCommand.CreateFromTask(
            RefreshDatabaseNamesAsync,
            this.WhenAnyValue(x => x.SelectedConnection, x => x.CanRefreshDatabaseNames, x => x.IsEditing,
                (connection, canRefresh, isEditing) => connection is not null && canRefresh && isEditing));

        this.WhenAnyValue(x => x.SelectedConnection)
            .Select(connection => connection is SqlServerConnectionSettings sqlServer
                ? sqlServer.WhenAnyValue(x => x.AuthenticationMode)
                : Observable.Return(SqlServerAuthenticationMode.SqlLogin))
            .Switch()
            .Subscribe(_ =>
            {
                this.RaisePropertyChanged(nameof(UsesCredentialsForSelectedConnection));
                this.RaisePropertyChanged(nameof(SelectedSqlServerAuthenticationOption));
            });

        this.WhenAnyValue(x => x.SelectedConnection)
            .Subscribe(connection =>
            {
                LoadAvailableDatabaseNames(GetCurrentDatabaseName(connection));
                this.RaisePropertyChanged(nameof(CanRefreshDatabaseNames));
            });
    }

    private async Task DuplicateConnectionAsync()
    {
        if (SelectedConnection is null)
            return;

        await Task.Run(() => _connectionSettingsRepository.LoadPassword(SelectedConnection));
        ConnectionSettings? duplicate = SelectedConnection.DatabaseType switch
        {
            DatabaseType.SqlServer => (ConnectionSettings?)SelectedConnection.Map<SqlServerConnectionSettings>(),
            DatabaseType.Oracle => (ConnectionSettings?)SelectedConnection.Map<OracleConnectionSettings>(),
            DatabaseType.PostgresSql => (ConnectionSettings?)SelectedConnection.Map<PostgresConnectionSettings>(),
            DatabaseType.MySql => (ConnectionSettings?)SelectedConnection.Map<MySqlConnectionSettings>(),
            DatabaseType.SqLite => (ConnectionSettings?)SelectedConnection.Map<SqLiteConnectionSettings>(),
            _ => null
        };
        if (duplicate == null) throw new Exception("Type not recognized");
        duplicate.Name = $"Copy of {duplicate.Name}";
        duplicate.Id = Guid.NewGuid();
        duplicate.CredentialId = null;
        duplicate.LoadedPasswordSnapshot = null;
        _allConnections.Add(duplicate);
        RefreshFilteredConnections();
        SelectedConnection = duplicate;
        IsEditing = true;
    }

    private async Task TestConnection(StyledElement element)
    {
        if (SelectedConnection is null)
            return;

        await Task.Run(() => _connectionSettingsRepository.LoadPassword(SelectedConnection));
        var databaseProvider = _databaseProviderFactoryService.GetDatabaseProvider(SelectedConnection);
        var result = databaseProvider.TestConnection();
        await _dialogService.ShowDialogAsync(
            result.Success ? result.ResultMessage : $"Could not connect to database\r\n\r\n{result.ResultMessage}",
            "Connection...",
            DialogButtons.Ok,
            result.Success ? DialogIcon.Success : DialogIcon.Error);
    }

    private void CancelAsync(StyledElement element)
    {
        var window = element.GetParentWindow();
        window?.Close();
    }

    private async Task DeleteAsync(StyledElement element)
    {
        var connectionModel = element.DataContext as ConnectionSettings;
        
        IsEditing = true; 
        SelectedConnection = connectionModel;

        var result = await _dialogService.ShowDialogAsync(
            "Are you sure to delete this connection?",
            "Connection...",
            DialogButtons.YesNo,
            DialogIcon.Question);

        if (result == DialogResult.Yes)
        {
            if (connectionModel is not null)
            {
                await Task.Run(() => _connectionSettingsRepository.Delete(connectionModel));
                _allConnections.Remove(connectionModel);
                RefreshFilteredConnections();
            }

            SelectedConnection = Connections.FirstOrDefault();
            IsEditing = false;
        }
    }

    public ObservableCollection<ConnectionSettings> Connections { get; private set; } = new();
    public ReadOnlyObservableCollection<string> AvailableDatabaseNames { get; }
    public IReadOnlyList<ConnectionTypeFilterOption> AvailableConnectionFilters { get; }
    public IReadOnlyList<SqlServerAuthenticationOption> SqlServerAuthenticationModes { get; }

    private ConnectionSettings? _selectedConnection;
    public ConnectionSettings? SelectedConnection
    {
        get => _selectedConnection;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedConnection, value);
            this.RaisePropertyChanged(nameof(IsMySqlConnectionSelected));
            this.RaisePropertyChanged(nameof(IsOracleConnectionSelected));
            this.RaisePropertyChanged(nameof(IsPostgresConnectionSelected));
            this.RaisePropertyChanged(nameof(IsSqLiteConnectionSelected));
            this.RaisePropertyChanged(nameof(IsSqlServerConnectionSelected));
            this.RaisePropertyChanged(nameof(IsPortConnectionSelected));
            this.RaisePropertyChanged(nameof(IsServerConnectionSelected));
            this.RaisePropertyChanged(nameof(UsesCredentials));
            this.RaisePropertyChanged(nameof(ShowsSecurityOptions));
            this.RaisePropertyChanged(nameof(DatabaseLabel));
            this.RaisePropertyChanged(nameof(UsesSqlServerAuthenticationMode));
            this.RaisePropertyChanged(nameof(UsesCredentialsForSelectedConnection));
            this.RaisePropertyChanged(nameof(SelectedSqlServerAuthenticationOption));
        }
    }

    private ConnectionTypeFilterOption? _selectedConnectionFilter;
    public ConnectionTypeFilterOption? SelectedConnectionFilter
    {
        get => _selectedConnectionFilter;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedConnectionFilter, value);
            RefreshFilteredConnections();
            this.RaisePropertyChanged(nameof(CanAddConnection));
            this.RaisePropertyChanged(nameof(ShowFilterSelectionHint));
        }
    }

    private bool _isEditing;
    public bool IsEditing
    {
        get => _isEditing;
        set => this.RaiseAndSetIfChanged(ref _isEditing, value);
    }    

    public bool IsMySqlConnectionSelected => SelectedConnection?.DatabaseType == DatabaseType.MySql;
    public bool IsOracleConnectionSelected => SelectedConnection?.DatabaseType == DatabaseType.Oracle;
    public bool IsPostgresConnectionSelected => SelectedConnection?.DatabaseType == DatabaseType.PostgresSql;
    public bool IsSqLiteConnectionSelected => SelectedConnection?.DatabaseType == DatabaseType.SqLite;
    public bool IsSqlServerConnectionSelected => SelectedConnection?.DatabaseType == DatabaseType.SqlServer;
    public bool IsPortConnectionSelected =>
        SelectedConnection?.DatabaseType is DatabaseType.MySql or DatabaseType.PostgresSql or DatabaseType.Oracle;
    public bool IsServerConnectionSelected => SelectedConnection?.DatabaseType != DatabaseType.SqLite;
    public bool UsesCredentials => SelectedConnection?.DatabaseType != DatabaseType.SqLite;
    public bool UsesSqlServerAuthenticationMode => SelectedConnection?.DatabaseType == DatabaseType.SqlServer;
    public bool UsesCredentialsForSelectedConnection =>
        SelectedConnection switch
        {
            SqlServerConnectionSettings sqlServer => sqlServer.AuthenticationMode == SqlServerAuthenticationMode.SqlLogin,
            null => false,
            _ => UsesCredentials
        };
    public bool ShowsSecurityOptions => SelectedConnection?.DatabaseType is DatabaseType.SqlServer or DatabaseType.PostgresSql or DatabaseType.MySql;
    public bool CanRefreshDatabaseNames => SelectedConnection?.DatabaseType is DatabaseType.SqlServer or DatabaseType.MySql or DatabaseType.PostgresSql;
    public string DatabaseLabel => SelectedConnection?.DatabaseType switch
    {
        DatabaseType.Oracle => "Service Name",
        DatabaseType.SqLite => "Database File",
        _ => "Database"
    };
    public bool IsSecureStorageUnavailable => !_secretStore.IsAvailable;
    public string SecureStorageWarningMessage => _secretStore.UnavailableReason ?? string.Empty;
    public bool CanAddConnection => SelectedConnectionFilter?.DatabaseType is not null;
    public bool ShowFilterSelectionHint => !CanAddConnection;
    public SqlServerAuthenticationOption? SelectedSqlServerAuthenticationOption
    {
        get => SelectedConnection is SqlServerConnectionSettings sqlServer
            ? SqlServerAuthenticationModes.FirstOrDefault(option => option.Value == sqlServer.AuthenticationMode)
            : null;
        set
        {
            if (SelectedConnection is not SqlServerConnectionSettings sqlServer || value is null)
                return;

            sqlServer.AuthenticationMode = value.Value;
        }
    }
    
    public SqlConnectionInfo? ConnectionInfo { get; private set; }

    public ReactiveCommand<StyledElement, Unit> OkCommand { get; }
    public ReactiveCommand<Unit, Unit> AddCommand { get; }
    public ReactiveCommand<ConnectionSettings, Unit> EditCommand { get; }
    public ReactiveCommand<StyledElement, Unit> ApplyCommand { get; }
    public ReactiveCommand<StyledElement, Unit> TestCommand { get; }
    public ReactiveCommand<StyledElement, Unit> CancelCommand { get; }
    public ReactiveCommand<StyledElement, Unit> DeleteCommand { get; }
    public ReactiveCommand<Unit, Unit> DuplicateConnectionCommand { get; }
    public ReactiveCommand<Unit, Unit> SelectSqLiteFileCommand { get; }
    public ReactiveCommand<Unit, Unit> CreateSqLiteFileCommand { get; }
    public ReactiveCommand<Unit, Unit> RefreshDatabaseNamesCommand { get; }

    private async Task OkAsync(StyledElement element)
    {
        var applied = await ApplyAsync(element);
        if (!applied || SelectedConnection is null)
            return;

        await Task.Run(() => _connectionSettingsRepository.LoadPassword(SelectedConnection));
        var window = element.GetParentWindow();
        window?.Close(SelectedConnection);
    }

    private async Task ApplyOnlyAsync(StyledElement element)
    {
        await ApplyAsync(element);
    }

    private async Task<bool> ApplyAsync(StyledElement element)
    {
        if (SelectedConnection == null)
        {
            await _dialogService.ShowDialogAsync("There is no connection selected to save!", "Connection...");
            return false;
        }

        SanitizeAuthenticationSensitiveFields(SelectedConnection);
        await Task.Run(() => _connectionSettingsRepository.LoadPassword(SelectedConnection));

        if (!_secretStore.IsAvailable && !string.IsNullOrWhiteSpace(SelectedConnection.Password))
        {
            await _dialogService.ShowDialogAsync(
                $"{_secretStore.UnavailableReason}\n\nThe connection can be saved only after secure credential storage is available or the password field is left blank.",
                "Secure storage unavailable",
                DialogButtons.Ok,
                DialogIcon.Warning);
            return false;
        }

        await SaveConnectionAsync(SelectedConnection);
        return true;
    }

    private async Task SaveConnectionAsync(ConnectionSettings connectionSettings)
    {
        await Task.Run(() => _connectionSettingsRepository.Save(connectionSettings));
        
        IsEditing = false;

        SortAllConnections();
        RefreshFilteredConnections();
        SelectedConnection = connectionSettings;
    }

    private void LoadConnections()
    {
        var sortedList = _connectionSettingsRepository.LoadAll();
        _allConnections.Clear();
        if (sortedList is not null)
            _allConnections.AddRange(sortedList.OrderBy(s => s.Name));
        RefreshFilteredConnections();
    }

    private void SortAllConnections()
    {
        var sortedConnections = _allConnections.OrderBy(connection => connection.Name).ToList();
        _allConnections.Clear();
        _allConnections.AddRange(sortedConnections);
    }

    private void RefreshFilteredConnections()
    {
        var selectedConnectionId = SelectedConnection?.Id;
        var filteredConnections = _allConnections
            .Where(connection => SelectedConnectionFilter?.DatabaseType is null || connection.DatabaseType == SelectedConnectionFilter.DatabaseType)
            .OrderBy(connection => connection.Name)
            .ToList();

        Connections.Clear();
        Connections.AddRange(filteredConnections);

        if (selectedConnectionId is null)
        {
            if (SelectedConnection is null || !Connections.Contains(SelectedConnection))
                SelectedConnection = Connections.FirstOrDefault();
            return;
        }

        SelectedConnection = Connections.FirstOrDefault(connection => connection.Id == selectedConnectionId) ?? Connections.FirstOrDefault();
    }

    private static ConnectionSettings CreateConnectionSettings(DatabaseType databaseType)
    {
        return databaseType switch
        {
            DatabaseType.SqlServer => new SqlServerConnectionSettings
            {
                Id = Guid.NewGuid(),
                Name = "New SQL Server connection",
                Server = "",
                Database = "",
                User = "",
                Password = "",
                Encrypt = true,
                TrustServerCertificate = false,
                DatabaseType = DatabaseType.SqlServer
            },
            DatabaseType.Oracle => new OracleConnectionSettings
            {
                Id = Guid.NewGuid(),
                Name = "New Oracle connection",
                Server = "",
                Database = "",
                User = "",
                Password = "",
                Port = 1521,
                Encrypt = false,
                TrustServerCertificate = false,
                DatabaseType = DatabaseType.Oracle
            },
            DatabaseType.PostgresSql => new PostgresConnectionSettings
            {
                Id = Guid.NewGuid(),
                Name = "New PostgreSQL connection",
                Server = "",
                Database = "",
                User = "",
                Password = "",
                Port = 5432,
                Encrypt = false,
                TrustServerCertificate = false,
                DatabaseType = DatabaseType.PostgresSql
            },
            DatabaseType.MySql => new MySqlConnectionSettings
            {
                Id = Guid.NewGuid(),
                Name = "New MySQL connection",
                Server = "",
                Database = "",
                User = "",
                Password = "",
                Port = 3306,
                Encrypt = false,
                TrustServerCertificate = true,
                DatabaseType = DatabaseType.MySql
            },
            DatabaseType.SqLite => new SqLiteConnectionSettings
            {
                Id = Guid.NewGuid(),
                Name = "New SQLite connection",
                Database = "",
                User = "",
                Password = "",
                Encrypt = false,
                TrustServerCertificate = false,
                DatabaseType = DatabaseType.SqLite
            },
            _ => throw new NotSupportedException($"Database type {databaseType} is not supported.")
        };
    }

    private static void SanitizeAuthenticationSensitiveFields(ConnectionSettings connectionSettings)
    {
        if (connectionSettings is not SqlServerConnectionSettings sqlServer ||
            sqlServer.AuthenticationMode != SqlServerAuthenticationMode.WindowsIntegrated)
            return;

        sqlServer.User = string.Empty;
        sqlServer.Password = string.Empty;
        sqlServer.IsPasswordLoaded = true;
        sqlServer.LoadedPasswordSnapshot = string.Empty;
    }

    private async Task SelectSqLiteFileAsync()
    {
        if (SelectedConnection is not SqLiteConnectionSettings sqLiteConnection)
            return;

        var selectedPath = await _dialogService.ShowOpenDatabaseFileAsync("Select SQLite database file...");
        if (!string.IsNullOrWhiteSpace(selectedPath))
            sqLiteConnection.Database = selectedPath;
    }

    private async Task CreateSqLiteFileAsync()
    {
        if (SelectedConnection is not SqLiteConnectionSettings sqLiteConnection)
            return;

        var suggestedName = string.IsNullOrWhiteSpace(sqLiteConnection.Name)
            ? "database.db"
            : $"{sqLiteConnection.Name}.db";
        var createdPath = await _dialogService.ShowCreateDatabaseFileAsync(suggestedName, "Create SQLite database file...");
        if (!string.IsNullOrWhiteSpace(createdPath))
            sqLiteConnection.Database = createdPath;
    }

    private async Task RefreshDatabaseNamesAsync()
    {
        if (SelectedConnection is null)
            return;

        try
        {
            await Task.Run(() => _connectionSettingsRepository.LoadPassword(SelectedConnection));
            SanitizeAuthenticationSensitiveFields(SelectedConnection);
            var databaseProvider = _databaseProviderFactoryService.GetDatabaseProvider(SelectedConnection);
            var names = await Task.Run(() => databaseProvider.GetAvailableDatabaseNames());
            LoadAvailableDatabaseNames(GetCurrentDatabaseName(SelectedConnection), names);
        }
        catch (Exception ex)
        {
            await _dialogService.ShowDialogAsync(
                $"Could not load databases\r\n\r\n{ex.Message}",
                "Connection...",
                DialogButtons.Ok,
                DialogIcon.Error);
        }
    }

    private void LoadAvailableDatabaseNames(string? currentDatabase, IEnumerable<string>? names = null)
    {
        _availableDatabaseNames.Clear();

        var values = (names ?? Enumerable.Empty<string>())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var name in values)
            _availableDatabaseNames.Add(name);

        if (!string.IsNullOrWhiteSpace(currentDatabase) &&
            !_availableDatabaseNames.Contains(currentDatabase, StringComparer.OrdinalIgnoreCase))
        {
            _availableDatabaseNames.Insert(0, currentDatabase);
        }
    }

    private static string? GetCurrentDatabaseName(ConnectionSettings? connectionSettings) =>
        connectionSettings switch
        {
            SqlServerConnectionSettings sqlServer => sqlServer.Database,
            MySqlConnectionSettings mySql => mySql.Database,
            PostgresConnectionSettings postgres => postgres.Database,
            OracleConnectionSettings oracle => oracle.Database,
            SqLiteConnectionSettings sqLite => sqLite.Database,
            _ => null
        };
}
