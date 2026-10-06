# DeliveryDash continuation prompt

Paste this document into the next model working in this workspace. Read the current files before editing: this is a live checkpoint, not proof that any feature works.

## Your task

Implement the owner's approved movement-first 3D pixel revamp to completion. Read `PROMPT.md`, `PROGRESSION.md`, this file, all six original images in `references/`, and `references/deliverydash-pixel-target.png`. Preserve the original brief and every reference. The owner rejected both the earlier primitive 3D result and the later projected-road experiment. Do not call either accepted, finished or fun.

Build a real downhill 3D world with a consistent 640 by 360 pixel treatment, displayed cleanly at 1280 by 720. Keep Unity 6000.2.14f1 and the built-in pipeline. Retain the seated pizza courier, warm town palette, automatic forward motion and steering-only A/D or arrow controls. No throttle, brake, manual hop, drift button or pizza balancing input.

Work autonomously on safe local development. Use subagents for course generation, movement engineering and read-only visual criticism. An independent subagent must also play with actual keyboard controls. Source review and controller simulations are not independent playtesting. Coordinate access to Unity so agents do not change Play state or scenes simultaneously.

## Approved implementation plan

### Movement and camera

- Fixed-step simulation with forward velocity, heading, steering angle, lateral grip and four wheel contact points. Steering rotates travel through the world rather than directly shifting the cart sideways along a rail.
- Start briskly. Initial hypotheses: 9 metres per second at launch, 16 to 22 during descent, visible steering response within 150 milliseconds. Measure and tune through play.
- Slope, cornering, surface and collisions change speed. No fixed cruise-speed behavior. Excess cornering demand and slick surfaces cause automatic slides; countersteering restores grip.
- Scrapes cost some speed and pizza condition but should normally recover within one second. Bumps compress contact before takeoff. Jumps follow a flight trajectory into a visible generous landing with suspension, rider lag and pizza correction.
- Perspective chase camera follows heading and velocity with a short delay, restrained banking, speed framing and landing response. Keep route choices visible at least three seconds before commitment.
- Animate frame, wheels, rider and pizza separately. Keep the left hand visibly on the grip and the right palm or fingertips under the middle of the box in every pose. Do not detach hands to fake body lean. Keep the pizza relatively level.

### Shared course and PCG

- Define CourseGraph, RouteModule and module sockets carrying world position, direction, width, elevation, surface, clearance and tested speed range. Rendering, wheel contact and events must use the same course geometry.
- First build a 25 to 30 second feel course from generator-compatible modules: acceleration, broad turn, bump, slide, small jump, scrape, recovery and delivery. Do not expand content before actual keyboard play establishes responsiveness and readability.
- Then build 60 to 90 second generated slices from a finite beat grammar: launch, sweeping turn, simple interaction, recovery, meaningful fork, combination, recovery, second choice, final approach, delivery.
- Vary action sequences, bend shapes, surfaces, branch lengths and risks. Two forks need wider longer safe paths and physically shorter or faster risky paths. A speed bonus alone is not a shortcut.
- Every traversable segment must descend, including ramps, lips, forks, joins and recovery. A launch may flatten its descent before a steeper drop, but its surface must never rise in the travel direction.
- Mesh roads, curbs, retaining walls, canal edges, bridges and close buildings share world coordinates. Only distant sky and mountains may use backdrops. No projected road over a painted foreground.
- Validate downhill slope, socket compatibility, overlap, width, curvature, steering demand, three-second previews, landing reach and delivery feasibility. Bound repairs and use a separately validated fallback.
- Run at least 1,000 seed validations and controller simulations at several frame rates. Inspect failures and measure action-sequence diversity. These are filters, not proof of fun or human playability.

### Presentation and complete loop

- Extend to 120 to 180 second runs only after the generated slice proves varied and fair. Independently play at least six representative seeds, exercise both branches, deliberately make mistakes and test failure, retry, pause and focus loss.
- Build coherent shaped architecture with roofs, recessed openings, shutters, awnings, balconies and grounded foundations. Details should serve route functions. Avoid empty brown expanses, disconnected road seams, generic cube towns and random clutter.
- Keep the courier and cart prominent, with enough view ahead to steer. Red backward cap, visible blond hair, red shirt, black half sleeves, tan shorts, red shoes and believable seated human anatomy.
- Compact start, pause and result menus. Short labels: Go, Resume, Retry, New road. No slash-separated labels, large driving tutorial panels or quippy captions. Essential time, pizza condition and progress only. Introduce steering before departure; pause later teaching cues.
- Visible customer destination, brief delivery action, elapsed time, pizza condition and immediate retry. Restrained rolling, scraping, landing and delivery audio with mute and browser activation.
- Use `../Sumi` and `../Hell's Bid` only as presentation references for restraint, focus and clear feedback. Do not copy their themes or modify those projects.

### Verification and release

