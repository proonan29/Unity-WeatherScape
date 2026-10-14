using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using WeatherScape.World;

namespace WeatherScape
{
    /// <summary>Orbit camera around the scene focus: slow auto-orbit, drag to rotate, wheel to zoom, earthquake shake,
    /// and a projection shift so the scene centre sits in the free area right of the UI panel.</summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Camera))]
    public class CameraRig : MonoBehaviour
    {
        public Vector3 focus = new Vector3(0, 20, 0);
        public float distance = 260f, height = 90f, yaw = 25f;
        public bool autoOrbit = true;
        public float orbitSpeed = 1.5f;
        [Tooltip("Width of the left UI panel in screen pixels (0 = none)")] public float panelPixels;
        public System.Func<Vector2, bool> isOverUI;
        [System.NonSerialized] public Vector3 shakeOffset, shakeEuler;

        Camera cam;
        float zoom = 1f, pitchAdd, idle;
        Vector2 lastMouse;
        bool dragging;

        public Camera Cam => cam != null ? cam : cam = GetComponent<Camera>();

        public void SetScene(BuiltScene s, bool resetView)
        {
            focus = s.focus;
            distance = s.camDistance; height = s.camHeight;
            if (resetView) { yaw = s.camYaw; zoom = 1f; pitchAdd = 0f; }
            Place();
        }

        void LateUpdate()
        {
            if (Application.isPlaying) HandleInput(Time.unscaledDeltaTime);
            Place();
        }

        void HandleInput(float dt)
        {
            bool interacted = false;
#if ENABLE_INPUT_SYSTEM
            var m = Mouse.current;
            if (m != null)
            {
                Vector2 pos = m.position.ReadValue();
                bool overUI = isOverUI != null && isOverUI(pos);
                if ((m.leftButton.wasPressedThisFrame || m.rightButton.wasPressedThisFrame) && !overUI) { dragging = true; lastMouse = pos; }
                if (!m.leftButton.isPressed && !m.rightButton.isPressed) dragging = false;
                if (dragging)
                {
                    var d = pos - lastMouse; lastMouse = pos;
                    yaw += d.x * 0.25f;
                    pitchAdd = Mathf.Clamp(pitchAdd - d.y * 0.15f, -25f, 40f);
                    interacted = d.sqrMagnitude > 0.01f;
                }
                float wheel = m.scroll.ReadValue().y;
                if (Mathf.Abs(wheel) > 0.01f && !overUI) { zoom = Mathf.Clamp(zoom * (wheel > 0 ? 0.9f : 1.1f), 0.25f, 2.5f); interacted = true; }
            }
#endif
            idle = interacted ? 0f : idle + dt;
            if (autoOrbit && idle > 4f) yaw += orbitSpeed * dt;
        }

        public void Place()
        {
            var c = Cam;
            float basePitch = Mathf.Atan2(height, distance) * Mathf.Rad2Deg;
            float pitch = Mathf.Clamp(basePitch + pitchAdd, 3f, 80f);
            float dist = new Vector2(distance, height).magnitude * zoom;
            var rot = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 pos = focus + rot * new Vector3(0, 0, -dist);
            if (pos.y < focus.y * 0.2f + 2f) pos.y = focus.y * 0.2f + 2f;
            transform.position = pos + shakeOffset;
            transform.rotation = Quaternion.LookRotation(focus - pos) * Quaternion.Euler(shakeEuler);
            c.ResetProjectionMatrix();
            if (panelPixels > 0f && Screen.width > 0)
            {
                float f = Mathf.Clamp01(panelPixels / Screen.width);
                var p = c.projectionMatrix;
                p.m02 = -f;
                c.projectionMatrix = p;
            }
        }
    }
}
