# Changelog

## Unreleased

Added linked design and troubleshooting guides for training-only fitting, save/reload/evaluate, label/metric ordering, dataset errors, output writes and trust/provenance limits. Closing-release acceptance remains pending under issue #4.

## 0.3.0 — 2026-11-21

Added `evaluate --model --data` for fresh labeled holdout data without retraining. Reports include accuracy, labeled confusion matrices, per-class metrics and model/holdout SHA-256 hashes. Evaluation works with the saved ZIP alone, accepts single-class holdouts with explicit zero-support classes, and rejects unknown labels and normalized duplicates. Added English/Persian synthetic holdout examples and saved-model evaluation tests.

## 0.2.0 — 2026-11-21

Added labeled confusion matrices, per-class precision/recall/F1 and validation support counts to training reports and manifests.

## 0.1.0 — 2026-11-20

Initial usable release with a C# CLI, synthetic examples, automated tests, and .NET tool packaging. See the README for the supported scope and limitations.
