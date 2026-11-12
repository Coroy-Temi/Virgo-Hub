using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Maui.Storage;
using VirgoHub.Shared;

namespace VirgoHub.Mobile.Services;

public sealed class StudentPortalStore : INotifyPropertyChanged
{
    private const string PreferredServerUrlKey = "virgohub.server.url";
    private const string LastSelectedSchoolTenantKey = "virgohub.school.tenant";
    private const string NotificationsEnabledKey = "virgohub.notifications.enabled";
    private const string DeliveredNotificationsKey = "virgohub.notifications.delivered";
    private const string ElectionNotificationsKey = "virgohub.notifications.elections";
    private const string EventNotificationsKey = "virgohub.notifications.events";
    private const string ComplaintNotificationsKey = "virgohub.notifications.complaints";
    private const string NewsNotificationsKey = "virgohub.notifications.news";

    private readonly VirgoHubApiClient _apiClient;
    private readonly IDeviceNotificationService _deviceNotificationService;
    private readonly HashSet<string> _deliveredNotificationIds;
    private bool _isAuthenticated;
    private bool _isDarkMode;
    private bool _areDeviceNotificationsEnabled;
    private bool _receiveElectionNotifications;
    private bool _receiveEventNotifications;
    private bool _receiveComplaintNotifications;
    private bool _receiveNewsNotifications;
    private AppUseMode _currentMode = AppUseMode.School;
    private UserProfile? _currentUser;
    private TenantProfile? _currentTenant;
    private CourseEnrollment? _selectedCourseEnrollment;
    private Election? _selectedElection;
    private PersonalAssessment? _selectedPersonalAssessment;

