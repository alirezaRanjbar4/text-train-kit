# Roadmap

## Available in v0.1.0

- Train, evaluate, save, and reload an ML.NET multiclass text model on CPU.
- Independent validation with overlap checks, a recorded seed, and dataset/model hashes.
- Class scores, synthetic examples, and CLI packaging.

## Delivered in v0.2.0

Added labeled confusion matrices, per-class precision/recall/F1 and validation support counts to training reports and manifests.

## Delivered in v0.3.0

Evaluate a saved model on fresh holdout data without fitting transforms or retraining, with file hashes, explicit class order, per-class metrics, single-class support and unknown-label checks.

## First-scope completion

The [design guide](docs/DESIGN.md) and [troubleshooting guide](docs/TROUBLESHOOTING.md) target v0.3.1 and are bundled with the tool's linked root documents, license and [dependency notices](THIRD-PARTY-NOTICES.md). The bounded scope is dataset checks, training with independent validation, saved-pipeline prediction, class metrics and fresh-holdout evaluation, plus these user/developer guides.

Version 0.3.1 is the closing release for this scope. Completion evidence is recorded in issue #4 after final-commit package installation/examples, supported-platform CI and published tag/checksums pass. Keep the repository public and maintained for reproducible bugs and feedback after that handoff. The future candidates below do not expand or reopen the required first scope.

## Future candidates

- Add group-aware data splitting and cross-validation without fitting transforms on validation data.
- Evaluate configurable trainers and multilingual feature settings on suitable public datasets.

This project currently trains traditional text classifiers, not neural language models or LLM adapters. Generalization claims require representative held-out data.
