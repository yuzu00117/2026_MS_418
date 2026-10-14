// 180度超も半角式で描き、円錐メッシュによる誤表示を避ける。
using UnityEditor;
using UnityEngine;

namespace Echo.Echolocation.Editor
{
    public static class EchoGizmoDrawer
    {
        [DrawGizmo(GizmoType.Selected | GizmoType.Active)]
        static void Draw(EchoController c, GizmoType type)
        {
            if (!c.IsGizmoVisible)
                return;
            var f = c.GetPreview();
            DrawVolume(f, f.OuterRadius, new Color(0, .8f, 1, .6f));
            if (Application.isPlaying)
                foreach (var wave in c.Frames)
                {
                    DrawVolume(wave, wave.OuterRadius, Color.green);
                    if (wave.State == EchoState.Hiding)
                        DrawVolume(wave, wave.EraseRadius, Color.red);
                }
        }

        static void DrawVolume(EchoFrameData f, float radius, Color color)
        {
            if (radius <= 0 || f.Angle <= 0)
                return;
            Handles.color = color;
            Quaternion q = Quaternion.LookRotation(f.Forward);
            float half = f.Angle * .5f * Mathf.Deg2Rad;
            for (int meridian = 0; meridian < 8; meridian++)
            {
                float az = meridian * Mathf.PI / 4;
                Vector3 prev = f.Origin + f.Forward * radius;
                for (int i = 1; i <= 32; i++)
                {
                    float a = half * i / 32;
                    var next = f.Origin + q * new Vector3(Mathf.Sin(a) * Mathf.Cos(az), Mathf.Sin(a) * Mathf.Sin(az), Mathf.Cos(a)) * radius;
                    Handles.DrawLine(prev, next);
                    prev = next;
                }

                if (f.Angle < 360)
                    Handles.DrawLine(f.Origin, prev);
            }

            if (f.Angle < 360)
                Handles.DrawWireDisc(f.Origin + f.Forward * (Mathf.Cos(half) * radius), f.Forward, Mathf.Sin(half) * radius);
        }
    }
}
