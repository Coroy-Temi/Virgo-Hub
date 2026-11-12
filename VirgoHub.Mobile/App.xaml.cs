using VirgoHub.Mobile.Services;

namespace VirgoHub.Mobile;

public partial class App : Application
{
    private readonly IServiceProvider _services;
    private readonly StudentPortalStore _store;

    public App(IServiceProvider services, StudentPortalStore store)
    {
        InitializeComponent();
        UserAppTheme = AppTheme.Light;
        _services = services;
        _store = store;
    }

    public StudentPortalStore Store => _store;

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(BuildNavigationPage<MainPage>());
    }

    public void ShowHomePage() => SetRootPage(_store.IsPersonalMode ? typeof(PersonalHomePage) : typeof(HomePage));

    public void ShowLoginPage() => SetRootPage<MainPage>();

    public void ShowPortalPage<TPage>() where TPage : Page => SetRootPage<TPage>();

    private void SetRootPage<TPage>() where TPage : Page
        => SetRootPage(typeof(TPage));

    private void SetRootPage(Type pageType)
    {
        if (Windows.Count == 0)
        {
            return;
        }

        Windows[0].Page = BuildNavigationPage(pageType);
    }

    private NavigationPage BuildNavigationPage<TPage>() where TPage : Page
        => BuildNavigationPage(typeof(TPage));

    private NavigationPage BuildNavigationPage(Type pageType)
    {
        var page = (Page)_services.GetRequiredService(pageType);
        return new NavigationPage(page)
        {
            BarBackgroundColor = Color.FromArgb("#F3F7FF"),
            BarTextColor = Color.FromArgb("#082A63")
        };
    }
}
