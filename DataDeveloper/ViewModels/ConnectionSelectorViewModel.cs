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
using DataDeveloper.Data.Providers.MySql;
using DataDeveloper.Data.Providers.SqlServer;
using DataDeveloper.Data.Services;
using DataDeveloper.Enums;
using DataDeveloper.Interfaces;
using DataDeveloper.Services;
using DynamicData;

namespace DataDeveloper.ViewModels;

public class ConnectionSelectorViewModel : ViewModelBase
{
    private readonly IConnectionSettingsRepository _connectionSettingsRepository;
    private readonly DatabaseProviderFactoryService _databaseProviderFactoryService;
    private readonly ISecretStore _secretStore;
    private readonly IDialogService _dialogService;

    public ConnectionSelectorViewModel(IConnectionSettingsRepository connectionSettingsRepository, DatabaseProviderFactoryService databaseProviderFactoryService, ISecretStore secretStore, IDialogService dialogService)
    {
        _connectionSettingsRepository = connectionSettingsRepository;
        _databaseProviderFactoryService = databaseProviderFactoryService;
        _secretStore = secretStore;
        _dialogService = dialogService;
        LoadConnections();
        SelectedDatabaseType = DatabaseType.SqlServer;
        
        AddCommand = ReactiveCommand.Create(() =>
        {
            var newConn = CreateConnectionSettings(SelectedDatabaseType);
            Connections.Add(newConn);
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
    }

    private async Task DuplicateConnectionAsync()
    {
        if (SelectedConnection is null)
            return;

        await Task.Run(() => _connectionSettingsRepository.LoadPassword(SelectedConnection));
        ConnectionSettings? duplicate = SelectedConnection.DatabaseType switch
        {
            DatabaseType.SqlServer => (ConnectionSettings?)SelectedConnection.Map<SqlServerConnectionSettings>(),
            DatabaseType.MySql => (ConnectionSettings?)SelectedConnection.Map<MySqlConnectionSettings>(),
            _ => null
        };
        if (duplicate == null) throw new Exception("Type not recognized");
        duplicate.Name = $"Copy of {duplicate.Name}";
        duplicate.Id = Guid.NewGuid();
        duplicate.CredentialId = null;
        duplicate.LoadedPasswordSnapshot = null;
        Connections.Add(duplicate);
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
                Connections.Remove(connectionModel);
            }

            SelectedConnection = Connections.FirstOrDefault();
            IsEditing = false;
        }
    }

    public ObservableCollection<ConnectionSettings> Connections { get; private set; } = new();
    public IReadOnlyList<DatabaseType> AvailableDatabaseTypes { get; } = [DatabaseType.SqlServer, DatabaseType.MySql];

    private ConnectionSettings? _selectedConnection;
    public ConnectionSettings? SelectedConnection
    {
        get => _selectedConnection;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedConnection, value);
            if (value is not null)
                SelectedDatabaseType = value.DatabaseType;
            this.RaisePropertyChanged(nameof(IsMySqlConnectionSelected));
            this.RaisePropertyChanged(nameof(IsSqlServerConnectionSelected));
        }
    }

    private DatabaseType _selectedDatabaseType;
    public DatabaseType SelectedDatabaseType
    {
        get => _selectedDatabaseType;
        set => this.RaiseAndSetIfChanged(ref _selectedDatabaseType, value);
    }

    private bool _isEditing;
    public bool IsEditing
    {
        get => _isEditing;
        set => this.RaiseAndSetIfChanged(ref _isEditing, value);
    }    

    public bool IsMySqlConnectionSelected => SelectedConnection?.DatabaseType == DatabaseType.MySql;
    public bool IsSqlServerConnectionSelected => SelectedConnection?.DatabaseType == DatabaseType.SqlServer;
    public bool IsSecureStorageUnavailable => !_secretStore.IsAvailable;
    public string SecureStorageWarningMessage => _secretStore.UnavailableReason ?? string.Empty;
    
    public SqlConnectionInfo? ConnectionInfo { get; private set; }

    public ReactiveCommand<StyledElement, Unit> OkCommand { get; }
    public ReactiveCommand<Unit, Unit> AddCommand { get; }
    public ReactiveCommand<ConnectionSettings, Unit> EditCommand { get; }
    public ReactiveCommand<StyledElement, Unit> ApplyCommand { get; }
    public ReactiveCommand<StyledElement, Unit> TestCommand { get; }
    public ReactiveCommand<StyledElement, Unit> CancelCommand { get; }
    public ReactiveCommand<StyledElement, Unit> DeleteCommand { get; }
    public ReactiveCommand<Unit, Unit> DuplicateConnectionCommand { get; }

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

        var sortedConnections = Connections.OrderBy(connection => connection.Name).ToList();
        Connections.Clear();
        Connections.AddRange(sortedConnections);
        SelectedConnection = connectionSettings;
    }

    private void LoadConnections()
    {
        var sortedList = _connectionSettingsRepository.LoadAll();
        Connections.Clear();
        if (sortedList is not null)
            Connections.AddRange(sortedList.OrderBy(s => s.Name));
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
            _ => throw new NotSupportedException($"Database type {databaseType} is not supported.")
        };
    }
}
