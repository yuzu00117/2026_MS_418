using UnityEngine;
using UnityEngine.UI;

namespace MiraiSozo.Options
{
    /// <summary>柔らかいSF調の装飾で使う、角丸・円・ぼかし線の頂点を追加する。</summary>
    public static class OptionSoftShapes
    {
        private const int CornerSegments = 8;
        private const int CircleSegments = 24;
        public static Color Accent => OptionMenuLayout.Accent;
        public static Color Light => OptionMenuLayout.Background;

        public static Color GetColorWithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        /// <summary>角丸四角形の外周に沿った帯。offsetは外周からの距離（内側は負）。</summary>
        public static void AddRoundedRing(VertexHelper vertices, Rect rect, float radius,
            float innerOffset, float outerOffset, Color innerColor, Color outerColor)
        {
            int start = vertices.currentVertCount;
            int count = GetPerimeterCount();
            for (int i = 0; i < count; i++)
            {
                vertices.AddVert(GetPerimeterPoint(rect, radius, i, innerOffset), innerColor, Vector2.zero);
                vertices.AddVert(GetPerimeterPoint(rect, radius, i, outerOffset), outerColor, Vector2.zero);
            }
            for (int i = 0; i < count; i++)
            {
                int current = start + i * 2, next = start + (i + 1) % count * 2;
                vertices.AddTriangle(current, next, current + 1);
                vertices.AddTriangle(next, next + 1, current + 1);
            }
        }

        public static void AddRoundedFill(VertexHelper vertices, Rect rect, float radius, Color color)
        {
            int center = vertices.currentVertCount;
            vertices.AddVert(rect.center, color, Vector2.zero);
            int count = GetPerimeterCount();
            for (int i = 0; i < count; i++) vertices.AddVert(GetPerimeterPoint(rect, radius, i, 0f), color, Vector2.zero);
            for (int i = 0; i < count; i++) vertices.AddTriangle(center, center + 1 + (i + 1) % count, center + 1 + i);
        }

        public static void AddCircle(VertexHelper vertices, Vector2 center, float radius, Color color)
        {
            int start = vertices.currentVertCount;
            vertices.AddVert(center, color, Vector2.zero);
            for (int i = 0; i < CircleSegments; i++)
            {
                float angle = i * Mathf.PI * 2f / CircleSegments;
                vertices.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, color, Vector2.zero);
            }
            for (int i = 0; i < CircleSegments; i++) vertices.AddTriangle(start, start + 1 + (i + 1) % CircleSegments, start + 1 + i);
        }

        /// <summary>中央が最も濃く、両端へ向けて消える横線。</summary>
        public static void AddFadingLine(VertexHelper vertices, float left, float right, float y, float thickness, Color color)
        {
            var clear = GetColorWithAlpha(color, 0f);
            float middle = (left + right) * 0.5f, half = thickness * 0.5f;
            AddGradientQuad(vertices, left, middle, y - half, y + half, clear, color);
            AddGradientQuad(vertices, middle, right, y - half, y + half, color, clear);
        }

        private static void AddGradientQuad(VertexHelper vertices, float left, float right, float bottom, float top, Color leftColor, Color rightColor)
        {
            int start = vertices.currentVertCount;
            vertices.AddVert(new Vector2(left, bottom), leftColor, Vector2.zero);
            vertices.AddVert(new Vector2(left, top), leftColor, Vector2.zero);
            vertices.AddVert(new Vector2(right, top), rightColor, Vector2.zero);
            vertices.AddVert(new Vector2(right, bottom), rightColor, Vector2.zero);
            vertices.AddTriangle(start, start + 1, start + 2);
            vertices.AddTriangle(start, start + 2, start + 3);
        }

        private static int GetPerimeterCount() => (CornerSegments + 1) * 4;

        // 右上の角から反時計回りに、四隅の円弧上の点を返す。
        private static Vector2 GetPerimeterPoint(Rect rect, float radius, int index, float offset)
        {
            float r = Mathf.Max(0f, Mathf.Min(radius, rect.width * 0.5f, rect.height * 0.5f));
            int corner = index / (CornerSegments + 1);
            float angle = (corner + (float)(index % (CornerSegments + 1)) / CornerSegments) * Mathf.PI * 0.5f;
            var center = new Vector2(corner == 0 || corner == 3 ? rect.xMax - r : rect.xMin + r,
                corner <= 1 ? rect.yMax - r : rect.yMin + r);
            return center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (r + offset);
        }
    }
}
