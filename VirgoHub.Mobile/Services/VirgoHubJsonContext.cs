using System.Text.Json;
using System.Text.Json.Serialization;
using VirgoHub.Shared;

namespace VirgoHub.Mobile.Services;

[JsonSourceGenerationOptions(
    JsonSerializerDefaults.Web,
    WriteIndented = false,
    DefaultIgnoreCondition = JsonIgnoreCondition.Never)]
[JsonSerializable(typeof(SendOtpRequest))]
[JsonSerializable(typeof(SendOtpResult))]
[JsonSerializable(typeof(VerifyOtpRequest))]
[JsonSerializable(typeof(VerifyOtpResult))]
[JsonSerializable(typeof(PlatformBootstrapResponse))]
[JsonSerializable(typeof(SchoolDirectoryEntry))]
[JsonSerializable(typeof(TenantProfile))]
[JsonSerializable(typeof(DashboardSnapshot))]
[JsonSerializable(typeof(CreateBookingRequest))]
[JsonSerializable(typeof(CreateComplaintRequest))]
[JsonSerializable(typeof(CastVoteRequest))]
[JsonSerializable(typeof(CreateCourseEnrollmentRequest))]
[JsonSerializable(typeof(CreatePersonalTeachingGroupRequest))]
[JsonSerializable(typeof(JoinPersonalTeachingGroupRequest))]
[JsonSerializable(typeof(CreatePersonalGroupPostRequest))]
[JsonSerializable(typeof(CreatePersonalMaterialRequest))]
[JsonSerializable(typeof(CreatePersonalAssignmentRequest))]
[JsonSerializable(typeof(SubmitPersonalAssignmentRequest))]
[JsonSerializable(typeof(CreatePersonalAssessmentRequest))]
[JsonSerializable(typeof(CreatePersonalAssessmentQuestionRequest))]
[JsonSerializable(typeof(SubmitPersonalAssessmentRequest))]
[JsonSerializable(typeof(SubmitPersonalAssessmentResponseRequest))]
[JsonSerializable(typeof(UserProfile))]
[JsonSerializable(typeof(CampusEvent))]
[JsonSerializable(typeof(SportsFacility))]
[JsonSerializable(typeof(SportsBooking))]
[JsonSerializable(typeof(ComplaintTicket))]
[JsonSerializable(typeof(Election))]
[JsonSerializable(typeof(Candidate))]
[JsonSerializable(typeof(CourseMaterial))]
[JsonSerializable(typeof(CourseEnrollment))]
[JsonSerializable(typeof(PersonalTeachingGroup))]
[JsonSerializable(typeof(PersonalGroupPost))]
[JsonSerializable(typeof(TeachingGroupMember))]
[JsonSerializable(typeof(PersonalAssessment))]
[JsonSerializable(typeof(PersonalAssessmentQuestion))]
[JsonSerializable(typeof(PersonalAssessmentSubmission))]
[JsonSerializable(typeof(PersonalAssessmentResponse))]
[JsonSerializable(typeof(PersonalAssignment))]
[JsonSerializable(typeof(PersonalAssignmentSubmission))]
[JsonSerializable(typeof(AppNotification))]
[JsonSerializable(typeof(ActivityEntry))]
internal partial class VirgoHubJsonContext : JsonSerializerContext
{
}
