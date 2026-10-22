# ゲーム内オプション

`Scenes/Scene_OptionTestScene.unity` を開いて Play すると、WASD とマウスで動かせる確認用ステージが起動します。
ESC（ゲームパッドでは Start）でメニューを開きます。

- **ゲームに戻る**：ゲームを再開します。
- **タイトルに戻る／ステージセレクトに戻る**：将来の接続用です。案内のみ表示し、どのシーンにも移動しません。
- **オプション**：下記の4タブを表示します。
- **ゲームを閉じる**：確認後に終了します。Unity Editor では Play を終了します。

## 設定

| タブ | 内容 |
| --- | --- |
| システム | FPS：30 / 60 / 120 / 144 / 無制限、明度：-2～+2、視野角：40～100度 |
| カメラ | 横・縦感度：0.02～1.00、左右・上下反転、画面揺れ軽減：0～100%、揺れの試演 |
| 音量 | マスター・SE・BGM・ボイス音量：0～100%、各カテゴリーの試聴 |
| 操作 | Input System によるキーボード・マウスボタンの変更。移動のWASD／矢印キーも個別変更可 |

各値は操作時に反映し、メニューを閉じる・タブを切り替える・アプリのフォーカスが外れるときなどに `PlayerPrefs` へ保存します。キー変更は確定時に保存します。ESCで変更をキャンセルし、10秒無入力でもキャンセルします。ESCの割り当てと新たな操作の重複は拒否します。既存の共有キー（例：調べる／エコースキャンのE）は初期配置として保持します。

エイムアシストはゲーム側に実装がないため、操作タブに未対応と表示し、スイッチを無効にしています。
「このタブを初期化」は確認後に、そのタブだけを初期化します。

## 他のゲームシーンへ配置する

`Assets/_Project/Prefabs/PF_InGameOptions.prefab` をSceneのHierarchy直下に置きます。Canvas・設定保存・メニュー処理をまとめたPrefabです。複数シーンで配置されても、実行時には1つを保持します。
`OptionGameManager` がなければ自動生成し、`OptionGameManager.Actions` をこのSandboxのゲーム操作・キー変更の共通入力として使用します。
ポーズ中は Player アクションマップと時間進行を止め、終了時に元の時間倍率・入力の有効状態・カーソル状態を復元します。

このSandboxでは、Aoki_Kの元スクリプトを変更せず、次のコピーを使用します。コピーのクラスはすべて `MiraiSozo.Options` 名前空間に分けています。

| コピー元 | オプション専用のコピー |
| --- | --- |
| Aoki_K/Scripts/CameraBehavior.cs | Scripts/OptionCameraBehavior.cs |
| Aoki_K/Scripts/GameManager.cs | Scripts/OptionGameManager.cs |
| Aoki_K/Scripts/GameSettings.cs | Scripts/OptionGameSettings.cs |

`Scene_OptionTestScene`のカメラとテストプレイヤーはコピー版を参照します。入力定義は既存の `InputSystem_Actions` を読み取り利用し、オプション専用のインスタンスを生成します。
既存の `GameManager` とは入力・ポーズ処理が独立しており、入力の共有やコンポーネントの自動無効化は行いません。既存の管理を使うSceneへの導入時の調整は、別途行う必要があります。
カメラ感度・反転は `OptionCameraBehavior` を使用するカメラに反映します。

共通Prefabではテスト用HUDは非表示です。EventSystemがあるSceneでは既存のものを使い、モジュールの追加や有効状態の変更は行いません。配置先のEventSystemには、そのSceneでUI操作できる入力モジュールを設定してください。EventSystemがないSceneではPrefab内へ自動生成します。Scene移動時はメニューを閉じ、移動先のEventSystemへ切り替えます。
Prefab・Scene・Materialは命名規則に合わせて改名し、GUIDを保持して参照を引き継いでいます。

- **カメラ**：`OptionCameraBehavior` を使用するカメラへ感度と反転を反映します。初回の感度・反転はそのInspector値を引き継ぎます。視野角と明度の対象は MainCamera または OptionCameraBehavior の付いた透視投影カメラです。対象は起動時とScene読み込み時に取得します。実行中にカメラを追加・切り替えた場合は `GameOptions.Instance.RefreshSceneTargets()` を呼んでください。明度はURPのグローバルVolumeで露出だけを上書きします。
- **音源**：各 `AudioSource` に `OptionAudioChannel` を付け、Category を SE / BGM / Voice に設定します。元の音量にカテゴリー音量を掛け、マスター音量は `AudioListener.volume` で全体に掛けます。実行中に音源自体の音量を変える場合は `BaseVolume` を使ってください。オプション生成時にイベントで接続し、破棄時またはChannel無効化時に元音量へ戻します。
- **画面揺れ**：カメラに `OptionCameraShake` を付け、演出側から `Play(strength, duration)` を呼びます。このコンポーネントの揺れに軽減率を適用します。別方式の揺れ演出を追加する場合も `GameOptions.Instance.Values.shakeReduction` を参照してください。
- **フォント**：Windowsではインストール済みの日本語フォントを使います。日本語フォントのない配布先ではPrefabの Font に同梱する日本語フォントを指定してください。
- **画面**：Prefab内のuGUI階層を編集できます。Canvasは1280×720基準の可変サイズです。

FPS制御は `QualitySettings.vSyncCount = 0` と `Application.targetFrameRate` を使います。プラットフォーム側の制限がある場合は、設定値より低いFPSになることがあります。
UI操作は変更したカテゴリーだけを反映します。保存に失敗した場合は警告を出し、メニューを閉じる際のポーズ解除とキー変更後の操作復帰を実行します。
テストシーンのBuild Settingsへの追加は行っていません。ビルドする際は必要なシーンとして追加してください。

## 検証と再生成

Unityのメニュー `Tools > Hirakawa R > Options` に以下の項目があります。

- `Setup Scene_OptionTestScene`：Prefabと確認用ステージを再生成します。生成対象のPrefabとステージオブジェクトを上書きします。
- `Validate Option Assets`：Prefabの構成とSceneのMissing Scriptを検査します。
- `Run PlayMode Validation`：実際のESC入力、タブ、設定反映、保存、キー変更・キャンセル・重複拒否、ポーズ復元を確認します。個人の保存設定はバックアップ・復元します。結果は `Logs/OptionValidation/PlayChecks.txt`、画面は同フォルダーのPNGに出力します。

保存キー：`Hirakawa.Options.v1`、`Hirakawa.Options.Bindings.v1`。
