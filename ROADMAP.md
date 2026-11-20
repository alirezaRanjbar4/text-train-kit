# Roadmap

## Available in v0.1.0

- Train, evaluate, save, and reload an ML.NET multiclass text model on CPU.
- Independent validation with overlap checks, a recorded seed, and dataset/model hashes.
- Class scores, synthetic examples, and CLI packaging.

## Next

- Add confusion matrices and per-class precision/recall/F1 to reports.
- Add group-aware data splitting and cross-validation without fitting transforms on validation data.
- Evaluate configurable trainers and multilingual feature settings on suitable public datasets.

This project currently trains traditional text classifiers, not neural language models or LLM adapters. Generalization claims require representative held-out data.
