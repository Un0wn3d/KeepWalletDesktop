using Serilog;
using Serilog.Events;

namespace KeepWalletDesktop.Services
{
    internal class SerilogConfig
    {
        public static void Initialize()
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
#if DEBUG
                .WriteTo.Console()
#endif
                .WriteTo.File(
                    "logs/log-.log", 
                    rollingInterval: RollingInterval.Day, 
                    retainedFileCountLimit: 7,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
#if DEBUG
                .WriteTo.File(
                    path: "logs/debug-.log",
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 7,
                    restrictedToMinimumLevel: LogEventLevel.Debug,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
                    .WriteTo.Debug()
#endif
                .CreateLogger();

        }
    }
}
