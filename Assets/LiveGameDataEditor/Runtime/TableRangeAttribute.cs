using System;

namespace LiveGameDataEditor
{
    /// <summary>
    ///     Renders an int or float field with constrained range editing.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class TableRangeAttribute : Attribute
    {
        public TableRangeAttribute(float min, float max)
        {
            Min = min;
            Max = max;
        }

        public float Min { get; }
        public float Max { get; }
    }
}