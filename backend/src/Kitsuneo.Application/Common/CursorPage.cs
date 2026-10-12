namespace Kitsuneo.Application.Common;

/// <summary>A page of results; <see cref="NextCursor"/> is null on the last page.</summary>
public sealed record CursorPage<T>(IReadOnlyList<T> Items, string? NextCursor);
