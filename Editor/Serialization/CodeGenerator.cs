#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using alpoLib.Core.Foundation;
using alpoLib.Data;
using UnityEngine;
using UnityEditor;

namespace alpoLib.Data.Serialization.Editor
{
    // http://landman-code.blogspot.com/2009/02/c-superfasthash-and-murmurhash2.html
    internal class SuperFastHash
    {
        public static uint Hash(byte[] dataToHash)
        {
            var dataLength = dataToHash.Length;
            if (dataLength == 0)
                return 0;
            var hash = (uint)dataLength;
            var remainingBytes = dataLength & 3;
            var numberOfLoops = dataLength >> 2;
            var currentIndex = 0;
            while (numberOfLoops > 0)
            {
                hash += (ushort)(dataToHash[currentIndex++] | dataToHash[currentIndex++] << 8);
                var tmp = (uint)(dataToHash[currentIndex++] | dataToHash[currentIndex++] << 8) << 11 ^ hash;
                hash = (hash << 16) ^ tmp;
                hash += hash >> 11;
                numberOfLoops--;
            }

            switch (remainingBytes)
            {
                case 3:
                    hash += (ushort)(dataToHash[currentIndex++] | dataToHash[currentIndex++] << 8);
                    hash ^= hash << 16;
                    hash ^= ((uint)dataToHash[currentIndex]) << 18;
                    hash += hash >> 11;
                    break;
                case 2:
                    hash += (ushort)(dataToHash[currentIndex++] | dataToHash[currentIndex] << 8);
                    hash ^= hash << 11;
                    hash += hash >> 17;
                    break;
                case 1:
                    hash += dataToHash[currentIndex];
                    hash ^= hash << 10;
                    hash += hash >> 1;
                    break;
            }

            /* Force "avalanching" of final 127 bits */
            hash ^= hash << 3;
            hash += hash >> 5;
            hash ^= hash << 4;
            hash += hash >> 17;
            hash ^= hash << 25;
            hash += hash >> 6;

            return hash;
        }
    }

    internal class CodeGeneratorMenu
    {
        [MenuItem("AlpoLib/Data/Serializer/Clear Serializer")]
        public static void ClearSerializer()
        {
            CodeGenerator.ClearSerializer();
        }

        [MenuItem("AlpoLib/Data/Serializer/Generate Serializer")]
        public static void GenerateSerializer()
        {
            CodeGenerator.GenerateSerializer();
        }
    }
    
    public class CodeGenerator
    {
        private const string TYPE_ACCESS_MODIFIER = "public";
        private const string SERIALIZER_NAMESPACE = "alpoLib.Data.Serialization";
        
        private const string NAMESPACE_HEADER =
            "using System;\n" +
            "using System.Collections.Generic;\n" +
            "using System.Linq;\n" +
            "using Newtonsoft.Json.Linq;\n" +
            "using alpoLib.Core.Serialization;\n" +
            "using UnityEngine;\n" +
            "\n" +
            "namespace " + SERIALIZER_NAMESPACE + ".Generated {";
        
        private const string NAMESPACE_FOOTER = "}";

        private const string WRAPPED_CLASS_HEADER = TYPE_ACCESS_MODIFIER + " sealed record {0} : {1} {{";
        private const string WRAPPED_CLASS_FOOTER = "}";

        private const string CLASS_HEADER = TYPE_ACCESS_MODIFIER + " sealed class {0} : SerializerBase<{1}> {{";
        private const string CLASS_FOOTER = "}";

        private const string DESERIALIZE_HEADER = "public override {0} Deserialize(BufferStream stream) {{";
        private const string DESERIALIZE_FOOTER = "return da; }";

        private const string SERIALIZE_HEADER = "public override void Serialize(BufferStream stream, {0} da) {{";
        private const string SERIALIZE_FOOTER = "}";

        private const string JSON_CONVERTER_HEADER = TYPE_ACCESS_MODIFIER + " sealed class JsonToRecord_{0} : JsonToRecord<{0}> {{";
        private const string JSON_CONVERTER_FOOTER = "}";

