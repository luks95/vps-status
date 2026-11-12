using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using VPSMonitor.Application.Abstractions;
using VPSMonitor.Domain.Monitoring;
using VPSMonitor.Domain.Servers;
using Brush = System.Windows.Media.Brush;
using Control = System.Windows.Controls.Control;

namespace VPSMonitor.App;

public partial class ServerEditorWindow : Window
{
    private static readonly string[] DefaultKeyNames = ["id_ed25519", "id_ecdsa", "id_rsa"];

    private readonly IServerProbe _serverProbe;
    private readonly VpsServer? _original;
    private readonly string? _existingSecret;
    private string? _trustedFingerprint;
    private string? _verifiedSignature;
    private TaskCompletionSource<bool>? _trustDecision;
    private bool _initializing = true;

    /// <param name="existingSecret">Secreto guardado de la VPS editada: contraseña o passphrase de la clave.</param>
    public ServerEditorWindow(IServerProbe serverProbe, VpsServer? server = null, string? existingSecret = null)
    {
        InitializeComponent();
        _serverProbe = serverProbe;
        _original = server;
        _existingSecret = existingSecret;
        _trustedFingerprint = server?.TrustedHostKeyFingerprint;
        SourceInitialized += (_, _) => EnableDarkTitleBar();
        Closing += (_, _) => _trustDecision?.TrySetResult(false);

        if (server is not null)
        {
            Title = "Editar VPS";
            HeadingText.Text = "Editar VPS";
            NameBox.Text = server.Name;
            HostBox.Text = server.Host;
            UsernameBox.Text = server.Username;
            PortBox.Text = server.Port.ToString();
            EnabledCheckBox.IsChecked = server.IsEnabled;
            if (server.AuthMethod == SshAuthMethod.PrivateKey)
            {
                KeyRadio.IsChecked = true;
                KeyPathBox.Text = server.PrivateKeyPath ?? string.Empty;
            }

            if (server.ThresholdOverrides is { } thresholds)
            {
                CustomThresholdsCheckBox.IsChecked = true;
                CpuThresholdBox.Text = thresholds.CpuPercent.ToString(CultureInfo.CurrentCulture);
                MemoryThresholdBox.Text = thresholds.MemoryPercent.ToString(CultureInfo.CurrentCulture);
                DiskThresholdBox.Text = thresholds.DiskPercent.ToString(CultureInfo.CurrentCulture);
                LatencyThresholdBox.Text = thresholds.LatencyMilliseconds.ToString(CultureInfo.CurrentCulture);
            }
        }

        UpdateAuthPanels();
        _initializing = false;
    }

    public VpsServer? SavedServer { get; private set; }

    /// <summary>Contraseña o passphrase nueva; null conserva la guardada.</summary>
    public string? NewPassword { get; private set; }

    private bool UsesKey => KeyRadio.IsChecked == true;

    // ───────── Prueba de conexión ─────────

