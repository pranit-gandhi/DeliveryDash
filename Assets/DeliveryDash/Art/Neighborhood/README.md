# Neighborhood review kit

29 original procedural prefabs created for this project; no purchased or downloaded assets. All meshes are constructed from Unity primitives and share 11 Standard materials. Original meshes/materials may be reused and replaced within the project. The built-in Unity font is used for signs.

Edit prefab geometry or materials here. `DeliveryDash > Install neighborhood detail preview` regenerates this kit and overwrites its generated prefab/mesh/material assets, then assigns them in TownDelivery.

Runtime scope: districts (0,0) and (1,0), anchored to world coordinates. This is a representative review neighborhood, not a full-town rollout. The starting district contains a pizza shop, customer home, intersection, cafe seating and a furnished pocket plaza. Placement follows the existing road and lot frames. Shop-front approaches remain clear; paving/curbs are visual so they do not trip the cart. Solid furniture uses simple box colliders. No real-time lights are added: shop panes and lanterns use emissive shared materials.

TownEnvironmentDetails combines the detail meshes by material per group and disposes those generated meshes when the district unloads. Text signs remain separate. Props retain their reusable source prefabs and colliders. Existing town meshes and NPCs are unchanged.

Validation menu in Play mode: `DeliveryDash > Checks > Review neighborhood details`. It checks bays and handoff approaches, confirms the furnished plaza and captures driving/plaza/overhead Unity renders. It also records a 45-editor-update comparison with the detail groups disabled/enabled; this is a diagnostic, not player FPS or a build benchmark.

Remaining before broader rollout: review the look, profile a standalone build, reduce the existing town/NPC rendering baseline, expand placement clearance checks across seeds, and tune building/prop variety. The current fixed review region intentionally remains small.
