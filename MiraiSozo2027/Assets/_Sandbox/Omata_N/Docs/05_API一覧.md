# エコーロケーション API 一覧

ほかのスクリプトから使える変数・関数・イベント・列挙型の一覧です。名前空間は `Echo.Echolocation` です。使い方の例は [04_スクリプトからの使い方.md](04_スクリプトからの使い方.md) を参照してください。

表の「読／書」は、読み取りだけ（読）か、書き換えもできる（読書）かを表します。

## EchoController（プレイヤー側）

### 状態

| 名前 | 型 | 読／書 | 内容 |
|---|---|---|---|
| `IsEchoInUse` | `bool` | 読 | エコーを使用中か。ON 中（断続方式の波と波の間も含む）と、OFF 後に波が消えきるまで `true`。単発方式では波の帯が最大半径を抜けきるまで |
| `IsScanRequested` | `bool` | 読 | ON／OFF の操作状態。OFF にした瞬間に `false`（消去中の波は含まない） |
| `CurrentState` | `EchoState` | 読 | 非表示・展開中・表示中・消去中 |
| `CurrentOuterRadius` | `float` | 読 | 今の外側の半径（m） |
| `CurrentEraseRadius` | `float` | 読 | 消去中の内側の半径（m） |
| `FollowWaveRadius` | `float` | 読 | ライト方式の拡張波の先端の半径（m）。出ていなければ負 |
| `Frame` | `EchoFrameData` | 読 | このフレームの最新の波 |
| `Frames` | `IReadOnlyList<EchoFrameData>` | 読 | 生きている全部の波。断続方式・単発方式は古い順。ライト方式は最大 1 件 |
| `RenderItems` | `IReadOnlyList<EchoRenderItem>` | 読 | このフレームの描画候補。変更しないこと |
| `RegisteredCount` | `int` | 読 | 登録されている Renderer の数 |

### ライト方式の持続可能な時間

| 名前 | 型 | 読／書 | 内容 |
|---|---|---|---|
| `LightSustainRemainingSeconds` | `float` | 読 | 残りの持続可能な時間（秒）。時間制限なしの設定では 0 |
| `LightSustainRatio` | `float` | 読 | 残り時間の割合（0〜1）。時間制限なしの設定では 1 |
| `RecoverLightSustain(float seconds)` | メソッド | — | 指定した秒数だけ回復する。持続可能な時間を超えない |
| `RefillLightSustain()` | メソッド | — | 満タンに戻す |

### 単発方式のストック

| 名前 | 型 | 読／書 | 内容 |
|---|---|---|---|
| `OnceStock` | `int` | 読 | 今打てる回数 |
| `OnceRechargeProgress` | `float` | 読 | 次のストックが回復するまでの進み具合（0〜1）。満タンなら 1 |

### 操作

| 名前 | 内容 |
|---|---|
| `SetScanEnabled(bool isEnabled)` | ON／OFF を要求する。次の `LateUpdate` で反映。同じ要求は進行をリセットしない |
| `ToggleScan()` | ON／OFF を反転する。単発方式では波を 1 つ打つ |
| `RefreshTargets()` | ロード済みシーンの描画対象を集め直す。全検索なので毎フレーム呼ばない |
| `ApplySettings(EchoSettings value)` | 設定をコピーして差し替える。反映は次の `LateUpdate` |
| `RegisterGeometryAdapter(IEchoGeometryAdapter adapter)` | 標準以外の Renderer から形状を取り出す処理を追加する |
| `GetPreview()` | 設定上の最大の範囲を `EchoFrameData` で返す（Scene ビュー用） |
| `static GetForCamera(Camera camera)` | そのカメラを描画カメラにしている Controller を返す。複数あれば `null` |

### 参照と設定

