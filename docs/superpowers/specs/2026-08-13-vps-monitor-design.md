# VPS Monitor para Windows — Diseño del MVP

Fecha: 2026-08-13

## 1. Objetivo

Construir una aplicación de escritorio para Windows que monitoree en tiempo real hasta cinco VPS Linux mediante SSH. La interfaz seguirá una jerarquía visual inspirada en la sección Rendimiento del Administrador de tareas de Windows 11, sin copiarla literalmente.

El producto es exclusivamente de observación. No permitirá ejecutar comandos arbitrarios, reiniciar servidores, terminar procesos ni modificar la configuración remota.

## 2. Alcance del MVP

### Incluido

- Aplicación de escritorio para Windows x64.
- Registro de hasta cinco VPS mediante nombre, IP o dominio, puerto SSH, usuario y contraseña.
- Autenticación SSH por usuario y contraseña.
- Almacenamiento seguro de contraseñas en el almacén de credenciales de Windows.
- Confirmación y persistencia de la huella digital de la clave de host SSH.
- Monitoreo simultáneo y en tiempo real de las VPS registradas.
- Métricas de CPU, memoria, discos, red, carga, procesos, uptime, temperatura cuando esté disponible y latencia.
- Gráficas con una ventana móvil de los últimos 60 segundos.
- Alertas globales editables y sobrescritura opcional por VPS.
- Funcionamiento en segundo plano desde la bandeja del sistema.
- Notificaciones nativas de Windows.
- Tema oscuro y navegación accesible mediante teclado.
- Distribución autocontenida para que el usuario no tenga que instalar .NET.

### Excluido

- Historial persistente de métricas.
- Administración remota o terminal SSH interactiva.
- Autenticación mediante clave privada.
- Servidores Windows.
- Aplicaciones para macOS, Linux, web o dispositivos móviles.
- Sincronización en la nube y cuentas multiusuario.
- Monitoreo mediante un agente instalado en la VPS.

## 3. Stack tecnológico

- .NET 10 LTS y C#.
- WPF para la aplicación de escritorio.
- MVVM para separar presentación, estado y servicios.
- SSH.NET para conexiones SSH.
- LiveCharts2 para gráficas en tiempo real.
- SQLite para metadatos y configuración local.
- Credential Manager de Windows mediante credenciales genéricas locales (`CredWrite`/`CredRead`) para las contraseñas. Las credenciales no se sincronizan entre equipos.
- APIs de Windows para bandeja del sistema y notificaciones.
- Publicación autocontenida para `win-x64`.

## 4. Arquitectura

La solución se organizará en proyectos con responsabilidades delimitadas:

### VPSMonitor.App

Aplicación WPF, recursos visuales, navegación, vistas, ViewModels, integración con la bandeja y composición de dependencias.

### VPSMonitor.Application

Casos de uso y coordinación. Incluye gestión de servidores, ciclo de monitoreo, alertas y contratos para infraestructura.

### VPSMonitor.Domain

Modelos y reglas que no dependen de WPF ni de servicios externos: definición de VPS, muestras de métricas, estados de conexión, límites de alerta y búfer circular.

### VPSMonitor.Infrastructure

Implementaciones de SSH, recolección Linux, SQLite, credenciales de Windows, latencia, notificaciones y registro seguro de eventos.

### VPSMonitor.Tests

Pruebas unitarias y de integración de los componentes anteriores.

## 5. Componentes principales

### Coordinador de monitoreo

Mantiene una sesión independiente por VPS habilitada. Inicia, detiene y cancela cada ciclo sin afectar a los demás servidores. El límite funcional inicial es de cinco VPS concurrentes.

### Gestor de sesiones SSH

Abre y reutiliza conexiones SSH, aplica timeouts, valida la clave de host y clasifica los fallos de conexión. En una desconexión involuntaria reintenta después de 2, 5, 10, 20 y finalmente 30 segundos; los intentos posteriores conservan el máximo de 30 segundos. Una conexión recuperada reinicia la secuencia.

### Recolector Linux

Ejecuta un conjunto fijo de consultas de solo lectura. Utiliza principalmente `/proc` y `/sys`, con comandos estándar como `df` cuando resulta necesario. No acepta texto de comando proveniente de la interfaz.

### Medidor de latencia

Intenta primero ICMP. Si ICMP está bloqueado o no es concluyente, usa el tiempo de establecimiento TCP contra el puerto SSH. La interfaz identifica el método utilizado para que el valor no se interprete incorrectamente.

### Procesador de métricas

Convierte los datos remotos en valores normalizados. CPU, velocidades de red y actividad de disco se calculan a partir de la diferencia entre muestras sucesivas. Cada VPS conserva en memoria un búfer circular de 60 segundos.

