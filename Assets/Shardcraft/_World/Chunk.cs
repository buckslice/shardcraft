using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;

public class Chunk {
    public const int SIZE = 32;
    // equal for now but keeping if u want to change this later. would have to change away from array3 tho actually
    // also very untested lol so prob will megachoke
    public const int CHUNK_WIDTH = SIZE;
    public const int CHUNK_HEIGHT = SIZE;

    public const int BPU = 2; // blocks per unit

    //public Block[] blocks;

    public NativeArray<Block> blocks;

    public NativeArray<Light> light;

    public NativeList<Face> faces;

    public Queue<BlockEdit> pendingEdits = new Queue<BlockEdit>();

    public Queue<LightOp> lightOps = new Queue<LightOp>();

    public World world;
    public GameObject gameObject;
    public Vector3i bp { get; private set; } // world block space position (not pure world position!)
    public Vector3i cp { get; private set; } // chunk space

    public bool loaded { get; private set; }    // indicates block array is set correctly (either loaded from save or freshly gend)
    public bool update { get; set; }            // means need to update mesh for some reason
    public bool lightUpdate { get; set; }       // mesh needs to update only light
    public bool rendered { get; private set; }  // has a mesh (may be out of date)
    public bool builtStructures { get; set; }
    int dataLock = 0;   // if not zero means one or more jobs are reading / writing from the data
    public bool needToUpdateSave { get; set; } // only gets set when generated or modified a block
    public bool dying { get; set; } // set when chunk is in process of getting destroyed
    public bool needNewCollider { get; private set; }

    public MeshRenderer mr { get; set; }
    MeshFilter filter;
    MeshCollider coll;

    // w d s e u n
    public Chunk[] neighbors = new Chunk[6];

    public int loadedNeighbors = 0;
    // dont think having all 26 neighbors would be good. not too hard to say neighbors[0].neighbors[3] for example or something
    // 12 edge neighbors, uw, us, ue, un, sw, se, nw, ne, dw, ds, de, dn
    // 8 corner neighbors, usw, use, unw, une, dsw, dse, dnw, dne
    // or just figure out way to index them like this neighbors[-1,0,1]; so that would be up west neighbor
    // could just be a method that remaps -1,0,1 indices to 0,1,2 array

    public static bool beGreedy = false;

    public Chunk(GameObject gameObject) {
        this.gameObject = gameObject;

        blocks = new NativeArray<Block>(SIZE * SIZE * SIZE, Allocator.Persistent);
        light = new NativeArray<Light>(SIZE * SIZE * SIZE, Allocator.Persistent);
        faces = new NativeList<Face>(Allocator.Persistent);

        mr = gameObject.GetComponent<MeshRenderer>();
        filter = gameObject.GetComponent<MeshFilter>();
        coll = gameObject.GetComponent<MeshCollider>();

    }

    public void Initialize(World world, Vector3i bp, Vector3i cp) {
        this.world = world; // maybe world can change? i dunno would prob have its own pool
        this.bp = bp;
        this.cp = cp;

        loaded = false;
        update = false;
        lightUpdate = false;
        rendered = false;
        dataLock = 0;
        builtStructures = false;
        needToUpdateSave = false;
        dying = false;
        needNewCollider = true;
        loadedNeighbors = 0;

        faces.Capacity = 32;
        faces.Clear();

        gameObject.transform.position = bp.ToVector3() / BPU;
        gameObject.name = "Chunk " + cp;
        gameObject.SetActive(true);

        for (int i = 0; i < 6; ++i) {
            neighbors[i] = null;
        }

        mr.material = Chunk.beGreedy ? world.TileMatGreedy : world.TileMat;
    }

    public void SetLoaded() {
        loaded = true;
        world.UpdateNeighborsLoadedNeighbors(cp, true);
    }

    public void LockData() {
        ++dataLock;
    }

