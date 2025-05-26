using System;
using DataDeveloper.Enums;
using DataDeveloper.ViewModels;
using ReactiveUI;

namespace DataDeveloper.Models;

public class ConnectionModel : ViewModelBase
{
    private string _name = string.Empty;

    public Guid Id { get; set; }
    public DatabaseType DatabaseType { get; set; } = DatabaseType.SqlServer;
    public string Name
    {
        get => _name;
        set => this.RaiseAndSetIfChanged(ref _name, value);
    }

    private string _server = string.Empty;
    public string Server
    {
        get => _server;
        set => this.RaiseAndSetIfChanged(ref _server, value);
    }

    private string _database = string.Empty;
    public string Database
    {
        get => _database;
        set => this.RaiseAndSetIfChanged(ref _database, value);
    }

    private string _user = string.Empty;
    public string User
    {
        get => _user;
        set => this.RaiseAndSetIfChanged(ref _user, value);
    }

    private string _password = string.Empty;
    public string Password
    {
        get => _password;
        set => this.RaiseAndSetIfChanged(ref _password, value);
    }
    
    public string ConnectionString =>
        $"Server={Server};Database={Database};User Id={User};Password={Password};TrustServerCertificate=True;";    
}