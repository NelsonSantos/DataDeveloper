
using System.Text.Json.Serialization;

public class SqlConnectionInfo
{
    [JsonPropertyName("server")]
    public string Server { get; set; } = string.Empty;

    [JsonPropertyName("database")]
    public string Database { get; set; } = string.Empty;

    [JsonPropertyName("user")]
    public string User { get; set; } = string.Empty;

    [JsonPropertyName("password")]
    public string Password { get; set; } = string.Empty;

    [JsonPropertyName("encrypt")]
    public bool Encrypt { get; set; } = true;

    [JsonPropertyName("trustServerCertificate")]
    public bool TrustServerCertificate { get; set; }

    public string ConnectionString =>
        $"Server={Server};Database={Database};User Id={User};Password={Password};Encrypt={Encrypt};TrustServerCertificate={TrustServerCertificate};";
}
