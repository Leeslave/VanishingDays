const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const path = require('node:path');
const source = fs.readFileSync(path.join(__dirname, '../Assets/Plugins/WebGL/BrowserSaveBackup.js.txt'), 'utf8');
const file = '/idbfs/abc123/vanishing-days-v03.json';
const key = 'vanishing-days.browser-save.v1';
const state = hour => JSON.stringify({schemaVersion: 12, day: 2, hour, PlayerNickname: '기록자'});
const storage = new Map();
let clock = 100;
function boot(initial = {}, localStorage = storage) {
  const files = new Map(Object.entries(initial).map(([p, v]) => [p, {...v}]));
  const warnings = [];
  const FS = {
    readFile(p) { if (!files.has(p)) throw Error('missing'); return files.get(p).data; },
    stat(p) { if (!files.has(p)) throw Error('missing'); return {mtime: new Date(files.get(p).time)}; },
    mkdirTree() {},
    writeFile(p, data) { files.set(p, {data, time: ++clock}); },
    rename(from, to) { if (!files.has(from)) throw Error('missing'); files.set(to, files.get(from)); files.delete(from); }
  };
  const context = vm.createContext({FS, window: {localStorage: {
    getItem: k => localStorage.get(k) || null, setItem: (k, v) => localStorage.set(k, v)
  }}, console: {warn: (...args) => warnings.push(args)}});
  vm.runInContext(source, context);
  return {FS, files, warnings, install: () => vm.runInContext(source, context)};
}
let run = boot();
run.FS.writeFile(file + '.tmp', state(8));
assert.equal(storage.has(key), false, 'Never back up a partial temporary write');
run.FS.rename(file + '.tmp', file);
assert.equal(JSON.parse(storage.get(key)).json, state(8));
run.FS.rename(file, file + '.bak');
run.FS.writeFile(file + '.tmp', state(10));
run.FS.rename(file + '.tmp', file);
assert.equal(JSON.parse(storage.get(key)).backup, state(8), 'Preserve native backup');
// Simulate reload before IndexedDB commits, or startup with unavailable IDB.
run = boot();
assert.equal(run.FS.readFile(file), state(10));
assert.equal(run.FS.readFile(file + '.bak'), state(8));
run = boot({[file]: {data: state(8), time: 1}});
assert.equal(run.FS.readFile(file), state(10), 'Recover over stale IndexedDB data');
run = boot({[file]: {data: state(12), time: 999}});
assert.equal(run.FS.readFile(file), state(12), 'Never replace a newer IndexedDB save');
const before = storage.get(key);
assert.throws(() => run.FS.rename(file + '.missing', file));
assert.equal(storage.get(key), before, 'Failed rename cannot replace the recovery copy');
run.install();
run.FS.writeFile(file + '.tmp', state(14));
run.FS.rename(file + '.tmp', file);
assert.equal(JSON.parse(storage.get(key)).json, state(14), 'Repeated installation stays safe');
storage.set(key, '{broken');
run = boot();
assert.equal(run.files.size, 0, 'Ignore malformed recovery data');
storage.set(key, JSON.stringify({version: 1, path: '/etc/other', json: state(10), modified: 100}));
run = boot();
assert.equal(run.files.size, 0, 'Recovery cannot write outside the game save directory');
const unavailable = {get() {throw new Error('blocked');}, set() {throw new Error('quota');}};
run = boot({}, unavailable);
run.FS.writeFile(file + '.tmp', state(16));
run.FS.rename(file + '.tmp', file);
assert.equal(run.FS.readFile(file), state(16), 'Fallback failure does not break native saving');
assert.equal(run.warnings.length, 1, 'Storage warnings are bounded');
console.log('VD_BROWSER_SAVE_BACKUP_CHECKS_OK: final-write snapshots, immediate reload, IDB failure/staleness, backups, newer-save protection, malformed data and quota failure.');
