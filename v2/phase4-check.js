'use strict';

// jsdomの隔離localStorageで開発／本番両ビルドと、本番v1フィクスチャ移行を確認する。
const fs = require('fs');
const { JSDOM, VirtualConsole } = require('jsdom');

const fixture = {
  coins: 2468,
  ownedPitch: { straight: true, curve: true, custom: true },
  ownedPose: { square: true, open: true, ichi: true },
  premium: true,
  pose: 'open', selPitch: 'custom', brkDir: 'rev', brkAmt: 75,
  mot: { arm: 10, rel: 20, fol: 30, swing: 40, take: 50, batFol: 60 },
  records: { bestScore: 90, bestDist: 121, totalHR: 6, totalHits: 14, games: 12, totalCoins: 8000 },
  app: { tickets: 2, scoutCount: 5, league: { results: [{ res: 'win' }] }, settings: { teamName: '本番移行テスト' } },
};

function run(file, seedV1 = false) {
  const html = fs.readFileSync(file, 'utf8');
  const errors = [];
  const vc = new VirtualConsole();
  vc.on('jsdomError', (e) => errors.push(e.message));
  vc.on('error', (...args) => errors.push(args.join(' ')));
  const dom = new JSDOM(html, {
    runScripts: 'dangerously', pretendToBeVisual: true, url: 'https://phase4.test/game?debug=1', virtualConsole: vc,
    beforeParse(window) {
      if (seedV1) window.localStorage.setItem('gunya-yakyu-save-v3', JSON.stringify(fixture));
    },
  });
  const w = dom.window;
  const result = w.eval('SelfTest.run()');
  if (result.failed) throw new Error(`${file}: ${result.failures.join(' / ')}`);
  if (errors.length) throw new Error(`${file}: ${errors.join(' / ')}`);
  return { dom, w, result };
}

const dev = run('index.html');
console.log(`index.html self-test ${dev.result.passed}/${dev.result.total}`);
dev.w.eval('Shell.closeModal("ui"); Router.go("config")');
const q = (s) => dev.w.document.querySelector(s);
const click = (el) => el.dispatchEvent(new dev.w.MouseEvent('click', { bubbles: true }));
if (!q('[data-act="exportData"]') || !q('[data-act="importData"]') || !q('[data-act="importFile"]') || !q('[data-act="resetData"]')) throw new Error('設定画面のデータ管理UIが不足');
click(q('[data-act="resetData"]'));
click(q('[data-act="confirmOk"]'));
if (dev.w.eval('Shell.currentModal()') !== 'confirm' || !/本当に初期化/.test(q('#layer-modal').textContent)) throw new Error('初期化の二重確認が動かない');
dev.w.eval('Shell.closeModal("ui")');
q('#import-json').value = '{bad-json';
const beforeInvalid = dev.w.localStorage.getItem('gunya.save.v2.dev');
click(q('[data-act="importData"]'));
click(q('[data-act="confirmOk"]'));
if (dev.w.localStorage.getItem('gunya.save.v2.dev') !== beforeInvalid || !/読み込みを拒否/.test(q('#layer-toast').textContent)) throw new Error('不正JSONのUI拒否または現行セーブ保護に失敗');
console.log('data management UI and double-confirm reset OK');
dev.dom.window.close();

const prod = run('index-prod.html', true);
const w = prod.w;
const raw = w.localStorage.getItem('gunya.save.v2');
const data = JSON.parse(raw);
const backup = w.localStorage.getItem('gunya.backup.v1');
if (data.wallet.coins !== fixture.coins || data.settings.selectedPitch !== 'mypitch' || data.stats.career.hits !== 20 || !data.meta.migration.completed) throw new Error('本番v1フィクスチャの対応づけに失敗');
if (backup !== JSON.stringify(fixture)) throw new Error('本番v1原文バックアップに失敗');
if (w.localStorage.getItem('gunya-yakyu-save-v3') !== JSON.stringify(fixture)) throw new Error('本番v1移行元が変更された');
const before = w.localStorage.getItem('gunya.save.v2');
w.eval('Save.load()');
if (w.localStorage.getItem('gunya.save.v2') !== before) throw new Error('二重移行でv2が変更された');
console.log(`index-prod.html self-test ${prod.result.passed}/${prod.result.total}`);
console.log('production fixture migration, backup, and duplicate prevention OK');
prod.dom.window.close();
