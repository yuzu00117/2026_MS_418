using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PhononkinesisTarget : MonoBehaviour
{
    public enum ReleaseBehavior
    {
        Auto,               // 初期設定を維持（元々浮いていれば浮いたまま、重力なら落下）
        AlwaysStayFloating, // 手放したら必ず空中で静止固定する（Kinematic化）
        AlwaysGravity       // 手放したら必ず重力で落下する
    }

    [Header("Settings")]
    [Tooltip("手放した（配置確定した）ときの挙動")]
    [SerializeField] private ReleaseBehavior _releaseBehavior = ReleaseBehavior.Auto;

    [Tooltip("念力で掴めるかどうかのフラグ")]
    [SerializeField] private bool _canBeGrabbed = true;
    public bool CanBeGrabbed => _canBeGrabbed;

    public Rigidbody Rb { get; private set; }

    private bool _initialUseGravity;
    private bool _initialIsKinematic;
    private float _initialLinearDamping;
    private RigidbodyConstraints _initialConstraints;

    private void Awake()
    {
        Rb = GetComponent<Rigidbody>();
        _initialUseGravity = Rb.useGravity;
        _initialIsKinematic = Rb.isKinematic;
        _initialLinearDamping = Rb.linearDamping;
        _initialConstraints = Rb.constraints;
    }

    /// <summary>
    /// 掴まれた瞬間の処理（物理で運べるように一時調整）
    /// </summary>
    public void OnGrabbed()
    {
        Rb.isKinematic = false;
        Rb.useGravity = false;
        Rb.linearDamping = 1.0f; // ブレ防止
        Rb.constraints = _initialConstraints | RigidbodyConstraints.FreezeRotation;
    }

    /// <summary>
    /// 手放された（配置確定）瞬間の処理
    /// </summary>
    public void OnReleased()
    {
        switch (_releaseBehavior)
        {
            case ReleaseBehavior.Auto:
                // 元々の設定に忠実に復帰
                Rb.linearVelocity = Vector3.zero;
                Rb.angularVelocity = Vector3.zero;
                Rb.isKinematic = _initialIsKinematic;
                Rb.useGravity = _initialUseGravity;
                break;

            case ReleaseBehavior.AlwaysStayFloating:
                // 空中でピタッと静止固定
                Rb.linearVelocity = Vector3.zero;
                Rb.angularVelocity = Vector3.zero;
                Rb.isKinematic = true;
                Rb.useGravity = false;
                break;

            case ReleaseBehavior.AlwaysGravity:
                // 通常の物理落下
                Rb.isKinematic = false;
                Rb.useGravity = true;
                break;
        }

        Rb.linearDamping = _initialLinearDamping;
        Rb.constraints = _initialConstraints;
    }
}