    public void UnlockData() {
        --dataLock;
        Debug.Assert(dataLock >= 0);
        TryApplyPendingEdits();
    }

    public bool IsDataLocked() {
        return dataLock > 0;
    }

    public void TryApplyPendingEdits() {
        if (dying) {
            return;
        }
        Debug.Assert(loaded);
        if (pendingEdits.Count > 0 && dataLock == 0) {
            int c = pendingEdits.Count;
            while (c-- > 0) { // just incase setblock fails, prob wont happen tho
                BlockEdit e = pendingEdits.Dequeue();
                SetBlock(e.x, e.y, e.z, e.block);
            }
            if (!NeighborsLoaded() || !IsLocalGroupFree()) {
                update = true;
            } else {    // slam out job right away if you can
                update = false;
                lightUpdate = false;
                JobController.StartMeshJob(this);
            }
        }
    }

    public void ClearMeshes() {
        filter.mesh.Clear();
        coll.sharedMesh = null;
    }

    // Updates the chunk based on its contents
    // returns whether or not a full update happens
    public bool UpdateChunk() {
        if (!loaded || dying || !NeighborsLoaded()) {
            return false;
        }

        if (!builtStructures) { // build structures like trees and such if you havent yet
            StructureGenerator.BuildStructures(this);
            builtStructures = true;
        }

        if (lightUpdate && !update && IsLocalGroupFree()) {
            lightUpdate = false;
            JobController.StartLightUpdateJob(this);
        } else if (update && IsLocalGroupFree()) {
            update = false;
            lightUpdate = false;
            JobController.StartMeshJob(this);
            return true;
        }

        return false;
    }

    public bool NeighborsLoaded() {
        Debug.Assert(loadedNeighbors >= 0 && loadedNeighbors <= 26);
        return loadedNeighbors == 26;
    }

    public void UpdateMeshNative(NativeList<Vector3> vertices, NativeList<Vector3> uvs, NativeList<Color32> colors, NativeList<int> triangles) {
        if (dying) {
            return;
        }
        if (triangles.Length < ushort.MaxValue) {
            filter.mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt16;
        } else {
            filter.mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        }

        filter.mesh.Clear();
        filter.mesh.vertices = vertices.ToArray();
        filter.mesh.SetUVs(0, new List<Vector3>(uvs.ToArray()));
        filter.mesh.colors32 = colors.ToArray();

        filter.mesh.triangles = triangles.ToArray();
        filter.mesh.RecalculateNormals();

        rendered = true;

        TryApplyPendingEdits();
    }

    public void UpdateMeshLight(NativeList<Color32> colors) {

        Color32[] meshCols = filter.mesh.colors32;
        Debug.Assert(meshCols.Length / 4 == colors.Length);

        for (int i = 0; i < colors.Length; ++i) {
            Color32 c = colors[i];
            meshCols[i * 4] = new Color32(c.r, c.g, c.b, meshCols[i * 4].a);
            meshCols[i * 4 + 1] = new Color32(c.r, c.g, c.b, meshCols[i * 4 + 1].a);
            meshCols[i * 4 + 2] = new Color32(c.r, c.g, c.b, meshCols[i * 4 + 2].a);
            meshCols[i * 4 + 3] = new Color32(c.r, c.g, c.b, meshCols[i * 4 + 3].a);
        }

        filter.mesh.colors32 = meshCols;
    }

    public void UpdateColliderNative(NativeList<Vector3> vertices, NativeList<int> triangles) {
        needNewCollider = false;
        if (dying) {
            return;
        }
        coll.sharedMesh = null;
        Mesh mesh = new Mesh(); // maybe reuse this?
        if (triangles.Length < ushort.MaxValue) {
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt16;
        } else {
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        }

        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.RecalculateNormals();
        coll.sharedMesh = mesh;
    }

    public Vector3 GetWorldPos() {
        return (bp / BPU).ToVector3();
    }