### Motor de alertas

Evalúa cada muestra contra límites globales o límites específicos de la VPS. Usa duración mínima, histéresis y enfriamiento para evitar alertas por picos aislados y notificaciones repetitivas.

### Almacenes locales

SQLite guarda nombres, direcciones, puertos, usuarios, preferencias, límites y huellas SSH aprobadas. Las contraseñas se guardan exclusivamente en el almacén seguro de Windows y se referencian mediante un identificador interno.

## 6. Métricas

### CPU

- Uso total porcentual.
- Carga media de 1, 5 y 15 minutos.
- Datos base obtenidos de `/proc/stat` y `/proc/loadavg`.

### Memoria

- Memoria total, usada, disponible y porcentaje de uso.
- Swap total y usada cuando exista.
- Datos obtenidos de `/proc/meminfo`, usando memoria disponible para evitar sobrestimar el consumo por caché.

### Disco

- Espacio usado, libre y porcentaje por sistema de archivos relevante.
- Actividad de lectura y escritura cuando el kernel exponga los contadores necesarios.
- Sistemas virtuales y pseudo-sistemas de archivos quedan excluidos del resumen principal.

### Red

- Velocidad de recepción y transmisión por segundo.
- Total agregado de interfaces activas, excluyendo loopback.
- Datos obtenidos de `/proc/net/dev`.

### Sistema

- Uptime.
- Cantidad total de procesos.
- Temperatura cuando exista una fuente legible en `/sys/class/thermal`.
- Distribución y versión del kernel como información secundaria.

### Latencia

- Milisegundos por ICMP cuando esté permitido.
- Milisegundos de establecimiento TCP al puerto SSH como alternativa.
- Timeout y método de medición visibles en la interfaz.

Una métrica opcional ausente se presenta como `No disponible` y no convierte la sesión completa en un error.

## 7. Flujo de actualización

Cada VPS habilitada ejecuta el siguiente ciclo con un intervalo objetivo de dos segundos:

1. Mide latencia sin bloquear la recolección SSH.
2. Obtiene una instantánea mediante la sesión SSH existente.
3. Interpreta y valida la respuesta.
4. Calcula valores derivados respecto de la muestra anterior.
5. Agrega el resultado al búfer circular.
6. Publica un estado inmutable para la interfaz.
7. Evalúa las reglas de alerta.

El siguiente ciclo no se superpone con uno anterior del mismo servidor. Si una consulta tarda demasiado, se cancela por timeout y se registra un estado degradado. Los servidores se procesan de forma concurrente y aislada.

## 8. Interfaz

### Navegación principal

- Resumen.
- Servidores.
- Alertas.
- Configuración.

La barra lateral también muestra las VPS registradas con estados de conectada, advertencia, desconectada o reconectando.

### Panel de servidor

El encabezado muestra nombre, dirección, distribución, estado SSH y momento de la última muestra. El contenido presenta tarjetas de uptime, carga, procesos, temperatura y conexión; gráficas principales de CPU y memoria; y paneles de disco, red y latencia.

Las gráficas muestran los últimos 60 segundos, se actualizan cada dos segundos y usan líneas de cuadrícula discretas. El color refuerza el estado, pero nunca es el único indicador.

### Alta y edición de VPS

El formulario contiene:

- Nombre visible.
- IP o dominio.
- Puerto, con valor predeterminado 22.
- Usuario.
- Contraseña.
- Opción para habilitar el monitoreo.
- Acción `Probar conexión`.

En la primera conexión exitosa se muestra la huella SSH y se solicita confirmación explícita antes de confiar en ella.

### Segundo plano

Cerrar la ventana la oculta y mantiene el monitoreo en la bandeja. El menú de bandeja permite abrir la aplicación o cerrarla completamente. La interfaz explica este comportamiento la primera vez.

## 9. Alertas

Los valores iniciales serán:

- CPU superior a 90 %.
- Memoria superior a 85 %.
- Disco superior a 80 %.
- Latencia superior a 200 ms.
- VPS inaccesible.

Los límites porcentuales y de latencia requieren tres muestras consecutivas fuera del rango antes de activar una alerta. Una VPS se considera inaccesible después de tres ciclos fallidos consecutivos. La recuperación requiere dos muestras consecutivas por debajo del límite menos cinco puntos porcentuales para CPU, memoria y disco, o menos 20 ms para latencia. Una alerta activa no vuelve a notificarse durante cinco minutos, salvo que cambie de severidad. Los umbrales principales se podrán editar globalmente y sobrescribir por VPS; los valores de estabilidad anteriores son constantes del MVP.

## 10. Seguridad

