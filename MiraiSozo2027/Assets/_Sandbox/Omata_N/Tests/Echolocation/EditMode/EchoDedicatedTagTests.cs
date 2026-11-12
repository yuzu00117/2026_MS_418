using System.Collections.Generic;
using Echo.Echolocation.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace Echo.Echolocation.Tests
{
    public sealed class EchoDedicatedTagTests
    {
        [Test]
        public void DefinitionIdentitySurvivesRenameAndDoesNotFallBackToUnityTag()
        {
            var definition = ScriptableObject.CreateInstance<EchoTagDefinition>();
            var other = ScriptableObject.CreateInstance<EchoTagDefinition>();
            var target = new GameObject("Dedicated classification");
            try
            {
                var tag = target.AddComponent<EchoTarget>(); tag.Definition = definition;
                var rule = definition.Settings;
                other.Settings.Fill.IsEnabled = false;
                var resolver = new EchoTagResolver();
                resolver.Update(new List<EchoTagColorRule> { rule });
                definition.name = "Renamed";
                Assert.That(resolver.TryGetRule(tag, out var resolved), Is.True);
                Assert.That(resolved, Is.SameAs(rule));
                tag.Definition = other;
                Assert.That(resolver.TryGetRule(tag, out _), Is.False);
                tag.Definition = definition; tag.enabled = false;
                Assert.That(resolver.TryGetRule(tag, out _), Is.False);
            }
            finally { Object.DestroyImmediate(target); Object.DestroyImmediate(definition); Object.DestroyImmediate(other); }
        }
        [Test]
        public void BatchAssignmentIsUndoableAndSkipsExistingTags()
        {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/_Sandbox/Omata_N/Scenes/Scene_EchoDemo.unity");
            var definition = ScriptableObject.CreateInstance<EchoTagDefinition>();
            var other = ScriptableObject.CreateInstance<EchoTagDefinition>();
            try
            {

                var target = new GameObject("Batch test");
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(target, scene);
                EchoTagBatchWindow.Apply(new[] { target }, definition, null, false);
                Assert.That(target.GetComponent<EchoTarget>().Definition, Is.SameAs(definition));
                EchoTagBatchWindow.Apply(new[] { target }, other, null, false);
                Assert.That(target.GetComponent<EchoTarget>().Definition, Is.SameAs(definition));
                Undo.FlushUndoRecordObjects();
                Undo.PerformUndo();
                Assert.That(target.GetComponent<EchoTarget>(), Is.Null);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
                Object.DestroyImmediate(definition); Object.DestroyImmediate(other);
            }
        }
        [Test]
        public void BakingPreservesAreaAndBoundsEachPatchEdge()
        {
            var target = GameObject.CreatePrimitive(PrimitiveType.Quad);
            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            EchoSurfaceBinding[] bindings = null;
            try
            {
                target.GetComponent<Renderer>().sharedMaterial = material;
                bindings = EchoSurfaceBaker.Bake(target.AddComponent<EchoTarget>(), .6f);
                Assert.That(EchoSurfaceBuildValidator.IsCurrent(bindings[0]), Is.True);
                target.transform.localScale = Vector3.one * 2;
                Assert.That(EchoSurfaceBuildValidator.IsCurrent(bindings[0]), Is.False);
                target.transform.localScale = Vector3.one;
                float area = 0;
                foreach (var patch in bindings[0].Data.Patches)
                {
                    area += patch.Area;
                    var vertices = patch.Mesh.vertices;
                    for (int i = 0; i < 3; i++)
                        Assert.That(Vector3.Distance(vertices[i], vertices[(i + 1) % 3]), Is.LessThanOrEqualTo(.60001f));
                    Assert.That(patch.Mesh.uv.Length, Is.EqualTo(3));
                    Assert.That(patch.Mesh.normals.Length, Is.EqualTo(3));
                    Assert.That(patch.Mesh.uv3.Length, Is.EqualTo(3));
                }
                Assert.That(area, Is.EqualTo(1).Within(.0001f));
            }
            finally
            {
                if (bindings != null) foreach (var binding in bindings) AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(binding.Data));
                Object.DestroyImmediate(target); Object.DestroyImmediate(material);
            }
        }
    }
}
