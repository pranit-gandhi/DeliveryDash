using System.Collections.Generic;
using UnityEngine;

namespace DeliveryDash
{
    [DefaultExecutionOrder(50)]
    [RequireComponent(typeof(TownDeliveryWorld))]
    public sealed class TownDeliveryGame : MonoBehaviour
    {
        [SerializeField] private CartFeelController runner;
        [SerializeField] private Transform pizzaPivot;
        private TownDeliveryWorld world;
        private ChaseCamera chase;
        private readonly TownOrderBook book = new TownOrderBook();
        private Transform pizzaOriginalParent;private Vector3 pizzaOriginalPosition;private Quaternion pizzaOriginalRotation;
        private readonly List<GameObject> extraPizzas = new List<GameObject>();
        private TownAddress selected;
        private TownAddress? pending, lastStop;
        private TownShopOwner servingOwner;
        private string notice;
        private float noticeUntil, routeRefresh, shopRefresh, targetChanged;
        private bool manualTarget;
        public bool ManualTarget => manualTarget;
        private List<Vector3> route = new List<Vector3>();
        private Vector2Int routeOrigin;
        private GUIStyle headingStyle, textStyle, smallStyle, iconStyle;
        public TownOrderBook Book => book;
        public TownAddress Selected => selected;
        public bool HasVisiblePizza => pizzaPivot != null && pizzaPivot.gameObject.activeSelf;
        public bool HasPendingStop => pending.HasValue;
        public void Configure(CartFeelController cart, Transform pizza) { runner = cart; pizzaPivot = pizza; }

