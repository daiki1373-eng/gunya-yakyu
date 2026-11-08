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

## 表情(どのキャラでも同じ仕組み)

`Scripts/Batting/RigSkinFace.cs`。表情7種(普段/集中/驚き/喜び/目が回る/がっかり/怒り)を、元絵1枚から作る。

- 顔の部分だけ細かい網目(3px)を重ね、目・口・眉のまわりを伸び縮みさせる
  - 集中: 目を細めて眉を下げる + こめかみに汗
  - 驚き: 目を見開き口を縦に大きく開け、頭がびよんと伸びる + 驚き線・びっくりマーク
  - 喜び: 目を細めて(笑い目)口を広げ、頭が弾む + 頬の赤み・キラキラ
  - 目が回る: 目を細め頭がぐらぐら + 頭の上を星が回る
  - がっかり: 目が下がり口がすぼみ、うなだれる + 顔の上半分が青ざめる・おでこに縦線・涙
  - 怒り: 眉をぐっと下げ口を開け、ぶるぶる震える + 顔が赤くなる・怒りマーク
  - 普段: ときどきまばたき
  - もともと大笑いの絵(口が大きく開いている)は、喜びのときに大きく弾んでのけぞる
- 目・口・眉の位置は pose.py が顔の68点から自動で出す(`eyeL/eyeR/mouth/browL/browR`)。
  人の顔でない絵で自動推定できないときは rig_manual.txt に `id eyeL x y` などを書く(片目だけでも可)。
  顔が分からないキャラは記号と頭の伸び縮みだけになる。ずれる絵は `id flag noface` で目口の変形を止められる
- 表情が大きく変わった瞬間は、顔のアップを画面の横に差し込む(`Scripts/Batting/FaceCutIn.cs`。打者は右・投手は左、表情の色の背景+金枠、リプレイ中とメニュー中は出さない)
- 試合での使い分け: 見逃し三振・凡打=打者がっかり / 被本塁打=投手が崩れてからがっかり / 四球・エラー=投手が怒る / 死球=打者が怒る
- 確認: 再生中に `FaceShot.Run(new[]{"monk","dog"})` → `Recordings/faces/<id>.png` に5表情が並ぶ

## Aポーズの立ち絵で動かす(2026-11-07〜、こちらが優先)

決めポーズの元絵を曲げる代わりに、動かす用に描いた正面のAポーズの立ち絵を体にする。
腕・脚が胴から離れて描かれているので、胴・左右の腕・左右の脚を別々の部品にして関節で回せる(伸びない・裂けない)。
バットとグローブもキャラごとの別画像。素材の依頼書はリポジトリ直下の `BRIEF_muse_rig_images.md`。

```
cd unity
# 1) 素材(chara/rig-src/<id>/<id>_apose.png・_bat.png・_bat2.png・_glove.png、_v2 などは新しい番号を優先)を試合用に整える
python Tools/chara-rig/prep_body.py ../chara/rig-src Assets/Resources/Chara      # → Body/ Bat/ Glove/
# 2) 関節(Aポーズ用。全員に flag apose が付く)
python Tools/chara-rig/pose.py Assets/Resources/Chara/Body Tools/chara-rig/pose_body_raw.json
python Tools/chara-rig/build.py Tools/chara-rig/pose_body_raw.json Tools/chara-rig/rig_body_manual.txt Assets/Resources/Chara/rig_body.txt --apose
# 3) 部品の塗り分け(胴/左腕/右腕/左脚/右脚。骨の線から、絵の不透明な所だけを伝って広げる)
python Tools/chara-rig/labels.py Assets/Resources/Chara/Body Assets/Resources/Chara/rig_body.txt Assets/Resources/Chara/BodyLabel
# 確認: 関節の重ね絵
python Tools/chara-rig/preview.py Assets/Resources/Chara/Body Assets/Resources/Chara/rig_body.txt body_check
```

- 試合では Body があるキャラは自動でこちらを使う(`RigSkin.UseBodyArt=false` で全員を元絵の体に戻せる)。Body がないキャラは従来どおり Cut。
- 腕・脚の向きは、ラグドールの腕・脚の向きそのまま(Aポーズの開きは打ち消す)。立っているときは腕を下ろし、構えればバットを握る形になる。
- 部品の境目は `Resources/Shaders/GunyaRigParts.shader` が画素単位で切る(BodyLabel を読む)。
- バット: 打者はラグドールのバットの見た目を `Bat/<id>.png` にする(握る側が左。長さの比は絵のまま、標準=横512px)。`Bat/<id>_2.png` があれば打席ごとにどちらか。
- グローブ: 投手と守備の選手は `Glove/<id>.png` を後ろの腕の手に付ける。
- 2人並んだ絵(双子)は prep_body.py の HALF_LEFT で左の1人だけを使う。
- 関節がずれるキャラは `rig_body_manual.txt` に正しい座標を書いて build し直す。使わないキャラは `id flag skip`。
- `Dev/PuppetShot.Run(ids)` で守備・走者の人形を一覧で確認できる(Recordings/puppets/)。
