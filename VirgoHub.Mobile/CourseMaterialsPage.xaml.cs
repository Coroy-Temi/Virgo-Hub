using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using VirgoHub.Mobile.Services;
using VirgoHub.Shared;

namespace VirgoHub.Mobile;

public partial class CourseMaterialsPage : ContentPage
{
    private bool _isPendingApproval;

    public CourseMaterialsPage(StudentPortalStore store)
    {
        InitializeComponent();
        Store = store;
        BindingContext = this;
    }

    public StudentPortalStore Store { get; }

    public ObservableCollection<CourseMaterial> VisibleMaterials { get; } = [];
    public ObservableCollection<PersonalAssessment> VisibleAssessments { get; } = [];

    public bool IsPendingApproval
    {
        get => _isPendingApproval;
        set
        {
            if (SetProperty(ref _isPendingApproval, value))
            {
                OnPropertyChanged(nameof(IsShowingMaterials));
            }
        }
    }

    public bool IsShowingMaterials => !IsPendingApproval;

    public bool HasVisibleAssessments => VisibleAssessments.Count > 0;

    protected override void OnAppearing()
    {
        base.OnAppearing();
        RefreshVisibleMaterials();
    }

    private async void OnOpenMaterialClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { BindingContext: CourseMaterial material })
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(material.ResourceUrl))
        {
            await DisplayAlert("VirgoHub", "This material does not have a resource link yet.", "OK");
            return;
        }

        try
        {
            await Launcher.OpenAsync(material.ResourceUrl);
        }
        catch (Exception ex)
        {
            await DisplayAlert("VirgoHub", ex.Message, "OK");
        }
    }

    private async void OnOpenAssessmentClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { BindingContext: PersonalAssessment assessment })
        {
            return;
        }

        Store.SelectPersonalAssessment(assessment);
        await Navigation.PushAsync(new PersonalAssessmentDetailPage(Store));
    }

    private void RefreshVisibleMaterials()
    {
        VisibleMaterials.Clear();
        VisibleAssessments.Clear();

        IsPendingApproval = Store.SelectedCourseEnrollment?.Status == CourseEnrollmentStatus.Pending;
        if (IsPendingApproval)
        {
            OnPropertyChanged(nameof(HasVisibleAssessments));
            return;
        }

        foreach (var material in Store.GetMaterialsForCourse(Store.SelectedCourseEnrollment?.CourseCode))
        {
            VisibleMaterials.Add(material);
        }

        foreach (var assessment in Store.GetAssessmentsForCourse(Store.SelectedCourseEnrollment?.CourseCode))
        {
            VisibleAssessments.Add(assessment);
        }

        OnPropertyChanged(nameof(HasVisibleAssessments));
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
