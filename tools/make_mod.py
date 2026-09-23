"""Generate the Restored Empires mod.

Claims are authored once in map-layer region names and emitted per scenario, because every
scenario prefixes its template names (2026_, 2070_, 2003_, 1962_; the Modern start is untagged)
and rows from one namespace are inert in another. Rome exists only in Broken Earth, so its two
projects are emitted there alone; the British and Liberian lines exist everywhere.
"""
import json, os
from names import SCENARIOS, region_map, nations

OUT = r'C:\Users\alex\Documents\GitHub\RestoredEmpires'
SUN, MARE, FAR, LIBERIA = ('Project_TheSunNeverSets', 'Project_MareNostrum',
                           'Project_ImperiumSineFine', 'Project_NewLiberia')
COMMONWEALTH, DOMINION = 'Project_CommonwealthRestored', 'Project_GreaterDominion'   # vanilla

# (region, hostile). Hostile where a population fought its way out of the Empire; peaceful for
# dependencies that left by agreement, or never did.
# Delhi and Calcutta (Commonwealth members) and Ireland, which left but was never foreign.
RAJ = [('NewDelhi', 1), ('Kolkata', 1), ('Ireland', 1)]
EMPIRE = [
    ('Faridabad', 1), ('Mumbai', 1), ('Chennai', 1), ('Bangalore', 1), ('Hyderabad', 1),
    ('Gujarat', 1), ('Odisha', 1), ('AndhraPradesh', 1), ('Indore', 1), ('Punjab', 1),
    ('Sindh', 1), ('Bangladesh', 1), ('SriLanka', 1), ('Maldives', 0),
    ('MalayPeninsula', 1), ('Singapore', 0), ('SarawakandSabah', 0), ('Brunei', 0),
    ('Ghana', 1), ('SierraLeone', 1), ('Senegambia', 1), ('Lagos', 1), ('PortHarcourt', 1),
    ('WestCameroon', 1), ('Cameroon', 1), ('Kenya', 1), ('Uganda', 1), ('Tanzania', 1),
    ('WestTanzania', 1), ('RwandaBurundi', 1), ('Seychelles', 0), ('Mauritius', 0),
    ('Zambia', 1), ('Zimbabwe', 1), ('Malawi', 1), ('Botswana', 0), ('Namibia', 1),
    ('CapeTown', 1), ('Johannesburg', 1), ('Lesotho', 0), ('Eswatini', 0), ('Mozambique', 1),
    ('NorthMozambique', 1), ('Jamaica', 0), ('Bahamas', 0), ('Guyana', 0), ('PapuaNewGuinea', 0),
    ('Dixie', 1), ('Shenandoah', 1), ('Cumberland', 1), ('MississippiDelta', 1),
]
MEDITERRANEAN = [
    ('Naples', 0), ('Milan', 0), ('Spain', 1), ('Barcelona', 1), ('LeonGalicia', 1),
    ('Portugal', 1), ('Paris', 1), ('Lyon', 1), ('Toulouse', 1), ('Nantes', 1),
    ('Switzerland', 1), ('Austria', 1), ('Slovenia', 1), ('CroatiaSlovenia', 1),
    ('BosniaMontenegro', 1), ('Albania', 1), ('Greece', 1), ('NorthMacedonia', 1), ('Kosovo', 1),
    ('Belgrade', 1), ('Bulgaria', 1), ('Romania', 1), ('Transylvania', 1), ('Hungary', 1),
    ('Tunisia', 1), ('Algeria', 1), ('Libya', 1), ('Cyrenaica', 1), ('Morocco', 1), ('Egypt', 1),
    ('Alexandria', 1), ('Luxor', 1), ('Anatolia', 1), ('Istanbul', 1), ('Syria', 1),
    ('Lebanon', 1), ('Israel', 1), ('Palestine', 1), ('Jordan', 1), ('Belgium', 1),
    ('Netherlands', 1), ('Germany', 1),
]
BEYOND = [
    ('England', 1), ('EnglishMidlands', 1), ('Wales', 1), ('Scotland', 1), ('Crimea', 1),
    ('Armenia', 1), ('Georgia', 1), ('Baghdad', 1), ('Mosul', 1), ('Diyarbakir', 1),
    ('Sudan', 1), ('NorthernSudan', 1), ('Ethiopia', 1),
    ('Halifax', 1), ('Quebec', 1), ('NewEngland', 1), ('NewYork', 1), ('Shenandoah', 1),
    ('Cumberland', 1), ('Dixie', 1), ('MississippiDelta', 1), ('Bahamas', 1), ('Cuba', 1),
    ('WestHispanola', 1), ('EastHispanola', 1), ('Jamaica', 1), ('AmericanCaribbean', 1),
    ('WestIndies', 1), ('FrenchWestIndies', 1), ('BritishWestIndies', 1), ('DutchCaribbean', 1),
    ('Guyana', 1), ('Suriname', 1), ('FrenchGuiana', 1), ('Belem', 1), ('Fortaleza', 1),
    ('Recife', 1), ('RiodeJaneiro', 1), ('SaoPaulo', 1), ('PortoAlegre', 1), ('Uruguay', 1),
    ('Pampas', 1), ('Patagonia', 1),
]
FREEDMEN = [('SierraLeone', 0), ('IvoryCoast', 0), ('Ghana', 0), ('TogoBenin', 0), ('LowerGuinea', 0)]

