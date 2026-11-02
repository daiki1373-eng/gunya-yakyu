# 段階H修正（第2便）確認レビュー

対象ファイル：[index.html](file:///home/hatch/workspace/gunya-yakyu-v2/index.html)（段階H CSS：[index.html:368-413](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L368-L413)）  
前回レビュー：[REVIEW-ANTIGRAVITY-phaseH.md](file:///home/hatch/workspace/gunya-yakyu-v2/REVIEW-ANTIGRAVITY-phaseH.md)  
自動検証結果：`phase4-check.js` 開発版・本番版とも **70/70 合格済み**

---

## 前回指摘した P1（3件）の解消判定

### P1-1：横向きで `.game-top` / `.game-strip` と中央カラム縦積みが Canvas を圧迫
- **判定**：**解消**
- **理由**：
  1. **上下クロームの圧縮・非表示化**：`@media (orientation:landscape)` にて `.game-strip` が `display:none; margin-bottom:0`（[index.html:382](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L382)）で非表示化され、`.game-top` が `height:calc(48px + var(--safe-t)); padding:calc(2px + var(--safe-t)) 6px 2px 10px`（[index.html:376-381](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L376-L381)）に圧縮されました。スコアボード内部のフォント・行高（見出し10px＋2行×11px＋メタ10px＋上下ボーダー2px＝44px）および一時停止ボタン（44×44px）が 44px のコンテンツ高に収まっています。
  2. **単一行グリッド＋オーバーレイ化によるステージ高の最大化**：`.eng` が `grid-template-rows:minmax(0,1fr)`（[index.html:384](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L384)）の単一行グリッドになり、`.eng-stage`（`grid-area:1/2`）が `.eng` のコンテンツ高100%を占有する構造に変わりました。`.eng-status` / `.eng-commentary` は上部オーバーレイ（`align-self:start; z-index:1; pointer-events:none`、[index.html:387-388](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L387-L388)）、`[data-eng-startrow]` / `[data-eng-throw]` / `.eng-meter` / `[data-engine-act="finish"]` は下部オーバーレイ（`align-self:end`、[index.html:399-405](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L399-L405)）として同じセル `1/2` に重なるため、ボタンやテロップの有無で `.eng-stage` の高さが削られなくなりました。
  3. **実機寸法での高さ検証**：
     - **画面高 390px**：`.engine-mount` 外寸 `390 - 48 = 342px`（上下ボーダー 2px を除く内寸 340px）→ `.eng` 上下パディング 12px を除いた **`.eng-stage` 高は 328px**（下セーフエリア 21px 時は **307px**）。
     - **画面高 360px**：`.engine-mount` 外寸 `360 - 48 = 312px`（内寸 310px）→ **`.eng-stage` 高は 298px**（下セーフエリア 21px 時は **277px**）。
     - 縦向き時の Canvas（約 370×208px）を大きく上回る表示サイズが確保されており、段階Hの主目的である「Canvasが画面いっぱいに大きく表示される」を満たしています。

---

### P1-2：低丈横向き（高さ360〜390px）でのスクロール発生・はみ出し
- **判定**：**解消**
- **理由**：
  1. **縦スクロール発生の根絶**：`@media (orientation:landscape)` で `.engine-mount{overflow:hidden}`（[index.html:383](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L383)）および `.eng{height:100%; min-height:0; grid-template-rows:minmax(0,1fr)}`（[index.html:384](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L384)）が指定されたことで、`.eng` の最小高による親スクロール誘発がなくなりました。
  2. **低丈横向き（`max-height:420px`）での収まり検証**：
     - `@media (orientation:landscape) and (max-height:420px)`（[index.html:407-413](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L407-L413)）により、`[data-eng-dpad]` は `52px + 48px + 52px + gap 8px = 160px`（下マージン `56px` 込みで `216px`）、`.eng-diamond` は `48px × 3 + gap 8px = 152px` に収まります。
     - 画面高 360px・下セーフエリア 21px という最も厳しい条件（ステージ高 `277px`）でも、左カラムの `[data-eng-dpad]`（160px）＋下部 `[data-eng-subs]`（48px、dpad の `margin-bottom:56px` 内に収まる）が重ならず 277px 以内に収まり、右カラムの `.eng-diamond`（152px）や中央オーバーレイ群（下端 meter 6px、startrow/throw 14〜62px、finish 70〜118px、上端 status/commentary 0〜40px）もすべてはみ出し・重なりなく収まっています。

---

### P1-3：下部セーフエリア（`safe-area-inset-bottom`）の二重加算
- **判定**：**解消**
- **理由**：
  - 横向き時は `.game-strip{display:none; margin-bottom:0}`（[index.html:382](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L382)）で `.game-strip` の `var(--safe-b)` が無効化され、かつ `.eng-controls{display:contents}`（[index.html:385](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L385)）により `.eng-controls` の下パディングもボックスを持たないため、下部セーフエリアは `.eng` の `padding:6px 6px calc(6px + env(safe-area-inset-bottom,0px))`（[index.html:384](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L384)）の **1箇所のみ** に一本化されています。

---

## P2 対応状況の確認

- **P2-2（打撃時の方向ボタン横幅）**：**対応確認済み**
  - `[data-eng-dpad]:has([data-dir="left"][hidden]):has([data-dir="right"][hidden])`（[index.html:397-398](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L397-L398)）により、左右ボタンが `hidden` になる打撃時のみ上下ボタンが `grid-column:1/-1` で左カラム全幅（164〜240px）に広がり、投球時は従来どおり3列の十字配置が維持されます。
- **P2-3（設定画面表示と `Q3_orientation`）**：**対応確認済み**
  - 冒頭コメント（[index.html:16](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L16)）、`CONFIG.PROVISIONAL.Q3_orientation: 'landscape'`（[index.html:496](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L496)）、設定画面表示 `<b>横向き推奨</b>`（[index.html:4122](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L4122)）がいずれも更新されています。

---

## 新たな P0 / P1 の有無

- **P0（使うと壊れる）**：**該当なし**
- **P1（実機で困る・仕様要求との乖離）**：**該当なし**
  - 中央セル（`1/2`）内でオーバーレイ配置された `[data-eng-startrow]`（打撃時のみ表示）と `[data-eng-throw]`（守備時の送球選択時のみ表示）は表示タイミングが排他であり、練習／10球チャレンジ時の `[data-engine-act="finish"]`（`margin-bottom:70px`）とも位置が分離されているため、操作上の干渉や新たなレイアウト崩れはありません。

---

## 結論

前回挙げた P1-1・P1-2・P1-3 はすべて解消されており、新たな P0 / P1 もありません。段階Hの実装は完了と判定します。
