// デモの操作案内のみ。移動はEchoDemoPlayer、スキャン入力はEchoPrototypeInputが担当する。
using UnityEngine;

namespace Echo.Echolocation.Samples
{
    /// <summary>操作方法と現在のスキャン・カメラ状態の表示。</summary>
    public sealed class EchoDemoToggle : MonoBehaviour
    {
        [Tooltip("デモで操作するControllerです。")]
        [UnityEngine.SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("Controller")]
        private EchoController _controller;
        public EchoController Controller { get => _controller; set => _controller = value; }

        private EchoDemoPlayer _player;
        private GUIStyle _text;
        private GUIStyle _title;
        void OnGUI()
        {
            if (_text == null)
            {
                _text = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 14
                };
                _title = new GUIStyle(_text)
                {
                    fontSize = 17,
                    fontStyle = FontStyle.Bold
                };
            }

            if (!_player)
                _player = GetComponent<EchoDemoPlayer>();
            GUI.Box(new Rect(16, 16, 510, 140), GUIContent.none);
            GUI.Label(new Rect(30, 24, 480, 26), "ECHOLOCATION / PLAYER PROTOTYPE", _title);
            GUI.Label(new Rect(30, 53, 480, 23), "WASD : Move    Mouse : Look    Z : First / Third person", _text);
            GUI.Label(new Rect(30, 77, 480, 23), "E : Scan    Esc : Release cursor    Click : Capture", _text);
            if (Controller)
                GUI.Label(new Rect(30, 101, 480, 23), $"{Controller.Settings.ScanMode}  Waves:{Controller.Frames.Count}  {Controller.CurrentOuterRadius:F1}m  {(_player && _player.IsThirdPerson ? "THIRD PERSON" : "FIRST PERSON")}", _text);
            GUI.Label(new Rect(Screen.width / 2f - 5, Screen.height / 2f - 10, 20, 20), "+", _text);
        }
    }
}
