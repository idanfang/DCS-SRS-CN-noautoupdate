from pathlib import Path
import subprocess,hashlib,json,shutil,os,zipfile,time,struct
import argparse,urllib.request,re
parser=argparse.ArgumentParser()
parser.add_argument('--source',required=True);parser.add_argument('--output',required=True);parser.add_argument('--tag',required=True)
args=parser.parse_args()
SRC=Path(args.source).resolve();BASE=Path(args.output).resolve();OUT=BASE/'install-build';WORK=BASE/'work';LOGS=BASE/'logs';SDK='dotnet'
UP=BASE/'upstream-official.zip'
match=re.fullmatch(r'v(2\.4\.1\.0)-cn\.([1-9]\d*)',args.tag)
assert match,'Only pinned upstream 2.4.1.0 is supported; add reviewed provenance for a new upstream version'
commit=subprocess.check_output(['git','rev-parse','HEAD'],cwd=SRC).decode().strip()
assert subprocess.check_output(['git','rev-parse',args.tag+'^{commit}'],cwd=SRC).decode().strip()==commit,'Source must match the immutable release tag'

def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def snapshot():
 names=subprocess.check_output(['git','ls-files','-z'],cwd=SRC).decode().split('\0')
 return {n:sha(SRC/n) for n in sorted(names) if n and (SRC/n).is_file()}
assert subprocess.check_output(['git','status','--porcelain'],cwd=SRC)==b'','Commit source before packaging'
inputs=snapshot();time.sleep(2);assert snapshot()==inputs,'Concurrent mutation'
assert not BASE.exists(),'Use a fresh output directory; do not mix old release artifacts'
OUT.mkdir(parents=True);WORK.mkdir();LOGS.mkdir()
env=os.environ.copy();env['DOTNET_CLI_TELEMETRY_OPTOUT']='1'
components=[('Client','DCS-SR-Client/DCS-SR-Client.csproj','win-x64',False),('Server','Server/Server.csproj','win-x64',False),('ServerCommandLine-Windows','ServerCommandLine/ServerCommandLine.csproj','win-x64',True),('ServerCommandLine-Linux','ServerCommandLine/ServerCommandLine.csproj','linux-x64',True),('ExternalAudio','DCS-SR-ExternalAudio/DCS-SR-ExternalAudio.csproj','win-x64',False),('AutoUpdater','AutoUpdater/AutoUpdater.csproj','win-x64',False),('Installer','Installer/Installer.csproj','win-x64',False)]
for label,project,rid,selfcontained in components:
 destination=WORK/label if label in ['AutoUpdater','Installer'] else OUT/label
 print('Publish '+label,flush=True)
 cmd=[str(SDK),'publish',str(SRC/project),'-c','Release','-r',rid,'--self-contained',str(selfcontained).lower(),'-o',str(destination),'--nologo','-v','minimal','-p:PublishSingleFile=true','-p:PublishReadyToRun=true','-p:DebugType=None','-p:DebugSymbols=false','-p:IncludeSourceRevisionInInformationalVersion=false']
 with (LOGS/(label+'.log')).open('w',encoding='utf-8') as f:result=subprocess.run(cmd,cwd=SRC,env=env,stdout=f,stderr=subprocess.STDOUT)
 if result.returncode:
  print((LOGS/(label+'.log')).read_text(encoding='utf-8')[-12000:],flush=True)
  raise RuntimeError('Publish failed: '+label)
 assert inputs==snapshot(),'Source changed while building'
 if rid=='win-x64':
  native=destination/'runtimes/win-x64/native'
  if native.exists():
   for p in native.glob('*.dll'):shutil.copy2(p,destination/p.name)
print('Download official 2.4.1.0 resource archive',flush=True)
url='https://github.com/ciribob/DCS-SimpleRadioStandalone/releases/download/2.4.1.0/DCS-SimpleRadioStandalone-2.4.1.0.zip'
with urllib.request.urlopen(url,timeout=60) as response,UP.open('wb') as output:
 shutil.copyfileobj(response,output,1024*1024)
assert UP.stat().st_size==290618021,'Incomplete official archive'
print('Validate official resource archive',flush=True)
assert sha(UP)=='8aedb7b2a24f25d15b3603a6ed6e3437dc0d1ff12297ed53e514d8674871c3d9','Official archive hash mismatch'
shutil.copytree(SRC/'Scripts',OUT/'Scripts')
provenance={}
with zipfile.ZipFile(UP) as z:
 assert z.testzip() is None
 for name in ['Scripts/DCS-SRS/bin/srs.dll','VC_redist.x64.exe','Examples.txt']:
  data=z.read(name);p=OUT/name;p.parent.mkdir(parents=True,exist_ok=True);p.write_bytes(data);provenance[name]=hashlib.sha256(data).hexdigest()
