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

    static Mutex loadListMutex = new Mutex();
    static Mutex chunksLoadedMutex = new Mutex();
    static Mutex saveListMutex = new Mutex();
    static Mutex killMutex = new Mutex();

    static List<Chunk> chunksToLoad = new List<Chunk>();
    static List<Chunk> chunksLoaded = new List<Chunk>();
    static List<Chunk> chunksToSave = new List<Chunk>();

    public static void LoadChunk(Chunk chunk) {
        loadListMutex.WaitOne();
        chunksToLoad.Add(chunk);
        loadListMutex.ReleaseMutex();
        newData.Set();
    }

    public static void SaveChunk(Chunk chunk) {
        if (!chunk.updateSave) {
            return;
        }
        saveListMutex.WaitOne();
        chunksToSave.Add(chunk);
        saveListMutex.ReleaseMutex();
        newData.Set();
    }

    static bool kill = false;
    public static void KillThread() {
        killMutex.WaitOne();
        kill = true;
        killMutex.ReleaseMutex();
    }

    public static void CheckNewLoaded() {
        // tell main thread that these chunks were loaded
        chunksLoadedMutex.WaitOne();
        for (int i = 0; i < chunksLoaded.Count; ++i) {
            chunksLoaded[i].loaded = true;
            chunksLoaded[i].update = true;
        }
        chunksLoaded.Clear();
        chunksLoadedMutex.ReleaseMutex();
    }

    static Thread serializationThread;
    public static void StartThread() {
        serializationThread = new Thread(SerializationThread);
        serializationThread.Start();
    }

    static EventWaitHandle newData = new EventWaitHandle(false, EventResetMode.AutoReset);

    static void SerializationThread() {

        List<Chunk> loads = new List<Chunk>();
        List<Chunk> saves = new List<Chunk>();

        while (true) {
            // check if thread should quit
            killMutex.WaitOne();
            if (kill) {
                return;
            }
            killMutex.ReleaseMutex();

            // wait for new data signal on main thread
            Debug.Log("waiting for data");
            newData.WaitOne();
            Debug.Log("ok going");

            // copy over lists
            loadListMutex.WaitOne();
            for (int i = 0; i < chunksToLoad.Count; ++i) {
                loads.Add(chunksToLoad[i]);
            }
            chunksToLoad.Clear();
            loadListMutex.ReleaseMutex();

            saveListMutex.WaitOne();
            for (int i = 0; i < chunksToSave.Count; ++i) {
                saves.Add(chunksToSave[i]);
            }
            chunksToSave.Clear();
            saveListMutex.ReleaseMutex();

            Debug.Log("loading " + loads.Count);

            // load chunks
            for (int i = 0; i < loads.Count; ++i) {
                _LoadChunk(loads[i]);
            }

            // tell main thread that these chunks were loaded
            chunksLoadedMutex.WaitOne();
            for (int i = 0; i < loads.Count; ++i) {
                chunksLoaded.Add(loads[i]);
            }
            chunksLoadedMutex.ReleaseMutex();

            Debug.Log("saving " + saves.Count);

            // save chunks
            for (int i = 0; i < saves.Count; ++i) {
                _SaveChunk(saves[i]);
            }

            loads.Clear();
            saves.Clear();
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