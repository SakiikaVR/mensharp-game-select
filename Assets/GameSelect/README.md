# Game Select / MenSharp

大富豪（CPU・12ルール・解説音声）、おやつ工房、おみくじを実装済みです。ゲーム名フォルダーの追加・削除による管理とゲームの仕様は [PACKAGES.md](PACKAGES.md) を参照してください。

1920 × 1080のWorld Space Canvas。`GameSelectPanel.prefab`をワールドに配置して使います。
現在のシーンにも配置済みです。左右の矢印・サイドカードで選択し、PLAYまたは中央カードで開始します。
単体パネルはローカル動作です。4枚構成のGameRoom.prefabはゲーム起動・終了をルームで同期します。詳細は[ROOM.md](ROOM.md)。得点や手札などゲーム内部の同期は各ゲーム側で実装します。

## 選択操作

- カードやボタンにカーソルを合わせると、細い枠でフォーカス先を表示します。ホバーだけではゲームは切り替わりません。
- 左右のめくりボタンは、アイコンを横に循環させます。中央へ移動するアイコンは拡大し、タイトルはフェードで切り替わります。連続クリックは順番に処理します（待機は最大8回）。
- クリック／Enterでフォーカス先を決定します。左右カード・矢印でゲームを切り替えると、フォーカスは中央カードへ戻ります。
- ←／→でカード列のフォーカスを移動、↓でPLAY、↑で中央カードへ移動。
- Tab／Shift+Tabで操作対象を順送り／逆送り。非表示・無効なボタンはスキップします。
- Escでパネルのフォーカスを解除します。他のUIへフォーカスを移した場合も枠が消えます。
- キー入力はパネルにフォーカスがある間だけ処理します。VRChat自体のカーソルロックやメニュー操作は変更しません。

既存Prefabの操作設定を再適用するには **Game Select > Upgrade Focus Controls** を実行します。

## ClientSimのGame画面でクリックする

再生後、ClientSimの **Close Menu** を押し、**Tabを押しながら**カーソルを左右ボタンへ移動してクリックします。Tabを離すとClientSimの視点操作に戻ります。

パネルのレイヤーは **Interactive (8)** です。**UI (5)** に変更すると、VRChat / ClientSimのメニューを閉じた通常状態でUIポインターの対象から外れ、表示されてもクリックできなくなります。生成・カタログ更新時にもInteractiveへ設定します。

## JSONでゲームを追加

`Assets/GameSelect/Packages/` に次のような `.json` ファイルを1つ追加してください。
サブフォルダも検索します。Unityのインポート後、カタログとPrefab、開いているシーン内のパネルを自動更新します。
手動更新は **Game Select > Refresh JSON Catalog**。シーンの変更は保存してください。

```json
{
  "schemaVersion": 1,
  "id": "new-game",
  "title": "新しいゲーム",
  "category": "PARTY GAME",
  "order": 40,
  "minPlayers": 1,
  "maxPlayers": 5,
  "icon": "gamepad",
  "prefab": "",
  "startEvent": "StartGame"
}
```

- `id`: 重複しない半角小文字・数字・ハイフン。選択保持のキーです。
- `title`: 1〜24文字。長いタイトルは自動縮小します。
- `order`: 昇順。等しい場合はid順。
- `minPlayers` / `maxPlayers`: 対応人数の表示（1〜80）。参加人数の制限はゲーム側の責務です。
- `icon`: `cookie` / `cards` / `shrine` / `gamepad`。省略時はgamepad。
- `iconPath`: 任意。`Assets/MyGame/icon.png`のような、Spriteとしてインポートした画像のパス。指定時はiconより優先。
- `prefab`: `gameType` とともに空文字なら未実装。PLAY時に「準備中」と表示します。
- `startEvent`: 将来のゲームPrefabに送る公開Udonイベント名。省略時は`StartGame`。

JSONは**Editorで検証してビルド用の配列・Unity参照へ変換**します。VRChat実行中の外部JSONダウンロードや実行コードの配信機能ではありません。追加後はワールドを再ビルド・アップロードしてください。
0件なら空状態、1件なら矢印無効、最大64件。壊れたJSON・重複id・不正な参照はConsoleにエラーを出し、最後に成功した一覧を維持します。

## ゲーム本体を後で接続

1. MenSharpBehaviourを継承するゲームのスクリプトを作成し、公開メソッド`public void StartGame()`を用意。
2. コンパイル後、そのスクリプトをゲームPrefabのルートに追加。ルートのUdonBehaviourは1つにします（MenSharpが自動生成する裏側のUdonBehaviourでOK）。
3. JSONの`prefab`にそのPrefabのAssetsパスを設定。
4. 自動生成された`PackageContent`に非アクティブなゲームが配置されます。PLAYで選択ゲームだけを有効化し、開始イベントを送ります。

ゲームPrefabはパネルのローカル座標（1920×1080）で作成してください。`PackageContent`と`Pagination`はカタログ更新で再生成されるため、直接編集せず元Prefabを編集します。
ゲームを切り替えると以前のゲームを無効化します。開始・再開始時の状態リセットはゲーム側の開始イベントで行います。

## 実装・編集

- `Assets/MenSharp/GameSelectPanel.cs`: MenSharpランタイム。入力、選択、人数、開始イベント。
- `Editor/GameSelectBuilder.cs`: UI生成、JSON検証・参照解決、Prefab更新。
- `Art/`: 再利用可能な白黒スプライト。`Editor/generate_art.py`で再生成可能（Python + Pillow）。
- **Game Select > Create Panel in Scene**: パネルがないシーンに生成。
- **Game Select > Capture Panel Preview**: 16:9画像を`Logs/game-select-preview.png`に出力。
- **MenSharp > Compile All**: Udonプログラムをコンパイル。

ボタンはMenSharpのプロキシではなく、実際のUdonBehaviour.SendCustomEventへ接続しています。
Canvasは4.8×2.7m、VRCUiShapeとGraphicRaycaster付きです。サイズ変更はルートを均等に拡縮してください。


## ライセンス

自作部分はMIT、同梱NotoフォントはSIL OFL 1.1。第三者表記はTHIRD_PARTY_NOTICES.mdを参照してください。

