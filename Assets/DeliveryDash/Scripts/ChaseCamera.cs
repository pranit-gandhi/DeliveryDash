using UnityEngine;

namespace DeliveryDash
{
    /// <summary>
    /// A restrained chase view with route preview and obstacle avoidance.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class ChaseCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField, Min(0.5f)] private float distance = 3.25f;
        [SerializeField, Min(0f)] private float height = 2.1f;
        [SerializeField] private float sideOffset = 1.15f;
        [SerializeField, Min(0f)] private float lookAhead = 3.8f;
        [SerializeField, Min(0f)] private float lookHeight = 0.05f;
        [SerializeField, Min(0.01f)] private float positionSmoothTime = 0.16f;
        [SerializeField, Min(0.01f)] private float rotationSharpness = 12f;
        [SerializeField, Range(35f, 90f)] private float fieldOfView = 53f;
        [SerializeField] private LayerMask obstructionMask = ~0;
        [SerializeField, Min(0f)] private float obstructionRadius = 0.25f;
        [SerializeField, Min(0f)] private float obstructionPadding = 0.16f;

        private Camera view;
        private Vector3 positionVelocity;

        private void Awake()
        {
            view = GetComponent<Camera>();
            view.fieldOfView = fieldOfView;
        }

        private void Start()
        {
            if (target == null)
            {
                CartFeelController cart = FindFirstObjectByType<CartFeelController>();
                if (cart != null) SetTarget(cart.transform);
            }
        }

        private void LateUpdate()
        {
            if (target == null) return;

            Vector3 anchor = target.position + Vector3.up * lookHeight;
            Vector3 desiredPosition = target.position - target.forward * distance + target.right * sideOffset + Vector3.up * height;
            Vector3 fromAnchor = desiredPosition - anchor;
            float rayDistance = fromAnchor.magnitude;
            RaycastHit hit = default;
            bool blocked = rayDistance > 0.001f && Physics.SphereCast(
                anchor, obstructionRadius, fromAnchor / rayDistance, out hit,
                rayDistance, obstructionMask, QueryTriggerInteraction.Ignore);

            if (blocked)
            {
                desiredPosition = anchor + fromAnchor.normalized * Mathf.Max(0f, hit.distance - obstructionPadding);
                // Move inward at once so smoothing cannot carry the camera through a wall.
                if (Vector3.Distance(transform.position, anchor) > Vector3.Distance(desiredPosition, anchor))
                {
                    transform.position = desiredPosition;
                    positionVelocity = Vector3.zero;
                }
            }

            transform.position = Vector3.SmoothDamp(
                transform.position, desiredPosition, ref positionVelocity, positionSmoothTime);

            Vector3 lookPoint = anchor + target.forward * lookAhead;
            Vector3 lookDirection = lookPoint - transform.position;
            if (lookDirection.sqrMagnitude > 0.001f)
            {
                Quaternion desiredRotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation, desiredRotation, 1f - Mathf.Exp(-rotationSharpness * Time.deltaTime));
            }
        }

        public void SetTarget(Transform newTarget, bool snapNow = true)
        {
            target = newTarget;
            if (target == null || !snapNow) return;

            positionVelocity = Vector3.zero;
            transform.position = target.position - target.forward * distance + target.right * sideOffset + Vector3.up * height;
            Vector3 lookPoint = target.position + Vector3.up * lookHeight + target.forward * lookAhead;
            transform.rotation = Quaternion.LookRotation(lookPoint - transform.position, Vector3.up);
        }

        public void ShiftWorld(Vector3 delta)
        {
            transform.position -= delta;
        }

        private void OnValidate()
        {
            if (view == null) view = GetComponent<Camera>();
            if (view != null) view.fieldOfView = fieldOfView;
        }
    }
}


