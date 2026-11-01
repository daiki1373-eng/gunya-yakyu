// node test-core.js — コアドラフトの単体テスト（DOM不要部分）
const assert = require('assert');
function freshEnv(seed = {}) {
  const store = new Map(Object.entries(seed));
  globalThis.localStorage = {
    getItem: (k) => (store.has(k) ? store.get(k) : null),
    setItem: (k, v) => store.set(k, String(v)),
    removeItem: (k) => store.delete(k),
  };
  delete globalThis.GNY;
  delete require.cache[require.resolve('./gunya-core-draft.js')];
  require('./gunya-core-draft.js');
  return { store, GNY: globalThis.GNY };
}
let pass = 0; const t = (name, fn) => { fn(); pass++; console.log('  ok', name); };

t('新規: 既定値・初期選手付与', () => {
  const { GNY, store } = freshEnv();
  const rep = GNY.App.init();
  assert.equal(rep.status, 'new');
  const s = GNY.App.get();
  assert.equal(s.wallet.coins, 300);
  assert.equal(s.roster.lineup.batters.length, 9);
  assert.equal(s.roster.lineup.pitcher, 'p01');
  assert.ok(store.has('gunya.save.v2'));
});

t('旧キー移行: コイン/設定/解禁/プレミアムを保持し旧キーは消さない', () => {
  const legacySettings = { pitch: { curve: { amount: 0.7 } }, form: 'sidearm' };
  const { GNY, store } = freshEnv({ gunya_coins: '1234', gunya_settings: JSON.stringify(legacySettings), gunya_unlocks: '["fork","sinker"]', gunya_premium: 'true' });
  const rep = GNY.App.init();
  assert.equal(rep.status, 'migrated');
  const s = GNY.App.get();
  assert.equal(s.wallet.coins, 1234);
  assert.deepEqual(s.settings.legacy, legacySettings);
  assert.deepEqual(s.unlocks.legacy, ['fork', 'sinker']);
  assert.equal(s.wallet.premium.active, true);
  assert.ok(store.has('gunya_coins'));
  assert.deepEqual(s.meta.migratedFrom.keys.sort(), ['gunya_coins', 'gunya_premium', 'gunya_settings', 'gunya_unlocks']);
});

t('旧キーのコインが壊れている → 既定値', () => {
  const { GNY } = freshEnv({ gunya_coins: 'abc' });
  GNY.App.init();
  assert.equal(GNY.App.get().wallet.coins, 300);
});

t('v2 が壊れたJSON → バックアップ復元 + 退避', () => {
  const { GNY, store } = freshEnv();
  GNY.App.init(); GNY.Economy.add(500, 't'); GNY.Economy.add(1, 't'); // BAK に 800 世代
  store.set('gunya.save.v2', '{broken');
  const env2 = freshEnv(Object.fromEntries(store));
  const rep = env2.GNY.App.init();
  assert.equal(rep.status, 'restored-backup');
  assert.equal(env2.GNY.App.get().wallet.coins, 800);
  assert.ok(env2.store.has('gunya.save.corrupt'));
});

t('欠損・型違い → 既定値で補完、未知キー保持', () => {
  const { GNY } = freshEnv({ 'gunya.save.v2': JSON.stringify({ schemaVersion: 2, wallet: { coins: -50 }, roster: 'x', futureField: { a: 1 }, league: { v: 1, junk: true } }) });
  const rep = GNY.App.init();
  const s = GNY.App.get();
  assert.equal(s.wallet.coins, 0);
  assert.equal(s.roster.lineup.batters.length, 9); // starter再付与
  assert.deepEqual(s.futureField, { a: 1 });
  assert.equal(s.league, null);
  assert.ok(rep.repaired.includes('league(reset)'));
});

t('不明カード・不正編成の除去', () => {
  const { GNY } = freshEnv({ 'gunya.save.v2': JSON.stringify({ schemaVersion: 2, roster: { starterGiven: true, cards: { zz9: { lv: 3 }, n01: { lv: 999 } }, lineup: { batters: ['n01', 'n01', 'zz9', 'p01'], pitcher: 'n01' } } }) });
  GNY.App.init();
  const r = GNY.App.get().roster;
  assert.deepEqual(Object.keys(r.cards), ['n01']);
  assert.equal(r.cards.n01.lv, 30);
  assert.deepEqual(r.lineup.batters, ['n01']);
  assert.equal(r.lineup.pitcher, null);
});

