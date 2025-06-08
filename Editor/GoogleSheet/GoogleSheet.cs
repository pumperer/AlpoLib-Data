using System;
using System.Collections.Generic;
using Google.Apis.Requests;
using Google.Apis.Sheets.v4.Data;
using static Google.Apis.Sheets.v4.SpreadsheetsResource;

namespace alpoLib.Data.Editor
{
    public class GoogleSheet
    {
        public IGoogleSheetsService SheetsService { get; private set; }
        
        public string SpreadSheetId { get; set; }

        public GoogleSheet(IGoogleSheetsService provider)
        {
            if (provider == null)
                throw new ArgumentNullException(nameof(provider));
            
            SheetsService = provider;
        }

        public List<(string name, int id)> GetSheets()
        {
            var sheets = new List<(string name, int id)>();
            var spreadsheetInfoRequest = SheetsService.Service.Spreadsheets.Get(SpreadSheetId);
            var sheetInfoReq = ExecuteRequest<Spreadsheet, GetRequest>(spreadsheetInfoRequest);
            
            foreach (var sheet in sheetInfoReq.Sheets)
            {
                sheets.Add((sheet.Properties.Title, sheet.Properties.SheetId.Value));
            }

            return sheets;
        }
        
        internal protected virtual TResponse ExecuteRequest<TResponse, TClientServiceRequest>(TClientServiceRequest req) where TClientServiceRequest : ClientServiceRequest<TResponse> => req.Execute();
    }
}