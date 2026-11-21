using Tooling;

namespace TextTrainKit;

public sealed record PerClassMetrics(string Label, int Support, int Predicted, double Precision, double Recall, double F1);
public sealed record ClassificationReport(IReadOnlyList<string> Labels, IReadOnlyList<IReadOnlyList<int>> ConfusionMatrix,
    IReadOnlyList<PerClassMetrics> PerClass, IReadOnlyDictionary<string, int> ValidationClasses);

public static class ClassificationMetrics
{
    // Rows are actual labels; columns are predicted labels, both in Labels order.
    public static ClassificationReport Calculate(IReadOnlyList<string> actual, IReadOnlyList<string> predicted, IReadOnlyList<string> labels)
    {
        if (actual.Count == 0 || actual.Count != predicted.Count || labels.Count == 0 || labels.Any(string.IsNullOrWhiteSpace) ||
            labels.Distinct(StringComparer.Ordinal).Count() != labels.Count)
            throw new InputException("Metric inputs require equal nonempty example lists and unique nonblank labels.");
        var index = labels.Select((label, i) => (label, i)).ToDictionary(pair => pair.label, pair => pair.i, StringComparer.Ordinal);
        var counts = labels.Select(_ => new int[labels.Count]).ToArray();
        for (var i = 0; i < actual.Count; i++)
        {
            if (!index.TryGetValue(actual[i], out var row) || !index.TryGetValue(predicted[i], out var column))
                throw new InputException("Metric inputs contain an unknown label.");
            counts[row][column]++;
        }
        var metrics = labels.Select((label, i) =>
        {
            var support = counts[i].Sum();
            var predictedCount = counts.Sum(row => row[i]);
            var precision = predictedCount == 0 ? 0 : (double)counts[i][i] / predictedCount;
            var recall = support == 0 ? 0 : (double)counts[i][i] / support;
            var f1 = precision + recall == 0 ? 0 : 2 * precision * recall / (precision + recall);
            return new PerClassMetrics(label, support, predictedCount, precision, recall, f1);
        }).ToArray();
        return new(labels.ToArray(), counts.Select(row => (IReadOnlyList<int>)row).ToArray(), metrics,
            metrics.ToDictionary(row => row.Label, row => row.Support, StringComparer.Ordinal));
    }
}
