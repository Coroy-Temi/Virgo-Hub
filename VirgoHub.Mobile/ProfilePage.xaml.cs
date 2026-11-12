using VirgoHub.Mobile.Services;

namespace VirgoHub.Mobile;

public partial class ProfilePage : ContentPage
{
    public ProfilePage(StudentPortalStore store)
    {
        InitializeComponent();
        Store = store;
        BindingContext = this;
    }

    public StudentPortalStore Store { get; }

    private async void OnNotificationsTapped(object? sender, TappedEventArgs e)
    {
        await Navigation.PushAsync(new NotificationsPage(Store));
    }

    private async void OnSecurityTapped(object? sender, TappedEventArgs e)
    {
        await Navigation.PushAsync(new SecurityPage(Store));
    }

    private async void OnSupportTapped(object? sender, TappedEventArgs e)
    {
        var message = Store.IsPersonalMode
            ? "Support channels can be added here for tutoring help, lecturer guidance, and personal workspace assistance."
            : "Support channels can be added here for campus ICT, hostel help, and student affairs.";
        await DisplayAlert("VirgoHub", message, "OK");
    }

    private void OnLogoutTapped(object? sender, TappedEventArgs e)
    {
        Store.Logout();
        if (Application.Current is App app)
        {
            app.ShowLoginPage();
        }
    }
}
