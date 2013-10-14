//
// TypableMap - typo しにくい id を自動生成
// $Id: TypableMap.cs 412 2008-09-14 19:21:39Z tomoyo $
// 
// Original code by cho45
// http://subtech.g.hatena.ne.jp/cho45/20080603/1212504034
//
// Copyright © 2008 Mayuki Sawatari <mayuki@misuzilla.org>
// License: MIT License
//
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace TypableMap
{
    public class TypableMap<T> : IDictionary<String, T>
    {
        private static readonly String[] Roma;
        
        private Dictionary<String, T> _map;
        private Int32 _size;
        private Int64 _num;
        
        static TypableMap()
        {
            List<String> roma = new List<string>();
            foreach (var c in "a i u e o k g s z t d n h b p m y r w j v l q".Split(' '))
                foreach (var d in "a i u e o".Split(' '))
                    roma.Add(String.Concat(c.ToString(), d.ToString()));
            Roma = roma.ToArray();
        }
        
        /// <summary>
        /// TypableMap のインスタンスを初期化します。
        /// </summary>
        public TypableMap() : this(2)
        {
        }
        
        /// <summary>
        /// TypableMap のインスタンスをサイズを指定して初期化します。
        /// </summary>
        /// <param name="size">保持するサイズの元となる値。ローマ字の組み合わせ数に指定した値を累乗します。</param>
        public TypableMap(Int32 size)
        {
            _size = size;
            _num = 0;
            _map = new Dictionary<String, T>();
        }
        
        private String Generate(Int64 num)
        {
            StringBuilder sb = new StringBuilder();
            do
            {
                Int32 r = (Int32)(num % Roma.Length);
                num /= Roma.Length;
                sb.Insert(0, Roma[r]);
            } while (num > 0);

            return sb.ToString();
        }
        
        public String Add(T value)
        {
            String id = Generate(_num);
            _map[id] = value;
            _num++;
            _num %= (Int64)(Math.Pow(Roma.Length, _size));
            return id;
        }

        #region IDictionary<string,T> メンバ

        void IDictionary<String, T>.Add(string key, T value)
        {
            throw new NotSupportedException();
        }

        public bool ContainsKey(string key)
        {
            return _map.ContainsKey(key);
        }

        public ICollection<string> Keys
        {
            get { return _map.Keys; }
        }

        public bool Remove(string key)
        {
            return _map.Remove(key);
        }

        public bool TryGetValue(string key, out T value)
        {
            return _map.TryGetValue(key, out value);
        }

        public ICollection<T> Values
        {
            get { return _map.Values; }
        }

        public T this[string key]
        {
            get
            {
                return _map[key];
            }
            set
            {
                throw new NotSupportedException();
            }
        }

        #endregion

        #region ICollection<KeyValuePair<string,T>> メンバ

        void ICollection<KeyValuePair<string, T>>.Add(KeyValuePair<string, T> item)
        {
            throw new NotSupportedException();
        }

        public void Clear()
        {
            _map.Clear();
            _num = 0;
        }

        bool ICollection<KeyValuePair<string, T>>.Contains(KeyValuePair<string, T> item)
        {
            return ((IDictionary<String, T>) _map).Contains(item);
        }

        void ICollection<KeyValuePair<string, T>>.CopyTo(KeyValuePair<string, T>[] array, int arrayIndex)
        {
            ((IDictionary<String, T>)_map).CopyTo(array, arrayIndex);
        }

        public int Count
        {
            get { return _map.Count; }
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        bool ICollection<KeyValuePair<string, T>>.Remove(KeyValuePair<string, T> item)
        {
            return Remove(item.Key);
        }

        #endregion

        #region IEnumerable<KeyValuePair<string,T>> メンバ

        public IEnumerator<KeyValuePair<string, T>> GetEnumerator()
        {
            return _map.GetEnumerator();
        }

        #endregion

        #region IEnumerable メンバ

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _map.GetEnumerator();
        }

        #endregion
    }
}
