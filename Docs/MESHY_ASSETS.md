# Meshy AI asset list — Paper Flight

3D models are made with Meshy AI. The greybox primitives in the Game scene stay as **colliders**; Meshy
models are **visuals only** and replace the greybox renderers. So collision never depends on Meshy topology.

## How to export (same for every model)
- **Format: FBX.** Unity imports FBX natively. GLB/glTF needs an extra package; ask first if you prefer it.
- **Style (add to every prompt):** `low poly, flat shaded, stylized, cozy, soft pastel colors, no text, no logos, single object, plain background`
- **Polycount:** remesh/target **1,000–5,000 triangles** (small props ≤ 1,500). Mobile, 60 FPS.
- **Textures:** base color only, **1024 px max** (512 for small props). No normal/roughness/metallic maps needed.
- **One object per file.** Moving parts (fan blades, mobile) as **separate files** so they can rotate.
- **File name:** exactly the name in the table, e.g. `Sofa.fbx`.
- **Where:** put files in `Assets/_Game/Art/Models/`.
- Scale, pivot and facing don't need to be perfect. The scene builder fits each model to the size below,
  puts the pivot at bottom-centre and faces it as listed. Upright, front facing the camera is best.

## Day 2 — layout 1 (needed next)

| File | Real size (W × H × D, m) | Prompt (add the style line) | Notes |
|---|---|---|---|
| `LaunchShelf.fbx` | 1.2 × 1.9 × 0.6 | tall wooden bookshelf cabinet with books and a small potted plant on the shelves, flat empty top | The plane launches from its top: top surface must be flat and clear |
| `Sofa.fbx` | 2.6 × 1.2 × 1.0 | three-seat fabric sofa with cushions | |
| `Armchair.fbx` | 1.0 × 0.9 × 1.0 | cozy fabric armchair | |
| `CoffeeTable.fbx` | 1.3 × 0.45 × 0.8 | low wooden coffee table | |
| `TVBench.fbx` | 2.2 × 0.6 × 0.5 | low wooden TV bench with drawers | |
| `TV.fbx` | 1.6 × 0.9 × 0.08 | flat screen television, black screen | |
| `Bookshelf.fbx` | 1.8 × 2.2 × 0.7 | wide bookshelf full of colorful books | Used twice |
| `DiningTable.fbx` | 1.8 × 0.76 × 1.0 | rectangular wooden dining table | |
| `DiningChair.fbx` | 0.45 × 0.9 × 0.45 | simple wooden dining chair | Used several times |
| `Desk.fbx` | 1.6 × 0.76 × 0.8 | wooden work desk with a laptop and a mug | |
| `FloorLamp.fbx` | 0.45 × 1.8 × 0.45 | tall floor lamp with fabric shade | |
| `PlantSmall.fbx` | 1.0 × 1.2 × 1.0 | potted leafy house plant | |
| `PlantLarge.fbx` | 1.2 × 1.9 × 1.2 | large monstera plant in a ceramic pot | |
| `BoxStack.fbx` | 1.2 × 1.0 × 0.7 | stack of three cardboard boxes | |
| `Cabinet.fbx` | 1.1 × 2.5 × 0.6 | tall wardrobe cabinet with two doors | |
| `FloorFan_Body.fbx` | 0.5 × 1.2 × 0.4 | standing floor fan, stand and motor housing with round front grille, without blades | Wind source |
| `FloorFan_Blades.fbx` | 0.4 × 0.4 × 0.08 | three-blade fan propeller, flat, centered hub | Spins; separate file |
| `Radiator.fbx` | 1.2 × 0.6 × 0.15 | white wall radiator heater | Thermal (updraft) source |

## Day 3 — layouts 2 and 3