        private const string JSON_TO_OBJECT_CONVERTER_METHOD_HEADER = "public override {0} JsonToObject(JToken token) {{";
        private const string JSON_TO_OBJECT_CONVERTER_METHOD_FOOTER = "return da; }";

        private const string OBJECT_TO_JSON_CONVERTER_METHOD_HEADER = "public override void ObjectToJson(JToken token, {0} da) {{";
        private const string OBJECT_TO_JSON_CONVERTER_METHOD_FOOTER = "}";

        private static StringBuilder sb;
        
        private static HashSet<Type> builtinValueTypes = new()
        {
            typeof(byte),       // u8
            typeof(ushort),     // u16
            typeof(uint),       // u32
            typeof(ulong),      // u64
            typeof(sbyte),      // s8
            typeof(short),      // s16
            typeof(int),        // s32
            typeof(long),       // s64
            typeof(float),      // f32
            typeof(double),     // f64
            typeof(bool),       // b
            typeof(string),     // s

            typeof(CustomDateTime),
            typeof(CustomBoolean),
            typeof(CustomColor),
        };

        private static Dictionary<Type, string> builtinValueTypeName = new()
        {
            { typeof(byte), "U8" }, // u8
            { typeof(ushort), "U16" }, // u16
            { typeof(uint), "U32" }, // u32
            { typeof(ulong), "U64" }, // u64
            { typeof(sbyte), "S8" }, // s8
            { typeof(short), "S16" }, // s16
            { typeof(int), "S32" }, // s32
            { typeof(long), "S64" }, // s64
            { typeof(float), "F32" }, // f32
            { typeof(double), "F64" }, // f64
            { typeof(bool), "Bool" }, // b
            { typeof(string), "Str" }, // s

            { typeof(CustomDateTime), "CustomDateTime" },
            { typeof(CustomBoolean), "CustomBoolean" },
            { typeof(CustomColor), "CustomColor" },
        };

        private static void Open()
        {
            sb = new StringBuilder(32767);
        }

        private static void Save()
        {
            var dirPath = Path.Combine(Application.dataPath, "Generated");
            var filePath = Path.Combine(dirPath, "GeneratedSerializer.cs");
            new DirectoryInfo(dirPath).Create();
            File.WriteAllText(filePath, sb.ToString());
            
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

            sb = null;
        }

        private static void WriteLine(string str)
        {
            sb.AppendLine(str);
        }
        
        public static void ClearSerializer()
        {
            sb = new StringBuilder(1024);

			WriteLine(NAMESPACE_HEADER);

			WriteLine(TYPE_ACCESS_MODIFIER + " static partial class GeneratedSerializerFactory {");
            WriteLine("[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]");
			WriteLine("public static void RegisterSerializers() { SerializerFactory.RegisterSerializers(ob); }");
			WriteLine("static Dictionary<Type, ISerializerBase> ob = new() {");
			WriteLine("};}");

			WriteLine(NAMESPACE_FOOTER);

			Save();
        }

