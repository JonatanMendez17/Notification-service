# Notification Service — Api + Engine

> Reemplazo de N8N para TGN, más gateway genérico de mensajería. Ver `README.md` para documentación orientada al usuario/consumidor de la API — este archivo es para trabajar en el código.

## Stack

- **.NET 8**, C#, sin ORM (ADO.NET directo con `Microsoft.Data.SqlClient` en el Engine)
- **Notification.Api**: ASP.NET Core 8 (Kestrel), gateway REST genérico multi-canal (hoy solo Telegram)
- **Notification.Engine**: Worker Service (`BackgroundService` × 5 jobs + 1 receiver), habla con SQL Server y Telegram de forma bidireccional
- Logging: Serilog (consola + archivo rotativo diario, `log/<Proyecto>/*_yyyyMMdd.txt`)

## Estructura

```
Notification-service/
├── Notification.Api/
│   ├── Controllers/        # MensajeriaController
│   ├── Services/           # MensajeriaService (valida token, formatea)
│   ├── Providers/          # TelegramProvider (INotificationProvider)
│   ├── Models/, Settings/
├── Notification.Engine/
│   ├── Jobs/               # 5 BackgroundService, uno por workflow de N8N
│   ├── Telegram/           # TelegramBotClient, PollingReceiver, RespuestaRegistroHandler
│   ├── Data/               # HitosRepository, GruposRepository, SqlDataAccess (ADO.NET crudo)
│   ├── Services/           # EnvioDiarioFilterService
│   ├── Models/, Settings/
└── Deploy/                 # notas de diseño de deploy (si existen), no implementado todavía
```

## ⚠️ Conexión a BD — TGN-dev/Engine usan una base DISTINTA a la de red

Antes de correr cualquier script SQL o migración, **verificar la connection string real**, no asumir:

| Entorno | Host | Base | Usa |
|---|---|---|---|
| `TGN-master` (Web) | `172.18.131.46` | `TGN` | `Web/Global.asax` |
| `TGN-dev` (Web) + `Notification.Engine` en dev | `CUAD-4041` | `TGN-TEST-ENGINE` | `Web/Global.asax` de TGN-dev / `appsettings.Development.json` |

User: `sa` / Password: `Admin_jm` en ambas.

**Por qué importa:** ya pasó que se corrió una migración contra `172.18.131.46/TGN` (la de red, la "obvia") cuando el código que realmente se estaba probando usaba `CUAD-4041/TGN-TEST-ENGINE` — rompió `hitos.aspx` con "nombre de columna no válido" hasta que se corrió también ahí. Cualquier cambio de schema que afecte a TGN se prueba en `CUAD-4041/TGN-TEST-ENGINE`; recién se replica a `172.18.131.46/TGN` cuando el código llega a `master`.

## Notification.Engine — los 5 jobs

Cada uno es un `BackgroundService` con su propio `PeriodicTimer`; la condición real de disparo (hora, día) vive en el SQL de `HitosRepository`, no en el timer.

| Job | Cadencia | Qué hace |
|---|---|---|
| `EnvioDiarioJob` | 1h | Manda el recordatorio inicial (cabecera + mensaje con botones) para hitos "vencidos hoy" cuya hora ya llegó |
| `ReprogramarJob` | 1h (`Parametria.hora_revision`) | Hitos sin respuesta hoy → reprograma a mañana, edita el mensaje a `⏰ {hito}` |
| `ReinicioMensualJob` | 1h (`Parametria.hora_reset`, días 1/15) | Resetea hitos `OK` → `Pendiente` |
| `ActualizacionesTiempoRealJob` | 5s | Sincroniza a Telegram (edita mensaje) los cambios de estado hechos desde la app web |
| `RespuestaRegistroHandler` + `PollingReceiver` | 5s (dev) | Recibe updates de Telegram: `/registrar` (alta de grupo), botones OK/Posponer |

`PollingReceiver` es dev-only (`getUpdates`); en server debería reemplazarse por webhook — **no implementado, punto pendiente conocido**.

### Cascada de hora de envío (hito > grupo > parametría)

`HitosRepository.SqlPendientesEnvioDiario` resuelve la hora con `ISNULL` anidado, de más a menos específico:

1. `Hitos_Mensuales.Hora_Envio` (por hito, `hitos.aspx`)
2. `Tg_Grupo.Tgg_Hora_Envio` (por grupo, `conf_grupos.aspx`)
3. `Parametria.hora_envio_diario` (global, `conf_parametros.aspx`)

