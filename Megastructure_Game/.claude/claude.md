Detailed Summary of the Building System
Overview
The building system allows players to place cubic blocks in a 3D grid with two modes: Normal Mode (camera follows player) and Build Mode (camera orbits around blocks). Blocks can be placed in 8 horizontal diagonal directions (45° accuracy) and full 3D diagonal directions when looking up/down.
Core Components
1. BuildModeController.cs - Main Build Mode Logic
Purpose: Manages the build mode state, camera transitions, and block placement. Key Fields:
isInBuildMode (bool): Whether build mode is active
currentSelectedBlock (GameObject): The block camera orbits around
playerController (SUPERCharacterAIO): Reference to player controller
gridSize (float): 1.0 - size of each grid cell
savedPlayerPosition (Vector3): Player position when entering build mode
savedCameraDistance (float): Camera distance before build mode
Key Methods: EnterBuildMode(GameObject firstBlock) (lines 69-112):
Saves player position and camera distance
Sets controllerPaused = true (freezes player movement)
Sets buildModeOverride = true (switches camera to orbit mode)
Rotates player avatar to face the first block using Quaternion.LookRotation
Sets orbit center to firstBlock.transform.position
Sets camera distance to buildModeOrbitDistance (8 units)
Forces third-person camera perspective
Shows directional arrows
ExitBuildMode() (lines 119-141):
Sets controllerPaused = false (re-enables player movement)
Sets buildModeOverride = false (returns camera to normal tracking)
Restores saved camera distance
Hides arrows
PlaceBlockInDirection(KeyCode key) (lines 142-179):
Gets placement direction from DirectionalArrowSystem
Calculates new position: currentBlock.position + direction * gridSize
Rounds position to 1.0 grid increments
Checks if position is occupied (BlockAdjacencyGrid)
Checks block count limit
Calculates block rotation: Quaternion.LookRotation(direction, Vector3.up) - block faces the placement direction
Instantiates block with rotation at new position
Registers block with adjacency grid
Calls SelectBlock() to update orbit center
Update() (lines 55-78):
Continuously resets player position to savedPlayerPosition (freezes player in place)
Updates arrow positions as camera rotates
Listens for Space key to exit build mode
Listens for WASD keys to place blocks (with 0.15s delay between placements)
2. DirectionalArrowSystem.cs - Direction Quantization & Arrow UI
Purpose: Converts camera orientation into quantized placement directions and displays UI arrows showing where blocks will be placed. Key Fields:
arrowTextElements (TextMeshProUGUI[4]): UI text showing "W", "A", "S", "D"
arrowData (ArrowPositionData[4]): Wrapper class holding world positions and DOTween tweeners
currentCardinalDirections (Vector3[4]): Current quantized directions for each arrow
snapDuration (float): 0.3s - DOTween animation duration when arrows snap to new directions
Key Methods: GetPlacementDirection(KeyCode key) (lines 212-250): Returns the quantized direction vector for block placement. Horizontal Mode (camera pitch < 45°):
Flattens camera forward to XZ plane
Rotates for A/D keys (±90°)
Calls QuantizeTo8Directions() to snap to nearest 45° angle
Returns one of 8 horizontal directions: N, NE, E, SE, S, SW, W, NW
Vertical Mode (camera pitch ≥ 45°):
Uses full camera forward vector (preserves Y component)
Rotates for A/D keys (±90°)
Calls QuantizeTo3DDiagonals() to snap to 3D diagonal
Returns 3D direction like (0.707, -0.707, 0) for "down-forward"
QuantizeTo8Directions(Vector3 direction) (lines 252-280):
Flattens direction to horizontal plane (y=0)
Calculates angle: Mathf.Atan2(x, z) in degrees (0-360°)
Divides by 45° and rounds to get sector (0-7)
Returns cardinal or diagonal: Vector3.forward, (forward+right).normalized, etc.
QuantizeTo3DDiagonals(Vector3 direction) (lines 282-325):
Separates into horizontal (XZ) and vertical (Y) components
Quantizes horizontal to 8 directions using same logic as above
Quantizes vertical to -1, 0, or +1 based on 0.3 threshold
Combines: (quantizedHorizontal + Vector3.up * verticalQuantized).normalized
Supports 26 total directions (8 horizontal × 3 vertical states + pure up/down)
UpdateArrowPositions() (lines 132-218): Called every frame when build mode is active.
Determine mode: Checks camera pitch to decide horizontal vs vertical
Calculate quantized directions for all 4 arrows (W/A/S/D):
Horizontal: Uses QuantizeTo8Directions() on camera-relative vectors
Vertical: Uses QuantizeTo3DDiagonals() on camera-relative vectors
Calculate world positions: blockCenter + direction * (blockSize/2 + offset)
Detect direction changes: Compares newCardinalDirections[i] to currentCardinalDirections[i]
Animate with DOTween: If direction changed, smoothly tween world position over 0.3s
Convert to screen space:
WorldToViewportPoint() → normalized 0-1 coordinates
Multiply by Screen.width/height to get pixel position
Works correctly with render textures for pixel effects
Update UI elements: Sets rectTransform.position to screen position
Hide if behind camera: Checks viewportPos.z > 0
3. SUPERCharacterAIO.cs - Third-Party Character Controller (Modified)
Purpose: Handles player movement and camera control. Modified to support build mode orbit. Build Mode Fields (lines 41-45):
buildModeOverride (bool): Switches camera from player tracking to orbit mode
buildModeOrbitCenter (Vector3): Point the camera orbits around
buildModeOrbitDistance (float): 8f - fixed orbit radius
cameraPerspective (PerspectiveModes): Default _3rdPerson
Key Modifications: Mouse Input Capture (lines 437-450):
Moved OUTSIDE controllerPaused check
Camera can still rotate even when player is frozen
Camera Update Logic (lines 629-643):
Camera updates allowed even when controllerPaused == true
Player movement blocked but camera orbit enabled
Orbit Center Override (lines 698-702):

