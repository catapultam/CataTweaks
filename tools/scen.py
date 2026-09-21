"""Claim graph for any scenario namespace: untagged (Modern), 2026_, 2070_, 2003_, 1962_."""
import json, os, re

BASE = r'C:\Program Files (x86)\Steam\steamapps\common\Terra Invicta\TerraInvicta_Data\StreamingAssets\Templates'
DLC = r'C:\Program Files (x86)\Steam\steamapps\common\Terra Invicta\DLC_Content\DarkSkies'
SCEN = {
    'Modern (2022)': ('', BASE),
    '2026': ('2026_', BASE),
    '2070': ('2070_', BASE),
    'Adamantine Sky (2003)': ('2003_', os.path.join(DLC, '2003_Scenario', 'Templates')),
    'Broken Earth (2112)': ('1962_', os.path.join(DLC, 'Broken_Earth_Scenario', 'Templates')),
}
load = lambda root, f: json.load(open(os.path.join(root, f), encoding='utf-8-sig'))
tagged = lambda name, pre: (name.startswith(pre) if pre else not re.match(r'^(19|20)\d\d_', name))


def graph(prefix, root):
    # both sides must belong to the namespace: some rows pair an untagged nation with a
    # 2070_ region and would otherwise smear two scenarios together.
    rows = [x for x in load(root, 'TIBilateralTemplate.json')
            if x.get('relationType') == 'Claim'
            and tagged(x.get('nation1') or '', prefix) and tagged(x.get('region1') or '', prefix)]
    claims, capital_of, hostile, start = {}, {}, {}, {}
    for x in rows:
        n, r = x['nation1'], x['region1']
        claims.setdefault(n, set()).add(r)
        if x.get('hostileClaim'):
            hostile.setdefault(n, set()).add(r)
        if x.get('capitalClaim'):
            capital_of.setdefault(r, []).append(n)
        if x.get('initialOwner'):
            start.setdefault(n, set()).add(r)
    regions = {r for v in claims.values() for r in v}
    return {'claims': claims, 'capital_of': capital_of, 'hostile': hostile, 'start': start,
            'regions': regions, 'nations': set(claims) | {n for v in capital_of.values() for n in v}}


def reach(g, root_nation, extra=()):
    claims = {k: set(v) for k, v in g['claims'].items()}
    for n, r in extra:
        claims.setdefault(n, set()).add(r)
    regions, absorbed = set(g['start'].get(root_nation, ())), {root_nation}
    while True:
        new_r = {r for t in absorbed for r in claims.get(t, ()) if r not in regions}
        new_n = {x for r in regions | new_r for x in g['capital_of'].get(r, ())
                 if x not in absorbed and not x.endswith('PRA')}
        if not new_r and not new_n:
            return regions, absorbed
        regions |= new_r
        absorbed |= new_n


if __name__ == '__main__':
    for label, (pre, root) in SCEN.items():
        g = graph(pre, root)
        print(f"\n{label}: {len(g['nations'])} nations, {len(g['regions'])} regions")
        for tag in ('GBR', 'SPR', 'CSA', 'RUS', 'BAV'):
            n = pre + tag
            if n not in g['nations']:
                print(f'   {tag:4s} absent')
                continue
            got, _ = reach(g, n)
            print(f"   {tag:4s} start {len(g['start'].get(n, ())):3d} -> {len(got):3d}/{len(g['regions'])}")
        # are Ireland and Jamaica claimable by anyone?
        for reg in ('Ireland', 'Jamaica'):
            r = pre + reg
            who = [n for n, v in g['claims'].items() if r in v and n != pre + reg[:3].upper()]
            print(f"   {reg:8s} claimed by {len(who)}: {[w.replace(pre, '') for w in who][:6]}")
