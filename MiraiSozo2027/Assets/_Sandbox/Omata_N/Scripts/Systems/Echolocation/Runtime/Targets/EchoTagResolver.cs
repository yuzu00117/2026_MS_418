// 有効行の先勝ちでタグ色を検索する。ハッシュが変化したときだけ辞書を再構築する。
using System.Collections.Generic;
using UnityEngine;

namespace Echo.Echolocation
{
    /// <summary>対象タグから表示色を取得する。</summary>
    public sealed class EchoTagResolver
    {
        private readonly Dictionary<string, Color> _tagColors = new Dictionary<string, Color>();
        private readonly Dictionary<string, EchoTagColorRule> _rules = new Dictionary<string, EchoTagColorRule>();
        private readonly Dictionary<EchoTagDefinition, EchoTagColorRule> _definitions = new Dictionary<EchoTagDefinition, EchoTagColorRule>();
        private int _previousHash;
        private bool _isBuilt;
        /// <summary>変更時だけ検索表を更新する。重複は上側を優先する。</summary>
        public void Update(List<EchoTagColorRule> rules)
        {
            int h = 17;
            unchecked
            {
                foreach (var r in rules)
                    if (r != null)
                        h = (((h * 31 + (r.TagName?.GetHashCode() ?? 0)) * 31 + r.Color.GetHashCode()) * 31 + r.IsEnabled.GetHashCode()) * 31 + r.GetHashCode() + (r.Definition ? r.Definition.GetHashCode() : 0);
            }

            if (_isBuilt && h == _previousHash)
                return;
            _isBuilt = true;
            _previousHash = h;
            _tagColors.Clear();
            _rules.Clear();
            _definitions.Clear();
            foreach (var rule in rules)
                if (rule != null && rule.IsEnabled && rule.Definition && !_definitions.ContainsKey(rule.Definition)) _definitions.Add(rule.Definition, rule);
            foreach (var r in rules)
                if (r != null && r.IsEnabled && !r.Definition && !string.IsNullOrEmpty(r.TagName))
                {
                    if (_tagColors.ContainsKey(r.TagName))
                    {
                        Debug.LogWarning($"エコー：タグ「{r.TagName}」が重複しています。上の行を使用します。");
                        continue;
                    }

                    _tagColors.Add(r.TagName, r.Color);
                    _rules.Add(r.TagName, r);
                }
        }

        /// <summary>完全一致するタグがある場合だけ色を返す。</summary>
        public bool TryGet(string tag, out Color color) => _tagColors.TryGetValue(tag, out color);
        public bool TryGetRule(EchoTag tag, out EchoTagColorRule rule)
        {
            rule = null;
            return tag && tag.isActiveAndEnabled && tag.Definition && _definitions.TryGetValue(tag.Definition, out rule);
        }
        public bool TryGetRule(string tag, out EchoTagColorRule rule) => _rules.TryGetValue(tag, out rule);
    }
}
