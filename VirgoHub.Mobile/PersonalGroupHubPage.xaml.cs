using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using VirgoHub.Mobile.Services;
using VirgoHub.Shared;

namespace VirgoHub.Mobile;

public partial class PersonalGroupHubPage : ContentPage
{
    private string _postTitle = string.Empty;
    private string _postMessage = string.Empty;
    private string _postAttachmentUrl = string.Empty;
    private string _replyMessage = string.Empty;
    private string _replyAttachmentUrl = string.Empty;
    private string _materialTitle = string.Empty;
    private string _materialType = string.Empty;
    private string _materialLink = string.Empty;
    private string _materialSummary = string.Empty;
    private string _assignmentTitle = string.Empty;
    private string _assignmentInstructions = string.Empty;
    private string _assignmentLink = string.Empty;
    private string _assignmentDueDays = "7";

    public PersonalGroupHubPage(StudentPortalStore store, PersonalTeachingGroup group)
    {
        InitializeComponent();
        Store = store;
        Group = group;
        BindingContext = this;
    }

    public StudentPortalStore Store { get; }
    public PersonalTeachingGroup Group { get; }
    public ObservableCollection<PersonalGroupPost> VisiblePosts { get; } = [];
    public ObservableCollection<PersonalAssignment> VisibleAssignments { get; } = [];

    public string GroupMeta => $"{Group.CourseCode} | {Group.Members.Count} learner(s)";
    public string AssignmentButtonText => Store.IsPersonalLecturer ? "Review submissions" : "Submit work";

    public string PostTitle { get => _postTitle; set => SetProperty(ref _postTitle, value); }
    public string PostMessage { get => _postMessage; set => SetProperty(ref _postMessage, value); }
    public string PostAttachmentUrl { get => _postAttachmentUrl; set => SetProperty(ref _postAttachmentUrl, value); }
    public string ReplyMessage { get => _replyMessage; set => SetProperty(ref _replyMessage, value); }
    public string ReplyAttachmentUrl { get => _replyAttachmentUrl; set => SetProperty(ref _replyAttachmentUrl, value); }
    public string MaterialTitle { get => _materialTitle; set => SetProperty(ref _materialTitle, value); }
    public string MaterialType { get => _materialType; set => SetProperty(ref _materialType, value); }
    public string MaterialLink { get => _materialLink; set => SetProperty(ref _materialLink, value); }
    public string MaterialSummary { get => _materialSummary; set => SetProperty(ref _materialSummary, value); }
    public string AssignmentTitle { get => _assignmentTitle; set => SetProperty(ref _assignmentTitle, value); }
    public string AssignmentInstructions { get => _assignmentInstructions; set => SetProperty(ref _assignmentInstructions, value); }
    public string AssignmentLink { get => _assignmentLink; set => SetProperty(ref _assignmentLink, value); }
    public string AssignmentDueDays { get => _assignmentDueDays; set => SetProperty(ref _assignmentDueDays, value); }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (Store.IsAuthenticated)
        {
            await Store.RefreshAsync();
        }

