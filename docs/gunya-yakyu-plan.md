# ぐにゃ野球 拡張 実装計画・ドラフト・検証チェックリスト v0.1

担当：実装・検証　／　連携先：仕様側（Claude本体）　／　2026-11-01

## 0. 前提と現状

- **現行の単一HTMLはこの作業環境にありません。** そのため既存の関数名・DOM id・localStorageのキー名は**仮の名前**で書いています（§4 の未確定点 U1〜U4）。現行HTMLが届けば、`GNY.LegacyBridge` と `GNY.Save.LEGACY` の2か所だけ直せば合わせられる作りにしてあります。
- 成果物のうち、コアのドラフト（保存・状態管理・リーグ・育成・スカウト・ミッション・ログボ・広告の順序制御）は **Node で単体テスト 17項目がすべて通過**しています。
- 画面シェル（ルーター＋各画面）は **Chromium（スマホ縦 390×844）で実際に操作して確認済み**です。旧キーからの移行、リーグ1シーズン完走、昇格、スカウト、編成、設定変更→保存→4秒広告、リロードでの復元、不明なルートからホームへの戻りを確かめ、コンソールエラーは0件でした。
- 既存の試合エンジンはまだつないでいないため、ドラフトでは試合結果を**スタブ（乱数）**で返しています。

## 1. 実装計画

### 1.1 モジュール構成（単一HTML内）

```
index.html
├─ <style>            既存CSS（触らない） ＋ 新UIトークン（.scr-*, .card, .hud, .tabs 名前空間）
├─ <div id="app">     新シェル：HUD（チーム名・コイン）／#screen／タブバー
├─ <div id="legacy-park" hidden>  既存ゲームDOM（canvas・既存メニュー）を“駐車”。ルーターが移設するだけで破棄しない
├─ <script> 既存コード（物理・球種7種・フォーム5種・モーション調整・練習・10球・3回制試合・
│           グラフィック・デモ広告・リワード・ショップ・デモ課金）……………… 原則変更しない
└─ <script> GNY 拡張（この後ろに置く。読み込み順：既存 → GNY）
     Util      乱数（シード付き）・日付・倍率変換
     Bus       モジュール間イベント（match:end / gacha:pull / ad:* / save:* / route:changed）
     Save      保存スキーマ v2・旧キー移行・欠損時の既定値補完・壊れたデータの復旧・BAK・旧キーへのミラー
     App       単一ストア（update は原子的・遅延保存300ms・flush・購読）
     LegacyBridge  ★既存コードとの唯一の接点（広告・試合起動・能力倍率注入・DOM移設）
     Economy   コインを書き換える唯一の窓口（add/spend と、update 内用の addIn/spendIn）
     AdGate    広告の順序制御（設定確定→保存→4秒広告／リワードは視聴完了時だけ付与→即保存）
     Roster    選手カードのマスタ・能力値・Lv/経験値・限界突破・編成→倍率
     Gacha     スカウト（排出率・天井・コイン消費と付与を1回の update で）
     League    3リーグ×6球団・総当たり・CPU同士の試合シミュ・順位表・昇格降格・報酬
     Missions  デイリー・実績（Bus経由で数え、既存コードは触らない）
     Login     7日周期のログインボーナス（同じ日・時計の巻き戻しでは付与しない）
     Flow      リーグ戦1試合：倍率注入 → 既存試合 → 結果反映（二重に反映しない）
     Router    hash方式・画面ライフサイクル（mount/unmount/canLeave）・不明ルートはホームへ
     Screens   home / league / match / roster / scout / shop / missions / settings / practice / challenge
```

### 1.2 既存コードを壊さないための境界ルール

| 区分 | 扱い | 既存側に入れる改修（最小） |
|---|---|---|
| 物理・ぐにゃ挙動・グラフィック | 変更しない | なし |
| 球種7種・変化カスタム・フォーム5種・モーション調整 | 変更しない。保存データは `settings.legacy` に**中身を解釈せず丸ごと**保持 | 既存の保存/読込の呼び出しを `LegacyBridge` 経由に差し替え（数行） |
| 練習・10球チャレンジ | 変更しない。ルーターが既存DOMを画面に移設するだけ | 「ホームへ戻る」動線のみ追加 |
| 3回制の試合 | エンジンは変更しない | ①開始関数が `cfg` を受け取る ②試合終了時に `done(result)` を1回呼ぶ ③物理/判定の該当箇所で `(window.__GNY_MODS?.x ?? 1)` を掛ける |
| デモ広告（4秒）・リワード広告 | 表示処理は既存のまま | 呼び出し元を `AdGate` に一本化 |
| コイン | 書き込みは `Economy` だけ | 既存のコイン加減算を `Economy.add/spend` に置換 |
| ショップ・デモ課金・プレミアムパス | 画面・ロジックは既存のまま | 解禁状態とプレミアム状態の保存先を `Save` 経由にする |

**能力倍率の不変条件：** 編成が空、または全員が平均（能力50）なら倍率はすべて 1.00 になり、現行バランスと完全に一致します。倍率は ±15%（0.85〜1.15）に収めてあり、ぐにゃ挙動の面白さを能力値が壊さないようにしています。

### 1.3 画面構成と遷移

```
タブ：ホーム ─ 試合 ─ 編成 ─ スカウト ─ ショップ
ホーム → 次の試合カード → 試合(リーグ) → [既存3回制] → 結果 → リーグ（順位表）
      → 練習モード / 10球チャレンジ（既存・全画面・タブ非表示）
      → ミッション / 設定（設定の確定ごとに 保存 → 4秒デモ広告）
試合(リーグ) → シーズン終了 → 報酬受取 → 次シーズン（昇格・残留・降格）
試合中にブラウザの戻る → 確認ダイアログ（中断した節は未消化のまま残る）
```

### 1.4 薄い縦スライスの実装順（各段階で動作を確かめられる）

| 段階 | 内容 | 終わった時点で確かめられること |
|---|---|---|
| **S0 基準取り** | 現行HTMLを変更せずに動かし、既存機能の動作を記録する（球種7種・フォーム5種・スライダー・練習・10球・試合・広告・ショップ・課金デモ）。旧キーの一覧と値の形を記録する | 回帰比較の基準になる記録。U1〜U4 が確定する |
| **S1 画面遷移** | Router・HUD・タブ・ホームを追加。既存画面は `legacy-park` から移設する。`AdGate.demo` を既存の設定広告に接続する | ホームから既存の全モードへ行って戻れる。設定変更で4秒広告が出る。**既存機能はすべて S0 と同じ** |
| **S2 試合＝リーグ** | League・Flow を追加。既存の試合に `cfg` と `done(result)` を追加。順位表・前節の結果・シーズン終了・報酬・昇格／降格 | ホーム → 試合 → 既存3回制 → 順位表に反映 → 5節でシーズン終了 → 報酬 → 次シーズン |
| **S3a 育成** | Roster（マスタ・能力・Lv・初期選手）、編成画面、`teamModifiers` を既存の物理に注入 | 編成を変えると試合中の打球・球速が変わる。空編成なら現行と同じ |
| **S3b スカウト** | Gacha（排出率の表示・天井・限界突破・コイン変換） | コインが減って選手が増える。不足時はボタンが押せない |
| **S3c ミッション・ログボ** | Missions・Login・ホームの受取件数表示 | 日付が変わるとデイリーがリセット。二重受取ができない |
| **S4 保存** | 旧キー → v2 への移行の確定、BAK・壊れたデータの退避、旧キーへのミラー、データ初期化 | 旧データを持つ端末で、コイン・解禁・設定が引き継がれる。JSONを壊しても起動できる |
| **S5 仕上げ** | 空状態・エラー状態、文言、演出（ガチャ・結果画面）、旧キーへのミラーを1版後に停止 | §3 のチェックリストをすべて通す |

各段階の終わりに §3 の該当項目と、S0 の回帰項目（R-xx）を必ず実施します。

### 1.5 デザイン方針

- 配色はナイター紺（`#0b1526`）、芝、白ライン、金（`#e8b923`）。レア度は N灰／R青／SR金／SS赤。紫のグラデーションと絵文字アイコンは使いません。
- 選手カードは、守備位置、名前、**大きな総合値**、Lv、能力値を S〜G のランクとバーと数値で見せます。投手の球速は km/h で表示します。
- 数値は等幅の数字（`tabular-nums`）に統一。順位表は NPB 形式で、勝率は引き分けを除いて計算し、ゲーム差も出します。
- タップ領域は 44〜48px 以上。safe-area に対応。HUD にコインを常時表示。

## 2. 実装ドラフト

