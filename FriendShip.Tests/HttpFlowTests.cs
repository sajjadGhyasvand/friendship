using System.Net;
using System.Text.RegularExpressions;
using FriendShip.Application.Abstractions;
using FriendShip.Application.Common;
using FriendShip.Application.Questions;
using FriendShip.Application.Services;
using FriendShip.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FriendShip.Tests;

public sealed class TestWebFactory(TestDatabase database) : WebApplicationFactory<Program>
{
    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureHostConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Server=.;Database=unused;Integrated Security=True;TrustServerCertificate=True",
            ["SeedQuestionBank"] = "false"
        }));
        return base.CreateHost(builder);
    }
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Server=.;Database=unused;Integrated Security=True;TrustServerCertificate=True",
            ["SeedQuestionBank"] = "false"
        }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IAppDbContextFactory>();
            services.AddSingleton<IAppDbContextFactory>(database);
        });
    }
    public HttpClient NewClient() => CreateClient(new WebApplicationFactoryClientOptions
    { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true });
}

public sealed partial class HttpFlowTests
{
    [Fact]
    public async Task Invalid_link_has_a_friendly_uncached_404_and_empty_registration_is_rejected()
    {
        await using var database = new TestDatabase(); await database.InitializeAsync();
        await using var app = new TestWebFactory(database);
        using var client = app.NewClient();
        var missing = await client.GetAsync("/g/THISLINKDOESNOTEXIST23456");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Contains("این بازی پیدا نشد", WebUtility.HtmlDecode(await missing.Content.ReadAsStringAsync()));
        Assert.True(missing.Headers.CacheControl?.NoStore);
        var html = await client.GetStringAsync("/Ui/Account");
        var rejected = await Post(client, "/Ui/Account?handler=Register", html, ("Input.Email", ""), ("Input.Password", ""));
        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        await using var db = database.Create();
        Assert.Equal(0, await db.Users.CountAsync());
    }

