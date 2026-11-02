# 段階I 第一弾（なんでもあり化：種族・必殺技・ラグドール誇張）コードレビュー

対象ファイル：[index.html](file:///home/hatch/workspace/gunya-yakyu-v2/index.html)（および生成物 [index-prod.html](file:///home/hatch/workspace/gunya-yakyu-v2/index-prod.html)）  
基準資料：[phaseI-review-brief.md](file:///home/hatch/workspace/gunya-yakyu-v2/phaseI-review-brief.md)、[phaseI-brief1.md](file:///home/hatch/workspace/gunya-yakyu-v2/phaseI-brief1.md)、[phaseI-brief2.md](file:///home/hatch/workspace/gunya-yakyu-v2/phaseI-brief2.md)、[phaseI-brief3.md](file:///home/hatch/workspace/gunya-yakyu-v2/phaseI-brief3.md)  
自動検証結果：`NODE_PATH=/tmp/jsdom-test/node_modules node phase4-check.js` → 開発版・本番版とも **self-test 74/74 合格**（データ管理UI・初期化二重確認・本番v1フィクスチャ移行もすべて合格）

---

## P0（使うと壊れる）

- **該当なし**
  - 試合進行不能（フリーズ・無限ループ）、未捕捉例外によるクラッシュ、既存セーブデータの破損を引き起こす P0 不具合はありません。

---

## P1（仕様不一致・要修正）

### 1. 主人公の種族 `species` が `Growth` のセーブ読込（`importSlice`）で復元されず、リロードやJSON読込で常に `'human'` に戻る
- **根拠箇所**：
  - `Growth` 内 `PROTAG_DEFAULT`（[index.html:1369-1373](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L1369-L1373)）
  - `Growth` 内 `Save.register('growth', { exportSlice, importSlice })`（[index.html:1893-1909](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L1893-L1909)）
- **内容**：
  - `createProtagonist`（[index.html:1561-1568](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L1561-L1568)）では `s.protagonist.species` を保存し、`exportSlice`（[index.html:1881](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L1881)）でも `protagonist: Util.copy(s.protagonist)` として JSON に書き出されています。
  - しかし `importSlice`（[index.html:1896-1908](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L1896-L1908)）で `s.protagonist` を再構築する際、`PROTAG_DEFAULT` にも明示コピー項目にも `species` が含まれていません。
  - その結果、主人公作成画面でカブトムシ・イヌ・パンダを選んで入部しても、**ページをリロード（`Save.load()`）またはセーブ書き出し→読み込み（`Save.importText()`）した瞬間に `s.protagonist.species` が `undefined` に戻り、見た目・種族表記・能力補正（`mods`）がすべて人間（`'human'`）にリセットされます**。
- **修正方針**：
  - `PROTAG_DEFAULT` に `species: 'human'` を含めたうえで、`importSlice` の `s.protagonist` 構築時に `species: Object.hasOwn(CONFIG.SPECIES, p.species) ? p.species : 'human'` を復元してください（旧セーブは `p.species` が `undefined` のため自動的に `'human'` へフォールバックされ、セーブ互換も保たれます）。

---

### 2. `armed` の消費タイミング不整合（①CPU打撃必殺が見逃しボールで消える、②ユーザー打撃必殺がバントで消費され強打化する、③攻守交代時に未消費 `armed` が残留して次半イニングでロックされる）
- **根拠箇所**：
  - `MatchEngine.armSpecial` / `setUi`（[index.html:2801-2806](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L2801-L2806)、[index.html:2819-2820](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L2819-L2820)）
  - `MatchEngine.swingStyle`（[index.html:2977-2990](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L2977-L2990)）
  - `MatchEngine.changeHalf`（[index.html:3071-3084](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L3071-L3084)）
  - `MatchEngine.resolveContact`（[index.html:3107-3143](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L3107-L3143)）
- **内容**：
  1. **CPU打撃必殺が見逃しボール（`addBall`）で消費される**（[index.html:3110-3112](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L3110-L3112)）：
     - `resolveContact` の CPU 分岐で、ボール球の見逃し判定 `if (!isStrikeZone(ball) && rng() < 0.55) { addBall(side); return; }`（3112行）より**前**に `if (cpuSpecial) { currentSwingSpecial = cpuSpecial; game.armed.opponent = null; slowmoT = 0.7; }`（3111行）を実行しています。
     - そのため、ユーザーがゾーン外に投げて CPU が見逃しボールを選んだ場合でも、スイングしていないのに CPU の打撃必殺（`game.armed.opponent`）が消費され、さらに四球時などの `between` 待機時間に `slowmoT = 0.7` が掛かってしまいます。
  2. **ユーザー打撃必殺がバント実行（2段目）で消費され、犠打（`buntPlay`）ではなく通常ヒッティング（`hitBall`）に化ける**（[index.html:2985](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L2985)、[index.html:3129-3138](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L3129-L3138)）：
     - `swingStyle`（2985行）で `style === 'bunt'`（バント構え後のバント実行）でも `game.armed.user` を消費して `currentSwingSpecial` に移しています。
     - さらに `resolveContact` の `currentSwingStyle === 'bunt'` 分岐（3133〜3138行）で `if (currentSwingSpecial && j.zone !== 'miss')` のときに `buntPlay(side)` ではなく `hitBall(...)` を呼んでいるため、バントしたのに長打・本塁打になる挙動になっています（また 3136行の `core` 時は `HOOKS.sweetSpot` も掛かっていません）。打撃必殺は通常スイング（`style !== 'bunt'`）でのみ消費・適用するのが自然です。
  3. **攻守交代（`changeHalf`）を跨いで逆属性の `armed` が残留し、次の半イニングで消費も再発動もできなくなる**（[index.html:2801](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L2801)、[index.html:2971](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L2971)、[index.html:2985](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L2985)、[index.html:3071](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L3071)）：
     - 例：2アウト2ストライクでユーザーが打撃必殺（`kind: 'bat'`）を `armSpecial()` で構えた後に見逃し三振した場合、または `armSpecial()` に `game.phase` のガードがないため3アウト目の打球飛行中（`phase === 'flight'`）に必殺ボタンを押した場合、`game.armed.user` が未消費のまま `changeHalf()` で攻守交代します。
     - 交代後の守備（投球）イニングでは `startPitch` が `SPECIALS[game.armed[ps]].kind === 'pitch'` のみを消費するため、`kind: 'bat'` の `game.armed.user` は消費されずに残り続けます。しかも `specialReady` は `!game.armed.user` を条件としているため、**守備イニング中にゲージが100%になっても投球必殺を発動できず、ボタンが「必殺：ぐるぐるトルネード打法（disabled）」のまま半イニング丸ごとロックされます**（逆の投球→打撃でも、`flight` 中に `armSpecial` すると同様にロックされます）。
- **修正方針**：
  - `resolveContact` の CPU 分岐では、`if (!isStrikeZone(ball) && rng() < 0.55) { addBall(side); return; }` を通過して実際にスイング（コンタクト判定）する時点で `game.armed.opponent` を消費する。
  - `swingStyle` でのユーザー打撃必殺消費は `style !== 'bunt'` のときに限定し、`resolveContact` の `currentSwingStyle === 'bunt'` 内の必殺分岐（3129〜3138行）を外す。
  - `armSpecial()`（および `setUi` の `specialReady`）に `['ready', 'windup', 'pitch'].includes(game.phase)`（投球時は `game.phase === 'ready'`、打撃時はスイング前 `swingAt === null`）の条件を加え、かつ `changeHalf()` で攻守と合わなくなった `game.armed` をクリア（または次半イニング用にリセット）してください。

---

## P2（改善提案）

### 1. CPU打撃必殺（`spin` / `giant`）の `currentSwingSpecial` と `slowmoT = 0.7` が投球終了時（`resolveContact`）にセットされるため、投球中の巨大バット・回転描画・ズームが効かず、打球飛行や打席間待機がスローになる
- **根拠箇所**：
  - `MatchEngine.resolveContact`（[index.html:3110-3111](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L3110-L3111)）
  - `MatchEngine.drawMatchScene`（[index.html:3306-3318](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L3306-L3318)）
  - `MatchEngine.draw`（[index.html:3352](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L3352)）
- **内容**：
  - ユーザー打撃時は `swingStyle`（`phase === 'pitch'` の途中、`t ≈ 0.82`）で `currentSwingSpecial` と `slowmoT = 0.7` がセットされるため、到達までのスイング中に巨大バット・回転・1.22倍ズーム＋スロー演出が入ります。
  - 一方、CPU打者（`!userBatting()`）では `resolveContact()`（`ball.t >= 1` で `phase` が `'flight'` または `'between'` / `'ready'` に切り替わる瞬間）で初めて `currentSwingSpecial = cpuSpecial; slowmoT = 0.7;` がセットされます。
  - そのため、投球中（`windup` / `pitch`）は奥のCPU打者のバットが巨大化せず回転もせず、代わりに打球が外野へ飛ぶ `flight` フェーズや凡退後の `between` フェーズが 0.35倍速のスローモーションになります。
- **改善案**：
  - `drawMatchScene` で `!userBatting()` のときは `game.armed.opponent === 'giant'` でも巨大バットを描画し、CPUの `slowmoT` は `startPitch` 時または `ball.t` がスイング開始（例：`0.75`）を超えた時点で発動させると、CPU打撃必殺の演出もユーザー時と揃います。

### 2. 横向きレイアウト（`@media (orientation:landscape)`）で右カラム下端の `.eng-gauge` が中央の4ボタンダイヤモンド（`.eng-diamond`）下段ボタンと重なりやすい
- **根拠箇所**：
  - `@media (orientation:landscape)` 内の `.eng-diamond` / `[data-eng-dpad]` / `.eng-gauge`（[index.html:392-404](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L392-L404)）
- **内容**：
  - 左カラム（列1）では、下端の `[data-eng-subs]`（高さ48px）と重ならないよう `[data-eng-dpad]` に `margin: 0 0 56px`（394行）が付与されています。
  - 一方、右カラム（列3）では、常時表示される `.eng-gauge`（ラベル＋バー＋高さ48pxボタンの2段折り返し＝高さ約69px、`align-self: end; z-index: 2`、404行）が追加されたにもかかわらず、`.eng-diamond`（392行）が `margin: 0; align-self: center` のままになっています。
  - そのため、画面高 360〜390px の横向き端末では、`.eng-diamond`（高さ 164〜185px）の下段ボタン（`[data-ps="cross"]`：「× 長打」／「× フォーク・スロー」）の下部に `.eng-gauge` のラベル・バーが重なります。
- **改善案**：
  - 左カラムの `[data-eng-dpad]` と同様に、横向き時の `.eng-diamond` にも `margin: 0 0 64px`（または `align-self: start`）を付けて上へ逃がすか、`.eng-gauge .eng-action` の高さを少し詰めて重なりを解消することを推奨します。

### 3. `.eng-action.special:disabled` で未定義の CSS 変数 `var(--muted)` が参照されている
- **根拠箇所**：
  - `:root`（[index.html:24-35](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L24-L35)）
  - `.eng-action.special:disabled`（[index.html:345](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L345)）
- **内容**：
  - `.eng-action.special:disabled{background:var(--panel);color:var(--muted);border-color:var(--line)}` となっていますが、`:root` のカラーパレットには `--muted` が定義されておらず、補助文字色の変数名は `--text-2` です（`.eng-action:disabled` の `opacity:.38` が効くため見た目上の破綻は軽微ですが、`var(--text-2)` に揃えるのが適切です）。

---

## 「特に確認してほしい点」への検証結果一覧

1. **段階G/Hの既存仕様を壊していないか**
   - **問題なし**：
     - 4ボタン打法の基準タイミング窓（`coreW = 50 * winMul`、`contactW = 110 * winMul`）およびスタイル倍率（`power: 0.7`、`hnr: 1.15`、高さ一致 `1.2` / 不一致 `0.85`）は `judgeTiming`（[index.html:2746-2750](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L2746-L2750)）で不変であり、`spin` の `×1.35` は必殺発動時のみ `resolveContact` 内でローカル適用されています。
     - 打順補正 `ORDER_MULT`（[index.html:2742](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L2742)）、カウント制、不可視の狙い（`aim`）、守備走塁オート、段階Hの横向き3カラムレイアウト（[index.html:372-415](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L372-L415)）すべて維持されています（右カラムの下部マージンのみ上記 P2-2 参照）。

2. **必殺ゲージの加算が二重に数えられる経路がないか**
   - **問題なし**：
     - `addGauge` の呼び出し箇所は `addStrike`（3ストライク目未満で投手側 `+10`、[index.html:2909](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L2909)）、`recordHit`（打撃側へ HR `+55` / 1B・2B `+30`、[index.html:3038](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L3038)）、`recordOut`（投手側へ三振 `+40` / 凡打 `+15`、[index.html:3056](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L3056)）の3箇所のみです。
     - 3ストライク目は `addStrike` の 2908行で `recordOut(side, true, ...)` を呼んで早期 `return` するため、`+10` と `+40` が重複することはありません。
     - `flightResult` でも `OUT` なら `recordOut`、安打なら `recordHit` の一方しか通らず、`buntPlay` は `recordOut`（`+15`）のみ、`walk`（四球）や `foulBall` は加算なし、`debugAct` も各1回のみ呼び出すため、二重計上される経路はありません。

3. **`armed` の消費漏れ・二重消費がないか**
   - **要修正（P1-2）**：
     - 投球必殺は `startPitch`（[index.html:2971](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L2971)）で `ball.special` に移して `game.armed[ps] = null` にするため二重消費はありません。
     - ただし打撃必殺と攻守交代に関して、**①CPU打撃必殺が見逃しボール（`addBall`）で消費される、②ユーザー打撃必殺がバント実行（2段目）で消費されて `hitBall` になる、③攻守交代（`changeHalf`）時に未消費 `armed` が残ると次半イニングで消費も再発動もできなくなる** の3点があるため、P1-2 の修正が必要です。

4. **CPU 必殺の自動発動が試合の自動進行を止めたり無限ループしないか**
   - **問題なし**：
     - CPU の自動発動は `atBat` 冒頭（[index.html:2898](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L2898)）と `startPitch` 冒頭（[index.html:2950](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L2950)）で `activateSpecial('opponent', ...)` を1回呼ぶだけで、`armed.opponent` セットと `gauge.opponent = 0` により再入せず、`frame()` の `slowmoT` 減衰も実時間 `dtReal`（[index.html:3368](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L3368)）で行われるため、進行停止や無限ループは起きません。

5. **`slowmoT` / ズーム変換が描画バッファ（480x270→960x540）と `shake` 変換に干渉しないか**
   - **問題なし**：
     - `draw()`（[index.html:3348-3362](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L3348-L3362)）において、`g.scale(0.5, 0.5)` → `shake` 変換 → `zoom` 変換（`cx = userBatting() ? 420 : 430, cy = 320` を中心に `1.22` 倍）→ シーン＋パーティクル描画 → `g.restore()` でズーム解除 → `drawHeader(g)`（ヘッダーはズーム対象外）→ `shake` 解除 → `480x270` から `960x540` への拡大転写、と変換スタックが正しくネスト・復元されています。

6. **種族 `mods` の適用漏れ、主人公が投手/野手どちらでも種族が効くか**
   - **問題なし（リロード時の P1-1 を除く）**：
     - `abilityAtCard(c, lv)`（[index.html:1408-1417](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L1408-L1417)）の1箇所で `CONFIG.SPECIES[c.species].mods` を加算・クランプ（通常 `1..99`、`kmh` `60..165`）しており、`getCard`・`abilityAt`・`overallOf`・`getLineup`・`getRotation`・`teamOverall`・`League.buildMatchConfig` のすべてがこの関数を通るため、表示値と試合実効値は完全に一致しています。
     - `protagMaster()`（[index.html:1392-1398](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L1392-L1398)）も `P(...)` / `B(...)` の双方に `p.species || 'human'` を渡しており、投手主人公・野手主人公のどちらでも種族が効きます。

7. **セーブ互換（旧セーブで `species` 無しのまま全機能が動くか）**
   - **旧セーブ読込は問題なし／新セーブの `protagonist.species` 復元のみ要修正（P1-1）**：
     - 旧セーブ（`species` フィールド無し）や v1 移行データを読み込んだ際は、すべての箇所で `'human'` にフォールバックされ、`Save.validateData` も新規必須フィールドを要求しないためエラーなく動作します。
     - ただし P1-1 のとおり、`Growth` の `importSlice` に `protagonist.species` のコピーが漏れているため、選択した主人公の種族をセーブから復元する処理のみ追加が必要です。

8. **自己テスト74件（既存70＋段階I新規4）が仕様を正しく固定できているか**
   - **良好（P1-1 の保存往復のみ追加推奨）**：
     - 既存70件は無変更のまま全件合格しており、新規4件（[index.html:5018-5064](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L5018-L5064)）も `X01〜X03` と `S01` の能力値・主人公の種族フォールバック／適用／引き継ぎ・`SPECIALS`/`GAUGE_GAIN` 定数・必殺ゲージの100キャップと `armSpecial()` 発動および `MatchEngine.abort()` 後片付けを正しく検証できています。
     - P1-1 修正時に、「主人公の種族選択」テスト内で `Save.exportText()` → `Save.load()` 相当の往復後も `getCard('PROTAG').species === 'dog'` が維持されることを1行追加で確認するとさらに堅牢になります。
