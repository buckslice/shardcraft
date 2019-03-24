#define _DEBUG

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System;
using System.Threading;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Serialization;
using System.Collections.Concurrent;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

public static class Serialization {

    // public methods add to these dictionaries
    static Dictionary<Vector3i, Queue<Chunk>> chunksToLoad = new Dictionary<Vector3i, Queue<Chunk>>();
    static Dictionary<Vector3i, Queue<Chunk>> chunksToSave = new Dictionary<Vector3i, Queue<Chunk>>();
    // these ones are used internally while thread works
    static Dictionary<Vector3i, Queue<Chunk>> _chunksToLoad = new Dictionary<Vector3i, Queue<Chunk>>();
    static Dictionary<Vector3i, Queue<Chunk>> _chunksToSave = new Dictionary<Vector3i, Queue<Chunk>>();

    static Queue<Chunk> chunksLoaded = new Queue<Chunk>();
    static Queue<Chunk> chunksFailed = new Queue<Chunk>();
    static Queue<Chunk> chunksFreed = new Queue<Chunk>();

    public static void LoadChunk(Chunk chunk) {
        Vector3i rc = GetRegionCoord(chunk.cp);
        lock (chunksToLoad) {
            if (chunksToLoad.TryGetValue(rc, out Queue<Chunk> q)) {
                q.Enqueue(chunk);
            } else {
                q = new Queue<Chunk>();
                q.Enqueue(chunk);
                chunksToLoad[rc] = q;
            }
        }
        newWork.Set();
    }

    public static void SaveChunk(Chunk chunk, bool manualSet = false) {
        if (!chunk.needToUpdateSave) {
            lock (chunksFreed) {
                chunksFreed.Enqueue(chunk);
            }
            return;
        }
        Vector3i rc = GetRegionCoord(chunk.cp);
        lock (chunksToSave) {
            if (chunksToSave.TryGetValue(rc, out Queue<Chunk> q)) {
                q.Enqueue(chunk);
            } else {
                q = new Queue<Chunk>();
                q.Enqueue(chunk);
                chunksToSave[rc] = q;
            }
        }
        if (!manualSet) {
            newWork.Set();
        }
    }

    public static void SetNewWork() {
        newWork.Set();
    }

    // sets new chunk variables and collects load failed chunks to be generated
    public static int CheckNewLoaded(Queue<Chunk> failed) {
        int loaded = 0;
        lock (chunksLoaded) {
            loaded = chunksLoaded.Count;
            while (chunksLoaded.Count > 0) {
                Chunk c = chunksLoaded.Dequeue();
                c.loaded = true;
                c.update = true;
            }
        }

        lock (chunksFailed) {
            while (chunksFailed.Count > 0) {
                failed.Enqueue(chunksFailed.Dequeue());
            }
        }

        return loaded;
    }

    public static void CheckChunkFreed(Pool<Chunk> pool) {
        lock (chunksFreed) {
            while (chunksFreed.Count > 0) {
                pool.Return(chunksFreed.Dequeue());
            }
        }
    }

    static readonly object killLock = new object();
    static bool kill = false;
    public static void KillThread() {
        lock (killLock) {
            kill = true;
            killWatch.Start();
        }
        newWork.Set();
        Debug.Log("IO thread shutting down...");
    }

    public static Thread thread;
    public static void StartThread() {
        thread = new Thread(SerializationThread);
        thread.Start();
    }

    static EventWaitHandle newWork = new EventWaitHandle(false, EventResetMode.AutoReset);

    static GafferNet.WriteStream writer = new GafferNet.WriteStream();
    static GafferNet.ReadStream reader = new GafferNet.ReadStream();
    static uint[] writeBuffer = new uint[32768];
    static System.Diagnostics.Stopwatch killWatch = new System.Diagnostics.Stopwatch();

    static void SerializationThread() {

        var watch = new System.Diagnostics.Stopwatch();

        while (true) {

            bool lastRun = false;
            lock (killLock) {
                if (kill) {
                    lastRun = true;
                }
            }

            if (!lastRun) { // if last run just blast through to double check
#if _DEBUG
                Debug.Log("waiting for data");
#endif
                // wait for new work signal on main thread
                newWork.WaitOne();
#if _DEBUG
                Debug.Log("ok going");
#endif
            } else {
#if _DEBUG
                Debug.Log("last check");
#endif

            }

            // swap references with internal lists
            Dictionary<Vector3i, Queue<Chunk>> tmp;
            lock (chunksToLoad) {
                tmp = chunksToLoad;
                chunksToLoad = _chunksToLoad;
                _chunksToLoad = tmp;
            }
            lock (chunksToSave) {
                tmp = chunksToSave;
                chunksToSave = _chunksToSave;
                _chunksToSave = tmp;
            }


            foreach (var entry in _chunksToLoad) {
                var chunks = entry.Value;
                if (chunks.Count <= 0) {
                    continue;
                }
#if _DEBUG
                Debug.Log("loading " + chunks.Count + " chunks from region " + entry.Key);
                watch.Restart();
#endif

                // open region file then load each chunk from it

                while (chunks.Count > 0) {
                    Chunk c = chunks.Dequeue();

                    string saveFile = SaveFileName(c);

                    if (!File.Exists(saveFile)) {
                        lock (chunksFailed) {
                            chunksFailed.Enqueue(c);
                        }
                    } else {
                        _LoadChunk(c);
                    }
                }

#if _DEBUG
                Debug.Log("loaded in " + watch.ElapsedMilliseconds + " ms");
#endif
            }

            foreach (var entry in _chunksToSave) {
                var chunks = entry.Value;
                if (chunks.Count <= 0) {
                    continue;
                }
#if _DEBUG
                Debug.Log("saving " + chunks.Count + " chunks from region " + entry.Key);
                watch.Restart();
#endif

                // open region file then save each chunk to it

                while (chunks.Count > 0) {
                    _SaveChunk(chunks.Dequeue());
                }
#if _DEBUG
                Debug.Log("saved in " + watch.ElapsedMilliseconds + " ms");
#endif
            }

            if (lastRun) {
                // verify that lists are all empty before quiting
                lock (chunksToSave) {
                    foreach (var entry in chunksToSave) {
                        Debug.Assert(entry.Value.Count == 0);
                    }
                }
                lock (chunksToLoad) {
                    foreach (var entry in chunksToLoad) {
                        Debug.Assert(entry.Value.Count == 0);
                    }
                }

                Debug.Log("IO thread shut down " + killWatch.ElapsedMilliseconds + " ms post kill");
                return;
            }

        }
    }


