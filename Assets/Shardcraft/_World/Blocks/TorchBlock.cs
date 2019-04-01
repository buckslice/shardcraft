using UnityEngine;
using System.Collections;

public class BlockTorch : BlockType {


    public override bool IsSolid(Dir dir) {
        return true;
    }

    public override bool ColliderSolid() {
        return false;
    }

    public override int GetTextureIndex(Dir dir, int x, int y, int z, ref NativeArray3x3<Block> blocks) {
        return 7;
    }

    public override int GetLight() {
        return 16;
    }

    //const float w = 0.25f;
    //public override void AddDataNative(int x, int y, int z, NativeMeshData data) {
    //    FaceDataEastNative(x, y, z, data);
    //    FaceDataUpNative(x, y, z, data);
    //    FaceDataNorthNative(x, y, z, data);
    //    FaceDataWestNative(x, y, z, data);
    //    FaceDataDownNative(x, y, z, data);
    //    FaceDataSouthNative(x, y, z, data);

    //}

    //protected override void FaceDataWestNative(int x, int y, int z, NativeMeshData data) {
    //    data.AddVertex(new Vector3(x + w, y, z + 1.0f - w));
    //    data.AddVertex(new Vector3(x + w, y + 1.0f, z + 1.0f - w));
    //    data.AddVertex(new Vector3(x + w, y + 1.0f, z + w));
    //    data.AddVertex(new Vector3(x + w, y, z + w));

    //    data.AddQuadTriangles();

    //    data.AddFaceUVs(TexturePosition(Dir.west));
    //}

    //protected override void FaceDataDownNative(int x, int y, int z, NativeMeshData data) {
    //    data.AddVertex(new Vector3(x + w, y, z + w));
    //    data.AddVertex(new Vector3(x + 1.0f - w, y, z + w));
    //    data.AddVertex(new Vector3(x + 1.0f - w, y, z + 1.0f - w));
    //    data.AddVertex(new Vector3(x + w, y, z + 1.0f - w));

    //    data.AddQuadTriangles();

    //    data.AddFaceUVs(TexturePosition(Dir.down));
    //}

    //protected override void FaceDataSouthNative(int x, int y, int z, NativeMeshData data) {
    //    data.AddVertex(new Vector3(x + w, y, z + w));
    //    data.AddVertex(new Vector3(x + w, y + 1.0f, z + w));
    //    data.AddVertex(new Vector3(x + 1.0f - w, y + 1.0f, z + w));
    //    data.AddVertex(new Vector3(x + 1.0f - w, y, z + w));

    //    data.AddQuadTriangles();

    //    data.AddFaceUVs(TexturePosition(Dir.south));
    //}

    //protected override void FaceDataEastNative(int x, int y, int z, NativeMeshData data) {
    //    data.AddVertex(new Vector3(x + 1.0f - w, y, z + w));
    //    data.AddVertex(new Vector3(x + 1.0f - w, y + 1.0f, z + w));
    //    data.AddVertex(new Vector3(x + 1.0f - w, y + 1.0f, z + 1.0f - w));
    //    data.AddVertex(new Vector3(x + 1.0f - w, y, z + 1.0f - w));

    //    data.AddQuadTriangles();

    //    data.AddFaceUVs(TexturePosition(Dir.east));
    //}

    //protected override void FaceDataUpNative(int x, int y, int z, NativeMeshData data) {
    //    data.AddVertex(new Vector3(x + w, y + 1.0f, z + 1.0f - w));
    //    data.AddVertex(new Vector3(x + 1.0f - w, y + 1.0f, z + 1.0f - w));
    //    data.AddVertex(new Vector3(x + 1.0f - w, y + 1.0f, z + w));
    //    data.AddVertex(new Vector3(x + w, y + 1.0f, z + w));

    //    data.AddQuadTriangles();

    //    data.AddFaceUVs(TexturePosition(Dir.up));
    //}

    //protected override void FaceDataNorthNative(int x, int y, int z, NativeMeshData data) {
    //    data.AddVertex(new Vector3(x + 1.0f - w, y, z + 1.0f - w));
    //    data.AddVertex(new Vector3(x + 1.0f - w, y + 1.0f, z + 1.0f - w));
    //    data.AddVertex(new Vector3(x + w, y + 1.0f, z + 1.0f - w));
    //    data.AddVertex(new Vector3(x + w, y, z + 1.0f - w));

    //    data.AddQuadTriangles();

    //    data.AddFaceUVs(TexturePosition(Dir.north));
    //}

    //// test smiley texture
    //public override Tile TexturePosition(Dir dir, int x, int y, int z, NativeMeshData data) {
    //    return new Tile(1, 1);
    //}

}