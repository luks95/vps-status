using System.Windows;
using System.Windows.Controls;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using VPSMonitor.Application.Abstractions;
using VPSMonitor.Domain.Monitoring;
using VPSMonitor.Domain.Servers;
using MessageBox = System.Windows.MessageBox;

namespace VPSMonitor.App;

public partial class ServerEditorWindow : Window
{
    private readonly IServerProbe _serverProbe;
    private readonly VpsServer? _original;
    private readonly string? _existingPassword;
    private string? _trustedFingerprint;
    private string? _verifiedSignature;
    private bool _initializing = true;

    public ServerEditorWindow(IServerProbe serverProbe, VpsServer? server = null, string? existingPassword = null)
    {
        InitializeComponent();
        _serverProbe = serverProbe;
        _original = server;
        _existingPassword = existingPassword;
        _trustedFingerprint = server?.TrustedHostKeyFingerprint;
        SourceInitialized += (_, _) => EnableDarkTitleBar();

        if (server is not null)
        {
            Title = "Editar VPS";
            HeadingText.Text = "Editar VPS";
            NameBox.Text = server.Name;
            HostBox.Text = server.Host;
            UsernameBox.Text = server.Username;
            PortBox.Text = server.Port.ToString();
            EnabledCheckBox.IsChecked = server.IsEnabled;
            PasswordHint.Text = "Déjala vacía para conservar la contraseña guardada.";
            if (server.ThresholdOverrides is { } thresholds)
            {
                CustomThresholdsCheckBox.IsChecked = true;
                CpuThresholdBox.Text = thresholds.CpuPercent.ToString(CultureInfo.CurrentCulture);
                MemoryThresholdBox.Text = thresholds.MemoryPercent.ToString(CultureInfo.CurrentCulture);
                DiskThresholdBox.Text = thresholds.DiskPercent.ToString(CultureInfo.CurrentCulture);
                LatencyThresholdBox.Text = thresholds.LatencyMilliseconds.ToString(CultureInfo.CurrentCulture);
            }
        }

        _initializing = false;
    }

    public VpsServer? SavedServer { get; private set; }
    public string? NewPassword { get; private set; }

    private async void TestConnectionClicked(object sender, RoutedEventArgs e)
    {
        ValidationText.Text = string.Empty;
        if (!TryBuildServer(out var server, out var error))
        {
            ValidationText.Text = error;
            return;
        }

        var password = string.IsNullOrEmpty(PasswordInput.Password) ? _existingPassword : PasswordInput.Password;
        if (string.IsNullOrEmpty(password))
        {
            ValidationText.Text = "Ingresa la contraseña para probar la conexión.";
            PasswordInput.Focus();
            return;
        }

        SetBusy(true, "Conectando de forma segura…");
        try
        {
            var result = await _serverProbe.ProbeAsync(server, password);
            if (result.FailureStatus is ConnectionStatus.HostKeyUnknown or ConnectionStatus.HostKeyChanged &&
                result.ObservedHostKeyFingerprint is { } fingerprint)
            {
                var changeWarning = result.FailureStatus == ConnectionStatus.HostKeyChanged
                    ? "ADVERTENCIA: la huella guardada cambió. Verifica el servidor antes de continuar.\n\n"
                    : "Primera conexión con este servidor.\n\n";
                var answer = MessageBox.Show(
                    $"{changeWarning}Huella presentada:\n{fingerprint}\n\n¿Confías en esta huella?",
                    "Confirmar identidad SSH",
                    MessageBoxButton.YesNo,
                    result.FailureStatus == ConnectionStatus.HostKeyChanged ? MessageBoxImage.Warning : MessageBoxImage.Question);
                if (answer != MessageBoxResult.Yes)
                {
                    ShowResult("La huella no fue aprobada. No se guardó ningún cambio.", false);
                    return;
                }

                _trustedFingerprint = fingerprint;
                server = server with { TrustedHostKeyFingerprint = fingerprint };
                result = await _serverProbe.ProbeAsync(server, password);
            }

            if (!result.IsSuccess)
            {
                ShowResult(result.Message ?? "No fue posible conectar con la VPS.", false);
                return;
            }

            _trustedFingerprint = result.ObservedHostKeyFingerprint ?? _trustedFingerprint;
            _verifiedSignature = Signature();
            ShowResult($"Conexión verificada · {result.Snapshot?.Distribution ?? "Linux"} · SSH de solo lectura", true);
        }
        catch (Exception exception)
        {
            ShowResult($"No fue posible completar la prueba: {exception.Message}", false);
        }
        finally
        {
            SetBusy(false, null);
        }
    }

