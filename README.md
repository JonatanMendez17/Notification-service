# Notification Service

API centralizada para el envío de notificaciones y mensajes a través de múltiples canales. Actualmente soporta **Telegram**, con una arquitectura extensible para agregar nuevos proveedores (WhatsApp, Email, SMS, etc.).

---

## Tabla de Contenidos

- [¿Qué es?](#qué-es)
- [Stack Tecnológico](#stack-tecnológico)
- [Arquitectura](#arquitectura)
- [Endpoints](#endpoints)
- [Configuración](#configuración)
- [Instalación y Ejecución](#instalación-y-ejecución)
- [Cómo Consumirlo](#cómo-consumirlo)
- [Extensibilidad](#extensibilidad)

---

## ¿Qué es?

Notification Service es una **API REST** construida con **ASP.NET Core 8** que actúa como intermediario centralizado para que otros sistemas envíen mensajes de forma segura mediante un token de autorización, abstrayendo la complejidad de cada proveedor de mensajería.

**Casos de uso:**
- Alertas y notificaciones de sistemas internos
- Avisos automáticos desde pipelines CI/CD
- Notificaciones de errores o eventos críticos en producción

---

## Stack Tecnológico

| Capa | Tecnología |
|---|---|
| Runtime | .NET 8.0 |
| Lenguaje | C# |
| Framework Web | ASP.NET Core 8 |
| Configuración | Microsoft.Extensions.Options (Options Pattern) |
| HTTP Client | HttpClientFactory |
| Canal actual | Telegram Bot API |

---

## Arquitectura

El servicio aplica una arquitectura limpia con separación de responsabilidades:

```
Notification-service/
└── Notification.Api/
    ├── Controllers/        # Recibe y valida las solicitudes HTTP
    ├── Services/           # Lógica de negocio (validación de token, formateo)
    ├── Providers/          # Implementaciones de cada canal (Telegram, ...)
    ├── Models/             # Request y Response DTOs
    └── Settings/           # Configuración tipada (ApiSettings, TelegramSettings)
```

**Flujo de una solicitud:**

```
Cliente → Controller → Service (valida token + formatea) → Provider → Canal externo
```

**Patrones aplicados:** Provider Pattern, Options Pattern, Dependency Injection, Async/Await.

---

## Endpoints

### `POST /api/mensajeria/enviarMsgTG`

Envía un mensaje al canal de Telegram configurado.

**Request:**

```http
POST /api/mensajeria/enviarMsgTG
Content-Type: application/json
```

```json
{
  "tokenBearer": "tu-token-seguro",
  "sistema": "NombreDelSistema",
  "de": "Remitente",
  "para": "Destinatario",
  "titulo": "Asunto del mensaje",
  "mensaje": "Cuerpo del mensaje"
}
```

| Campo | Tipo | Requerido | Descripción |
|---|---|---|---|
| `tokenBearer` | string | Sí | Token de autorización de la API |
| `sistema` | string | Sí | Nombre del sistema que origina la solicitud |
| `de` | string | Sí | Remitente |
| `para` | string | Sí | Destinatario |
| `titulo` | string | Sí | Asunto o título del mensaje |
| `mensaje` | string | Sí | Contenido del mensaje |

**El mensaje llega a Telegram con este formato:**

```
De: Remitente
Para: Destinatario
Asunto del mensaje

Cuerpo del mensaje
```

**Respuestas:**

| Código | Descripción | Cuerpo |
|---|---|---|
| `200 OK` | Mensaje enviado correctamente | `{ "exitoso": true, "mensaje": "...", "canal": "Telegram", "timestamp": "..." }` |
| `400 Bad Request` | Algún campo requerido está vacío o ausente | Detalle de errores de validación |
| `401 Unauthorized` | Token de autorización inválido | `{ "exitoso": false, "mensaje": "Token de autorización inválido.", ... }` |
| `500 Internal Server Error` | Error interno al enviar | `{ "exitoso": false, "mensaje": "Error interno del servidor.", ... }` |

---

## Configuración

La configuración se gestiona en `Notification.Api/appsettings.json`:

```json
{
  "ApiSettings": {
    "TokenBearer": "CAMBIAR-POR-TOKEN-SEGURO"
  },
  "Telegram": {
    "Token": "<token-del-bot-de-telegram>",
    "ChatId": "<id-del-chat-o-grupo>"
  }
}
```

| Clave | Descripción |
|---|---|
| `ApiSettings:TokenBearer` | Token que deben enviar los consumidores en cada solicitud |
| `Telegram:Token` | Token del bot obtenido desde [@BotFather](https://t.me/botfather) |
| `Telegram:ChatId` | ID del chat, grupo o canal donde se enviarán los mensajes |

> **Importante:** En entornos productivos, no almacenes credenciales en `appsettings.json`. Usa variables de entorno o un gestor de secretos (Azure Key Vault, AWS Secrets Manager, etc.).

**Cómo obtener las credenciales de Telegram:**
1. Crea un bot en Telegram hablando con [@BotFather](https://t.me/botfather) y obtienes el `Token`
2. Agrega el bot al grupo o canal donde quieres recibir mensajes
3. Obtén el `ChatId` usando `@userinfobot` o consultando la API de Telegram (`/getUpdates`)

---

## Instalación y Ejecución

### Requisitos

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Git

### Pasos

```bash
# 1. Clonar el repositorio
git clone https://github.com/JonatanMendez17/Notification-service.git
cd Notification-service

# 2. Restaurar dependencias
dotnet restore

# 3. Configurar credenciales en Notification.Api/appsettings.json

# 4. Ejecutar
cd Notification.Api
dotnet run
```

La API queda disponible en:
- **HTTPS:** `https://localhost:51391`
- **HTTP:** `http://localhost:51392`

---

## Cómo Consumirlo

### PowerShell

```powershell
$body = @{
    tokenBearer = "tu-token-aqui"
    sistema     = "MiSistema"
    de          = "Monitor"
    para        = "Equipo Dev"
    titulo      = "Alerta de produccion"
    mensaje     = "Se detectó un error crítico en el servicio de pagos."
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://localhost:51392/api/mensajeria/enviarMsgTG" `
    -Method POST `
    -ContentType "application/json" `
    -Body $body
```


