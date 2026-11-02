`SPEC-SUMMARY.md`、`index.html`、`INTEGRATION-NOTES.md`、`REVIEW-ANTIGRAVITY-phase1.md` を通読し、段階2（保存・リーグ進行・シーズン報酬・能力補正）および段階3（育成・スカウト実排出・ミッション／ログインボーナス）の実装をレビューしました。

前回P0だった2件（CPUデータ生成・第10節後のシーズン終了導線）および `match:end` トランザクションの一本化、保存データの事実化（`results` のみ保存）は高い品質で解消されています。一方、段階2・3の本実装に伴って**経済上限（Q1=B）による報酬消失バグ**、**イニング交代時の走者残留バグ**、**編成・ローテ周りの育成導線欠落**などが見つかりました。

以下に **P0 / P1 / P2** の優先度別指摘、**問題がない観点**、および**前回レビュー残件の解消状況**をまとめます。

---

## P0（致命的：報酬消失・ゲームルール破綻・進行上の重大不具合）

### 1. 非リーグ1日300コイン上限（Q1=B）により、通算ミッションやログボ等の報酬が切り捨て・0コイン消失し「受取済み」になる
- **該当箇所**：[index.html](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L618-L627)、[index.html](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L666-L685)（`capNonLeague` / `earn`）、[index.html](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L804-L809)（`grantMissionReward`）、[index.html](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L893-L904)（`LoginBonus.claim`）、[index.html](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L1117-L1121)（`Growth.acquire`）
- **根拠（問題点）**：
  1. **300コイン超の報酬が絶対に満額受け取れない**：通算ミッションの `hr50`（1,000コイン）・`champion`（1,000コイン）・`lv20`（500コイン）・`legend`（500コイン）、および限界突破上限後の重複変換（★3＝500コイン、★4＝1,000コイン）がすべて `source = 'nonLeague'` で `Economy.earn()` を通っています。`capNonLeague` が1日300コインで切り捨てるため、**本日獲得0コインの状態で受け取っても最大300コインしか入らず、差額の200〜700コインが永久に消失**します。
  2. **上限到達後に受け取ると0コインで「受取済み」になり権利が消滅する**：デイリー3件（100×3＝300コイン）等で1日上限に達したあと、通算ミッション・デイリーミッション・ログインボーナス・リワード広告を受け取ると、`earn()` の戻り値が `{}`（0コイン）であるにもかかわらず `claimed = true` / `s.login.lastDayKey = status.dayKey` / `s.ads.rewardCount += 1` が確定・保存されます。一度きりの通算ミッション報酬や当日ログボが**0コインのまま永久に失われます**。
