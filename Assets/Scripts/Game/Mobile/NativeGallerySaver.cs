using System;
using System.IO;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace CinematicPoker.Game.Mobile
{
    /// <summary>
    /// Saves PNG images into the device photo gallery.
    ///
    /// Android: inserted through MediaStore into Pictures/Cinematic Poker.
    ///   API 29+ needs no permission; API 23-28 requests WRITE_EXTERNAL_STORAGE
    ///   at runtime (declared in the manifest via BuildScript's
    ///   "Write Permission: External" player setting).
    /// iOS: handed to Assets/Plugins/iOS/CinematicPokerGallery.m, which uses
    ///   the Photos framework with add-only access.
    /// Editor / desktop: written to persistentDataPath/Screenshots so the
    ///   feature is testable without a device.
    /// </summary>
    public static class NativeGallerySaver
    {
        public const string AlbumName = "Cinematic Poker";

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void CinematicPoker_SaveImageToGallery(string path);
#endif

        public static void SavePng(byte[] pngBytes, string fileName)
        {
            if (pngBytes == null || pngBytes.Length == 0) return;

#if UNITY_ANDROID && !UNITY_EDITOR
            SaveOnAndroid(pngBytes, fileName);
#elif UNITY_IOS && !UNITY_EDITOR
            // The native plugin deletes the temp file once the import finishes.
            string path = Path.Combine(Application.temporaryCachePath, fileName);
            File.WriteAllBytes(path, pngBytes);
            CinematicPoker_SaveImageToGallery(path);
#else
            string dir = Path.Combine(Application.persistentDataPath, "Screenshots");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, fileName);
            File.WriteAllBytes(path, pngBytes);
            Debug.Log($"Win photo saved to {path} (editor/desktop fallback; on a phone it goes to the photo gallery).");
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static void SaveOnAndroid(byte[] pngBytes, string fileName)
        {
            int sdk;
            using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                sdk = version.GetStatic<int>("SDK_INT");

            if (sdk >= 29)
            {
                WriteToMediaStore(pngBytes, fileName, scopedStorage: true);
                return;
            }

            // API 23-28: writing to the shared gallery needs WRITE_EXTERNAL_STORAGE.
            if (Permission.HasUserAuthorizedPermission(Permission.ExternalStorageWrite))
            {
                WriteToMediaStore(pngBytes, fileName, scopedStorage: false);
                return;
            }

            var callbacks = new PermissionCallbacks();
            callbacks.PermissionGranted += _ => WriteToMediaStore(pngBytes, fileName, scopedStorage: false);
            callbacks.PermissionDenied += _ =>
                Debug.LogWarning("Win photo not saved: storage permission denied.");
            Permission.RequestUserPermission(Permission.ExternalStorageWrite, callbacks);
        }

        private static void WriteToMediaStore(byte[] pngBytes, string fileName, bool scopedStorage)
        {
            try
            {
                using var media = new AndroidJavaClass("android.provider.MediaStore$Images$Media");
                using AndroidJavaObject externalUri = media.GetStatic<AndroidJavaObject>("EXTERNAL_CONTENT_URI");

                using var values = new AndroidJavaObject("android.content.ContentValues");
                values.Call("put", "_display_name", fileName);
                values.Call("put", "mime_type", "image/png");
                if (scopedStorage)
                    values.Call("put", "relative_path", "Pictures/" + AlbumName);

                using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                using AndroidJavaObject resolver = activity.Call<AndroidJavaObject>("getContentResolver");
                using AndroidJavaObject uri = resolver.Call<AndroidJavaObject>("insert", externalUri, values);
                if (uri == null)
                {
                    Debug.LogWarning("Win photo not saved: MediaStore insert returned null.");
                    return;
                }

                using AndroidJavaObject stream = resolver.Call<AndroidJavaObject>("openOutputStream", uri);
                stream.Call("write", (object)ToSigned(pngBytes));
                stream.Call("flush");
                stream.Call("close");
                Debug.Log($"Win photo saved to the gallery as {fileName}.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Win photo could not be saved to the gallery: {ex.Message}");
            }
        }

        /// <summary>JNI marshals Java byte[] as sbyte[]; the reinterpret cast is free.</summary>
        private static sbyte[] ToSigned(byte[] bytes) => (sbyte[])(Array)bytes;
#endif
    }
}
