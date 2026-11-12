using System.Runtime.CompilerServices;
using VirgoHub.Mobile.Services;
using VirgoHub.Shared;

namespace VirgoHub.Mobile;

public partial class MainPage : ContentPage
{
    private readonly StudentPortalStore _store;
    private string _apiBaseUrl;
    private string _email = string.Empty;
    private string _otpCode = string.Empty;
    private string _infoMessage = string.Empty;
    private Color _feedbackColor = Color.FromArgb("#5B78A2");
    private string? _debugCode;
    private bool _isPageBusy;
    private bool _codeRequested;
    private bool _showLaunchExperience = true;
    private bool _launchAnimationPlayed;
    private OnboardingStage _stage = OnboardingStage.Intro;
    private UserRole _personalRole = UserRole.Student;
    private SchoolDirectoryEntry? _selectedSchool;
    private string _platformHeadline = "One platform for campus life and personal learning.";
    private string _platformSummary = "Move through the school experience, or switch into a lighter personal workspace for mentoring, materials, and guided reminders.";

    public MainPage(StudentPortalStore store)
    {
        InitializeComponent();
        _store = store;
        _apiBaseUrl = store.DefaultBaseUrl;
        if (Application.Current is not null)
        {
            Application.Current.UserAppTheme = AppTheme.Light;
        }

        BindingContext = this;
    }

    public string ApiBaseUrl
    {
        get => _apiBaseUrl;
        set
        {
            if (SetProperty(ref _apiBaseUrl, value))
            {
                _store.RememberServerBaseUrl(value);
            }
        }
    }

    public string Email
    {
        get => _email;
        set
        {
            if (!SetProperty(ref _email, value))
            {
                return;
            }

            TryResolveSchoolFromEmail();
        }
    }

    public string OtpCode
    {
        get => _otpCode;
        set => SetProperty(ref _otpCode, value);
    }

    public string InfoMessage
    {
        get => _infoMessage;
        set
        {
            if (SetProperty(ref _infoMessage, value))
            {
                OnPropertyChanged(nameof(HasInfoMessage));
            }
        }
    }

    public Color FeedbackColor
    {
        get => _feedbackColor;
        set => SetProperty(ref _feedbackColor, value);
    }

    public string? DebugCode
    {
        get => _debugCode;
        set
        {
            if (SetProperty(ref _debugCode, value))
            {
                OnPropertyChanged(nameof(HasDebugCode));
                OnPropertyChanged(nameof(DebugCodeMessage));
            }
        }
    }

    public bool HasDebugCode => !string.IsNullOrWhiteSpace(DebugCode);

    public string DebugCodeMessage => string.IsNullOrWhiteSpace(DebugCode)
        ? string.Empty
        : $"Use this test code: {DebugCode}";

    public bool IsPageBusy
    {
        get => _isPageBusy;
        set => SetProperty(ref _isPageBusy, value);
    }

    public bool CodeRequested
    {
        get => _codeRequested;
        set
        {
            if (SetProperty(ref _codeRequested, value))
            {
                OnPropertyChanged(nameof(ShowCodeEntry));
            }
        }
    }

    public bool ShowCodeEntry => CodeRequested && (IsSchoolLoginStage || IsPersonalLoginStage);

    public bool HasInfoMessage => !string.IsNullOrWhiteSpace(InfoMessage);

    public bool ShowLaunchExperience
    {
        get => _showLaunchExperience;
        set => SetProperty(ref _showLaunchExperience, value);
    }

    public string PlatformHeadline
    {
        get => _platformHeadline;
        set => SetProperty(ref _platformHeadline, value);
    }

    public string PlatformSummary
    {
        get => _platformSummary;
        set => SetProperty(ref _platformSummary, value);
    }

    public OnboardingStage Stage
    {
        get => _stage;
        set
        {
            if (!SetProperty(ref _stage, value))
            {
                return;
            }

            CodeRequested = false;
            DebugCode = null;
            OtpCode = string.Empty;
            OnPropertyChanged(nameof(IsIntroStage));
            OnPropertyChanged(nameof(IsModeSelectionStage));
            OnPropertyChanged(nameof(IsSchoolLoginStage));
            OnPropertyChanged(nameof(IsPersonalRoleStage));
            OnPropertyChanged(nameof(IsPersonalLoginStage));
        }
    }

