using Elevate.Data;
using Elevate.Data.Models;
using BCrypt.Net;

namespace Elevate.Pages;

public partial class LoginPage : ContentPage
{
    private readonly ElevateDatabase _db;

    public LoginPage(ElevateDatabase db)
    {
        InitializeComponent();
        _db = db;
    }

    // ── Tab Toggle Logic ──────────────────────────────────────────────────

    public void OnLoginTabTapped(object sender, EventArgs e)
    {
        LoginTabBg.BackgroundColor = Color.FromArgb("#6C63FF");
        LoginTabLabel.TextColor = Colors.White;
        RegisterTabBg.BackgroundColor = Colors.Transparent;
        RegisterTabLabel.TextColor = Color.FromArgb("#888888");
        LoginForm.IsVisible = true;
        RegisterForm.IsVisible = false;
    }

    public void OnRegisterTabTapped(object sender, EventArgs e)
    {
        RegisterTabBg.BackgroundColor = Color.FromArgb("#6C63FF");
        RegisterTabLabel.TextColor = Colors.White;
        LoginTabBg.BackgroundColor = Colors.Transparent;
        LoginTabLabel.TextColor = Color.FromArgb("#888888");
        LoginForm.IsVisible = false;
        RegisterForm.IsVisible = true;
    }

    // ── Sign In Logic ─────────────────────────────────────────────────────

    public async void OnSignInTapped(object sender, EventArgs e)
    {
        LoginErrorLabel.IsVisible = false;
        var input = LoginUsernameEntry.Text?.Trim() ?? "";
        var password = LoginPasswordEntry.Text ?? "";

        if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(password))
        {
            LoginErrorLabel.Text = "Please fill in all fields.";
            LoginErrorLabel.IsVisible = true;
            return;
        }

        // Try username first, then email
        var user = await _db.GetUserByUsernameAsync(input)
                   ?? await _db.GetUserByEmailAsync(input);

        // Verify using full namespace to avoid naming conflicts
        if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            LoginErrorLabel.Text = "Invalid username or password.";
            LoginErrorLabel.IsVisible = true;
            return;
        }

        Preferences.Set("LoggedInUserId", user.Id);
        _db.SetCurrentUser(user.Id);
        await _db.SeedMilestonesAsync(user.Id);
        await Shell.Current.GoToAsync($"//{nameof(HomePage)}");
    }

    // ── Register Logic ────────────────────────────────────────────────────

    public async void OnRegisterTapped(object sender, EventArgs e)
    {
        RegErrorLabel.IsVisible = false;

        var displayName = RegDisplayNameEntry.Text?.Trim() ?? "";
        var username = RegUsernameEntry.Text?.Trim() ?? "";
        var email = RegEmailEntry.Text?.Trim() ?? "";
        var password = RegPasswordEntry.Text ?? "";

        if (string.IsNullOrEmpty(displayName) || string.IsNullOrEmpty(username) ||
            string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            RegErrorLabel.Text = "Please fill in all fields.";
            RegErrorLabel.IsVisible = true;
            return;
        }

        if (password.Length < 6)
        {
            RegErrorLabel.Text = "Password must be at least 6 characters.";
            RegErrorLabel.IsVisible = true;
            return;
        }

        // Validate username uniqueness
        if (await _db.GetUserByUsernameAsync(username) != null)
        {
            RegErrorLabel.Text = "Username already taken.";
            RegErrorLabel.IsVisible = true;
            return;
        }

        var newUser = new UserAccount
        {
            DisplayName = displayName,
            Username = username,
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            CreatedAt = DateTime.Now
        };

        await _db.SaveUserAsync(newUser);

        // Fetch saved user to get ID and start session
        var saved = await _db.GetUserByUsernameAsync(username);
        if (saved != null)
        {
            Preferences.Set("LoggedInUserId", saved.Id);
            _db.SetCurrentUser(saved.Id);
            await _db.SeedMilestonesAsync(saved.Id);
        }

        await Shell.Current.GoToAsync($"//{nameof(HomePage)}");
    }
}