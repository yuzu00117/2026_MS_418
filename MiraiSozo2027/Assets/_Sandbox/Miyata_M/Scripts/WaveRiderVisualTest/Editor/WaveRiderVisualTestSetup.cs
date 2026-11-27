using System.IO;
using Aura.Gimmick;
using Aura.ObjectInfo;
using Aura.Wave;
using Aura.PlayerControl;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace Aura.Gimmick.EditorTools
{
    /// @brief WaveRider を目で見て確かめるシーン（WaveRiderVisualTest）を作る
    /// 波に乗っている間の動き（乗る、折り返す、押し戻す、線を追う、降りる、乗っている物を運ぶ）は自動テストが無いので、ここで見る
    /// プレイヤー・カメラ・GameManager・地面は WaveRiderTest シーンのものを引き継ぎ、それ以外の場面を X 方向に並べて置く
    /// メニュー: Tools/WaveRider/目視確認シーンを作る。何度実行しても同じシーンを作り直す（上書き）
    ///
    ///   z ^      A        B      C       D          E(4 本)            F            G
    ///     |   反射と   短い線  首振り  首振り   波が途切れる       乗っている物   スイッチ
    ///     |   折り返し         ゆっくり 速い    止/慣/戻/経路
    ///     +-----------------------------------------------------------------------> x
    ///        プレイヤーは A の手前（-z 側）から始まる
    public static class WaveRiderVisualTestSetup
    {
        private const string SourceScenePath = "Assets/_Sandbox/Miyata_M/Scenes/WaveRiderTest/WaveRiderTest.unity";
        private const string SceneDir = "Assets/_Sandbox/Miyata_M/Scenes/WaveRiderVisualTest";
        private const string ScenePath = SceneDir + "/WaveRiderVisualTest.unity";
        private const string MaterialDir = SceneDir + "/Materials";
        private const string NavMeshPath = SceneDir + "/NavMesh_WaveRiderVisualTest.asset";

        private const string RiderPrefabPath = "Assets/_Sandbox/Miyata_M/Prefabs/PF_WaveRider.prefab";
        private const string BeamTurretPrefabPath = "Assets/_Project/Prefabs/Stage/Turrets/PF_BeamTurret_Electromagnetic.prefab";
        private const string SwingTurretPrefabPath = "Assets/_Project/Prefabs/Stage/Turrets/PF_SwingTurret.prefab";
        private const string PressurePlatePrefabPath = "Assets/_Project/Prefabs/Stage/Switches/PF_PressurePlate.prefab";
        private const string MirrorSubstancePath = "Assets/_Project/ScriptableObjects/ObjectTypes/SO_Substance_Mirror.asset";
        private const string ConcreteSubstancePath = "Assets/_Project/ScriptableObjects/ObjectTypes/SO_Substance_Concrete.asset";

        private const string LitShaderName = "Universal Render Pipeline/Lit";

        private const float StationA = 0.0f;    ///< 反射と折り返し
        private const float StationB = 25.0f;   ///< 短い線
        private const float StationC = 50.0f;   ///< 首振り（ゆっくり）
        private const float StationD = 80.0f;   ///< 首振り（速い）
        private const float StationE = 115.0f;  ///< 波が途切れる（4 本）
        private const float StationF = 150.0f;  ///< 乗っている物
        private const float StationG = 185.0f;  ///< スイッチ
        private const float LaneSpacing = 6.0f; ///< 同じ場面の中で並べる線の間隔（m）

        private const float LineHeight = 3.54f;   ///< タレットの線の高さ（m）
        private const float MuzzleZ = 1.46f;      ///< タレットの線が始まる位置（砲身の先、タレットの位置からの z）
        private const float RiderHeight = 4.04f;  ///< 1m のライダーの中心の高さ（線の高さ + 線からの高さのずれ 0.5）
        private const float WallCenterY = 4.0f;   ///< 壁の中心の高さ（m）
        private const float WallHeight = 8.0f;    ///< 壁の高さ（m）
        private const float WallThickness = 0.6f; ///< 壁の厚み（m）
        private const float LabelY = 9.5f;        ///< 説明の文字の高さ（m）
        private const float LabelZ = -1.5f;       ///< 説明の文字の奥行きの位置（m）

        private const float GroundMinX = -20.0f;  ///< 地面の範囲（m）
        private const float GroundMaxX = 205.0f;
        private const float GroundMinZ = -12.0f;
        private const float GroundMaxZ = 24.0f;

        private const float FastSwingSpeed = 150.0f;  ///< D の首振りの速さ（度/s）。線が「線に付いていく速さの上限」より速く動くようにする
        private const float BlockerSpeed = 1.5f;      ///< E の遮る板が上下する速さ（m/s）
        private const float BlockerWait = 2.0f;       ///< E の遮る板が端で止まる時間（秒）
        private const float BlockerLift = 3.0f;       ///< E の遮る板が線の上へ退いている高さ（m）。上から始まるので、最初は乗れる
        private const float PlayerOnlyPlateMass = 1.0f; ///< G の床スイッチの起動に必要な Mass。プレイヤー(Mass 1)だけで踏んで起動するようにする

        private static Material s_riderMaterial;
        private static Material s_wallMaterial;
        private static Material s_mirrorMaterial;
        private static Material s_cargoMaterial;
        private static Transform s_environment;

        /// @brief シーンを作り直して保存する
        [MenuItem("Tools/WaveRider/目視確認シーンを作る")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }
            Directory.CreateDirectory(MaterialDir);
            CreateMaterials();

            Scene scene = EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Single);
            KeepOnlyCommonObjects(scene);

            s_environment = new GameObject("Environment").transform;
            PlaceGroundAndPlayer(scene);

            BuildReflectAndPingPong(StationA);
            BuildShortLine(StationB);
            BuildSlowSwing(StationC);
            BuildFastSwing(StationD);
            BuildLostWave(StationE);
            BuildPassengers(StationF);
            BuildSwitch(StationG);

            BakeNavMesh();
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("WaveRiderVisualTest: シーンを作りました " + ScenePath);
        }

        //============================================================
        // 場面
        //============================================================

        /// @brief A: 鏡で曲がる線を往復する 3x1x3 のライダー
        /// 見る所: 曲がり角で押し戻されても降りない / 終点で壁に食い込まない / 折り返しで砲台に食い込まない / 曲がり角で向きを変える
        private static void BuildReflectAndPingPong(float x)
        {
            WaveEmitter turret = PlaceTurret(BeamTurretPrefabPath, "A_Turret", x);
            PlaceWall("A_Mirror", new Vector3(x, WallCenterY, 14.0f), 45.0f, 6.0f, true);
            PlaceWall("A_EndWall", new Vector3(x - 10.0f, WallCenterY, 14.0f), 90.0f, 8.0f, false);
            WaveRider rider = PlaceRider("A_Rider_3x3", new Vector3(x, RiderHeight, 5.0f), new Vector3(3.0f, 1.0f, 3.0f), turret);
            SetEnum(rider, "_endMode", (int)WaveRiderEndMode.PingPong);
            SetBool(rider, "_isFacingTravelDirection", true);
            PlaceLabel(x, "A 反射と折り返し（3x1x3）\n角で降りない・終点と砲台に食い込まない");
        }

        /// @brief B: ライダーの前後の広がりより短い線
        /// 見る所: 終点側の端に留まり、毎ステップ向きが反転して震えない
        private static void BuildShortLine(float x)
        {
            WaveEmitter turret = PlaceTurret(BeamTurretPrefabPath, "B_Turret", x);
            // 線は砲口（z=1.46）から壁の面まで 0.6m。ライダー（前後 1m）より短い
            // ライダーは砲身と壁に重ならないよう横に置き、横から線へ寄せて乗せる
            PlaceWall("B_Wall", new Vector3(x, WallCenterY, MuzzleZ + 0.6f + WallThickness * 0.5f), 0.0f, 4.0f, false);
            WaveRider rider = PlaceRider("B_Rider", new Vector3(x + 1.5f, RiderHeight, MuzzleZ + 0.3f), Vector3.one, turret);
            SetEnum(rider, "_endMode", (int)WaveRiderEndMode.PingPong);
            PlaceLabel(x, "B 短い線（折り返す設定）\n震えずに留まる");
        }

        /// @brief C: ゆっくり首を振るタレットの線に乗る
        /// 見る所: 線が動いても遅れずに付いていく / 壁に沿って押し戻される
        private static void BuildSlowSwing(float x)
        {
            WaveEmitter turret = PlaceTurret(SwingTurretPrefabPath, "C_SwingTurret", x);
            PlaceSwingWalls("C", x);
            WaveRider rider = PlaceRider("C_Rider", new Vector3(x, RiderHeight, 4.0f), Vector3.one, turret);
            SetFloat(rider, "_carrySpeed", 1.0f);
            SetBool(rider, "_isFacingTravelDirection", true);
            PlaceLabel(x, "C 首振り（ゆっくり）\n線に付いていく");
        }

        /// @brief D: 速く首を振るタレットの線に乗る
        /// 見る所: 付いていく速さの上限で遅れ、1m 遅れたら降りる / 降りたあと慣性で飛んでいかない（「運ばれる速さ」まで）/ 線が戻れば乗り直す
        private static void BuildFastSwing(float x)
        {
            WaveEmitter turret = PlaceTurret(SwingTurretPrefabPath, "D_SwingTurret", x);
            SetFloat(turret.GetComponent<GimmickRotator>(), "_speed", FastSwingSpeed);
            PlaceSwingWalls("D", x);
            WaveRider rider = PlaceRider("D_Rider", new Vector3(x, RiderHeight, 8.0f), Vector3.one, turret);
            SetEnum(rider, "_lostWaveMode", (int)WaveRiderLostWaveMode.Inertia);
            PlaceLabel(x, "D 首振り（速い）\n遅れて降りる・慣性で飛ばない");
        }

        /// @brief E: 上下する板で波が途切れる線を 4 本並べ、「波が無くなったとき」の動きを比べる
        /// 見る所: その場で止まる / 慣性で少し進んで止まる / 初期位置へまっすぐ戻る / NavMesh の経路で戻る。波が戻れば乗り直す
        private static void BuildLostWave(float x)
        {
            WaveRiderLostWaveMode[] modes =
            {
                WaveRiderLostWaveMode.StopInPlace,
                WaveRiderLostWaveMode.Inertia,
                WaveRiderLostWaveMode.ReturnToStart,
                WaveRiderLostWaveMode.ReturnByNavMesh,
            };
            for (int i = 0; i < modes.Length; i++)
            {
                float laneX = x + i * LaneSpacing;
                string prefix = "E" + i;
                WaveEmitter turret = PlaceTurret(BeamTurretPrefabPath, prefix + "_Turret", laneX);
                PlaceBlocker(prefix + "_Blocker", new Vector3(laneX, LineHeight + BlockerLift, 2.2f));
                PlaceWall(prefix + "_EndWall", new Vector3(laneX, WallCenterY, 16.0f), 0.0f, 3.0f, false);
                WaveRider rider = PlaceRider(prefix + "_Rider_" + modes[i], new Vector3(laneX, RiderHeight, 4.0f), Vector3.one, turret);
                SetEnum(rider, "_lostWaveMode", (int)modes[i]);
                // 終点で止まると、そのあと波が途切れても動きの違いが見えないので往復させる
                SetEnum(rider, "_endMode", (int)WaveRiderEndMode.PingPong);
            }
            PlaceLabel(x + LaneSpacing * 1.5f, "E 波が途切れる（板が上下して遮る）\n左から: その場で止まる / 慣性 / 初期位置へ戻る / 経路探索で戻る");
        }

        /// @brief F: 乗っている物を運ぶ
        /// 見る所: 2 つのライダーにまたがった板が 2 倍の速さで運ばれない / 曲がり角で上の箱も一緒に回る
        private static void BuildPassengers(float x)
        {
            const float twinGap = 1.1f;  // 並んだ 2 本の線の間隔（m）
            WaveEmitter left = PlaceTurret(BeamTurretPrefabPath, "F_TurretLeft", x);
            WaveEmitter right = PlaceTurret(BeamTurretPrefabPath, "F_TurretRight", x + twinGap);
            PlaceWall("F_TwinEndWall", new Vector3(x + twinGap * 0.5f, WallCenterY, 14.0f), 0.0f, 4.0f, false);
            WaveRider leftRider = PlaceRider("F_RiderLeft", new Vector3(x, RiderHeight, 4.0f), Vector3.one, left);
            WaveRider rightRider = PlaceRider("F_RiderRight", new Vector3(x + twinGap, RiderHeight, 4.0f), Vector3.one, right);
            SetFloat(leftRider, "_carrySpeed", 1.5f);
            SetFloat(rightRider, "_carrySpeed", 1.5f);
            PlaceCargo("F_BridgeBoard", new Vector3(x + twinGap * 0.5f, RiderHeight + 0.71f, 4.0f), new Vector3(2.0f, 0.4f, 0.8f));

            float turnX = x + LaneSpacing;
            WaveEmitter turnTurret = PlaceTurret(BeamTurretPrefabPath, "F_TurnTurret", turnX);
            PlaceWall("F_Mirror", new Vector3(turnX, WallCenterY, 10.0f), -45.0f, 6.0f, true);
            PlaceWall("F_TurnEndWall", new Vector3(turnX + 10.0f, WallCenterY, 10.0f), 90.0f, 8.0f, false);
            WaveRider turnRider = PlaceRider("F_RiderTurn_2x2", new Vector3(turnX, RiderHeight, 4.0f), new Vector3(2.0f, 1.0f, 2.0f), turnTurret);
            SetFloat(turnRider, "_carrySpeed", 1.5f);
            SetBool(turnRider, "_isFacingTravelDirection", true);
            PlaceCargo("F_TurnBox", new Vector3(turnX, RiderHeight + 0.81f, 4.0f), new Vector3(0.6f, 0.6f, 0.6f));

            PlaceLabel(x + LaneSpacing * 0.5f, "F 乗っている物\nまたがった板は 1 回だけ運ぶ / 曲がり角で箱も回る");
        }

        /// @brief G: 床スイッチを踏んでいる間だけ波に乗る
        /// 見る所: プレイヤーが踏むと往復し、離れると止まる
        private static void BuildSwitch(float x)
        {
            WaveEmitter turret = PlaceTurret(BeamTurretPrefabPath, "G_Turret", x);
            PlaceWall("G_EndWall", new Vector3(x, WallCenterY, 14.0f), 0.0f, 4.0f, false);
            WaveRider rider = PlaceRider("G_Rider", new Vector3(x, RiderHeight, 4.0f), Vector3.one, turret);
            SetEnum(rider, "_endMode", (int)WaveRiderEndMode.PingPong);
            SetBool(rider, "_isRequiringActivation", true);

            var plate = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PressurePlatePrefabPath));
            plate.name = "G_PressurePlate";
            plate.transform.position = new Vector3(x - 3.0f, 0.05f, -3.0f);
            PressurePlate sensor = plate.GetComponentInChildren<PressurePlate>();
            SetFloat(sensor, "_requiredMass", PlayerOnlyPlateMass);   // プレハブの既定値(2)では、Mass 1 のプレイヤーだけで踏んでも起動しない
            AddListener(sensor, "_onActivated", rider.Activate);
            AddListener(sensor, "_onDeactivated", rider.Deactivate);
            PlaceLabel(x, "G スイッチ（左手前の床スイッチ）\n踏んでいる間だけ往復する");
        }

        //============================================================
        // 置く部品
        //============================================================

        /// @brief WaveRiderTest から引き継ぐ物（ライト、地面、GameManager、カメラ、プレイヤー）以外を消す
        private static void KeepOnlyCommonObjects(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                string name = root.name.Trim();
                bool isKept = name == "Directional Light" || name == "Ground" || name == "PF_GameManager"
                              || name == "PF_MainCamera" || name == "Test_Player";
                if (!isKept)
                {
                    Object.DestroyImmediate(root);
                }
            }
        }

        /// @brief 地面を全部の場面の下に広げ、プレイヤーを A の手前に置く
        private static void PlaceGroundAndPlayer(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                string name = root.name.Trim();
                if (name == "Ground")
                {
                    // 地面は Ground レイヤーのまま使う（プレイヤーの接地判定が見るため）。NavMesh を焼く対象に入れる
                    Vector3 meshSize = root.GetComponent<MeshFilter>().sharedMesh.bounds.size;
                    root.transform.SetParent(s_environment, true);
                    root.transform.position = new Vector3((GroundMinX + GroundMaxX) * 0.5f, 0.0f, (GroundMinZ + GroundMaxZ) * 0.5f);
                    root.transform.localScale = new Vector3((GroundMaxX - GroundMinX) / meshSize.x, 1.0f, (GroundMaxZ - GroundMinZ) / meshSize.z);
                }
                else if (name == "Test_Player")
                {
                    root.transform.position = new Vector3(StationA - 4.0f, 0.1f, -6.0f);
                }
                else if (name == "PF_GameManager")
                {
                    PlayerStart start = root.GetComponentInChildren<PlayerStart>();
                    if (start != null)
                    {
                        start.transform.position = new Vector3(StationA - 4.0f, 0.1f, -6.0f);
                    }
                }
            }
        }

        /// @brief タレットを置き、その WaveEmitter を返す。+z へ撃つ
        private static WaveEmitter PlaceTurret(string prefabPath, string name, float x)
        {
            var turret = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath));
            turret.name = name;
            turret.transform.position = new Vector3(x, 0.0f, 0.0f);
            return turret.GetComponent<WaveEmitter>();
        }

        /// @brief ライダー（PF_WaveRider）を置き、乗る波を出すものを入れる
        private static WaveRider PlaceRider(string name, Vector3 position, Vector3 scale, WaveEmitter emitter)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(RiderPrefabPath));
            go.name = name;
            go.transform.position = position;
            go.transform.localScale = scale;
            foreach (MeshRenderer renderer in go.GetComponentsInChildren<MeshRenderer>())
            {
                renderer.sharedMaterial = s_riderMaterial;
            }
            WaveRider rider = go.GetComponent<WaveRider>();
            SetObject(rider, "_emitter", emitter);
            return rider;
        }

        /// @brief 壁を置く。反射するなら鏡の Substance、しないならコンクリートの Substance を付ける
        /// @param yaw Y 軸まわりの向き（度）。0 なら面が z を向く
        private static void PlaceWall(string name, Vector3 position, float yaw, float width, bool isMirror)
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = name;
            wall.transform.SetParent(s_environment, false);
            wall.transform.SetPositionAndRotation(position, Quaternion.Euler(0.0f, yaw, 0.0f));
            wall.transform.localScale = new Vector3(width, WallHeight, WallThickness);
            wall.GetComponent<MeshRenderer>().sharedMaterial = isMirror ? s_mirrorMaterial : s_wallMaterial;
            Substance substance = wall.AddComponent<Substance>();
            SetObject(substance, "_type", AssetDatabase.LoadAssetAtPath<Object>(isMirror ? MirrorSubstancePath : ConcreteSubstancePath));
        }

        /// @brief 首を振るタレットの線が当たる L 字の壁（奥と右）を置く
        private static void PlaceSwingWalls(string prefix, float x)
        {
            PlaceWall(prefix + "_BackWall", new Vector3(x + 5.0f, WallCenterY, 12.0f), 0.0f, 16.0f, false);
            PlaceWall(prefix + "_SideWall", new Vector3(x + 12.0f, WallCenterY, 5.0f), 90.0f, 14.0f, false);
        }

        /// @brief 線を遮る板を線の上に置き、線の高さまで下りては戻る往復をさせる（下りている間は、板より先の波が途切れる）
        private static void PlaceBlocker(string name, Vector3 position)
        {
            GameObject blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blocker.name = name;
            blocker.transform.position = position;
            blocker.transform.localScale = new Vector3(1.5f, 1.5f, 0.3f);
            blocker.GetComponent<MeshRenderer>().sharedMaterial = s_wallMaterial;
            Substance substance = blocker.AddComponent<Substance>();
            SetObject(substance, "_type", AssetDatabase.LoadAssetAtPath<Object>(ConcreteSubstancePath));
            GimmickMover mover = blocker.AddComponent<GimmickMover>();
            SetEnum(mover, "_motionMode", (int)GimmickMotionMode.PingPong);
            SetBool(mover, "_isActiveOnStart", true);
            SetFloat(mover, "_endWaitSeconds", BlockerWait);
            SetFloat(mover, "_speed", BlockerSpeed);
            SetVector3(mover, "_activeOffset", new Vector3(0.0f, -BlockerLift, 0.0f));
        }

        /// @brief ライダーの上に載せる箱（Rigidbody）を置く
        private static void PlaceCargo(string name, Vector3 position, Vector3 size)
        {
            GameObject cargo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cargo.name = name;
            cargo.transform.position = position;
            cargo.transform.localScale = size;
            cargo.GetComponent<MeshRenderer>().sharedMaterial = s_cargoMaterial;
            cargo.AddComponent<Rigidbody>();
        }

        /// @brief 場面の説明を、場面の手前の上に置く（-z 側から読める向き）
        private static void PlaceLabel(float x, string text)
        {
            var go = new GameObject("Label");
            go.transform.position = new Vector3(x, LabelY, LabelZ);
            TextMesh mesh = go.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.fontSize = 64;
            mesh.characterSize = 0.08f;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = Color.white;
        }

        /// @brief 地面と壁から NavMesh を焼いて、アセットに保存する（E の「経路探索で戻る」用）
        /// ライダーは Environment の子に入れないので、NavMesh に含まれない
        private static void BakeNavMesh()
        {
            NavMeshSurface surface = s_environment.gameObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.BuildNavMesh();
            AssetDatabase.DeleteAsset(NavMeshPath);
            AssetDatabase.CreateAsset(surface.navMeshData, NavMeshPath);
        }

        /// @brief 場面ごとの色の材質を作る（あれば作り直さず色だけ合わせる）
        private static void CreateMaterials()
        {
            s_riderMaterial = LoadOrCreateMaterial("M_VT_Rider", new Color(0.95f, 0.6f, 0.1f));
            s_wallMaterial = LoadOrCreateMaterial("M_VT_Wall", new Color(0.55f, 0.55f, 0.6f));
            s_mirrorMaterial = LoadOrCreateMaterial("M_VT_Mirror", new Color(0.4f, 0.8f, 1.0f));
            s_cargoMaterial = LoadOrCreateMaterial("M_VT_Cargo", new Color(0.3f, 0.85f, 0.35f));
        }

        private static Material LoadOrCreateMaterial(string name, Color color)
        {
            string path = MaterialDir + "/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find(LitShaderName));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(material);
            return material;
        }

        //============================================================
        // private のシリアライズ欄に書く
        //============================================================

        private static SerializedProperty Find(SerializedObject so, string field)
        {
            SerializedProperty property = so.FindProperty(field);
            Debug.Assert(property != null, "WaveRiderVisualTest: 欄が見つからない " + field);
            return property;
        }

        private static void SetObject(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            Find(so, field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetFloat(Object target, string field, float value)
        {
            var so = new SerializedObject(target);
            Find(so, field).floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetBool(Object target, string field, bool value)
        {
            var so = new SerializedObject(target);
            Find(so, field).boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetEnum(Object target, string field, int value)
        {
            var so = new SerializedObject(target);
            Find(so, field).intValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetVector3(Object target, string field, Vector3 value)
        {
            var so = new SerializedObject(target);
            Find(so, field).vector3Value = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// @brief private の UnityEvent 欄に、引数なしの処理を保存される形でつなぐ
        private static void AddListener(Object target, string field, UnityAction action)
        {
            var so = new SerializedObject(target);
            so.Update();
            var holder = target as MonoBehaviour;
            var info = holder.GetType().GetField(field, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Debug.Assert(info != null, "WaveRiderVisualTest: イベントが見つからない " + field);
            var unityEvent = (UnityEvent)info.GetValue(holder);
            UnityEventTools.AddPersistentListener(unityEvent, action);
            EditorUtility.SetDirty(target);
        }
    }
}
