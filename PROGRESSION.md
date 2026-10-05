# DeliveryDash progression

| Status | Current state |
| --- | --- |
| Phase | Irregular road graph and town layout review |
| Playable state | TownDelivery starts empty and supports shop pickups, assigned houses, individual deadlines and late fees, automatic curbside handoffs, and a top-right route minimap. Automated rules and live stop checks pass. |
| Next step | Review the actual Unity layout captures, keyboard-playtest curves and junctions, then choose replaceable environment assets and profile a Web build. |

## 2026-10-02: kickoff audit

- Read `PROMPT.md` as the design brief. The folder contains that brief and six images under `references/`; no `Assets`, `Packages`, or `ProjectSettings` folders were present at audit.
- Inspected all six references. The repeated strengths are a rear chase view, legible cart and rider silhouette, a visible pizza balanced on one upturned hand, strong route depth, and physical shortcuts with clear landings. The images vary between a mall, canal, rooftops, and a rainy lane. They are composition and action references, not a direction to copy their glossy materials or pack all settings into one level.
- Initial art direction for the first experiment: one warm hillside market district with matte painted plaster, worn wood, brushed metal cart rails, and a canal conveyor shortcut. Keep the courier, box, wheel contact, fork shapes, and landing zones clearer than the background. Test the rear silhouette at the target browser embed size before building the district.
- Initial feel experiment: a 20 to 30 second authored route with a fast straight, wide corner, bump, drift, small ramp, generous landing, moving surface, recoverable wall scrape, and finish. Tune the cart, rider, pizza, and camera separately before procedural generation.
- PCG decision from the brief: seeded beat grammar and route graph first, tested module geometry second, then bounded validation and repair. No generator or seed has been implemented or tested yet.
- A Unity 6 editor installation was found, version 6000.2.14f1. Unity Hub was running, but no Unity Editor process was found at audit. This session exposed no Unity MCP tools, so no MCP operation, scene inspection, compilation, Play mode, screenshot, or Web build has succeeded yet. The MCP connection and project identity still need verification.
- Verified the current official itch.io HTML game requirements: ZIP root `index.html`, relative paths, at most 1,000 extracted files, 500 MB total extracted, 200 MB for one extracted file, and 240 characters per path. These are ceilings, not size targets. Source: https://itch.io/docs/creators/html5
- Rechecked the research basis. Tanagra ties level patterns to movement and geometry constraints, path constraints address whole-route outcomes, and action-trajectory comparison helps measure meaningful variety. These ideas support the proposed generator but do not prove this cart controller or any generated route playable. Sources: https://ojs.aaai.org/index.php/AIIDE/article/view/12379 , https://ojs.aaai.org/index.php/AIIDE/article/view/12511 , https://arxiv.org/abs/2201.10334
- Unity's Web performance guidance calls out draw-call cost and batching or instancing. Browser frame time still needs measurement in a real build. Source: https://docs.unity.com/en-us/engine/6000.0/manual/platform-specific/webgl/develop/performance
- No files were published or committed. No approval for public itch publication has been given.

### Open items

- Make the Unity MCP available to this session and inspect its actual tools before scene work.
- Capture an in-engine rear view for art feedback. Then build and play the feel playground.

## 2026-10-02: Unity project appeared

- The Unity project is now in this folder with `Assets`, `Packages`, and `ProjectSettings`. `ProjectVersion.txt` reports 6000.2.14f1. The package manifest is a minimal default set with no render pipeline or input package declared, and build settings contain no scenes. The WebGL support module is installed. No gameplay assets were found in `Assets` at this check.
- Git has not been initialized. No commits have been made.
- Unity Editor was not running when checked. This chat still exposes no Unity MCP operations, so compilation, Play mode, and screenshots remain unverified.

## 2026-10-02: first look and feel implementation slice