    private async void TestConnectionClicked(object sender, RoutedEventArgs e)
    {
        ValidationText.Text = string.Empty;
        if (!TryBuildServer(out var server, out var error, out var invalidField))
        {
            ShowValidation(error, invalidField);
            return;
        }

        var secret = CurrentSecretForProbe();
        if (!UsesKey && string.IsNullOrEmpty(secret))
        {
            ShowValidation("Ingresa la contraseña para probar la conexión.", PasswordInput);
            return;
        }

        SetBusy(true, "Conectando de forma segura…");
        try
        {
            var result = await _serverProbe.ProbeAsync(server, secret);
            if (result.FailureStatus is ConnectionStatus.HostKeyUnknown or ConnectionStatus.HostKeyChanged &&
                result.ObservedHostKeyFingerprint is { } fingerprint)
            {
                var changed = result.FailureStatus == ConnectionStatus.HostKeyChanged;
                if (!await AskToTrustAsync(fingerprint, changed))
                {
                    ShowResult("La huella no fue aprobada. No se guardó ningún cambio.", false);
                    return;
                }

                _trustedFingerprint = fingerprint;
                server = server with { TrustedHostKeyFingerprint = fingerprint };
                SetBusy(true, "Verificando con la huella aprobada…");
                result = await _serverProbe.ProbeAsync(server, secret);
            }

            if (!result.IsSuccess)
            {
                ShowResult(result.Message ?? "No fue posible conectar con la VPS.", false);
                return;
            }

            _trustedFingerprint = result.ObservedHostKeyFingerprint ?? _trustedFingerprint;
            _verifiedSignature = Signature();
            var method = UsesKey ? "clave SSH" : "contraseña";
            ShowResult($"Conexión verificada · {result.Snapshot?.Distribution ?? "Linux"} · {method} · solo lectura", true);
            SaveButton.Focus();
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

    private Task<bool> AskToTrustAsync(string fingerprint, bool changed)
    {
        _trustDecision = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        ResultPanel.Visibility = Visibility.Collapsed;
        FingerprintBox.Text = fingerprint;
        FingerprintTitle.Text = changed ? "La huella del servidor cambió" : "Confirma la identidad del servidor";
        FingerprintDescription.Text = changed
            ? "Advertencia: la huella guardada ya no coincide. Podría ser una reinstalación legítima o un intento de suplantación; verifícalo antes de continuar."
            : "Es la primera conexión con este servidor. Compara esta huella con la que muestra tu proveedor antes de confiar.";
        FingerprintPanel.Visibility = Visibility.Visible;
        TrustButton.Focus();
        return _trustDecision.Task;
    }

    private void TrustFingerprintClicked(object sender, RoutedEventArgs e) => CompleteTrust(true);

    private void RejectFingerprintClicked(object sender, RoutedEventArgs e) => CompleteTrust(false);

    private void CompleteTrust(bool trusted)
    {
        FingerprintPanel.Visibility = Visibility.Collapsed;
        _trustDecision?.TrySetResult(trusted);
    }

    // ───────── Guardado ─────────

    private async void SaveClicked(object sender, RoutedEventArgs e)
    {
        if (!TryBuildServer(out var server, out var error, out var invalidField))
        {
            ShowValidation(error, invalidField);
            return;
        }

        if (_verifiedSignature != Signature())
        {
            ShowValidation("Prueba la conexión después de completar o modificar los datos.", TestButton);
            return;
        }

        await Task.Yield();
        SavedServer = server with { TrustedHostKeyFingerprint = _trustedFingerprint };
        NewPassword = UsesKey
            ? NullIfEmpty(PassphraseInput.Password)
            : NullIfEmpty(PasswordInput.Password);
        DialogResult = true;
    }

    private static string? NullIfEmpty(string value) => string.IsNullOrEmpty(value) ? null : value;

    private bool TryBuildServer(out VpsServer server, out string error, out Control? invalidField)
    {
        server = null!;
        error = string.Empty;
        invalidField = null;

        if (string.IsNullOrWhiteSpace(NameBox.Text)) return Fail("El nombre es obligatorio.", NameBox, out error, out invalidField);
        if (string.IsNullOrWhiteSpace(HostBox.Text)) return Fail("La IP o dominio es obligatorio.", HostBox, out error, out invalidField);
        if (!int.TryParse(PortBox.Text, out var port) || port is < 1 or > 65535)
        {
            return Fail("El puerto debe ser un número entre 1 y 65535.", PortBox, out error, out invalidField);
        }

        if (string.IsNullOrWhiteSpace(UsernameBox.Text)) return Fail("El usuario es obligatorio.", UsernameBox, out error, out invalidField);

        string? keyPath = null;
        if (UsesKey)
        {
            keyPath = KeyPathBox.Text.Trim().Trim('"');
            if (keyPath.Length == 0)
            {
                return Fail("Indica la ruta de tu clave privada.", KeyPathBox, out error, out invalidField);
            }

            if (!File.Exists(Environment.ExpandEnvironmentVariables(keyPath)))
            {
                return Fail("No se encontró el archivo de la clave privada.", KeyPathBox, out error, out invalidField);
            }
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
                return Fail(
                    "Los límites deben ser números positivos; los porcentajes no pueden superar 100.",
                    CpuThresholdBox,
                    out error,
                    out invalidField);
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
            thresholds,
            UsesKey ? SshAuthMethod.PrivateKey : SshAuthMethod.Password,
            keyPath);

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

    private static bool Fail(string message, Control field, out string error, out Control? invalidField)
    {
        error = message;
        invalidField = field;
        return false;
    }

    private bool EndpointMatchesOriginal() => _original is not null &&
        string.Equals(_original.Host, HostBox.Text.Trim(), StringComparison.OrdinalIgnoreCase) &&
        _original.Port.ToString() == PortBox.Text.Trim() &&
        string.Equals(_original.Username, UsernameBox.Text.Trim(), StringComparison.Ordinal);

    /// <summary>Secreto a usar al probar: el escrito ahora, o el guardado si sigue siendo aplicable.</summary>
    private string CurrentSecretForProbe()
    {
        if (UsesKey)
        {
            if (!string.IsNullOrEmpty(PassphraseInput.Password)) return PassphraseInput.Password;
            var sameKey = _original is { AuthMethod: SshAuthMethod.PrivateKey } &&
                string.Equals(_original.PrivateKeyPath, KeyPathBox.Text.Trim().Trim('"'), StringComparison.OrdinalIgnoreCase);
            return sameKey ? _existingSecret ?? string.Empty : string.Empty;
        }

        if (!string.IsNullOrEmpty(PasswordInput.Password)) return PasswordInput.Password;
        return _original is { AuthMethod: SshAuthMethod.Password } ? _existingSecret ?? string.Empty : string.Empty;
    }

    private string Signature() => string.Join(
        '|',
        NameBox.Text.Trim(),
        HostBox.Text.Trim(),
        PortBox.Text.Trim(),
        UsernameBox.Text.Trim(),
        UsesKey ? "key" : "password",
        UsesKey ? KeyPathBox.Text.Trim().Trim('"') : string.Empty,
        UsesKey ? PassphraseInput.Password : PasswordInput.Password,
        EnabledCheckBox.IsChecked);

    private static bool TryThreshold(string text, double maximum, out double value) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value) && value > 0 && value <= maximum;

    // ───────── Método de autenticación ─────────

    private void AuthMethodChanged(object sender, RoutedEventArgs e)
    {
        if (PasswordPanel is null || KeyPanel is null) return;
        UpdateAuthPanels();
        if (_initializing) return;

        if (UsesKey && string.IsNullOrWhiteSpace(KeyPathBox.Text) && FindDefaultKey() is { } suggested)
        {
            KeyPathBox.Text = suggested;
        }

        ResetVerification();
    }

    private void UpdateAuthPanels()
    {
        PasswordPanel.Visibility = UsesKey ? Visibility.Collapsed : Visibility.Visible;
        KeyPanel.Visibility = UsesKey ? Visibility.Visible : Visibility.Collapsed;

        var editingPassword = _original is { AuthMethod: SshAuthMethod.Password };
        PasswordHint.Text = editingPassword
            ? "Déjala vacía para conservar la contraseña guardada."
            : "Se guardará cifrada mediante Credential Manager de Windows.";

        var editingKey = _original is { AuthMethod: SshAuthMethod.PrivateKey } && !string.IsNullOrEmpty(_existingSecret);
        PassphraseHint.Text = editingKey
            ? "Déjala vacía para conservar la passphrase guardada."
            : "Solo si tu clave está protegida. Se guarda en Credential Manager de Windows.";
    }

    private void BrowseKeyClicked(object sender, RoutedEventArgs e)
    {
        var sshDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ssh");
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Selecciona tu clave privada SSH",
            Filter = "Todos los archivos|*.*",
            CheckFileExists = true,
            InitialDirectory = Directory.Exists(sshDirectory) ? sshDirectory : string.Empty
        };

        if (dialog.ShowDialog(this) == true) KeyPathBox.Text = dialog.FileName;
    }

    private static string? FindDefaultKey()
    {
        var sshDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ssh");
        return DefaultKeyNames
            .Select(name => Path.Combine(sshDirectory, name))
            .FirstOrDefault(File.Exists);
    }

    // ───────── Estado de la interfaz ─────────

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

    private void ShowValidation(string message, Control? field)
    {
        ValidationText.Text = message;
        field?.Focus();
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
        (ResultIcon.Text, var brushKey) = success switch
        {
            true => ("", "SuccessBrush"),
            false => ("", "CriticalBrush"),
            _ => ("", "TextSecondaryBrush")
        };
        var brush = (Brush)FindResource(brushKey);
        ResultIcon.Foreground = brush;
        ResultText.Foreground = success is null ? brush : (Brush)FindResource("TextPrimaryBrush");
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