### 2.1 保存スキーマ v2（キー `gunya.save.v2`）

```jsonc
{
  "schemaVersion": 2,
  "meta":     { "createdAt": 0, "updatedAt": 0, "migratedFrom": { "schema": 1, "keys": ["gunya_coins"], "at": 0 } },
  "wallet":   { "coins": 300, "premium": { "active": false, "raw": null }, "purchases": [] },
  "unlocks":  { "legacy": null },                        // 既存の解禁データ（中身は解釈しない）
  "settings": { "legacy": null, "app": { "sound": true, "vibration": true } }, // legacy＝球種カスタム・フォーム・モーション
  "stats":    { "legacy": null, "league": { "games":0,"wins":0,"losses":0,"draws":0,"hits":0,"homeRuns":0,"seasons":0,"titles":0 } },
  "team":     { "name": "ぐにゃーズ", "color": "#c8102e" },
  "roster":   { "cards": { "n01": { "defId":"n01","lv":1,"exp":0,"lb":0 } }, "lineup": { "batters": ["n01"], "pitcher": "p01" }, "starterGiven": true },
  "league":   { "v":1, "seasonNo":1, "divisionIndex":0, "seed":0, "teams":["player","harbor"], "cpu":[{"id":"harbor","strength":45}],
                "games":[{"id":"g0","round":0,"home":"player","away":"harbor","status":"scheduled","hs":null,"as":null}],
                "rounds":5, "round":0, "status":"active", "pending":null, "result":null },
  "missions": { "daily": { "date":"2026-11-01", "progress":{}, "claimed":[] }, "achievements": { "progress":{}, "claimed":[] } },
  "login":    { "lastClaimDate":"2026-11-01", "streak":1, "cycleDay":1, "totalDays":1 },
  "gacha":    { "pulls":0, "pity":0 }
}
```

補助キー：`gunya.save.v2.bak`（直前の1世代）、`gunya.save.corrupt`（壊れたJSONの退避先。調査用）。旧キーは**削除しません**。

**読み込みの判定順**

1. localStorage が使えない → メモリのみで起動し、「保存できません」を通知する。
2. v2 が無い → 旧キーがあれば移行（`migrated`）、無ければ新規作成（`new`）。
3. v2 が壊れている → 退避したうえで、BAK から復元（`restored-backup`）→ 無ければ旧キーから（`recovered-legacy`）→ 無ければ初期化（`reset`）。
4. マイグレーション（`MIGRATIONS[n]`）を実行する。
5. 補正（sanitize）：既定値とマージする。型が違う値は既定値に戻し、知らないキーは残す。コインは 0〜9,999,999 に収める。存在しないカードと、矛盾した編成は取り除く。リーグの構造が壊れていればリセットし、進行に矛盾があれば修復する。

### 2.2 コア（`gunya-core-draft.js` 全文）

