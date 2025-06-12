#define ENABLE_THREAD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using alpoLib.Core.Foundation;
using alpoLib.Core.Serialization;
using alpoLib.Data.Serialization;
using Newtonsoft.Json.Linq;
#if ENABLE_THREAD
using System.Threading;
#endif

namespace alpoLib.Data
{
    public class CustomSerializerAttribute : Attribute
    {
        public string TableDataSheetName { get; }

        public CustomSerializerAttribute(string sheetName)
        {
            TableDataSheetName = sheetName;
        }
    }
    
	public class TableDataSheetNameAttribute : Attribute
    {
        public string TableDataSheetName { get; private set; }
        public bool IsPreloadable { get; private set; }
        
        public TableDataSheetNameAttribute(string sheetName)
        {
            TableDataSheetName = sheetName;
        }

        public TableDataSheetNameAttribute(string sheetName, bool isPreloadable)
        {
            TableDataSheetName = sheetName;
            IsPreloadable = isPreloadable;
        }
    }

    public abstract record TableDataBase
    {
        [DataColumn("Id")]
        public int Id { get; set; }
        
        [DataColumn("IsActive")]
        public CustomBoolean IsActive { get; set; }
    }

    public interface ICustomSerializer
    {
        void Serialize(JToken token);
    }

    public interface IThreadedTableDataLoader
    {
        void StartLoadingAsync(byte[] bytes);
        Task CompleteLoading(Action<float> progressUpdater = null);
		void PostProcess(ArrayList loadedElementList);
        void PostProcess();
		bool IsComplete { get; }
    }

    public interface ITableDataMapperBase
    {
    }

    public interface ITableDataServerSync
    {
        void Sync(object o);
    }

    public interface ITableDataServerSync<TData> : ITableDataServerSync where TData : TableDataBase
    {
        void Sync(List<TData> list);

        void ITableDataServerSync.Sync(object o)
        {
            if (o == null)
                return;

            var l = o switch
            {
                ArrayList al => al.Cast<TData>().ToList(),
                IEnumerable<TData> enumerable => enumerable.ToList(),
                _ => null
            };

            Sync(l);
        }
    }
    
    public abstract class ThreadedTableDataLoader<T> : DataManagerHolder, IThreadedTableDataLoader where T : TableDataBase, new()
    {
#if ENABLE_THREAD
        private Thread thread = null;
        private int interlockedThreadRunningState = 0;
#endif
        private IList<T> loadedElementList;

        private Exception savedException = null;
        private byte[] buffer = null;

        public float LoadProgress { get; private set; } = 0f;

        public bool IsComplete { get; private set; } = false;

        private string tableName;

        protected string TableName
        {
            get
            {
                if (!string.IsNullOrEmpty(tableName))
                    return tableName;
                
                var attr = GetType().GetCustomAttribute<TableDataSheetNameAttribute>();
                if (attr != null)
                    tableName = attr.TableDataSheetName;

                return tableName;
            }
        }
        
        private void DeserializeThreaded(object param)
        {
            buffer = (byte[])param;

            try
            {
                LoadProgress = 0;
                var bs = new BufferStream(buffer, 0, buffer.Length);
                loadedElementList = BinSerializer.DeserializeList<T>(bs);
            }
            catch (Exception e)
            {
                savedException = e;
                loadedElementList = null;
            }
            finally
            {
#if ENABLE_THREAD
                Interlocked.Exchange(ref interlockedThreadRunningState, 0);
#endif
                LoadProgress = 1;
            }
        }

        // protected void SerializeToLocalCache(IList<T> dataList)
        // {
        //     var folder = PathHelper.GetLocalPathForRemoteFile("TableData");
        //     var targetPath = Path.Combine(folder, $"{TableName}.b");
        //     BinSerializer.SerializeListToFile(dataList, targetPath);
        // }
        
        public void PostProcess(ArrayList loadedElementList)
        {
            PostProcess(loadedElementList.Cast<T>());
		}

        protected abstract void PostProcess(IEnumerable<T> loadedElementList);

        public virtual void PostProcess()
        {
            PostProcess(loadedElementList);

            loadedElementList = null;
            IsComplete = true;
        }

        public void StartLoadingAsync(byte[] bytes)
        {
#if ENABLE_THREAD
            if (thread is { IsAlive: true })
                return;

            thread = new Thread(new ParameterizedThreadStart(DeserializeThreaded));
            Interlocked.Exchange(ref interlockedThreadRunningState, 1);
            thread.Start(bytes);
#else
            DeserializeThreaded(bytes);
#endif
        }

        public async Task CompleteLoading(Action<float> progressUpdater = null)
        {
#if ENABLE_THREAD
            if (thread == null)
                return;

            int copiedRunningState;
            do
            {
                copiedRunningState = Interlocked.CompareExchange(ref interlockedThreadRunningState, 0, 0);
                progressUpdater?.Invoke(LoadProgress);
                await Task.Yield();
            } while (copiedRunningState != 0);

            thread.Join();
            thread = null;
#endif

            if (savedException != null)
            {
                Debug.LogException(savedException);
                Debug.LogWarning(savedException.StackTrace);
                throw savedException;
            }
            
            LoadProgress = 1.0f;
            progressUpdater?.Invoke(LoadProgress);

            Debug.Log($"Loaded {GetType().Name} Count : {loadedElementList.Count}");
            
#if !ENABLE_THREAD
            return;
#endif
        }
    }
}