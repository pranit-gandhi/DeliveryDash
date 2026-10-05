# DeliveryDash

A Unity 6 shopping-cart delivery game. Start empty, collect timed orders from pizza shops, choose streets through a procedural town, and deliver as many pizzas as possible during a shift.

## Play the town delivery game

1. Open this folder with Unity **6000.2.14f1**.
2. Open `Assets/DeliveryDash/Scenes/TownDelivery.unity` and press Play.
3. Click the Game view. The cart moves automatically; hold A/D or the arrow keys to turn freely through intersections.
4. Start with **zero pizzas**. Orange **P** icons are shops. Choose any shop, not necessarily the nearest, and steer into its orange curbside bay.
5. The cart automatically approaches, slows, stops for approximately 1.6 seconds, loads a batch of 1–3 orders (capacity three), and resumes. Each order has a specific house address and its own deadline.
6. Follow the yellow minimap route to a numbered customer. Pull into the turquoise bay at that house to stop and hand over its pizza. Other houses do not accept it. Refill at any shop when you have room.

| Control | Action |
| --- | --- |
| A / D or Left / Right | Turn left/right; hold to turn around |
| Tab | Cycle active customers and a nearby shop |
| Click map P / customer number or order row | Select the highlighted route |
| R | Restart the current town with an empty cart |
| N | Generate a different seeded town and restart empty |

The **top-right minimap** is north-up. The white arrow is your position and heading, orange P markers are pizza shops, numbered green markers are assigned houses, and red markers are overdue customers. The yellow line follows streets and intersections. Stops beyond the displayed area appear at the map edge.

The starting balance is $0. A five-minute shift ends with deliveries, earnings and outstanding orders. An on-time handoff earns **$12**. Exceeding a customer's deadline deducts **$5 once**, even while you carry other orders. The order remains deliverable for **$2**. The balance can become negative. Timers continue while loading and unloading; pickup deadlines begin when the pizzas are loaded. Numbers are initial balance settings, not final tuning.

Only steering is needed for driving. Entering an eligible bay within 2.8 metres starts the automatic approach/handoff; driving past a shop in the middle of the street does not load pizzas. A serviced stop stays locked until you move at least 11 metres away, preventing repeated loading while stationary.

## Town implementation and checks

- `TownMap.cs`: stable shop/house addresses. `TownRoadNetwork.cs`: seeded junctions, curved streets, plazas, lot placement, and graph-based route guidance.
- `TownDeliveryWorld.cs`: seeded shop/facade placement, house colliders, pickup/delivery bays, 25 streamed road districts with owned runtime meshes, and two-axis origin shifting to keep coordinates small during long exploration.
- `TownOrderBook.cs`: finite pizza inventory, batches, per-customer deadlines, payments and one-time late fees.
- `TownDeliveryGame.cs`: pickup/handoff flow, cargo visibility, minimap, route selection, HUD and shift results.
- `CartFeelController.cs`: a separate town driving mode with unrestricted heading, slower turns, automatic docking and short handoffs. Older scenes retain their downhill controls.

Unity menu: `DeliveryDash/Open town delivery` opens the saved scene (or creates it from the playground if absent).

Checks under `DeliveryDash/Checks`:

- **Validate town routes and orders** tests 500 seeds, street-only routes, reproducible batch assignments, capacity, wrong-house rejection, deadlines, one-time late fees, payment and reset.
- **Play town pickup and handoff** tests actual docking physics, a non-nearest shop, visible inventory, assigned-house handoff, resume, origin shifting and restart. It repositions the courier between stops and advances the clock for the late-fee case; it is not an autonomous navigation or human steering test. Background simulation and time scale are restored afterward. Screenshots go to `Captures/town-*.png`.

The town now uses a connected sparse road graph: displaced junctions, gentle curved streets, angled intersections, missing side streets, diagonal links, alternate loops, varying widths, and occasional open plazas. A hidden integer indexing system keeps streaming deterministic; the roads themselves do not follow those cell boundaries. Houses have varied roadside setbacks, scale, spacing, and orientation. Shop/customer bays and departure headings follow their own street tangent.

The minimap renders the actual graph, and route guidance follows sampled curves through connected junctions. Deadline estimates use those route lengths. The graph keeps a guaranteed connected backbone; route search is bounded to 4,096 expansions, so extreme long-distance guidance beyond a normal shift is not verified. Road generation and house references remain separate, so the current visual assets can be replaced without rewriting navigation.

Additional Unity checks:
- **Validate organic graph topology**: 200 seeds, connectivity, alternate loops, junction variety, width and curvature bounds, deterministic geometry, and roadside lot availability.
- **Inspect and capture organic town** (Play mode): samples actual road/junction colliders and delivery bays, then saves `Captures/organic-gameplay.png`, `Captures/organic-gameplay-hud.png`, and `Captures/organic-layout-overview.png`. Fog is temporarily disabled for the elevated diagnostic view only.

