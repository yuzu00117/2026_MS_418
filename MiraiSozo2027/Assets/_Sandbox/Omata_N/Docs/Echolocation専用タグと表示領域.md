# 専用タグ・表示領域・衝突・状態API

対象：Unity 6000.6.3f1 / URP。実装はOmata_N配下。チームの入力・PlayerController・共有Prefabは変更しない。

## まず試す

- `Scenes/Scene_EchoDemo.unity`：既存デモを専用タグへ移行済み。既存の塗り・輪郭・全体表示を維持する。
- `Scenes/Scene_EchoRevealDemo.unity`：上記に比較用の板を追加したデモ。`RevealWholeExample`は全体表示／全体Collider、`RevealRegionExample`は部分表示／部分Collider。
- 操作はチームの入力を使用する（Eでエコー切替）。
- 対象の`EchoRevealState`のInspectorで、Play中のActive・Collision・Operational・Fraction・Revisionを確認できる。

比較用デモの板はエコーを遮断しない設定。ほかの遮蔽物による判定は有効。部分表示の板は最大辺長1mで分割済み。

## 1. 専用タグを設定する

1. Projectで `Create > Echo > Tag` を選び、`SO_EchoTag_用途.asset`としてOmata_N配下に保存する。
2. 分類したいルートへ`EchoTarget`を追加する。
3. `EchoTarget`の「専用タグ」にタグアセットを指定する。
4. Controllerのタグ表示ルールの「専用タグ」に同じアセットを指定する。

アセット参照で照合するため、表示名・アセット名の変更では対応が切れない。最も近い`EchoTarget`が所有者になる。子に別のEchoTargetがある場合、親のタグはその境界を越えない。EchoTargetを無効化した対象は専用タグで一致しなくなる。旧`EchoTag`が残っている対象は、EchoTargetの専用タグが未設定の間だけ旧EchoTagの指定を使う。

> 以下の「EchoRevealWorld」「EchoRevealState」は旧方式の互換コンポーネントで、現在は付ける必要がない。

### 既存Unityタグから一括付与

`Tools > Echo > 専用タグを一括付与`

- 検索するUnityタグと付与する専用タグを選ぶ。
- ロード済みシーン全体／選択した階層、非アクティブを含むか、既存タグを置換するかを指定する。
- 「対象をプレビュー」で対象を確認してから適用する。
- 「表示状態のWorld」も指定すると、新規の対象にEchoRevealStateを追加して紐づける。
- Omata_N配下の保存済みシーンに限定。Undo対応。シーン保存・PrefabへのApplyは自動で行わない。
- 既にEchoRevealStateがある対象のWorldやベイクデータは上書きしない。

### 既存Controller設定をまとめて移行

`Tools > Echo > 現在の専用シーンを専用タグへ移行`

現在の専用シーンのControllerが1つの場合に利用する。表示ルールごとのタグアセット、対象のEchoTag、Controller上のEchoRevealWorld、対象のEchoRevealStateを用意する。元のUnityタグは削除・変更しない。

旧実装は「表示＝所有者のUnityタグ」「発生地点遮蔽＝Renderer自身のUnityタグ」で判定していた。移行ツールは遮蔽結果を各Rendererの`EchoOccluder`へ保存し、親タグへの移行で遮蔽が変わることを防ぐ。移行後、タグ別の遮断ルールへ変更する場合は、該当RendererのEchoOccluderを無効化または削除して明示的な上書きを解除する。

旧シーンの参照を壊さないため、シリアライズ済みの`EchoTagColorRule`と`_tagColorRules`の名称は残している。専用タグ未指定の行だけ、移行用に従来のUnityタグ照合を使用する。専用タグ指定済みの行はUnityタグへフォールバックしない。

## 2. 元マテリアルの表示範囲

Controllerに`EchoRevealWorld`を追加し、対象の`EchoRevealState.World`へ割り当てる。Worldは明示配置するコンポーネントで、常駐オブジェクトを自動生成しない。

ルールの「元のマテリアルを表示」「通常時は非表示にする」を有効にし、表示範囲を選ぶ。

