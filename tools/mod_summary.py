"""Collect everything the artifact needs: per-project claim lists, per-scenario verified reach."""
import json
from pathlib import Path
import make_mod as M
from localize import KINDS, read
from names import SCENARIOS, region_map, nations
from scen import graph, reach

rows, per_scenario = M.build()
# Summarize emitted rows, including Broken Earth's starting claims and project upgrades.
by_scenario = {}
keys = set()
for label, pre, root in SCENARIOS:
    inverse = {v: k for k, v in region_map(pre, root).items()}
    groups = {}
    for row in rows:
        if row['region1'] not in inverse:
            continue
        key = (row.get('projectUnlockName', ''), row['nation1'][len(pre):])
        groups.setdefault(key, set()).add((inverse[row['region1']], bool(row.get('hostileClaim'))))
    by_scenario[pre] = groups
    keys.update(groups)
blocks = []
for project, nation in sorted(keys):
    sets = [groups.get((project, nation), set()) for groups in by_scenario.values()]
    common = set.intersection(*sets)
    other_sets = [groups.get((project, nation), set()) for pre, groups in by_scenario.items()
                  if pre != '1962_']
    others = set.intersection(*other_sets) - common
    assert all(entries == common | others for entries in other_sets)
    be = by_scenario['1962_'].get((project, nation), set()) - common
    for scope, entries in [('all', common), ('others', others), ('be', be)]:
        if entries:
            blocks.append({'project': project, 'nation': nation, 'scope': scope,
                           'regions': sorted(r for r, h in entries),
                           'hostile': sorted(r for r, h in entries if h), 'count': len(entries)})
assert sum(b['count'] * {'all': 5, 'others': 4, 'be': 1}[b['scope']] for b in blocks) == len(rows)

scen = []
for label, pre, root in SCENARIOS:
    g = graph(pre, root)
    nats = nations(pre, root)
    extra = [(r['nation1'], r['region1']) for r in rows if r['nation1'].startswith(pre)
             and (pre or not r['nation1'][:4].isdigit())]
    powers = {}
    for tag in ('GBR', 'SPR', 'CSA', 'RUS', 'BAV'):
        n = pre + tag
        if n not in nats:
            continue
        powers[tag] = {'before': len(reach(g, n)[0]), 'after': len(reach(g, n, extra)[0])}
    scen.append({'label': label, 'prefix': pre or '(untagged)', 'rows': per_scenario[label][0],
                 'powers': powers, 'regions': len(g['regions'])})

PROJECT_META = {
    M.SUN: {'cost': '25,000', 'gate': 'Commonwealth Restored + Great Nations', 'holder': 'United Kingdom',
            'scope': 'All five scenarios'},
    M.LIBERIA: {'cost': '20,000', 'gate': 'Greater Dominion + Great Nations', 'holder': 'Liberia',
                'scope': 'All five scenarios'},
    M.COMMONWEALTH: {'cost': '10,000 (vanilla, unchanged)', 'gate': 'unchanged',
                     'holder': 'United Kingdom', 'scope': 'All five scenarios'},
    M.DOMINION: {'cost': '10,000 (vanilla, unchanged)', 'gate': 'unchanged',
                 'holder': 'Dominion of America', 'scope': 'All five scenarios'},
}

# The claim projects this mod adds, from the files it ships.
ROOT = Path(__file__).resolve().parent.parent
claimed = {project for project, _, _, _ in M.BLOCKS}
projects = {p['dataName']: p for p in json.loads((ROOT / 'TIProjectTemplate.json').read_text(encoding='utf-8-sig'))
            if p['dataName'] in claimed}
text = read(ROOT / 'Localization/en/TIProjectTemplate.en')
loc = {p: [text[f'TIProjectTemplate.{kind}.{p}'] for kind in KINDS] for p in projects}

for project, theatre in M.ROMAN_THEATRES:
    PROJECT_META[project] = {'cost': f"{projects[project]['researchCost']:,}", 'gate': 'Great Nations',
                             'holder': 'Republic of Rome', 'scope': 'Broken Earth only',
                             'control_point_capacity': 10, 'theatre': theatre}

out = {'blocks': blocks, 'scenarios': scen, 'loc': loc, 'meta': PROJECT_META, 'total_rows': len(rows),
       'projects': projects}
json.dump(out, open('mod_summary.json', 'w', encoding='utf-8'), ensure_ascii=False)
print('rows', len(rows), '| scenarios', [(s['label'], s['rows']) for s in scen])
for s in scen:
    print(' ', s['label'], {k: f"{v['before']}->{v['after']}" for k, v in s['powers'].items()})
