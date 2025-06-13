using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using alpoLib.Util;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace alpoLib.Data
{
	public interface IUserDataOpener
	{
		void DeserializeAll();
		void SerializeAll();
	}

	public interface IUserDataMapperBase
	{
	}

	public abstract record UserDataBase
	{
		public int Id { get; set; }
	}
	
	public abstract class UserDataManagerBase : DataManagerHolder
	{
		public abstract void OnCreateInstance();
		
		public virtual void CreateNewUser()
		{
		}

		public virtual void OnInitialize()
		{
		}

		public virtual void OnSyncFromServer(object serverData)
		{
		}

		public virtual object OnSyncToServer()
		{
			return null;
		}

		public virtual void CustomizeSerialize(JObject jObject)
		{
		}

		public virtual void OnSerialize()
		{
		}
		
		public virtual bool ShouldSyncToServer { get; set; }
	}
	
	public partial class UserDataManager
	{
		private JsonConverter jsonConverter = null;
		
		private readonly Dictionary<string, UserDataManagerBase> userManagerDic = new();
		private readonly Dictionary<Type, IUserDataMapperBase> userManagerDicByInterface = new();

		public bool WillBeDestroy { get; set; }
		
		public UserDataManager(JsonConverter jc)
		{
			jsonConverter = jc ?? new DefaultJsonSerializer();
			FindUserDataLoaders();
		}

		public void Initialize()
		{
			foreach (var userDataManagerBase in userManagerDic.Values)
			{
				userDataManagerBase.OnCreateInstance();
			}
			
			// foreach (var p in userManagerDic)
			// {
			// 	p.Value.OnInitialize();
			// }
		}

		private void FindUserDataLoaders()
		{
			var assembly = TypeHelper.GameAssembly;
			if (assembly == null)
				return;

			var baseTypeInterface = typeof(IUserDataMapperBase);
			var baseTypeClass = typeof(UserDataManagerBase);
			var loaderTypes = assembly.GetTypes().Where(t =>
			baseTypeInterface.IsAssignableFrom(t) && !t.IsEquivalentTo(baseTypeInterface) &&
			baseTypeClass.IsAssignableFrom(t) && !t.IsEquivalentTo(baseTypeClass) &&
			t.IsClass &&
			!t.IsGenericType
			);

			foreach (var loaderType in loaderTypes)
			{
				Debug.Log($"Found UserData loader : {loaderType.Name}");
				var instance = Activator.CreateInstance(loaderType);
				var l = instance as IUserDataMapperBase;
				var c = instance as UserDataManagerBase;
				AddLoader(c, l);
			}
		}

		private void AddLoader(UserDataManagerBase classBase, IUserDataMapperBase interfaceBase)
		{
			var loaderType = interfaceBase.GetType();
			var interfaces = loaderType.FindInterfaces(new TypeFilter(CheckInterfaceFilter), typeof(IUserDataMapperBase));
			foreach (var i in interfaces)
			{
				userManagerDicByInterface.Add(i, interfaceBase);
			}

			var key = loaderType.Name.Replace("Manager", "");
			userManagerDic.Add(key, classBase);
		}

		private static bool CheckInterfaceFilter(Type type, Object criteriaObject)
		{
			var targetType = criteriaObject as Type;
			return !type.IsEquivalentTo(targetType) && targetType.IsAssignableFrom(type);
		}

		public IEnumerable<UserDataManagerBase> GetAllLoaders()
		{
			return userManagerDic.Values;
		}

		public T GetLoader<T>() where T : IUserDataMapperBase
		{
			userManagerDicByInterface.TryGetValue(typeof(T), out var loader);
			return (T)loader;
		}
	}
}