- **修正案**：
  - **対象範囲の整理**：Q1=B（非リーグ1日300コイン上限）の対象を「非リーグ試合（練習・10球チャレンジ・エキシビション）のヒット／本塁打コイン（＋リワード広告）」に限定し、回数制限が自体に備わっている通算ミッション・ログインボーナス・重複カード変換・デイリーミッションは `source = 'mission'` / `'login'` / `'duplicate'` として日次上限の対象外にする（[SPEC-SUMMARY.md](file:///home/hatch/workspace/gunya-yakyu-v2/SPEC-SUMMARY.md#L39) の「1シーズン約3,350コイン目安」とも整合します）。
  - **上限到達時の受取ガード**：もしミッションやログボ等も上限対象に含める仕様であれば、`Economy.getDailyCap().remaining < reward.coins` のときは `claim()` / `grantReward()` を `{ ok: false, reason: '本日のコイン獲得上限に達しているため受け取れません' }` で拒否し、**受取済みフラグを立てない**ようにしてください。

---

### 2. 試合エンジンで3アウト交代（`changeHalf`）時に塁上の走者（`game.bases`）がリセットされない
- **該当箇所**：[index.html](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L1771-L1781)（`addRuns`）、[index.html](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L1831-L1841)（`changeHalf`）、[index.html](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L1944)（`start`）
- **根拠（問題点）**：
  - `game.bases`（`{ user: [0, 0, 0], opponent: [0, 0, 0] }`）は試合開始時（1944行）と本塁打時（1774行）にしか初期化されていません。
  - `changeHalf()`（1831行）で `game.outs = 0` にする際、`game.bases` を `[0, 0, 0]` にクリアしていないため、**前のイニングに残塁した走者が次のイニングにそのまま残り、次イニングの安打で得点してしまいます**。
  - また、非リーグ戦（練習・10球チャレンジ）でもアウト時に走者がリセットされず常に満塁状態になり続けます。
- **修正案**：
  - `changeHalf()` の先頭で `game.bases.user.fill(0); game.bases.opponent.fill(0);` を実行してください（非リーグ戦のアウト時 1827行でも必要に応じてクリア、または練習用の走者ルールを明示してください）。

---

## P1（重要：仕様不一致・機能欠落・境界／イベント順序の不備）

### 3. 新規獲得した投手を先発ローテに組み込む手段がなく、先発ローテ（`nextStarter`）も試合ごとに回らない
- **該当箇所**：[index.html](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L1005-L1006)、[index.html](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L1151-L1187)（`Growth.settleMatch` / `setLineupPosition` / `autoLineup`）、[index.html](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L2373)（`Screens.team`）
- **根拠（問題点）**：
  1. **スカウトで引いた投手が永久に使えない**：`s.rotation` は初期投手 `['S10', 'S11', 'S12']` で固定されており、手動入れ替えが未実装なだけでなく、`autoLineup()`（1177〜1187行）も野手（`type === 'B'`）しか更新しません。そのため、スカウトで★3 `P103`（鳴海）や★4 `P204`（黒鉄）を獲得しても、**試合に出す手段がゲーム内に一切存在しません**。
  2. **控え野手の手動入れ替え不可**：`cycleLineupPosition()`（1172〜1176行）はスタメン9人の中で守備位置をローテーションするだけで、所持一覧（10人目以降の野手）とスタメンを手動で入れ替えるUIがなく、`autoLineup()`（総合力順の自動選出）でしか新規獲得野手をスタメンに入れられません。
  3. **ローテが進行しない**：画面には「先発ローテ／順番に先発します」「次の先発」（2012, 2373行）と表示されますが、試合終了時に `s.nextStarter = (s.nextStarter + 1) % s.rotation.length` を進める処理がどこにもなく、常に1番手の `S10`（千葉）だけが先発し続け、他2人の先発投手には出場経験値も入りません。
- **修正案**：
  - `Growth.settleMatch()`（または `League.settleMatch()` 連携）のリーグ戦完了時に `s.nextStarter = (s.nextStarter + 1) % s.rotation.length` を進める。
  - `autoLineup()` で投手も総合力上位3人を `s.rotation` に編成するようにする（あわせて投手・野手の手動交代導線も追加するのが望ましいです）。

---

### 4. リーグ戦の途中放棄（`match:aborted`）でもデイリーミッション進捗と途中セーブが確定してしまう（経済・進行の抜け穴）
- **該当箇所**：[index.html](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L867-L880)（`Missions` の `match:hit` / `match:strikeout` リスナー）、[index.html](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L1527-L1542)（`League` の同リスナー）
- **根拠（問題点）**：
  - 試合放棄時の説明（2575行）および仕様（[SPEC-SUMMARY.md](file:///home/hatch/workspace/gunya-yakyu-v2/SPEC-SUMMARY.md#L21)）では「途中放棄は未消化で再挑戦可、記録・コイン・経験値は増えない」となっています。
  - しかし `Missions` の `match:hit` / `match:strikeout` リスナー（867〜876行）は、リーグ戦の最中であっても1打席ごとに `s.missions.progress.hits5` / `k3` を即時加算し、その場で `Save.commit('missionProgress:hit')` を呼んでいます。
  - これにより、**リーグ戦でヒットや三振を稼いでから一時停止→「試合をやめる」を繰り返すことで、節を進めずにデイリーミッションを達成して報酬を受け取る抜け穴**が生じています。
  - さらに、非リーグ戦において `match:hit` / `match:strikeout` 発火時のリスナー登録順が `Economy`（712行）→ `Missions`（867行）→ `League`（1532行）となっているため、`Save.commit` が走った**後**に `League.flush()`（1536, 1541行）で通算記録 `s.career` が更新され、直後のセーブに通算本塁打・奪三振が乗らない順序逆転も起きています。
- **修正案**：
  - リーグ戦中のミッション進捗（`hits5` / `k3`）も `Economy` や `League` と同様、試合中は `pending` に溜めておき、`MatchFlow`（`match:end`）の中で一括確定・1回保存し、`match:aborted` では破棄してください。
  - 非リーグ戦での打席ごとの更新も、`League.flush()` → `Missions` 進捗更新 → `Economy.earn()` → `Save.commit()` の順になるよう順序を一元化してください。

---

### 5. 試合中の相手（CPU）打者表示が「相手打者／段階2で表示」のプレースホルダーのままになっている
- **該当箇所**：[index.html](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L2229-L2241)（`GameHud.drawStrip`）
- **根拠（問題点）**：
  - 段階2で `cfg.teams.opponent.lineup` にCPU野手9人の名前・守備位置・能力値・`cardId` が入ったにもかかわらず、`GameHud.drawStrip` の2234行が `const b = userBat ? cfg.teams.user.lineup.find((x) => x.cardId === e.cardId) : null;` のままになっており、守備時には常に **`1番 相手打者 / 段階2で表示`** と表示されます。
- **修正案**：
  - 2234行を `const b = cfg.teams[e.side].lineup.find((x) => x.cardId === e.cardId);` に変更し、相手打者の守備位置・名前・能力値（`ミ/パ/走`）を表示してください。

---

### 6. 相手（CPU）投手が「ユーザーがショップで解禁した球種・フォーム・マイ変化球」を使って投球している
- **該当箇所**：[index.html](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L1740-L1760)（`MatchEngine.startPitch`）、[index.html](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L1921-L1925)（`MatchEngine.draw`）
- **根拠（問題点）**：
  - `startPitch()` の1744〜1745行で、CPU投手（`ps === 'opponent'`）の球種抽選に `cfg.unlockedPitches`（ユーザーのショップ解禁球種）を使っています。
  - そのため、ユーザーが初期状態（`fastball` のみ解禁）だと、得意球種 `special: ['curve']` や `['slider']` を持つCPU投手も**ストレートしか投げられず、`HOOKS.pitchBreak` の得意球種補正（1.15倍）が一切発動しません**。逆にユーザーが `mypitch` を解禁すると、CPU投手がユーザーのマイ変化球設定や投球フォーム設定を使って投げてきます。
- **修正案**：
  - `ps === 'opponent'` のときは、`['fastball', ...(p.special || [])]` から球種を選ぶようにし、フォーム・モーション補正（`cfg.play`）は `ps === 'user'`（または打撃時 `userBatting()`）にのみ適用してください。

---

## P2（軽微：UI挙動・エッジケース・段階4に向けた改善）

### 7. `Missions.claim(id, date)` で `grantMissionReward` に `date` 引数が渡っていない
- **該当箇所**：[index.html](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L804-L809)、[index.html](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L847)、[index.html](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L853)
- **根拠（問題点）**：
  - `allClear`（838行）では `earn(..., { commit: false, date })` と `date` を渡していますが、デイリー・通算ミッション用の `grantMissionReward(m, bucket)`（804行）は `date` を受け取っておらず、日付指定テスト時に `capNonLeague` が実時刻（`new Date()`）を参照してしまいます。
- **修正案**：
  - `function grantMissionReward(m, bucket, date)` とし、`earn(m.reward, ..., { commit: false, date })` に `date` を引き継いでください。

### 8. 10連スカウト演出（`reveal`）から「選手詳細」へ進み「戻る」を押すと、未開封の裏面状態に戻る
- **該当箇所**：[index.html](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L2418-L2430)（`Screens.reveal`）、[index.html](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L2640-L2647)（`Actions.flipCard`）
- **根拠（問題点）**：
  - `reveal` 画面でカードをめくった後に「選手詳細」へ遷移し、戻るボタンで `reveal` に戻ると、`params.opened` が保持されていないため再び裏面（タップでめくる状態）からやり直しになります。また、10連時も「選手詳細」ボタンは1人目（`results[0]`）にしかありません。
- **修正案**：
  - `flipCard()` 実行時に `Router.current().params.opened = true` 等を記録し、`S.reveal` 描画時に開封済みなら最初から `.is-open` と `#reveal-after` を表示する（10連の一覧各行にも `data-go="player" data-id="..."` を付けるとなお良いです）。

### 9. 保存読込（`Growth.importSlice`）で編成（`lineup` / `rotation`）の所持チェックが行われていない
- **該当箇所**：[index.html](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L1243-L1257)（`Growth.importSlice`）
- **根拠（問題点）**：
  - `data.team.lineup` と `data.team.rotation` の検証で `master(x.cardId)` の存在だけを確認しており、`s.owned[x.cardId]` に存在するかを検証していません（処理順序も `data.cards` の読込より前になっています）。データ改変や部分破損時に未所持選手がスタメンに入る余地があります。
- **修正案**：
  - `data.cards` を先に読み込んだうえで、`lineup` と `rotation` の各 `cardId` が `s.owned` に含まれることを条件に加えてください。

### 10. 第10節消化後・シーズン報酬受取前にホーム／試合ハブを見ると「次の試合 第10節」と表示される
- **該当箇所**：[index.html](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L1472-L1478)（`League.getNextMatch` / `getSeason`）、[index.html](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L2134)（`Shell.refreshHeader`）
- **根拠（問題点）**：
  - 主ボタンは正しく「シーズン結果へ」に切り替わりますが、`getNextMatch()` が `Math.min(s.round, 9)` を返すため、消化済みの第10節が「次の試合 第10節」、日程表の10節目も「次」ハイライトのまま表示されます。
- **修正案**：
  - `League.isSeasonComplete()` が `true` のときは、ヘッダーや電光掲示板の見出しを「全10節終了（シーズン結果待ち）」とし、日程表の `isNext` ハイライトを外してください。

---

## 前回レビュー（段階1）P0 / P1 / P2 の解消状況一覧

| ID | 前回指摘内容 | 段階2・3での状態 | 確認根拠 |
|---|---|---|---|
| **P0-1** | 能力補正ON時、CPU側データが空でクラッシュ | **解消** | `League.generateCpuTeam`（[1305〜1337行](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L1305-L1337)）でCPU野手9人＋投手の能力と `mult` をシード生成。 |
| **P0-2** | 第10節終了後にシーズン終了画面へ進む導線がない | **解消** | `League.resultDestination`（[1649行](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L1649)）、`Screens.result`（[2349行](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L2349)）、`Actions.startLeague`（[2564行](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L2564)）で `season` へ分岐。 |
| **P1-3** | デモ広告の契機（ショップ5秒リワード広告・スカウト無効ボタンと要約仕様「設定変更だけ」の不一致） | **未決（残存）** | [INTEGRATION-NOTES.md](file:///home/hatch/workspace/gunya-yakyu-v2/INTEGRATION-NOTES.md#L39) 記載のとおり判断保留中。段階4前に仕様の正で確定が必要。 |
| **P1-4** | DOM操作の分散（Router/Shell外でのDOM操作） | **一部改善・一部残存** | `GameSurface` 切り出しと区画統合は完了。ただし `Debug`（[2671, 2676, 2680行](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L2671-L2680)）が画面遷移区画外で `document.getElementById` を直接呼んでいる。 |
| **P1-5** | MatchEngine接続点が2本を超えている | **残存** | `mount` / `pause` / `resume` / `abort` / `isRunning` に加え `startPitch` / `swing` / `choosePitch` / `finish` を直接呼んでいる（[2078〜2094行](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L2078-L2094)）。仕様書側の境界定義に制御API・描画ポートを明記するか要整理。 |
| **P1-6** | 非リーグ戦の MatchConfig 定義不足 | **一部対応** | 10球終了・手動終了は実装されたが、`MatchConfig` ではなくエンジン内の `cfg.mode` 分岐（[1696, 1716, 1811行](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L1696-L1811)）に直接書かれている。 |
| **P1-7** | 3回制の終了ルールとアウト通知の欠落 | **解消（※P0-2の走者バグあり）** | 3回表後攻リード終了・サヨナラ・3回裏終了・`match:out` 通知とHUD即時更新を実装（[1812, 1824, 1831〜1841行](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L1812-L1841)）。 |
| **P1-8** | `match:end` リスナーの実行順序依存とクリーンアップ | **大部分解消** | `MatchFlow`（[2690〜2702行](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L2690-L2702)）で `Economy→Growth→League→Save.commit` の1回保存に集約。ただし `Missions` の `match:end`（[877行](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L877)）と `match:hit`/`strikeout` の順序分散（上記P1-4）が残っているため、`Missions` も `MatchFlow` に統合すべき。 |
| **P2-9** | 保存データに導出可能なリーグ成績 `{w,l,d}` が含まれる | **解消** | `league` スライスは `{ season, results }` のみ保存し、順位・勝敗・日程・CPU選手はすべて再計算（[1606〜1620行](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L1606-L1620)）。 |
| **P2-10** | `match:pitch` で球速表示 `#g-kmh` が更新されない | **解消** | `GameHud`（[2252行](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L2252)）で `#g-kmh` と `#g-count` の両方を更新。 |
| **P2-11** | 一時停止→「試合をやめる」確認をキャンセルすると即再開される | **未解決（残存）** | `Actions.askQuit`（[2577行](file:///home/hatch/workspace/gunya-yakyu-v2/index.html#L2577)）のキャンセル時コールバックが `() => MatchEngine.resume()` のまま。`() => Shell.openModal('pause', {}, { onClose: () => MatchEngine.resume() })` に戻すべき。 |

---

## 問題がないことを確認した観点

以下の観点は仕様どおり正しく実装・保護されていることを確認しました。

- **数値・確率・計算式の整合性**：
  - 能力補正式 `m = 1 + 0.30 * (v - 50) / 50`、適性外守備 `-30%`（基礎能力を書き換えず `MatchEngine.HOOKS.fielderRange` のみで適用）、CPUシーズン強化（毎シーズン＋3、シーズン6の＋15上限）、CPU同士の自動決着式（引き分け8%＋二乗勝率比・固定シード）。
  - 経験値（出場＋60・勝利＋40・MVP＋100、特訓100コインで＋150、必要経験値 `10×Lv＋40`）、限界突破（最大4回、1回につき最大Lv＋5）。
  - スカウト排出率（★4: 3% / ★3: 12% / ★2: 35% / ★1: 50%、確定枠 ★4: 20% / ★3: 80%）、10連10人目の★3以上確定、天井（★4なし49回後の50回目で★4確定・以後カウント0リセット）、単発のチケット優先消費。
  - 日付区切り（端末時刻の午前4時：`Util.dayKey`）、ログインボーナス7日周期（累積型、日付巻き戻しでの再受取防止）、ミッションの手動受取と受取済みロック。
- **保存破損時の復旧とキー隔離**：
  - `localStorage` へのアクセスは `Save` モジュール内の16箇所のみに閉じ込められており、段階3時点では開発用キー（`gunya.save.v2.dev` / `.bak` / `.broken`）のみを使用して本番セーブやv1セーブを汚染していません。
  - 本体保存前の `.bak` 退避、本体JSON破損・スキーマ不正時の `.broken` 退避と `.bak` からのロールバック復旧が正しく機能しています。
- **残高・上限・台帳管理（Wallet）**：
  - コイン・チケットの増減はすべて `Economy.earn` / `Economy.spend` を通り、残高不足時の購入・特訓・スカウト拒否（マイナス残高不可）、上限クランプ（コイン9,999,999、各チケット999）、直近50件の理由付き台帳（`ledger`）保持が一貫しています。
