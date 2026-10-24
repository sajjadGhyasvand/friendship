using FriendShip.Application;
using FriendShip.Infrastructure;
using FriendShip.Web.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using FriendShip.Application.Abstractions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.Name = "__Host-FriendShip.Antiforgery";
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.Path = "/";
});
builder.Services.AddHttpContextAccessor();

builder.Services.AddDataProtection()
    .SetApplicationName("FriendShip");

builder.Services.AddScoped<GuestIdentity>();
builder.Services.AddScoped<AccountCookieEvents>();

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "__Host-FriendShip.Account";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.Path = "/";

        options.LoginPath = "/Ui/Account";
        options.AccessDeniedPath = "/Ui/Account";

        options.ExpireTimeSpan = TimeSpan.FromHours(12);
        options.SlidingExpiration = true;
        options.EventsType = typeof(AccountCookieEvents);
    });

builder.Services.AddAuthorization();
builder.Services.AddApplication();

var connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Connection string 'DefaultConnection' is missing.");

builder.Services.AddInfrastructure(connectionString);

var app = builder.Build();
if (app.Configuration.GetValue<bool>("SeedQuestionBank"))
{
    await using var scope = app.Services.CreateAsyncScope();

    var dbFactory = scope.ServiceProvider
        .GetRequiredService<IAppDbContextFactory>();

    await QuestionBankSeeder.SeedAsync(dbFactory);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "same-origin";
    await next();
});
app.UseStaticFiles();

app.UseRouting();

// ترتیب این دو مهم است.
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

app.Run();

public partial class Program { }
