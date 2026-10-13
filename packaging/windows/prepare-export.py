"""Build a conservative resource allowlist, including runtime-generated paths."""
import hashlib
import json
import re
import shutil
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
REPORT_DIR = ROOT / 'output' / 'windows_export_tools'
RESOURCE_RE = re.compile(r'''["']\*?(res://[^"']+)["']''')
TEXT_EXTENSIONS = {'.tscn', '.tres', '.gd', '.cs', '.json', '.gdshader', '.godot'}
selected: set[str] = set()
pending: list[str] = []
missing: set[str] = set()
templates: set[str] = set()


def add(path: str, required: bool = True) -> None:
    if not path.startswith('res://'):
        return
    relative = path[6:]
    disk = ROOT / relative
    if disk.is_dir():
        return
    if any(token in relative for token in ('%', '{', '}')):
        templates.add(path)
        return
    if not disk.is_file():
        if required:
            missing.add(path)
        return
    if path not in selected:
        selected.add(path)
        pending.append(path)


def source(name: str) -> str:
    return (ROOT / name).read_text(encoding='utf-8-sig')


def block(text: str, start: str, end: str) -> str:
    return text.split(start, 1)[1].split(end, 1)[0]


def strings(text: str) -> list[str]:
    return re.findall(r'"([^"\r\n]+)"', text)


def prepare_icon(input_path: Path) -> None:
    destination = ROOT / 'assets/images/ui/forest_town_icon.png'
    destination.parent.mkdir(parents=True, exist_ok=True)
    if destination.exists() and destination.read_bytes() != input_path.read_bytes():
        raise RuntimeError(f'Icon destination already contains a different image: {destination}')
    shutil.copyfile(input_path, destination)
    image = Image.open(destination).convert('RGBA')
    image.save(destination.with_suffix('.ico'), format='ICO', sizes=[(n, n) for n in (16, 24, 32, 48, 64, 128, 256)])
    print(f'Icon: {image.size}, alpha range={image.getchannel("A").getextrema()}')
    print(f'Icon source SHA256: {hashlib.sha256(destination.read_bytes()).hexdigest()}')


def collect() -> None:
    add('res://project.godot')
    for path in (ROOT / 'scenes').rglob('*.tscn'):
        add('res://' + path.relative_to(ROOT).as_posix())
    # C# helpers can construct resources without being directly attached to a scene.
    for path in (ROOT / 'scripts').rglob('*.cs'):
        if 'tools' not in path.parts:
            add('res://' + path.relative_to(ROOT).as_posix())
    for day in range(1, 13):
        add(f'res://data/days/day_{day:02}.json')
        add(f'res://assets/images/stoies/day_{day}.png')
        add(f'res://assets/audio/voice/stories/story{day}.wav')
        add(f'res://assets/audio/voice/stories/day{day}_story.wav')
    for count in range(6):
        add(f'res://assets/images/ui/num{count}.png')

    picnic = source('scripts/days/Day4Picnic.cs')
    for names in re.findall(r'=\s*\[([^\]]+)\]', block(picnic, 'GroupAssets =', 'GroupPositions =')):
        for name in strings(names):
            add(f'res://assets/images/objects/day4/{name}.png')
    for filename in re.findall(r'=\s*"([^"]+)"', block(picnic, 'PlacementSoundFiles =', 'GroupAssets =')):
        add(f'res://assets/audio/voice/day4/{filename}')
    add('res://assets/audio/voice/day4/day4_bgm.mp3')

    toys = source('scripts/days/Day8ToyRoom.cs')
    for filename in re.findall(r'=\s*"([^"]+)"', block(toys, 'SoundFiles =', '[Export')):
        add(f'res://assets/audio/voice/day8/{filename}')

    courtyard = source('scripts/days/Day10Courtyard.cs')
    for name in strings(block(courtyard, 'string[][] Assets =', '// Layout')):
        add(f'res://assets/images/objects/day10/{name}.tres')
        if name.endswith('_closed'):
            add(f'res://assets/images/objects/day10/{name.replace("_closed", "_opened")}.tres')
    add('res://assets/images/objects/day10/tool_watering_can.tres')

    treasure = source('scripts/days/Day11Treasure.cs')
    for name in strings(block(treasure, 'string[][] AssetNames =', 'string[] SceneGroups')):
        add(f'res://assets/images/objects/day11/{name}.tres')
        if name.endswith('_closed'):
            add(f'res://assets/images/objects/day11/{name.replace("_closed", "_open")}.tres')
    add('res://assets/images/objects/day11/treasure_chest_open.tres')

    festival = source('scripts/days/Day12ActivityCatalog.cs')
    for kind, day, names in re.findall(r'\b([PT])\((\d+),\s*([^)]*)\)', festival):
        for name in strings(names):
            extension = 'png' if kind == 'P' else 'tres'
            add(f'res://assets/images/objects/day{day}/{name}.{extension}')

    for module in range(1, 9):
        add(f'res://assets/images/objects/day5/m{module}.png')
        add(f'res://assets/images/objects/day5/m{module}_bg.png')
        add(f'res://scenes/game/day5_targets/m{module}.tscn')
        for part in range(1, 6):
            add(f'res://assets/images/objects/day5/m{module}/m{module}_{part}.png')

    while pending:
        resource = pending.pop()
        disk = ROOT / resource[6:]
        if disk.suffix in TEXT_EXTENSIONS:
            for reference in RESOURCE_RE.findall(disk.read_text(encoding='utf-8-sig')):
                add(reference)


