// よく使う操作は日本語メニュー、全操作はInput System標準ピッカーで選択する。
using UnityEditor;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem.Editor;

namespace Echo.Echolocation.Editor
{
    [CustomPropertyDrawer(typeof(EchoInputBinding))]
    public sealed class EchoInputBindingDrawer : PropertyDrawer
    {
        private readonly Dictionary<string, InputControlPathEditor> _pickers = new Dictionary<string, InputControlPathEditor>();
        public override float GetPropertyHeight(SerializedProperty p, GUIContent l) => 7 * (EditorGUIUtility.singleLineHeight + 3) + 6;
        public override void OnGUI(Rect rect, SerializedProperty p, GUIContent label)
        {
            EditorGUI.BeginProperty(rect, label, p);
            float h = EditorGUIUtility.singleLineHeight;
            Rect line = new Rect(rect.x, rect.y, rect.width, h);
            EditorGUI.PropertyField(line, p.FindPropertyRelative("_isEnabled"), new GUIContent("有効"));
            line.y += h + 3;
            var path = p.FindPropertyRelative("_controlPath");
            if (GUI.Button(line, "機器・操作を選択（日本語）"))
                ShowMenu(p.Copy());
            line.y += h + 3;
            // 標準ピッカーは未接続デバイスのレイアウトと詳細なコントロールにも対応する。
            string pickerKey = p.serializedObject.targetObject.GetEntityId() + p.propertyPath;
            if (!_pickers.TryGetValue(pickerKey, out var picker))
            {
                picker = new InputControlPathEditor(path.Copy(), new InputControlPickerState(), () => path.serializedObject.ApplyModifiedProperties(), new GUIContent("全操作 / パス"));
                picker.SetExpectedControlLayout("Axis");
                _pickers.Add(pickerKey, picker);
            }

            picker.OnGUI(line, property: path);
            line.y += h + 3;
            var kind = p.FindPropertyRelative("_inputKind");
            EditorGUI.BeginChangeCheck();
            int k = EditorGUI.Popup(line, "入力の種類", kind.enumValueIndex, new[] { "ボタン", "通常軸", "マウス変位・ホイール" });
            if (EditorGUI.EndChangeCheck())
                kind.enumValueIndex = k;
            line.y += h + 3;
            EditorGUI.PropertyField(line, p.FindPropertyRelative("_isNegative"), new GUIContent("軸の負方向"));
            line.y += h + 3;
            EditorGUI.PropertyField(line, p.FindPropertyRelative("_pressThreshold"), new GUIContent("押下の閾値"));
            line.y += h + 3;
            EditorGUI.PropertyField(line, p.FindPropertyRelative("_releaseThreshold"), new GUIContent("再受付の閾値"));
            EditorGUI.EndProperty();
        }

        static void ShowMenu(SerializedProperty p)
        {
            var menu = new GenericMenu();
            void Add(string name, string path, int kind = 0, bool isNegative = false, float press = .5f)
            {
                menu.AddItem(new GUIContent(name), false, () =>
                {
                    p.serializedObject.Update();
                    p.FindPropertyRelative("_controlPath").stringValue = path;
                    p.FindPropertyRelative("_inputKind").enumValueIndex = kind;
                    p.FindPropertyRelative("_isNegative").boolValue = isNegative;
                    p.FindPropertyRelative("_pressThreshold").floatValue = press;
                    p.FindPropertyRelative("_releaseThreshold").floatValue = kind == 2 ? 0 : .25f;
                    p.serializedObject.ApplyModifiedProperties();
                });
            }

            for (char c = 'a'; c <= 'z'; c++)
                Add("キーボード/" + char.ToUpper(c), "<Keyboard>/" + c);
            for (int i = 0; i <= 9; i++)
                Add("キーボード/" + i, "<Keyboard>/digit" + i);
            for (int i = 1; i <= 12; i++)
                Add("キーボード/F" + i, "<Keyboard>/f" + i);
            string[] keys =
            {
                "space",
                "enter",
                "escape",
                "tab",
                "leftShift",
                "rightShift",
                "leftCtrl",
                "rightCtrl",
                "leftAlt",
                "rightAlt",
                "upArrow",
                "downArrow",
                "leftArrow",
                "rightArrow"
            };
            foreach (var key in keys)
                Add("キーボード/" + key, "<Keyboard>/" + key);
            string[] mouseBindings =
            {
                "左ボタン",
                "右ボタン",
                "中央ボタン",
                "戻るボタン",
                "進むボタン"
            }, paths =
            {
                "leftButton",
                "rightButton",
                "middleButton",
                "backButton",
                "forwardButton"
            };
            for (int i = 0; i < mouseBindings.Length; i++)
                Add("マウス/" + mouseBindings[i], "<Mouse>/" + paths[i]);
            string[] names =
            {
                "下側（A / ×相当）",
                "右側（B / ○相当）",
                "左側（X / □相当）",
                "上側（Y / △相当）",
                "左肩",
                "右肩",
                "左トリガー",
                "右トリガー",
                "左スティック押込",
                "右スティック押込",
                "Start",
                "Select"
            };
            string[] gamepadBindings =
            {
                "buttonSouth",
                "buttonEast",
                "buttonWest",
                "buttonNorth",
                "leftShoulder",
                "rightShoulder",
                "leftTrigger",
                "rightTrigger",
                "leftStickPress",
                "rightStickPress",
                "start",
                "select"
            };
            for (int i = 0; i < gamepadBindings.Length; i++)
                Add("ゲームパッド/" + names[i], "<Gamepad>/" + gamepadBindings[i]);
            string[] directions =
            {
                "上",
                "下",
                "左",
                "右"
            }, dpaths =
            {
                "up",
                "down",
                "left",
                "right"
            };
            for (int i = 0; i < 4; i++)
            {
                Add("ゲームパッド/十字キー/" + directions[i], "<Gamepad>/dpad/" + dpaths[i]);
                Add("ゲームパッド/左スティック/" + directions[i], "<Gamepad>/leftStick/" + dpaths[i], 1);
                Add("ゲームパッド/右スティック/" + directions[i], "<Gamepad>/rightStick/" + dpaths[i], 1);
                bool isNegative = i == 1 || i == 2;
                string axis = i < 2 ? "y" : "x";
                Add("マウス/ホイール/" + directions[i], "<Mouse>/scroll/" + axis, 2, isNegative, 1);
                Add("マウス/移動/" + directions[i], "<Mouse>/delta/" + axis, 2, isNegative, 10);
            }

            menu.ShowAsContext();
        }
    }
}
