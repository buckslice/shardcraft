using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using System.Runtime.CompilerServices;

public class Chunk {
    public const int SIZE = 32;
    // equal for now but keeping if u want to change this later. would have to change away from array3 tho actually
    // also very untested lol so prob will megachoke
    public const int CHUNK_WIDTH = SIZE;
    public const int CHUNK_HEIGHT = SIZE;

    public const int BPU = 2; // blocks per unit
    public const float BLOCK_SIZE = 1.0f / BPU;

    //public Block[] blocks;

    public NativeArray<Block> blocks;

    public NativeArray<Light> lights;

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
    public bool builtStructures { get; set; }   // indicates your structures are built (tho others can still build structures on you)

    int blockReaders = 0;   // how many jobs are reading from these blocks
    int lightReaders = 0;   // how many jobs are reading from these lights
    bool blockWriter = false;   // if any job is writing to these blocks (should only ever be one writer at a time)
    bool lightWriter = false;   // if any job is writing to these lights

    public bool needToUpdateSave { get; set; } // only gets set when generated or modified a block
    public bool dying { get; set; } // set when chunk is in process of getting destroyed
    public bool needNewCollider { get; private set; }

    public MeshRenderer mr { get; set; }
    MeshFilter filter;
    MeshCollider coll;

    // w d s e u n
    public Chunk[] neighbors = new Chunk[6];

    public int loadedNeighbors = 0; // 26 if whole local group is loaded

    public static bool beGreedy = false;

