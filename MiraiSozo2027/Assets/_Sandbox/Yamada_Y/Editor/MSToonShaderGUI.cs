using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
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
            { "_UseGlobalOutline", "プロジェクト共通の輪郭線設定(ToonStyleProfile)を使う。オフにするとこのマテリアルの個別値で描く。" },
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

        private const float ToggleOnThreshold = 0.5f;
        private const string UseGlobalOutlineName = "_UseGlobalOutline";
        private const string UseGlobalStyleName = "_UseGlobalStyle";
        private const string RampMapName = "_RampMap";
        private const string UndoName = "共通設定から個別設定へ切り替え";
        private const string NoticeUsingGlobal = "共通設定を使用中です(値を変えると個別設定に切り替わります)";
        private const string WarnNoApplier = "シーンに ToonStyleApplier が無いため、共通設定は反映されません(このマテリアルの値で描画されます)";
        private const string WarnNoProfile = "ToonStyleApplier に Profile が設定されていないため、共通設定は反映されません(このマテリアルの値で描画されます)";

        // 共通値で置換表示するグループ。トグルと、そのトグルがオンの間に置換されるプロパティ名を束ねる
        private sealed class GlobalGroup
        {
            public readonly string ToggleName;
            public readonly string[] MemberNames;

            public GlobalGroup(string toggleName, string[] memberNames)
            {
                ToggleName = toggleName;
                MemberNames = memberNames;
            }

            public bool Contains(string propertyName)
            {
                return System.Array.IndexOf(MemberNames, propertyName) >= 0;
            }

            // 通知はトグル自身かメンバーのうち最初に現れた行の手前に出す
            public bool Owns(string propertyName)
            {
                return propertyName == ToggleName || Contains(propertyName);
            }
        }

        private static readonly GlobalGroup[] Groups =
        {
            new GlobalGroup(UseGlobalOutlineName, new[]
            {
                "_OutlineColor", "_OutlineWidth", "_OutlineZPull", "_OutlineZPullMin", "_OutlineZPullMax",
            }),
            new GlobalGroup(UseGlobalStyleName, new[]
            {
                "_ShadowColor", "_ShadowStrength", "_RimColor", "_RimPower", "_RimIntensity", "_Smoothness", RampMapName,
            }),
        };

        // プロパティ名 -> Profile の値。置換表示とコピーで同じ対応を使うためテーブルにまとめる
        private static readonly Dictionary<string, System.Func<ToonStyleProfile, Color>> ProfileColors =
            new Dictionary<string, System.Func<ToonStyleProfile, Color>>
            {
                { "_ShadowColor", p => p.ShadowColor },
                { "_RimColor", p => p.RimColor },
                { "_OutlineColor", p => p.OutlineColor },
            };

        private static readonly Dictionary<string, System.Func<ToonStyleProfile, float>> ProfileFloats =
            new Dictionary<string, System.Func<ToonStyleProfile, float>>
            {
                { "_ShadowStrength", p => p.ShadowStrength },
                { "_RimPower", p => p.RimPower },
                { "_RimIntensity", p => p.RimIntensity },
                { "_Smoothness", p => p.Smoothness },
                { "_OutlineWidth", p => p.OutlineWidth },
                { "_OutlineZPull", p => p.OutlineZPull },
                { "_OutlineZPullMin", p => p.OutlineZPullMin },
                { "_OutlineZPullMax", p => p.OutlineZPullMax },
            };

        // Profile.Changed の購読は static で1つだけ持つ。インスタンスごとに登録するとインスペクタを閉じた後に残るため
        private static ToonStyleProfile s_subscribedProfile;

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            ToonStyleProfile profile = GetActiveProfile();
            SubscribeProfile(profile);

            // グループ見出しの通知は、各グループで最初に現れたプロパティの手前に1回だけ出す
            bool[] toggleOn = new bool[Groups.Length];
            bool[] noticeShown = new bool[Groups.Length];
            for (int i = 0; i < Groups.Length; i++)
            {
                toggleOn[i] = IsToggleOn(FindProperty(Groups[i].ToggleName, properties, false));
            }

            foreach (MaterialProperty property in properties)
            {
                if (IsHidden(property))
                {
                    continue;
                }

                string name = property.name;
                int groupIndex = FindGroupIndex(name);
                GlobalGroup group = groupIndex >= 0 ? Groups[groupIndex] : null;

                if (group != null && !noticeShown[groupIndex])
                {
                    noticeShown[groupIndex] = true;
                    DrawNotice(toggleOn[groupIndex], profile);
                }

                bool replace = profile != null && group != null && toggleOn[groupIndex] && group.Contains(name);
                // Ramp は Profile 側が未設定ならマテリアル自身の値を見せる
                if (replace && name == RampMapName && profile.Ramp == null)
                {
                    replace = false;
                }

                if (replace)
                {
                    DrawReplaced(materialEditor, properties, property, profile, group);
                }
                else
                {
                    DrawProperty(materialEditor, property);
                }
            }

            DrawStandardFooter(materialEditor);
        }

        private static int FindGroupIndex(string propertyName)
        {
            for (int i = 0; i < Groups.Length; i++)
            {
                if (Groups[i].Owns(propertyName))
                {
                    return i;
                }
            }
            return -1;
        }

        private static ToonStyleProfile GetActiveProfile()
        {
            ToonStyleApplier applier = ToonStyleApplier.Active;
            if (applier == null || !applier.isActiveAndEnabled)
            {
                return null;
            }
            return applier.Profile;
        }

        // 複数選択で値が割れているトグルは「共通使用中」と断定できないので、置換しない
        private static bool IsToggleOn(MaterialProperty toggle)
        {
            return toggle != null && !toggle.hasMixedValue && toggle.floatValue > ToggleOnThreshold;
        }

        private static void SubscribeProfile(ToonStyleProfile profile)
        {
            if (s_subscribedProfile == profile)
            {
                return;
            }
            if (s_subscribedProfile != null)
            {
                s_subscribedProfile.Changed -= OnProfileChanged;
            }
            s_subscribedProfile = profile;
            if (profile != null)
            {
                profile.Changed += OnProfileChanged;
            }
        }

        // Profile の値が変わったら、表示中の共通値を最新にするため全ビューを再描画する
        private static void OnProfileChanged()
        {
            InternalEditorUtility.RepaintAllViews();
        }

        private static void DrawNotice(bool toggleOn, ToonStyleProfile profile)
        {
            if (!toggleOn)
            {
                return;
            }
            ToonStyleApplier applier = ToonStyleApplier.Active;
            if (applier == null || !applier.isActiveAndEnabled)
            {
                EditorGUILayout.HelpBox(WarnNoApplier, MessageType.Warning);
            }
            else if (profile == null)
            {
                EditorGUILayout.HelpBox(WarnNoProfile, MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox(NoticeUsingGlobal, MessageType.Info);
            }
        }

        // MaterialProperty へ一時書き込みすると全ターゲットのアセットが変わるため、表示は自前のフィールドで描く。
        // 編集された時だけマテリアルへ確定する
        private static void DrawReplaced(MaterialEditor materialEditor, MaterialProperty[] properties, MaterialProperty property, ToonStyleProfile profile, GlobalGroup group)
        {
            GUIContent label = CreateLabel(property);
            Color newColor = Color.white;
            Texture newTexture = null;
            float newFloat = 0f;

            EditorGUI.BeginChangeCheck();
            switch (property.propertyType)
            {
                case UnityEngine.Rendering.ShaderPropertyType.Color:
                    newColor = EditorGUILayout.ColorField(label, GetProfileColor(property.name, profile));
                    break;
                case UnityEngine.Rendering.ShaderPropertyType.Texture:
                    newTexture = (Texture)EditorGUILayout.ObjectField(label, profile.Ramp, typeof(Texture), false);
                    break;
                case UnityEngine.Rendering.ShaderPropertyType.Range:
                    Vector2 limits = property.rangeLimits;
                    newFloat = EditorGUILayout.Slider(label, GetProfileFloat(property.name, profile), limits.x, limits.y);
                    break;
                default:
                    newFloat = EditorGUILayout.FloatField(label, GetProfileFloat(property.name, profile));
                    break;
            }
            if (!EditorGUI.EndChangeCheck())
            {
                return;
            }

            materialEditor.RegisterPropertyChangeUndo(UndoName);

            // (1) グループ全体に現在の共通値をコピーして見た目を保ち、(2) 編集値を上書きし、(3) 個別設定へ切り替える
            foreach (string groupName in group.MemberNames)
            {
                MaterialProperty target = FindProperty(groupName, properties, false);
                if (target != null)
                {
                    CopyProfileValue(target, profile);
                }
            }

            switch (property.propertyType)
            {
                case UnityEngine.Rendering.ShaderPropertyType.Color:
                    // アルファは使わない項目があるため、元のアルファを保つ
                    property.colorValue = new Color(newColor.r, newColor.g, newColor.b, property.colorValue.a);
                    break;
                case UnityEngine.Rendering.ShaderPropertyType.Texture:
                    property.textureValue = newTexture;
                    break;
                default:
                    property.floatValue = newFloat;
                    break;
            }

            MaterialProperty toggle = FindProperty(group.ToggleName, properties, false);
            if (toggle != null)
            {
                toggle.floatValue = 0f;
            }
        }

        private static void CopyProfileValue(MaterialProperty target, ToonStyleProfile profile)
        {
            switch (target.propertyType)
            {
                case UnityEngine.Rendering.ShaderPropertyType.Color:
                    Color c = GetProfileColor(target.name, profile);
                    target.colorValue = new Color(c.r, c.g, c.b, target.colorValue.a);
                    break;
                case UnityEngine.Rendering.ShaderPropertyType.Texture:
                    if (profile.Ramp != null)
                    {
                        target.textureValue = profile.Ramp;
                    }
                    break;
                default:
                    target.floatValue = GetProfileFloat(target.name, profile);
                    break;
            }
        }

        private static Color GetProfileColor(string name, ToonStyleProfile profile)
        {
            return ProfileColors[name](profile);
        }

        private static float GetProfileFloat(string name, ToonStyleProfile profile)
        {
            return ProfileFloats[name](profile);
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
