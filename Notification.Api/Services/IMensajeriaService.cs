using Notification.Api.Models.Request;
using Notification.Api.Models.Response;

namespace Notification.Api.Services;

public interface IMensajeriaService
{
    Task<EnviarMensajeResponse> EnviarTelegramAsync(EnviarMensajeRequest request, string token);
}
