using System.Globalization;
using System.Text;

namespace GreekSwitch.Evaluation.Support;

/// <summary>A frequency-weighted count of outcomes, keeping the heaviest offenders.</summary>
public sealed class Tally
{
    private readonly Dictionary<string, double> _offenders = [];

    public Tally(string scenario, string measure)
    {
        Scenario = scenario;
        Measure = measure;
    }

    public string Scenario { get; }

    /// <summary>What a "hit" means here, e.g. "wrongly changed" or "corrected".</summary>
    public string Measure { get; }

    public int Items { get; private set; }

    public int Hits { get; private set; }

    public double TotalWeight { get; private set; }

    public double HitWeight { get; private set; }

    public double Rate => TotalWeight == 0 ? 0 : HitWeight / TotalWeight;

    /// <summary>Records one item; <paramref name="offender"/> is listed when the outcome is the notable one.</summary>
    public void Add(double weight, bool hit, string? offender = null)
    {
        Items++;
        TotalWeight += weight;
        if (hit)
        {
            Hits++;
            HitWeight += weight;
        }

        if (offender is not null)
        {
            _offenders[offender] = _offenders.GetValueOrDefault(offender) + weight;
        }
    }

    public IEnumerable<string> TopOffenders(int count) =>
        _offenders.OrderByDescending(kv => kv.Value).Take(count).Select(kv => kv.Key);

    public static string Table(IEnumerable<Tally> tallies, int offenders = 10)
    {
        var sb = new StringBuilder()
            .AppendLine("| Scenario | Measure | Items | Weighted rate | Unweighted | Top offenders |")
            .AppendLine("|---|---|---:|---:|---:|---|");
        foreach (var t in tallies)
        {
            sb.Append(CultureInfo.InvariantCulture, $"| {t.Scenario} | {t.Measure} | {t.Items} | {t.Rate:P2} | ")
              .Append(CultureInfo.InvariantCulture, $"{(t.Items == 0 ? 0 : (double)t.Hits / t.Items):P2} | ")
              .Append(string.Join(", ", t.TopOffenders(offenders)))
              .AppendLine(" |");
        }

        return sb.ToString();
    }
}