        private void Start()
        {
            world = GetComponent<TownDeliveryWorld>(); chase = FindFirstObjectByType<ChaseCamera>();
            pizzaOriginalParent=pizzaPivot.parent;pizzaOriginalPosition=pizzaPivot.localPosition;pizzaOriginalRotation=pizzaPivot.localRotation;
            // Extra loaded pizzas ride in the basket; the last one stays on the courier's hand.
            for (int i = 0; i < 2; i++)
            {
                Transform extra = Instantiate(pizzaPivot, runner.transform);
                extra.name = "Loaded basket pizza " + (i + 2);
                extra.localPosition = new Vector3(0, .95f + i * .12f, .42f);
                extra.localRotation = Quaternion.identity;
                extra.gameObject.SetActive(false); extraPizzas.Add(extra.gameObject);
            }
            Restart(world.Seed);
            runner.GetComponent<TownRideSelector>()?.Open();
        }
        public void Restart(int seed)
        {
            if (servingOwner != null) servingOwner.Finish(); servingOwner = null;
            pending = lastStop = null;
            var rides=runner.GetComponent<TownRideSelector>();if(rides!=null)rides.Choose(rides.Selected);
            book.Reset(); world.Rebuild(seed); RefreshCargo();
            manualTarget = false; selected = NearestShop(); routeRefresh = 0; shopRefresh = Time.time + 2;
            route = world.Network.Route(runner.transform.position, world.Position(selected), world.Origin); routeOrigin = world.Origin;
            Notify("Your cart is empty. Pull into an orange pizza-shop bay to collect orders.", 9);
        }
        private void Notify(string text, float seconds = 4) { notice = text; noticeUntil = Time.time + seconds; }
        public void Select(TownAddress address) { selected = address; manualTarget = true; routeRefresh = 0; }
        public void AutomaticGuidance() { manualTarget=false; SelectBestTarget(); }
        public void RefreshShopGuidance()
        {
            if(manualTarget || book.Orders.Count>0 || pending.HasValue || Time.time-targetChanged<5)return;
            var candidate=NearestShop();
            float current=TownMap.RouteLength(world.Network.Route(runner.transform.position,world.Position(selected),world.Origin));
            float next=TownMap.RouteLength(world.Network.Route(runner.transform.position,world.Position(candidate),world.Origin));
            if(!TownMap.IsShop(world.Seed,selected) || next+Mathf.Max(30,current*.2f)<current)
            {selected=candidate;targetChanged=Time.time;routeRefresh=0;}
        }
        private void Update()
        {
            if(runner.GetComponent<TownRideSelector>() is TownRideSelector chooser && chooser.Choosing)return;
            if (Input.GetKeyDown(KeyCode.R)) { Restart(world.Seed); runner.GetComponent<TownRideSelector>()?.Open(); return; }
            if (Input.GetKeyDown(KeyCode.N)) { Restart(unchecked(world.Seed * 1664525 + 1013904223)); runner.GetComponent<TownRideSelector>()?.Open(); return; }
            if (Input.GetKeyDown(KeyCode.Tab)) CycleTarget();
            if (Input.GetKeyDown(KeyCode.G)) AutomaticGuidance();
            if(Time.time>=shopRefresh){shopRefresh=Time.time+2;RefreshShopGuidance();}
            if (book.Finished) { pending = null; if (servingOwner != null) servingOwner.Finish(); servingOwner = null; runner.CancelService(); runner.enabled = false; return; }
            int fees = book.Advance(Time.deltaTime);
            if (fees > 0) Notify($"Customer deadline missed: -${fees * TownOrderBook.LateFee}. You can still deliver.");
            if (book.Finished)
            {
                pending = null; if (servingOwner != null) servingOwner.Finish(); servingOwner = null; runner.CancelService(); runner.enabled = false; return;
            }
            if (pending.HasValue && !runner.IsServicing)
            {
                TownAddress address = pending.Value; pending = null;
                if (servingOwner != null) servingOwner.Finish(); servingOwner = null;
                if (runner.ServiceSucceeded)
                {
                    if (TownMap.IsShop(world.Seed, address))
                    {
                        int loaded = book.PickUp(world.Seed, address);
                        Notify($"Loaded {loaded} pizza{(loaded == 1 ? "" : "s")}. Departing — customer deadlines are on your map.", 6);
                    }
                    else if (book.Deliver(address)) Notify("Pizza delivered. Continue to your next customer.");
                    RefreshCargo();
                    if(!manualTarget || selected.Equals(address)) { manualTarget=false; SelectBestTarget(); }
                }
                else Notify("Couldn't pull over. Leave the bay and try again.");
            }
            if (lastStop.HasValue && HorizontalDistance(runner.transform.position, world.Position(lastStop.Value)) > 11) lastStop = null;
            if (!pending.HasValue && !runner.IsServicing) TryNearbyStop();
            if (Time.time >= routeRefresh || routeOrigin != world.Origin)
            {
                route = world.Network.Route(runner.transform.position, world.Position(selected), world.Origin);
                routeOrigin = world.Origin;
                routeRefresh = Time.time + .2f;
            }
        }
        public bool TryStop(TownAddress address)
        {
            if (book.Finished || pending.HasValue || runner.IsServicing || (lastStop.HasValue && lastStop.Value.Equals(address))) return false;
            bool shop = TownMap.IsShop(world.Seed, address);
            if ((shop && book.Orders.Count >= TownOrderBook.Capacity) || (!shop && book.Find(address) == null)) return false;
            Vector3 target = world.Position(address);
            if (!runner.IsGrounded || Mathf.Abs(runner.transform.position.y - target.y) > .75f || HorizontalDistance(runner.transform.position, target) > 2.8f) return false;
            servingOwner = world.Owner(address);
            pending = address; lastStop = address;
            runner.BeginService(target, servingOwner != null ? servingOwner.HoldSeconds : 1.6f, world.StopDirection(address));
            if (servingOwner != null) servingOwner.Begin(runner, shop ? book.PickupCount(world.Seed, address) : 1);
            return true;
        }
        private void TryNearbyStop()
        {
            // Every eligible shop works, even if a different shop is selected on the map.
            Vector2Int block = world.CurrentBlock;
            for (int x = -1; x <= 1; x++) for (int z = -1; z <= 1; z++)
            {
                var address = new TownAddress(block + new Vector2Int(x, z), 1);
                if (TownMap.IsShop(world.Seed, address) && TryStop(address)) return;
            }
            foreach (var order in book.Orders) if (TryStop(order.destination)) return;
        }
        static float HorizontalDistance(Vector3 a, Vector3 b) { a.y = b.y = 0; return Vector3.Distance(a, b); }
        public void RefreshRideCargo(){if(pizzaOriginalParent!=null)RefreshCargo();}
        private void RefreshCargo()
        {
            bool alternate=runner.GetComponent<TownRideSelector>()?.Selected>0;
            pizzaPivot.SetParent(alternate?runner.transform:pizzaOriginalParent,false);pizzaPivot.localPosition=alternate?new Vector3(0,.88f,-.93f):pizzaOriginalPosition;pizzaPivot.localRotation=alternate?Quaternion.identity:pizzaOriginalRotation;
            runner.GetComponent<CartVisualResponse>()?.SetPizzaRestRotation(pizzaPivot.localRotation);
            for(int i=0;i<extraPizzas.Count;i++)extraPizzas[i].transform.localPosition=alternate?new Vector3(0,1.01f+i*.13f,-.93f):new Vector3(0,.95f+i*.12f,.42f);
            pizzaPivot.gameObject.SetActive(book.Orders.Count > 0);
            for (int i = 0; i < extraPizzas.Count; i++) extraPizzas[i].SetActive(book.Orders.Count > i + 1);
            world.SetCustomers(book.Orders);
        }
        private TownAddress NearestShop()
        {
            var best = new TownAddress(Vector2Int.zero, 1); float distance = float.PositiveInfinity;
            int radius=TownMap.ShopSearchRadius(world.Seed);var candidates=new List<TownAddress>();
            for (int x=-radius;x<=radius;x++)for(int z=-radius;z<=radius;z++)
            {var address=new TownAddress(world.CurrentBlock+new Vector2Int(x,z),1);if(TownMap.IsShop(world.Seed,address))candidates.Add(address);}
            candidates.Sort((a,b)=>(world.Position(a)-runner.transform.position).sqrMagnitude.CompareTo((world.Position(b)-runner.transform.position).sqrMagnitude));
            foreach(var address in candidates)
            {
                if(Vector3.Distance(runner.transform.position,world.Position(address))>=distance)break;
                float length=TownMap.RouteLength(world.Network.Route(runner.transform.position,world.Position(address),world.Origin));
                if(length<distance){distance=length;best=address;}
            }
            return best;
        }
        private void SelectBestTarget()
        {
            if (book.Orders.Count == 0) selected = NearestShop();
            else
            {
                TownOrder soonest = book.Orders[0];
                foreach (var order in book.Orders) if (order.dueAt < soonest.dueAt) soonest = order;
                selected = soonest.destination;
            }
            routeRefresh = 0;
        }
        private void CycleTarget()
        {
            if (book.Orders.Count == 0)
            {
                var shops=new List<TownAddress>();int radius=TownMap.ShopSearchRadius(world.Seed);
                for(int x=-radius;x<=radius;x++)for(int z=-radius;z<=radius;z++)
                {var a=new TownAddress(world.CurrentBlock+new Vector2Int(x,z),1);if(TownMap.IsShop(world.Seed,a))shops.Add(a);}
                shops.Sort((a,b)=>Vector3.SqrMagnitude(world.Position(a)-runner.transform.position).CompareTo(Vector3.SqrMagnitude(world.Position(b)-runner.transform.position)));
                if(shops.Count>0){int i=shops.FindIndex(a=>a.Equals(selected));Select(shops[(i+1)%Mathf.Min(6,shops.Count)]);}return;
            }
            for (int i = 0; i < book.Orders.Count; i++)
                if (book.Orders[i].destination.Equals(selected))
                {
                    Select(i + 1 < book.Orders.Count ? book.Orders[i + 1].destination : NearestShop()); return;
                }
            Select(book.Orders[0].destination);
        }
        private void OnGUI()
        {
            if (world == null) return;
            if (headingStyle == null)
            {
                headingStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
                textStyle = new GUIStyle(GUI.skin.label) { fontSize = 16 };
                smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 13 };
                iconStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                iconStyle.normal.textColor = new Color(.06f, .12f, .12f);
            }
            Matrix4x4 old = GUI.matrix;
            float scale = Mathf.Min(Screen.width / 1100f, Screen.height / 680f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            float width = Screen.width / scale, height = Screen.height / scale;
            GUI.Box(new Rect(16, 16, 440, book.Orders.Count == 0 ? 125 : 105 + book.Orders.Count*31), GUIContent.none);
            GUI.Label(new Rect(30, 26, 415, 30), $"TOWN DELIVERY     {Mathf.FloorToInt(book.Remaining / 60):0}:{Mathf.FloorToInt(book.Remaining % 60):00}", headingStyle);
            GUI.Label(new Rect(30, 61, 415, 24), $"Pizzas {book.Orders.Count}/3    Delivered {book.Delivered}    Balance ${book.Money}", textStyle);
            if (book.Orders.Count == 0)
                GUI.Label(new Rect(30, 94, 415, 35), "Empty • Follow the orange P to a pickup bay.", textStyle);
            for (int i = 0; i < book.Orders.Count; i++)
            {
                TownOrder order = book.Orders[i];
                int remaining = Mathf.CeilToInt(order.dueAt - book.Elapsed);
                string time = order.late ? "LATE (-$5 charged)" : remaining + "s left";
                if (GUI.Button(new Rect(30, 98 + i * 31, 410, 27), $"{(selected.Equals(order.destination) ? "> " : "")}{i + 1}. House {order.destination.Label}   |   {time}")) Select(order.destination);
            }
            DrawMap(new Rect(width - 244, 16, 228, 228));
            string targetName = TownMap.IsShop(world.Seed, selected) ? "Pizza shop" : "House";
            GUI.Box(new Rect(width - 244, 250, 228, 76), GUIContent.none);
            GUI.Label(new Rect(width - 234, 255, 212, 45), $"{targetName} {selected.Label}\n{TownMap.RouteLength(route):0} m  |  {(manualTarget ? "Pinned" : "Auto route")}", smallStyle);
            if(GUI.Button(new Rect(width-234,300,208,22),"G: Auto route   |   Tab: change")) AutomaticGuidance();
            GUI.Box(new Rect(16, height - 48, 910, 32), $"{runner.Speed*3.6f:0} km/h   |   Hold W / ↑ to accelerate   |   A/D: steer   Tab: route   R: choose ride   N: new town");
            string status = runner.IsServicing ? (runner.IsHandingOff ? "Loading / handoff — please wait" : "Docking — slowing into the bay") : (Time.time < noticeUntil ? notice : "");
            if(!runner.IsServicing && !book.Finished && TownMap.IsShop(world.Seed,selected) && HorizontalDistance(runner.transform.position,world.Position(selected))<45)
                status="Approaching pickup • Pull in here to collect pizzas → orange bay";
            if (!string.IsNullOrEmpty(status))
            {
                GUI.Box(new Rect(16, height - 96, width - 32, 36), GUIContent.none);
                GUI.Label(new Rect(28, height - 91, width - 56, 28), status, textStyle);
            }
            if (book.Finished)
            {
                Rect r = new Rect((width - 480) / 2, (height - 240) / 2, 480, 240);
                GUI.Box(r, GUIContent.none);
                GUI.Label(new Rect(r.x + 30, r.y + 25, 420, 32), "SHIFT COMPLETE", headingStyle);
                GUI.Label(new Rect(r.x + 30, r.y + 73, 430, 80), $"{book.Delivered} pizzas delivered  /  Balance ${book.Money}\n{book.LateFees} late fees  /  {book.Orders.Count} undelivered\nOn time: +$12   Late fee: -$5   Late handoff: +$2", textStyle);
                if (GUI.Button(new Rect(r.x + 30, r.y + 177, 195, 38), "Choose ride / restart (R)")) {Restart(world.Seed);runner.GetComponent<TownRideSelector>()?.Open();}
                if (GUI.Button(new Rect(r.x + 250, r.y + 177, 195, 38), "New town (N)")) {Restart(unchecked(world.Seed * 1664525 + 1013904223));runner.GetComponent<TownRideSelector>()?.Open();}
            }
            GUI.matrix = old;
        }
        private void DrawMap(Rect rect)
        {
            GUI.Box(rect, GUIContent.none);
            GUI.BeginGroup(new Rect(rect.x + 8, rect.y + 8, rect.width - 16, rect.height - 16));
            const float span = 540;
            float size = rect.width - 16;
            Vector3 center = runner.transform.position;
            System.Func<Vector3, Vector2> point = p => new Vector2(size / 2 + (p.x - center.x) * size / span, size / 2 - (p.z - center.z) * size / span);
            Fill(new Rect(0, 0, size, size), new Color(.09f, .15f, .16f));
            foreach (var road in world.Network.RoadsNear(world.CurrentBlock, 3))
                for (int i = 4; i <= TownRoadNetwork.Samples; i += 4)
                    DrawLine(point(road.Point((i - 4) / (float)TownRoadNetwork.Samples, world.Origin)),
                        point(road.Point(i / (float)TownRoadNetwork.Samples, world.Origin)), new Color(.38f, .44f, .44f), road.Width(.5f) * 2 * size / span);
            for (int i = 1; i < route.Count; i++) DrawLine(point(route[i - 1]), point(route[i]), new Color(1, .88f, .28f), 2.5f);
            for (int x = -3; x <= 3; x++) for (int z = -3; z <= 3; z++)
            {
                var address = new TownAddress(world.CurrentBlock + new Vector2Int(x, z), 1);
                if (TownMap.IsShop(world.Seed, address)) MapIcon(point(world.Position(address)), address, "P", new Color(1, .53f, .17f), size);
            }
            if(TownMap.IsShop(world.Seed,selected)) MapIcon(point(world.Position(selected)),selected,"P",new Color(1,.53f,.17f),size);
            for (int i = 0; i < book.Orders.Count; i++)
            {
                var order = book.Orders[i];
                MapIcon(point(world.Position(order.destination)), order.destination, (i + 1).ToString(), order.late ? new Color(1, .3f, .3f) : new Color(.2f, 1, .75f), size);
            }
            Vector2 cart = point(runner.transform.position);
            Vector3 forward = runner.transform.forward;
            Vector2 direction = new Vector2(forward.x, -forward.z);
            Vector2 right = new Vector2(-direction.y, direction.x);
            DrawLine(cart - direction * 5 + right * 4, cart + direction * 7, Color.white, 3);
            DrawLine(cart - direction * 5 - right * 4, cart + direction * 7, Color.white, 3);
            GUI.Label(new Rect(4, 1, size - 8, 20), "N ↑   P shops   1–3 customers", smallStyle);
            GUI.EndGroup();
        }
        private void MapIcon(Vector2 p, TownAddress address, string label, Color color, float size)
        {
            bool outside = p.x < 8 || p.x > size - 8 || p.y < 24 || p.y > size - 8;
            if(outside && !selected.Equals(address))return;
            p.x = Mathf.Clamp(p.x, 10, size - 10); p.y = Mathf.Clamp(p.y, 30, size - 10);
            Rect r = new Rect(p.x - 9, p.y - 9, 18, 18);
            if (selected.Equals(address)) Fill(new Rect(r.x - 2, r.y - 2, 22, 22), Color.yellow);
            Fill(r, color);
            GUI.Label(r, label, iconStyle);
            if (GUI.Button(r, GUIContent.none, GUIStyle.none)) Select(address);
            if (outside) GUI.Label(new Rect(r.x, r.y + 16, 30, 18), "...", smallStyle);
        }
        static void Fill(Rect rect, Color color)
        {
            Color old = GUI.color; GUI.color = color; GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = old;
        }
        static void DrawLine(Vector2 a, Vector2 b, Color color, float thickness)
        {
            if ((b - a).sqrMagnitude < .01f) return;
            // Draw in group-local coordinates. Rotating GUI.matrix inside a clipped
            // group displaces lines when the HUD is scaled to the Game view.
            if (Mathf.Abs(a.y - b.y) < .01f)
                Fill(new Rect(Mathf.Min(a.x, b.x), a.y - thickness / 2, Mathf.Abs(b.x - a.x), thickness), color);
            else if (Mathf.Abs(a.x - b.x) < .01f)
                Fill(new Rect(a.x - thickness / 2, Mathf.Min(a.y, b.y), thickness, Mathf.Abs(b.y - a.y)), color);
            else
            {
                int steps = Mathf.Clamp(Mathf.CeilToInt(Vector2.Distance(a, b)), 1, 256);
                for (int i = 0; i <= steps; i++)
                {
                    Vector2 p = Vector2.Lerp(a, b, i / (float)steps);
                    Fill(new Rect(p.x - thickness / 2, p.y - thickness / 2, thickness, thickness), color);
                }
            }
        }
    }
}