# (project, nation, claims, scope). 'all' = every scenario, 'be' = Broken Earth only,
# 'others' = every scenario except Broken Earth.
BLOCKS = [
    (COMMONWEALTH, 'GBR', RAJ, 'all'),
    (SUN, 'GBR', EMPIRE, 'all'),
    (MARE, 'SPR', MEDITERRANEAN, 'be'),
    (FAR, 'SPR', BEYOND, 'be'),
    (LIBERIA, 'LBR', FREEDMEN, 'all'),
    (DOMINION, 'CSA', [('Liberia', 0)], 'all'),
    ('', 'RNA', [('Jamaica', 0)], 'be'),        # ungated: in Broken Earth nothing else claims Kingston
]

PROJECTS = [
    {'friendlyName': 'The Sun Never Sets', 'dataName': SUN, 'techCategory': 'SocialScience',
     'AI_techRole': 'EarthPolitics', 'AI_projectRole': 'ExpandNation', 'researchCost': 25000,
     'prereqs': [COMMONWEALTH, 'GreatNations'], 'oneTimeGlobally': True, 'repeatable': False,
     'requiresNation': 'GBR', 'factionAvailableChance': 100, 'initialUnlockChance': 0,
     'deltaUnlockChance': 5, 'maxUnlockChance': 50, 'resourcesGranted': []},
    {'friendlyName': 'Mare Nostrum', 'dataName': MARE, 'techCategory': 'SocialScience',
     'AI_techRole': 'EarthPolitics', 'AI_projectRole': 'ExpandNation', 'researchCost': 10000,
     'prereqs': ['UnityMovements'], 'oneTimeGlobally': True, 'repeatable': False,
     'requiresNation': 'SPR', 'factionAvailableChance': 100, 'initialUnlockChance': 0,
     'deltaUnlockChance': 5, 'maxUnlockChance': 50, 'resourcesGranted': []},
    {'friendlyName': 'Imperium Sine Fine', 'dataName': FAR, 'techCategory': 'SocialScience',
     'AI_techRole': 'EarthPolitics', 'AI_projectRole': 'ExpandNation', 'researchCost': 25000,
     'prereqs': [MARE, 'GreatNations'], 'oneTimeGlobally': True, 'repeatable': False,
     'requiresNation': 'SPR', 'factionAvailableChance': 100, 'initialUnlockChance': 0,
     'deltaUnlockChance': 5, 'maxUnlockChance': 50, 'resourcesGranted': []},
    {'friendlyName': 'New Liberia', 'dataName': LIBERIA, 'techCategory': 'SocialScience',
     'AI_techRole': 'EarthPolitics', 'AI_projectRole': 'ExpandNation', 'researchCost': 20000,
     'prereqs': [DOMINION, 'GreatNations'], 'oneTimeGlobally': True, 'repeatable': False,
     'requiresNation': 'LBR', 'factionAvailableChance': 100, 'initialUnlockChance': 0,
     'deltaUnlockChance': 5, 'maxUnlockChance': 50, 'resourcesGranted': []},
]

