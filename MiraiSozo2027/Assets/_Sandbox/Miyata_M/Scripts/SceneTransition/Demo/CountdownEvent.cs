using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace SceneFlow
{
    /// <summary>
    /// 動作確認用のデモ。開始から指定秒数が経つと UnityEvent を1回発火し、残り秒数を表示する。
    /// 「条件を満たしたら SceneTransitionTrigger.Go を呼ぶ」流れを目で確かめるために使う。
    /// 実際のゲームでは、クリア判定などが同じ形で UnityEvent を発火する。
    /// </summary>
    public class CountdownEvent : MonoBehaviour
    {
        private const float TickSeconds = 1f;
        private const string LabelFormat = "クリアまで {0} 秒";

        [SerializeField] private int _seconds = 5;
        [SerializeField] private Text _label;
        [SerializeField] private UnityEvent _onFinished;

        public UnityEvent OnFinished => _onFinished;

        private void Start()
        {
            StartCoroutine(CountdownRoutine());
        }

        private IEnumerator CountdownRoutine()
        {
            // timeScale = 0 でも進むよう Realtime で待つ
            var tick = new WaitForSecondsRealtime(TickSeconds);
            for (int remaining = _seconds; remaining > 0; remaining--)
            {
                if (_label != null) _label.text = string.Format(LabelFormat, remaining);
                yield return tick;
            }
            if (_label != null) _label.text = "クリア!";
            _onFinished.Invoke();
        }
    }
}
