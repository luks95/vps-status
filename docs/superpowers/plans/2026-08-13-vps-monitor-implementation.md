# VPS Monitor — Plan de implementación

Fecha: 2026-08-13
Especificación: `docs/superpowers/specs/2026-08-13-vps-monitor-design.md`

## Objetivo

Implementar el MVP de VPS Monitor como aplicación WPF autocontenida para Windows x64, con monitoreo simultáneo de hasta cinco VPS Linux mediante SSH, credenciales protegidas, gráficas en tiempo real, alertas y funcionamiento desde la bandeja.

## Principios de ejecución

- Implementar primero las reglas y contratos independientes de WPF.
- Escribir pruebas antes o junto con cada comportamiento observable.
- Mantener secretos fuera de SQLite, archivos y registros.
- Aislar todas las APIs de Windows y dependencias externas detrás de interfaces.
- Mantener cada ciclo de servidor cancelable e independiente.
- Validar compilación y pruebas después de cada bloque funcional.

## Fase 1 — Estructura de solución

1. Crear `VPSMonitor.slnx`.
2. Crear los proyectos:
   - `src/VPSMonitor.Domain/VPSMonitor.Domain.csproj`.
   - `src/VPSMonitor.Application/VPSMonitor.Application.csproj`.
   - `src/VPSMonitor.Infrastructure/VPSMonitor.Infrastructure.csproj`.
   - `src/VPSMonitor.App/VPSMonitor.App.csproj`.
   - `tests/VPSMonitor.Domain.Tests/VPSMonitor.Domain.Tests.csproj`.
   - `tests/VPSMonitor.Application.Tests/VPSMonitor.Application.Tests.csproj`.
   - `tests/VPSMonitor.Infrastructure.Tests/VPSMonitor.Infrastructure.Tests.csproj`.
3. Configurar referencias respetando Domain → Application → Infrastructure/App.
4. Activar nullable, analizadores y warnings relevantes.
5. Añadir `.gitignore`, `Directory.Build.props` y documentación de inicio.
6. Verificar `dotnet restore`, `dotnet build` y `dotnet test`.

## Fase 2 — Dominio y cálculos

1. Implementar entidades y value objects para VPS, endpoint SSH, huella confiable, estado de conexión, muestra de métricas y límites de alerta.
2. Implementar un búfer circular tipado de 30 muestras, equivalente a 60 segundos con intervalo de dos segundos.
3. Implementar cálculo de CPU desde deltas de `/proc/stat`.
4. Implementar tasas de red y disco desde deltas de contadores y tiempo monotónico.
5. Manejar primera muestra, contadores reiniciados y valores inválidos.
6. Cubrir reglas y límites con pruebas unitarias.

## Fase 3 — Recolección Linux

1. Definir un formato delimitado y versionado para la respuesta del script remoto fijo.
2. Implementar parsers independientes para:
   - `/proc/stat`.
   - `/proc/meminfo`.
   - `/proc/net/dev`.
   - `/proc/diskstats`.
   - `/proc/loadavg`.
   - `/proc/uptime`.
   - `df`.
   - información del sistema y temperatura opcional.
3. Crear fixtures realistas de distintas distribuciones y kernels.
4. Implementar agregación, exclusión de loopback y sistemas de archivos virtuales.
5. Verificar degradación por métrica cuando falten fuentes opcionales.

## Fase 4 — SSH, confianza y latencia

1. Integrar SSH.NET detrás de `ISshSession` y `ISshSessionFactory`.
2. Implementar autenticación por usuario y contraseña sin registrar secretos.
3. Capturar la huella de clave de host y representar los estados nueva, confiable y modificada.
4. Bloquear conexiones con una huella modificada hasta nueva aprobación explícita.
5. Ejecutar únicamente el script interno fijo con timeout y cancelación.
6. Implementar latencia ICMP y alternativa TCP al puerto SSH.
7. Probar credenciales inválidas, timeout, cancelación y clasificación de errores.

## Fase 5 — Persistencia y credenciales

1. Integrar SQLite para configuración y migración inicial.
2. Crear repositorios para VPS, preferencias, umbrales y huellas.
3. Integrar `PasswordVault` mediante `ICredentialVault`.
4. Garantizar operaciones coordinadas al crear, editar o eliminar una VPS.
5. Añadir pruebas que inspeccionen la base de datos y registros para descartar secretos.

## Fase 6 — Coordinación y alertas

1. Implementar un worker cancelable por VPS.
2. Aplicar el ciclo de dos segundos sin solapamiento.
3. Implementar reconexión a 2, 5, 10, 20 y 30 segundos.
4. Publicar estados inmutables para la interfaz.
5. Implementar umbrales, tres muestras de activación, dos de recuperación, histéresis y enfriamiento de cinco minutos.
6. Probar aislamiento entre VPS, reconexión y alertas.

## Fase 7 — Shell WPF y sistema visual

1. Crear tokens para colores, tipografía, espaciado, radios y estados.
2. Implementar ventana principal oscura con barra lateral y área de contenido.
3. Crear navegación Resumen, Servidores, Alertas y Configuración.
4. Implementar lista de VPS y estados accesibles.
5. Asegurar foco visible, teclado, DPI y reducción de movimiento.
6. Crear estados vacío, carga, error y sin conexión.

## Fase 8 — Gestión de VPS

1. Crear formulario de alta y edición.
2. Validar nombre, host, puerto, usuario y contraseña.
3. Implementar `Probar conexión`.
4. Crear diálogo de confirmación de huella SSH con formato legible.
5. Guardar metadatos y contraseña solamente después de validar el flujo.
6. Implementar eliminación segura de configuración y credencial asociada.

## Fase 9 — Dashboard en tiempo real

1. Integrar LiveCharts2.
2. Crear tarjetas de uptime, carga, procesos, temperatura y estado SSH.
3. Crear gráficas de CPU, memoria, red y latencia.
4. Crear panel de disco con capacidad y actividad.
5. Limitar cada serie a 30 puntos y evitar recrear colecciones completas por muestra.
6. Añadir resúmenes accesibles y estados `No disponible`.

## Fase 10 — Bandeja y notificaciones

1. Mantener monitoreo al ocultar la ventana.
2. Añadir icono y menú de bandeja para abrir y salir.
3. Mostrar una explicación la primera vez que se cierre la ventana.
4. Integrar notificaciones nativas para alerta y recuperación.
5. Evitar notificaciones duplicadas mediante el estado del motor de alertas.

## Fase 11 — Integración y entrega

1. Preparar un servidor SSH Linux aislado para pruebas de integración.
2. Probar cinco sesiones concurrentes.
3. Ejecutar una prueba prolongada para detectar crecimiento de memoria.
4. Verificar que UI, bandeja, cancelación y salida completa funcionen juntas.
5. Ejecutar analizadores, pruebas y compilación Release.
6. Publicar autocontenido para `win-x64`.
7. Documentar instalación, uso, límites y solución de problemas.

## Comandos de verificación

```powershell
dotnet restore
dotnet build --configuration Release --no-restore
dotnet test --configuration Release --no-build
dotnet publish src/VPSMonitor.App/VPSMonitor.App.csproj --configuration Release --runtime win-x64 --self-contained true
```

## Condición de finalización

La implementación termina únicamente cuando cumple los diez criterios de aceptación de la especificación, todas las pruebas automatizadas pasan y el artefacto autocontenido se inicia correctamente en Windows 11 x64.
