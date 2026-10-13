"""Check exported resources and icon metadata without launching the game."""
import argparse
import hashlib
import io
import json
import struct
from pathlib import Path

import pefile
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
parser = argparse.ArgumentParser()
parser.add_argument('--installer', type=Path)
parser.add_argument('--uninstaller', type=Path)
args = parser.parse_args()
audit = json.loads((ROOT / 'output/windows_export_tools/resource-audit.json').read_text(encoding='utf-8'))
log = (ROOT / 'output/windows_export_tools/export-release-1.0.1.log').read_text(encoding='utf-8-sig')
missing = [path for path in audit['selected_files'] if not path.endswith('.cs') and path not in log]
leaked = [entry['path'] for entry in audit['excluded_assets'] if entry['path'] in log]
assert not missing, missing
assert not leaked, leaked
assert 'SCRIPT ERROR:' not in log and 'ERROR:' not in log, 'Export reported errors'
print(f'Resource check: all selected resources present, {len(audit["excluded_assets"])} unused assets excluded')


def check_icon(path: Path) -> None:
    pe = pefile.PE(str(path), fast_load=True)
    pe.parse_data_directories(directories=[pefile.DIRECTORY_ENTRY['IMAGE_DIRECTORY_ENTRY_RESOURCE']])
    icons = {}
    groups = []
    for resource in pe.DIRECTORY_ENTRY_RESOURCE.entries:
        if resource.id == 3:
            for icon in resource.directory.entries:
                data = icon.directory.entries[0].data.struct
                icons[icon.id] = pe.get_data(data.OffsetToData, data.Size)
        elif resource.id == 14:
            for group in resource.directory.entries:
                data = group.directory.entries[0].data.struct
                groups.append(pe.get_data(data.OffsetToData, data.Size))
    assert groups, f'No icon group in {path}'
    expected = Image.open(ROOT / 'assets/images/ui/forest_town_icon.ico')
    match_count = 0
    for group in groups:
        count = struct.unpack_from('<H', group, 4)[0]
        for index in range(count):
            values = struct.unpack_from('<BBBBHHIH', group, 6 + index * 14)
            width, height, colors, reserved, planes, bits, size, icon_id = values
            blob = icons[icon_id]
            entry = struct.pack('<BBBBHHII', width, height, colors, reserved, planes, bits, len(blob), 22)
            actual = Image.open(io.BytesIO(struct.pack('<HHH', 0, 1, 1) + entry + blob)).convert('RGBA')
            reference = expected.ico.getimage(actual.size).convert('RGBA')
            assert actual.tobytes() == reference.tobytes(), f'Icon pixels differ: {path}, {actual.size}'
            match_count += 1
    pe.close()
    print(f'Icon check: {path.name}, {match_count} embedded sizes match the supplied icon')


check_icon(ROOT / 'output/windows_1.0.1/ForestTown.exe')
if args.installer:
    check_icon(args.installer)
    print(f'Installer SHA256: {hashlib.sha256(args.installer.read_bytes()).hexdigest()}')
if args.uninstaller:
    check_icon(args.uninstaller)
