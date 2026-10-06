using UnityEngine;

namespace DeliveryDash.Pixel
{
    [RequireComponent(typeof(PixelRunSession))]
    public sealed class PixelSceneView : MonoBehaviour
    {
        [SerializeField] private Sprite[] townPlates;
        [SerializeField] private Sprite courierCart;
        [SerializeField] private SpriteRenderer town;
        [SerializeField] private SpriteRenderer rider;
        [SerializeField] private Transform riderRoot;
        private PixelRunSession run;
        private Mesh heroMesh;
        private Material heroMaterial;
        private Vector3[] restVertices;
        private Vector3[] liveVertices;
        private SpriteRenderer contactShadow;
        private float phase;

        public void Bind(Sprite[] plates, Sprite hero, SpriteRenderer backgroundRenderer,
            SpriteRenderer heroRenderer, Transform motionRoot)
        {
            townPlates = plates; courierCart = hero; town = backgroundRenderer;
            rider = heroRenderer; riderRoot = motionRoot;
        }

        private void Awake()
        {
            run = GetComponent<PixelRunSession>();
            if (town != null && townPlates.Length > 0)
            {
                town.sprite = townPlates[0];
                town.transform.localScale = new Vector3(18f / town.sprite.bounds.size.x,
                    6.5f / town.sprite.bounds.size.y, 1f);
                town.transform.localPosition = new Vector3(0f, 1.5f, 0f);
            }
            BuildHeroMesh();
            BuildContactShadow();
        }

        private void BuildHeroMesh()
        {
            if (rider == null || courierCart == null) return;
            const int columns = 20, rows = 24;
            restVertices = new Vector3[(columns + 1) * (rows + 1)];
            liveVertices = new Vector3[restVertices.Length];
            var uv = new Vector2[restVertices.Length];
            var indices = new int[columns * rows * 6];
            Rect rect = courierCart.rect;
            Vector2 size = rect.size / courierCart.pixelsPerUnit;
            for (int y = 0; y <= rows; y++)
                for (int x = 0; x <= columns; x++)
                {
                    int i = y * (columns + 1) + x;
                    float u = x / (float)columns, v = y / (float)rows;
                    restVertices[i] = new Vector3((u - .5f) * size.x, (v - .5f) * size.y, 0f);
                    uv[i] = new Vector2((rect.x + u * rect.width) / courierCart.texture.width,
                        (rect.y + v * rect.height) / courierCart.texture.height);
                }
            int k = 0;
            for (int y = 0; y < rows; y++)
                for (int x = 0; x < columns; x++)
                {
                    int a = y * (columns + 1) + x;
                    indices[k++] = a; indices[k++] = a + columns + 1; indices[k++] = a + 1;
                    indices[k++] = a + 1; indices[k++] = a + columns + 1; indices[k++] = a + columns + 2;
                }
            heroMesh = new Mesh { name = "Courier cart connected response" };
            heroMesh.MarkDynamic(); heroMesh.vertices = restVertices; heroMesh.uv = uv;
            heroMesh.triangles = indices; heroMesh.RecalculateBounds();
            var filter = rider.gameObject.AddComponent<MeshFilter>();
            filter.sharedMesh = heroMesh;
            var meshRenderer = rider.gameObject.AddComponent<MeshRenderer>();
            heroMaterial = new Material(Shader.Find("Sprites/Default"));
            heroMaterial.mainTexture = courierCart.texture;
            meshRenderer.sharedMaterial = heroMaterial; meshRenderer.sortingOrder = 20;
            rider.enabled = false;
        }

        private void BuildContactShadow()
        {
            var texture = new Texture2D(96, 24, TextureFormat.RGBA32, false);
            texture.name = "Cart contact shadow"; texture.filterMode = FilterMode.Point;
            var pixels = new Color32[96 * 24];
            for (int y = 0; y < 24; y++)
                for (int x = 0; x < 96; x++)
                {
                    float r = Mathf.Pow((x - 47.5f) / 47f, 2f) + Mathf.Pow((y - 11.5f) / 11f, 2f);
                    pixels[y * 96 + x] = new Color32(32, 31, 43, (byte)(Mathf.Clamp01(1f-r) * 92f));
                }
            texture.SetPixels32(pixels); texture.Apply();
            var go = new GameObject("Four wheel contact shadow"); go.transform.SetParent(transform, false);
            contactShadow = go.AddComponent<SpriteRenderer>();
            contactShadow.sprite = Sprite.Create(texture, new Rect(0,0,96,24), new Vector2(.5f,.5f), 32f);
            contactShadow.sortingOrder = 19;
        }

        private void LateUpdate()
        {
            if (run == null || run.Cart == null || run.Plan == null || riderRoot == null) return;
            var cart = run.Cart;
            bool moving = run.State == PixelSessionState.Running;
            if (moving) phase += Time.deltaTime * cart.SpeedMetersPerSecond * 5f;
            float wheelRattle = moving ? Mathf.Sin(phase) * .012f : 0f;
            float heroX = Mathf.Clamp(cart.LateralOffset * .135f, -1.7f, 1.7f);
            float lift = cart.BumpDisplacement * .55f;
            float compression = cart.LastImpactStrength * .04f;
            riderRoot.localPosition = new Vector3(heroX, -1.85f + lift + wheelRattle - compression, 0f);
            riderRoot.localRotation = Quaternion.identity;
            if (town != null)
            {
                float heading = run.Plan.CenterX(cart.DistanceMeters + 70f) - run.Plan.CenterX(cart.DistanceMeters);
                town.transform.localPosition = new Vector3(-heading * .035f - heroX * .03f, 1.5f, 0f);
            }
            if (contactShadow != null)
            {
                contactShadow.transform.localPosition = new Vector3(heroX - .23f, -4.05f, 0f);
                contactShadow.transform.localScale = new Vector3(1f + lift*.18f, .5f, 1f);
                contactShadow.color = new Color(1f,1f,1f,1f - Mathf.Clamp01(lift)*.4f);
            }
            if (heroMesh == null) return;
            float width = courierCart.rect.width / courierCart.pixelsPerUnit;
            float height = courierCart.rect.height / courierCart.pixelsPerUnit;
            float bank = Mathf.Clamp(-cart.CartLean * .24f, -3.2f, 3.2f);
            Quaternion roll = Quaternion.Euler(0,0,bank);
            Vector3 pivot = new Vector3(0f,-height*.4f,0f);
            for (int i = 0; i < restVertices.Length; i++)
            {
                Vector3 p = restVertices[i];
                float bodyWeight = Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(-height*.10f,height*.30f,p.y));
                Vector3 bent = roll * (p - pivot) + pivot;
                bent.x += bodyWeight * cart.RiderLag * .075f;
                float boxWeight = Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(0f,width*.14f,p.x)) *
                    Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(height*.09f,height*.25f,p.y));
                bent.y -= boxWeight * (p.x-width*.24f) * Mathf.Sin((bank-cart.PizzaTilt*.22f)*Mathf.Deg2Rad);
                liveVertices[i] = bent;
            }
            heroMesh.vertices = liveVertices; heroMesh.RecalculateBounds();
        }

        private void OnDestroy()
        {
            if (heroMesh != null) Destroy(heroMesh);
            if (heroMaterial != null) Destroy(heroMaterial);
        }
    }
}
