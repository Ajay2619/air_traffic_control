using System.Collections.Generic;
using UnityEngine;

namespace ClearToLand
{
    /// <summary>
    /// Decides when, what and where aircraft enter: weighted type picks from the map's spawn table, edge points away
    /// from recent spawns and existing traffic, an edge "!" warning lead time, and a difficulty ramp over play time.
    /// Ticks only while Playing (scaled time). Deterministic for a given seed (System.Random only).
    /// </summary>
    public sealed class SpawnDirector : MonoBehaviour
    {
        private const float FirstSpawnDelay = 1f;
        private const float RecentEdgeMemory = 8f;
        private const float RetryDelay = 0.5f;
        private const float NoTypeRetryDelay = 1f;
        private const float EmptySkyMaxDelay = 1.5f;
        private const float AvoidDistance = 2.5f;
        private const float CornerAvoidDistance = 2.2f;
        private const int EdgeAttempts = 12;
        private const float SpawnOutsideExtra = 0.6f;
        private const float TargetInsetX = 3f;
        private const float TargetInsetY = 2f;

        private struct PendingSpawn
        {
            public AircraftType Type;
            public Vector2 Edge;
            public Vector2 Normal;
            public float TimeLeft;
            public EdgeWarning Warning;
        }

        private struct RecentEdge
        {
            public Vector2 Point;
            public float Time;
        }

        private readonly List<PendingSpawn> _pending = new List<PendingSpawn>(8);
        private readonly List<RecentEdge> _recent = new List<RecentEdge>(16);
        private readonly List<Vector2> _avoid = new List<Vector2>(32);
        private readonly List<float> _weights = new List<float>(8);
        private readonly List<AircraftType> _weightTypes = new List<AircraftType>(8);

        private GameManager _gm;
        private System.Random _rng = new System.Random(0);
        private float _timer = FirstSpawnDelay;
        private bool _warnedNoTypes;

        /// <summary>Seconds (scaled) until the next spawn attempt.</summary>
        public float NextSpawnIn => _timer;

        /// <summary>Spawns announced by an edge warning that have not entered yet.</summary>
        public int PendingCount => _pending.Count;

        /// <summary>Binds to the game and seeds the RNG. The first spawn comes after 1 s of play.</summary>
        public void Init(GameManager gm, int seed)
        {
            if (_gm != null) _gm.StateChanged -= OnStateChanged;
            _gm = gm;
            _rng = new System.Random(seed);
            _timer = FirstSpawnDelay;
            _pending.Clear();
            _recent.Clear();
            if (_gm != null) _gm.StateChanged += OnStateChanged;
        }

        private void OnDestroy()
        {
            if (_gm != null) _gm.StateChanged -= OnStateChanged;
        }

        private void OnStateChanged(GameState state)
        {
            if (state != GameState.CrashSlowMo && state != GameState.GameOver) return;
            // The round is over: announced aircraft will never arrive.
            for (int i = 0; i < _pending.Count; i++)
            {
                EdgeWarning w = _pending[i].Warning;
                if (w != null) w.Dismiss();
            }
            _pending.Clear();
        }

        private void Update()
        {
            if (_gm == null || _gm.State != GameState.Playing) return;
            MapDefinition map = _gm.Map;
            if (map == null) return;

            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            float now = _gm.ElapsedPlayTime;

            PruneRecent(now);
            TickPending(dt);

            int airborne = CountAirborne();
            if (airborne == 0 && _pending.Count == 0 && _timer > EmptySkyMaxDelay) _timer = EmptySkyMaxDelay;

            _timer -= dt;
            if (_timer > 0f) return;

            int limit = SpawnMath.MaxAircraft(now, map.initialMaxAircraft, map.maxAircraftCap, map.maxAircraftStepSeconds);
            if (airborne + _pending.Count >= limit)
            {
                _timer = 0f; // wait for a free slot, then spawn right away
                return;
            }

            AircraftType type = PickType(map, now);
            if (type == null)
            {
                _timer = NoTypeRetryDelay;
                return;
            }

            if (!TryAnnounce(type, now))
            {
                _timer = RetryDelay;
                return;
            }

            float interval = SpawnMath.SpawnInterval(now, map.initialSpawnInterval, map.minSpawnInterval, map.rampDuration);
            _timer = Mathf.Max(0.1f, interval) * Mathf.Lerp(0.8f, 1.2f, (float)_rng.NextDouble());
        }

