"""Per-scenario region naming. Claims are authored once in map-layer names and translated."""
import json, os, re

BASE = r'C:\Program Files (x86)\Steam\steamapps\common\Terra Invicta\TerraInvicta_Data\StreamingAssets\Templates'
DLC = r'C:\Program Files (x86)\Steam\steamapps\common\Terra Invicta\DLC_Content\DarkSkies'
SCENARIOS = [
    ('Modern (2022)', '', BASE),
    ('2026', '2026_', BASE),
    ('2070', '2070_', BASE),
    ('Adamantine Sky (2003)', '2003_', os.path.join(DLC, '2003_Scenario', 'Templates')),
    ('Broken Earth (2112)', '1962_', os.path.join(DLC, 'Broken_Earth_Scenario', 'Templates')),
]
load = lambda root, f: json.load(open(os.path.join(root, f), encoding='utf-8-sig'))
in_ns = lambda name, pre: (name.startswith(pre) if pre else not re.match(r'^(19|20)\d\d_', name))


def region_map(prefix, root):
    """map-layer name -> this scenario's region dataName."""
    out = {}
    for r in load(root, 'TIRegionTemplate.json'):
        if in_ns(r['dataName'], prefix) and r.get('mapRegionName', '').startswith('map_'):
            out[r['mapRegionName'][4:]] = r['dataName']
    return out


def nations(prefix, root):
    return {n['dataName'] for n in load(root, 'TINationTemplate.json') if in_ns(n['dataName'], prefix)}


if __name__ == '__main__':
    for label, pre, root in SCENARIOS:
        rm, nat = region_map(pre, root), nations(pre, root)
        picks = {k: rm.get(k) for k in ('Quebec', 'LowerGuinea', 'Ghana', 'Egypt', 'PapuaNewGuinea')}
        print(f'{label:24s} regions {len(rm):3d} nations {len(nat):3d}  '
              f"GBR={pre + 'GBR' in nat} LBR={pre + 'LBR' in nat} CSA={pre + 'CSA' in nat} "
              f"SPR={pre + 'SPR' in nat}")
        print(f'    {picks}')
