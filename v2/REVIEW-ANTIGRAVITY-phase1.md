# 段階1シェル レビュー（Antigravity、2026-11-02）

対象：phase1-shell.html / SPEC-SUMMARY.md

## P0（致命的：段階2でクラッシュ・進行不能）
1. **能力補正ON時、CPU側データが空で MatchEngine.HOOKS がクラッシュ**
   - `League.buildMatchConfig` で `teams.opponent.lineup` が空配列。`batterOf('opponent')` が null を返し、`HOOKS.sweetSpot` 等の `batter.mult.contact` / `fielder.mult.defense` 参照で TypeError。
   - 対策：段階2でシード（球団番号×シーズン）からCPU野手9人＋先発投手の能力と補正倍率 mult を生成して lineup に入れる。
2. **第10節終了後にシーズン終了画面へ進む導線がない**
   - 試合終了時は常に result 画面へ行き、「次の試合へ」が常時表示されるため10節目を無限再戦する。season 画面へはデバッグ操作からしか開けない。
   - 対策：リーグ最終節の result から season（順位報酬受取）へ分岐させる。

## P1（重要）
3. **デモ広告の契機**：仕様サマリは「設定の変更だけ」とするが、ショップに5秒のリワード広告が有効実装されている（スカウト画面にも無効ボタン）。仕様の正と要照合。
   - ※統合担当メモ：現行ゲームには本人の指示でリワード広告が実在する。仕様との食い違いとして要判断（→ 04_pending-decisions）。
4. **DOM操作の分散**：Debug／Actions／GameHud が Router/Shell の外で直接DOMを触っている（仕様は「DOMに触れるのはRouter/Shellだけ」）。
5. **MatchEngine接続点が2本を超えている**：メタ層が mount/pause/resume/abort/isRunning を直接呼ぶ。制御APIを許容するか通知バスへ統一するか境界定義の明確化が必要。
6. **非リーグ戦の MatchConfig 定義不足**：mode≠league は一律 innings:3 / teams:null。10球チャレンジや練習の終了条件・既定パラメータの置き場が未定で、エンジン内にモード分岐が漏れる恐れ。
7. **3回制の終了ルールとアウト通知の欠落**：3回表終了時の後攻リード終了、サヨナラ、3回裏終了（引き分け含む）が未実装。アウト専用通知がなくHUDのアウト表示が次打者まで遅れる。
8. **match:end リスナーの実行順序依存とクリーンアップ**：Economy→League→App の順に同期実行され Save.commit が複数回。段階3の Growth 追加時に競合しやすい。エンジン側 rAF ループやイベントリスナの unmount 規約が必要。

## P2（軽微）
9. 保存データに導出可能なリーグ成績 {w,l,d} が含まれている（仕様は「保存は事実だけ、順位表は毎回計算」）。
10. match:pitch で球速表示 #g-kmh が更新されない（球数のみ更新）。
11. 一時停止→「試合をやめる」確認をキャンセルすると即再開される（一時停止シートへ戻すべき）。
