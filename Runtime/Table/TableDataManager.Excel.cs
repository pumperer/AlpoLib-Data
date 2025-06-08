using System;
using System.Collections;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using alpoLib.Data.Serialization;
using ExcelDataReader;
using UnityEngine.Networking;

namespace alpoLib.Data
{
	public partial class TableDataManager
    {
        private class ColumnInfo
		{
			public DataAttributeParser.DataProperty DataProperty;
			
			public string ColumnAttrName => DataProperty.ColumnName;
			public PropertyInfo PropertyInfo => DataProperty.PropertyInfo;
		}
		
        public void Open_Excel(string excelFilePath, Action<bool> completeCallback = null)
        {
	        Open(excelFilePath, completeCallback);
        }
        
        private async void Open(string excelFilePath, Action<bool> completeCallback = null)
        {
	        await LoadTableDataFromExcel(excelFilePath);
	        completeCallback?.Invoke(true);
        }
        
		private async Task LoadTableDataFromExcel(string filepath)
		{
			//using var fs = new FileStream(filepath, FileMode.Open, FileAccess.Read);
			using var r = UnityWebRequest.Get(filepath);
			r.downloadHandler = new DownloadHandlerBuffer();
			
			r.SendWebRequest();
			while (!r.isDone)
				await Task.Yield();
			
			var bytes = r.downloadHandler.data;
			using var ms = new MemoryStream(bytes); 
			var ds = ExcelReaderFactory.CreateOpenXmlReader(ms).AsDataSet(new ExcelDataSetConfiguration
			{
				ConfigureDataTable = _=>new ExcelDataTableConfiguration
				{
					UseHeaderRow = true,
				}
			});

			foreach (DataTable ws in ds.Tables)
			{
				Debug.Log($"Load {ws.TableName} start.");
				if (!loaderInstanceMapBySheetName.TryGetValue(ws.TableName, out var loader))
					continue;

				if (loader is not IThreadedTableDataLoader threadedLoader)
					continue;

				var dataType = loader.GetType().BaseType.GenericTypeArguments[0];
				var dap = new DataAttributeParser(dataType);
				var dataList = new ArrayList();

				var dpList =
					dap.DataProperties.Select(dp => new ColumnInfo
					{ DataProperty = dp }).ToList();

				foreach (DataRow row in ws.Rows)
				{
					var ins = Activator.CreateInstance(dataType);
					foreach (var ci in dpList)
					{
						if (ci.DataProperty.CompoundTypeAttribute != null)
						{
							var prefix = ci.DataProperty.CompoundTypeAttribute.ColumnPrefix;
							var compIns = Activator.CreateInstance(ci.DataProperty.PropertyInfo.PropertyType);
							foreach (var prop in ci.DataProperty.CompoundProperties)
							{
								var cell = row[$"{prefix}{prop.ColumnName}"];
								var c = TypeDescriptor.GetConverter(prop.PropertyInfo.PropertyType);
								var value = c.ConvertFrom(cell.ToString());
								prop.PropertyInfo.SetValue(compIns, value);
							}
							ci.DataProperty.PropertyInfo.SetValue(ins, compIns);
						}
						else if (ci.DataProperty.CompoundListAttribute != null)
						{
							var listCount = ci.DataProperty.CompoundListAttribute.ListCount;
							var elementType = ci.DataProperty.PropertyInfo.PropertyType.GetElementType();
							var compList = Array.CreateInstance(elementType, listCount);
							for (var i = 0; i < listCount; i++)
							{
								var compIns = Activator.CreateInstance(elementType);
								foreach (var prop in ci.DataProperty.CompoundListElementProperties)
								{
									var cName = $"{prop.ColumnName}{i + 1}";
									var cell = row[cName];
									var c = TypeDescriptor.GetConverter(prop.PropertyInfo.PropertyType);
									var value = c.ConvertFrom(cell.ToString());
									prop.PropertyInfo.SetValue(compIns, value);
								}
								compList.SetValue(compIns, i);
							}
							ci.DataProperty.PropertyInfo.SetValue(ins, compList);
						}
						else if (ci.DataProperty.ListAttribute != null)
						{
							var listCount = ci.DataProperty.ListAttribute.ListCount;
							var elementType = ci.DataProperty.PropertyInfo.PropertyType.GetElementType();
							var compList = Array.CreateInstance(elementType, listCount);
							for (var i = 0; i < listCount; i++)
							{
								var cName = $"{ci.DataProperty.ColumnListName}{i + 1}";
								var cell = row[cName];
								var c = TypeDescriptor.GetConverter(elementType);
								var value = c.ConvertFrom(cell.ToString());
								compList.SetValue(value, i);
							}
							ci.DataProperty.PropertyInfo.SetValue(ins, compList);
						}
						else if (ci.DataProperty.ColumnAttribute != null)
						{
							var cell = row[ci.ColumnAttrName];
							var c = TypeDescriptor.GetConverter(ci.PropertyInfo.PropertyType);
							var value = c.ConvertFrom(cell.ToString());
							ci.PropertyInfo.SetValue(ins, value);
						}
					}
					dataList.Add(ins);
				}
				threadedLoader.PostProcess(dataList);
				await Task.Yield();
			}
		}
    }
}