# AIレビュー用 規約要約

これはAIレビュー用の要約。元の規約は `docs/coding-rules.md` と `docs/CONTRIBUTING.md`。元を変更したら同じPRでこのファイルも更新する。食い違う場合は元の規約が正。
各ルール末尾の `[coding-rules 1.4]` `[CONTRIBUTING 4.1]` は元の章番号。違反を指摘するときは該当の章番号を明記する。対象は Unity / C#。

## 1. 命名規則

### 変数・型の形式 [coding-rules 1.1]
- クラス・メソッド・プロパティ・enum・定数: PascalCase (定数も `MaxAmmo`。`MAX_AMMO` は不可)
- ローカル変数・引数: camelCase
- private フィールド: `_` + camelCase (`_health`)
- インターフェース: `I` + PascalCase (`IDamageable`)

### 変数名 [coding-rules 1.2]
- bool 変数は `is` / `has` / `can` で始める (`isAlive`, `canJump`, `hasKey`)
- コレクションは複数形 (`enemies`, `spawnPoints`)
- enum は単数形 (フラグ用途のみ複数形: `DirectionFlags`)
- `e` のような意味のわからない名前は不可

### メソッド名 [coding-rules 1.3]
PascalCase で記述し、動詞から始めることを基本とする。接頭辞:
- `Get` 値の取得 / `Set` 値の設定
- `Is` 状態の判定 / `Can` 可否の判定 / `Has` 所持の判定
- `On` イベント発火・通知 / `Handle` イベントのハンドル

### アセット命名 [coding-rules 1.4]
種類を示す接頭辞を付ける。
- `PF_` Prefab (`PF_Player`)
- `M_` Material (`M_Ground`)
- `T_` Texture (`T_Ground_Albedo`)
- `AC_` Animator Controller (`AC_Player`)
- `AN_` Animation Clip (`AN_Player_Run`)
- `SO_` ScriptableObject (`SO_EnemyStatus`)
- `SE_` 効果音 / `BGM_` BGM (`SE_Jump`, `BGM_Title`)
- `Scene_` Scene (`Scene_Title`, `Scene_Stage01`)

## 2. フォルダ構成

### 個人作業フォルダ [coding-rules 2.1]
- 各メンバーは `MiraiSozo2027/Assets/_Sandbox/<名前>/` を個人作業フォルダにする
- 作り方: Unity の Project ウィンドウで `_Sandbox/_Template` を右クリック → Duplicate → 自分の名前にリネーム
- 禁止: Windows エクスプローラーでのコピー (`.meta` の GUID が重複し参照が壊れる。必ず Unity 上で Duplicate)
- 禁止: `_Template` に個人の作業物を置くこと (雛形のため。作業はリネーム後のフォルダで行う)
- 禁止: 他メンバーのフォルダ・共有アセット・シーン・Prefab の無断編集 (必要なら事前に Slack 等で宣言)

### 作業の流れ [coding-rules 2.2]
個人フォルダ(`_Sandbox/<名前>/`)で作業 → PR 作成 → レビュー・承認 → `_Project/` 配下の該当フォルダへ移動。シーンは担当ごとに分割し、同時編集を避ける。
例外: PR 本文に「共有物を `_Project/` に直接用意することが目的」と明記されている PR は、`_Project/` への新規追加を指摘しない。

### 全体構成 [coding-rules 2.3]
`MiraiSozo2027/Assets/` 直下は次の3つ。
- `_Project/` 本番用(共有): `Scripts/`(`Player/` `Enemy/` `UI/` `Systems/` `Utils/`), `Prefabs/`, `Scenes/`, `Models/`, `Textures/`, `Materials/`, `Sprites/`, `Effects/`, `Fonts/`, `Audio/`, `Animations/`, `Settings/`
- `_Sandbox/` 個人の試作・検証用: `_Template/`(雛形。`Scripts/` `Prefabs/` `Scenes/` `Models/` `Textures/` `Materials/` `Sprites/` `Audio/` `Animations/`)、`<名前>/`(`_Template` を複製して作成)
- `ThirdParty/` 外部アセット置き場
- 禁止: `Misc` フォルダの作成 (分類できないものは適切なフォルダ名をチームで相談して決める)

### スクリプトの配置 [coding-rules 2.4]
- 機能別のサブフォルダに置く (例: `Scripts/Player/`)
- 1ファイル1クラス、ファイル名＝クラス名

## 3. スクリプト設計

- Inspector への公開は `[SerializeField] private` を使い、`public` フィールドは避ける [coding-rules 3.1]
- 役割分担: ロジックは C# スクリプト、見た目・調整値は Inspector、共有データは ScriptableObject [coding-rules 3.2]
- コンポーネント単位で小さく作る [coding-rules 3.3]
- シングルトンは最小限にする [coding-rules 3.3]
- 疎結合にしたい場合は `event` / `Action` を使う [coding-rules 3.3]
- コメントは「何をしているか」ではなく「なぜそうするか」を書く [coding-rules 3.3]
- メソッドは1つの責務に絞り、名前から処理内容が分かるようにする [coding-rules 3.3]

## 4. パフォーマンス・禁止事項

- `Update` 内で `GetComponent` / `Find` / `Camera.main` を呼ばない。`Awake` / `Start` でキャッシュする [coding-rules 4.1]
- 毎フレームの `new` は避ける (事前に生成して使い回す) [coding-rules 4.2]
- 毎フレームの文字列結合は避ける [coding-rules 4.2]
- 不要な `Debug.Log` は残さない [coding-rules 4.2]
- マジックナンバーは `const` か `[SerializeField]` にする [coding-rules 4.2]

