"""Checks that run without the game installed.

The mod cannot be compiled in CI: it references Assembly-CSharp and eleven Unity
assemblies out of the Terra Invicta install, which are proprietary and are not in this
repo. What can be checked is everything that fails silently in game, which is most of the
ways these files go wrong: a claim row whose scenario prefixes disagree is simply inert,
and a project with no localization shows its dataName to the player.
"""

import io
import json
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SCENARIOS = ["", "2026_", "2070_", "2003_", "1962_"]
problems = []


def fail(message):
    problems.append(message)


def read_json(name):
    path = os.path.join(ROOT, name)
    with io.open(path, encoding="utf-8-sig") as handle:
        return json.load(handle)


def prefix_of(data_name):
    for scenario in SCENARIOS:
        if scenario and data_name.startswith(scenario):
            return scenario
    return ""


def check_projects(projects):
    seen = set()
    for project in projects:
        name = project.get("dataName")
        if not name:
            fail("a project has no dataName")
            continue
        if name in seen:
            fail("duplicate project " + name)
        seen.add(name)
        for field in ("friendlyName", "techCategory", "researchCost"):
            if field not in project:
                fail(name + " is missing " + field)
        if project.get("researchCost", 0) <= 0:
            fail(name + " has no research cost")
        for prereq in project.get("prereqs", []):
            if prereq.startswith("Project_CataTweaks") and prereq not in seen:
                fail(name + " depends on " + prereq + ", which is declared after it")
    return seen


def check_localization(projects):
    path = os.path.join(ROOT, "Localization", "en", "TIProjectTemplate.en")
    if not os.path.exists(path):
        fail("no Localization/en/TIProjectTemplate.en")
        return
    with io.open(path, encoding="utf-8") as handle:
        keys = set()
        for line in handle:
            if "=" in line:
                keys.add(line.split("=", 1)[0].strip())
    for project in projects:
        name = project.get("dataName")
        for kind in ("displayName", "summary", "description"):
            key = "TIProjectTemplate." + kind + "." + name
            if key not in keys:
                fail("no " + kind + " localization for " + name)


def check_claims(claims, project_names):
    seen = set()
    for row in claims:
        name = row.get("dataName")
        if not name:
            fail("a claim row has no dataName")
            continue
        if name in seen:
            fail("duplicate claim row " + name)
        seen.add(name)

        nation = row.get("nation1", "")
        region = row.get("region1", "")
        if not nation or not region:
            fail(name + " is missing nation1 or region1")
            continue
        # A row whose two halves come from different scenarios is not an error the game
        # reports: it simply never matches anything.
        if prefix_of(nation) != prefix_of(region):
            fail(name + " mixes scenarios: " + nation + " with " + region)
        if row.get("relationType") != "Claim":
            fail(name + " is not a Claim")

        unlock = row.get("projectUnlockName")
        if unlock and unlock.startswith("Project_CataTweaks") and unlock not in project_names:
            fail(name + " is gated behind " + unlock + ", which this mod does not define")

    # Every claim chain should exist in all five scenarios, or it is inert in four of them.
    families = {}
    for name in seen:
        stripped = name
        for scenario in SCENARIOS:
            if scenario and stripped.startswith("Claim" + scenario):
                stripped = "Claim" + stripped[len("Claim" + scenario):]
                break
        families.setdefault(stripped, set()).add(prefix_of(name[len("Claim"):]))
    for family, found in sorted(families.items()):
        if len(found) not in (1, len(SCENARIOS)):
            fail(family + " exists in " + str(len(found)) + " scenarios, not 1 or 5")


def check_modinfo(projects):
    info = read_json("ModInfo.json")
    for field in ("Id", "DisplayName", "Version", "AssemblyName", "EntryMethod"):
        if not info.get(field):
            fail("ModInfo.json is missing " + field)
    version = info.get("Version", "")
    if not re.match(r"^\d+\.\d+\.\d+$", version):
        fail("ModInfo.json version is not x.y.z: " + version)
    csproj = os.path.join(ROOT, "CataTweaks.csproj")
    with io.open(csproj, encoding="utf-8") as handle:
        text = handle.read()
    match = re.search(r"<Version>([^<]+)</Version>", text)
    if match and match.group(1) != version:
        fail("CataTweaks.csproj says " + match.group(1) + ", ModInfo.json says " + version)


def check_house_style():
    for folder, _, names in os.walk(ROOT):
        if any(part in folder for part in (".git", "bin", "obj")):
            continue
        for name in names:
            if not name.endswith((".cs", ".md", ".txt", ".json", ".en")):
                continue
            path = os.path.join(folder, name)
            with io.open(path, encoding="utf-8", errors="replace") as handle:
                text = handle.read()
            if "—" in text or "–" in text:
                fail(os.path.relpath(path, ROOT) + " contains an em or en dash")


def main():
    projects = read_json("TIProjectTemplate.json")
    claims = read_json("TIBilateralTemplate.json")
    names = check_projects(projects)
    check_localization(projects)
    check_claims(claims, names)
    check_modinfo(projects)
    check_house_style()

    print(str(len(projects)) + " projects, " + str(len(claims)) + " claim rows")
    for problem in problems:
        print("FAIL " + problem)
    if problems:
        return 1
    print("ok")
    return 0


if __name__ == "__main__":
    sys.exit(main())
