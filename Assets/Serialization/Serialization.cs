using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System;
using System.Threading;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Serialization;
using System.Collections.Concurrent;

// todo: add regions which are 16x16x16 chunks in one file so not so many files reading and writing

public static class Serialization {

    static List<Chunk> chunksToLoad = new List<Chunk>();
    static List<Chunk> chunksLoaded = new List<Chunk>();
    static List<Chunk> chunksToSave = new List<Chunk>();

    public static void LoadChunk(Chunk chunk) {
        lock (chunksToLoad) {
            chunksToLoad.Add(chunk);
        }
        newData.Set();
    }

    public static void SaveChunk(Chunk chunk) {
        if (!chunk.needToUpdateSave) {
            return;
        }
        lock (chunksToSave) {
            chunksToSave.Add(chunk);
        }
        newData.Set();
    }

    public static void CheckNewLoaded() {
        lock (chunksLoaded) {
            for (int i = 0; i < chunksLoaded.Count; ++i) {
                chunksLoaded[i].loaded = true;
                chunksLoaded[i].update = true;
            }
            chunksLoaded.Clear();
        }
    }

    static readonly object killLock = new object();
    static bool kill = false;
    public static void KillThread() {
        lock (killLock) {
            kill = true;
        }
        newData.Set();
    }

    public static void StartThread() {
        Thread serializationThread = new Thread(SerializationThread);
        serializationThread.Start();
    }

    static EventWaitHandle newData = new EventWaitHandle(false, EventResetMode.AutoReset);

    static GafferNet.WriteStream writer = new GafferNet.WriteStream();
    static GafferNet.ReadStream reader = new GafferNet.ReadStream();
    const int buffMax = 32768;
    static uint[] writeBuffer = new uint[buffMax];
    static byte[] readBuffer = new byte[buffMax];

    static void SerializationThread() {

        var watch = new System.Diagnostics.Stopwatch();

        List<Chunk> newLoads = new List<Chunk>();
        List<Chunk> newSaves = new List<Chunk>();

        while (true) {

            bool lastRun = false;
            lock (killLock) {
                if (kill) {
                    lastRun = true;
                }
            }

            if (!lastRun) { // if last run just blast through to double check
                Debug.Log("waiting for data");
                // wait for new data signal on main thread
                newData.WaitOne();
                Debug.Log("ok going");
            } else {
                Debug.Log("last check");
            }

            // copy over new lists
            lock (chunksToLoad) {
                for (int i = 0; i < chunksToLoad.Count; ++i) {
                    newLoads.Add(chunksToLoad[i]);
                }
                chunksToLoad.Clear();
            }

            lock (chunksToSave) {
                for (int i = 0; i < chunksToSave.Count; ++i) {
                    newSaves.Add(chunksToSave[i]);
                }
                chunksToSave.Clear();
            }

            if (newLoads.Count > 0) {
                Debug.Log("loading " + newLoads.Count);

                watch.Restart();
                // load chunks
                for (int i = 0; i < newLoads.Count; ++i) {
                    _LoadChunk(newLoads[i]);

                    // tell main thread that this chunk was loaded
                    lock (chunksLoaded) {
                        chunksLoaded.Add(newLoads[i]);
                    }
                }
                watch.Stop();
                Debug.Log("loaded in " + watch.ElapsedMilliseconds + " ms");
            }

            if (newSaves.Count > 0) {
                Debug.Log("saving " + newSaves.Count);

                watch.Restart();

                // save chunks (dont need to tell main thread anything really)
                for (int i = 0; i < newSaves.Count; ++i) {
                    _SaveChunk(newSaves[i]);
                }

                watch.Stop();
                Debug.Log("saved in " + watch.ElapsedMilliseconds + " ms");
            }

            newLoads.Clear();
            newSaves.Clear();

            if (lastRun) {
                lock (chunksToSave) {
                    Debug.Assert(chunksToSave.Count == 0);
                }
                lock (chunksToLoad) {
                    Debug.Assert(chunksToLoad.Count == 0);
                }

                Debug.Log("IO thread shutting down");
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
        return SaveLocation(chunk.world.worldName) + FileName(chunk.pos);
    }

    static void _SaveChunk(Chunk chunk) {
        // build save pack
        PacketWriter pack = new PacketWriter(writer, writeBuffer);

        pack.Write(chunk.blocks.Length);

        // next up try RLE, write a byte for length then a byte for type or somethin!!! also were in xzy mode now very nice
        var data = chunk.blocks;
        for (int i = 0; i < data.Length; ++i) {
            pack.Write(data[i].type);
        }

        byte[] bytes = pack.GetData();

        string saveFile = SaveLocation(chunk.world.worldName) + FileName(chunk.pos);
        File.WriteAllBytes(saveFile, bytes);

    }

    static void _LoadChunk(Chunk chunk) {
        string saveFile = SaveFileName(chunk);
        Debug.Assert(File.Exists(saveFile));

        byte[] bytes = File.ReadAllBytes(saveFile);
        PacketReader pack = new PacketReader(reader, bytes);
        int count = pack.ReadInt();

        // read into block array
        Block[] blocks = new Block[Chunk.SIZE * Chunk.SIZE * Chunk.SIZE];
        Debug.Assert(count == blocks.Length);
        for (int i = 0; i < count; ++i) {
            blocks[i] = new Block { type = pack.ReadByte() };
        }
        chunk.blocks = blocks;
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