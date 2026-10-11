#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

namespace CinematicPoker.Editor
{
    /// <summary>
    /// Xcode project fixes for the win-photo gallery feature: photo-library
    /// usage descriptions in Info.plist and the Photos framework link that
    /// Assets/Plugins/iOS/CinematicPokerGallery.m needs.
    /// </summary>
    public static class IosGalleryPostProcessor
    {
        private const string UsageDescription =
            "Cinematic Poker saves a photo of your winning moments to your gallery.";

        [PostProcessBuild]
        public static void OnPostProcessBuild(BuildTarget target, string buildPath)
        {
            if (target != BuildTarget.iOS) return;

            string plistPath = Path.Combine(buildPath, "Info.plist");
            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            plist.root.SetString("NSPhotoLibraryAddUsageDescription", UsageDescription);
            // Pre-iOS 14 devices go through the full-access authorization prompt.
            plist.root.SetString("NSPhotoLibraryUsageDescription", UsageDescription);
            plist.WriteToFile(plistPath);

            string projPath = PBXProject.GetPBXProjectPath(buildPath);
            var proj = new PBXProject();
            proj.ReadFromFile(projPath);
            proj.AddFrameworkToProject(proj.GetUnityFrameworkTargetGuid(), "Photos.framework", false);
            proj.WriteToFile(projPath);
        }
    }
}
#endif