- Added the Unity AI Assistant package to `Packages/manifest.json` because this newly created project lacked the Unity-side MCP bridge. The local relay accepted an MCP connection, but its tool list was empty when checked. The package resolved into `packages-lock.json`; approval and tool discovery remain unverified.
- Added `Assets/DeliveryDash/Editor/BuildLookDev.cs`. It is intended to create `Assets/DeliveryDash/Scenes/FeelPlayground.unity` and `Captures/feel-playground-lookdev.png` with a rear-view cart and courier pose, a warm market road, bump, ramp, landing, conveyor, wall, and destination. The screenshot is not yet generated, so the visual direction has not passed in-engine review.
- Added `Assets/DeliveryDash/Scripts/CartFeelController.cs` and `ChaseCamera.cs`. The controller implements tunable forward drive, steering, drift, hop buffering, scrape recovery, and a stuck reset. The camera follows and checks occlusion. These scripts have not yet been compiled or playtested.
- Unity Editor is open but has not observed the new scene script at the latest check. The next action is an editor asset refresh, then compile error review, scene generation, actual screenshot inspection, and Play mode control tuning.

## 2026-10-02: visual review failed

- Unity compiled after correcting one definite-assignment error in `ChaseCamera.cs`. The look-dev scene and a 1280 by 720 capture were generated. No Play mode or Web test has happened.
- The capture failed the art brief and owner review. The rider read as stacked eggs, the cart occupied too little of the frame, the road was a straight empty corridor, and the buildings and trees were primitive placeholders. The box and fingertip support were not legible at the captured size. Do not treat this scene as accepted art direction.
- Research found a creator-hosted CC0 rigged human base from Quaternius, with roughly 13,000 triangles per character in the pack description. Downloaded the free Standard pack without entering any personal or payment information. Imported only the male FBX, hair FBX, relevant textures, and license into `Assets/DeliveryDash/ThirdParty/Quaternius`. Source: https://quaternius.com/packs/universalbasecharacters.html
- Next experiment: use the imported human mesh for anatomy, author the exact red cap, blond hair, red shirt with black half sleeves, tan shorts, and one-hand pizza pose, bring the camera close enough to read them, and rebuild the route composition. Inspect a new actual Unity capture before progressing to more content.

## 2026-10-02: downhill direction and rebuild

- Owner direction: the road descends continuously. The cart advances automatically; A/D and left/right arrows only steer. Removed hop and drift controls from the controller. Speed, side motion, collision recovery, and wheel/box response now use that single-input model. Play mode feel remains unverified.
- Split distinct local tasks for cart geometry, town route geometry, and visual motion feedback. The scene integration and courier mesh pose are being assembled separately. No personal or payment information has been entered into the asset site or project files. No public upload or commit has happened.
- Imported character rig inspection confirms a skinned human anatomy mesh with head, hand, arm, thigh, and calf bones. Added a courier visual builder that positions those bones and adds garment shells, cap, hair, and a box pivot at the supporting palm. It still needs compilation and an actual in-engine view before acceptance.

## 2026-10-02: actual Unity review and art reset

- Unity MCP is now available in this chat. Editor state, active scene, Console, GameObject lookup, a mesh report, and a four-angle scene capture succeeded through Unity tools. The active scene is `FeelPlayground`; it compiled with no current script errors. The Unity AI package has a cloud account warning unrelated to game compilation.
- The fresh 1280 by 720 capture and four-angle view still fail the visual brief. The rider reads as a red torso block, the head is obscured, the pizza support is unclear, the road appears empty, and close buildings look like simple boxes. Owner explicitly rejected this look and requested a full high-quality reskin. No visual approval has been given.
- The hair FBX was placed at the character origin even though its mesh is authored at head height. Parenting it to the head doubled that height; the reported center was roughly 3.20 units while the head bone was 1.62. Reparenting fixed the floating tuft. The current hair is too small and hidden by the cap, so it remains an open visual issue.
- A Unity mesh inspection found the imported body has 7,275 vertices and 12,566 triangles. Temporarily hiding the red clothing shell in Play mode exposed usable skinned anatomy, confirming that the oversized shell is the main reason the rider looks blocky. A fitted skinned garment pass is in progress. This temporary Play mode change has not been saved.
- Asset research found that the free Quaternius Standard download contains only the Superhero male and female bodies. A proportionate Teen body is in a paid Source tier; no purchase was made. For the environment, a small set of human-made CC0 Poly Haven materials is being prepared rather than mixing incompatible low-detail packs. License and Web size must be verified on import.
- No personal details were added to project files. No Git commit, public upload, asset purchase, or itch publication has happened.
## 2026-10-02: takeover audit with live Unity evidence

