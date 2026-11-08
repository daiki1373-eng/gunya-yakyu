// ============================================================
// deploy_hosting.js
// build-webgl を Firebase Hosting(サイト gunya-yakyu)へ公開する。
// firebase-tools の deploy はサービスアカウントに serviceusage の権限がなく 403 になるため、
// Hosting の REST API を直接使う(版を作る → ファイルの一覧を登録 → 足りないファイルを送る → 確定 → 公開)。
//
// 使い方(unity/ で):
//   npm i google-auth-library        (初回のみ。node_modules はコミットしない)
//   GOOGLE_APPLICATION_CREDENTIALS=/path/to/key.json node Tools/online/deploy_hosting.js
// 鍵は公開に使ったらすぐ GCP コンソールで削除し、JSON もゴミ箱へ。鍵をリポジトリに置かない。
// ============================================================
const fs = require('fs');
const path = require('path');
const zlib = require('zlib');
const crypto = require('crypto');
const { GoogleAuth } = require('google-auth-library');

const SITE = 'gunya-yakyu';
const ROOT = path.join(__dirname, '..', '..');
const PUBLIC_DIR = path.join(ROOT, 'build-webgl');
const API = `https://firebasehosting.googleapis.com/v1beta1/sites/${SITE}`;

function listFiles(dir, base = dir) {
  let out = [];
  for (const name of fs.readdirSync(dir)) {
    if (name.startsWith('.')) continue;
    const full = path.join(dir, name);
    if (fs.statSync(full).isDirectory()) out = out.concat(listFiles(full, base));
    else out.push('/' + path.relative(base, full).split(path.sep).join('/'));
  }
  return out;
}

async function main() {
  const auth = new GoogleAuth({ scopes: ['https://www.googleapis.com/auth/cloud-platform', 'https://www.googleapis.com/auth/firebase'] });
  const client = await auth.getClient();
  const req = (url, opts = {}) => client.request({ url, ...opts });

  // firebase.json の headers を版の設定に写す
  const fb = JSON.parse(fs.readFileSync(path.join(ROOT, 'firebase.json'), 'utf8')).hosting;
  const config = { headers: (fb.headers || []).map(h => ({ glob: h.source, headers: Object.fromEntries(h.headers.map(x => [x.key, x.value])) })) };

  const ver = (await req(`${API}/versions`, { method: 'POST', data: { config } })).data;
  console.log('version', ver.name);

  const files = listFiles(PUBLIC_DIR);
  const gz = {}, hashes = {};
  for (const f of files) {
    const data = zlib.gzipSync(fs.readFileSync(path.join(PUBLIC_DIR, f)), { level: 9 });
    const h = crypto.createHash('sha256').update(data).digest('hex');
    gz[h] = data; hashes[f] = h;
  }
  const pop = (await req(`https://firebasehosting.googleapis.com/v1beta1/${ver.name}:populateFiles`, { method: 'POST', data: { files: hashes } })).data;
  const need = pop.uploadRequiredHashes || [];
  console.log('files', files.length, 'upload', need.length);
  for (const h of need) {
    await req(`${pop.uploadUrl}/${h}`, { method: 'POST', headers: { 'Content-Type': 'application/octet-stream' }, data: gz[h], maxBodyLength: Infinity, maxContentLength: Infinity });
    console.log('uploaded', h.slice(0, 12), (gz[h].length / 1048576).toFixed(1) + 'MB');
  }
  await req(`https://firebasehosting.googleapis.com/v1beta1/${ver.name}?update_mask=status`, { method: 'PATCH', data: { status: 'FINALIZED' } });
  const rel = (await req(`${API}/releases?versionName=${encodeURIComponent(ver.name)}`, { method: 'POST' })).data;
  console.log('released', rel.name, '→ https://' + SITE + '.web.app');
}

main().catch(e => { console.error(e.response ? JSON.stringify(e.response.data) : e); process.exit(1); });
