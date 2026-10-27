#!/usr/bin/env node
'use strict';
const fs = require('node:fs');
const path = require('node:path');
const { spawnSync } = require('node:child_process');
const packageRoot = path.resolve(__dirname, '..');
const pkg = require('../package.json');
const commands = Object.freeze({
  install: ['Install.ps1'],
  doctor: ['Install-Prerequisites.ps1', '-CheckOnly'],
  diagnose: ['Diagnose-Readings.ps1'],
  start: ['Start-Monitor.ps1', '-EnableHardwareSensors'],
  stop: ['Stop-Monitor.ps1'],
  uninstall: ['Remove-Widget.ps1']
});
function plan(argv, platform = process.platform, env = process.env) {
  const command = argv[0] || 'help';
  if (command === 'help' || command === '--help' || command === '-h') return { help: true };
  if (command === '--version' || command === '-v') return { version: true };
  if (!Object.hasOwn(commands, command) || argv.length !== 1) throw new Error('Use a listed command without extra arguments. Run cyborg-vitals --help.');
  if (platform !== 'win32') throw new Error('The widget requires Windows 11 on an Intel/AMD x64 PC.');
  if (!env.SystemRoot) throw new Error('Windows SystemRoot is missing.');
  return { command, exe: path.join(env.SystemRoot, 'System32', 'WindowsPowerShell', 'v1.0', 'powershell.exe'), script: commands[command][0], extra: commands[command].slice(1) };
}
function copySource(source, destination) {
  fs.cpSync(source, destination, { recursive: true, filter(file) {
    const parts = path.relative(source, file).split(path.sep);
    return !parts.some(part => ['node_modules', '.git', 'obj', 'dist'].includes(part))
      && !(parts.includes('bin') && parts[0] !== 'bin')
      && !/\.(?:tgz|zip|pfx|cer|log|user|suo)$/i.test(file);
  }});
}
function main(argv = process.argv.slice(2), context = {}) {
  const env = context.env || process.env;
  const selected = plan(argv, context.platform || process.platform, env);
  const output = context.output || console.log;
  if (selected.help) {
    output('Cyborg Vitals ' + pkg.version + '\nCommands: install, doctor, diagnose, start, stop, uninstall\ninstall checks prerequisites, offers missing tools, then builds/registers for this user.\nNo Windows software is installed merely by npm install.');
    return 0;
  }
  if (selected.version) { output(pkg.version); return 0; }
  const runner = context.spawn || spawnSync;
  let root = packageRoot, staged;
  if (selected.command === 'install') {
    if (!env.LOCALAPPDATA) throw new Error('LOCALAPPDATA is missing. Run as the Windows user who will use the widget.');
    const stagingRoot = path.join(env.LOCALAPPDATA, 'CyborgVitals', 'Source');
    fs.mkdirSync(stagingRoot, { recursive: true });
    staged = fs.mkdtempSync(path.join(stagingRoot, pkg.version + '-'));
    copySource(packageRoot, staged);
    root = staged;
    output('Checking tools and installing for the current Windows user. Approve only the missing prerequisites you want.');
  } else if (['start', 'stop', 'diagnose', 'uninstall'].includes(selected.command)) {
    if (!env.LOCALAPPDATA) throw new Error('LOCALAPPDATA is missing.');
    root = path.join(env.LOCALAPPDATA, 'CyborgVitals');
    if (!fs.existsSync(path.join(root, selected.script))) throw new Error('Widget runtime is not installed for this user. Run cyborg-vitals install first.');
  }
  const args = ['-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', path.join(root, selected.script), ...selected.extra];
  const result = runner(selected.exe, args, { stdio: 'inherit', shell: false, windowsHide: false, env, cwd: root });
  if (result.error) throw result.error;
  const status = result.status ?? 1;
  if (staged && status === 0) fs.rmSync(staged, { recursive: true, force: true });
  else if (staged) output('Installation did not complete. Sources/build output retained at: ' + staged);
  return status;
}
module.exports = { main, plan, copySource };
if (require.main === module) {
  try { process.exitCode = main(); }
  catch (error) { console.error('Cyborg Vitals: ' + error.message); process.exitCode = 1; }
}