LOC = {
    SUN: ('The Sun Never Sets',
          'We reassert the claims of the whole Empire, not merely the settler dominions.',
          'Commonwealth Restored brought back the easy half: the dominions that never really left, '
          'whose flags still carry ours in the corner. The rest of the map was always the point. '
          'Delhi and Dhaka, Lagos and Nairobi, the Cape and the Straits, the sugar islands, and the '
          'thirteen colonies that got away - all of it administered once from a single square mile '
          'of London. We are not asking for it back. We are filing the paperwork that says it never '
          'stopped being ours, and letting the rest follow.'),
    MARE: ('Mare Nostrum',
           'The Republic reasserts its authority over the Mediterranean world.',
           'Every road still runs to this city, whatever the milestones have been repainted to say. '
           'The sea between the provinces is not a border and never was; it is the courtyard of a '
           'single house whose tenants have grown forgetful. Hispania, Gaul, Africa, Achaea, Asia, '
           'Aegyptus, Syria - the names survive on our maps because nobody has ever invented better '
           'ones. We begin with the sea, because the sea is ours.'),
    FAR: ('Imperium Sine Fine',
          'Britannia, the eastern roads, Aksum - and the far shore of the Ocean.',
          'Virgil promised an empire without end, and the surveyors took him literally. Britannia at '
          'the cold edge, Armenia and Mesopotamia where the legions marched and mostly regretted it, '
          'Nubia up the river, and Aksum at the far end of the Red Sea run, where our coins turn up '
          'in the dirt because our merchants got there first. And then the Ocean, which the ancients '
          'took for the edge of the world and which turns out to be merely wide. There is land on '
          'the other side of it, currently administered by people who arrived there fifteen '
          'centuries after we stopped looking. An empire without end does not recognise an ocean as '
          'an end. It recognises an unsurveyed province.'),
    LIBERIA: ('New Liberia',
              'Monrovia, Freetown and Libreville: one coast, one founding idea.',
              'Three powers had the same notion within forty years of each other, and none of them '
              'thought to compare notes. The Americans put freed slaves ashore at Monrovia, the '
              'British at Freetown, the French at Libreville - the free town, in case the point was '
              'missed. Between them lies the Kru coast, whose sailors crewed the ships that carried '
              'all three. The descendants of those settlements have more in common with each other '
              'than with the empires that dropped them here, and they have started saying so.'),
}


def build():
    rows, per_scenario = [], {}
    for label, prefix, root in SCENARIOS:
        rm, nats = region_map(prefix, root), nations(prefix, root)
        be = prefix == '1962_'
        made, skipped = 0, []
        for project, nation, claims, scope in BLOCKS:
            if (scope == 'be' and not be) or (scope == 'others' and be):
                continue
            if prefix + nation not in nats:
                skipped.append(nation)
                continue
            for region, hostile in claims:
                if region not in rm:              # scenario lacks the region: skip, never guess
                    skipped.append(region)
                    continue
                tag = project.replace('Project_', '') if project else 'Irredenta'
                row = {'dataName': f'Claim{prefix}{nation}_{region}_{tag}', 'relationType': 'Claim',
                       'nation1': prefix + nation, 'region1': rm[region]}
                if project:
                    row['projectUnlockName'] = project
                if hostile:
                    row['hostileClaim'] = True
                rows.append(row)
                made += 1
        per_scenario[label] = (made, sorted(set(skipped)))
    return rows, per_scenario


if __name__ == '__main__':
    rows, per_scenario = build()
    modinfo = {
        'title': 'Restored Empires',
        'author': 'alex',
        'description': (
            'Old powers get a route back to the whole map, each by a different road. The Sun Never '
            'Sets (United Kingdom) claims the Empire and Commonwealth beyond the settler dominions; '
            'Delhi and Calcutta join vanilla Commonwealth Restored. New Liberia (Liberia) unites '
            'Monrovia, Freetown and Libreville, and Monrovia joins vanilla Greater Dominion, so the '
            'Dominion of America reaches Africa by releasing Liberia and taking it back. Commonwealth '
            'Restored also gains Delhi, Calcutta and Ireland. In Broken Earth only, Mare Nostrum and '
            'Imperium Sine Fine give Rome the Mediterranean world, the eastern roads, Aksum and both '
            'American seaboards, and the Civil Defense Administration claims Jamaica - which nothing '
            'else in that scenario claims. Claims on territories that fought their way out are '
            'hostile; the small dependencies are peaceful. Covers all five starts.')
    }
    os.makedirs(os.path.join(OUT, 'Localization', 'en'), exist_ok=True)
    w = lambda p, s: open(os.path.join(OUT, p), 'w', encoding='utf-8', newline='\r\n').write(s)
    w('ModInfo.json', json.dumps(modinfo, indent=2))
    w('TIProjectTemplate.json', json.dumps(PROJECTS, indent=2))
    w('TIBilateralTemplate.json', json.dumps(rows, indent=2))
    w(os.path.join('Localization', 'en', 'TIProjectTemplate.en'), '\n'.join(
        f'TIProjectTemplate.{k}.{d}={v}' for d, (n, s, desc) in LOC.items()
        for k, v in (('displayName', n), ('summary', s), ('description', desc))) + '\n')

    print(f'{len(rows)} claim rows -> {OUT}')
    for label, (n, skipped) in per_scenario.items():
        print(f'  {label:24s} {n:3d} rows' + (f'   skipped: {skipped}' if skipped else ''))
