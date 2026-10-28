# GPU部分表示と輪郭グリッチ

対象: Assets/_Sandbox/Omata_N のみ。既存の Scene_EchoDemo とユーザーの削除・タグ変更は保持。

## 新しいDemoScene

Scenes/Scene_EchoGlitchDemo.unity を開いて再生。共有のGraphics/Quality設定アセットは編集せず、デモ専用Pipelineを再生中だけ使用し終了時に復元する。

- 左: 専用タグのグリッチ「常時」。通常表示中の物体の輪郭が常時乱れる。
- 中央: 全体表示＋「表示開始時のみ」。エコーが当たって全体が出現したとき、Duration秒だけ乱れる。再出現で再発火。
- 右: 接触部分表示＋「表示されている間」。現在エコーが届いた表面だけを表示し、その領域の内側境界にグリッチを描く。通常の輪郭線はOFF。
- Demo Controls の自動切替、またはGame ViewのStart/Stopで検証。管理元の「タグと表示設定」で専用タグごとに「輪郭のグリッチを表示」を有効化し、色・強度・幅・横ずれ・帯の高さ・速度・継続時間を日本語の項目で調整。

## 導入

EchoControllerと対象のEchoTagに同じ専用タグを設定。元マテリアル表示をON、通常時非表示をON、表示範囲をContactRegionまたはWholeObjectにする。対象へのEchoGlitch追加は不要。管理元の同じ専用タグのルールにグリッチ設定を持つ。子RendererはEchoTargetの所有範囲に従う。対応するURP RendererにEchoUrpRendererFeatureが必要。

ContactRegionはライトの照射領域のように現在到達している表面をGPUでマスクする。メッシュ分割・ベイク・部分Colliderは不要。移動やスケール変更にも追従する。通常Colliderのenabledは一切変更しない。物理衝突が不要な物体は通常のUnity設定でColliderを外す。

WholeObjectの残存時間は従来どおり。ContactRegionの元表示には残存時間を適用しない。波の残存時間はController側で設定する。グリッチのOnRevealは対象単位の表示開始を指し、部分表示の各ピクセルの到達時刻を保存するものではない。部分境界にはWhileRevealedを推奨。

表示部分の境界は画面のシルエットとエコー到達マスクから計算する。輪郭線そのものは描画しない。通常アウトラインは独立設定。現方式のマスクはジオメトリ形状に基づくため、特殊な頂点変形シェーダーや透明・AlphaClipの切り抜き輪郭には別途対応が必要。

## 旧方式からの移行

EchoRevealState / EchoRevealWorld / EchoRevealCollider は既存シーンのMissing Scriptを防ぐ互換用。実行時処理はなく、旧状態APIは非稼働(false/0)を返す。CPUの可視割合・物理確定状態・Changedイベントに依存するゲームロジックがある場合、その用途は別途設計する。既存シーンのコンポーネントは手動で外せる。新規タグ付与ツールはこれらを追加しない。

EchoContactSamplerと専用読み戻しシェーダーを廃止。関連するInitialize/Sample/Submit、マジックナンバー、areSourcesAvailableは処理ごとなくなった。旧Bakerは過去データの検証・再生成用APIとして保持し、依頼の数値をconst化。新方式から呼ばず、ベイクInspectorとビルド時の必須チェックは廃止。2500の実行時重複箇所はなくなったためBaker内の定数のみ。EchoObjectEffectsの_EchoOcclusionはPropertyToIDへ統一。

## 確認事項への回答

- ベイク結果のコミット: 新方式はSO_EchoSurface.assetを参照しないため不要。作業開始時に当該assetとmetaはすでにユーザー側で削除済みで、復元していない。旧シーンの保存内容は変更していないため過去のシリアライズ参照が残る場合があるが、新方式では読まない。旧方式を別ブランチで使う場合は再生成手順だけでなく生成元Meshと設定の管理が必要。
- メモリ: 同時対象数の要件はリポジトリから確定できないため、数を仮定しない。旧方式の1024² RFloat縮小列は約5.33MiB/対象だった。新方式にはその常駐列がない。対象履歴は1×1 RGBAFloatが2枚で生データ32B（実際の割当単位・管理領域は別）。RenderGraphの一時マスク、深度、元マテリアル再描画、WholeObjectの接触縮小は残るので、対象数・画面解像度・重なりが増えるほど描画負荷は増える。実機の上限台数・対象数はProfilerで別途測定が必要。
- Dispose待機: 旧WaitForCompletionは終了時に限らずルール変更・材質変更・検証更新でも発生し得た。Sampler自体を廃止したため、その同期待機はなくなった。新方式はGPU内でマスクと履歴を合成する。

## 検証

新しいDemoSceneをUnity Editorで生成し、PlayMode画像テストで部分表示、表示終了、再出現時グリッチ、部分境界、移動対象、Collider不変を確認する。実行結果はVerification~/GlitchPlayMode.xml、ログと画像も同フォルダに保存する。旧物理連動テストは廃止仕様ではなく新しい描画・Collider不変仕様のテストへ置き換えた。

### 実行結果（2026-10-28 / Unity 6000.6.3f1）

- DemoScene生成・C#コンパイル成功。描画テスト中のシェーダーエラーなし。
- 新Demo / 新仕様PlayMode: 5/5成功（GlitchPlayMode.xml）。
- 既存描画・遮蔽・ライフサイクルPlayMode: 29/29成功（GlitchRegression.xml）。
- タグ付与・旧ベイクAPI EditMode: 3/3成功（GlitchEditMode.xml）。
- 合計37件成功。保存したglitch-contact-boundary.pngを目視し、部分表示の境界に限定した描画も確認。
- 旧Bakerの長いBakeメソッドの再構成は、新方式が利用しないため対象外とした。

## 専用タグ設定への移行（2026-10-28）

グリッチはEchoControllerの「タグと表示設定」に統合。同じ専用タグの対象に同じ設定を適用する。別タグには別の有効状態・色・強度・発生タイミングを保存できる。設定コピーでもタグ間で変更が混ざらない。

グリッチの全項目名・選択肢・説明、デモ操作欄、管理元の動作状態表示は日本語。対象に旧EchoGlitchが残っていても描画設定には使わない。旧値は復元用に保持し、Inspectorには移行先を案内する。Scene_EchoGlitchDemoは追加コンポーネントを外してタグ設定に移行。Scene_EchoDemoにあった旧設定も対応タグへ転記し、その他の編集内容を保持した。
