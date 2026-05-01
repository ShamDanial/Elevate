using Elevate.Data;
using Elevate.Data.Models;
using Microsoft.Maui.Controls.Shapes;

namespace Elevate.Pages;

public partial class AlertsPage : ContentPage
{
    private readonly ElevateDatabase _db;

    public AlertsPage(ElevateDatabase db)
    {
        InitializeComponent();
        _db = db;
        StartClock();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAlertsAsync();
    }

    private void StartClock()
    {
        ClockLabel.Text = DateTime.Now.ToString("hh:mm tt");
        LockDateLabel.Text = DateTime.Now.ToString("dddd, MMMM d");

        Dispatcher.StartTimer(TimeSpan.FromSeconds(1), () =>
        {
            ClockLabel.Text = DateTime.Now.ToString("hh:mm tt");
            LockDateLabel.Text = DateTime.Now.ToString("dddd, MMMM d");
            return true;
        });
    }

    private async Task LoadAlertsAsync()
    {
        var alerts = await _db.GetAlertsAsync();
        int unread = alerts.Count(a => !a.IsRead);

        UnreadBadgeText.Text = unread.ToString();
        UnreadBadge.IsVisible = unread > 0;

        AlertsListStack.Children.Clear();

        if (!alerts.Any())
        {
            AlertsListStack.Children.Add(new Label
            {
                Text = "No alerts yet.",
                TextColor = Color.FromArgb("#666666"),
                FontSize = 13,
                HorizontalOptions = LayoutOptions.Center,
                Margin = new Thickness(0, 20)
            });
            return;
        }

        foreach (var alert in alerts)
            AlertsListStack.Children.Add(BuildAlertCard(alert));
    }

    private View BuildAlertCard(AppAlert alert)
    {
        var border = new Border
        {
            BackgroundColor = Color.FromArgb("#1F1F1F"),
            StrokeShape = new RoundRectangle { CornerRadius = 16 },
            StrokeThickness = 0,
            Padding = new Thickness(16)
        };

        var grid = new Grid { ColumnSpacing = 14 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var iconBorder = new Border
        {
            BackgroundColor = Color.FromArgb("#2A1A3A"),
            WidthRequest = 48,
            HeightRequest = 48,
            StrokeShape = new RoundRectangle { CornerRadius = 14 },
            Content = new Label { Text = alert.IconEmoji, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center }
        };

        var textStack = new VerticalStackLayout { Spacing = 4 };
        textStack.Children.Add(new Label { Text = alert.Title, FontAttributes = FontAttributes.Bold, TextColor = Colors.White });
        textStack.Children.Add(new Label { Text = alert.Body, FontSize = 13, TextColor = Color.FromArgb("#AAAAAA") });

        var dot = new Ellipse
        {
            WidthRequest = 9,
            HeightRequest = 9,
            Fill = new SolidColorBrush(Color.FromArgb("#6C63FF")),
            VerticalOptions = LayoutOptions.Start,
            IsVisible = !alert.IsRead
        };

        Grid.SetColumn(iconBorder, 0);
        Grid.SetColumn(textStack, 1);
        Grid.SetColumn(dot, 2);
        grid.Children.Add(iconBorder);
        grid.Children.Add(textStack);
        grid.Children.Add(dot);

        border.Content = grid;

        var tap = new TapGestureRecognizer();
        tap.Tapped += async (s, e) =>
        {
            if (!alert.IsRead)
            {
                alert.IsRead = true;
                await _db.SaveAlertAsync(alert);
                dot.IsVisible = false;
                await LoadUnreadBadgeAsync();
            }
        };
        border.GestureRecognizers.Add(tap);

        return border;
    }

    private async Task LoadUnreadBadgeAsync()
    {
        int unread = await _db.GetUnreadCountAsync();
        UnreadBadgeText.Text = unread.ToString();
        UnreadBadge.IsVisible = unread > 0;
    }

    private async void OnMarkAllReadTapped(object sender, TappedEventArgs e)
    {
        await _db.MarkAllAlertsReadAsync();
        await LoadAlertsAsync();
    }

    
    private async void OnBackTapped(object sender, EventArgs e) =>
        await Shell.Current.GoToAsync("///HomePage"); 
}