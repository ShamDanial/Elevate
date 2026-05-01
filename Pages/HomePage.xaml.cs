using Elevate.Data;
using Elevate.Data.Models;
using Plugin.LocalNotification;

namespace Elevate.Pages;

public partial class HomePage : ContentPage
{
    private readonly ElevateDatabase _db;
    private double _pendingProgress = 0;

    public HomePage(ElevateDatabase db)
    {
        InitializeComponent();
        _db = db;
        DateLabel.Text = DateTime.Now.ToString("MMMM d, yyyy");
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadDataAsync();
        await LocalNotificationCenter.Current.RequestNotificationPermission();
    }

    private async Task LoadDataAsync()
    {
        await Task.WhenAll(
            LoadGoalProgressAsync(),
            LoadRecentAchievementsAsync(),
            LoadUpcomingContentAsync(),
            LoadUnreadBadgeAsync()
        );
    }

    private async Task LoadGoalProgressAsync()
    {
        var goal = await _db.GetPrimaryGoalAsync();
        if (goal == null)
        {
            GoalTitleLabel.Text = "🚩 No Goal Set";
            GoalSubtitleLabel.Text = "Go to Track Progress to add a goal";
            GoalPercentLabel.Text = "0%";
            GoalProgressBar.WidthRequest = 0;
            return;
        }

        GoalTitleLabel.Text = $"🚩 {goal.Title}";
        GoalSubtitleLabel.Text = $"Reach {goal.TargetValue:N0} {goal.Unit} · Due {goal.Deadline:MMMM d, yyyy}";
        GoalPercentLabel.Text = goal.ProgressLabel;

        GoalProgressBar.SizeChanged -= OnGoalBarContainerSized;
        GoalProgressBar.SizeChanged += OnGoalBarContainerSized;
        _pendingProgress = goal.Progress;
    }

    private async void OnGoalBarContainerSized(object? sender, EventArgs e)
    {
        GoalProgressBar.SizeChanged -= OnGoalBarContainerSized;
        double containerWidth = ((View)GoalProgressBar.Parent).Width;
        if (containerWidth <= 0) return;
        double targetWidth = containerWidth * _pendingProgress;
        await GoalProgressBar.LayoutTo(
            new Rect(0, 0, targetWidth, GoalProgressBar.Height), 800, Easing.CubicOut);
    }

    private async Task LoadRecentAchievementsAsync()
    {
        var achievements = await _db.GetRecentAchievementsAsync(3);
        RecentAchievementsStack.Children.Clear();

        if (!achievements.Any())
        {
            RecentAchievementsStack.Children.Add(new Label
            {
                Text = "No achievements yet — tap 'Log Achievement' to record your first win! 🏆",
                TextColor = Color.FromArgb("#9AA0A6"),
                FontSize = 13,
                Margin = new Thickness(0, 8)
            });
            return;
        }

        foreach (var a in achievements)
            RecentAchievementsStack.Children.Add(BuildAchievementCard(a));
    }

