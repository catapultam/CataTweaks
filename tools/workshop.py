"""Stages the Workshop item and uploads it with SteamCMD.

    python tools/workshop.py stage
    python tools/workshop.py upload --changenote "..." [--login NAME]

stage runs package.py into dist/workshop/CataTweaks, writes the WorkshopItemInfo.xml the game
writes on an in-game upload, and writes dist/workshop/CataTweaks.vdf for SteamCMD.

upload stages, then runs SteamCMD's workshop_build_item, Valve's command-line route for updating a
Workshop item. It publishes to every subscriber. The title, description and preview on the
Workshop page are set from here too, the description from workshop-description.txt.

SteamCMD needs one interactive login first, for Steam Guard:

    C:\\steamcmd\\steamcmd.exe +login NAME +quit

It caches the session, and --login defaults to the one account cached there.
"""

import argparse
import os
import re
import shutil
import subprocess
import sys
from xml.sax.saxutils import escape

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
STEAMCMD = os.environ.get("STEAMCMD", r"C:\steamcmd\steamcmd.exe")

APP_ID = 1176470
PUBLISHED_FILE_ID = 3805519545
TITLE = "CataTweaks"
ICON = "CataTweaks.png"
TAGS = ["Gameplay"]

STAGE = os.path.join(ROOT, "dist", "workshop", "CataTweaks")
VDF = os.path.join(ROOT, "dist", "workshop", "CataTweaks.vdf")


def description():
    with open(os.path.join(ROOT, "workshop-description.txt"), encoding="utf-8") as handle:
        text = handle.read().rstrip("\n")
    if len(text) > 8000:
        sys.exit("workshop-description.txt is " + str(len(text)) + " characters; Steam allows 8000")
    return text


def item_info(text):
    """The file an in-game upload writes into the content, so that route stays open."""
    tags = "".join("    <string>" + escape(tag) + "</string>\r\n" for tag in TAGS)
    return ('<?xml version="1.0"?>\r\n'
            '<WorkshopItemInfo xmlns:xsd="http://www.w3.org/2001/XMLSchema"'
            ' xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">\r\n'
            "  <PublishedFileId>" + str(PUBLISHED_FILE_ID) + "</PublishedFileId>\r\n"
            "  <Name>" + escape(TITLE) + "</Name>\r\n"
            "  <Description>" + escape(text) + "</Description>\r\n"
            "  <IconFileName>" + ICON + "</IconFileName>\r\n"
            "  <Tags>\r\n" + tags + "  </Tags>\r\n"
            "</WorkshopItemInfo>")


def vdf_string(value):
    return '"' + value.replace("\\", "\\\\").replace('"', '\\"') + '"'


def vdf(text, changenote):
    # Forward slashes, so a path needs no escaping.
    fields = [
        ("appid", str(APP_ID)),
        ("publishedfileid", str(PUBLISHED_FILE_ID)),
        ("contentfolder", STAGE.replace("\\", "/")),
        ("previewfile", os.path.join(STAGE, ICON).replace("\\", "/")),
        ("visibility", "0"),                     # public, as the in-game upload sets it
        ("title", TITLE),
        ("description", text),
    ]
    if changenote:
        fields.append(("changenote", changenote))
    body = "".join("\t" + vdf_string(key) + " " + vdf_string(value) + "\n" for key, value in fields)
    return '"workshopitem"\n{\n' + body + "}\n"


def stage(changenote=None):
    if os.path.isdir(STAGE):
        shutil.rmtree(STAGE)
    os.makedirs(STAGE)
    result = subprocess.run([sys.executable, os.path.join(ROOT, "tools", "package.py"), STAGE], cwd=ROOT)
    if result.returncode != 0:
        sys.exit("package.py failed")
    text = description()
    with open(os.path.join(STAGE, "WorkshopItemInfo.xml"), "w", encoding="utf-8", newline="") as handle:
        handle.write(item_info(text))
    with open(VDF, "w", encoding="utf-8", newline="\n") as handle:
        handle.write(vdf(text, changenote))
    print("staged " + STAGE)
    print("wrote " + VDF)


def cached_login():
    config = os.path.join(os.path.dirname(STEAMCMD), "config", "config.vdf")
    if not os.path.exists(config):
        return None
    with open(config, encoding="utf-8", errors="replace") as handle:
        text = handle.read()
    block = re.search(r'"Accounts"\s*\{(.*?)\n\t*\}', text, re.S)
    names = re.findall(r'^\s*"([^"]+)"\s*$', block.group(1), re.M) if block else []
    return names[0] if len(names) == 1 else None


def upload(changenote, login):
    login = login or cached_login()
    if not login:
        sys.exit("no cached SteamCMD login; run " + STEAMCMD + " +login NAME +quit first, or pass --login")
    stage(changenote)
    command = [STEAMCMD, "+login", login, "+workshop_build_item", VDF, "+quit"]
    print("$ " + " ".join(command))
    result = subprocess.run(command, cwd=os.path.dirname(STEAMCMD))
    if result.returncode != 0:
        sys.exit("steamcmd failed with exit code " + str(result.returncode))


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    sub = parser.add_subparsers(dest="command", required=True)
    sub.add_parser("stage")
    up = sub.add_parser("upload")
    up.add_argument("--changenote", required=True)
    up.add_argument("--login")
    args = parser.parse_args()
    if args.command == "stage":
        stage()
    else:
        upload(args.changenote, args.login)


if __name__ == "__main__":
    main()
