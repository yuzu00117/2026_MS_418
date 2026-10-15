using System.Collections.Generic;
using UnityEngine;

namespace Aura.ObjectInfoDemo
{
    /// @brief ビームの点。InfoBeamEmitter が、Beam Shape が Point Chain のときに作って一定の間隔で並べる（自分で付けるものではない）
    /// 見えない Object で、念力で掴む対象を選ぶときに、ふつうの Object と同じように選べるようにするためにある
    /// （コライダーと Rigidbody を持つ。念力の側の印（Phononkinesis の PhononkinesisTarget など）は、Point Prefab に付けておく。ビームの実体は、点どうしをつなぐ直線の方）
    /// 掴まれたかは、次のどちらかで決める
    /// ・Is Held がオン（Inspector やコードから入れる）
    /// ・Rigidbody の Is Kinematic が外れ、Use Gravity も切れている（Rigidbody を速度で動かす念力が掴んだとき。Phononkinesis はこうなる）
    [DisallowMultipleComponent]
    public sealed class BeamPoint : MonoBehaviour
    {
        private const float GizmoRadius = 0.06f;        ///< Scene ビューに出す点の印の半径（m）
        private const float AnchorGizmoRadius = 0.12f;  ///< Scene ビューに出す支点の印の半径（m）

        [Tooltip("オンにすると、掴まれたものとして扱う（確かめる用。再生中にオンにして Scene ビューで動かすと、ビームが曲がる）\n" +
                 "Rigidbody を動かさない念力なら、掴んだときにコードからオンにし、離したときにオフにする\n" +
                 "Rigidbody を速度で動かす念力（Is Kinematic を外して重力を切るもの）なら、オンにしなくてよい")]
        [SerializeField] private bool _isHeld;  ///< 掴まれたものとして扱うか

        private readonly List<Collider> _colliders = new List<Collider>();  ///< この点のコライダー（子のものも含む）。掴めるかどうかで有効と無効を切り替える
        private Rigidbody _rigidbody;
        private bool _isGrabbable;

        /// @brief この点を作った InfoBeamEmitter
        public InfoBeamEmitter Emitter { get; private set; }

        /// @brief ビームの点の List（InfoBeamEmitter.Points）での番号。出る位置が 0。今のビームに使っていなければ -1
        public int Index { get; internal set; } = -1;

        /// @brief ビームのどこにある点か
        public BeamPointKind Kind { get; internal set; }

        /// @brief 支点（掴まれている点か、掴まれて離された後もビームを曲げている点）か
        public bool IsAnchor { get; internal set; }

        /// @brief 掴まれたものとして扱うか（Inspector の Is Held）。念力の機能から入れてもよい
        public bool IsHeld
        {
            get => _isHeld;
            set => _isHeld = value;
        }

        /// @brief 今、掴まれているか。Is Held がオンか、Rigidbody が念力で動かされている（Is Kinematic も Use Gravity も切れている）
        public bool IsGrabbed => _isHeld || (_rigidbody != null && !_rigidbody.isKinematic && !_rigidbody.useGravity);

        /// @brief 今、掴める点か（コライダーが有効か）
        public bool IsGrabbable => _isGrabbable;

        /// @brief この点の Rigidbody
        public Rigidbody Rigidbody => _rigidbody;

        /// @brief 何回跳ね返った後の区間にある点か（0 なら跳ね返る前）。支点が、どの区間を曲げるかを決めるのに使う
        internal int LegIndex { get; set; }

        /// @brief 点を作ったときに足した球のコライダー。プレハブから作った点では null
        internal SphereCollider OwnSphere { get; set; }

        /// @brief Rigidbody が、掴まれた後に重力で落ちる状態になっているか（手放すと重力で落とす念力で離されたとき）
        internal bool IsDropped => _rigidbody != null && !_rigidbody.isKinematic && _rigidbody.useGravity;

        //============================================================
        // InfoBeamEmitter から使う操作
        //============================================================

        /// @brief 作った直後に、持ち主と部品を覚える
        internal void Initialize(InfoBeamEmitter emitter)
        {
            Emitter = emitter;
            _rigidbody = GetComponent<Rigidbody>();
            GetComponentsInChildren(true, _colliders);
            _isGrabbable = true;
            SetGrabbable(false);
        }

        /// @brief 点を動かす。念力で動かされている間（Rigidbody が Kinematic でないとき）は動かさない
        internal void Place(Vector3 position)
        {
            if (_rigidbody != null)
            {
                if (!_rigidbody.isKinematic)
                {
                    return;
                }
                // Rigidbody の位置も合わせて、次の物理の更新を待たずに、念力のレイが新しい位置で当たるようにする
                _rigidbody.position = position;
            }
            transform.position = position;
        }

        /// @brief 掴めるかを切り替える（コライダーを有効か無効にする）
        internal void SetGrabbable(bool isGrabbable)
        {
            if (_isGrabbable == isGrabbable)
            {
                return;
            }
            _isGrabbable = isGrabbable;
            foreach (Collider pointCollider in _colliders)
            {
                if (pointCollider != null)
                {
                    pointCollider.enabled = isGrabbable;
                }
            }
        }

        /// @brief Rigidbody を、ビームが動かす状態（Kinematic、重力なし、止まっている）に戻す
        internal void ResetPhysics()
        {
            if (_rigidbody == null)
            {
                return;
            }
            if (!_rigidbody.isKinematic)
            {
                _rigidbody.linearVelocity = Vector3.zero;
                _rigidbody.angularVelocity = Vector3.zero;
            }
            _rigidbody.isKinematic = true;
            _rigidbody.useGravity = false;
        }

        private void OnDrawGizmos()
        {
            if (Kind == BeamPointKind.Unused && !IsAnchor)
            {
                return;
            }
            if (IsAnchor)
            {
                Gizmos.color = IsGrabbed ? Color.red : new Color(1.0f, 0.5f, 0.0f);
                Gizmos.DrawSphere(transform.position, AnchorGizmoRadius);
                return;
            }
            Gizmos.color = _isGrabbable ? Color.cyan : Color.gray;
            Gizmos.DrawSphere(transform.position, GizmoRadius);
        }
    }
}
