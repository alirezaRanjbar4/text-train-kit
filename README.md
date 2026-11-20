# Text Train Kit

[![CI](https://github.com/alirezaRanjbar4/text-train-kit/actions/workflows/ci.yml/badge.svg)](https://github.com/alirezaRanjbar4/text-train-kit/actions/workflows/ci.yml)
[![MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

**Train and use a text classifier entirely in C#, with an independent validation set.**

A .NET 10 / ML.NET 6 CLI for small text classification tasks: support-ticket routing, message categories, or document labels. Train on CPU, save the fitted pipeline, inspect held-out metrics, and reload the model for predictions. No Python, API key, or cloud service is required.

## Quick start

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) and network access for the first package restore.

```sh
git clone https://github.com/alirezaRanjbar4/text-train-kit.git
cd text-train-kit
dotnet run --project src/Cli -- train --train examples/train.jsonl --validation examples/validation.jsonl --model artifacts/ticket-model.zip --report artifacts/metrics.json
dotnet run --project src/Cli -- predict --model artifacts/ticket-model.zip --text "My invoice payment was charged twice"
```

The synthetic example contains 30 training and 12 validation messages in English and Persian, with billing, delivery, and technical labels. The prediction command returns a label and a sorted array of class scores.

**These examples are a smoke test, not an accuracy benchmark or a production-ready ticket model.** Use representative labeled data and independent validation for your task.

## Dataset format

Each line is one JSON object:

```json
{"text":"Please refund the duplicate payment","label":"billing"}
```

Text and labels must be nonblank strings. Labels are trimmed and case-sensitive. Training and validation must each contain at least two distinct labels, and every validation label must occur in training.

The tool rejects normalized duplicate text within a split and text overlap across training/validation, regardless of label. This comparison uses Unicode NFKC, invariant case folding, trimmed/collapsed whitespace. It is an exact overlap check; it cannot detect paraphrases, shared customers, copied document fragments, or other group leakage.

## What training does

1. Read and validate the supplied datasets.
2. Fit the label mapping and text featurizer on **training data only**.
3. Train an ML.NET `SdcaMaximumEntropy` multiclass classifier.
4. Evaluate the already-fitted pipeline on the separate validation file.
5. Save the complete model pipeline as a ZIP plus a manifest.

The report includes micro/macro accuracy, log loss when finite, label counts, seed, ML.NET version, and SHA-256 hashes of both input files and the model. The model manifest is written next to the model as `<model>.manifest.json`. Reports contain labels and aggregate metrics, without training text.

The default seed is `42`; override with `--seed 123`. Seeds and hashes aid reproducibility; numerical results and model bytes can vary across runtimes/platforms. Existing output files are rejected: choose fresh paths for another run.

## Commands

```text
text-train-kit train --train train.jsonl --validation validation.jsonl --model model.zip --report metrics.json [--seed 42]
text-train-kit predict --model model.zip --text "new message"
```

Output is JSON. Exit codes: `0` success, `2` invalid arguments/data or an operation failure. No automatic quality threshold is applied during training. Validation metrics must be assessed for the intended task.

Class scores are the model's outputs; high scores do not establish real-world reliability or calibrated confidence. Only load model files from a trusted source.

## Install

Download `TextTrainKit.Tool.0.1.0.nupkg` from the [release](https://github.com/alirezaRanjbar4/text-train-kit/releases/tag/v0.1.0) into an empty folder:

```sh
dotnet tool install --global TextTrainKit.Tool --version 0.1.0 --add-source ./downloaded-package
text-train-kit --help
```

Or build from source:

```sh
dotnet pack src/Cli -c Release -o artifacts
dotnet tool install --global TextTrainKit.Tool --version 0.1.0 --add-source ./artifacts
```

The release package includes its ML.NET runtime dependencies. NuGet.org publishing is not currently enabled.

## Scope

v0.1.0 trains traditional text classifiers. It does not fine-tune LLMs, train neural networks, choose a trainer automatically, or provide GPU training. Unicode text is accepted, but language-specific quality needs appropriate data and evaluation.

ML.NET's [text classification tutorial](https://learn.microsoft.com/dotnet/machine-learning/tutorials/sentiment-analysis) describes the underlying fit/evaluate workflow.

## Development

```sh
dotnet test -c Release
dotnet format --verify-no-changes
```

The integration test actually fits, saves, reloads, and predicts with a small model. Report reproducible problems using synthetic data in [issues](https://github.com/alirezaRanjbar4/text-train-kit/issues/new/choose). See [CONTRIBUTING.md](CONTRIBUTING.md) and [ROADMAP.md](ROADMAP.md).

## فارسی

آموزش واقعی مدل دسته‌بندی متن با C# و ML.NET: داده برچسب‌دار را می‌خواند، مدل را روی CPU آموزش می‌دهد، روی اعتبارسنجی جداگانه ارزیابی می‌کند و مدل ذخیره‌شده را برای پیش‌بینی به کار می‌گیرد. برای نمونه می‌توان از آن برای دسته‌بندی تیکت پشتیبانی استفاده کرد. این نسخه مدل زبانی بزرگ را Fine-tune نمی‌کند.

[MIT License](LICENSE)
