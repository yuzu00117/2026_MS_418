using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MS.Toon.Editor
{
    /// <summary>
    /// MS/Toon のマテリアルインスペクタ。各プロパティに日本語のツールチップを付ける。
    /// 項目が多く名前だけでは効果が分かりにくいため、カーソルを合わせると意図が読めるようにする。
    /// 表示名・見出し・Toggle/Range などの見た目はシェーダー側の定義に任せ、GUIContent の説明文だけを足す。
    /// </summary>
    public class MSToonShaderGUI : ShaderGUI
    {
        // プロパティ名 -> 説明。キーは MS_Toon.shader の Properties 名と一致させること
        private static readonly Dictionary<string, string> Tooltips = new Dictionary<string, string>
        {
            // Base
            { "_BaseMap", "ベースとなる色テクスチャ。物体の地の色や模様を決める。" },
            { "_BaseColor", "ベースマップに掛ける色。白で元のテクスチャそのまま、暗くすると全体が暗くなる。" },

            // Normal
            { "_UseNormalMap", "法線マップを使う。細かい凹凸の陰影を出せるが、Lite がオンの間は無効になる。" },
            { "_BumpMap", "表面の凹凸を表す法線マップ。陰影の境目に細かい凹凸が反映される。" },
            { "_BumpScale", "法線マップの強さ。上げるほど凹凸がはっきりし、0 で凹凸なしになる。" },

            // Emission
            { "_UseEmission", "自己発光を使う。ライトに関係なく光って見せたい部分に使う。" },
            { "_EmissionMap", "発光する場所を決めるテクスチャ。白い部分が発光色で光る。" },
            { "_EmissionColor", "発光の色と強さ(HDR)。明るくするほど強く光り、黒だと発光しない。" },

            // Toon Ramp
            { "_RampMap", "光の当たり具合を色に変換する 1D ランプ。左が暗部、右が明部で、トゥーン調の階調を決める。「Use Global Style」オン時はグローバルのランプが優先される。" },
            { "_Smoothness", "明暗の境目のぼかし具合。0 でくっきりしたトゥーン、上げるほど境目が柔らかくなる。" },
            { "_ShadowColor", "影の部分に乗せる色。影が真っ黒にならず、色味のある影にするために使う。" },
            { "_ShadowStrength", "影の色を乗せる強さ。0 で影色なし、1 で影色を最大限反映する。" },

            // Rim
            { "_UseRim", "輪郭に沿った縁取りの光(リムライト)を使う。Lite がオンの間は無効になる。" },
            { "_RimColor", "リムライトの色。" },
            { "_RimPower", "リムの幅の絞り込み。上げるほど縁の細い範囲にだけ光が出る。" },
            { "_RimIntensity", "リムの明るさ。上げるほど縁の光が強く出る。" },

            // Specular
            { "_UseSpecular", "ハイライト(光の反射)を使う。Lite がオンの間は無効になる。" },
            { "_SpecularSize", "ハイライトの大きさ。上げるほどハイライトの範囲が広がる。" },
            { "_SpecularIntensity", "ハイライトの明るさ。上げるほど反射が強く出る。" },

            // Outline
            { "_OutlineColor", "輪郭線の色。" },
            { "_OutlineWidth", "輪郭線の太さ(画面高に対する比率)。距離に関係なく画面上で一定の太さになり、0 で輪郭線を描かない。" },
            { "_OutlineZPull", "地面などに輪郭線が隠れるのを防ぐため、輪郭線をカメラ側に引き寄せる倍率(輪郭幅に比例)。上げるほど隠れにくいが、他の物体に食い込んで見えることがある。負の値で逆向きになる。" },
            { "_OutlineZPullMin", "カメラ側への引き寄せ量の下限(ワールド長)。近距離で引き寄せが小さすぎて地面に埋もれるのを防ぐ。" },
            { "_OutlineZPullMax", "カメラ側への引き寄せ量の上限(ワールド長)。遠距離で引き寄せすぎて輪郭線が手前の物体を突き抜けるのを防ぐ。" },
            { "_OutlineUseBakedNormal", "ToonSmoothNormalBaker で UV3 に焼いたスムース法線を使う。角で輪郭線が割れにくくなる。未焼き込みのメッシュではオフにする。" },

            // Gradient
            { "_GradientStrength", "オブジェクトの下側を暗くする強さ。0 でグラデーションなし、上げるほど足元が暗くなる。" },
            { "_GradientHeight", "グラデーションをかける高さ(ワールド長)。ピボットを基準に、この高さまで下から徐々に明るくなる。" },
            { "_GradientOffset", "グラデーションの開始位置のずらし量。上げるほど暗くなる範囲が狭くなる。" },

            // Style and Quality
            { "_UseGlobalStyle", "プロジェクト共通のスタイル設定(ToonStyleProfile)を使う。オフにするとこのマテリアルの個別値だけで描画する。" },
            { "_Lite", "モバイル向けの軽量モード。法線マップ・リム・スペキュラ・追加ライトを無効にして負荷を下げる。" },
            { "_Cull", "ポリゴンのどちら側の面を描かないか。Back で裏面を省略(通常)、Off で両面描画、Front で表面を省略。" },
        };

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            foreach (MaterialProperty property in properties)
            {
                DrawProperty(materialEditor, property);
            }

            DrawStandardFooter(materialEditor);
        }

        // HideInInspector を尊重しないと内部用プロパティまで表示されてしまうため、ここで弾く
        private static bool IsHidden(MaterialProperty property)
        {
            return (property.propertyFlags & UnityEngine.Rendering.ShaderPropertyFlags.HideInInspector) != 0;
        }

        private static GUIContent CreateLabel(MaterialProperty property)
        {
            // 辞書に無い項目は説明なし(従来どおりの表示)
            Tooltips.TryGetValue(property.name, out string tooltip);
            return new GUIContent(property.displayName, tooltip);
        }

        private static void DrawProperty(MaterialEditor materialEditor, MaterialProperty property)
        {
            if (IsHidden(property))
            {
                return;
            }

            materialEditor.ShaderProperty(property, CreateLabel(property));
        }

        // カスタム GUI にすると標準の下部欄が消えるので、同じものを明示的に描く
        private static void DrawStandardFooter(MaterialEditor materialEditor)
        {
            materialEditor.RenderQueueField();
            materialEditor.EnableInstancingField();
            materialEditor.DoubleSidedGIField();
        }
    }
}
