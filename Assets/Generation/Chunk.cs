using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;


public class Chunk {
    public const int SIZE = 16;
    // equal for now but keeping if u want to change this later. would have to change away from array3 tho actually
    // also very untested lol so prob will megachoke
    public const int CHUNK_WIDTH = SIZE;
    public const int CHUNK_HEIGHT = SIZE;

    public Block[] blocks;

    //public HashSet<ushort> modifiedBlockIndices = new HashSet<ushort>(); // hashset to avoid duplicates

    public World world;
    public GameObject gameObject;
    public readonly Vector3i wp; // world space position
    public readonly Vector3i cp;

    public bool loaded { get; set; }    // means ur blocks are loaded
    public bool update { get; set; }    // means need to update mesh
    public bool rendered { get; set; }  // has a mesh
    public bool waitingForMesh { get; set; }
    public bool builtStructures { get; set; }
    public bool needToUpdateSave { get; set; }

    public MeshRenderer mr { get; set; }
    MeshFilter filter;
    MeshCollider coll;

    // w d s e u n
    public Chunk[] neighbors = new Chunk[6];

    public static bool beGreedy = false;

    public static bool generateColliders = true;

    public Chunk(World world, Vector3i wp, Vector3i cp, GameObject gameObject) {
        this.world = world;
        this.wp = wp;
        this.cp = cp;
        this.gameObject = gameObject;

        loaded = false;
        update = false;
        rendered = false;
        waitingForMesh = false;
        builtStructures = false;
        needToUpdateSave = false;

        mr = gameObject.GetComponent<MeshRenderer>();
        filter = gameObject.GetComponent<MeshFilter>();
        coll = gameObject.GetComponent<MeshCollider>();

        mr.material = Chunk.beGreedy ? world.TileMatGreedy : world.TileMat;
    }

