using Aura.Inspector;
using UnityEngine;
using UnityEngine.Events;

namespace Aura.Gimmick
{
    /// @brief 念力で掴んで倒すレバー。オン側の端まで倒すとオン、オフ側の端まで戻すとオフになる
    /// 「オンになったときの処理」に GimmickMover の Activate、「オフになったときの処理」に Deactivate などをつなぐ
    /// 倒し方は HingeJoint（軸を中心に回る仕組み）に任せる。倒せる範囲は、このコンポーネントの角度で決める
    /// 置き方:
    /// - この GameObject の位置（原点）は持ち手に置き、HingeJoint の Anchor（回る軸）は根元に置く
    ///   （念力は原点を引っ張るので、原点が軸の上にあると引っ張っても回らない）
    /// - 念力で掴めるように PhononkinesisTarget を付け、「掴んでいる間も回転できる」を ON、
    ///   「手放したときの挙動」を AlwaysStayFloating にする（離したらその場で止まる）
    /// - Rigidbody は Is Kinematic を ON にしておく（掴むまで倒れないように）
    /// - 置いたときの角度が 0 度。そこから「オフ側の角度」「オン側の角度」まで倒れる
    [RequireComponent(typeof(HingeJoint))]
    public sealed class PsychoLever : MonoBehaviour
    {
        [InspectorLabel("オフ側の角度（度）")]
        [Tooltip("オフ側の端の角度（度）。置いたときの向きが 0 度。\n" +
                 "置いたときにオフ側へ倒れていることが多いので、0 のままでよいことが多い")]
        [SerializeField, Range(-180.0f, 180.0f)] private float _offAngle = 0.0f;  ///< オフ側の端の角度（度）

        [InspectorLabel("オン側の角度（度）")]
        [Tooltip("オン側の端の角度（度）。置いたときの向きが 0 度。回る向きは HingeJoint の Axis で決まる。\n" +
                 "逆に倒れるときは、マイナスの値にする")]
        [SerializeField, Range(-180.0f, 180.0f)] private float _onAngle = 90.0f;  ///< オン側の端の角度（度）

        [InspectorLabel("切り替わる位置（割合）")]
        [Tooltip("端のどれだけ手前で切り替えるか。0.95 なら、オフ側からオン側までの 95% まで倒すとオン、5% まで戻すとオフ。\n" +
                 "1 にすると、ぴったり端まで倒さないと切り替わらない（物理の揺れで反応しないことがある）")]
        [SerializeField, Range(0.5f, 1.0f)] private float _switchRatio = 0.95f;   ///< 切り替わる位置（オフ側 0、オン側 1 の割合）

        [InspectorLabel("オンになったときの処理")]
        [Tooltip("オン側の端まで倒したときに呼ぶ処理。扉を開けるなど。\n" +
                 "置いたときからオン側に倒れていれば、ゲーム開始時にも 1 回呼ぶ（つないだ扉などをレバーの状態にそろえるため）")]
        [SerializeField, FilteredEventMenu] private UnityEvent _onTurnedOn = new UnityEvent();   ///< オンになったときに呼ぶ

        [InspectorLabel("オフになったときの処理")]
        [Tooltip("オフ側の端まで戻したときに呼ぶ処理。扉を閉めるなど")]
        [SerializeField, FilteredEventMenu] private UnityEvent _onTurnedOff = new UnityEvent();  ///< オフになったときに呼ぶ

        private HingeJoint _hinge;  ///< 軸を中心に回る仕組み。今の角度を読むのと、倒せる範囲を決めるのに使う

        /// @brief オンになっているか
        public bool IsOn { get; private set; }

        /// @brief 今どこまで倒れているか（オフ側の端 0、オン側の端 1）
        public float GetProgress()
        {
            return Mathf.InverseLerp(_offAngle, _onAngle, _hinge.angle);
        }

        private void Awake()
        {
            _hinge = GetComponent<HingeJoint>();
            ApplyLimits();
        }

        private void Start()
        {
            // 置いたときからオン側なら、つないだ扉などもオンの状態にそろえる
            // Awake ではなく Start で呼ぶのは、つないだ先の Awake（初期位置の記録など）が終わってから呼ぶため
            if (GetProgress() >= _switchRatio)
            {
                IsOn = true;
                _onTurnedOn.Invoke();
            }
        }

        private void FixedUpdate()
        {
            UpdateState();
        }

        /// @brief 倒せる範囲を HingeJoint に設定する。跳ね返ると端で揺れて判定がぶれるので、跳ね返りは 0 にする
        private void ApplyLimits()
        {
            JointLimits limits = _hinge.limits;
            limits.min = Mathf.Min(_offAngle, _onAngle);
            limits.max = Mathf.Max(_offAngle, _onAngle);
            limits.bounciness = 0.0f;
            _hinge.limits = limits;
            _hinge.useLimits = true;
        }

        /// @brief 倒れ具合を見て、オンとオフを切り替える
        /// 真ん中あたりでは今の状態のまま変えない。境目で揺れたときに、何度も切り替わるのを防ぐため
        private void UpdateState()
        {
            float progress = GetProgress();
            if (!IsOn && progress >= _switchRatio)
            {
                IsOn = true;
                _onTurnedOn.Invoke();
            }
            else if (IsOn && progress <= 1.0f - _switchRatio)
            {
                IsOn = false;
                _onTurnedOff.Invoke();
            }
        }
    }
}
