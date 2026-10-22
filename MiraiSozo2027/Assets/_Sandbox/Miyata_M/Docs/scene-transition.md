# シーン遷移 仕様(タスク 4001)

## 目的
Title / StageSelect / Game の間の遷移を、`SceneTransition.Go(SceneReference)` の1行で行えるようにする。
遷移には必ずロード画面を挟む。ロード画面は遷移の外側(2006 の方式C)にあり、`SceneTransition` は UI を持たない。

## シーン
| シーン | 役割 |
|---|---|
| Title | タイトル |
| StageSelect | ステージ選択 |
| Game | ゲーム本編 |

- 遷移先は `SceneReference` で指定する。Inspector にシーンアセットをドラッグするだけで登録でき、コードの修正は不要。
- Build Settings への登録が必要。未登録の場合は Inspector に警告と「Build Settings に追加」ボタンが出る。実行時は警告を出して遷移しない。
- シーン名の文字列はコードに書かない。

## 構成
```
SceneTransitionButton / SceneTransitionTrigger ─▶ SceneTransition.Go(scene)      4001: 検証・二重遷移防止・前後通知
                                │  ISceneLoader(窓口)
                                ▼
                         SceneLoader(方式C)               2006: フェード・ロード画面・読み込み
```
- `SceneTransition` は `ISceneLoader` だけに依存し、具体的なロード画面を知らない。
- ロード画面側(`SceneLoader`)が起動時に `SceneTransition.RegisterLoader` で自分を登録する。
- 2006 の方式が変わっても、`ISceneLoader` を実装し直すだけで済む。

## 遷移の流れ
```
Go(sceneReference)
  │  ローダー未登録・二重遷移・未指定・未登録シーンは無視(警告)
  ▼
OnBeforeTransition(scene) 通知
  ▼
入力デバイスを無効化
  ▼
ISceneLoader.Load(path, onLoaded)
  ├ ロード画面フェードイン(入力を遮断)
  ├ LoadSceneAsync(Single) ─ バーを進捗に追従(最低 _minDisplayTime 秒)
  ├ 有効化 → 新シーンの初期化が一巡するのを1フレーム待つ
  ├ onLoaded → OnAfterTransition(scene) 通知
  └ ロード画面フェードアウト(入力遮断を解除)
```

## 使い方

### 前提
- ロード画面(`Resources/SceneLoader.prefab`、2006 の方式C)があること。起動時に自動生成され、`SceneTransition` に自分を登録する。
- 遷移先のシーンが Build Settings に登録されていること。

### 1. ボタンで遷移する
1. ボタンに `SceneTransitionButton` を付ける。
2. Inspector の `Target` に、遷移先のシーンアセットをドラッグする。
3. 「Build Settings に登録されていません」と出たら、「Build Settings に追加」を押す。

### 2. クリア判定などのイベントで遷移する
1. 空のオブジェクトに `SceneTransitionTrigger` を付け、`Target` に遷移先をドラッグする。
2. クリア判定などのイベント(UnityEvent)の `+` を押し、このオブジェクトの `SceneTransitionTrigger.Go` を選ぶ。

### 3. コードから遷移する
```csharp
using SceneFlow;
using UnityEngine;

public class StageClearJudge : MonoBehaviour
{
    [SerializeField] private SceneReference _nextScene; // Inspector でシーンをドラッグ

    // 条件を満たした瞬間に1回だけ呼ぶ(毎フレーム呼ばない)
    public void OnStageCleared()
    {
        SceneTransition.Go(_nextScene);
    }
}
```
- 遷移先を分けたい場合(クリア時はリザルト、失敗時はタイトルなど)は、`SceneReference` を複数持たせる。

### 4. 遷移の前後で処理する
```csharp
private void OnEnable()
{
    SceneTransition.OnBeforeTransition += HandleBefore; // セーブ、音の停止など
    SceneTransition.OnAfterTransition += HandleAfter;   // 新シーンの初期化通知
}

private void OnDisable()
{
    SceneTransition.OnBeforeTransition -= HandleBefore;
    SceneTransition.OnAfterTransition -= HandleAfter;
}
```
- 購読したら、`OnDisable` で必ず解除する(解除し忘れるとリークする)。
- 遷移中かどうかは `SceneTransition.IsTransitioning` で確認できる。

