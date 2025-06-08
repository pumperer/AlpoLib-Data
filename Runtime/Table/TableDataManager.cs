using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace alpoLib.Data
{
	public interface ITableDataOpener
	{
		void Open_Excel(string excelFilePath, Action<bool> completeCallback = null);
		void Open_Bin(TableDataManager.LocationType locationType, Action<bool> completeCallback = null);
		void Open_Bin_Preloadable(TableDataManager.LocationType locationType, Action<bool> completeCallback = null);
		List<string> GetAllTableDataSheetNames();
	}

	public partial class TableDataManager
    {
        private Dictionary<string, ITableDataMapperBase> loaderInstanceMapBySheetName = new();
        private Dictionary<string, ITableDataMapperBase> loaderInstanceMapOnlyPreloadable = new();
		private Dictionary<Type, ITableDataMapperBase> loaderInstanceMapByInterface = new();

		public TableDataManager()
		{
			FindTableDataLoaders();
		}

		public static List<Type> FindTableDataLoaderTypes()
		{
			var assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
			if (assembly == null)
				return new List<Type>();

			var baseType = typeof(IThreadedTableDataLoader);
			var loaderTypes = assembly.GetTypes().Where(t =>
				baseType.IsAssignableFrom(t) &&
				t.IsClass &&
				t.BaseType.IsGenericType &&
				!t.IsGenericType &&
				t.BaseType.GetGenericTypeDefinition() == typeof(ThreadedTableDataLoader<>) &&
				!t.IsEquivalentTo(baseType)
			);
			return loaderTypes.ToList();
		}

		public static Dictionary<string, Type> FindCustomSerializerTypes()
		{
			var assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
			if (assembly == null)
				return new Dictionary<string, Type>();

			var baseType = typeof(ICustomSerializer);
			var result = (from t in assembly.GetTypes()
				where baseType.IsAssignableFrom(t) && t.IsClass && !t.IsEquivalentTo(baseType) &&
				      t.GetCustomAttribute<CustomSerializerAttribute>() != null
				select t).ToList();
			if (result.Count == 0)
				return new Dictionary<string, Type>();
			return result.ToDictionary(t => t.GetCustomAttribute<CustomSerializerAttribute>().TableDataSheetName,
				t => t);
		}

		private void FindTableDataLoaders()
		{
			var loaderTypes = FindTableDataLoaderTypes();
			foreach (var loaderType in loaderTypes)
			{
				Debug.Log($"Found TableData loader : {loaderType.Name}");
				var l = Activator.CreateInstance(loaderType) as ITableDataMapperBase;
				AddLoader(l);
			}
		}

		public void AddLoaders(params ITableDataMapperBase[] loaders)
		{
			foreach (var loader in loaders)
				AddLoader(loader);
		}

        private void AddLoader(ITableDataMapperBase loader)
        {
			if (loader == null)
				return;

			var sheetName = string.Empty;
			var loaderType = loader.GetType();
			var interfaces = loaderType.FindInterfaces(new TypeFilter(CheckInterfaceFilter), typeof(ITableDataMapperBase));
			if (interfaces.Length != 1)
				throw new Exception($"Too many ITableDataMapperBase in {loaderType.Name}! Should be ONE!");
			var targetInterface = interfaces[0];
			
	        var sheetNameAttr = loaderType.GetCustomAttribute<TableDataSheetNameAttribute>();
	        var isPreloadable = false;
	        if (sheetNameAttr != null)
	        {
		        // if (!sheetNameAttr.TableDataSheetName.StartsWith("spec_"))
			       //  sheetName = $"spec_{sheetNameAttr.TableDataSheetName}";
		        // else
			    sheetName = sheetNameAttr.TableDataSheetName;
		        isPreloadable = sheetNameAttr.IsPreloadable;
	        }

	        if (string.IsNullOrEmpty(sheetName))
	        {
		        sheetName = loaderType.Name;
	        }

			loaderInstanceMapByInterface.Add(targetInterface, loader);
			loaderInstanceMapBySheetName.Add(sheetName, loader);
			if (isPreloadable)
				loaderInstanceMapOnlyPreloadable.Add(sheetName, loader);
        }

		private static bool CheckInterfaceFilter(Type type, Object criteriaObject)
		{
			var targetType = criteriaObject as Type;
			return !type.IsEquivalentTo(targetType) && targetType.IsAssignableFrom(type);
		}

        public ITableDataMapperBase GetLoader(string tableName)
        {
	        loaderInstanceMapBySheetName.TryGetValue(tableName, out var loader);
	        return loader;
        }

		public T GetLoader<T>() where T : ITableDataMapperBase
		{
			loaderInstanceMapByInterface.TryGetValue(typeof(T), out var loader);
			return (T)loader;
		}

		public List<string> GetAllTableDataSheetNames()
		{
			return loaderInstanceMapBySheetName.Keys.ToList();
		}
	}
}