using System;

namespace alpoLib.Data.Serialization
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class DataIgnoreAttribute : Attribute
    {
    }

    public abstract class DataBasicTypeAttribute : Attribute
    {
        public Type FromType { get; protected set; }

        protected DataBasicTypeAttribute(Type fromType)
        {
            FromType = fromType;
        }
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class DataColumnAttribute : DataBasicTypeAttribute
    {
        public string ColumnName { get; private set; }

        public DataColumnAttribute(string columnName, Type fromType)
            : base(fromType)
        {
            ColumnName = columnName;
        }

        public DataColumnAttribute(string columnName)
            : base(null)
        {
            ColumnName = columnName;
        }

        public DataColumnAttribute()
            : base(null)
        {
            ColumnName = default;
        }
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class DataColumnListAttribute : Attribute
    {
        public string ColumnName { get; private set; }
        public int ListCount { get; private set; }

        public DataColumnListAttribute(string columnName, int listCount)
        {
            ColumnName = columnName;
            ListCount = listCount;
        }
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class DataCompoundTypeAttribute : Attribute
    {
        public string ColumnPrefix { get; private set; }

        public DataCompoundTypeAttribute(string columnPrefix = "")
        {
            ColumnPrefix = columnPrefix;
        }
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class DataChildCompoundTypeAttribute : Attribute
    {
        public string ColumnName { get; private set; }

        public DataChildCompoundTypeAttribute(string columnName = "")
        {
            ColumnName = columnName;
        }
    }
    
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class DataCompoundElementAttribute : DataBasicTypeAttribute
    {
        public string ColumnName { get; private set; }

        public DataCompoundElementAttribute(string columnName, Type fromType)
            : base(fromType)
        {
            ColumnName = columnName;
        }
        
        public DataCompoundElementAttribute(string columnName)
            : base(null)
        {
            ColumnName = columnName;
        }
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class DataCompoundListElementAttribute : DataBasicTypeAttribute
    {
        public string ColumnName { get; private set; }

        public DataCompoundListElementAttribute(string columnName, Type fromType)
            : base(fromType)
        {
            ColumnName = columnName;
        }
        
        public DataCompoundListElementAttribute(string columnName)
            : base(null)
        {
            ColumnName = columnName;
        }

        public DataCompoundListElementAttribute()
            : base(null)
        {
            ColumnName = default;
        }
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class DataCompoundListAttribute : Attribute
    {
        public int ListCount { get; private set; }

        public DataCompoundListAttribute(int listCount) { ListCount = listCount; }
    }
}
