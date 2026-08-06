using System.Globalization;
using Serilog.Events;
using Serilog.Formatting;

namespace Notification.Api.Infrastructure.Logging;

// Formatter de consola: nivel como palabra corta al principio de la línea, coloreada
// (Info verde, Warn amarillo, Error rojo) via Console.ForegroundColor — más confiable en
// consolas de Windows que códigos ANSI crudos. Solo afecta a WriteTo.Console(); el archivo
// de log sigue con su formato de siempre, sin códigos de color.
public class ConsoleLevelFormatter : ITextFormatter
{
    public void Format(LogEvent logEvent, TextWriter output)
    {
        var (palabra, color) = logEvent.Level switch
        {
            LogEventLevel.Verbose => ("Trace", ConsoleColor.DarkGray),
            LogEventLevel.Debug => ("Debug", ConsoleColor.DarkGray),
            LogEventLevel.Information => ("Info ", ConsoleColor.Green),
            LogEventLevel.Warning => ("Warn ", ConsoleColor.Yellow),
            LogEventLevel.Error => ("Error", ConsoleColor.Red),
            LogEventLevel.Fatal => ("Fatal", ConsoleColor.Red),
            _ => (logEvent.Level.ToString(), ConsoleColor.Gray)
        };

        var original = Console.ForegroundColor;
        Console.ForegroundColor = color;
        output.Write(palabra);
        Console.ForegroundColor = original;

        output.Write(' ');
        output.Write(logEvent.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff zzz", CultureInfo.InvariantCulture));
        output.Write(' ');
        logEvent.RenderMessage(output);
        output.WriteLine();

        if (logEvent.Exception is not null)
        {
            output.WriteLine(logEvent.Exception);
        }
    }
}
