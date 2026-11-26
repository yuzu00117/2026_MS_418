using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Serialization;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Click on-screen buttons (or press 1-9) to play animation clips on this model.
// No Animator Controller needed.
[RequireComponent(typeof(Animator))]
public class AnimationTester : MonoBehaviour
{
    // FormerlySerializedAs keeps the values you already set in the Inspector after the rename.
    [FormerlySerializedAs("clips")]
    [SerializeField] private AnimationClip[] _clips;

    [FormerlySerializedAs("blendTime")]
    [SerializeField] private float _blendTime = 0.15f;   // crossfade between clips

    [FormerlySerializedAs("loop")]
    [SerializeField] private bool _isLooping = true;

    private PlayableGraph _graph;
    private AnimationMixerPlayable _mixer;
    private int _current = -1;
    private int _previous = -1;
    private float _blendT = 1f;

    private void OnEnable()
    {
        _graph = PlayableGraph.Create("AnimationTester");
        var output = AnimationPlayableOutput.Create(_graph, "out", GetComponent<Animator>());
        _mixer = AnimationMixerPlayable.Create(_graph, _clips.Length);
        output.SetSourcePlayable(_mixer);

        for (int i = 0; i < _clips.Length; i++)
        {
            if (_clips[i] == null) continue;
            var p = AnimationClipPlayable.Create(_graph, _clips[i]);
            _graph.Connect(p, 0, _mixer, i);
            _mixer.SetInputWeight(i, 0f);
        }
        _graph.Play();
        if (_clips.Length > 0) Play(0);
    }

    private void OnDisable()
    {
        if (_graph.IsValid()) _graph.Destroy();
    }

    public void Play(int index)
    {
        if (index < 0 || index >= _clips.Length || _clips[index] == null) return;
        _previous = _current;
        _current = index;
        _blendT = 0f;

        var p = _mixer.GetInput(index);
        p.SetTime(0);
        p.Play();
    }

    private void Update()
    {
        // Number keys 1-9
        for (int i = 0; i < Mathf.Min(9, _clips.Length); i++)
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null && kb[(Key)((int)Key.Digit1 + i)].wasPressedThisFrame) Play(i);
#else
            if (Input.GetKeyDown(KeyCode.Alpha1 + i)) Play(i);
#endif
        }

        // Crossfade weights
        if (_current >= 0)
        {
            _blendT = _blendTime > 0 ? Mathf.Min(1f, _blendT + Time.deltaTime / _blendTime) : 1f;
            for (int i = 0; i < _clips.Length; i++)
            {
                float w = 0f;
                if (i == _current) w = _blendT;
                else if (i == _previous) w = 1f - _blendT;
                _mixer.SetInputWeight(i, w);
            }

            // Loop the current clip
            var p = _mixer.GetInput(_current);
            var clip = _clips[_current];
            if (_isLooping && clip != null && p.GetTime() >= clip.length)
                p.SetTime(0);
        }
    }

    private void OnGUI()
    {
        for (int i = 0; i < _clips.Length; i++)
        {
            if (_clips[i] == null) continue;
            string label = (i < 9 ? (i + 1) + ": " : "") + _clips[i].name;
            if (GUI.Button(new Rect(10, 10 + i * 34, 200, 30), label))
                Play(i);
        }
    }
}
