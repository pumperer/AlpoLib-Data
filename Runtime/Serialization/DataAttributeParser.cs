using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace alpoLib.Data.Serialization
{
	public class DataAttributeParser
    {
        public class DataCompoundListElementProperty
        {
            public PropertyInfo PropertyInfo;
            public DataCompoundListElementAttribute CompoundListElementAttribute;

            public DataCompoundListElementProperty(PropertyInfo pi)
            {
                PropertyInfo = pi;
            }

            public string ColumnName
            {
                get
                {
                    if (CompoundListElementAttribute != null &&
                        !string.IsNullOrEmpty(CompoundListElementAttribute.ColumnName))
                        return CompoundListElementAttribute.ColumnName;
                    return PropertyInfo.Name;
                }
            }

            public string FromTypeName => CompoundListElementAttribute is { FromType: not null }
                ? CompoundListElementAttribute.FromType.FullName
                : PropertyInfo.PropertyType.FullName;
        }

        public class DataCompoundElementProperty
        {
            public PropertyInfo PropertyInfo;
            public DataCompoundElementAttribute CompoundElementAttribute;

            public DataCompoundElementProperty(PropertyInfo pi)
            {
                PropertyInfo = pi;
            }

            public string ColumnName
            {
                get
                {
                    if (CompoundElementAttribute != null && !string.IsNullOrEmpty(CompoundElementAttribute.ColumnName))
                        return CompoundElementAttribute.ColumnName;
                    return PropertyInfo.Name;
                }
            }

            public string FromTypeName => CompoundElementAttribute is { FromType: not null }
                ? CompoundElementAttribute.FromType.FullName
                : PropertyInfo.PropertyType.FullName;
        }

        public class DataProperty
        {
            public PropertyInfo PropertyInfo;
            public DataColumnAttribute ColumnAttribute;
            public DataColumnListAttribute ListAttribute;
            
            public DataCompoundTypeAttribute CompoundTypeAttribute;
            public List<DataCompoundElementProperty> CompoundProperties;
            
            public DataCompoundListAttribute CompoundListAttribute;
            public List<DataCompoundListElementProperty> CompoundListElementProperties;

            public DataChildCompoundTypeAttribute ChildCompoundTypeAttribute;

            public Type FromPropertyType;

            public DataProperty(PropertyInfo pi)
            {
                PropertyInfo = pi;
            }

            public string PropertyName => PropertyInfo.Name;

            public string ColumnName
            {
                get
                {
                    if (ColumnAttribute != null && !string.IsNullOrEmpty(ColumnAttribute.ColumnName))
                        return ColumnAttribute.ColumnName;
                    return PropertyName;
                }
            }

            public string ColumnListName
            {
                get
                {
                    if (ListAttribute != null && !string.IsNullOrEmpty(ListAttribute.ColumnName))
                        return ListAttribute.ColumnName;
                    return PropertyName;
                }
            }

            public string ChildCompoundColumnName
            {
                get
                {
                    if (ChildCompoundTypeAttribute != null &&
                        !string.IsNullOrEmpty(ChildCompoundTypeAttribute.ColumnName))
                        return ChildCompoundTypeAttribute.ColumnName;
                    return PropertyName;
                }
            }

            public string FromTypeName => FromPropertyType != null ? FromPropertyType.FullName : PropertyInfo.PropertyType.FullName;
        }

        private Type currentType;
        private List<DataProperty> properties;
        private List<string> compoundPropertyNames = new();

        public Type CurrentType => currentType;
        public List<DataProperty> DataProperties => properties;

        public DataAttributeParser(Type type)
        {
            currentType = type;
            //var type = typeof(T);
            var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance
                                                               | BindingFlags.GetProperty |
                                                               BindingFlags.SetProperty);
            properties =
                (from prop in props
                    where prop.GetCustomAttribute<DataIgnoreAttribute>() == null
                    orderby prop.Name
                    select new DataProperty(prop)).ToList();

            foreach (var prop in properties)
            {
                var columnAttr = prop.PropertyInfo.GetCustomAttribute<DataColumnAttribute>();
                if (columnAttr != null)
                {
                    prop.ColumnAttribute = columnAttr;
                    prop.FromPropertyType = columnAttr.FromType;
                }

                var listAttr = prop.PropertyInfo.GetCustomAttribute<DataColumnListAttribute>();
                if (listAttr != null)
                {
                    prop.ListAttribute = listAttr;
                }

                var compoundAttr = prop.PropertyInfo.GetCustomAttribute<DataCompoundTypeAttribute>();
                if (compoundAttr != null)
                {
                    var compType = prop.PropertyInfo.PropertyType;
                    var compoundProperties = compType.GetProperties(BindingFlags.Public | BindingFlags.Instance |
                                                                    BindingFlags.GetProperty |
                                                                    BindingFlags.SetProperty);
                    var ps = (from p in compoundProperties
                        where p.GetCustomAttribute<DataCompoundElementAttribute>() != null
                        select new DataCompoundElementProperty(p)).ToList();
                    foreach (var compoundElementProperty in ps)
                    {
                        compoundElementProperty.CompoundElementAttribute = compoundElementProperty.PropertyInfo
                            .GetCustomAttribute<DataCompoundElementAttribute>();
                        compoundPropertyNames.Add(
                            $"{compType.Name}+{compoundElementProperty.CompoundElementAttribute.ColumnName}");
                    }

                    prop.CompoundTypeAttribute = compoundAttr;
                    prop.CompoundProperties = ps;
                }

                var compoundListAttr = prop.PropertyInfo.GetCustomAttribute<DataCompoundListAttribute>();
                if (compoundListAttr != null)
                {
                    var elemType = prop.PropertyInfo.PropertyType.GetElementType();
                    var compoundListProperties = elemType.GetProperties(BindingFlags.Public | BindingFlags.Instance |
                                                                        BindingFlags.GetProperty |
                                                                        BindingFlags.SetProperty);
                    var ps = (from p in compoundListProperties
                        where p.GetCustomAttribute<DataCompoundListElementAttribute>() != null
                        select new DataCompoundListElementProperty(p)).ToList();
                    foreach (var compoundListElementProperty in ps)
                    {
                        compoundListElementProperty.CompoundListElementAttribute = compoundListElementProperty
                            .PropertyInfo.GetCustomAttribute<DataCompoundListElementAttribute>();
                        compoundPropertyNames.Add(compoundListElementProperty.CompoundListElementAttribute.ColumnName);
                    }

                    prop.CompoundListAttribute = compoundListAttr;
                    prop.CompoundListElementProperties = ps;
                }

                var childCompoundAttr = prop.PropertyInfo.GetCustomAttribute<DataChildCompoundTypeAttribute>();
                if (childCompoundAttr != null)
                {
                    prop.ChildCompoundTypeAttribute = childCompoundAttr;
                }
            }
        }
    }
}
