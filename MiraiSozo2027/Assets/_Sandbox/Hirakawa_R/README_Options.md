# ゲーム内オプション

現在の構成は、`PF_GameManager` の `GameManager`・`GameOptions`・`InGameOptionMenu` と、Sceneに配置した `PF_OptionsCanvas` を使用します。配置例は `Assets/_Project/Scenes/TestScene.unity` です。
ESC（ゲームパッドでは Start）でメニューを開きます。

## 配置済みCanvasとの接続

1. Sceneに `PF_GameManager` と `PF_OptionsCanvas` を配置します。
2. `GameManager` の **Menu UI** に、Scene上の `PF_OptionsCanvas` を指定します。`InGameOptionMenu` の **Canvas** に直接指定する方法も使えます。
3. Playすると既存Canvasにイベントを接続します。Canvasや画面全体を実行時に生成する処理はありません。

Canvas未指定時は、GameManagerのMenu UI、自分の子Canvas、Scene内の候補1つの順に取得します。非アクティブのCanvasも対象です。候補がなければポーズ開始を見送り、Playごとに一度だけConsoleへ案内を出します。候補が複数の場合は警告を出すので、Inspectorで使用するCanvasを指定してください。取得したCanvasに必須のUIオブジェクトやコンポーネントが欠けている場合は、対象の名前と型をログに出し、ポーズ開始を見送ります。
起動時とシーン読み込み時にCanvasとテスト用HUDの参照を取得します。ポーズ入力では、接続済みの参照を使ってメニューを開閉します。キー設定行や試聴音、必要なEventSystemは実行時に用意します。

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

操作タブはスクロール不要の4列一覧です。各枠に操作名と現在のキーを表示し、選択すると割り当てを変更できます。現在は20件と4つの予備枠を表示します。`InputSystem_Actions.inputactions` の `Player` マップにキーボードまたはマウスボタンの割り当てを追加すると、予備枠が順に埋まります。24件を超える場合は行が増え、一覧に収まるよう枠の高さを調整します。配置と余白はPrefabの `Binding Content` にある `Grid Layout Group` で調整できます。

## 他のゲームシーンへ配置する

入力とポーズは `Assets/_Sandbox/Aoki_K/Scripts/GameManager.cs` に統合しています。`GameManager.Actions` をゲーム操作・キー変更の共通入力として使用し、オプション側で別のManagerを生成しません。
ポーズ中は Player アクションマップと時間進行を止め、終了時に元の時間倍率・入力の有効状態・カーソル状態を復元します。

カメラ感度・反転は、統合後の `CameraBehavior` を使用するカメラに反映します。動作確認用のコピー版 `OptionCameraBehavior` とテスト用プレイヤー `OptionTestPlayer` も、`GameManager.Actions` とGameManagerのポーズ状態を使います。旧Sandbox用の `OptionGameManager` はどのスクリプトからも参照されていません（使用しているSceneがないことを確認後、削除できます）。現在の接続確認には上記のTestSceneを使用してください。

共通Prefabではテスト用HUDは非表示です。EventSystemがあるSceneでは既存のものを使います。配置先のEventSystemには、そのSceneでUI操作できる入力モジュールを設定してください。有効なEventSystemが見つからず、オプション用Canvasの接続が成功した場合にEventSystemを自動生成します。移動先でオプション用Canvasを使えない場合は、自動生成したEventSystemを無効にします。
Prefab・Scene・Materialは命名規則に合わせて改名し、GUIDを保持して参照を引き継いでいます。

- **カメラ**：`CameraBehavior` を使用するカメラへ感度と反転を反映します。初回の感度・反転はそのInspector値を引き継ぎます。視野角と明度の対象は MainCamera または CameraBehavior の付いた透視投影カメラです。対象は起動時とScene読み込み時に取得します。実行中にカメラを追加・切り替えた場合は `GameOptions.Instance.RefreshSceneTargets()` を呼んでください。明度はURPのグローバルVolumeで露出だけを上書きします。
- **音源**：各 `AudioSource` に `OptionAudioChannel` を付け、Category を SE / BGM / Voice に設定します。元の音量にカテゴリー音量を掛け、マスター音量は `AudioListener.volume` で全体に掛けます。実行中に音源自体の音量を変える場合は `BaseVolume` を使ってください。オプション生成時にイベントで接続し、破棄時またはChannel無効化時に元音量へ戻します。
- **画面揺れ**：カメラに `OptionCameraShake` を付け、演出側から `Play(strength, duration)` を呼びます。このコンポーネントの揺れに軽減率を適用します。別方式の揺れ演出を追加する場合も `GameOptions.Instance.Values.shakeReduction` を参照してください。
- **フォント**：Windowsではインストール済みの日本語フォントを使います。日本語フォントのない配布先ではPrefabの Font に同梱する日本語フォントを指定してください。
- **画面**：Prefab内のuGUI階層を編集できます。Canvasは1280×720基準の可変サイズです。

