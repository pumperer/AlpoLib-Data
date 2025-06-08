using System;

namespace alpoLib.Data
{
	public abstract class ParamBase
	{
	}

	public abstract class InitDataBase
	{
	}

	public abstract class DataManagerHolder
	{
		public static TableDataManager TableDataManager { protected get; set; }
		public static UserDataManager UserDataManager { protected get; set; }
	}

	public abstract class LoadingBlockBase<TInitData> : DataManagerHolder
		where TInitData : InitDataBase
	{
		public abstract TInitData MakeInitData();
	}

	public abstract class LoadingBlockBase<TParam, TInitData>
		: DataManagerHolder
		where TParam : ParamBase
		where TInitData : InitDataBase
	{
		public abstract TInitData MakeInitData(TParam param);
	}

	public class LoadingBlockDefinitionAttribute : Attribute
	{
		public Type LoadingBlock { get; }

		public LoadingBlockDefinitionAttribute(Type loadingBlockType)
		{
			LoadingBlock = loadingBlockType;
		}
	}
}