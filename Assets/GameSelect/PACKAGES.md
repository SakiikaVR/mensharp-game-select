# ゲームパッケージ

ゲーム名のフォルダーを `Assets/GameSelect/Packages/` に入れると自動インストールされます。フォルダーを削除するか、Assets外へ移すとアンインストールされます。Unityのコンパイル・インポート完了後、シーンを保存してください。再生中の変更は停止後に反映します。

## フォルダー構成

```text
Assets/GameSelect/Packages/
├─ CookieClicker/
│  ├─ game.json                          名前・アイコン・ルール・効果音設定
│  ├─ Runtime/
│  │  ├─ CookieClickerGame.cs             MenSharpゲーム本体
│  │  ├─ CookieClicker.Runtime.asmdef     コンパイル対象の定義
│  │  └─ Programs/CookieClickerGame.asset コンパイル済みUdonプログラム
│  ├─ Editor/ClickerPackageBuilder.cs     ゲーム画面の生成・設定検証
│  ├─ Images/cookie.png, round.png        ゲーム用画像
│  ├─ Audio/click.wav, purchase.wav       ゲーム用効果音
│  └─ README.md
├─ Daifugo/game.json, Images/, Audio/    現在は選択項目のみ
└─ Omikuji/game.json, Images/, Audio/    現在は選択項目のみ
```

移動・配布時は **.metaも含めてフォルダー全体** を扱ってください。MenSharp / VRChat SDK / 共通ゲーム選択画面はホスト側の前提です。日本語フォントは共通の `GameSelect/Fonts` を使用します。

画像・音声のJSONパスは `Images/cookie.png`、`Audio/click.wav` のように **game.jsonのあるフォルダーからの相対パス** です。フォルダー名を変えても参照できます。画像はUnityでSpriteとしてインポートします。

## 追加と削除

- インストール：完成したゲームフォルダーをPackagesへコピー。JSONを検出して一覧・ゲーム本体・イベント配線を生成します。
- アンインストール：ゲームフォルダー全体を削除、またはAssets外へ移動。生成済みのゲーム本体も除去します。
- 一時的な一覧非表示：game.jsonだけをPackages外へ移動。ソースコードはUnityのコンパイル対象に残ります。
- 復元：元のフォルダーを戻す。プログラム・画像・音声を再参照して自動登録します。

共通の選択画面はゲーム固有クラスを直接参照しません。ゲームフォルダーを削除しても、共通側に参照エラーを残さない構成です。不正JSON・重複id・不足した素材はエラーにして、最後に使えた一覧を維持します。

## クッキークリッカー

クッキーのクリックで生産、厚焼きレシピでクリック強化、オーブン・ベーカリー・工房で自動生産。購入価格は購入ごとに上昇し、所持数不足の購入ボタンは無効です。クリックと購入には効果音があります。

「ゲーム選択へ」で戻れます。同じプレイセッション中は進行を保持し、ゲームを閉じている間は生産を停止します。各プレイヤーのローカルゲームで、同期・永続保存・オフライン生産はありません。ClientSimではClose Menuの後、Tabを押しながらマウスで操作します。

## JSON・ゲーム実装

- schemaVersion：1
- id / title / category / order：一意の識別子・表示名・分類・順序
- minPlayers / maxPlayers：人数表示
- iconPath：パッケージ内のSpriteへの相対パス
- gameType / builderType：ゲーム種別と、このフォルダーのEditor生成クラス
- clicker：初期クリック報酬、価格倍率、1～4個の設備、効果音への相対パス

`builderType` はEditor側で `ValidatePackage(GameSelectBuilder.Package)` と `Create(GameSelectBuilder.Package, Transform)` を提供します。Createはルートに1個のUdonBehaviourを持つゲームオブジェクトを返し、共通側がStartGameを送ります。新ジャンルのゲームも自分のフォルダーに本体と生成処理を置けます。

生成処理を使わない既存ゲームは、`gameType` / `builderType` の代わりに `prefab` と `startEvent` を指定できます。Prefabも同じフォルダー内に置き、相対パスで参照してください。

RuntimeのasmdefがMenSharp.Runtimeを参照するため、MenSharpがゲームフォルダー内のソースを認識し、Runtime/Programsへコンパイルします。別ゲームとして複製する際はidに加え、C#クラス名・Editor生成クラス名・asmdef名も一意にしてください。

## VRChatへの反映

追加・削除はUnity編集時にPrefab／シーンへ焼き込みます。公開済みワールドへ反映するには再ビルド・アップロードが必要です。VRChat実行中にPC内のファイルや任意のC#を読み込む方式ではありません。

## 4モニター構成

GameRoom.prefabでは起動・終了をルーム共通にします。ゲーム側のsessionControllerフィールドを自動接続します。詳しくは[ROOM.md](ROOM.md)を参照してください。ゲーム内部の得点・手札は今回の同期対象外です。
