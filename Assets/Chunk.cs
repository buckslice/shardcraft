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


        //UpdateMesh(GreedyMesh());

        UpdateMesh(StupidMesh());

        rendered = true;
    }


    //https://github.com/roboleary/GreedyMesh/blob/master/src/mygame/Main.java
    MeshData GreedyMesh() {
        MeshData meshData = new MeshData();

        // setup variables for algo
        int i, j, k, l, w, h, u, v, n, side = 0;

        int[] x = new int[] { 0, 0, 0 };
        int[] q = new int[] { 0, 0, 0 };
        int[] du = new int[] { 0, 0, 0 };
        int[] dv = new int[] { 0, 0, 0 };

        // mask will contain groups of matching blocks as we proceed through chunk in 6 directions, onces for each face
        Block[] mask = new Block[SIZE * SIZE];



        return meshData;
    }

    MeshData StupidMesh() {
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
