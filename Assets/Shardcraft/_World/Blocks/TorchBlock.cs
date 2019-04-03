using UnityEngine;
using Unity.Collections;

public class BlockTorch : BlockType {

    public readonly ushort torchCol;

    public BlockTorch(int r, int g, int b) {
        torchCol = LightCalculator.GetColor(r, g, b);
    }

    public override bool IsSolid(Dir dir) {
        return false;
    }

    public override int GetTextureIndex(Dir dir, int x, int y, int z, ref NativeArray3x3<Block> blocks) {
        return 7;
    }

    public override ushort GetLight() {
        return torchCol;
    }

    public override void AddDataNative(int x, int y, int z, NativeMeshData data, ref NativeArray3x3<Block> blocks, ref NativeArray3x3<Light> lights, NativeList<Face> faces) {

        FaceDataWestNative(x, y, z, data, ref blocks, ref lights);
        faces.Add(new Face { pos = (ushort)(x + z * Chunk.SIZE + y * Chunk.SIZE * Chunk.SIZE), dir = Dir.none });

        if (!blocks.Get(x, y - 1, z).IsSolid(Dir.up)) {
            FaceDataDownNative(x, y, z, data, ref blocks, ref lights);
            faces.Add(new Face { pos = (ushort)(x + z * Chunk.SIZE + y * Chunk.SIZE * Chunk.SIZE), dir = Dir.none });
        }

        FaceDataSouthNative(x, y, z, data, ref blocks, ref lights);
        faces.Add(new Face { pos = (ushort)(x + z * Chunk.SIZE + y * Chunk.SIZE * Chunk.SIZE), dir = Dir.none });

        FaceDataEastNative(x, y, z, data, ref blocks, ref lights);
        faces.Add(new Face { pos = (ushort)(x + z * Chunk.SIZE + y * Chunk.SIZE * Chunk.SIZE), dir = Dir.none });

        if (!blocks.Get(x, y + 1, z).IsSolid(Dir.down)) {
            FaceDataUpNative(x, y, z, data, ref blocks, ref lights);
            faces.Add(new Face { pos = (ushort)(x + z * Chunk.SIZE + y * Chunk.SIZE * Chunk.SIZE), dir = Dir.none });
        }

        FaceDataNorthNative(x, y, z, data, ref blocks, ref lights);
        faces.Add(new Face { pos = (ushort)(x + z * Chunk.SIZE + y * Chunk.SIZE * Chunk.SIZE), dir = Dir.none });

    }

    const float sub = 0.35f;
    protected override void FaceDataWestNative(int x, int y, int z, NativeMeshData data, ref NativeArray3x3<Block> blocks, ref NativeArray3x3<Light> lights) {
        Color c = LightCalculator.GetColorFromLight(lights.Get(x, y, z));

        data.AddVertex(new Vector3(x + sub, y, z + 1.0f - sub) / Chunk.BPU, c);
        data.AddVertex(new Vector3(x + sub, y + 1.0f, z + 1.0f - sub) / Chunk.BPU, c);
        data.AddVertex(new Vector3(x + sub, y + 1.0f, z + sub) / Chunk.BPU, c);
        data.AddVertex(new Vector3(x + sub, y, z + sub) / Chunk.BPU, c);
        data.AddQuadTriangles();
        data.AddFaceUVs(GetTextureIndex(Dir.west, x, y, z, ref blocks));
    }

    protected override void FaceDataDownNative(int x, int y, int z, NativeMeshData data, ref NativeArray3x3<Block> blocks, ref NativeArray3x3<Light> lights) {
        Color c = LightCalculator.GetColorFromLight(lights.Get(x, y, z));

        data.AddVertex(new Vector3(x + sub, y, z + sub) / Chunk.BPU, c);
        data.AddVertex(new Vector3(x + 1.0f - sub, y, z + sub) / Chunk.BPU, c);
        data.AddVertex(new Vector3(x + 1.0f - sub, y, z + 1.0f - sub) / Chunk.BPU, c);
        data.AddVertex(new Vector3(x + sub, y, z + 1.0f - sub) / Chunk.BPU, c);
        data.AddQuadTriangles();
        data.AddFaceUVs(GetTextureIndex(Dir.down, x, y, z, ref blocks));
    }

