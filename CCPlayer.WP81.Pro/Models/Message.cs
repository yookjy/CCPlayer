using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CCPlayer.WP81.Models
{
    public class Message
    {
        public string Key { get; set; }

        public object Value { get; set; }

        public Action<object> Action { get; set; }

        public Message(string key, object value)
        {
            this.Key = key;
            this.Value = value;
        }

        public Message(string key)
        {
            this.Key = key;
        }

        public Message(string key, Action<object> action)
        {
            this.Key = key;
            this.Action = action;
        }

        public T GetValue<T>()
        {
            if (Value != null)
            {
                return (T)Value;
            }
            return default(T);
        }

        public bool IsValueTypeOf<T>()
        {
            return (Value is T);
        }
    }
}
