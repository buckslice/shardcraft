using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshCollider))]

public class Chunk : MonoBehaviour {
    public const int SIZE = 16;

    public Array3<Block> blocks = new Array3<Block>(SIZE);

    public World world;
    public Vector3i pos; // maybe switch to not be world space and in chunk space


    public bool update { get; set; }
    public bool rendered { get; set; }

    MeshFilter filter;
    MeshCollider coll;

    // Use this for initialization
    void Start() {
        update = false;
        rendered = false;

        filter = gameObject.GetComponent<MeshFilter>();
        coll = gameObject.GetComponent<MeshCollider>();

    }

    //Update is called once per frame
    void Update() {
        if (update) {
            update = false;
            UpdateChunk();
            //Debug.Log("updated " + pos.ToString());
        }
    }

    // Updates the chunk based on its contents
    void UpdateChunk() {

        // double check to make sure all nearby chunks are loaded by this point
        for (int x = -1; x <= 1; ++x) {
            for (int y = -1; y <= 1; ++y) {
                for (int z = -1; z <= 1; ++z) {
                    Debug.Assert(world.GetChunk(pos + new Vector3i(x, y, z) * SIZE) != null);
                }
            }
        }


        UpdateMesh(GreedyMesh());

        //UpdateMesh(NaiveMesh());

        rendered = true;
    }


    //private const int SOUTH = 0;
    //private const int NORTH = 1;
    //private const int EAST = 2;
    //private const int WEST = 3;
    //private const int TOP = 4;
    //private const int BOTTOM = 5;

    // equal for now but keeping if u want to change this later. would have to change away from array3 tho actually
    private const int CHUNK_WIDTH = SIZE;
    private const int CHUNK_HEIGHT = SIZE;

    private const int VOXEL_SIZE = 1;

    // this is used only once and currenly doesnt matter since all blocks are either true or false
    // will come into play later tho...
    Dir opDir(Dir dir) {
        if (dir == Dir.south) {
            return Dir.north;
        }
        if (dir == Dir.north) {
            return Dir.south;
        }
        if (dir == Dir.west) {
            return Dir.east;
        }
        if (dir == Dir.east) {
            return Dir.west;
        }
        if (dir == Dir.up) {
            return Dir.down;
        }
        if (dir == Dir.down) {
            return Dir.up;
        }
        return Dir.west;
    }

    //https://github.com/roboleary/GreedyMesh/blob/master/src/mygame/Main.java
    //https://github.com/darkedge/starlight/blob/master/starlight/starlight_game.cpp
    MeshData GreedyMesh() {
        MeshData data = new MeshData();

        // setup variables for algo
        int i, j, k, l, w, h, u, v, n = 0;
        Dir side = Dir.south;

        int[] x = new int[] { 0, 0, 0 };
        int[] q = new int[] { 0, 0, 0 };
        int[] du = new int[] { 0, 0, 0 };
        int[] dv = new int[] { 0, 0, 0 };

        // mask will contain groups of matching blocks as we proceed through chunk in 6 directions, onces for each face
        Block[] mask = new Block[CHUNK_WIDTH * CHUNK_HEIGHT];

        // this loop runs twice basically
        for (bool backFace = true, b = false; b != backFace; backFace = backFace && b, b = !b) {

            // sweep over three dimensions
            for (int d = 0; d < 3; ++d) {
                u = (d + 1) % 3;
                v = (d + 2) % 3;

                x[0] = 0;
                x[1] = 0;
                x[2] = 0;

                // set the direction vector from dimension
                q[0] = 0;
                q[1] = 0;
                q[2] = 0;
                q[d] = 1;

                // here we keep track of the side were meshing
                if (d == 0) {
                    //side = backFace ? WEST : EAST;
                    side = backFace ? Dir.west : Dir.east;
                } else if (d == 1) {
                    //side = backFace ? BOTTOM : TOP;
                    side = backFace ? Dir.down : Dir.up;
                } else if (d == 2) {
                    //side = backFace ? SOUTH : NORTH;
                    side = backFace ? Dir.south : Dir.north;
                }

                // move through dimension from front to back
                for (x[d] = 0; x[d] < CHUNK_WIDTH;) {

                    // compute mask
                    n = 0;
                    for (x[v] = 0; x[v] < CHUNK_HEIGHT; x[v]++) {
                        for (x[u] = 0; x[u] < CHUNK_WIDTH; x[u]++) {
                            // get two faces to compare
                            //Block block1 = (x[d] >= 0) ? GetBlockGreedy(x[0], x[1], x[2]) : Blocks.AIR;
                            //Block block2 = (x[d] < CHUNK_WIDTH - 1) ? GetBlockGreedy(x[0] + q[0], x[1] + q[1], x[2] + q[2]) : Blocks.AIR;

                            //mask[n++] = block1 == block2 ? Blocks.AIR : backFace ? block2 : block1;

                            Block block1 = GetBlock(x[0], x[1], x[2]); // block were at
                            Block block2 = GetBlock(x[0] + q[0], x[1] + q[1], x[2] + q[2]); // block were going to

                            mask[n++] = block1.IsSolid(opDir(side)) && block2.IsSolid(side) ? Blocks.AIR : backFace ? block2 : block1;

                            //mask[n++] = (block1 != Blocks.AIR && block2 != Blocks.AIR && block1 == block2) ?
                            //            Blocks.AIR : backFace ? block2 : block1;
                        }
                    }

                    x[d]++;

                    // generate mesh for the mask

                    n = 0;
                    for (j = 0; j < CHUNK_HEIGHT; ++j) {
                        for (i = 0; i < CHUNK_WIDTH;) {
                            if (mask[n] != Blocks.AIR) {

                                // compute width
                                for (w = 1; i + w < CHUNK_WIDTH && mask[n + w] == mask[n]; ++w) { }

                                // compute height
                                bool done = false;
                                for (h = 1; j + h < CHUNK_HEIGHT; ++h) {
                                    for (k = 0; k < w; ++k) {
                                        if (mask[n + k + h * CHUNK_WIDTH] != mask[n]) {
                                            done = true;
                                            break;
                                        }
                                    }
                                    if (done) {
                                        break;
                                    }
                                }

                                // check transparent attrib to make sure we dont mesh culled faces
                                // skipping that part for now, i dont rly get it
                                bool transparent = false;
                                if (!transparent) {
                                    // add a quad

                                    x[u] = i;
                                    x[v] = j;

                                    du[0] = 0;
                                    du[1] = 0;
                                    du[2] = 0;
                                    du[u] = w;

                                    dv[0] = 0;
                                    dv[1] = 0;
                                    dv[2] = 0;
                                    dv[v] = h;

                                    Vector3 botLeft = new Vector3(x[0], x[1], x[2]);
                                    Vector3 botRight = new Vector3(x[0] + dv[0], x[1] + dv[1], x[2] + dv[2]);
                                    Vector3 topLeft = new Vector3(x[0] + du[0], x[1] + du[1], x[2] + du[2]);
                                    Vector3 topRight = new Vector3(x[0] + du[0] + dv[0], x[1] + du[1] + dv[1], x[2] + du[2] + dv[2]);

                                    botLeft *= VOXEL_SIZE;
                                    topLeft *= VOXEL_SIZE;
                                    topRight *= VOXEL_SIZE;
                                    botRight *= VOXEL_SIZE;

                                    data.AddVertex(botLeft);
                                    data.AddVertex(botRight);
                                    data.AddVertex(topLeft);
                                    data.AddVertex(topRight);

                                    data.AddQuadTrianglesGreedy(backFace);

                                    data.uv.AddRange(mask[n].GetBlockType().FaceUVsGreedy(side));
                                }

                                // zero out mask
                                for (l = 0; l < h; ++l) {
                                    for (k = 0; k < w; ++k) { mask[n + k + l * CHUNK_WIDTH] = Blocks.AIR; }
                                }

                                // increment counters and continue
                                i += w;
                                n += w;
                            } else {
                                ++i;
                                ++n;
                            }
                        }
                    }

                }

            }
        }


        return data;
    }

