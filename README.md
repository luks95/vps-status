# VPS Monitor

Aplicación de escritorio para Windows 11 que monitorea hasta cinco VPS Linux por SSH. Muestra CPU, memoria, disco, red, carga, procesos, uptime, temperatura y latencia con una vista inspirada en la sección Rendimiento del Administrador de tareas.

## Características

- Monitoreo simultáneo en tiempo real con una muestra cada dos segundos.
- Gráficas móviles de los últimos 60 segundos; no guarda historial de métricas.
- Conexiones SSH persistentes y aisladas por VPS.
- Autenticación por IP o dominio, usuario y contraseña **o clave SSH** (OpenSSH/PEM: ed25519, ECDSA, RSA).
- Contraseñas y passphrases almacenadas como credenciales genéricas locales en Credential Manager de Windows.
- Con clave SSH solo se guarda la ruta del archivo; la clave privada nunca se copia.
- Verificación estricta de la huella SHA-256 de la clave de host SSH.
- Latencia ICMP con alternativa TCP al puerto SSH.
- Alertas globales y límites opcionales por VPS.
- Reconexión progresiva y notificaciones desde la bandeja de Windows.
- Operación exclusivamente de lectura; no incluye terminal ni acciones administrativas.

## Requisitos de desarrollo

- Windows 11 x64.
- .NET SDK 10.0.400 o una revisión compatible de .NET 10.
- Visual Studio con desarrollo de escritorio .NET, o la CLI de .NET.

La distribución publicada es autocontenida y no requiere instalar .NET en el equipo del usuario.

## Ejecutar durante el desarrollo

```powershell
dotnet restore
dotnet run --project src/VPSMonitor.App/VPSMonitor.App.csproj
```

## Primer uso

1. Selecciona **Agregar VPS**.
2. Ingresa nombre, IP o dominio, puerto SSH y usuario. Elige **Contraseña** o **Clave SSH** (con **Examinar** localizas p. ej. `~/.ssh/id_ed25519`; la passphrase es opcional). Los archivos `.ppk` de PuTTY no son compatibles: expórtalos a formato OpenSSH.
3. Selecciona **Probar conexión**.
4. Verifica por un canal confiable la huella SSH que muestra la aplicación.
5. Aprueba la huella y guarda la VPS.

La contraseña o passphrase no se guarda en SQLite. La base local contiene solamente configuración (incluida la ruta de la clave, si aplica) y huellas confiables. Para usar una clave, su parte pública debe estar en `~/.ssh/authorized_keys` del servidor.

## Datos locales

- Base de configuración: `%LOCALAPPDATA%\VPSMonitor\vpsmonitor.db`.
- Contraseñas: Credential Manager de Windows, con destinos `VPSMonitor/<id>`.
- Métricas: únicamente en memoria durante la ejecución.

Para eliminar una VPS y su credencial asociada, utiliza **Eliminar VPS** dentro de la aplicación.

## Comandos remotos

La aplicación ejecuta un script interno fijo de solo lectura. Consulta `/proc`, `/sys`, `df`, `/etc/os-release` y `uname`. No acepta comandos escritos por el usuario y no abre un shell interactivo.

Las métricas principales funcionan en distribuciones Linux modernas. Temperatura y actividad de disco pueden aparecer como **No disponible** si la VPS o el proveedor no exponen esos contadores.

## Verificación

```powershell
dotnet build VPSMonitor.slnx --configuration Release
dotnet test VPSMonitor.slnx --configuration Release --no-build
```

## Instalar

Descarga `VPSMonitor-Setup-<versión>.exe` desde la página de [Releases](https://github.com/luks95/vps-status/releases/latest) y ejecútalo. No requiere .NET ni permisos de administrador (se instala para tu usuario por defecto). Al desinstalar no se borran tus servidores ni credenciales.

Para generar el instalador tú mismo (requiere Inno Setup 6: `winget install JRSoftware.InnoSetup`):

```powershell
./installer/build-installer.ps1 -Version 1.1.0
# -> artifacts/installer/VPSMonitor-Setup-1.1.0.exe
```

## Publicar para Windows x64

```powershell
dotnet publish src/VPSMonitor.App/VPSMonitor.App.csproj `
  --configuration Release `
  --runtime win-x64 `
  --self-contained true `
  --output artifacts/VPSMonitor-win-x64
```

Ejecuta `VPSMonitor.exe` desde el directorio publicado.

## Estructura

- `src/VPSMonitor.Domain`: modelos, métricas y cálculos puros.
- `src/VPSMonitor.Application`: casos de uso, workers y alertas.
- `src/VPSMonitor.Infrastructure`: SSH, Linux, SQLite y Credential Manager.
- `src/VPSMonitor.App`: WPF, MVVM, gráficas, bandeja y formularios.
- `tests`: pruebas unitarias y de integración local.
- `design-system`: tokens y reglas visuales.
- `docs/superpowers`: especificación y plan de implementación.
