const fs = require('node:fs');
const path = require('node:path');
const zlib = require('node:zlib');
const crypto = require('node:crypto');
const assert = require('node:assert/strict');

const output = path.resolve(process.argv[2] || 'Builds/WebGL');
const bridge = fs.readFileSync(path.resolve(__dirname, '../Assets/Plugins/WebGL/BrowserSaveBackup.js.txt'), 'utf8');
const index = path.join(output, 'index.html');
let html = fs.readFileSync(index, 'utf8');
const match = html.match(/frameworkUrl:\s*buildUrl \+ "\/([^\"]+)"/);
assert.ok(match, 'Unity framework URL missing');
const original = path.join(output, 'Build', match[1]);
let framework = zlib.gunzipSync(fs.readFileSync(original)).toString('utf8');
const marker = 'Module.removeRunDependency("JS_FileSystem_Mount")';
assert.equal(framework.split(marker).length, 2, 'Expected one Unity filesystem startup hook');
if (!framework.includes('installVanishingDaysSaveBackup')) {
  framework = framework.replace(marker, bridge + ';' + marker);
}
const compressed = zlib.gzipSync(framework);
// Unity's fallback loader recognizes a gzip comment, not just the gzip magic.
const header = Buffer.from(compressed.subarray(0, 10));
header[3] |= 16;
const bytes = Buffer.concat([header, Buffer.from('UnityWeb Compressed Content (gzip)\0'), compressed.subarray(10)]);
const hash = crypto.createHash('sha256').update(bytes).digest('hex').slice(0, 12);
const name = `WebGL.${hash}.framework.js.unityweb`;
fs.writeFileSync(path.join(output, 'Build', name), bytes);
html = html.replace('"/' + match[1] + '"', '"/' + name + '"');
html = html.replace('// config.autoSyncPersistentDataPath = true;', 'config.autoSyncPersistentDataPath = true;');
fs.writeFileSync(index, html);
console.log('VD_WEB_SAVE_BACKUP_PATCH_OK: synchronous recovery copy, startup restore and cache-busted framework.');
