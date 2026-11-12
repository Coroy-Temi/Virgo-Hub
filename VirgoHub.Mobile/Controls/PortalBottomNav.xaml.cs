namespace VirgoHub.Mobile.Controls;

public partial class PortalBottomNav : ContentView
{
    public static readonly BindableProperty ActiveTabProperty = BindableProperty.Create(
        nameof(ActiveTab),
        typeof(PortalTab),
        typeof(PortalBottomNav),
        PortalTab.Home,
        propertyChanged: OnActiveTabChanged);

    public PortalBottomNav()
    {
        InitializeComponent();
        UpdateVisualState();
    }

    public PortalTab ActiveTab
    {
        get => (PortalTab)GetValue(ActiveTabProperty);
        set => SetValue(ActiveTabProperty, value);
    }

    private static void OnActiveTabChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is PortalBottomNav nav)
        {
            nav.UpdateVisualState();
        }
    }

    private async void OnHomeTapped(object? sender, TappedEventArgs e) => await NavigateAsync(PortalTab.Home);

    private async void OnEventsTapped(object? sender, TappedEventArgs e) => await NavigateAsync(PortalTab.Events);

    private async void OnVotingTapped(object? sender, TappedEventArgs e) => await NavigateAsync(PortalTab.Voting);

    private async void OnMaterialsTapped(object? sender, TappedEventArgs e) => await NavigateAsync(PortalTab.Materials);

    private async void OnProfileTapped(object? sender, TappedEventArgs e) => await NavigateAsync(PortalTab.Profile);

    private Task NavigateAsync(PortalTab tab)
    {
        if (Application.Current is not App app)
        {
            return Task.CompletedTask;
        }

        var isPersonalMode = app.Store.IsPersonalMode;

        var currentPageType = Application.Current.Windows.FirstOrDefault()?.Page is NavigationPage navigationPage
            ? navigationPage.CurrentPage?.GetType()
            : Application.Current.Windows.FirstOrDefault()?.Page?.GetType();

        switch (tab)
        {
            case PortalTab.Home:
                if (currentPageType == (isPersonalMode ? typeof(PersonalHomePage) : typeof(HomePage)))
                {
                    break;
                }
                if (isPersonalMode)
                {
                    app.ShowPortalPage<PersonalHomePage>();
                }
                else
                {
                    app.ShowPortalPage<HomePage>();
                }
                break;
            case PortalTab.Events:
                if (currentPageType == typeof(EventsPage))
                {
                    break;
                }
                app.ShowPortalPage<EventsPage>();
                break;
            case PortalTab.Voting:
                if (currentPageType == (isPersonalMode ? typeof(PersonalGroupsPage) : typeof(VotingPage)))
                {
                    break;
                }
                if (isPersonalMode)
                {
                    app.ShowPortalPage<PersonalGroupsPage>();
                }
                else
                {
                    app.ShowPortalPage<VotingPage>();
                }
                break;
            case PortalTab.Materials:
                if (currentPageType == typeof(LibraryPage))
                {
                    break;
                }
                app.ShowPortalPage<LibraryPage>();
                break;
            case PortalTab.Profile:
                if (currentPageType == (isPersonalMode ? typeof(PersonalProfilePage) : typeof(ProfilePage)))
                {
                    break;
                }
                if (isPersonalMode)
                {
                    app.ShowPortalPage<PersonalProfilePage>();
                }
                else
                {
                    app.ShowPortalPage<ProfilePage>();
                }
                break;
        }

        return Task.CompletedTask;
    }

    private void UpdateVisualState()
    {
        var isPersonalMode = Application.Current is App app && app.Store.IsPersonalMode;
        EventsLabel.Text = isPersonalMode ? "Plans" : "Events";
        VotingLabel.Text = isPersonalMode ? "Groups" : "Voting";
        MaterialsLabel.Text = isPersonalMode ? "Materials" : "Courses";
        VotingIcon.Source = isPersonalMode ? "icon_results.svg" : "icon_voting.svg";

        ApplyState(HomeShell, HomeLabel, HomeIcon, ActiveTab == PortalTab.Home);
        ApplyState(EventsShell, EventsLabel, EventsIcon, ActiveTab == PortalTab.Events);
        ApplyState(VotingShell, VotingLabel, VotingIcon, ActiveTab == PortalTab.Voting);
        ApplyState(MaterialsShell, MaterialsLabel, MaterialsIcon, ActiveTab == PortalTab.Materials);
        ApplyState(ProfileShell, ProfileLabel, ProfileIcon, ActiveTab == PortalTab.Profile);
    }

    private static void ApplyState(Border shell, Label label, Image icon, bool isActive)
    {
        shell.BackgroundColor = isActive ? Color.FromArgb("#38B8FF") : Colors.Transparent;
        label.TextColor = isActive
            ? Colors.White
            : Application.Current?.RequestedTheme == AppTheme.Dark
                ? Color.FromArgb("#BED3EA")
                : Color.FromArgb("#7B8794");
        label.FontAttributes = isActive ? FontAttributes.Bold : FontAttributes.None;
        icon.Opacity = isActive ? 1 : 0.72;
    }
}