    public bool IsIntroStage => Stage == OnboardingStage.Intro;
    public bool IsModeSelectionStage => Stage == OnboardingStage.ModeSelection;
    public bool IsSchoolLoginStage => Stage == OnboardingStage.SchoolLogin;
    public bool IsPersonalRoleStage => Stage == OnboardingStage.PersonalRole;
    public bool IsPersonalLoginStage => Stage == OnboardingStage.PersonalLogin;

    public IReadOnlyList<SchoolDirectoryEntry> Schools => _store.SchoolDirectory;

    public SchoolDirectoryEntry? SelectedSchool
    {
        get => _selectedSchool;
        set
        {
            if (SetProperty(ref _selectedSchool, value))
            {
                _store.RememberSelectedSchoolTenant(value?.TenantId);
                OnPropertyChanged(nameof(SelectedSchoolTitle));
                OnPropertyChanged(nameof(SelectedSchoolHint));
                OnPropertyChanged(nameof(SelectedSchoolDescription));
                OnPropertyChanged(nameof(SelectedSchoolDomainBadge));
            }
        }
    }

    public string SelectedSchoolTitle => SelectedSchool?.Name ?? "Choose a school";

    public string SelectedSchoolHint => SelectedSchool is null
        ? "Load the school list from your VirgoHub server, then choose your school."
        : $"School login uses the {SelectedSchool.Domain} mail rule for this school.";

    public string SelectedSchoolDescription => SelectedSchool?.Description ?? "Each school keeps its own administrators, students, and workspace data inside VirgoHub.";

    public string SelectedSchoolDomainBadge => SelectedSchool is null
        ? "No school selected"
        : $"Domain: @{SelectedSchool.Domain}";

    public UserRole PersonalRole
    {
        get => _personalRole;
        set
        {
            if (SetProperty(ref _personalRole, value))
            {
                OnPropertyChanged(nameof(IsPersonalLecturer));
                OnPropertyChanged(nameof(IsPersonalStudent));
                OnPropertyChanged(nameof(PersonalLoginHint));
            }
        }
    }

    public bool IsPersonalLecturer => PersonalRole == UserRole.Lecturer;
    public bool IsPersonalStudent => PersonalRole == UserRole.Student;

