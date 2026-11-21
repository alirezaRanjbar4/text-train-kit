using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.ML;
using TextTrainKit;

namespace Tests;

public sealed class SavedModelFixture : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "text-train-kit-model-" + Guid.NewGuid().ToString("N"));
    public string ModelPath => Path.Combine(root, "model.zip");

    public SavedModelFixture()
    {
        Directory.CreateDirectory(root);
        var train = Path.Combine(root, "train.jsonl");
        var validation = Path.Combine(root, "validation.jsonl");
        File.WriteAllLines(train, new[]
        {
            ("server error crash", "technical"), ("crash error broken", "technical"),
            ("broken server failure", "technical"), ("error failure crash", "technical"),
            ("billing payment invoice", "billing"), ("invoice payment charge", "billing"),
            ("refund payment billing", "billing"), ("payment invoice refund", "billing")
        }.Select(row => JsonSerializer.Serialize(new { text = row.Item1, label = row.Item2 })));
        File.WriteAllLines(validation, new[]
        {
            "{\"text\":\"invoice charge payment refund\",\"label\":\"billing\"}",
            "{\"text\":\"server broken crash failure\",\"label\":\"technical\"}"
        });
        Training.Train(train, validation, ModelPath, Path.Combine(root, "metrics.json"));
        // Evaluation must work with just the ZIP, without manifests or the original datasets.
        foreach (var path in Directory.GetFiles(root).Where(path => path != ModelPath)) File.Delete(path);
    }

    public void Dispose() => Directory.Delete(root, true);
}

