using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using VirgoHub.Mobile.Services;
using VirgoHub.Shared;

namespace VirgoHub.Mobile;

public partial class HomePage : ContentPage
{
    private string _searchQuery = string.Empty;

    public HomePage(StudentPortalStore store)
    {
        InitializeComponent();
        Store = store;
        BindingContext = this;
    }

    public StudentPortalStore Store { get; }

    public ObservableCollection<SearchResultItem> SearchResults { get; } = [];

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (SetProperty(ref _searchQuery, value))
            {
                RefreshSearchResults();
                RaiseHomeSummaryProperties();
            }
        }
    }

    public string GreetingTitle => $"Hello, {GetPreferredName()}";

    public string StudentReference
    {
        get
        {
            var user = Store.CurrentUser;
            if (user is null)
            {
                return "VirgoHub Student";
            }

            if (!string.IsNullOrWhiteSpace(user.MatricNumber))
            {
                return user.MatricNumber;
            }

            if (!string.IsNullOrWhiteSpace(user.Level) && !string.IsNullOrWhiteSpace(user.Department))
            {
                return $"{user.Level} Level | {user.Department}";
            }

            return user.Email;
        }
    }

    public string AvatarInitial
    {
        get
        {
            var name = GetPreferredName();
            return string.IsNullOrWhiteSpace(name) ? "V" : name[..1].ToUpperInvariant();
        }
    }

    public bool HasSearchResults => SearchResults.Count > 0 && !string.IsNullOrWhiteSpace(SearchQuery);

    public string SearchSummary => $"{SearchResults.Count} result(s) found for \"{SearchQuery}\".";

    public string FeaturedChapelTitle => FeaturedChapelEvent?.Title ?? "No chapel event is scheduled yet.";

    public string FeaturedChapelSubtitle
    {
        get
        {
            if (FeaturedChapelEvent is null)
            {
                return "Admin can add one from dashboard.";
            }

            var when = FeaturedChapelEvent.StartsAt.ToLocalTime().ToString("dd MMM yyyy, hh:mm tt");
            var venue = string.IsNullOrWhiteSpace(FeaturedChapelEvent.Venue) ? "Campus venue" : FeaturedChapelEvent.Venue;
            return $"{when} at {venue}";
        }
    }

    public string SportsStatus => Store.MyBookings.Count > 0
        ? $"{Store.MyBookings.Count} booking(s)"
        : "Book court";

    public string VotingStatus
    {
        get
        {
            var openPolls = Store.ActiveElections.Count(x => x.IsOpen);
            return openPolls > 0 ? $"{openPolls} active poll(s)" : "No active poll";
        }
    }

    public string LibraryStatus => Store.MyCourseEnrollments.Count > 0
        ? $"{Store.MyCourseEnrollments.Count} enrolled course(s)"
        : "Enroll course";

    public string HostelStatus
    {
        get
        {
            var pending = Store.MyComplaints.Count(x => x.Status != ComplaintStatus.Resolved);
            return pending > 0 ? $"{pending} pending issue(s)" : "No pending issue";
        }
    }

    private CampusEvent? FeaturedChapelEvent =>
        Store.UpcomingEvents
            .OrderBy(x => x.StartsAt)
            .FirstOrDefault(x =>
                Matches(x.Category, "chapel") ||
                Matches(x.Title, "chapel") ||
                Matches(x.Description, "chapel"));

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (!Store.IsAuthenticated)
        {
            if (Application.Current is App app)
            {
                app.ShowLoginPage();
            }

            return;
        }

        try
        {
            await Store.RefreshAsync();
            RefreshSearchResults();
            RaiseHomeSummaryProperties();
        }
        catch (Exception ex)
        {
            await DisplayAlert("VirgoHub", ex.Message, "OK");
        }
    }

    private void OnEventsViewAllClicked(object? sender, EventArgs e) => ShowPortalPage<EventsPage>();

    private void OnNotificationsClicked(object? sender, EventArgs e) => ShowPortalPage<NotificationsPage>();

    private void OnEventsClicked(object? sender, TappedEventArgs e) => ShowPortalPage<EventsPage>();

    private void OnSportsClicked(object? sender, TappedEventArgs e) => ShowPortalPage<SportsPage>();

    private void OnVotingClicked(object? sender, TappedEventArgs e) => ShowPortalPage<VotingPage>();

    private void OnLibraryClicked(object? sender, TappedEventArgs e) => ShowPortalPage<LibraryPage>();

    private void OnComplaintsClicked(object? sender, EventArgs e) => ShowPortalPage<ComplaintsPage>();

    private void OnComplaintsClicked(object? sender, TappedEventArgs e) => ShowPortalPage<ComplaintsPage>();

    private void OnProfileClicked(object? sender, EventArgs e) => ShowPortalPage<ProfilePage>();

    private void OnProfileClicked(object? sender, TappedEventArgs e) => ShowPortalPage<ProfilePage>();

    private void RefreshSearchResults()
    {
        SearchResults.Clear();

        if (string.IsNullOrWhiteSpace(SearchQuery))
        {
            return;
        }

        var query = SearchQuery.Trim();
        var matches = new List<SearchResultItem>();

        matches.AddRange(Store.UpcomingEvents
            .Where(x => Matches(x.Title, query) || Matches(x.Description, query) || Matches(x.Venue, query))
            .Select(x => new SearchResultItem(x.Title, x.Venue, "Events", SearchDestination.Events)));

        matches.AddRange(Store.Facilities
            .Where(x => Matches(x.Name, query) || Matches(x.Location, query))
            .Select(x => new SearchResultItem(x.Name, x.Location, "Sports", SearchDestination.Sports)));

        matches.AddRange(Store.ActiveElections
            .Where(x => Matches(x.Title, query) || Matches(x.Position, query))
            .Select(x => new SearchResultItem(x.Title, x.Position, "Voting", SearchDestination.Voting, x.Title)));

        matches.AddRange(Store.CourseMaterials
            .Where(x => Matches(x.Title, query) || Matches(x.CourseCode, query) || Matches(x.LecturerName, query))
            .Select(x => new SearchResultItem(x.CourseCode, x.Title, "Materials", SearchDestination.Materials, x.CourseCode)));

        matches.AddRange(Store.Notifications
            .Where(x => Matches(x.Title, query) || Matches(x.Message, query))
            .Select(x => new SearchResultItem(x.Title, x.Message, "Notifications", SearchDestination.Notifications)));

        matches.AddRange(Store.RecentActivities
            .Where(x => Matches(x.Title, query) || Matches(x.Description, query))
            .Select(x => new SearchResultItem(x.Title, x.Description, "Recent Activity", SearchDestination.Home)));

        foreach (var item in matches.Take(8))
        {
            SearchResults.Add(item);
        }
    }

    private async void OnSearchResultTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Grid { BindingContext: SearchResultItem item })
        {
            return;
        }

        switch (item.Destination)
        {
            case SearchDestination.Events:
                ShowPortalPage<EventsPage>();
                break;
            case SearchDestination.Sports:
                ShowPortalPage<SportsPage>();
                break;
            case SearchDestination.Voting:
                var electionTitle = item.ReferenceKey;
                if (!string.IsNullOrWhiteSpace(electionTitle))
                {
                    await Navigation.PushAsync(new ElectionGroupPage(Store, electionTitle));
                }
                else
                {
                    ShowPortalPage<VotingPage>();
                }
                break;
            case SearchDestination.Materials:
                var enrollment = Store.MyCourseEnrollments.FirstOrDefault(x => string.Equals(x.CourseCode, item.ReferenceKey, StringComparison.OrdinalIgnoreCase));
                if (enrollment is not null)
                {
                    Store.SelectCourse(enrollment);
                    await Navigation.PushAsync(new CourseMaterialsPage(Store));
                }
                else
                {
                    ShowPortalPage<LibraryPage>();
                }
                break;
            case SearchDestination.Profile:
                ShowPortalPage<ProfilePage>();
                break;
            case SearchDestination.Notifications:
                await Navigation.PushAsync(new NotificationActivityPage(Store));
                break;
            default:
                ShowPortalPage<HomePage>();
                break;
        }
    }

    private string GetPreferredName()
    {
        var fullName = Store.CurrentUser?.FullName?.Trim();
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return "Student";
        }

        var firstName = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return string.IsNullOrWhiteSpace(firstName) ? fullName : firstName;
    }

    private static bool Matches(string? source, string query) =>
        !string.IsNullOrWhiteSpace(source) && source.Contains(query, StringComparison.OrdinalIgnoreCase);

    private void RaiseHomeSummaryProperties()
    {
        OnPropertyChanged(nameof(GreetingTitle));
        OnPropertyChanged(nameof(StudentReference));
        OnPropertyChanged(nameof(AvatarInitial));
        OnPropertyChanged(nameof(HasSearchResults));
        OnPropertyChanged(nameof(SearchSummary));
        OnPropertyChanged(nameof(FeaturedChapelTitle));
        OnPropertyChanged(nameof(FeaturedChapelSubtitle));
        OnPropertyChanged(nameof(SportsStatus));
        OnPropertyChanged(nameof(VotingStatus));
        OnPropertyChanged(nameof(LibraryStatus));
        OnPropertyChanged(nameof(HostelStatus));
    }

    private static void ShowPortalPage<TPage>() where TPage : Page
    {
        if (Application.Current is App app)
        {
            app.ShowPortalPage<TPage>();
        }
    }

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    public sealed class SearchResultItem
    {
        public SearchResultItem(string title, string subtitle, string category, SearchDestination destination, string? referenceKey = null)
        {
            Title = title;
            Subtitle = subtitle;
            Category = category;
            Destination = destination;
            ReferenceKey = referenceKey;
        }

        public string Title { get; }
        public string Subtitle { get; }
        public string Category { get; }
        public SearchDestination Destination { get; }
        public string? ReferenceKey { get; }
    }

    public enum SearchDestination
    {
        Home,
        Events,
        Sports,
        Voting,
        Materials,
        Notifications,
        Profile
    }
}