- Called Unity_GetUserGuidelines, GetProjectRoot, GetState, GetActive, GetConsoleLogs, and RunCommand successfully. Unity 6000.2.14f1, built-in rendering, Edit mode, active scene `Assets/DeliveryDash/Scenes/FeelPlayground.unity`.
- Current source failed compilation because `TerrainAndDistance` and `SortingBelt` were missing from the interrupted town builder. Earlier clean-compile notes are stale.
- Read the complete brief and progression record and inspected all six preserved reference images. Captured the current live camera through Unity MCP at 1280 by 720: `Captures/takeover-baseline.png`. Timestamp verified after capture.
- Primary agent and independent read-only visual critic both reject the baseline: red block torso, obscured head, floating pizza, oversized basket, cropped wheels, blank pale street, weak facade depth and washed-out light.
- Local pre-edit source copies are in ignored `Captures/takeover-source-backup`. No Git repository exists and no commit was made. No asset purchase or upload was made.
- First experiment is fitted skinned clothing, corrected seated anatomy and both hand contacts, compact cart proportions, and adjusted rear three-quarter camera. Scene art and runtime behavior remain unaccepted.

## 2026-10-02: courier experiment 1 rejected

- Replaced the unskinned red loft and large cap with the imported anatomy, skinned garments, hand-attached pizza, curved backward visor, and compact cart. Rebuilt through Unity MCP in Edit mode, with zero Console errors at the check.
- Fresh evidence: `Captures/courier-pass-01.png`, 1280 by 720. Camera capture timestamp verified after rebuild. Primary and critic both FAIL: shirt follows muscle lobes and has serrated edges, hair clips through cap, grip is incomplete, shoes have inward-facing mesh winding, and only two casters read clearly.
- Diagnostic camera angles: `Captures/courier-detail-01.png` and `Captures/courier-front-diagnostic.png`. These exposed the shoe winding and hair covering the face. They are diagnostic views, not accepted gameplay framing.
- Removed unused primitive scene construction and automatic scene replacement on script reload from `BuildLookDev.cs`. Rebuild is now an explicit Edit-mode action, including a courier-only menu for controlled comparisons.
- Next experiment: smooth clipped cloth boundaries, exact two-bone hand placement, cap-safe lower hair locks, outward-facing shoe surfaces, and a 20 percent larger courier framing. No Play mode test yet.


## 2026-10-02: sharing audit and local visual state

- The owner supplied a commit name and email in chat and authorized pushing to the existing private GitHub repository. Do not copy those identity values into project files.
- Scanned project source, Unity YAML, package and settings files, and documentation for home paths, owner identifiers, email addresses, profanity, and secrets. No owner-specific content or profanity was found. Six reference PNGs have no EXIF fields. Imported textures carry only source tool/date metadata, not owner metadata.
- Changed root document mentions to relative paths. Added `Assets/DeliveryDash/Art/Textures/SOURCES.md` with official CC0 source links for five Poly Haven texture sets.
- Fresh courier evidence is `Captures/courier-pass-04.png` at 1280 by 720, captured after a garment fit correction. The red shirt appears again; it still has oversized shoulder shapes and is not an accepted final visual. The town remains the old pale corridor until its unfinished replacement is integrated. No Play mode or Web verification was performed for this sharing pass.
- Current Console reports no C# errors and only Unity AI account/security warnings. This is shared as active work in progress for teammate access, not a finished game.


## 2026-10-03: endless delivery shift

