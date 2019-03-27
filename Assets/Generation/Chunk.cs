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

    //public Block[] blocks;

    public NativeArray<Block> blocks;

    //public HashSet<ushort> modifiedBlockIndices = new HashSet<ushort>(); // hashset to avoid duplicates
    public Queue<BlockEdit> pendingEdits = new Queue<BlockEdit>();

    public World world;
    public GameObject gameObject;
    public Vector3i wp { get; private set; } // world space position
    public Vector3i cp { get; private set; } // chunk space

    public bool loaded { get; set; }    // means ur blocks are loaded
    public bool update { get; set; }    // means need to update mesh
    public bool rendered { get; private set; }  // has a mesh
    bool builtStructures;
    int dataLock = 0;
    public bool needToUpdateSave { get; set; } // only gets set when generated or modified a block

    public MeshRenderer mr { get; set; }
    MeshFilter filter;
    MeshCollider coll;

    // w d s e u n
    public Chunk[] neighbors = new Chunk[6];

    public static bool beGreedy = false;

    public static bool generateColliders = true;


    public Chunk(GameObject gameObject) {
        this.gameObject = gameObject;

        blocks = new NativeArray<Block>(Chunk.SIZE * Chunk.SIZE * Chunk.SIZE, Allocator.Persistent);

        mr = gameObject.GetComponent<MeshRenderer>();
        filter = gameObject.GetComponent<MeshFilter>();
        coll = gameObject.GetComponent<MeshCollider>();

    }

    public void Initialize(World world, Vector3i wp, Vector3i cp) {
        this.world = world; // maybe world can change? i dunno would prob have its own pool
        this.wp = wp;
        this.cp = cp;

        loaded = false;
        update = false;
        rendered = false;
        dataLock = 0;
        builtStructures = false;
        needToUpdateSave = false;

        gameObject.transform.position = wp.ToVector3();
        gameObject.name = "Chunk " + cp;
        gameObject.SetActive(true);

        for (int i = 0; i < 6; ++i) {
            neighbors[i] = null;
        }

        mr.material = Chunk.beGreedy ? world.TileMatGreedy : world.TileMat;
    }

    public void LockData() {
        dataLock += 1;
    }

    public void UnlockData() {
        dataLock -= 1;
        Debug.Assert(dataLock >= 0);
        if (dataLock == 0) {
            ApplyPendingEdits();
        }
    }

    public void ApplyPendingEdits() {
        if (pendingEdits.Count > 0 && dataLock == 0) {
            while (pendingEdits.Count > 0) {
                BlockEdit e = pendingEdits.Dequeue();
                blocks[e.x + e.z * SIZE + e.y * SIZE * SIZE] = e.block;
                CheckNeedToUpdateNeighbors(e.x, e.y, e.z);
            }
            needToUpdateSave = true; // blocks were modified so need to update save
            // just slam out another job asap (could try manually completing too? or give highest prio to chunks near player somehow)
            JobController.StartMeshJob(this);
        }
    }

    public void ClearMeshes() {
        filter.mesh.Clear();
        coll.sharedMesh = null;
    }

    // Updates the chunk based on its contents
    public bool UpdateChunk() {
        if (!loaded) {
            return false;
        }

        for (int i = 0; i < 6; ++i) {
            if (neighbors[i] == null || !neighbors[i].loaded) {
                return false;
            }
        }


        //if (!builtStructures) {
        //    BuildStructures();
        //    builtStructures = true;
        //}

        if (update && dataLock == 0) {
            update = false;
            JobController.StartMeshJob(this);
        } else {
            return false;
        }

        return true;
    }

    // called only once all neighbors are generated
    public void BuildStructures() {

        // not sure here lol
        Random.InitState(wp.GetHashCode() + world.seed);

        for (int z = 0; z < SIZE; ++z) {
            for (int y = 0; y < SIZE; ++y) {
                for (int x = 0; x < SIZE; ++x) {

                    if (GetBlock(x, y, z) == Blocks.AIR && GetBlock(x, y - 1, z) == Blocks.GRASS) {
                        if (Random.value < 0.01f) {
                            SetBlock(x, y, z, Blocks.TORCH);
                        }
                    }

                }
            }
        }

    }

    public void UpdateMeshNative(NativeList<Vector3> vertices, NativeList<int> triangles, NativeList<Vector2> uvs) {
        if (triangles.Length < short.MaxValue) {
            filter.mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt16;
        } else {
            filter.mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        }

        filter.mesh.Clear();
        filter.mesh.vertices = vertices.ToArray();
        filter.mesh.uv = uvs.ToArray();
        filter.mesh.triangles = triangles.ToArray();
        filter.mesh.RecalculateNormals();

        rendered = true;

        ApplyPendingEdits();
    }

    public void UpdateColliderNative(NativeList<Vector3> vertices, NativeList<int> triangles) {
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

    public Block GetBlock(int x, int y, int z) {
        // return block if its in range of this chunk
        if (InRange(x, y, z)) {
            if (!loaded) {
                return Blocks.AIR;
            }
            return blocks[x + z * SIZE + y * SIZE * SIZE];
        }
        return world.GetBlock(wp.x + x, wp.y + y, wp.z + z);
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
        } else {
            world.SetBlock(wp.x + x, wp.y + y, wp.z + z, block);
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

    // returns the chunk coord that pos is in
    public static Vector3i GetChunkPosition(Vector3 worldPos) {
        return new Vector3i(
            Mathf.FloorToInt(worldPos.x / SIZE),
            Mathf.FloorToInt(worldPos.y / SIZE),
            Mathf.FloorToInt(worldPos.z / SIZE)
        );
    }

    // linearize vector3i based on chunk size
    public static ushort CoordToUint(int x, int y, int z) {
        Debug.Assert(x >= 0 && x < SIZE && y >= 0 && y < SIZE && z >= 0 && z < SIZE);
        return (ushort)(x + y * SIZE + z * SIZE * SIZE);
    }

    public static Vector3i IntToCoord(int i) {
        int x = i % SIZE;
        int y = (i % (SIZE * SIZE)) / SIZE;
        int z = i / (SIZE * SIZE);
        return new Vector3i(x, y, z);
    }

}
