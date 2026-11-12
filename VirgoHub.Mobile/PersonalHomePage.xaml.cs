using Microsoft.Maui.Storage;
using System.Runtime.CompilerServices;
using VirgoHub.Mobile.Services;
using VirgoHub.Shared;

namespace VirgoHub.Mobile;

public partial class PersonalHomePage : ContentPage
{
    private const string PersonalTutorialPrefix = "virgohub.personal.tutorial";

    private readonly StudentPortalStore _store;
    private int _tutorialIndex;
    private bool _showTutorial;

    public PersonalHomePage(StudentPortalStore store)
    {
        InitializeComponent();
        _store = store;
        BindingContext = this;
    }

    public StudentPortalStore Store => _store;

    public string GreetingTitle => $"Hello, {GetPreferredName()}";

    public string PersonalReference => _store.CurrentUser is null
        ? "Personal Workspace"
        : _store.CurrentUser.Role == VirgoHub.Shared.UserRole.Lecturer
            ? "Lecturer workspace"
            : "Learner workspace";

    public string MaterialStatus => _store.MyCourseEnrollments.Count > 0
        ? $"{_store.MyCourseEnrollments.Count} material space(s) active"
        : _store.CourseMaterials.Count > 0
            ? $"{_store.CourseMaterials.Select(x => x.CourseCode).Distinct(StringComparer.OrdinalIgnoreCase).Count()} shared material code(s)"
            : "No material activity yet";

    public string GroupStatus => _store.PersonalGroupSummary;

    public string NotificationStatus => _store.Notifications.Count > 0
        ? $"{_store.Notifications.Count} recent alert(s)"
        : "Quiet for now";

    public bool ShowTutorial
    {
        get => _showTutorial;
        set => SetProperty(ref _showTutorial, value);
    }

    public string TutorialTitle => CurrentTutorialStep.Title;
    public string TutorialMessage => CurrentTutorialStep.Message;
    public string TutorialStepLabel => $"Step {_tutorialIndex + 1} of {CurrentTutorialSteps.Length}";
    public string TutorialNextButtonLabel => _tutorialIndex == CurrentTutorialSteps.Length - 1 ? "Start" : "Next";
    public string TutorialDirectionTitle => CurrentTutorialStep.DirectionTitle;
    public string TutorialDirectionHint => CurrentTutorialStep.DirectionHint;
    public string TutorialPrimaryActionLabel => CurrentTutorialStep.PrimaryActionLabel;
    public bool ShowTutorialPrimaryAction => !string.IsNullOrWhiteSpace(CurrentTutorialStep.PrimaryActionLabel);