    // Updates the chunk based on its contents
    public bool UpdateChunk() {
        for (int i = 0; i < 6; ++i) {
            if (neighbors[i] == null || !neighbors[i].loaded) {
                return false;
            }
        }

        //if (!builtStructures) {
        //    BuildStructures();
        //    builtStructures = true;
        //}

        if (update && !waitingForMesh) {
            waitingForMesh = true;
            update = false;
            JobController.StartMeshJob(this);
        } else {
            return false;
        }

        //if (beGreedy) {
        //    UpdateMesh(GreedyMesh(), true);
        //} else {
        //    UpdateMesh(NaiveMesh(), false);
        //}
        //rendered = true;
        //update = false;

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

    //public const int VOXEL_SIZE = 1;

    //https://github.com/roboleary/GreedyMesh/blob/master/src/mygame/Main.java
    //https://github.com/darkedge/starlight/blob/master/starlight/starlight_game.cpp

    // make separate version of this algo just for collisions. only cares about solid, not block type
    // also make faces bigger to get rid of cracks
    MeshData GreedyMesh(bool forCollision = false) {
        MeshData data = new MeshData();

        // setup variables for algo
        int i, j, k, l, w, h, d1, d2, n = 0;
        Dir side = Dir.south;

        int[] x = new int[] { 0, 0, 0 };
        int[] q = new int[] { 0, 0, 0 };
        int[] du = new int[] { 0, 0, 0 };
        int[] dv = new int[] { 0, 0, 0 };

        // slice will contain groups of matching blocks as we proceed through chunk in 6 directions, onces for each face
        Block[] slice = new Block[CHUNK_WIDTH * CHUNK_HEIGHT];

        int[] maxDim = new int[] { CHUNK_WIDTH, CHUNK_HEIGHT, CHUNK_WIDTH };

        // sweep over six dimensions
        for (int dim = 0; dim < 6; ++dim) {
            int d0 = dim % 3;
            d1 = (dim + 1) % 3; // u
            d2 = (dim + 2) % 3; // v
            // when going thru z dimension, make x d1 and y d2 so makes more sense for uvs
            if (d0 == 2) {
                d1 = 1;
                d2 = 0;
            }

            int bf = dim / 3 * 2 - 1; // -1 -1 -1 +1 +1 +1
            bool backFace = bf < 0;

            x[0] = 0;
            x[1] = 0;
            x[2] = 0;

            // set the direction vector from dimension
            q[0] = 0;
            q[1] = 0;
            q[2] = 0;
            q[d0] = 1;

            side = (Dir)dim;

            // move through dimension from front to back
            for (x[d0] = 0; x[d0] < maxDim[d0];) {

                // compute mask (which is a slice)
                n = 0;
                for (x[d2] = 0; x[d2] < maxDim[d2]; x[d2]++) {
                    for (x[d1] = 0; x[d1] < maxDim[d1]; x[d1]++) {
                        Block block1 = GetBlock(x[0], x[1], x[2]); // block were at
                        Block block2 = GetBlock(x[0] + q[0], x[1] + q[1], x[2] + q[2]); // block were going to

                        // this isSolid is probably wrong in some cases but no blocks use yet cuz i dont rly get so figure out later lol
                        slice[n++] = block1.IsSolid(side) && block2.IsSolid(Dirs.Opp(side)) ?
                            Blocks.AIR : backFace ? block2 : block1;
                    }
                }

                // i think the current dimension we are slicing thru is incremented here so the blocks
                // will have the correct placement coordinate
                x[d0]++;

                // generate mesh for the mask
                n = 0;
                for (j = 0; j < maxDim[d2]; ++j) {
                    for (i = 0; i < maxDim[d1];) {
                        if (slice[n] == Blocks.AIR) {
                            ++i;
                            ++n;
                            continue;
                        }

                        // compute width
                        for (w = 1; i + w < maxDim[d1] && slice[n + w] == slice[n]; ++w) { }

                        // compute height
                        bool done = false;
                        for (h = 1; j + h < maxDim[d2]; ++h) {
                            for (k = 0; k < w; ++k) {
                                if (slice[n + k + h * maxDim[d1]] != slice[n]) {
                                    done = true;
                                    break;
                                }
                            }
                            if (done) {
                                break;
                            }
                        }

                        x[d1] = i;
                        x[d2] = j;

                        du[0] = 0;
                        du[1] = 0;
                        du[2] = 0;
                        du[d1] = w;

                        dv[0] = 0;
                        dv[1] = 0;
                        dv[2] = 0;
                        dv[d2] = h;

                        int s = (int)side;
                        Vector3 botLeft = new Vector3(x[0], x[1], x[2]) + MeshUtils.padOffset[s][0];
                        Vector3 botRight = new Vector3(x[0] + dv[0], x[1] + dv[1], x[2] + dv[2]) + MeshUtils.padOffset[s][1];
                        Vector3 topLeft = new Vector3(x[0] + du[0], x[1] + du[1], x[2] + du[2]) + MeshUtils.padOffset[s][2];
                        Vector3 topRight = new Vector3(x[0] + du[0] + dv[0], x[1] + du[1] + dv[1], x[2] + du[2] + dv[2]) + MeshUtils.padOffset[s][3];

                        // not using for now
                        //botLeft *= VOXEL_SIZE;
                        //topLeft *= VOXEL_SIZE;
                        //topRight *= VOXEL_SIZE;
                        //botRight *= VOXEL_SIZE;

                        data.AddVertex(botLeft);
                        data.AddVertex(botRight);
                        data.AddVertex(topLeft);
                        data.AddVertex(topRight);

                        data.AddQuadTrianglesGreedy(d0 == 2 ? backFace : !backFace);

                        if (!forCollision) {
                            slice[n].GetBlockType().FaceUVsGreedy(side, data, w, h);
                        }

                        // zero out the quad in the mask
                        for (l = 0; l < h; ++l) {
                            for (k = 0; k < w; ++k) {
                                slice[n + k + l * maxDim[d1]] = Blocks.AIR;
                            }
                        }

                        // increment counters and continue
                        i += w;
                        n += w;

                    }
                }
            }
        }

        return data;
    }

    //public MeshData NaiveMesh() {
    //    MeshData meshData = new MeshData();

    //    for (int z = 0; z < SIZE; z++) {
    //        for (int y = 0; y < SIZE; y++) {
    //            for (int x = 0; x < SIZE; x++) {
    //                blocks[x, y, z].AddData(this, x, y, z, meshData);
    //            }
    //        }
    //    }

    //    return meshData;
    //}

    // Sends the calculated mesh information to the mesh and collision components
    void UpdateMesh(MeshData meshData, bool useMeshDataAsCollider) {
        filter.mesh.Clear();
        filter.mesh.vertices = meshData.vertices.ToArray();
        filter.mesh.uv = meshData.uv.ToArray();
        if (beGreedy) {
            filter.mesh.uv2 = meshData.uv2.ToArray();
        }
        filter.mesh.triangles = meshData.triangles.ToArray();
        filter.mesh.RecalculateNormals();

        if (!generateColliders) {
            return;
        }

        MeshData colData = meshData;
        if (!useMeshDataAsCollider) {
            colData = GreedyMesh(true);
        }

        // generate collider
        coll.sharedMesh = null;
        Mesh mesh = new Mesh();
        mesh.vertices = colData.vertices.ToArray();
        mesh.triangles = colData.triangles.ToArray();
        mesh.RecalculateNormals();
        coll.sharedMesh = mesh;

        //Debug.Log("updated: " + pos.ToString());
    }

    public void UpdateMeshNative(NativeList<Vector3> vertices, NativeList<int> triangles, NativeList<Vector2> uvs) {
        filter.mesh.Clear();
        filter.mesh.vertices = vertices.ToArray();
        filter.mesh.uv = uvs.ToArray();
        filter.mesh.triangles = triangles.ToArray();
        filter.mesh.RecalculateNormals();

        //// generate collider
        //MeshData colData = GreedyMesh(true);
        //coll.sharedMesh = null;
        //Mesh mesh = new Mesh();
        //mesh.vertices = colData.vertices.ToArray();
        //mesh.triangles = colData.triangles.ToArray();
        //mesh.RecalculateNormals();
        //coll.sharedMesh = mesh;
    }

    public void UpdateColliderNative(NativeList<Vector3> vertices, NativeList<int> triangles) {
        coll.sharedMesh = null;
        Mesh mesh = new Mesh();
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

    // sets block modified this way
    public void SetBlock(int x, int y, int z, Block block) {
        if (InRange(x, y, z)) {
            if (!loaded) {
                return;
            }
            blocks[x + z * SIZE + y * SIZE * SIZE] = block;
            //modifiedBlockIndices.Add(CoordToUint(x, y, z));
            needToUpdateSave = true; // block was modified so need to update save
            update = true;
        } else {
            world.SetBlock(wp.x + x, wp.y + y, wp.z + z, block);
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
        Debug.Assert(x >= 0 && x < 16 && y >= 0 && y < 16 && z >= 0 && z < 16);
        return (ushort)(x + y * SIZE + z * SIZE * SIZE);
    }

    public static Vector3i IntToCoord(int i) {
        int x = i % SIZE;
        int y = (i % (SIZE * SIZE)) / SIZE;
        int z = i / (SIZE * SIZE);
        return new Vector3i(x, y, z);
    }

}
