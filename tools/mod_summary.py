"""Collect everything the artifact needs: per-project claim lists, per-scenario verified reach."""
import json
import make_mod as M
from names import SCENARIOS, region_map, nations
from scen import graph, reach

rows, per_scenario = M.build()
blocks = []
for project, nation, claims, scope in M.BLOCKS:
    blocks.append({
        'project': project, 'nation': nation, 'scope': scope,
        'regions': [r for r, _ in claims],
        'hostile': [r for r, h in claims if h],
        'count': len(claims),
    })

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
    M.MARE: {'cost': '10,000', 'gate': 'Unity Movements', 'holder': 'Republic of Rome',
             'scope': 'Broken Earth only'},
    M.FAR: {'cost': '25,000', 'gate': 'Mare Nostrum + Great Nations', 'holder': 'Republic of Rome',
            'scope': 'Broken Earth only'},
    M.LIBERIA: {'cost': '20,000', 'gate': 'Greater Dominion + Great Nations', 'holder': 'Liberia',
                'scope': 'All five scenarios'},
    M.COMMONWEALTH: {'cost': '10,000 (vanilla, unchanged)', 'gate': 'unchanged',
                     'holder': 'United Kingdom', 'scope': 'All five scenarios'},
    M.DOMINION: {'cost': '10,000 (vanilla, unchanged)', 'gate': 'unchanged',
                 'holder': 'Dominion of America', 'scope': 'All five scenarios'},
}

out = {'blocks': blocks, 'scenarios': scen, 'loc': {k: list(v) for k, v in M.LOC.items()},
       'meta': PROJECT_META, 'total_rows': len(rows),
       'projects': {p['dataName']: p for p in M.PROJECTS}}
json.dump(out, open('mod_summary.json', 'w', encoding='utf-8'), ensure_ascii=False)
print('rows', len(rows), '| scenarios', [(s['label'], s['rows']) for s in scen])
for s in scen:
    print(' ', s['label'], {k: f"{v['before']}->{v['after']}" for k, v in s['powers'].items()})
