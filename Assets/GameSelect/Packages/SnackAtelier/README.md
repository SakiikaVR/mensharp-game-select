# おやつ工房 / Snack Atelier

このフォルダー全体がゲームパッケージです。game.json、Runtime、Editor、Images、Audio、.metaファイルをまとめて配布・移動してください。

ゲームルールはgame.json、動作はRuntime/CookieClickerGame.cs、画面生成はEditor/ClickerPackageBuilder.csで編集できます。画像・音声のパスはこのフォルダーからの相対パスです。

画像はプロジェクトで作成した図形素材、効果音はこのパッケージ向けに合成した音です。既存ゲームの配布素材は使用していません。フォントはホストのGameSelect/Fontsを使用します。

全体仕様：../../PACKAGES.md

独自の名称・案内文・設備名を使用するおやつ生産ゲームです。他社ゲームの公式版・移植版ではありません。内部のCookieClickerGameクラス名は既存アセット参照を保つため残しています。