This is a layout prototype for review, not an approved art direction. Streets remain level. Longer human driving sessions, advanced traffic/navigation behavior, final environment/character art, audio, deadline tuning, and browser profiling remain open; a limited living-town traffic preview is described below. There is no verified Web build.

## Earlier endless-road prototype

`EndlessRun.unity` remains available separately with its downhill generator and gate-delivery rules. `FeelPlayground.unity` is the original authored scene. Their controls and checks remain separate from the town game.

## Procedural road

- Reproducible sections indexed by seed, with smooth changes in road center and width, a continuous downhill grade, alternating challenge/recovery sections, side barriers, and split passages.
- Sections are 48 metres long. Six ahead and two behind are retained by default, for at most nine loaded sections including the current section.
- Geometry and colliders share the same sampled road surface. Old runtime meshes are released with their sections; authored house meshes/materials are shared.
- Every 768 metres the world shifts back near the origin. Distance, seed, camera, collision recovery, and visual motion history are preserved.
- Grounded stalls and off-road falls recover to the road. This is an initial generator with a small set of repeated obstacle archetypes, not unlimited unique art or a fully validated stunt/fork system.

Select **Endless delivery shift** outside Play mode to set its seed and section look-ahead. Code is in `Assets/DeliveryDash/Scripts/EndlessRoadLayout.cs`, `EndlessRoadGenerator.cs`, and `DeliveryShift.cs`.

## Checks

Unity menu commands:

- `DeliveryDash/Checks/Validate 500 seeds and shift rules`: checks 40,000 section layouts, downhill grade, steering slope limits, nominal passage width, joins, origin shifts, and scoring/timer rules.
- `DeliveryDash/Checks/Probe streaming in Play mode`: crosses 41 sections using diagnostic repositioning; checks road colliders at joins, two origin shifts, bounded section count, and restart.
- `DeliveryDash/Checks/Play a complete shift automatically`: runs the real controller without steering at 3x speed, checks delivery, expiry and restart, and captures results under `Captures/`. Temporarily enables background simulation and restores the previous settings afterward.

Automated checks do not replace keyboard playtesting. Browser performance, difficulty balance, final character art, audio, moving obstacles, and richer route choices still need work. No Web build is verified.

## Shop owners, customers, and obstacles

Town shops now have chefs who walk to your stopped cart and bring out pizza boxes. Assigned customers walk out to receive their pizza. The cart waits for the handoff and then resumes; timers keep running. Customers now use five free photo-fitted, skinned FBX models of Dario Amodei, Donald Trump, Elon Musk, Sam Altman, and Jensen Huang. Facial geometry and texture come from attributed photographs; skull, hair, clothes and body are approximate. These are work-in-progress likeness studies, not accurate scans or final character art. The original primitive customers remain as a fallback if a model is unassigned.

Crate stacks and parked produce wagons narrow street edges; planted islands split the roadway. These have solid colliders, so hitting them slows or blocks the cart. Junctions and delivery approaches are kept clear. They are streamed with the town and reproduced by its seed.

`DeliveryDash/Checks/Capture customer cast` captures all five customer variants in Play mode. The live pickup check also saves `Captures/shop-owner-handoff.png` and `Captures/customer-handoff.png`.

The editable likeness pipeline is documented in `Tools/CharacterLikeness/README.md`; licenses and photo credits are in `Assets/DeliveryDash/Art/Characters/Likeness/CREDITS.md`. Choose **DeliveryDash > Install free customer likenesses** outside Play mode to restore the model assignments in TownDelivery. Cast capture also saves individual `Captures/customer-*-unity.png` closeups rendered in Unity.

## Neighborhood environment preview

The neighborhood detail layer originally covered only districts (0,0) and (1,0); it now runs in every streamed district. It includes facades and side windows, balconies, gutters, roof vents, pizza signage and striped awnings, menu boards, cafe seating, gardens, mailboxes, a furnished pocket plaza, trees, market stall, bicycles, benches, bins, streetlights, curbs, drains, repairs and crossing stripes. All 29 props are original reusable prefabs under `Assets/DeliveryDash/Art/Neighborhood`, with shared materials.

Open TownDelivery and Play to visit the starting pizza shop. Actual Unity review images are `Captures/neighborhood-driving.png`, `Captures/neighborhood-plaza.png`, and `Captures/neighborhood-overhead.png`. `DeliveryDash > Checks > Review neighborhood details` reruns clearance checks and captures the preview. The detail layer uses 79 enabled renderers after per-material mesh combining; current editor performance remains dominated by the existing town and characters. See `Captures/neighborhood-performance.txt`; no standalone performance claim is made.