for label,exe in [('AutoUpdater','SRS-AutoUpdater.exe'),('Installer','Installer.exe')]:
 shutil.copy2(WORK/label/exe,OUT/exe)
 # Root programs must be bundled; no managed DLL is allowed to be omitted at the root.
 assert not list((WORK/label).glob('*.dll')),'Unbundled root dependencies: '+label
for name in ['LICENSE','LICENSE.txt','LICENSE.md']:
 if (SRC/name).is_file():shutil.copy2(SRC/name,OUT/name)
shutil.copy2(SRC/'docs/安装与手动更新.md',OUT/'Readme.txt')
shutil.copy2(SRC/'docs/翻译修订对照.md',OUT/'翻译修订对照.md')
required=['Installer.exe','SRS-AutoUpdater.exe','VC_redist.x64.exe','Examples.txt','Readme.txt','Client/SR-ClientRadio.exe','Client/opus.dll','Client/speexdsp.dll','Client/awacs-radios.json','Server/SRS-Server.exe','ServerCommandLine-Windows/SRS-Server-Commandline.exe','ServerCommandLine-Linux/SRS-Server-Commandline','ExternalAudio/DCS-SR-ExternalAudio.exe','Scripts/DCS-SRS/bin/srs.dll','Scripts/DCS-SRS/Scripts/DCS-SimpleRadioStandalone.lua']
for name in required:assert (OUT/name).is_file(),'Missing '+name
for name in ['Installer.exe','SRS-AutoUpdater.exe','Client/SR-ClientRadio.exe','Server/SRS-Server.exe','ServerCommandLine-Windows/SRS-Server-Commandline.exe','ExternalAudio/DCS-SR-ExternalAudio.exe','Scripts/DCS-SRS/bin/srs.dll']:
 b=(OUT/name).read_bytes();offset=struct.unpack_from('<I',b,0x3c)[0];assert struct.unpack_from('<H',b,offset+4)[0]==0x8664,'Non-x64 PE: '+name
assert (OUT/'ServerCommandLine-Linux/SRS-Server-Commandline').read_bytes()[:4]==b'\x7fELF'
assert not [p for p in OUT.rglob('*') if p.suffix.lower() in {'.ttf','.otf','.ttc','.woff','.woff2','.fon','.fnt','.compositefont'}],'Fonts must not be distributed'
assert inputs==snapshot()
manifest={'tag':args.tag,'internal_version':'2.4.1.0','commit':subprocess.check_output(['git','rev-parse','HEAD'],cwd=SRC).decode().strip(),'repository':'idanfang/DCS-SRS-CN-noautoupdate','upstream_commit':'f7bdcd42d41eff1e912cd0465b8a22c78108c53c','upstream_archive_url':'https://github.com/ciribob/DCS-SimpleRadioStandalone/releases/download/2.4.1.0/DCS-SimpleRadioStandalone-2.4.1.0.zip','upstream_archive_sha256':sha(UP),'reused_unmodified_official_files':provenance,'source_sha256':inputs,'source_stable':True,'required_payload':required,'components':[{'label':a,'project':b,'rid':c,'self_contained':d,'single_file':True} for a,b,c,d in components],'font_files':[],'payload_sha256':{str(p.relative_to(OUT)).replace('\\','/'):sha(p) for p in sorted(OUT.rglob('*')) if p.is_file()},'not_tested':['DCS integration','Live voice','Actual install/overwrite/update'],'known_unfixed':['Upstream installer retains its original process and destination behavior; explicit review required'],'signing':'Rebuilt fork binaries are unsigned; official reused files preserve original bytes/signatures'}
(OUT/'release-manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
shutil.copy2(OUT/'release-manifest.json',BASE/'release-manifest.json')
ASSETS=BASE/'assets';ASSETS.mkdir()
asset=ASSETS/('DCS-SimpleRadioStandalone-'+args.tag.removeprefix('v')+'.zip')
with zipfile.ZipFile(asset,'w',zipfile.ZIP_DEFLATED,compresslevel=6) as z:
 for p in sorted(OUT.rglob('*')):
  if p.is_file():z.write(p,str(p.relative_to(OUT)))
with zipfile.ZipFile(asset) as z:assert z.testzip() is None
(ASSETS/'SHA256SUMS.txt').write_text('\n'.join(sha(p)+'  '+p.name for p in [asset])+'\n',encoding='ascii')
print(json.dumps({'zip':str(asset),'sha256':sha(asset),'size':asset.stat().st_size,'commit':manifest['commit'],'manifest':str(OUT/'release-manifest.json'),'files':len(manifest['payload_sha256'])},ensure_ascii=False,indent=2),flush=True)
