"""Verify and install the approved 12 title and 12 story voice clips."""

import hashlib
import json
from pathlib import Path
import shutil

import numpy as np
import soundfile as sf

ROOT = Path(__file__).resolve().parents[2]


def main():
    output = ROOT / "output/story_audio_generated"
    plan = json.loads((ROOT / "docs/关卡入口配音计划.json").read_text(encoding="utf-8"))
    manifest = json.loads((output / "manifest.json").read_text(encoding="utf-8"))
    assert len(plan) == 24 and {item["id"] for item in plan} == set(manifest)
    script = (ROOT / "scripts/ui/label.gd").read_text(encoding="utf-8")
    assert '"res://assets/audio/voice/stories/story%d.wav"' in script
    assert '"res://assets/audio/voice/stories/day%d_story.wav"' in script
    checked = []
    for item in plan:
        entry = manifest[item["id"]]
        assert entry["status"] == "complete" and entry["text"] == item["text"], item["id"]
        assert entry["voice"] == "生动教学" and entry["speed"] == 1.0, item["id"]
        signature = hashlib.sha256(json.dumps(
            [entry["text"], entry["voice_sha256"], entry["speed"]], ensure_ascii=False).encode()).hexdigest()
        assert entry["signature"] == signature, item["id"]
        source = Path(entry["file"])
        assert source.name == item["filename"] and source.parent.resolve() == output.resolve()
        info = sf.info(source)
        samples, rate = sf.read(source)
        assert info.frames > 0 and info.duration > 0 and info.subtype == "PCM_16", item["id"]
        assert rate == info.samplerate and np.isfinite(samples).all() and np.max(np.abs(samples)) > 0, item["id"]
        checked.append(dict(id=item["id"], day=item["day"], text=item["text"],
                            filename=item["filename"], duration_seconds=info.duration,
                            sha256=hashlib.sha256(source.read_bytes()).hexdigest()))

    destination = ROOT / "assets/audio/voice/stories"
    backup = output / "placeholder_backup"
    backup.mkdir(parents=True, exist_ok=True)
    # Preserve every old clip before replacing any of the game's existing filenames.
    for item in checked:
        old_file = destination / item["filename"]
        saved = backup / item["filename"]
        if old_file.is_file() and not saved.exists():
            shutil.copy2(old_file, saved)
    for item in checked:
        target = destination / item["filename"]
        shutil.copy2(output / item["filename"], target)
        assert hashlib.sha256(target.read_bytes()).hexdigest() == item["sha256"]
        item["resource"] = "res://assets/audio/voice/stories/" + item["filename"]
    report = dict(total=24, voice="生动教学", speed=1.0, audio=checked,
                  placeholder_backup=str(backup), runtime_playtest="pending user test")
    (output / "integration_report.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print("Installed and verified 12 titles + 12 stories using the existing entrance playback paths.")


if __name__ == "__main__":
    main()
