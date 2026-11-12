using Microsoft.Maui.Storage;
using VirgoHub.Mobile.Services;
using VirgoHub.Shared;

namespace VirgoHub.Mobile;

public partial class PersonalProfilePage : ContentPage
{
    private const string PersonalTutorialPrefix = "virgohub.personal.tutorial";

    public PersonalProfilePage(StudentPortalStore store)
    {
        InitializeComponent();
        Store = store;
        BindingContext = this;
    }

    public StudentPortalStore Store { get; }

    public string PersonalRoleLabel => Store.CurrentUser?.Role == UserRole.Lecturer
        ? "Personal Lecturer Workspace"
        : "Personal Learner Workspace";

    public string NotificationSummary => Store.Notifications.Count > 0
        ? $"{Store.Notifications.Count} recent notification(s)"
        : "No new notifications right now";

    protected override void OnAppearing()
    {
        base.OnAppearing();
        OnPropertyChanged(nameof(PersonalRoleLabel));
        OnPropertyChanged(nameof(NotificationSummary));
    }

    private async void OnNotificationsTapped(object? sender, TappedEventArgs e)
    {
        await Navigation.PushAsync(new NotificationsPage(Store));
    }

    private async void OnSettingsTapped(object? sender, TappedEventArgs e)
    {
        await Navigation.PushAsync(new SecurityPage(Store));
    }

    private async void OnSupportTapped(object? sender, TappedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(Store.CurrentUser?.Id))
        {
            Preferences.Default.Remove($"{PersonalTutorialPrefix}.{Store.CurrentUser.Id}");
        }

        await DisplayAlert(
            "VirgoHub",
            "The personal walkthrough has been reset. Return to the home page to see the guided tutorial again.",
            "OK");
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
