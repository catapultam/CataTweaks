"""Regenerate CataTweaks claim rows.

Claims are authored once in map-layer region names and emitted per scenario, because every
scenario prefixes its template names (2026_, 2070_, 2003_, 1962_; the Modern start is untagged)
and rows from one namespace are inert in another. Rome exists only in Broken Earth, so its six regional
projects are emitted there alone; the British and Liberian lines exist everywhere.
"""
import json
from pathlib import Path
from names import SCENARIOS, region_map, nations

OUT = Path(__file__).resolve().parents[1] / "TIBilateralTemplate.json"
SUN, MARE, FAR, LIBERIA = ('Project_TheSunNeverSets', 'Project_MareNostrum',
                           'Project_ImperiumSineFine', 'Project_NewLiberia')
COMMONWEALTH, DOMINION = 'Project_CommonwealthRestored', 'Project_GreaterDominion'   # vanilla

# (region, hostile). Hostile for contested foreign territories and former colonies that fought
# for independence; peaceful for British dependencies that left by agreement, or never did.
# Delhi and Calcutta (Commonwealth members) and Ireland, which left but was never foreign.
RAJ = [('NewDelhi', 1), ('Kolkata', 1), ('Ireland', 1)]
# The map combines several historical colonies into each region. Include the eastern seaboard
# and its inland colonial hinterland, but not Louisiana/the Mississippi Delta or western NA.
THIRTEEN_COLONIES = [
    ('NewEngland', 1), ('NewYork', 1), ('Shenandoah', 1), ('Cumberland', 1), ('Dixie', 1),
]
CARIBBEAN = [
    ('Bahamas', 0), ('Jamaica', 0), ('BritishWestIndies', 0),
    ('Cuba', 1), ('WestHispanola', 1), ('EastHispanola', 1), ('AmericanCaribbean', 1),
    ('WestIndies', 1), ('FrenchWestIndies', 1), ('DutchCaribbean', 1),
]
# British India including princely states and Burma before its administrative separation in
# 1937. Delhi and Calcutta remain on the prerequisite Commonwealth Restored project.
BRITISH_INDIA = [
    ('Faridabad', 1), ('Mumbai', 1), ('Chennai', 1), ('Bangalore', 1), ('Hyderabad', 1),
    ('Gujarat', 1), ('Odisha', 1), ('AndhraPradesh', 1), ('Indore', 1), ('Jaipur', 1),
    ('Raipur', 1), ('Bihar', 1), ('Assam', 1), ('ArunchalPradesh', 1), ('JammuandKashmir', 1),
    ('Punjab', 1), ('Sindh', 1), ('Baluchistan', 1), ('Peshawar', 1), ('Bangladesh', 1),
    ('Myanmar', 1), ('UpperMyanmar', 1),
]
# Alternate-history dynastic claims: the selected continental crowns, rather than every
# country with a remote royal ancestor. Russia is limited to its European core on the map.
# No overseas French/Dutch possessions, Spanish islands, or Falklands mainland proxy.
DYNASTIC_EUROPE = [
    ('Paris', 1), ('Nantes', 1), ('Lyon', 1), ('Toulouse', 1),
    ('Germany', 1), ('EastGermany', 1), ('Bavaria', 1),
    ('Spain', 1), ('Barcelona', 1), ('LeonGalicia', 1), ('BasqueCountry', 1), ('Portugal', 1),
    ('Belgium', 1), ('Netherlands', 1), ('Denmark', 1), ('Norway', 1), ('Sweden', 1),
    ('Greece', 1), ('Romania', 1), ('Transylvania', 1),
    ('Moskva', 1), ('Novgorod', 1), ('Karelia', 1), ('Arkhangelsk', 1), ('Kirov', 1),
    ('Volga', 1), ('Voronezh', 1), ('Rostovskaya', 1), ('NorthCaucasus', 1), ('Kaliningrad', 1),
]
EMPIRE = THIRTEEN_COLONIES + CARIBBEAN + BRITISH_INDIA + DYNASTIC_EUROPE