        public static void GenerateSerializer()
        {
			sb = new StringBuilder(2);
            Save();
			Open();
            
            var type = typeof(IThreadedTableDataLoader);
            var types = AppDomain.CurrentDomain.GetAssemblies().SelectMany(x => x.GetTypes())
                .Where(p => type.IsAssignableFrom(p) && p.IsClass && p.BaseType.IsGenericType && !p.IsGenericType && p.BaseType.GetGenericTypeDefinition() == typeof(ThreadedTableDataLoader<>)); ;
            
            WriteLine(NAMESPACE_HEADER);
            
            var uniqueTypes = new HashSet<Type>();
            foreach (var t in types)
            {
                var dataType = t.BaseType.GetGenericArguments()[0];
                uniqueTypes.Add(dataType);

                var properties = GetProperties(dataType);
                if (!properties.Any())
                    continue;

                foreach (var prop in properties)
                {
                    var propType = prop.PropertyType;
                    if (propType.IsArray)
                        propType = propType.GetElementType();

                    if (!propType.IsClass)
                        continue;
                    
                    var nestedProperties = GetProperties(propType);
                    if (!nestedProperties.Any())
                        continue;
                    uniqueTypes.Add(propType);
                }
            }

            foreach (var t in uniqueTypes)
                Execute_Type(t);

			//WriteLine(TYPE_ACCESS_MODIFIER + " static partial class SerializerFactory {");
			//WriteLine("static SerializerFactory() { serializerMapper = ob; }");
			WriteLine(TYPE_ACCESS_MODIFIER + " static partial class GeneratedSerializerFactory {");
            WriteLine("[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]");
            WriteLine("public static void RegisterSerializers() { SerializerFactory.RegisterSerializers(ob); }");

            WriteLine("static Dictionary<Type, ISerializerBase> ob = new() {");
            foreach (var t in uniqueTypes)
                WriteLine($"{{ typeof({SanitizeTypeName(t.FullName)}), new {MakeSerializerClassName(t)}() }},");
            WriteLine("};}");
            
            
            
            WriteLine(NAMESPACE_FOOTER);
            
            Save();
            
            Debug.Log("Serializer code generate complete!!!!!");
        }

        private static void Execute_Type(Type dataType)
        {
            var properties = GetProperties(dataType);
            // if (!properties.Any())
            //     return;
            
            WriteLine(string.Format(WRAPPED_CLASS_HEADER, MakeWrappedClassName(dataType), SanitizeTypeName(dataType.FullName)));
            WriteLine(TYPE_ACCESS_MODIFIER + " static SchemeDefinition[] __schema__ = {");

            var count = 0;

            foreach (var prop in properties)
            {
                var isCompoundType = prop.GetCustomAttribute<DataCompoundTypeAttribute>() != null;
                var isCompoundListType = prop.GetCustomAttribute<DataCompoundListAttribute>() != null;
                var isChildCompoundType = prop.GetCustomAttribute<DataChildCompoundTypeAttribute>() != null;
                
                var nameHash = SuperFastHash.Hash(Encoding.UTF8.GetBytes(prop.Name + (isCompoundType ? "_comp" : "") + (isCompoundListType ? "_complist" : "")));
                var typeHash = MakePropHash(prop);

                if (isCompoundType || isCompoundListType || isChildCompoundType)
                {
                    var compProps = GetCompoundProperties(prop.PropertyType);
                    foreach (var cProp in compProps)
                    {
                        var compNameHash = SuperFastHash.Hash(Encoding.UTF8.GetBytes(cProp.Name));
                        var compTypeHash = MakePropHash(cProp);
                        count++;
                        WriteLine(
                            $"new() {{ NameHash = {compNameHash}, TypeHash = {compTypeHash} }}, /* (Compound) {cProp.PropertyType.Name} {prop.Name}.{cProp.Name} */");
                    }
                }

                count++;
                WriteLine(
                    $"new() {{ NameHash = {nameHash}, TypeHash = {typeHash} }}, /* {prop.PropertyType.Name} {prop.Name} */");
            }
            
            WriteLine($"/* Definition Count: {count} */");
            WriteLine("};");
            
            foreach (var prop in properties)
                WriteLine(TYPE_ACCESS_MODIFIER + " " + SanitizeTypeName(prop.PropertyType.FullName) + " " + MakeWrappedFieldName(prop.Name) + " { set { " + prop.Name + " = value; } }");
            
            WriteLine(WRAPPED_CLASS_FOOTER);
            
            var fullName = SanitizeTypeName(dataType.FullName);
            WriteLine(string.Format(CLASS_HEADER, MakeSerializerClassName(dataType), fullName));
            
            WriteLine(string.Format(TYPE_ACCESS_MODIFIER + " override SchemeDefinition[] GetSchemeDefinitions() {{ return {0}.__schema__; }}", MakeWrappedClassName(dataType)));

            WriteLine(string.Format(DESERIALIZE_HEADER, fullName));
            WriteLine($"var da = new {MakeWrappedClassName(dataType)}();");
            foreach (var prop in properties)
                Execute_Property(false, prop);
            WriteLine(DESERIALIZE_FOOTER);

            WriteLine(string.Format(SERIALIZE_HEADER, fullName));
            foreach (var prop in properties)
                Execute_Property(true, prop);
            WriteLine(SERIALIZE_FOOTER);

            var parser = new DataAttributeParser(dataType);
            
            WriteLine(string.Format(JSON_TO_OBJECT_CONVERTER_METHOD_HEADER, fullName));
            WriteLine($"var da = new {fullName}();");
            foreach (var prop in parser.DataProperties)
                MakeJsonToRecord(prop);
            WriteLine(JSON_TO_OBJECT_CONVERTER_METHOD_FOOTER);
            
            WriteLine(string.Format(OBJECT_TO_JSON_CONVERTER_METHOD_HEADER, fullName));
            WriteLine(OBJECT_TO_JSON_CONVERTER_METHOD_FOOTER);
            
            WriteLine(CLASS_FOOTER);
        }
        
