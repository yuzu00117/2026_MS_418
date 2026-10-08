namespace Aura.Wave
{
    /// @brief 波が物に当たったときに、跳ね返るかどうかの決め方。WaveEmitter の Reflection Mode で選ぶ
    /// シーンには番号で保存されるので、新しい値は最後に足す
    public enum WaveReflectionMode
    {
        None,            ///< 跳ね返らない。最初に当たった物で止まる（反射を入れる前と同じ動き）
        AllSurfaces,     ///< どの物でも跳ね返る。WaveSurface の Response を Absorb にした物と、トリガーのコライダー（Reflect の WaveSurface がないもの）では止まる
        ReflectorsOnly,  ///< 反射板（WaveSurface の Response を Reflect にした物）でだけ跳ね返る。ほかの物では止まる
    }
}