| 設定 | 動作 |
|---|---|
| WholeObject | 一部への接触で所有Renderer全体を表示。ベイク不要 |
| ContactRegion | 接触した小領域だけ表示。領域ごとに接触後の残存秒数を管理 |
| 接触後の表示残存 | 最後に接触した時刻から残る秒数。0なら最新の接触結果が有効な間だけ |
| 元マテリアルの壁越し表示 | カメラの壁越し表示を指定。接触・衝突・IsActiveには影響しない |
| 不透明度 | 合成の強さ。0の場合は表示領域・衝突・IsActiveも無効 |

塗りと全体輪郭は従来の経路で併用できる。部分表示に切り替えても輪郭まで部分輪郭に変わるわけではない。

### 部分表示のベイク

1. 静的なMeshRendererを対象にする。
2. EchoRevealStateの「分割の最大辺長（m）」を設定する。
3. 「所有Rendererを分割ベイク」を押す。
4. 生成した参照とシーンを保存する。

出力先は`Settings/EchoSurfaces/`。Meshの変更・スケールの不一致はInspectorとビルド時にも検査する。各三角形を最大辺長以下に分割し、元の位置・法線・接線・UV0・ライトマップUV・頂点色・マテリアルスロットを補間して保持する。面積比はベイク時のワールド面積から計算する。

**部分表示と部分Colliderの境界は分割三角形単位**。ピクセル単位の切断ではない。三角形内に接触があると、その三角形全体を表示・衝突対象にする。細かくすると境界の誤差が減り、Renderer・Collider・GPU判定の負荷が増える。

現在は1対象につき1024分割まで。対象を小分けにするか最大辺長を大きくして調整する。接触は各三角形を16×16のタイルに投影して判定するため、極小の接触は拾えない場合がある。WholeObjectの判定解像度にはルールの「接触判定の解像度」を使う。

## 3. Colliderの連動

対象へ`EchoRevealCollider`を追加する。制御対象の既存Colliderを`Original Colliders`へ明示指定する。

| Mode | 動作 |
|---|---|
| Unchanged | 既存Colliderを変更しない |
| WholeObject | 全体表示の間だけ指定Colliderを有効化する。WholeObject表示と組み合わせる |
| RevealedRegion | 指定Colliderを一時的に停止し、表示中の分割MeshColliderだけ有効化する。ContactRegion表示と組み合わせる |

- Colliderは最初のenabled状態を記録し、終了・無効化で復元する。
- 最初から無効な指定Colliderを有効化しない。指定Colliderがすべて無効の場合は部分Colliderも生成領域に対して有効化しない。
- Original Collidersが空なら、部分領域に新しく衝突だけを追加できる。
- `Patch Material`は生成する部分ColliderのPhysicsMaterial。
- 無関係なColliderやプレイヤーのColliderは指定しない。

### 侵入中の消失を保留する

`Has Penetration Guard`と`Occupant Layers`で任意に使用する。指定レイヤーのRigidbody付きColliderまたはCharacterControllerが、消える予定の領域に重なっている場合、**描画と衝突の両方**を保持する。残存タイマーを延長するのではなく、侵入が解消するまで消失を保留する。

既定のOccupant LayersはNothing。必要なプレイヤー等のレイヤーを指定する。非凸MeshColliderやCharacterControllerなど厳密な組合せ判定の対象外では、重なりの広い判定範囲を使って保守的に保持する。バッファ満杯時も保持する。単に近くにいるだけ／接触面から離れて立っているだけの場合を、常に保持すると保証する機能ではない。

GPU結果のタイムアウト、機能無効化、シーン終了時は保留を解除する。

## 4. 他スクリプトから状態を読む

```csharp
[SerializeField] private EchoRevealState _revealState;

private void OnEnable()
{
    _revealState.Changed += HandleRevealChanged;
    HandleRevealChanged(_revealState.Snapshot);
}

private void OnDisable() => _revealState.Changed -= HandleRevealChanged;

private void HandleRevealChanged(EchoRevealSnapshot snapshot)
{
    // snapshot.IsActive を扉・ギミック等の条件として使用する。
}
```

