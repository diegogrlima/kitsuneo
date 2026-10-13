namespace Kitsuneo.Application.Practice;

/// <param name="Items">Due reviews first, then new phrases.</param>
public sealed record TodayPracticeResponse(IReadOnlyList<PracticeItemResponse> Items, int ReviewCount, int NewCount);
