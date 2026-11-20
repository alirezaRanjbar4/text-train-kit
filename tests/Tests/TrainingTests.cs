using System.Text.Json;
using TextTrainKit;
using Tooling;
namespace Tests;

public sealed class TrainingTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "text-train-kit-tests-" + Guid.NewGuid().ToString("N"));
    private string PathOf(string name) { Directory.CreateDirectory(root); return Path.Combine(root, name); }
    private string Data(string name, params (string Text, string Label)[] rows)
    {
        var path = PathOf(name);
        File.WriteAllLines(path, rows.Select(row => JsonSerializer.Serialize(new { text = row.Text, label = row.Label })));
        return path;
    }
    [Fact]
    public void FitsSavesReloadsAndPredictsWithoutCloudServices()
    {
        var train = Data("train.jsonl", ("billing payment invoice", "billing"), ("invoice payment charge", "billing"), ("refund payment billing", "billing"), ("payment invoice refund", "billing"),
            ("server error crash", "technical"), ("crash error broken", "technical"), ("broken server failure", "technical"), ("error failure crash", "technical"));
        var val = Data("val.jsonl", ("invoice charge payment refund", "billing"), ("server broken crash failure", "technical"));
        var model = PathOf("model.zip");
        var report = Training.Train(train, val, model, PathOf("metrics.json"), 42);
        Assert.Equal(8, report.TrainingExamples);
        Assert.Equal(2, report.ValidationExamples);
        Assert.InRange(report.MicroAccuracy, 0, 1);
        Assert.Equal(64, report.ModelSha256.Length);
        Assert.True(File.Exists(model + ".manifest.json"));
        var prediction = Training.Predict(model, "invoice payment refund");
        Assert.Equal("billing", prediction.Label);
        Assert.Equal(2, prediction.Scores.Count);
        Assert.All(prediction.Scores, score => Assert.True(float.IsFinite(score.Score)));
    }
    [Fact]
    public void RejectsTrainValidationTextLeakage()
    {
        var train = Data("train", ("One example", "a"), ("two", "b"));
        var val = Data("val", (" ONE   EXAMPLE ", "a"), ("three", "b"));
        Assert.Contains("overlaps", Assert.Throws<InputException>(() => Training.Train(train, val, PathOf("m"), PathOf("r"))).Message);
    }
    [Fact]
    public void RejectsUnknownValidationLabels()
    {
        var train = Data("train", ("one", "a"), ("two", "b"));
        var val = Data("val", ("three", "a"), ("four", "c"));
        Assert.Contains("absent", Assert.Throws<InputException>(() => Training.Train(train, val, PathOf("m"), PathOf("r"))).Message);
    }
    [Fact]
    public void RejectsDuplicateTextsBeforeTraining()
    {
        var train = Data("train", ("same", "a"), ("SAME", "b"));
        var val = Data("val", ("three", "a"), ("four", "b"));
        Assert.Contains("duplicate", Assert.Throws<InputException>(() => Training.Train(train, val, PathOf("m"), PathOf("r"))).Message);
    }
    [Fact]
    public void RefusesToOverwriteExistingArtifacts()
    {
        var existing = PathOf("model.zip");
        File.WriteAllText(existing, "preserve");
        Assert.Throws<InputException>(() => Training.Train("unused", "unused", existing, PathOf("report")));
        Assert.Equal("preserve", File.ReadAllText(existing));
    }
    [Theory]
    [InlineData("{\"text\":\"\",\"label\":\"a\"}")]
    [InlineData("{\"text\":\"sample\"}")]
    [InlineData("{\"text\":\"sample\",\"label\":null}")]
    [InlineData("{")]
    public void InvalidDatasetsReturnFailureWithoutEchoingData(string row)
    {
        var path = PathOf("input"); File.WriteAllText(path, row);
        var output = new StringWriter();
        Assert.Equal(2, Application.Run(["train", "--train", path, "--validation", path, "--model", PathOf("m"), "--report", PathOf("r")], output, new StringWriter()));
        Assert.Empty(output.ToString());
    }
    [Fact]
    public void InvalidCommandReturnsUsageError() => Assert.Equal(2, Application.Run(["train"], new StringWriter(), new StringWriter()));
    public void Dispose()
    {
        if (Directory.Exists(root) && Path.GetFileName(root).StartsWith("text-train-kit-tests-", StringComparison.Ordinal)) Directory.Delete(root, true);
    }
}
