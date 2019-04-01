using UnityEngine;
using System.Collections;

public class CamModify : MonoBehaviour {

    RaycastHit lastHit;
    Vector3 lastPos;

    World world;

    DrawBounds drawer;

    bool drawChunkBorders = false;

    Block[] blocks = new Block[] {
        Blocks.TORCH,
        Blocks.GRASS,
        Blocks.STONE,
        Blocks.BIRCH,
    };
    int blockIndex = 0;

    MeshFilter blockMeshFilter;

    void Start() {
        world = FindObjectOfType<World>();

        drawer = GetComponent<DrawBounds>();

        blockMeshFilter = GetComponentInChildren<MeshFilter>();
        MeshBuilder.PrimeBasicBlock();
    }

    private void OnApplicationQuit() {
        MeshBuilder.DestroyBasicBlock();
    }

    void Update() {

        // scroll to select blocks
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
            // create a new block mesh
            MeshBuilder.GetBlockMesh(blocks[blockIndex], blockMeshFilter);
        }

        RaycastHit hit;

        // show square around block aiming at
        // todo: have option to show 2x2 for hammering!
        bool mainRaycast = Physics.Raycast(transform.position, transform.forward, out hit, 100);
        drawer.Clear();
        if (mainRaycast) {
            if (hit.collider.CompareTag(Tags.Terrain)) {
                Vector3i bp = WorldUtils.GetBlockPos(hit, false);

                // move cube towards camera direction a little so it looks better when intersecting with other blocks
                Vector3 center = (bp.ToVector3() + Vector3.one * 0.5f) / Chunk.BPU;
                drawer.AddBounds(new Bounds(center - transform.forward * 0.01f, Vector3.one / Chunk.BPU), Color.white);
            }
        }

        // left click delete
        if (Input.GetMouseButtonDown(0)) {
            if (mainRaycast) {
                lastPos = transform.position;
                lastHit = hit;
                WorldUtils.SetBlock(world, hit, Blocks.AIR);
            }
        }

        // right click place
        if (Input.GetMouseButtonDown(1)) {
            if (Physics.Raycast(transform.position, transform.forward, out hit, 100)) {
                lastPos = transform.position;
                lastHit = hit;
                WorldUtils.SetBlock(world, hit, blocks[blockIndex], true);
            }
        }

        if (Input.GetKeyDown(KeyCode.F1)) {
            drawChunkBorders = !drawChunkBorders;
        }
        if (drawChunkBorders) {
            DrawChunkBorders();
        }

        //if (Input.GetKeyDown(KeyCode.P)) {
        //    world.SwapGreedy();
        //}

    }

    private void OnDrawGizmos() {
        Debug.DrawLine(lastPos, lastHit.point, Color.green, 1.0f);
        Debug.DrawRay(lastHit.point, lastHit.normal, Color.magenta, 1.0f);

    }

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

