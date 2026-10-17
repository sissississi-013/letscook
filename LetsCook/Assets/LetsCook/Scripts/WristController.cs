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

        bool both = true;
        bool rightSelected = true;

        public string Mode => both ? "both hands" : (rightSelected ? "right hand" : "left hand");

        void Update()
        {
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

        void Move(Transform anchor, Vector3 delta, float turn)
        {
            if (anchor == null) return;
            var p = anchor.position + delta;
            anchor.position = new Vector3(Mathf.Clamp(p.x, min.x, max.x), Mathf.Clamp(p.y, min.y, max.y), Mathf.Clamp(p.z, min.z, max.z));
            anchor.Rotate(0f, turn, 0f, Space.World);
        }

        static float Axis(KeyCode plus, KeyCode minus) => (Input.GetKey(plus) ? 1f : 0f) - (Input.GetKey(minus) ? 1f : 0f);
    }
}
