using Elevate.Data;
using System.Text.Json;

namespace Elevate.Pages;

public partial class SettingsPage : ContentPage
{
    private readonly ElevateDatabase _db;

    public SettingsPage(ElevateDatabase db)
    {
        InitializeComponent();
        _db = db;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadSettingsAsync();
    }

    private async Task LoadSettingsAsync()
    {
        try
        {
            string json = await _db.GetSettingsAsync();
            var settings = JsonSerializer.Deserialize<Dictionary<string, bool>>(json);
            if (settings == null) return;

            RealTimeAlertsSwitch.IsToggled = settings.GetValueOrDefault("realTimeAlerts", true);
            PostRemindersSwitch.IsToggled = settings.GetValueOrDefault("postReminders", false);
            AnalyticsSwitch.IsToggled = settings.GetValueOrDefault("analyticsTracking", true);
            AutoBackupSwitch.IsToggled = settings.GetValueOrDefault("autoBackup", false);
        }
        catch { /* defaults already set in XAML */ }
    }

    private async void OnSwitchToggled(object sender, ToggledEventArgs e)
    {
        await SaveSettingsAsync();
    }

    private async Task SaveSettingsAsync()
    {
        var settings = new Dictionary<string, bool>
        {
            ["realTimeAlerts"] = RealTimeAlertsSwitch.IsToggled,
            ["postReminders"] = PostRemindersSwitch.IsToggled,
            ["analyticsTracking"] = AnalyticsSwitch.IsToggled,
            ["autoBackup"] = AutoBackupSwitch.IsToggled,
        };
        await _db.SaveSettingsAsync(JsonSerializer.Serialize(settings));
    }

    private async void OnBackTapped(object sender, EventArgs e) =>
        await Shell.Current.GoToAsync("///HomePage");

    private async void OnExportTapped(object sender, EventArgs e)
    {
        try
        {
            var achievements = await _db.GetAchievementsAsync();
            var content = await _db.GetContentItemsAsync();
            var goals = await _db.GetGoalsAsync();
            var snapshots = await _db.GetSnapshotsAsync();

            var exportObj = new
            {
                ExportedAt = DateTime.Now,
                Achievements = achievements,
                ContentItems = content,
                Goals = goals,
                Snapshots = snapshots
            };

            string json = JsonSerializer.Serialize(exportObj, new JsonSerializerOptions { WriteIndented = true });
            string fileName = $"elevate_export_{DateTime.Now:yyyyMMdd_HHmmss}.json";
            string path = Path.Combine(FileSystem.AppDataDirectory, fileName);
            await File.WriteAllTextAsync(path, json);

            await DisplayAlert("Export Complete",
                $"Data exported to:\n{fileName}\n\nYou can find it in the app's data folder.",
                "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Export Failed", ex.Message, "OK");
        }
    }

    private async void OnClearTapped(object sender, EventArgs e)
    {
        bool answer = await DisplayAlert("⚠️ Warning",
            "Are you sure you want to delete ALL your data? This cannot be undone.",
            "Yes, Delete", "Cancel");
        if (!answer) return;

        await _db.ClearAllDataAsync();
        await DisplayAlert("Done", "All data has been cleared.", "OK");
    }

    private async void OnLogoutTapped(object sender, EventArgs e)
    {
        bool confirm = await DisplayAlert("Sign Out",
            "Are you sure you want to sign out?", "Sign Out", "Cancel");
        if (!confirm) return;

        Preferences.Remove("LoggedInUserId");
        _db.SetCurrentUser(0);
        await Shell.Current.GoToAsync($"//{nameof(LoginPage)}");
    }
}