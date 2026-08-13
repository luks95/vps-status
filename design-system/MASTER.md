# VPS Monitor — Sistema visual

Fuente: reglas de UI/UX Pro Max adaptadas a una aplicación WPF de escritorio. La herramienta automática incluida en la habilidad no está disponible en la instalación local, por lo que este documento consolida manualmente las reglas aplicables.

## Dirección

- Producto: herramienta de monitoreo de infraestructura.
- Plataforma: Windows 11, escritorio, WPF.
- Estilo: oscuro, sobrio, denso pero legible, inspirado en Fluent Design.
- Prioridad: datos y estado operativo sobre decoración.
- Evitar: gradientes intensos, transparencias excesivas, brillo neón, sombras pesadas y animación ornamental.

## Colores semánticos

| Token | Valor | Uso |
|---|---:|---|
| `WindowBackground` | `#0B0F14` | Fondo de ventana |
| `SidebarBackground` | `#10161D` | Navegación |
| `Surface` | `#141B23` | Tarjetas y paneles |
| `SurfaceRaised` | `#19222C` | Controles elevados |
| `Border` | `#2A3541` | Bordes y separadores |
| `TextPrimary` | `#F4F7FA` | Texto principal |
| `TextSecondary` | `#A8B4C0` | Texto secundario |
| `TextMuted` | `#7E8B98` | Metadatos |
| `Accent` | `#27C2C1` | Serie principal y foco |
| `AccentBlue` | `#3B9CFF` | Serie secundaria y acción primaria |
| `Success` | `#42D17E` | Estado saludable |
| `Warning` | `#FFB020` | Advertencia |
| `Critical` | `#FF5C6C` | Error crítico |
| `Focus` | `#66B5FF` | Indicador de foco |

El texto normal debe mantener contraste mínimo 4.5:1. Líneas de datos y elementos gráficos deben mantener al menos 3:1 respecto del fondo. Todo estado por color incluye icono y texto.

## Tipografía

- Familia: `Segoe UI Variable`, con fallback `Segoe UI`.
- Escala: 12, 14, 16, 18, 24 y 30 px.
- Cuerpo predeterminado: 14 px.
- Encabezado de vista: 30 px, semibold.
- Encabezado de tarjeta: 16 px, semibold.
- Datos numéricos: cifras tabulares cuando la plataforma lo permita.
- No usar texto de cuerpo inferior a 12 px.

## Espaciado y forma

- Escala base: 4, 8, 12, 16, 24, 32 y 48 px.
- Altura mínima de controles: 40 px; acciones principales: 44 px.
- Separación mínima entre controles interactivos: 8 px.
- Radios: 6 px para controles y 8 px para tarjetas.
- Bordes: 1 px; elevación discreta solo para modales y menús.
- Ancho de barra lateral: 280 px; contenido con padding de 32 px en ventanas amplias y 20 px en ventanas compactas.

## Interacción

- Estados normal, hover, presionado, foco y deshabilitado definidos para cada control.
- Foco visible de 2 px, sin depender del color de fondo.
- Retroalimentación visual en menos de 100 ms.
- Transiciones entre 150 y 250 ms, cancelables y desactivables mediante reducción de movimiento.
- Una sola acción primaria por vista.
- Formularios con etiquetas persistentes, error junto al campo y foco en el primer error.
- Botones asíncronos se deshabilitan y muestran progreso mientras trabajan.

## Navegación

- Una única barra lateral persistente para destinos principales.
- La selección se expresa con fondo, indicador lateral, icono y peso tipográfico.
- Al cambiar de sección, el foco pasa al encabezado del contenido.
- Acciones destructivas se separan visualmente de las acciones habituales.

## Gráficas

- Tendencias temporales: líneas; capacidad de disco: barra de progreso.
- Ventana temporal rotulada como `Últimos 60 segundos`.
- Ejes con unidades explícitas y cuadrícula de bajo contraste.
- Tooltips accesibles por mouse y teclado cuando la biblioteca lo permita.
- Máximo 30 puntos visibles por serie.
- No mostrar símbolos en cada punto salvo al interactuar.
- Leyenda junto a las series múltiples.
- Estado vacío, cargando, no disponible y error con explicación textual.
- Cada gráfica tiene un resumen textual accesible con valor actual, mínimo y máximo.

## Diseño adaptable de escritorio

- Tamaño objetivo: 1440 × 900.
- Tamaño mínimo soportado: 1100 × 700.
- Por debajo de 1250 px, las tarjetas superiores pasan de cinco a tres columnas y el contenido secundario se envuelve.
- No se crean regiones de desplazamiento anidadas innecesarias.
- Debe funcionar con escalado de Windows de 100 %, 125 %, 150 % y 200 %.

## Lista de entrega

- Navegación completa por teclado.
- Orden de tabulación equivalente al orden visual.
- Nombres accesibles en botones de icono y gráficas.
- Contraste verificado.
- Estados de carga, vacío, error y desconexión.
- Sin emoji como iconos estructurales.
- Un único lenguaje de iconos vectoriales.
- Sin secretos visibles en UI, logs o archivos.
