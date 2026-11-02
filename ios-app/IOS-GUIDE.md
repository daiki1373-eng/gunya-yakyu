# iPhone版のビルドとリリース

このプロジェクトは「ぐにゃ野球」のiPhone向けリリース候補です。署名済みビルド、実機テスト、TestFlight配信、App Store審査は別の工程です。ソースの準備だけで公開済みにはなりません。

## Macでビルドする

Node.js 22以上、CocoaPods、Xcode 26以上を用意し、Xcodeで自分のApple Developerアカウントへサインインします。

```bash
git clone --branch main https://github.com/daiki1373-eng/gunya-yakyu.git
cd gunya-yakyu/ios-app
bash scripts/mac-release.sh --simulator
```

すでに取得している場合は、作業中の変更を保存してから `git pull` で更新してください。リリース用ZIPを使う場合は、展開したフォルダ内の `ios-app` で同じスクリプトを実行できます。

署名付きアーカイブは、Apple DeveloperのTeam IDを指定して作成します。Team IDはXcodeのSigning & Capabilities、またはApple DeveloperのMembershipで確認できます。パスワードや秘密鍵をリポジトリへ追加する必要はありません。

```bash
APPLE_TEAM_ID=自分のTeamID bash scripts/mac-release.sh
```

アーカイブが開いたら、Xcode Organizerの **Distribute App → App Store Connect → Upload** へ進みます。署名の許可が必要な場合は、Xcodeの案内に従って本人が設定してください。

## 実機で確認する

`npm run ios:open` でXcodeを開き、iPhoneを選んで実行します。

- タイトルから選手作成、試合開始、試合終了まで進む。
- 球種・コース、打法、送球先、走者の進塁・帰塁・停止を操作する。
- フライ捕球後の帰塁、タッチアップ、フォースアウト、ダブルプレーを確認する。
- フォーム編集、種族、必殺技を確認する。
- アプリを閉じて戻り、一時停止から再開できることを確認する。
- 再起動後のセーブ、機内モード、端末の発熱、電池消費、フレームレートを確認する。

Windowsでのブラウザーテストは、この実機確認の代わりにはなりません。

## TestFlightと審査

1. App Store ConnectでバンドルID `jp.daiki1373.gunyayakyu` のアプリを選択、または作成する。
2. XcodeからアップロードしたビルドをTestFlightで配信し、実機の不具合を修正する。
3. `store-metadata.json` の説明文を確認し、サポートURLとプライバシーポリシーURLが公開されていることを確認する。
4. 実機またはiOSシミュレーターのスクリーンショットを撮る。ブラウザーで作った確認画像を、実機撮影と記載しない。
5. 年齢制限とApp Privacyの質問票を、実際のビルドに合わせて回答する。
6. ビルドを選び、App Reviewへ提出する。承認後の公開はApp Store Connectで確認する。

価格、配信国、販売者情報はアカウント所有者が決めてください。現行ソースに広告や実課金はありません。

## 開発と同期

```bash
npm ci
npm test
npm run build
npm run ios:sync
```

`src/game.html` がゲーム本体、`src/renderer3d.js` が3D描画、`src/live-play.js` が守備・走塁、`src/live-controls.js` が操作とアニメーションです。`npm run build` で、Three.jsとネイティブ連携を含むオフラインHTMLを `www/index.html` と `../landscape/claude-v.html` に同じ内容で出力します。生成済みHTMLを直接編集しても、次のビルドで上書きされます。

`npm run ios:sync` でネイティブ側のWebアセットとプラグインを同期します。GitHub ActionsにもWebテストと署名なしiOSシミュレータービルドの設定があります。認証情報を使う配信ジョブは作成していません。

保存キー `koushien_psx97_v1` と従来のセーブ内容を引き継ぎ、iPhoneではPreferencesにもバックアップします。異なる端末間のクラウド同期は行いません。

3Dの描画にはオリジナルの手続き型モデルを使っています。フォトリアルな市販ゲームの人物・球場アセットは含みません。

## 公式資料

- [App StoreのSDK要件](https://developer.apple.com/news/?id=ueeok6yw)
- [Capacitor 8への更新](https://capacitorjs.com/docs/updating/8-0)
- [App Privacy](https://developer.apple.com/app-store/app-privacy-details/)
- [スクリーンショット仕様](https://developer.apple.com/help/app-store-connect/reference/app-information/screenshot-specifications)
