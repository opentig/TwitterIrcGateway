// $Id: CommandLineParser.cs 360 2008-01-20 09:34:39Z tomoyo $
#region This source code is licensed under MIT License
/*
 * The MIT License
 * 
 * Copyright © 2007 Mayuki Sawatari <mayuki@misuzilla.org>
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE.
*/
#endregion
using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using System.ComponentModel;
using System.Diagnostics;

namespace Misuzilla.Utilities
{
    public class CommandLineParser<T>
    {
        private Type _type;
        private Dictionary<String, PropertyInfo> _availableOptions = new Dictionary<string, PropertyInfo>();
        private List<String> _mandatoryOptions = new List<string>();
        
        public CommandLineParser()
        {
            _type = typeof(T);
            
            foreach (PropertyInfo pi in _type.GetProperties())
            {
                _availableOptions.Add(pi.Name, pi);
                if (GetDefaultValue(pi) == null)
                {
                    _mandatoryOptions.Add(pi.Name);
                }
            }
        }
        
        public void ShowHelp()
        {
            Int32 maxLen = 0;
            Dictionary<String, String> optionHelps = new Dictionary<string, string>();
            foreach (PropertyInfo pi in _type.GetProperties())
            {
                Object defaultValue = GetDefaultValue(pi);
                String defaultOrRequired = (defaultValue == null ? "(Required)" : "(Default: " + defaultValue.ToString() + ")");
                String keyName;
                String optionName = ToLowerAndDelimiterize('-', pi.Name);
                if (pi.PropertyType == typeof(Boolean))
                {
                    keyName = String.Format("--{0}=<true|false>", optionName);
                }
                else
                {
                    keyName = String.Format("--{0}=({1})", optionName, pi.PropertyType.Name);
                }
                optionHelps[keyName] = GetDescription(pi) + " " + defaultOrRequired;
                
                if (maxLen < keyName.Length)
                    maxLen = keyName.Length;
            }

            foreach (String key in optionHelps.Keys)
            {
                Console.WriteLine("{0,"+(-maxLen)+"}: {1}", key, optionHelps[key]);
            }
        }

        private T CreateAndInitlizeInstance()
        {
            T returnValue = Activator.CreateInstance<T>();
            foreach (PropertyInfo pi in _availableOptions.Values)
            {
                Object value = GetDefaultValue(pi);
                if (pi != null)
                    pi.SetValue(returnValue, value, null);
            }
            return returnValue;
        }

        public Boolean TryParse(String[] args, out T returnOptions)
        {
            try
            {
                returnOptions = Parse(args);
            }
            catch (ArgumentException)
            {
                returnOptions = default(T);
                return false;
            }

            return (returnOptions != null);
        }
        
        public T Parse(String[] args)
        {
            List<String> mandatories = new List<string>(_mandatoryOptions);
            T returnValue = CreateAndInitlizeInstance();
            
            for (Int32 i = 0; i < args.Length; i++)
            {
                String[] parts = args[i].Split(new Char[]{ '=', ':' }, 2);
                String memberName = ToUpperCamelCase('-', parts[0]);

                // Help
                if (String.Compare(memberName, "help", true) == 0)
                    return default(T);
                
                if (!_availableOptions.ContainsKey(memberName))
                {
                    //Debug.WriteLine(String.Format("Unknown option '{0}'", parts[0]));
                    //throw new ArgumentException("invalid argument", parts[0]);
                    continue;
                }    
                if (parts.Length == 1)
                {
                    // Boolean
                    //Debug.WriteLine("{0} -> true", ToUpperCamelCase('-', parts[0]));
                    if (_availableOptions[memberName].PropertyType != typeof(Boolean))
                    {
                        throw new ArgumentException("option type is a not Boolean", parts[0]);
                    }
                    _availableOptions[memberName].SetValue(returnValue, true, null);
                }
                else
                {
                    // Object
                    //Debug.WriteLine("{0} -> {1}", ToUpperCamelCase('-', parts[0]), parts[1]);
                    TypeConverter typeConv = TypeDescriptor.GetConverter(_availableOptions[memberName].PropertyType);
                    _availableOptions[memberName].SetValue(returnValue, typeConv.ConvertFromString(parts[1]), null);
                }

                if (mandatories.Contains(memberName))
                {
                    mandatories.Remove(memberName);
                }
            }

            if (mandatories.Count != 0)
            {
                throw new ArgumentException("Options are missing");
            }
            
            return returnValue;
        }

        private String ToUpperCamelCase(Char delimiter, String s)
        {
            StringBuilder sb = new StringBuilder();
            for (Int32 i = 0; i < s.Length; i++)
            {
                if (s[i] == delimiter)
                    continue;
                else if (i == 0 || s[i - 1] == delimiter)
                    sb.Append(Char.ToUpper(s[i]));
                else
                    sb.Append(s[i]);
            }
            return sb.ToString();
        }

        private String ToLowerAndDelimiterize(Char c, String s)
        {
            StringBuilder sb = new StringBuilder();
            for (Int32 i = 0; i < s.Length; i++)
            {
                if (i != 0 && Char.IsUpper(s[i]))
                {
                    sb.Append(c);
                }
                sb.Append(Char.ToLower(s[i]));
            }
            return sb.ToString();
        }

        private String GetDescription(MemberInfo memberInfo)
        {
            Object[] attrs = memberInfo.GetCustomAttributes(typeof(DescriptionAttribute), true);
            return (attrs.Length == 0) ? "" : ((DescriptionAttribute)attrs[0]).Description;
        }

        private Object GetDefaultValue(MemberInfo memberInfo)
        {
            Object[] attrs = memberInfo.GetCustomAttributes(typeof(DefaultValueAttribute), true);
            if (attrs.Length == 0)
                return null;

            DefaultValueAttribute attr = attrs[0] as DefaultValueAttribute;
            return attr.Value;
        }
    }
}
