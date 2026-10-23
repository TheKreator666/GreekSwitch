namespace GreekSwitch.Core.Decisions;

/// <summary>Words the user never wants converted, matched ignoring case (and accents for Greek).</summary>
public interface INeverConvertList
{
    bool Contains(string word);

    /// <summary>A word the user listed explicitly; kept as readable text so it can be edited.</summary>
    void Add(string word);

    /// <summary>
    /// A word learned from an undo. It may be anything the user typed, including a password in a
    /// box we couldn't recognise as one, so it is kept only as a keyed hash and never as text.
    /// </summary>
    void Learn(string word);
}
