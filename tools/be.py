"""Broken Earth claim graph, from the DLC's own templates (the authoritative source).

DLC_Content/DarkSkies/Broken_Earth_Scenario/Templates ships readable JSON: its own nations,
regions, bilaterals and projects, all 1962_-prefixed. Base-game untagged rows also apply, so the
graph is the union of both, resolved through each template's referenceAlias.
"""
import json, os

BASE = r'C:\Program Files (x86)\Steam\steamapps\common\Terra Invicta\TerraInvicta_Data\StreamingAssets\Templates'
DLC = r'C:\Program Files (x86)\Steam\steamapps\common\Terra Invicta\DLC_Content\DarkSkies\Broken_Earth_Scenario\Templates'
load = lambda root, f: json.load(open(os.path.join(root, f), encoding='utf-8-sig'))

be_nations = load(DLC, 'TINationTemplate.json')
be_regions = load(DLC, 'TIRegionTemplate.json')
NATIONS = {n['dataName'] for n in be_nations}
REGIONS = {r['dataName'] for r in be_regions}
# alias -> 1962_ dataName, so untagged base rows (nation1 "GBR") resolve onto Broken Earth
N_ALIAS = {n.get('referenceAlias') or n['dataName']: n['dataName'] for n in be_nations}
R_ALIAS = {r.get('referenceAlias') or r['dataName']: r['dataName'] for r in be_regions}
NAME = {n['dataName']: n.get('friendlyName') or n['dataName'] for n in be_nations}


def resolve_nation(tag):
    return tag if tag in NATIONS else N_ALIAS.get(tag)


def resolve_region(tag):
    return tag if tag in REGIONS else R_ALIAS.get(tag)


def claim_rows():
    """Every Claim bilateral that applies to Broken Earth, as (nation, region, hostile, capital, project)."""
    # DLC rows only: untagged base rows reference nations that do not exist in Broken Earth
    # (verified against a live save - 117 such claims, none of them present in game).
    out = []
    for root in (DLC,):
        for x in load(root, 'TIBilateralTemplate.json'):
            if x.get('relationType') != 'Claim':
                continue
            n, r = resolve_nation(x.get('nation1') or ''), resolve_region(x.get('region1') or '')
            if n and r:
                out.append((n, r, bool(x.get('hostileClaim')), bool(x.get('capitalClaim')),
                            x.get('projectUnlockName') or '', bool(x.get('initialOwner'))))
    return out


ROWS = claim_rows()
CLAIMS, CAPITAL_OF, HOSTILE = {}, {}, {}
for n, r, hostile, capital, proj, owns in ROWS:
    CLAIMS.setdefault(n, set()).add(r)
    HOSTILE.setdefault(n, set())
    if hostile:
        HOSTILE[n].add(r)
    if capital:
        CAPITAL_OF.setdefault(r, []).append(n)
# Starting ownership rides on the claim rows: initialOwner marks who holds the region at start.
START = {}
for n, r, hostile, capital, proj, owns in ROWS:
    if owns:
        START.setdefault(n, set()).add(r)


def reach(root, extra=(), skip=('1962_PRA', 'PRA')):
    claims = {k: set(v) for k, v in CLAIMS.items()}
    for n, r in extra:
        claims.setdefault(resolve_nation(n) or n, set()).add(resolve_region(r) or r)
    regions, absorbed = set(START.get(root, ())), {root}
    while True:
        new_r = {r for t in absorbed for r in claims.get(t, ()) if r not in regions}
        new_n = {x for r in regions | new_r for x in CAPITAL_OF.get(r, ())
                 if x not in absorbed and x not in skip}
        if not new_r and not new_n:
            return regions, absorbed
        regions |= new_r
        absorbed |= new_n


if __name__ == '__main__':
    print(f'Broken Earth: {len(NATIONS)} nations, {len(REGIONS)} regions, {len(ROWS)} claim rows')
    print(f'regions with a capital claim: {len(CAPITAL_OF)} | nations with starting regions: {len(START)}')
    for tag in ('SPR', 'GBR', 'CSA', 'RUS', 'BAV'):
        n = resolve_nation(tag)
        if not n:
            print(f'  {tag}: not in Broken Earth')
            continue
        got, nat = reach(n)
        print(f'  {tag:4s} {NAME[n][:24]:24s} start {len(START.get(n, ())):3d} -> {len(got):3d}/{len(REGIONS)} '
              f'via {len(nat)} nations')
