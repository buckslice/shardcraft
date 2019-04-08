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

    public static float Ridged(Vector3 v, int octaves, float frequency, float amplitude = 1.0f, float persistence = 0.5f, float lacunarity = 2.0f) {
        float total = 0.0f;

        for (int i = 0; i < octaves; ++i) {
            float n = noise.snoise(v * frequency);
            n = 1.0f - Mathf.Abs(n);
            total += n * amplitude;

            amplitude *= persistence;
            frequency *= lacunarity;
        }

        return (total - 1.1f) * 1.25f;
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

    public static float QuadraticEaseIn(float p) {
        return p * p;
    }

    // experiment with other blend function shapes
    public static float MapCubicSCurve(float p) {
        return p * p * (3f - 2f * p);
    }

    // n0/n1 lower/upper noise
    // bn blend noise
    // min/max of blend range
    // if bn < min, output is n0
    // if bn > max, output is n1
    // if in between then output is blending of the two
    public static float Blend(float n0, float n1, float bn, float min, float max) {
        if (bn <= min) {
            return n0;
        }
        if (bn < max) {
            float t = MapCubicSCurve((bn - min) / (max - min));
            return (1f - t) * n0 + t * n1;  // lerp(n0,n1,t);
        }
        return n1;
    }


}