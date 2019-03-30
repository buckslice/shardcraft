using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;

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


    public static Pool<NativeList<Vector3>> v3Pool = new Pool<NativeList<Vector3>>(NLBuilder<Vector3>, NLDisposer);
    //public static Pool<NativeList<Vector2>> v2Pool = new Pool<NativeList<Vector2>>(NLBuilder<Vector2>, NLDisposer);
    public static Pool<NativeList<Color32>> c32Pool = new Pool<NativeList<Color32>>(NLBuilder<Color32>, NLDisposer);

    public static Pool<NativeList<int>> intPool = new Pool<NativeList<int>>(NLBuilder<int>, NLDisposer);

    public static Pool<NativeQueue<LightOp>> loQPool = new Pool<NativeQueue<LightOp>>(NQBuilder<LightOp>, NQDisposer);
    public static Pool<NativeQueue<int>> intQPool = new Pool<NativeQueue<int>>(NQBuilder<int>, NQDisposer);

}


public class Pool<T> {

    List<T> pool = new List<T>();

    int free = 0;
    readonly Func<T> buildFunc;
    readonly Action<T> disposeAction;

    public Pool(Func<T> buildFunc, Action<T> disposeAction) {
        this.buildFunc = buildFunc;
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
        return pool[free++];
    }

    public void Dispose() {
        Debug.Assert(free == 0); // make sure everything is returned before calling this
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