    public Block GetBlock(int x, int y, int z) {
        // return block if its in range of this chunk
        if (InRange(x, y, z)) {
            if (!loaded) {
                return Blocks.AIR;
            }
            return blocks[x + z * SIZE + y * SIZE * SIZE];
        }
        return world.GetBlock(bp.x + x, bp.y + y, bp.z + z);
    }

    // standard way to safely set the block in this chunk
    public void SetBlock(int x, int y, int z, Block block) {
        if (InRange(x, y, z)) {
            if (!loaded) {
                return;
            }
            if (dataLock > 0) {
                pendingEdits.Enqueue(new BlockEdit { x = x, y = y, z = z, block = block });
            } else {
                blocks[x + z * SIZE + y * SIZE * SIZE] = block;
                update = true;
                needToUpdateSave = true; // block was modified so need to update save
                needNewCollider = true; // block was changed so collider prob needs to be updated
                CheckNeedToUpdateNeighbors(x, y, z);
                lightOps.Enqueue(new LightOp { index = x + z * SIZE + y * SIZE * SIZE, val = block.GetType().GetLight() });
            }
        } else {
            world.SetBlock(bp.x + x, bp.y + y, bp.z + z, block);
        }
    }

    // if block is on a chunk edge then update neighbor chunks
    // given x,y,z of block in local chunk space, check if you need to update your neighbors
    void CheckNeedToUpdateNeighbors(int x, int y, int z) {
        Debug.Assert(InRange(x, y, z));

        if (x == 0 && neighbors[0] != null) {
            neighbors[0].update = true;
            neighbors[0].needNewCollider = true;
        } else if (x == SIZE - 1 && neighbors[3] != null) {
            neighbors[3].update = true;
            neighbors[3].needNewCollider = true;
        }
        if (y == 0 && neighbors[1] != null) {
            neighbors[1].update = true;
            neighbors[1].needNewCollider = true;
        } else if (y == SIZE - 1 && neighbors[4] != null) {
            neighbors[4].update = true;
            neighbors[4].needNewCollider = true;
        }
        if (z == 0 && neighbors[2] != null) {
            neighbors[2].update = true;
            neighbors[2].needNewCollider = true;
        } else if (z == SIZE - 1 && neighbors[5] != null) {
            neighbors[5].update = true;
            neighbors[5].needNewCollider = true;
        }

    }

    public static bool InRange(int x, int y, int z) {
        return x >= 0 && x < SIZE && y >= 0 && y < SIZE && z >= 0 && z < SIZE;
    }

