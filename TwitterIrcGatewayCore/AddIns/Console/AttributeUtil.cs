using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Text;

namespace Misuzilla.Applications.TwitterIrcGateway.AddIns.Console
{
    internal static class AttributeUtil
    {
        public static Boolean IsBrowsable(Type t)
        {
            Object[] attrs = t.GetCustomAttributes(typeof(BrowsableAttribute), true);
            return !(attrs.Length != 0 && !((BrowsableAttribute)attrs[0]).Browsable);
        }
        public static Boolean IsBrowsable(ICustomAttributeProvider customAttributeProvider)
        {
            Object[] attrs = customAttributeProvider.GetCustomAttributes(typeof(BrowsableAttribute), true);
            return !(attrs.Length != 0 && !((BrowsableAttribute)attrs[0]).Browsable);
        }
        public static String GetDescription(Type t)
        {
            Object[] attrs = t.GetCustomAttributes(typeof(DescriptionAttribute), true);
            return (attrs.Length == 0) ? "" : ((DescriptionAttribute)attrs[0]).Description;
        }
        public static String GetDescription(ICustomAttributeProvider customAttributeProvider)
        {
            Object[] attrs = customAttributeProvider.GetCustomAttributes(typeof(DescriptionAttribute), true);
            return (attrs.Length == 0) ? "" : ((DescriptionAttribute)attrs[0]).Description;
        }
        public static Object GetDefaultValue(Type t)
        {
            Object[] attrs = t.GetCustomAttributes(typeof(DefaultValueAttribute), true);
            return (attrs.Length == 0) ? null : ((DefaultValueAttribute)attrs[0]).Value;
        }
        public static Object GetDefaultValue(ICustomAttributeProvider customAttributeProvider)
        {
            Object[] attrs = customAttributeProvider.GetCustomAttributes(typeof(DefaultValueAttribute), true);
            return (attrs.Length == 0) ? null : ((DefaultValueAttribute)attrs[0]).Value;
        }
    }
}