```js
/* =====================================================================
   ぐにゃ野球 拡張コア ドラフト v0.1
   配置: 単一HTMLの最後の <script>（既存スクリプトより後）に貼る。
   方針: 既存コード（物理・球種7種・フォーム5種・モーション調整・練習・10球・
         3回制試合・広告・ショップ・デモ課金）は書き換えない。
         既存との接点は GNY.LegacyBridge の中だけに閉じ込める。
   ===================================================================== */
(function (root) {
'use strict';
const GNY = (root.GNY = root.GNY || {});

/* ---------- 0. Util ---------- */
const U = (GNY.Util = {
  isObj: (v) => v !== null && typeof v === 'object' && !Array.isArray(v),
  clone: (v) => (v === undefined ? v : JSON.parse(JSON.stringify(v))),
  clamp: (v, lo, hi) => Math.min(hi, Math.max(lo, v)),
  int: (v, d = 0) => (v !== null && v !== '' && Number.isFinite(+v) ? Math.floor(+v) : d),
  tryParse(raw) { if (raw == null) return null; try { return JSON.parse(raw); } catch (e) { return null; } },
  localDate(ts = Date.now()) {
    const d = new Date(ts);
    return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
  },
  prevDate(s) { const [y, m, d] = s.split('-').map(Number); return U.localDate(new Date(y, m - 1, d - 1).getTime()); },
  rng(seed) { // mulberry32（シード固定で再現可能）
    let a = seed >>> 0;
    return () => { a = (a + 0x6d2b79f5) >>> 0; let t = a;
      t = Math.imul(t ^ (t >>> 15), t | 1); t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
      return ((t ^ (t >>> 14)) >>> 0) / 4294967296; };
  },
  hashSeed(...parts) { let h = 2166136261; for (const ch of parts.join('|')) { h ^= ch.charCodeAt(0); h = Math.imul(h, 16777619); } return h >>> 0; },
  shuffle(arr, r) { for (let i = arr.length - 1; i > 0; i--) { const j = Math.floor(r() * (i + 1)); [arr[i], arr[j]] = [arr[j], arr[i]]; } return arr; },
  // 能力値(1-100) → 物理パラメータ倍率。中立50=1.00、幅は±15%に制限（ぐにゃ挙動の笑いを壊さない）
  mul(v) { return +(0.85 + 0.30 * (U.clamp(v, 1, 100) - 1) / 99).toFixed(3); },
});

/* ---------- 1. Bus（モジュール間イベント） ---------- */
GNY.Bus = (() => {
  const map = new Map();
  return {
    on(ev, fn) { if (!map.has(ev)) map.set(ev, new Set()); map.get(ev).add(fn); return () => map.get(ev).delete(fn); },
    emit(ev, p) { (map.get(ev) || []).forEach((fn) => { try { fn(p); } catch (e) { console.error('[Bus]', ev, e); } }); },
  };
})();

/* ---------- 2. Save（保存スキーマ・移行・復旧） ---------- */
GNY.Save = (() => {
  const KEY = 'gunya.save.v2';
  const BAK = 'gunya.save.v2.bak';      // 直前世代1つ
  const CORRUPT = 'gunya.save.corrupt'; // 壊れたJSONの退避（調査用）
  const SCHEMA_VERSION = 2;
  const COIN_MAX = 9999999;
  // 【未確定】現行HTMLの実キー名。候補を先頭から探し、最初に見つかったものを採用する。
  const LEGACY = {
    coins:    ['gunya_coins', 'coins'],
    unlocks:  ['gunya_unlocks', 'unlocks'],
    settings: ['gunya_settings', 'settings'],
    stats:    ['gunya_stats', 'stats'],
    premium:  ['gunya_premium', 'premium'],
  };
  // 移行期間中は旧キーにも書き戻す（ロールバック・既存コード直読み対策）。S4完了後1版で false。
  let MIRROR_LEGACY = true;

  const ls = {
    get(k) { try { return root.localStorage.getItem(k); } catch (e) { return undefined; } }, // undefined=利用不可
    set(k, v) { try { root.localStorage.setItem(k, v); return { ok: true }; } catch (e) { return { ok: false, error: e }; } },
  };

  function defaults() {
    const now = Date.now();
    return {
      schemaVersion: SCHEMA_VERSION,
      meta: { createdAt: now, updatedAt: now, migratedFrom: null },
      wallet: { coins: 300 /*【未確定】現行の初期コイン*/, premium: { active: false, raw: null }, purchases: [] },
      // 既存機能の保存物は「中身を解釈せず丸ごと保持」する。解釈は既存コードだけが行う。
      unlocks: { legacy: null },
      settings: { legacy: null, app: { sound: true, vibration: true } },
      stats: { legacy: null, league: { games: 0, wins: 0, losses: 0, draws: 0, hits: 0, homeRuns: 0, seasons: 0, titles: 0 } },
      team: { name: 'ぐにゃーズ', color: '#c8102e' },
      roster: { cards: {}, lineup: { batters: [], pitcher: null }, starterGiven: false },
      league: null, // 初回の「試合」タブ表示時に生成
      missions: { daily: { date: null, progress: {}, claimed: [] }, achievements: { progress: {}, claimed: [] } },
      login: { lastClaimDate: null, streak: 0, cycleDay: 0, totalDays: 0 },
      gacha: { pulls: 0, pity: 0 },
    };
  }

  // 既定値でマージ：型が違う値は既定値に戻す。未知キーは保持（新しい版のデータを消さない）。
  function mergeDefaults(def, val) {
    if (def === null) return val === undefined ? null : val; // null既定 = 自由形式（legacy等）
    if (Array.isArray(def)) return Array.isArray(val) ? val : U.clone(def);
    if (U.isObj(def)) {
      if (!U.isObj(val)) return U.clone(def);
      const out = {};
      for (const k of Object.keys(def)) out[k] = mergeDefaults(def[k], val[k]);
      for (const k of Object.keys(val)) if (!(k in def)) out[k] = val[k];
      return out;
    }
    if (typeof def === 'number') return typeof val === 'number' && Number.isFinite(val) ? val : def;
    return typeof val === typeof def ? val : def;
  }

  function readLegacy() {
    const found = {};
    for (const [sec, keys] of Object.entries(LEGACY)) {
      for (const k of keys) {
        const raw = ls.get(k);
        if (raw != null) { const p = U.tryParse(raw); found[sec] = { key: k, value: p === null && raw !== 'null' ? raw : p }; break; }
      }
    }
    return Object.keys(found).length ? found : null;
  }

  function fromLegacy(found) {
    const d = defaults();
    if (found.coins) {
      const v = found.coins.value;
      d.wallet.coins = U.int(U.isObj(v) ? v.coins : v, d.wallet.coins);
    }
    if (found.unlocks) d.unlocks.legacy = found.unlocks.value;
    if (found.settings) d.settings.legacy = found.settings.value;
    if (found.stats) d.stats.legacy = found.stats.value;
    if (found.premium) {
      const v = found.premium.value;
      d.wallet.premium = { active: v === true || v === 'true' || v === 1 || (U.isObj(v) && !!v.active), raw: v };
    }
    d.meta.migratedFrom = { schema: 1, keys: Object.values(found).map((f) => f.key), at: Date.now() };
    return d;
  }

  // 将来の版上げはここに追加：MIGRATIONS[n] は (n-1) → n
  const MIGRATIONS = {
    // 3: (d) => { d.newSection = {...}; return d; },
  };
  function runMigrations(d) {
    let v = U.int(d.schemaVersion, 1);
    if (v < 2) v = 2; // v1 = 旧キー群。fromLegacy で v2 化済み
    while (v < SCHEMA_VERSION) { v++; if (MIGRATIONS[v]) d = MIGRATIONS[v](d); }
    d.schemaVersion = Math.max(v, U.int(d.schemaVersion, v));
    return d;
  }

  function sanitize(d, report) {
    d = mergeDefaults(defaults(), d);
    const c = U.clamp(U.int(d.wallet.coins, 0), 0, COIN_MAX);
    if (c !== d.wallet.coins) { report.repaired.push('wallet.coins'); d.wallet.coins = c; }
    if (GNY.Roster) { const r = GNY.Roster.sanitize(d.roster); if (r.changed) report.repaired.push('roster'); d.roster = r.roster; }
    if (d.league !== null) {
      if (!GNY.League || !GNY.League.validate(d.league)) { report.repaired.push('league(reset)'); d.league = null; }
      else if (GNY.League.repair(d.league)) report.repaired.push('league(repaired)');
    }
    return d;
  }

  function load() {
    const report = { status: 'ok', repaired: [], notes: [] };
    const raw = ls.get(KEY);
    let data;
    if (raw === undefined) { report.status = 'storage-unavailable'; return { data: sanitize(defaults(), report), report }; }
    if (raw === null) {
      const legacy = readLegacy();
      data = legacy ? fromLegacy(legacy) : defaults();
      report.status = legacy ? 'migrated' : 'new';
    } else {
      data = U.tryParse(raw);
      if (!U.isObj(data)) {
        ls.set(CORRUPT, JSON.stringify({ at: Date.now(), raw: String(raw).slice(0, 200000) }));
        const bak = U.tryParse(ls.get(BAK));
        if (U.isObj(bak)) { data = bak; report.status = 'restored-backup'; }
        else { const legacy = readLegacy(); data = legacy ? fromLegacy(legacy) : defaults(); report.status = legacy ? 'recovered-legacy' : 'reset'; }
      }
    }
    if (U.int(data.schemaVersion, 0) > SCHEMA_VERSION) report.notes.push('newer-schema'); // 新しい版のデータ：未知キーは保持
    data = sanitize(runMigrations(data), report);
    return { data, report };
  }

  function writeLegacyMirror(d) {
    ls.set(LEGACY.coins[0], String(d.wallet.coins));
    if (d.unlocks.legacy !== null) ls.set(LEGACY.unlocks[0], JSON.stringify(d.unlocks.legacy));
    if (d.settings.legacy !== null) ls.set(LEGACY.settings[0], JSON.stringify(d.settings.legacy));
    if (d.stats.legacy !== null) ls.set(LEGACY.stats[0], JSON.stringify(d.stats.legacy));
    if (d.wallet.premium.raw !== null) ls.set(LEGACY.premium[0], JSON.stringify(d.wallet.premium.raw));
  }

  function write(d) {
    const prev = ls.get(KEY);
    if (prev) ls.set(BAK, prev);
    const r = ls.set(KEY, JSON.stringify(d));
    if (!r.ok) GNY.Bus.emit('save:error', { error: r.error });
    if (r.ok && MIRROR_LEGACY) writeLegacyMirror(d);
    return r.ok;
  }

  function resetAll() { // 設定画面の「データ初期化」。旧キーは残す（再移行されないよう v2 を初期値で上書き）
    const d = defaults(); d.meta.migratedFrom = { schema: 'reset', at: Date.now() }; write(d); return d;
  }

  return { KEY, BAK, CORRUPT, SCHEMA_VERSION, LEGACY, defaults, load, write, resetAll, mergeDefaults,
    setMirror(v) { MIRROR_LEGACY = !!v; } };
})();

/* ---------- 3. App（単一ストア＋購読＋遅延保存） ---------- */
GNY.App = (() => {
  let state = null; const subs = new Set(); let timer = null; let lastReport = null;
  function init() {
    const { data, report } = GNY.Save.load();
    state = data; lastReport = report;
    if (report.status !== 'ok' || report.repaired.length) GNY.Bus.emit('save:report', report);
    GNY.Roster.ensureStarter(state);
    if (root.addEventListener) {
      root.addEventListener('pagehide', flush);
      root.document && root.document.addEventListener('visibilitychange', () => { if (root.document.hidden) flush(); else GNY.Bus.emit('app:resume'); });
    }
    flush();
    return report;
  }
  const get = () => state;
  // 変更は必ず update 経由。mutator は draft を直接書き換える。例外時は state を変えない（原子性）
  function update(mutator, opt = {}) {
    const draft = U.clone(state);
    const ret = mutator(draft);
    draft.meta.updatedAt = Date.now();
    state = draft;
    subs.forEach((fn) => { try { fn(state); } catch (e) { console.error(e); } });
    if (opt.flush) flush(); else if (opt.save !== false) { clearTimeout(timer); timer = setTimeout(flush, 300); }
    return ret;
  }
  function flush() { clearTimeout(timer); timer = null; if (state) GNY.Save.write(state); }
  function subscribe(fn) { subs.add(fn); return () => subs.delete(fn); }
  function _setForTest(s) { state = s; }
  return { init, get, update, flush, subscribe, report: () => lastReport, _setForTest };
})();

/* ---------- 4. LegacyBridge（既存コードとの唯一の接点） ----------
   ここ以外から既存のグローバル関数・既存DOMを触らない。
   【未確定】右辺の既存関数名は現行HTMLを読んで確定させる。 */
GNY.LegacyBridge = {
  // 既存の4秒デモ広告。閉じたら done()
  showDemoAd(ms, done) {
    if (typeof root.showDemoAd === 'function') return root.showDemoAd(ms, done);
    setTimeout(done, ms); // 既存未接続時のフォールバック（テスト用）
  },
  // 既存リワード広告。done(completed:boolean)
  showRewardAd(done) {
    if (typeof root.showRewardAd === 'function') return root.showRewardAd(done);
    setTimeout(() => done(true), 10);
  },
  // 既存3回制試合を起動し結果を返す。既存の試合終了処理に「結果を返す1行」を足すのが唯一の改修点。
  // 結果: { runsFor, runsAgainst, aborted, stats:{ atBats, hits, homeRuns, strikeouts, pitches } }
  startMatch(cfg) {
    if (typeof root.startGameMatch === 'function') return new Promise((res) => root.startGameMatch(cfg, res));
    const r = U.rng(U.hashSeed('stub', Date.now()));
    return Promise.resolve({ runsFor: Math.floor(r() * 5), runsAgainst: Math.floor(r() * 5), aborted: false,
      stats: { atBats: 9, hits: Math.floor(r() * 5), homeRuns: Math.floor(r() * 2), strikeouts: 2, pitches: 40 } });
  },
  // 能力倍率の注入。中立(全1.0)なら既存バランスと完全一致すること。
  applyModifiers(mods) { root.__GNY_MODS = Object.freeze({ ...mods }); },
  clearModifiers() { root.__GNY_MODS = null; },
  // 既存の画面要素（canvas等）を新ルーターの画面に“移設”する。破棄はしない。
  legacyElement(id) { return root.document ? root.document.getElementById(id) : null; },
};

/* ---------- 5. Economy（コイン唯一の書き手） ---------- */
GNY.Economy = (() => {
  const MAX = 9999999;
  return {
    coins: () => GNY.App.get().wallet.coins,
    add(n, reason) {
      n = U.int(n); if (n <= 0) return 0;
      GNY.App.update((s) => { s.wallet.coins = Math.min(MAX, s.wallet.coins + n); }, { flush: true });
      GNY.Bus.emit('coins:changed', { delta: n, reason });
      return n;
    },
    spend(n, reason) {
      n = U.int(n); if (n <= 0) return false;
      const ok = GNY.App.update((s) => { if (s.wallet.coins < n) return false; s.wallet.coins -= n; return true; }, { flush: true });
      if (ok) GNY.Bus.emit('coins:changed', { delta: -n, reason });
      return ok;
    },
    // 同一 update 内で使う版（ガチャ等で「消費＋付与」を原子的に）
    spendIn(s, n) { if (s.wallet.coins < n) return false; s.wallet.coins -= n; return true; },
    addIn(s, n) { s.wallet.coins = Math.min(MAX, s.wallet.coins + U.int(n)); },
  };
})();

/* ---------- 6. AdGate（広告の順序制御） ---------- */
GNY.AdGate = (() => {
  let chain = Promise.resolve(); let busy = false;
  // 設定変更 → 保存(flush) → 4秒デモ広告。表示中/待機中の重複要求は1回にまとめる。
  function demo(reason) {
    if (busy) return chain;
    busy = true;
    chain = chain.then(() => new Promise((res) => {
      GNY.Bus.emit('ad:start', { type: 'demo', reason });
      GNY.LegacyBridge.showDemoAd(4000, () => { busy = false; GNY.Bus.emit('ad:end', { type: 'demo', reason }); res(); });
    }));
    return chain;
  }
  // リワード：視聴完了時のみ grant(draft) を同一 update で実行→即保存
  function rewarded(reason, grant) {
    return new Promise((res) => {
      GNY.Bus.emit('ad:start', { type: 'reward', reason });
      GNY.LegacyBridge.showRewardAd((completed) => {
        if (completed) { GNY.App.update((s) => grant(s), { flush: true }); GNY.Bus.emit('ad:reward', { reason }); }
        GNY.Bus.emit('ad:end', { type: 'reward', reason, completed });
        res(!!completed);
      });
    });
  }
  async function commitSetting(mutator, reason = 'settings') {
    GNY.App.update((s) => mutator(s.settings), { flush: true }); // 先に保存（広告中に閉じても失われない）
    GNY.Bus.emit('settings:changed', { reason });
    await demo(reason);
  }
  return { demo, rewarded, commitSetting, isBusy: () => busy };
})();

/* ---------- 7. Roster（選手カード・育成・能力反映） ---------- */
GNY.Roster = (() => {
  const RARITY = { N: { maxLv: 30, w: 60 }, R: { maxLv: 40, w: 30 }, SR: { maxLv: 50, w: 9 }, SS: { maxLv: 60, w: 1 } };
  const LB_MAX = 4, LB_LV = 5;
  // 野手: [id, 名前, 守備, レア, ミート, パワー, 走力, 肩力, 守備力]
  // 投手: [id, 名前, 'P', レア, 球速km/h, 制球, スタミナ, 変化]
  const ROWS = [
    ['n01', '岩田 一平', 'C', 'N', 42, 40, 35, 50, 48], ['n02', '佐倉 健', '1B', 'N', 45, 52, 30, 38, 40],
    ['n03', '戸部 翔太', '2B', 'N', 48, 32, 55, 42, 50], ['n04', '三輪 大地', '3B', 'N', 40, 50, 38, 52, 44],
    ['n05', '早瀬 隼', 'SS', 'N', 46, 34, 58, 48, 52], ['n06', '北見 遼', 'LF', 'N', 50, 42, 45, 40, 38],
    ['n07', '須藤 光', 'CF', 'N', 44, 36, 60, 46, 50], ['n08', '江口 豪', 'RF', 'N', 41, 54, 40, 56, 40],
    ['n09', '小野 拓海', 'SS', 'N', 47, 30, 52, 44, 47], ['p01', '谷口 誠', 'P', 'N', 138, 45, 50, 42],
    ['r01', '桐生 蓮', 'CF', 'R', 60, 48, 70, 58, 62], ['r02', '大門 剛', '1B', 'R', 55, 68, 35, 45, 48],
    ['rp1', '宮城 颯', 'P', 'R', 145, 55, 58, 52],
    ['s01', '神崎 怜', 'SS', 'SR', 72, 60, 75, 70, 78], ['sp1', '真壁 修', 'P', 'SR', 151, 66, 64, 70],
    ['x01', '轟 豪太郎', 'RF', 'SS', 82, 92, 60, 85, 70], ['xp1', '氷室 迅', 'P', 'SS', 158, 78, 75, 84],
  ];
  const DEFS = {};
  for (const r of ROWS) {
    const [id, name, pos, rarity, a, b, c, d, e] = r;
    DEFS[id] = pos === 'P'
      ? { id, name, pos, rarity, base: { velocity: a, control: b, stamina: c, breaking: d } }
      : { id, name, pos, rarity, base: { contact: a, power: b, speed: c, arm: d, field: e } };
  }
  const STARTER = ['n01', 'n02', 'n03', 'n04', 'n05', 'n06', 'n07', 'n08', 'n09', 'p01'];
  const maxLv = (c) => RARITY[DEFS[c.defId].rarity].maxLv + c.lb * LB_LV;
  const expToNext = (lv) => 20 + lv * 10;

  function stats(card) {
    const def = DEFS[card.defId]; const g = card.lv - 1; const out = {};
    for (const [k, v] of Object.entries(def.base)) out[k] = k === 'velocity' ? Math.min(165, v + Math.floor(g * 0.25)) : Math.min(99, v + Math.floor(g * 0.6));
    return out;
  }
  function overall(card) {
    const s = stats(card);
    if (DEFS[card.defId].pos === 'P') return Math.round(((s.velocity - 120) * 2 * 1.2 + s.control * 1.1 + s.stamina * 0.6 + s.breaking * 1.1) / 4);
    return Math.round((s.contact * 1.2 + s.power * 1.2 + s.speed * 0.8 + s.arm * 0.4 + s.field * 0.4) / 4);
  }
  const grade = (v) => (v >= 90 ? 'S' : v >= 80 ? 'A' : v >= 70 ? 'B' : v >= 60 ? 'C' : v >= 50 ? 'D' : v >= 40 ? 'E' : v >= 20 ? 'F' : 'G');

  function sanitize(roster) {
    let changed = false; const cards = {};
    for (const [id, c] of Object.entries(roster.cards || {})) {
      if (!DEFS[id] || !U.isObj(c)) { changed = true; continue; }
      const card = { defId: id, lv: U.int(c.lv, 1), exp: Math.max(0, U.int(c.exp, 0)), lb: U.clamp(U.int(c.lb, 0), 0, LB_MAX) };
      const lv = U.clamp(card.lv, 1, maxLv(card)); if (lv !== c.lv) changed = true; card.lv = lv;
      cards[id] = card;
    }
    const seen = new Set();
    const batters = (roster.lineup.batters || []).filter((id) => cards[id] && DEFS[id].pos !== 'P' && !seen.has(id) && seen.add(id)).slice(0, 9);
    const pitcher = cards[roster.lineup.pitcher] && DEFS[roster.lineup.pitcher].pos === 'P' ? roster.lineup.pitcher : null;
    if (batters.length !== (roster.lineup.batters || []).length || pitcher !== roster.lineup.pitcher) changed = true;
    return { changed, roster: { ...roster, cards, lineup: { batters, pitcher } } };
  }
  function ensureStarter(s) {
    if (s.roster.starterGiven) return;
    GNY.App.update((d) => {
      for (const id of STARTER) if (!d.roster.cards[id]) d.roster.cards[id] = { defId: id, lv: 1, exp: 0, lb: 0 };
      d.roster.lineup = { batters: STARTER.filter((id) => DEFS[id].pos !== 'P'), pitcher: 'p01' };
      d.roster.starterGiven = true;
    }, { flush: true });
  }
  // 入手：重複は限界突破、上限超過はコイン変換
  function addCardIn(s, id) {
    const c = s.roster.cards[id];
    if (!c) { s.roster.cards[id] = { defId: id, lv: 1, exp: 0, lb: 0 }; return { id, kind: 'new' }; }
    if (c.lb < LB_MAX) { c.lb++; return { id, kind: 'limitbreak', lb: c.lb }; }
    const coin = { N: 10, R: 30, SR: 100, SS: 300 }[DEFS[id].rarity]; GNY.Economy.addIn(s, coin);
    return { id, kind: 'coin', coin };
  }
  function giveExpIn(s, id, exp) {
    const c = s.roster.cards[id]; if (!c) return null;
    c.exp += exp; let ups = 0;
    while (c.lv < maxLv(c) && c.exp >= expToNext(c.lv)) { c.exp -= expToNext(c.lv); c.lv++; ups++; }
    if (c.lv >= maxLv(c)) c.exp = 0;
    return ups;
  }
  // 編成 → 試合倍率。編成が空なら全1.0（＝現行バランスそのまま）
  function teamModifiers(s) {
    const b = s.roster.lineup.batters.map((id) => s.roster.cards[id]).filter(Boolean).map(stats);
    const p = s.roster.lineup.pitcher && s.roster.cards[s.roster.lineup.pitcher] ? stats(s.roster.cards[s.roster.lineup.pitcher]) : null;
    const avg = (k) => b.reduce((t, x) => t + x[k], 0) / b.length;
    return {
      batContact: b.length ? U.mul(avg('contact')) : 1, batPower: b.length ? U.mul(avg('power')) : 1,
      runSpeed: b.length ? U.mul(avg('speed')) : 1,
      pitchVelocity: p ? U.mul((p.velocity - 125) * 2.5) : 1,  // 125→1, 165→100
      pitchControl: p ? U.mul(p.control) : 1, breakAmount: p ? U.mul(p.breaking) : 1,
    };
  }
  function teamStrength(s) {
    const ids = [...s.roster.lineup.batters, s.roster.lineup.pitcher].filter((id) => id && s.roster.cards[id]);
    if (!ids.length) return 50;
    return Math.round(ids.reduce((t, id) => t + overall(s.roster.cards[id]), 0) / ids.length);
  }
  return { DEFS, RARITY, STARTER, stats, overall, grade, sanitize, ensureStarter, addCardIn, giveExpIn, teamModifiers, teamStrength, maxLv, expToNext };
})();

/* ---------- 8. Gacha（スカウト） ---------- */
GNY.Gacha = (() => {
  const COST = { 1: 100, 10: 900 }; const PITY = 50;
  function pool(rarity) { return Object.values(GNY.Roster.DEFS).filter((d) => d.rarity === rarity).map((d) => d.id); }
  function rollRarity(r) { const t = r() * 100; return t < 1 ? 'SS' : t < 10 ? 'SR' : t < 40 ? 'R' : 'N'; }
  // 戻り値: null=コイン不足 / 結果配列。消費・付与・天井更新は1つの update で原子的
  function pull(n, seed = Date.now()) {
    if (!COST[n]) throw new Error('bad-count');
    const r = U.rng(U.hashSeed('gacha', seed));
    const res = GNY.App.update((s) => {
      if (!GNY.Economy.spendIn(s, COST[n])) return null;
      const out = [];
      for (let i = 0; i < n; i++) {
        let rar = rollRarity(r);
        s.gacha.pity++;
        if (s.gacha.pity >= PITY) rar = 'SS';
        if (rar === 'SS') s.gacha.pity = 0;
        const p = pool(rar); out.push({ rarity: rar, ...GNY.Roster.addCardIn(s, p[Math.floor(r() * p.length)]) });
      }
      s.gacha.pulls += n;
      return out;
    }, { flush: true });
    if (res) GNY.Bus.emit('gacha:pull', { n, res });
    return res;
  }
  return { COST, PITY, pull, rates: { SS: 1, SR: 9, R: 30, N: 60 } };
})();

/* ---------- 9. League（リーグ進行） ---------- */
GNY.League = (() => {
  const CFG = { teams: 6, legs: 1, baseRuns: 1.6, homeAdv: 1.05, promote: 2, relegate: 1 };
  const DIVISIONS = [
    { id: 'd3', name: 'ルーキーリーグ', base: 40, rewardMul: 1.0 },
    { id: 'd2', name: 'セカンドリーグ', base: 55, rewardMul: 1.5 },
    { id: 'd1', name: 'トップリーグ', base: 70, rewardMul: 2.0 },
  ];
  const RANK_REWARD = [500, 300, 200, 120, 80, 50];
  const CPU = { // 架空球団（実在球団名・略称は使わない）
    harbor: { name: '港町ドルフィンズ', short: '港', color: '#1d4f91', bias: 2 },
    north:  { name: '北嶺グリズリーズ', short: '嶺', color: '#2f4f3a', bias: 4 },
    dune:   { name: '砂丘ファルコンズ', short: '砂', color: '#b5651d', bias: 0 },
    bridge: { name: '鉄橋スティーラーズ', short: '鉄', color: '#3c3c3c', bias: 3 },
    lake:   { name: '湖畔ヘロンズ', short: '湖', color: '#4a90a4', bias: -2 },
    ridge:  { name: '山脈ゴーツ', short: '山', color: '#6b4e2e', bias: 1 },
    night:  { name: '夜光オウルズ', short: '夜', color: '#1b1f3b', bias: -1 },
    cape:   { name: '灯台シーガルズ', short: '灯', color: '#d1a400', bias: -3 },
  };

  function roundRobin(ids, legs) {
    const t = ids.slice(); if (t.length % 2) t.push(null);
    const n = t.length, per = n - 1, games = []; let gid = 0;
    for (let leg = 0; leg < legs; leg++) {
      const arr = t.slice();
      for (let rd = 0; rd < per; rd++) {
        for (let i = 0; i < n / 2; i++) {
          const a = arr[i], b = arr[n - 1 - i];
          if (a && b) {
            const flip = (rd + i + leg) % 2 === 1;
            games.push({ id: 'g' + gid++, round: leg * per + rd, home: flip ? b : a, away: flip ? a : b, status: 'scheduled', hs: null, as: null });
          }
        }
        arr.splice(1, 0, arr.pop());
      }
    }
    return games;
  }

  function create({ seasonNo = 1, divisionIndex = 0, seed } = {}) {
    seed = seed == null ? U.hashSeed('season', seasonNo, Date.now()) : seed >>> 0;
    const r = U.rng(seed);
    const div = DIVISIONS[U.clamp(divisionIndex, 0, DIVISIONS.length - 1)];
    const ids = U.shuffle(Object.keys(CPU), r).slice(0, CFG.teams - 1);
    const cpu = ids.map((id) => ({ id, strength: U.clamp(Math.round(div.base + CPU[id].bias + (r() * 10 - 5)), 20, 95) }));
    const teams = ['player', ...ids];
    const games = roundRobin(teams, CFG.legs);
    return { v: 1, seasonNo, divisionIndex: DIVISIONS.indexOf(div), seed, teams, cpu, games,
      rounds: Math.max(...games.map((g) => g.round)) + 1, round: 0, status: 'active', pending: null, result: null };
  }

  function validate(lg) {
    try {
      if (!U.isObj(lg) || lg.v !== 1 || !Array.isArray(lg.games) || !Array.isArray(lg.teams) || !Array.isArray(lg.cpu)) return false;
      if (!lg.teams.includes('player') || !DIVISIONS[lg.divisionIndex]) return false;
      if (!lg.cpu.every((c) => CPU[c.id] && Number.isFinite(c.strength))) return false;
      const set = new Set(lg.teams);
      return lg.games.every((g) => set.has(g.home) && set.has(g.away) && Number.isInteger(g.round) && ['scheduled', 'final'].includes(g.status)
        && (g.status === 'scheduled' || (Number.isInteger(g.hs) && Number.isInteger(g.as))));
    } catch (e) { return false; }
  }
  // 整合性の自己修復：round は「未消化の最小ラウンド」から再計算。戻り値=修正したか
  function repair(lg) {
    const before = JSON.stringify([lg.round, lg.status, lg.pending]);
    const open = lg.games.filter((g) => g.status === 'scheduled').map((g) => g.round);
    lg.rounds = Math.max(...lg.games.map((g) => g.round)) + 1;
    lg.round = open.length ? Math.min(...open) : lg.rounds;
    lg.pending = null; // 試合途中で終了した場合は未消化扱い（再挑戦可）【未確定】敗戦扱いにするか
    if (lg.round >= lg.rounds && lg.status === 'active') finish(lg);
    return before !== JSON.stringify([lg.round, lg.status, lg.pending]);
  }

  const team = (lg, id) => id === 'player'
    ? { id, name: GNY.App.get() ? GNY.App.get().team.name : 'ぐにゃーズ', strength: GNY.App.get() ? GNY.Roster.teamStrength(GNY.App.get()) : 50, color: '#c8102e' }
    : { id, ...CPU[id], strength: lg.cpu.find((c) => c.id === id).strength };
  const playerGame = (lg) => lg.games.find((g) => g.round === lg.round && g.status === 'scheduled' && (g.home === 'player' || g.away === 'player')) || null;

  function poisson(lambda, r) { const L = Math.exp(-lambda); let k = 0, p = 1; do { k++; p *= r(); } while (p > L); return Math.min(k - 1, 20); }
  function simulateRound(lg, round) {
    for (const g of lg.games) {
      if (g.round !== round || g.status !== 'scheduled' || g.home === 'player' || g.away === 'player') continue;
      const r = U.rng(U.hashSeed(lg.seed, g.id));
      const h = team(lg, g.home).strength, a = team(lg, g.away).strength;
      g.hs = poisson(CFG.baseRuns * (h / a) * CFG.homeAdv, r);
      g.as = poisson(CFG.baseRuns * (a / h), r);
      g.status = 'final';
    }
  }

  function applyPlayerResult(lg, res) {
    if (lg.status !== 'active') throw new Error('season-not-active');
    const g = playerGame(lg); if (!g) throw new Error('no-player-game');
    const rf = U.clamp(U.int(res.runsFor), 0, 99), ra = U.clamp(U.int(res.runsAgainst), 0, 99);
    if (g.home === 'player') { g.hs = rf; g.as = ra; } else { g.hs = ra; g.as = rf; }
    g.status = 'final';
    simulateRound(lg, lg.round);
    lg.round++;
    if (lg.round >= lg.rounds) finish(lg);
    return { game: g, outcome: rf > ra ? 'W' : rf < ra ? 'L' : 'D' };
  }

  function standings(lg) {
    const rows = new Map(lg.teams.map((id) => [id, { id, g: 0, w: 0, l: 0, d: 0, rf: 0, ra: 0 }]));
    for (const g of lg.games) {
      if (g.status !== 'final') continue;
      const h = rows.get(g.home), a = rows.get(g.away);
      h.g++; a.g++; h.rf += g.hs; h.ra += g.as; a.rf += g.as; a.ra += g.hs;
      if (g.hs > g.as) { h.w++; a.l++; } else if (g.hs < g.as) { a.w++; h.l++; } else { h.d++; a.d++; }
    }
    const list = [...rows.values()].map((r) => ({ ...r, pct: r.w + r.l ? r.w / (r.w + r.l) : 0, diff: r.rf - r.ra }));
    // 勝率（引分除外）→ 勝数 → 得失点差 → 総得点 → ID（安定ソート用）
    list.sort((x, y) => y.pct - x.pct || y.w - x.w || y.diff - x.diff || y.rf - x.rf || (x.id < y.id ? -1 : 1));
    const top = list[0];
    return list.map((r, i) => ({ ...r, rank: i + 1, gb: i === 0 ? 0 : ((top.w - r.w) + (r.l - top.l)) / 2 }));
  }

  function finish(lg) {
    const rank = standings(lg).find((r) => r.id === 'player').rank;
    const div = DIVISIONS[lg.divisionIndex];
    let move = 0;
    if (rank <= CFG.promote && lg.divisionIndex < DIVISIONS.length - 1) move = 1;
    else if (rank > CFG.teams - CFG.relegate && lg.divisionIndex > 0) move = -1;
    lg.status = 'finished';
    lg.result = { rank, reward: Math.round(RANK_REWARD[rank - 1] * div.rewardMul), move, claimed: false };
  }
  // 報酬受取（冪等）→ 次シーズン生成。1回の update で行う
  function claimAndNext(s) {
    const lg = s.league; if (!lg || lg.status !== 'finished') return null;
    const r = lg.result;
    if (!r.claimed) {
      GNY.Economy.addIn(s, r.reward); r.claimed = true;
      s.stats.league.seasons++; if (r.rank === 1) s.stats.league.titles++;
    }
    s.league = create({ seasonNo: lg.seasonNo + 1, divisionIndex: lg.divisionIndex + r.move });
    return r;
  }
  // 相手の強さ → CPU側の倍率（既存のCPU投球/打撃に掛ける）
  const opponentModifiers = (strength) => ({ cpuPitchVelocity: U.mul(strength), cpuBatContact: U.mul(strength) });

  return { CFG, DIVISIONS, CPU, RANK_REWARD, create, validate, repair, team, playerGame, applyPlayerResult, standings, finish, claimAndNext, opponentModifiers, roundRobin };
})();

/* ---------- 10. Missions / Login ---------- */
GNY.Missions = (() => {
  const DAILY = [
    { id: 'd_play', label: 'リーグ戦を1試合プレイ', ev: 'match', goal: 1, reward: 50 },
    { id: 'd_hit', label: '試合でヒットを3本', ev: 'hits', goal: 3, reward: 50 },
    { id: 'd_scout', label: 'スカウトを1回', ev: 'scout', goal: 1, reward: 30 },
    { id: 'd_ad', label: 'リワード広告を1回視聴', ev: 'reward_ad', goal: 1, reward: 30 },
  ];
  const ACHV = [
    { id: 'a_win10', label: 'リーグ戦通算10勝', ev: 'win', goal: 10, reward: 300 },
    { id: 'a_hr10', label: '試合で通算10本塁打', ev: 'hr', goal: 10, reward: 300 },
    { id: 'a_title', label: 'リーグ優勝', ev: 'title', goal: 1, reward: 1000 },
  ];
  function rollover(s, now = Date.now()) {
    const today = U.localDate(now);
    if (s.missions.daily.date !== today) s.missions.daily = { date: today, progress: {}, claimed: [] };
  }
  function trackIn(s, ev, n = 1, now) {
    rollover(s, now);
    for (const [list, box] of [[DAILY, s.missions.daily], [ACHV, s.missions.achievements]])
      for (const m of list) if (m.ev === ev) box.progress[m.id] = Math.min(m.goal, (box.progress[m.id] || 0) + n);
  }
  function claim(id) {
    return GNY.App.update((s) => {
      rollover(s);
      const isDaily = DAILY.some((m) => m.id === id);
      const def = (isDaily ? DAILY : ACHV).find((m) => m.id === id); if (!def) return 0;
      const box = isDaily ? s.missions.daily : s.missions.achievements;
      if (box.claimed.includes(id) || (box.progress[id] || 0) < def.goal) return 0;
      box.claimed.push(id); GNY.Economy.addIn(s, def.reward); return def.reward;
    }, { flush: true });
  }
  function view(s) {
    const d = s.missions.daily.date === U.localDate() ? s.missions.daily : { progress: {}, claimed: [] };
    const map = (list, box) => list.map((m) => ({ ...m, progress: box.progress[m.id] || 0, claimed: box.claimed.includes(m.id) }));
    return { daily: map(DAILY, d), achievements: map(ACHV, s.missions.achievements) };
  }
  return { DAILY, ACHV, trackIn, claim, view, rollover };
})();

GNY.Login = (() => {
  const REWARDS = [50, 50, 80, 80, 100, 150, 300]; // 7日周期
  // 戻り値: null=受取済/時計巻き戻し、{day, coins, streak}
  function claimIn(s, now = Date.now()) {
    const today = U.localDate(now); const L = s.login;
    if (L.lastClaimDate && today <= L.lastClaimDate) return null;
    L.streak = L.lastClaimDate === U.prevDate(today) ? L.streak + 1 : 1;
    L.cycleDay = (L.cycleDay % REWARDS.length) + 1;
    L.totalDays++; L.lastClaimDate = today;
    const coins = REWARDS[L.cycleDay - 1]; GNY.Economy.addIn(s, coins);
    return { day: L.cycleDay, coins, streak: L.streak };
  }
  const claim = (now) => GNY.App.update((s) => claimIn(s, now), { flush: true });
  return { REWARDS, claimIn, claim };
})();

/* ---------- 11. Flow（試合の流れ：リーグ→既存試合→結果反映） ---------- */
GNY.Flow = {
  async playLeagueMatch() {
    const s0 = GNY.App.get();
    if (!s0.league) GNY.App.update((s) => { s.league = GNY.League.create(); }, { flush: true });
    const lg = GNY.App.get().league;
    if (lg.status !== 'active') return { error: 'season-finished' };
    const g = GNY.League.playerGame(lg); if (!g) return { error: 'no-game' };
    const opp = GNY.League.team(lg, g.home === 'player' ? g.away : g.home);
    const token = `${lg.seasonNo}:${g.id}`;
    GNY.App.update((s) => { s.league.pending = token; }, { flush: true });
    GNY.LegacyBridge.applyModifiers({ ...GNY.Roster.teamModifiers(GNY.App.get()), ...GNY.League.opponentModifiers(opp.strength) });
    let res;
    try { res = await GNY.LegacyBridge.startMatch({ innings: 3, playerIsHome: g.home === 'player', opponent: { name: opp.name, short: opp.short, color: opp.color } }); }
    finally { GNY.LegacyBridge.clearModifiers(); }
    if (!res || res.aborted) { GNY.App.update((s) => { if (s.league) s.league.pending = null; }, { flush: true }); return { aborted: true }; }
    const out = GNY.App.update((s) => {
      if (!s.league || s.league.pending !== token) return null; // 二重適用防止
      const r = GNY.League.applyPlayerResult(s.league, res);
      s.league.pending = null;
      const st = s.stats.league, x = res.stats || {};
      st.games++; st[{ W: 'wins', L: 'losses', D: 'draws' }[r.outcome]]++;
      st.hits += U.int(x.hits); st.homeRuns += U.int(x.homeRuns);
      GNY.Missions.trackIn(s, 'match', 1); GNY.Missions.trackIn(s, 'hits', U.int(x.hits)); GNY.Missions.trackIn(s, 'hr', U.int(x.homeRuns));
      if (r.outcome === 'W') GNY.Missions.trackIn(s, 'win', 1);
      if (s.league.status === 'finished' && s.league.result.rank === 1) GNY.Missions.trackIn(s, 'title', 1);
      // 出場選手に経験値（勝利30/引分20/敗戦15）
      const exp = { W: 30, D: 20, L: 15 }[r.outcome];
      const ids = [...s.roster.lineup.batters, s.roster.lineup.pitcher].filter(Boolean);
      const lvUps = ids.map((id) => [id, GNY.Roster.giveExpIn(s, id, exp)]).filter(([, n]) => n > 0);
      return { ...r, exp, lvUps };
    }, { flush: true });
    if (out) GNY.Bus.emit('match:end', { mode: 'league', ...res, ...out });
    return out;
  },
};

// Bus → ミッション（既存側を触らずに数える）
GNY.Bus.on('gacha:pull', () => GNY.App.update((s) => GNY.Missions.trackIn(s, 'scout', 1)));
GNY.Bus.on('ad:reward', () => GNY.App.update((s) => GNY.Missions.trackIn(s, 'reward_ad', 1)));

})(typeof window !== 'undefined' ? window : globalThis);
```

