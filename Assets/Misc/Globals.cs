using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// this file stores strings for tags and layers and such to reduce typos and
// allows for autocompletion when programming stuff
public static class Tags {
    public const string Player = "Player";
    public const string Terrain = "Terrain";
}


public static class Layers {
    //public const int PlayerHitBox = 8;
}

public static class ShaderParams {
    public static int Color = Shader.PropertyToID("_Color");
    public static int MainTex = Shader.PropertyToID("_MainTex");
}

public static class ListExtensions {

    private static System.Random rng = new System.Random();

    public static void Shuffle<T>(this IList<T> list) {
        int n = list.Count;
        while (n > 1) {
            n--;
            int k = rng.Next(n + 1);
            T value = list[k];
            list[k] = list[n];
            list[n] = value;
        }
    }

    // fast way to remove from list if order doesn't matter
    public static void SwapAndPop<T>(this IList<T> list, int i) {
        list[i] = list[list.Count - 1];
        list.RemoveAt(list.Count - 1);
    }

}