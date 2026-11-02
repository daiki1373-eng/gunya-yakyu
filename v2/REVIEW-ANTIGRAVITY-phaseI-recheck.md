# 段階I 第一弾 指摘修正の再確認レポート

対象ファイル：[index.html](file:///home/hatch/workspace/gunya-yakyu-v2/index.html)（および生成物 [index-prod.html](file:///home/hatch/workspace/gunya-yakyu-v2/index-prod.html)）  
参照レビュー：[REVIEW-ANTIGRAVITY-phaseI.md](file:///home/hatch/workspace/gunya-yakyu-v2/REVIEW-ANTIGRAVITY-phaseI.md)  
自動検証結果：`NODE_PATH=/tmp/jsdom-test/node_modules node phase4-check.js` → 開発版・本番版とも **self-test 76/76 合格**（データ管理UI・初期化二重確認・本番v1フィクスチャ移行もすべて合格）

---

## 結論

- **P0：0件 / P1：0件**
- 前回レビューの指摘4件（P1-1、P1-2、P2-2、P2-3）はすべて適切に解消されており、今回の修正に伴う新たな P0 / P1 の混入もありません。

---

## 指摘事項の解消確認

### 1. [解消] P1-1：主人公の種族 `species` がセーブ読込（`importSlice`）で消失する問題
- **確認箇所**：
  - `PROTAG_DEFAULT`（[index.html:1369-1373](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L1369-L1373)）
  - `Growth` の `importSlice`（[index.html:1896-1909](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L1896-L1909)）
  - 自己テスト「段階I 主人公の種族は保存往復で保持」（[index.html:5216-5224](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L5216-L5224)）
- **確認結果**：
  - `PROTAG_DEFAULT` に `species: 'human'` が追加され、`importSlice` でも `species: Object.hasOwn(CONFIG.SPECIES, p.species) ? p.species : 'human'` により種族が復元されるようになりました。
  - 合わせて `s.owned` の復元（[index.html:1911-1917](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L1911-L1917)）より先に `s.protagonist` が確定するため、`master('PROTAG')` 参照時にも正しい種族で処理されます。旧セーブ（`p.species === undefined`）でも `'human'` に安全にフォールバックされます。

### 2. [解消] P1-2：必殺予約 `armed` の消費・残留エッジケース（3点）
- **確認箇所**：
  - `specialPhaseReady` / `armSpecial` / `setUi`（[index.html:2802-2822](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L2802-L2822)）
  - `swingStyle`（[index.html:2979-2992](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L2979-L2992)）
  - `changeHalf` / `finish`（[index.html:3073-3090](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L3073-L3090)）
  - `resolveContact`（[index.html:3111-3151](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L3111-L3151)）
  - 自己テスト「段階I 必殺予約は攻守交代で消える」（[index.html:5225-5235](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L5225-L5235)）
- **確認結果**：
  1. **CPU打撃必殺の見逃し消費**：`resolveContact`（[index.html:3114-3116](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L3114-L3116)）で、ボール球見逃し判定 `if (!isStrikeZone(ball) && rng() < 0.55) { addBall(side); return; }` を通過した後に `if (cpuSpecial) game.armed.opponent = null;` で消費するよう修正され、かつ不要な `slowmoT` 付与も除去されました。
  2. **バント時の打撃必殺誤消費**：`swingStyle`（[index.html:2987](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L2987)）で `style !== 'bunt'` のときのみ `game.armed.user` を消費し、`resolveContact` の `currentSwingStyle === 'bunt'` 分岐（[index.html:3129-3137](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L3129-L3137)）も純粋なバント判定・犠打処理のみに整理されました。
  3. **攻守交代時の未消費 `armed` 残留と発動フェーズガード**：`specialPhaseReady()`（[index.html:2802](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L2802)）により打撃時は `['ready', 'windup', 'pitch']`、投球時は `'ready'` でのみ発動可能となり、さらに `changeHalf()`（[index.html:3074](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L3074)）および `finish()`（[index.html:3090](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L3090)）で `game.armed = { user: null, opponent: null }` にリセットされるため、次半イニングで必殺ボタンがロックされる問題は完全に解消しました。

### 3. [解消] P2-2：横向きレイアウトでの `.eng-diamond` と `.eng-gauge` の重なり
- **確認箇所**：
  - `@media (orientation:landscape)` 内 `.eng-diamond`（[index.html:392](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L392)）
- **確認結果**：
  - `.eng-diamond` のマージンが `margin:0 0 72px` に変更され、右カラム下端の `.eng-gauge`（高さ約69px）との重なりが解消されました。

### 4. [解消] P2-3：`.eng-action.special:disabled` の未定義 CSS 変数
- **確認箇所**：
  - `.eng-action.special:disabled`（[index.html:345](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L345)）
- **確認結果**：
  - `color:var(--muted)` から `:root` 定義済みの `color:var(--text-2)` に修正されました。
