# MenSharp Game Select

VRChat向けの16:9ゲーム選択パネルと、MenSharp製クッキークリッカー。ゲーム名フォルダーの追加・削除でゲームを管理できます。

![Game selector](docs/game-select.png)

## v0.2.0：4モニター・共有起動

GameRoom.prefabに4枚のモニターを配置しました。どのモニターからゲームを開いても全モニターで起動し、戻る操作も共通です。ゲームIDをManual同期し、途中参加時は受信状態から復元します。大富豪本体や得点・手札の同期は今回含みません。詳細は[ROOM.md](Assets/GameSelect/ROOM.md)。

## 機能

- 左右に循環するカルーセル、ホバーフォーカス、PLAY。
- クッキークリッカー：クリック生産、クリック強化、自動生産設備、効果音、選択画面へ戻る。
- ゲームフォルダーにJSON・本体ソース・生成処理・画像・音声を同梱。
- フォルダーを削除すると一覧と生成ゲームを除去。復元時は再登録。
- 大富豪・おみくじは選択項目のみで、ゲーム本体は未実装。

## 導入

確認環境：Unity 2022.3.22f1 / VRChat SDK Worlds 3.10.5 / MenSharp 0.1.1。

1. VRChat Creator CompanionでWorldsプロジェクトを作成します。
2. [MenSharp](https://github.com/ProjectTesca/MenSharp)を導入します。VRChat SDK・MenSharp本体はこの配布物に含みません。
3. [Releases](https://github.com/SakiikaVR/mensharp-game-select/releases)から `.unitypackage` をインポートします。ソースから使う場合はこのリポジトリのAssetsをプロジェクトのAssetsにコピーします。
4. コンパイル完了後、`MenSharp > Compile All` を実行します。
5. 4枚構成は `Assets/GameSelect/GameRoom.prefab` を配置します。既存の単体パネルを拡張する場合は `Game Select > Create Four Monitor Room`。1枚だけならGameSelectPanel.prefabも使えます。
6. `Game Select > Refresh JSON Catalog` を実行し、シーンを保存します。

ClientSimではメニューの **Close Menu** を押し、**Tabを押しながらクリック** します。パネルはInteractiveレイヤー (8) を使用します。UIレイヤー (5) にすると、VRChatメニューを閉じた際にクリック対象から外れます。

## ゲームの追加・削除

```text
Assets/GameSelect/Packages/CookieClicker/
├─ game.json
├─ Runtime/CookieClickerGame.cs
├─ Runtime/CookieClicker.Runtime.asmdef
├─ Runtime/Programs/
├─ Editor/ClickerPackageBuilder.cs
├─ Images/
└─ Audio/
```

`.meta`を含むフォルダー全体を `Assets/GameSelect/Packages/` に入れると追加、削除またはAssets外へ移すと解除されます。詳細は [パッケージ仕様](Assets/GameSelect/PACKAGES.md)。既存の`.json`登録と`.gamepackage`も読めますが、ゲーム全体の配布にはフォルダー形式を使います。

変更はUnity編集時に反映します。公開済みVRChatワールドへの反映には再ビルド・アップロードが必要です。4枚構成ではゲームの起動・終了を同期します。得点・手札などゲーム内部状態のネットワーク同期や永続保存は未実装です。クリッカーの進行は同じセッション中のみ保持します。

## ライセンス

- 自作コード・図形画像・合成効果音：**MIT**。 [LICENSE](LICENSE)
- MenSharp：**MIT**, Copyright (c) 2026 Tesca。 [全文](ThirdPartyLicenses/MenSharp-MIT.txt)
- 同梱Noto Sans JP Bold：**SIL OFL 1.1**。フォントにはMITを適用しません。 [全文](ThirdPartyLicenses/NotoSansJP-OFL.txt)

出典・配布対象は [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) を参照してください。VRChat SDK、MenSharpコンパイラ、Windowsフォント、個人のUnity設定やログは同梱していません。

## 検証

ClientSimで左右の実クリック、クッキーの実クリック、購入・所持数不足・価格上昇・自動生産・再開時の進行保持を確認。ゲームフォルダーをAssets外へ移して本体アセンブリがない状態での登録解除と、復元後の再登録も確認しています。VRChat実クライアントでの公開ワールド検証は別途必要です。

