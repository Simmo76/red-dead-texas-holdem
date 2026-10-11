#import <Foundation/Foundation.h>
#import <Photos/Photos.h>

// Saves the PNG at `path` into the user's photo library (add-only access),
// then deletes the temp file. Called from NativeGallerySaver.cs.
// Info.plist usage descriptions and the Photos framework link are added by
// Assets/Scripts/Editor/IosGalleryPostProcessor.cs at build time.

static void CinematicPoker_RemoveTempFile(NSURL *fileUrl)
{
    [[NSFileManager defaultManager] removeItemAtURL:fileUrl error:nil];
}

static void CinematicPoker_PerformSave(NSURL *fileUrl)
{
    [[PHPhotoLibrary sharedPhotoLibrary] performChanges:^{
        [PHAssetChangeRequest creationRequestForAssetFromImageAtFileURL:fileUrl];
    } completionHandler:^(BOOL success, NSError * _Nullable error) {
        if (!success)
            NSLog(@"[CinematicPoker] Could not save win photo: %@", error);
        CinematicPoker_RemoveTempFile(fileUrl);
    }];
}

void CinematicPoker_SaveImageToGallery(const char *path)
{
    if (path == NULL) return;
    NSURL *fileUrl = [NSURL fileURLWithPath:[NSString stringWithUTF8String:path]];

    if (@available(iOS 14, *)) {
        [PHPhotoLibrary requestAuthorizationForAccessLevel:PHAccessLevelAddOnly
                                                   handler:^(PHAuthorizationStatus status) {
            if (status == PHAuthorizationStatusAuthorized || status == PHAuthorizationStatusLimited) {
                CinematicPoker_PerformSave(fileUrl);
            } else {
                NSLog(@"[CinematicPoker] Win photo not saved: photo library access denied.");
                CinematicPoker_RemoveTempFile(fileUrl);
            }
        }];
    } else {
        [PHPhotoLibrary requestAuthorization:^(PHAuthorizationStatus status) {
            if (status == PHAuthorizationStatusAuthorized) {
                CinematicPoker_PerformSave(fileUrl);
            } else {
                NSLog(@"[CinematicPoker] Win photo not saved: photo library access denied.");
                CinematicPoker_RemoveTempFile(fileUrl);
            }
        }];
    }
}