| API | 意味 |
|---|---|
| IsActive | 確定した元マテリアル表示領域が1つ以上ある |
| HasActiveCollision | この機能が管理する衝突領域が1つ以上有効 |
| IsOperational | 有効な新しいGPU結果に基づいて状態を確定できている |
| RevealedFraction | 表示中の面積割合。全体モードは0または1 |
| Revision | 確定領域または公開状態が変化したときの番号 |
| PendingPatchCount | 侵入によって消失を保留している領域数 |
| Changed | 値・領域の変更時だけ届く読み取り専用スナップショット |
| Refresh() | 対象の構造・マテリアル等を意図的に変更した後に状態を再構築する |

IsActiveは「エコーによって現れているか」。カメラ外や壁の裏だからfalseになることはない。GameObject.SetActiveを呼ぶ機能でもない。カメラに実際に写った画素の有無を返すAPIは今回の対象外。

## 5. 更新順序と失敗時の動作

1. EchoControllerが波と発生地点遮蔽を更新する。
2. EchoRevealWorldがGPUへ接触判定を送信する。カメラの描画に依存しない。
3. 非同期readbackの結果をCPUへ取り込む。1対象につき同時に1要求だけ処理する。
4. 次のFixedUpdateで領域・Collider・IsActiveを一緒に確定する。
5. URPは同じ確定領域の元マテリアルを合成する。

GPU結果による数フレーム程度の遅延がある。表示だけ先に出すことはしない。timeScale=0中は確定領域を保ち、再開後のFixedUpdateで残存時間と最新の接触を反映する。無効化等の終了処理は直ちに状態を解除する。

Worldの`Stale Timeout`（既定0.25実時間秒）より古い結果は捨てる。新しい結果が届かない間は表示・管理中の衝突・IsActiveを解除し、IsOperationalをfalseにする。GPU非対応やベイク不整合はConsoleへ理由を出す。修正後はRefresh、Configure、またはコンポーネントの再有効化で再試行できる。

WorldまたはEchoRevealStateを無効化すると、元Rendererの通常マテリアルと既存Colliderの初期状態を復元する。エラーで停止したままの有効なコンポーネントでは、管理中のColliderを停止して透明な衝突が残らないようにする。

通常更新はGPU待機を行わない。終了時だけ、実行中GPUが参照するTextureを早期解放しないため完了を待って解放する。

## 6. 対応範囲と運用

- 部分表示／部分Collider：静的な不透明・非AlphaClipのMeshRenderer。SkinnedMesh、LODGroup、Rigidbody付き対象、実行中の頂点変形・移動は非対応。移動を検出した場合は停止する。
- ベイク後にMesh・スケール・頂点属性を変えたら再ベイクする。任意のカスタムシェーダーによる頂点変形やUV3以降の特殊用途は対応対象外。
- 全体表示は既存のSkinnedMeshや元マテリアル描画経路を維持する。
- 部分描画用の内部Rendererは発生地点の遮蔽や通常のタグ収集に再登録しない。発生地点の遮蔽形状は元のRendererを使う。
- 動的に生成する遮断物はEchoOccluderを付けるか、生成後にController.RefreshTargets()を呼ぶ。毎フレームの全Renderer検索は行わない。
- 子Rendererの追加・差替え後はEchoTarget.RefreshRenderers()とEchoRevealState.Refresh()を呼ぶ。親子関係の変更だけでベイク内容は自動更新しない。
- 1つのEchoRevealStateは1つのWorldに所属する。他Controllerの元マテリアル表示に確定領域を流用しない。
- 元マテリアル描画経路の上限は同時5000マテリアルスロット。分割領域もこの枠を使用する。
- 分割データはEditorで作成する。ベイクのUndoはコンポーネント参照を戻す。生成したアセットは自動削除しない。

設計書のクラス分離案から、既存シリアライズ型の名称は互換性のため維持した。GPU接触判定はURPのRenderGraphから分離し、RuntimeのCommandBufferとResources内シェーダーで実行する。描画がないフレームでも状態更新を続けられる。

## 7. 関連ファイル

- Runtime/Targets：専用分類、Renderer所有、移行互換のルール解決。
- Runtime/Reveal：分割データ、GPU接触判定、確定状態、World。
- Runtime/Physics：Collider制御と侵入保留。
- Editor：一括付与、既存シーン移行、分割ベイク。
- URP/EchoObjectEffects.cs：確定済みRenderer領域を元マテリアルで合成。
- Resources/EchoRevealContact.shader：既存EchoVolume.hlslと同じ波・遮蔽条件で接触を判定。
- Tests：専用タグ、Undo、分割精度、実GPUによる表示・衝突・状態同期、終了とポーズの検証。

