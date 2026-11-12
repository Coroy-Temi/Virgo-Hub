using System.Runtime.CompilerServices;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using VirgoHub.Mobile.Services;
using VirgoHub.Shared;

namespace VirgoHub.Mobile;

public partial class PersonalGroupsPage : ContentPage
{
    private string _groupTitle = string.Empty;
    private string _courseCode = string.Empty;
    private string _groupSummary = string.Empty;
    private string _inviteCode = string.Empty;

    public PersonalGroupsPage(StudentPortalStore store)
    {
        InitializeComponent();
        Store = store;
        BindingContext = this;
    }

    public StudentPortalStore Store { get; }

    public string GroupTitle
    {
        get => _groupTitle;
        set => SetProperty(ref _groupTitle, value);
    }

    public string CourseCode
    {
        get => _courseCode;
        set => SetProperty(ref _courseCode, value);
    }

    public string GroupSummary
    {
        get => _groupSummary;
        set => SetProperty(ref _groupSummary, value);
    }

    public string InviteCode
    {
        get => _inviteCode;
        set => SetProperty(ref _inviteCode, value);
    }

    public string PageSummary => Store.IsPersonalLecturer
        ? "Organize each cohort under its own teaching group, share a join code, and keep learners attached to the right learning space."
        : "Join the lecturer spaces that matter to you, then keep your enrolled personal groups neat and easy to revisit.";

    public string RoleSummary => Store.IsPersonalLecturer ? "Lecturer" : "Learner";

    public string GroupListTitle => Store.IsPersonalLecturer ? "My Teaching Groups" : "Joined Teaching Groups";

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (Store.IsAuthenticated)
        {
            await Store.RefreshAsync();
            RefreshPageMeta();
        }
    }

    private async void OnCreateGroupClicked(object? sender, EventArgs e)
    {
        try
        {
            await Store.CreatePersonalTeachingGroupAsync(new CreatePersonalTeachingGroupRequest
            {
                Title = GroupTitle,
                CourseCode = CourseCode,
                Summary = GroupSummary
            });

            GroupTitle = string.Empty;
            CourseCode = string.Empty;
            GroupSummary = string.Empty;
            RefreshPageMeta();
            await DisplayAlert("VirgoHub", "Teaching group created. You can now share its invite code with learners.", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("VirgoHub", ex.Message, "OK");
        }
    }

    private async void OnJoinGroupClicked(object? sender, EventArgs e)
    {
        try
        {
            await Store.JoinPersonalTeachingGroupAsync(new JoinPersonalTeachingGroupRequest
            {
                InviteCode = InviteCode
            });

            InviteCode = string.Empty;
            RefreshPageMeta();
            await DisplayAlert("VirgoHub", "Teaching group joined successfully.", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("VirgoHub", ex.Message, "OK");
        }
    }

    private async void OnCopyCodeClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { BindingContext: PersonalTeachingGroup group })
        {
            return;
        }

        await Clipboard.Default.SetTextAsync(group.InviteCode);
        await DisplayAlert("VirgoHub", $"{group.InviteCode} copied.", "OK");
    }

    private async void OnOpenHubClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { BindingContext: PersonalTeachingGroup group })
        {
            return;
        }

        await Navigation.PushAsync(new PersonalGroupHubPage(Store, group));
    }

    private async void OnRefreshClicked(object? sender, EventArgs e)
    {
        await Store.RefreshAsync();
        RefreshPageMeta();
    }

    private void RefreshPageMeta()
    {
        OnPropertyChanged(nameof(PageSummary));
        OnPropertyChanged(nameof(RoleSummary));
        OnPropertyChanged(nameof(GroupListTitle));
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
