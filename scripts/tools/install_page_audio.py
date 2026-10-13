"""Validate the six approved page narrations before installing any clip."""

import hashlib
import json
from pathlib import Path
import re
import shutil

import numpy as np
import soundfile as sf

ROOT = Path(__file__).resolve().parents[2]
SCENES = {
    "home": "scenes/main/Main.tscn",
    "choose_day": "scenes/ui/choseday.tscn",
    "choose_friend": "scenes/ui/chosefriend.tscn",
    "music_settings": "scenes/ui/gameset.tscn",
    "exit_prompt": "scenes/ui/gameexit.tscn",
    "day_complete": "scenes/ui/result.tscn",
}


def configured_scene(path, resource, completion):
    text = path.read_text(encoding="utf-8")
    if '[node name="PageVoice"' in text:
        assert resource in text and 'res://scripts/ui/page_voice.gd' in text
        assert 'stream = ExtResource("page_voice_audio")' in text
        return text
    assert 'id="page_voice_audio"' not in text and 'id="page_voice_script"' not in text
    first_node_or_subresource = re.search(r"(?m)^\[(?:sub_resource|node) ", text)
    assert first_node_or_subresource is not None
    resources = ('[ext_resource type="Script" path="res://scripts/ui/page_voice.gd" id="page_voice_script"]\n'
                 f'[ext_resource type="AudioStream" path="{resource}" id="page_voice_audio"]\n\n')
    text = text[:first_node_or_subresource.start()] + resources + text[first_node_or_subresource.start():]
    text = text.rstrip() + ('\n\n[node name="PageVoice" type="AudioStreamPlayer" parent="."]\n'
                            'script = ExtResource("page_voice_script")\n'
                            'stream = ExtResource("page_voice_audio")\n')
    if completion:
        text += 'preceding_sound_path = NodePath("../CongratulationSound")\n'
    return text


def main():
    output = ROOT / "output/page_audio_generated"
    plan = json.loads((ROOT / "docs/页面配音计划.json").read_text(encoding="utf-8"))
    manifest = json.loads((output / "manifest.json").read_text(encoding="utf-8"))
    assert len(plan) == 6 and set(manifest) == set(SCENES)
    checked = []
    for item in plan:
        entry = manifest[item["id"]]
        assert entry["status"] == "complete" and entry["text"] == item["text"], item["id"]
        assert entry["voice"] == "生动教学" and entry["speed"] == 1.0
        signature = hashlib.sha256(json.dumps(
            [entry["text"], entry["voice_sha256"], entry["speed"]], ensure_ascii=False).encode()).hexdigest()
        assert entry["signature"] == signature
        source = Path(entry["file"])
        assert source.parent.resolve() == output.resolve() and source.name == item["filename"]
        info = sf.info(source)
        samples, rate = sf.read(source)
        assert info.frames > 0 and info.duration > 0 and info.subtype == "PCM_16"
        assert rate == info.samplerate and np.isfinite(samples).all() and np.max(np.abs(samples)) > 0
        checked.append(dict(id=item["id"], text=item["text"], filename=item["filename"],
                            scene="res://" + SCENES[item["id"]], duration_seconds=info.duration,
                            sha256=hashlib.sha256(source.read_bytes()).hexdigest(),
                            resource="res://assets/audio/voice/pages/" + item["filename"]))
    # Read the current saved scenes only after generation, preserving all other nodes and properties.
    scenes = {ROOT / SCENES[item["id"]]: configured_scene(
        ROOT / SCENES[item["id"]], item["resource"], item["id"] == "day_complete") for item in checked}
    destination = ROOT / "assets/audio/voice/pages"
    destination.mkdir(parents=True, exist_ok=True)
    for item in checked:
        shutil.copy2(output / item["filename"], destination / item["filename"])
        assert hashlib.sha256((destination / item["filename"]).read_bytes()).hexdigest() == item["sha256"]
    for path, text in scenes.items():
        temporary = path.with_suffix(".page_audio.tmp")
        temporary.write_text(text, encoding="utf-8")
        temporary.replace(path)
    report = dict(total=6, voice="生动教学", speed=1.0, audio=checked,
                  runtime_playtest="pending user test")
    (output / "integration_report.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print("Installed and wired all 6 verified page voice clips.")


if __name__ == "__main__":
    main()
