using System;
using System.Collections.Generic;
using alpoLib.Core.Prng;
using alpoLib.Core.Security;
using alpoLib.Core.Serialization;
using Newtonsoft.Json.Linq;

namespace alpoLib.Data.Serialization
{
	public abstract class JsonToRecord<T> where T : TableDataBase
    {
        public abstract T ToRecord(JToken token);
    }
    
    public struct SchemeDefinition
    {
        public uint NameHash;
        public uint TypeHash;
    }

    public interface ISerializerBase
    {
        SchemeDefinition[] GetSchemeDefinitions();
        
        void Serialize(BufferStream stream, object data);
        
        object JsonToObject(object token);
    }

    public interface ISerializerBase<T> : ISerializerBase
    {
        void Serialize(BufferStream stream, T data);
        
        T JsonToObject(JToken token);

        void ISerializerBase.Serialize(BufferStream stream, object data)
        {
            Serialize(stream, (T)data);
        }
        
        object ISerializerBase.JsonToObject(object token)
        {
            return JsonToObject(token as JToken);
        }
    }
    
    public abstract class SerializerBase<T> : ISerializerBase<T>
    {
        public abstract SchemeDefinition[] GetSchemeDefinitions();
        public abstract T Deserialize(BufferStream stream);
        public abstract void Serialize(BufferStream stream, T data);
        public abstract T JsonToObject(JToken token);
        public abstract void ObjectToJson(JToken token, T data);

        public void SerializeComp<TData, TSerializer>(BufferStream stream, TData item) where TSerializer : SerializerBase<TData>
        {
            var serializer = (TSerializer)Generated.SerializerFactory.GetSerializer(typeof(TData));
            serializer.Serialize(stream, item);
        }

        public TData DeserializeComp<TData, TSerializer>(BufferStream stream) where TSerializer : SerializerBase<TData>
        {
            var serializer = (TSerializer)Generated.SerializerFactory.GetSerializer(typeof(TData));
            return serializer.Deserialize(stream);
        }

        public void ObjectToJsonComp<TData, TSerializer>(JToken token, TData item)
            where TSerializer : SerializerBase<TData>
        {
            var serializer = (TSerializer)Generated.SerializerFactory.GetSerializer(typeof(TData));
            serializer.ObjectToJson(token, item);
        }
        
        public TData JsonToObjectComp<TData, TSerializer>(JToken token) where TSerializer : SerializerBase<TData>
        {
            var serializer = (TSerializer)Generated.SerializerFactory.GetSerializer(typeof(TData));
            return serializer.JsonToObject(token);
        }
    }

    public class BinSerializer
    {
        private static bool ReadScheme(BufferStream stream, SchemeDefinition[] schemeList)
        {
            var lengthFromStream = stream.ReadU16();
            var schemeLen = schemeList.Length;
            if (lengthFromStream != schemeLen)
                return false;

            for (var i = 0; i < schemeLen; i++)
            {
                var nh = schemeList[i].NameHash;
                var nhFromStream = stream.ReadU32();
                var th = schemeList[i].TypeHash;
                var thFromStream = stream.ReadU32();

                if (nh != nhFromStream || th != thFromStream)
                    return false;
            }

            return true;
        }

        private static void WriteScheme(BufferStream stream, SchemeDefinition[] schemeList)
        {
            stream.WriteU16((ushort)schemeList.Length);
            for (var i = 0; i < schemeList.Length; i++)
            {
                stream.WriteU32(schemeList[i].NameHash);
                stream.WriteU32(schemeList[i].TypeHash);
            }
        }

        public static TData Deserialize<TData>(BufferStream stream) where TData : class
        {
            var serializer = (SerializerBase<TData>)Generated.SerializerFactory.GetSerializer(typeof(TData));
            if (serializer == null)
                return null;

            var scheme = serializer.GetSchemeDefinitions();
            if (!ReadScheme(stream, scheme))
                return null;

            var prng = new PrngEncryptionProvider<XSP>(new XSP(), 88725332);

            var encBytes = stream.Bytes;
            for (var i = stream.Offset; i < stream.Offset + stream.Length; i++)
            {
                encBytes[i] ^= prng.NextByte();
            }
            
            return serializer.Deserialize(stream);
        }

