using UnityEditor;
namespace Echo.Echolocation.Editor
{
    [CustomEditor(typeof(EchoOccluder)), CanEditMultipleObjects]
    public sealed class EchoOccluderEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI() => EditorGUILayout.HelpBox("旧設定です。遮断の有無は専用タグのアセットで指定してください。専用タグのない物体は遮断対象です。", MessageType.Info);
    }
}
