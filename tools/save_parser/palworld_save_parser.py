#!/usr/bin/env python3
import argparse,json,os,sys
from typing import Any
from palsav.core import decompress_sav_to_gvas
from palsav.gvas import GvasFile
from palsav.paltypes import PALWORLD_CUSTOM_PROPERTIES,PALWORLD_TYPE_HINTS

def find(node:Any,key:str):
    if isinstance(node,dict):
        if key in node:return node[key]
        for v in node.values():
            r=find(v,key)
            if r is not None:return r
    elif isinstance(node,list):
        for v in node:
            r=find(v,key)
            if r is not None:return r
    return None

def unwrap(v):
    while isinstance(v,dict):
        moved=False
        for k in ('value','Value','Str','Name','Enum','Guid','Bool','Int','Int64','Float'):
            if k in v and len(v)<=4:v=v[k];moved=True;break
        if not moved:break
    return v

def scalar(node,*keys,default=None):
    for k in keys:
        v=find(node,k)
        if v is not None:
            v=unwrap(v)
            if isinstance(v,(str,int,float,bool)):return v
    return default

def strings(node):
    out=[]
    if isinstance(node,str) and node:out.append(node)
    elif isinstance(node,dict):
        for v in node.values():out+=strings(v)
    elif isinstance(node,list):
        for v in node:out+=strings(v)
    return list(dict.fromkeys(out))

def entries(root,name):
    n=find(root,name)
    if n is None:return []
    if isinstance(n,list):return n
    v=find(n,'value')
    return v if isinstance(v,list) else []

def character(e):
    val=e.get('value',e) if isinstance(e,dict) else e
    key=e.get('key',{}) if isinstance(e,dict) else {}
    passive=find(val,'PassiveSkillList')
    ps=[x for x in strings(passive) if x not in ('None','EPalPassiveSkillID::None')]
    return {
      'isPlayer':bool(scalar(val,'IsPlayer','is_player',default=False)),
      'playerUid':str(scalar(key,'PlayerUId','PlayerUID',default='') or scalar(val,'PlayerUId','PlayerUID',default='') or ''),
      'ownerPlayerUid':str(scalar(val,'OwnerPlayerUId','OwnerPlayerUID','owner_player_uid',default='') or ''),
      'instanceId':str(scalar(key,'InstanceId','InstanceID',default='') or scalar(val,'InstanceId','InstanceID',default='') or ''),
      'name':str(scalar(val,'NickName','Nickname','PlayerName',default='') or ''),
      'species':str(scalar(val,'CharacterID','CharacterId','character_id',default='') or ''),
      'level':int(scalar(val,'Level','level',default=0) or 0),
      'gender':str(scalar(val,'Gender','gender',default='') or ''),
      'passiveSkills':ps}

def main():
    p=argparse.ArgumentParser();p.add_argument('save');p.add_argument('output');a=p.parse_args()
    if not os.path.isfile(a.save):raise FileNotFoundError(a.save)
    with open(a.save,'rb') as f:raw,save_type=decompress_sav_to_gvas(f.read())
    wanted={k:v for k,v in PALWORLD_CUSTOM_PROPERTIES.items() if 'CharacterSaveParameterMap' in k or 'GroupSaveDataMap' in k}
    data=GvasFile.read(raw,PALWORLD_TYPE_HINTS,wanted,allow_nan=False).dump()
    chars=[character(x) for x in entries(data,'CharacterSaveParameterMap')]
    names={c['playerUid']:(c['name'] or c['playerUid'] or 'Unknown player') for c in chars if c['isPlayer']}
    players=[{'name':names.get(c['playerUid'],'Unknown player'),'playerUid':c['playerUid'],'level':c['level']} for c in chars if c['isPlayer']]
    pals=[{'owner':names.get(c['ownerPlayerUid'],c['ownerPlayerUid'] or 'World / base'),'ownerPlayerUid':c['ownerPlayerUid'],'species':c['species'],'nickname':c['name'],'level':c['level'],'gender':c['gender'],'passiveSkills':c['passiveSkills'],'instanceId':c['instanceId']} for c in chars if not c['isPlayer']]
    result={'parser':'palsav-flex','saveType':save_type,'playerCount':len(players),'palCount':len(pals),'players':players,'pals':pals}
    with open(a.output,'w',encoding='utf-8') as f:json.dump(result,f,ensure_ascii=False,separators=(',',':'))
if __name__=='__main__':
    try:main()
    except Exception as e:
        message=str(e)
        if "instead of b'PlZ'" in message or "instead of b'PLZ'" in message:
            message += "\nThis save uses Palworld's newer Oodle-compressed format. Please install the latest PalworldHelper build."
        print(f'Parser error: {message}',file=sys.stderr)
        sys.exit(1)
