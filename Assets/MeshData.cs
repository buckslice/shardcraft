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
