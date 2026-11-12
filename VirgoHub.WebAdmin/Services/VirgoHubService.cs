using System.Globalization;
using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Options;
using VirgoHub.Shared;

namespace VirgoHub.WebAdmin.Services;

public sealed class VirgoHubService
{
    private const string DefaultSchoolTenantSlug = "virgohub-campus";
    private const string DefaultPersonalTenantSlug = "virgohub-personal";
    private const string DefaultSuperAdminPassword = "VirgoHub@2026";
    private const string DefaultLecturerPassword = "Lecturer@2026";
    private const string DefaultMalePorterPassword = "PorterMale@2026";
    private const string DefaultFemalePorterPassword = "PorterFemale@2026";

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly ILogger<VirgoHubService> _logger;
    private readonly IWebHostEnvironment _environment;
    private readonly VirgoHubOptions _options;
    private readonly MailSettings _mailSettings;
    private readonly string _statePath;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private AppState _state = new();

    public VirgoHubService(
        IWebHostEnvironment environment,
        IOptions<VirgoHubOptions> options,
        IOptions<MailSettings> mailSettings,
        ILogger<VirgoHubService> logger)
    {
        _environment = environment;
        _logger = logger;
        _options = options.Value;
        _mailSettings = mailSettings.Value;
        _statePath = Path.Combine(environment.ContentRootPath, "App_Data", "virgohub-state.json");
        Directory.CreateDirectory(Path.GetDirectoryName(_statePath)!);
        _state = LoadState();
    }

    public string SchoolEmailDomain => _options.SchoolEmailDomain;
    public string PlatformName => _options.PlatformName;

    private TenantProfile? DefaultSchoolTenant => _state.Tenants.FirstOrDefault(x => x.IsActive && x.Type == TenantType.School);
    private TenantProfile? DefaultPersonalTenant => _state.Tenants.FirstOrDefault(x => x.IsActive && x.Type == TenantType.Personal);
    private string DefaultSchoolTenantId => DefaultSchoolTenant?.Id ?? string.Empty;
    private string DefaultPersonalTenantId => DefaultPersonalTenant?.Id ?? string.Empty;

    public PlatformBootstrapResponse GetPlatformBootstrap() =>
        new()
        {
            PlatformName = PlatformName,
            IntroHeadline = "Choose how VirgoHub should work for you.",
            IntroSummary = "Use the school path for campus services, or the personal path for tutoring, materials, and guided study reminders.",
            Schools = GetSchoolDirectory()
        };

    public IReadOnlyList<SchoolDirectoryEntry> GetSchoolDirectory() =>
        _state.Tenants
            .Where(x => x.IsActive && x.Type == TenantType.School)
            .OrderBy(x => x.Name)
            .Select(x => new SchoolDirectoryEntry
            {
                TenantId = x.Id,
                Name = x.Name,
                Domain = x.PrimaryDomain,
                Description = x.Description,
                AccentColor = x.AccentColor
            })
            .ToList();

    public UserProfile? GetUserById(string userId) =>
        _state.Users.FirstOrDefault(x => x.Id == userId && x.IsActive);

    public AdminSummary GetSummary(string? actorUserId = null)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var tenantId = ResolveTenantIdForSchoolScope(actorUserId);

