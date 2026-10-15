"""Validate and reorder the checked-in project translations without inventing a fallback.

Localization/<lang>/TIProjectTemplate.<lang> is the translation source of truth. Project IDs
come from TIProjectTemplate.json, rather than a second, stale project catalogue. All fourteen
languages are checked before any file is written. Use --check for a read-only validation.
"""

import argparse
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
LANGUAGES = ("en", "chs", "cht", "cze", "deu", "esp", "fr", "ita", "jpn", "kor", "pol", "por", "rus", "ukr")
KINDS = ("displayName", "summary", "description")


def read(path):
    values = {}
    for line in path.read_text(encoding="utf-8-sig").splitlines():
        if not line.strip() or line.lstrip().startswith("//"):
            continue
        if "=" not in line:
            raise ValueError(f"{path}: malformed localization line")
        key, value = line.split("=", 1)
        key = key.strip()
        if key in values:
            raise ValueError(f"{path}: duplicate key {key}")
        if not value.split("//", 1)[0].strip():
            raise ValueError(f"{path}: empty translation for {key}")
        values[key] = value
    return values


def prepare(root):
    projects = json.loads((root / "TIProjectTemplate.json").read_text(encoding="utf-8-sig"))
    ids = [project["dataName"] for project in projects]
    if len(ids) != len(set(ids)):
        raise ValueError("duplicate project ID in TIProjectTemplate.json")
    order = ["TIProjectTemplate." + kind + "." + p["dataName"] for p in projects for kind in KINDS]
    english = read(root / "Localization/en/TIProjectTemplate.en")
    required = set(order) | english.keys()
    files = []
    for language in LANGUAGES:
        path = root / "Localization" / language / ("TIProjectTemplate." + language)
        values = read(path)
        missing = required - values.keys()
        if missing:
            raise ValueError(f"{language}: missing translations: {', '.join(sorted(missing))}")
        keys = order + [key for key in values if key not in order]
        content = "\n".join(key + "=" + values[key] for key in keys) + "\n"
        files.append((path, content))
    return files


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true", help="Validate without writing files")
    parser.add_argument("--root", type=Path, default=ROOT, help="Repository root (also useful for isolated checks)")
    args = parser.parse_args()
    try:
        files = prepare(args.root)
    except (OSError, ValueError, KeyError) as error:
        parser.exit(1, str(error) + "\n")
    if not args.check:
        for path, content in files:
            path.write_text(content, encoding="utf-8")
    print(f"{'checked' if args.check else 'updated'} {len(files)} languages; current project translations preserved")


if __name__ == "__main__":
    main()
