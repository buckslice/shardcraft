using UnityEngine;
using System.Collections;

public class CamModify : MonoBehaviour {

    RaycastVoxelHit lastHit;
    Vector3 lastPos;

    World world;

    DrawBounds drawer;
    Camera cam;

    bool drawChunkBorders = false;
    bool nightTime = true;

    Block[] blocks;
    int blockIndex = 0;
    int placementSize = 1;

    MeshFilter blockMeshFilter;

    Color startColor;
    bool isDebugDay = false;

    FlyCam flyCam;

    void Start() {
        // weird indexing to skip air
        blocks = new Block[Blocks.count - 1];
        for (int i = 1; i < Blocks.count; ++i) {
            blocks[i - 1] = new Block((byte)i);
        }

        cam = GetComponent<Camera>();
        flyCam = GetComponent<FlyCam>();
        startColor = cam.backgroundColor;
        ToggleDebugDay();

        world = FindObjectOfType<World>();

        drawer = GetComponent<DrawBounds>();

        blockMeshFilter = GetComponentInChildren<MeshFilter>();
        MeshBuilder.PrimeBasicBlock();
        MeshBuilder.GetBlockMesh(blocks[blockIndex], blockMeshFilter);
    }

    private void OnApplicationQuit() {
        MeshBuilder.DestroyBasicBlock();
    }

    private void OnApplicationFocus(bool focus) {
        if (focus) {
            Cursor.lockState = CursorLockMode.Locked;
        }
    }

    void ToggleDebugDay() {
        isDebugDay = !isDebugDay;
        if (isDebugDay) {
            cam.backgroundColor = startColor;
            RenderSettings.ambientLight = new Color(1, 1, 1);
        } else {
            cam.backgroundColor = Color.black;
            RenderSettings.ambientLight = Color.black;
        }
    }

    void Update() {

        // scroll to select blocks (create mesh as you switch)
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        bool changed = true;
        if (scroll > 0.0f) {
            blockIndex--;
        } else if (scroll < 0.0f) { // scroll down will progress list forward
            blockIndex++;
        } else {
            changed = false;
        }
        blockIndex = Mth.Mod(blockIndex, blocks.Length);
        if (changed) {
            MeshBuilder.GetBlockMesh(blocks[blockIndex], blockMeshFilter);
        }

        // show square around block aiming at
        // todo: have option to show 2x2 for hammering!
        //bool mainRaycast = Physics.Raycast(transform.position, transform.forward, out hit, 1000);
        drawer.Clear();

        RaycastVoxelHit vhit;
        bool success = BlonkPhysics.RaycastVoxel(world, transform.position, transform.forward, out vhit);
        if (success) {
            // move cube towards camera direction a little so it looks better when intersecting with other blocks
            Vector3 center = (vhit.bpos.ToVector3() + Vector3.one * placementSize * 0.5f) / Chunk.BPU;
            drawer.AddBounds(new Bounds(center - transform.forward * 0.01f, Vector3.one * placementSize / Chunk.BPU), Color.white);
        }

        // left click delete
        if (Input.GetMouseButtonDown(0) && success) {
            lastPos = transform.position;
            lastHit = vhit;

            //todo: add some better directional intuitions on where to place edit shape
            for (int y = 0; y < placementSize; ++y) {
                for (int z = 0; z < placementSize; ++z) {
                    for (int x = 0; x < placementSize; ++x) {
                        world.SetBlock(vhit.bpos + new Vector3i(x, y, z), Blocks.AIR);
                    }
                }
            }
        }

        // right click place
        if (Input.GetMouseButtonDown(1) && success) {
            lastPos = transform.position;
            lastHit = vhit;
            world.SetBlock(vhit.bpos + Dirs.GetNormal(vhit.dir), blocks[blockIndex]);
        }

        if (Input.GetKeyDown(KeyCode.UpArrow)) {
            placementSize++;
        }
        if (Input.GetKeyDown(KeyCode.DownArrow)) {
            placementSize--;
        }
        if (placementSize < 1) {
            placementSize = 1;
        }

        if (Input.GetKeyDown(KeyCode.F1)) {
            flyCam.ToggleFlyMode();
        }
        if (Input.GetKeyDown(KeyCode.F2)) {
            drawChunkBorders = !drawChunkBorders;
        }
        if (Input.GetKeyDown(KeyCode.F3)) {
            ToggleDebugDay();
        }
        if (Input.GetKeyDown(KeyCode.F4)) {
            LoadChunks.drawDebug = !LoadChunks.drawDebug;
        }
        if (Input.GetKeyDown(KeyCode.F5)) {
            LoadChunks.updateChunks = !LoadChunks.updateChunks;
        }
        if (drawChunkBorders) {
            DrawChunkBorders();
        }

    }

