using System;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Resources;
using System.Windows;
using System.Windows.Data;
using System.Globalization;
using RudeBuild;

namespace RudeBuildVSAddIn
{
    public class EnumDisplayer : IValueConverter
    {
        private Type _type;
        private IDictionary _enumToDisplayValues;
        private IDictionary _displayToEnumValues;
        private ReadOnlyCollection<string> _displayValues;
        private ResourceManager _resourceManager;

        public EnumDisplayer()
        {
        }

        public EnumDisplayer(Type type)
        {
            _type = type;
        }

        public Type Type
        {
            get { return _type; }
            set 
            {
                if (!value.IsEnum)
                    throw new ArgumentException("Type needs to be an enumeration type", "value");
                _type = value;

                _resourceManager = new ResourceManager(_type);

                Type genericDictionaryType = typeof(Dictionary<,>).GetGenericTypeDefinition();
                Type enumToDisplayValuesType = genericDictionaryType.MakeGenericType(_type, typeof(string));
                _enumToDisplayValues = (IDictionary)Activator.CreateInstance(enumToDisplayValuesType);
                Type displayToEnumValuesType = genericDictionaryType.MakeGenericType(typeof(string), _type);
                _displayToEnumValues = (IDictionary)Activator.CreateInstance(displayToEnumValuesType);
                var displayValues = new List<string>();

                FieldInfo[] fields = _type.GetFields(BindingFlags.Public | BindingFlags.Static);
                foreach (FieldInfo field in fields)
                {
                    var attributes = field.GetCustomAttributes(typeof(DisplayValueAttribute), false) as DisplayValueAttribute[];
                    object enumValue = field.GetValue(null);
                    string displayValue = GetDisplayValue(_type, enumValue, attributes);
                    if (null != displayValue)
                    {
                        _enumToDisplayValues.Add(enumValue, displayValue);
                        _displayToEnumValues.Add(displayValue, enumValue);
                        displayValues.Add(displayValue);
                    }
                }

                _displayValues = displayValues.AsReadOnly();
            }
        }

        private string GetDisplayValue(Type enumType, object enumValue, DisplayValueAttribute[] attributes)
        {
            if (null == attributes || 0 == attributes.Length)
                return null;
            DisplayValueAttribute attribute = attributes[0];
            if (!string.IsNullOrEmpty(attribute.ResourceKey))
                return _resourceManager.GetString(attribute.ResourceKey);
            else if (!string.IsNullOrEmpty(attribute.Value))
                return attribute.Value;
            else
                return Enum.GetName(enumType, enumValue);
        }

        public ReadOnlyCollection<string> DisplayValues
        {
            get { return _displayValues; }
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!value.GetType().IsEnum)
                return DependencyProperty.UnsetValue;
            return _enumToDisplayValues[value];
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value.GetType() != typeof(string))
                return DependencyProperty.UnsetValue;
            return _displayToEnumValues[value];
        }
    }
}