using System;
using System.Collections.Generic;
using System.IO;
using alpoLib.Core.FileSystem;
using UnityEngine;
using UnityEngine.Networking;
using System.Threading.Tasks;

namespace alpoLib.Data
{
    public partial class TableDataManager : ITableDataOpener
	{
        public enum LocationType
        {
            StreamingAssets,
            LocalCache,
        }
        
        private int loadCount;
        private float currentAmount = 0f;
        private Action<float> loadAmountAccessor = null;
        private readonly List<Task> tasks = new();
        public bool IsOpen { private set; get; }
        
        private float LoadAmount => loadCount + currentAmount;

        public async Task Open_Bin_Async(LocationType locationType)
        {
            await Open(locationType, false, null);
        }
        
        public void Open_Bin(LocationType locationType, Action<bool> completeCallback = null)
        {
            _ = Open(locationType, false, completeCallback);
        }

        public void Open_Bin_Preloadable(TableDataManager.LocationType locationType,
            Action<bool> completeCallback = null)
        {
            _ = Open(locationType, true, completeCallback);
        }

        private async Task Open(LocationType location, bool onlyPreloadable, Action<bool> completeCallback = null)
        {
            Debug.Log($"Start load table data from {location}.");
            await LoadTableData(location, onlyPreloadable);
            Debug.Log($"End load table data from {location}.");
            completeCallback?.Invoke(true);
        }
        
        private async Task LoadTableData(LocationType location, bool onlyPreloadable)
        {
            var loaderList = loaderInstanceMapBySheetName;
            if (onlyPreloadable)
                loaderList = loaderInstanceMapOnlyPreloadable;
            
            foreach (var (sheetName, value) in loaderList)
            {
                var relativePath = $"TableData/{sheetName}.d";
                switch (location)
                {
                    case LocationType.StreamingAssets:
                    {
                        var path = PathHelper.GetStreamingAssetFilePath(relativePath);
                        if (Application.isEditor)
                        {
                            tasks.Add(ThreadedLoadOneSheet(sheetName, path, value));
                        }
                        else
                        {
                            var lu = new LocalUri(path);
                            using var r = UnityWebRequest.Get(lu);
                            r.downloadHandler = new DownloadHandlerBuffer();
                            await r.SendWebRequest();
                            var loadedBytes = r.downloadHandler.data;
                            if (loadedBytes == null)
                            {
                                Debug.LogWarning($"TableData {sheetName} loadedBytes is NULL!");
                                continue;
                            }

                            if (value is not IThreadedTableDataLoader loader)
                                continue;

                            loader.StartLoadingAsync(loadedBytes);
                            tasks.Add(loader.CompleteLoading(LoadAmountUpdater));
                        }
                    }
                        break;

                    case LocationType.LocalCache:
                    {
                        var path = PathHelper.GetLocalPathForRemoteFile(relativePath);
                        var task = ThreadedLoadOneSheet(sheetName, path, value);
                        tasks.Add(task);
                    }
                        break;
                }
            }

            await Task.WhenAll(tasks);
            tasks.Clear();

            foreach (var (_, value) in loaderList)
            {
                if (value is not IThreadedTableDataLoader loader)
                    continue;
                loader.PostProcess();
            }
            
            IsOpen = true;
        }
        
        private Task ThreadedLoadOneSheet(string sheetName, string path, ITableDataMapperBase mapper)
        {
            var task = Task.Run(() =>
            {
                LoadOneSheet(sheetName, path, mapper);
            });
            return task;
        }

        private void LoadOneSheet(string sheetName, string path, ITableDataMapperBase mapper)
        {
            var lu = new LocalUri(path);
            var loadedBytes = File.ReadAllBytes(lu.LocalPath);
            if (loadedBytes.Length == 0)
            {
                Debug.LogWarning($"TableData {sheetName} loadedBytes is NULL!");
                return;
            }

            if (mapper is not IThreadedTableDataLoader loader)
                return;

            loader.StartLoadingAsync(loadedBytes);
            var t = loader.CompleteLoading(LoadAmountUpdater);
            t.Wait();
            Debug.Log($"TableData {sheetName} loadedBytes from {path}.");
        }

        private void LoadAmountUpdater(float v)
        {
            currentAmount = v;
            loadAmountAccessor?.Invoke(LoadAmount);
        }
    }
}