### 2.3 画面遷移（Router 部分の抜粋。画面とCSSを含む全体は `gunya-shell-draft.html`）

```js
/* ---------- Router（hash方式・画面ライフサイクル・離脱ガード） ---------- */
GNY.Router = (() => {
  const screens = new Map(); let cur = null, curName = null, busy = false, skipHash = false;
  const rootEl = $('#screen');
  const register = (name, def) => screens.set(name, def);
  function parse(){ const [n, q=''] = location.hash.replace(/^#\/?/, '').split('?'); return { name: n || 'home', params: Object.fromEntries(new URLSearchParams(q)) }; }
  async function go(name, params = {}, { replace = false } = {}){
    if (busy) return false;
    if (!screens.has(name)) { name = 'home'; replace = true; }               // 不明ルート → ホーム
    busy = true;
    try {
      if (cur && cur.canLeave && !(await cur.canLeave())) { setHash(curName, {}, true); return false; } // 試合中ガード
      try { cur && cur.unmount && cur.unmount(); } catch(e){ console.error(e); }
      rootEl.replaceChildren();
      const def = screens.get(name);
      const el = document.createElement('section'); el.className = 'scr scr-' + name; rootEl.appendChild(el);
      cur = def; curName = name;
      $('#tabs').hidden = !!def.fullscreen;
      document.querySelectorAll('#tabs a').forEach((a) => a.classList.toggle('on', a.dataset.r === (def.tab || name)));
      try { await def.mount(el, params); }
      catch(e){ console.error(e); el.replaceChildren(h(`<div class="empty">画面の表示に失敗しました。<br><button class="btn sub" style="margin-top:12px" onclick="location.hash='#/home'">ホームに戻る</button></div>`)); }
      setHash(name, params, replace);
      Bus.emit('route:changed', { name, params });
      return true;
    } finally { busy = false; }
  }
  function setHash(name, params, replace){
    const q = new URLSearchParams(params).toString(); const hh = '#/' + name + (q ? '?' + q : '');
    if (location.hash === hh) return;
    if (replace) history.replaceState(null, '', hh); else { skipHash = true; location.hash = hh; }
  }
  addEventListener('hashchange', () => { if (skipHash) { skipHash = false; return; } const p = parse(); go(p.name, p.params); });
  const refresh = () => curName && go(curName, {}, { replace: true });
  return { register, go, start(){ const p = parse(); go(p.name, p.params, { replace: true }); }, refresh, current: () => curName };
})();
const R = GNY.Router;
```

