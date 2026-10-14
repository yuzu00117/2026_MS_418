// 子Rendererの意味上の所有者。タグを親でまとめたい場合や生成Prefabに付ける。
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Echo.Echolocation
{
    /// <summary>このGameObjectのタグとレイヤーで子Rendererを分類する。</summary>
    [DisallowMultipleComponent]
    public sealed class EchoTarget : MonoBehaviour
    {
        [SerializeField, Tooltip("有効にすると一覧に指定した子Rendererだけを収集します。")]
        [UnityEngine.Serialization.FormerlySerializedAs("useManualRenderers")]
        private bool _isManualRendererSelectionEnabled;
        [SerializeField, Tooltip("自身または子のRenderer。子EchoTargetの所有物は指定できません。")]
        [UnityEngine.Serialization.FormerlySerializedAs("renderers")]
        private List<Renderer> _renderers = new List<Renderer>();
        /// <summary>登録・構造変更の通知。</summary>
        public static event Action Changed;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Changed = null;
        void OnEnable() => RefreshRenderers();
        void OnDisable() => RefreshRenderers();
        void OnDestroy() => RefreshRenderers();
        /// <summary>子の変更後に登録を更新する。</summary>
        public void RefreshRenderers() => Changed?.Invoke();
        /// <summary>最も近い所有者と手動一覧の双方を満たすRendererだけを受け付ける。</summary>
        public bool Owns(Renderer r) => isActiveAndEnabled && r.GetComponentInParent<EchoTarget>(true) == this && (!_isManualRendererSelectionEnabled || _renderers.Contains(r));
    }
}
