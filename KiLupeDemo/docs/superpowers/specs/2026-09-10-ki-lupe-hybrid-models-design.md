# Ki-Lupe Hybrid Text and Model Design

## Goal

Make the local text feature useful out of the box for OCR and word-level spelling errors, then evaluate Hugging Face models for sentence-level spelling, grammar, capitalization, and punctuation correction plus an RT-DETR object detector.

## Decisions

- Keep Tesseract as the local OCR engine because it returns word bounding boxes that the existing WPF overlays already understand.
- Load German and English Hunspell dictionaries independently. OCR must remain usable when one or both dictionaries are missing; a word-level spelling check uses every dictionary that is available.
- Copy local language data from project folders to the build output when present. Model weights are downloaded locally for evaluation and are not copied into the application or checked into source control.
- Test RT-DETR through a separate `IAnalysisService` implementation so its different Transformer output contract does not get mixed into the YOLO parser.
- Evaluate sentence correction outside the UI first. A text-to-text model returns a corrected sentence, not word rectangles; integration therefore needs line grouping and a conservative changed-line marker rather than pretending to provide exact token locations.

## Candidate models

- `onnx-community/rtdetr_v2_r18vd-ONNX`: Apache-2.0, COCO object detection, ready-made ONNX export, 640x640 input, 300 query predictions.
- `PekingU/rtdetr_v2_r18vd`: Transformers/Safetensors reference model for comparison if the ONNX export is insufficient.
- `facebook/detr-resnet-50` and `hustvl/yolos-tiny`: alternative object detectors for a later benchmark; both require a separate export and parser.
- `oliverguhr/spelling-correction-german-base`: Apache-2.0 German T5 spelling and punctuation correction, but large and generative.
- `Vinctilus/onnx-oliverguhr-spelling-correction-german-base`: Apache-2.0 ONNX export of the German spelling model; tokenizer and autoregressive decoding still need an adapter.
- `aiassociates/t5-small-grammar-correction-german`: German sentence correction candidate, but its CC-BY-NC-SA license is unsuitable for unrestricted commercial use.

## User-visible behavior

- `Text pruefen` reports the exact missing OCR/dictionary paths instead of silently producing no hits.
- OCR results continue to show text boxes without spelling data.
- A German or English spelling error is marked when at least one installed dictionary does not recognize it; missing dictionaries do not disable the other language.
- Model evaluation prints latency, tensor shapes, candidate detections, corrected text, and model status. It does not silently replace the stable baseline.

## Validation

- Unit tests cover independent dictionary decisions and missing-data status.
- The existing build and all unit tests remain green.
- A local smoke test runs the downloaded RT-DETR ONNX file against a fixture image and prints detections.
- A local text-model smoke test runs a known German error sentence and records the generated correction without asserting an exact generative string.
