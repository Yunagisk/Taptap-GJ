from pathlib import Path
import re,json

repo=Path(__file__).resolve().parents[2]
targets=['Assets/Prefabs/UI/EquipmentPanel.prefab','Assets/Prefabs/UI/CombatHUD.prefab']
ids_by_guid={}
report={}
for target in targets:
    path=repo/target;text=path.read_text(encoding='utf-8-sig')
    blocks={int(m[1]):m[0] for m in re.findall(r'(^--- !u!\d+ &(\d+)\n.*?)(?=^--- !u!|\Z)',text,re.M|re.S)}
    all_ids=re.findall(r'^--- !u!\d+ &(\d+)',text,re.M)
    assert len(all_ids)==len(set(all_ids)), 'duplicate fileID: '+target
    assert set(map(int,re.findall(r'\{fileID: (\d+)\}',text))) <= set(blocks)|{0}, 'dangling internal reference: '+target
    guid=re.search(r'^guid: (\w+)',Path(str(path)+'.meta').read_text(),re.M)[1]
    ids_by_guid[guid]=set(blocks)
    for id,body in blocks.items():
        if '\nRectTransform:' not in body:continue
        parent=int(re.search(r'  m_Father: \{fileID: (\d+)\}',body)[1])
        if parent:
            children=re.search(r'  m_Children:(.*?)(?=\n  \w+:)',blocks[parent],re.S)[1]
            assert id in map(int,re.findall(r'fileID: (\d+)',children)), ('missing child link',target,id,parent)
    report[target]={'objects':len(blocks),'internalReferencesValid':True,'hierarchyLinksValid':True}
    if 'EquipmentPanel' in target:
        for name in ['OwnedTitle','SlotsTitle','Slot_1','Slot_2','Slot_3','Slot_4']:
            assert not re.search(r'^  m_Name: '+name+'$',text,re.M), name+' still present'
        assert len(re.findall(r'^  m_Name: CategoryEquipment_\d$',text,re.M))==4
        assert len(re.findall(r'^  m_Name: NewWeaponDot$',text,re.M))==4
        assert '  slotButtons: []' in text and '  slotLabels: []' in text
        assert '  m_Name: CategoryWeaponPicker' in text
    else:
        assert '  m_Name: SpecialEffect' in text
        intent_id=next(id for id,b in blocks.items() if '\nGameObject:' in b and re.search(r'^  m_Name: Intent$',b,re.M))
        intent_rect=int(re.search(r'component: \{fileID: (\d+)\}',blocks[intent_id])[1])
        descendants=re.search(r'  m_Children:(.*?)(?=\n  \w+:)',blocks[intent_rect],re.S)[1]
        for child in map(int,re.findall(r'fileID: (\d+)',descendants)):
            go=int(re.search(r'  m_GameObject: \{fileID: (\d+)\}',blocks[child])[1])
            assert not re.search(r'^  m_Name: Icon$',blocks[go],re.M),'ordinary intent icon still present'

# Preserve references held by existing scene and prefab instances, including old focus targets.
for directory in ['Assets/Scenes','Assets/Prefabs']:
    for path in (repo/directory).rglob('*'):
        if path.suffix not in {'.unity','.prefab'}:continue
        for id,guid in re.findall(r'\{fileID: (\d+), guid: (\w+), type: \d+\}',path.read_text(encoding='utf-8-sig')):
            if guid in ids_by_guid:assert int(id) in ids_by_guid[guid]|{0,100100000}, ('dangling external reference',str(path),id)
report['existingSceneAndPrefabReferencesValid']=True
print(json.dumps(report,indent=2))
(repo/'output/inventory-category/static-validation.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