- After every meaningful visual change, capture the actual Unity camera at 1280 by 720, verify timestamp, inspect it yourself and send it to the critic. Require concrete pass or fail on anatomy, cap and hair, grip, box support, cart proportions, downhill read, gameplay beat, architecture, materials, lighting and composition.
- Capture turns, takeoff, landing, branch entry and finish. Static screenshots cannot establish movement quality. Never ask the owner to perform your visual QA.
- Build Unity Web and serve it over local HTTP. Independently test complete rounds, keyboard focus, startup, audio activation, resizing and frame time in an actual browser.
- Package only the verified build, with `index.html` at ZIP root, relative URLs, fewer than 1,000 files, at most 500 MB extracted, at most 200 MB per file and paths under 240 characters. Aim much smaller. No public upload.

## Current checkpoint: 2026-10-05

### Authoritative evidence

- Unity MCP guidelines call succeeded. Editor was running the old PixelRun scene. Stop succeeded before source integration.
- New Downhill code now compiles. Unity reports Edit mode, not compiling and not updating, with zero current Console errors. The new scene has not yet been built, visually reviewed or keyboard played.
- Earlier integration errors for missing CourseMeshBuilder and invalid Camera.AddComponent calls are resolved. A Unity_RunCommand compiled and executed CourseGraph.CreateFeel().Validate(out report), returning True, OK, 481 samples and 480 metres. This is a narrow authored-data check, not proof of physical playability or PCG.
- The old PixelRun experiment had automated simulated completion and earlier seed checks. Those results do not validate this new controller or generator. No independent six-seed keyboard suite or Web test is complete.

### New files and responsibilities

- `Assets/DeliveryDash/Downhill/Scripts/CourseGraph.cs`: CourseSurface, CourseEvent, CourseSample and CourseGraph. `CreateFeel(int seed = 2647)`, Samples, Length, Sample(distance), Closest(position), Validate(out diagnostics). Sample fields include Position, Forward, Width, Grip, Distance, Surface, Event and LaunchCue. Current authored course is 480 metres. Seed does not yet create a varied full route.
- `Assets/DeliveryDash/Downhill/Scripts/CourseMeshBuilder.cs`: saved real road, curb, wall and action geometry, with textured surface submeshes. It compiles but has not been rendered or physically verified. Expected API `Build(CourseGraph course, Transform parent)` returns GameObject.
- `Assets/DeliveryDash/Downhill/Scripts/DownhillCart.cs`: `Configure(CourseGraph)`, Begin, Stop, Step(dt, steering), SetKeyboardControl(bool). Properties Speed, Steer, Slip, Compression, Airborne, Progress (normalized), Distance (metres), Condition, Velocity, Running, GroundContacts, Recoveries, Course. Owns actual keyboard input in FixedUpdate by default.
- `Assets/DeliveryDash/Downhill/Scripts/DownhillChaseCamera.cs`: Configure(Transform, DownhillCart), Snap. Serialized target references and restrained 51 to 54 degree speed framing. Inspect actual camera composition.
- `Assets/DeliveryDash/Downhill/Scripts/DownhillSession.cs`: feel-course start, pause, retry, mute, finish and 55 second timeout. Finish uses Distance, not normalized Progress. No full run generation or game audio yet.
- `Assets/DeliveryDash/Downhill/Scripts/DownhillVisuals.cs`: frame lean, wheel rotation and pizza counter-rotation. Rider reference currently has no independent pose solver. Do not claim supported hands under independent animation are verified.
- `Assets/DeliveryDash/Downhill/Scripts/PixelOutput.cs`: built-in OnRenderImage downsamples to 640 by 360 with nearest upscale. Verify real output, aliasing and Web compatibility.
- `Assets/DeliveryDash/Downhill/Editor/BuildDownhill.cs`: menu `DeliveryDash/Build downhill feel course`; intended scene `Assets/DeliveryDash/Downhill/Scenes/DownhillFeel.unity`; capture menu `DeliveryDash/Capture downhill camera`. Uses earlier cart and courier builders as temporary geometry and adds unreviewed real terrace architecture. Saves transient mesh/material resources and changes build settings to the new scene when successfully run. Repeated builds currently create additional generated resources; clean up only verified unreferenced new resources if needed.

### Immediate technical defects to verify

