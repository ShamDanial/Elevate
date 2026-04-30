using Elevate.Data;
using Elevate.Data.Models;
using Microsoft.Maui.Controls.Shapes;

namespace Elevate.Pages;

public partial class ProgressPage : ContentPage
{
    private readonly ElevateDatabase _db;
    private string _selectedUnit = "followers";

    public ProgressPage(ElevateDatabase db)
    {
        InitializeComponent();
        _db = db;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        // Seed milestones here too so existing users who were logged in
        // before the milestone feature was added still get seeded.
        await _db.SeedMilestonesAsync(_db.CurrentUserId);
        await LoadAllAsync();
    }

    private async Task LoadAllAsync()
    {
        await Task.WhenAll(
            LoadGoalsAsync(),
            LoadMilestonesAsync(),
            LoadAchievementsAsync()
        );
    }

    // ── Custom Goals ──────────────────────────────────────────────────────

    private async Task LoadGoalsAsync()
    {
        var goals = await _db.GetGoalsAsync();
        GoalsStack.Children.Clear();

        if (!goals.Any())
        {
            GoalsStack.Children.Add(new Label
            {
                Text = "No custom goals yet — tap '+ Add Goal' to create one 🎯",
                TextColor = Colors.Gray,
                FontSize = 13,
                HorizontalOptions = LayoutOptions.Center,
                Margin = new Thickness(0, 4)
            });
            OverallPercentLabel.Text = "No custom goals yet";
            OverallBigLabel.Text = "—";
            OverallProgressBar.Progress = 0;
            OverallSubLabel.Text = "add a custom goal to track progress";
            return;
        }

        double avg = goals.Average(g => g.Progress);
        OverallPercentLabel.Text = $"{(int)(avg * 100)}% Complete";
        OverallBigLabel.Text = $"{(int)(avg * 100)}%";
        OverallSubLabel.Text = $"across {goals.Count} custom goal{(goals.Count > 1 ? "s" : "")}";
        await OverallProgressBar.ProgressTo(avg, 800, Easing.CubicOut);

        foreach (var goal in goals)
            GoalsStack.Children.Add(BuildGoalCard(goal));
    }

    private View BuildGoalCard(CreatorGoal goal)
    {
        var border = new Border
        {
            BackgroundColor = Color.FromArgb("#1A1A1A"),
            Stroke = new SolidColorBrush(Color.FromArgb("#2A2A2A")),
            StrokeShape = new RoundRectangle { CornerRadius = 20 },
            Padding = new Thickness(16)
        };

        var outer = new VerticalStackLayout { Spacing = 12 };

        // Top row: title + actions
        var topRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 12
        };

        var titleStack = new VerticalStackLayout { Spacing = 2 };
        titleStack.Children.Add(new Label
        {
            Text = goal.Title,
            TextColor = Colors.White,
            FontAttributes = FontAttributes.Bold,
            FontSize = 16
        });
        titleStack.Children.Add(new Label
        {
            Text = $"Due {goal.Deadline:MMM d, yyyy}  ·  {goal.Unit}",
            TextColor = Color.FromArgb("#666"),
            FontSize = 12
        });
        Grid.SetColumn(titleStack, 0);
        topRow.Children.Add(titleStack);

        var editLbl = new Label { Text = "✏️", FontSize = 18, VerticalOptions = LayoutOptions.Center };
        var editTap = new TapGestureRecognizer();
        editTap.Tapped += async (s, e) => await ShowUpdateGoalAsync(goal);
        editLbl.GestureRecognizers.Add(editTap);
        Grid.SetColumn(editLbl, 1);
        topRow.Children.Add(editLbl);

        var delLbl = new Label { Text = "🗑", FontSize = 17, VerticalOptions = LayoutOptions.Center };
        var delTap = new TapGestureRecognizer();
        delTap.Tapped += async (s, e) => await DeleteGoalAsync(goal);
        delLbl.GestureRecognizers.Add(delTap);
        Grid.SetColumn(delLbl, 2);
        topRow.Children.Add(delLbl);

        outer.Children.Add(topRow);

