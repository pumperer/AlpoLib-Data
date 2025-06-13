using System;
using System.Collections.Generic;
using UnityEngine;

namespace alpoLib.Data.Composition
{
    public abstract class CompositionDataBase : DataManagerHolder
    {
    }

    public abstract class CompositionDataBase<TBase, TUser> : CompositionDataBase, IEquatable<CompositionDataBase<TBase, TUser>>
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

        public bool Equals(CompositionDataBase<TBase, TUser> other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return EqualityComparer<TBase>.Default.Equals(BaseData, other.BaseData) && EqualityComparer<TUser>.Default.Equals(UserData, other.UserData);
        }

        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((CompositionDataBase<TBase, TUser>)obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(BaseData, UserData);
        }
    }
}