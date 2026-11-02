# 段階G（甲子園V方式への試合操作切り替え）コードレビュー

対象ファイル：[index.html](file:///home/hatch/workspace/gunya-yakyu-v2/index.html)（および生成物 [index-prod.html](file:///home/hatch/workspace/gunya-yakyu-v2/index-prod.html)）  
基準資料：[phaseG-brief1.md](file:///home/hatch/workspace/gunya-yakyu-v2/phaseG-brief1.md)、[phaseG-brief2.md](file:///home/hatch/workspace/gunya-yakyu-v2/phaseG-brief2.md)、[koshien-v-spec-2026-11-02.md](file:///home/hatch/workspace/gunya-yakyu-repo/docs/koshien-v-spec-2026-11-02.md)  
自動検証結果：`NODE_PATH=/tmp/jsdom-test/node_modules node phase4-check.js` → 開発版・本番版とも **self-test 70/70 合格**

---

## P0（使うと壊れる）

- **該当なし（既定設定 `throwSelect: false` 時）**
  - 既定の構成では試合進行不能・クラッシュ・セーブ破損を引き起こす P0 不具合はありません（ただし `CONFIG.FEATURES.throwSelect` を `true` に切り替えた場合は、下記 P1-3 の送球選択周りの不整合により3アウト目の得点・サヨナラ誤判定や攻撃側での送球停止が発生するため、有効化前に修正が必要です）。

---

## P1（仕様不一致・要修正）

### 1. `canStyle`（`windup` / `pitch` で有効）と `swingStyle`（`pitch` のみ受付）の条件不一致により、`windup` 中の打法入力がスイング音だけ鳴って無視される
- **根拠箇所**：
  - `MatchEngine.setUi`（[index.html:2729](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L2729)）
  - `MatchEngine.swingStyle`（[index.html:2881-2891](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L2881-L2891)）
  - `GameSurface.update` / `playStyle` / `onClick` / `onKey`（[index.html:3433-3438](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L3433-L3438)、[index.html:3502-3529](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L3502-L3529)）
- **内容**：
  - `setUi` では `canStyle: state === 'running' && userBatting() && ['windup', 'pitch'].includes(game.phase)`（2729行）としており、`GameSurface.update` でも `windup` 中は打法ダイヤモンドの4ボタンすべてが有効（`disabled = false`）になります。
  - しかし `MatchEngine.swingStyle(style)`（2887行）はバント構え開始を除き `if (game.phase !== 'pitch' || swingAt !== null) return;` で弾くため、`windup`（投球開始後 0.22〜0.42秒）の間に `△ ヒットエンドラン`・`□ ウエストボール`・`× 長打` を押すと、**`GameSurface.playStyle` で `NativeFeel.tap()` と空振り音 `Sfx.whoosh()` だけが鳴り、エンジンのスイング判定には一切記録されず見逃しになる**挙動になっています（指示書第1便 §2-5 と §2-8 の記述差に起因）。
  - また `GameSurface.onKey`（3525〜3528行）は `lastView.batting` しか見ておらず `lastView.canStyle` / `lastView.canSwing` を確認していないため、`ready`・`flight`・`between` 中やポーズモーダル表示中にキーボードの `W` / `A` / `S` を押しても毎回 `Sfx.whoosh()` が鳴ります。
- **修正方針**：
  - 非バント打法のボタン有効条件を `phase === 'pitch' && swingAt === null`（`v.canSwing`）に合わせるか、あるいは `windup` 中の入力を先行入力として受け付けるかを統一し、`playStyle` の効果音発火も実際にスイングが成立する状態（`v.canSwing`）に限定してください。

### 2. ユーザー投球時（`!userBatting()`）にキャンバスをタップすると、球種4ボタン割当を無視して `startPitch()` が直接発火する
- **根拠箇所**：
  - `GameSurface.mount` 内の `canvas` `pointerdown` ハンドラ（[index.html:3550](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L3550)）
  - `MatchEngine.startPitch`（[index.html:2853-2860](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L2853-L2860)）
- **内容**：
  - 3550行が `if (lastView && lastView.canStyle) MatchEngine.swingStyle('west'); else MatchEngine.startPitch();` となっており、`else` 側で `lastView.canStartPitch`（または `lastView.batting`）を確認していません。
  - そのため、**ユーザー投球時（守備時）にキャンバスをタップすると、4ボタン（`choosePitchButton`）を経由せずに `MatchEngine.startPitch()` が直接呼ばれます**。このとき `selectedPitch` の初期値（設定画面の「最初に選ぶ球種」）がそのまま投げられるため、登板中の投手の4ボタン割当（`pitchButtonsFor`）に含まれない球種まで投げられてしまいます。
- **修正方針**：
  - キャンバスの `pointerdown` の `else` 分岐を `else if (lastView && lastView.canStartPitch) MatchEngine.startPitch();` に制限してください。

### 3. ヒットエンドラン凡退時（`flightResult('OUT')`）およびバント時（`buntPlay`）の2アウト3塁からの得点計上と、0〜1アウト時のサヨナラ判定漏れ
- **根拠箇所**：
  - `MatchEngine.flightResult`（[index.html:3031-3036](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L3031-L3036)）
  - `MatchEngine.buntPlay`（[index.html:2995-2998](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L2995-L2998)）
  - `MatchEngine.recordOut`（[index.html:2952-2966](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L2952-L2966)）
- **内容**：
  1. **2アウト3塁で第3アウトと同時に得点が入る**：`flightResult('OUT')`（3034行：`if (ball.mods && ball.mods.style === 'hnr') advanceRunners(side, 1);`）と `buntPlay`（2996行：`advanceRunners(side, 1);`）が、`game.outs < 2` を確認せずに `recordOut` の前で走者を進めています。そのため **2アウト3塁でエンドランのフライアウトやバントアウトになると、3アウトチェンジになるにもかかわらず3塁走者の得点が加算されます**。
  2. **0〜1アウト3塁でのサヨナラスクイズ／エンドラン進塁打で試合が終わらない**：最終回裏（`game.inning >= cfg.innings && game.half === 'bottom'`）に0〜1アウト3塁から `buntPlay` や `flightResult('OUT')` で3塁走者が生還してホームが勝ち越しても、`recordOut` にはサヨナラ判定（`game.score.home > game.score.away` で `finish()`）が無いため、そのまま次打者へ進行してしまいます。
- **修正方針**：
  - `buntPlay` と `flightResult('OUT')` の `advanceRunners(side, 1)` は `game.outs < 2` のときのみ実行し、進塁によって最終回裏に `game.score.home > game.score.away` となった場合は `recordOut` 内で `finish()` を呼ぶようにしてください。

### 4. 送球選択（`THROW_SELECT` / `phase='throw'` / `chooseThrow`）における状態更新・3アウト判定・攻守判定の不整合
- **根拠箇所**：
  - `MatchEngine.update`（[index.html:3081-3084](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L3081-L3084)）
  - `MatchEngine.chooseThrow`（[index.html:3038-3054](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L3038-L3054)）
  - `MatchEngine.recordHit`（[index.html:2935-2951](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L2935-L2951)）
  - `MatchEngine.changeHalf`（[index.html:2967-2979](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L2967-L2979)）
- **内容**（指示書第1便 §2-8 の手順を忠実に実装した結果生じているエッジケースを含む）：
  1. **送球選択後の `setUi` 漏れにより、`between` 中も送球ボタンが表示されたまま残る**：`recordHit`（2950行）は得点時（`before !== game.score[sideKey(side)]`）しか `setUi` を呼ばず、`chooseThrow`・`changeHalf`・`advanceRunners` も `setUi` を呼びません。そのため無得点の単打等で `chooseThrow` を選んだ後、次の `atBat()`（0.85〜1.0秒後）まで `port.update` が走らず、**送球先ボタン（`[data-eng-throw]`）と「送球先を選んでください」の文言が `between` フェーズの間ずっと画面に残り続けます**（`throwSelect: false` の通常時も、無得点安打の後は `between` 中もステータスが「打球を追っています」のまま残ります）。
  2. **送球アウトで3アウト目（`game.outs === 3`）になっても直後の `recordHit` で他走者が生還・サヨナラ判定される**：`chooseThrow`（3046〜3049行）は `game.outs += 1` のあとに無条件で `recordHit(side, kind)` を呼んでから `if (game.outs >= 3) changeHalf()` しています。2アウト複数走者（例：2・3塁で二塁打、または1・2塁からエンドラン二塁打）で先頭走者を送球アウトにして3アウト目を取っても、`recordHit` で後続走者がホームインして得点が入り、最終回裏なら2948行のサヨナラ判定で `changeHalf()` の前に試合終了してしまいます。また同フレーム内で `recordHit` のバナーが「送球アウト」バナーを上書きします。
  3. **別塁送球による追加進塁（3052行）でのサヨナラ判定漏れ**：3052行の `advanceRunners(side, 1)` は `recordHit` のサヨナラ判定（2948行）の後に実行されるため、この追加1進塁で3塁走者が生還して最終回裏に勝ち越しても `finish()` が呼ばれません。
  4. **ユーザー打撃時にも `phase = 'throw'` で止まる＆キーボード操作対応がない**：3082行に `!userBatting()` の条件がないため、`throwSelect` 有効時はユーザーが打者のとき（練習・10球チャレンジ含む）にもユーザー自身が相手守備の送球先を選ぶ状態になり、また `GameSurface.onKey` に送球先（1〜4）のキー割当がないためキーボードのみでは `phase = 'throw'` から進めません。

---

## P2（改善提案）

1. **バント構え（`buntStance = true`）から他打法（ヒッティング）に切り替えた際、バット描画とボタン強調がバント構えのまま残る**
   - **根拠箇所**：`MatchEngine.swingStyle`（[index.html:2881-2891](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L2881-L2891)）、`MatchEngine.drawMatchScene`（[index.html:3131](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L3131)）
   - `ready` / `windup` で一度 `○ バント` を押して `buntStance = true` にした後、`pitch` 中に `□` / `△` / `×` で通常スイング（バスター）を行うと、`buntStance` が `false` に戻らないため、3131行の `userBatting() && buntStance ? -0.2 : ...` によりバットが水平固定のままスイングアニメーションせず、ボタンの `.on` クラスも残ります。`style !== 'bunt'` でスイングした際に `buntStance = false` に戻すと自然です。

2. **`buntAdjust` が内部状態で更新されるのみで打球判定に未反映**
   - **根拠箇所**：`MatchEngine.setDir`（[index.html:2799](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L2799)）、`MatchEngine.resolveContact`（[index.html:3016](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L3016)）
   - 指示書第1便 §2-3 / §2-5 / §2-7 の記述どおり `buntAdjust` の更新と `judgeTiming(errMs, 'bunt', 0)` が実装されていますが、`buntAdjust` の値がどこからも参照されていません。バント構え中の上下調整を高さ一致（`heightMatch`）やファウル率に効かせると甲子園V仕様書の意図により合致します。

3. **通常試合終了（`MatchEngine.finish`）時に `port.clear()` が呼ばれず、ポーズモーダル表示中も `GameSurface.onKey` が反応する**
   - **根拠箇所**：`MatchEngine.finish`（[index.html:2980-2994](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L2980-L2994)）、`MatchEngine.abort`（[index.html:3201](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L3201)）、`GameSurface.onKey`（[index.html:3517-3529](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L3517-L3529)）
   - `abort()` と異なり `finish()` は `port.clear()` を呼ばないため、試合終了〜結果画面遷移の間のタイマー・方向入力解除がスキップされます。また `onKey` に `if (Shell.hasModal() || !MatchEngine.isRunning()) return;` を入れると、ポーズ／デバッグモーダル表示中に矢印キーのスクロール抑止や `W`/`A`/`S`/`D` 入力が発火しなくなります。

4. **三振・四球・イニング交代時の `between` フェーズ中、`game.count` がリセットされず S/B ランプが全点灯のまま残る**
   - **根拠箇所**：`MatchEngine.addStrike` / `addBall` / `changeHalf`（[index.html:2813-2822](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L2813-L2822)、[index.html:2967-2979](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L2967-L2979)）、`drawHeader`（[index.html:3158-3161](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L3158-L3161)）
   - `game.count` のリセットが `atBat()`（2807行）のみのため、3アウト交代後の `between`（1.0秒間）でも前の打者の S/B ランプが点灯したまま残ります。`changeHalf()` でも `game.count = { s: 0, b: 0 }` にリセットすると表示がすっきりします。

5. **本塁打・エンドラン安打時に `match:run` が複数回に分けて emit される**
   - **根拠箇所**：`MatchEngine.advanceRunners` / `addRuns` / `recordHit`（[index.html:2902-2913](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L2902-L2913)、[index.html:2940-2941](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L2940-L2941)）
   - 走者ありの本塁打では `advanceRunners(side, 4)` と打者自身の得点で `match:run` が2回（エンドラン時は最大3回）に分けて発火します。スコアや打点の加算自体は正確で二重加算はありませんが、実況テロップ（`Commentary.line('run')`）が最後の「1点が入った！」で上書きされるため、合計得点で1回 emit するとなお良いです。

---

## 問題なしと確認できた観点

### 観点1：指示書との不一致チェック
- **打撃4ボタン打法**：**問題なし**（`swingStyle('west'|'hnr'|'power'|'bunt')`、`swing()` 互換別名、UIの `[data-eng-batbtns]` における PS 配置 △=上 `hnr` / □=左 `west` / ○=右 `bunt` / ×=下 `power`、記号色、キーボード `W/A/S/D` 対応すべて [index.html:331-337](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L331-L337)、[index.html:2880-2891](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L2880-L2891)、[index.html:3404-3406](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L3404-L3406) で一致）
- **二段式バント**：**問題なし**（1回目で `buntStance = true`・水平バット描画 `-0.2`・ボタン `.on` クラス、2回目 `phase === 'pitch'` でスイング記録と `judgeTiming(errMs, 'bunt', 0)` → `buntPlay` 分岐まで [index.html:2883-2890](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L2883-L2890)、[index.html:3015-3021](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L3015-L3021)、[index.html:3131](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L3131)、[index.html:3434-3438](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L3434-L3438) で一致）
- **タイミング窓 ±50ms / ±110ms と打法別倍率**：**問題なし**（`IDEAL_T = 0.82`、`judgeTiming` の `power: 0.7` / `hnr: 1.15` / 高さ一致 `1.2`・不一致 `0.85`、`coreW = 50 * winMul`、`contactW = 110 * winMul` が [index.html:2667-2676](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L2667-L2676) で一致）
- **上下入力の高さ対応と軌道**：**問題なし**（`ball.by <= -8` 高め / `>= 14` 低めと `swingDir` の一致判定、芯ヒット時の `heightMatch > 0 ? 1.06 : 1`、飛距離倍率 `down: 0.94` / `up: 1.08`、アウト確率 `down: +0.12` が [index.html:2920](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L2920)、[index.html:2928](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L2928)、[index.html:3022-3029](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L3022-L3029) で指示書第1便と完全一致）
- **打順補正テーブル**：**問題なし**（`ORDER_MULT = [1.1, 0.8, 1.3, 1.3, 1.3, 1, 1, 0.7, 0.7]`、`orderMultAt`、`orderMult`、CPU打撃 `q >= 0.58` 時および `hitBall` 飛距離への適用が [index.html:2668-2670](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L2668-L2670)、[index.html:2920](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L2920)、[index.html:3011](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L3011) で一致）
- **投球の不可視の狙いと投手別4ボタン割当・到達時間=球速換算**：**問題なし**（`setDir` 立ち上がりでの `aim` 0.5刻み移動・`-2..2` クランプ・`readyPitch` での中心リセット・非表示化、`pitchButtonsFor` の4ボタン割当と解禁ゲート、`slowball` 追加、`flightSeconds(kmh) = clamp(18.44 * 3.6 / kmh, 0.36, 0.90)` が [index.html:2638](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L2638)、[index.html:2671](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L2671)、[index.html:2679-2689](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L2679-L2689)、[index.html:2790-2804](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L2790-L2804) で一致。第1便の本文とテスト4の `['curve','sinker']` 順序差も両立処理済み）
- **カウント / 四球**：**問題なし**（`addStrike` / `addBall` / `foulBall` / `walk` の押し出し・`ab` 非加算・`match:walk` 通知・サヨナラ／10球判定・実況 `Commentary.line('walk')` とリスナー1回登録が [index.html:2813-2842](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L2813-L2842)、[index.html:3364](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L3364)、[index.html:3492](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L3492) で一致）
- **守備走塁オート・THROW_SELECT 既定OFF**：**問題なし**（`CONFIG.FEATURES.throwSelect: false`、`throwSelectEnabled()` が [index.html:439](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L439)、[index.html:2678](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L2678) で既定OFF、盗塁UI撤去とエンジンAPI温存も指示どおり）
- **S/B/O ランプ**：**問題なし**（`drawHeader` [index.html:3152-3162](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L3152-L3162) で帯幅 430、S 2灯 `#FFD94A` / B 3灯 `#57D977` / O 3灯 `#FF5A4E` の3行描画を確認）
- **ミートカーソルとゾーン表示の廃止**：**問題なし**（`cursorY` / `coreFactor2` / `setCursor` / `batterShift` / `setShift` / `setAim` / `setTactic`、Canvas 内のゾーン枠・グリッド・カーソル・狙いマーカー、DOM の旧パッド・スライダー・3×3狙い・作戦行すべて削除済み）

### 観点2：バグ・不整合チェック（問題なしの項目）
- **打席ごとのカウントリセット**：**問題なし**（`atBat()` [index.html:2807](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L2807) で新打席開始時に `game.count = { s: 0, b: 0 }` に確実にリセットされる）
- **走者進塁の二重加算**：**問題なし**（`advanceRunners` [index.html:2902-2907](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L2902-L2907) は `i = 2..0` の逆順走査で `bs[i] = 0` にクリアしてから進めるため、ヒットエンドランの `extra + addRuns(side, bases)` でも同一走者の二重得点加算は発生しない）
- **`rng` 消費順序**：**問題なし**（`startPitch` [index.html:2858-2859](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L2858-L2859) で CPU 球種決定が最初の `rng()` 消費を維持しており、`seed=1` 初回 `0.627...` が `sq` スロットの `'curve'` に落ちる自己テストも合格）
- **キーボード・DOMリスナーの重複登録防止**：**問題なし**（`GameSurface` [index.html:3393-3394](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L3393-L3394)、[index.html:3538-3548](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L3538-L3548) で `keysBound` フラグにより `window` の `keydown`/`keyup` は1回のみ登録され、`root.onclick` / `root.onchange` もプロパティ代入で再マウント時の重複を防止できている）

### 観点3：セーブデータ互換性への影響
- **セーブデータ互換性**：**問題なし**（`Save` モジュール、`CONFIG.STORAGE` の開発用・本番用キー、スキーマ検証 `validateData`、v1移行 `convertV1` / `migrateFromV1`、各モジュールの `exportSlice` / `importSlice` に一切変更はなく、`phase4-check.js` による開発版・本番版の往復／破損復旧／v1移行／二重移行防止テストすべて合格）

---

## 総評（3行以内）
指示書第1便・第2便の仕様（4ボタン打法・二段式バント・不可視の狙い・球種4ボタン・打順補正・S/B/Oランプ・旧UI撤去）は忠実に実装され、セーブ互換性と自己テスト70/70も完全に維持されています。
一方で、①`windup`中の打法ボタン有効化とエンジン側`pitch`限定ガードの不一致による空振り音誤発火、②投球時のキャンバスタップによる球種割当バイパス、③エンドラン／バントの2アウト3塁処理とサヨナラ判定漏れ、④`THROW_SELECT`有効時の`setUi`漏れ・3アウト目処理がP1課題として残っています。
既定設定（`throwSelect: false`）で致命的に壊れるP0はありませんが、実プレイの手触りとエッジケースの整合性を高めるため上記P1の4点を修正することを推奨します。
