using System;
using System.Collections.Generic;
using UnityEngine;

namespace PitGL
{
    public class Hook<T>
    {
        private T value;
        private object globalKey;
        public event Action set;
        public event Action<T, T> changed;

        public T Value
        {
            get { return value; }
            set
            {
                var oldValue = this.value;
                this.value = value;
                this.set?.Invoke();
                if (!value.Equals(oldValue))
                {
                    changed?.Invoke(oldValue, value);
                }
                
            }
        }

        public Hook(T value)
        {
            this.value = value;
        }

        public Hook(T value, object globalKey)
        {
            this.value = value;
            this.globalKey = globalKey;
            if(hooks.TryGetValue(globalKey, out var hook))
            {
                hook.Value = value;
            }
            else
            {
                hooks.Add(globalKey, this);
            }
        }

        #region GLOBAL

        public static Dictionary<object, Hook<T>> hooks = new Dictionary<object, Hook<T>>();

        public static Hook<T> Get(object globalKey)
        {
            if(hooks.TryGetValue(globalKey, out var hook))
            {
                return hook;
            }
            else
            {
                return new Hook<T>(default(T), globalKey);
            }
        }

        public static bool Release(object globalKey)
        {
            if (hooks.ContainsKey(globalKey))
            {
                hooks.Remove(globalKey);
                return true;
            }
            else return false;
        }

        #endregion
    }
}
