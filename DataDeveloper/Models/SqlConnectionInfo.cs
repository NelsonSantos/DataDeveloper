
using System.Text.Json.Serialization;

public class SqlConnectionInfo
{
    [JsonPropertyName("server")]
    public string Server { get; set; }

    [JsonPropertyName("database")]
    public string Database { get; set; }

    [JsonPropertyName("user")]
    public string User { get; set; }

    [JsonPropertyName("password")]
    public string Password { get; set; }

    public string ConnectionString =>
        $"Server={Server};Database={Database};User Id={User};Password={Password};TrustServerCertificate=True;";
}
