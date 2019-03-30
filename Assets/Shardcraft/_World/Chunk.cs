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

    public NativeArray<byte> light;

    //public HashSet<ushort> modifiedBlockIndices = new HashSet<ushort>(); // hashset to avoid duplicates
    public Queue<BlockEdit> pendingEdits = new Queue<BlockEdit>();

    public Queue<BlockEdit> lightOps = new Queue<BlockEdit>();

    public World world;
    public GameObject gameObject;
    public Vector3i bp { get; private set; } // world block space position (not pure world position!)
    public Vector3i cp { get; private set; } // chunk space

    public bool loaded { get; private set; }    // indicates block array is set correctly (either loaded from save or freshly gend)
    public bool update { get; set; }            // means need to update mesh for some reason
    public bool rendered { get; private set; }  // has a mesh (may be out of date)
    public bool builtStructures { get; set; }
    int dataLock = 0;   // if not zero means one or more jobs are reading from the data
    public bool needToUpdateSave { get; set; } // only gets set when generated or modified a block
    public bool dying { get; set; } // set when chunk is in process of getting destroyed

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
        light = new NativeArray<byte>(SIZE * SIZE * SIZE, Allocator.Persistent);

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
        rendered = false;
        dataLock = 0;
        builtStructures = false;
        needToUpdateSave = false;
        dying = false;
        loadedNeighbors = 0;

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
        if (dataLock == 0) {
            ApplyPendingEdits();
        }
    }

    public bool IsDataLocked() {
        return dataLock > 0;
    }

    public void ApplyPendingEdits() {
        Debug.Assert(loaded);
        if (pendingEdits.Count > 0 && dataLock == 0) {
            while (pendingEdits.Count > 0) {
                BlockEdit e = pendingEdits.Dequeue();
                blocks[e.x + e.z * SIZE + e.y * SIZE * SIZE] = e.block;
                CheckNeedToUpdateNeighbors(e.x, e.y, e.z);
            }
            needToUpdateSave = true; // blocks were modified so need to update save
            if (!NeighborsLoaded() || !IsLocalGroupFree()) {
                update = true;
            } else {    // slam out job right away if you can
                JobController.StartMeshJob(this);
            }
        }
    }

    public void ClearMeshes() {
        filter.mesh.Clear();
        coll.sharedMesh = null;
    }

    // Updates the chunk based on its contents
    public bool UpdateChunk() {
        if (!loaded || !NeighborsLoaded()) { // need to make sure you and your neighbors are loaded first
            return false;
        }

        if (!builtStructures) { // build structures like trees and such if you havent yet
            StructureGenerator.BuildStructures(this);
            builtStructures = true;
        }

        if (update && IsLocalGroupFree()) {
            update = false;
            JobController.StartMeshJob(this);
            return true;
        }

        return false;
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

    public bool NeighborsLoaded() {
        Debug.Assert(loadedNeighbors >= 0 && loadedNeighbors <= 26);
        return loadedNeighbors == 26;
    }

    static bool printedOnce = false;
    public void UpdateMeshNative(NativeList<Vector3> vertices, NativeList<Vector3> uvs, NativeList<Color32> colors, NativeList<int> triangles) {
        if (dying) {
            return;
        }
        if (triangles.Length < short.MaxValue) {
            filter.mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt16;
        } else {
            filter.mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        }

        filter.mesh.Clear();
        filter.mesh.vertices = vertices.ToArray();
        filter.mesh.SetUVs(0, new List<Vector3>(uvs.ToArray()));
        filter.mesh.colors32 = colors.ToArray();

        if (printedOnce == false) {
            for (int i = 0; i < colors.Length; ++i) {
                if (colors[i] != Color.black) {
                    Debug.Log(colors[i]);
                }
            }
        }

        filter.mesh.triangles = triangles.ToArray();
        filter.mesh.RecalculateNormals();

        rendered = true;

        ApplyPendingEdits();
    }

    public void UpdateColliderNative(NativeList<Vector3> vertices, NativeList<int> triangles) {
        if (dying) {
            return;
        }
        coll.sharedMesh = null;
        Mesh mesh = new Mesh(); // maybe reuse this?
        if (triangles.Length < short.MaxValue) {
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
                needToUpdateSave = true; // block was modified so need to update save
                update = true;
                CheckNeedToUpdateNeighbors(x, y, z);
            }
            lightOps.Enqueue(new BlockEdit { x = x, y = y, z = z, block = block });
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
        } else if (x == SIZE - 1 && neighbors[3] != null) {
            neighbors[3].update = true;
        }
        if (y == 0 && neighbors[1] != null) {
            neighbors[1].update = true;
        } else if (y == SIZE - 1 && neighbors[4] != null) {
            neighbors[4].update = true;
        }
        if (z == 0 && neighbors[2] != null) {
            neighbors[2].update = true;
        } else if (z == SIZE - 1 && neighbors[5] != null) {
            neighbors[5].update = true;
        }

    }

    public static bool InRange(int x, int y, int z) {
        return x >= 0 && x < SIZE && y >= 0 && y < SIZE && z >= 0 && z < SIZE;
    }

    // linearize vector3i based on chunk size
    //public static ushort CoordToUint(int x, int y, int z) {
    //    Debug.Assert(x >= 0 && x < SIZE && y >= 0 && y < SIZE && z >= 0 && z < SIZE);
    //    return (ushort)(x + y * SIZE + z * SIZE * SIZE);
    //}

    //public static Vector3i IntToCoord(int i) {
    //    int x = i % SIZE;
    //    int y = (i % (SIZE * SIZE)) / SIZE;
    //    int z = i / (SIZE * SIZE);
    //    return new Vector3i(x, y, z);
    //}


}
