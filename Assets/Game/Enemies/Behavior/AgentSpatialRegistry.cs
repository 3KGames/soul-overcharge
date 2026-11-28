using Game.Common.Runtime;
using UnityEngine;

namespace Game.Enemies.Behavior
{
    /// <summary>
    /// Zero-alloc реестр активных машин (враги, трафик, игрок) в координатах дороги (Path, s, lateralOffset).
    /// Исключает selfId и всегда возвращает БЛИЖАЙШЕЕ препятствие впереди.
    /// </summary>
    public sealed class AgentSpatialRegistry
    {
        private const int MaxAgents = 256;

        private struct Entry
        {
            public IRoadPath Path;
            public float S;
            public float LateralOffset;
            public bool Active;
        }

        private readonly Entry[] _entries = new Entry[MaxAgents];
        private int _highWaterMark;

        public int Register(IRoadPath path, float s, float lateralOffset)
        {
            for (int i = 0; i < MaxAgents; i++)
            {
                if (!_entries[i].Active)
                {
                    _entries[i] = new Entry { Path = path, S = s, LateralOffset = lateralOffset, Active = true };
                    if (i >= _highWaterMark) _highWaterMark = i + 1;
                    return i;
                }
            }

            return -1;
        }

        public void Unregister(int agentId)
        {
            if (agentId < 0 || agentId >= MaxAgents) return;
            _entries[agentId].Active = false;
            _entries[agentId].Path = null;
        }

        public void Update(int agentId, IRoadPath path, float s, float lateralOffset)
        {
            if (agentId < 0 || agentId >= MaxAgents || !_entries[agentId].Active) return;
            _entries[agentId].Path = path;
            _entries[agentId].S = s;
            _entries[agentId].LateralOffset = lateralOffset;
        }

        public bool TryFindNearestAhead(
            int selfId,
            IRoadPath path,
            float myS,
            float myLateral,
            float lookaheadMeters,
            float bandHalfWidth,
            out float obstacleDistanceAhead,
            out float obstacleLateralDelta)
        {
            obstacleDistanceAhead = float.MaxValue;
            obstacleLateralDelta = 0f;
            if (path == null) return false;

            bool found = false;
            for (int i = 0; i < _highWaterMark; i++)
            {
                if (i == selfId) continue;
                ref readonly Entry e = ref _entries[i];
                if (!e.Active || e.Path != path) continue;

                float ds = e.S - myS;
                if (ds <= 0.5f || ds > lookaheadMeters || ds >= obstacleDistanceAhead) continue;

                float latDelta = e.LateralOffset - myLateral;
                if (Mathf.Abs(latDelta) <= bandHalfWidth)
                {
                    obstacleDistanceAhead = ds;
                    obstacleLateralDelta = latDelta;
                    found = true;
                }
            }

            return found;
        }
    }
}