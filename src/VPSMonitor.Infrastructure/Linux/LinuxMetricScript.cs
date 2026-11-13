namespace VPSMonitor.Infrastructure.Linux;

internal static class LinuxMetricScript
{
    // El archivo fuente puede compilarse con CRLF (core.autocrlf en Windows); el shell remoto necesita LF.
    public static readonly string Value = Raw.Replace("\r\n", "\n", StringComparison.Ordinal);

    private const string Raw = """
        export LC_ALL=C
        printf '__CPU__\n'
        head -n 1 /proc/stat
        printf '__MEM__\n'
        cat /proc/meminfo
        printf '__NET__\n'
        cat /proc/net/dev
        printf '__BLOCK__\n'
        for f in /sys/block/*/stat; do
          [ -r "$f" ] || continue
          printf '%s ' "$(basename "$(dirname "$f")")"
          cat "$f"
        done
        printf '__DF__\n'
        df -P -k -x tmpfs -x devtmpfs -x squashfs -x overlay 2>/dev/null || df -P -k
        printf '__LOAD__\n'
        cat /proc/loadavg
        printf '__UPTIME__\n'
        cat /proc/uptime
        printf '__TEMP__\n'
        found=0
        for f in /sys/class/thermal/thermal_zone*/temp; do
          if [ -r "$f" ]; then cat "$f"; found=1; break; fi
        done
        [ "$found" -eq 1 ] || printf 'NA\n'
        printf '__OS__\n'
        if [ -r /etc/os-release ]; then
          . /etc/os-release
          printf '%s\n' "${PRETTY_NAME:-Linux}"
        else
          printf 'Linux\n'
        fi
        uname -r
        printf '__END__\n'
        """;
}
