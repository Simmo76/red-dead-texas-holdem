using System;
using System.IO;
using UnityEngine;

namespace CinematicPoker.Game.Save
{
    /// <summary>
    /// Offline-first persistence: JSON in Application.persistentDataPath with
    /// atomic writes (temp file + replace) so a crash or battery death mid-write
    /// never corrupts the save. No network, no account, no server.
    /// </summary>
    public static class SaveSystem
    {
        private const string FileName = "cinematic_poker_save.json";
        private static string SavePath => Path.Combine(Application.persistentDataPath, FileName);
        private static string TempPath => SavePath + ".tmp";
        private static string BackupPath => SavePath + ".bak";

        public static SaveData Load()
        {
            try
            {
                if (File.Exists(SavePath))
                    return JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath)) ?? new SaveData();

                // Recover from a backup if the main file vanished mid-write.
                if (File.Exists(BackupPath))
                    return JsonUtility.FromJson<SaveData>(File.ReadAllText(BackupPath)) ?? new SaveData();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Save load failed, starting fresh: {ex.Message}");
            }
            return new SaveData();
        }

        public static void Save(SaveData data)
        {
            try
            {
                string json = JsonUtility.ToJson(data, prettyPrint: false);
                File.WriteAllText(TempPath, json);

                if (File.Exists(SavePath))
                {
                    File.Copy(SavePath, BackupPath, overwrite: true);
                    File.Delete(SavePath);
                }
                File.Move(TempPath, SavePath);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Save failed: {ex.Message}");
            }
        }

        public static void DeleteAll()
        {
            foreach (string path in new[] { SavePath, TempPath, BackupPath })
                if (File.Exists(path))
                    File.Delete(path);
        }
    }
}