## 配色と開閉アニメーションの接続

アクセントは `#00BFFF`、背景は `#E0FFFF` で、パネルやボタンの不透明度は65%です。ポーズ画面は5つの項目を同じ高さ・等間隔で配置しています。画面上部の見出しと下部のESC案内を外し、未接続の項目を押したときの案内は確認ダイアログに表示します。固定UIは `PF_OptionsCanvas` と `PF_InGameOptions`、選択中のタブとキー設定一覧は `OptionMenuLayout` の配色を使います。文字は不透明で、通常の文字は薄い背景で読めるよう濃い色を使います。

ボタンのImageは白にし、ButtonのColor TintのNormal Colorに通常の半透明色を設定しています。マウスオンとキーボード選択では白・不透明度95%のHighlighted／Selected Colorへ切り替わります。ボタンの配色はButton側の各状態の色で調整してください。

ポーズ画面の5項目は同じ通常色を使い、開いた直後は「ゲームに戻る」を選択します。初期選択は `InGameOptionMenu.OpenMenu()` の `SelectControl("Resume")` で指定しています。

ポーズメニューを開くと、`Menu Overlay` の `OptionMenuAnimation` がまず枠を機械的に展開します。中央から細い光のラインが横へ伸び、一瞬止まってから上下へ開きます。閉じるときは逆に上下が閉じて一本のラインになり、中央へ縮んで消えます（設定アセットの `Close Duration`、初期値 0.26 秒）。開閉の動きは `OptionPanelExpansion` にまとめています。広がりきった後、枠の上端中央より少し外側から下端まで青い波紋を広げます。枠が広がる時間は設定アセットの `Open Duration`（初期値 0.36 秒）で調整できます。枠内の項目（ボタン・文字・装飾）は波紋が通過した範囲だけが描画され、波紋の広がりと同時に見えていきます。この表示制御には `Resources/OptionMenuReveal.shader` を使い、演出中だけ各Graphicのマテリアルを差し替えます（標準マテリアルを使う要素のみが対象です）。演出中はパネルのマウス操作を止め、キーボード・ゲームパッドの初期選択は開いた直後から有効です。波紋は枠の内側だけに描画します（`Clip To Panel` で切り替え可）。波紋の時間は設定アセットの `Wave Duration`（初期値 0.45 秒）で調整できます。演出の途中でオプション画面へ移った場合や閉じた場合は、すぐに元の表示へ戻します。追加の演出は `On Opening` と `On Closing` に登録します。

見た目は `#00BFFF` と `#E0FFFF` を基調にした柔らかいSF調で、Prefabを変更せずに実行時に適用しています（`OptionSoftStyle`）。パネルとボタン、キー設定の枠は角を丸くします。角丸は、スプライトが未設定かUnity標準のものだけに適用し、独自の画像を指定した場合はそのまま使います。ポーズ画面とオプション画面には `Soft Frame`（細い黒枠・外側の淡い光・白い縁取り・内側の細い線）を、ポーズ画面のボタンには `OptionSoftButton`（周囲の光・左端の表示灯・下線・右端の点。選択やマウスオンでなめらかに点灯）を付けます。角の半径は `OptionSoftStyle`、形と寸法は `OptionSoftFrame`・`OptionSoftButtonGraphic` の定数で調整できます。

### 見た目と演出の設定（Inspector）

色・枠・ボタン装飾・開閉アニメーション・波紋・背景の波紋・説明ウィンドウの設定は、`Resources/OptionMenuTheme.asset` にまとめています。アセットを選択してInspectorで変更してください。Play中に変更すると、その場で見た目に反映されます（角の丸みのみ次のPlayから）。
アセットは自動で読み込むため、Prefabや他のSceneでの割り当ては不要です。別の設定を使いたい場合は、`Create > MiraiSozo > Option Menu Theme` で作成し、`Menu Overlay` の `OptionMenuAnimation` の **Theme** に指定します。開閉と波紋の時間もこのアセットで設定します（以前 `OptionMenuAnimation` にあった時間の項目は使いません）。

効果音は設定アセットの「効果音」で指定します（初期設定：タブ切り替えは `Audio/カーソル移動6`、メニュー・オプション画面・確認画面の開閉と画面の切り替えは `Audio/ビープ音1`）。説明ウィンドウが現れたときは、同じ音を小さく鳴らします（`Description Sound Volume`、初期値 0.35）。再生の遅れを防ぐため、音は最初に読み込んでおき、先頭の無音を切り取ってから鳴らします。音量はオプションのSE音量に従い、メニューを閉じた後も鳴り終わるよう `InGameOptionMenu` のオブジェクトの下で鳴らします（`OptionMenuSound`）。

マウスとキー操作は自動で切り替わります（`OptionInputMode`）。マウスを動かすとキー操作の選択表示が消え、キーやゲームパッドのボタンを押すとマウスオンの表示が消えて、表示中の画面の一番上の項目から選択が始まります。キー設定の入力待ち中は切り替えません。

