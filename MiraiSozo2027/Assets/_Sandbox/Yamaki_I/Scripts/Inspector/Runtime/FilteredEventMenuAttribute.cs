using System;
using UnityEngine;

namespace Aura.Inspector
{
    /// @brief UnityEvent の欄に付けると、関数のメニューで、[ShowInEventMenu] の印があるスクリプトは印の付いたものだけを出す
    /// 見せ方だけで、保存される値や動きは何も変わらない。[InspectorLabel] と一緒に付けてよい
    /// 例: [InspectorLabel("起動したときの処理")] [SerializeField, FilteredEventMenu] private UnityEvent _onActivated;
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class FilteredEventMenuAttribute : PropertyAttribute
    {
    }
}
