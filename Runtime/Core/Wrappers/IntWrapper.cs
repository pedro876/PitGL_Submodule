using UnityEngine;

namespace PitGL
{
    public class IntWrapper : Wrapper<int>
    {
        public IntWrapper(int value) : base(value) { }

        // Operator overload for addition
        public static IntWrapper operator ++(IntWrapper a) { a.value++; return a; }
        public static IntWrapper operator --(IntWrapper a) { a.value--; return a; }
        public static IntWrapper operator +(IntWrapper a, IntWrapper b) => new IntWrapper(a.value + b.value);
        public static IntWrapper operator +(IntWrapper a, int b) => new IntWrapper(a.value + b);
        public static IntWrapper operator +(int a, IntWrapper b) => new IntWrapper(a + b.value);
        public static IntWrapper operator -(IntWrapper a, IntWrapper b) => new IntWrapper(a.value - b.value);
        public static IntWrapper operator -(IntWrapper a, int b) => new IntWrapper(a.value - b);
        public static IntWrapper operator -(int a, IntWrapper b) => new IntWrapper(a - b.value);
        public static bool operator ==(IntWrapper a, IntWrapper b) => a.value == b.value;
        public static bool operator ==(IntWrapper a, int b) => a.value == b;
        public static bool operator ==(int a, IntWrapper b) => a == b.value;
        public static bool operator !=(IntWrapper a, IntWrapper b) => a.value != b.value;
        public static bool operator !=(IntWrapper a, int b) => a.value != b;
        public static bool operator !=(int a, IntWrapper b) => a != b.value;

        public static implicit operator int(IntWrapper a) => a.value;

        public static implicit operator IntWrapper(int a) => new IntWrapper(a);

        public override bool Equals(object obj) => base.Equals(obj);
        public override int GetHashCode() => base.GetHashCode();
    }
}
