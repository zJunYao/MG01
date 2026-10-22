using MG01.Demo.Configuration;

namespace MG01.Demo.Domain
{
    public enum ItemContainer { Backpack, Reserve }

    /// <summary>零起算运行坐标。只能由本程序集的会话命令修改，外部取得的是快照副本。</summary>
    public sealed class ItemInstance
    {
        public string Key { get; }
        public string DefinitionKey { get; }
        public ItemContainer Container { get; internal set; }
        public int Column { get; internal set; }
        public int Row { get; internal set; }
        public int Slot { get; internal set; }
        public int Rotation { get; internal set; }
        internal ItemInstance(ConfigRecord row)
        {
            Key = row.Key; DefinitionKey = row.Text("item_key");
            Container = row.Text("container") == "backpack" ? ItemContainer.Backpack : ItemContainer.Reserve;
            Column = row.Integer("column") - 1; Row = row.Integer("row") - 1;
            Slot = row.Integer("slot") - 1; Rotation = row.Integer("rotation");
        }
        internal ItemInstance Copy() => (ItemInstance)MemberwiseClone();
    }

    public readonly struct GridRect
    {
        public int Column { get; }
        public int Row { get; }
        public int Width { get; }
        public int Height { get; }
        public long Right => (long)Column + Width;
        public long Bottom => (long)Row + Height;
        public GridRect(int column, int row, int width, int height)
        { Column = column; Row = row; Width = width; Height = height; }
        public bool Overlaps(GridRect other) => Column < other.Right && Right > other.Column && Row < other.Bottom && Bottom > other.Row;
        public bool SharesEdge(GridRect other) =>
            ((Right == other.Column || Column == other.Right) && Row < other.Bottom && Bottom > other.Row) ||
            ((Bottom == other.Row || Row == other.Bottom) && Column < other.Right && Right > other.Column);
        public static GridRect For(ItemInstance item, ConfigRecord definition) => At(item.Column, item.Row, item.Rotation, definition);
        public static GridRect At(int column, int row, int rotation, ConfigRecord definition) =>
            new GridRect(column, row, definition.Integer(rotation % 2 == 0 ? "width" : "height"), definition.Integer(rotation % 2 == 0 ? "height" : "width"));
    }
}