public sealed class EvaluationTests(SavedModelFixture fixture) : IClassFixture<SavedModelFixture>, IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "text-train-kit-evaluation-" + Guid.NewGuid().ToString("N"));
    private string PathOf(string name) { Directory.CreateDirectory(root); return Path.Combine(root, name); }
    private string Data(string content)
    {
        var path = PathOf("holdout.jsonl");
        File.WriteAllText(path, content);
        return path;
    }
    private int Evaluate(string path, TextWriter output, TextWriter error) =>
        Application.Run(["evaluate", "--model", fixture.ModelPath, "--data", path], output, error);

    [Fact]
    public void EvaluatesReloadedPipelineWithHashesAndPreservesSourceFiles()
    {
        var data = Data("{\"text\":\"invoice refund charge\",\"label\":\" billing \"}\n" +
            "{\"text\":\"broken error server\",\"label\":\"technical\"}");
        var modelBytes = File.ReadAllBytes(fixture.ModelPath);
        var inputBytes = File.ReadAllBytes(data);
        var output = new StringWriter();
        var error = new StringWriter();
        Assert.Equal(0, Evaluate(data, output, error));
        Assert.Empty(error.ToString());
        using var json = JsonDocument.Parse(output.ToString());
        var report = json.RootElement;
        Assert.Equal(2, report.GetProperty("holdoutExamples").GetInt32());
        Assert.Equal(Convert.ToHexString(SHA256.HashData(modelBytes)).ToLowerInvariant(), report.GetProperty("modelSha256").GetString());
        Assert.Equal(Convert.ToHexString(SHA256.HashData(inputBytes)).ToLowerInvariant(), report.GetProperty("holdoutSha256").GetString());
        var classification = report.GetProperty("classification");
        Assert.Equal(new[] { "billing", "technical" }, classification.GetProperty("labels").EnumerateArray().Select(label => label.GetString()));
        var expected = ClassificationMetrics.Calculate(["billing", "technical"],
            [Training.Predict(fixture.ModelPath, "invoice refund charge").Label, Training.Predict(fixture.ModelPath, "broken error server").Label],
            ["billing", "technical"]);
        var matrix = classification.GetProperty("confusionMatrix").EnumerateArray().ToArray();
        for (var i = 0; i < matrix.Length; i++)
            Assert.Equal(expected.ConfusionMatrix[i], matrix[i].EnumerateArray().Select(cell => cell.GetInt32()));
        Assert.Equal(expected.ConfusionMatrix.Select((row, i) => row[i]).Sum() / 2.0, report.GetProperty("accuracy").GetDouble());
        Assert.DoesNotContain("invoice refund charge", output.ToString());
        Assert.Equal(modelBytes, File.ReadAllBytes(fixture.ModelPath));
        Assert.Equal(inputBytes, File.ReadAllBytes(data));
        Assert.Single(Directory.GetFiles(root));
    }

    [Fact]
    public void SingleClassHoldoutRetainsZeroSupportModelClasses()
    {
        var data = Data("{\"text\":\"invoice refund charge\",\"label\":\"billing\"}");
        var report = Training.Evaluate(fixture.ModelPath, data);
        Assert.Equal(1, report.HoldoutExamples);
        Assert.Equal(1, report.Classification.ValidationClasses["billing"]);
        Assert.Equal(0, report.Classification.ValidationClasses["technical"]);
        Assert.All(report.Classification.ConfusionMatrix[1], count => Assert.Equal(0, count));
        var absent = report.Classification.PerClass.Single(row => row.Label == "technical");
        Assert.Equal(0, absent.Support);
        Assert.Equal(0, absent.Recall);
        Assert.Equal(0, absent.F1);
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("Billing")]
    public void RejectsUnknownOrDifferentCaseLabelsWithoutEchoingText(string label)
    {
        var data = Data(JsonSerializer.Serialize(new { text = "private holdout message", label }));
        var output = new StringWriter();
        var error = new StringWriter();
        Assert.Equal(2, Evaluate(data, output, error));
        Assert.Empty(output.ToString());
        Assert.Contains("absent from the saved model", error.ToString());
        Assert.DoesNotContain("private holdout message", error.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("{")]
    [InlineData("{\"text\":\"private message\"}")]
    [InlineData("{\"text\":\"private message\",\"label\":null}")]
    [InlineData("{\"text\":\" \",\"label\":\"billing\"}")]
    [InlineData("{\"text\":42,\"label\":\"billing\"}")]
    [InlineData("{\"text\":\"private message\",\"label\":\"billing\",\"label\":\"technical\"}")]
    public void RejectsInvalidHoldoutBeforeScoring(string content)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        Assert.Equal(2, Evaluate(Data(content), output, error));
        Assert.Empty(output.ToString());
        Assert.NotEmpty(error.ToString());
        Assert.DoesNotContain("private message", error.ToString());
    }

    [Theory]
    [InlineData("SAME TEXT")]
    [InlineData(" same   text ")]
    [InlineData("ＳＡＭＥ ＴＥＸＴ")]
    public void RejectsNormalizedDuplicatesEvenWithConflictingLabels(string duplicate)
    {
        var data = Data(JsonSerializer.Serialize(new { text = "same text", label = "billing" }) + "\n" +
            JsonSerializer.Serialize(new { text = duplicate, label = "technical" }));
        var output = new StringWriter();
        var error = new StringWriter();
        Assert.Equal(2, Evaluate(data, output, error));
        Assert.Empty(output.ToString());
        Assert.Contains("duplicate normalized text", error.ToString());
    }

    [Fact]
    public void RejectsIncompatibleSavedPipelineWithSafeError()
    {
        var ml = new MLContext();
        var view = ml.Data.LoadFromEnumerable(new[] { new TrainingRow { Text = "example", Label = "billing" } });
        var model = ml.Transforms.Text.FeaturizeText("Features", "Text").Fit(view);
        var path = PathOf("incompatible.zip");
        ml.Model.Save(model, view.Schema, path);
        var output = new StringWriter();
        var error = new StringWriter();
        Assert.Equal(2, Application.Run(["evaluate", "--model", path, "--data", Data("{\"text\":\"private message\",\"label\":\"billing\"}")], output, error));
        Assert.Empty(output.ToString());
        Assert.Contains("class score metadata", error.ToString());
    }

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }
}
