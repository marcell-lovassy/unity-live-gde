using System.Collections.Generic;
using UnityEngine.UIElements;

namespace LiveGameDataEditor.Editor
{
    /// <summary>
    /// Resolves table field drawers in a deterministic order.
    /// </summary>
    public static class TableFieldDrawerRegistry
    {
        private static readonly List<ITableFieldDrawer> Drawers = new()
        {
            new ReferenceFieldDrawer(),
            new ColorStringFieldDrawer(),
            new AssetGuidFieldDrawer(),
            new FlagsEnumFieldDrawer(),
            new RangeNumberFieldDrawer(),
            new ListFieldDrawer(),
            new StringFieldDrawer(),
            new IntegerFieldDrawer(),
            new FloatFieldDrawer(),
            new BoolFieldDrawer(),
            new EnumFieldDrawer(),
            new UnityObjectFieldDrawer(),
            new UnsupportedFieldDrawer(),
        };

        public static VisualElement CreateCell(TableFieldContext context)
        {
            foreach (var drawer in Drawers)
            {
                if (drawer.CanDraw(context))
                {
                    return drawer.CreateCell(context);
                }
            }

            return new UnsupportedFieldDrawer().CreateCell(context);
        }
    }
}
