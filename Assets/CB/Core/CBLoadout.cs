using System.Collections.Generic;
using UnityEngine;

namespace CB
{
    /// <summary>
    /// The player's persistent run-scoped loadout: equipment slots (rings / artifacts / statue)
    /// and acquired blessings. Recomputes a single aggregate <see cref="CBModifier"/> whenever
    /// something is gained, which gameplay code reads instead of poking individual items.
    /// </summary>
    public class CBLoadout
    {
        public CBEquipDef[] rings;
        public List<CBEquipDef> artifacts = new List<CBEquipDef>();
        public CBEquipDef statue;
        public List<CBBlessingDef> blessings = new List<CBBlessingDef>();
        public CBModifier Mods = new CBModifier();

        public int RingSlots { get; private set; }

        public void Init(int ringSlots)
        {
            RingSlots = Mathf.Max(0, ringSlots);
            rings = new CBEquipDef[RingSlots];
            Recompute();
        }

        /// <summary>True when the item was actually equipped (false if slots were full).</summary>
        public bool AddEquipment(CBEquipDef def)
        {
            if (def == null) return false;
            switch (def.Kind)
            {
                case CBEquipKind.Ring:
                    if (rings == null) return false;
                    for (int i = 0; i < rings.Length; i++)
                    {
                        if (rings[i] == null) { rings[i] = def; Recompute(); return true; }
                    }
                    return false;
                case CBEquipKind.Artifact:
                    artifacts.Add(def); Recompute(); return true;
                case CBEquipKind.Statue:
                    if (statue != null) return false;
                    statue = def; Recompute(); return true;
            }
            return false;
        }

        public bool AddBlessing(CBBlessingDef def)
        {
            if (def == null || blessings.Contains(def)) return false;
            blessings.Add(def);
            Recompute();
            return true;
        }

        public bool HasBlessing(CBBlessingDef def) { return def != null && blessings.Contains(def); }

        public void Recompute()
        {
            Mods = new CBModifier();
            if (rings != null)
                for (int i = 0; i < rings.Length; i++)
                    if (rings[i] != null) Mods.Add(rings[i].Modifier);
            for (int i = 0; i < artifacts.Count; i++)
                if (artifacts[i] != null) Mods.Add(artifacts[i].Modifier);
            if (statue != null) Mods.Add(statue.Modifier);
            for (int i = 0; i < blessings.Count; i++)
                if (blessings[i] != null) Mods.Add(blessings[i].Modifier);
        }
    }
}
