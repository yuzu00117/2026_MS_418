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
        public static void Validate(EchoController controller)
        {
            int errors = 0;
            void LogError(string message)
            {
                errors++;
                Debug.LogError("エコー：" + message, controller);
            }

            if (!controller.TargetCamera)
                LogError("描画カメラを指定してください。");
            foreach (var other in Object.FindObjectsByType<EchoController>())
                if (other != controller && other.isActiveAndEnabled && controller.TargetCamera && other.TargetCamera == controller.TargetCamera)
                    LogError("同じCameraを指定するControllerが重複しています。");
            if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset))
                LogError("Universal Render Pipelineが必要です。");
            if (controller.TargetCamera)
            {
                var renderer = controller.TargetCamera.GetUniversalAdditionalCameraData().scriptableRenderer;
                var field = typeof(ScriptableRenderer).GetProperty("rendererFeatures", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                bool isFound = false;
                if (field?.GetValue(renderer)is List<ScriptableRendererFeature> features)
                    foreach (var feature in features)
                        if (feature is EchoUrpRendererFeature && feature.isActive)
                            isFound = true;
                if (!isFound)
                    LogError("使用中のRenderer DataにEchoUrpRendererFeatureを追加してください。");
            }

            foreach (var sceneRenderer in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include))
            {
                var owner = sceneRenderer.GetComponentInParent<EchoTarget>(true);
                var definition = owner ? owner.Definition : null;
                if (!definition)
                    continue;
                if (!(sceneRenderer is MeshRenderer) && !(sceneRenderer is SkinnedMeshRenderer))
                    Debug.LogWarning($"エコー：{sceneRenderer.name} ({sceneRenderer.GetType().Name}) は専用描画アダプターが必要です。", sceneRenderer);
                if (sceneRenderer is SkinnedMeshRenderer skinned && !skinned.updateWhenOffscreen)
                    Debug.LogWarning($"エコー：{sceneRenderer.name} の壁裏アニメーションにはUpdate When Offscreenと適切なboundsを確認してください。", sceneRenderer);
            }

            if (errors == 0)
                Debug.Log("エコー：基本セットアップの検証が完了しました。", controller);
        }
    }
}