    bool IsLocalGroupFree() {
        return dataLock == 0 &&
        neighbors[Dirs.WEST].dataLock == 0 &&
        neighbors[Dirs.DOWN].dataLock == 0 &&
        neighbors[Dirs.SOUTH].dataLock == 0 &&
        neighbors[Dirs.EAST].dataLock == 0 &&
        neighbors[Dirs.UP].dataLock == 0 &&
        neighbors[Dirs.NORTH].dataLock == 0 &&
        neighbors[Dirs.UP].neighbors[Dirs.WEST].dataLock == 0 &&
        neighbors[Dirs.UP].neighbors[Dirs.SOUTH].dataLock == 0 &&
        neighbors[Dirs.UP].neighbors[Dirs.EAST].dataLock == 0 &&
        neighbors[Dirs.UP].neighbors[Dirs.NORTH].dataLock == 0 &&
        neighbors[Dirs.DOWN].neighbors[Dirs.WEST].dataLock == 0 &&
        neighbors[Dirs.DOWN].neighbors[Dirs.SOUTH].dataLock == 0 &&
        neighbors[Dirs.DOWN].neighbors[Dirs.EAST].dataLock == 0 &&
        neighbors[Dirs.DOWN].neighbors[Dirs.NORTH].dataLock == 0 &&
        neighbors[Dirs.SOUTH].neighbors[Dirs.WEST].dataLock == 0 &&
        neighbors[Dirs.SOUTH].neighbors[Dirs.EAST].dataLock == 0 &&
        neighbors[Dirs.NORTH].neighbors[Dirs.WEST].dataLock == 0 &&
        neighbors[Dirs.NORTH].neighbors[Dirs.EAST].dataLock == 0 &&
        neighbors[Dirs.UP].neighbors[Dirs.SOUTH].neighbors[Dirs.WEST].dataLock == 0 &&
        neighbors[Dirs.UP].neighbors[Dirs.SOUTH].neighbors[Dirs.EAST].dataLock == 0 &&
        neighbors[Dirs.UP].neighbors[Dirs.NORTH].neighbors[Dirs.WEST].dataLock == 0 &&
        neighbors[Dirs.UP].neighbors[Dirs.NORTH].neighbors[Dirs.EAST].dataLock == 0 &&
        neighbors[Dirs.DOWN].neighbors[Dirs.SOUTH].neighbors[Dirs.WEST].dataLock == 0 &&
        neighbors[Dirs.DOWN].neighbors[Dirs.SOUTH].neighbors[Dirs.EAST].dataLock == 0 &&
        neighbors[Dirs.DOWN].neighbors[Dirs.NORTH].neighbors[Dirs.WEST].dataLock == 0 &&
        neighbors[Dirs.DOWN].neighbors[Dirs.NORTH].neighbors[Dirs.EAST].dataLock == 0;
    }

    public NativeArray3x3<Block> GetLocalBlocks() {
        return new NativeArray3x3<Block> {
            c = blocks,
            w = neighbors[Dirs.WEST].blocks,
            d = neighbors[Dirs.DOWN].blocks,
            s = neighbors[Dirs.SOUTH].blocks,
            e = neighbors[Dirs.EAST].blocks,
            u = neighbors[Dirs.UP].blocks,
            n = neighbors[Dirs.NORTH].blocks,
            uw = neighbors[Dirs.UP].neighbors[Dirs.WEST].blocks,
            us = neighbors[Dirs.UP].neighbors[Dirs.SOUTH].blocks,
            ue = neighbors[Dirs.UP].neighbors[Dirs.EAST].blocks,
            un = neighbors[Dirs.UP].neighbors[Dirs.NORTH].blocks,
            dw = neighbors[Dirs.DOWN].neighbors[Dirs.WEST].blocks,
            ds = neighbors[Dirs.DOWN].neighbors[Dirs.SOUTH].blocks,
            de = neighbors[Dirs.DOWN].neighbors[Dirs.EAST].blocks,
            dn = neighbors[Dirs.DOWN].neighbors[Dirs.NORTH].blocks,
            sw = neighbors[Dirs.SOUTH].neighbors[Dirs.WEST].blocks,
            se = neighbors[Dirs.SOUTH].neighbors[Dirs.EAST].blocks,
            nw = neighbors[Dirs.NORTH].neighbors[Dirs.WEST].blocks,
            ne = neighbors[Dirs.NORTH].neighbors[Dirs.EAST].blocks,
            usw = neighbors[Dirs.UP].neighbors[Dirs.SOUTH].neighbors[Dirs.WEST].blocks,
            use = neighbors[Dirs.UP].neighbors[Dirs.SOUTH].neighbors[Dirs.EAST].blocks,
            unw = neighbors[Dirs.UP].neighbors[Dirs.NORTH].neighbors[Dirs.WEST].blocks,
            une = neighbors[Dirs.UP].neighbors[Dirs.NORTH].neighbors[Dirs.EAST].blocks,
            dsw = neighbors[Dirs.DOWN].neighbors[Dirs.SOUTH].neighbors[Dirs.WEST].blocks,
            dse = neighbors[Dirs.DOWN].neighbors[Dirs.SOUTH].neighbors[Dirs.EAST].blocks,
            dnw = neighbors[Dirs.DOWN].neighbors[Dirs.NORTH].neighbors[Dirs.WEST].blocks,
            dne = neighbors[Dirs.DOWN].neighbors[Dirs.NORTH].neighbors[Dirs.EAST].blocks,
        };
    }

