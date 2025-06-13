using System;
using alpoLib.Util;
using Newtonsoft.Json;
using UnityEngine;

namespace alpoLib.Data
{
    public class DataModuleInitParam
    {
        public IUserDataSaveProcess UserDataSave { get; set; }
        public JsonConverter JsonConverter { get; set; }
    }
    
    public static class Module
    {
        private static TableDataManager _tableDataManager;
        private static UserDataManager _userDataManager;
        
        public static void Initialize(DataModuleInitParam param)
        {
            ApplicationPauseListener.Init(true);
            UserDataSaveInterfaceHolder.Init(true);
            UserDataSaveInterfaceHolder.Instance.SetUserDataSaveInterface(param?.UserDataSave);

            _tableDataManager = new TableDataManager();
            DataManagerHolder.TableDataManager = _tableDataManager;
            
            _userDataManager = new UserDataManager(param?.JsonConverter);
            DataManagerHolder.UserDataManager = _userDataManager;
            _userDataManager.Initialize();
        }

        public static async Awaitable LoadTableDataAsync()
        {
            await _tableDataManager.Open_Bin_Async(TableDataManager.LocationType.StreamingAssets);
        }

        public static async Awaitable LoadUserDataAsync()
        {
            await _userDataManager.DeserializeAllAsync();
        }
    }
}