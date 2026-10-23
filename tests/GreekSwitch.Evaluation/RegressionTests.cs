using GreekSwitch.Evaluation.Support;
using Xunit.Abstractions;

namespace GreekSwitch.Evaluation;

/// <summary>Runs Data/regression.tsv: hand-written mixed sentences with real capitalisation.</summary>
[Collection(EvaluationGroup.Name)]
public class RegressionTests(EvaluationData data, ITestOutputHelper output)
{
    public static TheoryData<string, string, bool, string> Cases()
    {
        var cases = new TheoryData<string, string, bool, string>();
        foreach (var line in EvaluationData.Lines("regression.tsv"))
        {
            var cols = line.Split('\t');
            cases.Add(cols[0], cols[1], cols.Length > 2 && cols[2].Contains("xfail", StringComparison.Ordinal), cols.Length > 3 ? cols[3] : "");
        }

        return cases;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Sentence_comes_out_as_expected(string typed, string expected, bool knownFailure, string note)
    {
        var sim = new TypingSimulator(data, followLayoutSwitches: false);
        var segments = (typed + " ").Split('|');
        for (var i = 0; i < segments.Length; i++)
        {
            if (i > 0)
            {
                sim.SwitchLayoutWithHotkey();
            }

            sim.Type(segments[i]);
        }

        var actual = sim.Screen.TrimEnd(' ');
        output.WriteLine($"typed:    {typed}\nexpected: {expected}\nactual:   {actual}\nnote:     {note}");

        if (knownFailure)
        {
            actual.Should().NotBe(expected, "this case is tagged xfail but now passes: remove the tag from Data/regression.tsv");
            return;
        }

        actual.Should().Be(expected);
    }
}
