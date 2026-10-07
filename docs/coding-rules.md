# コーディングルール

本ドキュメントでは、チーム開発におけるコーディングルールを定めます。  
コードの品質・可読性を統一し、チーム全体の開発効率を高めることを目的とします。

> **📌 対象**: Unity / C#

---

## 1. 命名規則

### 1.1 C# の基本ルール

C# 標準に従い、対象ごとに**命名の形式**を統一してください。

| 対象 | 形式 | 例 |
|---|---|---|
| クラス | PascalCase | `PlayerController`, `EnemySpawner` |
| メソッド | PascalCase | `GetHealth()`, `HandleInput()` |
| プロパティ | PascalCase | `MaxHealth`, `CurrentSpeed` |
| enum | PascalCase | `GameState`, `WeaponType` |
| 定数 | PascalCase | `MaxAmmo`, `DefaultSpeed` |
| ローカル変数・引数 | camelCase | `damageAmount`, `targetEnemy` |
| private フィールド | `_` + camelCase | `_health`, `_moveSpeed` |
| インターフェース | `I` + PascalCase | `IDamageable`, `IInteractable` |

### 1.2 変数名

| ルール | 形式 | 例 |
|---|---|---|
| bool 変数 | `is` / `has` / `can` で始める | `isAlive`, `canJump`, `hasKey` |
| コレクション | 複数形 | `enemies`, `spawnPoints` |
| enum | 単数形（フラグ用途のみ複数形） | `WeaponType`, `DirectionFlags` |

```csharp
// ✅ 良い例
private int _currentHealth;
private bool _isDead;
private List<GameObject> _spawnedEnemies;
private const int MaxAmmo = 30;

// ❌ 悪い例
private int health;              // _camelCase でない
private bool dead;               // is/has/can で始まっていない
private List<GameObject> e;      // 意味がわからない
private const int MAX_AMMO = 30; // 定数は PascalCase
```

### 1.3 メソッド名

PascalCase で記述し、**動詞から始める**ことを基本とします。

| 用途 | 接頭辞 | 例 |
|---|---|---|
| 値の取得 | `Get` | `GetHealth()`, `GetPlayerName()` |
| 値の設定 | `Set` | `SetSpeed()`, `SetMaxHealth()` |
| 状態の判定 | `Is` | `IsDead()`, `IsGrounded()` |
| 可否の判定 | `Can` | `CanAttack()`, `CanJump()` |
| 所持の判定 | `Has` | `HasItem()`, `HasKey()` |
| イベント発火・通知 | `On` | `OnDeath()`, `OnHitReceived()` |
| イベントのハンドル | `Handle` | `HandleInput()`, `HandleDamage()` |

```csharp
// ✅ 良い例
public float GetHealth() => _health;
public void SetSpeed(float newSpeed) { _moveSpeed = newSpeed; }
public bool IsDead() => _health <= 0;
private void OnDeath() { }

// ❌ 悪い例
public float health() => _health;   // Get が無い、PascalCase でない
public void speed(float s) { }      // 動詞から始まっていない、引数名が不明瞭
```

### 1.4 アセット命名規則

アセットには**種類を示す接頭辞**を付けてください。

| 接頭辞 | アセット種類 | 例 |
|---|---|---|
| `PF_` | Prefab | `PF_Player`, `PF_Enemy` |
| `M_` | Material | `M_Ground`, `M_PlayerBody` |
| `T_` | Texture | `T_Ground_Albedo` |
| `AC_` | Animator Controller | `AC_Player` |
| `AN_` | Animation Clip | `AN_Player_Run` |
| `SO_` | ScriptableObject | `SO_EnemyStatus` |
| `SE_` / `BGM_` | Audio（効果音 / BGM） | `SE_Jump`, `BGM_Title` |
| `Scene_` | Scene | `Scene_Title`, `Scene_Stage01` |

---

## 2. フォルダ構成

### 2.1 個人作業フォルダ

各メンバーは `MiraiSozo2027/Assets/_Sandbox/<名前>/` を個人作業フォルダとして使用します。

**作り方**:
1. Unity の Project ウィンドウで `_Sandbox/_Template` フォルダを選択
2. 右クリック → **Duplicate** を選択
3. 複製されたフォルダを自分の名前にリネーム

> [!CAUTION]
> **Windows のエクスプローラーでのコピーは禁止です。** `.meta` ファイルの GUID が重複し、アセット参照が壊れます。**必ず Unity の Project ウィンドウ上で Duplicate を使用してください。**

> [!CAUTION]
> **`_Template` フォルダには個人の作業物を置かないでください。** あくまで雛形です。個人作業はリネーム後のフォルダで行ってください。

> [!CAUTION]
> **他メンバーのフォルダ・共有アセット・シーン・Prefab は無断で編集しないでください。**  
> 編集が必要な場合は、**事前に宣言**（Slack 等で共有）してから作業してください。

### 2.2 作業の流れ