        private static void Execute_Property(bool isSerialize, PropertyInfo prop)
        {
            var propType = prop.PropertyType;
            if (builtinValueTypes.Contains(propType))
                Execute_BuiltinValueType(isSerialize, prop);
            else if (propType.IsEnum)
                Execute_Enum(isSerialize, prop);
            else if (propType.IsArray)
                Execute_Array(isSerialize, prop);
            else
                Execute_Class(isSerialize, prop);
        }

        private static void Execute_BuiltinValueType(bool isSerialize, PropertyInfo prop)
        {
            WriteLine(isSerialize
                ? $"stream.Write{builtinValueTypeName[prop.PropertyType]}(da.{prop.Name});"
                : $"da.{MakeWrappedFieldName(prop.Name)} = stream.Read{builtinValueTypeName[prop.PropertyType]}();");
        }

        private static void Execute_Enum(bool isSerialize, PropertyInfo prop)
        {
            var propType = prop.PropertyType;
            var underlyingType = Enum.GetUnderlyingType(propType);

            WriteLine(
                isSerialize
                    ? $"stream.Write{builtinValueTypeName[underlyingType]}(({underlyingType.FullName})da.{prop.Name}); // ENUM"
                    : $"da.{MakeWrappedFieldName(prop.Name)} = ({SanitizeTypeName(propType.FullName)})stream.Read{builtinValueTypeName[underlyingType]}(); // ENUM");
        }

        private static void Execute_Array(bool isSerialize, PropertyInfo prop)
        {
            var arrayElemType = prop.PropertyType.GetElementType();
            var valListType = prop.GetCustomAttribute<DataColumnListAttribute>();
            var refListType = prop.GetCustomAttribute<DataCompoundListAttribute>();
            var typeListType = prop.GetCustomAttribute<DataCompoundTypeAttribute>();
            if (valListType != null)
            {
                var elemCount = valListType.ListCount;
                if (isSerialize)
                {
                    WriteLine($"for (int ___i = 0; ___i < {elemCount}; ___i++) {{");
                    WriteLine($"stream.Write{builtinValueTypeName[arrayElemType]}(da.{prop.Name}[___i]);");
                    WriteLine("}");
                }
                else
                {
                    WriteLine($"da.{MakeWrappedFieldName(prop.Name)} = new {arrayElemType.FullName}[{elemCount}];");
                    WriteLine($"for (int ___i = 0; ___i < {elemCount}; ___i++) {{");
                    WriteLine($"da.{prop.Name}[___i] = stream.Read{builtinValueTypeName[arrayElemType]}();");
                    WriteLine("}");
                }
            }
            else if (refListType != null)
            {
                var elemCount = refListType.ListCount;
                if (isSerialize)
                {
                    WriteLine($"for (int ___i = 0; ___i < {elemCount}; ___i++) {{");
                    WriteLine(
                        $"SerializeComp<{SanitizeTypeName(arrayElemType.FullName)}, {MakeSerializerClassName(arrayElemType)}>(stream, da.{prop.Name}[___i]);");
                    WriteLine("}");
                }
                else
                {
                    WriteLine(
                        $"da.{MakeWrappedFieldName(prop.Name)} = new {SanitizeTypeName(arrayElemType.FullName)}[{elemCount}];");
                    WriteLine($"for (int ___i = 0; ___i < {elemCount}; ___i++) {{");
                    WriteLine(
                        $"da.{prop.Name}[___i] = DeserializeComp<{SanitizeTypeName(arrayElemType.FullName)}, {MakeSerializerClassName(arrayElemType)}>(stream);");
                    WriteLine("}");
                }
            }
            else if (typeListType != null)
            {
            }
        }

