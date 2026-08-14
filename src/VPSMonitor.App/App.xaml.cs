using System.IO;
using System.Windows;
using VPSMonitor.App.Services;
using VPSMonitor.App.ViewModels;
using VPSMonitor.Application.Monitoring;
using VPSMonitor.Application.Servers;
using VPSMonitor.Infrastructure.Linux;
using VPSMonitor.Infrastructure.Network;
using VPSMonitor.Infrastructure.Persistence;
using VPSMonitor.Infrastructure.Security;
using VPSMonitor.Infrastructure.Ssh;

namespace VPSMonitor.App;

public partial class App : System.Windows.Application
{
    private TrayNotificationService? _trayService;
    private DashboardViewModel? _viewModel;
    private MainWindow? _mainWindow;
    private SshServerProbe? _serverProbe;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var applicationDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VPSMonitor");
        var databasePath = Path.Combine(applicationDirectory, "vpsmonitor.db");

        var serverRepository = new SqliteVpsRepository(databasePath);
        var settingsRepository = new SqliteAppSettingsRepository(databasePath);
        var credentialVault = new WindowsCredentialVault();
        _serverProbe = new SshServerProbe(new LinuxSnapshotParser(), new LatencyProbe());
        var vpsManager = new VpsManager(serverRepository, credentialVault);
        var coordinator = new MonitoringCoordinator(_serverProbe, credentialVault);

        _trayService = new TrayNotificationService();
        _viewModel = new DashboardViewModel(
            vpsManager,
            settingsRepository,
            coordinator,
            _trayService,
            Dispatcher);
        _mainWindow = new MainWindow(_viewModel, _serverProbe, credentialVault, _trayService);

        _trayService.RestoreRequested += RestoreWindow;
        _trayService.ExitRequested += async () => await ExitAsync();
        _mainWindow.Show();
    }

    private void RestoreWindow()
    {
        if (_mainWindow is null) return;
        _mainWindow.Show();
        if (_mainWindow.WindowState == WindowState.Minimized) _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
    }

    private async Task ExitAsync()
    {
        if (_mainWindow is not null) _mainWindow.AllowClose();
        if (_viewModel is not null) await _viewModel.DisposeAsync();
        _serverProbe?.Dispose();
        if (_trayService is not null)
        {
            _trayService.RestoreRequested -= RestoreWindow;
            _trayService.Dispose();
        }
        _mainWindow?.Close();
        Shutdown();
    }
}
