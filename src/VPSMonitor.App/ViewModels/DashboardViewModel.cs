using System.Collections.ObjectModel;
using System.Windows.Threading;
using VPSMonitor.App.Commands;
using VPSMonitor.Application.Abstractions;
using VPSMonitor.Application.Monitoring;
using VPSMonitor.Application.Servers;
using VPSMonitor.Domain.Monitoring;
using VPSMonitor.Domain.Servers;
using VPSMonitor.Domain.Settings;

namespace VPSMonitor.App.ViewModels;

public sealed class DashboardViewModel : ObservableObject, IAsyncDisposable
{
    private readonly VpsManager _vpsManager;
    private readonly IAppSettingsRepository _settingsRepository;
    private readonly MonitoringCoordinator _monitoringCoordinator;
    private readonly IUserNotificationService _notificationService;
    private readonly Dispatcher _dispatcher;
    private readonly AlertEvaluator _alertEvaluator = new();
    private ServerItemViewModel? _selectedServer;
    private string _currentPage = "Servidores";
    private bool _isInitialized;
    private bool _isBusy;
    private string? _bannerMessage;
    private bool _bannerIsError;
    private AppSettings _settings = AppSettings.Default;

    public DashboardViewModel(
        VpsManager vpsManager,
        IAppSettingsRepository settingsRepository,
        MonitoringCoordinator monitoringCoordinator,
        IUserNotificationService notificationService,
        Dispatcher dispatcher)
    {
        _vpsManager = vpsManager;
        _settingsRepository = settingsRepository;
        _monitoringCoordinator = monitoringCoordinator;
        _notificationService = notificationService;
        _dispatcher = dispatcher;
        _monitoringCoordinator.StateChanged += OnMonitorStateChanged;
        NavigateCommand = new RelayCommand<string>(page => CurrentPage = page ?? "Servidores");
        SaveSettingsCommand = new AsyncRelayCommand(SaveSettingsAsync, () => !IsBusy);
        OpenServerCommand = new RelayCommand<ServerItemViewModel>(server =>
        {
            if (server is null) return;
            SelectedServer = server;
            CurrentPage = "Servidores";
        });
        DismissBannerCommand = new RelayCommand(() => BannerMessage = null);
    }

    public ObservableCollection<ServerItemViewModel> Servers { get; } = [];
    public ObservableCollection<AlertItemViewModel> Alerts { get; } = [];
    public RelayCommand<string> NavigateCommand { get; }
    public AsyncRelayCommand SaveSettingsCommand { get; }
    public RelayCommand<ServerItemViewModel> OpenServerCommand { get; }
    public RelayCommand DismissBannerCommand { get; }

    public ServerItemViewModel? SelectedServer
    {
        get => _selectedServer;
        set
        {
            if (!SetProperty(ref _selectedServer, value)) return;
            OnPropertyChanged(nameof(HasSelection));
        }
    }

    public string CurrentPage
    {
        get => _currentPage;
        set
        {
            if (!SetProperty(ref _currentPage, value)) return;
            OnPropertyChanged(nameof(IsSummaryPage));
            OnPropertyChanged(nameof(IsServersPage));
            OnPropertyChanged(nameof(IsAlertsPage));
            OnPropertyChanged(nameof(IsSettingsPage));
            OnPropertyChanged(nameof(PageTitle));
            OnPropertyChanged(nameof(PageSubtitle));
        }
    }

    public string PageTitle => CurrentPage;

