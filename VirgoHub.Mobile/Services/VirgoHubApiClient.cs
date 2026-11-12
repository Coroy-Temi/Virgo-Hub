using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using VirgoHub.Shared;

namespace VirgoHub.Mobile.Services;

public sealed class VirgoHubApiClient
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions = VirgoHubJsonContext.Default.Options;

    public VirgoHubApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
        BaseUrl = GetDefaultBaseUrl();
    }

    public string BaseUrl { get; private set; }

    public string? SessionToken { get; private set; }

    public UserProfile? CurrentUser { get; private set; }

    public static string GetDefaultBaseUrl()
    {
#if ANDROID
        return "http://10.0.2.2:5128";
#else
        return "http://localhost:5128";
#endif
    }

    public void SetBaseUrl(string url)
    {
        BaseUrl = NormalizeBaseUrl(url);
    }

    public void ResetSession()
    {
        SessionToken = null;
        CurrentUser = null;
    }

    public async Task<SendOtpResult> SendCodeAsync(string email)
        => await SendCodeAsync(email, AppUseMode.School, string.Empty, null);

    public async Task<PlatformBootstrapResponse> GetPlatformBootstrapAsync()
    {
        using var response = await ExecuteRequestAsync(() => _httpClient.GetAsync(BuildUri("/api/platform/bootstrap")));
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(await ReadErrorAsync(response));
        }

        return await ReadFromJsonAsync(response.Content, VirgoHubJsonContext.Default.PlatformBootstrapResponse)
            ?? new PlatformBootstrapResponse();
    }

    public async Task<SendOtpResult> SendCodeAsync(string email, AppUseMode mode, string tenantId, UserRole? roleHint)
    {
        var response = await ExecuteRequestAsync(() => _httpClient.PostAsJsonAsync(BuildUri("/api/auth/send-code"), new SendOtpRequest
        {
            Email = email,
            Mode = mode,
            TenantId = tenantId,
            RoleHint = roleHint
        }, _jsonOptions));

        var result = await ReadFromJsonAsync(response.Content, VirgoHubJsonContext.Default.SendOtpResult) ?? new SendOtpResult
        {
            Success = false,
            Message = "No response from the server."
        };

        if (!response.IsSuccessStatusCode && !result.Success)
        {
            throw new InvalidOperationException(result.Message);
        }

        return result;
    }

    public async Task<VerifyOtpResult> VerifyCodeAsync(string email, string code)
        => await VerifyCodeAsync(email, code, AppUseMode.School, string.Empty, null);

    public async Task<VerifyOtpResult> VerifyCodeAsync(string email, string code, AppUseMode mode, string tenantId, UserRole? roleHint)
    {
        var response = await ExecuteRequestAsync(() => _httpClient.PostAsJsonAsync(BuildUri("/api/auth/verify-code"), new VerifyOtpRequest
        {
            Email = email,
            Code = code,
            Mode = mode,
            TenantId = tenantId,
            RoleHint = roleHint
        }, _jsonOptions));

        var result = await ReadFromJsonAsync(response.Content, VirgoHubJsonContext.Default.VerifyOtpResult) ?? new VerifyOtpResult
        {
            Success = false,
            Message = "No response from the server."
        };

        if (!response.IsSuccessStatusCode || !result.Success || result.User is null || string.IsNullOrWhiteSpace(result.SessionToken))
        {
            throw new InvalidOperationException(result.Message);
        }

        SessionToken = result.SessionToken;
        CurrentUser = result.User;
        return result;
    }

    public async Task<DashboardSnapshot> GetDashboardAsync()
    {
        EnsureAuthenticated();
        using var request = CreateAuthorizedRequest(HttpMethod.Get, $"/api/students/{CurrentUser!.Id}/dashboard");
        using var response = await ExecuteRequestAsync(() => _httpClient.SendAsync(request));

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(await ReadErrorAsync(response));
        }

        return await ReadFromJsonAsync(response.Content, VirgoHubJsonContext.Default.DashboardSnapshot)
            ?? new DashboardSnapshot();
    }

    public async Task CreateBookingAsync(CreateBookingRequest request)
    {
        await PostStudentActionAsync($"/api/students/{CurrentUser!.Id}/bookings", request);
    }

    public async Task ApproveBookingAsync(string bookingId)
    {
        EnsureAuthenticated();
        using var message = CreateAuthorizedRequest(HttpMethod.Post, $"/api/students/{CurrentUser!.Id}/bookings/{bookingId}/approve");
        using var response = await ExecuteRequestAsync(() => _httpClient.SendAsync(message));
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(await ReadErrorAsync(response));
        }
    }

    public async Task CreateComplaintAsync(CreateComplaintRequest request)
    {
        await PostStudentActionAsync($"/api/students/{CurrentUser!.Id}/complaints", request);
    }

    public async Task CastVoteAsync(CastVoteRequest request)
    {
        await PostStudentActionAsync($"/api/students/{CurrentUser!.Id}/votes", request);
    }

    public async Task EnrollCourseAsync(CreateCourseEnrollmentRequest request)
    {
        await PostStudentActionAsync($"/api/students/{CurrentUser!.Id}/course-enrollments", request);
    }

    public async Task CreatePersonalTeachingGroupAsync(CreatePersonalTeachingGroupRequest request)
    {
        await PostStudentActionAsync($"/api/students/{CurrentUser!.Id}/personal-groups", request);
    }

    public async Task JoinPersonalTeachingGroupAsync(JoinPersonalTeachingGroupRequest request)
    {
        await PostStudentActionAsync($"/api/students/{CurrentUser!.Id}/personal-groups/join", request);
    }

    public async Task CreatePersonalGroupPostAsync(CreatePersonalGroupPostRequest request)
    {
        await PostStudentActionAsync($"/api/students/{CurrentUser!.Id}/personal-groups/posts", request);
    }

    public async Task CreatePersonalMaterialAsync(CreatePersonalMaterialRequest request)
    {
        await PostStudentActionAsync($"/api/students/{CurrentUser!.Id}/personal-materials", request);
    }

    public async Task CreatePersonalAssignmentAsync(CreatePersonalAssignmentRequest request)
    {
        await PostStudentActionAsync($"/api/students/{CurrentUser!.Id}/personal-assignments", request);
    }

    public async Task SubmitPersonalAssignmentAsync(SubmitPersonalAssignmentRequest request)
    {
        await PostStudentActionAsync($"/api/students/{CurrentUser!.Id}/personal-assignments/submit", request);
    }

    public async Task CreatePersonalAssessmentAsync(CreatePersonalAssessmentRequest request)
    {
        await PostStudentActionAsync($"/api/students/{CurrentUser!.Id}/personal-assessments", request);
    }

    public async Task SubmitPersonalAssessmentAsync(SubmitPersonalAssessmentRequest request)
    {
        await PostStudentActionAsync($"/api/students/{CurrentUser!.Id}/personal-assessments/submit", request);
    }

    private async Task PostStudentActionAsync<TRequest>(string path, TRequest request)
    {
        EnsureAuthenticated();
        using var message = CreateAuthorizedRequest(HttpMethod.Post, path);
        message.Content = JsonContent.Create(request, options: _jsonOptions);

        using var response = await ExecuteRequestAsync(() => _httpClient.SendAsync(message));
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(await ReadErrorAsync(response));
        }
    }

    private HttpRequestMessage CreateAuthorizedRequest(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, BuildUri(path));
        if (!string.IsNullOrWhiteSpace(SessionToken))
        {
            request.Headers.Add("X-Session-Token", SessionToken);
        }

        return request;
    }

    private void EnsureAuthenticated()
    {
        if (CurrentUser is null || string.IsNullOrWhiteSpace(SessionToken))
        {
            throw new InvalidOperationException("You need to sign in again.");
        }
    }

    private static string NormalizeBaseUrl(string? url)
    {
        var candidate = string.IsNullOrWhiteSpace(url) ? GetDefaultBaseUrl() : url.Trim().TrimEnd('/');
        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException("Enter a valid VirgoHub server address, for example https://your-school.ngrok-free.app.");
        }

        return uri.ToString().TrimEnd('/');
    }

    private Uri BuildUri(string path) =>
        new($"{BaseUrl.TrimEnd('/')}/{path.TrimStart('/')}");

    private async Task<string> ReadErrorAsync(HttpResponseMessage response)
    {
        var raw = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return $"Request failed with {(int)response.StatusCode}.";
        }

        try
        {
            using var document = JsonDocument.Parse(raw);
            if (document.RootElement.TryGetProperty("message", out var message))
            {
                return message.GetString() ?? raw;
            }

            if (document.RootElement.TryGetProperty("Message", out var upperMessage))
            {
                return upperMessage.GetString() ?? raw;
            }
        }
        catch
        {
            // Ignore JSON parse errors and return the original response text.
        }

        return raw;
    }

    private static async Task<T?> ReadFromJsonAsync<T>(HttpContent content, JsonTypeInfo<T> typeInfo)
    {
        try
        {
            return await content.ReadFromJsonAsync(typeInfo);
        }
        catch (JsonException)
        {
            throw new InvalidOperationException("VirgoHub received a response it could not read. Install the latest app build and try again.");
        }
        catch (NotSupportedException)
        {
            throw new InvalidOperationException("This app build cannot read the current VirgoHub response. Install the latest app build and try again.");
        }
    }

    private static async Task<HttpResponseMessage> ExecuteRequestAsync(Func<Task<HttpResponseMessage>> action)
    {
        try
        {
            return await action();
        }
        catch (TaskCanceledException)
        {
            throw new InvalidOperationException("VirgoHub could not reach the server in time. Check the server address and try again.");
        }
        catch (HttpRequestException)
        {
            throw new InvalidOperationException("VirgoHub could not connect to the server. Confirm that the web portal is running and that the server address is correct.");
        }
        catch (Exception ex) when (LooksLikeHttpDiagnosticNoise(ex.Message))
        {
            throw new InvalidOperationException("VirgoHub could not complete the network request. Please confirm the server address and try again.");
        }
    }

    private static bool LooksLikeHttpDiagnosticNoise(string? message) =>
        !string.IsNullOrWhiteSpace(message) &&
        message.Contains("net_http_", StringComparison.OrdinalIgnoreCase);
}
