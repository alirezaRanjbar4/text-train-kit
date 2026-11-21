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

- Add concise architecture and troubleshooting documentation.
- Validate the documented installation and workflows in a closing release.

## Future candidates

- Add group-aware data splitting and cross-validation without fitting transforms on validation data.
- Evaluate configurable trainers and multilingual feature settings on suitable public datasets.

This project currently trains traditional text classifiers, not neural language models or LLM adapters. Generalization claims require representative held-out data.
