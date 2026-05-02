using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using SoccerCheckin.Web.Data;
using SoccerCheckin.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

var connectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? Environment.GetEnvironmentVariable("POSTGRES_CONNECTION")
    ?? throw new InvalidOperationException("Postgres connection string is not configured.");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// Add authentication services
var clientId = builder.Configuration["Authentication:Microsoft:ClientId"] 
    ?? Environment.GetEnvironmentVariable("MICROSOFT_CLIENT_ID")
    ?? "your_client_id";
var clientSecret = builder.Configuration["Authentication:Microsoft:ClientSecret"]
    ?? Environment.GetEnvironmentVariable("MICROSOFT_CLIENT_SECRET")
    ?? "your_client_secret";

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
    })
    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/LogOut";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
    })
    .AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, options =>
    {
        options.Authority = "https://login.microsoftonline.com/consumers/v2.0";
        options.ClientId = clientId;
        options.ClientSecret = clientSecret;
        options.ResponseType = "code";
        options.CallbackPath = "/signin-microsoft";
        options.SignedOutRedirectUri = "/";
        options.Scope.Clear();
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("email");
        options.SaveTokens = true;
        options.GetClaimsFromUserInfoEndpoint = true;
    });

// Require authenticated user by default for all endpoints.
// Use [AllowAnonymous] on actions/controllers that should be public.
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build())
    .AddPolicy("AdminOnly", p => p.Requirements.Add(new AdminRequirement()));

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<IAuthorizationHandler, AdminAuthorizationHandler>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    dbContext.Database.Migrate();

    // Seed admin users from configuration (Admins:Emails)
    var adminEmails = builder.Configuration.GetSection("Admins:Emails").Get<string[]>() ?? Array.Empty<string>();
    foreach (var email in adminEmails.Where(e => !string.IsNullOrWhiteSpace(e)))
    {
        var existing = dbContext.UserSessions.FirstOrDefault(u => u.MicrosoftEmail == email);
        if (existing == null)
        {
            dbContext.UserSessions.Add(new SoccerCheckin.Web.Models.UserSession
            {
                MicrosoftEmail = email,
                Role = SoccerCheckin.Web.Models.UserRole.Admin,
                CreatedAtUtc = DateTime.UtcNow,
                LastLoginUtc = DateTime.UtcNow
            });
        }
        else if (existing.Role != SoccerCheckin.Web.Models.UserRole.Admin)
        {
            existing.Role = SoccerCheckin.Web.Models.UserRole.Admin;
        }
    }
    dbContext.SaveChanges();
}

app.Run();
