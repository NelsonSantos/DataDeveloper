using System;
using DataDeveloper.Data;
using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Providers.SqlServer;
using DataDeveloper.Data.Services;
using DataDeveloper.Interfaces;
using DataDeveloper.Services;
using DataDeveloper.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DataDeveloper.Tests;

public class TabQueryEditorViewModelParameterTests
{
    public TabQueryEditorViewModelParameterTests()
    {
        var services = new ServiceCollection();
        services.AddSingleton<DatabaseProviderFactoryService>();
        DatabaseExtensionsMethods.SetServiceProvider(services.BuildServiceProvider());
    }

    [Fact]
    public void EditingStatement_ThenRestoringParameter_KeepsValueAndSendAsNull()
    {
        var viewModel = CreateViewModel();

        viewModel.SqlStatement = "select * from payment where id = @Id";
        var parameter = Assert.Single(viewModel.ParameterValues);
        parameter.Value = "42";
        parameter.IsNull = true;

        // Mid-edit, the parameter is momentarily removed from the statement and
        // disappears from detection - this is the "variable sums" the user reported.
        viewModel.SqlStatement = "select * from payment where id = 5";
        Assert.Empty(viewModel.ParameterValues);

        // Finishing the edit brings the same parameter name back.
        viewModel.SqlStatement = "select * from payment where id = @Id";

        var restored = Assert.Single(viewModel.ParameterValues);
        Assert.Equal("42", restored.Value);
        Assert.True(restored.IsNull);
    }

    [Fact]
    public void EditingStatement_WithUnrelatedChange_PreservesParameterInstance()
    {
        var viewModel = CreateViewModel();

        viewModel.SqlStatement = "select * from payment where id = @Id";
        var parameter = viewModel.ParameterValues.Single();
        parameter.Value = "7";

        viewModel.SqlStatement = "select * from payment where id = @Id and active = 1";

        var afterEdit = viewModel.ParameterValues.Single();
        Assert.Same(parameter, afterEdit);
        Assert.Equal("7", afterEdit.Value);
    }

    private static TabQueryEditorViewModel CreateViewModel()
    {
        var connectionSettings = new SqlServerConnectionSettings { DatabaseType = DatabaseType.SqlServer };
        return new TabQueryEditorViewModel(connectionSettings, "Query 1", file: null, canClose: true, new FakeServiceProvider());
    }

    private sealed class FakeServiceProvider : IServiceProvider
    {
        private readonly IEventAggregatorService _eventAggregatorService = new EventAggregatorService();

        public object? GetService(Type serviceType)
        {
            if (serviceType == typeof(IEventAggregatorService))
                return _eventAggregatorService;

            throw new NotSupportedException($"Service not configured for test: {serviceType}");
        }
    }
}
