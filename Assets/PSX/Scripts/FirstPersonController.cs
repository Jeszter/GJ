using UnityEngine;
using UnityEngine.InputSystem;

namespace GJ.PSX
{
    // Plain WASD + mouse-look walker with a little head bob. Click to lock the cursor, Esc to free it.
    [RequireComponent(typeof(CharacterController))]
    public class FirstPersonController : MonoBehaviour
    {
        public Transform head;
        public float walkSpeed = 2.2f;
        public float runSpeed = 3.6f;
        public float lookSensitivity = 0.08f;
        public float gravity = -15f;
        public float bobAmount = 0.03f;
        public float bobFrequency = 7f;
        public float pushStrength = 1.6f;
        public bool invertY;
        public AudioClip stepClip;

        // Set while a menu is open: no looking, no walking.
        public static bool Frozen;

        CharacterController _cc;
        Vector3 _headRest;
        float _yaw, _pitch, _vy, _bob;
        AudioSource _steps;

        void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _yaw = transform.eulerAngles.y;
            if (head != null) _headRest = head.localPosition;
            _steps = gameObject.AddComponent<AudioSource>();
            _steps.playOnAwake = false;
            _steps.spatialBlend = 0f;
        }

        public void SetLook(float yaw, float pitch)
        {
            _yaw = yaw;
            _pitch = pitch;
            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
        }

        // Shove loose physics props (the bucket) out of the way by walking into them.
        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            var body = hit.rigidbody;
            if (body == null || body.isKinematic || hit.moveDirection.y < -0.3f) return;
            Vector3 push = new Vector3(hit.moveDirection.x, 0f, hit.moveDirection.z);
            body.linearVelocity = push * pushStrength;
        }

        void Update()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb == null || mouse == null || Frozen) return;

            // In the editor the cursor lock is lost on every click outside the game view; a click takes it back.
            if (mouse.leftButton.wasPressedThisFrame) Cursor.lockState = CursorLockMode.Locked;

            if (Cursor.lockState == CursorLockMode.Locked)
            {
                Vector2 look = mouse.delta.ReadValue() * lookSensitivity;
                _yaw += look.x;
                _pitch = Mathf.Clamp(_pitch + (invertY ? look.y : -look.y), -85f, 85f);
            }
            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);

            Vector2 input = Vector2.zero;
            if (kb.wKey.isPressed) input.y += 1f;
            if (kb.sKey.isPressed) input.y -= 1f;
            if (kb.dKey.isPressed) input.x += 1f;
            if (kb.aKey.isPressed) input.x -= 1f;
            input = Vector2.ClampMagnitude(input, 1f);

            float speed = kb.leftShiftKey.isPressed ? runSpeed : walkSpeed;
            Vector3 move = (transform.right * input.x + transform.forward * input.y) * speed;
            if (_cc.isGrounded && _vy < 0f) _vy = -1f;
            _vy += gravity * Time.deltaTime;
            move.y = _vy;
            _cc.Move(move * Time.deltaTime);

            if (head == null) return;
            float planar = new Vector2(_cc.velocity.x, _cc.velocity.z).magnitude;
            if (planar > 0.1f)
            {
                // A footstep each time the bob passes the bottom of its swing.
                float before = _bob;
                _bob += Time.deltaTime * bobFrequency * planar / walkSpeed;
                if (stepClip != null && Mathf.Floor(_bob / Mathf.PI) != Mathf.Floor(before / Mathf.PI))
                {
                    _steps.pitch = Random.Range(0.85f, 1.12f);
                    _steps.PlayOneShot(stepClip, kb.leftShiftKey.isPressed ? 0.55f : 0.38f);
                }
            }
            else _bob = Mathf.MoveTowards(_bob, Mathf.Round(_bob / Mathf.PI) * Mathf.PI, Time.deltaTime * 4f);
            head.localPosition = _headRest + new Vector3(Mathf.Cos(_bob) * bobAmount * 0.5f, Mathf.Abs(Mathf.Sin(_bob)) * bobAmount, 0f);
            head.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }
    }
}
