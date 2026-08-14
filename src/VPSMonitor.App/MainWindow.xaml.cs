using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using VPSMonitor.App.Services;
using VPSMonitor.App.ViewModels;
using VPSMonitor.Application.Abstractions;
using MessageBox = System.Windows.MessageBox;

namespace VPSMonitor.App;

public partial class MainWindow : Window
{
    private readonly DashboardViewModel _viewModel;
    private readonly IServerProbe _serverProbe;
    private readonly ICredentialVault _credentialVault;
    private readonly TrayNotificationService _trayService;
    private bool _allowClose;

    public MainWindow(
        DashboardViewModel viewModel,
        IServerProbe serverProbe,
        ICredentialVault credentialVault,
        TrayNotificationService trayService)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _serverProbe = serverProbe;
        _credentialVault = credentialVault;
        _trayService = trayService;
        DataContext = viewModel;
        SourceInitialized += (_, _) => EnableDarkTitleBar();
    }

    public void AllowClose() => _allowClose = true;

    private async void WindowLoaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.InitializeAsync();
    }

    private async void AddServerClicked(object sender, RoutedEventArgs e)
    {
        if (_viewModel.Servers.Count >= 5)
        {
            MessageBox.Show("El MVP permite monitorear hasta cinco VPS.", "Límite alcanzado", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var editor = new ServerEditorWindow(_serverProbe) { Owner = this };
        if (editor.ShowDialog() != true || editor.SavedServer is null) return;
        await SaveFromEditorAsync(editor);
    }

    private async void EditServerClicked(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedServer is null)
        {
            MessageBox.Show("Selecciona una VPS para editarla.", "VPS Monitor", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var server = _viewModel.SelectedServer.Server;
        var password = await _credentialVault.GetPasswordAsync(server.Id);
        var editor = new ServerEditorWindow(_serverProbe, server, password) { Owner = this };
        if (editor.ShowDialog() != true || editor.SavedServer is null) return;
        await SaveFromEditorAsync(editor);
    }

    private async void DeleteServerClicked(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedServer is null) return;
        var answer = MessageBox.Show(
            $"¿Eliminar {_viewModel.SelectedServer.Name}? También se borrará su contraseña guardada.",
            "Eliminar VPS",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (answer != MessageBoxResult.Yes) return;

        try
        {
            await _viewModel.DeleteSelectedAsync();
        }
        catch (Exception exception)
        {
            MessageBox.Show($"No fue posible eliminar la VPS: {exception.Message}", "VPS Monitor", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task SaveFromEditorAsync(ServerEditorWindow editor)
    {
        try
        {
            await _viewModel.AddOrUpdateAsync(editor.SavedServer!, editor.NewPassword);
        }
        catch (Exception exception)
        {
            MessageBox.Show($"No fue posible guardar la VPS: {exception.Message}", "VPS Monitor", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void WindowClosing(object? sender, CancelEventArgs e)
    {
        if (_allowClose) return;
        e.Cancel = true;
        Hide();
        if (!_viewModel.HasShownTrayHint)
        {
            _trayService.Show(
                "VPS Monitor sigue activo",
                "El monitoreo continúa en segundo plano. Usa el icono de la bandeja para volver o salir.",
                NotificationSeverity.Information);
            await _viewModel.MarkTrayHintShownAsync();
        }
    }

    private void EnableDarkTitleBar()
    {
        if (PresentationSource.FromVisual(this) is not HwndSource source) return;
        var enabled = 1;
        _ = DwmSetWindowAttribute(source.Handle, 20, ref enabled, sizeof(int));
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
}
