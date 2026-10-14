// 仮プレイヤーの設定を日本語で表示する。
using UnityEditor;
using UnityEngine;

namespace Echo.Echolocation.Samples.Editor
{
    [CustomEditor(typeof(EchoDemoPlayer)), CanEditMultipleObjects]
    public sealed class EchoDemoPlayerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            void Field(string path, string label) => EditorGUILayout.PropertyField(serializedObject.FindProperty(path), new GUIContent(label), true);
            EditorGUILayout.HelpBox("WASD／左スティックで移動、マウス／右スティックで視点操作。視点切替の初期値はZ。Escでカーソル解放、左クリックで再固定します。", MessageType.Info);
            Field("_lookPivot", "目の位置・視点軸");
            Field("_viewCamera", "共用カメラ");
            Field("_bodyRenderers", "仮プレイヤーの表示モデル");
            Field("_moveSpeed", "移動速度（m/秒）");
            Field("_gravity", "重力（m/秒²）");
            Field("_mouseSensitivity", "マウス感度（度/ピクセル）");
            Field("_gamepadLookSpeed", "スティック感度（度/秒）");
            Field("_pitchLimit", "上下の角度制限（度）");
            Field("_thirdPersonDistance", "三人称の距離（m）");
            Field("_thirdPersonHeight", "三人称の追加高さ（m）");
            Field("_cameraCollisionRadius", "カメラ衝突半径（m）");
            Field("_cameraCollisionLayers", "カメラの障害物レイヤー");
            Field("_isThirdPersonOnStart", "開始時に三人称");
            Field("_isCursorLockedOnStart", "開始時にカーソル固定");
            Field("_viewTogglePath", "視点切り替え操作");
            serializedObject.ApplyModifiedProperties();
            using (new EditorGUI.DisabledScope(!Application.isPlaying))
                if (GUILayout.Button("視点を切り替え"))
                {
                    var p = (EchoDemoPlayer)target;
                    p.SetThirdPerson(!p.IsThirdPerson);
                }
        }
    }
}
