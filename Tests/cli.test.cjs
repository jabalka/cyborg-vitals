'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { plan, main, copySource } = require('../bin/cli.cjs');
test('help and version work without Windows or system changes', () => {
  const output = [];
  assert.equal(main(['--help'], { platform: 'linux', output: s => output.push(s) }), 0);
  assert.match(output[0], /No Windows software is installed merely by npm install/);
  assert.equal(main(['--version'], { platform: 'linux', output: s => output.push(s) }), 0);
  assert.equal(output[1], '1.2.7');
});
test('unknown commands, extra arguments and non-Windows execution are rejected', () => {
  for (const args of [['install; calc'], ['install', '--anything'], ['constructor']]) assert.throws(() => plan(args, 'win32', { SystemRoot: 'C:\\Windows' }));
  assert.throws(() => plan(['install'], 'linux', {}), /Windows 11/);
});
test('doctor is a read-only PowerShell check', () => {
  const result = plan(['doctor'], 'win32', { SystemRoot: 'C:\\Windows' });
  assert.equal(result.script, 'Install-Prerequisites.ps1');
  assert.deepEqual(result.extra, ['-CheckOnly']);
});
test('source staging excludes generated output, dependencies and signing keys', () => {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'vitals-copy-'));
  try {
    const source = path.join(root, 'source'), dest = path.join(root, 'dest');
    for (const name of ['Widget/bin/Debug/old.exe','Widget/obj/cache','node_modules/x/index.js','.git/config','sign.pfx','Widget/Assets/icon.png','bin/cli.cjs']) {
      const file = path.join(source, name); fs.mkdirSync(path.dirname(file), {recursive:true}); fs.writeFileSync(file, name);
    }
    copySource(source, dest);
    assert.ok(fs.existsSync(path.join(dest,'bin/cli.cjs')));
    assert.ok(fs.existsSync(path.join(dest,'Widget/Assets/icon.png')));
    for (const name of ['Widget/bin','Widget/obj','node_modules','.git','sign.pfx']) assert.ok(!fs.existsSync(path.join(dest, name)), name);
  } finally { fs.rmSync(root, {recursive:true,force:true}); }
});
test('install stages outside npm files, uses argument arrays and cleans only its successful staging directory', () => {
  const local = fs.mkdtempSync(path.join(os.tmpdir(), 'vitals-install-'));
  let staged;
  try {
    const status = main(['install'], { platform:'win32', env:{ SystemRoot:'C:\\Windows', LOCALAPPDATA:local }, output:()=>{}, spawn(exe,args,options) {
      assert.equal(options.shell, false);
      const file = args[args.indexOf('-File')+1]; staged = path.dirname(file);
      assert.equal(path.basename(file), 'Install.ps1');
      assert.ok(fs.existsSync(path.join(staged, 'Widget/SettingsPage.xaml')));
      assert.ok(fs.existsSync(path.join(staged, 'Collector/MsiFanReader.cs')));
      return {status:0};
    }});
    assert.equal(status,0); assert.ok(!fs.existsSync(staged)); assert.ok(fs.existsSync(local));
  } finally { fs.rmSync(local,{recursive:true,force:true}); }
});
test('a failed installer reports failure and retains sources for diagnosis', () => {
  const local = fs.mkdtempSync(path.join(os.tmpdir(), 'vitals-fail-')), messages=[];
  try {
    assert.equal(main(['install'], { platform:'win32', env:{SystemRoot:'C:\\Windows',LOCALAPPDATA:local}, output:s=>messages.push(s), spawn:()=>({status:7}) }),7);
    assert.match(messages.at(-1), /retained at/);
    assert.equal(fs.readdirSync(path.join(local,'CyborgVitals','Source')).length,1);
  } finally { fs.rmSync(local,{recursive:true,force:true}); }
});
test('runtime commands use installed per-user scripts and preserve failure codes', () => {
  const local=fs.mkdtempSync(path.join(os.tmpdir(),'vitals-runtime-'));
  try {
    const runtime=path.join(local,'CyborgVitals'); fs.mkdirSync(runtime);
    fs.writeFileSync(path.join(runtime,'Stop-Monitor.ps1'),'# fixture');
    assert.equal(main(['stop'],{platform:'win32',env:{SystemRoot:'C:\\Windows',LOCALAPPDATA:local},spawn(exe,args,options){assert.equal(options.cwd,runtime);assert.equal(args.at(-1),path.join(runtime,'Stop-Monitor.ps1'));return {status:3};}}),3);
    assert.throws(()=>main(['diagnose'],{platform:'win32',env:{SystemRoot:'C:\\Windows',LOCALAPPDATA:local}}),/install first/);
  } finally {fs.rmSync(local,{recursive:true,force:true});}
});
