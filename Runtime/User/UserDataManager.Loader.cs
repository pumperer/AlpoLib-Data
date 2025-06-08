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

        public void DeserializeAll()
        {
            var state = GameStateManager.Instance.GetState<TemporaryDataState>();
            var s = state.D;
            if (string.IsNullOrEmpty(s))
            {
                foreach (var (_, mapper) in userManagerDic)
                {
                    mapper.CreateNewUser();
                }
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
            }
        }

        private void Deserialize(UserDataManagerBase manager, JToken token, JsonSerializer serializer)
        {
            var type = manager.GetType();
            var fields = type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            var serializeFields = fields.Where(fi => fi.GetCustomAttribute<SerializeField>() != null);

            foreach (var f in serializeFields)
            {
                object obj = null;
                var fieldToken = token[f.Name];
                obj = fieldToken == null ? Activator.CreateInstance(f.FieldType) : fieldToken.ToObject(f.FieldType, serializer);
                f.SetValue(manager, obj);
            }
        }
    }
}