API仕様確認：[Unity AsyncGPUReadback](https://docs.unity3d.com/cn/6000.0/ScriptReference/Rendering.AsyncGPUReadback.html)、[Physics.ComputePenetration](https://docs.unity3d.com/jp/current/ScriptReference/Physics.ComputePenetration.html)。

## 8. 実装時の検証

2026-10-22、Unity 6000.6.3f1の独立した検証用コピーで実施。

- EditMode：37件成功（一括付与のUndo、タグ参照、分割の最大辺長・面積・ベイク整合性を含む）。
- PlayMode：42件成功（実GPU画像、部分ColliderへのRaycast、全体表示、発生地点遮蔽、カメラ外、残存時間、ポーズ、侵入保留、異常停止、両デモの読み込みを含む）。
- 個人フォルダーに関わるmeta GUIDの重複なし。変更範囲はOmata_N配下のみ。
- 結果XML：Verification~/reveal-release-edit.xml、Verification~/reveal-release-play.xml（検証用ファイルはGit対象外）。
- Playerの実行ファイルビルドと別GPU／別プラットフォームの動作確認は未実施。


### 接触部分表示が表示されないとき（2026-10-22追記）

Inspectorの表示範囲は「全体表示（WholeObject）」「接触部分表示（ContactRegion）」です。「接触部分表示」は分割データのベイクが必要です。再生を停止し、対象の EchoRevealState で「表示状態の管理元」を設定して「所有Rendererを分割ベイク」を押し、シーンを保存してください。未ベイクのRenderer名はInspectorの警告と実行時エラーに表示されます。メッシュやスケールを変更した場合は再ベイクしてください。

ベイク対象に無効なRendererが含まれる場合、実行時の有効なRenderer数との不一致だけで停止する問題を修正しました。実際に表示する各Rendererに対応する分割データがあるかを検証します。余分なベイクデータは表示対象に加えません。

専用タグ、表示管理元、分割データ、衝突設定のInspector項目と表示範囲・当たり判定モードを日本語化しました。保存済みのフィールド名・enum値は変更していません。今回の確認はPlayModeの EchoRevealTests 10件（全件成功）。無効なRendererを含むベイク、未ベイク時の診断、複数行の接触判定テクスチャにおける領域対応を含みます。結果: `Verification~/reveal-contact-fix.xml`。


### 新しいシーンへの設定順序

`Tools > エコーロケーション > 00 新しいシーンの設定ガイド` を開き、上から順に進めます。

1. シーンをOmata_N配下へ保存します。
2. 「描画準備ツール」で使用するURP Rendererを指定して描画Featureを設定します。同じRendererで設定済みなら省略できます。共有Rendererは変更対象外です。カメラとURP AssetがそのRendererを使う設定になっていることを確認してください。
3. ガイドでカメラと発信位置を指定し、ControllerとWorldを作成します。既存Controllerがある場合はそれを指定して更新します。
4. 専用タグを作成または選択し、表示ルールを追加します。Inspectorで色・輪郭・元マテリアル表示を設定します。
5. 「対象へのタグ付与ツール」を開き、Unityタグで検索してプレビュー後に適用します。ガイドの専用タグとWorldが引き継がれます。初期状態では現在のシーンだけを検索します。
6. 接触部分表示を使う場合は「部分表示と衝突の準備」を開き、対象のInspectorでベイクします。衝突を制御する場合はEchoRevealColliderを追加し、表示範囲に合うモードと元のColliderを設定します。
7. シーンを保存し、チーム入力でエコーを発信して確認します。入力の作成・変更はこのツールの対象外です。

個別メニューの01〜04は、設定済みシーンの該当部分だけを変更するときにも使用できます。「既存シーンの移行」は旧Unityタグルールの変換専用で、新規セットアップには不要です。専用タグの作成と描画設定は対象アセットを保存し、シーンの変更はガイド末尾の保存ボタンまたはUnityの通常保存で確定します。

