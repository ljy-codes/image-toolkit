using System.ComponentModel;
using System.IO;
using System.Windows;
using ImageToolkit.App.Services;
using ImageToolkit.App.ViewModels;
using ImageToolkit.Application.Import;
using ImageToolkit.Application.Preview;
using ImageToolkit.Application.Processing;
using ImageToolkit.Domain.Interfaces;
using ImageToolkit.Infrastructure.Config;
using ImageToolkit.Infrastructure.Files;
using ImageToolkit.Infrastructure.Imaging;
using ImageToolkit.Infrastructure.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ImageToolkit.App;

public partial class App : System.Windows.Application
{
    private ServiceProvider? _services;
    private IConfigurationStore<AppConfiguration>? _configurationStore;
    private MainWindowViewModel? _mainViewModel;
    private RollingFileLoggerProvider? _loggerProvider;
    private ILogger? _logger;
    private CancellationTokenSource? _saveCancellation;
    private bool _configurationReady;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            var dataDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ImageToolkit");
            Directory.CreateDirectory(dataDirectory);
            _configurationStore = new JsonConfigurationStore(
                Path.Combine(dataDirectory, "config.json"));
            _loggerProvider = new RollingFileLoggerProvider(
                Path.Combine(dataDirectory, "Logs"));
            _logger = _loggerProvider.CreateLogger("ImageToolkit.App");

            var registrations = new ServiceCollection();
            RegisterServices(registrations, _configurationStore);
            _services = registrations.BuildServiceProvider();

            var configuration = await _configurationStore.LoadAsync(
                CancellationToken.None);
            _mainViewModel = _services.GetRequiredService<MainWindowViewModel>();
            ApplyConfiguration(_mainViewModel, configuration);
            SubscribeConfigurationChanges(_mainViewModel);
            _configurationReady = true;

            var window = _services.GetRequiredService<MainWindow>();
            MainWindow = window;
            window.Show();
            _logger.LogInformation("应用已启动。");
        }
        catch (Exception exception)
        {
            _logger?.LogCritical(exception, "应用启动失败。");
            MessageBox.Show(
                $"应用启动失败：{exception.Message}",
                "图批处理",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _configurationReady = false;
        _saveCancellation?.Cancel();
        _saveCancellation?.Dispose();
        _saveCancellation = null;

        if (_configurationStore is not null && _mainViewModel is not null)
        {
            try
            {
                _configurationStore.SaveAsync(
                    CreateConfiguration(_mainViewModel),
                    CancellationToken.None).GetAwaiter().GetResult();
            }
            catch (Exception exception)
            {
                _logger?.LogError(exception, "退出时保存配置失败。");
            }
        }

        _logger?.LogInformation("应用已退出。");
        _services?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _loggerProvider?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        base.OnExit(e);
    }

    private static void RegisterServices(
        IServiceCollection services,
        IConfigurationStore<AppConfiguration> configurationStore)
    {
        services.AddSingleton(configurationStore);
        services.AddSingleton<IAtomicFileWriter, AtomicFileWriter>();
        services.AddSingleton<IOutputPathResolver, OutputPathResolver>();
        services.AddSingleton<IImageFileDiscovery, ImageFileDiscovery>();
        services.AddSingleton<IImageMetadataReader, MagickImageMetadataReader>();
        services.AddSingleton<IImagePreviewRenderer, MagickPreviewRenderer>();
        services.AddSingleton<IImageProcessor, MagickImageProcessor>();
        services.AddSingleton<ProcessingRequestValidator>();
        services.AddSingleton<ImageProcessingPipeline>();
        services.AddSingleton<ImportImagesUseCase>();
        services.AddSingleton<BuildPreviewUseCase>();
        services.AddSingleton<IDesktopFilePicker, DesktopFilePicker>();
        services.AddSingleton<IUserDialogService, UserDialogService>();
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<ProcessingSettingsViewModel>();
        services.AddSingleton<AppearanceSettingsViewModel>();
        services.AddSingleton<PreviewViewModel>();
        services.AddSingleton<FileQueueViewModel>();
        services.AddSingleton<BatchProgressViewModel>();
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<MainWindow>();
    }

    private static void ApplyConfiguration(
        MainWindowViewModel viewModel,
        AppConfiguration configuration)
    {
        viewModel.Settings.Apply(configuration.Processing);
        viewModel.Appearance.Apply(
            configuration.Theme,
            configuration.WorkspaceBackground,
            configuration.CustomWorkspaceColor,
            configuration.FontSize);
        viewModel.IncludeSubdirectories = configuration.IncludeSubdirectories;
    }

    private void SubscribeConfigurationChanges(MainWindowViewModel viewModel)
    {
        viewModel.Settings.PropertyChanged += OnConfigurationChanged;
        viewModel.Appearance.PropertyChanged += OnConfigurationChanged;
        viewModel.PropertyChanged += OnMainViewModelPropertyChanged;
    }

    private void OnMainViewModelPropertyChanged(
        object? sender,
        PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.IncludeSubdirectories))
        {
            ScheduleConfigurationSave();
        }
    }

    private void OnConfigurationChanged(
        object? sender,
        PropertyChangedEventArgs e) =>
        ScheduleConfigurationSave();

    private void ScheduleConfigurationSave()
    {
        if (!_configurationReady)
        {
            return;
        }

        _saveCancellation?.Cancel();
        _saveCancellation?.Dispose();
        _saveCancellation = new CancellationTokenSource();
        _ = SaveConfigurationAfterDelayAsync(_saveCancellation.Token);
    }

    private async Task SaveConfigurationAfterDelayAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(300, cancellationToken);
            if (_configurationStore is not null && _mainViewModel is not null)
            {
                await _configurationStore.SaveAsync(
                    CreateConfiguration(_mainViewModel),
                    cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            _logger?.LogError(exception, "自动保存配置失败。");
        }
    }

    private static AppConfiguration CreateConfiguration(
        MainWindowViewModel viewModel) =>
        new(
            viewModel.Settings.BuildRequest(),
            0,
            viewModel.Appearance.Theme,
            viewModel.IncludeSubdirectories,
            viewModel.Appearance.WorkspaceBackground,
            viewModel.Appearance.CustomWorkspaceColor,
            viewModel.Appearance.FontSize);
}
