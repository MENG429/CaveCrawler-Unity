using System;
using System.Collections.Generic;
using UnityEngine;

namespace CB
{
    public interface ICBFixedStep
    {
        void FixedStep(float dt);
    }

    /// <summary>
    /// Accumulator-driven fixed step clock at 1/60.
    ///
    /// Order of operations inside Update() is load bearing:
    ///   1) CBInput.Latch()  - edge input captured once per rendered frame
    ///   2) hitstop drain    - while frozen we do NOT simulate at all
    ///   3) N x FixedStep    - gameplay consumes the latched edges
    /// </summary>
    public class CBGameClock : MonoBehaviour
    {
        public const float FixedHz = 60f;
        public const float FixedDelta = 1f / FixedHz;

        static CBGameClock s_instance;
        public static CBGameClock Instance => s_instance;

        struct Entry
        {
            public ICBFixedStep Target;
            public int Priority;
        }

        readonly List<Entry> m_entries = new List<Entry>();
        readonly List<Entry> m_scratch = new List<Entry>();
        bool m_dirty = true;

        float m_accumulator;
        float m_hitStop;
        public float TimeScale = 1f;

        public int Tick { get; private set; }
        public float SimTime { get; private set; }
        public bool Frozen => m_hitStop > 0f;

        void Awake()
        {
            s_instance = this;
        }

        void OnDestroy()
        {
            if (ReferenceEquals(s_instance, this)) s_instance = null;
        }

        public void Register(ICBFixedStep target, int priority = 0)
        {
            if (target == null) return;
            m_entries.Add(new Entry { Target = target, Priority = priority });
            m_dirty = true;
        }

        public void Unregister(ICBFixedStep target)
        {
            if (target == null) return;
            for (int i = m_entries.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(m_entries[i].Target, target)) m_entries.RemoveAt(i);
            }
        }

        /// <summary>Freeze all simulation for a short time (impact / hit feel).</summary>
        public static void HitStop(float seconds)
        {
            if (s_instance == null) return;
            if (seconds > s_instance.m_hitStop) s_instance.m_hitStop = seconds;
        }

        public static void SetTimeScale(float scale)
        {
            if (s_instance != null) s_instance.TimeScale = Mathf.Max(0f, scale);
        }

        void Update()
        {
            // 1) Edge input latch - exactly once per rendered frame.
            CBInput.Latch();

            float dt = Time.deltaTime;
            if (dt > 0.25f) dt = 0.25f; // never spiral after a hitch

            // 2) Hitstop: freeze gameplay, but still let rendering/UI run.
            if (m_hitStop > 0f)
            {
                m_hitStop -= dt;
                if (m_hitStop > 0f) return;
            }

            // 3) Fixed steps.
            float step = FixedDelta;
            m_accumulator += dt * TimeScale;

            int steps = 0;
            int maxSteps = 5;
            while (m_accumulator >= step && steps < maxSteps)
            {
                m_accumulator -= step;
                steps++;
                Tick++;
                SimTime += step;
                Dispatch(step);
            }

            if (steps >= maxSteps) m_accumulator = 0f; // drop the backlog
        }

        void Dispatch(float dt)
        {
            if (m_dirty)
            {
                m_scratch.Clear();
                m_scratch.AddRange(m_entries);
                m_scratch.Sort((a, b) => a.Priority.CompareTo(b.Priority));
                m_dirty = false;
            }
            else
            {
                m_scratch.Clear();
                m_scratch.AddRange(m_entries);
            }

            for (int i = 0; i < m_scratch.Count; i++)
            {
                var t = m_scratch[i].Target;
                if (t == null) continue;

                // A destroyed MonoBehaviour still has a live managed reference but compares
                // equal to null through Unity's overloaded operator. ReferenceEquals is the
                // only way to tell "was never a Unity object" apart from "was destroyed".
                var unityObject = t as UnityEngine.Object;
                if (!ReferenceEquals(unityObject, null) && unityObject == null) continue;

                t.FixedStep(dt);
            }
        }
    }

    /// <summary>Registration priority buckets. Lower runs first.</summary>
    public static class CBStepOrder
    {
        public const int Level = 0;
        public const int Player = 10;
        public const int Enemy = 20;
        public const int Projectile = 30;
        public const int Items = 40;
        public const int Fx = 90;
    }
}
