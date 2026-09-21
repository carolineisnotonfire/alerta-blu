using Microsoft.Extensions.Logging;

namespace AlertaBlu.Infrastructure;

public static class LoggingBuilderExtensions
{
    /// <summary>
    /// Adds a file sink for Warning-and-above logs under <paramref name="logDirectory"/>, so
    /// upstream-scraping failures (the most likely failure mode of this app) are not silently
    /// dropped in Release builds, where the built-in Debug provider writes nothing.
    /// </summary>
    public static ILoggingBuilder AddAlertaBluFileLogging(this ILoggingBuilder logging, string logDirectory)
    {
        logging.AddProvider(new FileLoggerProvider(Path.Combine(logDirectory, "alertablu.log")));
        return logging;
    }
}
