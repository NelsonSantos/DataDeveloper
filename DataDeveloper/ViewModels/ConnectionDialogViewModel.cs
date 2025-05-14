using System.Collections.Generic;
using ReactiveUI;
using System.Reactive;
using System.Text.Json;
using System.IO;
using Avalonia.Controls;
using System.Threading.Tasks;
using Avalonia.Controls.ApplicationLifetimes;
using DataDeveloper.Views;

namespace DataDeveloper.ViewModels;

public class ConnectionDialogViewModel : ViewModelBase
{
    private const string FilePath = "connections.json";

    public string Server { get; set; } = "localhost";
    public string Database { get; set; } = "master";
    public string User { get; set; } = "sa";
    public string Password { get; set; } = "";

    public SqlConnectionInfo? ConnectionInfo { get; private set; }

    public ReactiveCommand<Unit, Unit> ConfirmCommand { get; }

    public ConnectionDialogViewModel()
    {
        ConfirmCommand = ReactiveCommand.CreateFromTask(ConfirmConnectionAsync);
    }

    public async void OpenDialog()
    {
        var dialog = new ConnectionDialog();
        var result = await dialog.ShowDialog<SqlConnectionInfo>(App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop ? desktop.MainWindow : null);

        if (result != null)
        {
            ConnectionInfo = result;
            SaveConnection(result);
        }
    }

    private Task ConfirmConnectionAsync()
    {
        ConnectionInfo = new SqlConnectionInfo
        {
            Server = Server,
            Database = Database,
            User = User,
            Password = Password
        };

        SaveConnection(ConnectionInfo);

        var window = App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop ? desktop.Windows[0] as Window : null;
        window?.Close(ConnectionInfo);
        return Task.CompletedTask;
    }

    private void SaveConnection(SqlConnectionInfo info)
    {
        var list = new List<SqlConnectionInfo>();

        if (File.Exists(FilePath))
        {
            var content = File.ReadAllText(FilePath);
            var loaded = JsonSerializer.Deserialize<List<SqlConnectionInfo>>(content);
            if (loaded != null)
                list = loaded;
        }

        list.Add(info);
        var json = JsonSerializer.Serialize(list);
        File.WriteAllText(FilePath, json);
    }
}
