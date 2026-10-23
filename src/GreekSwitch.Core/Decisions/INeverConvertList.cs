namespace GreekSwitch.Core.Decisions;

/// <summary>Words the user never wants converted, matched ignoring case (and accents for Greek).</summary>
public interface INeverConvertList
{
    bool Contains(string word);

    void Add(string word);
}
