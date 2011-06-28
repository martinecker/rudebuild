using System;

namespace RudeBuild
{
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class DisplayValueAttribute : Attribute
    {
        private readonly string _value;
        public string Value
        {
            get { return _value; }
        }

        public string ResourceKey { get; set; }

        public DisplayValueAttribute(string value)
        {
            _value = value;
        }

        public DisplayValueAttribute()
        {
        }
    }
}
