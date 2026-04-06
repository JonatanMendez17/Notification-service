namespace Notification.Api.Providers;

// Contrato base para cualquier proveedor de notificaciones
public interface INotificationProvider
{
    string Canal { get; }
    Task<bool> EnviarAsync(string mensaje);
}
