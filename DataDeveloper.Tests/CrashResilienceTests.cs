using System;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;
using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Providers.PostgresSql;
using DataDeveloper.Services;
using Xunit;

namespace DataDeveloper.Tests;

public class CrashResilienceTests
{
    [Fact]
    public void IsConnectionFailure_RecognizesNetworkErrorsAnywhereInTheChain()
    {
        Assert.True(UnhandledErrorReporter.IsConnectionFailure(new SocketException()));
        Assert.True(UnhandledErrorReporter.IsConnectionFailure(new TimeoutException()));
        Assert.True(UnhandledErrorReporter.IsConnectionFailure(new InvalidOperationException("query failed", new IOException("connection reset"))));
        Assert.True(UnhandledErrorReporter.IsConnectionFailure(new AggregateException(new SocketException())));
        Assert.False(UnhandledErrorReporter.IsConnectionFailure(new InvalidOperationException("bug")));
    }

    [Fact]
    public void BuildMessage_PointsToTheNetworkForConnectionFailures()
    {
        Assert.Contains("network or VPN", UnhandledErrorReporter.BuildMessage(new SocketException()));
        Assert.DoesNotContain("VPN", UnhandledErrorReporter.BuildMessage(new InvalidOperationException("bug")));
        Assert.Contains(UnhandledErrorReporter.LogFilePath, UnhandledErrorReporter.BuildMessage(new InvalidOperationException("bug")));
    }

    [Fact]
    public async Task Completion_AfterTheDatabaseIsUnreachable_StopsTryingUntilTheRetryDelay()
    {
        // Port 1 on localhost refuses at once, like a server behind a VPN that just dropped.
        var settings = new PostgresConnectionSettings
        {
            Id = Guid.NewGuid(),
            DatabaseType = DatabaseType.PostgresSql,
            Server = "127.0.0.1",
            Port = 1,
            Database = "none"
        };
        var now = DateTime.UtcNow;
        var originalClock = SqlCompletionProvider.UtcNow;
        SqlCompletionProvider.UtcNow = () => now;
        try
        {
            const string sql = "select * from ";
            var request = SqlCompletionProvider.GetManualCompletionRequest(sql, sql.Length);

            await Assert.ThrowsAnyAsync<Exception>(() => SqlCompletionProvider.GetCompletionsAsync(settings, sql, sql.Length, request));

            // Within the delay: no new connection attempt, no error, just nothing from the schema.
            Assert.Empty(await SqlCompletionProvider.GetCompletionsAsync(settings, sql, sql.Length, request));

            now += SqlCompletionProvider.UnavailableRetryDelay + TimeSpan.FromSeconds(1);
            await Assert.ThrowsAnyAsync<Exception>(() => SqlCompletionProvider.GetCompletionsAsync(settings, sql, sql.Length, request));
        }
        finally
        {
            SqlCompletionProvider.UtcNow = originalClock;
            SqlCompletionProvider.InvalidateSchemaCache(settings.Id);
        }
    }
}
