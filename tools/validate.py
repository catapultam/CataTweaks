"""Checks that run without the game installed.

The mod cannot be compiled in CI: it references Assembly-CSharp, nine Unity assemblies,
UnityModManager and 0Harmony out of the Terra Invicta install, which are proprietary and
are not in this repo. What can be checked is everything that fails silently in game, which is most of the
ways these files go wrong: a claim row whose scenario prefixes disagree is simply inert,
and a project with no localization shows its dataName to the player.
"""

import json
import os
import re
import sys
from pathlib import Path

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(ROOT, "tools"))
from localize import read  # noqa: E402
SCENARIOS = ["", "2026_", "2070_", "2003_", "1962_"]
problems = []


def fail(message):
    problems.append(message)


def read_json(name):
    path = os.path.join(ROOT, name)
    with open(path, encoding="utf-8-sig") as handle:
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


def read_values(path):
    """A localization file's key -> value, or an empty dict after a failure."""
    try:
        return read(Path(path))
    except ValueError as error:
        fail(str(error))
        return {}


def check_localization(projects):
    """Every language, not only English.

    The game has no English fallback. LocalizationManager.Find returns the key itself when a
    string is missing, so a player on another language reads
    TIProjectTemplate.displayName.Project_MareNostrum in the tech tree.
    """
    root = os.path.join(ROOT, "Localization")
    if not os.path.isdir(root):
        fail("no Localization folder")
        return
    languages = sorted(name for name in os.listdir(root)
                       if os.path.isdir(os.path.join(root, name)))
    if "en" not in languages:
        fail("no Localization/en")
    for language in languages:
        path = os.path.join(root, language, "TIProjectTemplate." + language)
        if not os.path.exists(path):
            fail(language + " has no TIProjectTemplate." + language)
            continue
        keys = read_values(path)
        for project in projects:
            name = project.get("dataName")
            for kind in ("displayName", "summary", "description"):
                key = "TIProjectTemplate." + kind + "." + name
                if key not in keys:
                    fail(language + " has no " + kind + " for " + name)
    # Every UI key English has, in every language, for the same reason: a key the game cannot
    # find prints itself, so a mod UI string missing one translation is fourteen ways to be wrong.
    english = read_values(os.path.join(root, "en", "UICataTweaks.en"))
    ui_keys = english.keys()
    # Settings rows derive their keys from field names, so checking literal Loc.T calls would
    # miss an untranslated new row. Cover every drawn bool/float, including the two reused keys.
    with open(os.path.join(ROOT, "Plugin.cs"), encoding="utf-8") as handle:
        source = handle.read()
    settings = source.split("public class Settings", 1)[1].split("public static class Main", 1)[0]
    for field in re.findall(r'\[Draw\([^\n]+\)\]\s+public (?:bool|float) (\w+)', settings):
        if field in ("pooledStationConstruction", "shipyardRally"):
            stem = "PooledConstruction" if field == "pooledStationConstruction" else "Rally"
            required = ["UI.CataTweaks." + stem + suffix for suffix in ("Setting", "Tooltip")]
        else:
            required = ["UI.CataTweaks.Setting." + field + suffix for suffix in (".Label", ".Tooltip")]
        for key in required:
            if key not in ui_keys:
                fail("settings row has no localization: " + key)

    for language in languages:
        path = os.path.join(root, language, "UICataTweaks." + language)
        if not os.path.exists(path):
            fail(language + " has no UICataTweaks." + language)
            continue
        values = read_values(path)
        for key in sorted(ui_keys - values.keys()):
            fail(language + " has no " + key)
        for key in ui_keys & values.keys():
            if set(re.findall(r'\{\d+\}', english[key])) != set(re.findall(r'\{\d+\}', values[key])):
                fail(language + " has mismatched placeholders for " + key)
    print(str(len(languages)) + " of 14 languages, " + str(len(ui_keys)) + " UI keys")
    if len(languages) != 14:
        fail("Terra Invicta ships 14 languages and the game has no English fallback")


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
        # In Broken Earth, Ireland starts hostile and is made peaceful by The Sun Never Sets.
        # The other four scenarios retain its Commonwealth Restored claim.
        if family == "ClaimGBR_Ireland_CommonwealthRestored":
            if found != set(SCENARIOS) - {"1962_"}:
                fail(family + " must exist in the four non-Broken-Earth scenarios")
            continue
        if len(found) not in (1, len(SCENARIOS)):
            fail(family + " exists in " + str(len(found)) + " scenarios, not 1 or 5")

    for nation, region, project in (
        ("GBR", "Jamaica", "Project_TheSunNeverSets"),
        ("GBR", "Ireland", "Project_TheSunNeverSets"),
        ("CSA", "Jamaica", "Project_GreaterDominion"),
    ):
        rows = [r for r in claims if r.get("nation1") == "1962_" + nation
                and r.get("region1") == "1962_" + region]
        starting = [r for r in rows if not r.get("projectUnlockName") and r.get("hostileClaim")]
        peaceful = [r for r in rows if r.get("projectUnlockName") == project and not r.get("hostileClaim")]
        if len(rows) != 2 or len(starting) != 1 or len(peaceful) != 1:
            fail("Broken Earth " + nation + " must start with a hostile claim on " + region
                 + " and gain a peaceful claim through " + project)