- Owner requested endless procedural content and selected “deliver as many pizzas as you can in one shift” as the goal. This supersedes the earlier finite-run scope for the new scene.
- Added `EndlessRun.unity`, preserving the authored playground separately. It reuses courier/cart/house art and hides the finite district only during play. Added the new scene to build settings.
- Added seeded 48-metre road sections with smooth lateral/width transitions, continuous downhill grade, alternating challenge/recovery beats, roadside crates, split-passage planters, and shared facade templates. At most nine sections are loaded; released sections destroy their owned meshes. A 768-metre floating-origin shift preserves progress and controller/camera/visual history.
- Added a 60-second shift, automatic customer-gate deliveries every 288 metres (+15 seconds, capped at 90), collision penalties (-3 seconds and 20 condition with a two-second cooldown), condition-based tips, fresh pizzas after delivery, a results panel, and same/new-seed restart using R/N. The timer is the losing condition; zero condition means zero tip rather than an instant loss.
- Added off-road and stall recovery. The new scene camera is pulled back for obstacle visibility. A/D and arrows remain the only driving inputs.
- Verification: Unity compilation without C# errors; 500 seeds x 80 sections (40,000 sections) validated for downhill grade, bounded lateral slope, nominal passage width, joins, determinism, and origin invariance. Shift-rule checks cover bonuses, damage/cooldown, tips, expiry, and reset.
- Live physics probe crossed 41 sections / 1,920 metres, checked collider coverage around seams, two origin shifts, a maximum of nine sections, and restart. This probe repositions the cart and is not a steering playtest.
- A separate real-controller automated run at 3x time scale completed five deliveries across 1,666.4 metres, earned $46, took damage, stopped at expiry, and passed new/same-seed restart checks. Background simulation and time scale were temporarily changed for this check and restored afterward. Evidence: `Captures/endless-first-delivery.png` and `Captures/endless-shift-results.png`.
- Visual inspection found a reversed customer sign; corrected its orientation and added crate braces and planter foliage to distinguish obstacle types. Confirmed the corrected sign and obstacle silhouettes in fresh `Captures/endless-customer-gate.png` and `Captures/endless-planters.png`. Existing character art still needs polish.
- Limits: a small obstacle grammar with repeatable variations, not unique assets forever; delivery is automatic at the gate; no moving carts, stunt/fork system, audio, difficulty ramp, browser build, or Web performance measurement yet. Runtime seed layouts are checked geometrically, not formally proven traversable in every circumstance. Human keyboard playtesting remains necessary.


## 2026-10-03: town pickup and customer delivery redesign

- Owner changed the game from a single downhill road to a town with multiple roads. Start empty, visit any pizza shop for one or multiple orders, give every customer a deadline with a monetary late penalty, and show shops/customers/routes on a top-right minimap. Automatic slowdown, a brief stop, and resumption were explicitly approved.
- Added `TownDelivery.unity` as the first build scene, keeping the older endless route and playground scenes. Level, connected streets support free steering in all directions; a separate controller mode preserves the old downhill behavior.
- Added a seeded streamed grid town, stable block/house addresses, shop awnings and orange bays, assigned-house turquoise bays, facade reuse, parks, building collisions, 25 loaded blocks, and origin shifts on both axes. The grid continues as the player explores. The road topology is intentionally regular in this first implementation; seeded shop/building placement supplies variation.
- Starts with no visible pizzas. Shops issue 1–3 orders up to a three-pizza capacity; one box appears on the hand and additional boxes in the basket. Any shop can be used, including an unselected/non-nearest one. Orders require their exact house. No automatic distance-gate delivery or free pizza refill exists in the town mode.
- Driving into an eligible bay initiates a bounded approach, deceleration, a 1.6-second stationary handoff, and automatic forward resumption. A stop cannot repeat until the cart leaves its vicinity. Failed docking times out without awarding cargo/payment.
- Added independent order deadlines based on street travel distance and batch sequence. Initial economy: $12 on time, a one-time $5 fee when overdue, $2 for a late handoff. Late orders stay deliverable; balances may go negative. A fixed five-minute shift is the overall run limit.
- Added a north-up minimap showing streets, heading, orange P shops, numbered customer markers, late markers, and a yellow route. Click map icons/order rows or use Tab to select a stop. Routes follow intersections rather than straight lines through buildings, including when starting between intersections.
- Validation passed for 500 seeds and 1,009 street routes, batch determinism, empty starts, refill capacity/uniqueness, wrong-house rejection, deadline fees, late delivery, expiry, and reset.
- Live physics/flow test passed: empty cargo, non-nearest shop, stopped loading, two assigned pizzas, specific-house delivery, late fee/payment, automatic resume, two-axis rebase, 25-block cap, shift expiry, and new-town reset. It uses diagnostic repositioning between stops and advances time for lateness; this is not a keyboard navigation playtest.
- Screenshots `Captures/town-empty-start.png`, `Captures/town-loaded-orders.png`, and `Captures/town-delivered.png` were inspected. The initial review caught clipped/offset minimap lines caused by rotating GUI coordinates inside a scaled group; switched to group-local line drawing and clearer icon colors. A fresh loaded-orders capture confirmed the corrected route, white heading arrow, and shop/customer markers.
- Remaining: human navigation/turning playtest, deadline and earning balance, less regular street topology, richer town/customer art, traffic/audio, and Web performance. No browser build or publication performed.


