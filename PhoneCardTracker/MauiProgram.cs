using Microsoft.Extensions.Logging;
using PhoneCardTracker.Services;

namespace PhoneCardTracker
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

#if DEBUG
            builder.Logging.AddDebug();
#endif

            // --- Register Core Backend Services ---
            builder.Services.AddSingleton<DatabaseService>();
            builder.Services.AddSingleton<NetworkSyncManager>();

            // --- Register Pages & ViewModels ---
            builder.Services.AddTransient<MainPage>();

            return builder.Build();
        }
    }
}