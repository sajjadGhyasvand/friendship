using FriendShip.Application.Abstractions;
using FriendShip.Application.Common;
using FriendShip.Application.Discovery;
using FriendShip.Domain.Common;
using FriendShip.Domain.Discovery;
using Microsoft.EntityFrameworkCore;

namespace FriendShip.Application.Services;

/// <summary>Own answers and consents only. Never derives a user's tastes from a friend's guesses.</summary>
public sealed class DiscoveryService(IAppDbContextFactory dbf, DiscoveryEngine engine,
    AdaptiveQuestionSelector selector, IRateLimiter limiter, TimeProvider time)
{
    private DateTime Now => time.GetUtcNow().UtcDateTime;
    public async Task<DiscoveryView?> GetMineAsync(Caller caller)
    {
        if (caller.UserId is not Guid id) return null;
        await using var db = dbf.Create();
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id && !u.IsBanned);
        if (user is null) return null;
        var settings = await db.DiscoveryProfiles.AsNoTracking().SingleOrDefaultAsync(p => p.UserId == id);
        if (settings is not { Enabled: true }) return new(settings, null, [], user.DisplayName, 0);
        var answers = await db.DiscoveryAnswers.AsNoTracking().Where(a => a.UserId == id).ToListAsync();
        var game = user.CurrentGameId is Guid gameId ? await db.Rooms.AsNoTracking().Include(r => r.Questions).ThenInclude(q => q.Options)
            .SingleOrDefaultAsync(r => r.Id == gameId && r.OwnerUserId == id) : null;
        var evidence = engine.ReadEvidence(answers, game, settings);
        var decisions = await db.AnalysisDecisions.AsNoTracking().Where(d => d.UserId == id).ToListAsync();
        var analysis = engine.Analyze(evidence, settings.AllowMbti, decisions);
        return new(settings, analysis, answers, user.DisplayName,
            selector.Available(evidence, answers.Select(a => a.QuestionKey), settings.AllowRelationships, settings.AllowMbti).Count);
    }

    public Task<Result> ConfigureAsync(Caller caller, bool enabled, bool comparison, bool relationships, bool mbti, Guid? version, bool aggregateSharing = false) =>
        Result.RunAsync(async () =>
        {
            if (caller.UserId is not Guid id) return Result.Fail("ابتدا وارد حساب شو.");
            await using var db = dbf.Create();
            if (!await db.Users.AnyAsync(u => u.Id == id && !u.IsBanned)) return Result.Fail("حساب در دسترس نیست.");
            var settings = await db.DiscoveryProfiles.SingleOrDefaultAsync(p => p.UserId == id);
            if (settings is not null && settings.Version != version) return Result.Fail("تنظیمات عوض شده؛ صفحه را تازه کن.");
            if (settings is null) { settings = DiscoveryProfile.Create(id); db.DiscoveryProfiles.Add(settings); }
            settings.Configure(enabled, comparison, relationships, mbti, Now, aggregateSharing);
            // Changing consent revokes published artifacts. Re-enabling never resurrects an old URL.
            foreach (var decision in await db.AnalysisDecisions.Where(d => d.UserId == id && d.ShareCode != null).ToListAsync())
                decision.Share(false, Now);
            if (!settings.AllowRelationships)
                db.DiscoveryAnswers.RemoveRange(await db.DiscoveryAnswers.Where(a => a.UserId == id && a.IsSensitive).ToListAsync());
            if (!settings.AllowMbti)
            {
                var keys = DiscoveryCatalog.All.Where(q => q.MbtiOnly).Select(q => q.Key).ToArray();
                db.DiscoveryAnswers.RemoveRange(await db.DiscoveryAnswers.Where(a => a.UserId == id && keys.Contains(a.QuestionKey)).ToListAsync());
            }
            try { await db.SaveChangesAsync(); }
            catch (DbUpdateException ex) when (ex is not DbUpdateConcurrencyException)
            {
                await using var verify = dbf.Create();
                if (!await verify.DiscoveryProfiles.AnyAsync(p => p.UserId == id)) throw;
                return Result.Fail("هم‌زمان تنظیمی ثبت شد؛ صفحه را تازه کن.");
            }
            return Result.Ok();
        });

    public Task<Result> AnswerAsync(Caller caller, string key, string questionVersion, int? option, string? text,
        bool skip, Guid version) => Result.RunAsync(async () =>
    {
        if (caller.UserId is not Guid id) return Result.Fail("ابتدا وارد حساب شو.");
        if (!limiter.TryAcquire($"discovery-answer:{id}", 80, TimeSpan.FromMinutes(1))) return Result.Fail("کمی صبر کن و دوباره امتحان کن.");
        var question = DiscoveryCatalog.Find(key);
        if (question is null || questionVersion != DiscoveryQuestion.Version) return Result.Fail("سؤال یا نسخه آن معتبر نیست؛ صفحه را تازه کن.");
        await using var db = dbf.Create();
        var settings = await db.DiscoveryProfiles.SingleOrDefaultAsync(p => p.UserId == id && p.Enabled);
        if (settings is null || !await db.Users.AnyAsync(u => u.Id == id && !u.IsBanned)) return Result.Fail("ابتدا تحلیل خصوصی را فعال کن.");
        if (question.Sensitive && !settings.AllowRelationships || question.MbtiOnly && !settings.AllowMbti)
            return Result.Fail("برای این دسته اجازه نداده‌ای.");
        string? value = null;
        if (!skip)
        {
            if (question.FreeText)
            {
                if (string.IsNullOrWhiteSpace(text) || text.Trim().Length > 100 || text.Any(char.IsControl)) return Result.Fail("پاسخ متنی بین ۱ تا ۱۰۰ حرف باشد.");
                value = text.Trim(); option = null;
            }
            else
            {
                if (option is not int choice || choice < 0 || choice >= question.Options.Length) return Result.Fail("یک گزینه معتبر انتخاب کن یا سؤال را رد کن.");
                value = question.Options[choice];
            }
        }
        var answer = await db.DiscoveryAnswers.SingleOrDefaultAsync(a => a.UserId == id && a.QuestionKey == key);
        if (answer is not null && answer.QuestionVersion == questionVersion && answer.IsSkipped == skip && answer.Value == value)
            return Result.Ok(); // Replayed identical POST does not duplicate evidence or invalidate a confirmation.
        if (settings.Version != version) return Result.Fail("هم‌زمان تغییری ثبت شده؛ سؤال را دوباره باز کن.");
        if (answer is null) { answer = DiscoveryAnswer.Create(id, key); db.DiscoveryAnswers.Add(answer); }
        answer.Record(questionVersion, question.Prompt, option, value, skip, question.Sensitive, Now);
        settings.Touch(Now);
        await db.SaveChangesAsync();
        return Result.Ok();
    });

    public Task<Result> DecideAsync(Caller caller, string key, AnalysisDecisionState state, string? correction) => Result.RunAsync(async () =>
    {
        if (caller.UserId is not Guid accountId) return Result.Fail("ابتدا وارد حساب شو.");
        if (!limiter.TryAcquire($"discovery-feedback:{accountId}", 80, TimeSpan.FromMinutes(1))) return Result.Fail("کمی صبر کن و دوباره امتحان کن.");
        var view = await GetMineAsync(caller);
        if (caller.UserId is not Guid id || view?.Analysis is not { } analysis) return Result.Fail("تحلیل در دسترس نیست.");
        if (!Enum.IsDefined(state)) return Result.Fail("تصمیم معتبر نیست.");
        var fact = analysis.Facts.FirstOrDefault(f => f.Id == key);
        var isMbti = analysis.Mbti?.ResultKey == key;
        if (fact is null && !isMbti) return Result.Fail("نتیجه عوض شده؛ صفحه را تازه کن.");
        if (state == AnalysisDecisionState.Partial && fact is null) return Result.Fail("این گزینه فقط برای فکت‌هاست.");
        if (state == AnalysisDecisionState.Corrected)
        {
            correction = correction?.Trim().ToUpperInvariant();
            if (!isMbti || correction is null || correction.Length != 4 || !"EI".Contains(correction[0]) ||
                !"SN".Contains(correction[1]) || !"TF".Contains(correction[2]) || !"JP".Contains(correction[3])) return Result.Fail("یک تیپ چهارحرفی معتبر انتخاب کن.");
        }
        else correction = null;
        await using var db = dbf.Create();
        var settings = await db.DiscoveryProfiles.SingleAsync(p => p.UserId == id);
        if (settings.Version != view.Settings!.Version) return Result.Fail("پاسخ‌ها عوض شده‌اند؛ صفحه را تازه کن.");
        var decision = await db.AnalysisDecisions.SingleOrDefaultAsync(d => d.UserId == id && d.ResultKey == key);
        if (decision is not null && decision.State == state && decision.Correction == correction) return Result.Ok();
        if (decision is null) { decision = AnalysisDecision.Create(id, key); db.AnalysisDecisions.Add(decision); }
        if (fact is { RuleKey.Length: > 0 }) decision.DescribeFact(fact.RuleKey, PersonalityComposer.Version, fact.SampleCount);
        decision.Set(state, correction, Now); settings.Touch(Now);
        await db.SaveChangesAsync(); return Result.Ok();
    });

    /// <summary>Internal quality report; no public dashboard or personal evidence. One latest vote per account/rule/version.</summary>
    public async Task<IReadOnlyList<FeedbackQuality>> GetFeedbackQualityAsync(Caller caller)
    {
        if (caller.UserId is not Guid id || !caller.IsAdmin) return [];
        await using var db = dbf.Create();
        if (!await db.Users.AnyAsync(u => u.Id == id && !u.IsBanned && u.Role == FriendShip.Domain.Users.UserRole.Admin)) return [];
        var votes = await db.AnalysisDecisions.AsNoTracking().Where(d => d.RuleKey != null && d.CopyVersion != null && d.FeedbackAt != null &&
            d.State != null && d.State != AnalysisDecisionState.Corrected)
            .Select(d => new { d.UserId, d.ResultKey, d.RuleKey, d.CopyVersion, d.State, d.FeedbackAt }).ToListAsync();
        return votes.GroupBy(v => new { v.UserId, v.RuleKey, v.CopyVersion })
            .Select(g => g.OrderByDescending(v => v.FeedbackAt).ThenBy(v => v.ResultKey).First())
            .GroupBy(v => new { v.RuleKey, v.CopyVersion }).Where(g => g.Count() >= 5)
            .Select(g => new FeedbackQuality(g.Key.RuleKey!, g.Key.CopyVersion!, g.Count(),
                g.Count(v => v.State == AnalysisDecisionState.Confirmed), g.Count(v => v.State == AnalysisDecisionState.Partial),
                g.Count(v => v.State == AnalysisDecisionState.Rejected),
                (int)Math.Round(g.Count(v => v.State == AnalysisDecisionState.Confirmed) * 100.0 / g.Count(), MidpointRounding.AwayFromZero)))
            .OrderBy(v => v.RuleKey).ThenBy(v => v.CopyVersion).ToArray();
    }

    public Task<Result<string?>> ShareAsync(Caller caller, string key, bool allow) => Result<string?>.RunAsync(async () =>
    {
        var view = await GetMineAsync(caller);
        if (caller.UserId is not Guid id || view?.Analysis is not { } analysis) return Result<string?>.Fail("تحلیل در دسترس نیست.");
        var fact = analysis.Facts.FirstOrDefault(f => f.Id == key);
        if (allow && (fact is null || !fact.CanShare || fact.Sensitive || fact.Decision == AnalysisDecisionState.Rejected))
            return Result<string?>.Fail("این فکت قابل انتشار نیست یا شواهد آن تغییر کرده.");
        await using var db = dbf.Create();
        var settings = await db.DiscoveryProfiles.SingleAsync(p => p.UserId == id);
        if (!settings.Enabled || settings.Version != view.Settings!.Version) return Result<string?>.Fail("اجازه یا پاسخ‌ها عوض شده‌اند؛ صفحه را تازه کن.");
        var decision = await db.AnalysisDecisions.SingleOrDefaultAsync(d => d.UserId == id && d.ResultKey == key);
        if (decision is null) { decision = AnalysisDecision.Create(id, key); db.AnalysisDecisions.Add(decision); }
        decision.Share(allow, Now); settings.Touch(Now);
        await db.SaveChangesAsync(); return Result<string?>.Ok(decision.ShareCode);
    });

    public async Task<SharedFact?> GetSharedFactAsync(string code)
    {
        var normalized = ShortCode.Normalize(code);
        if (!ShortCode.IsValid(normalized, 24)) return null;
        await using var db = dbf.Create();
        var record = await db.AnalysisDecisions.AsNoTracking().SingleOrDefaultAsync(d => d.ShareCode == normalized);
        if (record is null) return null;
        var caller = new Caller(record.UserId, Guid.Empty, "shared-read", false);
        var view = await GetMineAsync(caller);
        var fact = view?.Analysis?.Facts.FirstOrDefault(f => f.Id == record.ResultKey && f.CanShare && !f.Sensitive &&
            f.Decision != AnalysisDecisionState.Rejected && f.ShareCode == normalized);
        // Do not return raw evidence, account IDs, hidden answers, or private links on the public DTO.
        return fact is null ? null : new(view!.OwnerName, fact with { Evidence = [] }, DiscoveryEngine.Version);
    }

    public Task<Result> ResetAsync(Caller caller) => Result.RunAsync(async () =>
    {
        if (caller.UserId is not Guid id) return Result.Fail("ابتدا وارد شو.");
        await using var db = dbf.Create();
        var profile = await db.DiscoveryProfiles.SingleOrDefaultAsync(p => p.UserId == id);
        if (profile is not null) { db.DiscoveryProfiles.Remove(profile); await db.SaveChangesAsync(); }
        return Result.Ok();
    });

    public async Task<ProfileComparison?> CompareAsync(Caller caller, string code)
    {
        if (caller.UserId is not Guid id) return null;
        var normalized = ShortCode.Normalize(code);
        if (!ShortCode.IsValid(normalized, 24)) return null;
        await using var db = dbf.Create();
        var target = await db.DiscoveryProfiles.AsNoTracking().SingleOrDefaultAsync(p => p.ComparisonCode == normalized && p.Enabled && p.AllowComparison);
        if (target is null || target.UserId == id) return null;
        var mine = await GetMineAsync(caller);
        if (mine?.Settings is not { Enabled: true, AllowComparison: true } || mine.Analysis is null) return null;
        var theirs = await GetMineAsync(new Caller(target.UserId, Guid.Empty, "comparison-read", false));
        if (theirs?.Settings is not { Enabled: true, AllowComparison: true } || theirs.Analysis is null || theirs.Settings.ComparisonCode != normalized) return null;
        // Compare only explicitly selected non-sensitive preferences. Guesses and inferred traits are excluded.
        var other = theirs.Analysis.Interests.Where(i => i.Source == "اظهار مستقیم" && !i.Key.StartsWith("private.")).ToDictionary(i => i.Key);
        var items = mine.Analysis.Interests.Where(i => i.Source == "اظهار مستقیم" && !i.Key.StartsWith("private.") && other.ContainsKey(i.Key))
            .Select(i => new ComparisonItem(i.Key, i.Category, i.Value, other[i.Key].Value,
                string.Equals(i.Value, other[i.Key].Value, StringComparison.OrdinalIgnoreCase))).ToArray();
        var facts = items.GroupBy(i => i.Category).Where(g => g.Count() >= 2).Select(g =>
        {
            var same = g.Count(i => i.Same);
            var narrative = same == g.Count() ? $"توی همین چند انتخابِ {g.Key}، هم‌سلیقه‌این؛ انگار این بخش رو با هم هماهنگ کردین! ✨" :
                same == 0 ? $"این چند انتخابِ {g.Key}، دو حال‌وهوای متفاوت دارن؛ برای معرفی چیزهای تازه به هم جا هست!" :
                $"توی این چند انتخابِ {g.Key}، هم نقطهٔ مشترک دارین هم تفاوت؛ ترکیب خودتون رو دارین!";
            return new FriendFact("similarity:" + g.Key, "انتخاب‌های مشترک در " + g.Key,
                narrative, g.Key, g.Count(),
                (int)Math.Round(same * 100.0 / g.Count(), MidpointRounding.AwayFromZero), []);
        }).ToArray();
        return new(theirs.OwnerName, items.Length, items.Count(i => i.Same), items, facts);
    }
}