    public static string saveFolderName = "saves";

    static string SaveLocation(string worldName) {
        string saveLocation = saveFolderName + "/" + worldName + "/";

        if (!Directory.Exists(saveLocation)) {
            Directory.CreateDirectory(saveLocation);
        }

        return saveLocation;
    }

    static string FileName(Vector3i chunkPos) {
        chunkPos.Div(Chunk.SIZE);
        //string fileName = chunkLocation.x + "," + chunkLocation.y + "," + chunkLocation.z + ".scs";
        return string.Format("{0},{1},{2}.scs", chunkPos.x, chunkPos.y, chunkPos.z);
    }

    public static string SaveFileName(Chunk chunk) {
        return SaveLocation(chunk.world.worldName) + FileName(chunk.wp);
    }

    static void _SaveChunk(Chunk chunk) {

        // run length encoding, a byte for type followed by byte for runcount
        // rewrote project to access data in xzy order, because i assume there will be more horizontal than vertical structures in the world gen (and thus more runs)
        // WWWWWBWWWBWWWW  - example
        // W5B1W3B1W4   - type followed by run count (up to length 256)
        // WW5BWW3BWW4  - alternate way where double type indicates a run
        // runs cost one byte extra but singles cost one byte less? not sure if worth

        // todo: investigate whether using ushort is better for runs and types eventually maybe

        writer.Start(writeBuffer);

        var blocks = chunk.blocks;

        // circumvent the safety check here because it gets mad for some reason when trying to save
        // even though i am quite sure it is safe. Saving is only called when a chunk is being destroyed
        // and only if it was either already generated new or loaded and then modified
        // in both cases the generation job is finished editing the blocks
        // i think it doesnt trust the fact that its in a separate thread
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        var handle = AtomicSafetyHandle.Create();
        NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref blocks, handle);
#endif

        int i = 0;
        while (i < blocks.Length) {
            byte type = blocks[i].type;
            byte run = 1;
            while (++i < blocks.Length && blocks[i].type == type && run < byte.MaxValue) { run++; }
            writer.Write(type);
            writer.Write(run);
        }

        writer.Finish();
        byte[] bytes = writer.GetData();

        string saveFile = SaveLocation(chunk.world.worldName) + FileName(chunk.wp);
        File.WriteAllBytes(saveFile, bytes);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        AtomicSafetyHandle.Release(handle);
#endif

        lock (chunksFreed) {
            chunksFreed.Enqueue(chunk);
        }
    }

    static void _LoadChunk(Chunk chunk) {
        string saveFile = SaveFileName(chunk);
        Debug.Assert(File.Exists(saveFile));

        byte[] bytes = File.ReadAllBytes(saveFile);
        reader.Start(bytes);

        var blocks = chunk.blocks;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        var handle = AtomicSafetyHandle.Create();
        NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref blocks, handle);
#endif

        int i = 0;
        while (i < blocks.Length) {
            byte type = reader.ReadByte();
            byte run = reader.ReadByte();
            while (run-- > 0) {
                blocks[i++] = new Block { type = type };
            }
        }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        AtomicSafetyHandle.Release(handle);
#endif

        lock (chunksLoaded) {
            chunksLoaded.Enqueue(chunk);
        }
    }

    // regions are 16x16x16 chunks
    // todo: add chunk and region border line rendering
    public static Vector3i GetRegionCoord(Vector3i cp) {

        //return chunkPos / 16; // doesnt deal with negatives as smoothly as bit shifting
        return new Vector3i(cp.x >> 4, cp.y >> 4, cp.z >> 4);

    }


    public static void SavePlayer() {
        string saveFile = saveFolderName + "/" + "player.bin";

        CamModify player = Camera.main.transform.GetComponent<CamModify>();

        IFormatter formatter = new BinaryFormatter();
        Stream stream = new FileStream(saveFile, FileMode.Create, FileAccess.Write, FileShare.None);
        formatter.Serialize(stream, player.transform.position.x);
        formatter.Serialize(stream, player.transform.position.y);
        formatter.Serialize(stream, player.transform.position.z);
        formatter.Serialize(stream, player.yaw);
        formatter.Serialize(stream, player.pitch);

        stream.Close();
    }

    public static void LoadPlayer() {
        string saveFile = saveFolderName + "/" + "player.bin";

        if (!File.Exists(saveFile)) {
            return;
        }

        CamModify player = Camera.main.transform.GetComponent<CamModify>();

        IFormatter formatter = new BinaryFormatter();
        FileStream stream = new FileStream(saveFile, FileMode.Open);

        player.transform.position = new Vector3((float)formatter.Deserialize(stream), (float)formatter.Deserialize(stream), (float)formatter.Deserialize(stream));
        player.yaw = (float)formatter.Deserialize(stream);
        player.pitch = (float)formatter.Deserialize(stream);

        stream.Close();
    }

}