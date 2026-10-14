namespace Ronin7.Core
{
    /// <summary>One immutable node in a run's map. <see cref="Index"/> is the node id (its slot in
    /// <see cref="RunMap.Nodes"/>); <see cref="IndexInSector"/> is the row (0..4) and <see cref="Lane"/>
    /// the column within that row (P2-04 branching map). <see cref="Depth"/> = sector x 5 + row, the
    /// 0..14 value <see cref="RunScaling"/> and the summary read.</summary>
    public readonly struct RunNode
    {
        public readonly int Index;
        public readonly int Sector;
        public readonly int IndexInSector;
        public readonly RoomKind Kind;
        public readonly int Lane;
        public readonly int Depth;

        /// <summary>Linear-map form (id == depth, lane 0). Kept for tests and callers that only need a
        /// depth-positioned node.</summary>
        public RunNode(int index, int sector, int indexInSector, RoomKind kind)
            : this(index, sector, indexInSector, kind, 0, index) { }

        public RunNode(int index, int sector, int indexInSector, RoomKind kind, int lane, int depth)
        {
            Index = index;
            Sector = sector;
            IndexInSector = indexInSector;
            Kind = kind;
            Lane = lane;
            Depth = depth;
        }
    }
}
