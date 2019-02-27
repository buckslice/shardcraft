//using System.Collections;
//using System.Collections.Generic;
//using UnityEngine;

//public static class WorldGenerator {


//    public static void Generate(Chunk chunk) {

//        for (int x = 0; x < Chunk.SIZE; ++x) {
//            for (int y = 0; y < Chunk.SIZE; ++y) {
//                for (int z = 0; z < Chunk.SIZE; ++z) {
//                    Vector3 wp = new Vector3(x, y, z) + chunk.pos.ToVector3();
//                    float n = 0.0f;

//                    // experiment with catlike coding noise some more
//                    //NoiseSample samp = Noise.Sum(Noise.Simplex3D, wp, 0.015f, 5, 2.0f, 0.5f);
//                    //float n = samp.value * 3.0f;

//                    // TODO: go get that density.cs file in here, and convert more from shapes.cginc, maybe its called shapes.cs... i think and or get gen going on multiple thread (try job system!!!)
//                    n -= Vector3.Dot(wp, Vector3.up) * 0.05f;

//                    n += Noise.Fractal(wp, 5, 0.01f);

//                    if (n > 0.3f) {
//                        chunk.SetBlock(x, y, z, Blocks.STONE);
//                    } else if (n > 0.15f) {
//                        chunk.SetBlock(x, y, z, Blocks.GRASS);

//                        // trying to make grass not spawn on cliff edge...
//                        //if (Mathf.Abs(samp.derivative.normalized.y) < 0.4f) {
//                        //    chunk.SetBlock(x, y, z, new BlockGrass());
//                        //} else {
//                        //    chunk.SetBlock(x, y, z, new Block());
//                        //}
//                    } else {
//                        chunk.SetBlock(x, y, z, Blocks.AIR);
//                    }

//                }
//            }
//        }

//    }

//}
