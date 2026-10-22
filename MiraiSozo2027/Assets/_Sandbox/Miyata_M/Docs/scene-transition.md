# シーン遷移 仕様(タスク 4001)

## 目的
Title / StageSelect / Game の間の遷移を、`SceneTransition.Go(SceneReference)` の1行で行えるようにする。
遷移には必ずロード画面を挟む。

## シーン
| シーン | 役割 |
|---|---|
| Title | タイトル |
| StageSelect | ステージ選択 |
| Game | ゲーム本編 |

- 遷移先は `SceneReference` で指定する。Inspector にシーンアセットをドラッグするだけで登録でき、コードの修正は不要。
- Build Settings への登録が必要。未登録の場合は Inspector に警告と「Build Settings に追加」ボタンが出る。実行時は警告を出して遷移しない。
- シーン名の文字列はコードに書かない。

## 遷移の流れ
```
Go(sceneReference)
  │  二重遷移・未指定・未登録シーンは無視(警告)
  ▼
OnBeforeTransition(scene) 通知
  ▼
ロード画面フェードイン(入力を遮断)
  ▼
LoadSceneAsync(Single) ─ バーを進捗に追従(最低 _minDisplayTime 秒)
  ▼
有効化 → 新シーンの初期化が一巡するのを1フレーム待つ
  ▼
OnAfterTransition(scene) 通知
  ▼
ロード画面フェードアウト(入力遮断を解除)
```

## 決定事項
| 項目 | 決定 |
|---|---|
| 呼び出し | `SceneTransition.Go(sceneReference)`(static、どこからでも)。ボタンは `SceneTransitionButton` に遷移先をドラッグ |
| 読み込み方式 | Single(Additive は未対応。2006 の結論待ち) |
| ロード画面 | 遷移に必ず挟む。フェードとロードUIは同じ常駐 Canvas |
| 常駐 | DontDestroyOnLoad。`Resources/SceneTransition.prefab` を起動時に自動生成(prefab が無い環境では何もしない) |
| 入力 | 遷移中は Canvas が入力を遮断。二重遷移は無視 |
| フック | `OnBeforeTransition` / `OnAfterTransition`(引数 `SceneReference`) |
| 調整値 | フェード時間・最低表示時間は Inspector(`SerializeField`) |

## 範囲外(今回やらない)
- ステージ選択 → Game への「選んだステージ」の受け渡し(ステージ選択の担当が決める)
- ロード中のミニゲーム・ムービー(2006 の `ILoadingContent` 案で別途)
- Additive 方式
