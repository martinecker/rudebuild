using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Serialization;
using System.Xml;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using System.Security.Permissions;

namespace RudeBuild
{
    [Serializable()]
    public class SerializableDictionary<TKey, TValue> : Dictionary<TKey, TValue>, IXmlSerializable, ISerializable
    {
        private const string DictionaryNodeName = "Dictionary";
        private const string ItemNodeName = "Item";
        private const string KeyNodeName = "Key";
        private const string ValueNodeName = "Value";

        private XmlSerializer valueSerializer = null;
        protected XmlSerializer ValueSerializer
        {
            get
            {
                if (valueSerializer == null)
                    valueSerializer = new XmlSerializer(typeof(TValue));
                return valueSerializer;
            }
        }

        private XmlSerializer keySerializer = null;
        private XmlSerializer KeySerializer
        {
            get
            {
                if (keySerializer == null)
                    keySerializer = new XmlSerializer(typeof(TKey));
                return keySerializer;
            }
        }

        #region Constructors

        public SerializableDictionary()
        {
        }

        public SerializableDictionary(IDictionary<TKey,TValue> dictionary)
            : base(dictionary)
        {
        }

        public SerializableDictionary(IEqualityComparer<TKey> comparer)
            : base(comparer)
        {
        }

        public SerializableDictionary(int capacity)
            : base(capacity)
        {
        }

        public SerializableDictionary(IDictionary<TKey,TValue> dictionary, IEqualityComparer<TKey> comparer)
            : base(dictionary, comparer)
        {
        }

        public SerializableDictionary(int capacity, IEqualityComparer<TKey> comparer)
            : base(capacity, comparer)
        {
        }

        #endregion

        #region ISerializable Members

        protected SerializableDictionary(SerializationInfo info, StreamingContext context)
        {
            int itemCount = info.GetInt32("ItemCount");
            for (int i = 0; i < itemCount; i++)
            {
                KeyValuePair<TKey, TValue> kvp = (KeyValuePair<TKey, TValue>)info.GetValue(String.Format("Item{0}", i), typeof(KeyValuePair<TKey, TValue>));
                this.Add(kvp.Key, kvp.Value);
            }
        }

        [SecurityPermission(SecurityAction.LinkDemand, Flags = SecurityPermissionFlag.SerializationFormatter)]
        void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("ItemCount", this.Count);
            int itemIdx = 0;
            foreach (KeyValuePair<TKey, TValue> kvp in this)
            {
                info.AddValue(String.Format("Item{0}", itemIdx), kvp, typeof(KeyValuePair<TKey, TValue>));
                itemIdx++;
            }
        }

        #endregion

        #region IXmlSerializable Members

        void IXmlSerializable.WriteXml(System.Xml.XmlWriter writer)
        {
            //writer.WriteStartElement(DictionaryNodeName);
            foreach (KeyValuePair<TKey, TValue> kvp in this)
            {
                writer.WriteStartElement(ItemNodeName);
                writer.WriteStartElement(KeyNodeName);
                KeySerializer.Serialize(writer, kvp.Key);
                writer.WriteEndElement();
                writer.WriteStartElement(ValueNodeName);
                ValueSerializer.Serialize(writer, kvp.Value);
                writer.WriteEndElement();
                writer.WriteEndElement();
            }
            //writer.WriteEndElement();
        }

        void IXmlSerializable.ReadXml(System.Xml.XmlReader reader)
        {
            if (reader.IsEmptyElement)
            {
                reader.Read();
                return;
            }

            // Move past container
            if (!reader.Read())
                throw new XmlException("Error in deserialization of Dictionary");

            //reader.ReadStartElement(DictionaryNodeName);
            while (reader.NodeType != XmlNodeType.EndElement)
            {
                reader.ReadStartElement(ItemNodeName);
                reader.ReadStartElement(KeyNodeName);
                TKey key = (TKey)KeySerializer.Deserialize(reader);
                reader.ReadEndElement();
                reader.ReadStartElement(ValueNodeName);
                TValue value = (TValue)ValueSerializer.Deserialize(reader);
                reader.ReadEndElement();
                reader.ReadEndElement();
                this.Add(key, value);
                reader.MoveToContent();
            }
            //reader.ReadEndElement();

            reader.ReadEndElement(); // Read End Element to close Read of containing node
        }

        System.Xml.Schema.XmlSchema IXmlSerializable.GetSchema()
        {
            return null;
        }

        #endregion
    }
}
