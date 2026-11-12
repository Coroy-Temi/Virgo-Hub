using System.Runtime.CompilerServices;
using VirgoHub.Mobile.Services;
using VirgoHub.Shared;

namespace VirgoHub.Mobile;

public partial class LibraryPage : ContentPage
{
    private string _courseCodeQuery = string.Empty;

    public LibraryPage(StudentPortalStore store)
    {
        InitializeComponent();
        Store = store;
        BindingContext = this;
    }

    public StudentPortalStore Store { get; }

    public string CourseCodeQuery
    {
        get => _courseCodeQuery;
        set => SetProperty(ref _courseCodeQuery, value);
    }

    public string PageTitle => Store.IsPersonalMode ? "Shared Materials" : "Course Materials";

    public string PageSummary => Store.IsPersonalMode
        ? (Store.IsPersonalLecturer
            ? "Your teaching groups create material spaces automatically. Open a space to view the resources linked to that group."
            : "Use a lecturer invite code to join a teaching group, then open the material space connected to that group.")
        : "Search with your course code, enroll, and keep every course in its own material space.";

    public string EntryPlaceholder => Store.IsPersonalMode
        ? "Enter a lecturer invite code, for example PVT1-2468"
        : "Enter course code, for example CSC 312";

    public string ActionButtonText => Store.IsPersonalMode ? "Join with code" : "Enroll course";

    public string SpacesTitle => Store.IsPersonalMode ? "My Material Spaces" : "My Enrolled Courses";

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (Store.IsAuthenticated)
        {
            await Store.RefreshAsync();
        }

        RefreshLabels();
    }

    private async void OnEnrollCourseClicked(object? sender, EventArgs e)
    {
        try
        {
            string message;
            if (Store.IsPersonalMode)
            {
                await Store.JoinPersonalTeachingGroupAsync(new JoinPersonalTeachingGroupRequest
                {
                    InviteCode = CourseCodeQuery
                });

                CourseCodeQuery = string.Empty;
                message = "Teaching group joined successfully. The linked material space is now ready to open.";
            }
            else
            {
                await Store.EnrollCourseAsync(new CreateCourseEnrollmentRequest
                {
                    CourseCode = CourseCodeQuery
                });

                var latestEnrollment = Store.SelectedCourseEnrollment;
                CourseCodeQuery = string.Empty;
                message = latestEnrollment?.Status == CourseEnrollmentStatus.Pending
                    ? "Your enrollment request has been sent. The lecturer needs to approve it before the materials will open."
                    : "Course enrolled successfully. Open the course to view its materials.";
            }

            await DisplayAlert("VirgoHub", message, "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("VirgoHub", ex.Message, "OK");
        }
    }

    private async void OnOpenCourseClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { BindingContext: CourseEnrollment enrollment })
        {
            return;
        }

        Store.SelectCourse(enrollment);
        if (enrollment.Status == CourseEnrollmentStatus.Pending)
        {
            await DisplayAlert("VirgoHub", Store.IsPersonalMode ? "This material space is still pending lecturer approval." : "This course is still pending lecturer approval.", "OK");
            return;
        }

        await Navigation.PushAsync(new CourseMaterialsPage(Store));
    }

    private void RefreshLabels()
    {
        OnPropertyChanged(nameof(PageTitle));
        OnPropertyChanged(nameof(PageSummary));
        OnPropertyChanged(nameof(EntryPlaceholder));
        OnPropertyChanged(nameof(ActionButtonText));
        OnPropertyChanged(nameof(SpacesTitle));
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
}