試合画面の `mount` は待たずにすぐ返し、試合の進行は `setTimeout` で後から始めます。ルーターが遷移中のロックを持っている間に「リーグ画面へ戻る」遷移を呼ぶと止まってしまうためで、実際の画面確認でこの不具合が出たので直してあります。

### 2.4 既存コード側の改修点（LegacyBridge の接続先）

```js
// (1) 試合：既存の開始関数に cfg と done を追加（関数名は U1 で確定）
function startGameMatch(cfg, done) {
  /* 既存の初期化…… cfg.opponent.name/color を相手の表示に使う */
  onGameOver = (r) => done({ runsFor: r.my, runsAgainst: r.cpu, aborted: false,
    stats: { atBats: r.ab, hits: r.h, homeRuns: r.hr, strikeouts: r.k, pitches: r.np } });
  onQuit = () => done({ aborted: true });
}
// (2) 能力倍率：該当する計算に1行ずつ（未設定なら 1）
const M = window.__GNY_MODS || {};
batSweetSpot *= (M.batContact ?? 1);   exitVelocity *= (M.batPower ?? 1);
pitchSpeed   *= (M.pitchVelocity ?? 1); breakScale  *= (M.breakAmount ?? 1);
aimError     /= (M.pitchControl ?? 1); cpuPitchSpeed *= (M.cpuPitchVelocity ?? 1);
// (3) コイン：既存の coins += n / localStorage.setItem('…coins…') を Economy.add/spend に置換
// (4) 設定：既存の設定保存 → AdGate.commitSetting(s => { s.legacy = 既存設定オブジェクト })
```

