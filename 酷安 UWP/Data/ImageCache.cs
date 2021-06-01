﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Xaml.Media.Imaging;

namespace CoolapkUWP.Data
{
    public enum ImageType
    {
        SmallImage = 0,
        OriginImage,
        SmallAvatar,
        BigAvatar,
        Icon
    }

    static class ImageCache
    {
        static BitmapImage whiteNoPicMode = new BitmapImage(new Uri("ms-appx:/Assets/img_placeholder.png")) { DecodePixelHeight = 100, DecodePixelWidth = 100 };
        static BitmapImage darkNoPicMode = new BitmapImage(new Uri("ms-appx:/Assets/img_placeholder_night.png")) { DecodePixelHeight = 100, DecodePixelWidth = 100 };
        static Dictionary<ImageType, StorageFolder> folders = new Dictionary<ImageType, StorageFolder>();
        internal static BitmapImage NoPic { get => Settings.GetBoolen("IsNoPicsMode") ? darkNoPicMode : whiteNoPicMode; }

        private static async Task<StorageFolder> GetFolder(ImageType type)
        {
            StorageFolder folder;
            if (folders.ContainsKey(type)) folder = folders[type];
            else
            {
                folder = await ApplicationData.Current.LocalCacheFolder.TryGetItemAsync(type.ToString()) as StorageFolder;
                if (folder is null) folder = await ApplicationData.Current.LocalCacheFolder.CreateFolderAsync(type.ToString(), CreationCollisionOption.OpenIfExists);
                if (!folders.ContainsKey(type)) folders.Add(type, folder);
            }
            return folder;
        }

        public static async Task<BitmapImage> GetImage(ImageType type, string url, bool showMessage = false)
        {
            if (string.IsNullOrEmpty(url)) return NoPic;
            else if (url.IndexOf("ms-appx") == 0) return new BitmapImage(new Uri(url));
            else
            {
                string fileName = Tools.GetMD5(url);
                StorageFolder folder = await GetFolder(type);
                var item = await folder.TryGetItemAsync(fileName);
                if (type == ImageType.SmallImage)
                    url += ".s.jpg";
                if (item is null)
                {
                    StorageFile file = await folder.CreateFileAsync(fileName, CreationCollisionOption.OpenIfExists);
                    try { await DownloadImage(file, url, showMessage); }
                    catch (FileLoadException) { return GetLocalImage(file.Path); }
                    return GetLocalImage(file.Path);
                }
                else if (item is StorageFile file) return GetLocalImage(file.Path);
                else return NoPic;
            }
        }

        public static async Task<StorageFile> GetImagePath(ImageType type, string url, bool showMessage = false)
        {
            if (url.IndexOf("ms-appx") == 0) return await StorageFile.GetFileFromApplicationUriAsync(new Uri(url));
            else if (string.IsNullOrEmpty(url) || Settings.GetBoolen("IsNoPicsMode"))
                return await StorageFile.GetFileFromApplicationUriAsync(new Uri(Settings.GetBoolen("IsDarkMode") ? "ms-appx:/Assets/img_placeholder_night.png" : "ms-appx:/Assets/img_placeholder.png"));
            else
            {
                string fileName = Tools.GetMD5(url);
                StorageFolder folder = await GetFolder(type);
                var item = await folder.TryGetItemAsync(fileName);
                if (type == ImageType.SmallImage)
                    url += ".s.jpg";
                if (item is null)
                {
                    StorageFile file = await folder.CreateFileAsync(fileName, CreationCollisionOption.OpenIfExists);
                    try { await DownloadImage(file, url, showMessage); }
                    catch (FileLoadException) { return file; }
                    return file;
                }
                else if (item is StorageFile file) return file;
                else return null;
            }
        }

        static BitmapImage GetLocalImage(string filename) =>
            (filename is null || Settings.GetBoolen("IsNoPicsMode")) ? (Settings.GetBoolen("IsDarkMode") ? darkNoPicMode : whiteNoPicMode)
                                                                     : new BitmapImage(new Uri(filename));

        static async Task DownloadImage(StorageFile file, string url, bool showMessage)
        {
            if (!Settings.GetBoolen("IsNoPicsMode"))
                try
                {
                    if (showMessage) Tools.ShowProgressBar();
                    using (Stream stream = await new HttpClient().GetStreamAsync(url))
                    using (Stream fs = await file.OpenStreamForWriteAsync())
                        await stream.CopyToAsync(fs);
                }
                catch (HttpRequestException ex) { Tools.ShowHttpExceptionMessage(ex); }
                finally { if (showMessage) Tools.HideProgressBar(); }
        }

        public static async Task<double> GetCacheSize(ImageType type, System.Threading.CancellationToken token)
        {
            ulong size = 0;
            StorageFolder folder = await GetFolder(type);
            int index = 0;
            var query = folder.CreateFileQuery();
            while (true)
            {
                var array = await query.GetFilesAsync((uint)index, 100);
                index += array.Count;
                if (array.Count > 0)
                    foreach (var item in array)
                    {
                        token.ThrowIfCancellationRequested();
                        size += (await item.GetBasicPropertiesAsync()).Size;
                    }
                else break;
            }
            return size;
        }

        public static async Task CleanCache(ImageType type)
        {
            Tools.ShowProgressBar();
            await (await GetFolder(type)).DeleteAsync();
            await ApplicationData.Current.LocalCacheFolder.CreateFolderAsync(type.ToString());
            Tools.HideProgressBar();
        }
    }
}