    private static string Field(string html, string name)
    {
        var input = Regex.Match(html, "<input[^>]*name=\"" + Regex.Escape(name) + "\"[^>]*>").Value;
        return WebUtility.HtmlDecode(Regex.Match(input, "value=\"([^\"]*)\"").Groups[1].Value);
    }
    private static async Task<HttpResponseMessage> Post(HttpClient client, string url, string html, params (string, string)[] data)
    {
        var fields = data.ToDictionary(x => x.Item1, x => x.Item2);
        fields["__RequestVerificationToken"] = Field(html, "__RequestVerificationToken");
        return await client.PostAsync(url, new FormUrlEncodedContent(fields));
    }
    private static async Task Register(HttpClient client, string name)
    {
        var html = await client.GetStringAsync("/Ui/Account");
        var response = await Post(client, "/Ui/Account?handler=Register", html, ("Input.Email", name), ("Input.Password", "IntegrationPass!123"));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    [Fact]
    public async Task Real_Razor_flow_guest_csrf_score_private_access_share_and_pause()
    {
        await using var database = new TestDatabase(); await database.InitializeAsync();
        await using var app = new TestWebFactory(database);
        using var owner = app.NewClient(); using var friend = app.NewClient(); using var stranger = app.NewClient();
        var home = await owner.GetStringAsync("/Ui/Index");
        Assert.Contains("game-theme.css?v=", home);
        Assert.Contains("class=\"hero-cast\"", home);
        Assert.Contains("class=\"mobile-dock\"", home);
        Assert.Equal(HttpStatusCode.Redirect, (await owner.GetAsync("/Ui/Game/Index")).StatusCode);
        await Register(owner, "http_owner");
        var emptyDashboard = await owner.GetStringAsync("/Ui/Game/Index");
        Assert.Contains("recovery-code", emptyDashboard);
        var createHtml = await owner.GetStringAsync("/Ui/Game/Create");
        var data = GameTests.Answers().Select(a => ($"Answers[{a.Key}]", a.Value.ToString())).Append(("NewRound", "false")).ToArray();
        var create = await Post(owner, "/Ui/Game/Create", createHtml, data);
        Assert.Equal(HttpStatusCode.Redirect, create.StatusCode);
        var dashboard = await owner.GetStringAsync("/Ui/Game/Index");
        var link = WebUtility.HtmlDecode(Regex.Match(dashboard, "id=\"invite-link\"[^>]*value=\"([^\"]*)\"").Groups[1].Value);
        Assert.StartsWith("https://localhost/g/", link);
        Assert.True((await owner.GetAsync("/Ui/Game/Index")).Headers.CacheControl?.NoStore);
        var resolve = await friend.GetAsync(link); Assert.Equal(HttpStatusCode.Redirect, resolve.StatusCode);
        var joinUrl = resolve.Headers.Location!.ToString();
        var joinHtml = await friend.GetStringAsync(joinUrl);
        var code = Field(joinHtml, "Code"); Assert.Equal(24, code.Length);
        var joined = await Post(friend, joinUrl, joinHtml, ("Code", code), ("DisplayName", "رفیق آزمون"), ("AvatarKey", FriendShip.Domain.Users.AvatarCatalog.DefaultKey));
        Assert.Equal(HttpStatusCode.Redirect, joined.StatusCode);
        var playHtml = await friend.GetStringAsync($"/Ui/Play?code={code}");
        Assert.Contains("page-play is-focused", playHtml);
        Assert.Contains("class=\"question-scene\"", playHtml);
        Assert.Equal(4, Regex.Matches(playHtml, "class=\"live-option game-option\"").Count);
        Assert.DoesNotContain("class=\"mobile-dock\"", playHtml);
        var firstId = Field(playHtml, "RoomQuestionId");
        var csrf = await friend.PostAsync("/Ui/Play", new FormUrlEncodedContent(new Dictionary<string,string> { ["Code"] = code, ["RoomQuestionId"] = firstId, ["OptionId"] = Guid.NewGuid().ToString() }));
        Assert.Equal(HttpStatusCode.BadRequest, csrf.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await stranger.GetAsync($"/Ui/Rooms/Manage/{code}")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await stranger.GetAsync($"/Ui/Rooms/Insights?code={code}")).StatusCode);
        await using var db = database.Create();
        var room = await db.Rooms.Include(r => r.Questions).ThenInclude(q => q.Options).FirstAsync(r => r.Code == code);
        var answers = room.Questions.ToDictionary(q => q.Id.ToString(), q => q.OwnerAnswerOptionId!.Value.ToString());
        for (var i = 0; i < 10; i++)
        {
            playHtml = await friend.GetStringAsync($"/Ui/Play?code={code}");
            var id = Field(playHtml, "RoomQuestionId");
            var submit = await Post(friend, "/Ui/Play", playHtml, ("Code", code), ("RoomQuestionId", id), ("OptionId", answers[id]));
            Assert.Equal(HttpStatusCode.Redirect, submit.StatusCode);
        }
        var result = await friend.GetStringAsync($"/Ui/Rooms/Results?code={code}");
        Assert.Contains("100", result); Assert.DoesNotContain("انتخاب میزبان:", WebUtility.HtmlDecode(result));
        dashboard = await owner.GetStringAsync("/Ui/Game/Index");
        Assert.Contains("رفیق آزمون", WebUtility.HtmlDecode(dashboard)); Assert.Contains("score-bar", dashboard);
        var shareUrl = WebUtility.HtmlDecode(Regex.Match(result, "href=\"([^\"]*/Ui/Share[^\"]*)\"").Groups[1].Value);
        var share = await stranger.GetStringAsync(shareUrl); Assert.Contains("data-story-card", share);
        Assert.DoesNotContain("OwnerAnswerOptionId", share);
        Assert.Equal(HttpStatusCode.Redirect, (await Post(owner, "/Ui/Game/Index?handler=Pause", dashboard)).StatusCode);
        using var lateFriend = app.NewClient(); var pausedJoin = await lateFriend.GetStringAsync(joinUrl);
        Assert.DoesNotContain("name=\"DisplayName\"", pausedJoin);
        await Register(stranger, "http_other");
        var privateView = await stranger.GetAsync($"/Ui/Rooms/Manage/{code}"); Assert.Equal(HttpStatusCode.NotFound, privateView.StatusCode);
        var otherHtml = await stranger.GetStringAsync("/Ui/Game/Create");
        var delete = await Post(stranger, "/Ui/Game/Index?handler=Delete", otherHtml, ("code", code), ("confirmDelete", "true"));
        Assert.Equal(HttpStatusCode.Redirect, delete.StatusCode);
        Assert.NotNull(await new RoomService(database, new InMemoryRateLimiter(TimeProvider.System), TimeProvider.System).GetPublicAsync(new Caller(null, Guid.NewGuid(), "ip", false), code));
    }
}