## 3. 検証チェックリスト（受け入れ条件）

凡例：[自動] はドラフトの単体テスト／E2Eで確認済み、[手動] は実機で確認、[回帰] は S0 の記録と比べる。

### A. 既存機能の回帰（すべての段階で実施）

- [ ] R-01 [回帰] 球種7種（まっすぐ・カーブ・スライダー・フォーク・シュート・シンカー・マイ変化球）が S0 と同じ軌道になる（同じ設定で目視比較）
- [ ] R-02 [回帰] 変化の仕方・変化量のカスタムが試合と練習に反映され、再起動後も残る
- [ ] R-03 [回帰] 投球・打撃フォーム5種がすべて選べ、見た目と結果に反映される
- [ ] R-04 [回帰] モーション調整スライダーが結果に反映される
- [ ] R-05 [回帰] 練習モード・10球チャレンジがホームから起動し、終了後ホームに戻れる
- [ ] R-06 [回帰] 関節がぐにゃっと崩れる挙動が変わっていない（空編成で S0 と比較）
- [ ] R-07 [回帰] ショップの解禁購入・デモ課金パック・プレミアムパスが従来どおり動き、状態が残る
- [ ] R-08 [手動] 縦画面で 360×640 から 430×932 まで、横スクロールが出ない

### B. 実プレイの流れ

