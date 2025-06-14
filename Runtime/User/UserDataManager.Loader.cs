using System;
using System.Linq;
using System.Reflection;
using alpoLib.Util;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace alpoLib.Data
{
    internal class TemporaryDataState : PersistentGameState
    {
        public string D = string.Empty;
    }
    
    public partial class UserDataManager : IUserDataOpener
	{
        public bool IsLoadComplete { get; private set; }
        
        public void SerializeAll()
        {
            if (WillBeDestroy)
                return;
            
            var jo = new JObject();
            var serializer = new JsonSerializer();
            serializer.Converters.Add(jsonConverter);
            foreach (var (key, instance) in userManagerDic)
            {
                instance.OnSerialize();
                var result = Serialize(instance, serializer);
                instance.CustomizeSerialize(result);
                jo.Add(key, result);
            }

            var state = GameStateManager.Instance.GetState<TemporaryDataState>();
            state.D = jo.ToString(Formatting.None);
            GameStateManager.Instance.Save();
        }
        
        private static JObject Serialize(UserDataManagerBase manager, JsonSerializer serializer)
        {
            var type = manager.GetType();
            var fields = type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            var serializeFields = fields.Where(fi => fi.GetCustomAttribute<SerializeField>() != null);

            var json = new JObject();
            foreach (var f in serializeFields)
            {
                var value = f.GetValue(manager);
                if (value == null)
                {
                    if (f.FieldType.IsArray)
                        json.Add(f.Name, JToken.Parse("[]"));
                    else
                        json.Add(f.Name, JToken.Parse("{}"));
                    continue;
                }
                
                var jt = JToken.FromObject(value, serializer);
                json.Add(f.Name, jt);
            }
            return json;
        }

        public async Awaitable DeserializeAllAsync()
        {
            var state = GameStateManager.Instance.GetState<TemporaryDataState>();
            var s = state.D;
            if (string.IsNullOrEmpty(s))
            {
                Debug.Log("No user data found, creating new user data.");
                foreach (var (_, mapper) in userManagerDic)
                {
                    mapper.CreateNewUser();
                    mapper.OnInitialize();
                    await Awaitable.NextFrameAsync();
                }

                IsLoadComplete = true;
                return;
            }

            var jo = JObject.Parse(s);
            var serializer = new JsonSerializer();
            serializer.Converters.Add(jsonConverter);
            
            foreach (var (key, value) in jo)
            {
                if (!userManagerDic.TryGetValue(key, out var manager))
                    continue;
                if (value == null)
                    continue;

                try
                {
                    Deserialize(manager, value, serializer);
                    manager.OnInitialize();
                }
                catch (Exception e)
                {
                    Debug.LogError(e.Message);
                }
                
                await Awaitable.NextFrameAsync();
            }
            IsLoadComplete = true;
        }

        private void Deserialize(UserDataManagerBase manager, JToken token, JsonSerializer serializer)
        {
            var type = manager.GetType();
            var fields = type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            var serializeFields = fields.Where(fi => fi.GetCustomAttribute<SerializeField>() != null);

            Debug.Log($"Deserialize {type.Name} : {token}");
            
            foreach (var f in serializeFields)
            {
                object obj = null;
                var fieldToken = token[f.Name];
                if (fieldToken == null)
                {
                    Debug.LogWarning($"Field token is null : {f.Name}");
                    obj = Activator.CreateInstance(f.FieldType);
                }
                else
                    obj = fieldToken.ToObject(f.FieldType, serializer);
                f.SetValue(manager, obj);
            }
        }
    }
}