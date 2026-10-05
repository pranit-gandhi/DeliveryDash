using System.Collections.Generic;
using UnityEngine;

namespace DeliveryDash
{
    [DefaultExecutionOrder(-100)]
    public sealed class TownDeliveryWorld : MonoBehaviour
    {
        [SerializeField] private int seed = 6553;
        [Header("Pizza shop distribution (applied on next rebuild)")]
        [Range(3,8)] public int pizzaShopDistrictSpan = 4;
        [Range(120,450)] public float pizzaShopMinimumSeparation = 250;
        [SerializeField] private CartFeelController runner;
        [SerializeField] private GameObject authoredDistrict;
        [SerializeField] private Transform[] houses;
        [SerializeField] private GameObject[] customerModels = new GameObject[5];
        [SerializeField] private Material roadMaterial, sidewalkMaterial, wallMaterial;
        private Material shopMaterial, customerMaterial, parkMaterial;
        private Material chefWhite, skin, customerSkin, wood, dark, cardboard, signMaterial;
        private readonly Dictionary<TownAddress, TownShopOwner> owners = new Dictionary<TownAddress, TownShopOwner>();
        public TownShopOwner Owner(TownAddress address) => owners.TryGetValue(address, out var owner) ? owner : null;
        private readonly Dictionary<Vector2Int, GameObject> blocks = new Dictionary<Vector2Int, GameObject>();
        private readonly Dictionary<Vector2Int, List<Mesh>> meshes = new Dictionary<Vector2Int, List<Mesh>>();
        private readonly Dictionary<TownAddress, GameObject> zones = new Dictionary<TownAddress, GameObject>();
        private readonly HashSet<TownAddress> activeCustomers = new HashSet<TownAddress>();
        private readonly List<Vector2Int> expired = new List<Vector2Int>();
        private Vector2Int origin;
        private ChaseCamera chase;
        private CartVisualResponse response;
        public TownRoadNetwork Network { get; private set; }
        public int Seed => seed;
        public Vector2Int Origin => origin;
        public int LoadedBlocks => blocks.Count;
        public bool IsDistrictLoaded(Vector2Int key)=>blocks.ContainsKey(key);
        public CartFeelController Runner => runner;
        public Vector3 Position(TownAddress address) => Network.Address(address).Stop(origin);
        public Vector3 StopDirection(TownAddress address) => Network.Address(address).Direction;
        public Vector2Int CurrentBlock => Network.CellAt(runner.transform.position, origin);

