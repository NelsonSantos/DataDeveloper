using System.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reactive;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using DataDeveloper.Core;
using DataDeveloper.Data.Ai;
using DataDeveloper.Interfaces;
using DataDeveloper.Services;
using ReactiveUI;
using ReactiveUI.Reactive;
using ReactiveUI.SourceGenerators;

namespace DataDeveloper.ViewModels;

public partial class PreferencesViewModel : ViewModelBase
{
    private const string TestPrompt = "Reply with exactly the word: OK";

    private readonly IAiSettingsRepository? _settingsRepository;
    private readonly AiProviderFactoryService? _providerFactory;
    private readonly AiProviderType _providerType = AiProviderType.ClaudeCode;

    // Parameterless constructor is only for the XAML designer.
    public PreferencesViewModel()
    {
        SaveCommand = ReactiveCommand.Create<StyledElement>(_ => { });
        CancelCommand = ReactiveCommand.Create<StyledElement>(_ => { });
        TestCommand = ReactiveCommand.CreateFromTask(() => Task.CompletedTask);
        SignInCommand = ReactiveCommand.CreateFromTask(() => Task.CompletedTask);
    }

    public PreferencesViewModel(IAiSettingsRepository settingsRepository, AiProviderFactoryService providerFactory)
    {
        _settingsRepository = settingsRepository;
        _providerFactory = providerFactory;

        var settings = settingsRepository.Load(_providerType);
        _executablePath = settings.ExecutablePath ?? string.Empty;
        _model = settings.Model ?? string.Empty;
        _timeoutSeconds = settings.TimeoutSeconds;
        ProviderName = providerFactory.SupportedProviders[0].DisplayName;
        DetectedPath = LocateExecutable();

        SaveCommand = ReactiveCommand.Create<StyledElement>(element => Save(element));
        CancelCommand = ReactiveCommand.Create<StyledElement>(element => element.GetParentWindow().Close());
        var idle = this.WhenAnyValue(vm => vm.IsBusy, busy => !busy);
        TestCommand = ReactiveCommand.CreateFromTask(TestAsync, idle);
        SignInCommand = ReactiveCommand.CreateFromTask(SignInAsync, idle);
    }

    public string ProviderName { get; } = "Claude Code";

    public string DetectedPath { get; private set; } = string.Empty;

    [Reactive] private string _executablePath = string.Empty;
    [Reactive] private string _model = string.Empty;
    [Reactive] private decimal? _timeoutSeconds = AiProviderSettings.DefaultTimeoutSeconds;
    [Reactive] private bool _isBusy;
    [Reactive] private string _testResult = string.Empty;
    [Reactive] private bool _testSucceeded;
    [Reactive] private bool _testFailed;

    public ReactiveCommand<StyledElement, Unit> SaveCommand { get; }
    public ReactiveCommand<StyledElement, Unit> CancelCommand { get; }
    public ReactiveCommand<Unit, Unit> TestCommand { get; }
    public ReactiveCommand<Unit, Unit> SignInCommand { get; }

    public IReadOnlyList<string> ModelSuggestions { get; } = AiChatViewModel.ModelOptions.Select(option => option.Id).ToList();

    public decimal MinTimeoutSeconds => AiProviderSettings.MinTimeoutSeconds;
    public decimal MaxTimeoutSeconds => AiProviderSettings.MaxTimeoutSeconds;

    private string LocateExecutable()
    {
        var path = CliExecutableLocator.Locate("claude");
        return path ?? "not found";
    }

    private AiProviderSettings BuildSettings() => new()
    {
        ProviderType = _providerType,
        ExecutablePath = string.IsNullOrWhiteSpace(ExecutablePath) ? null : ExecutablePath.Trim(),
        Model = string.IsNullOrWhiteSpace(Model) ? null : Model.Trim(),
        TimeoutSeconds = TimeoutSeconds is { } seconds
            ? AiProviderSettings.NormalizeTimeout((int)seconds)
            : AiProviderSettings.DefaultTimeoutSeconds
    };

    private void Save(StyledElement element)
    {
        _settingsRepository?.Save(BuildSettings());
        element.GetParentWindow().Close(true);
    }

    private async Task SignInAsync()
    {
        if (_providerFactory is null)
            return;

        IsBusy = true;
        TestSucceeded = false;
        TestFailed = false;
        TestResult = "Waiting for the sign in to finish in your browser...";

        var signedIn = false;
        try
        {
            var provider = _providerFactory.Create(BuildSettings());
            var response = await provider.SignInAsync();
            signedIn = response.Success;

            if (!signedIn)
            {
                TestResult = response.ErrorMessage ?? "Sign in failed.";
                TestFailed = true;
            }
        }
        catch (Exception exception)
        {
            TestResult = exception.Message;
            TestFailed = true;
        }
        finally
        {
            IsBusy = false;
        }

        // Confirm right away that the CLI now answers.
        if (signedIn)
            await TestAsync();
    }

    private async Task TestAsync()
    {
        if (_providerFactory is null)
            return;

        IsBusy = true;
        TestSucceeded = false;
        TestFailed = false;
        TestResult = "Testing...";

        try
        {
            var provider = _providerFactory.Create(BuildSettings());

            var availability = await provider.CheckAvailabilityAsync();
            if (!availability.IsAvailable)
            {
                TestResult = availability.Detail ?? $"{provider.DisplayName} is not available.";
                TestFailed = true;
                return;
            }

            var stopwatch = Stopwatch.StartNew();
            var response = await provider.SendMessageAsync(new AiChatRequest(TestPrompt));
            stopwatch.Stop();

            TestSucceeded = response.Success;
            TestFailed = !response.Success;
            TestResult = response.Success
                ? $"Connected ({availability.Version}, {availability.ExecutablePath}) - answered \"{response.Content}\" in {stopwatch.Elapsed.TotalSeconds:0.0}s."
                : response.ErrorMessage ?? "The assistant did not answer.";
        }
        catch (Exception exception)
        {
            TestResult = exception.Message;
            TestFailed = true;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