# Six independent, broad conquest branches. Preserve the two original project IDs for saved
# completions, while repurposing them for Europe and Asia. All regions are assigned once.
ROME_COST = 35000
ROME_CAP_EFFECT = 'Effect_ControlPointMaintenanceBonus10'
ROME_REGIONS = {
    'Europe': '''
        Albania Arkhangelsk Austria Barcelona BasqueCountry Bavaria Belarus Belgium Belgrade
        BosniaMontenegro Bulgaria Crimea CroatiaSlovenia Czechia Denmark Donetsk EastGermany Estonia
        Finland Gdansk Germany Greece Hungary Istanbul Italy Kaliningrad Karelia Katowice Kharkiv
        Kiev Kirov Kosovo Latvia LeonGalicia Lithuania Lyon Milan Moldova Montenegro Moskva Nantes
        Naples Netherlands NorthCaucasus NorthMacedonia Norway Novgorod Odesa Paris Poland Portugal
        Romania Rostovskaya Slovakia Slovenia Spain Sweden Switzerland Toulouse Transylvania Volga
        Voronezh
        '''.split(),
    'Africa': '''
        Alexandria Algeria Angola Botswana BurkinaFaso CaboVerde Cameroon CanaryIslands CapeTown
        CentralAfricanRepublic Chad CongoBrazzaville Cyrenaica Darfur DireDiwa Djibouti EastAngola
        Egypt EquatorialGuinea Eritrea Eswatini Ethiopia FrenchRatnakara Gambia Gao Ghana Guinea
        GuineaBissau IvoryCoast Johannesburg Kano Kenya Kinshasa Kisangani Lagos Lesotho Liberia
        Libya Logone LowerGuinea Lubumbashi Luxor Madagascar Malawi Mali Mauritania Mauritius
        Mekelle Mombasa Morocco Mozambique Namibia Niger NorthMozambique NorthernChad NorthernSudan
        PortHarcourt Ratnakara RwandaBurundi SaoTomePrincipe Senegambia Seychelles SierraLeone
        Somalia Somaliland SouthAlgeria SouthSudan Sudan Tanzania Togo TogoBenin Tunisia Uganda
        WestCameroon WestTanzania WesternSahara Zambia Zanzibar Zimbabwe
        '''.split(),
    'India': '''
        AndhraPradesh ArunchalPradesh Assam Baluchistan Bangalore Bangladesh Bhutan Bihar Chennai
        Faridabad Gujarat Himalayas Hyderabad Indore Jaipur JammuandKashmir Kolkata Maldives Mumbai
        NewDelhi Odisha Peshawar Punjab Raipur Sindh SriLanka
        '''.split(),
    'NorthAmerica': '''
        Alaska Alberta AmericanCaribbean Bahamas Belize Boise BritishColumbia BritishWestIndies
        California CanadianArctic Cascadia CostaRica Cuba Cumberland Dakota Dixie DutchCaribbean
        EastHispanola ElSalvador FrenchWestIndies Greenland Guatemala Halifax HighPlains Honduras
        Jamaica Manitoba Merida MexicoCity MississippiDelta Monterrey NewEngland NewYork Nicaragua
        Ontario Panama Prairies Quebec RockyMountains RustBelt Shenandoah Texas Tijuana
        WestHispanola WestIndies Yukon
        '''.split(),
    'SouthAmerica': '''
        Amazonia Arequipa Belem Bolivia Brasilia Colombia Cuiaba Ecuador Fortaleza FrenchGuiana
        GranChaco Guyana NorthernChile Pampas Paraguay Patagonia Peru PortoAlegre Recife
        RiodeJaneiro SaoPaulo SouthernChile Suriname Uruguay Venezuela Villavicencio
        '''.split(),
    'Asia': '''
        Aceh Aden Afghanistan Ahvaz Almaty Anatolia Anhui Armenia Astana Azerbaijan Baghdad Beijing
        Borneo Brunei Cambodia Chongqing Diyarbakir EastTimor Fujian Gansu Georgia Guangzhou Guizhou
        GulfStates Hangzhou Hanoi Hijaz HoChiMinhCity Hokkaido HongKong Hunan InnerMongolia Irkutsk
        Isfahan Israel Java Jilin Jordan Kamchatka Karakalpakstan Kuwait Kyrgyzstan KyushuandShikoku
        Laos Lebanon LesserSundas Liaoning Luzon MalayPeninsula Manchuria Mashhad
        MoluccasandSulawesi Mongolia Mosul Myanmar Najd Nanchang Nanning NorthHonshu NorthKorea
        NorthKrasnoyarsk NorthThailand NorthUrals Novosibirsk Oman Palestine Qassimah Qinghai
        RussianFarEast Ryukus Sakha SakhalinKurils SarawakandSabah Shandong Shanghai Shanxi
        Singapore SouthHonshu SouthKorea SouthThailand Sulawesi Sumatra Syria Tabriz Taiwan
        Tajikistan Tehran Tibet Turkmenistan UnitedArabEmirates UpperMyanmar Urals Uzbekistan
        ViasyasyMindanao Visayas Wuhan Xian Xinjiang Yemen Yunnan Zhengzhou
        '''.split(),
}
ROMAN_THEATRES = [
    ('Project_MareNostrum', 'All Roads Lead to Rome', 'Europe'),
    ('Project_CataTweaks_RomanAfrica', 'Scipio’s Legacy', 'Africa'),
    ('Project_CataTweaks_RomanIndia', 'Beyond Alexander', 'India'),
    ('Project_CataTweaks_RomanNorthAmerica', 'Beyond the Pillars', 'NorthAmerica'),
    ('Project_CataTweaks_RomanSouthAmerica', 'Under Southern Stars', 'SouthAmerica'),
    ('Project_ImperiumSineFine', 'Empire Without End', 'Asia'),
]
# Continental Europe excludes the British Isles, Iceland and the Azores. Oceania is outside
# these six regions; North America includes Central America, Greenland and the Caribbean.
ROMAN_UNCLAIMED_REGIONS = '''
    AmericanPacific Azores England EnglishMidlands FrenchPacific Iceland Ireland Melanesia
    Micronesia NewGuinea NewSouthWales NewZealand NorthernAustralia NorthernIreland
    PapuaNewGuinea Polynesia Queensland Scotland SouthernAustralia TeWaipounamu Victoria Wales
    Westralia
    '''.split()

