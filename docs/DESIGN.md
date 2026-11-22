# Design and first-scope handoff

Text Train Kit trains a traditional multiclass text classifier on CPU with ML.NET 6.0.0. The bounded first scope is local JSONL validation, training with independent validation, saved-pipeline prediction, labeled class metrics and fresh-holdout evaluation. It does not fine-tune LLMs or provide GPU training.

## Code boundaries and input rules

`Application.Run` parses command-specific options, dispatches `train`/`predict`/`evaluate`, serializes JSON to stdout, and handles supported input/operation errors on stderr. The `Training` partial class implements fitting/prediction in `Training.cs` and saved-model evaluation in `Evaluation.cs`. `ClassificationMetrics` computes labeled confusion matrices and class metrics independently of ML.NET's aggregate evaluator. `JsonSupport` rejects invalid JSON and duplicate property names before deserializing each line.

Labeled datasets contain nonblank `text` and `label` strings. Labels are trimmed and remain case-sensitive. The loader retains original text and checks duplicate identity using NFKC, invariant uppercase, and trimmed/collapsed whitespace. This duplicate policy is separate from the learned ML.NET text features; it does not rewrite the stored training examples.

Each dataset must be nonempty and contain no normalized duplicate text, regardless of label. `train` additionally requires at least two classes in both training and validation, validation labels present in training, and no normalized text overlap across the two files. These checks run before fitting. They do not identify paraphrases, common customers/documents or other group leakage.

## Training-only fit and artifact flow

`train` loads the labeled inputs, then builds this pipeline:

1. `MapValueToKey("Label")` learns the label mapping.
2. `FeaturizeText("Features", "Text")` learns text transformations.
3. `SdcaMaximumEntropy` fits a multiclass classifier with a maximum of 50 iterations.
4. `MapKeyToValue("PredictedLabel")` maps predictions back to label strings.

The pipeline's single `Fit` call uses training data only. The already-fitted pipeline transforms validation rows; ML.NET supplies micro/macro accuracy and log loss (null when nonfinite), and the shared class calculator scores the same predictions. Validation data are not used to fit label/text transforms.

The full fitted pipeline is saved as a ZIP. The training report is written to `--report` and, with the same JSON, `<model>.manifest.json`; the report is also printed to stdout. It includes counts, seed, ML.NET version, input/model SHA-256 hashes and class metrics. Existing artifact paths are checked before training; model/report paths must differ. Writes are sequential, without exclusive path reservation or a multi-file transaction. An I/O failure can leave partial artifacts.

## Reloaded prediction and evaluation

`predict` loads the ZIP and transforms one text row. It uses `Score` slot-name metadata to pair scores with labels, then returns the predicted label and scores sorted descending. Score order is not the confusion-matrix axis order, and scores alone are not proof of calibrated confidence.

`evaluate` loads the ZIP and a fresh labeled file. It makes no `Fit` call and needs neither original datasets nor the manifest. Known labels come from the fitted pipeline's score metadata; unknown holdout labels are rejected. A single-class holdout is allowed. It transforms the rows, returns aggregate accuracy and shared class metrics, and hashes the holdout/model without saving or modifying them. It creates no report file itself; stdout redirection is the caller's choice.

## Class order and metric contract

Both axes of `classification.confusionMatrix` use the explicit ordinal-sorted `labels` array: rows are actual classes, columns predicted classes. Every trained/model label remains present even with zero support. `perClass` contains support, predicted count, precision (TP/predicted), recall (TP/support) and their harmonic-mean F1; a zero denominator gives `0`.

For compatibility, `classification.validationClasses` stores counts from the dataset being assessed: validation for `train`, holdout for `evaluate`. Evaluation's `accuracy` is the matrix diagonal sum divided by holdout rows. Training's aggregate fields come from ML.NET; evaluate does not add macro accuracy or log loss. Reports omit input text and individual evaluation predictions, but class names remain visible.

## Trust, reproducibility and completion boundary

Keep input/model files stable during a command: loading and hashing are separate reads. Hashes identify bytes read at hashing time; they do not prove an independent holdout, absence of leakage, anonymization, or model quality. Selecting a model using validation data still calls for a separate final holdout. Seeds aid reproducibility; model bytes and numeric results can vary across runtimes/platforms.

Load trusted ZIPs produced by this tool with compatible .NET/ML.NET dependencies. Fitted models can contain learned vocabulary and other data-derived information; omitting raw text from reports does not make every artifact safe to publish. The synthetic English/Persian examples demonstrate the workflow, not a production benchmark or a multilingual-quality guarantee.

CI tests, verifies formatting and packages the tool on Windows, Linux and macOS. See [CONTRIBUTING](../CONTRIBUTING.md), [troubleshooting](TROUBLESHOOTING.md) and the [README](../README.md). The guides complete the documentation portion of issue #4; a validated closing release is still required. Group-aware splitting, cross-validation and configurable trainers remain optional future work in the [roadmap](../ROADMAP.md).
