"""Generate resumable task voice audio through the local IndexTTS service."""

import argparse
import hashlib
import json
from pathlib import Path
import shutil
import time

from gradio_client import Client, handle_file
import soundfile


ROOT = Path(__file__).resolve().parents[2]
DEFAULT_IDS = ("move_over_red_flowers", "apples_tap", "drag_fruits")


def save_manifest(path, manifest):
    temporary = path.with_suffix(".tmp")
    temporary.write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")
    temporary.replace(path)


def output_path(result):
    if isinstance(result, str) and Path(result).is_file():
        return Path(result)
    if isinstance(result, dict):
        for key in ("value", "path"):
            if key in result:
                found = output_path(result[key])
                if found:
                    return found
    if isinstance(result, (list, tuple)):
        for value in result:
            found = output_path(value)
            if found:
                return found
    return None


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--url", default="http://127.0.0.1:7862/")
    parser.add_argument("--voice", required=True, type=Path)
    parser.add_argument("--output", type=Path, default=ROOT / "output/task_audio_samples")
    parser.add_argument("--speed", type=float, default=1.0)
    parser.add_argument("--task-ids", nargs="+", default=list(DEFAULT_IDS))
    parser.add_argument("--all", action="store_true", help="Include all tasks, steps, and the final chest")
    parser.add_argument("--seed-dir", type=Path, help="Reuse approved samples with matching text and settings")
    parser.add_argument("--plan", type=Path, help="Generate a reviewed list of id, day, text and filename entries")
    args = parser.parse_args()
    if not args.voice.is_file():
        raise FileNotFoundError(args.voice)

    filenames = {}
    if args.plan:
        entries = json.loads(args.plan.read_text(encoding="utf-8-sig"))
        scripts = {entry["id"]: entry["text"] for entry in entries}
        days = {entry["id"]: int(entry["day"]) for entry in entries}
        filenames = {entry["id"]: entry["filename"] for entry in entries}
        if len(scripts) != len(entries) or len(set(filenames.values())) != len(entries):
            raise ValueError("Plan IDs and filenames must be unique")
        for filename in filenames.values():
            if Path(filename).name != filename or not filename.endswith(".wav"):
                raise ValueError(f"Expected a WAV filename without directories: {filename}")
        args.task_ids = list(scripts)
    else:
        scripts = {}
        for line in (ROOT / "docs/操作提示音频文案.md").read_text(encoding="utf-8").splitlines():
            columns = [value.strip() for value in line.strip("|").split("|")]
            if len(columns) == 4 and columns[1].startswith("`") and columns[2].endswith((".mp3`", ".wav`")):
                scripts[columns[1].strip("`")] = columns[3]
            elif len(columns) == 4 and columns[2].endswith(("_hint.mp3`", "_hint.wav`")):
                scripts[columns[2].strip("`").removesuffix("_hint.mp3").removesuffix("_hint.wav")] = columns[3]
            elif len(columns) == 3 and columns[1] in ("`final_chest_hint.mp3`", "`final_chest_hint.wav`"):
                scripts["final_chest"] = columns[2]
        days = {}
        for path in sorted((ROOT / "data/days").glob("day_*.json")):
            config = json.loads(path.read_text(encoding="utf-8-sig"))
            for task in config["tasks"]:
                days[task["id"]] = int(config["day_id"])
                for index, step in enumerate(task.get("steps", []), 1):
                    days[f"{task['id']}_step_{index:02d}"] = int(config["day_id"])
        days["final_chest"] = 11
        if args.all:
            if set(days) != set(scripts):
                raise ValueError(f"Task/script mismatch: {set(days) ^ set(scripts)}")
            args.task_ids = list(days)
    for task_id in args.task_ids:
        if task_id not in scripts or task_id not in days:
            raise ValueError(f"No current task and voice script found for {task_id}")

    args.output.mkdir(parents=True, exist_ok=True)
    manifest_path = args.output / "manifest.json"
    manifest = json.loads(manifest_path.read_text(encoding="utf-8")) if manifest_path.exists() else {}
    voice_hash = hashlib.sha256(args.voice.read_bytes()).hexdigest()
    reusable = dict(manifest)
    if args.seed_dir:
        seed_path = args.seed_dir / "manifest.json"
        reusable.update(json.loads(seed_path.read_text(encoding="utf-8")))
    save_manifest(args.output / "plan.json", {"voice": args.voice.stem, "speed": args.speed,
                                             "total": len(args.task_ids), "task_ids": args.task_ids,
                                             "source_plan": str(args.plan) if args.plan else None})
    client = Client(args.url, verbose=False)
    for index, task_id in enumerate(args.task_ids, 1):
        text = scripts[task_id]
        target = (args.output / filenames[task_id] if args.plan else
                  args.output / f"day{days[task_id]:02d}" / f"{task_id}_hint.wav")
        signature = hashlib.sha256(json.dumps([text, voice_hash, args.speed], ensure_ascii=False).encode()).hexdigest()
        previous = manifest.get(task_id, {})
        if previous.get("status") == "complete" and previous.get("signature") == signature and target.is_file():
            print(f"SKIP {task_id}: already saved", flush=True)
            continue
        if previous.get("status") in ("running", "failed"):
            raise RuntimeError(f"{task_id} has an unfinished or failed request. Check its saved error and the service before submitting again.")
        cached = next((value for value in reusable.values()
                       if value.get("status") == "complete" and value.get("signature") == signature
                       and Path(value["file"]).is_file()), None)
        if cached:
            target.parent.mkdir(parents=True, exist_ok=True)
            if Path(cached["file"]).resolve() != target.resolve():
                shutil.copy2(cached["file"], target)
            entry = dict(cached, file=str(target), reused_from=cached["file"])
            manifest[task_id] = entry
            reusable[task_id] = entry
            save_manifest(manifest_path, manifest)
            print(f"REUSED {index}/{len(args.task_ids)} {task_id}", flush=True)
            continue
        entry = {"status": "running", "text": text, "voice": args.voice.stem,
                 "voice_sha256": voice_hash, "speed": args.speed, "signature": signature,
                 "file": str(target), "started_at": time.strftime("%Y-%m-%d %H:%M:%S")}
        manifest[task_id] = entry
        save_manifest(manifest_path, manifest)
        started = time.monotonic()
        print(f"START {index}/{len(args.task_ids)} {task_id}: {text}", flush=True)
        try:
            job = client.submit(emo_control_method="与音色参考音频相同",
                                prompt=handle_file(str(args.voice)), text=text,
                                emo_ref_path=None, speed=args.speed, api_name="/gen_single")
            last_report = -30
            while not job.done():
                elapsed = time.monotonic() - started
                if elapsed - last_report >= 30:
                    status = job.status()
                    print(f"WAIT {task_id}: {elapsed:.0f}s, {status.code.value}", flush=True)
                    last_report = elapsed
                time.sleep(5)
            result = job.result()
            source = output_path(result)
            if source is None:
                raise RuntimeError(f"No audio file returned: {result!r}")
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(source, target)
            info = soundfile.info(str(target))
            if info.frames <= 0:
                raise RuntimeError("Generated audio is empty")
            entry.update(status="complete", elapsed_seconds=round(time.monotonic() - started, 2),
                         duration_seconds=round(info.duration, 3), sample_rate=info.samplerate,
                         channels=info.channels, source=str(source))
            save_manifest(manifest_path, manifest)
            reusable[task_id] = entry
            print(f"SAVED {target}: {info.duration:.2f}s", flush=True)
        except Exception as error:
            entry.update(status="failed", error=str(error))
            save_manifest(manifest_path, manifest)
            raise
    print("COMPLETE: all requested audio saved", flush=True)


if __name__ == "__main__":
    main()
