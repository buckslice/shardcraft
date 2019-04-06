using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class Tests {

    static int passes = 0;
    static int failures = 0;

    static void TestEqual(object a, object b, string name) {
        if (!a.Equals(b)) {
            Debug.Log(string.Format("<color=#AA0000><b>Test '{0}' Failed</b></color>, {1} != {2}", name, a, b));
            failures++;
        } else {
            passes++;
        }

    }


    public static void Run() {

        passes = 0;
        failures = 0;

        {
            TestEqual(Dirs.Opp(Dir.west), Dir.east, "DirTest1");
            TestEqual(Dirs.Opp(Dir.down), Dir.up, "DirTest2");
            TestEqual(Dirs.Opp(Dir.south), Dir.north, "DirTest3");
            TestEqual(Dirs.Opp(Dir.east), Dir.west, "DirTest4");
            TestEqual(Dirs.Opp(Dir.up), Dir.down, "DirTest5");
            TestEqual(Dirs.Opp(Dir.north), Dir.south, "DirTest6");
        }

        {
            TestEqual(WorldUtils.GetRegionCoord(new Vector3i(0, 0, 0)), new Vector3i(0, 0, 0), "RegionCoordTest1");
            TestEqual(WorldUtils.GetRegionCoord(new Vector3i(1, 0, 0)), new Vector3i(0, 0, 0), "RegionCoordTest2");
            TestEqual(WorldUtils.GetRegionCoord(new Vector3i(5, 5, 5)), new Vector3i(0, 0, 0), "RegionCoordTest3");
            TestEqual(WorldUtils.GetRegionCoord(new Vector3i(15, 15, 15)), new Vector3i(0, 0, 0), "RegionCoordTest4");
            TestEqual(WorldUtils.GetRegionCoord(new Vector3i(16, 16, 16)), new Vector3i(1, 1, 1), "RegionCoordTest5");
            TestEqual(WorldUtils.GetRegionCoord(new Vector3i(18, 5, 5)), new Vector3i(1, 0, 0), "RegionCoordTest6");
            TestEqual(WorldUtils.GetRegionCoord(new Vector3i(-1, 0, 0)), new Vector3i(-1, 0, 0), "RegionCoordTest7");
            TestEqual(WorldUtils.GetRegionCoord(new Vector3i(0, -1, 0)), new Vector3i(0, -1, 0), "RegionCoordTest8");
            TestEqual(WorldUtils.GetRegionCoord(new Vector3i(0, 0, -1)), new Vector3i(0, 0, -1), "RegionCoordTest9");
            TestEqual(WorldUtils.GetRegionCoord(new Vector3i(0, 0, -1)), new Vector3i(0, 0, -1), "RegionCoordTest10");
            TestEqual(WorldUtils.GetRegionCoord(new Vector3i(14, 15, 15)), new Vector3i(0, 0, 0), "RegionCoordTest11");
            TestEqual(WorldUtils.GetRegionCoord(new Vector3i(17, 15, 15)), new Vector3i(1, 0, 0), "RegionCoordTest12");
        }

        {   // written assuming 2 blocks per unit
            TestEqual(WorldUtils.GetChunkPosFromWorldPos(new Vector3(0, 0, 0)), new Vector3i(0, 0, 0), "ChunkPos1");
            TestEqual(WorldUtils.GetChunkPosFromWorldPos(new Vector3(-1.0f, 0, 0)), new Vector3i(-1, 0, 0), "ChunkPos2");
            TestEqual(WorldUtils.GetChunkPosFromWorldPos(new Vector3(-16.01f, 0, 0)), new Vector3i(-2, 0, 0), "ChunkPos3");
            TestEqual(WorldUtils.GetChunkPosFromWorldPos(new Vector3(25, 35, -5)), new Vector3i(1, 2, -1), "ChunkPos4");
        }

        {
            TestEqual(WorldUtils.GetChunkPosFromBlockPos(0, 13, 0), new Vector3i(0, 0, 0), "GCPFBP1");
            TestEqual(WorldUtils.GetChunkPosFromBlockPos(32, 34, 0), new Vector3i(1, 1, 0), "GCPFBP2");
            TestEqual(WorldUtils.GetChunkPosFromBlockPos(0, -1, 0), new Vector3i(0, -1, 0), "GCPFBP3");
            TestEqual(WorldUtils.GetChunkPosFromBlockPos(-15, 0, 0), new Vector3i(-1, 0, 0), "GCPFBP4");
            TestEqual(WorldUtils.GetChunkPosFromBlockPos(-16, 0, 0), new Vector3i(-1, 0, 0), "GCPFBP5");
            TestEqual(WorldUtils.GetChunkPosFromBlockPos(-31, 0, 0), new Vector3i(-1, 0, 0), "GCPFBP6");
            TestEqual(WorldUtils.GetChunkPosFromBlockPos(-32, 0, 0), new Vector3i(-1, 0, 0), "GCPFBP7");
            TestEqual(WorldUtils.GetChunkPosFromBlockPos(-33, 0, 0), new Vector3i(-2, 0, 0), "GCPFBP8");
            TestEqual(WorldUtils.GetChunkPosFromBlockPos(-33, 0, 40), new Vector3i(-2, 0, 1), "GCPFBP9");
        }

        {
            TestEqual(Serialization.GetTablePos(new Vector3i(0, 0, 0)), 0, "LookUpPos1");
            TestEqual(Serialization.GetTablePos(new Vector3i(1, 0, 0)), 4, "LookUpPos2");
            TestEqual(Serialization.GetTablePos(new Vector3i(0, 1, 0)), 1024, "LookUpPos3");
            TestEqual(Serialization.GetTablePos(new Vector3i(-1, 0, 0)), 60, "LookUpPos4");
        }
        {
            TestEqual(Mth.Mod16(0), 0, "Mod16_1");
            TestEqual(Mth.Mod16(15), 15, "Mod16_2");
            TestEqual(Mth.Mod16(16), 0, "Mod16_3");
            TestEqual(Mth.Mod16(35), 3, "Mod16_4");
            TestEqual(Mth.Mod16(-1), 15, "Mod16_5");
            TestEqual(Mth.Mod16(-16), 0, "Mod16_6");
            TestEqual(Mth.Mod16(-5), 11, "Mod16_7");
        }

        {
            int b = 0;

            b |= 0x1;

            TestEqual(b & 0x1, 1, "bit1");

            TestEqual(0x1, 1, "hex0");
            TestEqual(0x2, 2, "hex1");
            TestEqual(0x4, 4, "hex2");
            TestEqual(0x8, 8, "hex3");
            TestEqual(0x10, 16, "hex4");
            TestEqual(0x20, 32, "hex5");
            TestEqual(0x40, 64, "hex6");
            TestEqual(0x80, 128, "hex7");
            TestEqual(0x100, 256, "hex8");
            TestEqual(0x200, 512, "hex9");
            TestEqual(0x400, 1024, "hex10");
            TestEqual(0x800, 2048, "hex11");
            TestEqual(0x1000, 4096, "hex12");

        }

        {
            // U RRRRR GGGGG BBBBB
            ushort torch = 0b0_00010_00110_10011;
            TestEqual(LightCalculator.GetRed(torch), 2, "torch1");
            TestEqual(LightCalculator.GetGreen(torch), 6, "torch2");
            TestEqual(LightCalculator.GetBlue(torch), 19, "torch3");

            TestEqual(LightCalculator.GetIsLight(torch), false, "torchb1");

            torch = 0b0_11111_00000_11111;

            TestEqual(LightCalculator.GetRed(torch), 31, "torch4");
            TestEqual(LightCalculator.GetGreen(torch), 0, "torch5");
            TestEqual(LightCalculator.GetBlue(torch), 31, "torch6");

            torch = LightCalculator.SetIsLight(torch, true);
            TestEqual(LightCalculator.GetIsLight(torch), true, "torchb1");

            torch = 0b0_10000_00100_01010;

            TestEqual(LightCalculator.GetRed(torch), 16, "torch7");
            TestEqual(LightCalculator.GetGreen(torch), 4, "torch8");
            TestEqual(LightCalculator.GetBlue(torch), 10, "torch9");

            torch = LightCalculator.SetRed(torch, 8);
            TestEqual(torch, (ushort)0b0_01000_00100_01010, "torch10");
            torch = LightCalculator.SetBlue(torch, 23);
            TestEqual(torch, (ushort)0b0_01000_00100_10111, "torch11");
            torch = LightCalculator.SetGreen(torch, 0);
            TestEqual(torch, (ushort)0b0_01000_00000_10111, "torch12");

            torch = LightCalculator.SetIsLight(torch, true);
            TestEqual(LightCalculator.GetIsLight(torch), true, "torchb1");
            torch = LightCalculator.SetIsLight(torch, false);
            TestEqual(LightCalculator.GetIsLight(torch), false, "torchb1");

            int c = LightCalculator.GetColor(31, 31, 31);
            TestEqual(LightCalculator.GetRed(c), 31, "torch13");
            TestEqual(LightCalculator.GetGreen(c), 31, "torch14");
            TestEqual(LightCalculator.GetBlue(c), 31, "torch15");

            c = LightCalculator.GetColor(1, 7, 30);
            TestEqual(LightCalculator.GetRed(c), 1, "torch16");
            TestEqual(LightCalculator.GetGreen(c), 7, "torch17");
            TestEqual(LightCalculator.GetBlue(c), 30, "torch18");

            c = LightCalculator.GetColor(0, 0, 0);
            TestEqual(LightCalculator.GetRed(c), 0, "torch19");
            TestEqual(LightCalculator.GetGreen(c), 0, "torch20");
            TestEqual(LightCalculator.GetBlue(c), 0, "torch21");


            torch = 0b0_00010_00110_10011;
            TestEqual(LightCalculator.GetChannel(torch, 2), 2, "torch1");
            TestEqual(LightCalculator.GetChannel(torch, 1), 6, "torch2");
            TestEqual(LightCalculator.GetChannel(torch, 0), 19, "torch3");

            TestEqual(LightCalculator.GetIsLight(torch), false, "torchb1");

            torch = 0b1_11111_00000_11111;

            TestEqual(LightCalculator.GetIsLight(torch), true, "torchb1");

            TestEqual(LightCalculator.GetChannel(torch, 2), 31, "torch4");
            TestEqual(LightCalculator.GetChannel(torch, 1), 0, "torch5");
            TestEqual(LightCalculator.GetChannel(torch, 0), 31, "torch6");

            TestEqual(LightCalculator.GetIsLight(torch), true, "torchb1");

            torch = 0b1_10000_00100_01010;

            TestEqual(LightCalculator.GetChannel(torch, 2), 16, "torch7");
            TestEqual(LightCalculator.GetChannel(torch, 1), 4, "torch8");
            TestEqual(LightCalculator.GetChannel(torch, 0), 10, "torch9");

            torch = LightCalculator.SetChannel(torch, 2, 8);
            TestEqual(torch, (ushort)0b1_01000_00100_01010, "torch10");
            torch = LightCalculator.SetChannel(torch, 0, 23);
            TestEqual(torch, (ushort)0b1_01000_00100_10111, "torch11");
            torch = LightCalculator.SetChannel(torch, 1, 0);
            TestEqual(torch, (ushort)0b1_01000_00000_10111, "torch12");

            TestEqual(LightCalculator.GetIsLight(torch), true, "torchb1");

            c = LightCalculator.GetColor(31, 31, 31);
            TestEqual(LightCalculator.GetChannel(c, 2), 31, "torch13");
            TestEqual(LightCalculator.GetChannel(c, 1), 31, "torch14");
            TestEqual(LightCalculator.GetChannel(c, 0), 31, "torch15");

            c = LightCalculator.GetColor(1, 7, 30);
            TestEqual(LightCalculator.GetChannel(c, 2), 1, "torch16");
            TestEqual(LightCalculator.GetChannel(c, 1), 7, "torch17");
            TestEqual(LightCalculator.GetChannel(c, 0), 30, "torch18");

            c = LightCalculator.GetColor(0, 0, 0);
            TestEqual(LightCalculator.GetChannel(c, 2), 0, "torch19");
            TestEqual(LightCalculator.GetChannel(c, 1), 0, "torch20");
            TestEqual(LightCalculator.GetChannel(c, 0), 0, "torch21");

            TestEqual(LightCalculator.GetIsLight(torch), true, "torchb1");

        }

        {
            BlockData test = new BlockData(0) {
                colliderSolid = 0,
            };
            TestEqual(test.texture, 0, "bd1");
            TestEqual(test.light, (ushort)0, "bd2");
            TestEqual(test.renderType, (byte)1, "bd3");
            TestEqual(test.colliderSolid, (byte)0, "bd4");
            TestEqual(test.renderSolid, (byte)1, "bd5");
        }

        {
            TestStruct t;
            t.member = 0;
            TestEqual(t.member, 0, "struct1");
            t.IncMember();
            TestEqual(t.member, 1, "struct2");

        }


        string msg = string.Format("{0}/{1} tests passed", passes, passes + failures);
        if (failures == 0) {
            Debug.Log(string.Format("<color=#00FF00><b>{0}</b></color>", msg));
        } else {
            Debug.Log(msg);
        }
    }
}


struct TestStruct {
    public int member;

    public void IncMember() {
        member++;
    }
}