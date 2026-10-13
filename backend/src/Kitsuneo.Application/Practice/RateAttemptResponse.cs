using Kitsuneo.Core.Enums;

namespace Kitsuneo.Application.Practice;

public sealed record RateAttemptResponse(DateTime DueAt, ReviewState State);
