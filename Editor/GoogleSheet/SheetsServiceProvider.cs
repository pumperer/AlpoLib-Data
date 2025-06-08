using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Util.Store;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;

namespace alpoLib.Data.Editor
{
    public interface IGoogleSheetsService
    {
        SheetsService Service { get; }
    }
    
    public class SheetsServiceProvider : ScriptableObject, IGoogleSheetsService
    {
        [MenuItem("Assets/Create/AlpoLib/Data/Table/Google Sheet")]
        public static void CreateInstance()
        {
            var directory = AssetPathHelper.GetNearestFolderPathFromSelection();
            var targetPath = Path.Combine(directory, "Google Sheet Service.asset");

            var ins = CreateInstance<SheetsServiceProvider>();
            AssetDatabase.CreateAsset(ins, targetPath);
            AssetDatabase.SetLabels(ins, new[] { "SheetServiceProvider" });
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            AssetDatabase.ImportAsset(targetPath);
            Selection.activeObject = ins;
        }

        [MenuItem("AlpoLib/Data/Show TableData Serializer from GoogleSheet")]
        public static void ShowInstance()
        {
            var assetPaths = AssetDatabase.FindAssets("l:SheetServiceProvider");
            if (assetPaths.Length == 0)
                return;

            var path = AssetDatabase.GUIDToAssetPath(assetPaths[0]);
            var asset = AssetDatabase.LoadAssetAtPath<SheetsServiceProvider>(path);
            Selection.activeObject = asset;
        }
        
        [SerializeField]
        private string applicationName;
        
        [SerializeField]
        private string clientId;

        [SerializeField]
        private string clientSecret;

        [SerializeField]
        private string spreadSheetId;

        [SerializeField]
        private string serverType;
        
        private SheetsService sheetsService;
        
        static readonly string[] scopes = { SheetsService.Scope.SpreadsheetsReadonly };
        
        public IDataStore DataStore { get; set; }

        public string ClientId => clientId;
        
        public string ClientSecret => clientSecret;

        public string SpreadSheetId => spreadSheetId;

        public string ApplicationName
        {
            get => applicationName;
            set => applicationName = value;
        }
        
        public virtual SheetsService Service => sheetsService ??= Connect();

        private SheetsService Connect()
        {
            return ConnectWithOAuth2();
        }

        private UserCredential AuthorizeOAuth()
        {
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            var connectTask = AuthorizeOAuthAsync(cts.Token);
            if (!connectTask.IsCompleted)
                connectTask.RunSynchronously();

            if (connectTask.Status == TaskStatus.Faulted)
            {
                throw new Exception($"Failed to connect to Google Sheets.\n{connectTask.Exception}");
            }
            return connectTask.Result;
        }
        
        public Task<UserCredential> AuthorizeOAuthAsync(CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(ClientSecret))
                throw new Exception($"{nameof(ClientSecret)} is empty");

            if (string.IsNullOrEmpty(ClientId))
                throw new Exception($"{nameof(ClientId)} is empty");

            // We create a separate area for each so that multiple providers don't clash.
            var dataStore = DataStore ?? new FileDataStore($"Library/Google/{name}", true);

            var secrets = new ClientSecrets { ClientId = clientId, ClientSecret = clientSecret };

            // We use the client Id for the user so that we can generate a unique token file and prevent conflicts when using multiple OAuth authentications. (LOC-188)
            var user = clientId;
            var connectTask = GoogleWebAuthorizationBroker.AuthorizeAsync(secrets, scopes, user, cancellationToken, dataStore);
            return connectTask;
        }
        
        private SheetsService ConnectWithOAuth2()
        {
            var userCredentials = AuthorizeOAuth();
            var sheetsService = new SheetsService(new BaseClientService.Initializer
            {
                HttpClientInitializer = userCredentials,
                ApplicationName = ApplicationName,
            });
            return sheetsService;
        }

        internal static ClientSecrets LoadSecrets(string credentials)
        {
            if (string.IsNullOrEmpty(credentials))
                throw new ArgumentException(nameof(credentials));

            using var stream = new MemoryStream(System.Text.Encoding.ASCII.GetBytes(credentials));
            var gcs = GoogleClientSecrets.FromStream(stream);
            return gcs.Secrets;
        }
    }
}