        private static void Execute_Class(bool isSerialize, PropertyInfo prop)
        {
            WriteLine(
                isSerialize
                    ? $"SerializeComp<{SanitizeTypeName(prop.PropertyType.FullName)}, {MakeSerializerClassName(prop.PropertyType)}>(stream, da.{prop.Name});"
                    : $"da.{MakeWrappedFieldName(prop.Name)} = DeserializeComp<{SanitizeTypeName(prop.PropertyType.FullName)}, {MakeSerializerClassName(prop.PropertyType)}>(stream);");
        }

        private static void MakeJsonToRecord(DataAttributeParser.DataProperty dp)
        {
            if (dp.CompoundTypeAttribute != null)
            {
                var prefix = dp.CompoundTypeAttribute.ColumnPrefix;
                WriteLine(
                    $"da.{dp.PropertyName} = new {SanitizeTypeName(dp.PropertyInfo.PropertyType.FullName)} {{");
                foreach (var prop in dp.CompoundProperties)
                    WriteLine(
                        $"{prop.PropertyInfo.Name} = token[\"{prefix}{prop.ColumnName}\"] != null ? token[\"{prefix}{prop.ColumnName}\"].ToObject<{prop.FromTypeName}>() : default,");
                WriteLine("};");
            }
            else if (dp.CompoundListAttribute != null)
            {
                var elemName = SanitizeTypeName(dp.PropertyInfo.PropertyType.GetElementType().FullName);
                var elemCount = dp.CompoundListAttribute.ListCount;

                WriteLine($"da.{dp.PropertyName} = new {elemName}[{elemCount}];");
                for (var i = 0; i < elemCount; i++)
                {
                    WriteLine($"da.{dp.PropertyName}[{i}] = new {elemName} {{");
                    foreach (var prop in dp.CompoundListElementProperties)
                    {
                        WriteLine(
                            $"{prop.PropertyInfo.Name} = token[\"{prop.ColumnName}{i + 1}\"] != null ? token[\"{prop.ColumnName}{i + 1}\"].ToObject<{prop.FromTypeName}>() : default,");
                    }

                    WriteLine("};");
                }
            }
            else if (dp.ListAttribute != null)
            {
                var elemName = SanitizeTypeName(dp.PropertyInfo.PropertyType.GetElementType().Name);
                var elemCount = dp.ListAttribute.ListCount;

                WriteLine($"da.{dp.PropertyName} = new {elemName}[{elemCount}];");
                for (var i = 0; i < elemCount; i++)
                {
                    WriteLine($"da.{dp.PropertyName}[{i}] = token[\"{dp.ColumnListName}{i + 1}\"] != null ? token[\"{dp.ColumnListName}{i + 1}\"].ToObject<{elemName}>() : default;");
                }
            }

            else if (dp.ColumnAttribute != null)
            {
                if (dp.PropertyInfo.PropertyType.IsEnum)
                {
                    var converterVariableName = $"__{dp.PropertyName}_converter__";
                    WriteLine($"var {converterVariableName} = TypeDescriptor.GetConverter(typeof({dp.FromTypeName}));");
                    WriteLine($"da.{dp.PropertyName} = ({dp.FromTypeName}){converterVariableName}.ConvertFrom((String)token[\"{dp.ColumnName}\"]);");
                }
                else
                {
                    WriteLine($"da.{dp.PropertyName} = token[\"{dp.ColumnName}\"] != null ? token[\"{dp.ColumnName}\"].ToObject<{dp.FromTypeName}>() : default;");
                }
            }
            
            else if (dp.ChildCompoundTypeAttribute != null)
            {
                var typeName = SanitizeTypeName(dp.PropertyInfo.PropertyType.FullName);
                var serName = MakeSerializerClassName(dp.PropertyInfo.PropertyType);
                WriteLine($"da.{dp.PropertyName} = JsonToObjectComp<{typeName}, {serName}>(token[\"{dp.ChildCompoundColumnName}\"]);");
            }
        }

