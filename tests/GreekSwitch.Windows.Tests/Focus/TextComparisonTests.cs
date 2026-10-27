using GreekSwitch.Windows.Focus;
using GreekSwitch.Windows.Output;

namespace GreekSwitch.Windows.Tests.Focus;

/// <summary>
/// Word's AutoCorrect capitalises the first word of a sentence when Space is pressed, before
/// GreekSwitch sees the delimiter. That one change must not block the correction; any other must.
/// </summary>
public class TextComparisonTests
{
    [Theory]
    [InlineData("ψονταινερ ", "ψονταινερ ")]
    [InlineData("Some text ψονταινερ ", "ψονταινερ ")]
    [InlineData("Ψονταινερ ", "Ψονταινερ ")] // the user typed the capital: nothing changed
    public void Same_text_matches(string screen, string expected) =>
        UiaTextVerifier.Classify(screen, expected).Should().Be(TextMatch.Match);

    /// <summary>
    /// Chrome's contenteditable (Gmail, Outlook and Teams on the web) stores a typed trailing space
    /// as a no-break space, which UI Automation reports as U+00A0. One Backspace deletes either.
    /// </summary>
    [Theory]
    [InlineData("ψονταινερ ", "ψονταινερ ")]
    [InlineData("ελεγχουμε παλι αμα ", "ελεγχουμε παλι αμα ")]
    public void A_no_break_space_counts_as_the_typed_space(string screen, string expected) =>
        UiaTextVerifier.Classify(screen, expected).Should().Be(TextMatch.Match);

    [Fact]
    public void A_no_break_space_and_a_capital_together_are_accepted() =>
        UiaTextVerifier.Classify("Ψονταινερ ", "ψονταινερ ").Should().Be(TextMatch.FirstLetterCapitalised);

    [Fact]
    public void A_no_break_space_where_a_letter_was_typed_is_a_mismatch() =>
        UiaTextVerifier.Classify("ψονταινε  ", "ψονταινερ ").Should().Be(TextMatch.Mismatch);

    [Theory]
    [InlineData("Ψονταινερ ", "ψονταινερ ")]
    [InlineData("Previous sentence. Ψονταινερ ", "ψονταινερ ")]
    [InlineData("Uelv ", "uelv ")]
    [InlineData("(Ψονταινερ ", "(ψονταινερ ")] // the first letter, not the first character
    [InlineData("Ελεγχουμε παλι αμα ", "ελεγχουμε παλι αμα ")] // an accent run starting a sentence
    public void Only_the_first_letter_capitalised_is_accepted(string screen, string expected) =>
        UiaTextVerifier.Classify(screen, expected).Should().Be(TextMatch.FirstLetterCapitalised);

    [Theory]
    [InlineData("ψονταινερ. ", "ψονταινερ ")] // something else added
    [InlineData("ψΟνταινερ ", "ψονταινερ ")] // a letter other than the first
    [InlineData("ΨΟνταινερ ", "ψονταινερ ")] // the first and another one
    [InlineData("ελεγχουμε Παλι ", "ελεγχουμε παλι ")] // a later word capitalised
    [InlineData("ψονταινερ ", "Ψονταινερ ")] // lowered, not raised
    [InlineData("Ηλεκτρονικό ", "ηλεκτρονικο ")] // capitalised and accented: not just the capital
    [InlineData("tv ", "ψονταινερ ")]
    public void Any_other_difference_is_a_mismatch(string screen, string expected) =>
        UiaTextVerifier.Classify(screen, expected).Should().Be(TextMatch.Mismatch);

    /// <summary>Accents fixed in place: each letter on screen may be as typed or already as wanted.</summary>
    [Theory]
    [InlineData("ενα κανει ", "ενα κανει ", "ένα κάνει ", "ενα κανει ", "ένα κάνει ")]  // nothing done yet
    [InlineData("x ενα κανει ", "ενα κανει ", "ένα κάνει ", "ενα κανει ", "ένα κάνει ")] // more text before
    [InlineData("ένα κανει ", "ενα κανει ", "ένα κάνει ", "ένα κανει ", "ένα κάνει ")]  // the app accented ένα
    [InlineData("Ενα ", "ενα ", "ένα ", "Ενα ", "Ένα ")]                               // the app capitalised it
    [InlineData("Ένα ", "ενα ", "ένα ", "Ένα ", "Ένα ")]                               // and accented it
    [InlineData("ενα ", "ενα ", "ένα ", "ενα ", "ένα ")]                          // Chrome's no-break space
    public void Reconcile_takes_what_the_app_already_did(string screen, string text, string replacement, string newText, string newReplacement) =>
        UiaTextVerifier.Reconcile(screen, text, replacement).Should().Be((newText, newReplacement));

    [Theory]
    [InlineData("ενο ")] // another letter
    [InlineData("ενά ")] // an accent we wouldn't add
    [InlineData("να ")]  // too short
    [InlineData("ενΑ ")] // a capital that isn't the first letter
    public void Reconcile_refuses_any_other_difference(string screen) =>
        UiaTextVerifier.Reconcile(screen, "ενα ", "ένα ").Should().BeNull();
}
