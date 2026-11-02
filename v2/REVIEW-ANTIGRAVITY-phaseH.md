# 段階H（横向き・ランドスケープレイアウト）コードレビュー

対象ファイル：[index.html](file:///home/hatch/workspace/gunya-yakyu-v2/index.html)（および生成物 [index-prod.html](file:///home/hatch/workspace/gunya-yakyu-v2/index-prod.html)）  
基準資料：[phaseH-brief.md](file:///home/hatch/workspace/gunya-yakyu-v2/phaseH-brief.md)、[phaseH-review-brief.md](file:///home/hatch/workspace/gunya-yakyu-v2/phaseH-review-brief.md)  
自動検証結果：`NODE_PATH=/tmp/jsdom-test/node_modules node phase4-check.js` → 開発版・本番版とも **self-test 70/70 合格**（段階Gからのスクリプト差分は `GameSurface.markup()` の `.eng-rotate-hint` 1行追加のみ）

---

## P0（使うと壊れる）

- **該当なし**
  - 横向き・縦向きともに試合進行不能・クラッシュ・セーブ破損を引き起こす P0 不具合はありません。

---

## P1（実機で困る・仕様要求との乖離）

### 1. 横向き時に試合画面の上下クローム（`.game-top` / `.game-strip`）と中央行が縦幅を圧迫し、Canvas が縦向き時より小さくなる（要求1「Canvasが画面いっぱいに大きく表示される」の不成立）
- **根拠箇所**：
  - `.game` / `.game-top` / `.sb` / `.engine-mount` / `.game-strip`（[index.html:270-289](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L270-L289)）
  - `@media (max-height:700px)`（[index.html:346](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L346)）
  - `@media (orientation:landscape)` 内の `.eng` / `.eng-stage` / `[data-eng-startrow]`（[index.html:375-393](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L375-L393)）
- **内容**：
  - 試合画面（`S.game`、[index.html:3921-3925](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L3921-L3925)）は外枠 `.game` の中に `.game-top`（スコアボード＋一時停止ボタン：約76〜82px）、`.engine-mount`（`flex:1 1 auto`）、`.game-strip`（選手帯：`max-height:700px` 適用時で 58px＋`margin-bottom:var(--safe-b)`）を縦積みしています。`@media (orientation:landscape)` で `.game-top` と `.game-strip` が圧縮・非表示化されていないため、**横向き時の高さから常に約 135〜160px が上下クロームに奪われます**。
  - さらに `.eng` の中央カラム（[index.html:375](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L375)）は、ステージ（row 3）の上下に `.eng-status`（row 1: 20px）、`.eng-commentary`（row 2: 20px）、`[data-eng-startrow]`（row 4: 打撃時は常時表示で **52px**）、`.eng-meter`（row 7: 10px）、および上下 `padding`（12px＋`safe-b`）をすべて縦に積んでいます（計 **約 114px**）。
  - **実機寸法での計算例（iPhone 14 横向き：844×390px、または一般的な横向き 667×360px）**：
    - 画面高 390px − `.game-top` (78px) − `.game-strip` (58px) ＝ **`.engine-mount` の高さはわずか約 254px**（画面高 360px 端末では **約 224px**）。
    - `.eng` は `min-height:240px`（[index.html:375](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L375)）または親の 254px になりますが、中央カラムで row 1, 2, 4, 7 と padding が **114px** を消費するため、row 3（`minmax(0,1fr)`）の **`.eng-stage` に残る高さは 254 − 114 ＝ 約 140px（画面高 360px 端末では 240 − 114 ＝ 126px）** しかありません。
    - Canvas は `object-fit:contain`（16:9）のため、高さ 140px に律速されて **描画サイズは約 249×140px（または 224×126px）** に縮小されます。これは**縦向き時の Canvas（幅 370px × 高さ 208px）よりもさらに小さい表示**になっており、段階Hの最優先仕様「Canvasが画面いっぱいに大きく表示される。縦向き時の小さな表示が難しさの主因だった」が達成できていません。
- **修正方針**：
  - 横向き試合中（`@media (orientation:landscape)` の `.game`）では、Canvas 内にすでにイニング・スコア・カウント・走者ヘッダー（`drawHeader` [index.html:3146-3165](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L3146-L3165)）が描かれているため、`.game-strip` を非表示（または `.game-top` と1行に統合・コンパクト化）にして `.engine-mount` に画面高の大半を割り当てる。
  - 打撃時の `[data-eng-startrow]`（投球開始ボタン）は、`ready` フェーズ以外では `disabled` になってただ場所を52px占有しているため、`!v.canStartPitch` のときは `hidden` にするか、右カラム／左カラムの上下余白やステージ上のオーバーレイ等に逃がして中央カラムの縦スペースを `.eng-stage` に開放してください。

---

### 2. 横向き時の高さが小さい端末（高さ360〜390px）で、左右カラムの `[data-eng-dpad]`（188px）と `.eng-diamond`（164px+）が中央行の最小高さ合計を上回り、`.engine-mount` に縦スクロールが発生・誤スクロールを招く
- **根拠箇所**：
  - `.engine-mount`（[index.html:282](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L282)：`overflow-y:auto`）
  - `.eng` / `.eng-diamond` / `[data-eng-dpad]`（[index.html:375](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L375)、[index.html:381-385](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L381-L385)）
- **内容**：
  - `[data-eng-dpad]` は `grid-area:1/1/-1/2`、`.eng-diamond` は `grid-area:1/3/-1/4` で明示的な全8行（line 1〜9）をスパンしています。
  - `[data-eng-dpad]` の高さは `grid-template-rows:64px 52px 64px` ＋ `gap:4px`×2 ＝ **188px**、`.eng-diamond` の高さは `repeat(3,minmax(52px,auto))` ＋ `gap:4px`×2 ＝ **最低 164px**（「ヒットエンドラン」「ウエストボール」が2行折り返しになると **約 180〜200px**）です。
  - CSS Grid のサイズ計算仕様上、複数トラックをまたぐアイテム（`1 / -1`）の最小高さが各行の合計を超える場合、フレキシブルトラックである row 3（`minmax(0,1fr)`）の最小サイズは `0` のまま、**`auto` トラック群（row 1, 2, 4〜8）に余剰分が均等配分**されるか、`.eng` の `height:100%`（例：224〜254px）および `min-height:240px` との兼ね合いで：
    1. **row 3（`.eng-stage`）がさらに潰れる**：188px 分の行高を `auto` トラック群が吸収し、残余空間（`1fr`）が削られる。
    2. **高さ360px前後の端末（モバイルブラウザのアドレスバー表示時を含む）で `.engine-mount` が 240px 未満になる**：`.eng{min-height:240px}` により `.engine-mount`（`overflow-y:auto`）に縦スクロールバーが発生し、親指でボタンや画面を操作した際に `.engine-mount` 全体が上下にスクロールしてしまいます。
    3. **代打・投手交代（`[data-eng-subs]`）や終了ボタン表示時に中央カラムの `auto` 行合計が 240px を突破**：その瞬間に `.eng` のコンテンツ高が `height:100%` を超えて下にはみ出し、スクロールしないと終了ボタンや下段ボタンが押せなくなります。
- **修正方針**：
  - P1-1 のとおり `.game-top` / `.game-strip` を圧縮して `.engine-mount` の高さを 300px 以上確保したうえで、低身長横向き（例：`@media (orientation:landscape) and (max-height:420px)`）では `[data-eng-dpad]` の上下行を `52px 48px 52px` 程度に抑える、または `.engine-mount` を横向き時は `overflow:hidden` にしてスクロール揺れを防ぐことを推奨します。

---

### 3. 横向きで `.hdr` / `.tabs` には左右セーフエリアが二重に当たらず中央寄せされる一方、試合画面（`.game-top` / `.game-strip` / `.eng`）では `safe-area-inset-bottom` が二重加算される
- **根拠箇所**：
  - `.app`（[index.html:372](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L372)）
  - `.game-strip`（[index.html:283](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L283)：`margin-bottom:var(--safe-b)`）
  - `.eng`（[index.html:375](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L375)：`padding:6px 6px calc(6px + env(safe-area-inset-bottom,0px))`）
- **内容**：
  - 試合画面では `.game` の最下部に `.game-strip`（`margin-bottom:var(--safe-b)`）があり、`.engine-mount` はその**上**に位置しています。
  - しかし [index.html:375](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L375) の `.eng` にも `padding-bottom:calc(6px + env(safe-area-inset-bottom,0px))` が指定されているため、iPhone 等のホームインジケータ付き端末（横向き時 `safe-area-inset-bottom: 21px`）では、**`.game-strip` の下で 21px 空いたうえに、`.engine-mount` 内の `.eng` 下部でもさらに 21px が空く二重計上**になっており、貴重な横向きの縦スペースを 21px 余分に削っています。
- **修正方針**：
  - `.game-strip` が下端にある場合は `.eng` 側の `env(safe-area-inset-bottom,0px)` を外す（逆に `.game-strip` を横向きで非表示にする場合は `.eng` 側だけに残す）ように一本化してください。

---

## P2（改善余地）

### 1. `.hdr-in` に `padding-top:env(safe-area-inset-top)` が指定されており、親 `.hdr` の `padding-top:var(--safe-t)` と二重になっている（かつ高さ固定で中身を圧迫）
- **根拠箇所**：
  - `.hdr` / `.hdr-in`（[index.html:52-53](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L52-L53)）
  - `.hdr-in`（[index.html:327](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L327)）
- **内容**：
  - 52行で `.hdr{padding-top:var(--safe-t)}` が効いているうえに、327行で `.hdr-in{padding-top:env(safe-area-inset-top)}`（`.hdr-in` は `height:56px` 固定かつ `box-sizing:border-box`）が入っているため、上部セーフエリアがある端末でヘッダー内のテキストやボタンが下へ押し下げられます（段階G以前からの既存記述ですが、横向き確認とあわせて整理すると綺麗です）。

### 2. 打撃時（`v.batting === true`）の `[data-eng-dpad]` で左右ボタンが `hidden` のときも、左カラムの1列目・3列目（各52px以上）が空列のまま残り、上下ボタンが横幅の1/3（約52〜77px）に制限される
- **根拠箇所**：
  - `[data-eng-dpad]` / `[data-dir]`（[index.html:383-386](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L383-L386)）
  - `GameSurface.update`（[index.html:3499](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L3499)）
- **内容**：
  - 打撃時は `[data-dir="left"]` と `[data-dir="right"]` が `hidden` になり、`[data-dir="up"]`（`grid-area:1/2`）と `[data-dir="down"]`（`grid-area:3/2`）だけが表示されます。
  - 3列グリッドの2列目に配置されているため十字の中央位置は保たれますが、仕様2「左手側に上下（高さ・軌道）の方向入力エリア」「高さ・軌道の上下が主役なので、上下ボタンを大きく縦に配置」に対して、打撃時は上下ボタンの横幅がカラム幅の1/3（約52px）に留まります。打撃時（左右ボタンが `hidden` のとき）や上下ボタン自体をもう少し幅広（または `grid-column:1/-1` や中央列広め）にすると、親指をラフに置いても押しやすくなります。

### 3. 設定画面の「画面の向き」表示および `CONFIG.PROVISIONAL.Q3_orientation` が「縦で統一」のまま残っている
- **根拠箇所**：
  - 冒頭コメント（[index.html:16](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L16)）
  - `CONFIG.PROVISIONAL.Q3_orientation`（[index.html:476](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L476)）
  - `Screens.config`（[index.html:4102](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L4102)：`<div class="kv"><span>画面の向き</span><b>縦で統一（Q3=A 仮置き）</b></div>`）
- **内容**：
  - 実装指示書（[phaseH-brief.md:40-41](file:///home/hatch/workspace/gunya-yakyu-v2/phaseH-brief.md#L40-L41)）では任意項目とされていますが、ユーザーが見える設定画面（`S.config` 4102行）に「画面の向き：縦で統一（Q3=A 仮置き）」という表示が残っているため、段階Hの「横向きがおすすめ」と表示上の矛盾があります。

### 4. デバッグFABボタン（`.dbg-fab`）の右端位置計算が縦幅430px前提のまま
- **根拠箇所**：
  - `.dbg-fab`（[index.html:395](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L395)：`right:max(10px,calc(50% - 205px))`）
- **内容**：
  - `?debug=1` 時のみ表示されるボタンですが、`calc(50% - 205px)` のため横向き時には画面中央寄り（右から約200px内側）に浮いて表示されます。横向きでは `right:max(12px, env(safe-area-inset-right))` 等に寄せるとなお良いです。

---

## 「特に見てほしい点」への回答一覧

1. **中央カラムのグリッド行定義（`grid-template-rows:auto auto minmax(0,1fr) repeat(5,auto)`）と各要素の `grid-area` の対応／`hidden` 要素の挙動／全行スパンとの干渉**
   - **グリッド配置と `hidden` の挙動**：`.eng-controls{display:contents}` により子要素が `.eng` の直接のグリッドアイテムになり、row 1（`.eng-status`）、row 2（`.eng-commentary`）、row 3（`.eng-stage`）、row 4（`[data-eng-startrow]`）、row 5（`[data-eng-throw]`）、row 6（`[data-eng-subs]`）、row 7（`.eng-meter`）、row 8（`[data-engine-act="finish"]`）がすべて列2に1対1で対応しています。`[hidden]{display:none!important}`（37行）と `.eng-row[hidden]{display:none}`（332行）が効くため、`hidden` の要素が置かれた `auto` 行は高さ `0px` に折りたたまれ、また `.eng` には `row-gap` が未指定（`column-gap:6px` のみ）のため、非表示行が余分な隙間を作ることもありません。
   - **全行スパン（`1/1/-1/2`・`1/3/-1/4`）との干渉**：列1・列3に明示配置されているため列2との水平方向の重なりはありませんが、**垂直方向のトラックサイズ計算で干渉します（上記 P1-2）**。明示8行すべてをまたぐため、左右カラムの高さ（dpad 188px / diamond 164px+）と中央カラムの `auto` 行群＋`minmax(0,1fr)` が同じ8行の総高を共有し、画面高が小さい端末で row 3 の圧迫や `.eng` 全体の最小高さ押し上げを引き起こします。

2. **`.screen{max-width:720px}` と「Canvasを画面いっぱいに大きく」の整合性／`.hdr-in,.tabs` セレクタの有効性**
   - **`.screen{max-width:720px}` は試合画面には影響しない**：試合画面（`S.game`、[index.html:3921](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L3921)）のルート要素は `<section class="game">` であり、`.screen` クラスを持っていません。そのため `.screen{max-width:720px}` によって試合画面の横幅が制限されることはなく、`.game` と `.engine-mount` は `.app` の全幅（左右セーフエリアを除く100%）を使えています（Canvas が小さくなる原因は横幅制限ではなく、**P1-1 で挙げた上下方向のクローム圧迫**です）。
   - **`.hdr-in,.tabs` セレクタの実在性**：[index.html:407](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L407) が `<nav class="tabs" id="tabs" aria-label="メインメニュー"></nav>`、[index.html:3634](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L3634) が `<div class="hdr-in">...</div>` であるため、**`.hdr-in` と `.tabs` はどちらも実在する要素に正しく効いています**。なお `.tabs` 自体に `max-width:720px;margin-inline:auto` を当てているため、720px超の横長画面では下部ナビバーの背景色（`var(--stand)`）と上ボーダーが左右720pxで途切れ、外側に `.app` の `var(--navy)` が見えます（ヘッダー `.hdr` は親が全幅で中身 `.hdr-in` だけ720px中央寄せなのに対し、タブは `<nav class="tabs">` 自体が720pxに縮む違いがありますが、レイアウト崩れではありません）。

3. **Canvas の `position:absolute;inset:0;width/height:100%;object-fit:contain` による16:9維持とタップ座標のズレ**
   - **16:9維持**：内部解像度 `width="960" height="540"`（16:9）に対し `object-fit:contain` が効くため、`.eng-stage` のアスペクト比が16:9から外れても描画自体は歪まず16:9に保たれ、余白は `.eng-stage` の `background:#07152B` でレターボックス表示されます。
   - **タップ座標とのズレ**：Canvas 上のイベントリスナーを確認したところ、[index.html:3605](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L3605) の `canvas.addEventListener('pointerdown', () => { ... if (lastView && lastView.canStyle) MatchEngine.swingStyle('west'); else if (lastView && lastView.canStartPitch) MatchEngine.startPitch(); })` のみで、**`clientX` / `clientY` / `offsetX` / `offsetY` 等のポインタ座標計算は一切行われていません**（段階Gで旧ミートカーソル・ゾーンタップが全廃されたため）。したがって、`object-fit:contain` のレターボックス余白によって座標ズレが起きる問題は**ありません**。

4. **横向きで高さが極端に小さい端末（高さ360px前後）でのダイヤモンドと dpad の収まり**
   - **カラム幅（水平方向）**：左右カラム幅は `clamp(164px,23%,240px)`（[index.html:375](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L375)）で最小164pxが確保されており、3列×最小52px＋`gap:4px`×2 ＝ **164px** と完全に一致するため、水平方向のはみ出しはありません。
   - **カラム高（垂直方向）**：**P1-1 / P1-2 のとおり問題があります**。画面高360pxでは `.game-top`（約78px）と `.game-strip`（58px）を除いた `.engine-mount` の高さが約224pxしかなく、`.eng{min-height:240px}` によって `.engine-mount` に縦スクロールが発生します。ボタン自体が画面外へ消えて全く押せなくなるわけではありませんが、スクロールが発生するうえに中央の Canvas が高さ約126pxまで潰れます。

5. **縦向きレイアウトが段階G時点から変わっていないか（横向きメディアクエリの外への影響）**
   - **問題なし**：`@media (orientation:landscape)` の外で変更されたのは、①旧 `.rotate` ルールと `<div class="rotate">` の削除、② `.eng-rotate-hint{display:none...}` と `@media (orientation:portrait){.eng-rotate-hint{display:block}}` の追加、③ `GameSurface.markup()` の `.eng-controls` 先頭への `<p class="eng-rotate-hint">横向きがおすすめです</p>` 追加のみです。縦向き時の Canvas（幅100%・`aspect-ratio:16/9`）と下部操作盤の縦積みレイアウトは段階G時点から一切崩れていません。

6. **`.eng{min-height:240px}` や `.eng{height:100%}` と親要素（`.engine-mount`）の関係**
   - 親の `.engine-mount`（[index.html:282](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L282)）は `.game`（`display:flex;flex-direction:column;height:100%`）のフレックス子要素（`flex:1 1 auto;min-height:0`）であるため、CSS仕様上 `.engine-mount` の高さは確定（definite）しており、子の `.eng{height:100%}` は正しく解決されます（その結果 `.eng-stage` の `minmax(0,1fr)` も機能し、`position:absolute;inset:0` の `.eng-canvas` も高さ0にならず描画されます）。
   - ただし、前述（P1-1 / P1-2）のとおり `.game-top` と `.game-strip` が残っている状態で `.engine-mount` の高さが 240px を下回ると、`.eng{min-height:240px}` が `.engine-mount` の高さを超えて縦スクロールを発生させます。

---

## 総評

`.eng-controls{display:contents}` を活用した DOM 構造無改変での3カラムグリッド化、旧 `.rotate` ブロックの撤廃と縦向き時のみの控えめな `.eng-rotate-hint` 表示、段階Gのゲームロジック・セーブ互換性・自己テスト（70/70）の完全維持は非常にクリーンに実装されています。  
一方で、**横向き試合画面で `.game-top`（スコアボード）と `.game-strip`（選手帯）および中央カラムの `[data-eng-startrow]`（投球開始ボタン）が縦幅を合計約250px以上消費しているため、肝心の `.eng-stage`（Canvas）の高さが約126〜140pxに潰れて縦向き時より小さくなってしまう点（P1-1・P1-2）** と、**下部セーフエリアの二重計上（P1-3）** が実機ランドスケープ表示での大きな課題です。横向き時の `.game-strip` 非表示（またはヘッダー統合）と `[data-eng-startrow]` の省スペース化を行えば、段階Hの狙いどおり画面いっぱいの快適な横向きプレイ環境が完成します。