## Living-town playable preview (October 2026)

Open `Assets/DeliveryDash/Scenes/TownDelivery.unity` and press Play. Choose **1 shopping cart**, **2 pocket pizza kart**, or **3 pizza garden mower** using the buttons or number keys. A/D or Left/Right steer. Hold **W / Up Arrow** to accelerate; release to settle back to cruising speed. The HUD shows speed. R opens ride selection for a fresh shift; N generates a new town and opens selection. The shift timer pauses while choosing. Pickups and handoffs override the throttle; shift expiry disables driving.

The richer layout and facade/street detail layers now run in every streamed district, including distant procedural areas. It adds eleven imported house variants, three original shop/apartment fronts, roadside parked vehicles, public gardens with crossed paths, benches and beds, and small groves. Geometry follows the existing irregular road graph. Building gaps and setbacks vary by seed. The same scenery rules apply after leaving and revisiting a district.

The bounded activity pool contains at most **24 pedestrians, 8 cars/vans and 4 cyclists**. People walk on pavement, cross near junctions, use park paths, sit, or gather at shops. Characters use articulated procedural poses and six clothing variants. Traffic follows graph edges, slows near nodes, yields to nearby actors/player, probes static obstacles, and recycles after prolonged blockage. Walkers try lateral pavement detours before turning around. Player impacts trigger a short comic vehicle wobble/message. The existing bounded activity pool follows the player within nearby loaded districts; district coordinates and actors account for rebasing. Park and gathering registrations are removed when their district unloads. Expensive character renderers are hidden beyond 85 m.

The two additional rides share the courier, orders and docking controller. Loaded boxes sit on their rear cargo rack. Ride speed/acceleration/turn rates are configured in `TownRideSelector.rides`; population limits and replaceable prefab arrays are on `TownLivingTown`. `DeliveryDash > Install living town preview` rebuilds the generated reusable assets and assignments outside Play mode. Static scenery is combined per material/district, with dynamic rigs and world text excluded.

Free asset sources, licenses, preview images and credits are recorded in `Assets/DeliveryDash/ThirdParty/Kenney/SOURCES.md`. Kenney Car Kit and City Kit Suburban are CC0; the crowd reuses the existing free Quaternius base. The mower, shopfronts and activity systems are original. The five existing named customers and their photo credits remain unchanged.

### Verification and limits

- `DeliveryDash > Checks > Review living town`: three ride acceleration/release/docking checks, 18 preview bay clearance checks, pedestrian/car/cyclist movement and a bounded 36-actor pool. Saves driving-height, overhead, park and ride screenshots plus sampled Unity motion frames under `Captures/`.
- `DeliveryDash > Checks > Exercise traffic collision and recovery`: actual CharacterController impact reaction and deliberately obstructed traffic recovery.
- `DeliveryDash > Checks > Play town pickup and handoff`: empty start, non-nearest shop, visible chef/customer transfers, designated deliveries, late fees, resume, rebasing, 25-district streaming cap, expiry and restart.
- `DeliveryDash > Checks > Profile living town rendering`: fixed 1280×720 camera with identical paused simulation, comparing character culling off/on. Results in `Captures/living-performance.txt` measure Editor Camera.Render CPU submission/wall time, not standalone frame rate or GPU time. Editor counters can include other views and shadow passes. Culling reduced reported geometry; the measured CPU cost did not improve. No standalone-build performance result is claimed.

This is a playable, replaceable-art preview, not a finished town or final visual direction. Crowd motion, hair/appearance diversity, foot-to-pedal/seat alignment, traffic turning and crossing behavior still need polish. Navigation uses local avoidance and recovery rather than a complete pedestrian route planner or traffic-light simulation. Some parks/blocks still need more authored variety, back-lane connections and richer ground cover. Recovery and spawning are kept away from the player but are not yet guaranteed to occur outside every camera view. Scenery now streams across the town; population remains capped at 36 actors. Standalone profiling and longer human driving sessions are still needed before increasing that cap.

`DeliveryDash > Checks > Verify detailed town streaming` visits the origin, distant districts (7,6) and (-8,-7), then revisits (7,6). It checks rich scenery, street/facade details, delivery bay clearance, activity following the player, rebasing, 25 district registrations and the bounded actor pool. Distant-area captures and the report are saved as `Captures/streamed-town-*`.

## Town layout and delivery clarity revision — October 2026

This section supersedes the older preview-only descriptions above. The same neighborhood generator now runs throughout streamed districts. Road surfaces use main streets (8.4–9.4 m total width), residential streets (6.4–7.4 m), and narrower diagonal lanes (5.6–6.6 m). Junction radii derive from incident road widths. The connected, curved seeded graph, address identities, minimap routes and traffic paths share these changes.