        private int CountAirborne()
        {
            IReadOnlyList<Aircraft> list = _gm.ActiveAircraft;
            int count = 0;
            for (int i = 0; i < list.Count; i++)
            {
                Aircraft a = list[i];
                if (a != null && a.IsAirborne) count++;
            }
            return count;
        }

        private void PruneRecent(float now)
        {
            for (int i = _recent.Count - 1; i >= 0; i--)
            {
                if (now - _recent[i].Time > RecentEdgeMemory) _recent.RemoveAt(i);
            }
        }

        private AircraftType PickType(MapDefinition map, float now)
        {
            _weights.Clear();
            _weightTypes.Clear();
            List<SpawnEntry> table = map.spawnTable;
            bool anyType = false;
            if (table != null)
            {
                for (int i = 0; i < table.Count; i++)
                {
                    SpawnEntry e = table[i];
                    if (e == null || e.type == null) continue;
                    anyType = true;
                    _weightTypes.Add(e.type);
                    _weights.Add(e.unlockTime <= now ? e.weight : 0f);
                }
            }

            if (!anyType && !_warnedNoTypes)
            {
                _warnedNoTypes = true;
                Debug.LogWarning("[ClearToLand] SpawnDirector: map '" + map.id + "' has no spawnable aircraft in its spawn table.");
            }

            int index = SpawnMath.PickWeighted(_weights, _rng.NextDouble());
            return index >= 0 && index < _weightTypes.Count ? _weightTypes[index] : null;
        }

        private bool TryAnnounce(AircraftType type, float now)
        {
            _avoid.Clear();
            for (int i = 0; i < _recent.Count; i++) _avoid.Add(_recent[i].Point);
            IReadOnlyList<Aircraft> list = _gm.ActiveAircraft;
            for (int i = 0; i < list.Count; i++)
            {
                Aircraft a = list[i];
                if (a != null) _avoid.Add(a.Position);
            }

            Rect view = _gm.ViewRect;
            if (!SpawnMath.TryPickEdgePoint(view, _rng, _avoid, AvoidDistance, CornerAvoidDistance, EdgeAttempts,
                    out Vector2 edge, out Vector2 normal))
                return false;

            EdgeWarning warning = EdgeWarning.Show(edge, normal, type.Color, _gm);
            AudioManager.Instance.Play(Sfx.Alert);

            GameConfig cfg = _gm.Config;
            _pending.Add(new PendingSpawn
            {
                Type = type,
                Edge = edge,
                Normal = normal,
                TimeLeft = cfg != null ? Mathf.Max(0f, cfg.spawnWarningLeadTime) : 2.2f,
                Warning = warning,
            });
            _recent.Add(new RecentEdge { Point = edge, Time = now });
            return true;
        }

        private void TickPending(float dt)
        {
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                PendingSpawn p = _pending[i];
                p.TimeLeft -= dt;
                if (p.TimeLeft > 0f)
                {
                    _pending[i] = p;
                    continue;
                }

                _pending.RemoveAt(i);
                Aircraft aircraft = SpawnNow(p);
                if (p.Warning != null)
                {
                    if (aircraft != null) p.Warning.TrackAircraft(aircraft);
                    else p.Warning.Dismiss();
                }
            }
        }

        private Aircraft SpawnNow(PendingSpawn p)
        {
            if (p.Type == null) return null;

            Rect view = _gm.ViewRect;
            // The view may have been resized (window / fullscreen) since the warning was announced.
            Vector2 edge = GameplayUtil.ProjectOntoEdge(view, p.Edge, p.Normal);
            Vector2 spawnPos = edge + p.Normal * (p.Type.radius + SpawnOutsideExtra);
            float w = Mathf.Max(0f, view.width - TargetInsetX * 2f);
            float h = Mathf.Max(0f, view.height - TargetInsetY * 2f);
            Rect inner = new Rect(view.center.x - w * 0.5f, view.center.y - h * 0.5f, w, h);
            Vector2 target = SpawnMath.RandomPointInRect(inner, _rng);

            Vector2 heading = target - spawnPos;
            heading = heading.sqrMagnitude > 1e-6f ? heading.normalized : -p.Normal;
            return Aircraft.Spawn(p.Type, spawnPos, heading, _gm);
        }
    }
}
