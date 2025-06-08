using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace alpoLib.Data.Editor
{
    [CustomEditor(typeof(SheetsServiceProvider))]
    public class SheetServiceProviderInspector : UnityEditor.Editor
    {
        class Styles
        {
            public static readonly GUIContent loadAndSerialize = EditorGUIUtility.TrTextContent("Load & Serialize");
            public static readonly GUIContent authorize = EditorGUIUtility.TrTextContent("Authorize...", "Authorize the user. This is not required however the first time a connection to a Google sheet is required then authorization will be required.");
            public static readonly GUIContent authentication = EditorGUIUtility.TrTextContent("Authentication");
            public static readonly GUIContent cancel = EditorGUIUtility.TrTextContent("Cancel Authentication");
            public static readonly GUIContent clientId = EditorGUIUtility.TrTextContent("Client Id");
            public static readonly GUIContent clientSecret = EditorGUIUtility.TrTextContent("Client Secret");
            public static readonly GUIContent spreadSheetId = EditorGUIUtility.TrTextContent("SpreadSheet Id");
            public static readonly GUIContent noCredentials = EditorGUIUtility.TrTextContent("No Credentials Selected");
            public static readonly GUIContent loadCredentials = EditorGUIUtility.TrTextContent("Load Credentials...", "Load the credentials from a json file");
        }
        
        private SerializedProperty applicationName;
        private SerializedProperty clientId;
        private SerializedProperty clientSecret;
        private SerializedProperty spreadSheetId;
        private SerializedProperty serverType;
        
        private static Task<UserCredential> authorizeTask;
        private static CancellationTokenSource cancellationToken;
        private bool authOk = false;

        private void OnEnable()
        {
            clientId = serializedObject.FindProperty("clientId");
            clientSecret = serializedObject.FindProperty("clientSecret");
            applicationName = serializedObject.FindProperty("applicationName");
            spreadSheetId = serializedObject.FindProperty("spreadSheetId");
            serverType = serializedObject.FindProperty("serverType");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            
            EditorGUILayout.PropertyField(applicationName);
            
            EditorGUILayout.PropertyField(clientId);
            EditorGUILayout.PropertyField(clientSecret);
            EditorGUILayout.PropertyField(serverType);
            EditorGUILayout.PropertyField(spreadSheetId);
            
            if (GUILayout.Button(Styles.loadCredentials))
            {
                var file = EditorUtility.OpenFilePanel(Styles.loadCredentials.text, "", "json");
                if (!string.IsNullOrEmpty(file))
                {
                    var json = File.ReadAllText(file);
                    var secrets = SheetsServiceProvider.LoadSecrets(json);
                    clientId.stringValue = secrets.ClientId;
                    clientSecret.stringValue = secrets.ClientSecret;
                }
            }

            EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(clientId.stringValue) ||
                                         string.IsNullOrEmpty(clientSecret.stringValue));

            if (authorizeTask != null)
            {
                if (GUILayout.Button(Styles.cancel))
                {
                    cancellationToken.Cancel();
                }

                if (authorizeTask.IsCompleted)
                {
                    if (authorizeTask.Status == TaskStatus.RanToCompletion)
                    {
                        Debug.Log($"Authorized : {authorizeTask.Result.Token.IssuedUtc}", target);
                        authOk = true;
                    }
                    else if (authorizeTask.Exception != null)
                        Debug.Log(authorizeTask.Exception, target);
                    authorizeTask = null;
                    cancellationToken = null;
                }
            }
            else
            {
                if (authOk)
                {
                    if (GUILayout.Button(Styles.loadAndSerialize))
                    {
                        EditorUtility.DisplayProgressBar("Processing...", "Load sheet and serializing...", 0f);
                        var provider = target as SheetsServiceProvider;
                        var request = provider.Service.Spreadsheets.Get(provider.SpreadSheetId).Execute();
                        var sheetJsonObject = new JObject();
                        foreach (var sheet in request.Sheets)
                        {
                            var sheetName = sheet.Properties.Title;
                            if (sheetName.StartsWith('#'))
                                continue;
                            
                            var r = provider.Service.Spreadsheets.Values.Get(provider.SpreadSheetId,
                                sheetName);
                            var dataList = r.Execute().Values;
                            var ja = new JArray();
                            for (var i = 0; i < dataList.Count; i++)
                            {
                                if (i == 0)
                                    continue;
                                var row = dataList[i];
                                var jo = new JObject();
                                for (var j = 0; j < dataList[0].Count; j++)
                                {
                                    try
                                    {
                                        var column = dataList[0][j].ToString();
                                        var value = string.Empty;
                                        
                                        if (j<row.Count)
                                            value = row[j].ToString();
                                        
                                        jo.Add(column, value);
                                    }
                                    catch (Exception e)
                                    {
                                        Debug.LogException(e);
                                        var where = $"{sheetName} row[{i}] column[{j}]";
                                        EditorUtility.DisplayDialog("구글 시트 파싱 에러!", $"{where}\n{e.Message}", "뭐지?");
                                        EditorUtility.ClearProgressBar();
                                        return;
                                    }
                                }
                                ja.Add(jo);
                            }

                            sheetJsonObject.Add(sheetName, ja);
                        }

                        try
                        {
                            TableDataManager.Sync(sheetJsonObject, "DUMMY");
                        }
                        catch (Exception e)
                        {
                            Debug.LogException(e);
                            EditorUtility.DisplayDialog("에러에러!", e.Message, "젠장...");
                        }

                        EditorUtility.ClearProgressBar();
                    }
                }
                else
                {
                    if (GUILayout.Button(Styles.authorize))
                    {
                        var provider = target as SheetsServiceProvider;
                        cancellationToken = new CancellationTokenSource();
                        authorizeTask = provider.AuthorizeOAuthAsync(cancellationToken.Token);
                    }
                }
            }
            
            EditorGUI.EndDisabledGroup();
            
            serializedObject.ApplyModifiedProperties();
        }
    }
}