        public void Configure(CartFeelController cart, GameObject district, Transform[] templates, Material road, Material sidewalk, Material wall)
        {
            runner = cart; authoredDistrict = district; houses = templates;
            roadMaterial = road; sidewalkMaterial = sidewalk; wallMaterial = wall;
        }
        private void Start()
        {
            chase = FindFirstObjectByType<ChaseCamera>(); response = runner.GetComponent<CartVisualResponse>();
            authoredDistrict.SetActive(false);
            signMaterial = new Material(Shader.Find("DeliveryDash/Depth tested world text"));
            chefWhite = Tint("Chef linen", new Color(.96f,.92f,.81f));
            customerSkin = Tint("Customer complexion", new Color(.88f,.66f,.52f));
            skin = Tint("Owner skin", new Color(.65f,.38f,.23f));
            wood = Tint("Market timber", new Color(.38f,.22f,.1f));
            dark = Tint("Cart metal and chef trousers", new Color(.12f,.16f,.17f));
            cardboard = Tint("Pizza cardboard", new Color(.88f,.68f,.38f));
            shopMaterial = new Material(wallMaterial) { name = "Pizza shop orange", color = new Color(.95f, .43f, .12f) };
            customerMaterial = new Material(wallMaterial) { name = "Customer teal", color = new Color(.1f, .72f, .58f) };
            parkMaterial = new Material(wallMaterial) { name = "Town open ground", color = new Color(.43f, .48f, .36f) };
        }
        Material Tint(string label, Color color) => new Material(wallMaterial) { name = label, color = color };
        void Release(Vector2Int key)
        {
            GetComponent<TownLivingTown>()?.ReleaseDistrict(key);
            blocks[key].SetActive(false); Destroy(blocks[key]); blocks.Remove(key);
            foreach (Mesh mesh in meshes[key]) Destroy(mesh);
            meshes.Remove(key);
            owners.Remove(new TownAddress(key, 1)); owners.Remove(new TownAddress(key, -1));
            zones.Remove(new TownAddress(key, -1)); zones.Remove(new TownAddress(key, 1));
        }
        public void Rebuild(int newSeed)
        {
            foreach (var key in new List<Vector2Int>(blocks.Keys)) Release(key);
            GetComponent<TownLivingTown>()?.ResetActivity();
            zones.Clear(); activeCustomers.Clear(); origin = Vector2Int.zero; seed = newSeed;
            TownMap.ConfigureShops(seed,pizzaShopDistrictSpan,pizzaShopMinimumSeparation);
            Network = TownMap.Network(seed);
            var first = Network.Address(new TownAddress(Vector2Int.zero, 1));
            float t = Mathf.Max(.05f, first.t - .18f);
            runner.enabled = true;
            runner.ResetAt(first.road.Point(t, origin) + Vector3.up * .12f, Quaternion.LookRotation(first.road.Direction(t)));
            RefreshBlocks(); Physics.SyncTransforms();
            if (response != null) response.ResetMotion();
            if (chase != null) chase.SetTarget(runner.transform);
        }
        private void Update()
        {
            if (blocks.Count == 0) return;
            RefreshBlocks(false);
            if (runner.transform.position.y < -3) RecoverRunner();
        }
        public void RefreshBlocks(bool immediate = true)
        {
            Vector3 p = runner.transform.position;
            if (Mathf.Abs(p.x) >= 720 || Mathf.Abs(p.z) >= 624)
            {
                var shift = new Vector2Int(Mathf.RoundToInt(p.x / TownRoadNetwork.CellX), Mathf.RoundToInt(p.z / TownRoadNetwork.CellZ));
                Vector3 delta = TownRoadNetwork.Offset(shift, Vector2Int.zero); origin += shift;
                foreach (var root in blocks.Values) root.transform.position -= delta;
                runner.ShiftWorld(delta);
                if (response != null) response.ShiftWorld(delta);
                if (chase != null) chase.ShiftWorld(delta);
                Physics.SyncTransforms();
            }
            Vector2Int center = CurrentBlock; expired.Clear();
            foreach (var key in blocks.Keys)
                if (Mathf.Abs(key.x - center.x) > 2 || Mathf.Abs(key.y - center.y) > 2) expired.Add(key);
            foreach (var key in expired) Release(key);
            var needed=new List<Vector2Int>();
            for(int x=-2;x<=2;x++)for(int z=-2;z<=2;z++)
            {var key=center+new Vector2Int(x,z);if(!blocks.ContainsKey(key))needed.Add(key);}
            needed.Sort((a,b)=>(a-center).sqrMagnitude.CompareTo((b-center).sqrMagnitude));
            int budget=immediate?needed.Count:2;
            foreach(var key in needed){if(budget--<=0)break;blocks.Add(key,BuildBlock(key));}
        }
        public void SetCustomers(IEnumerable<TownOrder> orders)
        {
            activeCustomers.Clear(); foreach (var order in orders) activeCustomers.Add(order.destination);
            foreach (var pair in zones) pair.Value.SetActive(TownMap.IsShop(seed, pair.Key) || activeCustomers.Contains(pair.Key));
        }
        public void RecoverRunner()
        {
            var road = Network.NearestRoad(runner.transform.position, origin, out float t, out Vector3 p);
            Vector3 direction = road.Direction(t);
            if (Vector3.Dot(direction, runner.transform.forward) < 0) direction = -direction;
            runner.ResetAt(p + Vector3.up * .12f, Quaternion.LookRotation(direction));
            if (response != null) response.ResetMotion();
            if (chase != null) chase.SetTarget(runner.transform);
        }
        GameObject BuildBlock(Vector2Int key)
        {
            var root = new GameObject($"Road district {key.x}, {key.y}");
            root.transform.SetParent(transform, false); root.transform.position = TownRoadNetwork.Offset(key, origin);
            meshes[key] = new List<Mesh>();
            Box(root.transform, "Open ground", new Vector3(0, -.16f, 0), new Vector3(TownRoadNetwork.CellX, .2f, TownRoadNetwork.CellZ), parkMaterial, true);
            foreach (var end in Network.Outgoing(key))
            {
                var road = Network.Edge(key, end);
                Ribbon(root.transform, key, road, 2f, -.015f, sidewalkMaterial, "Street shoulders");
                Ribbon(root.transform, key, road, 0, .01f, roadMaterial, "Curving street");
                BuildObstacles(root.transform, key, road);
                GetComponent<TownEnvironmentDetails>()?.Road(root.transform,key,Network,road);
            }
            Vector3 junction = Network.Node(key, key);
            float radius = Network.JunctionRadius(key);
            Disc(root.transform, key, junction, radius + 2, -.012f, sidewalkMaterial, "Junction pavement");
            Disc(root.transform, key, junction, radius, .024f, Network.Plaza(key) ? sidewalkMaterial : roadMaterial,
                Network.Plaza(key) ? "Open plaza" : "Angled junction");
            for (int side = -1; side <= 1; side += 2) BuildLot(root.transform, key, new TownAddress(key, side));
            GetComponent<TownLivingTown>()?.BuildDistrict(root.transform,key);
            GetComponent<TownEnvironmentDetails>()?.FinishDistrict(root.transform);
            TownStaticGeometry.Combine(root.transform);
            return root;
        }
        void BuildLot(Transform root, Vector2Int key, TownAddress address)
        {
            var lot = Network.Address(address);
            var frame = new GameObject("Roadside lot " + address.Label).transform;
            frame.SetParent(root, false); frame.localPosition = lot.road.Point(lot.t, key);
            frame.localRotation = Quaternion.LookRotation(lot.Direction);
            int side = address.side; bool shop = TownMap.IsShop(seed, address);
            float width = lot.road.Width(lot.t);
            uint hash = EndlessRoadLayout.Hash(seed, ((long)key.x << 32) ^ (uint)key.y, (uint)(side + 40));
            var template = houses[(int)(hash % (uint)houses.Length)];
            Transform house = Instantiate(template, frame);
            house.localPosition = new Vector3(side * lot.setback, .08f, 0);
            house.localRotation = Quaternion.Euler(0, System.Math.Sign(template.localPosition.x) == side ? 0 : 180, 0);
            house.localScale = new Vector3(lot.scale, .85f + (hash % 41) / 100f, lot.scale);
            house.gameObject.SetActive(true);
            Box(frame, "Building collision", new Vector3(side * lot.setback, 4, 0), new Vector3(8.2f * lot.scale, 8, 13 * lot.scale), wallMaterial, true, false);
            float front = lot.setback - 4.1f * lot.scale;
            Box(frame, "Entrance approach", new Vector3(side * (width + front) * .5f, -.005f, 0), new Vector3(front - width + 2, .04f, 7), sidewalkMaterial, true);
            if (shop)
            {
                Box(frame, "Pizza shop awning", new Vector3(side * (front - .7f), 3.1f, 0), new Vector3(2, .22f, 7), shopMaterial);
                var owner = new GameObject("Pizza store owner " + address.Label).AddComponent<TownShopOwner>();
                owner.transform.SetParent(frame, false);
                // The dedicated shop entrance faces the bay, keeping the walk on the approach.
                Box(frame, "Shop doorway", new Vector3(side * (front - .04f), 1.25f, 0), new Vector3(.08f, 2.5f, 1.35f), dark);
                owner.Configure(new Vector3(side * (front - .45f), .06f, 0), new Vector3(side * (width - 1.25f), .06f, 0), side,
                    chefWhite, skin, shopMaterial, dark, cardboard);
                owners.Add(address, owner);
                var icon=TownShopOwner.Part(frame,"Pizza beacon crust",PrimitiveType.Cylinder,new Vector3(side*(front-.3f),5.8f,0),new Vector3(2.1f,.09f,2.1f),cardboard);
                icon.localRotation=Quaternion.Euler(0,0,90);
                var cheese=TownShopOwner.Part(frame,"Pizza beacon cheese",PrimitiveType.Cylinder,new Vector3(side*(front-.42f),5.8f,0),new Vector3(1.8f,.04f,1.8f),shopMaterial);cheese.localRotation=Quaternion.Euler(0,0,90);
                for(int pepper=0;pepper<5;pepper++){float a=pepper*Mathf.PI*.4f;TownShopOwner.Part(frame,"Pizza beacon topping",PrimitiveType.Sphere,new Vector3(side*(front-.48f),5.8f+Mathf.Sin(a)*.55f,Mathf.Cos(a)*.55f),Vector3.one*.23f,dark);}
                Sign(frame, "PIZZA\n" + address.Label, new Vector3(side * front, 4.6f, 0), side, .105f, Color.white);
            }
            else
            {
                int style = (int)(hash % 5);
                var customer = new GameObject("Customer " + TownShopOwner.CustomerNames[style] + " " + address.Label).AddComponent<TownShopOwner>();
                customer.transform.SetParent(frame,false);
                Box(frame,"Customer doorway",new Vector3(side*(front-.04f),1.25f,0),new Vector3(.08f,2.5f,1.35f),dark);
                customer.Configure(new Vector3(side*(front-.45f),.06f,0),new Vector3(side*(width-1.25f),.06f,0),side,
                    chefWhite,customerSkin,shopMaterial,dark,cardboard,style,
                    customerModels != null && style < customerModels.Length ? customerModels[style] : null);
                owners.Add(address,customer);
                Sign(frame, TownShopOwner.CustomerNames[style]+"\n"+address.Label, new Vector3(side * front, 3.6f, 0), side, .08f, Color.white);
            }
            var details=GetComponent<TownEnvironmentDetails>();
            if(details!=null){details.BeginDistrict(seed);details.Lot(frame,key,Network,address,front,lot.scale,hash,house);}
            var zone = new GameObject(shop ? "Pizza pickup zone" : "Assigned customer zone"); zone.transform.SetParent(frame, false);
            Box(zone.transform, "Pull over marking", new Vector3(side * (width - 1.25f), .035f, 0), new Vector3(3.5f, .035f, 6), shop ? shopMaterial : customerMaterial);
            Sign(zone.transform, shop ? "PICK UP" : "DELIVER", new Vector3(side * (width + 1.2f), 1.7f, 0), side, .07f, shop ? new Color(1, .7f, .25f) : Color.cyan);
            if(shop)for(int approach=-1;approach<=1;approach+=2)for(int i=0;i<3;i++)
            {
                var arrow=new GameObject("Pickup approach chevron").transform;arrow.SetParent(zone.transform,false);arrow.localPosition=new Vector3(side*(width-1.25f),.065f,approach*(4+i*1.5f));
                for(int wing=-1;wing<=1;wing+=2){var mark=TownShopOwner.Part(arrow,"Arrow wing",PrimitiveType.Cube,new Vector3(wing*.28f,0,0),new Vector3(.13f,.025f,.85f),chefWhite);mark.localRotation=Quaternion.Euler(0,wing*approach*45,0);}
            }
            zone.SetActive(shop || activeCustomers.Contains(address)); zones.Add(address, zone);
        }
        // Deterministic candidates; exclusion zones protect all nearby lots, not only this district's shops.
        public static bool ObstacleAllowed(TownRoadNetwork network, TownRoadNetwork.Road road, float t, Vector2Int key)
        {
            Vector3 p = road.Point(t, key);
            for (int x=-1;x<=1;x++) for(int z=-1;z<=1;z++)
            {
                var cell = key + new Vector2Int(x,z);
                if(Vector3.Distance(p,network.Node(cell,key)) < network.JunctionRadius(cell)+9) return false;
                for(int side=-1;side<=1;side+=2)
                {
                    var lot = network.Address(new TownAddress(cell,side));
                    if(Vector3.Distance(p,lot.road.Point(lot.t,key)) < 15) return false;
                }
            }
            return true;
        }
        public void BuildObstacles(Transform root, Vector2Int key, TownRoadNetwork.Road road, float frontageT = -1, int frontageSide = 1)
        {
            uint hash = EndlessRoadLayout.Hash(seed, ((long)key.x << 32) ^ (uint)key.y, (uint)(900+road.b.x-key.x));
            int count=1;
            for(int i=0;i<count;i++)
            {
                if(frontageT<0 && hash%3!=0)continue;
                float t = frontageT>=0?frontageT:.18f+((hash>>(i*9))%1000)/1000f*.64f;
                if(!ObstacleAllowed(Network,road,t,key)) continue;
                int kind = frontageT>=0?(int)(hash%2):2, side = frontageT>=0?frontageSide:((hash >> (i+2))&1)==0 ? -1 : 1;
                var obstacle = new GameObject(kind==0 ? "Obstacle - stacked market crates" : kind==1 ? "Obstacle - parked produce cart" : "Obstacle - construction verge").transform;
                obstacle.SetParent(root,false);
                float offset = side*(road.Width(t)-.5f);
                obstacle.localPosition = road.Point(t,key)+Vector3.Cross(Vector3.up,road.Direction(t))*offset;
                obstacle.localRotation = Quaternion.LookRotation(road.Direction(t));
                var collider = obstacle.gameObject.AddComponent<BoxCollider>();
                collider.center = new Vector3(0,.7f,0); collider.size = new Vector3(kind==2?1.5f:2.1f,1.4f,kind==1?3.2f:2.6f);
                if(kind==0)
                {
                    Crate(obstacle,new Vector3(0,.43f,-.65f)); Crate(obstacle,new Vector3(0,.43f,.65f)); Crate(obstacle,new Vector3(0,1.23f,.1f));
                }
                else if(kind==1)
                {
                    Box(obstacle,"Wooden wagon bed",new Vector3(0,.8f,0),new Vector3(1.8f,.2f,2.8f),wood);
                    for(int a=-1;a<=1;a+=2) for(int b=-1;b<=1;b+=2)
                    {
                        var wheel=TownShopOwner.Part(obstacle,"Wagon wheel",PrimitiveType.Cylinder,new Vector3(a*.96f,.4f,b*.92f),new Vector3(.7f,.1f,.7f),dark);
                        wheel.localRotation=Quaternion.Euler(0,0,90);
                    }
                    Crate(obstacle,new Vector3(0,1.25f,0));
                    Box(obstacle,"Wagon handle",new Vector3(0,1.2f,-1.5f),new Vector3(1.7f,.12f,.12f),dark);
                }
                else
                {
                    Box(obstacle,"Roadworks gravel",new Vector3(0,.14f,0),new Vector3(1.5f,.25f,2.6f),dark);
                    for(int j=-1;j<=1;j+=2){Box(obstacle,"Safety barrier",new Vector3(0,.8f,j),new Vector3(1.6f,.22f,.12f),shopMaterial);Box(obstacle,"Barrier foot",new Vector3(0,.4f,j),new Vector3(.1f,.8f,.2f),dark);}
                }
            }
        }
        void Crate(Transform root, Vector3 position)
        {
            Box(root,"Crate body",position,new Vector3(1.65f,.75f,1.1f),wood);
            for(int i=-1;i<=1;i++)
                Box(root,"Crate slat",position+new Vector3(0,i*.22f,.565f),new Vector3(1.68f,.13f,.055f),cardboard);
            for(int i=-1;i<=1;i++) TownShopOwner.Part(root,"Market tomatoes",PrimitiveType.Sphere,position+new Vector3(i*.43f,.43f,0),Vector3.one*.32f,shopMaterial);
        }
        void Ribbon(Transform root, Vector2Int key, TownRoadNetwork.Road road, float shoulder, float y, Material material, string label)
        {
            float ra = Network.JunctionRadius(road.a), rb = Network.JunctionRadius(road.b);
            float trimA = Mathf.Sqrt(ra * ra - Mathf.Pow(road.widthA, 2)) - 1;
            float trimB = Mathf.Sqrt(rb * rb - Mathf.Pow(road.widthB, 2)) - 1;
            float start = trimA / road.Length, finish = 1 - trimB / road.Length;
            const int count = TownRoadNetwork.Samples;
            var vertices = new Vector3[(count + 1) * 2]; var uv = new Vector2[vertices.Length]; var triangles = new int[count * 6];
            for (int i = 0; i <= count; i++)
            {
                float t = Mathf.Lerp(start, finish, i / (float)count);
                Vector3 p = road.Point(t, key); p.y = y;
                Vector3 right = Vector3.Cross(Vector3.up, road.Direction(t)) * (road.Width(t) + shoulder);
                vertices[i * 2] = p - right; vertices[i * 2 + 1] = p + right;
                uv[i * 2] = new Vector2(0, road.Arc(t) * .1f); uv[i * 2 + 1] = new Vector2(1, road.Arc(t) * .1f);
                if (i == count) continue;
                int v = i * 2, k = i * 6;
                triangles[k] = v; triangles[k + 1] = v + 2; triangles[k + 2] = v + 1;
                triangles[k + 3] = v + 1; triangles[k + 4] = v + 2; triangles[k + 5] = v + 3;
            }
            Surface(root, key, vertices, triangles, uv, material, label);
        }
        void Disc(Transform root, Vector2Int key, Vector3 p, float radius, float y, Material material, string label)
        {
            const int count = 48; var vertices = new Vector3[count + 2]; var uv = new Vector2[vertices.Length]; var triangles = new int[count * 3];
            p.y = y; vertices[0] = p; uv[0] = Vector2.one * .5f;
            for (int i = 0; i <= count; i++)
            {
                float angle = i * Mathf.PI * 2 / count;
                Vector3 offset = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                vertices[i + 1] = p + offset * radius; uv[i + 1] = new Vector2(offset.x, offset.z) * .5f + Vector2.one * .5f;
                if (i == count) continue;
                triangles[i * 3] = 0; triangles[i * 3 + 1] = i + 2; triangles[i * 3 + 2] = i + 1;
            }
            Surface(root, key, vertices, triangles, uv, material, label);
        }
        void Surface(Transform root, Vector2Int key, Vector3[] vertices, int[] triangles, Vector2[] uv, Material material, string label)
        {
            var mesh = new Mesh { name = label }; mesh.vertices = vertices; mesh.triangles = triangles; mesh.uv = uv; mesh.RecalculateNormals(); mesh.RecalculateBounds(); meshes[key].Add(mesh);
            var go = new GameObject(label); go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh; go.AddComponent<MeshRenderer>().sharedMaterial = material;
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
        }
        void Sign(Transform parent, string text, Vector3 position, int side, float size, Color color)
        {
            var go = new GameObject(text); go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localRotation = Quaternion.Euler(0, side * 90, 0);
            var label = go.AddComponent<TextMesh>(); label.text = text; label.fontSize = 48; label.characterSize = size;
            label.anchor = TextAnchor.MiddleCenter; label.alignment = TextAlignment.Center; label.color = color;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); go.AddComponent<TownWorldText>().sharedFontMaterial=signMaterial; go.GetComponent<MeshRenderer>().sharedMaterial=signMaterial;
        }
        static void Box(Transform parent, string label, Vector3 p, Vector3 size, Material material, bool solid = false, bool visible = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = label; go.transform.SetParent(parent, false); go.transform.localPosition = p; go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = material; go.GetComponent<Renderer>().enabled = visible;
            if (!solid) { go.GetComponent<Collider>().enabled = false; Destroy(go.GetComponent<Collider>()); }
        }
        private void OnDestroy()
        {
            foreach (var list in meshes.Values) foreach (var mesh in list) if (mesh != null) Destroy(mesh);
            foreach (var material in new[] { chefWhite, skin, customerSkin, wood, dark, cardboard, signMaterial }) if (material != null) Destroy(material);
            if (shopMaterial != null) Destroy(shopMaterial); if (customerMaterial != null) Destroy(customerMaterial); if (parkMaterial != null) Destroy(parkMaterial);
        }
    }
}