```mermaid
flowchart LR
    A[個人フォルダで作業<br/>_Sandbox/&lt;名前&gt;/] --> B[PR を作成]
    B --> C[レビュー・承認]
    C --> D[_Project/配下の<br/>該当フォルダへ移動]
```

> **📌 ルール**: シーンは**担当ごとに分割**し、同時編集を避けてください。

### 2.3 MiraiSozo2027/Assets/ 全体の構成

```
MiraiSozo2027/Assets/
├── _Project/                 # 本番用(共有)
│   ├── Scripts/
│   │   ├── Player/
│   │   ├── Enemy/
│   │   ├── UI/
│   │   ├── Systems/
│   │   └── Utils/
│   ├── Prefabs/
│   ├── Scenes/
│   ├── Art/
│   ├── Audio/
│   ├── Animations/
│   └── Settings/
├── _Sandbox/                 # 個人の試作・検証用
│   ├── _Template/            # メンバー別フォルダの雛形(Unity上で複製して使う)
│   │   ├── Scripts/
│   │   ├── Prefabs/
│   │   ├── Scenes/
│   │   ├── Art/
│   │   ├── Audio/
│   │   └── Animations/
│   └── <名前>/               # 個人作業フォルダ(_Template を複製して作成)
└── ThirdParty/               # 外部アセット置き場
```

> [!CAUTION]
> **`Misc` フォルダは作成禁止です。** 分類できないものは適切なフォルダ名をチームで相談して決めてください。

### 2.4 スクリプトの配置

- スクリプトは**機能別のサブフォルダ**に置く（例: `Scripts/Player/`）
- **1ファイル1クラス**、**ファイル名＝クラス名**とする

---

## 3. スクリプト設計

### 3.1 Inspector への公開

Inspector に公開する場合は `[SerializeField] private` を使い、`public` フィールドは避けてください。

```csharp
// ✅ 良い例
[SerializeField] private float _moveSpeed = 5f;

// ❌ 悪い例
public float moveSpeed = 5f;   // 外部から書き換え可能になってしまう
```

### 3.2 役割分担

| 対象 | 置き場所 |
|---|---|
| ロジック | C# スクリプト |
| 見た目・調整値 | Inspector |
| 共有データ | ScriptableObject |

### 3.3 設計方針

- コンポーネント単位で**小さく**作る
- シングルトンは**最小限**にする
- 疎結合にしたい場合は `event` / `Action` を使う
- コメントは「何をしているか」ではなく**「なぜそうするか」**を書く
- メソッドは**目安 30 行以内**に収める

---

## 4. パフォーマンス・禁止事項

### 4.1 Update 内での検索・取得

`Update` 内で `GetComponent` / `Find` / `Camera.main` を呼ばず、`Awake` / `Start` で**キャッシュ**してください。

```csharp
// ✅ 良い例
private Rigidbody _rigidbody;
private Camera _mainCamera;

private void Awake()
{
    _rigidbody = GetComponent<Rigidbody>();
    _mainCamera = Camera.main;
}

private void Update()
{
    _rigidbody.AddForce(Vector3.forward);
}

// ❌ 悪い例
private void Update()
{
    GetComponent<Rigidbody>().AddForce(Vector3.forward);  // 毎フレーム取得している
    var cam = Camera.main;                                // 毎フレーム検索している
    GameObject.Find("Player");                            // 毎フレーム検索している
}
```

### 4.2 禁止事項・注意事項

| 項目 | ルール |
|---|---|
| 毎フレームの `new` | 避ける（事前に生成して使い回す） |
| 毎フレームの文字列結合 | 避ける |
| `Debug.Log` | 不要なものは残さない |
| マジックナンバー | `const` か `[SerializeField]` にする |

```csharp
// ✅ 良い例
private const float JumpPower = 8f;
[SerializeField] private float _moveSpeed = 5f;

// ❌ 悪い例
rigidbody.AddForce(Vector3.up * 8f);   // 8f の意味が不明
```

---

## 5. Git との接点

> **📌 ルール**: ブランチ運用・コミット・PR の詳細は別途 `CONTRIBUTING.md` で定めます。ここではコーディングに関わる最低限のみ記載します。

| 項目 | ルール |
|---|---|
| `.meta` ファイル | **必ずコミットする** |
| 秘匿情報（API キー等） | コミットしない |
| `Library/`, `Temp/`, `Logs/`, `obj/` | コミットしない（`.gitignore` を確認） |
| コミット単位 | 1コミット1目的 |
| `main` への push | 直接 push せず、ブランチ → PR |
| 同じシーン／Prefab の同時編集 | しない（担当を事前共有） |

---

## 6. 生成AI・素材

### 6.1 生成AIの利用

- AI 生成コードは**理解・検証してから採用**する（説明できないコードは入れない）
- 生成結果をそのまま作品にしない
- 責任は**制作者本人**が負う

### 6.2 素材

- 使用素材の出典・ライセンスは `CREDITS.md` に記録する
- 外注の丸投げは禁止

---

ブランチ運用・コミット・PR は別途 `CONTRIBUTING.md` で定める。
