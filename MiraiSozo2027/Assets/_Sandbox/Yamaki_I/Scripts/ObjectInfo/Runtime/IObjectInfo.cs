namespace Aura.ObjectInfo
{
    /// @brief Object に付ける情報のコンポーネントの印（TelekinesisTarget、WaveReaction、WeakPoint、Substance、Concealment、BeamSourceInfo）
    /// ObjectInfoLookup.TryGet で探せるのは、これを付けたコンポーネントだけ
    /// （調べた結果を覚えておくには、有効・無効が変わったことを自分で知らせてもらう必要があるため）
    /// 付けた型は OnEnable と OnDisable で ObjectInfoCache.MarkDirty を呼ぶ。MarkDirty はこのアセンブリの中からしか呼べないので、
    /// Aura.ObjectInfo の外の型には付けない（付けると、有効・無効の変化が知らされず、古い結果が返る）
    public interface IObjectInfo
    {
    }
}
