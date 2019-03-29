using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DrawBounds : MonoBehaviour {

    public Material lineMat;

    Camera cam;
    List<Bounds> bounds = new List<Bounds>();
    List<Color> colors = new List<Color>();

    List<Vector3> lines = new List<Vector3>();
    List<Color> lineColors = new List<Color>();

    public Matrix4x4 matrix { get; set; }

    void Start() {
        matrix = Matrix4x4.identity;
        cam = GetComponent<Camera>();
    }

    public void Clear() {
        bounds.Clear();
        colors.Clear();
        lines.Clear();
    }

    public void AddBounds(Bounds b, Color c) {
        bounds.Add(b);
        colors.Add(c);
    }

    public void AddLine(Vector3 p1, Vector3 p2, Color col) {
        lines.Add(p1);
        lines.Add(p2);
        lineColors.Add(col);
    }

    Vector3[] v = new Vector3[8];
    void OnPostRender() {
        for (int bc = 0; bc < bounds.Count; ++bc) {
            Bounds b = bounds[bc];
            Color col = colors[bc];

            Vector3 c = b.center;
            Vector3 e = b.extents;

            v[0] = new Vector3(c.x - e.x, c.y - e.y, c.z - e.z);
            v[1] = new Vector3(c.x + e.x, c.y - e.y, c.z - e.z);
            v[2] = new Vector3(c.x - e.x, c.y + e.y, c.z - e.z);
            v[3] = new Vector3(c.x + e.x, c.y + e.y, c.z - e.z);
            v[4] = new Vector3(c.x - e.x, c.y - e.y, c.z + e.z);
            v[5] = new Vector3(c.x + e.x, c.y - e.y, c.z + e.z);
            v[6] = new Vector3(c.x - e.x, c.y + e.y, c.z + e.z);
            v[7] = new Vector3(c.x + e.x, c.y + e.y, c.z + e.z);

            GL.PushMatrix();
            GL.MultMatrix(matrix);

            lineMat.SetPass(0);

            GL.Begin(GL.LINES);
            GL.Color(col);

            for (int i = 0; i < 4; ++i) {
                // forward lines
                GL.Vertex(v[i]);
                GL.Vertex(v[i + 4]);

                // right lines
                GL.Vertex(v[i * 2]);
                GL.Vertex(v[i * 2 + 1]);

                // up lines
                int u = i < 2 ? 0 : 2;
                GL.Vertex(v[i + u]);
                GL.Vertex(v[i + u + 2]);
            }

            GL.End();

            GL.PopMatrix();
        }


        // draw all line pairs
        GL.PushMatrix();
        GL.MultMatrix(matrix);

        lineMat.SetPass(0);

        GL.Begin(GL.LINES);

        for (int l = 0; l < lines.Count / 2; l++) {
            GL.Color(lineColors[l]);
            GL.Vertex(lines[l * 2]);
            GL.Vertex(lines[l * 2 + 1]);
        }

        GL.End();

        GL.PopMatrix();
    }
}
