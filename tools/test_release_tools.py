"""Offline release preflight regression tests. Never builds or downloads payloads."""
import contextlib,io,runpy,subprocess,sys,tempfile,unittest
from pathlib import Path
from unittest.mock import patch

SCRIPT=Path(__file__).with_name('build_cn_release.py')
class ReleasePreflightTests(unittest.TestCase):
 def setUp(self):
  self.temp=tempfile.TemporaryDirectory();self.root=Path(self.temp.name);self.source=self.root/'source';self.source.mkdir()
  self.git('init','-q');self.git('config','user.name','Release test');self.git('config','user.email','test@example.invalid')
  (self.source/'input.txt').write_text('source',encoding='utf-8');self.git('add','.');self.git('commit','-qm','fixture');self.git('tag','v2.4.1.0-cn.2')
 def tearDown(self): self.temp.cleanup()
 def git(self,*args): return subprocess.check_output(['git',*args],cwd=self.source,stderr=subprocess.STDOUT)
 def build(self,tag='v2.4.1.0-cn.2'):
  original=subprocess.run
  def offline(args,*a,**kw):
   if args[0]=='dotnet': return subprocess.CompletedProcess(args,1)
   return original(args,*a,**kw)
  with patch.object(sys,'argv',[str(SCRIPT),'--source',str(self.source),'--output',str(self.root/'output'),'--tag',tag]),patch('subprocess.run',side_effect=offline),patch('time.sleep'),contextlib.redirect_stdout(io.StringIO()):
   runpy.run_path(str(SCRIPT),run_name='__main__')
 def test_next_revision_reaches_build_without_hardcoded_first_commit(self):
  with self.assertRaisesRegex(RuntimeError,'Publish failed'): self.build()
 def test_tag_must_match_source_commit(self):
  (self.source/'input.txt').write_text('changed');self.git('add','.');self.git('commit','-qm','other commit')
  with self.assertRaisesRegex(AssertionError,'immutable release tag'): self.build()
 def test_dirty_source_is_rejected(self):
  (self.source/'input.txt').write_text('dirty')
  with self.assertRaisesRegex(AssertionError,'Commit source'): self.build()
 def test_old_output_cannot_contaminate_release(self):
  (self.root/'output').mkdir()
  with self.assertRaisesRegex(AssertionError,'fresh output'): self.build()
 def test_new_upstream_requires_reviewed_provenance(self):
  with self.assertRaisesRegex(AssertionError,'pinned upstream'): self.build('v2.5.0.0-cn.1')
if __name__=='__main__': unittest.main()
