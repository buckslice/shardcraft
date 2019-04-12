using UnityEngine;
using System.Collections;
using System;
using Unity.Collections;
using System.Runtime.CompilerServices;
using Unity.Mathematics;

[Serializable]
public struct Block : IEquatable<Block> {

    public byte type;

    public Block(byte type) {
        this.type = type;
    }

    public static bool operator ==(Block a, Block b) {
        return a.type == b.type;
    }
    public static bool operator !=(Block a, Block b) {
        return !(a == b);
    }

    public bool Equals(Block other) {
        return this == other;
    }

    public override bool Equals(object other) {
        if (!(other is Block)) {
            return false;
        }
        return this == (Block)other;
    }

    public override int GetHashCode() {
        return type;
    }

    // only use these in managed circumstances NOT JOBS OMG
    public bool ColliderSolid() {
        return BlockDatas.data[type].colliderSolid == 1;
    }
    public bool RenderSolid(Block b, Dir dir) { // not using dir for now but keeping it there for later possibly
        return BlockDatas.data[type].renderSolid == 1;
    }
    public BlockData BlockData() {
        return BlockDatas.data[type];
    }

}

public struct BlockData {
    public int texture; // index into tex array, if -1 then dynamically figured out
    public ushort light;
    public byte renderType; // 0 no mesh, 1 regular texture, 2 tiled texture
    public byte colliderSolid;
    public byte renderSolid;
    // this is really like additional light reduction because no matter what light will reduce 1 each block it moves
    // by default this is at really high (so basically solid)
    public byte lightReduction;

    // something like this later to add subshapes
    // might as well at this point add actual field in Block for storing rotation and stuff
    //public Vector3 scale;
    //public Vector3 offset; // offset from min pos, so with scale and offset can define any aabb if u think about it

    public BlockData(int i) {
        // later have string name here once jobs can support that
        texture = 0;
        light = 0;
        renderType = 1;
        colliderSolid = 1;
        renderSolid = 1;
        lightReduction = byte.MaxValue;
        //scale = Vector3.one; // block scale in each dimension
        //offset = Vector3.zero; // face offset in each dimension
    }

    public static bool RenderSolid(NativeArray<BlockData> blockData, Block b, Dir dir) {
        return blockData[b.type].renderSolid == 1;
    }

    public static bool ColliderSolid(NativeArray<BlockData> blockData, Block b) {
        return blockData[b.type].colliderSolid == 1;
    }
}

public static class BlockDatas {

    public static BlockData[] data;

    // inits BlockData[] array for managed and also
    // returns a NativeArray version for jobs
    public static NativeArray<BlockData> InitBlockData() {

        data = new BlockData[] {

        new BlockData(0) { // AIR
            renderType = 0,
            renderSolid = 0,
            colliderSolid = 0,
            lightReduction = 0,
        },

        new BlockData(0) { // STONE
            texture = -1,
            renderType = 2,
        },

        new BlockData(0) { // GRASS
            texture = -1, // figured out dynamically
            renderType = 2,
        },

        new BlockData(0) { // PINE
            texture = -1,
            renderType = 2,
        },

        new BlockData(0) { // PINELEAF
            renderSolid = 0,
            renderType = 2,
            texture = 6,
            lightReduction = 4,
        },

        new BlockData(0) { // TORCH
            light = LightCalculator.GetColor(31, 31, 31),
            texture = 7,
        },

        new BlockData(0) { // TORCH R
            light = LightCalculator.GetColor(31, 0, 0),
            texture = 7,
        },
        new BlockData(0) { // TORCH B
            light = LightCalculator.GetColor(0, 31, 0),
            texture = 7,
        },
        new BlockData(0) { // TORCH G
            light = LightCalculator.GetColor(0, 0, 31),
            texture = 7,
        },
        new BlockData(0) { // TORCH M
            light = LightCalculator.GetColor(31, 0, 31),
            texture = 7,
        },
        new BlockData(0) { // TORCH Y
            light = LightCalculator.GetColor(31, 31, 0),
            texture = 7,
        },
        new BlockData(0) { // TORCH O
            light = LightCalculator.GetColor(31, 15, 0),
            texture = 7,
        },
        new BlockData(0) { // TORCH W
            light = LightCalculator.GetColor(10, 10, 10),
            texture = 7,
        },

        new BlockData(0) { // COAL
            texture = 7,
            renderType = 2,
        },
        new BlockData(0) { // RUBY
            texture = 8,
            renderType = 2,
            light = LightCalculator.GetColor(20, 0, 0),
        },
        new BlockData(0) { // EMERALD
            texture = 9,
            renderType = 2,
            light = LightCalculator.GetColor(0, 20, 0),
        },
        new BlockData(0) { // SAPPHIRE
            texture = 10,
            renderType = 2,
            light = LightCalculator.GetColor(0, 0, 20),
        }

        };

        return new NativeArray<BlockData>(data, Allocator.Persistent);

    }

}


// make sure this matches types array below
public static class Blocks {
    public static readonly Block AIR = new Block(0);
    public static readonly Block STONE = new Block(1);
    public static readonly Block GRASS = new Block(2);
    public static readonly Block PINE = new Block(3);
    public static readonly Block PINELEAF = new Block(4);
    public static readonly Block TORCH = new Block(5);
    public static readonly Block TORCH_R = new Block(6);
    public static readonly Block TORCH_G = new Block(7);
    public static readonly Block TORCH_B = new Block(8);
    public static readonly Block TORCH_M = new Block(9);
    public static readonly Block TORCH_Y = new Block(10);
    public static readonly Block TORCH_O = new Block(11);
    public static readonly Block TORCH_W = new Block(12);
    public static readonly Block COAL = new Block(13);
    public static readonly Block RUBY = new Block(14);
    public static readonly Block EMERALD = new Block(15);
    public static readonly Block SAPPHIRE = new Block(16);

    public const int count = 17;
}

//    // todo: translate to native array job option
//    //public virtual void FaceUVsGreedy(Dir dir, MeshData data, int w, int h) {
//    //    Tile tp = TexturePosition(dir, data);

//    //    // store the offset
//    //    data.uv.Add(new Vector2(tp.x, tp.y) * Tile.SIZE);
//    //    data.uv.Add(new Vector2(tp.x, tp.y) * Tile.SIZE);
//    //    data.uv.Add(new Vector2(tp.x, tp.y) * Tile.SIZE);
//    //    data.uv.Add(new Vector2(tp.x, tp.y) * Tile.SIZE);

//    //    // then add width and height in uv2, shader will calculate coordinate from this
//    //    data.uv2.Add(new Vector2(0, 0));
//    //    data.uv2.Add(new Vector2(h, 0));
//    //    data.uv2.Add(new Vector2(0, w));
//    //    data.uv2.Add(new Vector2(h, w));
//    //}

//}