ゲーム終了・タイトルに戻るなどの確認画面は、メニューと同じ角丸・枠・ボタン装飾にし、メニューと同じ機械的な展開で開閉します（`OptionDialogAnimation`。波紋はなく、文字は最初から表示）。閉じる演出の間は確認画面を操作できません。

パネル内の見出しや説明の文字・飾りは、ボタンに重なってクリックを横取りしないよう、実行時にマウス判定を外します（`OptionSoftStyle.PreventClickTheft`。ボタン自身・スライダーなどの部品・スクロール範囲の判定は残す）。

ポーズ画面とオプション画面を表示している間は、背景で控えめな波紋を約3秒ごとに流します（`OptionAmbientSonar`。項目と枠の下に描画し、最初の発信は表示から3秒後）。間隔・濃さ・速さは同クラスの定数で調整できます。

ポーズ画面のボタンをキー操作で選ぶか、マウスを乗せると、右側に縦長の説明ウィンドウ（見出しはボタンの表示名、本文は `InGameOptionMenu.BindDescriptions` で設定）をメニューと同じ見た目で表示します。高さはパネルに揃え（最低400）、画面からはみ出す場合は内側へ寄せます。開閉はメニューと同じ機械的な展開（横へ伸びるライン→上下へ開く）で、背景の板は拡大ではなくサイズを変えるので角丸や枠線は潰れません。文字は開ききる直前から表示します。メニューの開閉や画面の移動の演出中は、演出が終わってから開きます。

オプションを選ぶと、メイン画面と説明ウィンドウの文字がすぐに消え、メイン画面が左下へ畳まれます。畳みきった後に説明ウィンドウがオプション画面の位置と大きさへ変形し（メイン画面と重ならないよう、変形はメイン画面が消えてから始める）（時間は通常の開く演出と同じ `Open Duration` と `Wave Duration` を使用）、オプション画面と入れ替わった後、左端中央の外側から波紋が広がって項目が表示されます。キー操作で選んだなど説明ウィンドウが出ていない場合は、普段の表示位置から現れて変形します。オプション画面から戻るときは逆に、文字を消してオプション画面を右上へ畳み、畳みきってからメイン画面を左下から広げ、上端からの波紋で項目を表示します（入れ替えの処理は `InGameOptionMenu.ShowPausePanel`）。画面を入れ替える前の演出中は、オプションを開く・戻る操作を受け付けません。タブの切り替えには演出はありません。入れ替えの処理は `InGameOptionMenu.ShowOptionsPanel` です。追加の演出は `On Options Opening` に登録できます。自動検証では `OptionMenuAnimation.CompleteAnimations` で演出を最後まで進めてから確認します。

閉じる演出を接続するときは `Has Closing Animation` を有効にし、演出の最後から `OptionMenuAnimation.CompleteClosing()` を呼びます。同じ `Menu Overlay` にAnimatorを置く場合は、閉じるクリップ末尾のAnimation Eventに `CompleteClosing` を設定できます。AnimatorのUpdate Modeは `Unscaled Time` にしてください。ポーズ中も演出を進めるための設定です。

`GameManager.ResumePause()` → `InGameOptionMenu.RequestClose()` → `On Closing` → `CompleteClosing()` → 画面を閉じる → ゲームの時間と入力を復元、の順に処理します。閉じる間はUI操作を止め、完了通知の重複を防ぎます。`Has Closing Animation` が無効なら即時に閉じます。初期状態では両イベントは空で、この設定は無効です。

FPS制御は `QualitySettings.vSyncCount = 0` と `Application.targetFrameRate` を使います。プラットフォーム側の制限がある場合は、設定値より低いFPSになることがあります。
UI操作は変更したカテゴリーだけを反映します。保存に失敗した場合は警告を出し、メニューを閉じる際のポーズ解除とキー変更後の操作復帰を実行します。
テストシーンのBuild Settingsへの追加は行っていません。ビルドする際は必要なシーンとして追加してください。

## 検証と再生成

Unityのメニュー `Tools > Hirakawa R > Options` に以下の項目があります。

- `Validate Existing Canvas (Play Mode)`：統合後のTestSceneをPlayし、メニューを閉じた状態で実行します。既存Canvasの参照・再表示・ポーズ復元・設定ボタンの重複登録を確認します。保存値は復元し、結果は `Logs/OptionValidation/ExistingCanvasChecks.txt` に出力します。
- `Setup Scene_OptionTestScene`：旧テスト構成を再生成します。Canvasは `PF_OptionsCanvas` から配置します。生成対象のPrefabとステージオブジェクトを上書きします。
- `Validate Option Assets`：Prefabの構成とSceneのMissing Scriptを検査します。

保存キー：`Hirakawa.Options.v1`、`Hirakawa.Options.Bindings.v1`。