## 5. Git・Unity 特有の注意

### コーディングに関わる Git ルール [coding-rules 5]
- `.meta` ファイルは必ずコミットする
- 秘匿情報 (API キー等) はコミットしない
- `Library/`, `Temp/`, `Logs/`, `obj/` はコミットしない (`.gitignore` を確認)
- 1コミット1目的
- `main` へ直接 push せず、ブランチ → PR
- 同じシーン／Prefab を同時編集しない (担当を事前共有)

### Unity 特有の注意 [CONTRIBUTING 2]
- `.meta` はアセットと同時に必ずコミットする (欠けると参照が切れる)
- コミットしないフォルダ: `Library/`, `Temp/`, `Logs/`, `obj/`, `UserSettings/` など。`.gitignore` で除外されているか確認する
- Asset Serialization は `Edit > Project Settings > Editor` で Force Text にする
- 動画・高解像度テクスチャ・音源などの大容量素材はコミット前に相談する。Git LFS は現時点では使わない
- シーン・Prefab は同時編集しない。編集前に事前宣言(Slack 等)し、同じファイルは1人が担当する

## 6. 生成AI・素材 [coding-rules 6]

- AI 生成コードは理解・検証してから採用する (説明できないコードは入れない) [coding-rules 6.1]
- 生成結果をそのまま作品にしない。責任は制作者本人が負う [coding-rules 6.1]
- 使用素材の出典・ライセンスは `CREDITS.md` に記録する [coding-rules 6.2]
- 外注の丸投げは禁止 [coding-rules 6.2]

## 7. ブランチ戦略 [CONTRIBUTING 1]

### ブランチ一覧 [CONTRIBUTING 1.1]
- `main`: 動作確認済みの安定版 (展示・提出用)。作成元・マージ先なし
- `develop`: 開発の統合ブランチ。作成元 `main`、マージ先 `main`
- `feature/*`: 機能ごとの開発。作成元 `develop`、マージ先 `develop`
- 禁止: `main` と `develop` への直接 push (必ず PR 経由)
- `release/*` と `hotfix/*` は使わない。緊急修正も `feature/*` → `develop` → `main`

### feature ブランチ [CONTRIBUTING 1.3]
- 命名: `feature/<タスク番号>_<機能名>` (例: `feature/1001_player-movement`)
- 機能名は英小文字とハイフン
- マージ後はブランチを削除する
- 展示前・提出前に `develop` から `main` へマージし、マイルストーンタグを付ける

## 8. コミットルール [CONTRIBUTING 3]

形式は `<種別>: <要約>` [CONTRIBUTING 3.1]。種別:
- `feat` 新機能の追加
- `fix` バグ修正
- `docs` ドキュメントの変更
- `style` 動作に影響しない整形
- `refactor` 機能を変えない構造改善
- `chore` 設定・素材追加など

1コミット1目的。要約は日本語で簡潔に書く。 [CONTRIBUTING 3.1]

## 9. Pull Request ルール [CONTRIBUTING 4]

- タイトルはコミットと同じ形式 (例: `feat: プレイヤーの移動処理を追加`) [CONTRIBUTING 4.1]
- マージ先(ベース)は `develop`。誤って `main` 向けに作った `feature/*` の PR は GitHub Actions で自動的に `develop` へ付け替えられる。`develop` → `main` の PR は対象外 [CONTRIBUTING 4.1]
- 説明テンプレートの項目: 「概要」(何のための変更か)、「変更内容」、「動作確認」、「スクリーンショット(任意)」 [CONTRIBUTING 4.2]
- 動作確認のチェック項目: 「Unity で開いてエラーが出ない」「既存機能に影響がない」。チェックボックスは PR 作成後に確認できた項目をクリックしてチェックする。作成時は `[ ]` のまま (◯や x などは入力しない) [CONTRIBUTING 4.2]
- 承認数: 最低1名の承認を得てからマージする [CONTRIBUTING 4.3]
- `develop` → `main` はリーダーの承認が必要 [CONTRIBUTING 4.3]
- `docs/coding-rules.md` または `docs/CONTRIBUTING.md` を変更する PR では、同じ PR で `docs/ai-review-rules.md` (AIレビュー用の要約)も更新する。未更新だと CI が失敗する [CONTRIBUTING 4.3]

## 10. コンフリクト解消 [CONTRIBUTING 5]

- 通常ファイル(スクリプトなど): 最新の `develop` を `merge` で取り込んで解消し、通常の push を行う [CONTRIBUTING 5.1]
- シーン・Prefab: 手動マージしない。片方(`develop` 側 または 自分側)を採用し、もう片方の変更は再作業する。対応は担当者に相談する [CONTRIBUTING 5.2]
- merge 中は `--ours` が自分側、`--theirs` が `develop` 側 [CONTRIBUTING 5.2]

## 11. マイルストーンタグ [CONTRIBUTING 6]

- 提出日・審査日ごとに、`main` へマージしたタイミングでタグを付ける。SemVer (`v1.0.0` 形式)は使わない
- タグ名の例: `m1-企画確定` (企画の提出時点)、`m2-作品完成` (作品が完成した時点)、`m3-搬入` (展示会場への搬入時点)
- `develop` を `main` へマージするのは PR 経由・リーダー承認後
