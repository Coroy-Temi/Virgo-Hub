using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using VirgoHub.Shared;
using VirgoHub.WebAdmin.Components;
using VirgoHub.WebAdmin.Services;

var builder = WebApplication.CreateBuilder(args);
var dataProtectionPath = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "DataProtection-Keys");
Directory.CreateDirectory(dataProtectionPath);

builder.Services.Configure<VirgoHubOptions>(builder.Configuration.GetSection("VirgoHub"));
builder.Services.Configure<MailSettings>(builder.Configuration.GetSection("Mail"));
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionPath))
    .SetApplicationName("VirgoHub");
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.Name = "__VirgoHub.AntiXsrf";
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.HttpOnly = true;
});
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "VirgoHub.Auth";
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/login";
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(12);
    });
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddSingleton<VirgoHubService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapGet("/api/health", () => Results.Ok(new
{
    app = "VirgoHub",
    status = "ok",
    timestamp = DateTimeOffset.UtcNow
}));

app.MapGet("/api/platform/bootstrap", (VirgoHubService hub) => Results.Ok(hub.GetPlatformBootstrap()));

app.MapPost("/auth/login", async (HttpContext context, VirgoHubService hub) =>
{
    var form = await context.Request.ReadFormAsync();
    var request = new PortalLoginRequest
    {
        Identifier = form["identifier"].ToString(),
        Password = form["password"].ToString()
    };

    var returnUrl = NormalizeReturnUrl(form["returnUrl"].ToString());
    PortalLoginResult result;
    try
    {
        result = hub.AuthenticatePortalUser(request);
    }
    catch (InvalidOperationException ex)
    {
        return Results.Redirect($"/login?error={Uri.EscapeDataString(ex.Message)}&returnUrl={Uri.EscapeDataString(returnUrl)}");
    }

    if (!result.Success || result.User is null)
    {
        return Results.Redirect($"/login?error={Uri.EscapeDataString(result.Message)}&returnUrl={Uri.EscapeDataString(returnUrl)}");
    }

    var claims = new List<Claim>
    {
        new(ClaimTypes.NameIdentifier, result.User.Id),
        new(ClaimTypes.Name, result.User.FullName),
        new(ClaimTypes.Role, result.User.Role.ToString())
    };

    if (!string.IsNullOrWhiteSpace(result.User.Email))
    {
        claims.Add(new Claim(ClaimTypes.Email, result.User.Email));
    }

    if (!string.IsNullOrWhiteSpace(result.User.StaffId))
    {
        claims.Add(new Claim("staff_id", result.User.StaffId));
    }

    var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
    await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
    return Results.Redirect(ResolvePortalDestination(result.User, returnUrl, hub));
}).DisableAntiforgery();

app.MapGet("/auth/logout", async (HttpContext context) =>
{
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/login?signedOut=1");
});

app.MapPost("/api/auth/send-code", async (SendOtpRequest request, VirgoHubService hub) =>
{
    try
    {
        return Results.Ok(await hub.SendOtpAsync(request));
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new SendOtpResult
        {
            Success = false,
            Message = ex.Message
        });
    }
});

app.MapPost("/api/auth/verify-code", async (VerifyOtpRequest request, VirgoHubService hub) =>
{
    var result = await hub.VerifyOtpAsync(request);
    return result.Success ? Results.Ok(result) : Results.BadRequest(result);
});

