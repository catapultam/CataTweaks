"""Exercise the localization tool in an isolated copy, including its failure-before-write policy."""
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(ROOT / "tools"))
from localize import read  # noqa: E402


def raw(path):
    # Every line with "=", comments included, so a dropped comment line also fails.
    return {line for line in path.read_text(encoding="utf-8-sig").splitlines() if "=" in line}


def run(root):
    return subprocess.run([sys.executable, str(ROOT / "tools/localize.py"), "--root", str(root)],
                          capture_output=True, text=True)


with tempfile.TemporaryDirectory(prefix="catatweaks-localization-") as folder:
    copy = Path(folder)
    shutil.copy2(ROOT / "TIProjectTemplate.json", copy / "TIProjectTemplate.json")
    shutil.copytree(ROOT / "Localization", copy / "Localization")
    paths = list((copy / "Localization").glob("*/TIProjectTemplate.*"))
    before = {p: (read(p), raw(p)) for p in paths}
    result = run(copy)
    assert result.returncode == 0, result.stderr
    # All translated values, including the four new Roman branches, must survive a normal run.
    for path in paths:
        assert (read(path), raw(path)) == before[path], path
    broken = copy / "Localization/ukr/TIProjectTemplate.ukr"
    broken.write_text("\n".join(line for line in broken.read_text().splitlines()
                              if not line.startswith("TIProjectTemplate.description.Project_CataTweaks_RomanAfrica=")) + "\n")
    before_failure = {p: p.read_bytes() for p in paths}
    result = run(copy)
    assert result.returncode != 0 and "missing translations" in result.stderr, result.stderr
    assert all(p.read_bytes() == content for p, content in before_failure.items()), "Partial rewrite on failure"
print("localization tool preserves all translations and rejects incomplete input before writing")
