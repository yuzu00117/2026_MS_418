using System;
using Aura.Wave;
using Aura.Wave.Controls;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Aura.Prototype.Editor
{
    /// @brief 波を試すデモのシーン（Assets/_Sandbox/Yamaki_I/Scenes/Scene_WaveDemo.unity）と、波の種類のアセットを作る
    /// メニューの Aura > Prototype > Build Wave Demo Scene から実行する
    public static class WaveDemoSceneBuilder
    {
        private const string ScenePath = "Assets/_Sandbox/Yamaki_I/Scenes/Scene_WaveDemo.unity";
        private const string WaveTypeFolder = "Assets/_Sandbox/Yamaki_I/Art/WaveTypes";
        private const string MaterialFolder = "Assets/_Sandbox/Yamaki_I/Art/Materials";
        private const string ReflectionCourseName = "ReflectionCourse (Reflect / Sustain)";
        private const float SensorPanelHeight = 1.6f;  ///< 板の形の受け手の、板の中心の高さ（m）。目の高さにそろえて、まっすぐ狙えるようにする

        private static readonly Vector3 PitchPivotPosition = new Vector3(0.0f, 1.6f, 0.0f);            ///< プレイヤーの足元から見た、視点を上下に回す中心（目の高さ）
        private static readonly Vector3 FirstPersonMuzzlePosition = new Vector3(0.25f, -0.22f, 0.62f);  ///< 一人称の視点から見た、手の先（Muzzle）の位置

        private static readonly Color GroundColor = new Color(0.25f, 0.27f, 0.3f);
        private static readonly Color WallColor = new Color(0.55f, 0.57f, 0.6f);
        private static readonly Color DoorColor = new Color(0.85f, 0.5f, 0.2f);
        private static readonly Color LiftColor = new Color(0.25f, 0.45f, 0.85f);
        private static readonly Color DeviceColor = new Color(0.35f, 0.35f, 0.38f);
        private static readonly Color ReflectorColor = new Color(0.8f, 0.92f, 1.0f);

        //============================================================
        // メニュー
        //============================================================

        /// @brief デモのシーンを作り直して開く
        [MenuItem("Aura/Prototype/Build Wave Demo Scene")]
        public static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }
            bool hasScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null;
            if (hasScene && !EditorUtility.DisplayDialog("Build Wave Demo Scene",
                    $"{ScenePath} を作り直します。シーンへの変更は消えます。", "作り直す", "やめる"))
            {
                return;
            }
            Build();
        }

        /// @brief 開いているシーンに、反射を試すコースだけを作り直して足す。シーンのほかの物と、その変更は残す
        /// Reflection Mode が None の WaveEmitter は、コースを試せるよう Reflectors Only にする。シーンは保存しない（Undo で戻せる）
        /// マテリアルは、すでにあれば色を変えずにそのまま使う（ないものだけ作る）
        [MenuItem("Aura/Prototype/Add Reflection Course To Open Scene")]
        public static void AddReflectionCourseFromMenu()
        {
            Scene scene = SceneManager.GetActiveScene();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == ReflectionCourseName)
                {
                    Undo.DestroyObjectImmediate(root);
                }
            }

            GameObject course = BuildReflectionCourse(
                LoadOrCreateMaterial("M_Wall", WallColor),
                LoadOrCreateMaterial("M_Reflector", ReflectorColor),
                LoadOrCreateMaterial("M_Door", DoorColor),
                LoadOrCreateMaterial("M_Device", DeviceColor));
            Undo.RegisterCreatedObjectUndo(course, "Add Reflection Course");

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (WaveEmitter emitter in root.GetComponentsInChildren<WaveEmitter>(true))
                {
                    if (emitter.ReflectionMode != WaveReflectionMode.None)
                    {
                        continue;
                    }
                    var serialized = new SerializedObject(emitter);
                    serialized.FindProperty("_reflectionMode").enumValueIndex = (int)WaveReflectionMode.ReflectorsOnly;
                    serialized.ApplyModifiedProperties();
                    Debug.Log($"Reflection Mode of {emitter.name} was changed from None to ReflectorsOnly");
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("Reflection course was added to the open scene. Save the scene to keep it.");
        }

        /// @brief コマンドラインから実行する（-executeMethod Aura.Prototype.Editor.WaveDemoSceneBuilder.Build）
        public static void Build()
        {
            WaveType ultrasound = CreateOrUpdateWaveType("SO_Ultrasound", "超音波", new Color(1.0f, 0.45f, 0.9f), 0.25f, 0.05f, 4.0f, 0.035f);
            WaveType radio = CreateOrUpdateWaveType("SO_RadioWave", "電波", new Color(0.3f, 0.9f, 1.0f), 0.7f, 0.12f, 10.0f, 0.045f);

            Material groundMaterial = CreateOrUpdateMaterial("M_Ground", GroundColor);
            Material wallMaterial = CreateOrUpdateMaterial("M_Wall", WallColor);
            Material doorMaterial = CreateOrUpdateMaterial("M_Door", DoorColor);
            Material liftMaterial = CreateOrUpdateMaterial("M_Lift", LiftColor);
            Material deviceMaterial = CreateOrUpdateMaterial("M_Device", DeviceColor);
            Material reflectorMaterial = CreateOrUpdateMaterial("M_Reflector", ReflectorColor);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.GetComponent<Camera>() != null)
                {
                    Object.DestroyImmediate(root);
                }
            }

            CreateBox("Ground", null, new Vector3(0.0f, -0.25f, 10.0f), new Vector3(40.0f, 0.5f, 50.0f), groundMaterial);
            BuildPlayer(ultrasound, radio);
            BuildSoundDoor(ultrasound, wallMaterial, doorMaterial, deviceMaterial);
            BuildRadioLift(radio, wallMaterial, liftMaterial, deviceMaterial);
            BuildToggleLamp(deviceMaterial);
            BuildReflectionCourse(wallMaterial, reflectorMaterial, doorMaterial, deviceMaterial);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"Wave demo scene was built: {ScenePath}");
        }

        //============================================================
        // プレイヤー
        //============================================================

        private static void BuildPlayer(WaveType ultrasound, WaveType radio)
        {
            var player = new GameObject("Player");
            var characterController = player.AddComponent<CharacterController>();
            characterController.height = 1.8f;
            characterController.radius = 0.35f;
            characterController.center = new Vector3(0.0f, 0.9f, 0.0f);

            GameObject body = CreatePrimitive(PrimitiveType.Capsule, "Body", player.transform,
                new Vector3(0.0f, 0.9f, 0.0f), new Vector3(0.7f, 0.9f, 0.7f), null, false);

            Transform pitchPivot = CreateEmpty("PitchPivot", player.transform, PitchPivotPosition);

            // 一人称: カメラは目の位置、手は右下に見える
            Transform firstPersonView = CreateEmpty("FirstPersonView", pitchPivot, Vector3.zero);
            Camera firstPersonCamera = CreateCamera("FirstPersonCamera", firstPersonView, Vector3.zero);
            CreatePrimitive(PrimitiveType.Cube, "Hand", firstPersonView,
                new Vector3(0.25f, -0.22f, 0.45f), new Vector3(0.09f, 0.09f, 0.3f), null, false);
            Transform firstPersonMuzzle = CreateEmpty("Muzzle", firstPersonView, FirstPersonMuzzlePosition);

            // 三人称: カメラは右肩の後ろ、手は体の右から前に出す
            Transform thirdPersonView = CreateEmpty("ThirdPersonView", pitchPivot, Vector3.zero);
            Camera thirdPersonCamera = CreateCamera("ThirdPersonCamera", thirdPersonView, new Vector3(0.6f, 0.25f, -3.0f));
            CreatePrimitive(PrimitiveType.Cube, "Arm", thirdPersonView,
                new Vector3(0.4f, -0.3f, 0.25f), new Vector3(0.1f, 0.1f, 0.5f), null, false);
            Transform thirdPersonMuzzle = CreateEmpty("Muzzle", thirdPersonView, new Vector3(0.4f, -0.3f, 0.52f));
            thirdPersonView.gameObject.SetActive(false);
            body.SetActive(false);

            var mover = player.AddComponent<DemoPlayerController>();
            Configure(mover, serialized => SetObject(serialized, "_pitchPivot", pitchPivot));

            var emitter = player.AddComponent<WaveEmitter>();
            Configure(emitter, serialized =>
            {
                SetObjects(serialized, "_waveTypes", ultrasound, radio);
                SetObject(serialized, "_muzzle", firstPersonMuzzle);
                SetObject(serialized, "_aimOrigin", firstPersonCamera.transform);
                SetObject(serialized, "_ignoreRoot", player.transform);
                // 反射板だけで跳ね返る。今までのギミックには反射板がないので、今までどおりに動く（R で切り替えて比べる）
                serialized.FindProperty("_reflectionMode").enumValueIndex = (int)WaveReflectionMode.ReflectorsOnly;
            });

            player.AddComponent<WaveEmitterInput>();

            var switcher = player.AddComponent<ViewModeSwitcher>();
            Configure(switcher, serialized =>
            {
                SetObject(serialized, "_emitter", emitter);
                SetObjects(serialized, "_firstPersonObjects", firstPersonView.gameObject);
                SetObject(serialized, "_firstPersonCamera", firstPersonCamera.transform);
                SetObject(serialized, "_firstPersonMuzzle", firstPersonMuzzle);
                SetObjects(serialized, "_thirdPersonObjects", thirdPersonView.gameObject, body);
                SetObject(serialized, "_thirdPersonCamera", thirdPersonCamera.transform);
                SetObject(serialized, "_thirdPersonMuzzle", thirdPersonMuzzle);
            });

            var hud = player.AddComponent<DemoHud>();
            Configure(hud, serialized =>
            {
                SetObject(serialized, "_emitter", emitter);
                SetObject(serialized, "_viewSwitcher", switcher);
            });
        }

        //============================================================
        // ギミック
        //============================================================

        /// @brief 超音波をマイクに当てると開く扉（Latch）
        private static void BuildSoundDoor(WaveType ultrasound, Material wallMaterial, Material doorMaterial, Material deviceMaterial)
        {
            var root = new GameObject("SoundDoor (Ultrasound / Latch)");
            CreateBox("WallLeft", root.transform, new Vector3(-5.5f, 2.0f, 16.0f), new Vector3(9.0f, 4.0f, 0.5f), wallMaterial);
            CreateBox("WallRight", root.transform, new Vector3(5.5f, 2.0f, 16.0f), new Vector3(9.0f, 4.0f, 0.5f), wallMaterial);

            GameObject door = CreateBox("Door", root.transform, new Vector3(0.0f, 1.5f, 16.0f), new Vector3(2.0f, 3.0f, 0.4f), doorMaterial);
            var doorMover = door.AddComponent<DemoGimmickMover>();
            Configure(doorMover, serialized => serialized.FindProperty("_activeOffset").vector3Value = new Vector3(0.0f, 3.2f, 0.0f));

            WaveReceiver receiver = CreateMicrophone("Microphone", root.transform, new Vector3(-2.2f, 0.0f, 15.2f), deviceMaterial);
            Configure(receiver, serialized =>
            {
                SetObjects(serialized, "_acceptedTypes", ultrasound);
                serialized.FindProperty("_mode").enumValueIndex = (int)WaveActivationMode.Latch;
                serialized.FindProperty("_requiredEnergy").floatValue = 1.0f;
            });
            UnityEventTools.AddPersistentListener(receiver.ActivatedEvent, doorMover.Activate);
        }

        /// @brief 電波をアンテナに当てている間だけ上がるリフト（Sustain）
        private static void BuildRadioLift(WaveType radio, Material wallMaterial, Material liftMaterial, Material deviceMaterial)
        {
            var root = new GameObject("RadioLift (Radio Wave / Sustain)");
            GameObject lift = CreateBox("Lift", root.transform, new Vector3(-8.0f, 0.15f, 8.0f), new Vector3(3.0f, 0.3f, 3.0f), liftMaterial);
            var liftMover = lift.AddComponent<DemoGimmickMover>();
            Configure(liftMover, serialized =>
            {
                serialized.FindProperty("_activeOffset").vector3Value = new Vector3(0.0f, 2.6f, 0.0f);
                serialized.FindProperty("_speed").floatValue = 1.5f;
            });
            CreateBox("Ledge", root.transform, new Vector3(-8.0f, 1.4f, 11.0f), new Vector3(3.0f, 2.8f, 3.0f), wallMaterial);

            WaveReceiver receiver = CreateAntenna("Antenna", root.transform, new Vector3(-5.5f, 0.0f, 6.5f), deviceMaterial);
            Configure(receiver, serialized =>
            {
                SetObjects(serialized, "_acceptedTypes", radio);
                serialized.FindProperty("_mode").enumValueIndex = (int)WaveActivationMode.Sustain;
                serialized.FindProperty("_requiredEnergy").floatValue = 1.5f;
                serialized.FindProperty("_decayPerSecond").floatValue = 0.4f;
            });
            UnityEventTools.AddPersistentListener(receiver.ActivatedEvent, liftMover.Activate);
            UnityEventTools.AddPersistentListener(receiver.DeactivatedEvent, liftMover.Deactivate);
        }

        /// @brief どの波でも当てるたびに点いたり消えたりするランプ（Toggle）
        private static void BuildToggleLamp(Material deviceMaterial)
        {
            var root = new GameObject("ToggleLamp (Any Wave / Toggle)");
            root.transform.position = new Vector3(6.0f, 0.0f, 7.0f);

            CreatePrimitive(PrimitiveType.Cylinder, "Pole", root.transform,
                new Vector3(0.0f, 1.0f, 0.0f), new Vector3(0.12f, 1.0f, 0.12f), deviceMaterial, true);
            GameObject bulb = CreatePrimitive(PrimitiveType.Sphere, "Bulb", root.transform,
                new Vector3(0.0f, 2.2f, 0.0f), new Vector3(0.5f, 0.5f, 0.5f), deviceMaterial, true);

            var lightObject = new GameObject("Light");
            lightObject.transform.SetParent(bulb.transform, false);
            var lamp = lightObject.AddComponent<Light>();
            lamp.type = LightType.Point;
            lamp.range = 8.0f;
            lamp.intensity = 3.0f;
            lamp.color = new Color(1.0f, 0.9f, 0.6f);
            lamp.enabled = false;

            // 見た目でどちらの波にも反応すると分かるよう、マイクとアンテナの両方を付ける
            CreatePrimitive(PrimitiveType.Sphere, "MicHead", root.transform,
                new Vector3(-0.35f, 1.6f, 0.0f), new Vector3(0.25f, 0.25f, 0.25f), deviceMaterial, true);
            CreatePrimitive(PrimitiveType.Cylinder, "AntennaRod", root.transform,
                new Vector3(0.0f, 2.85f, 0.0f), new Vector3(0.03f, 0.4f, 0.03f), deviceMaterial, true);

            var receiver = root.AddComponent<WaveReceiver>();
            Configure(receiver, serialized =>
            {
                serialized.FindProperty("_canAcceptAnyType").boolValue = true;
                serialized.FindProperty("_mode").enumValueIndex = (int)WaveActivationMode.Toggle;
                serialized.FindProperty("_requiredEnergy").floatValue = 1.0f;
            });
            root.AddComponent<WaveReceiverFeedback>();

            var setEnabled = (UnityAction<bool>)Delegate.CreateDelegate(typeof(UnityAction<bool>), lamp,
                typeof(Behaviour).GetProperty(nameof(Behaviour.enabled)).GetSetMethod());
            UnityEventTools.AddBoolPersistentListener(receiver.ActivatedEvent, setEnabled, true);
            UnityEventTools.AddBoolPersistentListener(receiver.DeactivatedEvent, setEnabled, false);
        }

        /// @brief 音波を受けるマイクの形の受け手を作る
        private static WaveReceiver CreateMicrophone(string name, Transform parent, Vector3 position, Material material)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.position = position;

            CreatePrimitive(PrimitiveType.Cylinder, "Stand", root.transform,
                new Vector3(0.0f, 0.6f, 0.0f), new Vector3(0.08f, 0.6f, 0.08f), material, true);
            CreatePrimitive(PrimitiveType.Capsule, "Head", root.transform,
                new Vector3(0.0f, 1.4f, 0.0f), new Vector3(0.35f, 0.3f, 0.35f), material, true);

            var receiver = root.AddComponent<WaveReceiver>();
            root.AddComponent<WaveReceiverFeedback>();
            return receiver;
        }

        /// @brief 電波を受けるアンテナの形の受け手を作る
        private static WaveReceiver CreateAntenna(string name, Transform parent, Vector3 position, Material material)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.position = position;

            CreatePrimitive(PrimitiveType.Cube, "Base", root.transform,
                new Vector3(0.0f, 0.15f, 0.0f), new Vector3(0.6f, 0.3f, 0.6f), material, true);
            CreatePrimitive(PrimitiveType.Cylinder, "Mast", root.transform,
                new Vector3(0.0f, 1.3f, 0.0f), new Vector3(0.08f, 1.0f, 0.08f), material, true);
            GameObject crossBar = CreatePrimitive(PrimitiveType.Cylinder, "CrossBar", root.transform,
                new Vector3(0.0f, 2.0f, 0.0f), new Vector3(0.05f, 0.45f, 0.05f), material, true);
            crossBar.transform.localRotation = Quaternion.Euler(0.0f, 0.0f, 90.0f);
            CreatePrimitive(PrimitiveType.Sphere, "Tip", root.transform,
                new Vector3(0.0f, 2.4f, 0.0f), new Vector3(0.22f, 0.22f, 0.22f), material, true);

            var receiver = root.AddComponent<WaveReceiver>();
            root.AddComponent<WaveReceiverFeedback>();
            return receiver;
        }

        //============================================================
        // 反射を試すコース
        //============================================================

        /// @brief 反射を試すコース（スポーンの右）。WaveEmitter の Reflection Mode で、届く受け手が変わる
        /// MirrorSensor（反射板）に当てると、壁の陰の HiddenSensor まで 1 発で届く（Reflectors Only と All Surfaces）
        /// BlockingWall の手前の面に当てて跳ね返らせると、物陰の BankSensor に届く（All Surfaces だけ）
        /// 受け手は Sustain なので、しばらくすると元に戻る（切り替えて何度でも比べられる）
        /// @return コースのいちばん上の GameObject
        private static GameObject BuildReflectionCourse(Material wallMaterial, Material reflectorMaterial,
            Material beaconMaterial, Material deviceMaterial)
        {
            var root = new GameObject(ReflectionCourseName);

            // 手前（西）の面で波を跳ね返らせ、裏の HiddenSensor をスポーンから隠す壁
            GameObject wall = CreateBox("BlockingWall", root.transform, new Vector3(8.5f, 1.5f, 1.25f), new Vector3(0.5f, 3.0f, 11.5f), wallMaterial);
            float wallFaceX = wall.transform.position.x - wall.transform.lossyScale.x * 0.5f;

            Vector3 mirrorPosition = new Vector3(5.0f, 0.0f, 11.0f);
            Vector3 hiddenPosition = new Vector3(10.0f, 0.0f, 7.0f);
            Vector3 bankPosition = new Vector3(2.0f, 0.0f, -6.0f);
            Vector3 mirrorCenter = mirrorPosition + Vector3.up * SensorPanelHeight;
            Vector3 hiddenCenter = hiddenPosition + Vector3.up * SensorPanelHeight;
            Vector3 bankCenter = bankPosition + Vector3.up * SensorPanelHeight;

            // 反射板: スポーンから一人称で板の中心を狙ったときの手の先と、HiddenSensor の、ちょうど真ん中に面を向ける
            Vector3 muzzle = GetFirstPersonMuzzleWhenAiming(mirrorCenter);
            Vector3 mirrorFacing = (GetFlatDirection(mirrorCenter, muzzle) + GetFlatDirection(mirrorCenter, hiddenCenter)).normalized;
            WaveReceiver mirrorReceiver = CreatePanelSensor("MirrorSensor", root.transform, mirrorPosition, mirrorFacing,
                new Vector2(1.6f, 1.6f), reflectorMaterial, deviceMaterial, out GameObject mirrorPanel);
            ConfigureSustainReceiver(mirrorReceiver, 0.5f);
            // 板の面だけを反射板にする。柱などは、いちばん近い親の WaveSurface（Absorb）で止める
            mirrorPanel.AddComponent<WaveSurface>().Response = WaveSurfaceResponse.Reflect;
            mirrorReceiver.gameObject.AddComponent<WaveSurface>().Response = WaveSurfaceResponse.Absorb;
            // 板は反射板の色のまま見せ、起動しているかは板の上の玉の色で見せる（玉には当たらない）
            GameObject indicator = CreatePrimitive(PrimitiveType.Sphere, "Indicator", mirrorReceiver.transform,
                new Vector3(0.0f, SensorPanelHeight + 1.15f, 0.0f), new Vector3(0.4f, 0.4f, 0.4f), deviceMaterial, false);
            var mirrorFeedback = mirrorReceiver.gameObject.AddComponent<WaveReceiverFeedback>();
            Configure(mirrorFeedback, serialized => SetObjects(serialized, "_renderers", indicator.GetComponent<Renderer>()));

            // 壁の陰の受け手: 反射板で跳ね返った波で起動する。起動すると、壁の上に塔がせり上がる
            WaveReceiver hiddenReceiver = CreatePanelSensor("HiddenSensor", root.transform, hiddenPosition,
                GetFlatDirection(hiddenCenter, mirrorCenter), new Vector2(2.0f, 2.0f), deviceMaterial, deviceMaterial, out _);
            ConfigureSustainReceiver(hiddenReceiver, 0.33f);
            hiddenReceiver.gameObject.AddComponent<WaveSurface>().Response = WaveSurfaceResponse.Absorb;
            hiddenReceiver.gameObject.AddComponent<WaveReceiverFeedback>();
            CreateBeacon("HiddenBeacon", root.transform, new Vector3(11.0f, 1.4f, 5.5f), beaconMaterial, hiddenReceiver);

            // 物陰の受け手: 壁の手前の面に映した位置へ向けて撃つと、壁で跳ね返って届く。その位置へ面を向ける
            Vector3 mirroredBankCenter = new Vector3(2.0f * wallFaceX - bankCenter.x, bankCenter.y, bankCenter.z);
            Vector3 eye = PitchPivotPosition;
            Vector3 bankPoint = eye + (mirroredBankCenter - eye) * ((wallFaceX - eye.x) / (mirroredBankCenter.x - eye.x));
            CreateBox("Occluder", root.transform, new Vector3(1.5f, 1.5f, -4.3f), new Vector3(3.0f, 3.0f, 0.5f), wallMaterial);
            WaveReceiver bankReceiver = CreatePanelSensor("BankSensor", root.transform, bankPosition,
                GetFlatDirection(bankCenter, bankPoint), new Vector2(2.0f, 2.0f), deviceMaterial, deviceMaterial, out _);
            ConfigureSustainReceiver(bankReceiver, 0.33f);
            bankReceiver.gameObject.AddComponent<WaveSurface>().Response = WaveSurfaceResponse.Absorb;
            bankReceiver.gameObject.AddComponent<WaveReceiverFeedback>();
            CreateBeacon("BankBeacon", root.transform, new Vector3(1.2f, 1.4f, -7.0f), beaconMaterial, bankReceiver);

            return root;
        }

        /// @brief 柱の上に板を載せた形の受け手を作る。板の中心は SensorPanelHeight の高さで、facing の向きに面を向ける
        /// @param outPanel 板の GameObject
        /// @return 作った受け手。どの種類の波を受けるかなどは、呼んだ側で決める
        private static WaveReceiver CreatePanelSensor(string name, Transform parent, Vector3 position, Vector3 facing,
            Vector2 panelSize, Material panelMaterial, Material postMaterial, out GameObject outPanel)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(position, Quaternion.LookRotation(facing));

            // 柱は板の下端までにして、板の面から突き出ないようにする
            float panelBottom = SensorPanelHeight - panelSize.y * 0.5f;
            CreatePrimitive(PrimitiveType.Cylinder, "Post", root.transform,
                new Vector3(0.0f, panelBottom * 0.5f, 0.0f), new Vector3(0.1f, panelBottom * 0.5f, 0.1f), postMaterial, true);
            outPanel = CreatePrimitive(PrimitiveType.Cube, "Panel", root.transform,
                new Vector3(0.0f, SensorPanelHeight, 0.0f), new Vector3(panelSize.x, panelSize.y, 0.1f), panelMaterial, true);

            return root.AddComponent<WaveReceiver>();
        }

        /// @brief どの種類の波でも、当てている間（と、その後しばらく）だけ起動する受け手にする
        /// @param decayPerSecond 当てるのをやめてから止まるまでの時間を決める（1 ÷ これ 秒で止まる）
        private static void ConfigureSustainReceiver(WaveReceiver receiver, float decayPerSecond)
        {
            Configure(receiver, serialized =>
            {
                serialized.FindProperty("_canAcceptAnyType").boolValue = true;
                serialized.FindProperty("_mode").enumValueIndex = (int)WaveActivationMode.Sustain;
                serialized.FindProperty("_requiredEnergy").floatValue = 1.0f;
                serialized.FindProperty("_decayPerSecond").floatValue = decayPerSecond;
            });
        }

        /// @brief 受け手が起動している間だけせり上がる塔を作る。物陰の受け手が起動したことを、遠くから見せる
        private static void CreateBeacon(string name, Transform parent, Vector3 position, Material material, WaveReceiver receiver)
        {
            GameObject beacon = CreateBox(name, parent, position, new Vector3(0.6f, 2.8f, 0.6f), material);
            var mover = beacon.AddComponent<DemoGimmickMover>();
            Configure(mover, serialized =>
            {
                serialized.FindProperty("_activeOffset").vector3Value = new Vector3(0.0f, 3.0f, 0.0f);
                serialized.FindProperty("_speed").floatValue = 4.0f;
            });
            UnityEventTools.AddPersistentListener(receiver.ActivatedEvent, mover.Activate);
            UnityEventTools.AddPersistentListener(receiver.DeactivatedEvent, mover.Deactivate);
        }

        /// @brief スポーンに立ち、一人称で target を狙ったときの手の先（Muzzle）の位置
        /// target は目の高さにある（上下には向けない）として、左右の向きだけを target に合わせて求める
        private static Vector3 GetFirstPersonMuzzleWhenAiming(Vector3 target)
        {
            Vector3 toTarget = target - PitchPivotPosition;
            float yaw = Mathf.Atan2(toTarget.x, toTarget.z) * Mathf.Rad2Deg;
            return PitchPivotPosition + Quaternion.Euler(0.0f, yaw, 0.0f) * FirstPersonMuzzlePosition;
        }

        /// @brief from から to への水平な向き（高さの差を除いて正規化したもの）
        private static Vector3 GetFlatDirection(Vector3 from, Vector3 to)
        {
            Vector3 direction = to - from;
            direction.y = 0.0f;
            return direction.normalized;
        }

        //============================================================
        // アセット
        //============================================================

        private static WaveType CreateOrUpdateWaveType(string assetName, string displayName, Color color,
            float wavelength, float amplitude, float scrollSpeed, float lineWidth)
        {
            EnsureFolder(WaveTypeFolder);
            string path = $"{WaveTypeFolder}/{assetName}.asset";
            var waveType = AssetDatabase.LoadAssetAtPath<WaveType>(path);
            if (waveType == null)
            {
                waveType = ScriptableObject.CreateInstance<WaveType>();
                AssetDatabase.CreateAsset(waveType, path);
            }
            Configure(waveType, serialized =>
            {
                serialized.FindProperty("_displayName").stringValue = displayName;
                serialized.FindProperty("_color").colorValue = color;
                serialized.FindProperty("_wavelength").floatValue = wavelength;
                serialized.FindProperty("_amplitude").floatValue = amplitude;
                serialized.FindProperty("_scrollSpeed").floatValue = scrollSpeed;
                serialized.FindProperty("_lineWidth").floatValue = lineWidth;
            });
            EditorUtility.SetDirty(waveType);
            return waveType;
        }

        private static Material CreateOrUpdateMaterial(string assetName, Color color)
        {
            EnsureFolder(MaterialFolder);
            string path = $"{MaterialFolder}/{assetName}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                material = new Material(shader != null ? shader : Shader.Find("Standard"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor(material.HasProperty("_BaseColor") ? "_BaseColor" : "_Color", color);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// @brief マテリアルのアセットを読み込む。なければ作って色を付け、保存する（すでにあるものの色は変えない）
        private static Material LoadOrCreateMaterial(string assetName, Color color)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialFolder}/{assetName}.mat");
            if (material != null)
            {
                return material;
            }
            material = CreateOrUpdateMaterial(assetName, color);
            AssetDatabase.SaveAssetIfDirty(material);
            return material;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }
            int separator = path.LastIndexOf('/');
            string parent = path.Substring(0, separator);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(separator + 1));
        }

        private static void AddSceneToBuildSettings(string path)
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            foreach (EditorBuildSettingsScene buildScene in scenes)
            {
                if (buildScene.path == path)
                {
                    return;
                }
            }
            var newScenes = new EditorBuildSettingsScene[scenes.Length + 1];
            scenes.CopyTo(newScenes, 0);
            newScenes[scenes.Length] = new EditorBuildSettingsScene(path, true);
            EditorBuildSettings.scenes = newScenes;
        }

        //============================================================
        // GameObject を作る補助
        //============================================================

        private static Transform CreateEmpty(string name, Transform parent, Vector3 localPosition)
        {
            var gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent, false);
            gameObject.transform.localPosition = localPosition;
            return gameObject.transform;
        }

        private static Camera CreateCamera(string name, Transform parent, Vector3 localPosition)
        {
            Transform cameraTransform = CreateEmpty(name, parent, localPosition);
            cameraTransform.gameObject.tag = "MainCamera";
            var camera = cameraTransform.gameObject.AddComponent<Camera>();
            camera.nearClipPlane = 0.05f;
            camera.fieldOfView = 70.0f;
            cameraTransform.gameObject.AddComponent<AudioListener>();
            return camera;
        }

        private static GameObject CreateBox(string name, Transform parent, Vector3 position, Vector3 size, Material material)
        {
            GameObject box = CreatePrimitive(PrimitiveType.Cube, name, parent, Vector3.zero, size, material, true);
            box.transform.position = position;
            return box;
        }

        private static GameObject CreatePrimitive(PrimitiveType type, string name, Transform parent,
            Vector3 localPosition, Vector3 localScale, Material material, bool hasCollider)
        {
            GameObject primitive = GameObject.CreatePrimitive(type);
            primitive.name = name;
            primitive.transform.SetParent(parent, false);
            primitive.transform.localPosition = localPosition;
            primitive.transform.localScale = localScale;
            if (material != null)
            {
                primitive.GetComponent<Renderer>().sharedMaterial = material;
            }
            if (!hasCollider)
            {
                Object.DestroyImmediate(primitive.GetComponent<Collider>());
            }
            return primitive;
        }

        //============================================================
        // SerializedObject の補助（private の [SerializeField] を設定する）
        //============================================================

        private static void Configure(Object target, Action<SerializedObject> configure)
        {
            var serialized = new SerializedObject(target);
            configure(serialized);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetObject(SerializedObject serialized, string propertyName, Object value)
        {
            serialized.FindProperty(propertyName).objectReferenceValue = value;
        }

        private static void SetObjects(SerializedObject serialized, string propertyName, params Object[] values)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; ++i)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
        }
    }
}
