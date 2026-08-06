using Notification.Engine.Models;
using Notification.Engine.Services;
using Xunit;

namespace Notification.Engine.Tests.Services;

public class EnvioDiarioFilterServiceTests
{
    private static EnvioDiarioFilterService CrearSut() => new();

    private static Hito H(int id, int diaMensual, string chatId = "100", string estado = "Pendiente",
        DateTime? reprogramar = null, bool enviaFinDeSemana = false) =>
        new(id, diaMensual, $"hito {id}", estado, reprogramar, MsgId: null, chatId, enviaFinDeSemana);

    [Fact]
    public void Filtrar_HitoVencidoHoy_SeIncluyeEnHitosPorChat()
    {
        var ahora = new DateTime(2026, 8, 5); // miércoles
        var hito = H(1, diaMensual: 5);

        var resultado = CrearSut().Filtrar([hito], ahora);

        Assert.Single(resultado.HitosPorChat["100"]);
        Assert.Empty(resultado.MarcarLunes);
        Assert.Empty(resultado.ChatsSinRecordatorios);
    }

    [Fact]
    public void Filtrar_HitoEnEstadoOk_SeExcluyeYChatQuedaSinRecordatorios()
    {
        var ahora = new DateTime(2026, 8, 5); // miércoles
        var hito = H(1, diaMensual: 5, estado: "OK");

        var resultado = CrearSut().Filtrar([hito], ahora);

        Assert.Empty(resultado.HitosPorChat);
        Assert.Contains("100", resultado.ChatsSinRecordatorios);
    }

    [Fact]
    public void Filtrar_HitoVencidoHoyFinDeSemanaSinEnviaFinDeSemana_VaAMarcarLunes()
    {
        var ahora = new DateTime(2026, 8, 1); // sábado
        var hito = H(1, diaMensual: 1, enviaFinDeSemana: false);

        var resultado = CrearSut().Filtrar([hito], ahora);

        Assert.Empty(resultado.HitosPorChat);
        var (hitoMarcado, lunes) = Assert.Single(resultado.MarcarLunes);
        Assert.Equal(1, hitoMarcado.Id);
        Assert.Equal(new DateOnly(2026, 8, 3), lunes); // sábado + 2
    }

    [Fact]
    public void Filtrar_HitoVencidoHoyFinDeSemanaConEnviaFinDeSemana_SeIncluyeEnMatching()
    {
        var ahora = new DateTime(2026, 8, 2); // domingo
        var hito = H(1, diaMensual: 2, enviaFinDeSemana: true);

        var resultado = CrearSut().Filtrar([hito], ahora);

        Assert.Single(resultado.HitosPorChat["100"]);
        Assert.Empty(resultado.MarcarLunes);
    }

    [Fact]
    public void Filtrar_MarcarLunes_DesdeDomingo_UsaElLunesSiguiente()
    {
        var ahora = new DateTime(2026, 8, 2); // domingo
        var hito = H(1, diaMensual: 2, enviaFinDeSemana: false);

        var resultado = CrearSut().Filtrar([hito], ahora);

        var (_, lunes) = Assert.Single(resultado.MarcarLunes);
        Assert.Equal(new DateOnly(2026, 8, 3), lunes); // domingo + 1
    }

    [Fact]
    public void Filtrar_HitoReprogramadoParaFechaPasada_SeConsideraVencidoHoy()
    {
        var ahora = new DateTime(2026, 8, 5);
        var hito = H(1, diaMensual: 1, reprogramar: new DateTime(2026, 8, 4));

        var resultado = CrearSut().Filtrar([hito], ahora);

        Assert.Single(resultado.HitosPorChat["100"]);
    }

    [Fact]
    public void Filtrar_HitoReprogramadoParaFechaFutura_NoSeIncluyeTodavia()
    {
        var ahora = new DateTime(2026, 8, 5); // miércoles, no aplica adelanto de viernes
        var hito = H(1, diaMensual: 1, reprogramar: new DateTime(2026, 8, 6));

        var resultado = CrearSut().Filtrar([hito], ahora);

        Assert.Empty(resultado.HitosPorChat);
        Assert.Contains("100", resultado.ChatsSinRecordatorios);
    }