    private View BuildAchievementCard(Achievement a)
    {
        var border = new Border
        {
            BackgroundColor = Colors.White,
            StrokeThickness = 0,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 16 },
            Padding = new Thickness(16)
        };

        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var iconBorder = new Border
        {
            BackgroundColor = Color.FromArgb("#F0F2FF"),
            WidthRequest = 48,
            HeightRequest = 48,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 }
        };
        iconBorder.Content = new Label
        {
            Text = a.IconEmoji,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };

        var textStack = new VerticalStackLayout { VerticalOptions = LayoutOptions.Center };
        textStack.Children.Add(new Label { Text = a.Title, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#1A1C1E") });
        textStack.Children.Add(new Label { Text = a.Description, FontSize = 12, TextColor = Color.FromArgb("#5F6368") });

        var dateLabel = new Label
        {
            Text = a.Date.ToString("MMM d"),
            FontSize = 12,
            TextColor = Color.FromArgb("#9AA0A6"),
            VerticalOptions = LayoutOptions.Center
        };

        Grid.SetColumn(iconBorder, 0);
        Grid.SetColumn(textStack, 1);
        Grid.SetColumn(dateLabel, 2);
        grid.Children.Add(iconBorder);
        grid.Children.Add(textStack);
        grid.Children.Add(dateLabel);

        border.Content = grid;
        return border;
    }

    private async Task LoadUpcomingContentAsync()
    {
        var items = await _db.GetUpcomingContentAsync(3);
        UpcomingPostsStack.Children.Clear();

        if (!items.Any())
        {
            UpcomingPostsStack.Children.Add(new Label
            {
                Text = "No upcoming posts scheduled — tap 'Plan Content' to schedule something! 📅",
                TextColor = Color.FromArgb("#9AA0A6"),
                FontSize = 13,
                Margin = new Thickness(0, 8)
            });
            return;
        }

        foreach (var item in items)
            UpcomingPostsStack.Children.Add(BuildContentCard(item));
    }

    private View BuildContentCard(ContentItem item)
    {
        bool isLivestream = item.ContentType == "Livestream";
        string iconEmoji = isLivestream ? "📺" : "⬆️";
        string typeColor = isLivestream ? "#FF7043" : "#6C63FF";
        string typeLabel = isLivestream ? "Live Stream" : "Video Upload";

        var border = new Border
        {
            BackgroundColor = Colors.White,
            StrokeThickness = 1,
            Stroke = new SolidColorBrush(Color.FromArgb("#EEF0FF")),
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 16 },
            Padding = new Thickness(16),
            Margin = new Thickness(0, 0, 0, 12)
        };

        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var iconBorder = new Border
        {
            BackgroundColor = Color.FromArgb("#F0F2FF"),
            WidthRequest = 48,
            HeightRequest = 48,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 }
        };
        iconBorder.Content = new Label { Text = iconEmoji, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };

        var textStack = new VerticalStackLayout { VerticalOptions = LayoutOptions.Center };
        textStack.Children.Add(new Label { Text = item.Title, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#1A1C1E") });
        textStack.Children.Add(new Label { Text = typeLabel, FontSize = 12, TextColor = Color.FromArgb(typeColor) });

        
        DateTime timeAsDate = DateTime.Today.Add(item.ScheduledTime);
        string timeString = timeAsDate.ToString("hh:mm tt");

        string scheduledText = item.ScheduledDate.Date == DateTime.Today
            ? $"Today, {timeString}"
            : $"{item.ScheduledDate:MMM d}, {timeString}";

        var timeLabel = new Label
        {
            Text = scheduledText,
            FontSize = 12,
            TextColor = Color.FromArgb("#5F6368"),
            VerticalOptions = LayoutOptions.Center
        };

        Grid.SetColumn(iconBorder, 0);
        Grid.SetColumn(textStack, 1);
        Grid.SetColumn(timeLabel, 2);
        grid.Children.Add(iconBorder);
        grid.Children.Add(textStack);
        grid.Children.Add(timeLabel);

        border.Content = grid;
        return border;
    }

    private async Task LoadUnreadBadgeAsync()
    {
        int unread = await _db.GetUnreadCountAsync();
        if (AlertBadge != null)
        {
            AlertBadge.IsVisible = unread > 0;
            AlertBadge.Text = unread.ToString();
        }
    }

    private async void OnAlertsTapped(object sender, EventArgs e) =>
    await Shell.Current.GoToAsync($"///{nameof(AlertsPage)}");
    private async void OnLogAchievementTapped(object sender, EventArgs e) => await Shell.Current.GoToAsync($"//{nameof(LoggerPage)}");
    private async void OnPlanContentTapped(object sender, EventArgs e) => await Shell.Current.GoToAsync($"//{nameof(PlannerPage)}");
    private async void OnSettingsTapped(object sender, EventArgs e) =>
     await Shell.Current.GoToAsync($"///{nameof(SettingsPage)}");
    private async void OnProfileTapped(object sender, EventArgs e) =>
     await Shell.Current.GoToAsync($"///{nameof(AboutPage)}"); //
    private async void OnAnalyticsTapped(object sender, EventArgs e) => await Shell.Current.GoToAsync($"//{nameof(AnalyticsPage)}");
    private async void OnProgressTapped(object sender, EventArgs e) => await Shell.Current.GoToAsync($"//{nameof(ProgressPage)}");
}