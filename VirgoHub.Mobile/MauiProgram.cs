using VirgoHub.Mobile.Services;

namespace VirgoHub.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        builder.Services.AddSingleton(new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(25)
        });
        builder.Services.AddSingleton<IDeviceNotificationService, DeviceNotificationService>();
        builder.Services.AddSingleton<VirgoHubApiClient>();
        builder.Services.AddSingleton<StudentPortalStore>();
        builder.Services.AddTransient<MainPage>();
        builder.Services.AddTransient<HomePage>();
        builder.Services.AddTransient<PersonalHomePage>();
        builder.Services.AddTransient<EventsPage>();
        builder.Services.AddTransient<SportsPage>();
        builder.Services.AddTransient<VotingPage>();
        builder.Services.AddTransient<PersonalGroupsPage>();
        builder.Services.AddTransient<PersonalAssessmentDetailPage>();
        builder.Services.AddTransient<ElectionGroupPage>();
        builder.Services.AddTransient<ElectionDetailPage>();
        builder.Services.AddTransient<ElectionResultsPage>();
        builder.Services.AddTransient<LibraryPage>();
        builder.Services.AddTransient<CourseMaterialsPage>();
        builder.Services.AddTransient<ComplaintsPage>();
        builder.Services.AddTransient<ProfilePage>();
        builder.Services.AddTransient<PersonalProfilePage>();
        builder.Services.AddTransient<NotificationsPage>();
        builder.Services.AddTransient<NotificationActivityPage>();
        builder.Services.AddTransient<SecurityPage>();

        return builder.Build();
    }
}
