using Microsoft.Extensions.Logging;
using PhoneCardTracker.Services;
using PhoneCardTracker.ViewModels;
using PhoneCardTracker.Views;

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

            // --- Core Services ---
            builder.Services.AddSingleton<DatabaseService>();
            builder.Services.AddSingleton<NetworkSyncManager>();
            builder.Services.AddSingleton<FirebaseSyncService>();

            // --- ViewModels & Pages ---
            builder.Services.AddSingleton<MainViewModel>();
            builder.Services.AddTransient<MainPage>();
            builder.Services.AddTransient<CardListPage>();

            return builder.Build();
        }
    }
}