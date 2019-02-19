using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class WorldGenerator {


    public static void Generate(Chunk chunk) {

        for (int x = chunk.pos.x; x < chunk.pos.x + Chunk.SIZE; ++x) {
            for (int y = chunk.pos.y; y < chunk.pos.y + Chunk.SIZE; ++y) {
                for (int z = chunk.pos.z; z < chunk.pos.z + Chunk.SIZE; ++z) {
                    float n = Noise.Fractal(new Vector3(x, y, z), 5, 0.01f);

                    if (n > 0.2f) {
                        chunk.SetBlock(x, y, z, new Block(), true);
                    } else {
                        chunk.SetBlock(x, y, z, new BlockAir(), true);
                    }
                }
            }
        }

    }

}
