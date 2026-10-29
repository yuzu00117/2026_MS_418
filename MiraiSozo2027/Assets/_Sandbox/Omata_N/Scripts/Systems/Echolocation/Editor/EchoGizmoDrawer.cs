// 180度超も半角式で描き、円錐メッシュによる誤表示を避ける。
using UnityEditor;
using UnityEngine;

namespace Echo.Echolocation.Editor
{
    public static class EchoGizmoDrawer
    {
        [DrawGizmo(GizmoType.Selected | GizmoType.Active)]
        static void Draw(EchoController controller, GizmoType type)
        {
            if (!controller.IsGizmoVisible)
                return;
            var frame = controller.GetPreview();
            DrawVolume(frame, frame.OuterRadius, new Color(0, .8f, 1, .6f));
            if (Application.isPlaying)
                foreach (var wave in controller.Frames)
                {
                    DrawVolume(wave, wave.OuterRadius, Color.green);
                    if (wave.State == EchoState.Hiding)
                        DrawVolume(wave, wave.EraseRadius, Color.red);
                }
        }

        static void DrawVolume(EchoFrameData frame, float radius, Color color)
        {
            if (radius <= 0 || frame.Angle <= 0)
                return;
            Handles.color = color;
            Quaternion rotation = Quaternion.LookRotation(frame.Forward);
            float half = frame.Angle * .5f * Mathf.Deg2Rad;
            for (int meridian = 0; meridian < 8; meridian++)
            {
                float azimuth = meridian * Mathf.PI / 4;
                Vector3 prev = frame.Origin + frame.Forward * radius;
                for (int i = 1; i <= 32; i++)
                {
                    float polar = half * i / 32;
                    var next = frame.Origin + rotation * new Vector3(Mathf.Sin(polar) * Mathf.Cos(azimuth), Mathf.Sin(polar) * Mathf.Sin(azimuth), Mathf.Cos(polar)) * radius;
                    Handles.DrawLine(prev, next);
                    prev = next;
                }

                if (frame.Angle < 360)
                    Handles.DrawLine(frame.Origin, prev);
            }

            if (frame.Angle < 360)
                Handles.DrawWireDisc(frame.Origin + frame.Forward * (Mathf.Cos(half) * radius), frame.Forward, Mathf.Sin(half) * radius);
        }
    }
}
