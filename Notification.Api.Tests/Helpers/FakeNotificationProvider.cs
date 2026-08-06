using Notification.Api.Channels;

namespace Notification.Api.Tests.Helpers;

public class FakeNotificationProvider(string canal, bool resultado) : INotificationProvider
{
    public string Canal { get; } = canal;
    public string? UltimoDestino { get; private set; }
    public string? UltimoMensaje { get; private set; }
    public int Llamadas { get; private set; }

    public Task<bool> EnviarAsync(string destino, string mensaje)
    {
        Llamadas++;
        UltimoDestino = destino;
        UltimoMensaje = mensaje;
        return Task.FromResult(resultado);
    }
}