    void AddQuad(MeshData data, Vector3 bottomLeft, Vector3 topLeft, Vector3 topRight, Vector3 bottomRight, bool backFace) {

    }

    Block GetBlockGreedy(int x, int y, int z) {
        return blocks[x, y, z];
    }

    MeshData NaiveMesh() {
        MeshData meshData = new MeshData();

        for (int x = 0; x < SIZE; x++) {
            for (int y = 0; y < SIZE; y++) {
                for (int z = 0; z < SIZE; z++) {
                    blocks[x, y, z].AddData(this, x, y, z, meshData);
                }
            }
        }

        return meshData;
    }

    // Sends the calculated mesh information
    // to the mesh and collision components
    void UpdateMesh(MeshData data) {
        filter.mesh.Clear();
        filter.mesh.vertices = data.vertices.ToArray();
        filter.mesh.uv = data.uv.ToArray();
        filter.mesh.triangles = data.triangles.ToArray();
        filter.mesh.RecalculateNormals();

        // generate collider
        coll.sharedMesh = null;
        Mesh mesh = new Mesh();
        mesh.vertices = data.colVertices.ToArray();
        mesh.triangles = data.colTriangles.ToArray();
        mesh.RecalculateNormals();

        coll.sharedMesh = mesh;

        //Debug.Log("updated: " + pos.ToString());
    }

    public Block GetBlock(int x, int y, int z) {
        // return block if its in range of this chunk
        if (InRange(x, y, z)) {
            return blocks[x, y, z];
        }
        return world.GetBlock(pos.x + x, pos.y + y, pos.z + z);
    }

    public void SetBlock(int x, int y, int z, Block block) {
        if (InRange(x, y, z)) {
            blocks[x, y, z] = block;
        } else {
            world.SetBlock(pos.x + x, pos.y + y, pos.z + z, block);
        }
    }

    public static bool InRange(int x, int y, int z) {
        return x >= 0 && x < SIZE && y >= 0 && y < SIZE && z >= 0 && z < SIZE;
    }

    // returns the chunk coord that pos is in
    public static Vector3i GetChunkPosition(Vector3 pos) {
        return new Vector3i(
            Mathf.FloorToInt(pos.x / SIZE) * SIZE,
            Mathf.FloorToInt(pos.y / SIZE) * SIZE,
            Mathf.FloorToInt(pos.z / SIZE) * SIZE
        );
    }

    public void SetBlocksUnmodified() {
        for (int i = 0; i < blocks.sizeCubed; ++i) {
            Block b = blocks[i];
            b.changed = false;
            blocks[i] = b;
        }
    }

}
