// Sourcery規約参照テスト用。マージしない
using UnityEngine;

public class PlayerStatus : MonoBehaviour
{
    private int _health = 100;
    private bool _isGrounded;

    public bool Alive() => _health > 0;

    public bool Jumpable() => _isGrounded;
}
