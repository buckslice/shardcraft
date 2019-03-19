using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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
    }

    public static void StartThread() {
        Thread serializationThread = new Thread(SerializationThread);
        serializationThread.Start();
    }

    static EventWaitHandle newData = new EventWaitHandle(false, EventResetMode.AutoReset);

    static void SerializationThread() {

        List<Chunk> loads = new List<Chunk>();
        List<Chunk> saves = new List<Chunk>();

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
                Debug.Log("last run");
            }

            // copy over lists
            lock (chunksToLoad) {
                for (int i = 0; i < chunksToLoad.Count; ++i) {
                    loads.Add(chunksToLoad[i]);
                }
                chunksToLoad.Clear();
            }

            lock (chunksToSave) {
                for (int i = 0; i < chunksToSave.Count; ++i) {
                    saves.Add(chunksToSave[i]);
                }
                chunksToSave.Clear();
            }

            Debug.Log("loading " + loads.Count);

            // load chunks
            for (int i = 0; i < loads.Count; ++i) {
                _LoadChunk(loads[i]);

                // tell main thread that this chunk was loaded
                lock (chunksLoaded) {
                    chunksLoaded.Add(loads[i]);
                }
            }

            Debug.Log("saving " + saves.Count);

            // save chunks (dont need to tell main thread anything really)
            for (int i = 0; i < saves.Count; ++i) {
                _SaveChunk(saves[i]);
            }

            loads.Clear();
            saves.Clear();

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
        string saveFile = SaveLocation(chunk.world.worldName) + FileName(chunk.pos);

        IFormatter formatter = new BinaryFormatter();
        Stream stream = new FileStream(saveFile, FileMode.Create, FileAccess.Write, FileShare.None);
        //formatter.Serialize(stream, save);
        formatter.Serialize(stream, chunk.blocks.data);

        stream.Close();

    }

    static bool _LoadChunk(Chunk chunk) {
        string saveFile = SaveFileName(chunk);

        Debug.Assert(File.Exists(saveFile));

        IFormatter formatter = new BinaryFormatter();
        FileStream stream = new FileStream(saveFile, FileMode.Open);

        chunk.blocks = new Array3<Block>((Block[])formatter.Deserialize(stream), Chunk.SIZE);

        stream.Close();
        return true;
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