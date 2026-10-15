"""Exercise the localization tool in an isolated copy, including its failure-before-write policy."""
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
with tempfile.TemporaryDirectory(prefix="catatweaks-localization-") as folder:
    copy = Path(folder)
    shutil.copy2(ROOT / "TIProjectTemplate.json", copy / "TIProjectTemplate.json")
    shutil.copytree(ROOT / "Localization", copy / "Localization")
    paths = list((copy / "Localization").glob("*/TIProjectTemplate.*"))
    before = {p: p.read_bytes() for p in paths}
    result = subprocess.run([sys.executable, str(ROOT / "tools/localize.py"), "--root", str(copy)],
                            capture_output=True, text=True)
    assert result.returncode == 0, result.stderr
    # All translated values, including the four new Roman branches, must survive a normal run.
    for path in paths:
        old = dict(line.split("=", 1) for line in before[path].decode().splitlines() if "=" in line)
        new = dict(line.split("=", 1) for line in path.read_text().splitlines() if "=" in line)
        assert old == new, path
    broken = copy / "Localization/ukr/TIProjectTemplate.ukr"
    broken.write_text("\n".join(line for line in broken.read_text().splitlines()
                              if not line.startswith("TIProjectTemplate.description.Project_CataTweaks_RomanAfrica=")) + "\n")
    before_failure = {p: p.read_bytes() for p in paths}
    result = subprocess.run([sys.executable, str(ROOT / "tools/localize.py"), "--root", str(copy)],
                            capture_output=True, text=True)
    assert result.returncode != 0 and "missing translations" in result.stderr, result.stderr
    assert all(p.read_bytes() == content for p, content in before_failure.items()), "Partial rewrite on failure"
print("localization tool preserves all translations and rejects incomplete input before writing")
