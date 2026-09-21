import html, json, re

S = json.load(open('mod_summary.json', encoding='utf-8'))
esc = html.escape
spaced = lambda s: re.sub(r'(?<=[a-z])(?=[A-Z])', ' ', s)
LOC, META = S['loc'], S['meta']

ORDER = [
    ('Project_CommonwealthRestored', 'GBR', 'Commonwealth Restored',
     'vanilla, extended', 'Delhi and Calcutta, the two seats of the Raj, and Ireland. India, '
     'Pakistan, Bangladesh and Sri Lanka are Commonwealth members, so they belong at this tier '
     'rather than behind the imperial one - and it is what lets another power inherit the '
     'subcontinent from a Britain that has only done the cheap project. Ireland sits here too, '
     'gated rather than granted at the start.'),
    ('Project_TheSunNeverSets', 'GBR', 'The Sun Never Sets', 'new', None),
    ('Project_MareNostrum', 'SPR', 'Mare Nostrum', 'new', None),
    ('Project_ImperiumSineFine', 'SPR', 'Imperium Sine Fine', 'new', None),
    ('Project_GreaterDominion', 'CSA', 'Greater Dominion', 'vanilla, extended',
     'Monrovia joins the Dominion\'s claims: the one colony the Republic actually founded, and the '
     'hinge the whole American route turns on.'),
    ('Project_NewLiberia', 'LBR', 'New Liberia', 'new', None),
]
BLOCK = {(b['project'], b['nation']): b for b in S['blocks']}


def project(pid, nation, title, kind, prose):
    keys = [k for k in BLOCK if k[0] == pid]
    regions = sorted({r for k in keys for r in BLOCK[k]['regions']})
    hostile = {r for k in keys for r in BLOCK[k]['hostile']}
    # a claim carried by an 'others'-scoped block is absent from Broken Earth, where it is
    # handled differently - mark it rather than letting the entry imply all five scenarios
    conditional = {r for k in keys if BLOCK[k]['scope'] == 'others' for r in BLOCK[k]['regions']}
    meta = META.get(pid, {})
    loc = LOC.get(pid)
    body = f'<p class="summary">{esc(loc[1])}</p><blockquote>{esc(loc[2])}</blockquote>' if loc \
        else f'<p class="summary">{prose}</p>'
    chips = ''.join(
        f'<li class="rg{" rg--h" if r in hostile else ""}{" rg--cond" if r in conditional else ""}">'
        f'{esc(spaced(r))}{"<sup>†</sup>" if r in conditional else ""}</li>' for r in regions)
    if conditional:
        chips += ''
    caption = ('<p class="chipnote">† ' + ', '.join(sorted(conditional)) +
               ' is claimed here in four starts. In Broken Earth nothing else claims it, so the mod '
               'grants it ungated from turn one instead - see below.</p>') if conditional else ''
    proj = S['projects'].get(pid)
    prereq = ' + '.join(p.replace('Project_', '').replace('_', ' ') for p in proj['prereqs']) \
        if proj else meta.get('gate', '')
    return f"""<article class="entry">
  <header>
    <div class="titleline"><h3>{esc(title)}</h3><span class="kind kind--{'new' if kind == 'new' else 'ext'}">{kind}</span></div>
    <dl class="meta">
      <div><dt>Holder</dt><dd>{esc(meta.get('holder', ''))}</dd></div>
      <div><dt>Cost</dt><dd>{esc(meta.get('cost', ''))}</dd></div>
      <div><dt>Gate</dt><dd>{esc(prereq or 'unchanged')}</dd></div>
      <div><dt>Claims</dt><dd><b>{len(regions)}</b> · {len(hostile)} hostile</dd></div>
      <div><dt>Scenarios</dt><dd>{esc(meta.get('scope', ''))}{' *' if conditional else ''}</dd></div>
    </dl>
  </header>
  {body}
  <ul class="regions">{chips}</ul>
  {caption}
</article>"""


entries = '\n'.join(project(*o) for o in ORDER)

srows = ''
for s in S['scenarios']:
    p = s['powers']
    cell = lambda t: (f"<td class='num'>{p[t]['before']} → <b>{p[t]['after']}</b></td>"
                      if t in p else "<td class='num none'>—</td>")
    srows += (f"<tr><td>{esc(s['label'])}<span class='ns mono'>{esc(s['prefix'])}</span></td>"
              f"<td class='num'>{s['rows']}</td>{cell('GBR')}{cell('CSA')}{cell('SPR')}{cell('RUS')}</tr>")

