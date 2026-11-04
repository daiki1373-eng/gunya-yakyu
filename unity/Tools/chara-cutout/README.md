# キャラ画像の切り抜き(試合用)

chara/ の元画像から、試合で使う立ち姿と顔を作るスクリプト。

1. `cut.py` — 背景を抜く(isnet-anime.onnx を ~/models に置いて実行。30秒ごとに区切って何度か実行する)
2. `fix2.py` — 審判(3コマの下だけ使う)と幽霊(半透明なので色で抜く)を個別に作り直す
3. `export.py` — `Assets/Resources/Chara/Cut`(512x512・下中央)と `Head`(円形256)へ書き出す。顔の位置は `heads_manual.json`(中心x, 中心y, 幅 を画像比で)

キャラを追加したら heads_manual.json に1行足す。