    public string PageSubtitle => CurrentPage switch
    {
        "Resumen" => "Estado general de todas tus VPS de un vistazo",
        "Servidores" => "Métricas en vivo de los últimos 60 segundos",
        "Alertas" => "Eventos de esta sesión; no se guarda historial",
        "Configuración" => "Límites globales de alerta",
        _ => string.Empty
    };
    public bool IsSummaryPage => CurrentPage == "Resumen";
    public bool IsServersPage => CurrentPage == "Servidores";
    public bool IsAlertsPage => CurrentPage == "Alertas";
    public bool IsSettingsPage => CurrentPage == "Configuración";
    public bool HasServers => Servers.Count > 0;
    public bool HasAlerts => Alerts.Count > 0;
    public int AlertCount => Alerts.Count;
    public bool HasSelection => SelectedServer is not null;
    public int OnlineCount => Servers.Count(server => server.Status == ConnectionStatus.Online);
    public int WarningCount => Servers.Count(server => server.Status is ConnectionStatus.Warning or ConnectionStatus.Reconnecting);
    public int OfflineCount => Servers.Count - OnlineCount - WarningCount;

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value)) SaveSettingsCommand.NotifyCanExecuteChanged();
        }
    }

    public string? BannerMessage
    {
        get => _bannerMessage;
        private set => SetProperty(ref _bannerMessage, value);
    }

    public bool BannerIsError
    {
        get => _bannerIsError;
        private set => SetProperty(ref _bannerIsError, value);
    }

    private void ShowBanner(string message, bool isError)
    {
        BannerIsError = isError;
        BannerMessage = message;
        if (isError) return;

        // Los avisos de éxito se retiran solos; los errores permanecen hasta que se descartan.
        _ = Task.Delay(TimeSpan.FromSeconds(4)).ContinueWith(
            _ => _dispatcher.BeginInvoke(() =>
            {
                if (BannerMessage == message && !BannerIsError) BannerMessage = null;
            }),
            TaskScheduler.Default);
    }

    public double CpuThreshold { get; set; } = 90;
    public double MemoryThreshold { get; set; } = 85;
    public double DiskThreshold { get; set; } = 80;
    public double LatencyThreshold { get; set; } = 200;

    public async Task InitializeAsync()
    {
        if (_isInitialized) return;
        _isInitialized = true;
        IsBusy = true;
        try
        {
            _settings = await _settingsRepository.GetAsync();
            ApplySettings(_settings.GlobalThresholds);
            var servers = await _vpsManager.GetAllAsync();
            foreach (var server in servers.Take(5)) Servers.Add(new ServerItemViewModel(server));
            SelectedServer = Servers.FirstOrDefault();
            _monitoringCoordinator.Start(servers);
            NotifyCollectionSummary();
        }
        catch (Exception exception)
        {
            ShowBanner($"No se pudo iniciar la aplicación: {exception.Message}", true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task AddOrUpdateAsync(VpsServer server, string? password)
    {
        IsBusy = true;
        try
        {
            await _vpsManager.SaveAsync(server, password);
            var existing = Servers.FirstOrDefault(item => item.Id == server.Id);
            if (existing is null)
            {
                existing = new ServerItemViewModel(server);
                Servers.Add(existing);
                _monitoringCoordinator.Start(server);
            }
            else
            {
                existing.ReplaceServer(server);
                await _monitoringCoordinator.RestartAsync(server);
            }

            SelectedServer = existing;
            CurrentPage = "Servidores";
            BannerMessage = null;
            NotifyCollectionSummary();
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task DeleteAsync(ServerItemViewModel item)
    {
        if (!Servers.Contains(item)) return;
        IsBusy = true;
        try
        {
            await _monitoringCoordinator.StopAsync(item.Id);
            await _vpsManager.DeleteAsync(item.Id);
            var wasSelected = ReferenceEquals(SelectedServer, item);
            Servers.Remove(item);
            if (wasSelected) SelectedServer = Servers.FirstOrDefault();
            NotifyCollectionSummary();
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task MarkTrayHintShownAsync()
    {
        if (_settings.HasShownTrayHint) return;
        _settings = _settings with { HasShownTrayHint = true };
        await _settingsRepository.SaveAsync(_settings);
    }

    public bool HasShownTrayHint => _settings.HasShownTrayHint;

    private async Task SaveSettingsAsync()
    {
        if (CpuThreshold is < 1 or > 100 || MemoryThreshold is < 1 or > 100 || DiskThreshold is < 1 or > 100 || LatencyThreshold < 1)
        {
            ShowBanner("Revisa los límites: los porcentajes deben estar entre 1 y 100 y la latencia debe ser positiva.", true);
            return;
        }

        IsBusy = true;
        try
        {
            _settings = _settings with
            {
                GlobalThresholds = new AlertThresholds(CpuThreshold, MemoryThreshold, DiskThreshold, LatencyThreshold)
            };
            await _settingsRepository.SaveAsync(_settings);
            ShowBanner("Configuración guardada.", false);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void OnMonitorStateChanged(ServerMonitorState state)
    {
        _dispatcher.BeginInvoke(() =>
        {
            var server = Servers.FirstOrDefault(item => item.Id == state.ServerId);
            if (server is null) return;
            server.Apply(state);

            var isReachable = state.Status is ConnectionStatus.Online or ConnectionStatus.Warning;
            var thresholds = server.Server.ThresholdOverrides ?? _settings.GlobalThresholds;
            foreach (var alert in _alertEvaluator.Evaluate(server.Id, state.Metrics, isReachable, thresholds))
            {
                var item = AlertItemViewModel.From(alert, server.Name);
                Alerts.Insert(0, item);
                while (Alerts.Count > 100) Alerts.RemoveAt(Alerts.Count - 1);
                OnPropertyChanged(nameof(HasAlerts));
                OnPropertyChanged(nameof(AlertCount));
                _notificationService.Show(
                    $"{server.Name} · {item.Title}",
                    item.Message,
                    alert.IsRecovery ? NotificationSeverity.Success : NotificationSeverity.Warning);
            }

            NotifyCollectionSummary();
        });
    }

    private void ApplySettings(AlertThresholds thresholds)
    {
        CpuThreshold = thresholds.CpuPercent;
        MemoryThreshold = thresholds.MemoryPercent;
        DiskThreshold = thresholds.DiskPercent;
        LatencyThreshold = thresholds.LatencyMilliseconds;
        OnPropertyChanged(nameof(CpuThreshold));
        OnPropertyChanged(nameof(MemoryThreshold));
        OnPropertyChanged(nameof(DiskThreshold));
        OnPropertyChanged(nameof(LatencyThreshold));
    }

    private void NotifyCollectionSummary()
    {
        OnPropertyChanged(nameof(HasServers));
        OnPropertyChanged(nameof(OnlineCount));
        OnPropertyChanged(nameof(WarningCount));
        OnPropertyChanged(nameof(OfflineCount));
    }

    public async ValueTask DisposeAsync()
    {
        _monitoringCoordinator.StateChanged -= OnMonitorStateChanged;
        await _monitoringCoordinator.DisposeAsync();
    }
}
