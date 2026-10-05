using System;
using UnityEditor;
using UnityEngine;

namespace DeliveryDash.Editor
{
    public static class TownDeliveryChecks
    {
        static void Check(bool ok, string message)
        {
            if (!ok) throw new InvalidOperationException("Town delivery check failed: " + message);
        }
        [MenuItem("DeliveryDash/Checks/Validate town routes and orders")]
        public static void Validate()
        {
            int routes = 0;
            for (int seed = 0; seed < 500; seed++)
            {
                var shop = new DeliveryDash.TownAddress(Vector2Int.zero, 1);
                var book = new DeliveryDash.TownOrderBook();
                Check(book.Orders.Count == 0 && book.Money == 0, "start empty");
                int count = book.PickUp(seed, shop);
                Check(count >= 1 && count <= 3 && count == book.Orders.Count, "batch capacity");
                Check(book.PickUp(seed, new DeliveryDash.TownAddress(Vector2Int.zero, -1)) == 0, "houses cannot supply pizza");
                var replay = new DeliveryDash.TownOrderBook(); replay.PickUp(seed, shop);
                foreach (var order in book.Orders)
                {
                    Check(!DeliveryDash.TownMap.IsShop(seed, order.destination), "customer is a house");
                    Check(order.dueAt > book.Elapsed, "future deadline");
                    var network = DeliveryDash.TownMap.Network(seed);
                    Vector3 destination = DeliveryDash.TownMap.StopPosition(order.destination, Vector2Int.zero, seed);
                    Vector3 from = DeliveryDash.TownMap.StopPosition(shop, Vector2Int.zero, seed);
                    var route = network.Route(from, destination, Vector2Int.zero);
                    Check(route.Count > 1 && Vector3.Distance(route[0], from) < .01f && Vector3.Distance(route[route.Count - 1], destination) < .01f, "route endpoints");
                    for (int i = 0; i < route.Count; i += 4)
                    {
                        var road = network.NearestRoad(route[i], Vector2Int.zero, out float t, out Vector3 nearest);
                        Check(Vector3.Distance(route[i], nearest) < road.Width(t) + .25f, "route left the actual curved road");
                    }
                    var lot = network.Address(order.destination);
                    foreach (var road in network.RoadsNear(order.destination.block, 1))
                    {
                        float t = road.Project(lot.Building(order.destination.block), order.destination.block, out Vector3 nearest);
                        Check(Vector3.Distance(lot.Building(order.destination.block), nearest) - road.Width(t) >= 7.49f, "building blocks a road");
                    }
                    routes++;
                }
                for (int i = 0; i < count; i++) Check(book.Orders[i].destination.Equals(replay.Orders[i].destination), "same seed reproduces orders");
                Check(!book.Deliver(new DeliveryDash.TownAddress(new Vector2Int(88, 88), -1)), "wrong house cannot receive order");
                var first = book.Orders[0];
                Check(book.Deliver(first.destination) && book.Money == 12, "on-time payment");
                Check(!book.Deliver(first.destination), "no duplicate delivery");
                book.Reset(); book.PickUp(seed, shop);
                first = book.Orders[0];
                int charged = book.Advance(first.dueAt + .01f);
                int money = book.Money;
                Check(first.late && money == -charged * 5, "deadline deducts money");
                Check(book.Advance(0) == 0 && book.Money == money, "late fee charged only once");
                Check(book.Deliver(first.destination) && book.Money == money + 2, "late delivery remains possible");
                book.Advance(300);
                Check(book.Finished && book.PickUp(seed, shop) == 0, "shift expiry blocks pickup");
                if (book.Orders.Count > 0) Check(!book.Deliver(book.Orders[0].destination), "shift expiry blocks handoff");
                book.Reset(); Check(book.Orders.Count == 0 && book.Money == 0 && !book.Finished, "restart empty");
                for (int refill = 0; refill < 4; refill++) book.PickUp(seed, shop);
                Check(book.Orders.Count == 3 && book.PickUp(seed, shop) == 0, "refilling never exceeds capacity");
                for (int a = 0; a < 3; a++) for (int b = a + 1; b < 3; b++)
                    Check(!book.Orders[a].destination.Equals(book.Orders[b].destination), "batch destinations stay unique");
            }
            Debug.Log($"PASS: 500 town seeds, {routes} curved graph routes and roadside lot clearances, seeded batches, empty start, destination matching, capacity, per-order deadlines, one-time late fees, payments, expiry, and restart.");
        }

