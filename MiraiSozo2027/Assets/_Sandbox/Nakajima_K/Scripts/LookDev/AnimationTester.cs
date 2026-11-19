using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Click on-screen buttons (or press 1-9) to play animation clips on this model.
// No Animator Controller needed.
[RequireComponent(typeof(Animator))]
public class AnimationTester : MonoBehaviour
{
    public AnimationClip[] clips;
    public float blendTime = 0.15f;   // crossfade between clips
    public bool loop = true;

    PlayableGraph graph;
    AnimationMixerPlayable mixer;
    int current = -1;
    int previous = -1;
    float blendT = 1f;

    void OnEnable()
    {
        graph = PlayableGraph.Create("AnimationTester");
        var output = AnimationPlayableOutput.Create(graph, "out", GetComponent<Animator>());
        mixer = AnimationMixerPlayable.Create(graph, clips.Length);
        output.SetSourcePlayable(mixer);

        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] == null) continue;
            var p = AnimationClipPlayable.Create(graph, clips[i]);
            graph.Connect(p, 0, mixer, i);
            mixer.SetInputWeight(i, 0f);
        }
        graph.Play();
        if (clips.Length > 0) Play(0);
    }

    void OnDisable()
    {
        if (graph.IsValid()) graph.Destroy();
    }

    public void Play(int index)
    {
        if (index < 0 || index >= clips.Length || clips[index] == null) return;
        previous = current;
        current = index;
        blendT = 0f;

        var p = mixer.GetInput(index);
        p.SetTime(0);
        p.Play();
    }

    void Update()
    {
        // Number keys 1-9
        for (int i = 0; i < Mathf.Min(9, clips.Length); i++)
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null && kb[(Key)((int)Key.Digit1 + i)].wasPressedThisFrame) Play(i);
#else
            if (Input.GetKeyDown(KeyCode.Alpha1 + i)) Play(i);
#endif
        }

        // Crossfade weights
        if (current >= 0)
        {
            blendT = blendTime > 0 ? Mathf.Min(1f, blendT + Time.deltaTime / blendTime) : 1f;
            for (int i = 0; i < clips.Length; i++)
            {
                float w = 0f;
                if (i == current) w = blendT;
                else if (i == previous) w = 1f - blendT;
                mixer.SetInputWeight(i, w);
            }

            // Loop the current clip
            var p = mixer.GetInput(current);
            var clip = clips[current];
            if (loop && clip != null && p.GetTime() >= clip.length)
                p.SetTime(0);
        }
    }

    void OnGUI()
    {
        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] == null) continue;
            string label = (i < 9 ? (i + 1) + ": " : "") + clips[i].name;
            if (GUI.Button(new Rect(10, 10 + i * 34, 200, 30), label))
                Play(i);
        }
    }
}