Pizza shops are substantially less frequent: the default uses one candidate per 4×4 district sector, rejects conflicting candidates within **250 m of another shop bay**, and guarantees the starting shop. In the Inspector, `TownDeliveryWorld.pizzaShopDistrictSpan` controls frequency (larger means fewer), and `pizzaShopMinimumSeparation` controls spacing; restart to apply. Shop selection is deterministic independently of streamed objects. Other businesses include bakeries, groceries, cafés, bookstores, laundries and workshops.

When empty, guidance evaluates nearby shops by road-route distance every two seconds. It changes the automatic target only for a worthwhile improvement (at least 30 m or 20%) and with a five-second switch cooldown. Clicking a map icon/order or pressing **Tab** pins a destination. **G**, or the Auto route button, restores automatic guidance. Any eligible shop still accepts pickups, regardless of the selected destination. Pizza emblems, bay arrows, approach cues and docking/loading/departure messages explain service. Acceleration still yields to automatic handoffs.

Commercial frontage, housing, courtyard clusters and connected gardens are placed before woodland infill, with smoothly varying land use. Shared house palettes vary roofs; business buildings have side windows and roof fittings. Grocery/workshop loading obstacles are anchored to their storefronts, with occasional protected roadside works elsewhere. Delivery bays, crossings and intersections retain exclusion zones. Normal streaming builds at most two new districts per update; explicit rebuilds/diagnostic teleports still synchronously fill the 25-district window.

Activity remains bounded at 24 pedestrians, eight cars and four cyclists. Spawning favors nearby streets and avoids the current camera view and a 22 m player exclusion radius. Shop groups, park walkers/joggers, seated people, headwear and clothing variation improve everyday activity. Cyclists pedal, wheel rotation uses wheel circumference, and vehicles steer toward their movement path and avoid immediate U-turns when another route exists. Local avoidance, yielding and timed jam recovery remain in place.

All three selectable rides, W/Up acceleration, five named customers, empty starts, multiple timed orders, late fees, owner/customer handoffs and the five-minute delivery goal are retained. New assets in this revision are original; existing free asset provenance remains in `Assets/DeliveryDash/ThirdParty/Kenney/SOURCES.md` and the character credits.

Actual Unity captures: `Captures/living-town-driving.png`, `Captures/living-town-overhead.png`, `Captures/streamed-town-distant-street.png`, and `Captures/streamed-town-distant-overhead.png`. `Captures/living-town-motion-preview.mp4` is an eight-second, time-sampled animation preview, not uninterrupted keyboard gameplay or an FPS demonstration.

Validation menus under **DeliveryDash > Checks** include sparse shops/guidance, organic graph topology, routes/orders, detailed streaming/revisits, living-town controls/activity, traffic collisions/recovery, and live pickup/handoff checks for each ride. The standalone macOS preview is built to `Builds/DeliveryDash.app`. `TownBenchmark` is opt-in via `--town-benchmark --report-dir <directory>`; normal play does not run it. Measured reports live in `Captures/town-editor-benchmark.txt` and `Captures/town-standalone-benchmark.txt`. They distinguish fixed-camera CPU render submission from whole-loop intervals, and are not isolated GPU measurements or sustained driving benchmarks.

Remaining limits: the town is still a procedural prototype with repeated modular assets and level terrain. Courts, woodland and street arrangements need broader artistic variety. Crowd locomotion and foot-to-pedal alignment remain approximate; this is local obstacle avoidance, not a full pedestrian navigation or traffic-light system. Recovery can remove a blocked vehicle in view even though new spawns are kept out of view. Startup and diagnostic teleports still cause generation stalls; normal streaming needs longer driving/low-end-hardware profiling. No Web build is verified, and longer human route/difficulty playtesting is still needed.

A full physical route is also checked by **Drive complete pickup delivery route**. The latest run drove 260.5 m, collected an order, completed its assigned customer handoff and earned $12 in 40.7 wall seconds. It supplies diagnostic heading inputs while the regular movement, collisions, traffic, deadlines and service systems run; it does not teleport between stops. This is still not a substitute for human steering/difficulty testing. See `Captures/full-driving-run.txt`.

Final fixed-camera measurements on Apple M4 Max (Unity 6000.2.14f1, 1280×720, 30 warmup + 180 samples): Editor `Camera.Render` wall time **6.30 ms mean / 8.16 ms p95**, with background loop intervals near **100 ms**; standalone **5.13 / 5.88 ms** render wall time and **15.01 / 16.09 ms** whole-loop interval. The normal game camera is also active. These are stationary diagnostics, not sustained driving FPS or isolated GPU timings. There is no pre-change standalone baseline and no demonstrated CPU improvement over the saved Editor baseline. Full details and known limits are in `Captures/town-final-verification.txt`.