    private async void SaveClicked(object sender, RoutedEventArgs e)
    {
        if (!TryBuildServer(out var server, out var error))
        {
            ValidationText.Text = error;
            return;
        }

        if (_verifiedSignature != Signature())
        {
            ValidationText.Text = "Prueba la conexión después de completar o modificar los datos.";
            TestButton.Focus();
            return;
        }

        await Task.Yield();
        SavedServer = server with { TrustedHostKeyFingerprint = _trustedFingerprint };
        NewPassword = string.IsNullOrEmpty(PasswordInput.Password) ? null : PasswordInput.Password;
        DialogResult = true;
    }

    private bool TryBuildServer(out VpsServer server, out string error)
    {
        error = string.Empty;
        server = null!;
        if (!int.TryParse(PortBox.Text, out var port) || port is < 1 or > 65535)
        {
            error = "El puerto debe ser un número entre 1 y 65535.";
            PortBox.Focus();
            return false;
        }

        var fingerprint = EndpointMatchesOriginal() ? _trustedFingerprint : null;
        AlertThresholds? thresholds = null;
        if (CustomThresholdsCheckBox.IsChecked == true)
        {
            if (!TryThreshold(CpuThresholdBox.Text, 100, out var cpu) ||
                !TryThreshold(MemoryThresholdBox.Text, 100, out var memory) ||
                !TryThreshold(DiskThresholdBox.Text, 100, out var disk) ||
                !TryThreshold(LatencyThresholdBox.Text, double.MaxValue, out var latency))
            {
                error = "Los límites deben ser números positivos; los porcentajes no pueden superar 100.";
                return false;
            }
            thresholds = new AlertThresholds(cpu, memory, disk, latency);
        }

        server = new VpsServer(
            _original?.Id ?? Guid.NewGuid(),
            NameBox.Text.Trim(),
            HostBox.Text.Trim(),
            port,
            UsernameBox.Text.Trim(),
            EnabledCheckBox.IsChecked == true,
            fingerprint,
            thresholds);

        try
        {
            server.Validate();
            return true;
        }
        catch (ArgumentException exception)
        {
            error = exception.Message;
            return false;
        }
    }

    private bool EndpointMatchesOriginal() => _original is not null &&
        string.Equals(_original.Host, HostBox.Text.Trim(), StringComparison.OrdinalIgnoreCase) &&
        _original.Port.ToString() == PortBox.Text.Trim() &&
        string.Equals(_original.Username, UsernameBox.Text.Trim(), StringComparison.Ordinal);

    private string Signature() => $"{NameBox.Text.Trim()}|{HostBox.Text.Trim()}|{PortBox.Text.Trim()}|{UsernameBox.Text.Trim()}|{PasswordInput.Password}|{EnabledCheckBox.IsChecked}";

    private static bool TryThreshold(string text, double maximum, out double value) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value) && value > 0 && value <= maximum;

    private void CustomThresholdsChanged(object sender, RoutedEventArgs e)
    {
        if (CustomThresholdPanel is not null)
        {
            CustomThresholdPanel.Visibility = CustomThresholdsCheckBox.IsChecked == true
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
    }

    private void AnyFieldChanged(object sender, TextChangedEventArgs e)
    {
        if (!_initializing) ResetVerification();
    }

    private void AnyPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (!_initializing) ResetVerification();
    }

    private void ResetVerification()
    {
        _verifiedSignature = null;
        ResultPanel.Visibility = Visibility.Collapsed;
        ValidationText.Text = string.Empty;
    }

    private void SetBusy(bool busy, string? message)
    {
        TestButton.IsEnabled = !busy;
        SaveButton.IsEnabled = !busy;
        if (message is not null) ShowResult(message, null);
    }

    private void ShowResult(string message, bool? success)
    {
        ResultPanel.Visibility = Visibility.Visible;
        ResultText.Text = message;
        ResultText.Foreground = success switch
        {
            true => (System.Windows.Media.Brush)FindResource("SuccessBrush"),
            false => (System.Windows.Media.Brush)FindResource("CriticalBrush"),
            _ => (System.Windows.Media.Brush)FindResource("TextSecondaryBrush")
        };
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