    public NativeArray3x3<Light> GetLocalLights() {
        return new NativeArray3x3<Light> {
            c = light,
            w = neighbors[Dirs.WEST].light,
            d = neighbors[Dirs.DOWN].light,
            s = neighbors[Dirs.SOUTH].light,
            e = neighbors[Dirs.EAST].light,
            u = neighbors[Dirs.UP].light,
            n = neighbors[Dirs.NORTH].light,
            uw = neighbors[Dirs.UP].neighbors[Dirs.WEST].light,
            us = neighbors[Dirs.UP].neighbors[Dirs.SOUTH].light,
            ue = neighbors[Dirs.UP].neighbors[Dirs.EAST].light,
            un = neighbors[Dirs.UP].neighbors[Dirs.NORTH].light,
            dw = neighbors[Dirs.DOWN].neighbors[Dirs.WEST].light,
            ds = neighbors[Dirs.DOWN].neighbors[Dirs.SOUTH].light,
            de = neighbors[Dirs.DOWN].neighbors[Dirs.EAST].light,
            dn = neighbors[Dirs.DOWN].neighbors[Dirs.NORTH].light,
            sw = neighbors[Dirs.SOUTH].neighbors[Dirs.WEST].light,
            se = neighbors[Dirs.SOUTH].neighbors[Dirs.EAST].light,
            nw = neighbors[Dirs.NORTH].neighbors[Dirs.WEST].light,
            ne = neighbors[Dirs.NORTH].neighbors[Dirs.EAST].light,
            usw = neighbors[Dirs.UP].neighbors[Dirs.SOUTH].neighbors[Dirs.WEST].light,
            use = neighbors[Dirs.UP].neighbors[Dirs.SOUTH].neighbors[Dirs.EAST].light,
            unw = neighbors[Dirs.UP].neighbors[Dirs.NORTH].neighbors[Dirs.WEST].light,
            une = neighbors[Dirs.UP].neighbors[Dirs.NORTH].neighbors[Dirs.EAST].light,
            dsw = neighbors[Dirs.DOWN].neighbors[Dirs.SOUTH].neighbors[Dirs.WEST].light,
            dse = neighbors[Dirs.DOWN].neighbors[Dirs.SOUTH].neighbors[Dirs.EAST].light,
            dnw = neighbors[Dirs.DOWN].neighbors[Dirs.NORTH].neighbors[Dirs.WEST].light,
            dne = neighbors[Dirs.DOWN].neighbors[Dirs.NORTH].neighbors[Dirs.EAST].light,
        };
    }


