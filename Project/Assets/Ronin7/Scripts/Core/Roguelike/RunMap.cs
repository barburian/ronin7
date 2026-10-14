namespace Ronin7.Core
{
    /// <summary>P2-04: a run's branching map. <see cref="Next"/>[id] lists the node ids reachable from
    /// node <c>id</c> (empty only for the final Boss). Immutable after generation.</summary>
    public sealed class RunMap
    {
        public readonly RunNode[] Nodes;
        public readonly int[][] Next;
        public readonly int StartId;

        public RunMap(RunNode[] nodes, int[][] next, int startId)
        {
            Nodes = nodes;
            Next = next;
            StartId = startId;
        }

        /// <summary>Sector x 5 + row: the 0..14 depth <see cref="RunScaling"/> expects.</summary>
        public int Depth(int id) => Nodes[id].Depth;

        /// <summary>A shortest walk StartId..<paramref name="target"/> (inclusive), or null if unreachable.
        /// Used by tests, dev tools and resume checks; never per frame.</summary>
        public System.Collections.Generic.List<int> PathTo(int target)
        {
            var prev = new int[Nodes.Length];
            for (int i = 0; i < prev.Length; i++) prev[i] = -2;
            var queue = new System.Collections.Generic.Queue<int>();
            prev[StartId] = -1;
            queue.Enqueue(StartId);
            while (queue.Count > 0)
            {
                int n = queue.Dequeue();
                if (n == target) break;
                foreach (int m in Next[n])
                    if (prev[m] == -2) { prev[m] = n; queue.Enqueue(m); }
            }
            if (target < 0 || target >= Nodes.Length || prev[target] == -2) return null;
            var path = new System.Collections.Generic.List<int>();
            for (int n = target; n != -1; n = prev[n]) path.Add(n);
            path.Reverse();
            return path;
        }

        public bool HasEdge(int from, int to)
        {
            if (from < 0 || from >= Next.Length) return false;
            int[] next = Next[from];
            for (int i = 0; i < next.Length; i++) if (next[i] == to) return true;
            return false;
        }
    }
}
