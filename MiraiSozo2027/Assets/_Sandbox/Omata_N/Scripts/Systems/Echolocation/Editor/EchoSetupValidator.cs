// 構成ミスを対象名と対処方法付きで表示する。対応外を無言で対応済みにしない。
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Echo.Echolocation.Editor
{
    public static class EchoSetupValidator
    {
        /// <summary>指定Controllerの構成とタグ対象を検証する。</summary>
        public static void Validate(EchoController c)
        {
            int errors = 0;
            void Error(string message)
            {
                errors++;
                Debug.LogError("エコー：" + message, c);
            }

            if (!c.TargetCamera)
                Error("描画カメラを指定してください。");
            foreach (var other in Object.FindObjectsByType<EchoController>())
                if (other != c && other.isActiveAndEnabled && c.TargetCamera && other.TargetCamera == c.TargetCamera)
                    Error("同じCameraを指定するControllerが重複しています。");
            if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset))
                Error("Universal Render Pipelineが必要です。");
            if (c.TargetCamera)
            {
                var renderer = c.TargetCamera.GetUniversalAdditionalCameraData().scriptableRenderer;
                var field = typeof(ScriptableRenderer).GetProperty("rendererFeatures", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                bool isFound = false;
                if (field?.GetValue(renderer)is List<ScriptableRendererFeature> features)
                    foreach (var feature in features)
                        if (feature is EchoUrpRendererFeature && feature.isActive)
                            isFound = true;
                if (!isFound)
                    Error("使用中のRenderer DataにEchoUrpRendererFeatureを追加してください。");
            }

            var knownTags = new HashSet<string>(InternalEditorUtility.tags);
            var configuredTags = new HashSet<string>();
            foreach (var row in c.Settings.TagColorRules)
                if (row != null && row.IsEnabled)
                {
                    if (!knownTags.Contains(row.TagName ?? ""))
                        Error($"タグ「{row.TagName}」が存在しません。");
                    if (!configuredTags.Add(row.TagName ?? ""))
                        Error($"タグ「{row.TagName}」が重複しています。");
                }

            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include))
            {
                var owner = r.GetComponentInParent<EchoTarget>(true);
                string tag = owner ? owner.tag : r.tag;
                if (!configuredTags.Contains(tag))
                    continue;
                if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer))
                    Debug.LogWarning($"エコー：{r.name} ({r.GetType().Name}) は専用描画アダプターが必要です。", r);
                if (r is SkinnedMeshRenderer s && !s.updateWhenOffscreen)
                    Debug.LogWarning($"エコー：{r.name} の壁裏アニメーションにはUpdate When Offscreenと適切なboundsを確認してください。", r);
            }

            if (errors == 0)
                Debug.Log("エコー：基本セットアップの検証が完了しました。", c);
        }
    }
}
