using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using alpoLib.Core.FileSystem;
using alpoLib.Data.Serialization;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace alpoLib.Data
{
    public partial class TableDataManager
    {
        public static Dictionary<Type, ISerializerBase> FindSerializer()
        {
            var assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
            if (assembly == null)
                throw new Exception();

            var types = assembly.GetTypes().Where(t =>
                t.Namespace == "alpoLib.Data.Serialization.Generated" &&
                t.Name == "GeneratedSerializerFactory");
            var enumerable = types as Type[] ?? types.ToArray();
            if (enumerable.Length != 1)
                throw new Exception();

            var type = enumerable[0];

            var method = type.GetMethod("RegisterSerializers", BindingFlags.Static | BindingFlags.Public);
            if (method != null)
            {
                method.Invoke(null, null);
            }
            
            var field = type.GetField("ob", BindingFlags.Static | BindingFlags.NonPublic);
            if (field != null)
            {
                return field.GetValue(null) as Dictionary<Type, ISerializerBase>;
            }

            return null;
        }
        
        private static Type GetLoaderType(List<Type> loaderTypes, string sheetName)
        {
            var type = loaderTypes.Find(t =>
            {
                var attr = t.GetCustomAttribute<TableDataSheetNameAttribute>();
                return attr != null && attr.TableDataSheetName == sheetName;
            });
            return type;
        }
        
        public static void Sync(object o, string name)
        {
            var loaderTypes = FindTableDataLoaderTypes();
            var customSerializerTypes = FindCustomSerializerTypes();
            var serializerDic = FindSerializer();
            
            var dataToken = JToken.Parse(o.ToString());
            foreach (var token in dataToken)
            {
                var sheetName = name;
                if (token.Path != "DEFAULT")
                    sheetName = $"{name}_{token.Path}";
                var loaderType = GetLoaderType(loaderTypes, sheetName);
                var baseType = loaderType?.BaseType;
                if (baseType == null || baseType.GenericTypeArguments.Length != 1)
                {
                    if (customSerializerTypes.TryGetValue(sheetName, out var customSerializerType))
                    {
                        var customSerializer = Activator.CreateInstance(customSerializerType);
                        if (customSerializer is ICustomSerializer cs)
                        {
                            cs.Serialize(token);
                        }
                    }
                    continue;
                }

                var tableDataBaseType = baseType.GenericTypeArguments[0];
                if (serializerDic.TryGetValue(tableDataBaseType, out var s))
                {
                    List<object> dataList;
                    try
                    {
                        dataList = token.Values().Select(valueToken => s.JsonToObject(valueToken)).ToList();
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                        throw new Exception(
                            $"{sheetName} ({tableDataBaseType.Name}) 의 JsonToObject 중 오류가 발생했습니다.\n시트 컬럼이 데이터 정의와 맞지 않을 수 있습니다.");
                    }

                    var folderList = new List<string>();
                    if (Application.isPlaying)
                    {
                        var folder = PathHelper.GetLocalPathForRemoteFile("TableData");
                        folderList.Add(folder);
                    }
                    else
                    {
                        var saPath = PathHelper.GetStreamingAssetFilePath("TableData");
                        // var patchSetPath = PathHelper.GetPatchSetPath();
                        // folderList.Add(Path.Combine(patchSetPath, "Android", "TableData"));
                        // folderList.Add(Path.Combine(patchSetPath, "iOS", "TableData"));
                        folderList.Add(saPath);
                    }

                    foreach (var folder in folderList)
                    {
                        if (!Directory.Exists(folder))
                            Directory.CreateDirectory(folder);

                        var targetFileName = $"{name}_{token.Path}.d";
                        if (token.Path == "DEFAULT")
                            targetFileName = $"{name}.d";
                        var targetPath = Path.Combine(folder, targetFileName);
                        BinSerializer.SerializeListToFile(dataList, tableDataBaseType, targetPath);
                    }
                }
                else
                    throw new Exception($"{sheetName} ({tableDataBaseType.Name}) 의 시리얼라이저가 없습니다.\n제레네이트를 한번 하고 다시 해주세요.");
            }
        }
    }
}