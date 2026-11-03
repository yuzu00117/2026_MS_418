using Aura.Inspector;
using UnityEngine;
using UnityEngine.Events;

namespace Aura.Gimmick
{
    /// @brief 起動した数を数えて、必要な数がそろったときに動かすギミック（3 つのスイッチを全部起動すると開く扉など）
    /// 受け手（ReactionTrigger）の「起動したときの処理」に Add、「止まったときの処理」に Remove をつなぐ
    /// そろったときの動き（扉を開けるなど）は「そろったときの処理」に、GimmickMover の Activate などをつなぐ
    public sealed class ActivationCounter : MonoBehaviour
    {
        [InspectorLabel("必要な数")]
        [Tooltip("そろったとみなす数。数えた数がこれに届いた瞬間に「そろったときの処理」を呼ぶ。例: 3 なら、3 つのスイッチを起動するとそろう")]
        [SerializeField, Min(1)] private int _requiredCount = 3;  ///< そろったとみなす数

        [InspectorLabel("そろったときの処理")]
        [Tooltip("数えた数が「必要な数」に届いたときに呼ぶ処理。扉を開けるなど")]
        [SerializeField, FilteredEventMenu] private UnityEvent _onCompleted = new UnityEvent();  ///< そろったときに呼ぶ

        [InspectorLabel("崩れたときの処理")]
        [Tooltip("そろった後に数が減って、「必要な数」を下回ったときに呼ぶ処理。扉を閉めるなど。Remove を使わなければ呼ばれない")]
        [SerializeField, FilteredEventMenu] private UnityEvent _onBroken = new UnityEvent();     ///< そろった状態が崩れたときに呼ぶ

        /// @brief 今までに数えた数
        public int Count { get; private set; }

        /// @brief そろったとみなす数
        public int RequiredCount => _requiredCount;

        /// @brief そろっているか（数えた数が「必要な数」以上か）
        public bool IsCompleted => Count >= _requiredCount;

        /// @brief 1 つ数える。受け手の「起動したときの処理」につなぐ
        [ShowInEventMenu]
        public void Add()
        {
            SetCount(Count + 1);
        }

        /// @brief 1 つ減らす。受け手の「止まったときの処理」につなぐ（0 より下にはならない）
        [ShowInEventMenu]
        public void Remove()
        {
            SetCount(Mathf.Max(0, Count - 1));
        }

        /// @brief 数えた数を 0 に戻す
        [ShowInEventMenu]
        public void ResetCount()
        {
            SetCount(0);
        }

        private void SetCount(int count)
        {
            bool wasCompleted = IsCompleted;
            Count = count;
            if (!wasCompleted && IsCompleted)
            {
                _onCompleted.Invoke();
            }
            else if (wasCompleted && !IsCompleted)
            {
                _onBroken.Invoke();
            }
        }
    }
}
