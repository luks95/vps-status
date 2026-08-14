using System.Globalization;
using VPSMonitor.Domain.Monitoring;

namespace VPSMonitor.Infrastructure.Linux;

public sealed class LinuxSnapshotParser
{
    private static readonly HashSet<string> Markers =
    [
        "CPU", "MEM", "NET", "BLOCK", "DF", "LOAD", "UPTIME", "TEMP", "OS", "END"
    ];

    public RawSystemSnapshot Parse(string output, DateTimeOffset capturedAt)
    {
        if (string.IsNullOrWhiteSpace(output)) throw new FormatException("La respuesta de la VPS está vacía.");
        var sections = SplitSections(output);

        var cpu = ParseCpu(Required(sections, "CPU").FirstOrDefault());
        var memory = ParseMemory(Required(sections, "MEM"));
        var network = ParseNetwork(Required(sections, "NET"));
        var disk = ParseBlock(Required(sections, "BLOCK"));
        var fileSystems = ParseFileSystems(Required(sections, "DF"));
        var load = ParseLoad(Required(sections, "LOAD").FirstOrDefault());
        var uptime = ParseDouble(Required(sections, "UPTIME").FirstOrDefault()?.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault(), "uptime");
        var temperature = ParseTemperature(sections.GetValueOrDefault("TEMP"));
        var os = Required(sections, "OS");

        return new RawSystemSnapshot(
            capturedAt,
            cpu,
            memory.Total,
            memory.Available,
            memory.SwapTotal,
            memory.SwapFree,
            network.Received,
            network.Transmitted,
            disk.Read,
            disk.Written,
            fileSystems,
            load.One,
            load.Five,
            load.Fifteen,
            load.Processes,
            TimeSpan.FromSeconds(uptime),
            temperature,
            os.ElementAtOrDefault(0) ?? "Linux",
            os.ElementAtOrDefault(1) ?? "Desconocido");
    }

    private static Dictionary<string, List<string>> SplitSections(string output)
    {
        var sections = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        string? current = null;

        foreach (var rawLine in output.Replace("\r", string.Empty, StringComparison.Ordinal).Split('\n'))
        {
            var line = rawLine.TrimEnd();
            if (line.StartsWith("__", StringComparison.Ordinal) && line.EndsWith("__", StringComparison.Ordinal))
            {
                var marker = line.Trim('_');
                if (Markers.Contains(marker))
                {
                    current = marker;
                    sections.TryAdd(marker, []);
                    continue;
                }
            }

            if (current is not null && line.Length > 0) sections[current].Add(line);
        }

        return sections;
    }

    private static IReadOnlyList<string> Required(Dictionary<string, List<string>> sections, string name) =>
        sections.TryGetValue(name, out var lines) && lines.Count > 0
            ? lines
            : throw new FormatException($"Falta la sección Linux {name}.");

    private static CpuCounters ParseCpu(string? line)
    {
        var values = (line ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (values.Length < 8 || values[0] != "cpu") throw new FormatException("La sección CPU no es válida.");

        return new CpuCounters(
            ParseUlong(values[1], "cpu.user"),
            ParseUlong(values[2], "cpu.nice"),
            ParseUlong(values[3], "cpu.system"),
            ParseUlong(values[4], "cpu.idle"),
            ParseUlong(values[5], "cpu.iowait"),
            ParseUlong(values[6], "cpu.irq"),
            ParseUlong(values[7], "cpu.softirq"),
            values.Length > 8 ? ParseUlong(values[8], "cpu.steal") : 0);
    }

    private static (ulong Total, ulong Available, ulong SwapTotal, ulong SwapFree) ParseMemory(IEnumerable<string> lines)
    {
        var values = new Dictionary<string, ulong>(StringComparer.Ordinal);
        foreach (var line in lines)
        {
            var parts = line.Split(':', 2);
            if (parts.Length != 2) continue;
            var value = parts[1].Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var kibibytes))
            {
                values[parts[0]] = kibibytes * 1024;
            }
        }

        if (!values.TryGetValue("MemTotal", out var total)) throw new FormatException("Falta MemTotal.");
        var available = values.GetValueOrDefault("MemAvailable", values.GetValueOrDefault("MemFree"));
        return (total, available, values.GetValueOrDefault("SwapTotal"), values.GetValueOrDefault("SwapFree"));
    }

    private static (ulong Received, ulong Transmitted) ParseNetwork(IEnumerable<string> lines)
    {
        ulong received = 0;
        ulong transmitted = 0;
        foreach (var line in lines)
        {
            var parts = line.Split(':', 2);
            if (parts.Length != 2 || parts[0].Trim() == "lo") continue;
            var fields = parts[1].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length < 9) continue;
            received += ParseUlong(fields[0], "net.rx");
            transmitted += ParseUlong(fields[8], "net.tx");
        }

        return (received, transmitted);
    }

    private static (ulong Read, ulong Written) ParseBlock(IEnumerable<string> lines)
    {
        ulong readSectors = 0;
        ulong writtenSectors = 0;
        foreach (var line in lines)
        {
            var fields = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length < 8) continue;
            var device = fields[0];
            if (device.StartsWith("loop", StringComparison.Ordinal) || device.StartsWith("ram", StringComparison.Ordinal)) continue;
            readSectors += ParseUlong(fields[3], "disk.readSectors");
            writtenSectors += ParseUlong(fields[7], "disk.writeSectors");
        }

        return (readSectors * 512, writtenSectors * 512);
    }

    private static IReadOnlyList<FileSystemUsage> ParseFileSystems(IEnumerable<string> lines)
    {
        var result = new List<FileSystemUsage>();
        foreach (var line in lines.Skip(1))
        {
            var fields = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length < 6) continue;
            if (!ulong.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out var total)) continue;
            if (!ulong.TryParse(fields[2], NumberStyles.None, CultureInfo.InvariantCulture, out var used)) continue;
            if (!ulong.TryParse(fields[3], NumberStyles.None, CultureInfo.InvariantCulture, out var available)) continue;
            result.Add(new FileSystemUsage(
                fields[0],
                string.Join(' ', fields.Skip(5)),
                checked(total * 1024),
                checked(used * 1024),
                checked(available * 1024)));
        }

        return result;
    }

    private static (double One, double Five, double Fifteen, int Processes) ParseLoad(string? line)
    {
        var fields = (line ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length < 4) throw new FormatException("La sección LOAD no es válida.");
        var processParts = fields[3].Split('/');
        return (
            ParseDouble(fields[0], "load.1"),
            ParseDouble(fields[1], "load.5"),
            ParseDouble(fields[2], "load.15"),
            processParts.Length == 2 && int.TryParse(processParts[1], CultureInfo.InvariantCulture, out var count) ? count : 0);
    }

    private static double? ParseTemperature(IEnumerable<string>? lines)
    {
        var value = lines?.FirstOrDefault();
        if (value is null || value.Equals("NA", StringComparison.OrdinalIgnoreCase)) return null;
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var raw)) return null;
        return raw > 1_000 ? raw / 1_000d : raw;
    }

    private static ulong ParseUlong(string? value, string field) =>
        ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var result)
            ? result
            : throw new FormatException($"El campo {field} no es válido.");

    private static double ParseDouble(string? value, string field) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result)
            ? result
            : throw new FormatException($"El campo {field} no es válido.");
}
