using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;

public class MeshData {
    public List<Vector3> vertices = new List<Vector3>();
    public List<int> triangles = new List<int>();
    public List<Vector2> uv = new List<Vector2>();
    public List<Vector2> uv2 = new List<Vector2>();

    public MeshData() { }


    public void AddQuadTriangles() {
        triangles.Add(vertices.Count - 4);  // 0
        triangles.Add(vertices.Count - 3);  // 1
        triangles.Add(vertices.Count - 2);  // 2

        triangles.Add(vertices.Count - 4);  // 0
        triangles.Add(vertices.Count - 2);  // 2
        triangles.Add(vertices.Count - 1);  // 3

    }

    public void AddQuadTrianglesGreedy(bool clockwise) {
        if (!clockwise) {

            triangles.Add(vertices.Count - 2);  // 2
            triangles.Add(vertices.Count - 4);  // 0
            triangles.Add(vertices.Count - 3);  // 1

            triangles.Add(vertices.Count - 3);  // 1
            triangles.Add(vertices.Count - 1);  // 3
            triangles.Add(vertices.Count - 2);  // 2
        } else {
            triangles.Add(vertices.Count - 2);  // 2
            triangles.Add(vertices.Count - 1);  // 3
            triangles.Add(vertices.Count - 3);  // 1

            triangles.Add(vertices.Count - 3);  // 1
            triangles.Add(vertices.Count - 4);  // 0
            triangles.Add(vertices.Count - 2);  // 2

        }
    }

    public void AddTriangle(int tri) {
        triangles.Add(tri);
    }


    public void AddVertex(Vector3 vertex) {
        vertices.Add(vertex);
    }

}

public class NativeMeshData {
    public NativeArray<Block> blocks;
    public NativeArray<Block> west;
    public NativeArray<Block> down;
    public NativeArray<Block> south;
    public NativeArray<Block> east;
    public NativeArray<Block> up;
    public NativeArray<Block> north;

    public NativeList<Vector3> vertices;
    public NativeList<int> triangles;
    public NativeList<Vector2> uvs;
    readonly int size;

    public NativeMeshData(int size, NativeArray<Block> blocks, NativeList<Vector3> vertices, NativeList<int> triangles, NativeList<Vector2> uvs) {
        this.size = size;
        this.blocks = blocks;
        this.vertices = vertices;
        this.triangles = triangles;
        this.uvs = uvs;
    }

    public Block GetBlock(int x, int y, int z) {
        // only one of these can ever be true at once... otherwise need more neighbors
        if (x < 0) {
            return west[(size - 1) + z * size + y * size * size];
        } else if (y < 0) {
            return down[x + z * size + (size - 1) * size * size];
        } else if (z < 0) {
            return south[x + (size - 1) * size + y * size * size];
        } else if (x >= size) {
            return east[0 + z * size + y * size * size];
        } else if (y >= size) {
            return up[x + z * size + 0];
        } else if (z >= size) {
            return north[x + 0 + y * size * size];
        } else {
            return blocks[x + z * size + y * size * size];
        }

    }

    public void AddVertex(Vector3 vertex) {
        vertices.Add(vertex);
    }

    public void AddTriangle(int tri) {
        triangles.Add(tri);
    }

    public void AddQuadTriangles() {
        triangles.Add(vertices.Length - 4);  // 0
        triangles.Add(vertices.Length - 3);  // 1
        triangles.Add(vertices.Length - 2);  // 2

        triangles.Add(vertices.Length - 4);  // 0
        triangles.Add(vertices.Length - 2);  // 2
        triangles.Add(vertices.Length - 1);  // 3

    }

    public void AddFaceUVs(Tile tp) {
        uvs.Add(new Vector2(tp.x + 1, tp.y) * Tile.SIZE);
        uvs.Add(new Vector2(tp.x + 1, tp.y + 1) * Tile.SIZE);
        uvs.Add(new Vector2(tp.x, tp.y + 1) * Tile.SIZE);
        uvs.Add(new Vector2(tp.x, tp.y) * Tile.SIZE);
    }

}


public static class MeshUtils {
    // adds small overlap between blocks to prevent t junctions causing small pixel gaps between meshes
    // (annoying because you can see through mesh slightly because of this)
    public const float pad = 0.0001f;
    public const float fpad = 0.0f; // padding in direction of face, dont think this is better actually

    public static readonly Vector3[][] padOffset = {
        new[] { // west, 
            new Vector3(-fpad, -pad, -pad), // bl
            new Vector3(-fpad, -pad, +pad), // br
            new Vector3(-fpad, +pad, -pad), // tl
            new Vector3(-fpad, +pad, +pad), // tr
        },
        new[] { // down
            new Vector3(-pad, -fpad, -pad),
            new Vector3(+pad, -fpad, -pad),
            new Vector3(-pad, -fpad, +pad),
            new Vector3(+pad, -fpad, +pad),
        },
        new[] { // south
            new Vector3(-pad, -pad, -fpad),
            new Vector3(+pad, -pad, -fpad),
            new Vector3(-pad, +pad, -fpad),
            new Vector3(+pad, +pad, -fpad),
        },
        new[] { // east
            new Vector3(+fpad, -pad, -pad),
            new Vector3(+fpad, -pad, +pad),
            new Vector3(+fpad, +pad, -pad),
            new Vector3(+fpad, +pad, +pad),
        },
        new[] { // up
            new Vector3(-pad, +fpad, -pad),
            new Vector3(+pad, +fpad, -pad),
            new Vector3(-pad, +fpad, +pad),
            new Vector3(+pad, +fpad, +pad),
        },
        new[] { // north
            new Vector3(-pad, -pad, +fpad),
            new Vector3(+pad, -pad, +fpad),
            new Vector3(-pad, +pad, +fpad),
            new Vector3(+pad, +pad, +fpad),
        }
    };

}
