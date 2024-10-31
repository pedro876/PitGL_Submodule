using UnityEngine;

namespace PitGL
{
    public class Wrapper<T>
    {
        public T value;
        public Wrapper(T value) => this.value = value;

        public override int GetHashCode() => value.GetHashCode();
        public override string ToString() => value.ToString();

        public override bool Equals(object obj)
        {
            if (obj is Wrapper<T> wrapper) return value.Equals(wrapper.value);
            else if (obj is T i) return value.Equals(i);
            else return false;
        }
    }
}
