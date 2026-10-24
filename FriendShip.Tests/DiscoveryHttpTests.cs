using System.Net;
using FriendShip.Application.Discovery;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FriendShip.Tests;

public sealed partial class HttpFlowTests
{
    [Fact]
    public async Task Unified_portrait_feedback_json_and_story_selection_enforce_ownership_confirmation_and_csrf()
    {
        await using var database = new TestDatabase(); await database.InitializeAsync();
        await using var app = new TestWebFactory(database);
        using var owner = app.NewClient(); using var stranger = app.NewClient();
        Assert.Equal(HttpStatusCode.Redirect, (await stranger.GetAsync("/Ui/Discover/Story")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await stranger.GetAsync("/Ui/Game/Story")).StatusCode);
        await Register(owner, "portrait_http"); await ConfigureDiscovery(owner); await AnswerDiscovery(owner, "animal");
        var portrait = await owner.GetStringAsync("/Ui/Discover");
        Assert.Contains("چه جور آدمی هستی", WebUtility.HtmlDecode(portrait));
        Assert.Contains("data-fact-feedback", portrait); Assert.DoesNotContain("interest-grid", portrait); Assert.DoesNotContain("trait-row", portrait);
        Assert.DoesNotContain("mbti-axes", portrait); Assert.DoesNotContain("OptionIndex", portrait);
        var initial = await owner.GetStringAsync("/Ui/Discover/Story"); Assert.DoesNotContain("narrative-data", initial);
        await using var db = database.Create();
        var profile = await db.DiscoveryProfiles.AsNoTracking().SingleAsync();
        var evidence = new DiscoveryEngine().ReadEvidence(await db.DiscoveryAnswers.AsNoTracking().ToListAsync(), null, profile);
        var fact = Assert.Single(new DiscoveryEngine().Analyze(evidence, false).Facts);
        owner.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        var noCsrf = await owner.PostAsync("/Ui/Discover?handler=Decision", new FormUrlEncodedContent(new Dictionary<string, string> { ["key"] = fact.Id, ["state"] = "Confirmed" }));
        Assert.Equal(HttpStatusCode.BadRequest, noCsrf.StatusCode);
        var confirmed = await Post(owner, "/Ui/Discover?handler=Decision", portrait, ("key", fact.Id), ("state", "Confirmed"));
        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
        Assert.Contains("Confirmed", await confirmed.Content.ReadAsStringAsync());
        var story = await owner.GetAsync("/Ui/Discover/Story"); Assert.True(story.Headers.CacheControl?.NoStore);
        var html = await story.Content.ReadAsStringAsync(); Assert.Contains("narrative-data", html); Assert.Contains("animals.cat", html);
        Assert.DoesNotContain("QuestionVersion", html); Assert.DoesNotContain("Evidence", html); Assert.DoesNotContain("OptionIndex", html);
        Assert.DoesNotContain(profile.UserId.ToString(), html);
        var partial = await Post(owner, "/Ui/Discover?handler=Decision", portrait, ("key", fact.Id), ("state", "Partial"));
        Assert.Equal(HttpStatusCode.OK, partial.StatusCode);
        Assert.Contains("Partial", await partial.Content.ReadAsStringAsync());
        Assert.DoesNotContain("narrative-data", await owner.GetStringAsync("/Ui/Discover/Story"));
        var partialPage = await owner.GetStringAsync("/Ui/Discover");
        Assert.Contains("value=\"Partial\" aria-pressed=\"true\"", partialPage);
        Assert.Contains("data-portrait-coverage", partialPage);
        await Post(owner, "/Ui/Discover?handler=Decision", portrait, ("key", fact.Id), ("state", "Confirmed"));
        var empty = await owner.GetStringAsync("/Ui/Discover/Story?choose=true"); Assert.DoesNotContain("narrative-data", empty);
        var forged = await owner.GetStringAsync("/Ui/Discover/Story?choose=true&fact=not-owned"); Assert.DoesNotContain("narrative-data", forged);
        var rejected = await Post(owner, "/Ui/Discover?handler=Decision", portrait, ("key", fact.Id), ("state", "Rejected"));
        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        Assert.DoesNotContain("narrative-data", await owner.GetStringAsync("/Ui/Discover/Story"));
        Assert.Equal(1, await db.AnalysisDecisions.CountAsync());
        var bad = await Post(owner, "/Ui/Discover?handler=Decision", portrait, ("key", fact.Id), ("state", "777"));
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        await Register(stranger, "portrait_other"); await ConfigureDiscovery(stranger);
        var otherHtml = await stranger.GetStringAsync("/Ui/Discover/Settings");
        stranger.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        var attack = await Post(stranger, "/Ui/Discover?handler=Decision", otherHtml, ("key", fact.Id), ("state", "Confirmed"), ("UserId", profile.UserId.ToString()));
        Assert.Equal(HttpStatusCode.BadRequest, attack.StatusCode);
        Assert.Equal(FriendShip.Domain.Discovery.AnalysisDecisionState.Rejected, (await db.AnalysisDecisions.AsNoTracking().SingleAsync()).State);
    }
    [Fact]
    public async Task Aggregate_permission_is_explicit_and_persisted_through_the_existing_settings_form()
    {
        await using var database = new TestDatabase(); await database.InitializeAsync();
        await using var app = new TestWebFactory(database); using var owner = app.NewClient();
        await Register(owner, "consent_http"); await ConfigureDiscovery(owner, true);
        var html = await owner.GetStringAsync("/Ui/Discover/Settings");
        Assert.Contains("name=\"aggregateSharing\"", html);
        await using var db = database.Create();
        Assert.False((await db.DiscoveryProfiles.AsNoTracking().SingleAsync()).AllowAggregateSharing);
        var allow = await Post(owner, "/Ui/Discover?handler=Configure", html, ("version", Field(html, "version")),
            ("enabled", "true"), ("comparison", "true"), ("aggregateSharing", "true"));
        Assert.Equal(HttpStatusCode.Redirect, allow.StatusCode);
        Assert.True((await db.DiscoveryProfiles.AsNoTracking().SingleAsync()).AllowAggregateSharing);
        await ConfigureDiscovery(owner, true);
        var settings = await db.DiscoveryProfiles.AsNoTracking().SingleAsync();
        Assert.False(settings.AllowAggregateSharing); Assert.True(settings.AllowComparison);
    }
    private static async Task ConfigureDiscovery(HttpClient client, bool compare = false)
    {
        var html = await client.GetStringAsync("/Ui/Discover/Settings");
        var result = await Post(client, "/Ui/Discover?handler=Configure", html, ("version", Field(html, "version")),
            ("enabled", "true"), ("comparison", compare.ToString()));
        Assert.Equal(HttpStatusCode.Redirect, result.StatusCode);
    }
    private static async Task AnswerDiscovery(HttpClient client, string key, string? text = null)
    {
        var html = await client.GetStringAsync("/Ui/Discover/Question?key=" + key);
        var result = await Post(client, "/Ui/Discover/Question", html, ("Key", key), ("Version", Field(html, "Version")),
            ("QuestionVersion", DiscoveryQuestion.Version), ("Option", "0"), ("Text", text ?? ""));
        Assert.Equal(HttpStatusCode.Redirect, result.StatusCode);
    }
    [Fact]
    public async Task Discovery_Razor_flow_enforces_csrf_privacy_explicit_publication_and_mutual_comparison()
    {
        await using var database = new TestDatabase(); await database.InitializeAsync();
        await using var app = new TestWebFactory(database);
        using var owner = app.NewClient(); using var friend = app.NewClient(); using var visitor = app.NewClient();
        Assert.Equal(HttpStatusCode.Redirect, (await visitor.GetAsync("/Ui/Discover")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await visitor.GetAsync("/Ui/Discover/Facts")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await visitor.GetAsync("/f/23456789ABCDEFGHJKLMNPQRS")).StatusCode);
        await Register(owner, "discovery_http"); await Register(friend, "discovery_friend");
        var initial = await owner.GetAsync("/Ui/Discover"); Assert.True(initial.Headers.CacheControl?.NoStore);
        Assert.Contains("کشف خصوصی رو شروع کنیم", WebUtility.HtmlDecode(await initial.Content.ReadAsStringAsync()));
        var csrf = await owner.PostAsync("/Ui/Discover?handler=Configure", new FormUrlEncodedContent(new Dictionary<string, string> { ["enabled"] = "true" }));
        Assert.Equal(HttpStatusCode.BadRequest, csrf.StatusCode);
        await ConfigureDiscovery(owner);
        await AnswerDiscovery(owner, "music"); await AnswerDiscovery(owner, "music-context");
        const string hostile = "<img src=x onerror=alert(1)>";
        await AnswerDiscovery(owner, "music-artist", hostile);
        Assert.Equal(HttpStatusCode.Redirect, (await owner.GetAsync("/Ui/Discover/Facts")).StatusCode);
        var facts = await owner.GetStringAsync("/Ui/Discover");
        Assert.DoesNotContain(hostile, facts); Assert.DoesNotContain("&lt;img", facts);
        var edit = await owner.GetStringAsync("/Ui/Discover/Question?key=music-artist");
        Assert.DoesNotContain(hostile, edit); Assert.Contains("&lt;img", edit);
        await using var db = database.Create();
        var account = (await db.DiscoveryProfiles.SingleAsync()).UserId;
        Assert.Empty(await db.AnalysisDecisions.ToListAsync());
        var answers = await db.DiscoveryAnswers.AsNoTracking().ToListAsync();
        var settings = await db.DiscoveryProfiles.AsNoTracking().SingleAsync();
        var engine = new DiscoveryEngine(); var fact = Assert.Single(engine.Analyze(engine.ReadEvidence(answers, null, settings), false).Facts);
        var intruderHtml = await friend.GetStringAsync("/Ui/Discover");
        var intrude = await Post(friend, "/Ui/Discover/Facts?handler=Share", intruderHtml, ("key", fact.Id), ("allow", "true"), ("UserId", account.ToString()));
        Assert.Equal(HttpStatusCode.Redirect, intrude.StatusCode); Assert.Empty(await db.AnalysisDecisions.AsNoTracking().ToListAsync());
        var publish = await Post(owner, "/Ui/Discover/Facts?handler=Share", facts, ("key", fact.Id), ("allow", "true"));
        Assert.Equal(HttpStatusCode.Redirect, publish.StatusCode);
        var code = (await db.AnalysisDecisions.AsNoTracking().SingleAsync()).ShareCode!;
        var shared = await visitor.GetAsync("/f/" + code); Assert.Equal(HttpStatusCode.OK, shared.StatusCode); Assert.True(shared.Headers.CacheControl?.NoStore);
        var publicHtml = await shared.Content.ReadAsStringAsync();
        Assert.Contains("data-card-kind=\"fact\"", publicHtml); Assert.DoesNotContain(hostile, publicHtml);
        Assert.DoesNotContain(account.ToString(), publicHtml); Assert.DoesNotContain("بر چه اساسی", WebUtility.HtmlDecode(publicHtml));
        Assert.DoesNotContain("QuestionVersion", publicHtml);
        await AnswerDiscovery(owner, "music", ""); // identical replay leaves publication valid
        Assert.Equal(HttpStatusCode.OK, (await visitor.GetAsync("/f/" + code)).StatusCode);
        var questionHtml = await owner.GetStringAsync("/Ui/Discover/Question?key=music");
        await Post(owner, "/Ui/Discover/Question", questionHtml, ("Key", "music"), ("Version", Field(questionHtml, "Version")),
            ("QuestionVersion", DiscoveryQuestion.Version), ("Option", "1"));
        Assert.Equal(HttpStatusCode.NotFound, (await visitor.GetAsync("/f/" + code)).StatusCode);
        await ConfigureDiscovery(owner, true);
        var comparisonCode = (await db.DiscoveryProfiles.AsNoTracking().SingleAsync(p => p.UserId == account)).ComparisonCode!;
        Assert.Equal(HttpStatusCode.NotFound, (await friend.GetAsync("/Ui/Discover/Compare/" + comparisonCode)).StatusCode);
        await ConfigureDiscovery(friend, true);
        await AnswerDiscovery(friend, "music"); await AnswerDiscovery(friend, "music-context");
        var comparison = await friend.GetAsync("/Ui/Discover/Compare/" + comparisonCode); Assert.Equal(HttpStatusCode.OK, comparison.StatusCode);
        var comparisonHtml = WebUtility.HtmlDecode(await comparison.Content.ReadAsStringAsync());
        Assert.Contains("ترجیح مشترک", comparisonHtml); Assert.DoesNotContain(hostile, comparisonHtml);
        Assert.DoesNotContain("comparison-list", comparisonHtml); Assert.DoesNotContain("comparison-row", comparisonHtml);
        Assert.Equal(HttpStatusCode.Redirect, (await visitor.GetAsync("/Ui/Discover/Compare/" + comparisonCode)).StatusCode);
        var resetHtml = await owner.GetStringAsync("/Ui/Discover");
        var reset = await Post(owner, "/Ui/Discover?handler=Reset", resetHtml, ("confirmReset", "true")); Assert.Equal(HttpStatusCode.Redirect, reset.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await friend.GetAsync("/Ui/Discover/Compare/" + comparisonCode)).StatusCode);
        Assert.Empty(await db.DiscoveryAnswers.AsNoTracking().Where(a => a.UserId == account).ToListAsync());
    }
}
