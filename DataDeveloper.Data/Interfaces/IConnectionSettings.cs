using DataDeveloper.Data.Enums;

namespace DataDeveloper.Data.Interfaces;

public interface IConnectionSettings
{
    Guid Id { get; set; }
    string Name { get; set; }
    string User { get; set; }
    string Password { get; set; }
    bool UseTrustedConnection { get; set; }
    bool AllowBlankPassword { get; set; }
    DatabaseType DatabaseType { get; set; }
}