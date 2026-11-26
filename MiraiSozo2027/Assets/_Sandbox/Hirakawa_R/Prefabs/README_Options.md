# オプション用Prefab

ゲーム内オプションは、次の2つのPrefabを組み合わせて使います。

| Prefab | 内容 |
| --- | --- |
| `PF_GameManager` | `GameManager`（ポーズ・ゲーム入力）、`GameOptions`（設定の保存と反映）、`InGameOptionMenu`（メニューの制御） |
| `PF_OptionsCanvas` | ポーズメニュー・4タブのオプション・確認画面のCanvas |

## 新しいSceneへの導入

1. Sceneに `PF_GameManager` と `PF_OptionsCanvas` を配置します。
2. `GameManager` の **Menu UI** に、Scene上の `PF_OptionsCanvas` を指定します。
3. Play中に **ESC**（ゲームパッドではStart）でメニューを開きます。

見た目・演出・効果音は実行時に自動で適用されます。設定は `Assets/_Sandbox/Hirakawa_R/Resources/OptionMenuTheme.asset` で変更します。
テーマはSceneごとに、`PF_OptionsCanvas` 内の `Menu Overlay` にある `OptionMenuAnimation` の **Theme** で指定できます。未指定のSceneでは、前のSceneのテーマを引き継がず、`Resources/OptionMenuTheme` を使います。

## PF_InGameOptions について（旧構成）

`PF_InGameOptions` は旧Sandbox構成のPrefabで、`GameOptions` と `InGameOptionMenu` を単独で持っています。
**`PF_GameManager` と同じSceneに置かないでください。** `GameOptions` が重複して片方のPrefabが破棄され、ポーズメニューが動作しなくなります。
現在は `Tools > Hirakawa R > Options > Setup Scene_OptionTestScene` で旧テスト構成を再生成するときだけ使います。新しいSceneでは使用しません。

## ゲーム側の接続

- **カメラ**：プレイヤーのカメラに `CameraBehavior` を使うと、感度と反転が反映されます。視野角・明度は MainCamera の透視投影カメラに反映します。
- **ゲーム入力・ポーズ**：`GameManager.Actions` と `GameManager` が担当します（オプション専用の管理オブジェクトはありません）。
- **音源**：AudioSourceに `MiraiSozo.Options.OptionAudioChannel` を付け、CategoryをSE / BGM / Voiceから選びます。
- **画面揺れ**：カメラに `MiraiSozo.Options.OptionCameraShake` を付け、演出から `Play(strength, duration)` を呼びます。

SceneにEventSystemがあればそのまま使用し、なければ自動生成します。既存のEventSystemには、そのSceneのUIを操作できる入力モジュールを設定してください。
タイトル・ステージセレクトのボタンは将来の接続用で、シーン移動は行いません。
依存スクリプトは `Assets/_Sandbox/Hirakawa_R/Scripts` 内にあります。詳細は `Assets/_Sandbox/Hirakawa_R/README_Options.md` を参照してください。