    //private void OnDrawGizmos() {
    //    Vector3 p = lastHit.bpos.ToVector3() / Chunk.BPU;
    //    Debug.DrawLine(lastPos, p, Color.green, 1.0f);
    //    Debug.DrawRay(p, Dirs.GetNormal(lastHit.dir).ToVector3(), Color.magenta, 1.0f);
    //}

    void DrawChunkBorders() {
        // local function to add chunks quick
        const float ws = Chunk.SIZE / Chunk.BPU; // world size
        void AddChunkBounds(Chunk c, Color col, float scale) {
            if (c != null) {
                drawer.AddBounds(new Bounds((c.cp.ToVector3() + Vector3.one * 0.5f) * ws, Vector3.one * ws * scale), col);
            }
        }
        // draw chunk and neighbors and region
        Vector3i cp = WorldUtils.GetChunkPosFromWorldPos(transform.position);
        Chunk chunk = world.GetChunk(cp);
        if (chunk != null) {
            const float step = 1.0f;
            Vector3 min = chunk.cp.ToVector3() * ws;
            // draw lines along the edge of chunk in each axis, skip the corners because were just gona draw a bounds cube for that
            for (float f = step; f < ws; f += step) {
                // x direction
                drawer.AddLine(new Vector3(min.x, min.y + f, min.z), new Vector3(min.x + ws, min.y + f, min.z), Color.yellow);
                drawer.AddLine(new Vector3(min.x, min.y, min.z + f), new Vector3(min.x + ws, min.y, min.z + f), Color.yellow);
                drawer.AddLine(new Vector3(min.x, min.y + f, min.z + ws), new Vector3(min.x + ws, min.y + f, min.z + ws), Color.yellow);
                drawer.AddLine(new Vector3(min.x, min.y + ws, min.z + f), new Vector3(min.x + ws, min.y + ws, min.z + f), Color.yellow);

                // z direction
                drawer.AddLine(new Vector3(min.x + f, min.y, min.z), new Vector3(min.x + f, min.y, min.z + ws), Color.yellow);
                drawer.AddLine(new Vector3(min.x, min.y + f, min.z), new Vector3(min.x, min.y + f, min.z + ws), Color.yellow);
                drawer.AddLine(new Vector3(min.x + f, min.y + ws, min.z), new Vector3(min.x + f, min.y + ws, min.z + ws), Color.yellow);
                drawer.AddLine(new Vector3(min.x + ws, min.y + f, min.z), new Vector3(min.x + ws, min.y + f, min.z + ws), Color.yellow);

                // y direction
                drawer.AddLine(new Vector3(min.x + f, min.y, min.z), new Vector3(min.x + f, min.y + ws, min.z), Color.yellow);
                drawer.AddLine(new Vector3(min.x, min.y, min.z + f), new Vector3(min.x, min.y + ws, min.z + f), Color.yellow);
                drawer.AddLine(new Vector3(min.x + f, min.y, min.z + ws), new Vector3(min.x + f, min.y + ws, min.z + ws), Color.yellow);
                drawer.AddLine(new Vector3(min.x + ws, min.y, min.z + f), new Vector3(min.x + ws, min.y + ws, min.z + f), Color.yellow);
            }

            AddChunkBounds(chunk, Color.yellow, 1.0f);
            for (int i = 0; i < 6; ++i) {
                AddChunkBounds(chunk.neighbors[i], Color.cyan, 0.998f);
            }

            Vector3i rc = WorldUtils.GetRegionCoord(chunk.cp);
            drawer.AddBounds(new Bounds((rc.ToVector3() + Vector3.one * 0.5f) * 16 * ws, Vector3.one * 16 * ws), Color.red);
        }
    }

}

