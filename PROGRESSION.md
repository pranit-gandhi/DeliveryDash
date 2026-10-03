# DeliveryDash progression

| Status | Current state |
| --- | --- |
| Phase | 1: art reset and feel playground |
| Playable state | The saved scene opens and current Console has no C# errors. Fresh courier capture still fails visual review. No complete run, PCG, or Web build is verified. |
| Next step | Complete and inspect the downhill district rebuild; fix invisible skinned garments, hand contacts, and cart silhouette; then test in Play mode. |

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
