using Tooling;
namespace TextTrainKit;

public static class Application
{
    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        if (args is ["--help"] or ["-h"])
        {
            output.WriteLine("text-train-kit train --train train.jsonl --validation validation.jsonl --model model.zip --report metrics.json [--seed 42]\ntext-train-kit predict --model model.zip --text \"ticket text\"\nC# / ML.NET text classification. Input JSONL: {text,label}. Exit: 0 success, 2 invalid input or operation failed.");
            return 0;
        }
        if (args is ["--version"]) { output.WriteLine("0.1.0"); return 0; }
        try
        {
            object report;
            if (args.Length > 0 && args[0] == "train")
            {
                var options = new Options(args.Skip(1), "--train", "--validation", "--model", "--report", "--seed");
                report = Training.Train(options.Require("--train"), options.Require("--validation"), options.Require("--model"), options.Require("--report"), options.Int("--seed", 42));
            }
            else if (args.Length > 0 && args[0] == "predict")
            {
                var options = new Options(args.Skip(1), "--model", "--text");
                report = Training.Predict(options.Require("--model"), options.Require("--text"));
            }
            else throw new InputException("Use train or predict; see --help.");
            output.WriteLine(JsonSupport.Serialize(report));
            return 0;
        }
        catch (InputException exception) { error.WriteLine(exception.Message); return 2; }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        { error.WriteLine("Operation failed. Check paths, model compatibility, and dataset schema. Raw text is omitted."); return 2; }
    }
}
