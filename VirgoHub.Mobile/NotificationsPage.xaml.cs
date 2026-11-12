using VirgoHub.Mobile.Services;

namespace VirgoHub.Mobile;

public partial class NotificationsPage : ContentPage
{
    public NotificationsPage(StudentPortalStore store)
    {
        InitializeComponent();
        Store = store;
        BindingContext = this;
    }

    public StudentPortalStore Store { get; }

    public string ActivitySummary => Store.IsPersonalMode
        ? "Open the in-app activity feed to review delivered personal updates, reminders, and material alerts."
        : "Open the in-app activity feed to review what VirgoHub has already delivered.";

    private async void OnSavePreferencesClicked(object? sender, EventArgs e)
    {
        var success = await Store.SaveNotificationPreferencesAsync();
        if (!success)
        {
            await DisplayAlert("VirgoHub", "Phone notifications could not be enabled on this device yet.", "OK");
            return;
        }

        await DisplayAlert("VirgoHub", "Notification preferences saved.", "OK");
    }

    private async void OnOpenActivityClicked(object? sender, EventArgs e) =>
        await Navigation.PushAsync(new NotificationActivityPage(Store));

    private async void OnBackClicked(object? sender, EventArgs e) =>
        await Navigation.PopAsync();
}
