namespace Elevate.Pages;

public partial class AboutPage : ContentPage
{
    public AboutPage()
    {
        InitializeComponent();
    }

    
    private async void OnBackTapped(object sender, EventArgs e)
    {
        
        await Shell.Current.GoToAsync("///HomePage");
    }
}