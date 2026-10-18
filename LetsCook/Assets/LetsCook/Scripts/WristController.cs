using UnityEngine;

namespace LetsCook
{
    /// Moves the wrist anchors with the keyboard until real wrist tracking exists.
    /// WASD = across the counter, Q/E = down/up, Z/C = turn, Tab = switch hand, Space = move both.
    public class WristController : MonoBehaviour
    {
        public Transform left, right;
        public float speed = 0.35f;
        public float turnSpeed = 90f;
        public Vector3 min = new Vector3(-0.75f, 0.9f, 0.05f);
        public Vector3 max = new Vector3(0.75f, 1.45f, 0.8f);

        /// True while the wrist camera is moving the hands (keyboard movement pauses).
        [System.NonSerialized] public bool externallyDriven;
        bool both = true;
        bool rightSelected = true;

        public string Mode => both ? "both hands" : (rightSelected ? "right hand" : "left hand");

        RokokoReceiver receiver;
        Camera cam;
        /// Where the right hand's thumb–index pinch sits relative to its wrist (from the mock pinch pose).
        public static readonly Vector3 RightPinchOffset = new Vector3(-0.031f, -0.063f, 0.0835f);

        void Update()
        {
            if (ToolRequester.Typing || externallyDriven) return;
            if (receiver == null) receiver = FindFirstObjectByType<RokokoReceiver>();
            if (receiver != null && receiver.MouseHands) { MouseMode(); return; }
            if (Input.GetKeyDown(KeyCode.Tab)) { both = false; rightSelected = !rightSelected; GameHud.Toast("moving " + Mode, 1f); }
            if (Input.GetKeyDown(KeyCode.Space)) { both = !both; GameHud.Toast("moving " + Mode, 1f); }

            var move = new Vector3(
                Axis(KeyCode.D, KeyCode.A),
                Axis(KeyCode.E, KeyCode.Q),
                Axis(KeyCode.W, KeyCode.S)) * (speed * Time.deltaTime);
            float turn = Axis(KeyCode.C, KeyCode.Z) * turnSpeed * Time.deltaTime;

            if (both || !rightSelected) Move(left, move, turn);
            if (both || rightSelected) Move(right, move, turn);
        }

        public Vector3 Clamp(Vector3 p) =>
            new Vector3(Mathf.Clamp(p.x, min.x, max.x), Mathf.Clamp(p.y, min.y, max.y), Mathf.Clamp(p.z, min.z, max.z));

        /// No gloves: the right hand follows the mouse over the counter (scroll = up/down), the left hand uses WASD/QE.
        void MouseMode()
        {
            if (cam == null) cam = Camera.main;
            if (right != null && cam != null)
            {
                float scroll = Input.mouseScrollDelta.y;
                if (Mathf.Abs(scroll) > 0.01f) right.position = Clamp(right.position + Vector3.up * scroll * 0.015f);
                var plane = new Plane(Vector3.up, new Vector3(0f, right.position.y + RightPinchOffset.y, 0f));
                var ray = cam.ScreenPointToRay(Input.mousePosition);
                if (plane.Raycast(ray, out float d))
                {
                    Vector3 pinch = ray.GetPoint(d);
                    Vector3 target = Clamp(new Vector3(pinch.x - RightPinchOffset.x, right.position.y, pinch.z - RightPinchOffset.z));
                    right.position = Vector3.Lerp(right.position, target, 1f - Mathf.Exp(-18f * Time.deltaTime));
                }
            }
            var move = new Vector3(Axis(KeyCode.D, KeyCode.A), Axis(KeyCode.E, KeyCode.Q), Axis(KeyCode.W, KeyCode.S)) * (speed * Time.deltaTime);
            Move(left, move, Axis(KeyCode.C, KeyCode.Z) * turnSpeed * Time.deltaTime);
        }

        void Move(Transform anchor, Vector3 delta, float turn)
        {
            if (anchor == null) return;
            anchor.position = Clamp(anchor.position + delta);
            anchor.Rotate(0f, turn, 0f, Space.World);
        }

        static float Axis(KeyCode plus, KeyCode minus) => (Input.GetKey(plus) ? 1f : 0f) - (Input.GetKey(minus) ? 1f : 0f);
    }
}