## 2026-10-03: irregular streets and graph navigation

- Owner explicitly requested layout work before visual-style commitment: curved streets, angled intersections, uneven blocks, plazas and loops; street-aligned houses; connected graph routing/minimap; preserved delivery rules; actual Unity screenshots for review.
- Replaced the orthogonal road construction with a deterministic sparse triangular graph using displaced nodes and curved, width-varying edges. Some links are omitted and others added; a guaranteed backbone connects every district. Hidden integer keys identify streaming ownership, not visible street coordinates.
- Street meshes and colliders come from sampled curves. Shared node positions and overlapping paved junction surfaces connect sections. Wider open plaza nodes punctuate the network. Per-district mesh ownership releases generated meshes on unload; 25 districts remain loaded.
- Buildings are placed along owned streets at varied longitudinal positions, setbacks, scales, and street-relative orientations. A conservative road-clearance envelope guides lot selection. Existing house/material references remain replaceable; no new fixed aesthetic or asset purchase was introduced.
- Shop and customer bays, stall recovery, spawn heading, and automatic handoff departure now follow the relevant curve tangent. Narrower streets use a 2.8-metre stop-entry radius so passing along the road center does not force a pickup.
- Route guidance searches the graph and follows sampled centerlines; the minimap draws the same curved edges. Deadline budgets use graph-route length. The route search is capped at 4,096 expansions; ordinary order routes are checked, extreme long-distance route selection remains unverified.
- Rules/route validation passed on 500 seeds and 1,009 curved delivery routes, including conservative lot clearance from roads. Topology checks passed on 200 seeds: 21,260 curves, 1,166 T-junctions and 1,326 plazas, with connected interior districts, alternate loops, repeatable curves, and bounded road width/curvature.
- Live surface checks passed on 2,016 centerline/junction samples and ten delivery bays. Live pickup/handoff checks passed on the changed graph, including non-nearest shop, inventory, late fee/payment, correct-house handoff, resume, two-axis rebase, bounded districts, expiry and restart. Diagnostic repositioning is used between stops; this does not constitute a full keyboard steering test.
- Captured and inspected actual Unity images: `Captures/organic-gameplay-hud.png` and `Captures/organic-layout-overview.png`, plus a clean gameplay-camera capture. The elevated diagnostic view temporarily disables fog to reveal road connectivity; gameplay retains its normal fog. Curves, angled junctions, uneven polygons, loops, road-aligned houses and the central plaza are visible.
- This is the requested layout-review stage. Broader art direction is not committed. Environment density, visual polish, additional obstacles, and browser performance remain future work.

## 2026-10-03: shop owners, customer cast, and town obstacles

- Added a runtime chef at each shop and five named customer caricature variants: Dario Amodei, Donald Trump, Elon Musk, Sam Altman, and Jensen Huang. These are procedural placeholder figures with distinguishing hair/accessories, not final likeness assets.
- Each character walks from its doorway to the stopped cart. Visible pizza boxes move toward the cart for pickup and toward the customer for delivery. Hold time accounts for walking distance; characters return to their doorway afterward. Order creation/payment still occur only after successful service. Restart, cancellation, shift expiry, and streaming remove/reset the interaction.
- The current town previously omitted the older scene's obstacles. It now generates deterministic solid crate stacks, produce wagons, and center planters along curved streets. Placement excludes nearby junctions and all lot approaches, and leaves driving space past each obstacle.
- Expanded live checks to verify obstacle colliders, chef movement and transfer before inventory loads, and visible customer receipt, alongside the existing timed-order, late-fee, resume, world-rebase, and restart checks. Captured actual Unity handoffs and a five-character lineup under Captures/.
- Remaining: detailed character likenesses, authored animation clips, moving traffic, human obstacle-difficulty testing, and final art direction.

