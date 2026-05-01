using Elevate.Data;
using Elevate.Pages;

namespace Elevate.Pages;

public partial class SplashPage : ContentPage
{
    private readonly ElevateDatabase _db;

    // Inject the database so we can check the session
    public SplashPage(ElevateDatabase db)
    {
        InitializeComponent();
        _db = db;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RunSplashAnimationAsync();
    }

    private async Task RunSplashAnimationAsync()
    {
        // 1. Fade in the content
        await MainContent.FadeTo(1, 600, Easing.CubicOut);

        // 2. Pulse the icon
        await IconBorder.ScaleTo(1.08, 200, Easing.CubicOut);
        await IconBorder.ScaleTo(1.0, 150, Easing.CubicIn);

        // 3. Run the loading bar while checking the session
        await AnimateLoadingBar();

        // ── Session Check ──────────────────────────────────────────────────
        int savedUserId = Preferences.Get("LoggedInUserId", 0);
        string targetRoute = $"//{nameof(LoginPage)}";

        if (savedUserId > 0)
        {
            var user = await _db.GetUserByIdAsync(savedUserId);
            if (user != null)
            {
                _db.SetCurrentUser(savedUserId);
                targetRoute = $"//{nameof(HomePage)}";
            }
        }
        // ───────────────────────────────────────────────────────────────────

        LoadingLabel.Text = "Almost ready...";
        await Task.Delay(300);

        // 4. Fade out and navigate to the CORRECT page
        await MainContent.FadeTo(0, 400, Easing.CubicIn);
        await Shell.Current.GoToAsync(targetRoute);
    }

    private async Task AnimateLoadingBar()
    {
        uint duration = 1500;
        uint steps = 60;
        uint stepDelay = duration / steps;
        double targetWidth = 180;

        for (int i = 0; i <= steps; i++)
        {
            double progress = (double)i / steps;
            double eased = progress < 0.5
                ? 2 * progress * progress
                : 1 - Math.Pow(-2 * progress + 2, 2) / 2;

            LoadingBar.WidthRequest = eased * targetWidth;
            await Task.Delay((int)stepDelay);
        }
    }
}