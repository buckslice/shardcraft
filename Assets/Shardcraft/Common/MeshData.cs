﻿using UnityEngine;
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
    public NativeList<Vector3> vertices;
    public NativeList<Vector3> uvs;
    public NativeList<Color32> colors;
    public NativeList<int> triangles;

    public NativeMeshData(NativeList<Vector3> vertices, NativeList<Vector3> uvs, NativeList<Color32> colors, NativeList<int> triangles) {
        this.vertices = vertices;
        this.uvs = uvs;
        this.colors = colors;
        this.triangles = triangles;
    }

    public void AddVertex(Vector3 vertex, Color32 color) {
        vertices.Add(vertex);
        colors.Add(color);
    }

    public void AddQuadTriangles() {
        triangles.Add(vertices.Length - 4);  // 0
        triangles.Add(vertices.Length - 3);  // 1
        triangles.Add(vertices.Length - 2);  // 2

        triangles.Add(vertices.Length - 4);  // 0
        triangles.Add(vertices.Length - 2);  // 2
        triangles.Add(vertices.Length - 1);  // 3
    }

    public void AddFlippedQuadTriangles() {
        int len = vertices.Length;
        triangles.Add(len - 4);  // 0
        triangles.Add(len - 3);  // 1
        triangles.Add(len - 1);  // 3

        triangles.Add(len - 3);  // 1
        triangles.Add(len - 2);  // 2
        triangles.Add(len - 1);  // 3
    }

    //public void AddFaceUVs(Tile tp) {
    //    uvs.Add(new Vector2(tp.x + 1, tp.y) * Tile.SIZE);
    //    uvs.Add(new Vector2(tp.x + 1, tp.y + 1) * Tile.SIZE);
    //    uvs.Add(new Vector2(tp.x, tp.y + 1) * Tile.SIZE);
    //    uvs.Add(new Vector2(tp.x, tp.y) * Tile.SIZE);

    //}

    // given slice into texture2Darray, selecting it by the z uv coordinate
    public void AddFaceUVs(int slice) {
        uvs.Add(new Vector3(1, 0, slice));
        uvs.Add(new Vector3(1, 1, slice));
        uvs.Add(new Vector3(0, 1, slice));
        uvs.Add(new Vector3(0, 0, slice));
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