# InGameOptions.prefab

Canvas、ポーズメニュー、4タブのオプション、設定保存をまとめたPrefabです。

1. `InGameOptions.prefab` を使いたいSceneのHierarchy直下へドラッグします。
2. Play中に **ESC**（ゲームパッドではStart）でメニューを開きます。
3. ゲーム側のカメラ・入力・音源を以下の方法で接続します。

## ゲーム側の接続

- **カメラ**：`Assets/_Sandbox/Hirakawa_R/Scripts/OptionCameraBehavior.cs` を使用し、Targetをプレイヤーへ設定します。感度と反転はこのコピー版へ反映します。視野角・明度はMainCameraの付いた透視投影カメラにも反映します。
- **ゲーム入力**：`MiraiSozo.Options.OptionGameManager.Actions` を参照します。OptionGameManagerは必要な場合にPrefab内へ自動生成します。ポーズとキー変更はこの入力インスタンスへ反映します。
- **ポーズ担当**：ESCを処理する既存のGameManagerとは併用せず、Scene内の該当コンポーネントを無効にしてコピー版に揃えます。既存のCSを編集する必要はありません。
- **音源**：AudioSourceに `MiraiSozo.Options.OptionAudioChannel` を付け、CategoryをSE / BGM / Voiceから選びます。
- **画面揺れ**：カメラに `MiraiSozo.Options.OptionCameraShake` を付け、演出から `Play()` を呼びます。

EventSystemの追加は不要です。Sceneにあれば既存のものを使用し、なければ自動生成します。
Sceneを移動しても設定とメニューを保持し、各SceneにPrefabを置いた場合は重複するインスタンスを除去します。
テスト用HUDは既定で非表示です。

タイトル・ステージセレクトのボタンは将来の接続用で、シーン移動は行いません。
依存スクリプトは `Assets/_Sandbox/Hirakawa_R/Scripts` 内にあります。Prefabと併せて管理してください。
動作例は `Assets/_Sandbox/Hirakawa_R/Scenes/OptionTestScene.unity`、詳細は `Assets/_Sandbox/Hirakawa_R/README_Options.md` を参照してください。
