using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.ML;
using Microsoft.ML.Data;
using Tooling;

namespace TextTrainKit;

public sealed record TextExample(string? Text, string? Label);
public sealed class TrainingRow
{
    public string Text { get; set; } = "";
    public string Label { get; set; } = "";
}
public sealed class ModelPrediction
{
    [ColumnName("PredictedLabel")]
    public string Label { get; set; } = "";
    public float[] Score { get; set; } = [];
}
public sealed record ClassScore(string Label, float Score);
public sealed record Prediction(string Label, IReadOnlyList<ClassScore> Scores);
public sealed record TrainingReport(string Trainer, string MlNetVersion, int Seed, int TrainingExamples, int ValidationExamples,
    IReadOnlyDictionary<string, int> TrainingClasses, double MicroAccuracy, double MacroAccuracy, double? LogLoss,
    string TrainingSha256, string ValidationSha256, string ModelSha256, ClassificationReport Classification);

public static partial class Training
{
    public static TrainingReport Train(string trainPath, string validationPath, string modelPath, string reportPath, int seed = 42)
    {
        foreach (var path in new[] { modelPath, reportPath, modelPath + ".manifest.json" })
            if (File.Exists(path) || Directory.Exists(path)) throw new InputException("Output already exists. Choose fresh model and report paths.");
        if (Path.GetFullPath(modelPath).Equals(Path.GetFullPath(reportPath), StringComparison.OrdinalIgnoreCase))
            throw new InputException("Model and report paths must differ.");

        var train = Load(trainPath);
        var validation = Load(validationPath);
        var labels = train.Select(r => r.Label).ToHashSet(StringComparer.Ordinal);
        if (labels.Count < 2 || validation.Select(r => r.Label).Distinct(StringComparer.Ordinal).Count() < 2)
            throw new InputException("Training and validation must each contain at least two classes.");
        if (validation.Any(row => !labels.Contains(row.Label)))
            throw new InputException("Validation contains a class absent from training.");
        var seen = train.Select(r => Normalize(r.Text)).ToHashSet(StringComparer.Ordinal);
        if (validation.Any(row => seen.Contains(Normalize(row.Text))))
            throw new InputException("Normalized text overlaps training and validation. Use an independent validation set.");

        var ml = new MLContext(seed);
        var trainView = ml.Data.LoadFromEnumerable(train);
        var pipeline = ml.Transforms.Conversion.MapValueToKey("Label")
            .Append(ml.Transforms.Text.FeaturizeText("Features", nameof(TrainingRow.Text)))
            .Append(ml.MulticlassClassification.Trainers.SdcaMaximumEntropy(labelColumnName: "Label", featureColumnName: "Features", maximumNumberOfIterations: 50))
            .Append(ml.Transforms.Conversion.MapKeyToValue("PredictedLabel"));
        var model = pipeline.Fit(trainView);
        var scored = model.Transform(ml.Data.LoadFromEnumerable(validation));
        var metrics = ml.MulticlassClassification.Evaluate(scored);
        var predictions = ml.Data.CreateEnumerable<ModelPrediction>(scored, reuseRowObject: false).Select(row => row.Label).ToArray();
        var classification = ClassificationMetrics.Calculate(validation.Select(row => row.Label).ToArray(), predictions,
            labels.OrderBy(label => label, StringComparer.Ordinal).ToArray());
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(modelPath))!);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        ml.Model.Save(model, trainView.Schema, modelPath);
        var report = new TrainingReport("SdcaMaximumEntropy", "6.0.0", seed, train.Count, validation.Count,
            train.GroupBy(r => r.Label).OrderBy(g => g.Key, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Count()),
            metrics.MicroAccuracy, metrics.MacroAccuracy, double.IsFinite(metrics.LogLoss) ? metrics.LogLoss : null,
            Hash(trainPath), Hash(validationPath), Hash(modelPath), classification);
        var json = JsonSupport.Serialize(report);
        File.WriteAllText(reportPath, json);
        File.WriteAllText(modelPath + ".manifest.json", json);
        return report;
    }

    public static Prediction Predict(string modelPath, string text)
    {
        if (string.IsNullOrWhiteSpace(text)) throw new InputException("Prediction text must not be blank.");
        var ml = new MLContext();
        var model = ml.Model.Load(modelPath, out _);
        var view = model.Transform(ml.Data.LoadFromEnumerable(new[] { new TrainingRow { Text = text } }));
        var prediction = ml.Data.CreateEnumerable<ModelPrediction>(view, reuseRowObject: false).Single();
        VBuffer<ReadOnlyMemory<char>> names = default;
        view.Schema["Score"].Annotations.GetValue("SlotNames", ref names);
        var labels = names.DenseValues().Select(n => n.ToString()).ToArray();
        if (labels.Length != prediction.Score.Length) throw new InputException("Model score metadata is incompatible.");
        return new(prediction.Label, labels.Select((label, i) => new ClassScore(label, prediction.Score[i]))
            .OrderByDescending(score => score.Score).ToArray());
    }

    private static List<TrainingRow> Load(string path)
    {
        var examples = JsonSupport.Read<TextExample>(path);
        if (examples.Any(row => string.IsNullOrWhiteSpace(row.Text) || string.IsNullOrWhiteSpace(row.Label)))
            throw new InputException("Every example must contain nonblank text and label strings.");
        var rows = examples.Select(row => new TrainingRow { Text = row.Text!, Label = row.Label!.Trim() }).ToList();
        var normalized = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var row in rows)
            if (!normalized.TryAdd(Normalize(row.Text), row.Label))
                throw new InputException("Dataset contains duplicate normalized text. Deduplicate before training.");
        return rows;
    }

    private static string Normalize(string value) => Whitespace().Replace(value.Normalize(NormalizationForm.FormKC).Trim().ToUpperInvariant(), " ");
    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
