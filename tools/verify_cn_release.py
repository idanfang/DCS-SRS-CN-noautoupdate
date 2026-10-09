from pathlib import Path
import json,hashlib,zipfile,struct,zlib
import argparse
parser=argparse.ArgumentParser();parser.add_argument('--source',required=True);parser.add_argument('--output',required=True);args=parser.parse_args()
SRC=Path(args.source).resolve();BASE=Path(args.output).resolve();OUT=BASE/'install-build'
signature=bytes.fromhex('8b1202b96a612038727b930214d7a03213f5b9e6efae3318ee3b2dce24b36aae')
def bundle(p):
 b=p.read_bytes();where=b.index(signature);pos=struct.unpack_from('<Q',b,where-8)[0]
 major,minor,count=struct.unpack_from('<IIi',b,pos);pos+=12
 def string():
  nonlocal pos
  size=0;shift=0
  while True:
   c=b[pos];pos+=1;size|=(c&127)<<shift;shift+=7
   if not c&128:break
  value=b[pos:pos+size].decode();pos+=size;return value
 bundleid=string()
 if major>=2:pos+=40
 entries={}
 for _ in range(count):
  offset,size=struct.unpack_from('<qq',b,pos);pos+=16;compressed=0
  if major>=6:compressed=struct.unpack_from('<q',b,pos)[0];pos+=8
  kind=b[pos];pos+=1;name=string();data=b[offset:offset+(compressed or size)]
  if compressed:data=zlib.decompress(data,-15)
  assert len(data)==size
  entries[name]=data
 return entries
m=json.loads((OUT/'release-manifest.json').read_text(encoding='utf-8'));issues=[];checks=[]
for name,digest in m['source_sha256'].items():assert hashlib.sha256((SRC/name).read_bytes()).hexdigest()==digest,'Changed source '+name
for name,digest in m['payload_sha256'].items():assert hashlib.sha256((OUT/name).read_bytes()).hexdigest()==digest,'Changed payload '+name
apps=[('Installer.exe','Installer'),('SRS-AutoUpdater.exe','SRS-AutoUpdater'),('Client/SR-ClientRadio.exe','SR-ClientRadio'),('Server/SRS-Server.exe','SRS-Server'),('ExternalAudio/DCS-SR-ExternalAudio.exe','DCS-SR-ExternalAudio'),('ServerCommandLine-Windows/SRS-Server-Commandline.exe','SRS-Server-Commandline'),('ServerCommandLine-Linux/SRS-Server-Commandline','SRS-Server-Commandline')]
for path,app in apps:
 p=OUT/path;entries=bundle(p)
 assert app+'.dll' in entries
 assert app+'.runtimeconfig.json' in entries
 deps=json.loads(entries[app+'.deps.json']);target=deps['targets'][deps['runtimeTarget']['name']];checked=0
 for library,data in target.items():
  for kind in ['runtime','native','resources']:
   for name,meta in data.get(kind,{}).items():
    if name.endswith('/_._'):continue
    basename=Path(name).name;locale=meta.get('locale');candidates=[name,basename]
    if locale:candidates.insert(0,locale+'/'+basename)
    assert any(n in entries or (p.parent/n).is_file() for n in candidates),'Missing dependency '+path+' '+name
    checked+=1
 checks.append({'app':path,'bundle_entries':len(entries),'dependency_entries_checked':checked,'runtimeconfig':json.loads(entries[app+'.runtimeconfig.json'])})
 assert not any(Path(n).suffix.lower() in {'.ttf','.ttc','.otf','.woff','.woff2','.fon','.fnt','.compositefont'} for n in entries)
asset=BASE/'assets/DCS-SimpleRadioStandalone-2.4.1.0-cn.1.zip'
with zipfile.ZipFile(asset) as z:
 assert z.testzip() is None
 assert 'Installer.exe' in z.namelist() and 'Client/SR-ClientRadio.exe' in z.namelist()
 assert all(hashlib.sha256(z.read(n)).hexdigest()==digest for n,digest in m['payload_sha256'].items())
report={'status':'passed','commit':m['commit'],'tag':m['tag'],'zip_sha256':hashlib.sha256(asset.read_bytes()).hexdigest(),'source_files':len(m['source_sha256']),'payload_files':len(m['payload_sha256']),'root_installer_layout':True,'bundled_dependency_checks':checks,'font_files':[],'not_tested':m['not_tested'],'known_unfixed':m['known_unfixed']}
(BASE/'verification.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({k:v for k,v in report.items() if k!='bundled_dependency_checks'},ensure_ascii=False,indent=2))
