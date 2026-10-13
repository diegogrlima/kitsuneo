using Kitsuneo.Application.Abstractions;
using Kitsuneo.Application.Users;
using Kitsuneo.Core.Enums;
using Kitsuneo.Core.Exceptions;
using Kitsuneo.Core.Scheduling;

namespace Kitsuneo.Application.Reviews;

public sealed class ReviewService(IReviewRepository repository, FsrsScheduler scheduler, TimeProvider timeProvider)
{
    public const int DefaultLimit = 20;
    public const int MaxLimit = 100;

    public async Task<IReadOnlyList<DueReviewResponse>> ListDueAsync(
        int limit = DefaultLimit,
        CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, MaxLimit);

        var phrases = await repository.ListDueAsync(
            DefaultUser.Id, timeProvider.GetUtcNow().UtcDateTime, limit, cancellationToken);

        return phrases.Select(DueReviewResponse.FromEntity).ToList();
    }

    /// <summary>Returns null when the phrase does not exist, is archived or belongs to another user.</summary>
    public async Task<ReviewScheduleResponse?> ReviewAsync(
        Guid phraseId,
        ReviewRequest request,
        CancellationToken cancellationToken = default)
    {
        // JSON accepts any number for an enum, so an unknown rating can get this far
        if (!Enum.IsDefined(request.Rating))
            throw new DomainException("Invalid rating.");

        var phrase = await repository.GetForReviewAsync(phraseId, DefaultUser.Id, cancellationToken);
        if (phrase is null)
            return null;

        phrase.ReviewSchedule.Review(scheduler, request.Rating, timeProvider.GetUtcNow().UtcDateTime);
        await repository.SaveChangesAsync(cancellationToken);

        return ReviewScheduleResponse.FromEntity(phrase.ReviewSchedule);
    }
}
