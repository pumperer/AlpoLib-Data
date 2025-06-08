using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using ExcelDataReader;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace alpoLib.Data.Editor
{
    public class ExcelSheet : ScriptableObject
    {
        [MenuItem("Assets/Create/AlpoLib/Data/Table/Excel Sheet")]
        public static void CreateInstance()
        {
            var directory = AssetPathHelper.GetNearestFolderPathFromSelection();
            var targetFilePath = Path.Combine(directory, "Serializer From Excel Sheet.asset");
            Debug.Log(targetFilePath);
            
            var ins = CreateInstance<ExcelSheet>();
            AssetDatabase.CreateAsset(ins, targetFilePath);
            AssetDatabase.SetLabels(ins, new[] { "ExcelSheetProvider" });
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            AssetDatabase.ImportAsset(targetFilePath);
            Selection.activeObject = ins;
        }
        
        [MenuItem("AlpoLib/Data/Show TableData Serializer from ExcelSheet")]
        public static void ShowInstance()
        {
            var assetPaths = AssetDatabase.FindAssets("l:ExcelSheetProvider");
            if (assetPaths.Length == 0)
                return;

            var path = AssetDatabase.GUIDToAssetPath(assetPaths[0]);
            var asset = AssetDatabase.LoadAssetAtPath<ExcelSheet>(path);
            Selection.activeObject = asset;
        }
        
        [Header("엑셀 파일들이 들어있는 폴더를 넣어주세요!")]
        [SerializeField]
        private Object excelFolderObject;

        public string ExcelFolderPath =>
            !excelFolderObject ? string.Empty : AssetDatabase.GetAssetPath(excelFolderObject);
    }

    [CustomEditor(typeof(ExcelSheet))]
    public class ExcelSheetInspector : UnityEditor.Editor
    {
        private SerializedProperty excelFileObjectProp;

        private void Awake()
        {
            excelFileObjectProp ??= serializedObject.FindProperty("excelFolderObject");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(excelFileObjectProp);

            if (GUILayout.Button("Serialize"))
            {
                if (target is ExcelSheet t)
                {
                    var serializer = new SerializerFromExcel(t.ExcelFolderPath);
                    serializer.SerializeAll();
                }
            }
            
            serializedObject.ApplyModifiedProperties();
        }
    }

    public class SerializerFromExcel
    {
        private readonly List<DataSet> dataSetList = new();
        
        public SerializerFromExcel(string folderPath)
        {
            var excelFiles = Directory.GetFiles(folderPath, "*.xlsx", SearchOption.AllDirectories);
            foreach (var excelPath in excelFiles)
            {
                using var fs = new FileStream(excelPath, FileMode.Open, FileAccess.Read);
                var ds = ExcelReaderFactory.CreateOpenXmlReader(fs).AsDataSet(new ExcelDataSetConfiguration
                {
                    ConfigureDataTable = _=>new ExcelDataTableConfiguration
                    {
                        UseHeaderRow = true,
                    }
                });
                var key = Path.GetFileNameWithoutExtension(excelPath);
                ds.DataSetName = key;
                dataSetList.Add(ds);
            }
        }

        public void SerializeAll()
        {
            foreach (var ds in dataSetList)
                SerializeOne(ds);
        }
        
        private static void SerializeOne(DataSet ds)
        {
            var sheetJsonObject = new JObject();
            foreach (DataTable ws in ds.Tables)
            {
                var sheetName = ws.TableName;
                if (sheetName.StartsWith('#'))
                    continue;
                
                Debug.Log($"Serialize {sheetName} start.");
                var ja = new JArray();
                for (var i = 0; i < ws.Rows.Count; i++)
                {
                    var row = ws.Rows[i].ItemArray.ToList();
                    var jo = new JObject();
                    for (var j = 0; j < row.Count; j++)
                    {
                        try
                        {
                            if (ws.Columns.Count <= j)
                                continue;
                            var column = ws.Columns[j].ToString();
                            var value = row[j].ToString();
                            jo.Add(column, value);
                        }
                        catch (Exception e)
                        {
                            var where = $"{ds.DataSetName}:{sheetName} row[{i}] column[{j}]";
                            EditorUtility.DisplayDialog("엑셀 시트 파싱 에러!", $"{where}\n{e.Message}", "뭐지?");
                            return;
                        }
                    }
                    ja.Add(jo);
                }
                sheetJsonObject.Add(sheetName, ja);
            }
            
            try
            {
                TableDataManager.Sync(sheetJsonObject, ds.DataSetName);
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("에러에러!", e.Message, "젠장...");
                Debug.LogException(e);
            }
        }
    }
}