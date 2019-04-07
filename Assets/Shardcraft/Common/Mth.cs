using UnityEngine;
using Unity.Mathematics;
//https://github.com/Unity-Technologies/Unity.Mathematics/blob/master/src/Unity.Mathematics/random.cs

public static class Mth {

    // true modulo implementation
    public static int Mod(int x, int m) {
        return (x % m + m) % m;
        //int r = x % m;
        //return r < 0 ? r + m : r;
    }

    // better not to use extra mod with floats if can i guess
    public static float Mod(float a, float b) {
        float c = a % b;
        if ((c < 0 && b > 0) || (c > 0 && b < 0)) {
            c += b;
        }
        return c;
    }

    public static int Mod16(int x) {
        return (x % 16 + 16) % 16;
    }
    public static int Mod32(int x) {
        return (x % 32 + 32) % 32;
    }

    // range -1 - 1
    public static float Fractal(Vector3 v, int octaves, float frequency, float amplitude = 1.0f, float persistence = 0.5f, float lacunarity = 2.0f) {
        float total = 0.0f;

        for (int i = 0; i < octaves; ++i) {
            float n = noise.snoise((v * frequency));
            //float n = noise.cellular(v * frequency).y;

            //float2 n2 = noise.cellular(v * frequency);
            //float n = n2.y - n2.x;

            total += n * amplitude;

            amplitude *= persistence;
            frequency *= lacunarity;
        }

        return total;
    }

    public static float Billow(Vector3 v, int octaves, float frequency, float amplitude = 1.0f, float persistence = 0.5f, float lacunarity = 2.0f) {
        float total = 0.0f;

        for (int i = 0; i < octaves; ++i) {
            float n = noise.snoise(v * frequency);
            n = 2.0f * Mathf.Abs(n) - 1.0f;
            total += n * amplitude;

            amplitude *= persistence;
            frequency *= lacunarity;
        }
        total -= 0.5f;

        return total;
    }

    public static float4 FractalGrad(Vector3 v, int octaves, float frequency, float amplitude = 1.0f, float persistence = 0.5f, float lacunarity = 2.0f) {
        float total = 0.0f;
        float3 gradTotal = new float3();

        for (int i = 0; i < octaves; ++i) {
            float n = noise.snoise((v * frequency), out float3 grad);
            //float n = noise.cellular(v * frequency).y;

            //float2 n2 = noise.cellular(v * frequency);
            //float n = n2.y - n2.x;

            total += n * amplitude;
            gradTotal += grad * amplitude;

            amplitude *= persistence;
            frequency *= lacunarity;
        }

        return new float4(total, gradTotal.x, gradTotal.y, gradTotal.z);
    }

    public static float QuadraticEaseOut(float p) {
        return -(p * (p - 2));
    }

}