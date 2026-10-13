namespace Kitsuneo.Core.Scheduling;

/// <summary>When the card would be due next for each rating.</summary>
public sealed record ReviewPreview(DateTime Again, DateTime Hard, DateTime Good, DateTime Easy);