    [Fact]
    public void Filtrar_ViernesConHitoHabilDelSabadoSiguiente_SeAdelantaAMatching()
    {
        var ahora = new DateTime(2026, 8, 28); // viernes; sábado = 29
        var hito = H(1, diaMensual: 29, enviaFinDeSemana: false);

        var resultado = CrearSut().Filtrar([hito], ahora);

        Assert.Single(resultado.HitosPorChat["100"]);
    }

    [Fact]
    public void Filtrar_ViernesConHitoQueSiEnviaFinDeSemana_NoSeAdelanta()
    {
        // El adelanto de viernes es solo para grupos "hábiles" (que no reciben fin de semana);
        // uno que sí recibe fin de semana ya se manda normalmente el sábado, sin adelanto.
        var ahora = new DateTime(2026, 8, 28); // viernes; sábado = 29
        var hito = H(1, diaMensual: 29, enviaFinDeSemana: true);

        var resultado = CrearSut().Filtrar([hito], ahora);

        Assert.Empty(resultado.HitosPorChat);
        Assert.Contains("100", resultado.ChatsSinRecordatorios);
    }

    [Fact]
    public void Filtrar_UltimoDiaDelMesLaborable_IncluyeHitoDeDiaInexistente()
    {
        var ahora = new DateTime(2026, 6, 30); // martes, último día de junio (30 días)
        var hito = H(1, diaMensual: 31, enviaFinDeSemana: false); // el 31 no existe en junio

        var resultado = CrearSut().Filtrar([hito], ahora);

        Assert.Single(resultado.HitosPorChat["100"]);
    }

    [Fact]
    public void Filtrar_UltimoDiaDelMesEnFinDeSemana_HitoInexistenteHabilVaAMarcarLunes()
    {
        var ahora = new DateTime(2026, 2, 28); // sábado, último día de febrero (28 días, no bisiesto)
        var hito = H(1, diaMensual: 30, enviaFinDeSemana: false); // el 30 no existe en febrero

        var resultado = CrearSut().Filtrar([hito], ahora);

        Assert.Empty(resultado.HitosPorChat);
        var (_, lunes) = Assert.Single(resultado.MarcarLunes);
        Assert.Equal(new DateOnly(2026, 3, 2), lunes); // sábado + 2
    }

    [Fact]
    public void Filtrar_UltimoDiaDelMesEnFinDeSemana_HitoInexistenteQueSiEnviaFinDeSemana_VaAMatching()
    {
        var ahora = new DateTime(2026, 2, 28); // sábado
        var hito = H(1, diaMensual: 30, enviaFinDeSemana: true);

        var resultado = CrearSut().Filtrar([hito], ahora);

        Assert.Single(resultado.HitosPorChat["100"]);
        Assert.Empty(resultado.MarcarLunes);
    }

    [Fact]
    public void Filtrar_DosHitosMismoChat_SeAgrupanJuntos()
    {
        var ahora = new DateTime(2026, 8, 5);
        var hitos = new[] { H(1, diaMensual: 5), H(2, diaMensual: 5) };

        var resultado = CrearSut().Filtrar(hitos, ahora);

        Assert.Equal(2, resultado.HitosPorChat["100"].Count);
    }

    [Fact]
    public void Filtrar_ChatEnFinDeSemanaSinNingunHitoQueEnvieFinDeSemana_NoApareceEnNingunLado()
    {
        var ahora = new DateTime(2026, 8, 1); // sábado
        var hito = H(1, diaMensual: 15, enviaFinDeSemana: false); // no vencido, ni sábado/domingo inmediato

        var resultado = CrearSut().Filtrar([hito], ahora);

        Assert.Empty(resultado.HitosPorChat);
        Assert.Empty(resultado.MarcarLunes);
        Assert.DoesNotContain("100", resultado.ChatsSinRecordatorios);
    }

    [Fact]
    public void Filtrar_ChatIdInvalido_SeExcluyeDeChatsSinRecordatorios()
    {
        var ahora = new DateTime(2026, 8, 5);
        var hito = H(1, diaMensual: 20, chatId: "0"); // no vencido, chat id inválido

        var resultado = CrearSut().Filtrar([hito], ahora);

        Assert.Empty(resultado.HitosPorChat);
        Assert.DoesNotContain("0", resultado.ChatsSinRecordatorios);
    }
}
