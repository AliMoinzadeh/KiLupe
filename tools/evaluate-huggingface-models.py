from __future__ import annotations

import argparse
import json
import time
from pathlib import Path

import numpy as np
import onnxruntime as ort
from PIL import Image


DEFAULT_TEXT_MODELS = (
    "oliverguhr/spelling-correction-german-base",
    "aiassociates/t5-small-grammar-correction-german",
)


def sigmoid(values: np.ndarray) -> np.ndarray:
    return 1.0 / (1.0 + np.exp(-values))


def run_rtdetr(model_directory: Path, image_path: Path, threshold: float) -> None:
    model_path = model_directory / "onnx" / "model.onnx"
    config_path = model_directory / "config.json"
    if not model_path.is_file():
        raise FileNotFoundError(f"RT-DETR ONNX model is missing: {model_path}")
    if not config_path.is_file():
        raise FileNotFoundError(f"RT-DETR config is missing: {config_path}")
    if not image_path.is_file():
        raise FileNotFoundError(f"Fixture image is missing: {image_path}")

    config = json.loads(config_path.read_text(encoding="utf-8"))
    labels = {int(key): value for key, value in config.get("id2label", {}).items()}
    session = ort.InferenceSession(str(model_path), providers=["CPUExecutionProvider"])
    image = Image.open(image_path).convert("RGB")
    source_width, source_height = image.size
    resized = image.resize((640, 640))
    pixels = np.asarray(resized, dtype=np.float32) / 255.0
    input_tensor = np.transpose(pixels, (2, 0, 1))[None, ...]

    started = time.perf_counter()
    output_values = session.run(None, {session.get_inputs()[0].name: input_tensor})
    elapsed_ms = (time.perf_counter() - started) * 1000
    output_names = [output.name for output in session.get_outputs()]
    outputs = dict(zip(output_names, output_values))
    logits = outputs["logits"][0]
    boxes = outputs["pred_boxes"][0]
    scores = sigmoid(logits)
    best_classes = np.argmax(scores, axis=1)
    best_scores = scores[np.arange(scores.shape[0]), best_classes]

    print(f"RT-DETR model: {model_path}")
    print(f"Input image: {source_width}x{source_height}")
    print(f"Input shape: {input_tensor.shape}; outputs: {output_names}")
    print(f"Output shapes: logits={outputs['logits'].shape}, pred_boxes={outputs['pred_boxes'].shape}")
    print(f"Inference: {elapsed_ms:.1f} ms")
    print("Detections:")
    for query in np.argsort(best_scores)[::-1]:
        score = float(best_scores[query])
        if score < threshold:
            break
        center_x, center_y, width, height = boxes[query]
        left = max(0.0, (center_x - width / 2) * source_width)
        top = max(0.0, (center_y - height / 2) * source_height)
        right = min(float(source_width), (center_x + width / 2) * source_width)
        bottom = min(float(source_height), (center_y + height / 2) * source_height)
        label = labels.get(int(best_classes[query]), f"class-{best_classes[query]}")
        print(f"  {label}: {score:.3f} [{left:.0f}, {top:.0f}, {right:.0f}, {bottom:.0f}]")


def run_text_model(model_id: str, sentence: str, max_length: int) -> None:
    from transformers import AutoModelForSeq2SeqLM, AutoTokenizer

    print(f"\nText model: {model_id}")
    started = time.perf_counter()
    tokenizer = AutoTokenizer.from_pretrained(model_id)
    model = AutoModelForSeq2SeqLM.from_pretrained(model_id)
    prompt = sentence
    if model_id == "oliverguhr/spelling-correction-german-base":
        prompt = f"correct: {sentence}"
    elif model_id == "aiassociates/t5-small-grammar-correction-german":
        prompt = f"grammar: {sentence}"
    inputs = tokenizer(prompt, return_tensors="pt")
    outputs = model.generate(
        **inputs,
        max_length=max_length,
        num_beams=4,
        early_stopping=True,
        no_repeat_ngram_size=3,
    )
    corrected = tokenizer.decode(outputs[0], skip_special_tokens=True).strip()
    elapsed_ms = (time.perf_counter() - started) * 1000
    if not corrected:
        raise RuntimeError(f"Model returned empty text: {model_id}")
    print(f"Latency including model load: {elapsed_ms:.1f} ms")
    print(f"Input:  {sentence}")
    print(f"Output: {corrected}")