`Hitos_Mensuales.Envia_Fin_Semana` (booleano, por hito — **ya no existe a nivel grupo**, se movió acá) controla si ese hito puntual recibe recordatorios en fin de semana. `EnvioDiarioFilterService.Filtrar` aplica la lógica de "vencido hoy" (día del mes / reprogramar) + exclusión de fin de semana + adelanto de viernes — es puro C#, sin acceso a datos, fácil de testear.

**Se evaluó y se descartó** (2026-07-24) un mecanismo de "envío inmediato" (job de 5s + flag `tg_actualizar` propagado desde los 3 formularios) para no esperar hasta la próxima hora en punto cuando se ajusta una hora ya pasada. Se consideró demasiado engorroso para el beneficio — **se prefiere la simplicidad de `EnvioDiarioJob` corriendo cada hora**, aceptando que un cambio de hora se aplica recién en la próxima ventana horaria. No reintentar este enfoque sin que el usuario lo pida explícitamente de nuevo.

## Gotchas de Telegram (`Telegram/TelegramBotClient.cs`)

- **No se usa `parse_mode`** en ningún mensaje — los mensajes son texto plano. No usar `*negrita*` ni `_cursiva_` pensando que se va a renderizar, aparece literal.
- `reply_markup` con `null` explícito en el JSON hace que Telegram rechace el request (`400 object expected as reply markup`) en vez de ignorarlo — por eso `SendMessagePayload.ReplyMarkup` tiene `[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]`. Si se agrega un nuevo payload con `reply_markup` opcional, replicar ese atributo.
- `EditarMensajeAsync` siempre manda `reply_markup` con `inline_keyboard: []` (nunca null) — así es como se sacan los botones de un mensaje ya resuelto.
- **Reintentos y rate limit**: `EnviarPeticionAsync` (Engine) y `TelegramProvider.EnviarAsync` (Api) reintentan hasta 3 veces (backoff 1s/2s) solo errores transitorios — excepción, 5xx, o 429 (leyendo `parameters.retry_after` de Telegram, con tope de 5s). Un error permanente (ej. 400 "message can't be edited") no se reintenta, se devuelve al toque. `ResponderCallbackAsync` queda afuera del retry a propósito (es cosmético, no vale la latencia extra).

## Callbacks duplicados (doble tap de botón)

`RegistrarHitoOkAsync` / `RegistrarHitoPospuestoAsync` (`Data/HitosRepository.cs`) hacen `UPDATE ... WHERE <condición de que todavía no está en ese estado>` y devuelven las filas afectadas. `0` filas = ya estaba resuelto (double-tap, o dos personas del mismo grupo tocando el mismo botón) → el handler (`RespuestaRegistroHandler.ProcesarCallbackAsync`) lo loguea y corta sin reprocesar ni volver a editar el mensaje en Telegram. Si se agrega una acción nueva de callback, seguir el mismo patrón (UPDATE condicional + chequear rows affected) en vez de un SELECT-then-UPDATE separado.

## Patrón VB en TGN (`hitos.aspx`, `conf_grupos.aspx`, `conf_parametros.aspx`)

SQL armado con reemplazo de placeholders `\1`, `\2`, ... `\N` sobre un string (no parametrizado, ver `cValida` para sanitizar). **Cuidado con `\1` siendo substring de `\10`, `\11`, etc.** — si se necesita un placeholder de dos dígitos, reemplazarlo *antes* que los de un dígito, o el `.Replace("\1", ...)` corrompe `\10` a mitad de camino.

## Testing manual del Engine

- `dotnet run` en `Notification.Engine/` levanta los 5 jobs contra la BD configurada en `appsettings.Development.json`.
- **No correr dos instancias del Engine en simultáneo** (una por VS/F5 + otra por terminal, por ejemplo) — ambas comparten el mismo bot de Telegram (`getUpdates`) y pueden competir por procesar los mismos hitos, dando resultados cruzados difíciles de interpretar en los logs.
- Logs en `Notification.Engine/bin/Debug/net8.0/log/Notification.Engine/engine_yyyyMMdd.txt` — más confiable para diagnosticar que asumir por los mensajes de Telegram recibidos, ya que ahí se ve exactamente qué job actuó y por qué.
