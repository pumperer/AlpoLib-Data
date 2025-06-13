using UnityEngine;

namespace alpoLib.Data.Composition
{
    public abstract record CompositionDataBase
    {
    }

    public abstract record CompositionDataBase<TBase, TUser> : CompositionDataBase
        where TBase : TableDataBase
        where TUser : UserDataBase
    {
        protected TBase BaseData { get; }
        protected TUser UserData { get; }
        
        protected CompositionDataBase(TBase baseData, TUser userData)
        {
            BaseData = baseData;
            UserData = userData;
        }
    }
}