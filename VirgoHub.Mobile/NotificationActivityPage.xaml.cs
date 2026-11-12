using System.Collections.ObjectModel;
using VirgoHub.Mobile.Services;
using VirgoHub.Shared;

namespace VirgoHub.Mobile;

public partial class NotificationActivityPage : ContentPage
{
    public NotificationActivityPage(StudentPortalStore store)
    {
        InitializeComponent();
        Store = store;
        BindingContext = this;
    }

    public StudentPortalStore Store { get; }

    public ObservableCollection<AppNotification> VisibleNotifications { get; } = [];

    public string ActivityPageSummary => Store.IsPersonalMode
        ? "Review delivered material updates, reminders, access notices, and helpful VirgoHub tips."
        : "Review the messages that have already been delivered inside VirgoHub.";

    public string UpdatesFilterLabel => Store.NotificationFilterUpdatesLabel;
    public string RemindersFilterLabel => Store.NotificationFilterRemindersLabel;
    public string AccessFilterLabel => Store.NotificationFilterAccessLabel;
    public string NewsFilterLabel => Store.NotificationFilterNewsLabel;

    protected override void OnAppearing()
    {
        base.OnAppearing();
        ApplyFilter(NotificationFeedFilter.All);
    }

    private void ApplyFilter(NotificationFeedFilter channel)
    {
        VisibleNotifications.Clear();
        foreach (var item in Store.GetNotificationsForChannel(channel))
        {
            VisibleNotifications.Add(item);
        }
    }

    private void OnAllClicked(object? sender, EventArgs e) => ApplyFilter(NotificationFeedFilter.All);
    private void OnElectionsClicked(object? sender, EventArgs e) => ApplyFilter(NotificationFeedFilter.Elections);
    private void OnEventsClicked(object? sender, EventArgs e) => ApplyFilter(NotificationFeedFilter.Events);
    private void OnReportsClicked(object? sender, EventArgs e) => ApplyFilter(NotificationFeedFilter.Reports);
    private void OnNewsClicked(object? sender, EventArgs e) => ApplyFilter(NotificationFeedFilter.News);

    private async void OnBackClicked(object? sender, EventArgs e) =>
        await Navigation.PopAsync();
}