### 5. 動作を確かめる
1. `Tools/SceneTransition/Setup` でテスト用シーンを生成する(`Scenes/SceneTransition/`)。
2. `Scene_Title` を開いて Play する。
3. ボタンで `Scene_StageSelect` → `Scene_Game` へ進む。
4. `Scene_Game` では「クリアまで N 秒」が減り、0 になると自動でタイトルへ戻る(`CountdownEvent` → `SceneTransitionTrigger.Go` の接続例)。
5. 自動確認はバッチで `-executeMethod SceneFlow.EditorTools.SceneTransitionVerify.Run`(`-quit` は付けない)。終了コード 0 なら OK。

### 遷移中の入力停止
- 既定で、遷移の開始からロード画面が消えるまで、キーボード・マウス・パッドなどの Input System のデバイスを無効化する。
- 戻すタイミング: 遷移の完了、ローダーの破棄、アプリ(Play)の終了のいずれでも必ず戻す。
- 止めたくない場合は `SceneTransition.BlockInputDuringTransition = false;`(遷移の前に設定する)。
- 仕組みは `TransitionInputBlocker`(Input System のデバイスを無効化・復帰)。Input System を使わない入力(旧 Input Manager)は止まらない。
- 遷移中に接続されたデバイスは止めない。

### 注意
- 遷移中の再呼び出し、遷移先の未指定、Build Settings 未登録のときは、警告を出して何もしない。
- ロード画面が無い環境では「ISceneLoader が登録されていません」と警告が出て、遷移しない。
- シーンを増やすときは、コードの修正は不要。ボタンやトリガーにシーンをドラッグするだけ。

## 決定事項
| 項目 | 決定 |
|---|---|
| 呼び出し | `SceneTransition.Go(sceneReference)`(static、どこからでも)。ボタンは `SceneTransitionButton` に遷移先をドラッグ |
| ボタン以外 | クリア判定などのイベントからは `SceneTransitionTrigger` の `Go()` を UnityEvent に接続、またはコードで `SceneTransition.Go` を呼ぶ |
| 読み込み方式 | Single(Additive は未対応。2006 の結論待ち) |
| ロード画面 | 遷移に必ず挟む。2006 の方式C(`SceneLoader`)を `ISceneLoader` 経由で使う |
| 常駐 | `SceneTransition` は static(常駐物なし)。ロード画面の常駐は `SceneLoader.prefab`(2006 側)が担当 |
| 入力 | 遷移中は、UI はロード画面の Canvas が遮断し、キーボード・マウス・パッドはデバイス単位で無効化する(完了時に必ず戻す)。二重遷移は無視 |
| フック | `OnBeforeTransition` / `OnAfterTransition`(引数 `SceneReference`) |
| 調整値 | フェード時間・最低表示時間は `SceneLoader` の Inspector |

## ファイル構成(`Scripts/SceneTransition/`)
| 場所 | ファイル | 役割 |
|---|---|---|
| 本体 | `SceneTransition` | 遷移の窓口(static)。検証・二重遷移防止・前後通知・入力停止 |
| 本体 | `TransitionInputBlocker` | 遷移中の入力デバイスの無効化と復帰 |
| 本体 | `ISceneLoader` | ロード画面側が満たす窓口 |
| 本体 | `SceneReference` | シーンのドラッグ指定用の参照型 |
| 本体 | `SceneTransitionButton` / `SceneTransitionTrigger` | ボタン / UnityEvent から遷移する部品 |
| `Editor/` | `SceneReferenceDrawer` | Inspector 表示(Build Settings 未登録の警告と追加ボタン) |
| `Demo/` | `CountdownEvent` | 動作確認用。N秒後にUnityEventを発火する(本番では不要) |
| `Demo/Editor/` | `SceneTransitionSetup` / `SceneTransitionVerify` / `DemoScenes` | テスト用シーンの生成と自己診断(本番では不要) |

本番へ移すときは、`Demo/` 以外を移し、`Demo/` は削除する。

## 範囲外(今回やらない)
- ステージ選択 → Game への「選んだステージ」の受け渡し(ステージ選択の担当が決める)
- ロード中のミニゲーム・ムービー(2006 の `ILoadingContent` 案で別途)
- Additive 方式
