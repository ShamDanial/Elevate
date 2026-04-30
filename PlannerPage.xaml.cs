using Elevate.Data;
using Elevate.Data.Models;
using Microsoft.Maui.Controls.Shapes;
using Plugin.LocalNotification;
using Plugin.LocalNotification.Core.Models;

namespace Elevate.Pages;

public partial class PlannerPage : ContentPage
{
    private readonly ElevateDatabase _db;
    private string _selectedContentType = "Video";

    public PlannerPage(ElevateDatabase db)
    {
        InitializeComponent();
        _db = db;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadTasksAsync();
    }

    private async Task LoadTasksAsync()
    {
        var items = await _db.GetContentItemsAsync();
        TaskListStack.Children.Clear();

        int total = items.Count;
        int completed = items.Count(i => i.IsCompleted);
        int overdue = items.Count(i => !i.IsCompleted && IsOverdue(i));

        if (total == 0)
        {
            SubtitleLabel.Text = "No posts scheduled yet";
            return;
        }

        SubtitleLabel.Text = overdue > 0
            ? $"{completed}/{total} completed · {overdue} overdue"
            : $"{completed}/{total} completed";

        // Group by date, show overdue dates (past) first, then today, then future
        var grouped = items
            .GroupBy(i => i.ScheduledDate.Date)
            .OrderBy(g => g.Key);

        foreach (var group in grouped)
        {
            bool isPast = group.Key < DateTime.Today;

            // Date header
            string dateText = group.Key == DateTime.Today
                ? "📅 Today"
                : group.Key == DateTime.Today.AddDays(1)
                    ? "📅 Tomorrow"
                    : isPast
                        ? $"⚠️ {group.Key:dddd, MMMM d}"
                        : $"📅 {group.Key:dddd, MMMM d}";

            // Overdue date headers are red
            TaskListStack.Children.Add(new Label
            {
                Text = dateText,
                TextColor = isPast ? Color.FromArgb("#FF5252") : Color.FromArgb("#B388FF"),
                FontAttributes = FontAttributes.Bold,
                FontSize = 15,
                Margin = new Thickness(0, 16, 0, 8)
            });

            foreach (var item in group.OrderBy(i => i.ScheduledTime))
                TaskListStack.Children.Add(BuildTaskCard(item));
        }
    }

    // An item is overdue if its scheduled date+time has passed and it's not done
    private bool IsOverdue(ContentItem item)
    {
        DateTime scheduledDateTime = item.ScheduledDate.Date + item.ScheduledTime;
        return scheduledDateTime < DateTime.Now && !item.IsCompleted;
    }

