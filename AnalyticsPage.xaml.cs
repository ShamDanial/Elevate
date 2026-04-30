using Elevate.Data;
using Elevate.Data.Models;

namespace Elevate.Pages;

public partial class AnalyticsPage : ContentPage
{
    private readonly ElevateDatabase _db;

    public AnalyticsPage(ElevateDatabase db)
    {
        InitializeComponent();
        _db = db;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAllAnalyticsAsync();
    }

    private async Task LoadAllAnalyticsAsync()
    {
        await Task.WhenAll(
            LoadFollowerStatsAsync(),
            LoadLikesStatsAsync(),
            LoadPostStatsAsync(),
            LoadAchievementStatsAsync(),
            LoadGoalProgressAsync(),
            LoadSnapshotChartAsync()
        );
    }

    // ── Followers ─────────────────────────────────────────────────────────

    private async Task LoadFollowerStatsAsync()
    {
        var snapshots = await _db.GetSnapshotsAsync(30);
        if (!snapshots.Any())
        {
            TotalFollowersLabel.Text = "—";
            FollowerGrowthLabel.Text = "No data yet — log via Log Stats";
            DailyGainLabel.Text = "—";
            GrowthRateLabel.Text = "—";
            return;
        }

        var latest = snapshots.First();   // most recent (ordered desc)
        var oldest = snapshots.Last();    // oldest in the window

        double totalGain = latest.FollowerCount - oldest.FollowerCount;
        double avgDaily = snapshots.Average(s => s.DailyGain);
        double growthPct = oldest.FollowerCount > 0
            ? totalGain / oldest.FollowerCount * 100
            : 0;

        TotalFollowersLabel.Text = latest.FollowerCount.ToString("N0");
        FollowerGrowthLabel.Text = totalGain >= 0
            ? $"↑ {totalGain:N0} since first entry"
            : $"↓ {Math.Abs(totalGain):N0} since first entry";
        DailyGainLabel.Text = avgDaily >= 0 ? $"+{avgDaily:N0}" : avgDaily.ToString("N0");
        GrowthRateLabel.Text = $"{growthPct:N1}%";
    }

    // ── Likes (read from Achievement table) ──────────────────────────────

    private async Task LoadLikesStatsAsync()
    {
        var latest = await _db.GetLatestLikesAsync();
        var prev = await _db.GetPreviousLikesAsync();

        if (latest == null)
        {
            TotalLikesLabel.Text = "—";
            LikesChangeLabel.Text = "No data yet";
            return;
        }

        TotalLikesLabel.Text = latest.MetricValue.ToString("N0");

        if (prev != null)
        {
            double diff = latest.MetricValue - prev.MetricValue;
            LikesChangeLabel.Text = diff >= 0
                ? $"↑ {diff:N0} since last log"
                : $"↓ {Math.Abs(diff):N0} since last log";
        }
        else
        {
            LikesChangeLabel.Text = $"logged {latest.Date:MMM d}";
        }
    }

    // ── Content ───────────────────────────────────────────────────────────

    private async Task LoadPostStatsAsync()
    {
        var all = await _db.GetContentItemsAsync();
        TotalPostsLabel.Text = all.Count.ToString();
        CompletedPostsLabel.Text = $"{all.Count(c => c.IsCompleted)} completed";
    }

    // ── Achievements ──────────────────────────────────────────────────────

    private async Task LoadAchievementStatsAsync()
    {
        var all = await _db.GetAchievementsAsync();
        TotalAchievementsLabel.Text = all.Count.ToString();
        LatestAchievementLabel.Text = all.FirstOrDefault()?.Title ?? "None";
    }

    // ── Goal progress bars ────────────────────────────────────────────────

    private async Task LoadGoalProgressAsync()
    {
        var goals = await _db.GetGoalsAsync();
        GoalProgressStack.Children.Clear();

        if (!goals.Any())
        {
            GoalProgressStack.Children.Add(new Label
            {
                Text = "No goals set — add one in Track Progress.",
                TextColor = Color.FromArgb("#666"),
                FontSize = 13
            });
            return;
        }

        foreach (var goal in goals)
        {
            var container = new VerticalStackLayout { Spacing = 6 };
            var header = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto)
                }
            };

            header.Children.Add(new Label
            {
                Text = goal.Title,
                TextColor = Colors.White,
                FontAttributes = FontAttributes.Bold
            });

            var pLabel = new Label
            {
                Text = goal.ProgressLabel,
                TextColor = Color.FromArgb(goal.AccentColor),
                FontAttributes = FontAttributes.Bold
            };
            Grid.SetColumn(pLabel, 1);
            header.Children.Add(pLabel);

            container.Children.Add(header);

            // Sub-label: current / target
            container.Children.Add(new Label
            {
                Text = $"{goal.CurrentValue:N0} / {goal.TargetValue:N0} {goal.Unit}",
                TextColor = Color.FromArgb("#888"),
                FontSize = 12
            });

            var bar = new Grid { HeightRequest = 7 };
            bar.Children.Add(new BoxView
            {
                BackgroundColor = Color.FromArgb("#2A2A2A"),
                CornerRadius = 4
            });
            var fill = new BoxView
            {
                BackgroundColor = Color.FromArgb(goal.AccentColor),
                CornerRadius = 4,
                HorizontalOptions = LayoutOptions.Start,
                WidthRequest = 0
            };
            bar.Children.Add(fill);

            // Capture for lambda
            double progress = goal.Progress;
            bar.SizeChanged += (s, e) => { fill.WidthRequest = bar.Width * progress; };
            container.Children.Add(bar);

            GoalProgressStack.Children.Add(container);
        }
    }

    // ── Follower chart ────────────────────────────────────────────────────

    private async Task LoadSnapshotChartAsync()
    {
        var snapshots = (await _db.GetSnapshotsAsync(7))
            .OrderBy(s => s.Date)
            .ToList();

        SnapshotChartGrid.Children.Clear();
        SnapshotChartGrid.ColumnDefinitions.Clear();

        if (snapshots.Count < 2) return;

        double max = snapshots.Max(s => s.FollowerCount);
        double min = snapshots.Min(s => s.FollowerCount);
        double range = Math.Max(1, max - min);

        for (int i = 0; i < snapshots.Count; i++)
        {
            SnapshotChartGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

            var bar = new BoxView
            {
                BackgroundColor = Color.FromArgb("#6C63FF"),
                CornerRadius = 4,
                VerticalOptions = LayoutOptions.End,
                HeightRequest = Math.Max(8, 90 * ((snapshots[i].FollowerCount - min) / range))
            };
            Grid.SetColumn(bar, i);
            SnapshotChartGrid.Children.Add(bar);
        }
    }

    // ── Navigation ────────────────────────────────────────────────────────

    private async void OnBackTapped(object sender, EventArgs e) =>
        await Shell.Current.GoToAsync($"//{nameof(HomePage)}");

    private async void OnShareTapped(object sender, EventArgs e) =>
        await DisplayAlert("Export", "Analytics exported.", "OK");
}