def check_roman_projects(projects, claims):
    roman = [p for p in projects if p.get("requiresNation") == "SPR"]
    ids = {p["dataName"] for p in roman}
    if len(roman) != 6:
        fail("Rome must have six regional conquest projects")
    if sum(p.get("researchCost", 0) for p in roman) < 200000:
        fail("Rome's regional projects must cost at least 200,000 RP combined")
    for project in roman:
        name = project["dataName"]
        if project.get("prereqs") != ["GreatNations"]:
            fail(name + " must unlock independently after Great Nations")
        if project.get("effects") != ["Effect_ControlPointMaintenanceBonus10"]:
            fail(name + " must grant +10 control point capacity")
        if project.get("repeatable") or not project.get("oneTimeGlobally"):
            fail(name + " must be a unique, non-repeatable conquest project")
        rows = [r for r in claims if r.get("projectUnlockName") == name]
        if not rows:
            fail(name + " has no territorial claims")
        if any(r.get("nation1") != "1962_SPR" or prefix_of(r.get("region1", "")) != "1962_"
               for r in rows):
            fail(name + " must grant claims only to Rome in Broken Earth")
    regions = [r.get("region1") for r in claims if r.get("projectUnlockName") in ids]
    if len(regions) != len(set(regions)):
        fail("Rome's regional conquest projects have overlapping claims")
    # Anchor regions catch swapped theatres and accidental reintroduction of capital-only lists.
    anchors = {
        "Project_MareNostrum": ["Paris", "EastGermany", "Moskva"],
        "Project_CataTweaks_RomanAfrica": ["Egypt", "Lagos", "CapeTown"],
        "Project_CataTweaks_RomanIndia": ["NewDelhi", "Mumbai", "SriLanka"],
        "Project_CataTweaks_RomanNorthAmerica": ["NewYork", "MexicoCity", "Jamaica"],
        "Project_CataTweaks_RomanSouthAmerica": ["Colombia", "SaoPaulo", "Pampas"],
        "Project_ImperiumSineFine": ["Beijing", "Anatolia", "Java"],
    }
    # Use the authored map names in dataName; Broken Earth renames some actual region templates.
    for name, expected in anchors.items():
        rows = [r for r in claims if r.get("projectUnlockName") == name]
        for region in expected:
            if not any(r.get("dataName", "").startswith("Claim1962_SPR_" + region + "_") for r in rows):
                fail(name + " is missing its regional claim on " + region)
        if len(rows) < 20:
            fail(name + " must grant broad regional claims")


def check_modinfo():
    info = read_json("ModInfo.json")
    for field in ("Id", "DisplayName", "Version", "AssemblyName", "EntryMethod"):
        if not info.get(field):
            fail("ModInfo.json is missing " + field)
    version = info.get("Version", "")
    if not re.match(r"^\d+\.\d+\.\d+$", version):
        fail("ModInfo.json version is not x.y.z: " + version)
    csproj = os.path.join(ROOT, "CataTweaks.csproj")
    with open(csproj, encoding="utf-8") as handle:
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
            with open(path, encoding="utf-8", errors="replace") as handle:
                text = handle.read()
            if "—" in text or "–" in text:
                fail(os.path.relpath(path, ROOT) + " contains an em or en dash")


def main():
    projects = read_json("TIProjectTemplate.json")
    claims = read_json("TIBilateralTemplate.json")
    names = check_projects(projects)
    check_localization(projects)
    check_claims(claims, names)
    check_roman_projects(projects, claims)
    check_modinfo()
    notifications = read_json("TINotificationTemplate.json")
    rally = next((item for item in notifications if item.get("dataName") == "CataTweaks_ShipRally"), None)
    if rally is None:
        fail("missing ship rally notification template")
    elif (rally.get("alertAudience") != "None" or rally.get("firstAlertOverride") is not False
          or rally.get("summaryAudience", {}).get("category") != "SpaceEvents"
          or rally.get("newsFeedAudience") != "PrimaryFactions"):
        fail("ship rallies must be logged as space events without a default or first-time popup")
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
