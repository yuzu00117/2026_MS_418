namespace Aura.Combat
{
    /// <summary>
    /// キャッシュ構造体が実装する共通インターフェース
    /// </summary>
    public interface ICachedTarget
    {
        bool IsTargetLost { get; }
    }
}