| 名前 | 型 | 読／書 | 内容 |
|---|---|---|---|
| `Settings` | `EchoSettings` | 読 | スキャンの設定。中身は書き換えられる（[EchoSettings](#echosettings)） |
| `RangeDisplay` | `EchoRangeDisplaySettings` | 読 | 範囲表示の設定。中身は書き換えられる |
| `TargetCamera` | `Camera` | 読書 | 描画カメラ |
| `OriginTransform` | `Transform` | 読書 | スキャン中心 |
| `DirectionTransform` | `Transform` | 読書 | 正面の基準 |
| `ExcludedRoot` | `Transform` | 読書 | 除外するルート |
| `ShadowMaps` | `EchoShadowMaps` | 読 | 発生位置からの遮蔽データ（描画側が使う） |
| `IsGizmoVisible` | `bool` | 読 | Scene ビューにギズモを表示するか |

### イベント

| 名前 | 型 | 内容 |
|---|---|---|
| `UsageChanged` | `Action<bool>` | `IsEchoInUse` が変わったとき。新しい値を渡す |
| `StateChanged` | `Action<EchoState, EchoState>` | `CurrentState` が変わったとき。旧状態・新状態の順に渡す |
| `OnceStockChanged` | `Action<int>` | ストック数が変わったとき。新しい値を渡す |

Inspector の「状態変更時の通知」（UnityEvent）でも、`StateChanged` と同じタイミングで呼べます。

## EchoTarget（物体側）

### 表示

| 名前 | 型 | 読／書 | 内容 |
|---|---|---|---|
| `IsRevealed` | `bool` | 読 | ①②③のどれかで表示中か（残存表示中も含む） |
| `IsInContact` | `bool` | 読 | 波が今届いているか（残存は含まない。発生位置から遮断された部分は含まない） |
| `LastContactTime` | `float` | 読 | 最後に波が届いた時刻（Controller の「時間の基準」）。未接触は負の無限大 |

この 3 つは、初めて読んだとき（または `RevealChanged` を購読したとき）から判定を始めます。結果は 1〜3 フレーム遅れます。

### 実体化

| 名前 | 型 | 読／書 | 内容 |
|---|---|---|---|
| `State` | `EchoMaterializationState` | 読 | 透明・照射中・実体化。通常の描画を隠さない専用タグでは常に実体化 |
| `IsMaterialized` | `bool` | 読 | 実体化しているか |
| `IrradiationSeconds` | `float` | 読 | 実体化に向けて蓄積した照射時間（秒） |
| `MaterializationProgress` | `float` | 読 | 実体化までの進み具合（0〜1）。実体化中は 1 |
| `Materialize()` | メソッド | — | 照射を待たずに実体化させる。隠さない専用タグでは何もしない |
| `ResetToTransparent()` | メソッド | — | 蓄積を捨てて透明に戻す。隠さない専用タグでは何もしない |

### その他

| 名前 | 型 | 読／書 | 内容 |
|---|---|---|---|
| `Definition` | `EchoTagDefinition` | 読書 | 専用タグ。旧 `EchoTag` が残っているシーンでは、未設定の間だけその指定を使う |
| `OwnDefinition` | `EchoTagDefinition` | 読 | このコンポーネント自身に保存された専用タグ（移行ツール用） |
| `LegacyTag` | `EchoTag` | 読 | 旧 `EchoTag`。移行後は `null` |
| `RefreshRenderers()` | メソッド | — | 子 Renderer や LOD を変えた後に、登録を更新する |
| `ForgetLegacyTag()` | メソッド | — | 旧 `EchoTag` を追加・削除した後に、参照を取り直す |
| `Owns(Renderer renderer)` | メソッド | — | その Renderer がこの `EchoTarget` の持ち物か |

### イベント

| 名前 | 型 | 内容 |
|---|---|---|
| `RevealChanged` | `Action<EchoTarget, bool>` | `IsRevealed` が変わったとき。対象と新しい値を渡す |
| `StateChanged` | `Action<EchoTarget, EchoMaterializationState>` | 実体化の状態が変わったとき。対象と新しい状態を渡す |
| `static Changed` | `Action` | いずれかの `EchoTarget` の登録・構成が変わったとき（Controller が使う） |

## EchoTagDefinition（専用タグアセット）

| 名前 | 型 | 読／書 | 内容 |
|---|---|---|---|
| `DisplayName` | `string` | 読 | 表示名。空ならアセット名 |
| `Description` | `string` | 読 | 説明 |
| `IsBlocking` | `bool` | 読書 | エコーロケーションを遮断するか |
| `Settings` | `EchoTagColorRule` | 読 | 表示・実体化の設定。中身は書き換えられる |

### EchoTagColorRule（`Settings` の中身）

旧データとの互換のため多くのメンバーがありますが、新しく使うのは次のものです。

| 名前 | 型 | 内容 |
|---|---|---|
| `IsEnabled` | `bool` | この設定を使うか |
| `IsNormalRenderingHidden` | `bool` | 通常の描画を隠すか |
| `Materialization` | `EchoMaterializationSettings` | 実体化の条件 |
| `Irradiated` | `EchoDisplaySettings` | 照射中の表示①②③ |
| `Fill` / `Outline` / `Original` | `EchoEffectSettings` | 実体化しているときの表示①②③の共通項目 |
| `Color` | `Color` | 実体化しているときの①の強調色 |
| `OutlineColor` / `OutlineWidth` | `Color` / `float` | 実体化しているときの②の色と幅 |
| `OriginalOpacity` | `float` | 実体化しているときの③の不透明度 |
| `ContactResolution` | `int` | 全体接触判定の解像度 |
| `SurfaceMemoryResolution` | `int` | 接触部分の残存精度（読み取りのみ） |
| `HasAnyEffect` | `bool` | 何か描く設定があるか（読み取りのみ） |
| `CopyMaterializedDisplayToIrradiated()` | メソッド | 実体化しているときの表示①②③を、照射中の表示へコピーする |
| `CopyMaterializedFillToIrradiated()` / `CopyMaterializedOutlineToIrradiated()` / `CopyMaterializedOriginalToIrradiated()` | メソッド | ①／②／③だけをコピーする |

### EchoMaterializationSettings（実体化の条件）

| 名前 | 型 | 内容 |
|---|---|---|
| `RequiredSeconds` | `float` | 実体化までの照射時間（秒） |
| `InterruptionMode` | `EchoIrradiationInterruptionMode` | 照射が途切れたときの扱い |
| `DecaySpeed` | `float` | 「徐々に減らす」の減る速さ（秒/秒） |
| `PersistenceMode` | `EchoMaterializedPersistenceMode` | 実体化した後の扱い |
| `RevertSeconds` | `float` | 透明に戻るまでの時間（秒） |
| `IsColliderDisabledWhileTransparent` | `bool` | 透明な間は当たり判定を無効にするか |

### EchoDisplaySettings（照射中の表示）

| 名前 | 型 | 内容 |
|---|---|---|
| `Fill` / `Outline` / `Original` | `EchoEffectSettings` | ①②③の共通項目 |
| `Color` | `Color` | ①の強調色 |
| `OutlineColor` / `OutlineWidth` | `Color` / `float` | ②の色と幅（ピクセル） |
| `OriginalOpacity` | `float` | ③の不透明度 |

### EchoEffectSettings（表示①②③の共通項目）

| 名前 | 型 | 内容 |
|---|---|---|
| `IsEnabled` | `bool` | 表示する |
| `IsThroughWalls` | `bool` | 壁越しに表示する |
| `DisplayScope` | `EchoOriginalRevealMode` | 表示範囲（全体表示／接触部分表示） |
| `HasPersistence` | `bool` | 接触終了後も表示を残す |
| `PersistenceSeconds` | `float` | 残存時間（秒） |
| `IsSeamlessTransition` | `bool` | 実体化へシームレスに移行する（照射中の表示だけで使う） |

## EchoSettings

`EchoController.Settings` の中身です。

| 名前 | 型 | 内容 |
|---|---|---|
| `Radius` | `float` | 最大半径（m） |
| `AngleDegrees` | `float` | 全開き角（度） |
| `Duration` | `float` | 伝播時間（秒） |
| `ScanMode` | `EchoScanMode` | スキャン方式 |
| `FollowWaveInterval` | `float` | ライト方式の拡張波を繰り返す間隔（秒） |
| `LightSustain` | `EchoLightSustainSettings` | ライト方式の持続可能な時間 |
| `PulseInterval` | `float` | 断続方式の再展開間隔（秒） |
| `PulseHoldDuration` | `float` | 断続方式の展開後の残存時間（秒） |
| `OnceStockCapacity` | `int` | 単発方式の連続で打てる回数 |
| `OnceRechargeSeconds` | `float` | 単発方式のストック 1 回復までの時間（秒） |
| `OffMode` | `EchoOffMode` | 停止時の消し方 |
| `TimeMode` | `EchoTimeMode` | 時間の基準 |
| `IsEnabledOnStart` | `bool` | 開始時に有効 |
| `TargetLayers` | `LayerMask` | 対象レイヤー |
| `IsAutoDiscoveryEnabled` | `bool` | 対象を自動収集 |
| `Shadow` | `EchoShadowSettings` | エコー地点からの遮蔽（`IsEnabled`・`Resolution`・`Bias`） |
| `Copy()` | メソッド | 中身まで丸ごとコピーした設定を返す |

`OcclusionMode`・`TagColorRules` は旧データとの互換用で、Inspector には出ません。表示の設定は専用タグアセットで行います。

### EchoLightSustainSettings（ライト方式の持続可能な時間）

| 名前 | 型 | 内容 |
|---|---|---|
| `HasSustainLimit` | `bool` | 持続可能な時間を設定するか |
| `SustainSeconds` | `float` | 持続可能な時間（秒） |
| `IsLimited` | `bool` | 時間制限があるか（読み取りのみ） |
| `DepletionMode` | `EchoLightDepletionMode` | 使い切ったときの扱い |
| `RecoveryMode` | `EchoLightRecoveryMode` | 回復のしかた |
| `RecoverySpeed` | `float` | 回復する速さ（秒/秒） |
| `IsRadiusShrinking` / `MinimumRadius` | `bool` / `float` | 最大半径を狭めるか／0 になった時の最小の距離（m） |
| `IsAngleShrinking` / `MinimumAngleDegrees` | `bool` / `float` | 全開き角を狭めるか／0 になった時の最小の角度（度） |

### EchoRangeDisplaySettings（範囲表示）

`EchoController.RangeDisplay` の中身です。

| 名前 | 型 | 内容 |
|---|---|---|
| `IsEnabled` | `bool` | スキャン範囲を表示 |
| `IsSurfaceVisible` | `bool` | 床・壁に範囲を表示 |
| `IsBoundaryVisible` | `bool` | 空間の境界線を表示 |
| `FillColor` / `WaveColor` / `EraseColor` / `BoundaryColor` | `Color` | 範囲内・拡張波・消去波・境界線の色 |
| `WaveWidth` | `float` | 波の帯幅（m） |

## EchoFrameData（1 つの波の情報）

`EchoController.Frame` / `Frames` で受け取る、読み取り専用の値です。

| 名前 | 型 | 内容 |
|---|---|---|
| `Origin` | `Vector3` | 波の中心 |
| `Forward` | `Vector3` | 正面の向き |
| `Angle` | `float` | 全開き角（度）。ライト方式で狭めているときは狭めた値 |
| `OuterRadius` | `float` | 外側の半径（m） |
| `EraseRadius` | `float` | 内側の半径（m）。消去中や単発方式の帯の後ろ側 |
| `State` | `EchoState` | この波の状態 |
| `PulseId` | `long` | 波の番号（断続方式・単発方式） |
| `IsBand` | `bool` | 単発方式の帯状の波か |

## そのほかのコンポーネント・関数

| 名前 | 内容 |
|---|---|
| `EchoStockDisplay.Controller` | ストックを表示する Controller（読書）。未設定ならシーン内から探す |
| `EchoFrameRateLimiter.IsLimitEnabled` | FPS 上限を有効にするか（読書） |
| `EchoFrameRateLimiter.MaxFps` | 上限 FPS（読書） |
| `EchoTargetRegistry.GetOriginalMaterials(Renderer renderer)` | 通常の描画を隠している間も、元のマテリアルの配列を返す |

## 列挙型

| 列挙型 | 値 | 意味 |
|---|---|---|
| `EchoScanMode` | `Light` / `Intermittent` / `Single` | ライト方式／断続方式／単発方式 |
| `EchoState` | `Hidden` / `Revealing` / `Active` / `Hiding` | 非表示／展開中／表示中／消去中 |
| `EchoOffMode` | `Immediate` / `Wave` | 即時／手前から順番 |
| `EchoTimeMode` | `Scaled` / `Unscaled` | ゲーム時間／実時間 |
| `EchoLightDepletionMode` | `TurnOff` / `KeepMinimum` | 自動でOFFになる／最小の範囲で照らし続ける |
| `EchoLightRecoveryMode` | `WhileOff` / `RefillOnOff` / `None` | OFFの間に回復する／OFFにすると満タンに戻る／回復しない |
| `EchoMaterializationState` | `Transparent` / `Irradiated` / `Materialized` | 透明／照射中／実体化 |
| `EchoIrradiationInterruptionMode` | `Reset` / `Keep` / `Decay` | 0に戻す／保持する／徐々に減らす |
| `EchoMaterializedPersistenceMode` | `Permanent` / `RevertAfterIdle` | ずっと実体化／一定時間で透明に戻る |
| `EchoOriginalRevealMode` | `WholeObject` / `ContactRegion` | 全体表示／接触部分表示 |
| `EchoOcclusionMode` | `ThroughWalls` / `VisibleOnly` | 壁越しに表示する／壁で隠す（旧データ用） |
| `EchoEffectOcclusionMode` | `Inherit` / `ThroughWalls` / `VisibleOnly` | 共通設定に従う／壁越しに表示する／壁で隠す（旧データ用） |
