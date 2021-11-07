﻿using livelywpf.Core.API;
using livelywpf.Models;
using System.Collections.ObjectModel;

namespace livelywpf.Core
{
    public interface IDesktopCore
    {
        ReadOnlyCollection<IWallpaper> Wallpapers { get; }

        void CloseAllWallpapers(bool terminate = false);
        void CloseWallpaper(ILibraryModel wp, bool terminate = false);
        void CloseWallpaper(ILivelyScreen display, bool terminate = false);
        void CloseWallpaper(WallpaperType type, bool terminate = false);
        void ResetWallpaper();
        void RestoreWallpaper();
        void SeekWallpaper(ILibraryModel wp, float seek, PlaybackPosType type);
        void SeekWallpaper(ILivelyScreen display, float seek, PlaybackPosType type);
        void SendMessageWallpaper(ILibraryModel wp, IpcMessage msg);
        void SendMessageWallpaper(ILivelyScreen display, LibraryModel wp, IpcMessage msg);
        void SetWallpaper(ILibraryModel wallpaper, ILivelyScreen display);
        void ShutDown();
    }
}