        RefreshContent();
    }

    private async void OnSendMessageClicked(object? sender, EventArgs e) =>
        await PostAsync(false);

    private async void OnPostAnnouncementClicked(object? sender, EventArgs e) =>
        await PostAsync(true);

    private async void OnSendReplyClicked(object? sender, EventArgs e)
    {
        try
        {
            await Store.CreatePersonalGroupPostAsync(new CreatePersonalGroupPostRequest
            {
                GroupId = Group.Id,
                Title = "Learner reply",
                Message = ReplyMessage,
                AttachmentUrl = ReplyAttachmentUrl,
                IsAnnouncement = false
            });

            ReplyMessage = string.Empty;
            ReplyAttachmentUrl = string.Empty;
            RefreshContent();
            await DisplayAlert("VirgoHub", "Your message has been sent to the lecturer.", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("VirgoHub", ex.Message, "OK");
        }
    }

    private async Task PostAsync(bool isAnnouncement)
    {
        try
        {
            await Store.CreatePersonalGroupPostAsync(new CreatePersonalGroupPostRequest
            {
                GroupId = Group.Id,
                Title = string.IsNullOrWhiteSpace(PostTitle) ? (isAnnouncement ? "Group announcement" : "Group update") : PostTitle,
                Message = PostMessage,
                AttachmentUrl = PostAttachmentUrl,
                IsAnnouncement = isAnnouncement
            });

            PostTitle = string.Empty;
            PostMessage = string.Empty;
            PostAttachmentUrl = string.Empty;
            RefreshContent();
            await DisplayAlert("VirgoHub", isAnnouncement ? "Announcement posted." : "Message sent.", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("VirgoHub", ex.Message, "OK");
        }
    }

    private async void OnPublishMaterialClicked(object? sender, EventArgs e)
    {
        try
        {
            await Store.CreatePersonalMaterialAsync(new CreatePersonalMaterialRequest
            {
                GroupId = Group.Id,
                Title = MaterialTitle,
                MaterialType = MaterialType,
                ResourceUrl = MaterialLink,
                Summary = MaterialSummary
            });

            MaterialTitle = string.Empty;
            MaterialType = string.Empty;
            MaterialLink = string.Empty;
            MaterialSummary = string.Empty;
            RefreshContent();
            await DisplayAlert("VirgoHub", "Material published for this group.", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("VirgoHub", ex.Message, "OK");
        }
    }

    private async void OnPublishAssignmentClicked(object? sender, EventArgs e)
    {
        try
        {
            var dueDays = int.TryParse(AssignmentDueDays, out var parsed) ? parsed : 7;
            await Store.CreatePersonalAssignmentAsync(new CreatePersonalAssignmentRequest
            {
                GroupId = Group.Id,
                Title = AssignmentTitle,
                Instructions = AssignmentInstructions,
                ResourceUrl = AssignmentLink,
                DueAt = DateTimeOffset.UtcNow.AddDays(Math.Max(1, dueDays))
            });

            AssignmentTitle = string.Empty;
            AssignmentInstructions = string.Empty;
            AssignmentLink = string.Empty;
            AssignmentDueDays = "7";
            RefreshContent();
            await DisplayAlert("VirgoHub", "Assignment published successfully.", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("VirgoHub", ex.Message, "OK");
        }
    }

    private async void OnAssignmentActionClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { BindingContext: PersonalAssignment assignment })
        {
            return;
        }

        if (Store.IsPersonalLecturer)
        {
            var submissions = Store.PersonalAssignmentSubmissions
                .Where(x => x.AssignmentId == assignment.Id)
                .OrderByDescending(x => x.SubmittedAt)
                .Select(x => $"{x.StudentName}: {x.Note}")
                .ToList();

            await DisplayAlert("VirgoHub", submissions.Count == 0 ? "No submissions yet." : string.Join("\n\n", submissions), "OK");
            return;
        }

        var note = await DisplayPromptAsync("Submit Assignment", "Add a short note for the lecturer:", initialValue: Store.GetSubmissionForAssignment(assignment.Id)?.Note);
        if (note is null)
        {
            return;
        }

        var link = await DisplayPromptAsync("Submit Assignment", "Add a link to your assignment file or shared document:", initialValue: Store.GetSubmissionForAssignment(assignment.Id)?.ResourceUrl);
        if (link is null && string.IsNullOrWhiteSpace(note))
        {
            return;
        }

        try
        {
            await Store.SubmitPersonalAssignmentAsync(new SubmitPersonalAssignmentRequest
            {
                AssignmentId = assignment.Id,
                Note = note,
                ResourceUrl = link ?? string.Empty
            });

            RefreshContent();
            await DisplayAlert("VirgoHub", "Assignment sent successfully.", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("VirgoHub", ex.Message, "OK");
        }
    }

    private async void OnRefreshClicked(object? sender, EventArgs e)
    {
        await Store.RefreshAsync();
        RefreshContent();
    }

    private void RefreshContent()
    {
        VisiblePosts.Clear();
        foreach (var post in Store.GetPostsForGroup(Group.Id))
        {
            VisiblePosts.Add(post);
        }

        VisibleAssignments.Clear();
        foreach (var assignment in Store.GetAssignmentsForGroup(Group.Id))
        {
            VisibleAssignments.Add(assignment);
        }

        OnPropertyChanged(nameof(GroupMeta));
        OnPropertyChanged(nameof(AssignmentButtonText));
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
