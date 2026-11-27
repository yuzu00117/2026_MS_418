namespace Aura.PlayerControl
{
    /// <summary>
    /// 同時に使えるかを決めるための、プレイヤーの能力の種類。
    /// 能力を増やす時は、ここに足して PlayerAbilityLock.IsInUse に「使用中」の判定を足す
    /// </summary>
    public enum PlayerAbility
    {
        Echolocation,
        Phononkinesis,
        Wave
    }
}
