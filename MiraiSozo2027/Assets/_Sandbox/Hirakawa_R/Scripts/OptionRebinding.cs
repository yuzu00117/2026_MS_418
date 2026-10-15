using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;

namespace MiraiSozo.Options
{
    public static class OptionRebinding
    {
        public readonly struct Entry
        {
            public readonly InputAction Action;
            public readonly int Index;
            public readonly string Label;
            public Entry(InputAction action, int index, string label) { Action = action; Index = index; Label = label; }
        }

        public static List<Entry> Entries(InputActionMap map)
        {
            var entries = new List<Entry>();
            foreach (var action in map.actions)
            {
                for (int i = 0; i < action.bindings.Count; i++)
                {
                    var binding = action.bindings[i];
                    if (binding.isComposite || !IsKeyboardOrMouseButton(binding.path)) continue;
                    string part = binding.isPartOfComposite ? " / " + PartName(binding.name) : "";
                    entries.Add(new Entry(action, i, ActionName(action.name) + part));
                }
            }
            return entries;
        }

        public static bool TryApply(Entry entry, string path, out string message)
        {
            if (!IsKeyboardOrMouseButton(path) || string.Equals(path, "<Keyboard>/escape", StringComparison.OrdinalIgnoreCase))
            {
                message = "ESCはメニュー用です。別のキー・マウスボタンを選んでください。";
                return false;
            }
            string original = entry.Action.bindings[entry.Index].effectivePath;
            if (!string.Equals(original, path, StringComparison.OrdinalIgnoreCase))
            {
                foreach (var other in Entries(entry.Action.actionMap))
                {
                    if (other.Action == entry.Action && other.Index == entry.Index) continue;
                    if (!string.Equals(other.Action.bindings[other.Index].effectivePath, path, StringComparison.OrdinalIgnoreCase)) continue;
                    message = "「" + other.Label + "」が使用中です。別のキーを選んでください。";
                    return false;
                }
            }
            entry.Action.ApplyBindingOverride(entry.Index, path);
            message = "「" + entry.Label + "」を変更して保存しました。";
            return true;
        }

        private static bool IsKeyboardOrMouseButton(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            return path.StartsWith("<Keyboard>/", StringComparison.OrdinalIgnoreCase) ||
                (path.StartsWith("<Mouse>/", StringComparison.OrdinalIgnoreCase) && path.EndsWith("Button", StringComparison.OrdinalIgnoreCase));
        }

        private static string PartName(string name)
        {
            switch (name)
            {
                case "up": return "前";
                case "down": return "後";
                case "left": return "左";
                case "right": return "右";
                default: return name;
            }
        }

        private static string ActionName(string name)
        {
            switch (name)
            {
                case "Move": return "移動";
                case "Fire": return "発射";
                case "Jump": return "ジャンプ";
                case "Sprint": return "ダッシュ";
                case "Crouch": return "しゃがむ";
                case "Interact": return "調べる";
                case "Previous": return "前のアイテム";
                case "Next": return "次のアイテム";
                case "Confirmed": return "配置を確定";
                case "LayoutMode": return "配置モード";
                case "SwitchWave": return "波を切り替え";
                case "ToggleFireMode": return "発射モード切り替え";
                case "EchoScan": return "エコースキャン";
                default: return name;
            }
        }
    }
}
