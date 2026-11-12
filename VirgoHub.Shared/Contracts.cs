namespace VirgoHub.Shared;

public enum UserRole
{
    Student,
    Admin,
    Lecturer,
    Porter,
    Librarian,
    ElectoralOfficer,
    PlatformSuperAdmin,
    SuperAdmin
}

public enum UserGender
{
    Male,
    Female,
    Other
}

public enum ThemeMode
{
    System,
    Light,
    Dark
}

public enum AppUseMode
{
    School,
    Personal
}

public enum TenantType
{
    School,
    Personal
}

public enum AdminModule
{
    Students,
    Events,
    Voting,
    Sports,
    Library,
    Complaints
}

public enum ComplaintStatus
{
    Pending,
    InProgress,
    Resolved
}

public enum LoanStatus
{
    Requested,
    Approved,
    Rejected,
    Returned
}

public enum CourseEnrollmentStatus
{
    Pending,
    Approved
}

public enum SportsBookingStatus
{
    PendingApproval,
    Approved,
    Rejected
}

public enum PersonalAssessmentType
{
    Test,
    Exam
}

public sealed class TenantProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public TenantType Type { get; set; } = TenantType.School;
    public string PrimaryDomain { get; set; } = string.Empty;
    public List<string> AllowedDomains { get; set; } = [];
    public string Description { get; set; } = string.Empty;
    public string AccentColor { get; set; } = "#25A9F5";
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class UserProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TenantId { get; set; } = string.Empty;
    public string TenantName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string StaffId { get; set; } = string.Empty;
    public string PortalPasswordHash { get; set; } = string.Empty;
    public string PortalPasswordSalt { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Faculty { get; set; } = string.Empty;
    public string MatricNumber { get; set; } = string.Empty;
    public string Hostel { get; set; } = string.Empty;
    public UserGender Gender { get; set; } = UserGender.Other;
    public ThemeMode PreferredTheme { get; set; } = ThemeMode.System;
    public UserRole Role { get; set; } = UserRole.Student;
    public bool IsSportsDirector { get; set; }
    public List<AdminModule> AdminModules { get; set; } = [];
    public bool IsActive { get; set; } = true;
    public bool IsSeedAccount { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class OtpChallenge
{
    public string Email { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
    public bool Consumed { get; set; }
}

public sealed class UserSession
{
    public string Token { get; set; } = Guid.NewGuid().ToString("N");
    public string UserId { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; } = DateTimeOffset.UtcNow.AddHours(8);
}

public sealed class CampusEvent
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TenantId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Venue { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string AudienceFaculty { get; set; } = string.Empty;
    public List<string> AudienceDepartments { get; set; } = [];
    public DateTimeOffset StartsAt { get; set; } = DateTimeOffset.UtcNow.AddDays(1);
    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class SportsFacility
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TenantId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string OpenHours { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public string Description { get; set; } = string.Empty;
}

public sealed class SportsBooking
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TenantId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string FacilityId { get; set; } = string.Empty;
    public string FacilityName { get; set; } = string.Empty;
    public DateOnly BookingDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public string SlotLabel { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public SportsBookingStatus Status { get; set; } = SportsBookingStatus.PendingApproval;
    public string ApprovedByUserId { get; set; } = string.Empty;
    public string ApprovedByName { get; set; } = string.Empty;
    public DateTimeOffset? ApprovedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class ComplaintTicket
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TenantId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Hostel { get; set; } = string.Empty;
    public string Room { get; set; } = string.Empty;
    public UserGender StudentGender { get; set; } = UserGender.Other;
    public string Description { get; set; } = string.Empty;
    public ComplaintStatus Status { get; set; } = ComplaintStatus.Pending;
    public string ResolutionNote { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class Candidate
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public string Manifesto { get; set; } = string.Empty;
    public int VoteCount { get; set; }
}

public sealed class Election
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TenantId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public string AudienceFaculty { get; set; } = string.Empty;
    public List<string> AudienceDepartments { get; set; } = [];
    public DateTimeOffset OpensAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ClosesAt { get; set; } = DateTimeOffset.UtcNow.AddDays(2);
    public bool IsActive { get; set; } = true;
    public int TotalVotes { get; set; }
    public bool HasVoted { get; set; }
    public bool IsOpen { get; set; }
    public bool IsClosed { get; set; }
    public string StatusLabel { get; set; } = string.Empty;
    public List<Candidate> Candidates { get; set; } = [];
}

public sealed class VoteRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TenantId { get; set; } = string.Empty;
    public string ElectionId { get; set; } = string.Empty;
    public string CandidateId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public DateTimeOffset CastAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class CourseMaterial
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TenantId { get; set; } = string.Empty;
    public string CourseCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string LecturerName { get; set; } = string.Empty;
    public string LecturerUserId { get; set; } = string.Empty;
    public string Faculty { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public bool IsGeneralCourse { get; set; }
    public bool RequiresManualApproval { get; set; }
    public string MaterialType { get; set; } = string.Empty;
    public string ResourceUrl { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string UploadedBy { get; set; } = string.Empty;
    public DateTimeOffset PublishedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class CourseEnrollment
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TenantId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string CourseCode { get; set; } = string.Empty;
    public string CourseTitle { get; set; } = string.Empty;
    public string Faculty { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public CourseEnrollmentStatus Status { get; set; } = CourseEnrollmentStatus.Pending;
    public bool RequiresManualApproval { get; set; }
    public string ApprovedByUserId { get; set; } = string.Empty;
    public string ApprovedByName { get; set; } = string.Empty;
    public DateTimeOffset? ApprovedAt { get; set; }
    public DateTimeOffset EnrolledAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class TeachingGroupMember
{
    public string UserId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTimeOffset JoinedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class PersonalTeachingGroup
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TenantId { get; set; } = string.Empty;
    public string LecturerUserId { get; set; } = string.Empty;
    public string LecturerName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string CourseCode { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string InviteCode { get; set; } = string.Empty;
    public string InviteLink { get; set; } = string.Empty;
    public List<TeachingGroupMember> Members { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class PersonalGroupPost
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TenantId { get; set; } = string.Empty;
    public string GroupId { get; set; } = string.Empty;
    public string CourseCode { get; set; } = string.Empty;
    public string AuthorUserId { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string AttachmentUrl { get; set; } = string.Empty;
    public bool IsAnnouncement { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class PersonalAssignment
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TenantId { get; set; } = string.Empty;
    public string GroupId { get; set; } = string.Empty;
    public string CourseCode { get; set; } = string.Empty;
    public string LecturerUserId { get; set; } = string.Empty;
    public string LecturerName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Instructions { get; set; } = string.Empty;
    public string ResourceUrl { get; set; } = string.Empty;
    public DateTimeOffset DueAt { get; set; } = DateTimeOffset.UtcNow.AddDays(7);
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class PersonalAssignmentSubmission
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TenantId { get; set; } = string.Empty;
    public string AssignmentId { get; set; } = string.Empty;
    public string GroupId { get; set; } = string.Empty;
    public string StudentUserId { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
    public string ResourceUrl { get; set; } = string.Empty;
    public DateTimeOffset SubmittedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class PersonalAssessmentQuestion
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Prompt { get; set; } = string.Empty;
    public string OptionA { get; set; } = string.Empty;
    public string OptionB { get; set; } = string.Empty;
    public string OptionC { get; set; } = string.Empty;
    public string OptionD { get; set; } = string.Empty;
    public string CorrectOption { get; set; } = "A";
    public string Explanation { get; set; } = string.Empty;
}

public sealed class PersonalAssessment
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TenantId { get; set; } = string.Empty;
    public string GroupId { get; set; } = string.Empty;
    public string GroupTitle { get; set; } = string.Empty;
    public string CourseCode { get; set; } = string.Empty;
    public string LecturerUserId { get; set; } = string.Empty;
    public string LecturerName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public PersonalAssessmentType Type { get; set; } = PersonalAssessmentType.Test;
    public int TimeLimitMinutes { get; set; } = 20;
    public DateTimeOffset OpensAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset DueAt { get; set; } = DateTimeOffset.UtcNow.AddDays(7);
    public bool IsPublished { get; set; } = true;
    public List<PersonalAssessmentQuestion> Questions { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class PersonalAssessmentResponse
{
    public string QuestionId { get; set; } = string.Empty;
    public string SelectedOption { get; set; } = string.Empty;
}

public sealed class PersonalAssessmentSubmission
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TenantId { get; set; } = string.Empty;
    public string AssessmentId { get; set; } = string.Empty;
    public string GroupId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string AssessmentTitle { get; set; } = string.Empty;
    public string CourseCode { get; set; } = string.Empty;
    public List<PersonalAssessmentResponse> Responses { get; set; } = [];
    public int Score { get; set; }
    public int TotalQuestions { get; set; }
    public DateTimeOffset SubmittedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class AppNotification
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TenantId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class ActivityEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TenantId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class AppState
{
    public List<TenantProfile> Tenants { get; set; } = [];
    public List<UserProfile> Users { get; set; } = [];
    public List<OtpChallenge> OtpChallenges { get; set; } = [];
    public List<UserSession> Sessions { get; set; } = [];
    public List<CampusEvent> Events { get; set; } = [];
    public List<SportsFacility> Facilities { get; set; } = [];
    public List<SportsBooking> Bookings { get; set; } = [];
    public List<ComplaintTicket> Complaints { get; set; } = [];
    public List<Election> Elections { get; set; } = [];
    public List<VoteRecord> Votes { get; set; } = [];
    public List<CourseMaterial> Materials { get; set; } = [];
    public List<CourseEnrollment> Enrollments { get; set; } = [];
    public List<PersonalTeachingGroup> TeachingGroups { get; set; } = [];
    public List<PersonalGroupPost> PersonalGroupPosts { get; set; } = [];
    public List<PersonalAssessment> PersonalAssessments { get; set; } = [];
    public List<PersonalAssessmentSubmission> PersonalAssessmentSubmissions { get; set; } = [];
    public List<PersonalAssignment> PersonalAssignments { get; set; } = [];
    public List<PersonalAssignmentSubmission> PersonalAssignmentSubmissions { get; set; } = [];
    public List<LibraryBook> Books { get; set; } = [];
    public List<LoanRecord> Loans { get; set; } = [];
    public List<AppNotification> Notifications { get; set; } = [];
    public List<ActivityEntry> Activities { get; set; } = [];
}

public sealed class LibraryBook
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Isbn { get; set; } = string.Empty;
    public string Shelf { get; set; } = string.Empty;
    public int TotalCopies { get; set; } = 1;
    public int AvailableCopies { get; set; } = 1;
    public string Description { get; set; } = string.Empty;
}

public sealed class LoanRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string UserId { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string BookId { get; set; } = string.Empty;
    public string BookTitle { get; set; } = string.Empty;
    public LoanStatus Status { get; set; } = LoanStatus.Requested;
    public string Note { get; set; } = string.Empty;
    public DateTimeOffset RequestedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset DueAt { get; set; } = DateTimeOffset.UtcNow.AddDays(7);
}

public sealed class RegisterUserRequest
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Faculty { get; set; } = string.Empty;
    public string MatricNumber { get; set; } = string.Empty;
    public string Hostel { get; set; } = string.Empty;
    public UserGender Gender { get; set; } = UserGender.Other;
    public UserRole Role { get; set; } = UserRole.Student;
    public bool IsSportsDirector { get; set; }
}

public sealed class RegisterPorterRequest
{
    public string FullName { get; set; } = string.Empty;
    public string StaffId { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Department { get; set; } = "Hostel Operations";
    public string Faculty { get; set; } = "Administration";
    public UserGender Gender { get; set; } = UserGender.Other;
}

public sealed class RegisterPortalAdminRequest
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Department { get; set; } = "School Operations";
    public string Faculty { get; set; } = "Administration";
    public List<AdminModule> Modules { get; set; } = [];
}

public sealed class RegisterLecturerRequest
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string StaffId { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Faculty { get; set; } = string.Empty;
}

public sealed class UpdatePortalAdminRequest
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Faculty { get; set; } = string.Empty;
    public List<AdminModule> Modules { get; set; } = [];
    public string Password { get; set; } = string.Empty;
}

public sealed class CreateSchoolTenantRequest
{
    public string SchoolName { get; set; } = string.Empty;
    public string PrimaryDomain { get; set; } = string.Empty;
    public string AdditionalDomainsText { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string AccentColor { get; set; } = "#25A9F5";
    public string SuperAdminName { get; set; } = string.Empty;
    public string SuperAdminEmail { get; set; } = string.Empty;
    public string SuperAdminPassword { get; set; } = string.Empty;
}

public sealed class UpdateSchoolTenantRequest
{
    public string SchoolName { get; set; } = string.Empty;
    public string PrimaryDomain { get; set; } = string.Empty;
    public string AdditionalDomainsText { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string AccentColor { get; set; } = "#25A9F5";
    public bool IsActive { get; set; } = true;
}

public sealed class UpdateLecturerRequest
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string StaffId { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Faculty { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public sealed class PortalLoginRequest
{
    public string Identifier { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public sealed class PortalLoginResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public UserProfile? User { get; set; }
}

public sealed class PorterLoginRequest
{
    public string StaffId { get; set; } = string.Empty;
}

public sealed class PorterLoginResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public UserProfile? Porter { get; set; }
}

public sealed class SendOtpRequest
{
    public AppUseMode Mode { get; set; } = AppUseMode.School;
    public string TenantId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public UserRole? RoleHint { get; set; }
}

public sealed class SendOtpResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? DebugCode { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public string ResolvedTenantId { get; set; } = string.Empty;
    public string ResolvedTenantName { get; set; } = string.Empty;
    public AppUseMode ResolvedMode { get; set; } = AppUseMode.School;
}

public sealed class VerifyOtpRequest
{
    public AppUseMode Mode { get; set; } = AppUseMode.School;
    public string TenantId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public UserRole? RoleHint { get; set; }
}

public sealed class VerifyOtpResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? SessionToken { get; set; }
    public UserProfile? User { get; set; }
}

public sealed class CreateEventRequest
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Venue { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string AudienceFaculty { get; set; } = string.Empty;
    public List<string> AudienceDepartments { get; set; } = [];
    public DateTimeOffset StartsAt { get; set; } = DateTimeOffset.UtcNow.AddDays(1);
    public string CreatedBy { get; set; } = "Super Admin";
}

public sealed class CreateFacilityRequest
{
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string OpenHours { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public string Description { get; set; } = string.Empty;
}

public sealed class UpdateFacilityRequest
{
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string OpenHours { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public string Description { get; set; } = string.Empty;
}

public sealed class CreateBookingRequest
{
    public string FacilityId { get; set; } = string.Empty;
    public DateOnly BookingDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public string SlotLabel { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
}

public sealed class CreateComplaintRequest
{
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Hostel { get; set; } = string.Empty;
    public string Room { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public sealed class UpdateComplaintStatusRequest
{
    public ComplaintStatus Status { get; set; }
    public string ResolutionNote { get; set; } = string.Empty;
}

public sealed class CreateElectionRequest
{
    public string Title { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public string AudienceFaculty { get; set; } = string.Empty;
    public List<string> AudienceDepartments { get; set; } = [];
    public DateTimeOffset OpensAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ClosesAt { get; set; } = DateTimeOffset.UtcNow.AddDays(2);
    public string CandidateNames { get; set; } = string.Empty;
    public List<CandidateInput> Candidates { get; set; } = [];
}

public sealed class CandidateInput
{
    public string Name { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Manifesto { get; set; } = string.Empty;
}

public sealed class UpdateElectionRequest
{
    public string Title { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public string AudienceFaculty { get; set; } = string.Empty;
    public List<string> AudienceDepartments { get; set; } = [];
    public DateTimeOffset OpensAt { get; set; }
    public DateTimeOffset ClosesAt { get; set; }
    public bool IsActive { get; set; } = true;
    public List<CandidateInput> Candidates { get; set; } = [];
}

public sealed class CastVoteRequest
{
    public string ElectionId { get; set; } = string.Empty;
    public string CandidateId { get; set; } = string.Empty;
}

public sealed class CreateCourseMaterialRequest
{
    public string CourseCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string LecturerName { get; set; } = string.Empty;
    public string LecturerUserId { get; set; } = string.Empty;
    public string Faculty { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public bool IsGeneralCourse { get; set; }
    public bool RequiresManualApproval { get; set; }
    public string MaterialType { get; set; } = string.Empty;
    public string ResourceUrl { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string UploadedBy { get; set; } = string.Empty;
}

public sealed class UpdateCourseMaterialRequest
{
    public string CourseCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string LecturerName { get; set; } = string.Empty;
    public string LecturerUserId { get; set; } = string.Empty;
    public string Faculty { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public bool IsGeneralCourse { get; set; }
    public bool RequiresManualApproval { get; set; }
    public string MaterialType { get; set; } = string.Empty;
    public string ResourceUrl { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string UploadedBy { get; set; } = string.Empty;
}

public sealed class CreateCourseEnrollmentRequest
{
    public string CourseCode { get; set; } = string.Empty;
}

public sealed class CreatePersonalTeachingGroupRequest
{
    public string Title { get; set; } = string.Empty;
    public string CourseCode { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
}

public sealed class JoinPersonalTeachingGroupRequest
{
    public string InviteCode { get; set; } = string.Empty;
}

public sealed class CreatePersonalGroupPostRequest
{
    public string GroupId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string AttachmentUrl { get; set; } = string.Empty;
    public bool IsAnnouncement { get; set; }
}

public sealed class CreatePersonalMaterialRequest
{
    public string GroupId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string MaterialType { get; set; } = string.Empty;
    public string ResourceUrl { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
}

public sealed class CreatePersonalAssignmentRequest
{
    public string GroupId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Instructions { get; set; } = string.Empty;
    public string ResourceUrl { get; set; } = string.Empty;
    public DateTimeOffset DueAt { get; set; } = DateTimeOffset.UtcNow.AddDays(7);
}

public sealed class SubmitPersonalAssignmentRequest
{
    public string AssignmentId { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
    public string ResourceUrl { get; set; } = string.Empty;
}

public sealed class CreatePersonalAssessmentQuestionRequest
{
    public string Prompt { get; set; } = string.Empty;
    public string OptionA { get; set; } = string.Empty;
    public string OptionB { get; set; } = string.Empty;
    public string OptionC { get; set; } = string.Empty;
    public string OptionD { get; set; } = string.Empty;
    public string CorrectOption { get; set; } = "A";
    public string Explanation { get; set; } = string.Empty;
}

public sealed class CreatePersonalAssessmentRequest
{
    public string GroupId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public PersonalAssessmentType Type { get; set; } = PersonalAssessmentType.Test;
    public int TimeLimitMinutes { get; set; } = 20;
    public DateTimeOffset OpensAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset DueAt { get; set; } = DateTimeOffset.UtcNow.AddDays(7);
    public List<CreatePersonalAssessmentQuestionRequest> Questions { get; set; } = [];
}

public sealed class SubmitPersonalAssessmentResponseRequest
{
    public string QuestionId { get; set; } = string.Empty;
    public string SelectedOption { get; set; } = string.Empty;
}

public sealed class SubmitPersonalAssessmentRequest
{
    public string AssessmentId { get; set; } = string.Empty;
    public List<SubmitPersonalAssessmentResponseRequest> Responses { get; set; } = [];
}

public sealed class CourseEnrollmentApprovalRequest
{
    public string CourseCode { get; set; } = string.Empty;
    public string Faculty { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
}

public sealed class UpdateEventRequest
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Venue { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string AudienceFaculty { get; set; } = string.Empty;
    public List<string> AudienceDepartments { get; set; } = [];
    public DateTimeOffset StartsAt { get; set; } = DateTimeOffset.UtcNow.AddDays(1);
}

public sealed class UpdateUserRequest
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Faculty { get; set; } = string.Empty;
    public string MatricNumber { get; set; } = string.Empty;
    public string Hostel { get; set; } = string.Empty;
    public UserGender Gender { get; set; } = UserGender.Other;
    public bool IsSportsDirector { get; set; }
}

public sealed class DashboardSnapshot
{
    public UserProfile? User { get; set; }
    public TenantProfile? Tenant { get; set; }
    public AppUseMode Mode { get; set; } = AppUseMode.School;
    public IReadOnlyList<CampusEvent> UpcomingEvents { get; set; } = [];
    public IReadOnlyList<SportsFacility> Facilities { get; set; } = [];
    public IReadOnlyList<SportsBooking> MyBookings { get; set; } = [];
    public IReadOnlyList<SportsBooking> SportsDirectorBookings { get; set; } = [];
    public IReadOnlyList<ComplaintTicket> MyComplaints { get; set; } = [];
    public IReadOnlyList<Election> ActiveElections { get; set; } = [];
    public IReadOnlyList<CourseMaterial> CourseMaterials { get; set; } = [];
    public IReadOnlyList<CourseEnrollment> MyCourseEnrollments { get; set; } = [];
    public IReadOnlyList<PersonalTeachingGroup> PersonalGroups { get; set; } = [];
    public IReadOnlyList<PersonalGroupPost> PersonalGroupPosts { get; set; } = [];
    public IReadOnlyList<PersonalAssessment> PersonalAssessments { get; set; } = [];
    public IReadOnlyList<PersonalAssessmentSubmission> PersonalAssessmentSubmissions { get; set; } = [];
    public IReadOnlyList<PersonalAssignment> PersonalAssignments { get; set; } = [];
    public IReadOnlyList<PersonalAssignmentSubmission> PersonalAssignmentSubmissions { get; set; } = [];
    public IReadOnlyList<AppNotification> Notifications { get; set; } = [];
    public IReadOnlyList<ActivityEntry> RecentActivities { get; set; } = [];
}

public sealed class SchoolDirectoryEntry
{
    public string TenantId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string AccentColor { get; set; } = "#25A9F5";
}

public sealed class PlatformBootstrapResponse
{
    public string PlatformName { get; set; } = "VirgoHub";
    public string IntroHeadline { get; set; } = string.Empty;
    public string IntroSummary { get; set; } = string.Empty;
    public IReadOnlyList<SchoolDirectoryEntry> Schools { get; set; } = [];
}

public sealed class PorterDashboardSnapshot
{
    public UserProfile? Porter { get; set; }
    public IReadOnlyList<ComplaintTicket> VisibleComplaints { get; set; } = [];
}

public sealed class AdminSummary
{
    public int TotalUsers { get; set; }
    public int StudentCount { get; set; }
    public int AdminCount { get; set; }
    public int PorterCount { get; set; }
    public int ActiveEvents { get; set; }
    public int PendingComplaints { get; set; }
    public int ActiveElections { get; set; }
    public int MaterialsInHub { get; set; }
    public int EnrolledCourses { get; set; }
    public int TodaysBookings { get; set; }
}

public sealed class MailSettings
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public string FromAddress { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool EnableSsl { get; set; } = true;
}

public sealed class VirgoHubOptions
{
    public string PlatformName { get; set; } = "VirgoHub";
    public string DefaultSchoolName { get; set; } = "VirgoHub Campus";
    public string SchoolEmailDomain { get; set; } = "virgohub.edu";
    public int OtpMinutesToLive { get; set; } = 10;
    public int SessionHoursToLive { get; set; } = 8;
    public string TestStudentOtpCode { get; set; } = "246810";
}