        private static IEnumerable<PropertyInfo> GetProperties(IReflect type)
        {
            var props = type.GetProperties(BindingFlags.Public |
                                           BindingFlags.Instance |
                                           BindingFlags.GetProperty |
                                           BindingFlags.SetProperty)
                .Where(p => p.GetCustomAttribute<DataColumnAttribute>() != null ||
                            p.GetCustomAttribute<DataColumnListAttribute>() != null ||
                            p.GetCustomAttribute<DataCompoundTypeAttribute>() != null ||
                            p.GetCustomAttribute<DataCompoundElementAttribute>() != null ||
                            p.GetCustomAttribute<DataCompoundListAttribute>() != null ||
                            p.GetCustomAttribute<DataCompoundListElementAttribute>() != null ||
                            p.GetCustomAttribute<DataChildCompoundTypeAttribute>() != null);
            return props;
        }
        
        private static IEnumerable<PropertyInfo> GetCompoundProperties(IReflect type)
        {
            var props = type.GetProperties(BindingFlags.Public |
                                           BindingFlags.Instance |
                                           BindingFlags.GetProperty |
                                           BindingFlags.SetProperty)
                .Where(p =>
                    p.GetCustomAttribute<DataCompoundElementAttribute>() != null ||
                    p.GetCustomAttribute<DataCompoundListElementAttribute>() != null ||
                    p.GetCustomAttribute<DataChildCompoundTypeAttribute>() != null);
            return props;
        }
        
        private static uint MakeEnumHash(Type type)
        {
            var s = new StringBuilder(1024);
            s.Append("_hs_");
            s.Append(type.FullName);
            s.Append('.');

            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                var rn = field.Name;
                var sn = field.GetRawConstantValue().ToString();
                s.Append(rn);
                s.Append('+');
                s.Append(sn);
                s.Append('-');
            }
            
            return SuperFastHash.Hash(Encoding.UTF8.GetBytes(s.ToString()));
        }
        
        private static uint MakeTypeHash(Type type)
        {
            return SuperFastHash.Hash(Encoding.UTF8.GetBytes("_hs_" + type.FullName));
        }
        
        private static uint MakePropHash(PropertyInfo prop)
        {
            var typeHash = prop.PropertyType.IsEnum ? MakeEnumHash(prop.PropertyType) : MakeTypeHash(prop.PropertyType);
            return typeHash;
        }
        
        private static string MakeSerializerClassName(Type type)
        {
            var name = type.Name;
            var fullName = type.FullName;
            var fullNameHash = SuperFastHash.Hash(Encoding.UTF8.GetBytes(fullName));
            name += "_" + fullNameHash;

            return "__r_" + name + "_Serializer";
        }
        
        private static string MakeWrappedClassName(Type type)
        {
            var name = type.Name;
            var fullName = type.FullName;
            var fullNameHash = SuperFastHash.Hash(Encoding.UTF8.GetBytes(fullName));
            name += "_" + fullNameHash;

            return "__r_" + name + "_Wrapped";
        }
        
        private static string MakeWrappedFieldName(string name)
        {
            return "__r_" + name + "_Ovr";
        }
        
        private static string SanitizeTypeName(string name)
        {
            return name.Replace('+', '.');
        }
    }
}
#endif