app.MapGet("/api/students/{userId}/dashboard", (string userId, HttpContext context, VirgoHubService hub) =>
{
    if (!hub.IsAuthorized(userId, context.Request.Headers["X-Session-Token"]))
    {
        return Results.Unauthorized();
    }

    try
    {
        return Results.Ok(hub.GetStudentDashboard(userId));
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
});

app.MapPost("/api/students/{userId}/bookings", async (string userId, CreateBookingRequest request, HttpContext context, VirgoHubService hub) =>
{
    if (!hub.IsAuthorized(userId, context.Request.Headers["X-Session-Token"]))
    {
        return Results.Unauthorized();
    }

    try
    {
        return Results.Ok(await hub.CreateBookingAsync(userId, request));
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
});

app.MapPost("/api/students/{userId}/bookings/{bookingId}/approve", async (string userId, string bookingId, HttpContext context, VirgoHubService hub) =>
{
    if (!hub.IsAuthorized(userId, context.Request.Headers["X-Session-Token"]))
    {
        return Results.Unauthorized();
    }

    try
    {
        return Results.Ok(await hub.ApproveBookingAsync(userId, bookingId));
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
});

app.MapPost("/api/students/{userId}/complaints", async (string userId, CreateComplaintRequest request, HttpContext context, VirgoHubService hub) =>
{
    if (!hub.IsAuthorized(userId, context.Request.Headers["X-Session-Token"]))
    {
        return Results.Unauthorized();
    }

    try
    {
        return Results.Ok(await hub.CreateComplaintAsync(userId, request));
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
});

app.MapPost("/api/students/{userId}/votes", async (string userId, CastVoteRequest request, HttpContext context, VirgoHubService hub) =>
{
    if (!hub.IsAuthorized(userId, context.Request.Headers["X-Session-Token"]))
    {
        return Results.Unauthorized();
    }

    try
    {
        await hub.CastVoteAsync(userId, request);
        return Results.Ok(new { message = "Vote submitted successfully." });
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
});

app.MapPost("/api/students/{userId}/course-enrollments", async (string userId, CreateCourseEnrollmentRequest request, HttpContext context, VirgoHubService hub) =>
{
    if (!hub.IsAuthorized(userId, context.Request.Headers["X-Session-Token"]))
    {
        return Results.Unauthorized();
    }

    try
    {
        return Results.Ok(await hub.EnrollCourseAsync(userId, request));
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
});

app.MapPost("/api/students/{userId}/personal-groups", async (string userId, CreatePersonalTeachingGroupRequest request, HttpContext context, VirgoHubService hub) =>
{
    if (!hub.IsAuthorized(userId, context.Request.Headers["X-Session-Token"]))
    {
        return Results.Unauthorized();
    }

    try
    {
        return Results.Ok(await hub.CreatePersonalTeachingGroupAsync(userId, request));
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
});

app.MapPost("/api/students/{userId}/personal-groups/join", async (string userId, JoinPersonalTeachingGroupRequest request, HttpContext context, VirgoHubService hub) =>
{
    if (!hub.IsAuthorized(userId, context.Request.Headers["X-Session-Token"]))
    {
        return Results.Unauthorized();
    }

    try
    {
        return Results.Ok(await hub.JoinPersonalTeachingGroupAsync(userId, request));
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
});

app.MapPost("/api/students/{userId}/personal-groups/posts", async (string userId, CreatePersonalGroupPostRequest request, HttpContext context, VirgoHubService hub) =>
{
    if (!hub.IsAuthorized(userId, context.Request.Headers["X-Session-Token"]))
    {
        return Results.Unauthorized();
    }

    try
    {
        return Results.Ok(await hub.CreatePersonalGroupPostAsync(userId, request));
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
});

app.MapPost("/api/students/{userId}/personal-materials", async (string userId, CreatePersonalMaterialRequest request, HttpContext context, VirgoHubService hub) =>
{
    if (!hub.IsAuthorized(userId, context.Request.Headers["X-Session-Token"]))
    {
        return Results.Unauthorized();
    }

    try
    {
        return Results.Ok(await hub.CreatePersonalMaterialAsync(userId, request));
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
});

app.MapPost("/api/students/{userId}/personal-assignments", async (string userId, CreatePersonalAssignmentRequest request, HttpContext context, VirgoHubService hub) =>
{
    if (!hub.IsAuthorized(userId, context.Request.Headers["X-Session-Token"]))
    {
        return Results.Unauthorized();
    }

    try
    {
        return Results.Ok(await hub.CreatePersonalAssignmentAsync(userId, request));
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
});

app.MapPost("/api/students/{userId}/personal-assignments/submit", async (string userId, SubmitPersonalAssignmentRequest request, HttpContext context, VirgoHubService hub) =>
{
    if (!hub.IsAuthorized(userId, context.Request.Headers["X-Session-Token"]))
    {
        return Results.Unauthorized();
    }

    try
    {
        return Results.Ok(await hub.SubmitPersonalAssignmentAsync(userId, request));
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
});

app.MapPost("/api/students/{userId}/personal-assessments", async (string userId, CreatePersonalAssessmentRequest request, HttpContext context, VirgoHubService hub) =>
{
    if (!hub.IsAuthorized(userId, context.Request.Headers["X-Session-Token"]))
    {
        return Results.Unauthorized();
    }

    try
    {
        return Results.Ok(await hub.CreatePersonalAssessmentAsync(userId, request));
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
});

app.MapPost("/api/students/{userId}/personal-assessments/submit", async (string userId, SubmitPersonalAssessmentRequest request, HttpContext context, VirgoHubService hub) =>
{
    if (!hub.IsAuthorized(userId, context.Request.Headers["X-Session-Token"]))
    {
        return Results.Unauthorized();
    }

    try
    {
        return Results.Ok(await hub.SubmitPersonalAssessmentAsync(userId, request));
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
});

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

static string NormalizeReturnUrl(string? value)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        return "/";
    }

    var candidate = Uri.UnescapeDataString(value);
    return candidate.StartsWith('/') && !candidate.StartsWith("//", StringComparison.Ordinal)
        ? candidate
        : "/";
}

static string ResolvePortalDestination(UserProfile user, string requestedUrl, VirgoHubService hub)
{
    var normalized = NormalizeReturnUrl(requestedUrl);
    return IsRouteAllowedForUser(user, normalized, hub)
        ? normalized
        : GetDefaultPortalRoute(user.Role);
}

static bool IsRouteAllowedForUser(UserProfile user, string requestedUrl, VirgoHubService hub)
{
    var routePath = GetRoutePath(requestedUrl);

    return routePath switch
    {
        "/" => user.Role is UserRole.SuperAdmin or UserRole.Admin or UserRole.Porter,
        "/schools" => user.Role == UserRole.PlatformSuperAdmin,
        "/admins" => user.Role == UserRole.SuperAdmin,
        "/students" => user.Role == UserRole.SuperAdmin || hub.CanAccessModule(user.Id, AdminModule.Students),
        "/events" => user.Role == UserRole.SuperAdmin || hub.CanAccessModule(user.Id, AdminModule.Events),
        "/voting" => user.Role == UserRole.SuperAdmin || hub.CanAccessModule(user.Id, AdminModule.Voting),
        "/sports" => user.Role == UserRole.SuperAdmin || hub.CanAccessModule(user.Id, AdminModule.Sports),
        "/library" => user.Role == UserRole.SuperAdmin || user.Role == UserRole.Lecturer || hub.CanAccessModule(user.Id, AdminModule.Library),
        "/complaints" => user.Role == UserRole.SuperAdmin || user.Role == UserRole.Porter || hub.CanAccessModule(user.Id, AdminModule.Complaints),
        "/login" => false,
        _ => false
    };
}

static string GetDefaultPortalRoute(UserRole role) =>
    role switch
    {
        UserRole.PlatformSuperAdmin => "/schools",
        UserRole.Lecturer => "/library",
        UserRole.Porter => "/complaints",
        _ => "/"
    };

static string GetRoutePath(string requestedUrl)
{
    var questionIndex = requestedUrl.IndexOf('?');
    if (questionIndex >= 0)
    {
        requestedUrl = requestedUrl[..questionIndex];
    }

    if (string.IsNullOrWhiteSpace(requestedUrl))
    {
        return "/";
    }

    return requestedUrl.Length > 1
        ? requestedUrl.TrimEnd('/')
        : requestedUrl;
}
