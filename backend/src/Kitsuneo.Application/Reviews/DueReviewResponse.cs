using Kitsuneo.Application.Phrases;
using Kitsuneo.Core.Entities;

namespace Kitsuneo.Application.Reviews;

public sealed record DueReviewResponse(PhraseResponse Phrase, ReviewScheduleResponse Schedule)
{
    public static DueReviewResponse FromEntity(Phrase phrase) => new(
        PhraseResponse.FromEntity(phrase),
        ReviewScheduleResponse.FromEntity(phrase.ReviewSchedule));
}