t('総当たり日程: 6球団・5節・各節3試合・全カード1回', () => {
  const { GNY } = freshEnv(); GNY.App.init();
  const lg = GNY.League.create({ seed: 42 });
  assert.equal(lg.rounds, 5); assert.equal(lg.games.length, 15);
  for (let r = 0; r < 5; r++) {
    const gs = lg.games.filter((g) => g.round === r); assert.equal(gs.length, 3);
    assert.equal(new Set(gs.flatMap((g) => [g.home, g.away])).size, 6);
  }
  const pairs = new Set(lg.games.map((g) => [g.home, g.away].sort().join('-'))); assert.equal(pairs.size, 15);
});

(async () => {
  const { GNY } = freshEnv(); GNY.App.init();
  GNY.App.update((s) => { s.league = GNY.League.create({ seed: 7 }); });
  for (let i = 0; i < 5; i++) {
    const out = await GNY.Flow.playLeagueMatch();
    assert.ok(out && out.game.status === 'final');
  }
  let s = GNY.App.get();
  assert.equal(s.league.status, 'finished');
  assert.equal(s.stats.league.games, 5);
  assert.ok(s.league.games.every((g) => g.status === 'final'));
  const st = GNY.League.standings(s.league);
  assert.equal(st.reduce((a, r) => a + r.w, 0), st.reduce((a, r) => a + r.l, 0));
  assert.equal(st.find((r) => r.id === 'player').rank, s.league.result.rank);
  const coins0 = s.wallet.coins;
  const rr = GNY.App.update((d) => GNY.League.claimAndNext(d), { flush: true });
  s = GNY.App.get();
  assert.equal(s.wallet.coins, coins0 + rr.reward);
  assert.equal(s.league.seasonNo, 2);
  assert.equal(s.league.divisionIndex, Math.max(0, rr.move));
  assert.equal(GNY.App.update((d) => GNY.League.claimAndNext(d)), null); // 進行中は受取不可
  assert.equal(s.stats.league.seasons, 1);
  pass++; console.log('  ok シーズン進行→終了→報酬→次シーズン (rank', rr.rank, 'reward', rr.reward, ')');

  // 中断 → 未消化のまま
  GNY.LegacyBridge.startMatch = () => Promise.resolve({ aborted: true });
  const r0 = GNY.App.get().league.round;
  const ab = await GNY.Flow.playLeagueMatch();
  assert.ok(ab.aborted); assert.equal(GNY.App.get().league.round, r0); assert.equal(GNY.App.get().league.pending, null);
  pass++; console.log('  ok 試合中断は未消化のまま');

  // 試合中にアプリ終了（pending残り）→ 再起動で修復
  const env = freshEnv(); env.GNY.App.init();
  env.GNY.App.update((d) => { d.league = env.GNY.League.create({ seed: 1 }); d.league.pending = '1:g0'; }, { flush: true });
  const env2 = freshEnv(Object.fromEntries(env.store)); const rep = env2.GNY.App.init();
  assert.equal(env2.GNY.App.get().league.pending, null); assert.ok(rep.repaired.includes('league(repaired)'));
  pass++; console.log('  ok 試合中の強制終了→pending解除');

  // 決定性：同じseedで同じCPU結果
  const a = GNY.League.create({ seed: 99 }), b = GNY.League.create({ seed: 99 });
  GNY.League.applyPlayerResult(a, { runsFor: 3, runsAgainst: 1 }); GNY.League.applyPlayerResult(b, { runsFor: 3, runsAgainst: 1 });
  assert.deepEqual(a.games, b.games);
  pass++; console.log('  ok CPU同士の結果はseedで再現');

  // ガチャ：コイン不足/天井/原子性
  const g = freshEnv(); g.GNY.App.init();
  g.GNY.App.update((d) => { d.wallet.coins = 50; });
  assert.equal(g.GNY.Gacha.pull(1), null); assert.equal(g.GNY.App.get().wallet.coins, 50);
  g.GNY.App.update((d) => { d.wallet.coins = 900 * 6; d.gacha.pity = 0; });
  let ss = 0; for (let i = 0; i < 5; i++) ss += g.GNY.Gacha.pull(10, i).filter((x) => x.rarity === 'SS').length;
  assert.ok(ss >= 1, '50連でSS最低1'); assert.equal(g.GNY.App.get().gacha.pulls, 50);
  assert.equal(g.GNY.Missions.view(g.GNY.App.get()).daily.find((m) => m.id === 'd_scout').progress, 1);
  pass++; console.log('  ok ガチャ（不足時は消費なし・天井・ミッション連動）');

  // 能力反映：空編成は中立1.0
  const e = freshEnv(); e.GNY.App.init();
  e.GNY.App.update((d) => { d.roster.lineup = { batters: [], pitcher: null }; });
  assert.ok(Object.values(e.GNY.Roster.teamModifiers(e.GNY.App.get())).every((v) => v === 1));
  e.GNY.App.update((d) => { d.roster.cards.x01 = { defId: 'x01', lv: 60, exp: 0, lb: 0 }; d.roster.lineup.batters = ['x01']; });
  const m = e.GNY.Roster.teamModifiers(e.GNY.App.get());
  assert.ok(m.batPower > 1 && m.batPower <= 1.15);
  pass++; console.log('  ok 編成→倍率（空=1.0、上限1.15）');

  // ログインボーナス
  const L = freshEnv(); L.GNY.App.init();
  const day = (d) => new Date(2026, 10, d, 12).getTime();
  assert.equal(L.GNY.Login.claim(day(1)).coins, 50);
  assert.equal(L.GNY.Login.claim(day(1)), null);       // 同日2回目
  assert.equal(L.GNY.Login.claim(day(2)).streak, 2);
  assert.equal(L.GNY.Login.claim(day(1)), null);       // 時計巻き戻し
  const r4 = L.GNY.Login.claim(day(4)); assert.equal(r4.streak, 1); assert.equal(r4.day, 3);
  pass++; console.log('  ok ログインボーナス（同日/巻き戻し/連続切れ）');

  // ミッション日付切替と二重受取
  const M = freshEnv(); M.GNY.App.init();
  M.GNY.App.update((d) => M.GNY.Missions.trackIn(d, 'match', 1));
  const c0 = M.GNY.App.get().wallet.coins;
  assert.equal(M.GNY.Missions.claim('d_play'), 50); assert.equal(M.GNY.Missions.claim('d_play'), 0);
  assert.equal(M.GNY.Missions.claim('d_hit'), 0);
  assert.equal(M.GNY.App.get().wallet.coins, c0 + 50);
  pass++; console.log('  ok ミッション（未達/二重受取不可）');

  // AdGate: 保存→広告の順、リワード未完了は付与なし
  const A = freshEnv(); A.GNY.App.init(); const log = [];
  A.GNY.LegacyBridge.showDemoAd = (ms, done) => { log.push('ad:' + ms + ':saved=' + JSON.parse(A.store.get('gunya.save.v2')).settings.app.sound); setTimeout(done, 5); };
  const p1 = A.GNY.AdGate.commitSetting((st) => { st.app.sound = false; });
  const p2 = A.GNY.AdGate.demo('dup');
  await Promise.all([p1, p2]);
  assert.deepEqual(log, ['ad:4000:saved=false']);
  A.GNY.LegacyBridge.showRewardAd = (done) => done(false);
  const cA = A.GNY.App.get().wallet.coins;
  assert.equal(await A.GNY.AdGate.rewarded('coin', (st) => A.GNY.Economy.addIn(st, 100)), false);
  assert.equal(A.GNY.App.get().wallet.coins, cA);
  A.GNY.LegacyBridge.showRewardAd = (done) => done(true);
  assert.equal(await A.GNY.AdGate.rewarded('coin', (st) => A.GNY.Economy.addIn(st, 100)), true);
  assert.equal(JSON.parse(A.store.get('gunya.save.v2')).wallet.coins, cA + 100);
  pass++; console.log('  ok 広告順序（保存→4秒広告、重複まとめ、リワード完了時のみ付与・即保存）');

  // localStorage 利用不可
  const N = freshEnv(); globalThis.localStorage = { getItem() { throw new Error('denied'); }, setItem() { throw new Error('denied'); } };
  const nr = N.GNY.App.init(); assert.equal(nr.status, 'storage-unavailable'); assert.equal(N.GNY.App.get().wallet.coins, 300);
  pass++; console.log('  ok localStorage不可でも起動（メモリのみ）');

  console.log(`\n${pass} passed`);
})().catch((e) => { console.error('FAIL', e); process.exit(1); });