1. Course Closest initially returned integer metre samples, which would create stair-stepped contact heights. Route agent reports it now projects onto XZ segments and interpolates continuously. This source correction still needs live contact testing.
2. Launch slope initially changed too gradually for natural takeoff. Route agent reports a .025 shallow downhill grade transitioning to a .40 downhill drop near 231 metres. Controller waits for LaunchCue above .85. Verify geometry, trajectory and contact agreement instead of adding arbitrary upward input.
3. Inspect scrape recovery timing. Current source may take longer than the one-second target. Measure actual deliberate mistakes.
4. Verify retries snap camera and reset all movement/animation state. Verify pause, focus loss and keyboard behavior in Play mode.
5. The visual critic rejected the draft architecture on source inspection: 38 similarly sized repeated cube houses with identical window patterns are not acceptable final town art. The added roofs and shutters do not resolve repetitive silhouettes. Replace close architecture with varied authored shapes, credible surface materials and functional placement. A new camera capture remains required.
6. Old courier/cart issues remain: basket bars can hide legs; brown pizza slab is too heavy; hair, cap and shoes are root-parented and would detach under independent bone animation. New builder scales the cart frame to (.67, .88, .67) independently of the courier. Critic predicts left-hand separation: courier hand target is near local (-.49, 1.025, -.48), while the scaled handle is farther back near z -.76. Inspect and retarget to the actual grip geometry. Correct using actual camera evidence.
7. World finish currently lacks a verified customer delivery action. Audio and full HUD remain incomplete.
8. Do not claim PCG is implemented merely because a class is named CourseGraph. The current new course is authored and has no complete socket/module graph, branches, bounded repair or independent fallback.

## Earlier experiments and reusable assets

- Rejected old 3D scene: `Assets/DeliveryDash/Scenes/FeelPlayground.unity`.
- Rejected projected-road scene: `Assets/DeliveryDash/Pixel/Scenes/PixelRun.unity`.
- Earlier builders: `Assets/DeliveryDash/Editor/BuildLookDev.cs`, CartVisualBuilder, CourierVisualBuilder, SkinnedGarmentBuilder, TownVisualBuilder, SurfaceMaterialBuilder.
- Existing Quaternius body and license: `Assets/DeliveryDash/ThirdParty/Quaternius/`. Garment and exposed-skin assets are modified in the working tree. Inspect rather than revert them.
- Pixel art experiments: `Assets/DeliveryDash/Pixel/Art/`, with SOURCE.md provenance. DistantBay is a skyline-only background; TownDescent, TownMarket and TownFinish include foreground roads and must not be overlaid behind a new real road. CourierCart is a rigid sprite and must not replace the real articulated courier.
- Earlier captures under `Captures/` are ignored local evidence. `courier-fit-05.png` is a historical failed 3D frame. `pixel-run-gameview.png` is a 2560 by 1440 actual old Game View. Other pixel-run simulated captures are historical and do not prove live keyboard testing.
- `Assets/DeliveryDash/Pixel/Editor/BuildPixelRun.cs` may still warn about obsolete TextureImporter.spritesheet. Do not rebuild that experiment as the new game.

## Unity tool procedure

1. Call Unity_GetUserGuidelines first. Verify project root, active scene, Play state, compiling state and Console. Stop Play before saved-scene rebuilds.
2. Refresh assets, wait for compilation and inspect Console. A source read is not a compile test.
3. Build using the new menu only after source compiles. Verify saved scene and active build entry.
4. Capture a fresh1280 by720 camera view and inspect it yourself. Camera-only capture excludes IMGUI menus, so also inspect actual Game View for UI.
5. Unity_Camera_Capture previously returned `Error executing tool: Failed to render scene preview.` Other MCP calls worked. Do not describe the entire Bridge as unavailable. Camera.Render plus ReadPixels is an established fallback in BuildLookDev.Capture. ScreenCapture captures actual Game View including UI but inherits Game View resolution.
6. Unity_RunCommand requires `internal class CommandScript : IRunCommand` with `public void Execute(ExecutionResult result)`. Dynamic command compilation can reset Play state or timing; prefer stable editor menu actions for repeated observations.
7. When loading a scene through Unity_ManageScene, use folder Path and extension-free Name separately. Do not pass the entire file path as the folder.

## Privacy, source control and reporting

- Keep all project documentation paths relative. No personal details, account information, profanity or em dashes in project files. Do not enter personal or payment information on asset sites.
- Preserve existing uncommitted changes, references and earlier experiments. Do not overwrite the working tree with an older checkout. Inspect git status first.
- No asset purchases, public repository creation, history rewrite, itch page, public upload or new commit/push without appropriate explicit owner authorization. Do not copy a commit identity into tracked documentation or add co-author trailers.
- Update PROGRESSION.md after each meaningful implementation or test. Keep its top status accurate. Record exact scene, capture path and timestamp, compile status, actual keyboard results, simulated results separately, seeds, browser checks, bugs and next experiment.
- Never claim looks good, playtested, 60 fps, ready for itch or complete without direct supporting evidence. A critic pass does not replace owner approval.
- Communicate concrete findings frequently. Continue the work instead of asking the owner to inspect an obvious failed scene.

## Next concrete action

Recheck the current Console and Editor state, then build DownhillFeel in Edit mode. The last compilation and authored-data validation passed; do not confuse that with a gameplay test. Inspect the first fresh1280 by720 camera capture and obtain the critic's blunt findings. Then coordinate an independent actual-keyboard feel-course test and tune movement before expanding the generator. Update this checkpoint whenever later evidence supersedes it.