| File | Real size (W × H × D, m) | Prompt (add the style line) | Notes |
|---|---|---|---|
| `CeilingFan_Base.fbx` | 0.4 × 0.5 × 0.4 | ceiling fan motor housing with short down rod, without blades | Hangs from the ceiling |
| `CeilingFan_Blades.fbx` | 1.3 × 0.1 × 1.3 | four-blade ceiling fan rotor, flat wooden blades, centered hub | Spins; crash hazard |
| `ACUnit.fbx` | 0.9 × 0.3 × 0.25 | wall mounted air conditioner unit | Downdraft hazard |
| `Window.fbx` | 1.6 × 2.0 × 0.2 | tall open window with white frame and light curtains | Gust source; mounts on a wall |
| `Mobile_Frame.fbx` | 0.8 × 0.6 × 0.8 | hanging baby mobile frame, wooden cross bars with strings, without ornaments | Hangs from the ceiling |
| `Mobile_Ornament.fbx` | 0.15 × 0.15 × 0.15 | small felt star ornament | Several, swing slowly |
| `BookStack.fbx` | 0.3 × 0.3 × 0.22 | small stack of hardcover books | Clutter |
| `Ball.fbx` | 0.3 × 0.3 × 0.3 | colorful beach ball | Clutter |
| `ToyBlocks.fbx` | 0.5 × 0.3 × 0.4 | pile of wooden toy blocks | Clutter |
| `Rug.fbx` | 3.0 × 0.02 × 2.0 | round woven rug | Visual only, no collider |

## Optional
- **Paper plane:** keep the generated mesh. It is tiny and thin, Meshy tends to make it thick, and Day 2's
  wing flex needs a simple mesh. Only try Meshy if you want a printed-paper look:
  `folded white paper airplane, dart shape, thin paper, 25 cm long`.

## Phase 1 — the house (PENDING: Meshy returned server errors on 2026-11-05; all tasks refunded)
Already mapped in `Assets/_Game/Editor/PropModels.cs`: drop each FBX + `Textures/<Name>_base_color.png` into
`Assets/_Game/Art/Models/` (or generate via the Meshy MCP) and rebuild the scenes; until then these show as greybox.
Settings used: Smart Topology, `meshy-t2.5`, FBX, style line above.

| File | Real size (W × H × D, m) | Prompt (add the style line) |
|---|---|---|
| `KitchenCounter.fbx` | 4.0 × 0.9 × 0.65 | long kitchen counter with lower cabinets and drawers and a sink on top |
| `KitchenIsland.fbx` | 2.0 × 0.9 × 1.0 | kitchen island with flat countertop and cabinets underneath |
| `Fridge.fbx` | 0.9 × 1.9 × 0.75 | tall retro two-door refrigerator, rounded edges |
| `Stove.fbx` | 0.8 × 0.9 × 0.65 | kitchen stove with oven door and four burners on top |
| `RangeHood.fbx` | 0.8 × 0.7 × 0.5 | wall mounted kitchen range hood extractor, chimney style |
| `BarStool.fbx` | 0.4 × 0.75 × 0.4 | tall wooden bar stool with round seat and footrest |
| `Bed.fbx` | 1.6 × 1.0 × 2.1 | double bed with wooden headboard, mattress, duvet and two pillows |
| `Nightstand.fbx` | 0.5 × 0.55 × 0.4 | small wooden bedside nightstand with one drawer and a little lamp on top |
| `CeilingFan_Base.fbx` | 0.25 × 0.6 × 0.25 | ceiling fan motor housing with short down rod and ceiling mount, without blades |
| `CeilingFan_Blades.fbx` | 1.3 × 0.06 × 1.3 | four-blade ceiling fan rotor seen flat, long flat wooden blades around a round central hub |
| `Window.fbx` | 1.0 × 1.6 × 0.1 | tall open window with white wooden frame, two glass panes swung open and light flowing curtains |
| `BunkBed.fbx` | 2.0 × 1.7 × 1.0 | wooden kids bunk bed with ladder, two mattresses with blankets and pillows |
| `ToyShelf.fbx` | 1.2 × 1.0 × 0.4 | low wooden toy shelf with cubby compartments holding colorful toys |
| `ToyBlocks.fbx` | 0.5 × 0.3 × 0.4 | small pile of colorful wooden toy building blocks |
| `Ball.fbx` | 0.3 × 0.3 × 0.3 | colorful striped beach ball |
| `DeskFan.fbx` | 0.3 × 0.4 × 0.25 | small retro desk fan with round grille cage and stand |
| `OfficeChair.fbx` | 0.6 × 1.1 × 0.6 | swivel office chair with padded seat, backrest, armrests and five-star wheeled base |
| `Globe.fbx` | 0.5 × 1.0 × 0.5 | antique world globe on a wooden floor stand |
| `FilingCabinet.fbx` | 0.5 × 1.3 × 0.6 | metal office filing cabinet with four drawers and handles |

Facing (yaw) per model is set in `PropModels.cs`; check each with a render after import.
