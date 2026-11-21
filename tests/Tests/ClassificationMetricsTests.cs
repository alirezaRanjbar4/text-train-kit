using TextTrainKit;
using Tooling;

namespace Tests;

public sealed class ClassificationMetricsTests
{
    [Fact]
    public void ComputesHandCalculatedMatrixAndMinorityClassMetrics()
    {
        var report = ClassificationMetrics.Calculate(["billing", "billing", "technical", "delivery"],
            ["billing", "technical", "technical", "technical"], ["billing", "delivery", "technical", "unused"]);
        Assert.Equal(new[] { 1, 0, 1, 0 }, report.ConfusionMatrix[0]);
        Assert.Equal(new[] { 0, 0, 1, 0 }, report.ConfusionMatrix[1]);
        Assert.Equal(new[] { 0, 0, 1, 0 }, report.ConfusionMatrix[2]);
        Assert.Equal(new[] { 0, 0, 0, 0 }, report.ConfusionMatrix[3]);
        Assert.Equal(4, report.ConfusionMatrix.Sum(row => row.Sum()));
        Assert.Equal(2, report.ValidationClasses["billing"]);
        var billing = report.PerClass.Single(row => row.Label == "billing");
        Assert.Equal(1, billing.Precision);
        Assert.Equal(0.5, billing.Recall);
        Assert.Equal(2.0 / 3, billing.F1, 10);
        var technical = report.PerClass.Single(row => row.Label == "technical");
        Assert.Equal(1.0 / 3, technical.Precision, 10);
        Assert.Equal(1, technical.Recall);
        Assert.Equal(0.5, technical.F1);
        Assert.All(report.PerClass.Where(row => row.Label is "delivery" or "unused"), row => Assert.Equal(0, row.F1));
    }

    [Fact]
    public void LabelOrderIsExplicitAndCaseSensitive()
    {
        var report = ClassificationMetrics.Calculate(["a", "A"], ["A", "A"], ["A", "a"]);
        Assert.Equal(new[] { "A", "a" }, report.Labels);
        Assert.Equal(new[] { 1, 0 }, report.ConfusionMatrix[0]);
        Assert.Equal(new[] { 1, 0 }, report.ConfusionMatrix[1]);
    }

    [Fact]
    public void RejectsInvalidOrUnknownLabels()
    {
        Assert.Throws<InputException>(() => ClassificationMetrics.Calculate([], [], ["a"]));
        Assert.Throws<InputException>(() => ClassificationMetrics.Calculate(["a"], [], ["a"]));
        Assert.Throws<InputException>(() => ClassificationMetrics.Calculate(["a"], ["b"], ["a"]));
        Assert.Throws<InputException>(() => ClassificationMetrics.Calculate(["a"], ["a"], ["a", "a"]));
    }
}