    public StudentPortalStore(VirgoHubApiClient apiClient, IDeviceNotificationService deviceNotificationService)
    {
        _apiClient = apiClient;
        _deviceNotificationService = deviceNotificationService;
        _areDeviceNotificationsEnabled = true;
        _receiveElectionNotifications = true;
        _receiveEventNotifications = true;
        _receiveComplaintNotifications = true;
        _receiveNewsNotifications = false;
        _deliveredNotificationIds = [];
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<CampusEvent> UpcomingEvents { get; } = [];
    public ObservableCollection<SportsFacility> Facilities { get; } = [];
    public ObservableCollection<SportsBooking> MyBookings { get; } = [];
    public ObservableCollection<SportsBooking> SportsDirectorBookings { get; } = [];
    public ObservableCollection<ComplaintTicket> MyComplaints { get; } = [];
    public ObservableCollection<Election> ActiveElections { get; } = [];
    public ObservableCollection<CourseMaterial> CourseMaterials { get; } = [];
    public ObservableCollection<CourseEnrollment> MyCourseEnrollments { get; } = [];
    public ObservableCollection<PersonalTeachingGroup> PersonalGroups { get; } = [];
    public ObservableCollection<PersonalGroupPost> PersonalGroupPosts { get; } = [];
    public ObservableCollection<PersonalAssessment> PersonalAssessments { get; } = [];
    public ObservableCollection<PersonalAssessmentSubmission> PersonalAssessmentSubmissions { get; } = [];
    public ObservableCollection<PersonalAssignment> PersonalAssignments { get; } = [];
    public ObservableCollection<PersonalAssignmentSubmission> PersonalAssignmentSubmissions { get; } = [];
    public ObservableCollection<AppNotification> Notifications { get; } = [];
    public ObservableCollection<ActivityEntry> RecentActivities { get; } = [];
    public ObservableCollection<SchoolDirectoryEntry> SchoolDirectory { get; } = [];

    public IReadOnlyList<string> SlotOptions { get; } =
    [
        "08:00 - 09:00",
        "09:00 - 10:00",
        "10:00 - 11:00",
        "13:00 - 14:00",
        "14:00 - 15:00",
        "16:00 - 17:00"
    ];

    public IReadOnlyList<string> ComplaintCategories { get; } =
    [
        "Electrical",
        "Plumbing",
        "Furniture",
        "Cleaning",
        "Internet",
        "Other"
    ];

    public string DefaultBaseUrl => Preferences.Default.Get(PreferredServerUrlKey, VirgoHubApiClient.GetDefaultBaseUrl());

    public bool IsAuthenticated
    {
        get => _isAuthenticated;
        private set
        {
            if (SetProperty(ref _isAuthenticated, value))
            {
                OnPropertyChanged(nameof(ShowDashboard));
                OnPropertyChanged(nameof(ShowLogin));
            }
        }
    }

    public bool ShowDashboard => IsAuthenticated;
    public bool ShowLogin => !IsAuthenticated;

    public AppUseMode CurrentMode
    {
        get => _currentMode;
        set
        {
            if (SetProperty(ref _currentMode, value))
            {
                LoadNotificationPreferencesForCurrentContext();
                OnPropertyChanged(nameof(IsPersonalMode));
                OnPropertyChanged(nameof(IsSchoolMode));
                OnPropertyChanged(nameof(CurrentUserHostel));
                OnPropertyChanged(nameof(CurrentProfileSummary));
                OnPropertyChanged(nameof(SecuritySummary));
                OnPropertyChanged(nameof(NotificationPreferenceSummary));
                OnPropertyChanged(nameof(IsPersonalLecturer));
                OnPropertyChanged(nameof(IsPersonalLearner));
                OnPropertyChanged(nameof(PersonalGroupSummary));
                OnPropertyChanged(nameof(NotificationPageTitle));
                OnPropertyChanged(nameof(NotificationPageSummary));
                OnPropertyChanged(nameof(ElectionNotificationTitle));
                OnPropertyChanged(nameof(ElectionNotificationSummary));
                OnPropertyChanged(nameof(EventNotificationTitle));
                OnPropertyChanged(nameof(EventNotificationSummary));
                OnPropertyChanged(nameof(ComplaintNotificationTitle));
                OnPropertyChanged(nameof(ComplaintNotificationSummary));
                OnPropertyChanged(nameof(NewsNotificationTitle));
                OnPropertyChanged(nameof(NewsNotificationSummary));
                OnPropertyChanged(nameof(NotificationFilterUpdatesLabel));
                OnPropertyChanged(nameof(NotificationFilterRemindersLabel));
                OnPropertyChanged(nameof(NotificationFilterAccessLabel));
                OnPropertyChanged(nameof(NotificationFilterNewsLabel));
                OnPropertyChanged(nameof(SupportSummary));
            }
        }
    }

    public bool IsPersonalMode => CurrentMode == AppUseMode.Personal;
    public bool IsSchoolMode => CurrentMode == AppUseMode.School;

    public UserProfile? CurrentUser
    {
        get => _currentUser;
        private set
        {
            if (SetProperty(ref _currentUser, value))
            {
                LoadNotificationPreferencesForCurrentContext();
                OnPropertyChanged(nameof(CurrentUserName));
                OnPropertyChanged(nameof(CurrentUserMeta));
                OnPropertyChanged(nameof(CurrentUserHostel));
                OnPropertyChanged(nameof(CurrentProfileSummary));
                OnPropertyChanged(nameof(SecuritySummary));
                OnPropertyChanged(nameof(ProfileHeader));
                OnPropertyChanged(nameof(HasSportsDirectorAccess));
                OnPropertyChanged(nameof(SportsDirectorSummary));
                OnPropertyChanged(nameof(HasNotifications));
                OnPropertyChanged(nameof(HasActivities));
                OnPropertyChanged(nameof(IsPersonalLecturer));
                OnPropertyChanged(nameof(IsPersonalLearner));
                OnPropertyChanged(nameof(PersonalGroupSummary));
            }
        }
    }

    public TenantProfile? CurrentTenant
    {
        get => _currentTenant;
        private set
        {
            if (SetProperty(ref _currentTenant, value))
            {
                OnPropertyChanged(nameof(CurrentTenantName));
            }
        }
    }

    public string CurrentTenantName => CurrentTenant?.Name ?? "VirgoHub";

    public bool IsDarkMode
    {
        get => _isDarkMode;
        set
        {
            if (!SetProperty(ref _isDarkMode, value))
            {
                return;
            }

            if (Application.Current is not null)
            {
                Application.Current.UserAppTheme = value ? AppTheme.Dark : AppTheme.Light;
            }

            OnPropertyChanged(nameof(SecuritySummary));
        }
    }

    public bool AreDeviceNotificationsEnabled
    {
        get => _areDeviceNotificationsEnabled;
        private set
        {
            if (SetProperty(ref _areDeviceNotificationsEnabled, value))
            {
                Preferences.Default.Set(BuildPreferenceKey(NotificationsEnabledKey), value);
                OnPropertyChanged(nameof(NotificationPreferenceSummary));
            }
        }
    }

    public bool ReceiveElectionNotifications
    {
        get => _receiveElectionNotifications;
        set
        {
            if (SetProperty(ref _receiveElectionNotifications, value))
            {
                Preferences.Default.Set(BuildPreferenceKey(ElectionNotificationsKey), value);
            }
        }
    }

    public bool ReceiveEventNotifications
    {
        get => _receiveEventNotifications;
        set
        {
            if (SetProperty(ref _receiveEventNotifications, value))
            {
                Preferences.Default.Set(BuildPreferenceKey(EventNotificationsKey), value);
            }
        }
    }

    public bool ReceiveComplaintNotifications
    {
        get => _receiveComplaintNotifications;
        set
        {
            if (SetProperty(ref _receiveComplaintNotifications, value))
            {
                Preferences.Default.Set(BuildPreferenceKey(ComplaintNotificationsKey), value);
            }
        }
    }

    public bool ReceiveNewsNotifications
    {
        get => _receiveNewsNotifications;
        set
        {
            if (SetProperty(ref _receiveNewsNotifications, value))
            {
                Preferences.Default.Set(BuildPreferenceKey(NewsNotificationsKey), value);
            }
        }
    }

    public CourseEnrollment? SelectedCourseEnrollment
    {
        get => _selectedCourseEnrollment;
        private set
        {
            if (SetProperty(ref _selectedCourseEnrollment, value))
            {
                OnPropertyChanged(nameof(SelectedCourseTitle));
                OnPropertyChanged(nameof(SelectedCourseMeta));
            }
        }
    }

    public PersonalAssessment? SelectedPersonalAssessment
    {
        get => _selectedPersonalAssessment;
        private set
        {
            if (SetProperty(ref _selectedPersonalAssessment, value))
            {
                OnPropertyChanged(nameof(SelectedPersonalAssessmentTitle));
                OnPropertyChanged(nameof(SelectedPersonalAssessmentMeta));
                OnPropertyChanged(nameof(SelectedPersonalAssessmentSubmission));
            }
        }
    }

    public string CurrentUserName => CurrentUser?.FullName ?? "VirgoHub Student";

    public string CurrentUserMeta => CurrentUser is null
        ? string.Empty
        : IsPersonalMode
            ? (CurrentUser.Role == UserRole.Lecturer ? "Personal Lecturer" : "Personal Learner")
            : $"{CurrentUser.Department} | {CurrentUser.Level} Level | {CurrentUser.MatricNumber}";

    public string CurrentUserHostel => CurrentUser is null
        ? string.Empty
        : IsPersonalMode
            ? $"Email: {CurrentUser.Email}"
            : $"Hostel: {CurrentUser.Hostel} | Email: {CurrentUser.Email}";

    public string CurrentProfileSummary => CurrentUser is null
        ? string.Empty
        : IsPersonalMode
            ? (CurrentUser.Role == UserRole.Lecturer
                ? "Personal lecturer workspace for teaching groups, materials, and assessments."
                : "Personal learner workspace for materials, reminders, and guided study.")
            : $"{(CurrentUser.IsSportsDirector ? "Sports Director | " : string.Empty)}Faculty: {CurrentUser.Faculty} | Gender: {CurrentUser.Gender}";

    public bool IsPersonalLecturer => IsPersonalMode && CurrentUser?.Role == UserRole.Lecturer;
    public bool IsPersonalLearner => IsPersonalMode && CurrentUser?.Role == UserRole.Student;
    public string PersonalGroupSummary => PersonalGroups.Count switch
    {
        0 => IsPersonalLecturer ? "No teaching groups created yet" : "No teaching groups joined yet",
        1 => IsPersonalLecturer ? "1 teaching group live" : "1 teaching group joined",
        _ => IsPersonalLecturer ? $"{PersonalGroups.Count} teaching groups live" : $"{PersonalGroups.Count} teaching groups joined"
    };

    public string SecuritySummary => CurrentUser is null
        ? string.Empty
        : IsPersonalMode
            ? $"Protected by VirgoHub email verification. Theme: {(IsDarkMode ? "Dark" : "Light")} mode."
            : $"Protected by school email verification. Theme: {(IsDarkMode ? "Dark" : "Light")} mode.";

    public string ProfileHeader => CurrentUser is null
        ? "Profile Hub"
        : $"{CurrentUser.FullName}'s Profile Hub";

    public bool HasSportsDirectorAccess => CurrentUser?.IsSportsDirector == true;

    public string SportsDirectorSummary => HasSportsDirectorAccess
        ? $"{SportsDirectorBookings.Count(x => x.Status == SportsBookingStatus.PendingApproval)} booking request(s) awaiting your approval."
        : string.Empty;

    public bool HasNotifications => Notifications.Count > 0;
    public bool HasActivities => RecentActivities.Count > 0;

    public string NotificationPreferenceSummary => AreDeviceNotificationsEnabled
        ? IsPersonalMode
            ? "Phone alerts are enabled for material updates, reminders, and learning activity."
            : "Phone alerts are enabled for new events, elections, and campus updates."
        : "Phone alerts are paused. In-app notifications will still appear here.";

    public string NotificationPageTitle => IsPersonalMode ? "Personal Notifications" : "Notifications";

    public string NotificationPageSummary => IsPersonalMode
        ? "Choose which personal alerts VirgoHub should surface on this device, then open activity to review what has already been delivered."
        : "Control which notification pages and push alerts you want to receive on this device.";

    public string ElectionNotificationTitle => IsPersonalMode ? "Material Updates" : "Election Updates";
    public string ElectionNotificationSummary => IsPersonalMode ? "New materials, group posts, and assessment activity" : "New elections and closing reminders";

    public string EventNotificationTitle => IsPersonalMode ? "Study Reminders" : "Event Reminders";
    public string EventNotificationSummary => IsPersonalMode ? "Revision sessions, tests, and check-in reminders" : "Chapel and general campus events";

    public string ComplaintNotificationTitle => IsPersonalMode ? "Access & Approval" : "Report Status";
    public string ComplaintNotificationSummary => IsPersonalMode ? "Approvals, sign-in activity, and access changes" : "Updates on your maintenance complaints";

    public string NewsNotificationTitle => IsPersonalMode ? "VirgoHub Tips" : "News & Announcements";
    public string NewsNotificationSummary => IsPersonalMode ? "Helpful tips and product notes for your personal workspace" : "Library, sports, account, and campus broadcast updates";

    public string NotificationFilterUpdatesLabel => IsPersonalMode ? "Updates" : "Elections";
    public string NotificationFilterRemindersLabel => IsPersonalMode ? "Reminders" : "Events";
    public string NotificationFilterAccessLabel => IsPersonalMode ? "Access" : "Reports";
    public string NotificationFilterNewsLabel => IsPersonalMode ? "Tips" : "News";

    public string SupportSummary => IsPersonalMode
        ? "Tutorials, workspace tips, and VirgoHub help for your personal learning flow."
        : "Help, support, and account assistance for your school workspace.";

    public string SelectedCourseTitle => SelectedCourseEnrollment?.CourseCode ?? "Course Materials";

    public string SelectedCourseMeta => SelectedCourseEnrollment is null
        ? (IsPersonalMode ? "Select a material space to keep your resources organized." : "Select an enrolled course to keep your materials organized.")
        : IsPersonalMode
            ? $"{SelectedCourseEnrollment.CourseTitle} | Material access | {SelectedCourseEnrollment.Status}"
            : $"{SelectedCourseEnrollment.CourseTitle} | {SelectedCourseEnrollment.Department} | {SelectedCourseEnrollment.Level} Level | {SelectedCourseEnrollment.Status}";

    public Election? SelectedElection
    {
        get => _selectedElection;
        private set
        {
            if (SetProperty(ref _selectedElection, value))
            {
                OnPropertyChanged(nameof(SelectedElectionTitle));
                OnPropertyChanged(nameof(SelectedElectionMeta));
            }
        }
    }

    public string SelectedElectionTitle => SelectedElection?.Title ?? "Election Board";

    public string SelectedElectionMeta => SelectedElection is null
        ? "Open an election to view candidates, ballot details, and live results."
        : $"{SelectedElection.Position} | {SelectedElection.TotalVotes} vote(s) recorded";

    public string SelectedPersonalAssessmentTitle => SelectedPersonalAssessment?.Title ?? "Assessment";

    public string SelectedPersonalAssessmentMeta => SelectedPersonalAssessment is null
        ? "Open an assessment to review the questions, timing, and submission status."
        : $"{SelectedPersonalAssessment.Type} | {SelectedPersonalAssessment.CourseCode} | {SelectedPersonalAssessment.Questions.Count} question(s)";

    public PersonalAssessmentSubmission? SelectedPersonalAssessmentSubmission => SelectedPersonalAssessment is null || CurrentUser is null
        ? null
        : PersonalAssessmentSubmissions.FirstOrDefault(x =>
            x.AssessmentId.Equals(SelectedPersonalAssessment.Id, StringComparison.OrdinalIgnoreCase) &&
            x.UserId.Equals(CurrentUser.Id, StringComparison.OrdinalIgnoreCase));

    public async Task<PlatformBootstrapResponse> LoadPlatformBootstrapAsync(string baseUrl)
    {
        _apiClient.SetBaseUrl(baseUrl);
        RememberServerBaseUrl(baseUrl);
        var response = await _apiClient.GetPlatformBootstrapAsync();
        Replace(SchoolDirectory, response.Schools);
        return response;
    }

    public async Task<SendOtpResult> SendCodeAsync(string baseUrl, string email)
    {
        _apiClient.SetBaseUrl(baseUrl);
        return await _apiClient.SendCodeAsync(email);
    }

    public async Task<SendOtpResult> SendCodeAsync(string baseUrl, string email, AppUseMode mode, string tenantId, UserRole? roleHint)
    {
        _apiClient.SetBaseUrl(baseUrl);
        RememberServerBaseUrl(baseUrl);
        CurrentMode = mode;
        var result = await _apiClient.SendCodeAsync(email, mode, tenantId, roleHint);

        if (!string.IsNullOrWhiteSpace(result.ResolvedTenantId))
        {
            RememberSelectedSchoolTenant(result.ResolvedTenantId);
            CurrentTenant = new TenantProfile
            {
                Id = result.ResolvedTenantId,
                Name = result.ResolvedTenantName,
                Type = mode == AppUseMode.Personal ? TenantType.Personal : TenantType.School
            };
        }

        return result;
    }

    public async Task SignInAsync(string baseUrl, string email, string code)
    {
        _apiClient.SetBaseUrl(baseUrl);
        var result = await _apiClient.VerifyCodeAsync(email, code);
        CurrentUser = result.User;
        IsDarkMode = false;
        IsAuthenticated = true;
        await RefreshAsync(suppressDeviceNotifications: true);
    }

    public async Task SignInAsync(string baseUrl, string email, string code, AppUseMode mode, string tenantId, UserRole? roleHint)
    {
        _apiClient.SetBaseUrl(baseUrl);
        RememberServerBaseUrl(baseUrl);
        CurrentMode = mode;
        if (mode == AppUseMode.School && !string.IsNullOrWhiteSpace(tenantId))
        {
            RememberSelectedSchoolTenant(tenantId);
        }

        var result = await _apiClient.VerifyCodeAsync(email, code, mode, tenantId, roleHint);
        CurrentUser = result.User;
        IsDarkMode = false;
        IsAuthenticated = true;
        await RefreshAsync(suppressDeviceNotifications: true);
    }

    public async Task RefreshAsync(bool suppressDeviceNotifications = false)
    {
        var dashboard = await _apiClient.GetDashboardAsync();
        CurrentUser = dashboard.User;
        CurrentTenant = dashboard.Tenant;
        CurrentMode = dashboard.Mode;

        Replace(UpcomingEvents, dashboard.UpcomingEvents);
        Replace(Facilities, dashboard.Facilities);
        Replace(MyBookings, dashboard.MyBookings);
        Replace(SportsDirectorBookings, dashboard.SportsDirectorBookings);
        Replace(MyComplaints, dashboard.MyComplaints);
        Replace(ActiveElections, dashboard.ActiveElections);
        Replace(CourseMaterials, dashboard.CourseMaterials);
        Replace(MyCourseEnrollments, dashboard.MyCourseEnrollments);
        Replace(PersonalGroups, dashboard.PersonalGroups);
        Replace(PersonalGroupPosts, dashboard.PersonalGroupPosts);
        Replace(PersonalAssessments, dashboard.PersonalAssessments);
        Replace(PersonalAssessmentSubmissions, dashboard.PersonalAssessmentSubmissions);
        Replace(PersonalAssignments, dashboard.PersonalAssignments);
        Replace(PersonalAssignmentSubmissions, dashboard.PersonalAssignmentSubmissions);
        Replace(Notifications, dashboard.Notifications);
        Replace(RecentActivities, dashboard.RecentActivities);

        if (SelectedCourseEnrollment is not null)
        {
            SelectedCourseEnrollment = MyCourseEnrollments.FirstOrDefault(x => x.Id == SelectedCourseEnrollment.Id);
        }

        if (SelectedElection is not null)
        {
            SelectedElection = ActiveElections.FirstOrDefault(x => x.Id == SelectedElection.Id);
        }

        if (SelectedPersonalAssessment is not null)
        {
            SelectedPersonalAssessment = PersonalAssessments.FirstOrDefault(x => x.Id == SelectedPersonalAssessment.Id);
        }

        if (suppressDeviceNotifications || !AreDeviceNotificationsEnabled)
        {
            MarkNotificationsAsDelivered(dashboard.Notifications.Select(x => x.Id));
        }
        else
        {
            await AnnounceNewNotificationsAsync(dashboard.Notifications);
        }

        OnPropertyChanged(nameof(HasNotifications));
        OnPropertyChanged(nameof(HasActivities));
        OnPropertyChanged(nameof(SportsDirectorSummary));
        OnPropertyChanged(nameof(IsPersonalLecturer));
        OnPropertyChanged(nameof(IsPersonalLearner));
        OnPropertyChanged(nameof(PersonalGroupSummary));
        OnPropertyChanged(nameof(SelectedPersonalAssessmentSubmission));
    }

    public async Task CreateBookingAsync(CreateBookingRequest request)
    {
        await _apiClient.CreateBookingAsync(request);
        await RefreshAsync();
    }

    public async Task ApproveBookingAsync(string bookingId)
    {
        await _apiClient.ApproveBookingAsync(bookingId);
        await RefreshAsync();
    }

    public async Task CreateComplaintAsync(CreateComplaintRequest request)
    {
        await _apiClient.CreateComplaintAsync(request);
        await RefreshAsync();
    }

    public async Task EnrollCourseAsync(CreateCourseEnrollmentRequest request)
    {
        await _apiClient.EnrollCourseAsync(request);
        await RefreshAsync();
        SelectedCourseEnrollment = MyCourseEnrollments
            .FirstOrDefault(x => string.Equals(x.CourseCode, request.CourseCode.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public async Task CreatePersonalTeachingGroupAsync(CreatePersonalTeachingGroupRequest request)
    {
        await _apiClient.CreatePersonalTeachingGroupAsync(request);
        await RefreshAsync();
    }

    public async Task JoinPersonalTeachingGroupAsync(JoinPersonalTeachingGroupRequest request)
    {
        await _apiClient.JoinPersonalTeachingGroupAsync(request);
        await RefreshAsync();
    }

    public async Task CreatePersonalGroupPostAsync(CreatePersonalGroupPostRequest request)
    {
        await _apiClient.CreatePersonalGroupPostAsync(request);
        await RefreshAsync();
    }

    public async Task CreatePersonalMaterialAsync(CreatePersonalMaterialRequest request)
    {
        await _apiClient.CreatePersonalMaterialAsync(request);
        await RefreshAsync();
    }

    public async Task CreatePersonalAssignmentAsync(CreatePersonalAssignmentRequest request)
    {
        await _apiClient.CreatePersonalAssignmentAsync(request);
        await RefreshAsync();
    }

    public async Task SubmitPersonalAssignmentAsync(SubmitPersonalAssignmentRequest request)
    {
        await _apiClient.SubmitPersonalAssignmentAsync(request);
        await RefreshAsync();
    }

    public async Task CreatePersonalAssessmentAsync(CreatePersonalAssessmentRequest request)
    {
        await _apiClient.CreatePersonalAssessmentAsync(request);
        await RefreshAsync();
    }

    public async Task SubmitPersonalAssessmentAsync(SubmitPersonalAssessmentRequest request)
    {
        await _apiClient.SubmitPersonalAssessmentAsync(request);
        await RefreshAsync();
    }

    public async Task CastVoteAsync(CastVoteRequest request)
    {
        await _apiClient.CastVoteAsync(request);
        await RefreshAsync();
    }

    public IReadOnlyList<CourseMaterial> GetMaterialsForCourse(string? courseCode)
    {
        if (string.IsNullOrWhiteSpace(courseCode))
        {
            return [];
        }

        return CourseMaterials
            .Where(x => string.Equals(x.CourseCode, courseCode.Trim(), StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.Title)
            .ToList();
    }

    public IReadOnlyList<PersonalAssessment> GetAssessmentsForCourse(string? courseCode)
    {
        if (string.IsNullOrWhiteSpace(courseCode))
        {
            return [];
        }

        return PersonalAssessments
            .Where(x => string.Equals(x.CourseCode, courseCode.Trim(), StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.DueAt)
            .ToList();
    }

    public IReadOnlyList<PersonalAssessmentSubmission> GetSubmissionsForAssessment(string? assessmentId)
    {
        if (string.IsNullOrWhiteSpace(assessmentId))
        {
            return [];
        }

        return PersonalAssessmentSubmissions
            .Where(x => x.AssessmentId.Equals(assessmentId.Trim(), StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.SubmittedAt)
            .ToList();
    }

    public IReadOnlyList<PersonalGroupPost> GetPostsForGroup(string? groupId)
    {
        if (string.IsNullOrWhiteSpace(groupId))
        {
            return [];
        }

        return PersonalGroupPosts
            .Where(x => x.GroupId.Equals(groupId.Trim(), StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.CreatedAt)
            .ToList();
    }

    public IReadOnlyList<PersonalAssignment> GetAssignmentsForGroup(string? groupId)
    {
        if (string.IsNullOrWhiteSpace(groupId))
        {
            return [];
        }

        return PersonalAssignments
            .Where(x => x.GroupId.Equals(groupId.Trim(), StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.DueAt)
            .ToList();
    }

    public PersonalAssignmentSubmission? GetSubmissionForAssignment(string? assignmentId)
    {
        if (string.IsNullOrWhiteSpace(assignmentId) || CurrentUser is null)
        {
            return null;
        }

        return PersonalAssignmentSubmissions.FirstOrDefault(x =>
            x.AssignmentId.Equals(assignmentId.Trim(), StringComparison.OrdinalIgnoreCase) &&
            x.StudentUserId.Equals(CurrentUser.Id, StringComparison.OrdinalIgnoreCase));
    }

    public void SelectCourse(CourseEnrollment? enrollment)
    {
        SelectedCourseEnrollment = enrollment;
    }

    public void SelectElection(Election? election)
    {
        SelectedElection = election;
    }

    public void SelectPersonalAssessment(PersonalAssessment? assessment)
    {
        SelectedPersonalAssessment = assessment;
    }

    public async Task<bool> SetDeviceNotificationsEnabledAsync(bool enabled)
    {
        if (!enabled)
        {
            AreDeviceNotificationsEnabled = false;
            MarkNotificationsAsDelivered(Notifications.Select(x => x.Id));
            return true;
        }

        var granted = await _deviceNotificationService.EnsurePermissionAsync();
        if (!granted)
        {
            AreDeviceNotificationsEnabled = false;
            return false;
        }

        AreDeviceNotificationsEnabled = true;
        MarkNotificationsAsDelivered(Notifications.Select(x => x.Id));
        return true;
    }

    public async Task<bool> SaveNotificationPreferencesAsync()
    {
        if (!ReceiveElectionNotifications &&
            !ReceiveEventNotifications &&
            !ReceiveComplaintNotifications &&
            !ReceiveNewsNotifications)
        {
            AreDeviceNotificationsEnabled = false;
            return true;
        }

        return await SetDeviceNotificationsEnabledAsync(true);
    }

    public IReadOnlyList<AppNotification> GetNotificationsForChannel(NotificationFeedFilter channel)
    {
        return Notifications
            .Where(x => channel == NotificationFeedFilter.All || MapNotificationChannel(x, IsPersonalMode) == channel)
            .OrderByDescending(x => x.CreatedAt)
            .ToList();
    }

    public SchoolDirectoryEntry? ResolveSchoolByTenantId(string? tenantId)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return null;
        }

        return SchoolDirectory.FirstOrDefault(x => x.TenantId.Equals(tenantId.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public SchoolDirectoryEntry? ResolveSchoolByEmail(string? email)
    {
        var domain = GetEmailDomain(email);
        if (string.IsNullOrWhiteSpace(domain))
        {
            return null;
        }

        return SchoolDirectory.FirstOrDefault(x => x.Domain.Equals(domain, StringComparison.OrdinalIgnoreCase));
    }

    public SchoolDirectoryEntry? GetRememberedSchool()
    {
        var tenantId = Preferences.Default.Get(LastSelectedSchoolTenantKey, string.Empty);
        return ResolveSchoolByTenantId(tenantId);
    }

    public void RememberServerBaseUrl(string? baseUrl)
    {
        var normalized = string.IsNullOrWhiteSpace(baseUrl)
            ? VirgoHubApiClient.GetDefaultBaseUrl()
            : baseUrl.Trim().TrimEnd('/');
        Preferences.Default.Set(PreferredServerUrlKey, normalized);
    }

    public void RememberSelectedSchoolTenant(string? tenantId)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            Preferences.Default.Remove(LastSelectedSchoolTenantKey);
            return;
        }

        Preferences.Default.Set(LastSelectedSchoolTenantKey, tenantId.Trim());
    }

    public void Logout()
    {
        _apiClient.ResetSession();
        CurrentUser = null;
        CurrentTenant = null;
        SelectedCourseEnrollment = null;
        SelectedElection = null;
        SelectedPersonalAssessment = null;
        IsAuthenticated = false;
        IsDarkMode = false;
        CurrentMode = AppUseMode.School;
        _deliveredNotificationIds.Clear();
        ClearCollections();
    }

    private async Task AnnounceNewNotificationsAsync(IEnumerable<AppNotification> notifications)
    {
        var freshNotifications = notifications
            .Where(IsNotificationEnabled)
            .Where(x => !_deliveredNotificationIds.Contains(x.Id))
            .OrderBy(x => x.CreatedAt)
            .ToList();

        if (freshNotifications.Count == 0)
        {
            return;
        }

        var granted = await _deviceNotificationService.EnsurePermissionAsync();
        if (!granted)
        {
            AreDeviceNotificationsEnabled = false;
            MarkNotificationsAsDelivered(freshNotifications.Select(x => x.Id));
            return;
        }

        foreach (var notification in freshNotifications.Take(3))
        {
            await _deviceNotificationService.ShowAsync(notification);
        }

        MarkNotificationsAsDelivered(freshNotifications.Select(x => x.Id));
    }

    private void ClearCollections()
    {
        UpcomingEvents.Clear();
        Facilities.Clear();
        MyBookings.Clear();
        SportsDirectorBookings.Clear();
        MyComplaints.Clear();
        ActiveElections.Clear();
        CourseMaterials.Clear();
        MyCourseEnrollments.Clear();
        PersonalGroups.Clear();
        PersonalGroupPosts.Clear();
        PersonalAssessments.Clear();
        PersonalAssessmentSubmissions.Clear();
        PersonalAssignments.Clear();
        PersonalAssignmentSubmissions.Clear();
        Notifications.Clear();
        RecentActivities.Clear();
        OnPropertyChanged(nameof(HasNotifications));
        OnPropertyChanged(nameof(HasActivities));
        OnPropertyChanged(nameof(PersonalGroupSummary));
    }

    private void MarkNotificationsAsDelivered(IEnumerable<string> notificationIds)
    {
        var changed = false;

        foreach (var id in notificationIds.Where(x => !string.IsNullOrWhiteSpace(x)))
        {
            changed |= _deliveredNotificationIds.Add(id);
        }

        if (changed)
        {
            SaveDeliveredNotificationIds();
        }
    }

    private void LoadNotificationPreferencesForCurrentContext()
    {
        _areDeviceNotificationsEnabled = Preferences.Default.Get(BuildPreferenceKey(NotificationsEnabledKey), true);
        _receiveElectionNotifications = Preferences.Default.Get(BuildPreferenceKey(ElectionNotificationsKey), true);
        _receiveEventNotifications = Preferences.Default.Get(BuildPreferenceKey(EventNotificationsKey), true);
        _receiveComplaintNotifications = Preferences.Default.Get(BuildPreferenceKey(ComplaintNotificationsKey), true);
        _receiveNewsNotifications = Preferences.Default.Get(BuildPreferenceKey(NewsNotificationsKey), false);

        _deliveredNotificationIds.Clear();
        var raw = Preferences.Default.Get(BuildPreferenceKey(DeliveredNotificationsKey), string.Empty);
        foreach (var id in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            _deliveredNotificationIds.Add(id);
        }

        OnPropertyChanged(nameof(AreDeviceNotificationsEnabled));
        OnPropertyChanged(nameof(ReceiveElectionNotifications));
        OnPropertyChanged(nameof(ReceiveEventNotifications));
        OnPropertyChanged(nameof(ReceiveComplaintNotifications));
        OnPropertyChanged(nameof(ReceiveNewsNotifications));
        OnPropertyChanged(nameof(NotificationPreferenceSummary));
    }

    private void SaveDeliveredNotificationIds()
    {
        Preferences.Default.Set(BuildPreferenceKey(DeliveredNotificationsKey), string.Join(",", _deliveredNotificationIds.OrderBy(x => x)));
    }

    private bool IsNotificationEnabled(AppNotification notification) =>
        MapNotificationChannel(notification, IsPersonalMode) switch
        {
            NotificationFeedFilter.Elections => ReceiveElectionNotifications,
            NotificationFeedFilter.Events => ReceiveEventNotifications,
            NotificationFeedFilter.Reports => ReceiveComplaintNotifications,
            NotificationFeedFilter.News => ReceiveNewsNotifications,
            _ => true
        };

    public static NotificationFeedFilter MapNotificationChannel(AppNotification notification, bool isPersonalMode) =>
        isPersonalMode
            ? notification.Category.Trim() switch
            {
                "Library" or "Personal Groups" or "Assessment" => NotificationFeedFilter.Elections,
                "Events" => NotificationFeedFilter.Events,
                "Account" or "Security" => NotificationFeedFilter.Reports,
                _ => NotificationFeedFilter.News
            }
            : notification.Category.Trim() switch
            {
                "Voting" => NotificationFeedFilter.Elections,
                "Events" => NotificationFeedFilter.Events,
                "Complaints" => NotificationFeedFilter.Reports,
                _ => NotificationFeedFilter.News
            };

    private string BuildPreferenceKey(string baseKey)
    {
        var userScope = string.IsNullOrWhiteSpace(CurrentUser?.Id) ? "guest" : CurrentUser!.Id;
        var modeScope = IsPersonalMode ? "personal" : "school";
        return $"{baseKey}.{modeScope}.{userScope}";
    }

    public static NotificationFeedFilter MapNotificationChannel(AppNotification notification) =>
        notification.Category.Trim() switch
        {
            "Voting" => NotificationFeedFilter.Elections,
            "Events" => NotificationFeedFilter.Events,
            "Complaints" => NotificationFeedFilter.Reports,
            _ => NotificationFeedFilter.News
        };

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        target.Clear();
        foreach (var item in items)
        {
            target.Add(item);
        }
    }

    private static string GetEmailDomain(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return string.Empty;
        }

        var index = email.IndexOf('@');
        return index >= 0 && index < email.Length - 1
            ? email[(index + 1)..].Trim().ToLowerInvariant()
            : string.Empty;
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

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public enum NotificationFeedFilter
{
    All,
    Elections,
    Events,
    Reports,
    News
}
