namespace Kitsuneo.Core.Dictation;

public enum DictationTokenStatus
{
    /// <summary>Typed exactly as expected.</summary>
    Correct = 1,

    /// <summary>Expected but not typed.</summary>
    Missing = 2,

    /// <summary>Typed in place of the expected token, but different.</summary>
    Wrong = 3,

    /// <summary>Typed but not expected.</summary>
    Extra = 4
}