    public string PersonalLoginHint => IsPersonalLecturer
        ? "Use a personal lecturer account. For the demo path, try mentor.demo@gmail.com."
        : "Use a tutoring or personal learner account. For the demo path, try learner.demo@gmail.com.";

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_launchAnimationPlayed)
        {
            return;
        }

        _launchAnimationPlayed = true;
        await RunLaunchExperienceAsync();
    }

    private void OnProceedClicked(object? sender, EventArgs e)
    {
        Stage = OnboardingStage.ModeSelection;
        SetFeedback("Choose the VirgoHub path that matches how you want to use the platform.", "#0F4BA8");
    }

    private async void OnSchoolUseClicked(object? sender, EventArgs e)
    {
        Stage = OnboardingStage.SchoolLogin;
        await LoadSchoolDirectoryAsync();
    }

    private void OnPersonalUseClicked(object? sender, EventArgs e)
    {
        Stage = OnboardingStage.PersonalRole;
        SetFeedback("Personal mode focuses on tutoring groups, guided materials, and smart reminders.", "#0F4BA8");
    }

    private void OnBackToModesClicked(object? sender, EventArgs e)
    {
        Stage = OnboardingStage.ModeSelection;
        SetFeedback(string.Empty, "#5B78A2");
    }

    private void OnBackToIntroClicked(object? sender, EventArgs e)
    {
        Stage = OnboardingStage.Intro;
        SetFeedback(string.Empty, "#5B78A2");
    }

    private void OnLecturerModeClicked(object? sender, EventArgs e)
    {
        PersonalRole = UserRole.Lecturer;
        Stage = OnboardingStage.PersonalLogin;
        SetFeedback("Personal lecturer mode is ready for materials, assessments, reminders, and group-led teaching.", "#0F4BA8");
    }

    private void OnStudentModeClicked(object? sender, EventArgs e)
    {
        PersonalRole = UserRole.Student;
        Stage = OnboardingStage.PersonalLogin;
        SetFeedback("Personal learner mode is prepared for guided materials, reminders, and focused study support.", "#0F4BA8");
    }

    private async void OnRefreshSchoolsClicked(object? sender, EventArgs e)
    {
        await LoadSchoolDirectoryAsync();
    }

    private async void OnSendCodeClicked(object? sender, EventArgs e)
    {
        await RunBusyActionAsync(async () =>
        {
            var mode = IsPersonalLoginStage ? AppUseMode.Personal : AppUseMode.School;
            var tenantId = mode == AppUseMode.School ? SelectedSchool?.TenantId ?? string.Empty : string.Empty;
            UserRole? roleHint = mode == AppUseMode.Personal ? PersonalRole : null;

            var result = await _store.SendCodeAsync(ApiBaseUrl, Email, mode, tenantId, roleHint);
            CodeRequested = true;
            DebugCode = result.DebugCode;
            SetFeedback(result.Message, "#0F4BA8");

            if (mode == AppUseMode.School &&
                SelectedSchool is null &&
                !string.IsNullOrWhiteSpace(result.ResolvedTenantId))
            {
                SelectedSchool = Schools.FirstOrDefault(x => x.TenantId == result.ResolvedTenantId);
            }
        });
    }

    private async void OnVerifyCodeClicked(object? sender, EventArgs e)
    {
        await RunBusyActionAsync(async () =>
        {
            var mode = IsPersonalLoginStage ? AppUseMode.Personal : AppUseMode.School;
            var tenantId = mode == AppUseMode.School ? SelectedSchool?.TenantId ?? string.Empty : string.Empty;
            UserRole? roleHint = mode == AppUseMode.Personal ? PersonalRole : null;

            await _store.SignInAsync(ApiBaseUrl, Email, OtpCode, mode, tenantId, roleHint);
            DebugCode = null;
            OtpCode = string.Empty;
            SetFeedback("Welcome to VirgoHub.", "#0F4BA8");
            if (Application.Current is App app)
            {
                app.ShowHomePage();
            }
        });
    }

    private async Task LoadSchoolDirectoryAsync()
    {
        await RunBusyActionAsync(async () =>
        {
            var bootstrap = await _store.LoadPlatformBootstrapAsync(ApiBaseUrl);
            PlatformHeadline = bootstrap.IntroHeadline;
            PlatformSummary = bootstrap.IntroSummary;
            SelectedSchool ??= _store.GetRememberedSchool() ?? Schools.FirstOrDefault();
            TryResolveSchoolFromEmail();
            SetFeedback(Schools.Count == 0
                ? "No schools were returned yet. You can still continue with a recognized school mail."
                : "School list loaded. Choose the school that matches your email domain.", "#0F4BA8");
        });
    }

    private void TryResolveSchoolFromEmail()
    {
        if (!IsSchoolLoginStage || Schools.Count == 0 || string.IsNullOrWhiteSpace(Email))
        {
            return;
        }

        var matchedSchool = _store.ResolveSchoolByEmail(Email);
        if (matchedSchool is not null)
        {
            SelectedSchool = matchedSchool;
        }
    }

    private async Task RunBusyActionAsync(Func<Task> action)
    {
        if (IsPageBusy)
        {
            return;
        }

        try
        {
            IsPageBusy = true;
            await action();
        }
        catch (Exception ex)
        {
            SetFeedback(FormatUserFacingError(ex.Message), "#B91C3A");
        }
        finally
        {
            IsPageBusy = false;
        }
    }

    private void SetFeedback(string message, string colorHex)
    {
        InfoMessage = message;
        FeedbackColor = Color.FromArgb(colorHex);
    }

    private async Task RunLaunchExperienceAsync()
    {
        try
        {
            LoginLogo.Scale = 0.9;
            LaunchLogo.Scale = 0.6;
            LaunchLogo.Rotation = -14;
            LaunchLogoShell.Scale = 0.82;
            LaunchLogoShell.Rotation = -6;
            LaunchLogoShell.TranslationY = 16;
            LaunchHalo.Scale = 0.76;
            LaunchHalo.Opacity = 0.05;
            LaunchHaloInner.Scale = 0.84;
            LaunchHaloInner.Opacity = 0.16;
            LaunchOrbitLeft.Scale = 0.4;
            LaunchOrbitLeft.Opacity = 0;
            LaunchOrbitRight.Scale = 0.4;
            LaunchOrbitRight.Opacity = 0;
            LaunchOrbitLeft.TranslationX = 22;
            LaunchOrbitLeft.TranslationY = -34;
            LaunchOrbitRight.TranslationX = -18;
            LaunchOrbitRight.TranslationY = 36;
            LaunchCaption.Opacity = 0;
            LaunchCaption.TranslationY = 12;
            LaunchTrack.Opacity = 0;
            LaunchTrack.ScaleX = 0.88;
            LaunchShuttle.TranslationX = -78;
            LaunchShuttle.ScaleX = 0.88;
            LaunchShuttle.Opacity = 0.82;

            await Task.WhenAll(
                LoginLogo.ScaleTo(1, 440, Easing.CubicOut),
                LaunchLogo.ScaleTo(1, 620, Easing.SpringOut),
                LaunchLogo.RotateTo(0, 620, Easing.CubicOut),
                LaunchLogoShell.ScaleTo(1, 600, Easing.SpringOut),
                LaunchLogoShell.RotateTo(0, 620, Easing.CubicOut),
                LaunchLogoShell.TranslateTo(0, 0, 580, Easing.CubicOut),
                LaunchHalo.ScaleTo(1.1, 760, Easing.CubicOut),
                LaunchHalo.FadeTo(0.22, 700, Easing.CubicOut),
                LaunchHaloInner.ScaleTo(1, 640, Easing.CubicOut),
                LaunchHaloInner.FadeTo(0.68, 620, Easing.CubicOut),
                LaunchOrbitLeft.ScaleTo(0.92, 620, Easing.SpringOut),
                LaunchOrbitLeft.FadeTo(0.72, 460, Easing.CubicOut),
                LaunchOrbitLeft.TranslateTo(-18, -54, 700, Easing.CubicInOut),
                LaunchOrbitRight.ScaleTo(0.92, 660, Easing.SpringOut),
                LaunchOrbitRight.FadeTo(0.82, 500, Easing.CubicOut),
                LaunchOrbitRight.TranslateTo(22, 56, 740, Easing.CubicInOut),
                LaunchCaption.FadeTo(1, 380, Easing.CubicOut),
                LaunchCaption.TranslateTo(0, 0, 440, Easing.CubicOut),
                LaunchTrack.FadeTo(1, 360, Easing.CubicOut),
                LaunchTrack.ScaleTo(1, 460, Easing.CubicOut),
                AnimateShuttleAsync());

            await Task.WhenAll(
                LaunchHalo.ScaleTo(1.18, 380, Easing.CubicInOut),
                LaunchHalo.FadeTo(0.12, 380, Easing.CubicInOut),
                LaunchHaloInner.ScaleTo(1.04, 320, Easing.CubicInOut),
                LaunchOrbitLeft.TranslateTo(-6, -34, 340, Easing.CubicOut),
                LaunchOrbitRight.TranslateTo(8, 38, 340, Easing.CubicOut),
                LaunchLogoShell.ScaleTo(1.02, 260, Easing.CubicOut));

            await Task.WhenAll(
                LaunchLogoShell.ScaleTo(1, 220, Easing.CubicInOut),
                LaunchCaption.FadeTo(0.94, 220, Easing.CubicInOut));

            await Task.Delay(140);
        }
        finally
        {
            ShowLaunchExperience = false;
        }
    }

    private async Task AnimateShuttleAsync()
    {
        await LaunchShuttle.TranslateTo(84, 0, 380, Easing.CubicInOut);
        await LaunchShuttle.ScaleXTo(1.06, 140, Easing.CubicOut);
        await LaunchShuttle.ScaleXTo(0.98, 120, Easing.CubicIn);
        await LaunchShuttle.TranslateTo(-8, 0, 220, Easing.SinInOut);
        await LaunchShuttle.TranslateTo(92, 0, 260, Easing.CubicInOut);
        await LaunchShuttle.TranslateTo(0, 0, 220, Easing.CubicOut);
        await Task.WhenAll(
            LaunchShuttle.ScaleXTo(1, 120, Easing.CubicOut),
            LaunchShuttle.FadeTo(0.92, 120, Easing.CubicOut));
    }

    private static string FormatUserFacingError(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return "Something went wrong. Please try again.";
        }

        if (message.Contains("net_http_", StringComparison.OrdinalIgnoreCase))
        {
            return "VirgoHub could not complete the request. Please confirm the server address and try again.";
        }

        if (message.Contains("Properties can only be modified before sending the first request", StringComparison.OrdinalIgnoreCase))
        {
            return "VirgoHub refreshed its connection settings. Please try the request again.";
        }

        return message;
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

public enum OnboardingStage
{
    Intro,
    ModeSelection,
    SchoolLogin,
    PersonalRole,
    PersonalLogin
}
