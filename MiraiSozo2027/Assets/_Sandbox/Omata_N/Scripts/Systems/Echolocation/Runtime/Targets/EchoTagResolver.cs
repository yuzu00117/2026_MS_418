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
        // 一覧の変更検出用ハッシュ。一般的な素数の組み合わせ。
        private const int HashSeed = 17;
        private const int HashMultiplier = 31;
        /// <summary>変更時だけ検索表を更新する。重複は上側を優先する。</summary>
        public void Update(List<EchoTagColorRule> rules)
        {
            int hash = HashSeed;
            unchecked
            {
                foreach (var rule in rules)
                    if (rule != null)
                        hash = (((hash * HashMultiplier + (rule.TagName?.GetHashCode() ?? 0)) * HashMultiplier + rule.Color.GetHashCode()) * HashMultiplier + rule.IsEnabled.GetHashCode()) * HashMultiplier + rule.GetHashCode() + (rule.Definition ? rule.Definition.GetHashCode() : 0);
            }

            if (_isBuilt && hash == _previousHash)
                return;
            _isBuilt = true;
            _previousHash = hash;
            _tagColors.Clear();
            _rules.Clear();
            _definitions.Clear();
            foreach (var rule in rules)
                if (rule != null && rule.IsEnabled && rule.Definition && !_definitions.ContainsKey(rule.Definition)) _definitions.Add(rule.Definition, rule);
            foreach (var rule in rules)
                if (rule != null && rule.IsEnabled && !rule.Definition && !string.IsNullOrEmpty(rule.TagName))
                {
                    if (_tagColors.ContainsKey(rule.TagName))
                    {
                        Debug.LogWarning($"エコー：タグ「{rule.TagName}」が重複しています。上の行を使用します。");
                        continue;
                    }

                    _tagColors.Add(rule.TagName, rule.Color);
                    _rules.Add(rule.TagName, rule);
                }
        }

        /// <summary>完全一致するタグがある場合だけ色を返す。</summary>
        public bool TryGet(string tag, out Color color) => _tagColors.TryGetValue(tag, out color);
        public bool TryGetRule(EchoTarget target, out EchoTagColorRule rule)
        {
            rule = null;
            var definition = target ? target.Definition : null;
            if (!target || !target.isActiveAndEnabled || !definition) return false;
            rule = definition.Settings;
            return rule.HasAnyEffect;
        }
        public bool TryGetRule(string tag, out EchoTagColorRule rule) => _rules.TryGetValue(tag, out rule);
    }
}