- [x] B-01 [自動] 初回起動で初期選手10人（野手9・投手1）とスタメンが入る
- [x] B-02 [自動] ホーム → 試合 → 第1節 → 結果 → 順位表が更新される
- [x] B-03 [自動] 5節で全15試合が消化され、勝敗の合計が一致し、順位・報酬・昇格／降格が決まる
- [x] B-04 [自動] 報酬は1回だけ受け取れ、受取後に次シーズン（昇格先のリーグ）が始まる
- [x] B-05 [自動] 試合を中断した節は未消化のまま残り、リーグの進行もコインも変わらない
- [ ] B-06 [手動] 試合中に戻るボタン → 確認ダイアログ → キャンセルすると試合が続く
- [x] B-07 [自動] 編成の能力が倍率に反映される（空編成は全1.00、最大1.15）
- [ ] B-08 [手動] 強い編成と弱い編成で、打球・球速の違いが体感できる（それでも、ぐにゃで笑える範囲に収まっている）
- [x] B-09 [自動] 試合後、出場した選手に経験値が入り、Lvが上がる（上限で止まる）
- [x] B-10 [自動] スカウト：コイン不足なら消費しない／50回でSS確定／重複は限界突破、上限を超えた分はコインに変換
- [x] B-11 [自動] ミッション：未達成では受け取れない／二重に受け取れない／日付が変わるとリセット
- [x] B-12 [自動] ログボ：同じ日の2回目・時計の巻き戻しでは付与しない。連続が途切れると連続日数が1に戻る