if (buildModeOverride)
    headPos = buildModeOrbitCenter; // Orbit around selected block
else
    headPos = transform.position + Vector3.up * standingEyeHeight; // Follow player
Vertical Rotation Limit Removal (lines 707-712, 799-803):

if (!buildModeOverride) {
    headRot.x = Mathf.Clamp(headRot.x, -0.5f * verticalRotationRange, 0.5f * verticalRotationRange);
}
In build mode, camera can look straight up/down without limits. Fixed Distance Camera (lines 925-954):

void UpdateCameraPosition_3rdPerson()
{
    if (buildModeOverride) {
        currentCameraZ = -buildModeOrbitDistance; // Fixed 8 units, no smoothing
    } else {
        // Normal mode: obstacle detection + smooth damping
    }
}
Zoom Disabled in Build Mode (lines 524-528):

if (!buildModeOverride) {
    maxCameraDistInternal = Mathf.Clamp(maxCameraDistInternal - mouseScrollWheel * ..., ...);
}
4. BlockAdjacencyGrid.cs - Position Tracking
Purpose: Tracks which grid positions are occupied to prevent overlapping blocks. Key Fields:
blockGrid (Dictionary<Vector3Int, GameObject>): Maps grid coordinates to block GameObjects
gridCellSize (float): 1.0f - size of each grid cell
Key Methods: RegisterBlock(GameObject block, Vector3 worldPosition):
Converts world position to grid coordinates: Vector3Int(Round(x), Round(y), Round(z))
Adds to dictionary if not already occupied
Warns if position already has a block
IsPositionOccupied(Vector3 worldPosition):
Converts to grid coordinates
Returns blockGrid.ContainsKey(gridPos)
UnregisterBlock(Vector3 worldPosition):
Converts to grid coordinates
Removes from dictionary
5. BuildingSystem.cs - Block Management
Purpose: Tracks all placed blocks and handles first block placement (E key). Key Fields:
cubePrefab (GameObject): The block prefab to instantiate
maxBlocks (int): Maximum number of blocks allowed
CurrentBlockCount (int): Number of blocks currently placed
Key Methods: AddBlock(GameObject block):
Adds block to internal tracking list
Increments CurrentBlockCount
Update() (E key placement):
Checks if E key pressed and not in build mode
Calculates preview position using raycast or player forward
Rounds position to 1.0 grid
Creates first block
Calls BuildModeController.EnterBuildMode(firstBlock)
Workflow Example
Entering Build Mode:
Player presses E → BuildingSystem places first block
BuildingSystem calls BuildModeController.EnterBuildMode(firstBlock)
BuildModeController:
Saves player position (3, 0, 5)
Freezes player at this position
Rotates player avatar to face block using LookRotation
Sets camera orbit center to block position (4, 0, 5)
Sets camera distance to 8 units
Enables camera control
DirectionalArrowSystem:
Shows 4 UI text elements ("W", "A", "S", "D")
Calculates quantized directions based on camera orientation
Positions arrows around block in world space
Converts to screen space for UI display
Placing a Block:
Player rotates camera → arrows smoothly animate to new quantized positions via DOTween
Player presses W (camera facing northeast at 30° pitch):
Camera pitch < 45° → Horizontal Mode
Camera yaw ≈ 45° → quantizes to Northeast diagonal
GetPlacementDirection(W) returns (0.707, 0, 0.707).normalized
BuildModeController.PlaceBlockInDirection:
New position: (4, 0, 5) + (0.707, 0, 0.707) * 1.0 = (4.707, 0, 5.707)
Rounded: (5, 0, 6)
Block rotation: Quaternion.LookRotation((0.707, 0, 0.707), Vector3.up) → facing NE
Instantiates block at (5, 0, 6) rotated 45° around Y-axis
Registers with adjacency grid
Updates orbit center to new block (5, 0, 6)
Arrows reposition around new block
Vertical Mode Example:
Player tilts camera down at 60° pitch, facing east:
Camera pitch ≥ 45° → Vertical Mode
camForwardFull ≈ (1, -1.732, 0).normalized = (0.5, -0.866, 0)
Player presses W:
QuantizeTo3DDiagonals((0.5, -0.866, 0)):
Horizontal component: (0.5, 0, 0) → quantizes to East (1, 0, 0)
Vertical component: -0.866 → quantizes to -1 (down)
Result: (1, 0, 0) + (0, -1, 0) = (1, -1, 0).normalized = (0.707, -0.707, 0)
Block placed diagonally down-east
Block rotation: Tilted 45° downward, facing east
Exiting Build Mode:
Player presses Space → BuildModeController.ExitBuildMode()
BuildModeController:
Re-enables player movement
Disables orbit override
Restores camera distance
Hides arrows
Camera returns to following player in third-person
Key Features
Grid Snapping:
All positions rounded to nearest 1.0 integer: Mathf.Round(x/y/z)
Prevents floating-point precision errors
BlockAdjacencyGrid uses Vector3Int for O(1) position lookups
Direction Quantization:
Horizontal mode: 8 directions at 45° increments (N, NE, E, SE, S, SW, W, NW)
Vertical mode: 26 possible directions (8 horizontals × 3 vertical states)
Camera-relative: W always places "forward" relative to camera view
Block Rotation:
Blocks rotate to face their placement direction
Uses Quaternion.LookRotation(direction, Vector3.up)
Horizontal placements: Rotate around Y-axis only
3D diagonal placements: Tilt and rotate to align with placement vector
Camera Orbit:
Fixed 8-unit distance (no zoom)
No vertical rotation limits in build mode
Smooth mouse control preserved
No obstacle detection in build mode
Camera updates even when player is frozen
Arrow UI:
4 TextMeshPro elements showing key labels
World-space positions converted to screen-space for UI overlay
DOTween smooth transitions when directions change (0.3s)
Arrows hide when behind camera
Compatible with render texture pixel effects via viewport coordinates
Player Freezing:
Player position continuously reset to savedPlayerPosition in Update()
Player avatar rotates once when entering build mode to face first block
Avatar doesn't rotate on subsequent block placements
File Dependencies

