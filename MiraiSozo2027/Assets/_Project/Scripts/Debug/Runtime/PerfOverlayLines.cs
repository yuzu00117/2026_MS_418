using System;
using System.Collections.Generic;
using UnityEngine;

namespace Aura.DebugTools
{
    /// @brief 性能表示（PerfOverlay）に、機能ごとの 1 行を足すための窓口。他の機能のスクリプトから呼ぶ
    /// 足した行は、PerfOverlay の左上に「[名前] 文字」の形で、薄い紫色で出る
    /// 文字を返す関数（getText）は、PerfOverlay の文字の更新間隔（初期値は 1 秒に 4 回）でだけ呼ばれる。毎フレームは呼ばれない
    /// 関数の中で例外が出ても止まらず、その行は「[名前] error」と出る（ログは最初の 1 回だけ）
    /// 開発用ビルド（Editor と Development Build）以外では、呼んでも何もしない（Add は -1 を返す）
    /// 使い方の例（OnEnable で足して、OnDisable で外す）
    ///   private int _perfLineHandle = -1;
    ///   private void OnEnable()  { _perfLineHandle = PerfOverlayLines.Add("Wave", () => "Active " + _activeCount); }
    ///   private void OnDisable() { PerfOverlayLines.Remove(_perfLineHandle); }
    /// 使うには、使う側の asmdef の references に「Aura.DebugTools」を足す
    public static class PerfOverlayLines
    {
        private const int InvalidHandle = -1;  ///< 足せなかったときに返す番号

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// @brief 足した 1 行ぶんの情報
        private sealed class Entry
        {
            public int Handle { get; set; }               ///< Add が返した番号
            public string Owner { get; set; }             ///< 行の頭に付く名前
            public Func<string> GetText { get; set; }     ///< 文字を返す関数
            public bool HasLoggedError { get; set; }      ///< 例外のログをもう出したか
        }

        private static readonly List<Entry> Entries = new List<Entry>();  ///< 足された行の一覧
        private static int _nextHandle = 1;                               ///< 次に渡す番号

        /// @brief Editor で「Domain Reload を切った状態」で再生しても、前回の登録が残らないようにする
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState()
        {
            Entries.Clear();
            _nextHandle = 1;
        }
#endif

        /// @brief 行を 1 つ足す。戻り値の番号は Remove で外すときに使う
        /// @param owner 行の頭に付く名前（例: "Wave" → 「[Wave] ...」）
        /// @param getText 行の文字を返す関数
        public static int Add(string owner, Func<string> getText)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (getText == null)
            {
                return InvalidHandle;
            }

            Entry entry = new Entry();
            entry.Handle = _nextHandle++;
            entry.Owner = string.IsNullOrEmpty(owner) ? "?" : owner;
            entry.GetText = getText;
            Entries.Add(entry);
            return entry.Handle;
#else
            return InvalidHandle;
#endif
        }

        /// @brief Add で足した行を外す。知らない番号や -1 を渡しても何も起きない
        public static void Remove(int handle)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (handle == InvalidHandle)
            {
                return;
            }

            for (int i = 0; i < Entries.Count; i++)
            {
                if (Entries[i].Handle == handle)
                {
                    Entries.RemoveAt(i);
                    return;
                }
            }
#endif
        }

        /// @brief 足された行の文字を集める（PerfOverlay が、文字を更新するときに呼ぶ）
        /// @param results 結果を入れる入れ物。先に中身を空にする
        internal static void Collect(List<string> results)
        {
            results.Clear();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            for (int i = 0; i < Entries.Count; i++)
            {
                Entry entry = Entries[i];
                string text;
                try
                {
                    text = entry.GetText() ?? string.Empty;
                }
                catch (Exception exception)
                {
                    // 毎回ログを出すと画面が埋まるので、1 つの行につき 1 回だけ出す
                    if (!entry.HasLoggedError)
                    {
                        entry.HasLoggedError = true;
                        Debug.LogWarning("[PerfOverlayLines] " + entry.Owner + " の文字を作るときに例外が出た: " + exception);
                    }
                    text = "error";
                }

                results.Add("[" + entry.Owner + "] " + text);
            }
#endif
        }
    }
}
