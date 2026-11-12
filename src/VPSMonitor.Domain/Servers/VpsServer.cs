namespace VPSMonitor.Domain.Servers;

public sealed record VpsServer(
    Guid Id,
    string Name,
    string Host,
    int Port,
    string Username,
    bool IsEnabled,
    string? TrustedHostKeyFingerprint = null,
    AlertThresholds? ThresholdOverrides = null,
    SshAuthMethod AuthMethod = SshAuthMethod.Password,
    string? PrivateKeyPath = null)
{
    public static VpsServer Create(string name, string host, int port, string username) =>
        new(Guid.NewGuid(), name.Trim(), host.Trim(), port, username.Trim(), true);

    public void Validate()
    {
        if (Id == Guid.Empty) throw new ArgumentException("El identificador de la VPS es obligatorio.");
        if (string.IsNullOrWhiteSpace(Name)) throw new ArgumentException("El nombre es obligatorio.");
        if (string.IsNullOrWhiteSpace(Host)) throw new ArgumentException("La IP o dominio es obligatorio.");
        if (Port is < 1 or > 65535) throw new ArgumentOutOfRangeException(nameof(Port), "El puerto debe estar entre 1 y 65535.");
        if (string.IsNullOrWhiteSpace(Username)) throw new ArgumentException("El usuario es obligatorio.");
        if (AuthMethod == SshAuthMethod.PrivateKey && string.IsNullOrWhiteSpace(PrivateKeyPath))
        {
            throw new ArgumentException("La ruta de la clave privada es obligatoria.");
        }
    }
}

public enum SshAuthMethod
{
    Password = 0,
    PrivateKey = 1
}

public sealed record AlertThresholds(
    double CpuPercent = 90,
    double MemoryPercent = 85,
    double DiskPercent = 80,
    double LatencyMilliseconds = 200);
