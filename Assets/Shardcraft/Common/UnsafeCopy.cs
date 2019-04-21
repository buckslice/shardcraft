using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

// had to check 'Allow unsafe code' in player settings of unity to get this to compile
// sketchy af workaround until unity adds better helpers to work with meshes and native containers
// code from https://forum.unity.com/threads/can-mesh-generation-be-done.556873/
// and here https://forum.unity.com/threads/nativearray-and-mesh.522951/

public static class UnsafeCopy {
    public static unsafe void CopyVectors(NativeList<Vector3> src, List<Vector3> dst) {
        if (dst.Capacity < src.Length)
            dst.Capacity = src.Length;

        var array = NoAllocHelpers.ExtractArrayFromListT(dst);

        fixed (Vector3* arrayPtr = array) {
            var dstSlice = NativeSliceUnsafeUtility.ConvertExistingDataToNativeSlice<Vector3>(arrayPtr, sizeof(Vector3), src.Length);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            NativeSliceUnsafeUtility.SetAtomicSafetyHandle(ref dstSlice, AtomicSafetyHandle.GetTempUnsafePtrSliceHandle());
#endif
            dstSlice.CopyFrom((NativeArray<Vector3>)src);
        }

        NoAllocHelpers.ResizeList(dst, src.Length);
    }

    public static unsafe void CopyColors(NativeList<Color32> src, List<Color32> dst) {
        if (dst.Capacity < src.Length)
            dst.Capacity = src.Length;

        var array = NoAllocHelpers.ExtractArrayFromListT(dst);

        fixed (Color32* arrayPtr = array) {
            var dstSlice = NativeSliceUnsafeUtility.ConvertExistingDataToNativeSlice<Color32>(arrayPtr, sizeof(Color32), src.Length);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            NativeSliceUnsafeUtility.SetAtomicSafetyHandle(ref dstSlice, AtomicSafetyHandle.GetTempUnsafePtrSliceHandle());
#endif
            dstSlice.CopyFrom((NativeArray<Color32>)src);
        }

        NoAllocHelpers.ResizeList(dst, src.Length);
    }

    public static unsafe void CopyIntegers(NativeList<int> src, List<int> dst) {
        if (dst.Capacity < src.Length)
            dst.Capacity = src.Length;

        var array = NoAllocHelpers.ExtractArrayFromListT(dst);

        fixed (int* arrayPtr = array) {
            var dstSlice = NativeSliceUnsafeUtility.ConvertExistingDataToNativeSlice<int>(arrayPtr, sizeof(int), src.Length);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            NativeSliceUnsafeUtility.SetAtomicSafetyHandle(ref dstSlice, AtomicSafetyHandle.GetTempUnsafePtrSliceHandle());
#endif
            dstSlice.CopyFrom((NativeArray<int>)src);
        }

        NoAllocHelpers.ResizeList(dst, src.Length);
    }


    //public static unsafe void NativeAddRange<T>(this List<T> list, DynamicBuffer<T> dynamicBuffer)
    //        where T : struct {
    //    NativeAddRange(list, dynamicBuffer.GetBasePointer(), dynamicBuffer.Length);
    //}

    //public static unsafe void NativeAddRange<T>(this List<T> list, NativeList<T> nativeList)
    //where T : struct {
    //    NativeAddRange(list, UnsafeUtility., nativeList.Length);
    //}

    //private static unsafe void NativeAddRange<T>(List<T> list, void* arrayBuffer, int length)
    //    where T : struct {
    //    var index = list.Count;
    //    var newLength = index + length;

    //    // Resize our list if we require
    //    if (list.Capacity < newLength) {
    //        list.Capacity = newLength;
    //    }

    //    var items = NoAllocHelpers.ExtractArrayFromListT(list);
    //    var size = UnsafeUtility.SizeOf<T>();

    //    // Get the pointer to the end of the list
    //    var bufferStart = (IntPtr)UnsafeUtility.AddressOf(ref items[0]);
    //    var buffer = (byte*)(bufferStart + (size * index));

    //    UnsafeUtility.MemCpy(buffer, arrayBuffer, length * (long)size);

    //    NoAllocHelpers.ResizeList(list, newLength);
    //}

}

public static class NoAllocHelpers {
    private static readonly Dictionary<Type, Delegate> ExtractArrayFromListTDelegates = new Dictionary<Type, Delegate>();
    private static readonly Dictionary<Type, Delegate> ResizeListDelegates = new Dictionary<Type, Delegate>();

    /// <summary>
    /// Extract the internal array from a list.
    /// </summary>
    /// <typeparam name="T"><see cref="List{T}"/>.</typeparam>
    /// <param name="list">The <see cref="List{T}"/> to extract from.</param>
    /// <returns>The internal array of the list.</returns>
    public static T[] ExtractArrayFromListT<T>(List<T> list) {
        if (!ExtractArrayFromListTDelegates.TryGetValue(typeof(T), out var obj)) {
            var ass = Assembly.GetAssembly(typeof(Mesh)); // any class in UnityEngine
            var type = ass.GetType("UnityEngine.NoAllocHelpers");
            var methodInfo = type.GetMethod("ExtractArrayFromListT", BindingFlags.Static | BindingFlags.Public)
                .MakeGenericMethod(typeof(T));

            obj = ExtractArrayFromListTDelegates[typeof(T)] = Delegate.CreateDelegate(typeof(Func<List<T>, T[]>), methodInfo);
        }

        var func = (Func<List<T>, T[]>)obj;
        return func.Invoke(list);
    }

    /// <summary>
    /// Resize a list.
    /// </summary>
    /// <typeparam name="T"><see cref="List{T}"/>.</typeparam>
    /// <param name="list">The <see cref="List{T}"/> to resize.</param>
    /// <param name="size">The new length of the <see cref="List{T}"/>.</param>
    public static void ResizeList<T>(List<T> list, int size) {
        if (!ResizeListDelegates.TryGetValue(typeof(T), out var obj)) {
            var ass = Assembly.GetAssembly(typeof(Mesh)); // any class in UnityEngine
            var type = ass.GetType("UnityEngine.NoAllocHelpers");
            var methodInfo = type.GetMethod("ResizeList", BindingFlags.Static | BindingFlags.Public)
                .MakeGenericMethod(typeof(T));
            obj = ResizeListDelegates[typeof(T)] =
                Delegate.CreateDelegate(typeof(Action<List<T>, int>), methodInfo);
        }

        var action = (Action<List<T>, int>)obj;
        action.Invoke(list, size);
    }
}