CSS = open('mod_page.css', encoding='utf-8').read()
page = f"""<title>Restored Empires</title>
<link rel="preconnect" href="https://fonts.googleapis.com">
<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
<link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=Spectral:wght@400;600&family=Source+Sans+3:wght@400;600&family=JetBrains+Mono:wght@400;500&display=swap">
<style>{CSS}</style>
<div class="wrap">
<header class="head">
  <p class="eyebrow">Terra Invicta · claim mod · all five starts</p>
  <h1>Restored Empires</h1>
  <p class="lede">Four new projects and two vanilla ones extended, giving old powers a route back to
  the map by claim - each by a different road, and none of them duplicating another's work.
  {S['total_rows']} claim rows across every scenario.</p>
</header>

<section>
  <h2>The problem</h2>
  <p>A nation can only absorb another by claiming its <b>capital</b>. Where no claim points, expansion
  stops - and the claim web has holes.</p>
  <ul class="findings">
    <li><b>Broken Earth is nearly closed.</b> Of 297 nations, <b>none</b> can reach every region by
    claim. The lone exception is the Protectorate Authority, which claims all 363 at once - a
    different game, not a chain.</li>
    <li><b>Ireland and Jamaica are unclaimable there.</b> Ireland is claimed only by Ireland, Jamaica
    only by Jamaica. The 2026 start has the European Union claiming Ireland and the USA claiming
    Jamaica; Broken Earth deleted both parents and nothing inherited the claims. Commonwealth
    Restored now carries Ireland, and the Civil Defense Administration carries Kingston.</li>
    <li><b>Commonwealth Restored is the settler dominions only</b> - Canada, Australia, New Zealand,
    Hong Kong, Belize. Every other Commonwealth member is missing, which is most of what Britain
    actually administered.</li>
  </ul>
</section>

<section>
  <h2>The projects</h2>
  <p>Region tags below are <span class="rg rg--h inline">hostile</span> where a population fought its
  way out, and <span class="rg inline">peaceful</span> for dependencies that left by agreement or
  never did. Hostile claims need the long loop; peaceful ones can be merged diplomatically.</p>
  {entries}
  <article class="entry entry--plain">
    <header><div class="titleline"><h3>One ungated claim</h3><span class="kind kind--fix">repair</span></div>
      <dl class="meta">
        <div><dt>Live from</dt><dd>turn one</dd></div>
        <div><dt>Scenarios</dt><dd>Broken Earth only</dd></div>
      </dl>
    </header>
    <p class="summary">The Civil Defense Administration holds a peaceful claim on <b>Jamaica</b>, as
    Sons of Terra has it. Nothing else in Broken Earth claims Kingston, so without this row the
    island sits outside every chain on Earth. Ireland is the same problem handled differently: it
    rides on Commonwealth Restored, so Britain earns it rather than starting with it.</p>
  </article>
</section>

<section>
  <h2>How each power gets there</h2>
  <div class="routes">
    <div class="route"><h3>Britain</h3><p>Self-sufficient in Broken Earth. The Empire touched every
    isolated cluster on the map - Africa, India, the Caribbean, the American South - so its claims
    reach all of them directly.</p></div>
    <div class="route"><h3>Rome</h3><p>Aksum opens Africa, the Atlantic province opens the Americas.
    India and Ireland it does <em>not</em> claim: those arrive by absorbing Britannia and inheriting
    what London claims. 352 alone, 363 once Britain has done the cheap 10,000 project.</p></div>
    <div class="route"><h3>America</h3><p>Claims Monrovia, releases Liberia, lets it grow along the
    freedmen's coast to Libreville - where the African web opens - then takes it back. The Raj and
    Ireland come later, through Britain.</p></div>
  </div>
</section>

<section>
  <h2>Verified reach, by scenario</h2>
  <p>Regions reachable by claim, before and after, computed against each scenario's own templates.</p>
  <div class="tablewrap"><table>
    <thead><tr><th>Scenario</th><th class="num">Rows</th><th class="num">Britain</th>
      <th class="num">America</th><th class="num">Rome</th><th class="num">Russia</th></tr></thead>
    <tbody>{srows}</tbody>
  </table></div>
  <p class="foot"><b>The payoff is not the same everywhere.</b> Broken Earth's claim web is dense -
  nations there claim each other's capitals constantly - so one doorway cascades across the map. The
  intact starts are sparse: Gabon's chain reaches <b>one</b> region in 2026 against 346 in Broken
  Earth, so New Liberia opens a coast rather than a continent, and the Dominion gains six regions
  rather than the world. Britain still roughly quintuples its reach, 37 to 198, because the Empire's
  claims are direct rather than inherited. Closing the intact starts to 363 would need its own
  analysis and more claims - it is not a port.</p>
</section>

<section>
  <h2>Mechanics it relies on</h2>
  <dl class="mech">
    <div><dt>Capital claims</dt><dd>A claim on a region that is some nation's original capital is what
    lets you absorb that nation, and with it every claim it holds. That is the edge the graph runs
    on, and it is set by a claim row carrying <span class="mono">capitalClaim</span>.</dd></div>
    <div><dt>Release and regrow</dt><dd>Hold a dormant nation's original capital and you can release
    it, let it expand into its own claims, then absorb it again. New Liberia exists to be released:
    the claims sit on Liberia, not on America.</dd></div>
    <div><dt>Hostile claims</dt><dd>They need conquest, then a government push until the claim turns
    peaceful, then release, regrow and reabsorb. Peaceful claims are a diplomatic merger.</dd></div>
    <div><dt>Scenario namespaces</dt><dd>Every scenario prefixes its template names and rows from one
    namespace are inert in another, so each claim is emitted five times -
    <span class="mono">1962_Libreville</span>, <span class="mono">2026_Libreville</span>, and so on.
    Projects are global and need no duplication.</dd></div>
  </dl>
</section>

<section>
  <h2>Files</h2>
  <ul class="files">
    <li><span class="mono">TIProjectTemplate.json</span> — four new projects</li>
    <li><span class="mono">TIBilateralTemplate.json</span> — {S['total_rows']} claim rows across five scenarios</li>
    <li><span class="mono">Localization/en/TIProjectTemplate.en</span> — names, summaries, descriptions</li>
    <li><span class="mono">ModInfo.json</span></li>
  </ul>
  <p class="warn"><b>Not tested in game.</b> Every row is checked against the scenario templates and
  every route recomputed from them, but the mod has not been loaded, and no research screen, merger
  or release has been seen working.</p>
</section>
</div>"""

open('restored_empires.html', 'w', encoding='utf-8').write(page)
print('bytes', len(page))
