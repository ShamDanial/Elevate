using Elevate.Data;
using Microsoft.Extensions.Logging;
using Plugin.LocalNotification;

namespace Elevate
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

            // ── SQLite database ──────────────────────────────────────────────
            string dbPath = Path.Combine(FileSystem.AppDataDirectory, "elevate.db3");
            builder.Services.AddSingleton(new ElevateDatabase(dbPath));

            builder.UseLocalNotification();

            // ── Pages ────────────────────────────────────────────────────────
            builder.Services.AddTransient<Pages.LoginPage>();
            builder.Services.AddTransient<Pages.SplashPage>();
            builder.Services.AddTransient<Pages.HomePage>();
            builder.Services.AddTransient<Pages.LoggerPage>();
            builder.Services.AddTransient<Pages.PlannerPage>();
            builder.Services.AddTransient<Pages.AnalyticsPage>();
            builder.Services.AddTransient<Pages.ProgressPage>();
            builder.Services.AddTransient<Pages.AlertsPage>();
            builder.Services.AddTransient<Pages.AboutPage>();
            builder.Services.AddTransient<Pages.SettingsPage>();

#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}