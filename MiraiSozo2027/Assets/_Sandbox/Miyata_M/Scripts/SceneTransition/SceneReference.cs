using System;
using System.IO;
using UnityEngine;

namespace SceneFlow
{
    /// <summary>
    /// Inspector でシーンアセットをドラッグして指定するための参照型。
    /// 実行時はシーンのパスと名前だけを使い、SceneAsset はエディタ上にのみ存在する。
    /// </summary>
    [Serializable]
    public class SceneReference : ISerializationCallbackReceiver
    {
#if UNITY_EDITOR
        // エディタ専用。ビルドには含まれない
        [SerializeField] private UnityEditor.SceneAsset _sceneAsset;
#endif
        [SerializeField] private string _scenePath;
        [SerializeField] private string _sceneName;

        /// <summary>Build Settings 上のパス(例: Assets/Scenes/Scene_Title.unity)。</summary>
        public string ScenePath => _scenePath;

        public string SceneName => _sceneName;

        /// <summary>シーンが指定されているか。</summary>
        public bool IsAssigned => !string.IsNullOrEmpty(_scenePath);

        public void OnBeforeSerialize()
        {
#if UNITY_EDITOR
            // アセットの移動・リネームに追従するため、保存のたびにパスと名前を更新する
            if (_sceneAsset != null)
            {
                _scenePath = UnityEditor.AssetDatabase.GetAssetPath(_sceneAsset);
                _sceneName = Path.GetFileNameWithoutExtension(_scenePath);
            }
            else
            {
                _scenePath = string.Empty;
                _sceneName = string.Empty;
            }
#endif
        }

        public void OnAfterDeserialize()
        {
        }
    }
}
