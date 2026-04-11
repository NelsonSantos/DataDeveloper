using System.Diagnostics;
using System.Reflection;
using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Interfaces;
using DataDeveloper.Data.Models;
using DataDeveloper.Interfaces;
using DataDeveloper.Services;
using DataDeveloper.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DataDeveloper.Tests;

public class TabDataGridViewModelTests
{
    [Fact]
    public async Task RefreshDataAsync_ReusesStatementParameters()
    {
        var executor = new CapturingStatementExecutor();
        var services = new ServiceCollection();
        services.AddSingleton<IEventAggregatorService, EventAggregatorService>();
        var serviceProvider = services.BuildServiceProvider();

        var parameters = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["IntegrationId"] = Guid.Parse("c681c0c4-aa79-4aba-9b94-665f6f7ebcf7")
        };

        var viewModel = new TestTabDataGridViewModel(
            new ConnectionSettings
            {
                DatabaseType = DatabaseType.SqlServer,
                Name = "Test"
            },
            new StatementResult(
                dataReader: null,
                connection: null,
                command: null,
                statement: "select * from Integration where id = @IntegrationId",
                watcher: new Stopwatch(),
                parameters: parameters),
            selectedPage: 100,
            name: "result 01",
            canClose: true,
            serviceProvider: serviceProvider,
            messageTargetId: Guid.NewGuid(),
            statementExecutor: executor);

        var refreshMethod = typeof(TabDataGridViewModel).GetMethod("RefreshDataAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(refreshMethod);

        var refreshTask = Assert.IsAssignableFrom<Task>(refreshMethod!.Invoke(viewModel, null));
        await refreshTask;

        Assert.Equal("select * from Integration where id = @IntegrationId", executor.LastSql);
        Assert.NotNull(executor.LastParameters);
        Assert.Equal(parameters["IntegrationId"], executor.LastParameters!["IntegrationId"]);
    }

    private sealed class CapturingStatementExecutor : IStatementExecutor
    {
        public string? LastSql { get; private set; }
        public IReadOnlyDictionary<string, object?>? LastParameters { get; private set; }

        public Task<IEnumerable<StatementResult>> ExecuteStatement(
            string sqlStatement,
            IReadOnlyDictionary<string, object?>? parameters = null,
            int? commandTimeoutSeconds = null,
            CancellationToken cancellationToken = default)
        {
            LastSql = sqlStatement;
            LastParameters = parameters;
            return Task.FromResult<IEnumerable<StatementResult>>([]);
        }

        public void Cancel()
        {
        }
    }

    private sealed class TestTabDataGridViewModel : TabDataGridViewModel
    {
        private readonly IStatementExecutor _statementExecutor;

        public TestTabDataGridViewModel(
            ConnectionSettings connectionSettings,
            StatementResult statementResult,
            int selectedPage,
            string name,
            bool canClose,
            IServiceProvider serviceProvider,
            Guid messageTargetId,
            IStatementExecutor statementExecutor)
            : base(connectionSettings, statementResult, selectedPage, name, canClose, serviceProvider, messageTargetId)
        {
            _statementExecutor = statementExecutor;
        }

        protected override IStatementExecutor CreateStatementExecutor()
        {
            return _statementExecutor;
        }
    }
}
