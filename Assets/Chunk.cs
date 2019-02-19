using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshCollider))]

public class Chunk : MonoBehaviour {
    public Block[,,] blocks = new Block[SIZE, SIZE, SIZE];

    public World world;
    public Vector3i pos;

    public const int SIZE = 16;

    public bool update = true;

    MeshFilter filter;
    MeshCollider coll;

    // Use this for initialization
    void Start() {

        filter = gameObject.GetComponent<MeshFilter>();
        coll = gameObject.GetComponent<MeshCollider>();

    }

    //Update is called once per frame
    void Update() {
        if (update) {
            update = false;
            UpdateChunk();
        }
    }

    // Updates the chunk based on its contents
    void UpdateChunk() {
        MeshData meshData = new MeshData();

        for (int x = 0; x < SIZE; x++) {
            for (int y = 0; y < SIZE; y++) {
                for (int z = 0; z < SIZE; z++) {
                    blocks[x, y, z].AddData(this, x, y, z, meshData);
                }
            }
        }

        UpdateMesh(meshData);
    }

    // Sends the calculated mesh information
    // to the mesh and collision components
    void UpdateMesh(MeshData data) {
        filter.mesh.Clear();
        filter.mesh.vertices = data.vertices.ToArray();
        filter.mesh.triangles = data.triangles.ToArray();

        filter.mesh.uv = data.uv.ToArray();
        filter.mesh.RecalculateNormals();

        //additions:
        coll.sharedMesh = null;
        Mesh mesh = new Mesh();
        mesh.vertices = data.colVertices.ToArray();
        mesh.triangles = data.colTriangles.ToArray();
        mesh.RecalculateNormals();

        coll.sharedMesh = mesh;
    }

    public Block GetBlock(int x, int y, int z) {
        // return block if its in range of this chunk
        if (InRange(x, y, z)) {
            return blocks[x, y, z];
        }
        return world.GetBlock(pos.x + x, pos.y + y, pos.z + z);
    }

    public void SetBlock(int x, int y, int z, Block block, bool worldSpace=false) {
        if (worldSpace) {
            x -= pos.x;
            y -= pos.y;
            z -= pos.z;
        }

        if (InRange(x, y, z)) {
            blocks[x, y, z] = block;
        } else {
            world.SetBlock(pos.x + x, pos.y + y, pos.z + z, block);
        }
    }

    public static bool InRange(int x, int y, int z) {
        return x >= 0 && x < SIZE && y >= 0 && y < SIZE && z >= 0 && z < SIZE;
    }

    public void SetBlocksUnmodified() {
        foreach (Block block in blocks) {
            block.changed = false;
        }
    }

}
