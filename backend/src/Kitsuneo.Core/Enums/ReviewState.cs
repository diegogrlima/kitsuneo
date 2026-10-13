namespace Kitsuneo.Core.Enums;

public enum ReviewState
{
    /// <summary>New or still going through the learning steps.</summary>
    Learning = 1,

    /// <summary>Graduated; scheduled in days.</summary>
    Review = 2,

    /// <summary>Forgotten during review and going through the relearning steps.</summary>
    Relearning = 3
}