        public static IList<TData> DeserializeList<TData>(BufferStream stream) where TData : class
        {
            var serializer = (SerializerBase<TData>)Generated.SerializerFactory.GetSerializer(typeof(TData));
            if (serializer == null)
                return null;

            var scheme = serializer.GetSchemeDefinitions();
            if (!ReadScheme(stream, scheme))
                return null;

            VarInt.ReadVarInt(stream, out var listCount);
            var readPos = stream.Pointer;
            var prng = new PrngEncryptionProvider<XSP>(new XSP(), listCount);

            var encBytes = stream.Bytes;
            for (var i = readPos; i < stream.Offset + stream.Length; i++)
                encBytes[i] ^= prng.NextByte();

            var list = new List<TData>(listCount);
            for (var i = 0; i < listCount; i++)
            {
                var data = serializer.Deserialize(stream);
                list.Add(data);
            }

            return list;
        }

        public static ArraySegment<byte> Serialize<TData>(TData value)
        {
            var stream = new BufferStream { Bytes = new byte[1024], Length = 0 };
            var serializer = (SerializerBase<TData>)Generated.SerializerFactory.GetSerializer(typeof(TData));
            
            WriteScheme(stream, serializer.GetSchemeDefinitions());
            var writtenPos = stream.Pointer;

            var prng = new PrngEncryptionProvider<XSP>(new XSP(), 88725332);
            
            serializer.Serialize(stream, value);

            var encBytes = stream.Bytes;
            for (var i = writtenPos; i < stream.Offset + stream.Length; i++)
                encBytes[i] ^= prng.NextByte();

            return new ArraySegment<byte>(stream.Bytes, stream.Offset, stream.Length);
        }

        public static ArraySegment<byte> SerializeList<TData>(IList<TData> valueList)
        {
            var stream = new BufferStream { Bytes = new byte[1024 * valueList.Count], Length = 0 };
            var serializer = (SerializerBase<TData>)Generated.SerializerFactory.GetSerializer(typeof(TData));
            
            WriteScheme(stream, serializer.GetSchemeDefinitions());
            
            stream.EnsureCapacity(5);
            VarInt.WriteVarInt(stream, valueList.Count);
            var writtenPos = stream.Pointer;

            var prng = new PrngEncryptionProvider<XSP>(new XSP(), valueList.Count);

            foreach (var value in valueList)
                serializer.Serialize(stream, value);
            
            var encBytes = stream.Bytes;
            for (var i = writtenPos; i < stream.Offset + stream.Length; i++)
                encBytes[i] ^= prng.NextByte();
            
            return new ArraySegment<byte>(stream.Bytes, stream.Offset, stream.Length); 
        }

        public static ArraySegment<byte> SerializeList(IList<object> valueList, Type dataType)
        {
            var stream = new BufferStream { Bytes = new byte[1024 * valueList.Count], Length = 0 };
            var serializer = Generated.SerializerFactory.GetSerializer(dataType);
            
            WriteScheme(stream, serializer.GetSchemeDefinitions());
            
            stream.EnsureCapacity(5);
            VarInt.WriteVarInt(stream, valueList.Count);
            var writtenPos = stream.Pointer;

            var prng = new PrngEncryptionProvider<XSP>(new XSP(), valueList.Count);

            foreach (var value in valueList)
                serializer.Serialize(stream, value);
            
            var encBytes = stream.Bytes;
            for (var i = writtenPos; i < stream.Offset + stream.Length; i++)
                encBytes[i] ^= prng.NextByte();
            
            return new ArraySegment<byte>(stream.Bytes, stream.Offset, stream.Length); 
        }

        public static void SerializeListToFile<TData>(IList<TData> valueList, string path)
        {
            var segment = SerializeList<TData>(valueList);

            var copied = new byte[segment.Count];
            Array.Copy(segment.Array, segment.Offset, copied, 0, segment.Count);
            System.IO.File.WriteAllBytes(path, copied);
            Debug.Log($"Saved list of {typeof(TData).Name} to {path}");
        }

        public static void SerializeListToFile(IList<object> valueList, Type dataType, string path)
        {
            var segment = SerializeList(valueList, dataType);
            
            var copied = new byte[segment.Count];
            Array.Copy(segment.Array, segment.Offset, copied, 0, segment.Count);
            System.IO.File.WriteAllBytes(path, copied);
            Debug.Log($"Saved list of {dataType.Name} to {path}");
        }
}
    
    namespace Generated
	{
		public static partial class SerializerFactory
        {
            private static Dictionary<Type, ISerializerBase> serializerMapper = null;

            public static void RegisterSerializers(Dictionary<Type, ISerializerBase> serializers)
            {
                serializerMapper = serializers;
            }
            
            public static ISerializerBase GetSerializer<T>() where T : TableDataBase
            {
                return GetSerializer(typeof(T)) as ISerializerBase;
            }
            
            public static ISerializerBase GetSerializer(Type type)
            {
                if (serializerMapper == null)
                    return null;

                serializerMapper.TryGetValue(type, out var serializer);
                return serializer;
            }
        }
    }
}