FREEDMEN = [('SierraLeone', 0), ('IvoryCoast', 0), ('Ghana', 0), ('TogoBenin', 0), ('LowerGuinea', 0)]

# (project, nation, claims, scope). 'all' = every scenario, 'be' = Broken Earth only,
# 'others' = every scenario except Broken Earth.
BLOCKS = [
    (COMMONWEALTH, 'GBR', RAJ, 'all'),
    (SUN, 'GBR', EMPIRE, 'all'),
    *((project, 'SPR', [(r, 0 if r in {'Italy', 'Milan', 'Naples'} else 1)
                        for r in ROME_REGIONS[theatre]], 'be')
      for project, title, theatre in ROMAN_THEATRES),
    (LIBERIA, 'LBR', FREEDMEN, 'all'),
    (DOMINION, 'CSA', [('Liberia', 0), ('Jamaica', 0)], 'all'),
    ('', 'RNA', [('Jamaica', 0)], 'be'),        # ungated: in Broken Earth nothing else claims Kingston
]

# Hostile starting claims in Broken Earth, with projects that subsequently make them peaceful.
# Vanilla SetClaim(region, false) removes hostility even when the nation already owns the claim.
BROKEN_EARTH_START_CLAIMS = {
    ('GBR', 'Jamaica'): SUN,
    ('CSA', 'Jamaica'): DOMINION,
    ('GBR', 'Ireland'): SUN,
}

PROJECTS = [
    {'friendlyName': 'The Sun Never Sets', 'dataName': SUN, 'techCategory': 'SocialScience',
     'AI_techRole': 'EarthPolitics', 'AI_projectRole': 'ExpandNation', 'researchCost': 25000,
     'prereqs': [COMMONWEALTH, 'GreatNations'], 'oneTimeGlobally': True, 'repeatable': False,
     'requiresNation': 'GBR', 'factionAvailableChance': 100, 'initialUnlockChance': 0,
     'deltaUnlockChance': 5, 'maxUnlockChance': 50, 'resourcesGranted': []},
    *({'friendlyName': title, 'dataName': project, 'techCategory': 'SocialScience',
       'AI_techRole': 'EarthPolitics', 'AI_projectRole': 'ExpandNation', 'researchCost': ROME_COST,
       'prereqs': ['GreatNations'], 'effects': [ROME_CAP_EFFECT],
       'oneTimeGlobally': True, 'repeatable': False, 'requiresNation': 'SPR',
       'factionAvailableChance': 100, 'initialUnlockChance': 0, 'deltaUnlockChance': 5,
       'maxUnlockChance': 50, 'resourcesGranted': []}
      for project, title, theatre in ROMAN_THEATRES),
    {'friendlyName': 'New Liberia', 'dataName': LIBERIA, 'techCategory': 'SocialScience',
     'AI_techRole': 'EarthPolitics', 'AI_projectRole': 'ExpandNation', 'researchCost': 20000,
     'prereqs': [DOMINION, 'GreatNations'], 'oneTimeGlobally': True, 'repeatable': False,
     'requiresNation': 'LBR', 'factionAvailableChance': 100, 'initialUnlockChance': 0,
     'deltaUnlockChance': 5, 'maxUnlockChance': 50, 'resourcesGranted': []},
]

