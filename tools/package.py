"""Builds the mod, checks it, and writes the release package.

    python tools/package.py [destination ...]

Always writes dist/CataTweaks-<version>.zip. Each destination given is also filled with the same
files, which is how a release reaches the game folder and the Steam upload staging folder.

Terra Invicta uploads to the Workshop from inside the game and hands out a fresh
DummyItemContentFolder<number> under AppData/LocalLow each time, so the staging path is an
argument rather than a constant.

Files the player's own install writes, such as Settings.xml, are never part of the package.
"""

import json
import os
import shutil
import subprocess
import sys
import zipfile

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

# Source path relative to ROOT, then the path it takes inside the package.
FILES = [
    ("bin/Release/net48/CataTweaks.dll", "CataTweaks.dll"),
    ("ModInfo.json", "ModInfo.json"),
    ("TIProjectTemplate.json", "TIProjectTemplate.json"),
    ("TIBilateralTemplate.json", "TIBilateralTemplate.json"),
    ("README.txt", "README.txt"),
    ("LICENSE", "LICENSE"),
    ("CataTweaks.png", "CataTweaks.png"),
]


def run(*command):
    print("$ " + " ".join(command))
    result = subprocess.run(command, cwd=ROOT)
    if result.returncode != 0:
        sys.exit("failed: " + " ".join(command))


def contents():
    """Every (source, package path) pair, with each localization file discovered rather than listed."""
    pairs = list(FILES)
    localization = os.path.join(ROOT, "Localization")
    for language in sorted(os.listdir(localization)):
        folder = os.path.join(localization, language)
        if not os.path.isdir(folder):
            continue
        for name in sorted(os.listdir(folder)):
            pairs.append(("Localization/" + language + "/" + name,
                          "Localization/" + language + "/" + name))
    return pairs


def version():
    with open(os.path.join(ROOT, "ModInfo.json"), encoding="utf-8") as handle:
        return json.load(handle)["Version"]


def main(destinations):
    run("dotnet", "build", "-c", "Release")
    run(sys.executable, "tools/validate.py")

    pairs = contents()
    missing = [src for src, _ in pairs if not os.path.exists(os.path.join(ROOT, src))]
    if missing:
        sys.exit("missing from the build: " + ", ".join(missing))

    dist = os.path.join(ROOT, "dist")
    os.makedirs(dist, exist_ok=True)
    archive = os.path.join(dist, "CataTweaks-" + version() + ".zip")
    # Everything sits under one folder, so the zip unpacks straight into Mods/Enabled.
    with zipfile.ZipFile(archive, "w", zipfile.ZIP_DEFLATED) as zip_file:
        for source, packaged in pairs:
            zip_file.write(os.path.join(ROOT, source), "CataTweaks/" + packaged)
    print("wrote " + archive + " (" + str(len(pairs)) + " files)")

    for destination in destinations:
        if not os.path.isdir(destination):
            sys.exit("not a folder: " + destination)
        for source, packaged in pairs:
            target = os.path.join(destination, packaged.replace("/", os.sep))
            os.makedirs(os.path.dirname(target), exist_ok=True)
            shutil.copy2(os.path.join(ROOT, source), target)
        print("filled " + destination)


if __name__ == "__main__":
    main(sys.argv[1:])