        // Progress value row
        var valRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            }
        };
        valRow.Children.Add(new Label
        {
            Text = $"{goal.CurrentValue:N0} / {goal.TargetValue:N0}",
            TextColor = Color.FromArgb(goal.AccentColor),
            FontAttributes = FontAttributes.Bold,
            FontSize = 14
        });
        var pctLbl = new Label
        {
            Text = goal.ProgressLabel,
            TextColor = Color.FromArgb(goal.AccentColor),
            FontAttributes = FontAttributes.Bold,
            FontSize = 14
        };
        Grid.SetColumn(pctLbl, 1);
        valRow.Children.Add(pctLbl);
        outer.Children.Add(valRow);

        // Progress bar
        var barGrid = new Grid { HeightRequest = 7 };
        barGrid.Children.Add(new BoxView
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
        barGrid.Children.Add(fill);
        double prog = goal.Progress;
        barGrid.SizeChanged += (s, e) => fill.WidthRequest = barGrid.Width * prog;
        outer.Children.Add(barGrid);

        border.Content = outer;
        return border;
    }

    private async Task ShowUpdateGoalAsync(CreatorGoal goal)
    {
        string? input = await DisplayPromptAsync(
            "Update Progress",
            $"New current value for '{goal.Title}' ({goal.Unit}):",
            keyboard: Keyboard.Numeric,
            initialValue: goal.CurrentValue.ToString(),
            placeholder: goal.CurrentValue.ToString());

        if (input == null) return;
        if (!double.TryParse(input, out double newVal) || newVal < 0)
        {
            await DisplayAlert("Invalid", "Please enter a valid number.", "OK");
            return;
        }

        bool wasComplete = goal.Progress >= 1.0;
        goal.CurrentValue = newVal;
        await _db.SaveGoalAsync(goal);

        if (!wasComplete && goal.Progress >= 1.0)
        {
            await _db.SaveAlertAsync(new AppAlert
            {
                Title = $"🎉 Goal Completed: {goal.Title}",
                Body = $"You reached {goal.TargetValue:N0} {goal.Unit}!",
                IconEmoji = "🎯",
                IsRead = false
            });

            // Auto-remove the goal once completed — it served its purpose
            await DisplayAlert("🎉 Goal Complete!",
                $"You reached {goal.TargetValue:N0} {goal.Unit}! The goal has been removed.",
                "Awesome!");
            await _db.DeleteGoalAsync(goal);
        }

        await LoadAllAsync();
    }

    private async Task DeleteGoalAsync(CreatorGoal goal)
    {
        bool confirm = await DisplayAlert("Delete Goal", $"Remove '{goal.Title}'?", "Delete", "Cancel");
        if (!confirm) return;
        await _db.DeleteGoalAsync(goal);
        await LoadAllAsync();
    }

    // ── Milestones ────────────────────────────────────────────────────────

    private async Task LoadMilestonesAsync()
    {
        var all = await _db.GetMilestonesAsync(); // ordered by TargetValue asc

        var followerMs = all.Where(m => m.Unit == "followers").ToList();
        var likesMs = all.Where(m => m.Unit == "likes").ToList();

        RenderMilestoneBlock(
            badgesScroll: FollowerBadgesScroll,
            badgesRow: FollowerBadgesRow,
            nextStack: FollowerNextStack,
            milestones: followerMs,
            accentColor: "#6C63FF",
            emptyText: "Log your followers to start earning milestones");

        RenderMilestoneBlock(
            badgesScroll: LikesBadgesScroll,
            badgesRow: LikesBadgesRow,
            nextStack: LikesNextStack,
            milestones: likesMs,
            accentColor: "#E53935",
            emptyText: "Log your likes to start earning milestones");
    }

    private void RenderMilestoneBlock(
        ScrollView badgesScroll,
        HorizontalStackLayout badgesRow,
        VerticalStackLayout nextStack,
        List<CreatorGoal> milestones,
        string accentColor,
        string emptyText)
    {
        badgesRow.Children.Clear();
        nextStack.Children.Clear();

        if (!milestones.Any())
        {
            nextStack.Children.Add(new Label
            {
                Text = emptyText,
                TextColor = Color.FromArgb("#555"),
                FontSize = 13
            });
            return;
        }

        // Unlocked badges
        var unlocked = milestones.Where(m => m.IsUnlocked).ToList();
        if (unlocked.Any())
        {
            foreach (var m in unlocked)
                badgesRow.Children.Add(BuildUnlockedBadge(m));
            badgesScroll.IsVisible = true;
        }
        else
        {
            badgesScroll.IsVisible = false;
        }

        // Next locked milestone
        var next = milestones.FirstOrDefault(m => !m.IsUnlocked);
        if (next == null)
        {
            // All milestones complete!
            nextStack.Children.Add(new Label
            {
                Text = "🎉 All milestones unlocked!",
                TextColor = Color.FromArgb(accentColor),
                FontSize = 14,
                FontAttributes = FontAttributes.Bold
            });
            return;
        }

        // "Next milestone" card
        nextStack.Children.Add(BuildNextMilestoneCard(next, accentColor));
    }

    private View BuildNextMilestoneCard(CreatorGoal m, string accentColor)
    {
        double pct = Math.Round(m.Progress * 100, 1);
        double current = m.CurrentValue;
        double target = m.TargetValue;
        double remain = target - current;

        var card = new VerticalStackLayout { Spacing = 10 };

        // Header row: badge + title + percentage
        var headerRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 12
        };

        // Badge circle
        var badgeBorder = new Border
        {
            BackgroundColor = Color.FromArgb("#252540"),
            WidthRequest = 48,
            HeightRequest = 48,
            StrokeShape = new RoundRectangle { CornerRadius = 24 },
            StrokeThickness = 0
        };
        badgeBorder.Content = new Label
        {
            Text = m.MilestoneBadge,
            FontSize = 24,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };
        Grid.SetColumn(badgeBorder, 0);
        headerRow.Children.Add(badgeBorder);

        var textStack = new VerticalStackLayout { VerticalOptions = LayoutOptions.Center, Spacing = 2 };
        textStack.Children.Add(new Label
        {
            Text = $"Next: {m.Title}",
            TextColor = Colors.White,
            FontAttributes = FontAttributes.Bold,
            FontSize = 15
        });
        textStack.Children.Add(new Label
        {
            Text = remain > 0
                ? $"{remain:N0} more {m.Unit} to go"
                : m.Description,
            TextColor = Color.FromArgb("#888"),
            FontSize = 12
        });
        Grid.SetColumn(textStack, 1);
        headerRow.Children.Add(textStack);

        var pctLabel = new Label
        {
            Text = $"{pct}%",
            TextColor = Color.FromArgb(m.AccentColor),
            FontAttributes = FontAttributes.Bold,
            FontSize = 15,
            VerticalOptions = LayoutOptions.Center
        };
        Grid.SetColumn(pctLabel, 2);
        headerRow.Children.Add(pctLabel);

        card.Children.Add(headerRow);

        // Progress bar
        var barGrid = new Grid { HeightRequest = 8 };
        barGrid.Children.Add(new BoxView
        {
            BackgroundColor = Color.FromArgb("#2A2A3A"),
            CornerRadius = 4
        });
        var fill = new BoxView
        {
            BackgroundColor = Color.FromArgb(m.AccentColor),
            CornerRadius = 4,
            HorizontalOptions = LayoutOptions.Start,
            WidthRequest = 0
        };
        barGrid.Children.Add(fill);
        double prog = m.Progress;
        barGrid.SizeChanged += (s, e) => fill.WidthRequest = barGrid.Width * prog;
        card.Children.Add(barGrid);

        // current / target label
        card.Children.Add(new Label
        {
            Text = $"{current:N0} / {target:N0} {m.Unit}",
            TextColor = Color.FromArgb("#666"),
            FontSize = 11
        });

        return card;
    }

    private View BuildUnlockedBadge(CreatorGoal m)
    {
        var border = new Border
        {
            BackgroundColor = Color.FromArgb("#1E1A2E"),
            Stroke = new SolidColorBrush(Color.FromArgb(m.AccentColor)),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 14 },
            Padding = new Thickness(12, 8)
        };

        var row = new HorizontalStackLayout { Spacing = 6 };
        row.Children.Add(new Label
        {
            Text = m.MilestoneBadge,
            FontSize = 16,
            VerticalOptions = LayoutOptions.Center
        });
        row.Children.Add(new Label
        {
            Text = m.Title,
            TextColor = Color.FromArgb(m.AccentColor),
            FontSize = 12,
            FontAttributes = FontAttributes.Bold,
            VerticalOptions = LayoutOptions.Center
        });

        border.Content = row;
        return border;
    }

    // ── Recent Wins ───────────────────────────────────────────────────────

    private async Task LoadAchievementsAsync()
    {
        var achievements = await _db.GetRecentAchievementsAsync(5);
        AchievementsStack.Children.Clear();

        if (!achievements.Any())
        {
            AchievementsStack.Children.Add(new Label
            {
                Text = "No wins logged yet — use Log Stats to record your first one!",
                TextColor = Colors.Gray,
                FontSize = 13
            });
            return;
        }

        foreach (var a in achievements)
        {
            var card = new Border
            {
                BackgroundColor = Color.FromArgb("#1F1F1F"),
                StrokeShape = new RoundRectangle { CornerRadius = 16 },
                StrokeThickness = 0,
                Padding = new Thickness(14)
            };

            var grid = new Grid { ColumnSpacing = 12 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var iconBorder = new Border
            {
                BackgroundColor = Color.FromArgb("#2A1A3A"),
                WidthRequest = 42,
                HeightRequest = 42,
                StrokeShape = new RoundRectangle { CornerRadius = 11 }
            };
            iconBorder.Content = new Label
            {
                Text = a.IconEmoji,
                FontSize = 18,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            };

            var textStack = new VerticalStackLayout { VerticalOptions = LayoutOptions.Center, Spacing = 2 };
            textStack.Children.Add(new Label
            {
                Text = a.Title,
                TextColor = Colors.White,
                FontAttributes = FontAttributes.Bold,
                FontSize = 14
            });
            // Show note if user added one, otherwise show the date
            string subText = !string.IsNullOrEmpty(a.Description)
                ? a.Description
                : a.Date.ToString("MMM d, yyyy");
            textStack.Children.Add(new Label
            {
                Text = subText,
                TextColor = Color.FromArgb("#888"),
                FontSize = 12
            });

            var metricLabel = new Label
            {
                Text = $"{a.MetricValue:N0}",
                TextColor = Color.FromArgb("#B388FF"),
                FontAttributes = FontAttributes.Bold,
                FontSize = 14,
                VerticalOptions = LayoutOptions.Center
            };

            Grid.SetColumn(iconBorder, 0);
            Grid.SetColumn(textStack, 1);
            Grid.SetColumn(metricLabel, 2);
            grid.Children.Add(iconBorder);
            grid.Children.Add(textStack);
            grid.Children.Add(metricLabel);

            card.Content = grid;
            AchievementsStack.Children.Add(card);
        }
    }

    // ── Add Goal Popup ────────────────────────────────────────────────────

    private async void OnBackTapped(object sender, EventArgs e) =>
        await Shell.Current.GoToAsync($"//{nameof(HomePage)}");

    private async void OnAddGoalTapped(object sender, TappedEventArgs e)
    {
        GoalPopupOverlay.IsVisible = true;
        await Task.WhenAll(
            GoalPopupOverlay.FadeTo(1, 200),
            GoalBottomSheet.TranslateTo(0, 0, 400, Easing.CubicOut)
        );
    }

    public async void OnCloseGoalPopupTapped(object sender, EventArgs e) =>
        await CloseGoalPopupAsync();

    private async Task CloseGoalPopupAsync()
    {
        await Task.WhenAll(
            GoalBottomSheet.TranslateTo(0, 800, 400, Easing.CubicIn),
            GoalPopupOverlay.FadeTo(0, 200)
        );
        GoalPopupOverlay.IsVisible = false;
    }

    private void OnUnitFollowersTapped(object sender, TappedEventArgs e)
    {
        _selectedUnit = "followers";
        UnitFollowersChip.BackgroundColor = Color.FromArgb("#6C63FF");
        UnitLikesChip.BackgroundColor = Color.FromArgb("#252525");
        if (UnitFollowersChip.Content is Label fl) fl.TextColor = Colors.White;
        if (UnitLikesChip.Content is Label ll) ll.TextColor = Color.FromArgb("#888");
    }

    private void OnUnitLikesTapped(object sender, TappedEventArgs e)
    {
        _selectedUnit = "likes";
        UnitLikesChip.BackgroundColor = Color.FromArgb("#E53935");
        UnitFollowersChip.BackgroundColor = Color.FromArgb("#252525");
        if (UnitLikesChip.Content is Label ll) ll.TextColor = Colors.White;
        if (UnitFollowersChip.Content is Label fl) fl.TextColor = Color.FromArgb("#888");
    }

    private async void OnSaveGoalClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(GoalTitleEntry.Text))
        {
            await DisplayAlert("Error", "Please enter a goal title.", "OK");
            return;
        }
        if (!double.TryParse(GoalCurrentEntry.Text, out double current)) current = 0;
        if (!double.TryParse(GoalTargetEntry.Text, out double target) || target <= 0)
        {
            await DisplayAlert("Error", "Please enter a valid target value.", "OK");
            return;
        }

        var goal = new CreatorGoal
        {
            Title = GoalTitleEntry.Text.Trim(),
            Description = GoalDescEntry.Text?.Trim() ?? "",
            CurrentValue = current,
            TargetValue = target,
            Unit = _selectedUnit,
            Deadline = (DateTime)GoalDeadlinePicker.Date,
            AccentColor = _selectedUnit == "followers" ? "#6C63FF" : "#E53935",
            IsMilestone = false
        };

        await _db.SaveGoalAsync(goal);

        GoalTitleEntry.Text = "";
        GoalDescEntry.Text = "";
        GoalCurrentEntry.Text = "";
        GoalTargetEntry.Text = "";

        await CloseGoalPopupAsync();
        await LoadAllAsync();
    }
}