### C. 保存と復元

- [x] C-01 [自動] 旧キー（コイン・設定・解禁・プレミアム）から v2 へ移行し、値が一致する。旧キーは残る
- [x] C-02 [自動] 旧キーのコインが壊れている（"abc"）→ 既定値で起動する
- [x] C-03 [自動] v2 のJSONが壊れている → BAK から復元し、壊れた中身は corrupt キーに退避する
- [x] C-04 [自動] 項目の欠損・型違い・負のコイン → 既定値で補完・補正する。知らないキーは残す
- [x] C-05 [自動] 存在しないカード・重複したスタメン・投手を野手枠に置いた編成 → 取り除く
- [x] C-06 [自動] 試合中に強制終了（pending が残った状態）→ 再起動で解除され、その節は未消化のまま
- [x] C-07 [自動] リロード前後でコイン・シーズン・所持カードが一致する（E2E）
- [x] C-08 [自動] localStorage が使えない → メモリのみで起動し、通知が出る
- [ ] C-09 [手動] 保存容量の超過（大きなダミーを入れて再現）→ 「保存できませんでした」と表示され、ゲームは続けられる
- [ ] C-10 [手動] 移行後に旧版HTMLへ戻しても、旧キーへのミラーで最新のコインと設定が見える
- [ ] C-11 [手動] 「データを初期化」→ 確認 → 初期状態になり、旧キーから再移行されない
- [ ] C-12 [手動] 新しいスキーマ（v3）のデータを v2 版で開いても、知らない項目が消えない

### D. 広告の表示順

- [x] D-01 [自動] 設定変更：**保存が先、その後に4秒のデモ広告**（広告の途中でアプリを閉じても設定は残る）
- [x] D-02 [自動] 広告の表示中に重ねて要求が来ても、広告は1回だけ
- [x] D-03 [自動] リワード：視聴を完了したときだけ付与してすぐ保存する。途中で閉じたら付与しない
- [ ] D-04 [手動] スライダーはドラッグ中ではなく、離して値が確定したとき（change）にだけ広告を出す（現行の挙動と一致させる。U6）
- [ ] D-05 [手動] 広告の表示中は下の画面を操作できない。タブ遷移もできない
- [ ] D-06 [手動] 試合中・ガチャの演出中に設定広告が割り込まない
- [ ] D-07 [手動] プレミアムパスの有無で、広告の扱いが現行と同じ（U5）

### E. 空状態・エラー状態

- [ ] E-01 [手動] リーグ未開始：ホームに「試合から開幕しましょう」と出る
- [ ] E-02 [手動] 所持カード0：編成に「スカウトで獲得しましょう」と出る
- [ ] E-03 [手動] コイン不足：スカウトのボタンが押せない（理由もわかる）
- [ ] E-04 [手動] シーズン終了後に試合へ行く → 「報酬を受け取り次シーズンへ」の表示になる
- [x] E-05 [自動] 不明なURL（`#/nope`）→ ホームへ置き換える
- [ ] E-06 [手動] 画面の描画で例外が出る → 「ホームに戻る」ボタン付きのエラー表示（アプリ全体は落ちない）
- [ ] E-07 [手動] 受け取るものが無い：ミッションのボタンが押せない表示になる

## 4. モジュール境界と未確定点（仕様側の調整用）

### 4.1 モジュール境界（公開API）

- **Save**：`load() → {data, report}`／`write(data)`／`resetAll()`／`defaults()`／`LEGACY`／`MIGRATIONS[n]`
- **App**：`init()`／`get()`／`update(mutator, {flush|save})`／`subscribe(fn)`／`flush()`。状態の変更は必ず update を通す。
- **LegacyBridge**：`showDemoAd(ms, done)`／`showRewardAd(done(completed))`／`startMatch(cfg) → Promise<result>`／`applyModifiers(mods)`／`clearModifiers()`／`legacyElement(id)`
- **Economy**：`coins()`／`add(n, reason)`／`spend(n, reason) → bool`／`addIn(s, n)`／`spendIn(s, n)`
- **AdGate**：`demo(reason)`／`rewarded(reason, grant)`／`commitSetting(mutator)`
- **Roster**：`DEFS`／`stats(card)`／`overall(card)`／`grade(v)`／`teamModifiers(state)`／`teamStrength(state)`／`addCardIn`／`giveExpIn`／`sanitize`
- **League**：`create({seasonNo, divisionIndex, seed})`／`playerGame(lg)`／`applyPlayerResult(lg, res)`／`standings(lg)`／`claimAndNext(state)`／`validate`／`repair`
- **Gacha**：`pull(n) → results|null`。**Missions**：`trackIn(s, ev, n)`／`claim(id)`／`view(s)`。**Login**：`claim(now)`
- **Router**：`register(name, {mount, unmount, canLeave, fullscreen, tab})`／`go(name, params)`／`refresh()`
- **イベント**：`match:end`／`gacha:pull`／`ad:start`／`ad:end`／`ad:reward`／`settings:changed`／`coins:changed`／`save:report`／`save:error`／`route:changed`／`app:resume`

### 4.2 未確定点

- **U1** 既存の試合を開始・終了・中断する関数名と、試合結果の項目（得点・安打・本塁打）の取り出し方
- **U2** 旧localStorageのキー名と値の形（コインが数値か文字列か、解禁が配列かオブジェクトか）。現在は候補のキー名を順に探している
- **U3** 既存のデモ広告・リワード広告の関数名とコールバックの形
- **U4** 能力倍率を掛ける物理・判定の変数名（ミートの範囲、打球の初速、球速、変化量、制球の誤差、CPU側）
- **U5** プレミアムパスの特典（広告を消すのか、ログボを2倍にするのか）。ドラフトでは現行の挙動を変えていない
- **U6** 「設定変更のたび」の単位（スライダーの1操作ごとか、画面を閉じたときにまとめてか）。ドラフトでは確定（change）ごと＋重複はまとめる
- **U7** リーグの規模：球団数6・1回総当たり（5節）・3リーグ・昇格は上位2・降格は最下位1。報酬額とリーグごとの倍率
- **U8** 試合を中断した扱い：ドラフトは未消化（やり直せる）。やり直しで結果を選べてしまうのを防ぐため、敗戦扱いにする案もある
- **U9** 引き分けの扱い：3回で同点なら引き分け（延長なし）。勝率は引き分けを除いて計算
- **U10** カード：レア度4段階、Lv上限 30/40/50/60、限界突破は最大4回で1回ごとに +5Lv、経験値は勝30・分20・負15。コインで育てる「特訓」を追加するかどうか
- **U11** スカウトの価格（1回100・10回900）、排出率（SS1/SR9/R30/N60）、天井50回
- **U12** 初期コイン（ドラフトでは300）と、既存の経済バランス（ショップ価格）との整合
- **U13** ミッションの種類と報酬額。週間ミッションを入れるかどうか
- **U14** ログボ：7日周期の額。連続が途切れたときに周期を1日目に戻すか（ドラフトは周期を進め、連続日数だけ1に戻す）
- **U15** チーム名・カラーの編集を入れるかどうか
- **U16** 選手名・球団名はすべて架空。実在の球団・選手名に似ていないかの確認は仕様側で行う
- **U17** 旧キーへのミラーをやめる時期（ドラフトは S4 の後、1版で停止）


## 5. 同梱ファイル

| ファイル | 内容 |
|---|---|
| `gunya-yakyu-plan.md` | 本書（計画・ドラフト全文・チェックリスト・未確定点） |
| `gunya-core-draft.js` | コアのドラフト（§2.2 と同一） |
| `gunya-shell-draft.html` | 画面シェル（Router・各画面・CSS）。core と同じフォルダに置き、ブラウザで開くと動く（試合はスタブ） |
| `test-core.js` | 単体テスト（`node test-core.js` で17項目） |
| `shots/*.png` | スマホ縦（390×844）の画面キャプチャ |