    public void LockLocalGroup() {
        LockData();
        neighbors[Dirs.WEST].LockData();
        neighbors[Dirs.DOWN].LockData();
        neighbors[Dirs.SOUTH].LockData();
        neighbors[Dirs.EAST].LockData();
        neighbors[Dirs.UP].LockData();
        neighbors[Dirs.NORTH].LockData();
        neighbors[Dirs.UP].neighbors[Dirs.WEST].LockData();
        neighbors[Dirs.UP].neighbors[Dirs.SOUTH].LockData();
        neighbors[Dirs.UP].neighbors[Dirs.EAST].LockData();
        neighbors[Dirs.UP].neighbors[Dirs.NORTH].LockData();
        neighbors[Dirs.DOWN].neighbors[Dirs.WEST].LockData();
        neighbors[Dirs.DOWN].neighbors[Dirs.SOUTH].LockData();
        neighbors[Dirs.DOWN].neighbors[Dirs.EAST].LockData();
        neighbors[Dirs.DOWN].neighbors[Dirs.NORTH].LockData();
        neighbors[Dirs.SOUTH].neighbors[Dirs.WEST].LockData();
        neighbors[Dirs.SOUTH].neighbors[Dirs.EAST].LockData();
        neighbors[Dirs.NORTH].neighbors[Dirs.WEST].LockData();
        neighbors[Dirs.NORTH].neighbors[Dirs.EAST].LockData();
        neighbors[Dirs.UP].neighbors[Dirs.SOUTH].neighbors[Dirs.WEST].LockData();
        neighbors[Dirs.UP].neighbors[Dirs.SOUTH].neighbors[Dirs.EAST].LockData();
        neighbors[Dirs.UP].neighbors[Dirs.NORTH].neighbors[Dirs.WEST].LockData();
        neighbors[Dirs.UP].neighbors[Dirs.NORTH].neighbors[Dirs.EAST].LockData();
        neighbors[Dirs.DOWN].neighbors[Dirs.SOUTH].neighbors[Dirs.WEST].LockData();
        neighbors[Dirs.DOWN].neighbors[Dirs.SOUTH].neighbors[Dirs.EAST].LockData();
        neighbors[Dirs.DOWN].neighbors[Dirs.NORTH].neighbors[Dirs.WEST].LockData();
        neighbors[Dirs.DOWN].neighbors[Dirs.NORTH].neighbors[Dirs.EAST].LockData();
    }

    public void UnlockLocalGroup() {
        UnlockData();
        neighbors[Dirs.WEST].UnlockData();
        neighbors[Dirs.DOWN].UnlockData();
        neighbors[Dirs.SOUTH].UnlockData();
        neighbors[Dirs.EAST].UnlockData();
        neighbors[Dirs.UP].UnlockData();
        neighbors[Dirs.NORTH].UnlockData();
        neighbors[Dirs.UP].neighbors[Dirs.WEST].UnlockData();
        neighbors[Dirs.UP].neighbors[Dirs.SOUTH].UnlockData();
        neighbors[Dirs.UP].neighbors[Dirs.EAST].UnlockData();
        neighbors[Dirs.UP].neighbors[Dirs.NORTH].UnlockData();
        neighbors[Dirs.DOWN].neighbors[Dirs.WEST].UnlockData();
        neighbors[Dirs.DOWN].neighbors[Dirs.SOUTH].UnlockData();
        neighbors[Dirs.DOWN].neighbors[Dirs.EAST].UnlockData();
        neighbors[Dirs.DOWN].neighbors[Dirs.NORTH].UnlockData();
        neighbors[Dirs.SOUTH].neighbors[Dirs.WEST].UnlockData();
        neighbors[Dirs.SOUTH].neighbors[Dirs.EAST].UnlockData();
        neighbors[Dirs.NORTH].neighbors[Dirs.WEST].UnlockData();
        neighbors[Dirs.NORTH].neighbors[Dirs.EAST].UnlockData();
        neighbors[Dirs.UP].neighbors[Dirs.SOUTH].neighbors[Dirs.WEST].UnlockData();
        neighbors[Dirs.UP].neighbors[Dirs.SOUTH].neighbors[Dirs.EAST].UnlockData();
        neighbors[Dirs.UP].neighbors[Dirs.NORTH].neighbors[Dirs.WEST].UnlockData();
        neighbors[Dirs.UP].neighbors[Dirs.NORTH].neighbors[Dirs.EAST].UnlockData();
        neighbors[Dirs.DOWN].neighbors[Dirs.SOUTH].neighbors[Dirs.WEST].UnlockData();
        neighbors[Dirs.DOWN].neighbors[Dirs.SOUTH].neighbors[Dirs.EAST].UnlockData();
        neighbors[Dirs.DOWN].neighbors[Dirs.NORTH].neighbors[Dirs.WEST].UnlockData();
        neighbors[Dirs.DOWN].neighbors[Dirs.NORTH].neighbors[Dirs.EAST].UnlockData();
    }
}