        return new AdminSummary
        {
            TotalUsers = _state.Users.Count(x => x.IsActive && IsTenantMatch(x.TenantId, tenantId)),
            StudentCount = _state.Users.Count(x => x.IsActive && x.Role == UserRole.Student && IsTenantMatch(x.TenantId, tenantId)),
            AdminCount = _state.Users.Count(x => x.IsActive && x.Role == UserRole.Admin && IsTenantMatch(x.TenantId, tenantId)),
            PorterCount = _state.Users.Count(x => x.IsActive && x.Role == UserRole.Porter && IsTenantMatch(x.TenantId, tenantId)),
            ActiveEvents = _state.Events.Count(x => x.StartsAt >= DateTimeOffset.UtcNow && IsTenantMatch(x.TenantId, tenantId)),
            PendingComplaints = _state.Complaints.Count(x => x.Status != ComplaintStatus.Resolved && IsTenantMatch(x.TenantId, tenantId)),
            ActiveElections = _state.Elections.Count(x => x.IsActive && IsTenantMatch(x.TenantId, tenantId)),
            MaterialsInHub = _state.Materials.Count(x => IsTenantMatch(x.TenantId, tenantId)),
            EnrolledCourses = _state.Enrollments.Count(x => IsTenantMatch(x.TenantId, tenantId)),
            TodaysBookings = _state.Bookings.Count(x => x.BookingDate == today && IsTenantMatch(x.TenantId, tenantId))
        };
    }

    public IReadOnlyList<UserProfile> GetUsers(string? actorUserId = null)
    {
        var tenantId = ResolveTenantIdForSchoolScope(actorUserId);
        return _state.Users
            .Where(x => x.IsActive && IsTenantMatch(x.TenantId, tenantId))
            .OrderByDescending(x => x.CreatedAt)
            .ToList();
    }

    public IReadOnlyList<UserProfile> GetPorters(string? actorUserId = null)
    {
        var tenantId = ResolveTenantIdForSchoolScope(actorUserId);
        return _state.Users
            .Where(x => x.IsActive && x.Role == UserRole.Porter && IsTenantMatch(x.TenantId, tenantId))
            .OrderBy(x => x.FullName)
            .ToList();
    }

    public IReadOnlyList<UserProfile> GetSportsDirectors(string? actorUserId = null)
    {
        var tenantId = ResolveTenantIdForSchoolScope(actorUserId);
        return _state.Users
            .Where(x => x.IsActive && x.Role == UserRole.Student && x.IsSportsDirector && IsTenantMatch(x.TenantId, tenantId))
            .OrderBy(x => x.FullName)
            .ToList();
    }

    public IReadOnlyList<UserProfile> GetPortalAdmins(string? actorUserId = null)
    {
        var tenantId = ResolveTenantIdForSchoolScope(actorUserId);
        return _state.Users
            .Where(x => x.IsActive && x.Role == UserRole.Admin && IsTenantMatch(x.TenantId, tenantId))
            .OrderBy(x => x.FullName)
            .ToList();
    }

    public IReadOnlyList<UserProfile> GetLecturers(string? actorUserId = null)
    {
        var tenantId = ResolveTenantIdForSchoolScope(actorUserId);
        return _state.Users
            .Where(x => x.IsActive && x.Role == UserRole.Lecturer && IsTenantMatch(x.TenantId, tenantId))
            .OrderBy(x => x.Faculty)
            .ThenBy(x => x.Department)
            .ThenBy(x => x.FullName)
            .ToList();
    }

    public UserProfile? GetSuperAdmin(string? tenantId = null) =>
        _state.Users.FirstOrDefault(x => x.IsActive && x.Role == UserRole.SuperAdmin && IsTenantMatch(x.TenantId, tenantId ?? DefaultSchoolTenantId));

    public IReadOnlyList<TenantProfile> GetSchoolTenants() =>
        _state.Tenants
            .Where(x => x.IsActive && x.Type == TenantType.School)
            .OrderBy(x => x.Name)
            .ToList();

    public IReadOnlyList<CampusEvent> GetEvents(string? actorUserId = null)
    {
        var tenantId = ResolveTenantIdForSchoolScope(actorUserId);
        return _state.Events
            .Where(x => IsTenantMatch(x.TenantId, tenantId))
            .OrderBy(x => x.StartsAt)
            .ToList();
    }

    public IReadOnlyList<SportsFacility> GetFacilities(string? actorUserId = null)
    {
        var tenantId = ResolveTenantIdForSchoolScope(actorUserId);
        return _state.Facilities
            .Where(x => IsTenantMatch(x.TenantId, tenantId))
            .OrderBy(x => x.Name)
            .ToList();
    }

    public IReadOnlyList<SportsBooking> GetBookings(string? actorUserId = null)
    {
        var tenantId = ResolveTenantIdForSchoolScope(actorUserId);
        return _state.Bookings
            .Where(x => IsTenantMatch(x.TenantId, tenantId))
            .OrderByDescending(x => x.BookingDate)
            .ThenBy(x => x.SlotLabel)
            .ToList();
    }

    public IReadOnlyList<ComplaintTicket> GetComplaints(string? actorUserId = null)
    {
        var tenantId = ResolveTenantIdForSchoolScope(actorUserId);
        return _state.Complaints
            .Where(x => IsTenantMatch(x.TenantId, tenantId))
            .OrderByDescending(x => x.UpdatedAt)
            .ToList();
    }

    public IReadOnlyList<Election> GetElections(string? actorUserId = null)
    {
        var tenantId = ResolveTenantIdForSchoolScope(actorUserId);
        return _state.Elections
            .Where(x => IsTenantMatch(x.TenantId, tenantId))
            .OrderByDescending(x => x.OpensAt)
            .Select(x => BuildElectionView(x))
            .ToList();
    }

    public IReadOnlyList<CourseMaterial> GetCourseMaterials(string? actorUserId = null)
    {
        var tenantId = ResolveTenantIdForSchoolScope(actorUserId);
        return _state.Materials
            .Where(x => IsTenantMatch(x.TenantId, tenantId))
            .OrderBy(x => x.CourseCode)
            .ThenBy(x => x.Title)
            .ToList();
    }

    public IReadOnlyList<CourseEnrollment> GetEnrollments(string? actorUserId = null)
    {
        var tenantId = ResolveTenantIdForSchoolScope(actorUserId);
        return _state.Enrollments
            .Where(x => IsTenantMatch(x.TenantId, tenantId))
            .OrderByDescending(x => x.EnrolledAt)
            .ToList();
    }

    public IReadOnlyList<AdminModule> GetAccessibleModules(UserProfile? portalUser)
    {
        if (portalUser is null)
        {
            return [];
        }

        if (portalUser.Role == UserRole.SuperAdmin)
        {
            return Enum.GetValues<AdminModule>();
        }

        if (portalUser.Role == UserRole.PlatformSuperAdmin)
        {
            return [];
        }

        if (portalUser.Role == UserRole.Admin)
        {
            return portalUser.AdminModules
                .Distinct()
                .OrderBy(x => x.ToString())
                .ToList();
        }

        if (portalUser.Role == UserRole.Lecturer)
        {
            return [AdminModule.Library];
        }

        return portalUser.Role == UserRole.Porter
            ? [AdminModule.Complaints]
            : [];
    }

    public bool CanAccessModule(string userId, AdminModule module)
    {
        var portalUser = RequirePortalUser(userId);

        if (portalUser.Role == UserRole.SuperAdmin)
        {
            return true;
        }

        if (portalUser.Role == UserRole.PlatformSuperAdmin)
        {
            return false;
        }

        if (portalUser.Role == UserRole.Admin)
        {
            return portalUser.AdminModules.Contains(module);
        }

        if (portalUser.Role == UserRole.Lecturer)
        {
            return module == AdminModule.Library;
        }

        return portalUser.Role == UserRole.Porter && module == AdminModule.Complaints;
    }

    public IReadOnlyList<ComplaintTicket> GetComplaintsForOperator(string operatorUserId)
    {
        var portalUser = RequirePortalUser(operatorUserId);
        return portalUser.Role is UserRole.SuperAdmin or UserRole.Admin
            ? GetComplaints(operatorUserId)
            : _state.Complaints
                .Where(x => IsTenantMatch(x.TenantId, portalUser.TenantId) && x.StudentGender == portalUser.Gender)
                .OrderByDescending(x => x.UpdatedAt)
                .ToList();
    }

    public IReadOnlyList<CourseMaterial> GetCourseMaterialsForPortalUser(string userId)
    {
        var actor = RequirePortalUser(userId);
        return actor.Role == UserRole.Lecturer
            ? _state.Materials
                .Where(x => x.LecturerUserId == actor.Id)
                .OrderBy(x => x.CourseCode)
                .ThenBy(x => x.Title)
                .ToList()
            : GetCourseMaterials(userId);
    }

    public IReadOnlyList<CourseEnrollment> GetEnrollmentsForPortalUser(string userId)
    {
        var actor = RequirePortalUser(userId);
        if (actor.Role != UserRole.Lecturer)
        {
            return GetEnrollments(userId);
        }

        var courseCodes = _state.Materials
            .Where(x => x.LecturerUserId == actor.Id)
            .Select(x => x.CourseCode)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return _state.Enrollments
            .Where(x => courseCodes.Contains(x.CourseCode))
            .OrderByDescending(x => x.EnrolledAt)
            .ToList();
    }

    public DashboardSnapshot GetStudentDashboard(string userId)
    {
        var user = RequireMobileUser(userId);
        var tenant = GetTenantById(user.TenantId);
        var mode = tenant?.Type == TenantType.Personal ? AppUseMode.Personal : AppUseMode.School;
        var tenantId = user.TenantId;

        return new DashboardSnapshot
        {
            User = user,
            Tenant = tenant,
            Mode = mode,
            UpcomingEvents = _state.Events
                .Where(x => IsTenantMatch(x.TenantId, tenantId))
                .Where(x => x.StartsAt >= DateTimeOffset.UtcNow.AddDays(-1))
                .Where(x => mode == AppUseMode.Personal || CanStudentAccessAudience(user, x.AudienceFaculty, x.AudienceDepartments))
                .OrderBy(x => x.StartsAt)
                .Take(8)
                .ToList(),
            Facilities = mode == AppUseMode.School
                ? _state.Facilities
                    .Where(x => IsTenantMatch(x.TenantId, tenantId))
                    .OrderBy(x => x.Name)
                    .ToList()
                : [],
            MyBookings = mode == AppUseMode.School
                ? _state.Bookings
                    .Where(x => x.UserId == userId && IsTenantMatch(x.TenantId, tenantId))
                    .OrderByDescending(x => x.BookingDate)
                    .ThenBy(x => x.SlotLabel)
                    .Take(10)
                    .ToList()
                : [],
            SportsDirectorBookings = mode == AppUseMode.School && user.IsSportsDirector
                ? _state.Bookings
                    .Where(x => IsTenantMatch(x.TenantId, tenantId))
                    .OrderBy(x => x.Status == SportsBookingStatus.PendingApproval ? 0 : 1)
                    .ThenByDescending(x => x.CreatedAt)
                    .Take(20)
                    .ToList()
                : [],
            MyComplaints = mode == AppUseMode.School
                ? _state.Complaints
                    .Where(x => x.UserId == userId && IsTenantMatch(x.TenantId, tenantId))
                    .OrderByDescending(x => x.UpdatedAt)
                    .Take(10)
                    .ToList()
                : [],
            ActiveElections = mode == AppUseMode.School && user.Role == UserRole.Student
                ? _state.Elections
                    .Where(x => x.IsActive && IsTenantMatch(x.TenantId, tenantId) && CanStudentAccessAudience(user, x.AudienceFaculty, x.AudienceDepartments))
                    .OrderBy(x => x.ClosesAt)
                    .Select(x => BuildElectionView(x, user.Id))
                    .ToList()
                : [],
            CourseMaterials = _state.Materials
                .Where(x => IsTenantMatch(x.TenantId, tenantId))
                .Where(x => mode == AppUseMode.Personal
                    ? CanUserAccessPersonalMaterial(user, x)
                    : user.Role == UserRole.Lecturer
                        ? string.Equals(x.LecturerUserId, user.Id, StringComparison.OrdinalIgnoreCase) || x.IsGeneralCourse
                        : CanStudentAccessMaterial(user, x))
                .OrderBy(x => x.CourseCode)
                .ThenBy(x => x.Title)
                .ToList(),
            MyCourseEnrollments = mode == AppUseMode.Personal
                ? BuildPersonalMaterialSpaces(user)
                : user.Role == UserRole.Student
                    ? _state.Enrollments
                        .Where(x => x.UserId == userId && IsTenantMatch(x.TenantId, tenantId))
                        .OrderByDescending(x => x.EnrolledAt)
                        .Take(10)
                        .ToList()
                    : [],
            PersonalGroups = mode == AppUseMode.Personal
                ? _state.TeachingGroups
                    .Where(x => IsTenantMatch(x.TenantId, tenantId))
                    .Where(x => string.Equals(x.LecturerUserId, user.Id, StringComparison.OrdinalIgnoreCase) ||
                                x.Members.Any(member => string.Equals(member.UserId, user.Id, StringComparison.OrdinalIgnoreCase)))
                    .OrderByDescending(x => x.CreatedAt)
                    .ToList()
                : [],
            PersonalGroupPosts = mode == AppUseMode.Personal
                ? _state.PersonalGroupPosts
                    .Where(x => IsTenantMatch(x.TenantId, tenantId))
                    .Where(x => CanUserAccessPersonalGroup(user, x.GroupId))
                    .OrderByDescending(x => x.CreatedAt)
                    .ToList()
                : [],
            PersonalAssessments = mode == AppUseMode.Personal
                ? _state.PersonalAssessments
                    .Where(x => IsTenantMatch(x.TenantId, tenantId))
                    .Where(x => CanUserAccessPersonalAssessment(user, x))
                    .OrderBy(x => x.DueAt)
                    .ThenBy(x => x.Title)
                    .ToList()
                : [],
            PersonalAssessmentSubmissions = mode == AppUseMode.Personal
                ? _state.PersonalAssessmentSubmissions
                    .Where(x => IsTenantMatch(x.TenantId, tenantId))
                    .Where(x => string.Equals(x.UserId, user.Id, StringComparison.OrdinalIgnoreCase) ||
                                (user.Role == UserRole.Lecturer &&
                                 _state.PersonalAssessments.Any(assessment =>
                                     assessment.Id == x.AssessmentId &&
                                     string.Equals(assessment.LecturerUserId, user.Id, StringComparison.OrdinalIgnoreCase))))
                    .OrderByDescending(x => x.SubmittedAt)
                    .ToList()
                : [],
            PersonalAssignments = mode == AppUseMode.Personal
                ? _state.PersonalAssignments
                    .Where(x => IsTenantMatch(x.TenantId, tenantId))
                    .Where(x => CanUserAccessPersonalGroup(user, x.GroupId))
                    .OrderBy(x => x.DueAt)
                    .ToList()
                : [],
            PersonalAssignmentSubmissions = mode == AppUseMode.Personal
                ? _state.PersonalAssignmentSubmissions
                    .Where(x => IsTenantMatch(x.TenantId, tenantId))
                    .Where(x => string.Equals(x.StudentUserId, user.Id, StringComparison.OrdinalIgnoreCase) ||
                                (user.Role == UserRole.Lecturer &&
                                 _state.PersonalAssignments.Any(assignment =>
                                     assignment.Id == x.AssignmentId &&
                                     string.Equals(assignment.LecturerUserId, user.Id, StringComparison.OrdinalIgnoreCase))))
                    .OrderByDescending(x => x.SubmittedAt)
                    .ToList()
                : [],
            Notifications = _state.Notifications
                .Where(x => x.UserId == userId && IsTenantMatch(x.TenantId, tenantId))
                .OrderByDescending(x => x.CreatedAt)
                .Take(20)
                .ToList(),
            RecentActivities = _state.Activities
                .Where(x => x.UserId == userId && IsTenantMatch(x.TenantId, tenantId))
                .OrderByDescending(x => x.CreatedAt)
                .Take(20)
                .ToList()
        };
    }

    public async Task<UserProfile> RegisterUserAsync(RegisterUserRequest request, string? actorUserId = null)
    {
        var actor = ResolveSchoolActor(actorUserId);
        ValidateRequired(request.FullName, "Full name");
        ValidateRequired(request.Email, "School email");
        ValidateRequired(request.Level, "Level");
        ValidateRequired(request.Department, "Department");
        ValidateRequired(request.MatricNumber, "Matric number");

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        EnsureSchoolEmail(normalizedEmail, actor.TenantId);

        await _gate.WaitAsync();
        try
        {
            if (_state.Users.Any(x => x.Email.Equals(normalizedEmail, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("A profile with that school email already exists.");
            }

            var user = new UserProfile
            {
                TenantId = actor.TenantId,
                TenantName = actor.TenantName,
                FullName = request.FullName.Trim(),
                Email = normalizedEmail,
                Level = request.Level.Trim(),
                Department = request.Department.Trim(),
                Faculty = request.Faculty.Trim(),
                MatricNumber = request.MatricNumber.Trim(),
                Hostel = request.Hostel.Trim(),
                Gender = request.Gender,
                Role = request.Role,
                IsSportsDirector = request.IsSportsDirector,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            };

            _state.Users.Add(user);
            AddNotification(user.Id, "Account ready", "Your VirgoHub account has been created. Use your school mail to request a login code.", "Account");
            AddActivity(user.Id, "Profile created", "Your campus profile is now active and ready for sign in.", "Account");
            await SaveStateAsync();
            return user;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<UserProfile> RegisterPorterAsync(RegisterPorterRequest request, string? actorUserId = null)
    {
        var actor = ResolveSchoolActor(actorUserId);
        ValidateRequired(request.FullName, "Porter name");
        ValidateRequired(request.StaffId, "Staff ID");
        ValidateRequired(request.Password, "Portal password");

        await _gate.WaitAsync();
        try
        {
            var staffId = request.StaffId.Trim().ToUpperInvariant();
            if (_state.Users.Any(x => x.StaffId == staffId))
            {
                throw new InvalidOperationException("That staff ID is already assigned.");
            }

            var porter = new UserProfile
            {
                TenantId = actor.TenantId,
                TenantName = actor.TenantName,
                FullName = request.FullName.Trim(),
                StaffId = staffId,
                Department = request.Department.Trim(),
                Faculty = request.Faculty.Trim(),
                Level = "Staff",
                Hostel = request.Gender == UserGender.Female ? "Female Hostels" : "Male Hostels",
                Gender = request.Gender,
                Role = UserRole.Porter,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            };

            ApplyPassword(porter, request.Password);
            _state.Users.Add(porter);
            await SaveStateAsync();
            return porter;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<UserProfile> RegisterPortalAdminAsync(RegisterPortalAdminRequest request, string? actorUserId = null)
    {
        var actor = ResolveSchoolActor(actorUserId);
        ValidateRequired(request.FullName, "Admin name");
        ValidateRequired(request.Email, "Portal email");
        ValidateRequired(request.Password, "Portal password");

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        EnsureSchoolEmail(normalizedEmail, actor.TenantId);
        var modules = NormalizeModules(request.Modules);
        if (modules.Count == 0)
        {
            throw new InvalidOperationException("Select at least one admin area.");
        }

        await _gate.WaitAsync();
        try
        {
            if (_state.Users.Any(x => x.IsActive && x.Email.Equals(normalizedEmail, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("A portal account with that email already exists.");
            }

            var admin = new UserProfile
            {
                TenantId = actor.TenantId,
                TenantName = actor.TenantName,
                FullName = request.FullName.Trim(),
                Email = normalizedEmail,
                Department = string.IsNullOrWhiteSpace(request.Department) ? "School Operations" : request.Department.Trim(),
                Faculty = string.IsNullOrWhiteSpace(request.Faculty) ? "Administration" : request.Faculty.Trim(),
                Level = "Staff",
                Hostel = "N/A",
                Gender = UserGender.Other,
                Role = UserRole.Admin,
                AdminModules = modules,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            };

            ApplyPassword(admin, request.Password);
            _state.Users.Add(admin);
            await SaveStateAsync();
            return admin;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<UserProfile> UpdatePortalAdminAsync(string userId, UpdatePortalAdminRequest request, string? actorUserId = null)
    {
        var actor = ResolveSchoolActor(actorUserId);
        ValidateRequired(request.FullName, "Admin name");
        ValidateRequired(request.Email, "Portal email");

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        EnsureSchoolEmail(normalizedEmail, actor.TenantId);
        var modules = NormalizeModules(request.Modules);
        if (modules.Count == 0)
        {
            throw new InvalidOperationException("Select at least one admin area.");
        }

        await _gate.WaitAsync();
        try
        {
            var admin = _state.Users.FirstOrDefault(x => x.Id == userId && x.IsActive && x.Role == UserRole.Admin && IsTenantMatch(x.TenantId, actor.TenantId))
                ?? throw new InvalidOperationException("Portal admin not found.");

            if (_state.Users.Any(x => x.Id != userId && x.IsActive && IsTenantMatch(x.TenantId, actor.TenantId) && x.Email.Equals(normalizedEmail, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("Another account is already using that portal email.");
            }

            admin.FullName = request.FullName.Trim();
            admin.Email = normalizedEmail;
            admin.Department = request.Department.Trim();
            admin.Faculty = request.Faculty.Trim();
            admin.AdminModules = modules;

            if (!string.IsNullOrWhiteSpace(request.Password))
            {
                ApplyPassword(admin, request.Password);
            }

            await SaveStateAsync();
            return admin;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task DeletePortalAdminAsync(string userId, string? actorUserId = null)
    {
        var actor = ResolveSchoolActor(actorUserId);
        await _gate.WaitAsync();
        try
        {
            var admin = _state.Users.FirstOrDefault(x => x.Id == userId && x.IsActive && x.Role == UserRole.Admin && IsTenantMatch(x.TenantId, actor.TenantId))
                ?? throw new InvalidOperationException("Portal admin not found.");

            admin.IsActive = false;
            await SaveStateAsync();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<UserProfile> RegisterLecturerAsync(RegisterLecturerRequest request, string? actorUserId = null)
    {
        var actor = ResolveSchoolActor(actorUserId);
        ValidateRequired(request.FullName, "Lecturer name");
        ValidateRequired(request.Email, "Virgo mail");
        ValidateRequired(request.StaffId, "Staff ID");
        ValidateRequired(request.Password, "Portal password");
        ValidateRequired(request.Faculty, "Faculty");
        ValidateRequired(request.Department, "Department");

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var normalizedStaffId = request.StaffId.Trim().ToUpperInvariant();
        EnsureSchoolEmail(normalizedEmail, actor.TenantId);

        await _gate.WaitAsync();
        try
        {
            if (_state.Users.Any(x => x.IsActive && IsTenantMatch(x.TenantId, actor.TenantId) && x.Email.Equals(normalizedEmail, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("A lecturer account with that email already exists.");
            }

            if (_state.Users.Any(x => x.IsActive && IsTenantMatch(x.TenantId, actor.TenantId) && !string.IsNullOrWhiteSpace(x.StaffId) && x.StaffId.Equals(normalizedStaffId, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("Another portal account is already using that staff ID.");
            }

            var lecturer = new UserProfile
            {
                TenantId = actor.TenantId,
                TenantName = actor.TenantName,
                FullName = request.FullName.Trim(),
                Email = normalizedEmail,
                StaffId = normalizedStaffId,
                Department = request.Department.Trim(),
                Faculty = request.Faculty.Trim(),
                Level = "Staff",
                Hostel = "N/A",
                Gender = UserGender.Other,
                Role = UserRole.Lecturer,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            };

            ApplyPassword(lecturer, request.Password);
            _state.Users.Add(lecturer);
            await SaveStateAsync();
            return lecturer;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<UserProfile> UpdateLecturerAsync(string userId, UpdateLecturerRequest request, string? actorUserId = null)
    {
        var actor = ResolveSchoolActor(actorUserId);
        ValidateRequired(request.FullName, "Lecturer name");
        ValidateRequired(request.Email, "Virgo mail");
        ValidateRequired(request.StaffId, "Staff ID");
        ValidateRequired(request.Faculty, "Faculty");
        ValidateRequired(request.Department, "Department");

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var normalizedStaffId = request.StaffId.Trim().ToUpperInvariant();
        EnsureSchoolEmail(normalizedEmail, actor.TenantId);

        await _gate.WaitAsync();
        try
        {
            var lecturer = _state.Users.FirstOrDefault(x => x.Id == userId && x.IsActive && x.Role == UserRole.Lecturer && IsTenantMatch(x.TenantId, actor.TenantId))
                ?? throw new InvalidOperationException("Lecturer account not found.");

            if (_state.Users.Any(x => x.Id != userId && x.IsActive && IsTenantMatch(x.TenantId, actor.TenantId) && x.Email.Equals(normalizedEmail, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("Another account is already using that Virgo mail.");
            }

            if (_state.Users.Any(x => x.Id != userId && x.IsActive && IsTenantMatch(x.TenantId, actor.TenantId) && !string.IsNullOrWhiteSpace(x.StaffId) && x.StaffId.Equals(normalizedStaffId, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("Another portal account is already using that staff ID.");
            }

            lecturer.FullName = request.FullName.Trim();
            lecturer.Email = normalizedEmail;
            lecturer.StaffId = normalizedStaffId;
            lecturer.Department = request.Department.Trim();
            lecturer.Faculty = request.Faculty.Trim();

            if (!string.IsNullOrWhiteSpace(request.Password))
            {
                ApplyPassword(lecturer, request.Password);
            }

            foreach (var material in _state.Materials.Where(x => x.LecturerUserId == lecturer.Id))
            {
                material.LecturerName = lecturer.FullName;
                if (!material.IsGeneralCourse)
                {
                    material.Faculty = lecturer.Faculty;
                    material.Department = lecturer.Department;
                }
            }

            await SaveStateAsync();
            return lecturer;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task DeleteLecturerAsync(string userId, string? actorUserId = null)
    {
        var actor = ResolveSchoolActor(actorUserId);
        await _gate.WaitAsync();
        try
        {
            var lecturer = _state.Users.FirstOrDefault(x => x.Id == userId && x.IsActive && x.Role == UserRole.Lecturer && IsTenantMatch(x.TenantId, actor.TenantId))
                ?? throw new InvalidOperationException("Lecturer account not found.");

            lecturer.IsActive = false;
            await SaveStateAsync();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<UserProfile> UpdateStudentAsync(string userId, UpdateUserRequest request, string? actorUserId = null)
    {
        var actor = ResolveSchoolActor(actorUserId);
        ValidateRequired(request.FullName, "Full name");
        ValidateRequired(request.Email, "School email");
        ValidateRequired(request.Level, "Level");
        ValidateRequired(request.Department, "Department");
        ValidateRequired(request.MatricNumber, "Matric number");

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        EnsureSchoolEmail(normalizedEmail, actor.TenantId);

        await _gate.WaitAsync();
        try
        {
            var user = _state.Users.FirstOrDefault(x => x.Id == userId && x.IsActive && x.Role == UserRole.Student && IsTenantMatch(x.TenantId, actor.TenantId))
                ?? throw new InvalidOperationException("Student profile not found.");

            if (_state.Users.Any(x => x.Id != userId && x.IsActive && IsTenantMatch(x.TenantId, actor.TenantId) && x.Email.Equals(normalizedEmail, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("Another profile is already using that school email.");
            }

            user.FullName = request.FullName.Trim();
            user.Email = normalizedEmail;
            user.Level = request.Level.Trim();
            user.Department = request.Department.Trim();
            user.Faculty = request.Faculty.Trim();
            user.MatricNumber = request.MatricNumber.Trim();
            user.Hostel = request.Hostel.Trim();
            user.Gender = request.Gender;
            user.IsSportsDirector = request.IsSportsDirector;

            await SaveStateAsync();
            return user;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task DeleteStudentAsync(string userId, string? actorUserId = null)
    {
        var actor = ResolveSchoolActor(actorUserId);
        await _gate.WaitAsync();
        try
        {
            var user = _state.Users.FirstOrDefault(x => x.Id == userId && x.IsActive && x.Role == UserRole.Student && IsTenantMatch(x.TenantId, actor.TenantId))
                ?? throw new InvalidOperationException("Student profile not found.");

            user.IsActive = false;
            _state.Sessions.RemoveAll(x => x.UserId == userId);
            _state.OtpChallenges.RemoveAll(x => x.Email.Equals(user.Email, StringComparison.OrdinalIgnoreCase));
            await SaveStateAsync();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<TenantProfile> CreateSchoolTenantAsync(string actorUserId, CreateSchoolTenantRequest request)
    {
        RequirePlatformSuperAdmin(actorUserId);
        ValidateRequired(request.SchoolName, "School name");
        ValidateRequired(request.PrimaryDomain, "Primary domain");
        ValidateRequired(request.SuperAdminName, "Super admin name");
        ValidateRequired(request.SuperAdminEmail, "Super admin email");
        ValidateRequired(request.SuperAdminPassword, "Super admin password");

        var schoolName = request.SchoolName.Trim();
        var primaryDomain = NormalizeDomain(request.PrimaryDomain);
        var allowedDomains = NormalizeSchoolDomains(primaryDomain, request.AdditionalDomainsText);
        var superAdminEmail = request.SuperAdminEmail.Trim().ToLowerInvariant();
        EnsureSchoolEmail(superAdminEmail, null, allowedDomains);

        await _gate.WaitAsync();
        try
        {
            if (_state.Tenants.Any(x =>
                    x.IsActive &&
                    x.Type == TenantType.School &&
                    (x.Name.Equals(schoolName, StringComparison.OrdinalIgnoreCase) ||
                     x.AllowedDomains.Any(domain => allowedDomains.Contains(domain, StringComparer.OrdinalIgnoreCase)))))
            {
                throw new InvalidOperationException("A school tenant with that name or domain already exists.");
            }

            if (_state.Users.Any(x => x.IsActive && !string.IsNullOrWhiteSpace(x.Email) && x.Email.Equals(superAdminEmail, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("A portal account with that super admin email already exists.");
            }

            var tenant = new TenantProfile
            {
                Name = schoolName,
                Slug = BuildTenantSlug(schoolName),
                Type = TenantType.School,
                PrimaryDomain = primaryDomain,
                AllowedDomains = allowedDomains,
                Description = request.Description.Trim(),
                AccentColor = NormalizeAccentColor(request.AccentColor)
            };

            var superAdmin = new UserProfile
            {
                TenantId = tenant.Id,
                TenantName = tenant.Name,
                FullName = request.SuperAdminName.Trim(),
                Email = superAdminEmail,
                StaffId = $"SCH-{tenant.Slug[..Math.Min(6, tenant.Slug.Length)].ToUpperInvariant()}-ADM",
                Department = "School Operations",
                Faculty = "Administration",
                Level = "Staff",
                Hostel = "N/A",
                Gender = UserGender.Other,
                Role = UserRole.SuperAdmin,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            };

            ApplyPassword(superAdmin, request.SuperAdminPassword);
            _state.Tenants.Add(tenant);
            _state.Users.Add(superAdmin);
            await SaveStateAsync();
            return tenant;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<TenantProfile> UpdateSchoolTenantAsync(string actorUserId, string tenantId, UpdateSchoolTenantRequest request)
    {
        RequirePlatformSuperAdmin(actorUserId);
        ValidateRequired(request.SchoolName, "School name");
        ValidateRequired(request.PrimaryDomain, "Primary domain");

        var schoolName = request.SchoolName.Trim();
        var primaryDomain = NormalizeDomain(request.PrimaryDomain);
        var allowedDomains = NormalizeSchoolDomains(primaryDomain, request.AdditionalDomainsText);

        await _gate.WaitAsync();
        try
        {
            var tenant = _state.Tenants.FirstOrDefault(x =>
                x.Id.Equals(tenantId, StringComparison.OrdinalIgnoreCase) &&
                x.Type == TenantType.School)
                ?? throw new InvalidOperationException("School tenant not found.");

            if (_state.Tenants.Any(x =>
                    x.Id != tenant.Id &&
                    x.IsActive &&
                    x.Type == TenantType.School &&
                    (x.Name.Equals(schoolName, StringComparison.OrdinalIgnoreCase) ||
                     x.AllowedDomains.Any(domain => allowedDomains.Contains(domain, StringComparer.OrdinalIgnoreCase)))))
            {
                throw new InvalidOperationException("Another school tenant is already using that name or domain.");
            }

            tenant.Name = schoolName;
            tenant.Slug = BuildTenantSlug(schoolName);
            tenant.PrimaryDomain = primaryDomain;
            tenant.AllowedDomains = allowedDomains;
            tenant.Description = request.Description.Trim();
            tenant.AccentColor = NormalizeAccentColor(request.AccentColor);
            tenant.IsActive = request.IsActive;

            foreach (var user in _state.Users.Where(x => IsTenantMatch(x.TenantId, tenant.Id)))
            {
                user.TenantName = tenant.Name;
            }

            await SaveStateAsync();
            return tenant;
        }
        finally
        {
            _gate.Release();
        }
    }

    public PortalLoginResult AuthenticatePortalUser(PortalLoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Identifier))
        {
            return new PortalLoginResult
            {
                Success = false,
                Message = "Enter the portal email or porter staff ID."
            };
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            return new PortalLoginResult
            {
                Success = false,
                Message = "Enter the portal password."
            };
        }

        var identifier = request.Identifier.Trim();
        var user = identifier.Contains('@')
            ? _state.Users.FirstOrDefault(x =>
                x.IsActive &&
                x.Role is UserRole.PlatformSuperAdmin or UserRole.SuperAdmin or UserRole.Admin or UserRole.Lecturer &&
                x.Email.Equals(identifier, StringComparison.OrdinalIgnoreCase))
            : _state.Users.FirstOrDefault(x =>
                x.IsActive &&
                x.Role is UserRole.Porter or UserRole.Lecturer &&
                x.StaffId.Equals(identifier, StringComparison.OrdinalIgnoreCase));

        if (user is null || string.IsNullOrWhiteSpace(user.PortalPasswordHash))
        {
            return new PortalLoginResult
            {
                Success = false,
                Message = "That portal account could not be found."
            };
        }

        if (!VerifyPassword(request.Password, user.PortalPasswordHash, user.PortalPasswordSalt))
        {
            return new PortalLoginResult
            {
                Success = false,
                Message = "The portal password is incorrect."
            };
        }

        return new PortalLoginResult
        {
            Success = true,
            Message = "Portal access granted.",
            User = user
        };
    }

    public PorterLoginResult LoginPorter(PorterLoginRequest request)
    {
        var staffId = request.StaffId.Trim().ToUpperInvariant();
        var porter = _state.Users.FirstOrDefault(x => x.Role == UserRole.Porter && x.IsActive && x.StaffId == staffId);

        return porter is null
            ? new PorterLoginResult { Success = false, Message = "No porter account matches that staff ID." }
            : new PorterLoginResult { Success = true, Message = "Porter access granted.", Porter = porter };
    }

    public PorterDashboardSnapshot GetPorterDashboard(string porterId)
    {
        var porter = RequirePortalUser(porterId);
        if (porter.Role != UserRole.Porter)
        {
            throw new InvalidOperationException("This account is not a porter portal account.");
        }

        return new PorterDashboardSnapshot
        {
            Porter = porter,
            VisibleComplaints = _state.Complaints
                .Where(x => IsTenantMatch(x.TenantId, porter.TenantId) && x.StudentGender == porter.Gender)
                .OrderByDescending(x => x.UpdatedAt)
                .ToList()
        };
    }

    public async Task<SendOtpResult> SendOtpAsync(SendOtpRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        ValidateOtpRequestEmail(email, request);

        await _gate.WaitAsync();
        try
        {
            var user = ResolveOtpUser(email, request);
            if (user is null && request.Mode == AppUseMode.Personal)
            {
                user = EnsurePersonalWorkspaceUser(email, request.RoleHint);
                await SaveStateAsync();
            }

            if (user is null)
            {
                return new SendOtpResult
                {
                    Success = false,
                    Message = request.Mode == AppUseMode.Personal
                        ? "That personal VirgoHub account is not active yet."
                        : "This school email has not been activated by the super admin yet."
                };
            }

            var isTestAccount = user.IsSeedAccount;
            var code = isTestAccount
                ? _options.TestStudentOtpCode
                : Random.Shared.Next(100000, 999999).ToString(CultureInfo.InvariantCulture);
            _state.OtpChallenges.RemoveAll(x => x.Email == email);

            var challenge = new OtpChallenge
            {
                Email = email,
                Code = code,
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(_options.OtpMinutesToLive)
            };

            _state.OtpChallenges.Add(challenge);
            await SaveStateAsync();

            if (isTestAccount)
            {
                return new SendOtpResult
                {
                    Success = true,
                    Message = "This seeded demo account always uses the fixed test code shown below.",
                    DebugCode = code,
                    ExpiresAt = challenge.ExpiresAt,
                    ResolvedTenantId = user.TenantId,
                    ResolvedTenantName = user.TenantName,
                    ResolvedMode = request.Mode
                };
            }

            var mailDelivery = await TrySendMailAsync(email, code);
            if (!mailDelivery.Delivered)
            {
                var canShowLocalCode = !mailDelivery.IsConfigured && _environment.IsDevelopment();
                return new SendOtpResult
                {
                    Success = !mailDelivery.IsConfigured && _environment.IsDevelopment(),
                    Message = mailDelivery.IsConfigured
                        ? mailDelivery.ErrorMessage ?? "VirgoHub could not send the sign-in code email."
                        : "SMTP is not configured yet. Add your Gmail address and app password in appsettings before requesting a real email code.",
                    DebugCode = canShowLocalCode ? code : null,
                    ExpiresAt = challenge.ExpiresAt,
                    ResolvedTenantId = user.TenantId,
                    ResolvedTenantName = user.TenantName,
                    ResolvedMode = request.Mode
                };
            }

            return new SendOtpResult
            {
                Success = true,
                Message = "A secure sign-in code has been sent to the provided email address.",
                DebugCode = null,
                ExpiresAt = challenge.ExpiresAt,
                ResolvedTenantId = user.TenantId,
                ResolvedTenantName = user.TenantName,
                ResolvedMode = request.Mode
            };
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<VerifyOtpResult> VerifyOtpAsync(VerifyOtpRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var code = request.Code.Trim();

        await _gate.WaitAsync();
        try
        {
            var challenge = _state.OtpChallenges.FirstOrDefault(x =>
                x.Email == email &&
                x.Code == code &&
                !x.Consumed &&
                x.ExpiresAt >= DateTimeOffset.UtcNow);

            if (challenge is null)
            {
                return new VerifyOtpResult
                {
                    Success = false,
                    Message = "That sign-in code is invalid or has expired."
                };
            }

            var user = ResolveOtpUser(email, request.Mode, request.TenantId, request.RoleHint);
            if (user is null)
            {
                return new VerifyOtpResult
                {
                    Success = false,
                    Message = "This VirgoHub account is no longer active."
                };
            }

            challenge.Consumed = true;
            _state.Sessions.RemoveAll(x => x.UserId == user.Id || x.ExpiresAt < DateTimeOffset.UtcNow);

            var session = new UserSession
            {
                UserId = user.Id,
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(_options.SessionHoursToLive)
            };

            _state.Sessions.Add(session);
            AddNotification(user.Id, "Login successful", "You are now signed in to VirgoHub.", "Security");
            AddActivity(user.Id, "Signed in", "You signed in with your email and verification code.", "Security");
            await SaveStateAsync();

            return new VerifyOtpResult
            {
                Success = true,
                Message = "Sign-in successful.",
                SessionToken = session.Token,
                User = user
            };
        }
        finally
        {
            _gate.Release();
        }
    }

    public bool IsAuthorized(string userId, string? sessionToken) =>
        !string.IsNullOrWhiteSpace(sessionToken) &&
        _state.Sessions.Any(x => x.UserId == userId && x.Token == sessionToken && x.ExpiresAt >= DateTimeOffset.UtcNow);

    public async Task<CampusEvent> CreateEventAsync(CreateEventRequest request, string? actorUserId = null)
    {
        var actor = ResolveSchoolActor(actorUserId);
        ValidateRequired(request.Title, "Event title");
        ValidateRequired(request.Venue, "Venue");
        var audienceFaculty = NormalizeAudienceFaculty(request.AudienceFaculty);
        var audienceDepartments = NormalizeAudienceDepartments(request.AudienceDepartments);
        ValidateAudienceSelection(audienceFaculty, audienceDepartments, "event");

        var campusEvent = new CampusEvent
        {
            TenantId = actor.TenantId,
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            Venue = request.Venue.Trim(),
            Category = request.Category.Trim(),
            AudienceFaculty = audienceFaculty,
            AudienceDepartments = audienceDepartments,
            StartsAt = request.StartsAt,
            CreatedBy = request.CreatedBy.Trim()
        };

        await _gate.WaitAsync();
        try
        {
            _state.Events.Add(campusEvent);
            foreach (var student in _state.Users.Where(x =>
                         x.Role == UserRole.Student &&
                         x.IsActive &&
                         IsTenantMatch(x.TenantId, campusEvent.TenantId) &&
                         CanStudentAccessAudience(x, campusEvent.AudienceFaculty, campusEvent.AudienceDepartments)))
            {
                AddNotification(student.Id, campusEvent.Title, $"A new {campusEvent.Category.ToLowerInvariant()} update has been published for {campusEvent.Venue}.", "Events");
                AddActivity(student.Id, "Event update", $"{campusEvent.Title} is now visible in your campus feed.", "Events");
            }

            await SaveStateAsync();
            return campusEvent;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<CampusEvent> UpdateEventAsync(string eventId, UpdateEventRequest request, string? actorUserId = null)
    {
        var actor = ResolveSchoolActor(actorUserId);
        ValidateRequired(request.Title, "Event title");
        ValidateRequired(request.Venue, "Venue");
        var audienceFaculty = NormalizeAudienceFaculty(request.AudienceFaculty);
        var audienceDepartments = NormalizeAudienceDepartments(request.AudienceDepartments);
        ValidateAudienceSelection(audienceFaculty, audienceDepartments, "event");

        await _gate.WaitAsync();
        try
        {
            var campusEvent = _state.Events.FirstOrDefault(x => x.Id == eventId && IsTenantMatch(x.TenantId, actor.TenantId))
                ?? throw new InvalidOperationException("Event not found.");

            campusEvent.Title = request.Title.Trim();
            campusEvent.Description = request.Description.Trim();
            campusEvent.Venue = request.Venue.Trim();
            campusEvent.Category = request.Category.Trim();
            campusEvent.AudienceFaculty = audienceFaculty;
            campusEvent.AudienceDepartments = audienceDepartments;
            campusEvent.StartsAt = request.StartsAt;

            await SaveStateAsync();
            return campusEvent;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task DeleteEventAsync(string eventId, string? actorUserId = null)
    {
        var actor = ResolveSchoolActor(actorUserId);
        await _gate.WaitAsync();
        try
        {
            var removed = _state.Events.RemoveAll(x => x.Id == eventId && IsTenantMatch(x.TenantId, actor.TenantId));
            if (removed == 0)
            {
                throw new InvalidOperationException("Event not found.");
            }

            await SaveStateAsync();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<SportsFacility> CreateFacilityAsync(CreateFacilityRequest request, string? actorUserId = null)
    {
        var actor = ResolveSchoolActor(actorUserId);
        ValidateRequired(request.Name, "Facility name");
        ValidateRequired(request.Location, "Location");
        ValidateRequired(request.OpenHours, "Open hours");

        var facility = new SportsFacility
        {
            TenantId = actor.TenantId,
            Name = request.Name.Trim(),
            Location = request.Location.Trim(),
            OpenHours = request.OpenHours.Trim(),
            Capacity = Math.Max(1, request.Capacity),
            Description = request.Description.Trim()
        };

        await _gate.WaitAsync();
        try
        {
            _state.Facilities.Add(facility);
            await SaveStateAsync();
            return facility;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<SportsFacility> UpdateFacilityAsync(string facilityId, UpdateFacilityRequest request, string? actorUserId = null)
    {
        var actor = ResolveSchoolActor(actorUserId);
        ValidateRequired(request.Name, "Facility name");
        ValidateRequired(request.Location, "Location");
        ValidateRequired(request.OpenHours, "Open hours");

        await _gate.WaitAsync();
        try
        {
            var facility = _state.Facilities.FirstOrDefault(x => x.Id == facilityId && IsTenantMatch(x.TenantId, actor.TenantId))
                ?? throw new InvalidOperationException("Facility not found.");

            facility.Name = request.Name.Trim();
            facility.Location = request.Location.Trim();
            facility.OpenHours = request.OpenHours.Trim();
            facility.Capacity = Math.Max(1, request.Capacity);
            facility.Description = request.Description.Trim();

            foreach (var booking in _state.Bookings.Where(x => x.FacilityId == facility.Id && IsTenantMatch(x.TenantId, actor.TenantId)))
            {
                booking.FacilityName = facility.Name;
            }

            await SaveStateAsync();
            return facility;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task DeleteFacilityAsync(string facilityId, string? actorUserId = null)
    {
        var actor = ResolveSchoolActor(actorUserId);
        await _gate.WaitAsync();
        try
        {
            var hasBookings = _state.Bookings.Any(x => x.FacilityId == facilityId && IsTenantMatch(x.TenantId, actor.TenantId));
            if (hasBookings)
            {
                throw new InvalidOperationException("This facility already has booking history and cannot be deleted.");
            }

            var removed = _state.Facilities.RemoveAll(x => x.Id == facilityId && IsTenantMatch(x.TenantId, actor.TenantId));
            if (removed == 0)
            {
                throw new InvalidOperationException("Facility not found.");
            }

            await SaveStateAsync();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<SportsBooking> CreateBookingAsync(string userId, CreateBookingRequest request)
    {
        var user = RequireStudent(userId);
        ValidateRequired(request.FacilityId, "Facility");
        ValidateRequired(request.SlotLabel, "Time slot");

        await _gate.WaitAsync();
        try
        {
            var facility = _state.Facilities.FirstOrDefault(x => x.Id == request.FacilityId)
                ?? throw new InvalidOperationException("The selected facility was not found.");

            var conflict = _state.Bookings.Any(x =>
                x.FacilityId == request.FacilityId &&
                x.BookingDate == request.BookingDate &&
                x.Status != SportsBookingStatus.Rejected &&
                x.SlotLabel.Equals(request.SlotLabel, StringComparison.OrdinalIgnoreCase));

            if (conflict)
            {
                throw new InvalidOperationException("That facility time has already been booked or is awaiting approval.");
            }

            var booking = new SportsBooking
            {
                TenantId = user.TenantId,
                UserId = user.Id,
                StudentName = user.FullName,
                FacilityId = facility.Id,
                FacilityName = facility.Name,
                BookingDate = request.BookingDate,
                SlotLabel = request.SlotLabel.Trim(),
                Purpose = request.Purpose.Trim(),
                Status = SportsBookingStatus.PendingApproval
            };

            _state.Bookings.Add(booking);
            AddNotification(user.Id, "Booking request sent", $"Your request for {facility.Name} on {booking.BookingDate:dd MMM yyyy} now awaits sports approval.", "Sports");
            AddActivity(user.Id, "Sports booking requested", $"Requested {facility.Name} for {booking.SlotLabel}.", "Sports");

            foreach (var director in _state.Users.Where(x => x.IsActive && x.Role == UserRole.Student && x.IsSportsDirector && IsTenantMatch(x.TenantId, booking.TenantId)))
            {
                AddNotification(director.Id, "Booking approval needed", $"{user.FullName} requested {facility.Name} for {booking.SlotLabel} on {booking.BookingDate:dd MMM yyyy}.", "Sports");
            }

            await SaveStateAsync();
            return booking;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<SportsBooking> ApproveBookingAsync(string actorUserId, string bookingId)
    {
        var actor = RequireStudent(actorUserId);
        if (!actor.IsSportsDirector)
        {
            throw new InvalidOperationException("This account does not have sports director approval access.");
        }

        await _gate.WaitAsync();
        try
        {
            var booking = _state.Bookings.FirstOrDefault(x => x.Id == bookingId)
                ?? throw new InvalidOperationException("Booking request not found.");

            if (booking.Status == SportsBookingStatus.Approved)
            {
                return booking;
            }

            var conflict = _state.Bookings.Any(x =>
                x.Id != booking.Id &&
                x.FacilityId == booking.FacilityId &&
                x.BookingDate == booking.BookingDate &&
                x.Status == SportsBookingStatus.Approved &&
                x.SlotLabel.Equals(booking.SlotLabel, StringComparison.OrdinalIgnoreCase));

            if (conflict)
            {
                booking.Status = SportsBookingStatus.Rejected;
                AddNotification(booking.UserId, "Booking unavailable", $"That {booking.FacilityName} time is no longer available.", "Sports");
                await SaveStateAsync();
                throw new InvalidOperationException("That facility time has already been booked.");
            }

            booking.Status = SportsBookingStatus.Approved;
            booking.ApprovedAt = DateTimeOffset.UtcNow;
            booking.ApprovedByUserId = actor.Id;
            booking.ApprovedByName = actor.FullName;
            AddNotification(booking.UserId, "Facility booked", $"Your reservation for {booking.FacilityName} on {booking.BookingDate:dd MMM yyyy} has been approved.", "Sports");
            AddActivity(booking.UserId, "Sports booking approved", $"{booking.FacilityName} was approved for {booking.SlotLabel}.", "Sports");
            await SaveStateAsync();
            return booking;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<ComplaintTicket> CreateComplaintAsync(string userId, CreateComplaintRequest request)
    {
        var user = RequireStudent(userId);
        ValidateRequired(request.Title, "Complaint title");
        ValidateRequired(request.Category, "Complaint category");
        ValidateRequired(request.Description, "Complaint details");

        var complaint = new ComplaintTicket
        {
            TenantId = user.TenantId,
            UserId = user.Id,
            StudentName = user.FullName,
            Title = request.Title.Trim(),
            Category = request.Category.Trim(),
            Hostel = string.IsNullOrWhiteSpace(request.Hostel) ? user.Hostel : request.Hostel.Trim(),
            Room = request.Room.Trim(),
            StudentGender = user.Gender,
            Description = request.Description.Trim()
        };

        await _gate.WaitAsync();
        try
        {
            _state.Complaints.Add(complaint);
            AddNotification(user.Id, "Complaint submitted", "Your hostel complaint has been submitted and routed to the right porter team.", "Complaints");
            AddActivity(user.Id, "Complaint filed", $"Submitted a {complaint.Category.ToLowerInvariant()} complaint for {complaint.Hostel}.", "Complaints");
            await SaveStateAsync();
            return complaint;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<ComplaintTicket> UpdateComplaintAsync(string actorUserId, string complaintId, UpdateComplaintStatusRequest request)
    {
        var actor = RequirePortalUser(actorUserId);

        await _gate.WaitAsync();
        try
        {
            var complaint = _state.Complaints.FirstOrDefault(x => x.Id == complaintId)
                ?? throw new InvalidOperationException("Complaint not found.");

            EnsureComplaintAccess(actor, complaint);

            complaint.Status = request.Status;
            complaint.ResolutionNote = request.ResolutionNote.Trim();
            complaint.UpdatedAt = DateTimeOffset.UtcNow;
            AddNotification(complaint.UserId, "Complaint update", $"Your complaint '{complaint.Title}' is now marked as {complaint.Status}.", "Complaints");
            AddActivity(complaint.UserId, "Complaint updated", $"Complaint '{complaint.Title}' moved to {complaint.Status}.", "Complaints");
            await SaveStateAsync();
            return complaint;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<Election> CreateElectionAsync(CreateElectionRequest request, string? actorUserId = null)
    {
        var actor = ResolveSchoolActor(actorUserId);
        ValidateRequired(request.Title, "Election title");
        ValidateRequired(request.Position, "Position");

        var normalizedPosition = request.Position.Trim();
        var audienceFaculty = NormalizeAudienceFaculty(request.AudienceFaculty);
        var audienceDepartments = NormalizeAudienceDepartments(request.AudienceDepartments);
        ValidateAudienceSelection(audienceFaculty, audienceDepartments, "election");
        var candidates = BuildCandidates(request.Candidates, request.CandidateNames, normalizedPosition);

        if (candidates.Count < 2)
        {
            throw new InvalidOperationException("Add at least two candidates for an election.");
        }

        var election = new Election
        {
            TenantId = actor.TenantId,
            Title = request.Title.Trim(),
            Position = normalizedPosition,
            AudienceFaculty = audienceFaculty,
            AudienceDepartments = audienceDepartments,
            OpensAt = request.OpensAt,
            ClosesAt = request.ClosesAt,
            IsActive = true,
            Candidates = candidates
        };

        await _gate.WaitAsync();
        try
        {
            _state.Elections.Add(election);
            foreach (var student in _state.Users.Where(x =>
                         x.Role == UserRole.Student &&
                         x.IsActive &&
                         IsTenantMatch(x.TenantId, election.TenantId) &&
                         CanStudentAccessAudience(x, election.AudienceFaculty, election.AudienceDepartments)))
            {
                AddNotification(student.Id, election.Title, $"Voting for {election.Position} is now scheduled inside VirgoHub.", "Voting");
                AddActivity(student.Id, "Election notice", $"{election.Title} is now available for your audience group.", "Voting");
            }

            await SaveStateAsync();
            return election;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<Election> UpdateElectionAsync(string electionId, UpdateElectionRequest request, string? actorUserId = null)
    {
        var actor = ResolveSchoolActor(actorUserId);
        ValidateRequired(request.Title, "Election title");
        ValidateRequired(request.Position, "Position");

        var normalizedPosition = request.Position.Trim();
        var audienceFaculty = NormalizeAudienceFaculty(request.AudienceFaculty);
        var audienceDepartments = NormalizeAudienceDepartments(request.AudienceDepartments);
        ValidateAudienceSelection(audienceFaculty, audienceDepartments, "election");
        var candidates = BuildCandidates(request.Candidates, string.Empty, normalizedPosition);

        if (candidates.Count < 2)
        {
            throw new InvalidOperationException("Add at least two candidates for an election.");
        }

        await _gate.WaitAsync();
        try
        {
            var election = _state.Elections.FirstOrDefault(x => x.Id == electionId && IsTenantMatch(x.TenantId, actor.TenantId))
                ?? throw new InvalidOperationException("Election not found.");

            election.Title = request.Title.Trim();
            election.Position = normalizedPosition;
            election.AudienceFaculty = audienceFaculty;
            election.AudienceDepartments = audienceDepartments;
            election.OpensAt = request.OpensAt;
            election.ClosesAt = request.ClosesAt;
            election.IsActive = request.IsActive;
            election.Candidates = candidates;

            _state.Votes.RemoveAll(x => x.ElectionId == election.Id && election.Candidates.All(c => c.Id != x.CandidateId));
            await SaveStateAsync();
            return BuildElectionView(election);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task DeleteElectionAsync(string electionId, string? actorUserId = null)
    {
        var actor = ResolveSchoolActor(actorUserId);
        await _gate.WaitAsync();
        try
        {
            var removed = _state.Elections.RemoveAll(x => x.Id == electionId && IsTenantMatch(x.TenantId, actor.TenantId));
            if (removed == 0)
            {
                throw new InvalidOperationException("Election not found.");
            }

            _state.Votes.RemoveAll(x => x.ElectionId == electionId && IsTenantMatch(x.TenantId, actor.TenantId));
            await SaveStateAsync();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task CastVoteAsync(string userId, CastVoteRequest request)
    {
        var user = RequireStudent(userId);

        await _gate.WaitAsync();
        try
        {
            var election = _state.Elections.FirstOrDefault(x => x.Id == request.ElectionId)
                ?? throw new InvalidOperationException("Election not found.");

            if (!CanStudentAccessAudience(user, election.AudienceFaculty, election.AudienceDepartments))
            {
                throw new InvalidOperationException("This election is outside your faculty or department voting scope.");
            }

            if (!IsElectionOpen(election))
            {
                throw new InvalidOperationException("This election is not open for voting.");
            }

            if (_state.Votes.Any(x => x.ElectionId == election.Id && x.UserId == user.Id))
            {
                throw new InvalidOperationException("You have already voted in this election.");
            }

            var candidate = election.Candidates.FirstOrDefault(x => x.Id == request.CandidateId)
                ?? throw new InvalidOperationException("Candidate not found.");

            _state.Votes.Add(new VoteRecord
            {
                TenantId = election.TenantId,
                ElectionId = election.Id,
                CandidateId = candidate.Id,
                UserId = user.Id
            });

            AddNotification(user.Id, "Vote submitted", $"Your vote for {candidate.Name} in {election.Title} has been recorded.", "Voting");
            AddActivity(user.Id, "Vote cast", $"Participated in {election.Title}.", "Voting");
            await SaveStateAsync();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<CourseMaterial> CreateCourseMaterialAsync(string actorUserId, CreateCourseMaterialRequest request)
    {
        var actor = RequirePortalUser(actorUserId);
        ValidateRequired(request.CourseCode, "Course code");
        ValidateRequired(request.Title, "Course title");
        ValidateRequired(request.LecturerName, "Lecturer name");

        if (!request.IsGeneralCourse)
        {
            ValidateRequired(request.Faculty, "Faculty");
            ValidateRequired(request.Department, "Department");
            ValidateRequired(request.Level, "Level");
        }

        var owner = ResolveMaterialOwner(actor, request.LecturerUserId);

        var material = new CourseMaterial
        {
            TenantId = actor.TenantId,
            CourseCode = request.CourseCode.Trim().ToUpperInvariant(),
            Title = request.Title.Trim(),
            LecturerName = owner.FullName,
            LecturerUserId = owner.Id,
            Faculty = request.IsGeneralCourse ? "General Studies" : request.Faculty.Trim(),
            Department = request.IsGeneralCourse ? "All Departments" : request.Department.Trim(),
            Level = request.IsGeneralCourse ? "All Levels" : request.Level.Trim(),
            IsGeneralCourse = request.IsGeneralCourse,
            RequiresManualApproval = request.RequiresManualApproval,
            MaterialType = string.IsNullOrWhiteSpace(request.MaterialType) ? "Course Note" : request.MaterialType.Trim(),
            ResourceUrl = request.ResourceUrl.Trim(),
            Summary = request.Summary.Trim(),
            UploadedBy = string.IsNullOrWhiteSpace(request.UploadedBy) ? actor.FullName : request.UploadedBy.Trim(),
            PublishedAt = DateTimeOffset.UtcNow
        };

        EnsureMaterialScope(actor, material);

        await _gate.WaitAsync();
        try
        {
            _state.Materials.Add(material);
            await SaveStateAsync();
            return material;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<CourseMaterial> UpdateCourseMaterialAsync(string actorUserId, string materialId, UpdateCourseMaterialRequest request)
    {
        var actor = RequirePortalUser(actorUserId);
        ValidateRequired(request.CourseCode, "Course code");
        ValidateRequired(request.Title, "Course title");
        ValidateRequired(request.LecturerName, "Lecturer name");

        if (!request.IsGeneralCourse)
        {
            ValidateRequired(request.Faculty, "Faculty");
            ValidateRequired(request.Department, "Department");
            ValidateRequired(request.Level, "Level");
        }

        await _gate.WaitAsync();
        try
        {
            var material = _state.Materials.FirstOrDefault(x => x.Id == materialId)
                ?? throw new InvalidOperationException("Course material not found.");
            EnsureMaterialManagementAccess(actor, material);

            var owner = ResolveMaterialOwner(actor, request.LecturerUserId);

            material.CourseCode = request.CourseCode.Trim().ToUpperInvariant();
            material.Title = request.Title.Trim();
            material.LecturerName = owner.FullName;
            material.LecturerUserId = owner.Id;
            material.Faculty = request.IsGeneralCourse ? "General Studies" : request.Faculty.Trim();
            material.Department = request.IsGeneralCourse ? "All Departments" : request.Department.Trim();
            material.Level = request.IsGeneralCourse ? "All Levels" : request.Level.Trim();
            material.IsGeneralCourse = request.IsGeneralCourse;
            material.RequiresManualApproval = request.RequiresManualApproval;
            material.MaterialType = request.MaterialType.Trim();
            material.ResourceUrl = request.ResourceUrl.Trim();
            material.Summary = request.Summary.Trim();
            material.UploadedBy = request.UploadedBy.Trim();

            EnsureMaterialScope(actor, material);

            await SaveStateAsync();
            return material;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task DeleteCourseMaterialAsync(string actorUserId, string materialId)
    {
        var actor = RequirePortalUser(actorUserId);
        await _gate.WaitAsync();
        try
        {
            var material = _state.Materials.FirstOrDefault(x => x.Id == materialId)
                ?? throw new InvalidOperationException("Course material not found.");
            EnsureMaterialManagementAccess(actor, material);

            var removed = _state.Materials.RemoveAll(x => x.Id == materialId);
            if (removed == 0)
            {
                throw new InvalidOperationException("Course material not found.");
            }

            await SaveStateAsync();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<CourseEnrollment> EnrollCourseAsync(string userId, CreateCourseEnrollmentRequest request)
    {
        var user = RequireStudent(userId);
        ValidateRequired(request.CourseCode, "Course code");

        var courseCode = request.CourseCode.Trim().ToUpperInvariant();

        await _gate.WaitAsync();
        try
        {
            var matchingMaterial = _state.Materials
                .Where(x => x.CourseCode.Equals(courseCode, StringComparison.OrdinalIgnoreCase))
                .FirstOrDefault(x => CanStudentAccessMaterial(user, x));

            if (matchingMaterial is null)
            {
                throw new InvalidOperationException("No eligible course material matches that course code for your level, department, or faculty.");
            }

            var existingEnrollment = _state.Enrollments.FirstOrDefault(x =>
                x.UserId == user.Id &&
                x.CourseCode.Equals(courseCode, StringComparison.OrdinalIgnoreCase));

            if (existingEnrollment is not null)
            {
                throw new InvalidOperationException("You are already enrolled in that course.");
            }

            var enrollment = new CourseEnrollment
            {
                TenantId = user.TenantId,
                UserId = user.Id,
                StudentName = user.FullName,
                CourseCode = matchingMaterial.CourseCode,
                CourseTitle = matchingMaterial.Title,
                Faculty = user.Faculty,
                Department = user.Department,
                Level = user.Level,
                Status = matchingMaterial.RequiresManualApproval ? CourseEnrollmentStatus.Pending : CourseEnrollmentStatus.Approved,
                RequiresManualApproval = matchingMaterial.RequiresManualApproval,
                ApprovedAt = matchingMaterial.RequiresManualApproval ? null : DateTimeOffset.UtcNow,
                ApprovedByUserId = matchingMaterial.RequiresManualApproval ? string.Empty : matchingMaterial.LecturerUserId,
                ApprovedByName = matchingMaterial.RequiresManualApproval ? string.Empty : matchingMaterial.LecturerName,
                EnrolledAt = DateTimeOffset.UtcNow
            };

            _state.Enrollments.Add(enrollment);
            if (matchingMaterial.RequiresManualApproval)
            {
                AddNotification(user.Id, "Enrollment pending", $"{matchingMaterial.CourseCode} now awaits lecturer approval.", "Library");
                AddActivity(user.Id, "Enrollment requested", $"Requested access to {matchingMaterial.CourseCode}.", "Library");
            }
            else
            {
                AddNotification(user.Id, "Course enrolled", $"You now have access to {matchingMaterial.CourseCode} materials.", "Library");
                AddActivity(user.Id, "Course enrolled", $"Joined {matchingMaterial.CourseCode} material stream.", "Library");
            }

            await SaveStateAsync();
            return enrollment;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<CourseEnrollment> ApproveCourseEnrollmentAsync(string actorUserId, string enrollmentId)
    {
        var actor = RequirePortalUser(actorUserId);

        await _gate.WaitAsync();
        try
        {
            var enrollment = _state.Enrollments.FirstOrDefault(x => x.Id == enrollmentId)
                ?? throw new InvalidOperationException("Enrollment request not found.");
            var material = _state.Materials.FirstOrDefault(x => x.CourseCode.Equals(enrollment.CourseCode, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException("The course material for this enrollment no longer exists.");

            EnsureEnrollmentApprovalAccess(actor, material);

            enrollment.Status = CourseEnrollmentStatus.Approved;
            enrollment.ApprovedAt = DateTimeOffset.UtcNow;
            enrollment.ApprovedByUserId = actor.Id;
            enrollment.ApprovedByName = actor.FullName;

            AddNotification(enrollment.UserId, "Enrollment approved", $"You can now open {enrollment.CourseCode} materials.", "Library");
            AddActivity(enrollment.UserId, "Enrollment approved", $"Your {enrollment.CourseCode} request was approved.", "Library");
            await SaveStateAsync();
            return enrollment;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<int> ApproveCourseEnrollmentsAsync(string actorUserId, CourseEnrollmentApprovalRequest request)
    {
        var actor = RequirePortalUser(actorUserId);
        ValidateRequired(request.CourseCode, "Course code");
        var courseCode = request.CourseCode.Trim().ToUpperInvariant();

        await _gate.WaitAsync();
        try
        {
            var material = _state.Materials.FirstOrDefault(x => x.CourseCode.Equals(courseCode, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException("Course material not found.");
            EnsureEnrollmentApprovalAccess(actor, material);

            var enrollments = _state.Enrollments
                .Where(x => x.CourseCode.Equals(courseCode, StringComparison.OrdinalIgnoreCase))
                .Where(x => x.Status == CourseEnrollmentStatus.Pending)
                .Where(x => string.IsNullOrWhiteSpace(request.Faculty) || x.Faculty.Equals(request.Faculty.Trim(), StringComparison.OrdinalIgnoreCase))
                .Where(x => string.IsNullOrWhiteSpace(request.Department) || x.Department.Equals(request.Department.Trim(), StringComparison.OrdinalIgnoreCase))
                .Where(x => string.IsNullOrWhiteSpace(request.Level) || x.Level.Equals(request.Level.Trim(), StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var enrollment in enrollments)
            {
                enrollment.Status = CourseEnrollmentStatus.Approved;
                enrollment.ApprovedAt = DateTimeOffset.UtcNow;
                enrollment.ApprovedByUserId = actor.Id;
                enrollment.ApprovedByName = actor.FullName;
                AddNotification(enrollment.UserId, "Enrollment approved", $"You can now open {enrollment.CourseCode} materials.", "Library");
                AddActivity(enrollment.UserId, "Enrollment approved", $"Your {enrollment.CourseCode} request was approved.", "Library");
            }

            await SaveStateAsync();
            return enrollments.Count;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<PersonalTeachingGroup> CreatePersonalTeachingGroupAsync(string actorUserId, CreatePersonalTeachingGroupRequest request)
    {
        var actor = RequirePersonalLecturer(actorUserId);
        ValidateRequired(request.Title, "Group title");
        ValidateRequired(request.CourseCode, "Course code");

        var group = new PersonalTeachingGroup
        {
            TenantId = actor.TenantId,
            LecturerUserId = actor.Id,
            LecturerName = actor.FullName,
            Title = request.Title.Trim(),
            CourseCode = request.CourseCode.Trim().ToUpperInvariant(),
            Summary = request.Summary.Trim()
        };

        group.InviteCode = BuildInviteCode(group.CourseCode);
        group.InviteLink = $"virgohub://personal/join/{group.InviteCode}";

        await _gate.WaitAsync();
        try
        {
            while (_state.TeachingGroups.Any(x =>
                       x.InviteCode.Equals(group.InviteCode, StringComparison.OrdinalIgnoreCase)))
            {
                group.InviteCode = BuildInviteCode(group.CourseCode);
                group.InviteLink = $"virgohub://personal/join/{group.InviteCode}";
            }

            _state.TeachingGroups.Add(group);
            AddNotification(actor.Id, "Teaching group ready", $"{group.Title} is ready. Share {group.InviteCode} so learners can join your space.", "Personal Groups");
            AddActivity(actor.Id, "Teaching group created", $"Created {group.Title} with invite code {group.InviteCode}.", "Personal Groups");
            await SaveStateAsync();
            return group;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<PersonalTeachingGroup> JoinPersonalTeachingGroupAsync(string userId, JoinPersonalTeachingGroupRequest request)
    {
        var actor = RequirePersonalMobileUser(userId);
        ValidateRequired(request.InviteCode, "Invite code");

        if (actor.Role != UserRole.Student)
        {
            throw new InvalidOperationException("Only personal learners can join a teaching group with an invite code.");
        }

        var inviteCode = request.InviteCode.Trim().ToUpperInvariant();

        await _gate.WaitAsync();
        try
        {
            var group = _state.TeachingGroups.FirstOrDefault(x =>
                x.InviteCode.Equals(inviteCode, StringComparison.OrdinalIgnoreCase) &&
                IsTenantMatch(x.TenantId, actor.TenantId))
                ?? throw new InvalidOperationException("That teaching group invite code could not be found.");

            if (group.Members.Any(x => string.Equals(x.UserId, actor.Id, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("You are already inside that teaching group.");
            }

            group.Members.Add(new TeachingGroupMember
            {
                UserId = actor.Id,
                FullName = actor.FullName,
                Email = actor.Email
            });

            AddNotification(actor.Id, "Group joined", $"You now have access to {group.Title}.", "Personal Groups");
            AddActivity(actor.Id, "Joined teaching group", $"Joined {group.Title} with invite code {group.InviteCode}.", "Personal Groups");
            AddNotification(group.LecturerUserId, "New learner joined", $"{actor.FullName} joined {group.Title}.", "Personal Groups");
            await SaveStateAsync();
            return group;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<PersonalGroupPost> CreatePersonalGroupPostAsync(string actorUserId, CreatePersonalGroupPostRequest request)
    {
        var actor = RequireMobileUser(actorUserId);
        var group = RequireAccessiblePersonalGroup(actor, request.GroupId);
        ValidateRequired(request.Message, "Message");

        if (request.IsAnnouncement && actor.Role != UserRole.Lecturer)
        {
            throw new InvalidOperationException("Only personal lecturers can publish announcements to a teaching group.");
        }

        await _gate.WaitAsync();
        try
        {
            var post = new PersonalGroupPost
            {
                TenantId = actor.TenantId,
                GroupId = group.Id,
                CourseCode = group.CourseCode,
                AuthorUserId = actor.Id,
                AuthorName = actor.FullName,
                Title = request.Title.Trim(),
                Message = request.Message.Trim(),
                AttachmentUrl = request.AttachmentUrl.Trim(),
                IsAnnouncement = request.IsAnnouncement
            };

            _state.PersonalGroupPosts.Add(post);
            foreach (var member in group.Members.Where(x => !string.Equals(x.UserId, actor.Id, StringComparison.OrdinalIgnoreCase)))
            {
                AddNotification(member.UserId,
                    request.IsAnnouncement ? "Group announcement" : "New group message",
                    $"{actor.FullName} posted in {group.Title}.",
                    "Personal Groups");
            }

            if (actor.Role == UserRole.Student)
            {
                AddNotification(group.LecturerUserId, "Learner reply received", $"{actor.FullName} sent a new message in {group.Title}.", "Personal Groups");
            }

            await SaveStateAsync();
            return post;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<CourseMaterial> CreatePersonalMaterialAsync(string actorUserId, CreatePersonalMaterialRequest request)
    {
        var actor = RequirePersonalLecturer(actorUserId);
        var group = RequireOwnedPersonalGroup(actor, request.GroupId);
        ValidateRequired(request.Title, "Material title");
        ValidateRequired(request.MaterialType, "Material type");
        ValidateRequired(request.ResourceUrl, "Material link");

        await _gate.WaitAsync();
        try
        {
            var material = new CourseMaterial
            {
                TenantId = actor.TenantId,
                CourseCode = group.CourseCode,
                Title = request.Title.Trim(),
                LecturerName = actor.FullName,
                LecturerUserId = actor.Id,
                Faculty = "VirgoHub Personal",
                Department = group.Title,
                Level = "Personal",
                IsGeneralCourse = false,
                RequiresManualApproval = false,
                MaterialType = request.MaterialType.Trim(),
                ResourceUrl = request.ResourceUrl.Trim(),
                Summary = request.Summary.Trim(),
                UploadedBy = actor.FullName
            };

            _state.Materials.Add(material);
            _state.PersonalGroupPosts.Add(new PersonalGroupPost
            {
                TenantId = actor.TenantId,
                GroupId = group.Id,
                CourseCode = group.CourseCode,
                AuthorUserId = actor.Id,
                AuthorName = actor.FullName,
                Title = request.Title.Trim(),
                Message = $"New {request.MaterialType.Trim()} shared for {group.Title}.",
                AttachmentUrl = request.ResourceUrl.Trim(),
                IsAnnouncement = true
            });

            foreach (var member in group.Members)
            {
                AddNotification(member.UserId, "New material shared", $"{actor.FullName} shared {request.Title.Trim()} in {group.Title}.", "Library");
            }

            await SaveStateAsync();
            return material;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<PersonalAssignment> CreatePersonalAssignmentAsync(string actorUserId, CreatePersonalAssignmentRequest request)
    {
        var actor = RequirePersonalLecturer(actorUserId);
        var group = RequireOwnedPersonalGroup(actor, request.GroupId);
        ValidateRequired(request.Title, "Assignment title");
        ValidateRequired(request.Instructions, "Assignment instructions");

        await _gate.WaitAsync();
        try
        {
            var assignment = new PersonalAssignment
            {
                TenantId = actor.TenantId,
                GroupId = group.Id,
                CourseCode = group.CourseCode,
                LecturerUserId = actor.Id,
                LecturerName = actor.FullName,
                Title = request.Title.Trim(),
                Instructions = request.Instructions.Trim(),
                ResourceUrl = request.ResourceUrl.Trim(),
                DueAt = request.DueAt
            };

            _state.PersonalAssignments.Add(assignment);
            foreach (var member in group.Members)
            {
                AddNotification(member.UserId, "Assignment ready", $"{assignment.Title} is now available in {group.Title}.", "Assessment");
            }

            await SaveStateAsync();
            return assignment;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<PersonalAssignmentSubmission> SubmitPersonalAssignmentAsync(string actorUserId, SubmitPersonalAssignmentRequest request)
    {
        var actor = RequireMobileUser(actorUserId);
        if (actor.Role != UserRole.Student)
        {
            throw new InvalidOperationException("Only personal learners can submit assignments.");
        }

        ValidateRequired(request.AssignmentId, "Assignment");
        if (string.IsNullOrWhiteSpace(request.Note) && string.IsNullOrWhiteSpace(request.ResourceUrl))
        {
            throw new InvalidOperationException("Add a note or a submission link before sending your assignment.");
        }

        await _gate.WaitAsync();
        try
        {
            var assignment = _state.PersonalAssignments.FirstOrDefault(x => x.Id == request.AssignmentId)
                ?? throw new InvalidOperationException("That assignment could not be found.");
            var group = RequireAccessiblePersonalGroup(actor, assignment.GroupId);

            var existing = _state.PersonalAssignmentSubmissions.FirstOrDefault(x =>
                x.AssignmentId == assignment.Id &&
                x.StudentUserId == actor.Id);

            if (existing is not null)
            {
                existing.Note = request.Note.Trim();
                existing.ResourceUrl = request.ResourceUrl.Trim();
                existing.SubmittedAt = DateTimeOffset.UtcNow;
                await SaveStateAsync();
                AddNotification(assignment.LecturerUserId, "Assignment updated", $"{actor.FullName} updated work for {assignment.Title}.", "Assessment");
                return existing;
            }

            var submission = new PersonalAssignmentSubmission
            {
                TenantId = actor.TenantId,
                AssignmentId = assignment.Id,
                GroupId = group.Id,
                StudentUserId = actor.Id,
                StudentName = actor.FullName,
                Note = request.Note.Trim(),
                ResourceUrl = request.ResourceUrl.Trim()
            };

            _state.PersonalAssignmentSubmissions.Add(submission);
            AddNotification(actor.Id, "Assignment submitted", $"{assignment.Title} has been sent successfully.", "Assessment");
            AddNotification(assignment.LecturerUserId, "New assignment submission", $"{actor.FullName} submitted {assignment.Title}.", "Assessment");
            await SaveStateAsync();
            return submission;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<PersonalAssessment> CreatePersonalAssessmentAsync(string actorUserId, CreatePersonalAssessmentRequest request)
    {
        var actor = RequirePersonalLecturer(actorUserId);
        ValidateRequired(request.GroupId, "Teaching group");
        ValidateRequired(request.Title, "Assessment title");

        var questions = request.Questions
            .Where(x => !string.IsNullOrWhiteSpace(x.Prompt))
            .Select(BuildAssessmentQuestion)
            .ToList();

        if (questions.Count == 0)
        {
            throw new InvalidOperationException("Add at least one assessment question.");
        }

        await _gate.WaitAsync();
        try
        {
            var group = _state.TeachingGroups.FirstOrDefault(x =>
                x.Id.Equals(request.GroupId.Trim(), StringComparison.OrdinalIgnoreCase) &&
                x.TenantId.Equals(actor.TenantId, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException("That teaching group could not be found.");

            if (!string.Equals(group.LecturerUserId, actor.Id, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("You can only create assessments for your own teaching groups.");
            }

            var assessment = new PersonalAssessment
            {
                TenantId = actor.TenantId,
                GroupId = group.Id,
                GroupTitle = group.Title,
                CourseCode = group.CourseCode,
                LecturerUserId = actor.Id,
                LecturerName = actor.FullName,
                Title = request.Title.Trim(),
                Summary = request.Summary.Trim(),
                Type = request.Type,
                TimeLimitMinutes = Math.Clamp(request.TimeLimitMinutes, 5, 180),
                OpensAt = request.OpensAt,
                DueAt = request.DueAt <= request.OpensAt ? request.OpensAt.AddDays(1) : request.DueAt,
                Questions = questions,
                IsPublished = true
            };

            _state.PersonalAssessments.Add(assessment);
            AddNotification(actor.Id, "Assessment published", $"{assessment.Title} is now live for {group.Title}.", "Assessment");
            AddActivity(actor.Id, "Assessment created", $"Published {assessment.Title} for {group.Title}.", "Assessment");

            foreach (var member in group.Members)
            {
                AddNotification(member.UserId, $"{assessment.Type} ready", $"{assessment.Title} is now available in {group.Title}.", "Assessment");
                AddActivity(member.UserId, "Assessment assigned", $"{assessment.Title} was added to {group.Title}.", "Assessment");
            }

            await SaveStateAsync();
            return assessment;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<PersonalAssessmentSubmission> SubmitPersonalAssessmentAsync(string userId, SubmitPersonalAssessmentRequest request)
    {
        var actor = RequirePersonalMobileUser(userId);
        if (actor.Role != UserRole.Student)
        {
            throw new InvalidOperationException("Only personal learners can submit an assessment.");
        }

        ValidateRequired(request.AssessmentId, "Assessment");
        if (request.Responses.Count == 0)
        {
            throw new InvalidOperationException("Select answers before submitting this assessment.");
        }

        await _gate.WaitAsync();
        try
        {
            var assessment = _state.PersonalAssessments.FirstOrDefault(x =>
                x.Id.Equals(request.AssessmentId.Trim(), StringComparison.OrdinalIgnoreCase) &&
                x.TenantId.Equals(actor.TenantId, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException("That assessment could not be found.");

            if (!CanUserAccessPersonalAssessment(actor, assessment))
            {
                throw new InvalidOperationException("This assessment is outside your personal teaching groups.");
            }

            if (_state.PersonalAssessmentSubmissions.Any(x =>
                    x.AssessmentId.Equals(assessment.Id, StringComparison.OrdinalIgnoreCase) &&
                    x.UserId.Equals(actor.Id, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("You have already submitted this assessment.");
            }

            var normalizedResponses = request.Responses
                .Where(x => !string.IsNullOrWhiteSpace(x.QuestionId))
                .Select(x => new PersonalAssessmentResponse
                {
                    QuestionId = x.QuestionId.Trim(),
                    SelectedOption = NormalizeAnswerOption(x.SelectedOption)
                })
                .ToList();

            var score = assessment.Questions.Count(question =>
                normalizedResponses.Any(response =>
                    response.QuestionId.Equals(question.Id, StringComparison.OrdinalIgnoreCase) &&
                    response.SelectedOption.Equals(NormalizeAnswerOption(question.CorrectOption), StringComparison.OrdinalIgnoreCase)));

            var submission = new PersonalAssessmentSubmission
            {
                TenantId = actor.TenantId,
                AssessmentId = assessment.Id,
                GroupId = assessment.GroupId,
                UserId = actor.Id,
                StudentName = actor.FullName,
                AssessmentTitle = assessment.Title,
                CourseCode = assessment.CourseCode,
                Responses = normalizedResponses,
                Score = score,
                TotalQuestions = assessment.Questions.Count
            };

            _state.PersonalAssessmentSubmissions.Add(submission);
            AddNotification(actor.Id, "Assessment submitted", $"{assessment.Title} was submitted successfully.", "Assessment");
            AddActivity(actor.Id, "Assessment completed", $"Submitted {assessment.Title} and scored {score}/{assessment.Questions.Count}.", "Assessment");
            AddNotification(assessment.LecturerUserId, "New assessment submission", $"{actor.FullName} submitted {assessment.Title}.", "Assessment");
            await SaveStateAsync();
            return submission;
        }
        finally
        {
            _gate.Release();
        }
    }

    private static bool CanStudentAccessMaterial(UserProfile user, CourseMaterial material)
    {
        if (material.IsGeneralCourse)
        {
            return true;
        }

        var facultyMatch = string.Equals(user.Faculty.Trim(), material.Faculty.Trim(), StringComparison.OrdinalIgnoreCase);
        var departmentMatch = string.Equals(user.Department.Trim(), material.Department.Trim(), StringComparison.OrdinalIgnoreCase);
        var levelMatch = string.Equals(user.Level.Trim(), material.Level.Trim(), StringComparison.OrdinalIgnoreCase);
        return facultyMatch && departmentMatch && levelMatch;
    }

    private List<CourseEnrollment> BuildPersonalMaterialSpaces(UserProfile user)
    {
        var accessibleGroups = _state.TeachingGroups
            .Where(x => IsTenantMatch(x.TenantId, user.TenantId))
            .Where(x => string.Equals(x.LecturerUserId, user.Id, StringComparison.OrdinalIgnoreCase) ||
                        x.Members.Any(member => string.Equals(member.UserId, user.Id, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(x => x.CreatedAt)
            .ToList();

        var materialSpaces = new List<CourseEnrollment>();
        foreach (var group in accessibleGroups)
        {
            var linkedMaterial = _state.Materials.FirstOrDefault(x =>
                IsTenantMatch(x.TenantId, user.TenantId) &&
                x.CourseCode.Equals(group.CourseCode, StringComparison.OrdinalIgnoreCase));

            materialSpaces.Add(new CourseEnrollment
            {
                Id = $"personal-space-{group.Id}",
                TenantId = user.TenantId,
                UserId = user.Id,
                StudentName = user.FullName,
                CourseCode = group.CourseCode,
                CourseTitle = linkedMaterial?.Title ?? group.Title,
                Faculty = "VirgoHub Personal",
                Department = user.Role == UserRole.Lecturer ? "Teaching Space" : "Joined Space",
                Level = user.Role == UserRole.Lecturer ? "Tutor Track" : "Learner Track",
                Status = CourseEnrollmentStatus.Approved,
                RequiresManualApproval = false,
                ApprovedByUserId = group.LecturerUserId,
                ApprovedByName = group.LecturerName,
                ApprovedAt = group.CreatedAt,
                EnrolledAt = group.CreatedAt
            });
        }

        return materialSpaces;
    }

    private bool CanUserAccessPersonalMaterial(UserProfile user, CourseMaterial material)
    {
        if (!string.Equals(user.TenantId, material.TenantId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (user.Role == UserRole.Lecturer)
        {
            return string.Equals(user.Id, material.LecturerUserId, StringComparison.OrdinalIgnoreCase);
        }

        var group = _state.TeachingGroups.FirstOrDefault(x =>
            x.CourseCode.Equals(material.CourseCode, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.TenantId, material.TenantId, StringComparison.OrdinalIgnoreCase));

        return group is not null &&
               group.Members.Any(member => string.Equals(member.UserId, user.Id, StringComparison.OrdinalIgnoreCase));
    }

    private bool CanUserAccessPersonalGroup(UserProfile user, string groupId)
    {
        var group = _state.TeachingGroups.FirstOrDefault(x => x.Id == groupId);
        if (group is null || !string.Equals(group.TenantId, user.TenantId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return string.Equals(group.LecturerUserId, user.Id, StringComparison.OrdinalIgnoreCase) ||
               group.Members.Any(member => string.Equals(member.UserId, user.Id, StringComparison.OrdinalIgnoreCase));
    }

    private bool CanUserAccessPersonalAssessment(UserProfile user, PersonalAssessment assessment)
    {
        if (!string.Equals(user.TenantId, assessment.TenantId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (user.Role == UserRole.Lecturer)
        {
            return string.Equals(user.Id, assessment.LecturerUserId, StringComparison.OrdinalIgnoreCase);
        }

        var group = _state.TeachingGroups.FirstOrDefault(x => x.Id == assessment.GroupId);
        return group is not null &&
               group.Members.Any(member => string.Equals(member.UserId, user.Id, StringComparison.OrdinalIgnoreCase));
    }

    private PersonalTeachingGroup RequireAccessiblePersonalGroup(UserProfile user, string groupId)
    {
        var group = _state.TeachingGroups.FirstOrDefault(x => x.Id == groupId)
            ?? throw new InvalidOperationException("That teaching group could not be found.");

        if (!CanUserAccessPersonalGroup(user, groupId))
        {
            throw new InvalidOperationException("That teaching group is outside your personal workspace.");
        }

        return group;
    }

    private PersonalTeachingGroup RequireOwnedPersonalGroup(UserProfile lecturer, string groupId)
    {
        var group = RequireAccessiblePersonalGroup(lecturer, groupId);
        if (!string.Equals(group.LecturerUserId, lecturer.Id, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("You can only manage groups you created.");
        }

        return group;
    }

    private static PersonalAssessmentQuestion BuildAssessmentQuestion(CreatePersonalAssessmentQuestionRequest request)
    {
        ValidateRequired(request.Prompt, "Question prompt");
        ValidateRequired(request.OptionA, "Option A");
        ValidateRequired(request.OptionB, "Option B");
        ValidateRequired(request.OptionC, "Option C");
        ValidateRequired(request.OptionD, "Option D");

        var correctOption = NormalizeAnswerOption(request.CorrectOption);
        if (correctOption is not "A" and not "B" and not "C" and not "D")
        {
            throw new InvalidOperationException("Each question must have a valid correct option between A and D.");
        }

        return new PersonalAssessmentQuestion
        {
            Prompt = request.Prompt.Trim(),
            OptionA = request.OptionA.Trim(),
            OptionB = request.OptionB.Trim(),
            OptionC = request.OptionC.Trim(),
            OptionD = request.OptionD.Trim(),
            CorrectOption = correctOption,
            Explanation = request.Explanation.Trim()
        };
    }

    private static string NormalizeAnswerOption(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToUpperInvariant();

    private UserProfile ResolveMaterialOwner(UserProfile actor, string? lecturerUserId)
    {
        if (actor.Role == UserRole.Lecturer)
        {
            return actor;
        }

        if (string.IsNullOrWhiteSpace(lecturerUserId))
        {
            throw new InvalidOperationException("Select the lecturer who owns this course.");
        }

        return _state.Users.FirstOrDefault(x =>
                   x.Id == lecturerUserId &&
                   x.IsActive &&
                   x.Role == UserRole.Lecturer &&
                   IsTenantMatch(x.TenantId, actor.TenantId))
            ?? throw new InvalidOperationException("Selected lecturer account could not be found.");
    }

    private void EnsureMaterialScope(UserProfile actor, CourseMaterial material)
    {
        if (actor.Role != UserRole.Lecturer)
        {
            return;
        }

        if (!string.Equals(material.LecturerUserId, actor.Id, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Lecturers can only publish materials inside their own account.");
        }

        if (material.IsGeneralCourse)
        {
            return;
        }

        if (!string.Equals(material.Faculty, actor.Faculty, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(material.Department, actor.Department, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Lecturers can only manage materials inside their faculty and department.");
        }
    }

    private void EnsureMaterialManagementAccess(UserProfile actor, CourseMaterial material)
    {
        if (!IsTenantMatch(material.TenantId, actor.TenantId))
        {
            throw new InvalidOperationException("This course material belongs to another school workspace.");
        }

        if (actor.Role == UserRole.SuperAdmin)
        {
            return;
        }

        if (actor.Role == UserRole.Admin && actor.AdminModules.Contains(AdminModule.Library))
        {
            return;
        }

        if (actor.Role == UserRole.Lecturer && string.Equals(material.LecturerUserId, actor.Id, StringComparison.Ordinal))
        {
            return;
        }

        throw new InvalidOperationException("This course material is outside your permitted material scope.");
    }

    private void EnsureEnrollmentApprovalAccess(UserProfile actor, CourseMaterial material)
    {
        if (!IsTenantMatch(material.TenantId, actor.TenantId))
        {
            throw new InvalidOperationException("This enrollment request belongs to another school workspace.");
        }

        if (actor.Role == UserRole.SuperAdmin)
        {
            return;
        }

        if (actor.Role == UserRole.Admin && actor.AdminModules.Contains(AdminModule.Library))
        {
            return;
        }

        if (actor.Role == UserRole.Lecturer && string.Equals(material.LecturerUserId, actor.Id, StringComparison.Ordinal))
        {
            return;
        }

        throw new InvalidOperationException("This enrollment request is outside your permitted course scope.");
    }

    private static List<CourseMaterial> MigrateLegacyBooks(IEnumerable<LibraryBook> books)
    {
        return books.Select((book, index) => new CourseMaterial
        {
            CourseCode = index switch
            {
                0 => "CSC 312",
                1 => "CSC 401",
                2 => "MCM 402",
                _ => $"VGH {300 + index}"
            },
            Title = book.Title,
            LecturerName = book.Author,
            Faculty = index == 2 ? "Humanities" : "Computing and Digital Technologies",
            Department = index == 2 ? "Mass Communication" : "Computer Science",
            Level = index == 2 ? "400" : "300",
            MaterialType = "Lecture Note",
            Summary = book.Description,
            UploadedBy = "Seed Lecturer",
            PublishedAt = DateTimeOffset.UtcNow.AddDays(-(index + 1)),
            ResourceUrl = string.Empty
        }).ToList();
    }

    private UserProfile RequireStudent(string userId) =>
        _state.Users.FirstOrDefault(x => x.Id == userId && x.IsActive && x.Role == UserRole.Student)
        ?? throw new InvalidOperationException("Student account not found.");

    private UserProfile RequireMobileUser(string userId) =>
        _state.Users.FirstOrDefault(x =>
            x.Id == userId &&
            x.IsActive &&
            x.Role is UserRole.Student or UserRole.Lecturer)
        ?? throw new InvalidOperationException("Mobile account not found.");

    private UserProfile RequirePersonalMobileUser(string userId)
    {
        var user = RequireMobileUser(userId);
        var tenant = GetTenantById(user.TenantId);
        if (tenant?.Type != TenantType.Personal)
        {
            throw new InvalidOperationException("This action is available only inside a personal VirgoHub workspace.");
        }

        return user;
    }

    private UserProfile RequirePersonalLecturer(string userId)
    {
        var user = RequirePersonalMobileUser(userId);
        if (user.Role != UserRole.Lecturer)
        {
            throw new InvalidOperationException("Only personal lecturers can manage this workspace content.");
        }

        return user;
    }

    private UserProfile RequirePortalUser(string userId) =>
        _state.Users.FirstOrDefault(x =>
            x.Id == userId &&
            x.IsActive &&
            x.Role is UserRole.PlatformSuperAdmin or UserRole.SuperAdmin or UserRole.Admin or UserRole.Lecturer or UserRole.Porter)
        ?? throw new InvalidOperationException("Portal account not found.");

    private void EnsureComplaintAccess(UserProfile actor, ComplaintTicket complaint)
    {
        if (actor.Role == UserRole.SuperAdmin)
        {
            return;
        }

        if (actor.Role == UserRole.Admin && actor.AdminModules.Contains(AdminModule.Complaints))
        {
            return;
        }

        if (actor.Role != UserRole.Porter || actor.Gender != complaint.StudentGender)
        {
            throw new InvalidOperationException("This complaint is outside your permitted complaint scope.");
        }
    }

    private static List<AdminModule> NormalizeModules(IEnumerable<AdminModule>? modules) =>
        (modules ?? [])
            .Distinct()
            .OrderBy(x => x.ToString())
            .ToList();

    private void EnsureSchoolEmail(string email, string? tenantId = null, IEnumerable<string>? explicitDomains = null)
    {
        var allowedDomains = explicitDomains?.ToArray() ?? GetAllowedSchoolDomains(tenantId);
        if (!allowedDomains.Any(domain => email.EndsWith($"@{domain}", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("Use an approved VirgoHub school email address.");
        }
    }

    private string ResolveTenantIdForSchoolScope(string? actorUserId)
    {
        if (string.IsNullOrWhiteSpace(actorUserId))
        {
            return DefaultSchoolTenantId;
        }

        var actor = RequirePortalUser(actorUserId);
        return actor.Role == UserRole.PlatformSuperAdmin
            ? DefaultSchoolTenantId
            : actor.TenantId;
    }

    private UserProfile ResolveSchoolActor(string? actorUserId)
    {
        if (string.IsNullOrWhiteSpace(actorUserId))
        {
            return GetSuperAdmin(DefaultSchoolTenantId)
                   ?? throw new InvalidOperationException("A school super admin account is required.");
        }

        var actor = RequirePortalUser(actorUserId);
        if (actor.Role == UserRole.PlatformSuperAdmin)
        {
            throw new InvalidOperationException("Platform administrators manage schools from the platform console, not from school operations pages.");
        }

        return actor;
    }

    private UserProfile RequirePlatformSuperAdmin(string userId)
    {
        var actor = RequirePortalUser(userId);
        if (actor.Role != UserRole.PlatformSuperAdmin)
        {
            throw new InvalidOperationException("Only the platform super admin can manage school workspaces.");
        }

        return actor;
    }

    private TenantProfile? GetTenantById(string? tenantId)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return null;
        }

        return _state.Tenants.FirstOrDefault(x => x.IsActive && x.Id.Equals(tenantId, StringComparison.OrdinalIgnoreCase));
    }

    private void ValidateOtpRequestEmail(string email, SendOtpRequest request)
    {
        if (request.Mode == AppUseMode.School)
        {
            EnsureSchoolEmail(email, request.TenantId);
            return;
        }

        if (!email.Contains('@', StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Enter a valid email address for your personal VirgoHub workspace.");
        }
    }

    private UserProfile? ResolveOtpUser(string email, SendOtpRequest request) =>
        ResolveOtpUser(email, request.Mode, request.TenantId, request.RoleHint);

    private UserProfile? ResolveOtpUser(string email, AppUseMode mode, string? tenantId, UserRole? roleHint)
    {
        var normalizedTenantId = NormalizeTenantSelection(mode, tenantId, email);

        return _state.Users.FirstOrDefault(x =>
            x.IsActive &&
            x.Email.Equals(email, StringComparison.OrdinalIgnoreCase) &&
            IsTenantMatch(x.TenantId, normalizedTenantId) &&
            IsSupportedMobileRole(x.Role, mode, roleHint));
    }

    private UserProfile EnsurePersonalWorkspaceUser(string email, UserRole? roleHint)
    {
        var personalTenant = DefaultPersonalTenant
            ?? throw new InvalidOperationException("The VirgoHub personal workspace is not available yet.");
        var normalizedRole = roleHint == UserRole.Lecturer ? UserRole.Lecturer : UserRole.Student;

        var existingRoleMismatch = _state.Users.FirstOrDefault(x =>
            x.IsActive &&
            x.Email.Equals(email, StringComparison.OrdinalIgnoreCase) &&
            x.TenantId.Equals(personalTenant.Id, StringComparison.OrdinalIgnoreCase));

        if (existingRoleMismatch is not null)
        {
            throw new InvalidOperationException(existingRoleMismatch.Role == UserRole.Lecturer
                ? "This email already belongs to a personal lecturer workspace. Choose the lecturer path to continue."
                : "This email already belongs to a personal learner workspace. Choose the learner path to continue.");
        }

        var user = new UserProfile
        {
            TenantId = personalTenant.Id,
            TenantName = personalTenant.Name,
            FullName = BuildPersonalDisplayName(email),
            Email = email,
            Faculty = "VirgoHub Personal",
            Department = normalizedRole == UserRole.Lecturer ? "Mentor Workspace" : "Learner Workspace",
            Level = normalizedRole == UserRole.Lecturer ? "Tutor Track" : "Learner Track",
            MatricNumber = string.Empty,
            Hostel = string.Empty,
            Gender = UserGender.Other,
            Role = normalizedRole,
            IsActive = true
        };

        _state.Users.Add(user);
        AddNotification(
            user.Id,
            normalizedRole == UserRole.Lecturer ? "Mentor workspace ready" : "Personal workspace ready",
            normalizedRole == UserRole.Lecturer
                ? "Your personal lecturer workspace is ready for groups, materials, and assessments."
                : "Your personal learning workspace is ready for materials, reminders, and guided study.",
            "Account");
        AddActivity(
            user.Id,
            normalizedRole == UserRole.Lecturer ? "Mentor profile created" : "Personal profile created",
            normalizedRole == UserRole.Lecturer
                ? "Your lecturer workspace is ready to start teaching groups and share materials."
                : "Your learner workspace is ready to join groups and access study materials.",
            "Account");

        return user;
    }

    private string NormalizeTenantSelection(AppUseMode mode, string? tenantId, string email)
    {
        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            return tenantId.Trim();
        }

        if (mode == AppUseMode.Personal)
        {
            return DefaultPersonalTenantId;
        }

        return ResolveSchoolTenantByEmail(email)?.Id ?? DefaultSchoolTenantId;
    }

    private TenantProfile? ResolveSchoolTenantByEmail(string email)
    {
        var domain = GetEmailDomain(email);
        return _state.Tenants.FirstOrDefault(x =>
            x.IsActive &&
            x.Type == TenantType.School &&
            x.AllowedDomains.Any(allowed => allowed.Equals(domain, StringComparison.OrdinalIgnoreCase)));
    }

    private static string BuildPersonalDisplayName(string email)
    {
        var localPart = email.Split('@', 2)[0]
            .Replace('.', ' ')
            .Replace('_', ' ')
            .Replace('-', ' ');

        var words = localPart
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(word => char.ToUpperInvariant(word[0]) + word[1..].ToLowerInvariant());

        var displayName = string.Join(' ', words);
        return string.IsNullOrWhiteSpace(displayName) ? "VirgoHub User" : displayName;
    }

    private string[] GetAllowedSchoolDomains(string? tenantId = null)
    {
        var schoolTenants = !string.IsNullOrWhiteSpace(tenantId)
            ? _state.Tenants.Where(x => x.IsActive && x.Type == TenantType.School && x.Id.Equals(tenantId, StringComparison.OrdinalIgnoreCase))
            : _state.Tenants.Where(x => x.IsActive && x.Type == TenantType.School);

        var domains = schoolTenants
            .SelectMany(x => x.AllowedDomains)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (domains.Length > 0)
        {
            return domains;
        }

        var configured = _options.SchoolEmailDomain.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(configured))
        {
            return ["virgohub.edu", "student.virgohub.edu"];
        }

        if (configured.StartsWith("student.", StringComparison.OrdinalIgnoreCase))
        {
            return [configured, configured["student.".Length..]];
        }

        return [configured, $"student.{configured}"];
    }

    private static bool IsSupportedMobileRole(UserRole role, AppUseMode mode, UserRole? roleHint) =>
        mode switch
        {
            AppUseMode.Personal => roleHint == UserRole.Lecturer
                ? role == UserRole.Lecturer
                : role is UserRole.Student or UserRole.Lecturer,
            _ => role == UserRole.Student
        };

    private static bool IsTenantMatch(string? entityTenantId, string? tenantId) =>
        string.IsNullOrWhiteSpace(tenantId) ||
        string.Equals(entityTenantId ?? string.Empty, tenantId, StringComparison.OrdinalIgnoreCase);

    private static string NormalizeDomain(string value) =>
        value.Trim().ToLowerInvariant().Replace("@", string.Empty);

    private static List<string> NormalizeSchoolDomains(string primaryDomain, string? additionalDomainsText)
    {
        var additionalDomains = (additionalDomainsText ?? string.Empty)
            .Split([',', '\n', '\r', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizeDomain);

        return additionalDomains
            .Prepend(NormalizeDomain(primaryDomain))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToList();
    }

    private static string BuildTenantSlug(string schoolName)
    {
        var slug = new string(schoolName
            .Trim()
            .ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
            .ToArray());

        while (slug.Contains("--", StringComparison.Ordinal))
        {
            slug = slug.Replace("--", "-", StringComparison.Ordinal);
        }

        return slug.Trim('-');
    }

    private static string NormalizeAccentColor(string? color)
    {
        var normalized = color?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? "#25A9F5" : normalized;
    }

    private static string GetEmailDomain(string email)
    {
        var index = email.IndexOf('@');
        return index >= 0 && index < email.Length - 1
            ? email[(index + 1)..].Trim().ToLowerInvariant()
            : string.Empty;
    }

    private static string BuildInviteCode(string courseCode)
    {
        var prefix = new string(courseCode
            .ToUpperInvariant()
            .Where(char.IsLetterOrDigit)
            .Take(4)
            .ToArray());

        if (string.IsNullOrWhiteSpace(prefix))
        {
            prefix = "VGH";
        }

        var suffix = RandomNumberGenerator.GetInt32(1000, 9999);
        return $"{prefix}-{suffix}";
    }

    private static void ValidateRequired(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{fieldName} is required.");
        }
    }

    private bool IsElectionOpen(Election election) =>
        election.IsActive &&
        election.OpensAt <= DateTimeOffset.UtcNow &&
        election.ClosesAt >= DateTimeOffset.UtcNow;

    private Election BuildElectionView(Election election, string? userId = null)
    {
        var voteLookup = _state.Votes
            .Where(x => x.ElectionId == election.Id)
            .GroupBy(x => x.CandidateId)
            .ToDictionary(x => x.Key, x => x.Count());

        var totalVotes = voteLookup.Values.Sum();
        var now = DateTimeOffset.UtcNow;

        return new Election
        {
            Id = election.Id,
            TenantId = election.TenantId,
            Title = election.Title,
            Position = election.Position,
            AudienceFaculty = election.AudienceFaculty,
            AudienceDepartments = NormalizeAudienceDepartments(election.AudienceDepartments),
            OpensAt = election.OpensAt,
            ClosesAt = election.ClosesAt,
            IsActive = election.IsActive,
            TotalVotes = totalVotes,
            HasVoted = !string.IsNullOrWhiteSpace(userId) && _state.Votes.Any(x => x.ElectionId == election.Id && x.UserId == userId),
            IsOpen = election.IsActive && election.OpensAt <= now && election.ClosesAt >= now,
            IsClosed = election.IsActive && election.ClosesAt < now,
            StatusLabel = election.IsActive && election.OpensAt <= now && election.ClosesAt >= now
                ? "Open"
                : election.IsActive && election.ClosesAt < now
                    ? "Closed"
                    : "Scheduled",
            Candidates = election.Candidates
                .Select(candidate => new Candidate
                {
                    Id = candidate.Id,
                    Name = candidate.Name,
                    Department = candidate.Department,
                    Position = candidate.Position,
                    Manifesto = candidate.Manifesto,
                    VoteCount = voteLookup.GetValueOrDefault(candidate.Id)
                })
                .OrderByDescending(x => x.VoteCount)
                .ThenBy(x => x.Name)
                .ToList()
        };
    }

    private static List<Candidate> BuildCandidates(IEnumerable<CandidateInput> candidateInputs, string fallbackNames, string position)
    {
        var expanded = candidateInputs
            .Where(x => !string.IsNullOrWhiteSpace(x.Name))
            .Select(x => new Candidate
            {
                Name = x.Name.Trim(),
                Department = x.Department.Trim(),
                Manifesto = x.Manifesto.Trim(),
                Position = position
            })
            .ToList();

        if (expanded.Count == 0 && !string.IsNullOrWhiteSpace(fallbackNames))
        {
            expanded = fallbackNames
                .Split([',', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(name => new Candidate
                {
                    Name = name,
                    Position = position
                })
                .ToList();
        }

        return expanded
            .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .ToList();
    }

    private void AddNotification(string userId, string title, string message, string category)
    {
        var tenantId = _state.Users.FirstOrDefault(x => x.Id == userId)?.TenantId ?? DefaultSchoolTenantId;
        _state.Notifications.Add(new AppNotification
        {
            TenantId = tenantId,
            UserId = userId,
            Title = title,
            Message = message,
            Category = category,
            CreatedAt = DateTimeOffset.UtcNow
        });
    }

    private void AddActivity(string userId, string title, string description, string category)
    {
        var tenantId = _state.Users.FirstOrDefault(x => x.Id == userId)?.TenantId ?? DefaultSchoolTenantId;
        _state.Activities.Add(new ActivityEntry
        {
            TenantId = tenantId,
            UserId = userId,
            Title = title,
            Description = description,
            Category = category,
            CreatedAt = DateTimeOffset.UtcNow
        });
    }

    private static string NormalizeAudienceFaculty(string? faculty) =>
        string.IsNullOrWhiteSpace(faculty) ? string.Empty : faculty.Trim();

    private static List<string> NormalizeAudienceDepartments(IEnumerable<string>? departments) =>
        (departments ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToList();

    private static string InferFacultyFromDepartment(string? department)
    {
        var normalized = department?.Trim() ?? string.Empty;
        return normalized switch
        {
            "Computer Science" or "Software Engineering" or "Information Technology" => "Computing and Digital Technologies",
            "Mass Communication" or "English" or "History" => "Humanities",
            _ => "General Studies"
        };
    }

    private static void ValidateAudienceSelection(string audienceFaculty, IReadOnlyCollection<string> audienceDepartments, string audienceType)
    {
        if (audienceDepartments.Count > 0 && string.IsNullOrWhiteSpace(audienceFaculty))
        {
            throw new InvalidOperationException($"Choose a faculty before assigning departments for this {audienceType}.");
        }
    }

    private static bool CanStudentAccessAudience(UserProfile student, string? audienceFaculty, IEnumerable<string>? audienceDepartments)
    {
        var normalizedFaculty = NormalizeAudienceFaculty(audienceFaculty);
        var normalizedDepartments = NormalizeAudienceDepartments(audienceDepartments);

        if (string.IsNullOrWhiteSpace(normalizedFaculty))
        {
            return true;
        }

        if (!string.Equals(student.Faculty.Trim(), normalizedFaculty, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return normalizedDepartments.Count == 0 ||
               normalizedDepartments.Contains(student.Department.Trim(), StringComparer.OrdinalIgnoreCase);
    }

    private async Task<(bool IsConfigured, bool Delivered, string? ErrorMessage)> TrySendMailAsync(string email, string code)
    {
        if (!HasConfiguredMailSettings())
        {
            _logger.LogInformation("SMTP is not configured. OTP for {Email}: {Code}", email, code);
            return (false, false, null);
        }

        try
        {
            using var message = new MailMessage(_mailSettings.FromAddress.Trim(), email)
            {
                Subject = "VirgoHub sign-in code",
                Body = $"Your VirgoHub one-time login code is {code}. It expires in {_options.OtpMinutesToLive} minutes."
            };

            using var client = new SmtpClient(_mailSettings.Host, _mailSettings.Port)
            {
                EnableSsl = _mailSettings.EnableSsl,
                UseDefaultCredentials = false
            };

            if (!string.IsNullOrWhiteSpace(_mailSettings.Username))
            {
                client.Credentials = new NetworkCredential(
                    _mailSettings.Username.Trim(),
                    NormalizeMailSecret(_mailSettings.Password));
            }

            await client.SendMailAsync(message);
            return (true, true, null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send OTP email to {Email}.", email);
            return (true, false, "VirgoHub could not send the sign-in code email. Check your Gmail address, app password, and SMTP settings.");
        }
    }

    private bool HasConfiguredMailSettings()
    {
        var host = _mailSettings.Host?.Trim() ?? string.Empty;
        var fromAddress = _mailSettings.FromAddress?.Trim() ?? string.Empty;
        var username = _mailSettings.Username?.Trim() ?? string.Empty;
        var password = NormalizeMailSecret(_mailSettings.Password);

        if (string.IsNullOrWhiteSpace(host) ||
            string.IsNullOrWhiteSpace(fromAddress) ||
            string.IsNullOrWhiteSpace(username) ||
            string.IsNullOrWhiteSpace(password))
        {
            return false;
        }

        if (string.Equals(fromAddress, "yourgmailaddress@gmail.com", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(username, "yourgmailaddress@gmail.com", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(password, "your-gmail-app-password", StringComparison.Ordinal))
        {
            return false;
        }

        return true;
    }

    private static string NormalizeMailSecret(string? value) =>
        (value ?? string.Empty).Trim().Replace(" ", string.Empty, StringComparison.Ordinal);

    private AppState LoadState()
    {
        if (!File.Exists(_statePath))
        {
            var seeded = CreateSeedState();
            File.WriteAllText(_statePath, JsonSerializer.Serialize(seeded, _jsonOptions));
            return seeded;
        }

        try
        {
            var json = File.ReadAllText(_statePath);
            var state = JsonSerializer.Deserialize<AppState>(json, _jsonOptions) ?? CreateSeedState();
            return NormalizeState(state);
        }
        catch
        {
            return CreateSeedState();
        }
    }

    private AppState NormalizeState(AppState state)
    {
        if (!state.Users.Any(x => x.Role == UserRole.SuperAdmin))
        {
            return CreateSeedState();
        }

        foreach (var user in state.Users)
        {
            if (!string.IsNullOrWhiteSpace(user.Email))
            {
                user.Email = NormalizeSeedEmail(user.Email);
            }

            if (ShouldMarkAsSeedAccount(user))
            {
                user.IsSeedAccount = true;
            }
        }

        var schoolTenant = EnsureDefaultSchoolTenant(state);
        var personalTenant = EnsureDefaultPersonalTenant(state);
        EnsureSchoolTenantAssignments(state, schoolTenant);
        EnsurePlatformSuperAdmin(state);
        EnsurePersonalSeedState(state, personalTenant);

        foreach (var portalUser in state.Users.Where(x => x.Role is UserRole.PlatformSuperAdmin or UserRole.SuperAdmin or UserRole.Admin or UserRole.Porter or UserRole.Lecturer))
        {
            if (!string.IsNullOrWhiteSpace(portalUser.PortalPasswordHash))
            {
                continue;
            }

            var fallbackPassword = portalUser.Role is UserRole.PlatformSuperAdmin or UserRole.SuperAdmin or UserRole.Admin
                ? DefaultSuperAdminPassword
                : portalUser.Role == UserRole.Lecturer
                    ? DefaultLecturerPassword
                : portalUser.Gender == UserGender.Female
                    ? DefaultFemalePorterPassword
                    : DefaultMalePorterPassword;

            ApplyPassword(portalUser, fallbackPassword);
        }

        state.Sessions.RemoveAll(x => x.ExpiresAt < DateTimeOffset.UtcNow);
        state.OtpChallenges.RemoveAll(x => x.ExpiresAt < DateTimeOffset.UtcNow || x.Consumed);

        if (state.Materials.Count == 0 && state.Books.Count > 0)
        {
            state.Materials = MigrateLegacyBooks(state.Books);
        }

        foreach (var campusEvent in state.Events)
        {
            campusEvent.AudienceFaculty = NormalizeAudienceFaculty(campusEvent.AudienceFaculty);
            campusEvent.AudienceDepartments = NormalizeAudienceDepartments(campusEvent.AudienceDepartments);
        }

        foreach (var election in state.Elections)
        {
            election.AudienceFaculty = NormalizeAudienceFaculty(election.AudienceFaculty);
            election.AudienceDepartments = NormalizeAudienceDepartments(election.AudienceDepartments);
        }

        var lecturerAina = EnsureSeedLecturer(state, "Dr. Robert Aina", "dr.aina", "LECT-CMP-201", "Computing and Digital Technologies", "Computer Science");
        var lecturerSolanke = EnsureSeedLecturer(state, "Prof. Miriam Solanke", "prof.solanke", "LECT-CMP-202", "Computing and Digital Technologies", "Computer Science");
        var lecturerBassey = EnsureSeedLecturer(state, "Mrs. Linda Bassey", "mrs.bassey", "LECT-HUM-301", "Humanities", "Mass Communication");

        foreach (var material in state.Materials)
        {
            material.Faculty = material.IsGeneralCourse
                ? "General Studies"
                : string.IsNullOrWhiteSpace(material.Faculty)
                    ? InferFacultyFromDepartment(material.Department)
                    : material.Faculty.Trim();
            material.Department = string.IsNullOrWhiteSpace(material.Department)
                ? "All Departments"
                : material.Department.Trim();
            material.Level = string.IsNullOrWhiteSpace(material.Level)
                ? "All Levels"
                : material.Level.Trim();
            material.UploadedBy = string.IsNullOrWhiteSpace(material.UploadedBy)
                ? "VirgoHub"
                : material.UploadedBy.Trim();

            if (string.IsNullOrWhiteSpace(material.LecturerUserId))
            {
                var owner = material.LecturerName.Trim() switch
                {
                    "Dr. Robert Aina" => lecturerAina,
                    "Prof. Miriam Solanke" => lecturerSolanke,
                    "Mrs. Linda Bassey" => lecturerBassey,
                    _ => null
                };

                if (owner is not null)
                {
                    material.LecturerUserId = owner.Id;
                    material.LecturerName = owner.FullName;
                }
            }
        }

        foreach (var enrollment in state.Enrollments)
        {
            var student = state.Users.FirstOrDefault(x => x.Id == enrollment.UserId && x.IsActive);
            enrollment.Faculty = string.IsNullOrWhiteSpace(enrollment.Faculty)
                ? student?.Faculty ?? string.Empty
                : enrollment.Faculty.Trim();
            enrollment.Department = string.IsNullOrWhiteSpace(enrollment.Department)
                ? student?.Department ?? string.Empty
                : enrollment.Department.Trim();
            enrollment.Level = string.IsNullOrWhiteSpace(enrollment.Level)
                ? student?.Level ?? string.Empty
                : enrollment.Level.Trim();

            if (enrollment.Status == CourseEnrollmentStatus.Pending && !enrollment.RequiresManualApproval && enrollment.ApprovedAt is null)
            {
                enrollment.Status = CourseEnrollmentStatus.Approved;
                enrollment.ApprovedAt = enrollment.EnrolledAt;
            }
        }

        foreach (var booking in state.Bookings)
        {
            if (booking.Status == SportsBookingStatus.PendingApproval && booking.ApprovedAt is null)
            {
                booking.Status = SportsBookingStatus.Approved;
                booking.ApprovedAt = booking.CreatedAt;
            }
        }

        if (state.Notifications.Count == 0 && state.Activities.Count == 0)
        {
            foreach (var student in state.Users.Where(x => x.Role == UserRole.Student && x.IsActive))
            {
                AddNotificationForSeed(state, student.Id, "Welcome to VirgoHub", "Your student account is ready. Use your school email to sign in securely.", "Account");
                AddActivityForSeed(state, student.Id, "Profile created", "Your student profile is active and ready for secure sign in.", "Account");
            }
        }

        return state;
    }

    private UserProfile EnsureSeedLecturer(AppState state, string fullName, string emailAlias, string staffId, string faculty, string department)
    {
        var existing = state.Users.FirstOrDefault(x =>
            x.IsActive &&
            x.Role == UserRole.Lecturer &&
            (x.Email.Equals($"{emailAlias}@{_options.SchoolEmailDomain}", StringComparison.OrdinalIgnoreCase) ||
             x.FullName.Equals(fullName, StringComparison.OrdinalIgnoreCase)));

        if (existing is not null)
        {
            var schoolTenant = state.Tenants.First(x => x.IsActive && x.Type == TenantType.School);
            existing.Email = $"{emailAlias}@{_options.SchoolEmailDomain}";
            existing.StaffId = string.IsNullOrWhiteSpace(existing.StaffId) ? staffId : existing.StaffId;
            existing.Faculty = string.IsNullOrWhiteSpace(existing.Faculty) ? faculty : existing.Faculty;
            existing.Department = string.IsNullOrWhiteSpace(existing.Department) ? department : existing.Department;
            existing.Level = "Staff";
            existing.Hostel = "N/A";
            existing.TenantId = schoolTenant.Id;
            existing.TenantName = schoolTenant.Name;
            existing.IsSeedAccount = true;
            return existing;
        }

        var defaultSchoolTenant = state.Tenants.First(x => x.IsActive && x.Type == TenantType.School);
        var lecturer = new UserProfile
        {
            FullName = fullName,
            Email = $"{emailAlias}@{_options.SchoolEmailDomain}",
            StaffId = staffId,
            Faculty = faculty,
            Department = department,
            Level = "Staff",
            Hostel = "N/A",
            Gender = UserGender.Other,
            Role = UserRole.Lecturer,
            TenantId = defaultSchoolTenant.Id,
            TenantName = defaultSchoolTenant.Name,
            IsActive = true,
            IsSeedAccount = true,
            CreatedAt = DateTimeOffset.UtcNow
        };
        ApplyPassword(lecturer, DefaultLecturerPassword);
        state.Users.Add(lecturer);
        return lecturer;
    }

    private TenantProfile EnsureDefaultSchoolTenant(AppState state)
    {
        var existing = state.Tenants.FirstOrDefault(x => x.IsActive && x.Type == TenantType.School);
        if (existing is not null)
        {
            existing.Name = string.IsNullOrWhiteSpace(existing.Name) ? _options.DefaultSchoolName : existing.Name;
            existing.Slug = string.IsNullOrWhiteSpace(existing.Slug) ? DefaultSchoolTenantSlug : existing.Slug;
            existing.PrimaryDomain = string.IsNullOrWhiteSpace(existing.PrimaryDomain) ? _options.SchoolEmailDomain : existing.PrimaryDomain.Trim().ToLowerInvariant();
            existing.AllowedDomains = GetAllowedDomainsForSeed(existing.PrimaryDomain);
            existing.Description = string.IsNullOrWhiteSpace(existing.Description)
                ? "Full campus operations for events, voting, sports, complaints, and course materials."
                : existing.Description;
            existing.AccentColor = string.IsNullOrWhiteSpace(existing.AccentColor) ? "#25A9F5" : existing.AccentColor;
            return existing;
        }

        var tenant = new TenantProfile
        {
            Name = _options.DefaultSchoolName,
            Slug = DefaultSchoolTenantSlug,
            Type = TenantType.School,
            PrimaryDomain = _options.SchoolEmailDomain.Trim().ToLowerInvariant(),
            AllowedDomains = GetAllowedDomainsForSeed(_options.SchoolEmailDomain),
            Description = "Full campus operations for events, voting, sports, complaints, and course materials.",
            AccentColor = "#25A9F5"
        };
        state.Tenants.Add(tenant);
        return tenant;
    }

    private TenantProfile EnsureDefaultPersonalTenant(AppState state)
    {
        var existing = state.Tenants.FirstOrDefault(x => x.IsActive && x.Type == TenantType.Personal);
        if (existing is not null)
        {
            existing.Name = string.IsNullOrWhiteSpace(existing.Name) ? "VirgoHub Personal Workspace" : existing.Name;
            existing.Slug = string.IsNullOrWhiteSpace(existing.Slug) ? DefaultPersonalTenantSlug : existing.Slug;
            existing.Description = string.IsNullOrWhiteSpace(existing.Description)
                ? "Personal tutoring, shared course materials, reminders, and guided academic groups."
                : existing.Description;
            existing.AccentColor = string.IsNullOrWhiteSpace(existing.AccentColor) ? "#0F4BA8" : existing.AccentColor;
            return existing;
        }

        var tenant = new TenantProfile
        {
            Name = "VirgoHub Personal Workspace",
            Slug = DefaultPersonalTenantSlug,
            Type = TenantType.Personal,
            PrimaryDomain = "gmail.com",
            AllowedDomains = ["gmail.com", "outlook.com", "yahoo.com", "virgohub.edu"],
            Description = "Personal tutoring, shared course materials, reminders, and guided academic groups.",
            AccentColor = "#0F4BA8"
        };
        state.Tenants.Add(tenant);
        return tenant;
    }

    private void EnsureSchoolTenantAssignments(AppState state, TenantProfile schoolTenant)
    {
        foreach (var user in state.Users.Where(x => string.IsNullOrWhiteSpace(x.TenantId)))
        {
            user.TenantId = schoolTenant.Id;
            user.TenantName = schoolTenant.Name;
        }

        foreach (var user in state.Users.Where(x => x.TenantId == schoolTenant.Id))
        {
            user.TenantName = schoolTenant.Name;
        }

        foreach (var campusEvent in state.Events.Where(x => string.IsNullOrWhiteSpace(x.TenantId)))
        {
            campusEvent.TenantId = schoolTenant.Id;
        }

        foreach (var facility in state.Facilities.Where(x => string.IsNullOrWhiteSpace(x.TenantId)))
        {
            facility.TenantId = schoolTenant.Id;
        }

        foreach (var booking in state.Bookings.Where(x => string.IsNullOrWhiteSpace(x.TenantId)))
        {
            booking.TenantId = state.Users.FirstOrDefault(x => x.Id == booking.UserId)?.TenantId ?? schoolTenant.Id;
        }

        foreach (var complaint in state.Complaints.Where(x => string.IsNullOrWhiteSpace(x.TenantId)))
        {
            complaint.TenantId = state.Users.FirstOrDefault(x => x.Id == complaint.UserId)?.TenantId ?? schoolTenant.Id;
        }

        foreach (var election in state.Elections.Where(x => string.IsNullOrWhiteSpace(x.TenantId)))
        {
            election.TenantId = schoolTenant.Id;
        }

        foreach (var vote in state.Votes.Where(x => string.IsNullOrWhiteSpace(x.TenantId)))
        {
            vote.TenantId = state.Elections.FirstOrDefault(x => x.Id == vote.ElectionId)?.TenantId ?? schoolTenant.Id;
        }

        foreach (var material in state.Materials.Where(x => string.IsNullOrWhiteSpace(x.TenantId)))
        {
            material.TenantId = state.Users.FirstOrDefault(x => x.Id == material.LecturerUserId)?.TenantId ?? schoolTenant.Id;
        }

        foreach (var enrollment in state.Enrollments.Where(x => string.IsNullOrWhiteSpace(x.TenantId)))
        {
            enrollment.TenantId = state.Users.FirstOrDefault(x => x.Id == enrollment.UserId)?.TenantId ?? schoolTenant.Id;
        }

        foreach (var group in state.TeachingGroups.Where(x => string.IsNullOrWhiteSpace(x.TenantId)))
        {
            group.TenantId = state.Users.FirstOrDefault(x => x.Id == group.LecturerUserId)?.TenantId ?? schoolTenant.Id;
        }

        foreach (var assessment in state.PersonalAssessments.Where(x => string.IsNullOrWhiteSpace(x.TenantId)))
        {
            assessment.TenantId = state.TeachingGroups.FirstOrDefault(x => x.Id == assessment.GroupId)?.TenantId ??
                                  state.Tenants.FirstOrDefault(x => x.IsActive && x.Type == TenantType.Personal)?.Id ??
                                  schoolTenant.Id;
        }

        foreach (var submission in state.PersonalAssessmentSubmissions.Where(x => string.IsNullOrWhiteSpace(x.TenantId)))
        {
            submission.TenantId = state.Users.FirstOrDefault(x => x.Id == submission.UserId)?.TenantId ??
                                  state.Tenants.FirstOrDefault(x => x.IsActive && x.Type == TenantType.Personal)?.Id ??
                                  schoolTenant.Id;
        }

        foreach (var notification in state.Notifications.Where(x => string.IsNullOrWhiteSpace(x.TenantId)))
        {
            notification.TenantId = state.Users.FirstOrDefault(x => x.Id == notification.UserId)?.TenantId ?? schoolTenant.Id;
        }

        foreach (var activity in state.Activities.Where(x => string.IsNullOrWhiteSpace(x.TenantId)))
        {
            activity.TenantId = state.Users.FirstOrDefault(x => x.Id == activity.UserId)?.TenantId ?? schoolTenant.Id;
        }
    }

    private void EnsurePersonalSeedState(AppState state, TenantProfile personalTenant)
    {
        var lecturer = state.Users.FirstOrDefault(x =>
            x.IsActive &&
            x.TenantId == personalTenant.Id &&
            x.Role == UserRole.Lecturer);

        if (lecturer is null)
        {
            lecturer = new UserProfile
            {
                FullName = "VirgoHub Mentor Demo",
                Email = "mentor.demo@gmail.com",
                StaffId = "PERS-LECT-001",
                Department = "Personal Mentoring",
                Faculty = "VirgoHub Personal",
                Level = "Staff",
                Hostel = "N/A",
                Gender = UserGender.Other,
                Role = UserRole.Lecturer,
                TenantId = personalTenant.Id,
                TenantName = personalTenant.Name,
                IsActive = true,
                IsSeedAccount = true,
                CreatedAt = DateTimeOffset.UtcNow
            };
            ApplyPassword(lecturer, DefaultLecturerPassword);
            state.Users.Add(lecturer);
        }
        else
        {
            lecturer.TenantName = personalTenant.Name;
        }

        var learner = state.Users.FirstOrDefault(x =>
            x.IsActive &&
            x.TenantId == personalTenant.Id &&
            x.Role == UserRole.Student);

        if (learner is null)
        {
            learner = new UserProfile
            {
                FullName = "VirgoHub Learner Demo",
                Email = "learner.demo@gmail.com",
                Level = "Tutoring Track",
                Department = "Personal Learning Group",
                Faculty = "VirgoHub Personal",
                MatricNumber = "PERS/LEARN/001",
                Hostel = "N/A",
                Gender = UserGender.Other,
                Role = UserRole.Student,
                TenantId = personalTenant.Id,
                TenantName = personalTenant.Name,
                IsActive = true,
                IsSeedAccount = true,
                CreatedAt = DateTimeOffset.UtcNow
            };
            state.Users.Add(learner);
        }
        else
        {
            learner.TenantName = personalTenant.Name;
        }

        if (!state.Events.Any(x => x.TenantId == personalTenant.Id))
        {
            state.Events.Add(new CampusEvent
            {
                TenantId = personalTenant.Id,
                Title = "Revision Sprint Reminder",
                Category = "Event",
                Venue = "Personal Learning Space",
                Description = "A guided reminder for the next tutoring sprint, revision target, and group check-in.",
                StartsAt = DateTimeOffset.UtcNow.AddDays(1).Date.AddHours(19),
                CreatedBy = lecturer.FullName
            });
        }

        if (!state.Materials.Any(x => x.TenantId == personalTenant.Id))
        {
            state.Materials.Add(new CourseMaterial
            {
                TenantId = personalTenant.Id,
                CourseCode = "PVT 101",
                Title = "Personal Study Blueprint",
                LecturerName = lecturer.FullName,
                LecturerUserId = lecturer.Id,
                Faculty = "VirgoHub Personal",
                Department = "Personal Learning Group",
                Level = "Tutoring Track",
                IsGeneralCourse = true,
                MaterialType = "Course Outline",
                ResourceUrl = "https://virgohub.app/personal/pvt101",
                Summary = "A guided personal learning outline with checkpoints, reminders, and focused revision tasks.",
                UploadedBy = lecturer.FullName
            });
        }

        if (!state.Notifications.Any(x => x.TenantId == personalTenant.Id && x.UserId == learner.Id))
        {
            AddNotificationForSeed(state, learner.Id, "Personal workspace ready", "Your personal VirgoHub workspace is ready for guided course access and reminders.", "Account");
        }

        if (!state.Notifications.Any(x => x.TenantId == personalTenant.Id && x.UserId == lecturer.Id))
        {
            AddNotificationForSeed(state, lecturer.Id, "Mentor workspace ready", "Your personal lecturer workspace is ready for teaching groups and course planning.", "Account");
        }

        if (!state.Activities.Any(x => x.TenantId == personalTenant.Id && x.UserId == learner.Id))
        {
            AddActivityForSeed(state, learner.Id, "Personal mode activated", "Your personal VirgoHub learning space is prepared for materials and reminders.", "Account");
        }

        if (!state.Activities.Any(x => x.TenantId == personalTenant.Id && x.UserId == lecturer.Id))
        {
            AddActivityForSeed(state, lecturer.Id, "Mentor dashboard prepared", "Your personal VirgoHub teaching workspace is prepared for future group setup.", "Account");
        }

        if (!state.TeachingGroups.Any(x => x.TenantId == personalTenant.Id))
        {
            state.TeachingGroups.Add(new PersonalTeachingGroup
            {
                TenantId = personalTenant.Id,
                LecturerUserId = lecturer.Id,
                LecturerName = lecturer.FullName,
                Title = "Foundations Mentoring Circle",
                CourseCode = "PVT 101",
                Summary = "A starter personal teaching group for shared outlines, reminders, and guided progress.",
                InviteCode = "PVT1-2468",
                InviteLink = "virgohub://personal/join/PVT1-2468",
                Members =
                [
                    new TeachingGroupMember
                    {
                        UserId = learner.Id,
                        FullName = learner.FullName,
                        Email = learner.Email
                    }
                ]
            });
        }

        var demoGroup = state.TeachingGroups.FirstOrDefault(x =>
            x.TenantId == personalTenant.Id &&
            x.CourseCode.Equals("PVT 101", StringComparison.OrdinalIgnoreCase));

        if (demoGroup is not null && !state.PersonalAssessments.Any(x => x.TenantId == personalTenant.Id))
        {
            state.PersonalAssessments.Add(new PersonalAssessment
            {
                TenantId = personalTenant.Id,
                GroupId = demoGroup.Id,
                GroupTitle = demoGroup.Title,
                CourseCode = demoGroup.CourseCode,
                LecturerUserId = lecturer.Id,
                LecturerName = lecturer.FullName,
                Title = "Foundations Checkpoint Test",
                Summary = "A short personal test to confirm the learner understands the first tutoring outline and revision tasks.",
                Type = PersonalAssessmentType.Test,
                TimeLimitMinutes = 15,
                OpensAt = DateTimeOffset.UtcNow.AddHours(-2),
                DueAt = DateTimeOffset.UtcNow.AddDays(3),
                Questions =
                [
                    new PersonalAssessmentQuestion
                    {
                        Prompt = "Which study approach best matches the personal blueprint for PVT 101?",
                        OptionA = "One long reading session without checkpoints",
                        OptionB = "Short focused sprints with revision checkpoints",
                        OptionC = "Only video lessons with no note-taking",
                        OptionD = "Waiting until the final week to revise",
                        CorrectOption = "B",
                        Explanation = "The blueprint is designed around focused sprints and structured check-ins."
                    },
                    new PersonalAssessmentQuestion
                    {
                        Prompt = "What should a learner do after joining a personal teaching group?",
                        OptionA = "Ignore the group reminders",
                        OptionB = "Submit unrelated materials",
                        OptionC = "Review the outline and follow the shared milestones",
                        OptionD = "Leave the group immediately",
                        CorrectOption = "C",
                        Explanation = "The group exists to guide the learner through the shared outline and milestones."
                    }
                ]
            });
        }
    }

    private void EnsurePlatformSuperAdmin(AppState state)
    {
        var platformAdmin = state.Users.FirstOrDefault(x => x.IsActive && x.Role == UserRole.PlatformSuperAdmin);
        if (platformAdmin is null)
        {
            platformAdmin = new UserProfile
            {
                FullName = "VirgoHub Platform Admin",
                Email = $"platform@{_options.SchoolEmailDomain}".ToLowerInvariant(),
                StaffId = "VGH-PLATFORM-001",
                Department = "Platform Operations",
                Faculty = "VirgoHub",
                Level = "Staff",
                Hostel = "N/A",
                Gender = UserGender.Other,
                Role = UserRole.PlatformSuperAdmin,
                TenantId = string.Empty,
                TenantName = "VirgoHub Platform",
                IsActive = true,
                IsSeedAccount = true,
                CreatedAt = DateTimeOffset.UtcNow
            };

            ApplyPassword(platformAdmin, DefaultSuperAdminPassword);
            state.Users.Add(platformAdmin);
            return;
        }

        platformAdmin.Email = $"platform@{_options.SchoolEmailDomain}".ToLowerInvariant();
        platformAdmin.StaffId = string.IsNullOrWhiteSpace(platformAdmin.StaffId) ? "VGH-PLATFORM-001" : platformAdmin.StaffId;
        platformAdmin.Department = string.IsNullOrWhiteSpace(platformAdmin.Department) ? "Platform Operations" : platformAdmin.Department;
        platformAdmin.Faculty = string.IsNullOrWhiteSpace(platformAdmin.Faculty) ? "VirgoHub" : platformAdmin.Faculty;
        platformAdmin.Level = string.IsNullOrWhiteSpace(platformAdmin.Level) ? "Staff" : platformAdmin.Level;
        platformAdmin.Hostel = string.IsNullOrWhiteSpace(platformAdmin.Hostel) ? "N/A" : platformAdmin.Hostel;
        platformAdmin.TenantId = string.Empty;
        platformAdmin.TenantName = "VirgoHub Platform";
        platformAdmin.IsSeedAccount = true;
    }

    private static List<string> GetAllowedDomainsForSeed(string schoolDomain)
    {
        var normalized = schoolDomain.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return ["virgohub.edu", "student.virgohub.edu"];
        }

        if (normalized.StartsWith("student.", StringComparison.OrdinalIgnoreCase))
        {
            return [normalized, normalized["student.".Length..]];
        }

        return [normalized, $"student.{normalized}"];
    }

    private async Task SaveStateAsync()
    {
        await using var stream = File.Create(_statePath);
        await JsonSerializer.SerializeAsync(stream, _state, _jsonOptions);
    }

    private AppState CreateSeedState()
    {
        var superAdmin = new UserProfile
        {
            FullName = "VirgoHub Super Admin",
            Email = $"admin@{_options.SchoolEmailDomain}",
            StaffId = "VGH-SUPER-001",
            Department = "School Operations",
            Faculty = "Administration",
            Level = "Staff",
            Hostel = "N/A",
            Gender = UserGender.Other,
            Role = UserRole.SuperAdmin,
            IsSeedAccount = true
        };
        ApplyPassword(superAdmin, DefaultSuperAdminPassword);

        var maleStudent = new UserProfile
        {
            FullName = "Ayomide Adebayo",
            Email = $"ayomide@{_options.SchoolEmailDomain}",
            Level = "300",
            Department = "Computer Science",
            Faculty = "Computing and Digital Technologies",
            MatricNumber = "VGH/CMP/24/1032",
            Hostel = "Atlas Hall",
            Gender = UserGender.Male,
            Role = UserRole.Student,
            IsSportsDirector = true,
            IsSeedAccount = true
        };

        var femaleStudent = new UserProfile
        {
            FullName = "Miriam Daniel",
            Email = $"miriam@{_options.SchoolEmailDomain}",
            Level = "400",
            Department = "Mass Communication",
            Faculty = "Humanities",
            MatricNumber = "VGH/MCM/24/1008",
            Hostel = "Aurora Hall",
            Gender = UserGender.Female,
            Role = UserRole.Student,
            IsSeedAccount = true
        };

        var malePorter = new UserProfile
        {
            FullName = "Samuel Ogunleye",
            StaffId = "PORT-M-101",
            Department = "Hostel Operations",
            Faculty = "Administration",
            Level = "Staff",
            Hostel = "Male Hostels",
            Gender = UserGender.Male,
            Role = UserRole.Porter,
            IsSeedAccount = true
        };
        ApplyPassword(malePorter, DefaultMalePorterPassword);

        var femalePorter = new UserProfile
        {
            FullName = "Grace Afolabi",
            StaffId = "PORT-F-102",
            Department = "Hostel Operations",
            Faculty = "Administration",
            Level = "Staff",
            Hostel = "Female Hostels",
            Gender = UserGender.Female,
            Role = UserRole.Porter,
            IsSeedAccount = true
        };
        ApplyPassword(femalePorter, DefaultFemalePorterPassword);

        var lecturerAina = new UserProfile
        {
            FullName = "Dr. Robert Aina",
            Email = $"dr.aina@{_options.SchoolEmailDomain}",
            StaffId = "LECT-CMP-201",
            Department = "Computer Science",
            Faculty = "Computing and Digital Technologies",
            Level = "Staff",
            Hostel = "N/A",
            Gender = UserGender.Other,
            Role = UserRole.Lecturer,
            IsSeedAccount = true
        };
        ApplyPassword(lecturerAina, DefaultLecturerPassword);

        var lecturerSolanke = new UserProfile
        {
            FullName = "Prof. Miriam Solanke",
            Email = $"prof.solanke@{_options.SchoolEmailDomain}",
            StaffId = "LECT-CMP-202",
            Department = "Computer Science",
            Faculty = "Computing and Digital Technologies",
            Level = "Staff",
            Hostel = "N/A",
            Gender = UserGender.Other,
            Role = UserRole.Lecturer,
            IsSeedAccount = true
        };
        ApplyPassword(lecturerSolanke, DefaultLecturerPassword);

        var lecturerBassey = new UserProfile
        {
            FullName = "Mrs. Linda Bassey",
            Email = $"mrs.bassey@{_options.SchoolEmailDomain}",
            StaffId = "LECT-HUM-301",
            Department = "Mass Communication",
            Faculty = "Humanities",
            Level = "Staff",
            Hostel = "N/A",
            Gender = UserGender.Other,
            Role = UserRole.Lecturer,
            IsSeedAccount = true
        };
        ApplyPassword(lecturerBassey, DefaultLecturerPassword);

        var election = new Election
        {
            Title = "Student Union President",
            Position = "President",
            AudienceFaculty = "Computing and Digital Technologies",
            AudienceDepartments = ["Computer Science", "Software Engineering"],
            OpensAt = DateTimeOffset.UtcNow.AddHours(-1),
            ClosesAt = DateTimeOffset.UtcNow.AddDays(1),
            Candidates =
            [
                new Candidate { Name = "Miriam Daniel", Department = "Mass Communication", Position = "President", Manifesto = "Transparent welfare and stronger student representation." },
                new Candidate { Name = "Joshua Bello", Department = "Accounting", Position = "President", Manifesto = "Faster response systems and better student engagement." }
            ]
        };

        var state = new AppState
        {
            Users =
            [
                superAdmin,
                maleStudent,
                femaleStudent,
                lecturerAina,
                lecturerSolanke,
                lecturerBassey,
                malePorter,
                femalePorter
            ],
            Events =
            [
                new CampusEvent
                {
                    Title = "Campus Worship Evening",
                    Category = "Chapel",
                    Venue = "Main Auditorium",
                    Description = "An evening gathering designed for reflection, worship, and community connection.",
                    StartsAt = DateTimeOffset.UtcNow.AddDays(1).Date.AddHours(18),
                    CreatedBy = superAdmin.FullName
                },
                new CampusEvent
                {
                    Title = "Innovation Showcase",
                    Category = "Event",
                    Venue = "Innovation Hub",
                    AudienceFaculty = "Computing and Digital Technologies",
                    AudienceDepartments = ["Computer Science"],
                    Description = "A curated showcase where student teams present ideas and prototypes shaping campus life.",
                    StartsAt = DateTimeOffset.UtcNow.AddDays(2).Date.AddHours(14),
                    CreatedBy = superAdmin.FullName
                }
            ],
            Facilities =
            [
                new SportsFacility
                {
                    Name = "Centenary Football Pitch",
                    Location = "South Sports Complex",
                    OpenHours = "08:00 - 18:00",
                    Capacity = 22,
                    Description = "A full-size pitch designed for training sessions and friendly fixtures."
                },
                new SportsFacility
                {
                    Name = "Blue Court Basketball Arena",
                    Location = "Student Recreation Centre",
                    OpenHours = "09:00 - 20:00",
                    Capacity = 10,
                    Description = "A modern open-air court with strong evening visibility and student access."
                },
                new SportsFacility
                {
                    Name = "Unity Tennis Court",
                    Location = "West Campus",
                    OpenHours = "08:00 - 17:00",
                    Capacity = 4,
                    Description = "A neatly maintained tennis court for training, practice, and student matches."
                }
            ],
            Complaints =
            [
                new ComplaintTicket
                {
                    UserId = maleStudent.Id,
                    StudentName = maleStudent.FullName,
                    Title = "Reading light not working",
                    Category = "Electrical",
                    Hostel = maleStudent.Hostel,
                    Room = "B14",
                    StudentGender = maleStudent.Gender,
                    Description = "The reading light beside the study desk has stopped working for two days.",
                    Status = ComplaintStatus.InProgress,
                    ResolutionNote = "Assigned to the male hostel maintenance team.",
                    UpdatedAt = DateTimeOffset.UtcNow.AddHours(-5)
                },
                new ComplaintTicket
                {
                    UserId = femaleStudent.Id,
                    StudentName = femaleStudent.FullName,
                    Title = "Bathroom tap leaking",
                    Category = "Plumbing",
                    Hostel = femaleStudent.Hostel,
                    Room = "A03",
                    StudentGender = femaleStudent.Gender,
                    Description = "The bathroom tap leaks continuously and is affecting the floor area.",
                    Status = ComplaintStatus.Pending,
                    UpdatedAt = DateTimeOffset.UtcNow.AddHours(-2)
                }
            ],
            Elections =
            [
                election
            ],
            Materials =
            [
                new CourseMaterial
                {
                    CourseCode = "CSC 312",
                    Title = "Mobile Application Development",
                    LecturerName = lecturerAina.FullName,
                    LecturerUserId = lecturerAina.Id,
                    Faculty = lecturerAina.Faculty,
                    Department = "Computer Science",
                    Level = "300",
                    MaterialType = "Lecture Note",
                    ResourceUrl = "https://virgohub.edu/materials/csc312-mobile-development",
                    Summary = "Weekly slides, lab sheets, and practical notes for mobile application development.",
                    UploadedBy = lecturerAina.FullName
                },
                new CourseMaterial
                {
                    CourseCode = "CSC 401",
                    Title = "Advanced Algorithms",
                    LecturerName = lecturerSolanke.FullName,
                    LecturerUserId = lecturerSolanke.Id,
                    Faculty = lecturerSolanke.Faculty,
                    Department = "Computer Science",
                    Level = "400",
                    MaterialType = "Past Question",
                    ResourceUrl = "https://virgohub.edu/materials/csc401-algorithms",
                    Summary = "Algorithm design notes, worked examples, and revision resources for final-year students.",
                    UploadedBy = lecturerSolanke.FullName
                },
                new CourseMaterial
                {
                    CourseCode = "MCM 402",
                    Title = "Digital Media Strategy",
                    LecturerName = lecturerBassey.FullName,
                    LecturerUserId = lecturerBassey.Id,
                    Faculty = lecturerBassey.Faculty,
                    Department = "Mass Communication",
                    Level = "400",
                    MaterialType = "Course Outline",
                    ResourceUrl = "https://virgohub.edu/materials/mcm402-digital-media",
                    Summary = "Course outline, recommended readings, and media campaign case studies.",
                    UploadedBy = lecturerBassey.FullName
                },
                new CourseMaterial
                {
                    CourseCode = "GST 101",
                    Title = "Use of English and Communication",
                    LecturerName = lecturerBassey.FullName,
                    LecturerUserId = lecturerBassey.Id,
                    Faculty = "General Studies",
                    Department = "All Departments",
                    Level = "All Levels",
                    IsGeneralCourse = true,
                    RequiresManualApproval = true,
                    MaterialType = "Course Outline",
                    ResourceUrl = "https://virgohub.edu/materials/gst101-communication",
                    Summary = "General studies course with lecturer approval enabled before full material access is granted.",
                    UploadedBy = lecturerBassey.FullName
                }
            ]
        };

        AddNotificationForSeed(state, maleStudent.Id, "Welcome to VirgoHub", "Your student account is ready. Use your school email to sign in securely.", "Account");
        AddNotificationForSeed(state, femaleStudent.Id, "Welcome to VirgoHub", "Your student account is ready. Use your school email to sign in securely.", "Account");
        AddNotificationForSeed(state, maleStudent.Id, "Complaint in progress", "Your hostel lighting complaint is now being handled.", "Complaints");
        AddNotificationForSeed(state, femaleStudent.Id, "Complaint received", "Your maintenance request has been routed to the female porter team.", "Complaints");
        AddActivityForSeed(state, maleStudent.Id, "Complaint filed", "Submitted a lighting complaint for Atlas Hall.", "Complaints");
        AddActivityForSeed(state, maleStudent.Id, "Materials hub ready", "Search your course code and enroll to access lecturer materials.", "Library");
        AddActivityForSeed(state, femaleStudent.Id, "Profile created", "Your student profile is active and ready for secure sign in.", "Account");
        AddActivityForSeed(state, femaleStudent.Id, "Event feed ready", "New campus and chapel updates will appear on your home screen.", "Events");

        return NormalizeState(state);
    }

    private void ApplyPassword(UserProfile user, string password)
    {
        var saltBytes = RandomNumberGenerator.GetBytes(16);
        user.PortalPasswordSalt = Convert.ToBase64String(saltBytes);
        user.PortalPasswordHash = HashPassword(password, saltBytes);
    }

    private static string HashPassword(string password, byte[] saltBytes)
    {
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            saltBytes,
            100_000,
            HashAlgorithmName.SHA256,
            32);

        return Convert.ToBase64String(hash);
    }

    private static bool VerifyPassword(string password, string passwordHash, string passwordSalt)
    {
        if (string.IsNullOrWhiteSpace(passwordHash) || string.IsNullOrWhiteSpace(passwordSalt))
        {
            return false;
        }

        var saltBytes = Convert.FromBase64String(passwordSalt);
        var actualHash = HashPassword(password, saltBytes);
        return CryptographicOperations.FixedTimeEquals(
            Convert.FromBase64String(actualHash),
            Convert.FromBase64String(passwordHash));
    }

    private static void AddNotificationForSeed(AppState state, string userId, string title, string message, string category)
    {
        var tenantId = state.Users.FirstOrDefault(x => x.Id == userId)?.TenantId ?? string.Empty;
        state.Notifications.Add(new AppNotification
        {
            TenantId = tenantId,
            UserId = userId,
            Title = title,
            Message = message,
            Category = category,
            CreatedAt = DateTimeOffset.UtcNow
        });
    }

    private static void AddActivityForSeed(AppState state, string userId, string title, string description, string category)
    {
        var tenantId = state.Users.FirstOrDefault(x => x.Id == userId)?.TenantId ?? string.Empty;
        state.Activities.Add(new ActivityEntry
        {
            TenantId = tenantId,
            UserId = userId,
            Title = title,
            Description = description,
            Category = category,
            CreatedAt = DateTimeOffset.UtcNow
        });
    }

    private string NormalizeSeedEmail(string email)
    {
        var trimmed = email.Trim().ToLowerInvariant();
        if (!trimmed.EndsWith("@student.virgohub.edu", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        var localPart = trimmed[..trimmed.IndexOf('@')];
        return $"{localPart}@{_options.SchoolEmailDomain}".ToLowerInvariant();
    }

    private static bool ShouldMarkAsSeedAccount(UserProfile user)
    {
        if (user.IsSeedAccount)
        {
            return true;
        }

        var localPart = GetEmailLocalPart(user.Email);
        return localPart is "ayomide" or "miriam" or "admin" or "platform" or "mentor.demo" or "learner.demo" ||
               user.StaffId is "PORT-M-101" or "PORT-F-102" or "VGH-SUPER-001" or "VGH-PLATFORM-001";
    }

    private static string GetEmailLocalPart(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return string.Empty;
        }

        var index = email.IndexOf('@');
        return index > 0
            ? email[..index].Trim().ToLowerInvariant()
            : email.Trim().ToLowerInvariant();
    }
}
