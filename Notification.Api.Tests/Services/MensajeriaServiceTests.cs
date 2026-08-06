using Microsoft.Extensions.Logging.Abstractions;
using Notification.Api.Models.Request;
using Notification.Api.Services;
using Notification.Api.Tests.Helpers;
using Xunit;

namespace Notification.Api.Tests.Services;

public class MensajeriaServiceTests
{
    private static MensajeriaService CrearSut(params FakeNotificationProvider[] providers) =>
        new(providers, NullLogger<MensajeriaService>.Instance);

    private static EnviarMensajeRequest Request(string canal, string destino = "12345") => new()
    {
        Sistema = "TGN",
        Canal = canal,
        Destino = destino,
        De = "Sistema",
        Para = "Juan",
        Titulo = "Aviso",
        Mensaje = "Contenido del mensaje"
    };

    [Fact]
    public async Task EnviarAsync_CanalNoRegistrado_DevuelveNoExitosoSinLlamarNingunProvider()
    {
        var telegram = new FakeNotificationProvider("Telegram", resultado: true);

        var respuesta = await CrearSut(telegram).EnviarAsync(Request("Email"));

        Assert.False(respuesta.Exitoso);
        Assert.Equal("Email", respuesta.Canal);
        Assert.Contains("Email", respuesta.Mensaje);
        Assert.Equal(0, telegram.Llamadas);
    }

    [Fact]
    public async Task EnviarAsync_CanalMatcheaSinImportarMayusculas_LlamaAlProviderCorrecto()
    {
        var telegram = new FakeNotificationProvider("Telegram", resultado: true);

        var respuesta = await CrearSut(telegram).EnviarAsync(Request("telegram"));

        Assert.True(respuesta.Exitoso);
        Assert.Equal("Telegram", respuesta.Canal); // usa el Canal del provider, no el del request
        Assert.Equal(1, telegram.Llamadas);
    }

    [Fact]
    public async Task EnviarAsync_ProviderFalla_DevuelveNoExitoso()
    {
        var telegram = new FakeNotificationProvider("Telegram", resultado: false);

        var respuesta = await CrearSut(telegram).EnviarAsync(Request("Telegram"));

        Assert.False(respuesta.Exitoso);
        Assert.Equal("Telegram", respuesta.Canal);
    }

    [Fact]
    public async Task EnviarAsync_PasaElDestinoTalCualAlProvider()
    {
        var telegram = new FakeNotificationProvider("Telegram", resultado: true);

        await CrearSut(telegram).EnviarAsync(Request("Telegram", destino: "-100123456"));

        Assert.Equal("-100123456", telegram.UltimoDestino);
    }

    [Fact]
    public async Task EnviarAsync_ConstruyeElTextoConDeParaTituloYMensaje()
    {
        var telegram = new FakeNotificationProvider("Telegram", resultado: true);
        var request = Request("Telegram");

        await CrearSut(telegram).EnviarAsync(request);

        var esperado = $"De: {request.De}\nPara: {request.Para}\n{request.Titulo}\n\n{request.Mensaje}";
        Assert.Equal(esperado, telegram.UltimoMensaje);
    }

    [Fact]
    public async Task EnviarAsync_ConVariosProviders_SoloLlamaAlQueMatchea()
    {
        var telegram = new FakeNotificationProvider("Telegram", resultado: true);
        var email = new FakeNotificationProvider("Email", resultado: true);

        await CrearSut(telegram, email).EnviarAsync(Request("Email"));

        Assert.Equal(0, telegram.Llamadas);
        Assert.Equal(1, email.Llamadas);
    }
}