    protected override void FaceDataSouthNative(int x, int y, int z, NativeMeshData data, ref NativeArray3x3<Block> blocks, ref NativeArray3x3<Light> lights) {
        Color c = LightCalculator.GetColorFromLight(lights.Get(x, y, z));

        data.AddVertex(new Vector3(x + sub, y, z + sub) / Chunk.BPU, c);
        data.AddVertex(new Vector3(x + sub, y + 1.0f, z + sub) / Chunk.BPU, c);
        data.AddVertex(new Vector3(x + 1.0f - sub, y + 1.0f, z + sub) / Chunk.BPU, c);
        data.AddVertex(new Vector3(x + 1.0f - sub, y, z + sub) / Chunk.BPU, c);
        data.AddQuadTriangles();
        data.AddFaceUVs(GetTextureIndex(Dir.south, x, y, z, ref blocks));
    }

    protected override void FaceDataEastNative(int x, int y, int z, NativeMeshData data, ref NativeArray3x3<Block> blocks, ref NativeArray3x3<Light> lights) {
        Color c = LightCalculator.GetColorFromLight(lights.Get(x, y, z));

        data.AddVertex(new Vector3(x + 1.0f - sub, y, z + sub) / Chunk.BPU, c);
        data.AddVertex(new Vector3(x + 1.0f - sub, y + 1.0f, z + sub) / Chunk.BPU, c);
        data.AddVertex(new Vector3(x + 1.0f - sub, y + 1.0f, z + 1.0f - sub) / Chunk.BPU, c);
        data.AddVertex(new Vector3(x + 1.0f - sub, y, z + 1.0f - sub) / Chunk.BPU, c);
        data.AddQuadTriangles();
        data.AddFaceUVs(GetTextureIndex(Dir.east, x, y, z, ref blocks));
    }

    protected override void FaceDataUpNative(int x, int y, int z, NativeMeshData data, ref NativeArray3x3<Block> blocks, ref NativeArray3x3<Light> lights) {
        Color c = LightCalculator.GetColorFromLight(lights.Get(x, y, z));

        data.AddVertex(new Vector3(x + sub, y + 1.0f, z + 1.0f - sub) / Chunk.BPU, c);
        data.AddVertex(new Vector3(x + 1.0f - sub, y + 1.0f, z + 1.0f - sub) / Chunk.BPU, c);
        data.AddVertex(new Vector3(x + 1.0f - sub, y + 1.0f, z + sub) / Chunk.BPU, c);
        data.AddVertex(new Vector3(x + sub, y + 1.0f, z + sub) / Chunk.BPU, c);
        data.AddQuadTriangles();
        data.AddFaceUVs(GetTextureIndex(Dir.up, x, y, z, ref blocks));
    }

    protected override void FaceDataNorthNative(int x, int y, int z, NativeMeshData data, ref NativeArray3x3<Block> blocks, ref NativeArray3x3<Light> lights) {
        Color c = LightCalculator.GetColorFromLight(lights.Get(x, y, z));

        data.AddVertex(new Vector3(x + 1.0f - sub, y, z + 1.0f - sub) / Chunk.BPU, c);
        data.AddVertex(new Vector3(x + 1.0f - sub, y + 1.0f, z + 1.0f - sub) / Chunk.BPU, c);
        data.AddVertex(new Vector3(x + sub, y + 1.0f, z + 1.0f - sub) / Chunk.BPU, c);
        data.AddVertex(new Vector3(x + sub, y, z + 1.0f - sub) / Chunk.BPU, c);
        data.AddQuadTriangles();
        data.AddFaceUVs(GetTextureIndex(Dir.north, x, y, z, ref blocks));
    }

}