def report() -> None:
    inventory = []
    for path in (ROOT / 'assets').rglob('*'):
        if path.is_file() and path.suffix.lower() in {'.png', '.jpg', '.jpeg', '.webp', '.svg', '.wav', '.mp3', '.ogg', '.tres', '.ttf', '.otf'}:
            resource = 'res://' + path.relative_to(ROOT).as_posix()
            inventory.append({'path': resource, 'bytes': path.stat().st_size, 'included': resource in selected})
    excluded = sorted((entry for entry in inventory if not entry['included']), key=lambda entry: entry['bytes'], reverse=True)
    result = {
        'selected_files': sorted(selected - {'res://project.godot'}),
        'asset_count': len(inventory),
        'selected_asset_count': sum(entry['included'] for entry in inventory),
        'excluded_asset_count': len(excluded),
        'excluded_source_bytes': sum(entry['bytes'] for entry in excluded),
        'dynamic_path_templates': sorted(templates),
        'missing_references': sorted(missing),
        'excluded_assets': excluded,
    }
    REPORT_DIR.mkdir(parents=True, exist_ok=True)
    (REPORT_DIR / 'resource-audit.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps({key: result[key] for key in ('asset_count', 'selected_asset_count', 'excluded_asset_count', 'excluded_source_bytes', 'dynamic_path_templates', 'missing_references')}, ensure_ascii=False, indent=2))
    if missing:
        raise RuntimeError('Unresolved resource references; review resource-audit.json before exporting.')

    preset_path = ROOT / 'export_presets.cfg'
    preset = preset_path.read_text(encoding='utf-8-sig')
    preset = re.sub(r'^export_filter=.*$', 'export_filter="resources"', preset, flags=re.MULTILINE)
    file_array = 'export_files=PackedStringArray(' + ', '.join(json.dumps(path, ensure_ascii=False) for path in result['selected_files']) + ')'
    if re.search(r'^export_files=', preset, flags=re.MULTILINE):
        preset = re.sub(r'^export_files=.*$', lambda _: file_array, preset, flags=re.MULTILINE)
    else:
        preset = preset.replace('export_filter="resources"\n', 'export_filter="resources"\n' + file_array + '\n')
    preset_path.write_text(preset, encoding='utf-8')


if __name__ == '__main__':
    import argparse
    parser = argparse.ArgumentParser()
    parser.add_argument('--icon', type=Path)
    args = parser.parse_args()
    if args.icon:
        prepare_icon(args.icon)
    collect()
    report()