### Free photo-fitted customer models

- Replaced assigned customer primitives in TownDelivery with five skinned FBX likeness studies; original primitive construction remains a missing-model fallback.
- Added reproducible MediaPipe/Blender reconstruction tools, editable source files, original attributed photo textures and license credits. No paid assets/services.
- Added skeletal carry/walk poses while preserving existing timed handoff behavior. Fixed model import facing and outward hair/skull normals after Unity screenshot inspection.
- Unity compiled without errors. Live pickup/handoff checks passed: empty start, non-nearest shop, chef transfer, customer receipt, late fees, resume, rebase, streamed obstacles, expiry and restart.
- Captured actual Unity cast and individual close-ups. Quality is still a prototype: one-photo geometry cannot verify profiles; photo lighting/expressions are baked in, hair/body/clothing remain generic and there is no facial animation. Do not describe these as exact or production-quality replicas.

### Neighborhood environment review

- Added original 29-prefab environment kit with 11 shared materials, contextual storefront/home/plaza/street placement in two starting districts only.
- Added facade details sized to actual building dimensions, side windows, balcony rails, cornices, downpipes, roof vents, warm shop glazing, signs/awnings/menu boards, cafe patios, gardens, mailboxes, trees, plaza paving/furniture, market stall and parked bicycle/rack.
- Curbs/drains/repairs/crosswalks follow road curves; delivery approach gaps are retained. The road graph, minimap and orders are unchanged.
- PASS: 72 solid props; 4 clear bays and handoff corridors; furnished plaza; Unity driving/plaza/overhead captures. Live pickup/handoff regression also passed, including late fees, resumption, streaming/rebase, expiry and restart.
- Decorative meshes combined by material and disposed with streamed districts: 79 enabled detail renderers. Same-camera editor diagnostic showed draw calls 9465 disabled / 9647 enabled; triangles 3,859,500 / 4,267,972. Existing baseline is expensive; editor timing near 100 ms is not standalone FPS. Broader rollout remains pending visual review and build profiling.

## Living-town preview — 2026-10-04

- Imported inspected free Kenney Car Kit 3.1 and City Kit Suburban 2.0 subsets with original CC0 license files, sources and preview images retained. Reused the existing CC0 Quaternius rig; additional Animated Men download was quota-blocked and is not claimed as imported.
- Central 3×3 connected districts now have increased roadside density, mixed original storefront/apartment fronts, parked cars, planted parks, seating, paths and groves. Final art direction remains open; the broader streamed world is unchanged.
- Added a bounded 24 pedestrian / 8 motor vehicle / 4 cyclist pool. Graph following, junction slowing/yielding, pavement detours, near-junction crossings, park walkers/sitters, shop gatherings, articulated motion and wheel rotation. Static collision probes and timed jam recovery keep the preview moving; this is local avoidance rather than full multi-agent path planning.
- Added selectable pocket kart and original ride-on pizza mower alongside the cart. Configurable speed/acceleration/steering, W/Up hold acceleration, speed HUD, preserved service priority and shift-end lockout. Alternative rides have rear cargo placement.
- Added shared-material district mesh combining and distance visibility for expensive character rigs. Added depth-tested shared font material for storefront signs so they do not show through buildings.
- Automated Play-mode checks cover all three acceleration profiles, throttle priority during handoffs, preview bay clearance, moving actor types and pool limits. Real controller traffic collision and deliberately blocked-vehicle recovery passed. Existing delivery/late-fee/streaming checks passed; dedicated kart and mower variants exercise the same delivery sequence.
- Captures and diagnostic reports are under `Captures/`. Fixed-camera Editor render measurements explicitly do not claim standalone FPS. Longer human driving tests, standalone CPU/GPU profiling, richer crowd appearance/motion, improved traffic turns/crossings, and more varied interiors of blocks remain before wider rollout.

## Remove the origin-only detail cutoff — 2026-10-04

The player reported that the town became sparse after leaving the starting area. The cause was three fixed location restrictions: living scenery in central 3×3 cells, facade/street decoration in two cells, and crowd spawn/navigation anchored to cell (0,0).

