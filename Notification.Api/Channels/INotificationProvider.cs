namespace Notification.Api.Channels;

// Contrato base para cualquier proveedor de notificaciones
public interface INotificationProvider
{
    string Canal { get; }
    Task<bool> EnviarAsync(string destino, string mensaje);
}