    public Color HeroHighlightStroke => GetHighlightStroke(PersonalTutorialTarget.Hero);
    public double HeroHighlightStrokeThickness => GetHighlightStrokeThickness(PersonalTutorialTarget.Hero);
    public Color ProfileHighlightStroke => GetHighlightStroke(PersonalTutorialTarget.Profile);
    public double ProfileHighlightStrokeThickness => GetHighlightStrokeThickness(PersonalTutorialTarget.Profile);
    public Color GroupsHighlightStroke => GetHighlightStroke(PersonalTutorialTarget.Groups);
    public double GroupsHighlightStrokeThickness => GetHighlightStrokeThickness(PersonalTutorialTarget.Groups);
    public Color MaterialsHighlightStroke => GetHighlightStroke(PersonalTutorialTarget.Materials);
    public double MaterialsHighlightStrokeThickness => GetHighlightStrokeThickness(PersonalTutorialTarget.Materials);
    public Color AlertsHighlightStroke => GetHighlightStroke(PersonalTutorialTarget.Alerts);
    public double AlertsHighlightStrokeThickness => GetHighlightStrokeThickness(PersonalTutorialTarget.Alerts);
    public Color PlansHighlightStroke => GetHighlightStroke(PersonalTutorialTarget.Plans);
    public double PlansHighlightStrokeThickness => GetHighlightStrokeThickness(PersonalTutorialTarget.Plans);

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_store.IsAuthenticated)
        {
            await _store.RefreshAsync();
            RefreshSummaries();
            ShowTutorialIfNeeded();
        }
    }

    private async void OnRefreshClicked(object? sender, EventArgs e)
    {
        await _store.RefreshAsync();
        RefreshSummaries();
    }

    private void OnGroupsClicked(object? sender, TappedEventArgs e)
    {
        if (Application.Current is App app)
        {
            app.ShowPortalPage<PersonalGroupsPage>();
        }
    }

    private void OnMaterialsClicked(object? sender, TappedEventArgs e)
    {
        if (Application.Current is App app)
        {
            app.ShowPortalPage<LibraryPage>();
        }
    }

    private async void OnNotificationsClicked(object? sender, TappedEventArgs e)
    {
        await Navigation.PushAsync(new NotificationsPage(_store));
    }

    private async void OnNotificationsClicked(object? sender, EventArgs e)
    {
        await Navigation.PushAsync(new NotificationsPage(_store));
    }

    private void OnProfileClicked(object? sender, TappedEventArgs e)
    {
        if (Application.Current is App app)
        {
            app.ShowPortalPage<PersonalProfilePage>();
        }
    }

    private void OnEventsClicked(object? sender, TappedEventArgs e)
    {
        if (Application.Current is App app)
        {
            app.ShowPortalPage<EventsPage>();
        }
    }

    private void OnTutorialNextClicked(object? sender, EventArgs e)
    {
        if (_tutorialIndex < CurrentTutorialSteps.Length - 1)
        {
            _tutorialIndex++;
            RefreshTutorial();
            return;
        }

        DismissTutorial();
    }

    private void OnTutorialSkipClicked(object? sender, EventArgs e) => DismissTutorial();

    private async void OnTutorialPrimaryActionClicked(object? sender, EventArgs e)
    {
        switch (CurrentTutorialStep.Target)
        {
            case PersonalTutorialTarget.Groups:
                if (Application.Current is App groupsApp)
                {
                    groupsApp.ShowPortalPage<PersonalGroupsPage>();
                }
                break;
            case PersonalTutorialTarget.Materials:
                if (Application.Current is App materialsApp)
                {
                    materialsApp.ShowPortalPage<LibraryPage>();
                }
                break;
            case PersonalTutorialTarget.Alerts:
                await Navigation.PushAsync(new NotificationsPage(_store));
                break;
            case PersonalTutorialTarget.Plans:
                if (Application.Current is App plansApp)
                {
                    plansApp.ShowPortalPage<EventsPage>();
                }
                break;
            case PersonalTutorialTarget.Profile:
                if (Application.Current is App profileApp)
                {
                    profileApp.ShowPortalPage<PersonalProfilePage>();
                }
                break;
        }
    }

    private void ShowTutorialIfNeeded()
    {
        if (string.IsNullOrWhiteSpace(_store.CurrentUser?.Id))
        {
            return;
        }

        if (Preferences.Default.Get(GetTutorialKey(), false))
        {
            return;
        }

        _tutorialIndex = 0;
        ShowTutorial = true;
        RefreshTutorial();
    }

    private void DismissTutorial()
    {
        Preferences.Default.Set(GetTutorialKey(), true);
        ShowTutorial = false;
        RefreshTutorial();
    }

    private string GetTutorialKey() => $"{PersonalTutorialPrefix}.{_store.CurrentUser?.Id}";

    private PersonalTutorialStep CurrentTutorialStep => CurrentTutorialSteps[_tutorialIndex];

    private PersonalTutorialStep[] CurrentTutorialSteps => Store.IsPersonalLecturer
        ? LecturerTutorialSteps
        : LearnerTutorialSteps;

    private static PersonalTutorialStep[] LecturerTutorialSteps =>
    [
        new("Welcome, Lecturer", "This workspace is shaped for teaching. You can guide learners, publish materials, drop messages, and manage assignments without the heavier school modules.", PersonalTutorialTarget.Hero, "Start here", "This home page gives you the quick read on your personal teaching flow before you move into action.", string.Empty),
        new("Groups", "Open Groups to create teaching spaces, share invite codes, and keep each cohort organized under the right course.", PersonalTutorialTarget.Groups, "Bottom-left card", "Use the highlighted Groups card below when you want to manage a cohort or open the group hub.", "Open Groups"),
        new("Materials", "Materials is where your shared files and lecturer resources stay organized for every teaching group you manage.", PersonalTutorialTarget.Materials, "Top-right card", "Use the highlighted Materials card to publish or revisit outlines, notes, and linked resources.", "Open Materials"),
        new("Plans", "Plans is your academic control center for tests and exams. Set the answers there so learners only see results after they submit.", PersonalTutorialTarget.Plans, "Bottom-right card", "Use the highlighted Plans card whenever you want to create or review a graded assessment.", "Open Plans"),
        new("Alerts", "Alerts keeps the teaching side responsive with activity on materials, reminders, submissions, and group messages.", PersonalTutorialTarget.Alerts, "Bottom-left card", "Use the highlighted Alerts card to tune what reaches your device and review recent activity.", "Open Alerts"),
        new("Profile", "Profile keeps notifications, settings, tutorial reset, and sign-out in one clean place.", PersonalTutorialTarget.Profile, "Top-right profile button", "Use the highlighted profile button when you need account controls or want to replay this walkthrough later.", "Open Profile")
    ];

    private static PersonalTutorialStep[] LearnerTutorialSteps =>
    [
        new("Welcome, Learner", "This workspace is shaped for guided study. You can join lecturer spaces, open materials, submit work, and keep your reminders tidy.", PersonalTutorialTarget.Hero, "Start here", "This home page is your quick map to every learning area inside VirgoHub Personal.", string.Empty),
        new("Groups", "Open Groups to join a lecturer's teaching space with the invite code they share with you and to send messages back when needed.", PersonalTutorialTarget.Groups, "Bottom-left card", "Use the highlighted Groups card below whenever you want to join or open a teaching space.", "Open Groups"),
        new("Materials", "Materials shows the shared resource spaces linked to the groups you belong to, so files and notes stay organized.", PersonalTutorialTarget.Materials, "Top-right card", "Use the highlighted Materials card to open lecturer resources and revisit what has already been shared.", "Open Materials"),
        new("Plans", "Plans helps you keep up with tests, exams, study reminders, and results once you finish a graded assessment.", PersonalTutorialTarget.Plans, "Bottom-right card", "Use the highlighted Plans card for assessment dates, submissions, and released results.", "Open Plans"),
        new("Alerts", "Alerts keeps material updates, lecturer posts, and workspace reminders visible so you do not miss important changes.", PersonalTutorialTarget.Alerts, "Bottom-left card", "Use the highlighted Alerts card to manage what VirgoHub sends to this device.", "Open Alerts"),
        new("Profile", "Profile keeps notifications, settings, tutorial reset, and sign-out in one clean place.", PersonalTutorialTarget.Profile, "Top-right profile button", "Use the highlighted profile button when you want account controls or need to reset this walkthrough.", "Open Profile")
    ];

    private string GetPreferredName()
    {
        var fullName = _store.CurrentUser?.FullName?.Trim();
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return "there";
        }

        var firstName = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return string.IsNullOrWhiteSpace(firstName) ? fullName : firstName;
    }

    private void RefreshSummaries()
    {
        OnPropertyChanged(nameof(GreetingTitle));
        OnPropertyChanged(nameof(PersonalReference));
        OnPropertyChanged(nameof(MaterialStatus));
        OnPropertyChanged(nameof(GroupStatus));
        OnPropertyChanged(nameof(NotificationStatus));
    }

    private void RefreshTutorial()
    {
        OnPropertyChanged(nameof(TutorialTitle));
        OnPropertyChanged(nameof(TutorialMessage));
        OnPropertyChanged(nameof(TutorialStepLabel));
        OnPropertyChanged(nameof(TutorialNextButtonLabel));
        OnPropertyChanged(nameof(TutorialDirectionTitle));
        OnPropertyChanged(nameof(TutorialDirectionHint));
        OnPropertyChanged(nameof(TutorialPrimaryActionLabel));
        OnPropertyChanged(nameof(ShowTutorialPrimaryAction));
        OnPropertyChanged(nameof(HeroHighlightStroke));
        OnPropertyChanged(nameof(HeroHighlightStrokeThickness));
        OnPropertyChanged(nameof(ProfileHighlightStroke));
        OnPropertyChanged(nameof(ProfileHighlightStrokeThickness));
        OnPropertyChanged(nameof(GroupsHighlightStroke));
        OnPropertyChanged(nameof(GroupsHighlightStrokeThickness));
        OnPropertyChanged(nameof(MaterialsHighlightStroke));
        OnPropertyChanged(nameof(MaterialsHighlightStrokeThickness));
        OnPropertyChanged(nameof(AlertsHighlightStroke));
        OnPropertyChanged(nameof(AlertsHighlightStrokeThickness));
        OnPropertyChanged(nameof(PlansHighlightStroke));
        OnPropertyChanged(nameof(PlansHighlightStrokeThickness));
    }

    private Color GetHighlightStroke(PersonalTutorialTarget target)
    {
        if (ShowTutorial && CurrentTutorialStep.Target == target)
        {
            return Color.FromArgb("#25A9F5");
        }

        return target == PersonalTutorialTarget.Profile
            ? Colors.Transparent
            : (Application.Current?.RequestedTheme == AppTheme.Dark
                ? Color.FromArgb("#1A3B70")
                : Color.FromArgb("#DCEEFF"));
    }

    private double GetHighlightStrokeThickness(PersonalTutorialTarget target)
    {
        if (ShowTutorial && CurrentTutorialStep.Target == target)
        {
            return 3;
        }

        return target == PersonalTutorialTarget.Profile ? 0 : 1;
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

    private sealed record PersonalTutorialStep(
        string Title,
        string Message,
        PersonalTutorialTarget Target,
        string DirectionTitle,
        string DirectionHint,
        string PrimaryActionLabel);

    private enum PersonalTutorialTarget
    {
        Hero,
        Groups,
        Materials,
        Alerts,
        Plans,
        Profile
    }
}
