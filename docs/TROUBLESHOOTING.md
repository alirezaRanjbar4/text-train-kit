# Troubleshooting

These commands target the published **0.3.0** package and synthetic repository examples. Use fresh artifact paths and run from the repository root. See the [README](../README.md) for schemas/metrics and the [design guide](DESIGN.md) for fitting and saved-model data flow.

## Install the intended version

Download `TextTrainKit.Tool.0.3.0.nupkg` and `SHA256SUMS.txt` from the [release](https://github.com/alirezaRanjbar4/text-train-kit/releases/tag/v0.3.0) into an empty `downloaded-package` folder. Verify the package SHA-256, then choose a fresh isolated tool path:

```sh
dotnet tool install TextTrainKit.Tool --version 0.3.0 --tool-path ./artifacts/handoff-tool --add-source ./downloaded-package
```

Run `./artifacts/handoff-tool/text-train-kit --version` on Linux/macOS or `.\artifacts\handoff-tool\text-train-kit.exe --version` in PowerShell. For an existing isolated install, use `dotnet tool update` with the same package/version/source/tool-path options. Global installations use `--global` instead of `--tool-path ...`; check `dotnet tool list --global` and PATH if the command is not found.

The package includes ML.NET dependencies but requires .NET 10. Check `dotnet --info` for host/runtime problems; those can prevent the application from starting. A package-source/version error may mean the download folder is wrong: this package is distributed through GitHub Releases, not NuGet.org. Commands below use `text-train-kit` as shorthand for the intended executable.

## Run the complete local workflow

```sh
text-train-kit train --train examples/train.jsonl --validation examples/validation.jsonl --model artifacts/handoff-run/model.zip --report artifacts/handoff-run/training.json --seed 42
text-train-kit predict --model artifacts/handoff-run/model.zip --text "My invoice payment was charged twice"
text-train-kit evaluate --model artifacts/handoff-run/model.zip --data examples/holdout.jsonl
```

The examples have 30 training, 12 validation and 6 holdout rows with billing/delivery/technical labels. Training creates the model, report and adjacent manifest; predict/evaluate print JSON. Once the application starts, exit `0` means the operation succeeded and `2` means invalid input or an operation error. There is no quality-gate exit `1`: low accuracy does not by itself fail the command. The examples are workflow checks, not model-quality evidence.

## Invalid JSONL, duplicate text, or overlapping splits

Each physical line must be one JSON object with nonblank string `text` and `label` fields. Blank lines, malformed/multiline records, wrong field types and duplicate JSON properties are invalid. Labels are trimmed but case-sensitive; text duplicate checks use NFKC, invariant uppercase and collapsed whitespace. Text differing only by those normalizations can therefore be a duplicate even with different labels.

Inspect and resolve duplicate/conflicting labels before rerunning. Training also rejects normalized text present in both training and validation; prepare independent splits rather than reusing the same file. These exact checks do not establish freedom from paraphrase or customer/document leakage.

## Too few classes or an unknown label

`train` requires at least two classes in **each** training and validation file, and every validation label must occur in training. `evaluate` accepts one class, but every label must be known to the saved model. `Billing` and `billing` differ; trimming surrounding whitespace does not change case or merge different class meanings.

Check label spelling and dataset provenance. Learning a genuinely new class requires a new training dataset/model; evaluation does not extend the model's label mapping. Errors go to stderr without a partial JSON result or raw example text.

## Existing outputs and interrupted training

Choose fresh model/report destinations and leave `<model>.manifest.json` available as well. Existing files or directories at those paths are refused. Use separate paths for model, report and manifest, and put redirected stdout in another fresh file. The tool checks destinations before fitting but does not reserve them exclusively; avoid concurrent jobs targeting the same artifacts.

A write failure can leave a model or partial report behind. Inspect the run directory and use a fresh directory for the next attempt; the training command does not roll back all created files. Input datasets are not written by the tool.

## Model cannot load or class metadata is incompatible

Use a trusted ZIP created by this tool's `train` command with compatible .NET/ML.NET dependencies. Check its path and the tool version; unrelated ML.NET pipelines may lack the expected text/label columns or score slot names. An unreadable or incompatible model may produce the generic operation error with exit `2` instead of exposing exception text.

The ZIP contains the complete fitted pipeline. Losing the manifest or original datasets does not prevent `predict`/`evaluate`; retraining is not part of either command. Keep a new holdout file for evaluation, and leave both it and the ZIP unchanged during the run.

## Zero-support classes or unexpected accuracy

Match both matrix axes to `classification.labels`; rows are actual labels and columns predicted labels. A class absent from the evaluated file remains in the report with support `0`, recall `0` and F1 `0`. That is an absence of evidence about the class, not a measured failure rate. Obtain representative held-out examples before drawing conclusions.

`classification.validationClasses` contains holdout counts when the command is `evaluate`, despite the historical field name. `accuracy` is correct predictions divided by holdout examples. Prediction scores are sorted by score; their order need not match the matrix's ordinal label order.

Training metrics concern its validation file; saved-model evaluation concerns the supplied holdout. Different files need not give matching scores. Seeds and hashes help trace a run, but do not guarantee byte-identical models across platforms or prove that holdout examples were excluded from model selection. The tool does not compare a fresh holdout against the original training corpus.

## Larger data or a reproducible problem

Labeled datasets are loaded into memory and training requires additional feature/model working memory. Size data for the available machine; there is no GPU or streaming-training option in this scope. Labels remain visible in reports, and models are data-derived artifacts. Share a small synthetic case, tool version, command, platform and expected result in [issues](https://github.com/alirezaRanjbar4/text-train-kit/issues/new/choose), following [CONTRIBUTING](../CONTRIBUTING.md).