- All streamed districts now build infill housing/storefronts, parks/groves, street details and facade details. Seeded spacing and setbacks vary along streets; occasional furnished shop plazas can appear beyond the origin.
- Ambient actors use the player's current district and nearby loaded roads. Existing pool limits remain 24 pedestrians, 8 motor vehicles and 4 cyclists.
- Park/gathering registrations are released with districts and reset on town rebuild; the activity pool is reused after travel and rebasing.
- Play-mode streaming checks visited (0,0), (7,6), (-8,-7), and (7,6) again. Every visit retained 25 loaded/registered districts, 464–498 infill buildings across that loaded window, 75 parks, 50 detailed delivery facades, 50 clear bays, and a pool of at most 36 actors. Counts describe the entire 25-district window, not one street.
- Distant positive/negative teleport rebuilds measured roughly 565–576 ms for all 25 districts in the Editor. This is a bulk regeneration diagnostic, not ordinary crossing latency or standalone FPS; streaming hitches and standalone performance remain work to profile.
- Actual distant-area Unity screenshots: `Captures/streamed-town-distant-street.png` and `Captures/streamed-town-distant-overhead.png`. Reproduction: `DeliveryDash > Checks > Verify detailed town streaming`.

## 2026-10-05 — Sparse shops, delivery guidance and streamed neighborhoods

- Replaced the one-in-five shop rule with seeded sector candidates, configurable frequency/minimum bay separation and a guaranteed starting shop. Default distribution test: 591 shops over 12 × 31 × 31 sampled districts (~5.1%, previously ~20%); all measured shop pairs meet 250 m separation.
- Empty-cart auto guidance now compares road-route distances with switch hysteresis. Manual destinations remain pinned; G restores automatic routing and Tab cycles stops. Added pizza beacons, approach chevrons, service cues and a smaller minimap/empty-order panel; world labels use depth-tested font atlases.
- Retained the road graph topology but introduced main/residential/lane widths and junction radii based on incident streets. Road, routing, minimap and traffic use the same geometry.
- Added coherent land-use variation, closer frontages, housing courts with reserved entrance alleys, connected public gardens/squares, six original business variants, side windows, roof details, multiple roof palettes, and original woodland understory. These rules run throughout streamed districts.
- Grocery/workshop frontages host loading obstacles; construction barriers have a visible work patch. Road/bay/intersection exclusions remain. Traffic wheels/turning and cycling poses improved; shop groups, joggers, headwear and safer off-camera spawning added without increasing the 36-actor pool.
- Replaced excessive plaza-tile instances used for narrow paths with a simple shared footpath prefab. Normal streaming builds at most two new districts per update. Full rebuilds and diagnostic teleports still generate synchronously and can stall.
- Verified 200 graph seeds / 21,260 curves and 500 order/route seeds / 1,009 routes; sparse-shop separation/manual guidance; distant positive/negative districts and revisits; 50 clear bays per loaded window; three ride acceleration/release/service overrides; actual controller collision reactions and timed jam recovery; complete automatic pickup/customer handoffs including late fees and restart. The five named customers remain unchanged.
- Actual Unity captures and a time-sampled animation clip are in Captures. macOS development build succeeds without build warnings/errors. Editor and standalone benchmark reports are separate and state their limits: CPU render submission and loop intervals, not isolated GPU timings or sustained-driving FPS. No new external asset downloads; original additions and existing CC0 credits are documented in ThirdParty/Kenney/SOURCES.md.
- Remaining work: broader architectural/ground variety, more natural animation/foot-pedal alignment, full pedestrian/traffic planning, smoother startup/teleport generation, longer human driving and lower-end/Web performance validation. No final art direction is claimed.
- Final physical route check: 260.5 m from initial approach through shop pickup to the assigned customer; $12 on-time delivery in 40.7 wall seconds. Diagnostic heading only, normal controller/physics/traffic, no test repositioning between stops.
- Final green-space pass expands connected park loops and increases woodland planting in suitable interiors. Distant/revisit checks still pass (493–561 infill buildings and 25 public spaces in the loaded window). Full 25-district diagnostic rebuilds now measure 1.05–1.13 seconds; this remains an acknowledged hitch. Final standalone diagnostic loop: 15.01 ms mean / 16.09 ms p95; Editor render submission: 6.30 / 8.16 ms, with background loops throttled near 100 ms.
