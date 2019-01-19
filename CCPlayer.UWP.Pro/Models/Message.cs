using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation.Collections;

namespace CCPlayer.UWP.Models
{
    public class Message<T> : Message
    {
        public Action<T> Action { get; set; }

        public Message(Action<T> action) : base()
        {
            this.Action = action;
        }

        public new Message<T> Add(string key, object value)
        {
            base.Add(key, value);
            return this;
        }
    }

    public class Message 
    {
        private string _DefaultKey = "_DeFaulT_Key_";

        private PropertySet _PropertySet;

        
        public Message(string key, object value)
        {
            _PropertySet = new PropertySet();
            _PropertySet[key] = value;
        }

        public Message()
        {
            _PropertySet = new PropertySet();
        }

        public Message Add(string key, object value)
        {
            _PropertySet[key] = value;
            return this;
        }

        public Message (object value)
        {
            _PropertySet = new PropertySet();
            _PropertySet[_DefaultKey] = value;
        }

        public T GetValue<T>()
        {
            return GetValue<T>(_DefaultKey);
        }

        public T GetValue<T>(string key)
        {
            object value = null;
            _PropertySet.TryGetValue(key, out value);

            if (value is T)
            {
                return (T)value;
            }
            return default(T);
        }

        public bool IsValueType<T>(string key)
        {
            object value = null;
            _PropertySet.TryGetValue(key, out value);

            if (value is T)
            {
                return true;
            }
            return false;
        }

        public bool ContainsKey(string key)
        {
            return _PropertySet.ContainsKey(key);
        }
    }
}
