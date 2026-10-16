using Kitsuneo.Application.Abstractions;
using Kitsuneo.Core.Dictation;
using Kitsuneo.Core.Entities;
using Kitsuneo.Core.Enums;
using Kitsuneo.Core.Exceptions;
using Kitsuneo.Core.Scheduling;

namespace Kitsuneo.Application.Practice;

public sealed class PracticeService(
    IPhraseRepository phraseRepository,
    IReviewRepository reviewRepository,
    IPracticeAttemptRepository attemptRepository,
    FsrsScheduler scheduler,
    TimeProvider timeProvider,
    ICurrentUser currentUser)
{
    /// <summary>How many never-practiced phrases are introduced per UTC day.</summary>
    public const int NewPhrasesPerDay = 10;

    /// <summary>
    /// Due reviews, then new phrases. New phrases are those never reviewed, capped at
    /// <see cref="NewPhrasesPerDay"/> minus the phrases first attempted today (UTC).
    /// </summary>
    public async Task<TodayPracticeResponse> GetTodayAsync(
        Language? language = null,
        CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;

        var reviews = await reviewRepository.ListDueReviewsAsync(currentUser.Id, now, language, cancellationToken);

        var startedToday = await attemptRepository.CountPhrasesFirstAttemptedSinceAsync(
            currentUser.Id, now.Date, cancellationToken);
        var newLimit = Math.Max(NewPhrasesPerDay - startedToday, 0);

        var newPhrases = newLimit > 0
            ? await reviewRepository.ListNewAsync(currentUser.Id, newLimit, language, cancellationToken)
            : [];

        var items = reviews.Select(p => new PracticeItemResponse(p.Id, p.Language, IsNew: false))
            .Concat(newPhrases.Select(p => new PracticeItemResponse(p.Id, p.Language, IsNew: true)))
            .ToList();

        return new TodayPracticeResponse(items, reviews.Count, newPhrases.Count);
    }

    /// <summary>Returns null when the phrase does not exist, is archived or belongs to another user.</summary>
    public async Task<AttemptResultResponse?> SubmitAttemptAsync(
        Guid phraseId,
        string? answer,
        int playCount,
        bool usedSlowSpeed,
        int durationMs,
        CancellationToken cancellationToken = default)
    {
        var phrase = await phraseRepository.GetByIdAsync(phraseId, cancellationToken);
        if (phrase is null || phrase.UserId != currentUser.Id)
            return null;

        // Japanese is dictated in kana, so it is checked against the reading when there is one
        var expected = phrase.Language == Language.Japanese && phrase.Reading is not null
            ? phrase.Reading
            : phrase.Text;
        var result = DictationComparer.Compare(phrase.Language, expected, answer ?? string.Empty);

        var attempt = PracticeAttempt.Create(
            phrase.Id, currentUser.Id, answer, result.Accuracy, playCount, usedSlowSpeed, durationMs);

        await attemptRepository.AddAsync(attempt, cancellationToken);
        await attemptRepository.SaveChangesAsync(cancellationToken);

        var preview = scheduler.Preview(phrase.ReviewSchedule.ToCard(), timeProvider.GetUtcNow().UtcDateTime);

        return new AttemptResultResponse(
            attempt.Id,
            result.Tokens,
            result.Accuracy,
            phrase.Text,
            phrase.SpokenForm,
            phrase.Reading,
            phrase.Translation,
            preview);
    }

    /// <summary>
    /// Returns null when the attempt does not exist, belongs to another user or its phrase is archived.
    /// Throws <see cref="DomainConflictException"/> when the attempt was already rated.
    /// </summary>
    public async Task<RateAttemptResponse?> RateAttemptAsync(
        Guid attemptId,
        ReviewRating rating,
        CancellationToken cancellationToken = default)
    {
        // JSON accepts any number for an enum, so an unknown rating can get this far
        if (!Enum.IsDefined(rating))
            throw new DomainException("Invalid rating.");

        var attempt = await attemptRepository.GetByIdAsync(attemptId, currentUser.Id, cancellationToken);
        if (attempt is null)
            return null;

        var phrase = await reviewRepository.GetForReviewAsync(attempt.PhraseId, currentUser.Id, cancellationToken);
        if (phrase is null)
            return null;

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var schedule = phrase.ReviewSchedule;
        var dueAtBefore = schedule.DueAt;

        schedule.Review(scheduler, rating, now);
        attempt.Rate(rating, dueAtBefore, schedule.DueAt, now);

        // Attempt and schedule share the request's DbContext, so this saves both together
        await attemptRepository.SaveChangesAsync(cancellationToken);

        return new RateAttemptResponse(schedule.DueAt, schedule.State);
    }
}
