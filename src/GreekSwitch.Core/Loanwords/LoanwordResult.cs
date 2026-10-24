namespace GreekSwitch.Core.Loanwords;

/// <summary>Why a loanword is replaced without asking.</summary>
public enum LoanwordTier
{
    /// <summary>Tier 1: a pair the user entered in Settings, or accepted often enough to be promoted.</summary>
    UserPair = 1,

    /// <summary>Tier 2 (off by default): a strong match on a word that isn't Greek, to a domain-vocabulary term.</summary>
    StrongMatch = 2,
}

/// <summary>Outcome of <see cref="ILoanwordDetector.Check"/> for one completed Greek word.</summary>
public abstract record LoanwordResult
{
    /// <summary>Nothing to do: no close English word, or the word is ordinary Greek.</summary>
    public static readonly LoanwordResult None = new LoanwordNone();
}

/// <summary>Leave the word alone and show nothing.</summary>
public sealed record LoanwordNone : LoanwordResult;

/// <summary>Replace the word with <paramref name="Target"/> (already cased like the typed word).</summary>
public sealed record LoanwordReplace(string Target, LoanwordTier Tier) : LoanwordResult;

/// <summary>Tier 3: offer <paramref name="Target"/> (cased like the typed word); Tab accepts it.</summary>
public sealed record LoanwordSuggest(string Target, double Score) : LoanwordResult;
