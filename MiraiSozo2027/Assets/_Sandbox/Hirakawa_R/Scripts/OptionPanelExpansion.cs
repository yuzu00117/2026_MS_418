using UnityEngine;

namespace MiraiSozo.Options
{
    /// <summary>
    /// メニューと説明ウィンドウの開閉で使う、機械が展開するような動き。
    /// 中央から細いラインが横へ伸び、一瞬止まってから上下へ開く（逆に進めると閉じる）。
    /// </summary>
    public static class OptionPanelExpansion
    {
        // 段階の割合・行き過ぎなどはOptionMenuThemeで調整する。
        // 横へ伸びる段階のこの割合までに不透明度を上げきる。
        private const float FadeInShare = 0.3f;
        // 上下へ開く段階のこの割合で、光のラインを消しきる。
        private const float LineFadeShare = 0.5f;
        // 設定で段階の割合を大きくしすぎても、上下へ開く段階がなくならないよう残す最小の割合。
        private const float MinimumOpenShare = 0.01f;
        // 急に止まる動きの鋭さ（2のべき乗で減衰させる）。
        private const float ExpoBase = 2f;
        private const float ExpoSteepness = 10f;

        public static float OpenDuration => OptionMenuTheme.Current.OpenDuration;
        public static float CloseDuration => OptionMenuTheme.Current.CloseDuration;

        /// <summary>
        /// progressが0で中央の点、1で元の大きさ。中心を固定したまま拡縮する。
        /// 戻り値は光のラインの濃さ（ラインの段階で1、上下に開くにつれて0）。
        /// </summary>
        public static float Apply(RectTransform panel, Vector3 baseScale, Vector3 basePosition, CanvasGroup group, float baseAlpha, float progress)
        {
            float height = panel.rect.height * Mathf.Abs(baseScale.y);
            var scale = GetScale(progress, height, out float opacity, out float line);
            panel.localScale = Vector3.Scale(baseScale, new Vector3(scale.x, scale.y, 1f));
            var center = Vector2.Scale(panel.rect.center, baseScale);
            panel.localPosition = basePosition + (Vector3)Vector2.Scale(center, Vector2.one - scale);
            if (group != null) group.alpha = baseAlpha * opacity;
            return line;
        }

        /// <summary>
        /// 元の大きさに対する横と縦の倍率。heightは元の高さ（Canvas上の長さ）で、ラインの段階の厚みを決める。
        /// opacityはパネルの不透明度、lineは光のラインの濃さ。
        /// </summary>
        public static Vector2 GetScale(float progress, float height, out float opacity, out float line)
        {
            var theme = OptionMenuTheme.Current;
            float widthShare = theme.WidthShare;
            float holdShare = Mathf.Min(theme.HoldShare, 1f - MinimumOpenShare - widthShare);
            float widthProgress = Mathf.Clamp01(progress / widthShare);
            float heightProgress = Mathf.Clamp01((progress - widthShare - holdShare) / (1f - widthShare - holdShare));
            float lineScale = Mathf.Min(1f, theme.LineThickness / Mathf.Max(1f, height));
            opacity = Mathf.SmoothStep(0f, 1f, widthProgress / FadeInShare);
            line = opacity * (1f - Mathf.SmoothStep(0f, 1f, heightProgress / LineFadeShare));
            return new Vector2(Mathf.Lerp(theme.StartWidth, 1f, GetExpoEaseOut(widthProgress)),
                Mathf.LerpUnclamped(lineScale, 1f, GetBackEaseOut(heightProgress, theme.Overshoot)));
        }

        // 出だしが非常に速く、急に止まる動き。
        private static float GetExpoEaseOut(float progress) => progress >= 1f ? 1f : 1f - Mathf.Pow(ExpoBase, -ExpoSteepness * progress);

        // 終点を少しだけ越えてから戻る動き。
        private static float GetBackEaseOut(float progress, float overshoot)
        {
            float t = progress - 1f;
            return 1f + (overshoot + 1f) * t * t * t + overshoot * t * t;
        }
    }
}
