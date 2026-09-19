namespace Ronin7.Core
{
    /// <summary>One immutable slot in a run's 15-node map. Sector/IndexInSector are carried
    /// alongside the global Index so UI and scaling can reason about position without re-deriving
    /// it (Index / NodesPerSector) every time.</summary>
    public readonly struct RunNode
    {
        public readonly int Index;
        public readonly int Sector;
        public readonly int IndexInSector;
        public readonly RoomKind Kind;

        public RunNode(int index, int sector, int indexInSector, RoomKind kind)
        {
            Index = index;
            Sector = sector;
            IndexInSector = indexInSector;
            Kind = kind;
        }
    }
}