GameManager (singleton)
    ├─ playerController (SUPERCharacterAIO)
    ├─ arrowSystem (DirectionalArrowSystem)
    ├─ buildingSystem (BuildingSystem)
    └─ adjacencyGrid (BlockAdjacencyGrid)

BuildModeController
    └─ Uses all GameManager references

DirectionalArrowSystem
    ├─ Requires Canvas (auto-finds or creates)
    └─ Creates 4 TextMeshProUGUI elements

SUPERCharacterAIO (third-party plugin, modified)
    └─ playerCamera (Camera)
Technical Notes
DOTween Animation:
Used for smooth arrow transitions between quantized directions
ArrowPositionData wrapper class holds world position and tween reference
Tween killed when direction changes or arrows hidden
Easing: Ease.OutCubic for natural motion
Render Texture Compatibility:
UI uses WorldToViewportPoint() instead of WorldToScreenPoint()
Viewport coordinates (0-1) independent of render texture resolution
Multiplied by Screen.width/height for pixel positions
Requires Canvas in Screen Space - Overlay mode
Third-Party Integration:
SUPERCharacterAIO is a purchased asset
Minimal modifications to preserve compatibility:
Added build mode override fields
Conditional checks for build mode behavior
No changes to core movement logic
Performance:
Arrow positions updated every frame (Update loop)
Grid lookups are O(1) via Dictionary
DOTween manages animation efficiently
Only 4 UI elements active at once
This system provides intuitive 3D block placement with visual feedback, camera-relative controls, and support for both 2D (horizontal) and full 3D diagonal placement.