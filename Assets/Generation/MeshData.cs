using UnityEngine;
using System.Collections;
using System.Collections.Generic;

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
