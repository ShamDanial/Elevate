using Elevate.Data;
using Elevate.Data.Models;

namespace Elevate.Pages;

public partial class LoggerPage : ContentPage
{
    private readonly ElevateDatabase _db;
    private double _currentMetric = 0;

    // "followers" | "likes"
    private string _selectedCategory = "followers";

    public LoggerPage(ElevateDatabase db)
    {
        InitializeComponent();
        _db = db;
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        // Clamp the date picker so the calendar never shows future dates
        AchievementDatePicker.MaximumDate = DateTime.Today;
        AchievementDatePicker.Date = DateTime.Today;
        await LoadCurrentGoalAsync();
        await PrefillMetricAsync();
    }

    // ── Goal hint ─────────────────────────────────────────────────────────

    private async Task LoadCurrentGoalAsync()
    {
        var goal = await _db.GetPrimaryGoalAsync();
        CurrentGoalLabel.Text = goal != null
            ? $"Active goal: {goal.Title} · {goal.ProgressLabel} done"
            : "No goals set yet — add one in Track Progress";
    }

    // Prefill counter with last known value to save typing
    private async Task PrefillMetricAsync()
    {
        if (_selectedCategory == "followers")
        {
            var latest = await _db.GetLatestSnapshotAsync();
            if (latest != null)
            {
                _currentMetric = latest.FollowerCount;
                MetricEntry.Text = _currentMetric.ToString("N0");
            }
            else
            {
                _currentMetric = 0;
                MetricEntry.Text = "0";
            }
        }
        else
        {
            var latest = await _db.GetLatestLikesAsync();
            if (latest != null)
            {
                _currentMetric = latest.MetricValue;
                MetricEntry.Text = _currentMetric.ToString("N0");
            }
            else
            {
                _currentMetric = 0;
                MetricEntry.Text = "0";
            }
        }
    }

    // ── Category chips ────────────────────────────────────────────────────

    private async void OnFollowersTapped(object sender, TappedEventArgs e)
    {
        _selectedCategory = "followers";

        FollowersChip.BackgroundColor = Color.FromArgb("#6C63FF");
        FollowersChipLabel.TextColor = Colors.White;
        LikesChip.BackgroundColor = Color.FromArgb("#1F1F1F");
        LikesChipLabel.TextColor = Color.FromArgb("#888888");

        MetricLabel.Text = "Total Followers";
        MetricHintLabel.Text = "Enter your current total follower count";

        await PrefillMetricAsync();
    }

    private async void OnLikesTapped(object sender, TappedEventArgs e)
    {
        _selectedCategory = "likes";

        LikesChip.BackgroundColor = Color.FromArgb("#E53935");
        LikesChipLabel.TextColor = Colors.White;
        FollowersChip.BackgroundColor = Color.FromArgb("#1F1F1F");
        FollowersChipLabel.TextColor = Color.FromArgb("#888888");

        MetricLabel.Text = "Total Likes";
        MetricHintLabel.Text = "Enter your current total likes count";

        await PrefillMetricAsync();
    }

    // ── Counter buttons ───────────────────────────────────────────────────

    private void OnPlusTapped(object sender, EventArgs e)
    {
        _currentMetric++;
        MetricEntry.Text = _currentMetric.ToString("N0");
    }

    private void OnMinusTapped(object sender, EventArgs e)
    {
        if (_currentMetric > 0)
        {
            _currentMetric--;
            MetricEntry.Text = _currentMetric.ToString("N0");
        }
    }

    private void OnMetricEntryChanged(object sender, TextChangedEventArgs e)
    {
        string clean = e.NewTextValue?.Replace(",", "") ?? "0";
        if (double.TryParse(clean, out double val))
            _currentMetric = val;
    }

    // ── Save ──────────────────────────────────────────────────────────────

    public async void OnRecordTapped(object sender, EventArgs e)
    {
        if (_currentMetric <= 0)
        {
            await DisplayAlert("Error", "Please enter a value greater than 0.", "OK");
            return;
        }

        // Validate: log date cannot be in the future
        DateTime logDate = (DateTime)AchievementDatePicker.Date;
        if (logDate.Date > DateTime.Today)
        {
            await DisplayAlert("Invalid Date",
                "You can't log a stat for a future date. Please select today or an earlier date.",
                "OK");
            AchievementDatePicker.Date = DateTime.Today;
            return;
        }

        string emoji = _selectedCategory == "followers" ? "👥" : "❤️";
        string titleLabel = _selectedCategory == "followers" ? "Followers" : "Likes";
        string noteText = DescriptionEditor.Text?.Trim() ?? "";

        // 1. Save Achievement record
        // If user typed a note, that becomes the title (more personal).
        // Otherwise generate a clean readable title automatically.
        string autoTitle = _selectedCategory == "followers"
            ? $"Reached {_currentMetric:N0} followers"
            : $"Reached {_currentMetric:N0} likes";
        string finalTitle = !string.IsNullOrEmpty(noteText) ? noteText : autoTitle;
        string finalDesc = !string.IsNullOrEmpty(noteText) ? autoTitle : "";

        var achievement = new Achievement
        {
            Title = finalTitle,
            Description = finalDesc,
            MetricValue = _currentMetric,
            Date = logDate,
            IconEmoji = emoji,
            UserId = _db.CurrentUserId
        };
        await _db.SaveAchievementAsync(achievement);

        // 2. Sync Followers → FollowerSnapshot + Goals
        if (_selectedCategory == "followers")
        {
            var latest = await _db.GetLatestSnapshotAsync();
            double gain = latest != null ? _currentMetric - latest.FollowerCount : 0;

            await _db.SaveSnapshotAsync(new FollowerSnapshot
            {
                Date = logDate,
                FollowerCount = _currentMetric,
                DailyGain = gain,
                UserId = _db.CurrentUserId
            });

            await SyncGoalsAsync("followers", _currentMetric);
            await _db.CheckAndUnlockMilestonesAsync("followers", _currentMetric);
        }

        // 3. Sync Likes → Goals
        if (_selectedCategory == "likes")
        {
            await SyncGoalsAsync("likes", _currentMetric);
            await _db.CheckAndUnlockMilestonesAsync("likes", _currentMetric);
        }

        // 4. Confirm and return
        await DisplayAlert("Saved ✅",
            _selectedCategory == "followers"
                ? $"Follower count updated to {_currentMetric:N0}. Goals & analytics synced!"
                : $"Likes count updated to {_currentMetric:N0}. Goals synced!",
            "OK");

        await Shell.Current.GoToAsync("///HomePage");
    }

    // Updates every goal whose Unit matches the logged category
    private async Task SyncGoalsAsync(string unit, double value)
    {
        var goals = await _db.GetGoalsAsync();
        foreach (var goal in goals)
        {
            if (!goal.Unit.Equals(unit, StringComparison.OrdinalIgnoreCase))
                continue;

            bool wasComplete = goal.Progress >= 1.0;
            goal.CurrentValue = value;
            await _db.SaveGoalAsync(goal);

            // Fire a one-time completion alert
            if (!wasComplete && goal.Progress >= 1.0)
            {
                await _db.SaveAlertAsync(new AppAlert
                {
                    Title = $"🎯 Goal Achieved: {goal.Title}",
                    Body = $"You reached your target of {goal.TargetValue:N0} {goal.Unit}!",
                    IconEmoji = "🎉",
                    UserId = _db.CurrentUserId
                });
                // Auto-remove completed goal
                await _db.DeleteGoalAsync(goal);
            }
        }
    }

    public async void OnBackTapped(object sender, EventArgs e) =>
        await Shell.Current.GoToAsync("///HomePage");
}