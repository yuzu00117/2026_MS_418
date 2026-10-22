// 旧APIの互換入口。描画設定の選択画面へ案内する。
namespace Echo.Echolocation.Samples.Editor
{
    public static class EchoDemoSetup
    {
        public static void InitializeImportedProject() => InstallFeatures();
        public static void InstallFeatures()
            => UnityEditor.EditorApplication.ExecuteMenuItem("Tools/エコーロケーション/01 描画機能を準備");
    }
}
