using UnityEngine;
using UnityEngine.UI;

namespace MiraiSozo.Options
{
    /// <summary>パネルやボタンの角を丸くする9スライス用スプライトを実行時に生成する。</summary>
    public static class OptionSoftSprite
    {
        private const int CornerPixels = 32;
        private const int TextureSize = CornerPixels * 2 + 2;
        private const float PixelsPerUnit = 100f;
        // 置き換えてよいUnity標準のスプライト名（未設定時と、UIの初期素材）。
        private const string DefaultSpriteName = "UISprite";
        private const string DefaultBackgroundName = "Background";
        private static Sprite _rounded;

        public static Sprite Rounded => _rounded != null ? _rounded : (_rounded = CreateRounded());

        /// <summary>未設定またはUnity標準のスプライトだけを角丸に置き換え、独自の画像はそのまま残す。</summary>
        public static void Apply(Image image, float cornerRadius)
        {
            if (image == null || !IsReplaceable(image.sprite)) return;
            image.sprite = Rounded;
            image.type = Image.Type.Sliced;
            image.fillCenter = true;
            // スライス幅がCanvas上で指定の半径になるよう、Canvasの基準ピクセル数を加味して倍率を決める。
            image.pixelsPerUnitMultiplier = 1f;
            image.pixelsPerUnitMultiplier = CornerPixels / (cornerRadius * image.pixelsPerUnit);
        }

        private static bool IsReplaceable(Sprite sprite)
            => sprite == null || sprite == _rounded || sprite.name == DefaultSpriteName || sprite.name == DefaultBackgroundName;

        private static Sprite CreateRounded()
        {
            var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
            {
                name = "Option Soft Rounded",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave
            };
            texture.SetPixels32(CreatePixels());
            texture.Apply(false, true);
            var border = Vector4.one * CornerPixels;
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, TextureSize, TextureSize), new Vector2(0.5f, 0.5f),
                PixelsPerUnit, 0, SpriteMeshType.FullRect, border);
            sprite.name = texture.name;
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }

        // 四隅を半径CornerPixelsの円で切り抜き、境目は1ピクセル分なめらかにする。
        private static Color32[] CreatePixels()
        {
            var pixels = new Color32[TextureSize * TextureSize];
            for (int y = 0; y < TextureSize; y++)
            {
                for (int x = 0; x < TextureSize; x++)
                {
                    var point = new Vector2(x + 0.5f, y + 0.5f);
                    var center = new Vector2(Mathf.Clamp(point.x, CornerPixels, TextureSize - CornerPixels),
                        Mathf.Clamp(point.y, CornerPixels, TextureSize - CornerPixels));
                    float alpha = Mathf.Clamp01(CornerPixels - Vector2.Distance(point, center) + 0.5f);
                    pixels[y * TextureSize + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            return pixels;
        }
    }
}
