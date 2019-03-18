using UnityEngine;
using System.Collections;
using System.IO;
using System;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Serialization;

// todo: add regions which are 16x16x16 chunks in one file so not so many files reading and writing

public static class Serialization {

    public static string saveFolderName = "saves";

    public static string SaveLocation(string worldName) {
        string saveLocation = saveFolderName + "/" + worldName + "/";

        if (!Directory.Exists(saveLocation)) {
            Directory.CreateDirectory(saveLocation);
        }

        return saveLocation;
    }

    public static string FileName(Vector3i chunkLocation) {
        chunkLocation.Div(Chunk.SIZE);
        string fileName = chunkLocation.x + "," + chunkLocation.y + "," + chunkLocation.z + ".scs";
        return fileName;
    }

    public static string SaveFileName(Chunk chunk) {
        return SaveLocation(chunk.world.worldName) + FileName(chunk.pos);
    }

    public static void SaveChunk(Chunk chunk) {
        //ChunkSave save = new ChunkSave(chunk);
        //if (save.blocks.Count == 0) {
        //    return;
        //}

        string saveFile = SaveLocation(chunk.world.worldName) + FileName(chunk.pos);

        IFormatter formatter = new BinaryFormatter();
        Stream stream = new FileStream(saveFile, FileMode.Create, FileAccess.Write, FileShare.None);
        //formatter.Serialize(stream, save);
        formatter.Serialize(stream, chunk.blocks.data);
        stream.Close();

    }

    public static bool LoadChunk(Chunk chunk) {
        string saveFile = SaveFileName(chunk);

        Debug.Assert(File.Exists(saveFile));

        IFormatter formatter = new BinaryFormatter();
        FileStream stream = new FileStream(saveFile, FileMode.Open);

        //ChunkSave save = (ChunkSave)formatter.Deserialize(stream);
        //foreach (var block in save.blocks) {
        //    chunk.blocks[block.Key] = new Block(block.Value.type);
        //    chunk.modifiedBlockIndices.Add(block.Key);
        //}

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