- Las contraseñas no se almacenan en SQLite, archivos de configuración ni registros.
- Los controles de contraseña no conservan el valor más tiempo del necesario.
- Los mensajes de error eliminan credenciales y datos sensibles.
- La clave de host se valida en cada conexión.
- Una huella nueva requiere confirmación inicial.
- Una huella previamente aprobada que cambie bloquea la conexión hasta que el usuario revise y apruebe el cambio.
- Los comandos remotos son constantes internas y de solo lectura.
- No se utiliza un shell interactivo.
- Las operaciones admiten cancelación y timeout.
- Los registros locales contienen eventos técnicos mínimos y aplican rotación.

## 11. Manejo de errores

La interfaz distingue al menos los siguientes casos:

- Dirección no resoluble o host inaccesible.
- Puerto cerrado.
- Timeout.
- Credenciales rechazadas.
- Huella SSH desconocida.
- Huella SSH modificada.
- Sesión interrumpida.
- Respuesta parcial o formato Linux inesperado.
- Métrica opcional no disponible.

Cada error incluye una explicación breve y una acción útil cuando corresponda, como reintentar, editar credenciales o revisar la huella. La falta de una métrica opcional no detiene el resto del monitoreo.

## 12. Compatibilidad Linux

El MVP admite distribuciones Linux modernas que expongan las interfaces estándar `/proc` y `/sys` y permitan una sesión SSH no interactiva. La recolección principal no depende de un gestor de paquetes ni de systemd. Las diferencias de distribución se detectan y se degradan por métrica.

## 13. Accesibilidad y calidad visual

- Contraste mínimo adecuado para texto y controles.
- Navegación completa mediante teclado.
- Indicadores textuales e iconográficos además del color.
- Foco visible.
- Etiquetas accesibles para gráficas y controles con iconos.
- Resumen textual del valor actual de cada gráfica.
- Animaciones discretas y compatibles con reducción de movimiento.
- Escalado correcto con la configuración de DPI de Windows.

## 14. Estrategia de pruebas

### Pruebas unitarias

- Parsers de `/proc/stat`, `/proc/meminfo`, `/proc/net/dev`, `/proc/diskstats`, `/proc/loadavg` y uptime.
- Cálculos entre muestras, incluyendo reinicio o desbordamiento de contadores.
- Búfer circular de 60 segundos.
- Reglas de alerta, duración mínima, histéresis y enfriamiento.
- Reconexión progresiva, timeout y cancelación.
- ViewModels y navegación sin depender de la interfaz gráfica.

### Pruebas de integración

- Conexión contra un servidor SSH Linux aislado para respuestas válidas y fallos controlados.
- Autenticación correcta e incorrecta.
- Confirmación, persistencia y cambio de huella SSH.
- Lectura y eliminación de credenciales mediante una abstracción del almacén de Windows.
- Persistencia de configuración sin secretos.

### Verificaciones de entrega

- Compilación y ejecución en Windows 11 x64.
- Publicación autocontenida limpia.
- Monitoreo simultáneo de cinco VPS o instancias de prueba.
- Consumo estable de memoria durante una ejecución prolongada.
- Cierre, restauración y salida desde la bandeja.
- Notificaciones de alerta y recuperación.
- Inspección de SQLite, configuración y registros para confirmar que no contienen contraseñas.

## 15. Criterios de aceptación

El MVP se considera terminado cuando:

1. El usuario puede registrar, editar y eliminar hasta cinco VPS Linux.
2. Puede probar credenciales y aprobar la huella SSH antes de guardar una conexión confiable.
3. La contraseña persiste únicamente en el almacén seguro de Windows.
4. La aplicación muestra las métricas acordadas en tiempo real para cada VPS disponible.
5. Las gráficas mantienen una ventana móvil de 60 segundos sin persistencia histórica.
6. Una VPS desconectada no bloquea ni degrada el monitoreo de las demás.
7. Las alertas globales y específicas generan notificaciones sin repetición excesiva.
8. La aplicación continúa monitoreando desde la bandeja cuando se cierra la ventana.
9. No existe una función para ejecutar comandos arbitrarios o modificar la VPS.
10. El paquete autocontenido funciona en Windows 11 x64 sin una instalación previa de .NET.

## 16. Evolución posterior al MVP

Las siguientes capacidades quedan deliberadamente fuera de la primera versión, pero la arquitectura no debe impedir incorporarlas más adelante:

- Autenticación mediante clave privada.
- Historial persistente y comparación por períodos.
- Más de cinco VPS.
- Importación y exportación de configuración sin secretos.
- Servidores Windows y plataformas de escritorio adicionales.
- Monitoreo opcional mediante agente.
