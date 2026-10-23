using System.Runtime.CompilerServices;
using System.Text.Json;
using GreekSwitch.Evaluation.Support;
using Xunit.Abstractions;

namespace GreekSwitch.Evaluation;

/// <summary>
/// Prints the full evaluation table and fails if any false-positive rate ("wrongly changed")
/// is above its committed baseline in Data/baseline.json. After an intended improvement, run
/// with GREEKSWITCH_UPDATE_BASELINE=1 to rewrite the baseline, and commit it with the numbers.
/// </summary>
[Collection(EvaluationGroup.Name)]
public class EvaluationTests(EvaluationData data, ITestOutputHelper output)
{
    private const double Tolerance = 1e-9;

    [Fact]
    public void False_positive_rates_do_not_rise_above_the_baseline()
    {
        var tallies = Evaluator.Run(data);
        var table = Tally.Table(tallies);
        output.WriteLine(table);
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "evaluation-report.md"), table);

        var current = tallies
            .Where(t => t.Measure == "wrongly changed")
            .ToDictionary(t => t.Scenario, t => Math.Round(t.Rate, 6));

        if (Environment.GetEnvironmentVariable("GREEKSWITCH_UPDATE_BASELINE") == "1")
        {
            File.WriteAllText(SourceBaselinePath(), JsonSerializer.Serialize(current, new JsonSerializerOptions { WriteIndented = true }) + "\n");
            output.WriteLine("Baseline updated.");
            return;
        }

        var baseline = JsonSerializer.Deserialize<Dictionary<string, double>>(File.ReadAllText(EvaluationData.DataPath("baseline.json")))!;
        var regressions = current
            .Where(kv => !baseline.TryGetValue(kv.Key, out var allowed) || kv.Value > allowed + Tolerance)
            .Select(kv => $"{kv.Key}: {kv.Value:P4} (baseline {(baseline.TryGetValue(kv.Key, out var b) ? b.ToString("P4", System.Globalization.CultureInfo.InvariantCulture) : "missing")})")
            .ToList();

        regressions.Should().BeEmpty("false-positive rates must not rise above Data/baseline.json");
    }

    private static string SourceBaselinePath([CallerFilePath] string thisFile = "") =>
        Path.Combine(Path.GetDirectoryName(thisFile)!, "Data", "baseline.json");
}
