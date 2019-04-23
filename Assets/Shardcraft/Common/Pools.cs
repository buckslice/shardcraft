using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using UnityEngine.Assertions;

public static class Pools {

    static NativeList<T> NLBuilder<T>() where T : struct {
        return new NativeList<T>(Allocator.Persistent);
    }

    static NativeQueue<T> NQBuilder<T>() where T : struct {
        return new NativeQueue<T>(Allocator.Persistent);
    }

    static void NLDisposer<T>(NativeList<T> list) where T : struct {
        list.Dispose();
    }
    static void NQDisposer<T>(NativeQueue<T> queue) where T : struct {
        queue.Dispose();
    }

    static void NLClearer<T>(NativeList<T> list) where T : struct {
        list.Clear();
    }
    static void NQClearer<T>(NativeQueue<T> list) where T : struct {
        list.Clear();
    }

    static List<T> LBuilder<T>() {
        return new List<T>();
    }
    static void LClearer<T>(List<T> list) {
        list.Clear();
    }

    public static Pool<NativeList<Vector3>> v3N = new Pool<NativeList<Vector3>>(NLBuilder<Vector3>, NLClearer, NLDisposer);
    //public static Pool<NativeList<Vector2>> v2Pool = new Pool<NativeList<Vector2>>(NLBuilder<Vector2>, NLDisposer);
    public static Pool<NativeList<Color32>> c32N = new Pool<NativeList<Color32>>(NLBuilder<Color32>, NLClearer, NLDisposer);

    public static Pool<NativeList<int>> intN = new Pool<NativeList<int>>(NLBuilder<int>, NLClearer, NLDisposer);

    public static Pool<NativeQueue<TorchLightOp>> tloQN = new Pool<NativeQueue<TorchLightOp>>(NQBuilder<TorchLightOp>, NQClearer, NQDisposer);
    public static Pool<NativeQueue<LightRemovalNode>> lrnQN = new Pool<NativeQueue<LightRemovalNode>>(NQBuilder<LightRemovalNode>, NQClearer, NQDisposer);
    public static Pool<NativeQueue<int>> intQN = new Pool<NativeQueue<int>>(NQBuilder<int>, NQClearer, NQDisposer);

    public static Pool<List<Vector3>> v3 = new Pool<List<Vector3>>(LBuilder<Vector3>, LClearer, null);
    public static Pool<List<int>> i = new Pool<List<int>>(LBuilder<int>, LClearer, null);
    public static Pool<List<Color32>> c32 = new Pool<List<Color32>>(LBuilder<Color32>, LClearer, null);

    public static void Dispose() {
        v3N.Dispose();
        c32N.Dispose();
        intN.Dispose();

        tloQN.Dispose();
        intQN.Dispose();
        lrnQN.Dispose();

    }

}


public class Pool<T> {

    List<T> pool = new List<T>();

    int free = 0;
    readonly Func<T> buildFunc;
    readonly Action<T> disposeAction;
    readonly Action<T> getAction;

    public Pool(Func<T> buildFunc, Action<T> getAction, Action<T> disposeAction) {
        this.buildFunc = buildFunc;
        this.getAction = getAction;
        this.disposeAction = disposeAction;
    }

    public void Return(T t) {
        if (free <= 0) {
            throw new InvalidOperationException(); // returned more than you got
        }
        pool[--free] = t;
    }

    public T Get() {
        if (free >= pool.Count) {
            pool.Add(buildFunc());
        }
        getAction?.Invoke(pool[free]);
        return pool[free++];
    }

    public void Dispose() {
        Assert.IsTrue(free == 0); // make sure everything is returned before calling this
        for (int i = 0; i < pool.Count; ++i) {
            disposeAction(pool[i]);
        }
    }

    public int Count() {
        return pool.Count;
    }
    public int CountFree() {
        return pool.Count - free;
    }

}
