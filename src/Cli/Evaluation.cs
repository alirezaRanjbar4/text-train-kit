using Microsoft.ML;
using Microsoft.ML.Data;
using Tooling;

namespace TextTrainKit;

public sealed record EvaluationReport(string MlNetVersion, int HoldoutExamples, double Accuracy,
    string HoldoutSha256, string ModelSha256, ClassificationReport Classification);

public static partial class Training
{
    public static EvaluationReport Evaluate(string modelPath, string holdoutPath)
    {
        var holdout = Load(holdoutPath);
        var ml = new MLContext();
        var model = ml.Model.Load(modelPath, out _);
        var scored = model.Transform(ml.Data.LoadFromEnumerable(holdout));
        if (!scored.Schema.Any(column => column.Name == "Score") ||
            !scored.Schema["Score"].Annotations.Schema.Any(column => column.Name == "SlotNames"))
            throw new InputException("Model lacks class score metadata. Use a model saved by text-train-kit train.");

        VBuffer<ReadOnlyMemory<char>> names = default;
        scored.Schema["Score"].Annotations.GetValue("SlotNames", ref names);
        var labels = names.DenseValues().Select(name => name.ToString()).OrderBy(label => label, StringComparer.Ordinal).ToArray();
        if (labels.Length < 2 || labels.Any(string.IsNullOrWhiteSpace) || labels.Distinct(StringComparer.Ordinal).Count() != labels.Length)
            throw new InputException("Model class metadata is incompatible. Use a model saved by text-train-kit train.");
        var knownLabels = labels.ToHashSet(StringComparer.Ordinal);
        if (holdout.Any(row => !knownLabels.Contains(row.Label)))
            throw new InputException("Holdout contains a class absent from the saved model. Labels are case-sensitive.");

        var predictions = ml.Data.CreateEnumerable<ModelPrediction>(scored, reuseRowObject: false).Select(row => row.Label).ToArray();
        var classification = ClassificationMetrics.Calculate(holdout.Select(row => row.Label).ToArray(), predictions, labels);
        var correct = classification.ConfusionMatrix.Select((row, i) => row[i]).Sum();
        return new("6.0.0", holdout.Count, (double)correct / holdout.Count, Hash(holdoutPath), Hash(modelPath), classification);
    }
}
