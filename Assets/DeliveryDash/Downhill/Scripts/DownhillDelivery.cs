using UnityEngine;

namespace DeliveryDash.Downhill
{
    public sealed class DownhillDelivery : MonoBehaviour
    {
        public float Elapsed { get; private set; }
        public bool Active { get; private set; }
        Transform box, support;
        Vector3 rest, start, destination, cartStart, park;
        Quaternion rotation, cartStartRotation, parkRotation;
        DownhillVisuals visuals;
        void Awake()
        {
            visuals = GetComponent<DownhillVisuals>();
            box = visuals.Pizza; support = box.parent; rest = box.localPosition; rotation = box.localRotation;
        }
        public void Begin(CourseGraph course)
        {
            Active = true; Elapsed = 0f;
            cartStart = transform.position;
            cartStartRotation = transform.rotation;
            CourseSample end = course.Sample(course.Length - 2f);
            parkRotation = Quaternion.LookRotation(end.Forward, Vector3.up);
            Vector3 right = Vector3.Cross(Vector3.up, end.Forward).normalized;
            park = end.Position + right * (end.Width * .5f - 1.6f) + Vector3.up * .12f;
            var customer = GameObject.Find("Waiting customer");
            destination = customer != null ? customer.transform.TransformPoint(new Vector3(0, 1.32f, -.25f)) : park + Vector3.up * 1.35f + right;
        }
        void LateUpdate()
        {
            if (!Active) return;
            Elapsed += Time.deltaTime;
            float arrival = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Elapsed / .65f));
            transform.position = Vector3.Lerp(cartStart, park, arrival);
            transform.rotation = Quaternion.Slerp(cartStartRotation, parkRotation, arrival);
            if (Elapsed < .65f) visuals.SettleForDelivery(Time.deltaTime);
            if (Elapsed >= .65f)
            {
                if (box.parent != null) { start = box.position; box.SetParent(null, true); }
                box.position = Vector3.Lerp(start, destination, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.65f, 1.05f, Elapsed)));
                box.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(transform.forward, Vector3.up), Vector3.up);
            }
        }
        public void ResetDelivery()
        {
            Active = false; Elapsed = 0f;
            box.SetParent(support, false); box.localPosition = rest; box.localRotation = rotation;
            visuals.enabled = true;
        }
        void OnDestroy() { if (box != null && box.parent == null) Destroy(box.gameObject); }
    }
}
