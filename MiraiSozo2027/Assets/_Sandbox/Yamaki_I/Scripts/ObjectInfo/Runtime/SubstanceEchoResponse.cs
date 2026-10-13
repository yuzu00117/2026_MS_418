namespace Aura.ObjectInfo
{
    /// @brief 素材がエコーロケーションを受けたときの扱い。SubstanceType の Echo Response で選ぶ
    /// アセットには番号で保存されるので、番号を変えず、新しい値は最後に足す
    public enum SubstanceEchoResponse
    {
        Block = 0,        ///< さえぎる。この物の向こうは見つけられない（壁など）
        PassThrough = 1,  ///< 通す。この物の向こうも見つけられる（ガラスなど）
    }
}
