# 元絵をそのまま体にする仕組み(chara-rig)

試合の打者・投手は、キャラの元絵(切り抜き `Assets/Resources/Chara/Cut/<id>.png`)をそのまま体として使い、
ラグドール(当たり判定と物理)の動きに合わせて元絵を変形させて動かす。

- 実行時: `Scripts/Batting/RigSkin.cs`(変形)、`Scripts/Batting/CharaRig.cs`(関節データの読み込み)
- 関節データ: `Assets/Resources/Chara/rig.txt`(このフォルダのツールで自動生成)
- リグがないキャラ・画像がないキャラは従来のぐにゃ体で表示される(`RagdollPlayer.UseRigSkin=false` で全員従来体)

## 動かし方の要点

1. 元絵の関節(首・肩・肘・手首・腰・膝・足首・頭)を、骨格推定(DWPose)で自動で求める
2. 元絵を 8px 角の網目にして、各点を近い骨に重み付けする
3. ラグドールの各パーツの「最初の姿勢からの回転」を、元絵の同じ関節まわりに当てる
   → 立ち姿は元絵そのまま。振る・投げる・転ぶ・歩く・登場ポーズはラグドールに追従
4. 違う骨にまたがる網目が大きく伸びたら描かない(腕を振り上げたときのゴム状の伸び防止)
5. 元絵に描かれたバットは、握っている手の前腕(打者の振る腕はラグドールのバット)に付いて動く。
   バットが描かれていないキャラは、ラグドールのバットを元絵の手の位置に持たせる

## キャラを増やしたとき

```
# 準備(初回のみ): モデルを ~/models に置く
curl -L -o ~/models/dw-ll_ucoco_384.onnx https://huggingface.co/yzd-v/DWPose/resolve/main/dw-ll_ucoco_384.onnx
# 依存: python3 + numpy + opencv-python + onnxruntime + pillow

cd unity
python Tools/chara-rig/pose.py Assets/Resources/Chara/Cut Tools/chara-rig/pose_raw.json      # 自動推定
python Tools/chara-rig/build.py Tools/chara-rig/pose_raw.json Tools/chara-rig/rig_manual.txt Assets/Resources/Chara/rig.txt
python Tools/chara-rig/preview.py Assets/Resources/Chara/Cut Assets/Resources/Chara/rig.txt rig_check   # 確認用の一覧画像
```

- `rig_check0.jpg` などを見て、骨の線が体からずれていたら `rig_manual.txt` に正しい座標を書いて build し直す
- 人の形でない絵(ロボット・2人組など)で自動推定が失敗したキャラは build が名前を出すので、`id flag replace` を付けて全部手で書く
- バットなどの持ち物は `batGrip`(握る手)/`batTip`(先端)/`batTail`(反対の端)を書くと、手に付いて一緒に動く
- 上半身だけの絵などで大きさが合わないときは `id scale 0.7` のように補正
- 振る腕(打者)・投げる腕(投手)は自動で決める(バットを握る腕、なければ手が高い腕)。逆なら `id flag frontL` / `frontR`