    private View BuildTaskCard(ContentItem item)
    {
        bool isLive = item.ContentType == "Livestream";
        bool isDone = item.IsCompleted;
        bool overdue = IsOverdue(item);

        string emoji = isLive ? "📺" : "📹";
        string typeColor = isLive ? "#FF7043" : "#6C63FF";
        string formattedTime = DateTime.Today.Add(item.ScheduledTime).ToString("hh:mm tt");

        // Card background: completed = darker, overdue = dark red tint, normal = default
        string cardBg = isDone ? "#181818" : overdue ? "#2A1010" : "#1F1F1F";
        string borderColor = isDone ? "#222222" : overdue ? "#FF5252" : "#2A2A2A";
        string titleColor = isDone ? "#666666" : overdue ? "#FF8A80" : "#FFFFFF";
        string iconBg = isDone ? "#1A1A1A"
                           : overdue ? "#3A1515"
                           : isLive ? "#3A1A1A" : "#2A1A3A";

        var border = new Border
        {
            BackgroundColor = Color.FromArgb(cardBg),
            Padding = new Thickness(16),
            StrokeShape = new RoundRectangle { CornerRadius = 16 },
            StrokeThickness = 1,
            Stroke = new SolidColorBrush(Color.FromArgb(borderColor)),
            Margin = new Thickness(0, 0, 0, 10)
        };

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto), // checkbox
                new ColumnDefinition(GridLength.Auto), // icon
                new ColumnDefinition(GridLength.Star), // text
            },
            ColumnSpacing = 12
        };

        // ── Checkbox ──────────────────────────────────────────────────────
        var checkBorder = new Border
        {
            WidthRequest = 26,
            HeightRequest = 26,
            StrokeShape = new RoundRectangle { CornerRadius = 8 },
            StrokeThickness = 2,
            Stroke = isDone
                ? new SolidColorBrush(Color.FromArgb("#6C63FF"))
                : new SolidColorBrush(Color.FromArgb("#444444")),
            BackgroundColor = isDone
                ? Color.FromArgb("#6C63FF")
                : Colors.Transparent,
            VerticalOptions = LayoutOptions.Center
        };

        if (isDone)
        {
            checkBorder.Content = new Label
            {
                Text = "✓",
                TextColor = Colors.White,
                FontSize = 14,
                FontAttributes = FontAttributes.Bold,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            };
        }

        var checkTap = new TapGestureRecognizer();
        checkTap.Tapped += async (s, e) =>
        {
            item.IsCompleted = !item.IsCompleted;
            await _db.SaveContentItemAsync(item);
            await LoadTasksAsync(); // Refresh the whole list
        };
        checkBorder.GestureRecognizers.Add(checkTap);
        Grid.SetColumn(checkBorder, 0);
        grid.Children.Add(checkBorder);

        // ── Icon ──────────────────────────────────────────────────────────
        var iconBorder = new Border
        {
            BackgroundColor = Color.FromArgb(iconBg),
            WidthRequest = 46,
            HeightRequest = 46,
            StrokeShape = new RoundRectangle { CornerRadius = 12 },
            Opacity = isDone ? 0.4 : 1.0
        };
        iconBorder.Content = new Label
        {
            Text = emoji,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };
        Grid.SetColumn(iconBorder, 1);
        grid.Children.Add(iconBorder);

        // ── Text ──────────────────────────────────────────────────────────
        var stack = new VerticalStackLayout { VerticalOptions = LayoutOptions.Center, Spacing = 3 };

        var titleLabel = new Label
        {
            Text = item.Title,
            TextColor = Color.FromArgb(titleColor),
            FontAttributes = isDone ? FontAttributes.None : FontAttributes.Bold
        };

        // Strikethrough effect for completed items
        if (isDone)
        {
            titleLabel.TextDecorations = TextDecorations.Strikethrough;
        }

        stack.Children.Add(titleLabel);

        // Time + type label — show "Overdue" badge if applicable
        string timeLabel = overdue
            ? $"⚠️ Overdue · {formattedTime}  ·  {item.ContentType}"
            : $"🕒 {formattedTime}  ·  {item.ContentType}";
        string timeLabelColor = overdue ? "#FF5252" : isDone ? "#444444" : typeColor;

        stack.Children.Add(new Label
        {
            Text = timeLabel,
            TextColor = Color.FromArgb(timeLabelColor),
            FontSize = 12
        });

        Grid.SetColumn(stack, 2);
        grid.Children.Add(stack);

        border.Content = grid;
        return border;
    }

    private async void OnScheduleClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ContentTitleEntry.Text)) return;

        DateTime scheduledDate = (DateTime)ContentDatePicker.Date;
        TimeSpan scheduledTime = (TimeSpan)ContentTimePicker.Time;
        DateTime fullDateTime = scheduledDate.Date + scheduledTime;

        // Validate: cannot schedule content in the past
        if (fullDateTime < DateTime.Now)
        {
            await DisplayAlert("Invalid Date & Time",
                "You can't schedule content in the past. Please pick a current or future date and time.",
                "OK");
            // Reset to sensible defaults: today + 1 hour from now
            ContentDatePicker.Date = DateTime.Today;
            ContentTimePicker.Time = TimeSpan.FromHours(DateTime.Now.Hour + 1);
            return;
        }

        var newItem = new ContentItem
        {
            Title = ContentTitleEntry.Text.Trim(),
            ContentType = _selectedContentType,
            ScheduledDate = scheduledDate,
            ScheduledTime = scheduledTime,
            Description = ContentDescEditor.Text?.Trim() ?? "",
            IsCompleted = false
        };
        await _db.SaveContentItemAsync(newItem);

        // Schedule a phone notification
        var notifTime = newItem.ScheduledDate.Date + newItem.ScheduledTime;
        var notification = new NotificationRequest
        {
            NotificationId = newItem.Id,
            Title = $"⏰ Time to go live!",
            Description = $"{newItem.Title} is scheduled now.",
            Schedule = new NotificationRequestSchedule
            {
                NotifyTime = notifTime
            }
        };
        await LocalNotificationCenter.Current.Show(notification);

        await ClosePopupInternal();
        await LoadTasksAsync();
    }

    private async void OnAddContentTapped(object sender, EventArgs e)
    {
        // Clamp date picker so you can only pick today or future
        ContentDatePicker.MinimumDate = DateTime.Today;
        ContentDatePicker.Date = DateTime.Today;
        // Default time to next full hour
        ContentTimePicker.Time = TimeSpan.FromHours(DateTime.Now.Hour + 1);
        PopupOverlay.IsVisible = true;
        await Task.WhenAll(PopupOverlay.FadeTo(1, 200), BottomSheet.TranslateTo(0, 0, 400, Easing.CubicOut));
    }

    private async void OnClosePopupTapped(object sender, EventArgs e) => await ClosePopupInternal();

    private async Task ClosePopupInternal()
    {
        await Task.WhenAll(BottomSheet.TranslateTo(0, 600, 400, Easing.CubicIn), PopupOverlay.FadeTo(0, 200));
        PopupOverlay.IsVisible = false;
    }

    private void OnVideoTypeTapped(object sender, TappedEventArgs e)
    {
        _selectedContentType = "Video";
        VideoTypeBorder.BackgroundColor = Color.FromArgb("#6C63FF");
        LiveTypeBorder.BackgroundColor = Color.FromArgb("#2A2A2A");
    }

    private void OnLiveTypeTapped(object sender, TappedEventArgs e)
    {
        _selectedContentType = "Livestream";
        LiveTypeBorder.BackgroundColor = Color.FromArgb("#FF7043");
        VideoTypeBorder.BackgroundColor = Color.FromArgb("#2A2A2A");
    }

    private async void OnBackTapped(object sender, TappedEventArgs e) =>
        await Shell.Current.GoToAsync($"//{nameof(HomePage)}");
}