LOC = {
    SUN: ('The Sun Never Sets',
          'Reassert claims on the thirteen colonies, the Caribbean, British India and selected European crowns.',
          'Commonwealth Restored brought back the settler dominions. Now we turn to the thirteen '
          'colonies, the Caribbean islands and British India, including Burma. In Europe, the '
          'Crown invokes its dynastic connections: France, Germany, Spain, Portugal, the Low '
          'Countries, Scandinavia, Greece, Romania and European Russia. The claim ends at these '
          'shores and crowns; it is not a mandate for the rest of the former Empire. We file '
          'the paperwork, and let the rest follow. In Broken Earth, completing this project also '
          'makes our starting claims on Jamaica and Ireland peaceful.'),
    LIBERIA: ('New Liberia',
              'Monrovia, Freetown and Libreville: one coast, one founding idea.',
              'Three powers had the same notion within forty years of each other, and none of them '
              'thought to compare notes. The Americans put freed slaves ashore at Monrovia, the '
              'British at Freetown, the French at Libreville - the free town, in case the point was '
              'missed. Between them lies the Kru coast, whose sailors crewed the ships that carried '
              'all three. The descendants of those settlements have more in common with each other '
              'than with the empires that dropped them here, and they have started saying so.'),
}

for project, title, theatre in ROMAN_THEATRES:
    area = {'Europe': 'continental Europe, including European Russia',
            'Africa': 'Africa and its adjacent islands',
            'India': 'the Indian subcontinent, including Pakistan, Bangladesh, Nepal, Bhutan, Sri Lanka and the Maldives',
            'NorthAmerica': 'North America, Central America, Greenland and the Caribbean',
            'SouthAmerica': 'South America',
            'Asia': 'the rest of Asia, including the Middle East, Siberia, East Asia and Southeast Asia'}[theatre]
    italian = ' Claims on the Italian core are peaceful.' if theatre == 'Europe' else ''
    LOC[project] = (title, f'Broad claims in {area}. +10 control point capacity.',
                   f'Grants territorial claims throughout {area} for incremental conquest. '
                   f'New foreign claims are hostile.{italian} Requires Great Nations and is independent '
                   'of the other Roman regional projects. Completing this project grants the researching '
                   'faction +10 control point capacity. Available only in Broken Earth.')



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
                claim_project = project
                if be and (nation, region) in BROKEN_EARTH_START_CLAIMS:
                    start_tag = f'Claim{prefix}{nation}_{region}_Irredenta'
                    rows.append({'dataName': start_tag, 'relationType': 'Claim',
                                 'nation1': prefix + nation, 'region1': rm[region],
                                 'hostileClaim': True})
                    made += 1
                    claim_project = BROKEN_EARTH_START_CLAIMS[nation, region]
                    hostile = 0
                tag = claim_project.replace('Project_', '') if claim_project else 'Irredenta'
                row = {'dataName': f'Claim{prefix}{nation}_{region}_{tag}', 'relationType': 'Claim',
                       'nation1': prefix + nation, 'region1': rm[region]}
                if claim_project:
                    row['projectUnlockName'] = claim_project
                if hostile:
                    row['hostileClaim'] = True
                rows.append(row)
                made += 1
        per_scenario[label] = (made, sorted(set(skipped)))
    return rows, per_scenario


if __name__ == '__main__':
    rows, per_scenario = build()
    OUT.write_text(json.dumps(rows, indent=2) + "\n", encoding="utf-8")
    print(f"{len(rows)} claim rows -> {OUT}")
    for label, (n, skipped) in per_scenario.items():
        print(f'  {label:24s} {n:3d} rows' + (f'   skipped: {skipped}' if skipped else ''))