def tensor_dtype(input_info: ort.NodeArg) -> np.dtype:
    return np.int32 if "int32" in input_info.type else np.int64


def run_local_onnx_text_model(
    model_directory: Path,
    sentence: str,
    max_length: int,
) -> None:
    from transformers import T5Tokenizer

    model_path = model_directory / "model.onnx"
    if not model_path.is_file():
        raise FileNotFoundError(f"Local ONNX correction model is missing: {model_path}")

    tokenizer = T5Tokenizer.from_pretrained(
        str(model_directory),
        local_files_only=True,
    )
    model = ort.InferenceSession(str(model_path), providers=["CPUExecutionProvider"])
    encoded = tokenizer(
        f"correct: {sentence}",
        return_tensors="np",
        truncation=True,
        max_length=256,
    )
    input_ids = encoded["input_ids"]
    attention_mask = encoded["attention_mask"]

    started = time.perf_counter()
    generated = [tokenizer.pad_token_id]
    for _ in range(max_length):
        decoder_ids = np.asarray([generated])
        decoder_attention_mask = np.ones_like(decoder_ids)
        model_inputs = {}
        for input_info in model.get_inputs():
            input_name = input_info.name.lower()
            if "decoder_input_ids" in input_name:
                model_inputs[input_info.name] = decoder_ids.astype(tensor_dtype(input_info))
            elif "decoder_attention_mask" in input_name:
                model_inputs[input_info.name] = decoder_attention_mask.astype(
                    tensor_dtype(input_info)
                )
            elif "input_ids" in input_name:
                model_inputs[input_info.name] = input_ids.astype(tensor_dtype(input_info))
            elif "attention_mask" in input_name:
                model_inputs[input_info.name] = attention_mask.astype(tensor_dtype(input_info))
            else:
                raise RuntimeError(f"Unsupported model input: {input_info.name}")

        model_outputs = model.run(None, model_inputs)
        logits = next(
            output
            for output in model_outputs
            if np.issubdtype(output.dtype, np.floating)
            and output.ndim == 3
            and output.shape[-1] >= 10000
        )
        next_token = int(np.argmax(logits[0, -1]))
        if next_token in {tokenizer.eos_token_id, tokenizer.pad_token_id}:
            break
        generated.append(next_token)

    corrected = tokenizer.decode(generated[1:], skip_special_tokens=True).strip()
    elapsed_ms = (time.perf_counter() - started) * 1000
    if not corrected:
        raise RuntimeError("Local ONNX correction model returned empty text")
    print(f"\nLocal ONNX text model: {model_directory}")
    print(f"Latency excluding model load: {elapsed_ms:.1f} ms")
    print(f"Input:  {sentence}")
    print(f"Output: {corrected}")


def main() -> int:
    parser = argparse.ArgumentParser(description="Evaluate local RT-DETR and German text-correction models.")
    parser.add_argument(
        "--model-directory",
        type=Path,
        default=Path("artifacts/models/rtdetr_v2_r18vd-ONNX"),
    )
    parser.add_argument(
        "--correction-directory",
        type=Path,
        default=Path("artifacts/models/german-spelling-correction-onnx"),
    )
    parser.add_argument("--image", type=Path, default=Path("artifacts/fixtures/bus.jpg"))
    parser.add_argument("--threshold", type=float, default=0.45)
    parser.add_argument(
        "--sentence",
        default="dies ist ein falsch geschriebener satz, der auch bei der großschreibung und zeichensetzung fehler hat",
    )
    parser.add_argument("--max-length", type=int, default=128)
    parser.add_argument("--skip-rtdetr", action="store_true")
    parser.add_argument("--skip-text", action="store_true")
    parser.add_argument("--skip-local-correction", action="store_true")
    parser.add_argument("--text-model", action="append", dest="text_models")
    arguments = parser.parse_args()

    if not arguments.skip_rtdetr:
        run_rtdetr(arguments.model_directory, arguments.image, arguments.threshold)
    if not arguments.skip_text:
        for model_id in arguments.text_models or DEFAULT_TEXT_MODELS:
            run_text_model(model_id, arguments.sentence, arguments.max_length)
    if not arguments.skip_local_correction:
        run_local_onnx_text_model(
            arguments.correction_directory,
            arguments.sentence,
            arguments.max_length,
        )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())