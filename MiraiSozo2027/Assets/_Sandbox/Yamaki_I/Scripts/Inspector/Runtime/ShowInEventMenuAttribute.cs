using System;

namespace Aura.Inspector
{
    /// @brief UnityEvent の関数のメニューに出すメソッド・プロパティに付ける印
    /// この印が 1 つでもあるスクリプトは、[FilteredEventMenu] を付けた UnityEvent のメニューで、印の付いたものだけを出す
    /// （MonoBehaviour から受け継いだ enabled、name、SendMessage などは出さない）。印の無いスクリプトは、ふつうのメニューのまま
    /// メニューに出るのは、Unity の決まりで、戻り値が void で、引数が無いか 1 つ（int、float、string、bool、Object）のものだけ
    /// 例: [ShowInEventMenu] public void Activate() { ... }
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property)]
    public sealed class ShowInEventMenuAttribute : Attribute
    {
    }
}