        static DeliveryDash.TownDeliveryWorld world;
        static DeliveryDash.TownDeliveryGame game;
        static DeliveryDash.CartFeelController cart;
        static DeliveryDash.TownAddress shop, customer;
        static int phase, loaded, paid;
        static bool oldBackground;
        static float oldScale;
        static double began, stageAt;
        static bool sawStop, sawOwner, sawTransfer, sawCustomer;

        [MenuItem("DeliveryDash/Checks/Play kart pickup and handoff")]
        public static void KartCheck(){UnityEngine.Object.FindFirstObjectByType<TownRideSelector>().Choose(1);PlayCheck();}
        [MenuItem("DeliveryDash/Checks/Play mower pickup and handoff")]
        public static void MowerCheck(){UnityEngine.Object.FindFirstObjectByType<TownRideSelector>().Choose(2);PlayCheck();}
        [MenuItem("DeliveryDash/Checks/Play town pickup and handoff")]
        public static void PlayCheck()
        {
            Check(EditorApplication.isPlaying, "enter Play mode first");
            world = UnityEngine.Object.FindFirstObjectByType<DeliveryDash.TownDeliveryWorld>();
            game = UnityEngine.Object.FindFirstObjectByType<DeliveryDash.TownDeliveryGame>();
            cart = UnityEngine.Object.FindFirstObjectByType<DeliveryDash.CartFeelController>();
            Check(world != null && game != null, "open TownDelivery");
            game.Restart(6553);
            Check(game.Book.Orders.Count == 0 && !game.HasVisiblePizza, "empty cart hides pizza");
            oldBackground = Application.runInBackground; oldScale = Time.timeScale;
            Application.runInBackground = true; Time.timeScale = 1; EditorApplication.isPaused = false;
            shop = new DeliveryDash.TownAddress(Vector2Int.zero, 1);
            // Choose a different shop from the initially selected, nearest one.
            for (int x = -12; x <= 12; x++) for (int z = -12; z <= 12; z++)
            {
                var candidate = new DeliveryDash.TownAddress(new Vector2Int(x, z), 1);
                if (candidate.block != Vector2Int.zero && DeliveryDash.TownMap.IsShop(world.Seed, candidate)) shop = candidate;
            }
            Check(shop.block != Vector2Int.zero, "alternative shop exists");
            sawOwner = sawTransfer = sawCustomer = false;
            int obstacles = 0;
            foreach(var collider in world.GetComponentsInChildren<BoxCollider>())
                if(collider.name.StartsWith("Obstacle -"))
                {
                    obstacles++;
                    Check(collider.Raycast(new Ray(collider.bounds.center + Vector3.up*5,Vector3.down),out var hit,10),"obstacle is solid");
                }
            Check(obstacles > 10,"streamed town contains visible solid obstacles");
            phase = 0; began = stageAt = EditorApplication.timeSinceStartup; sawStop = false;
            EditorApplication.update -= LiveTick; EditorApplication.update += LiveTick;
        }
        static void Place(Vector3 position, Vector3? forward = null)
        {
            cart.ResetAt(position + Vector3.up * .12f, Quaternion.LookRotation(forward ?? Vector3.forward));
            world.RefreshBlocks(); Physics.SyncTransforms();
            UnityEngine.Object.FindFirstObjectByType<DeliveryDash.ChaseCamera>().SetTarget(cart.transform);
        }
        static void End()
        {
            Application.runInBackground = oldBackground; Time.timeScale = oldScale;
            EditorApplication.update -= LiveTick;
        }
        static void Capture(string name)
        {
            System.IO.Directory.CreateDirectory("Captures");
            ScreenCapture.CaptureScreenshot("Captures/" + name + ".png");
        }
        static void CapturePortrait(DeliveryDash.TownShopOwner person,string filename)
        {
            var go = new GameObject("Handoff verification camera");
            var camera = go.AddComponent<Camera>(); camera.fieldOfView=48;
            Vector3 target = (person.transform.GetChild(0).position+cart.transform.position)*.5f + Vector3.up*1.1f;
            go.transform.position = target + person.transform.forward*6 + (cart.transform.position-person.transform.GetChild(0).position).normalized*7 + Vector3.up*4;
            go.transform.LookAt(target);
            BuildLookDev.Capture(camera,"Captures/"+filename+".png");
            UnityEngine.Object.Destroy(go);
        }
        static void LiveTick()
        {
            if (!EditorApplication.isPlaying || game == null) { End(); return; }
            try
            {
                Check(EditorApplication.timeSinceStartup - began < 45, "pickup/handoff timed out");
                if (cart.IsHandingOff)
                {
                    Check(cart.Speed < .01f, "handoff stops cart"); sawStop = true;
                }
                var owner = world.Owner(shop);
                if(phase == 2 && owner != null)
                {
                    if(owner.IsOutside && owner.HasTransferred && !sawOwner)
                    {
                        sawOwner = true; CapturePortrait(owner,"shop-owner-handoff");
                    }
                    sawTransfer |= owner.HasTransferred;
                }
                if(phase == 4)
                {
                    var receiver = world.Owner(customer);
                    if(receiver != null && receiver.HasTransferred && !sawCustomer)
                    { sawCustomer = true; CapturePortrait(receiver,"customer-handoff"); }
                }
                if (phase == 0 && EditorApplication.timeSinceStartup - stageAt > .5)
                {
                    Capture("town-empty-start"); phase = 1; stageAt = EditorApplication.timeSinceStartup;
                }
                else if (phase == 1 && EditorApplication.timeSinceStartup - stageAt > .4)
                {
                    Place(world.Position(shop) - world.StopDirection(shop) * 4, world.StopDirection(shop)); phase = 2;
                }
                else if (phase == 2 && game.Book.Orders.Count > 0)
                {
                    Check(sawOwner && sawTransfer,"owner walks out and transfers boxes before loading orders");
                    Check(sawStop && game.HasVisiblePizza, "pickup follows complete stop and loads visible pizza");
                    loaded = game.Book.Orders.Count; customer = game.Book.Orders[0].destination;
                    Check(!game.TryStop(new DeliveryDash.TownAddress(new Vector2Int(999, 999), -1)), "unassigned house rejected");
                    Capture("town-loaded-orders"); phase = 3; stageAt = EditorApplication.timeSinceStartup;
                }
                else if (phase == 3 && EditorApplication.timeSinceStartup - stageAt > .5)
                {
                    // Exercise lateness without waiting several real minutes.
                    var order = game.Book.Find(customer);
                    game.Book.Advance(order.dueAt - game.Book.Elapsed + .1f);
                    Check(order.late, "order expired"); paid = game.Book.Money;
                    game.Book.Advance(0); Check(game.Book.Money == paid, "no repeated late fee");
                    game.Select(customer); sawStop = false;
                    Place(world.Position(customer) - world.StopDirection(customer) * 4, world.StopDirection(customer)); phase = 4;
                }
                else if (phase == 4 && game.Book.Delivered == 1)
                {
                    Check(sawCustomer,"customer receives visible pizza");
                    Check(sawStop && game.Book.Orders.Count == loaded - 1 && game.Book.Find(customer) == null, "only designated order handed over");
                    Check(game.Book.Money == paid + 2, "late handoff pays reduced amount");
                    Capture("town-delivered"); phase = 5; stageAt = EditorApplication.timeSinceStartup;
                }
                else if (phase == 5 && EditorApplication.timeSinceStartup - stageAt > .5)
                {
                    Check(cart.Speed > 0 && !cart.IsServicing, "cart resumes automatically");
                    // Move across both axes and back; origin shifts retain address identities.
                    Place(new Vector3(840, 0, -840));
                    Check(world.Origin != Vector2Int.zero && world.LoadedBlocks == 25, "two-axis streaming and origin shift");
                    Vector3 stop = world.Position(shop);
                    Vector3 absolute = stop + DeliveryDash.TownRoadNetwork.Offset(world.Origin, Vector2Int.zero);
                    Check(Vector3.Distance(absolute, DeliveryDash.TownMap.StopPosition(shop, Vector2Int.zero)) < .01f, "stable shop coordinates after rebase");
                    game.Book.Advance(300); phase = 6; stageAt = EditorApplication.timeSinceStartup;
                }
                else if (phase == 6 && EditorApplication.timeSinceStartup - stageAt > .2)
                {
                    Check(game.Book.Finished && !cart.enabled, "shift expiry stops driving");
                    game.Restart(42);
                    Check(!game.Book.Finished && game.Book.Orders.Count == 0 && !game.HasVisiblePizza && cart.enabled && world.Seed == 42, "new town restart empty");
                    game.Restart(6553); End();
                    Debug.Log("PASS: solid streamed obstacles, chef walk and visible box transfer, customer receipt, live empty start, non-nearest shop, automatic dock/stop/pickup, visible cargo, assigned house handoff, late fee, automatic resume, two-axis rebase, 25-block cap, shift stop, restart.");
                }
            }
            catch (Exception e) { End(); Debug.LogException(e); }
        }
    }
}