    public Chunk(GameObject gameObject) {
        this.gameObject = gameObject;

        blocks = new NativeArray<Block>(SIZE * SIZE * SIZE, Allocator.Persistent);// NativeArrayOptions.UninitializedMemory);
        lights = new NativeArray<Light>(SIZE * SIZE * SIZE, Allocator.Persistent);// NativeArrayOptions.UninitializedMemory);
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
        blockReaders = 0;
        lightReaders = 0;
        blockWriter = false;
        lightWriter = false;
        builtStructures = false;
        needToUpdateSave = false;
        dying = false;
        needNewCollider = true;
        loadedNeighbors = 0;

        faces.Clear();
        // just to make sure list doesnt get too large
        faces.Capacity = 32;

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

    public void TryApplyPendingEdits() {
        if (dying) {
            return;
        }
        Debug.Assert(loaded);
        if (pendingEdits.Count > 0 && blockReaders == 0 && !blockWriter) {
            int c = pendingEdits.Count;
            while (c-- > 0) { // just incase setblock fails, prob wont happen tho
                BlockEdit e = pendingEdits.Dequeue();
                SetBlock(e.x, e.y, e.z, e.block);
            }
            if (!NeighborsLoaded() || !IsLocalGroupFreeForMeshing()) {
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
    // returns whether or not a meshing happens
    public bool UpdateChunk() {
        if (!loaded || dying || !NeighborsLoaded()) {
            return false;
        }

        if (!builtStructures && IsLocalGroupFreeForStructuring()) { // build structures like trees and such if you havent yet
            JobController.StartStructureJob(this);
        } else if (lightUpdate && !update && IsLocalGroupFreeToUpdateLights()) {
            lightUpdate = false;
            JobController.StartLightUpdateJob(this);
        } else if (update && IsLocalGroupFreeForMeshing()) {
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
            if (!loaded || blockWriter) {
                return Blocks.AIR;
            }
            return blocks[x + z * SIZE + y * SIZE * SIZE];
        }
        return world.GetBlock(bp.x + x, bp.y + y, bp.z + z);
    }

    // standard way to safely set the block in this chunk
    public void SetBlock(int x, int y, int z, Block block) {
        if (InRange(x, y, z)) {
            if (!loaded || !builtStructures) {
                return;
            }
            if (blockWriter || blockReaders > 0) {
                pendingEdits.Enqueue(new BlockEdit { x = x, y = y, z = z, block = block });
            } else {
                blocks[x + z * SIZE + y * SIZE * SIZE] = block;
                update = true;
                needToUpdateSave = true; // block was modified so need to update save
                needNewCollider = true; // block was changed so collider prob needs to be updated
                CheckNeedToUpdateNeighbors(x, y, z);
                lightOps.Enqueue(new LightOp { index = x + z * SIZE + y * SIZE * SIZE, val = BlockDatas.GetBlockData(block).light });
            }
        } else {
            world.SetBlock(bp.x + x, bp.y + y, bp.z + z, block);
        }
    }

    // called by structure job
    public void BlocksWereUpdated() {
        update = true;
        needToUpdateSave = true;
        needNewCollider = true;
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool InRange(int x, int y, int z) {
        return x >= 0 && x < SIZE && y >= 0 && y < SIZE && z >= 0 && z < SIZE;
    }


    // nobody can write or read to lights and nobody can write to blocks
    public bool FreeToMesh() {
        return lightReaders == 0 && !lightWriter && !blockWriter;
    }

    public void LockForMeshing() {
        Debug.Assert(lightWriter == false);
        lightWriter = true; // meshing process potentially writes to lights
        lightReaders++;
        blockReaders++;
    }

    public void UnlockForMeshing() {
        lightWriter = false;
        lightReaders--;
        blockReaders--;

        Debug.Assert(lightReaders >= 0);
        Debug.Assert(blockReaders >= 0);

        TryApplyPendingEdits();
    }

    public bool FreeToUpdateLights() {
        return !lightWriter;    // just make sure nobody is writing to lights
    }

    public void LockForLightUpdate() {
        lightReaders++;
    }

    public void UnlockForLightUpdate() {
        lightReaders--;
    }

    public bool FreeForStructuring() {
        return !blockWriter && blockReaders == 0; // need read write access for blocks
    }

    public void LockForStructuring() {
        Debug.Assert(!blockWriter);
        blockWriter = true;
        blockReaders++;
    }

    public void UnlockForStructuring() {
        blockWriter = false;
        blockReaders--;
    }

    public bool IsAnyDataInUse() {
        return blockWriter || lightWriter || blockReaders > 0 || lightReaders > 0;
    }

    public bool IsLocalGroupFreeForMeshing() {
        return FreeToMesh() &&
        neighbors[Dirs.WEST].FreeToMesh() &&
        neighbors[Dirs.DOWN].FreeToMesh() &&
        neighbors[Dirs.SOUTH].FreeToMesh() &&
        neighbors[Dirs.EAST].FreeToMesh() &&
        neighbors[Dirs.UP].FreeToMesh() &&
        neighbors[Dirs.NORTH].FreeToMesh() &&
        neighbors[Dirs.UP].neighbors[Dirs.WEST].FreeToMesh() &&
        neighbors[Dirs.UP].neighbors[Dirs.SOUTH].FreeToMesh() &&
        neighbors[Dirs.UP].neighbors[Dirs.EAST].FreeToMesh() &&
        neighbors[Dirs.UP].neighbors[Dirs.NORTH].FreeToMesh() &&
        neighbors[Dirs.DOWN].neighbors[Dirs.WEST].FreeToMesh() &&
        neighbors[Dirs.DOWN].neighbors[Dirs.SOUTH].FreeToMesh() &&
        neighbors[Dirs.DOWN].neighbors[Dirs.EAST].FreeToMesh() &&
        neighbors[Dirs.DOWN].neighbors[Dirs.NORTH].FreeToMesh() &&
        neighbors[Dirs.SOUTH].neighbors[Dirs.WEST].FreeToMesh() &&
        neighbors[Dirs.SOUTH].neighbors[Dirs.EAST].FreeToMesh() &&
        neighbors[Dirs.NORTH].neighbors[Dirs.WEST].FreeToMesh() &&
        neighbors[Dirs.NORTH].neighbors[Dirs.EAST].FreeToMesh() &&
        neighbors[Dirs.UP].neighbors[Dirs.SOUTH].neighbors[Dirs.WEST].FreeToMesh() &&
        neighbors[Dirs.UP].neighbors[Dirs.SOUTH].neighbors[Dirs.EAST].FreeToMesh() &&
        neighbors[Dirs.UP].neighbors[Dirs.NORTH].neighbors[Dirs.WEST].FreeToMesh() &&
        neighbors[Dirs.UP].neighbors[Dirs.NORTH].neighbors[Dirs.EAST].FreeToMesh() &&
        neighbors[Dirs.DOWN].neighbors[Dirs.SOUTH].neighbors[Dirs.WEST].FreeToMesh() &&
        neighbors[Dirs.DOWN].neighbors[Dirs.SOUTH].neighbors[Dirs.EAST].FreeToMesh() &&
        neighbors[Dirs.DOWN].neighbors[Dirs.NORTH].neighbors[Dirs.WEST].FreeToMesh() &&
        neighbors[Dirs.DOWN].neighbors[Dirs.NORTH].neighbors[Dirs.EAST].FreeToMesh();
    }

    public void LockLocalGroupForMeshing() {
        LockForMeshing();
        neighbors[Dirs.WEST].LockForMeshing();
        neighbors[Dirs.DOWN].LockForMeshing();
        neighbors[Dirs.SOUTH].LockForMeshing();
        neighbors[Dirs.EAST].LockForMeshing();
        neighbors[Dirs.UP].LockForMeshing();
        neighbors[Dirs.NORTH].LockForMeshing();
        neighbors[Dirs.UP].neighbors[Dirs.WEST].LockForMeshing();
        neighbors[Dirs.UP].neighbors[Dirs.SOUTH].LockForMeshing();
        neighbors[Dirs.UP].neighbors[Dirs.EAST].LockForMeshing();
        neighbors[Dirs.UP].neighbors[Dirs.NORTH].LockForMeshing();
        neighbors[Dirs.DOWN].neighbors[Dirs.WEST].LockForMeshing();
        neighbors[Dirs.DOWN].neighbors[Dirs.SOUTH].LockForMeshing();
        neighbors[Dirs.DOWN].neighbors[Dirs.EAST].LockForMeshing();
        neighbors[Dirs.DOWN].neighbors[Dirs.NORTH].LockForMeshing();
        neighbors[Dirs.SOUTH].neighbors[Dirs.WEST].LockForMeshing();
        neighbors[Dirs.SOUTH].neighbors[Dirs.EAST].LockForMeshing();
        neighbors[Dirs.NORTH].neighbors[Dirs.WEST].LockForMeshing();
        neighbors[Dirs.NORTH].neighbors[Dirs.EAST].LockForMeshing();
        neighbors[Dirs.UP].neighbors[Dirs.SOUTH].neighbors[Dirs.WEST].LockForMeshing();
        neighbors[Dirs.UP].neighbors[Dirs.SOUTH].neighbors[Dirs.EAST].LockForMeshing();
        neighbors[Dirs.UP].neighbors[Dirs.NORTH].neighbors[Dirs.WEST].LockForMeshing();
        neighbors[Dirs.UP].neighbors[Dirs.NORTH].neighbors[Dirs.EAST].LockForMeshing();
        neighbors[Dirs.DOWN].neighbors[Dirs.SOUTH].neighbors[Dirs.WEST].LockForMeshing();
        neighbors[Dirs.DOWN].neighbors[Dirs.SOUTH].neighbors[Dirs.EAST].LockForMeshing();
        neighbors[Dirs.DOWN].neighbors[Dirs.NORTH].neighbors[Dirs.WEST].LockForMeshing();
        neighbors[Dirs.DOWN].neighbors[Dirs.NORTH].neighbors[Dirs.EAST].LockForMeshing();
    }

    public void UnlockLocalGroupForMeshing() {
        UnlockForMeshing();
        neighbors[Dirs.WEST].UnlockForMeshing();
        neighbors[Dirs.DOWN].UnlockForMeshing();
        neighbors[Dirs.SOUTH].UnlockForMeshing();
        neighbors[Dirs.EAST].UnlockForMeshing();
        neighbors[Dirs.UP].UnlockForMeshing();
        neighbors[Dirs.NORTH].UnlockForMeshing();
        neighbors[Dirs.UP].neighbors[Dirs.WEST].UnlockForMeshing();
        neighbors[Dirs.UP].neighbors[Dirs.SOUTH].UnlockForMeshing();
        neighbors[Dirs.UP].neighbors[Dirs.EAST].UnlockForMeshing();
        neighbors[Dirs.UP].neighbors[Dirs.NORTH].UnlockForMeshing();
        neighbors[Dirs.DOWN].neighbors[Dirs.WEST].UnlockForMeshing();
        neighbors[Dirs.DOWN].neighbors[Dirs.SOUTH].UnlockForMeshing();
        neighbors[Dirs.DOWN].neighbors[Dirs.EAST].UnlockForMeshing();
        neighbors[Dirs.DOWN].neighbors[Dirs.NORTH].UnlockForMeshing();
        neighbors[Dirs.SOUTH].neighbors[Dirs.WEST].UnlockForMeshing();
        neighbors[Dirs.SOUTH].neighbors[Dirs.EAST].UnlockForMeshing();
        neighbors[Dirs.NORTH].neighbors[Dirs.WEST].UnlockForMeshing();
        neighbors[Dirs.NORTH].neighbors[Dirs.EAST].UnlockForMeshing();
        neighbors[Dirs.UP].neighbors[Dirs.SOUTH].neighbors[Dirs.WEST].UnlockForMeshing();
        neighbors[Dirs.UP].neighbors[Dirs.SOUTH].neighbors[Dirs.EAST].UnlockForMeshing();
        neighbors[Dirs.UP].neighbors[Dirs.NORTH].neighbors[Dirs.WEST].UnlockForMeshing();
        neighbors[Dirs.UP].neighbors[Dirs.NORTH].neighbors[Dirs.EAST].UnlockForMeshing();
        neighbors[Dirs.DOWN].neighbors[Dirs.SOUTH].neighbors[Dirs.WEST].UnlockForMeshing();
        neighbors[Dirs.DOWN].neighbors[Dirs.SOUTH].neighbors[Dirs.EAST].UnlockForMeshing();
        neighbors[Dirs.DOWN].neighbors[Dirs.NORTH].neighbors[Dirs.WEST].UnlockForMeshing();
        neighbors[Dirs.DOWN].neighbors[Dirs.NORTH].neighbors[Dirs.EAST].UnlockForMeshing();
    }

    public bool IsLocalGroupFreeToUpdateLights() {
        return FreeToUpdateLights() &&
        neighbors[Dirs.WEST].FreeToUpdateLights() &&
        neighbors[Dirs.DOWN].FreeToUpdateLights() &&
        neighbors[Dirs.SOUTH].FreeToUpdateLights() &&
        neighbors[Dirs.EAST].FreeToUpdateLights() &&
        neighbors[Dirs.UP].FreeToUpdateLights() &&
        neighbors[Dirs.NORTH].FreeToUpdateLights() &&
        neighbors[Dirs.UP].neighbors[Dirs.WEST].FreeToUpdateLights() &&
        neighbors[Dirs.UP].neighbors[Dirs.SOUTH].FreeToUpdateLights() &&
        neighbors[Dirs.UP].neighbors[Dirs.EAST].FreeToUpdateLights() &&
        neighbors[Dirs.UP].neighbors[Dirs.NORTH].FreeToUpdateLights() &&
        neighbors[Dirs.DOWN].neighbors[Dirs.WEST].FreeToUpdateLights() &&
        neighbors[Dirs.DOWN].neighbors[Dirs.SOUTH].FreeToUpdateLights() &&
        neighbors[Dirs.DOWN].neighbors[Dirs.EAST].FreeToUpdateLights() &&
        neighbors[Dirs.DOWN].neighbors[Dirs.NORTH].FreeToUpdateLights() &&
        neighbors[Dirs.SOUTH].neighbors[Dirs.WEST].FreeToUpdateLights() &&
        neighbors[Dirs.SOUTH].neighbors[Dirs.EAST].FreeToUpdateLights() &&
        neighbors[Dirs.NORTH].neighbors[Dirs.WEST].FreeToUpdateLights() &&
        neighbors[Dirs.NORTH].neighbors[Dirs.EAST].FreeToUpdateLights() &&
        neighbors[Dirs.UP].neighbors[Dirs.SOUTH].neighbors[Dirs.WEST].FreeToUpdateLights() &&
        neighbors[Dirs.UP].neighbors[Dirs.SOUTH].neighbors[Dirs.EAST].FreeToUpdateLights() &&
        neighbors[Dirs.UP].neighbors[Dirs.NORTH].neighbors[Dirs.WEST].FreeToUpdateLights() &&
        neighbors[Dirs.UP].neighbors[Dirs.NORTH].neighbors[Dirs.EAST].FreeToUpdateLights() &&
        neighbors[Dirs.DOWN].neighbors[Dirs.SOUTH].neighbors[Dirs.WEST].FreeToUpdateLights() &&
        neighbors[Dirs.DOWN].neighbors[Dirs.SOUTH].neighbors[Dirs.EAST].FreeToUpdateLights() &&
        neighbors[Dirs.DOWN].neighbors[Dirs.NORTH].neighbors[Dirs.WEST].FreeToUpdateLights() &&
        neighbors[Dirs.DOWN].neighbors[Dirs.NORTH].neighbors[Dirs.EAST].FreeToUpdateLights();
    }

    public void LockLocalGroupForLightUpdate() {
        LockForLightUpdate();
        neighbors[Dirs.WEST].LockForLightUpdate();
        neighbors[Dirs.DOWN].LockForLightUpdate();
        neighbors[Dirs.SOUTH].LockForLightUpdate();
        neighbors[Dirs.EAST].LockForLightUpdate();
        neighbors[Dirs.UP].LockForLightUpdate();
        neighbors[Dirs.NORTH].LockForLightUpdate();
        neighbors[Dirs.UP].neighbors[Dirs.WEST].LockForLightUpdate();
        neighbors[Dirs.UP].neighbors[Dirs.SOUTH].LockForLightUpdate();
        neighbors[Dirs.UP].neighbors[Dirs.EAST].LockForLightUpdate();
        neighbors[Dirs.UP].neighbors[Dirs.NORTH].LockForLightUpdate();
        neighbors[Dirs.DOWN].neighbors[Dirs.WEST].LockForLightUpdate();
        neighbors[Dirs.DOWN].neighbors[Dirs.SOUTH].LockForLightUpdate();
        neighbors[Dirs.DOWN].neighbors[Dirs.EAST].LockForLightUpdate();
        neighbors[Dirs.DOWN].neighbors[Dirs.NORTH].LockForLightUpdate();
        neighbors[Dirs.SOUTH].neighbors[Dirs.WEST].LockForLightUpdate();
        neighbors[Dirs.SOUTH].neighbors[Dirs.EAST].LockForLightUpdate();
        neighbors[Dirs.NORTH].neighbors[Dirs.WEST].LockForLightUpdate();
        neighbors[Dirs.NORTH].neighbors[Dirs.EAST].LockForLightUpdate();
        neighbors[Dirs.UP].neighbors[Dirs.SOUTH].neighbors[Dirs.WEST].LockForLightUpdate();
        neighbors[Dirs.UP].neighbors[Dirs.SOUTH].neighbors[Dirs.EAST].LockForLightUpdate();
        neighbors[Dirs.UP].neighbors[Dirs.NORTH].neighbors[Dirs.WEST].LockForLightUpdate();
        neighbors[Dirs.UP].neighbors[Dirs.NORTH].neighbors[Dirs.EAST].LockForLightUpdate();
        neighbors[Dirs.DOWN].neighbors[Dirs.SOUTH].neighbors[Dirs.WEST].LockForLightUpdate();
        neighbors[Dirs.DOWN].neighbors[Dirs.SOUTH].neighbors[Dirs.EAST].LockForLightUpdate();
        neighbors[Dirs.DOWN].neighbors[Dirs.NORTH].neighbors[Dirs.WEST].LockForLightUpdate();
        neighbors[Dirs.DOWN].neighbors[Dirs.NORTH].neighbors[Dirs.EAST].LockForLightUpdate();
    }

    public void UnlockLocalGroupForLightUpdate() {
        UnlockForLightUpdate();
        neighbors[Dirs.WEST].UnlockForLightUpdate();
        neighbors[Dirs.DOWN].UnlockForLightUpdate();
        neighbors[Dirs.SOUTH].UnlockForLightUpdate();
        neighbors[Dirs.EAST].UnlockForLightUpdate();
        neighbors[Dirs.UP].UnlockForLightUpdate();
        neighbors[Dirs.NORTH].UnlockForLightUpdate();
        neighbors[Dirs.UP].neighbors[Dirs.WEST].UnlockForLightUpdate();
        neighbors[Dirs.UP].neighbors[Dirs.SOUTH].UnlockForLightUpdate();
        neighbors[Dirs.UP].neighbors[Dirs.EAST].UnlockForLightUpdate();
        neighbors[Dirs.UP].neighbors[Dirs.NORTH].UnlockForLightUpdate();
        neighbors[Dirs.DOWN].neighbors[Dirs.WEST].UnlockForLightUpdate();
        neighbors[Dirs.DOWN].neighbors[Dirs.SOUTH].UnlockForLightUpdate();
        neighbors[Dirs.DOWN].neighbors[Dirs.EAST].UnlockForLightUpdate();
        neighbors[Dirs.DOWN].neighbors[Dirs.NORTH].UnlockForLightUpdate();
        neighbors[Dirs.SOUTH].neighbors[Dirs.WEST].UnlockForLightUpdate();
        neighbors[Dirs.SOUTH].neighbors[Dirs.EAST].UnlockForLightUpdate();
        neighbors[Dirs.NORTH].neighbors[Dirs.WEST].UnlockForLightUpdate();
        neighbors[Dirs.NORTH].neighbors[Dirs.EAST].UnlockForLightUpdate();
        neighbors[Dirs.UP].neighbors[Dirs.SOUTH].neighbors[Dirs.WEST].UnlockForLightUpdate();
        neighbors[Dirs.UP].neighbors[Dirs.SOUTH].neighbors[Dirs.EAST].UnlockForLightUpdate();
        neighbors[Dirs.UP].neighbors[Dirs.NORTH].neighbors[Dirs.WEST].UnlockForLightUpdate();
        neighbors[Dirs.UP].neighbors[Dirs.NORTH].neighbors[Dirs.EAST].UnlockForLightUpdate();
        neighbors[Dirs.DOWN].neighbors[Dirs.SOUTH].neighbors[Dirs.WEST].UnlockForLightUpdate();
        neighbors[Dirs.DOWN].neighbors[Dirs.SOUTH].neighbors[Dirs.EAST].UnlockForLightUpdate();
        neighbors[Dirs.DOWN].neighbors[Dirs.NORTH].neighbors[Dirs.WEST].UnlockForLightUpdate();
        neighbors[Dirs.DOWN].neighbors[Dirs.NORTH].neighbors[Dirs.EAST].UnlockForLightUpdate();
    }

    public bool IsLocalGroupFreeForStructuring() {
        return FreeForStructuring() &&
        neighbors[Dirs.WEST].FreeForStructuring() &&
        neighbors[Dirs.DOWN].FreeForStructuring() &&
        neighbors[Dirs.SOUTH].FreeForStructuring() &&
        neighbors[Dirs.EAST].FreeForStructuring() &&
        neighbors[Dirs.UP].FreeForStructuring() &&
        neighbors[Dirs.NORTH].FreeForStructuring() &&
        neighbors[Dirs.UP].neighbors[Dirs.WEST].FreeForStructuring() &&
        neighbors[Dirs.UP].neighbors[Dirs.SOUTH].FreeForStructuring() &&
        neighbors[Dirs.UP].neighbors[Dirs.EAST].FreeForStructuring() &&
        neighbors[Dirs.UP].neighbors[Dirs.NORTH].FreeForStructuring() &&
        neighbors[Dirs.DOWN].neighbors[Dirs.WEST].FreeForStructuring() &&
        neighbors[Dirs.DOWN].neighbors[Dirs.SOUTH].FreeForStructuring() &&
        neighbors[Dirs.DOWN].neighbors[Dirs.EAST].FreeForStructuring() &&
        neighbors[Dirs.DOWN].neighbors[Dirs.NORTH].FreeForStructuring() &&
        neighbors[Dirs.SOUTH].neighbors[Dirs.WEST].FreeForStructuring() &&
        neighbors[Dirs.SOUTH].neighbors[Dirs.EAST].FreeForStructuring() &&
        neighbors[Dirs.NORTH].neighbors[Dirs.WEST].FreeForStructuring() &&
        neighbors[Dirs.NORTH].neighbors[Dirs.EAST].FreeForStructuring() &&
        neighbors[Dirs.UP].neighbors[Dirs.SOUTH].neighbors[Dirs.WEST].FreeForStructuring() &&
        neighbors[Dirs.UP].neighbors[Dirs.SOUTH].neighbors[Dirs.EAST].FreeForStructuring() &&
        neighbors[Dirs.UP].neighbors[Dirs.NORTH].neighbors[Dirs.WEST].FreeForStructuring() &&
        neighbors[Dirs.UP].neighbors[Dirs.NORTH].neighbors[Dirs.EAST].FreeForStructuring() &&
        neighbors[Dirs.DOWN].neighbors[Dirs.SOUTH].neighbors[Dirs.WEST].FreeForStructuring() &&
        neighbors[Dirs.DOWN].neighbors[Dirs.SOUTH].neighbors[Dirs.EAST].FreeForStructuring() &&
        neighbors[Dirs.DOWN].neighbors[Dirs.NORTH].neighbors[Dirs.WEST].FreeForStructuring() &&
        neighbors[Dirs.DOWN].neighbors[Dirs.NORTH].neighbors[Dirs.EAST].FreeForStructuring();
    }

    public void LockLocalGroupForStructuring() {
        LockForStructuring();
        neighbors[Dirs.WEST].LockForStructuring();
        neighbors[Dirs.DOWN].LockForStructuring();
        neighbors[Dirs.SOUTH].LockForStructuring();
        neighbors[Dirs.EAST].LockForStructuring();
        neighbors[Dirs.UP].LockForStructuring();
        neighbors[Dirs.NORTH].LockForStructuring();
        neighbors[Dirs.UP].neighbors[Dirs.WEST].LockForStructuring();
        neighbors[Dirs.UP].neighbors[Dirs.SOUTH].LockForStructuring();
        neighbors[Dirs.UP].neighbors[Dirs.EAST].LockForStructuring();
        neighbors[Dirs.UP].neighbors[Dirs.NORTH].LockForStructuring();
        neighbors[Dirs.DOWN].neighbors[Dirs.WEST].LockForStructuring();
        neighbors[Dirs.DOWN].neighbors[Dirs.SOUTH].LockForStructuring();
        neighbors[Dirs.DOWN].neighbors[Dirs.EAST].LockForStructuring();
        neighbors[Dirs.DOWN].neighbors[Dirs.NORTH].LockForStructuring();
        neighbors[Dirs.SOUTH].neighbors[Dirs.WEST].LockForStructuring();
        neighbors[Dirs.SOUTH].neighbors[Dirs.EAST].LockForStructuring();
        neighbors[Dirs.NORTH].neighbors[Dirs.WEST].LockForStructuring();
        neighbors[Dirs.NORTH].neighbors[Dirs.EAST].LockForStructuring();
        neighbors[Dirs.UP].neighbors[Dirs.SOUTH].neighbors[Dirs.WEST].LockForStructuring();
        neighbors[Dirs.UP].neighbors[Dirs.SOUTH].neighbors[Dirs.EAST].LockForStructuring();
        neighbors[Dirs.UP].neighbors[Dirs.NORTH].neighbors[Dirs.WEST].LockForStructuring();
        neighbors[Dirs.UP].neighbors[Dirs.NORTH].neighbors[Dirs.EAST].LockForStructuring();
        neighbors[Dirs.DOWN].neighbors[Dirs.SOUTH].neighbors[Dirs.WEST].LockForStructuring();
        neighbors[Dirs.DOWN].neighbors[Dirs.SOUTH].neighbors[Dirs.EAST].LockForStructuring();
        neighbors[Dirs.DOWN].neighbors[Dirs.NORTH].neighbors[Dirs.WEST].LockForStructuring();
        neighbors[Dirs.DOWN].neighbors[Dirs.NORTH].neighbors[Dirs.EAST].LockForStructuring();
    }

    public void UnlockLocalGroupForStructuring() {
        UnlockForStructuring();
        neighbors[Dirs.WEST].UnlockForStructuring();
        neighbors[Dirs.DOWN].UnlockForStructuring();
        neighbors[Dirs.SOUTH].UnlockForStructuring();
        neighbors[Dirs.EAST].UnlockForStructuring();
        neighbors[Dirs.UP].UnlockForStructuring();
        neighbors[Dirs.NORTH].UnlockForStructuring();
        neighbors[Dirs.UP].neighbors[Dirs.WEST].UnlockForStructuring();
        neighbors[Dirs.UP].neighbors[Dirs.SOUTH].UnlockForStructuring();
        neighbors[Dirs.UP].neighbors[Dirs.EAST].UnlockForStructuring();
        neighbors[Dirs.UP].neighbors[Dirs.NORTH].UnlockForStructuring();
        neighbors[Dirs.DOWN].neighbors[Dirs.WEST].UnlockForStructuring();
        neighbors[Dirs.DOWN].neighbors[Dirs.SOUTH].UnlockForStructuring();
        neighbors[Dirs.DOWN].neighbors[Dirs.EAST].UnlockForStructuring();
        neighbors[Dirs.DOWN].neighbors[Dirs.NORTH].UnlockForStructuring();
        neighbors[Dirs.SOUTH].neighbors[Dirs.WEST].UnlockForStructuring();
        neighbors[Dirs.SOUTH].neighbors[Dirs.EAST].UnlockForStructuring();
        neighbors[Dirs.NORTH].neighbors[Dirs.WEST].UnlockForStructuring();
        neighbors[Dirs.NORTH].neighbors[Dirs.EAST].UnlockForStructuring();
        neighbors[Dirs.UP].neighbors[Dirs.SOUTH].neighbors[Dirs.WEST].UnlockForStructuring();
        neighbors[Dirs.UP].neighbors[Dirs.SOUTH].neighbors[Dirs.EAST].UnlockForStructuring();
        neighbors[Dirs.UP].neighbors[Dirs.NORTH].neighbors[Dirs.WEST].UnlockForStructuring();
        neighbors[Dirs.UP].neighbors[Dirs.NORTH].neighbors[Dirs.EAST].UnlockForStructuring();
        neighbors[Dirs.DOWN].neighbors[Dirs.SOUTH].neighbors[Dirs.WEST].UnlockForStructuring();
        neighbors[Dirs.DOWN].neighbors[Dirs.SOUTH].neighbors[Dirs.EAST].UnlockForStructuring();
        neighbors[Dirs.DOWN].neighbors[Dirs.NORTH].neighbors[Dirs.WEST].UnlockForStructuring();
        neighbors[Dirs.DOWN].neighbors[Dirs.NORTH].neighbors[Dirs.EAST].UnlockForStructuring();
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
            c = lights,
            w = neighbors[Dirs.WEST].lights,
            d = neighbors[Dirs.DOWN].lights,
            s = neighbors[Dirs.SOUTH].lights,
            e = neighbors[Dirs.EAST].lights,
            u = neighbors[Dirs.UP].lights,
            n = neighbors[Dirs.NORTH].lights,
            uw = neighbors[Dirs.UP].neighbors[Dirs.WEST].lights,
            us = neighbors[Dirs.UP].neighbors[Dirs.SOUTH].lights,
            ue = neighbors[Dirs.UP].neighbors[Dirs.EAST].lights,
            un = neighbors[Dirs.UP].neighbors[Dirs.NORTH].lights,
            dw = neighbors[Dirs.DOWN].neighbors[Dirs.WEST].lights,
            ds = neighbors[Dirs.DOWN].neighbors[Dirs.SOUTH].lights,
            de = neighbors[Dirs.DOWN].neighbors[Dirs.EAST].lights,
            dn = neighbors[Dirs.DOWN].neighbors[Dirs.NORTH].lights,
            sw = neighbors[Dirs.SOUTH].neighbors[Dirs.WEST].lights,
            se = neighbors[Dirs.SOUTH].neighbors[Dirs.EAST].lights,
            nw = neighbors[Dirs.NORTH].neighbors[Dirs.WEST].lights,
            ne = neighbors[Dirs.NORTH].neighbors[Dirs.EAST].lights,
            usw = neighbors[Dirs.UP].neighbors[Dirs.SOUTH].neighbors[Dirs.WEST].lights,
            use = neighbors[Dirs.UP].neighbors[Dirs.SOUTH].neighbors[Dirs.EAST].lights,
            unw = neighbors[Dirs.UP].neighbors[Dirs.NORTH].neighbors[Dirs.WEST].lights,
            une = neighbors[Dirs.UP].neighbors[Dirs.NORTH].neighbors[Dirs.EAST].lights,
            dsw = neighbors[Dirs.DOWN].neighbors[Dirs.SOUTH].neighbors[Dirs.WEST].lights,
            dse = neighbors[Dirs.DOWN].neighbors[Dirs.SOUTH].neighbors[Dirs.EAST].lights,
            dnw = neighbors[Dirs.DOWN].neighbors[Dirs.NORTH].neighbors[Dirs.WEST].lights,
            dne = neighbors[Dirs.DOWN].neighbors[Dirs.NORTH].neighbors[Dirs.EAST].lights,
        };
    }
}
