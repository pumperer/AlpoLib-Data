using alpoLib.Util;
using UnityEngine;

namespace alpoLib.Data
{
    public interface IUserDataSaveProcess
    {
        void Save();
    }

    public class DefaultUserDataSaveProcess : DataManagerHolder, IUserDataSaveProcess
    {
        public virtual void Save()
        {
            if (UserDataManager == null)
                return;

            if (!UserDataManager.IsLoadComplete)
                return;
            
            if (UserDataManager.WillBeDestroy)
                return;
            
            UserDataManager.SerializeAll();
        }

        protected static bool CheckIsDirty(bool forceAllData = false)
        {
            var allLoaders = UserDataManager.GetAllLoaders();
            foreach (var l in allLoaders)
            {
                if (forceAllData)
                {
                    l.ShouldSyncToServer = true;
                }
                else
                {
                    if (l.ShouldSyncToServer)
                        return true;
                }
            }

            return forceAllData;
        }
    }
    
    internal class UserDataSaveInterfaceHolder : SingletonMonoBehaviour<UserDataSaveInterfaceHolder>
    {
        private IUserDataSaveProcess _processor;

        protected override void OnAwakeEvent()
        {
            ApplicationPauseListener.OnSaveEvent += OnSave;
        }

        protected override void OnDestroyEvent()
        {
            ApplicationPauseListener.OnSaveEvent -= OnSave;
        }

        private void OnSave()
        {
#if !UNITY_EDITOR
            if (!ApplicationPauseListener.InQuitProcess)
#endif
            {
                _processor?.Save();
            }
        }

        public void SetUserDataSaveInterface(IUserDataSaveProcess processor)
        {
            processor ??= new DefaultUserDataSaveProcess();
            _processor = processor;
        }
    }
}