"""Validate the approved narration and install it without rewriting level settings."""

import hashlib
import json
from pathlib import Path
import re
import shutil

import numpy as np
import soundfile as sf

ROOT = Path(__file__).resolve().parents[2]


def without_hints(value):
    if isinstance(value, dict):
        return {key: without_hints(item) for key, item in value.items() if key != "hint_audio_path"}
    if isinstance(value, list):
        return [without_hints(item) for item in value]
    return value


def main():
    generated = ROOT / "output/task_audio_generated"
    revised = ROOT / "output/task_audio_day12_revision"
    entries = json.loads((generated / "manifest.json").read_text(encoding="utf-8"))
    replacement = json.loads((revised / "manifest.json").read_text(encoding="utf-8"))
    day12 = json.loads((ROOT / "data/days/day_12.json").read_text(encoding="utf-8-sig"))
    assert set(replacement) == {task["id"] for task in day12["tasks"]}
    entries.update(replacement)
    scripts = {}
    for line in (ROOT / "docs/操作提示音频文案.md").read_text(encoding="utf-8").splitlines():
        columns = [value.strip() for value in line.strip("|").split("|")]
        if len(columns) == 4 and columns[1].startswith("`") and columns[2].endswith((".mp3`", ".wav`")):
            scripts[columns[1].strip("`")] = columns[3]
        elif len(columns) == 4 and columns[2].endswith(("_hint.mp3`", "_hint.wav`")):
            scripts[columns[2].strip("`").removesuffix("_hint.mp3").removesuffix("_hint.wav")] = columns[3]
        elif len(columns) == 3 and columns[1] in ("`final_chest_hint.mp3`", "`final_chest_hint.wav`"):
            scripts["final_chest"] = columns[2]
    assert set(entries) == set(scripts) and len(entries) == 137

    paths = {}
    verified = {}
    # Verify every source before changing any configuration or replacing an asset.
    for key, item in entries.items():
        assert item["status"] == "complete" and item["text"] == scripts[key], key
        assert item["voice"] == "生动教学" and item["speed"] == 1.0, key
        signature = hashlib.sha256(json.dumps(
            [item["text"], item["voice_sha256"], item["speed"]], ensure_ascii=False).encode()).hexdigest()
        assert item["signature"] == signature, key
        source = Path(item["file"])
        info = sf.info(source)
        samples, rate = sf.read(source)
        assert info.frames > 0 and info.duration > 0 and rate == info.samplerate, key
        assert info.subtype == "PCM_16" and np.isfinite(samples).all() and np.max(np.abs(samples)) > 0, key
        resource = f"res://assets/audio/voice/task_hints/{source.parent.name}/{source.name}"
        paths[key] = resource
        verified[key] = dict(source=str(source), resource=resource, text=item["text"],
                             duration_seconds=info.duration, sha256=hashlib.sha256(source.read_bytes()).hexdigest())

    decoder = json.JSONDecoder()
    configs = []
    references = []
    for config_path in sorted((ROOT / "data/days").glob("day_*.json")):
        raw = config_path.read_text(encoding="utf-8-sig")
        original = json.loads(raw)
        objects = []
        for match in re.finditer(r"\{", raw):
            try:
                value, length = decoder.raw_decode(raw[match.start():])
            except json.JSONDecodeError:
                continue
            if isinstance(value, dict):
                objects.append((match.start(), match.start() + length, value))
        edits = []

        def set_path(start, end, key):
            fragment = raw[start:end]
            field = re.search(r'"hint_audio_path"\s*:\s*"[^"\\]*"', fragment)
            if field:
                edits.append((start + field.start(), start + field.end(),
                              '"hint_audio_path": ' + json.dumps(paths[key])))
            else:
                field = re.search(r'(?m)^(\s*)"text"\s*:', fragment)
                assert field, key
                insertion = field.group(1) + '"hint_audio_path": ' + json.dumps(paths[key]) + ',\n'
                edits.append((start + field.start(), start + field.start(), insertion))
            references.append(key)

        for start, end, task in objects:
            if "id" not in task or task["id"] not in paths:
                continue
            set_path(start, end, task["id"])
            children = [(a, b, obj) for a, b, obj in objects
                        if a > start and b < end and "text" in obj and "id" not in obj]
            assert len(children) == len(task.get("steps", [])), task["id"]
            for index, (a, b, _) in enumerate(children, 1):
                set_path(a, b, f"{task['id']}_step_{index:02d}")
        for start, end, value in sorted(edits, reverse=True):
            raw = raw[:start] + value + raw[end:]
        assert without_hints(json.loads(raw)) == without_hints(original), config_path
        configs.append((config_path, raw))
    assert len(references) == 136 and set(references) == set(entries) - {"final_chest"}
    assert paths["final_chest"] in (ROOT / "scripts/days/Day11Treasure.cs").read_text(encoding="utf-8")

    for key, item in verified.items():
        destination = ROOT / item["resource"].removeprefix("res://")
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(item["source"], destination)
        assert hashlib.sha256(destination.read_bytes()).hexdigest() == item["sha256"]
    for path, raw in configs:
        path.write_text(raw, encoding="utf-8")
    report = dict(total=137, voice="生动教学", speed=1.0, audio=verified)
    (generated / "integration_report.json").write_text(
        json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print("Installed and verified 137 hints, including the revised 8 Day12 clips.")


if __name__ == "__main__":
    main()
