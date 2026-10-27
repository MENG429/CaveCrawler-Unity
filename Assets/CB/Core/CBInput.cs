using UnityEngine;

namespace CB
{
    /// <summary>
    /// Edge (press) input MUST be latched inside Update and consumed inside the fixed step.
    /// Reading Input.GetKeyDown directly from a fixed step drops presses whenever a rendered
    /// frame contains zero or several fixed steps - the classic "I can only jump once" bug.
    ///
    /// CBInput is a plain static class: CBGameClock calls Latch() once at the very top of its
    /// own Update(), which makes ordering explicit instead of relying on Script Execution Order.
    ///
    /// Held state (GetKey) is safe to read at any time - those accessors are provided directly.
    /// </summary>
    public static class CBInput
    {
        // ---- held state (refreshed every Latch) ----
        public static float Horizontal;
        public static bool DownHeld;
        public static bool UpHeld;

        // ---- edge latches ----
        static bool s_jumpLatch;
        static bool s_attackLatch;
        static bool s_shootLatch;
        static bool s_interactLatch;
        static bool s_restartLatch;
        static bool s_regenLatch;
        static bool s_switchMeleeLatch;
        static bool s_switchBowLatch;
        static bool s_throwLatch;

        public static void Latch()
        {
            float h = 0f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) h -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) h += 1f;
            Horizontal = h;

            DownHeld = Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow);
            UpHeld = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow);

            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
                s_jumpLatch = true;
            if (Input.GetKeyDown(KeyCode.J) || Input.GetKeyDown(KeyCode.Z))
                s_attackLatch = true;
            if (Input.GetKeyDown(KeyCode.K) || Input.GetKeyDown(KeyCode.X))
                s_shootLatch = true;
            if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.F))
                s_interactLatch = true;
            if (Input.GetKeyDown(KeyCode.R))
                s_restartLatch = true;
            if (Input.GetKeyDown(KeyCode.N))
                s_regenLatch = true;
            if (Input.GetKeyDown(KeyCode.Q))
                s_switchMeleeLatch = true;
            if (Input.GetKeyDown(KeyCode.G))
                s_switchBowLatch = true;
            if (Input.GetKeyDown(KeyCode.L))
                s_throwLatch = true;
        }

        // ---- consumption (call from fixed step only) ----
        public static bool ConsumeJump() { return Take(ref s_jumpLatch); }
        public static bool ConsumeAttack() { return Take(ref s_attackLatch); }
        public static bool ConsumeShoot() { return Take(ref s_shootLatch); }
        public static bool ConsumeInteract() { return Take(ref s_interactLatch); }
        public static bool ConsumeRestart() { return Take(ref s_restartLatch); }
        public static bool ConsumeRegen() { return Take(ref s_regenLatch); }
        public static bool ConsumeSwitchMelee() { return Take(ref s_switchMeleeLatch); }
        public static bool ConsumeSwitchBow() { return Take(ref s_switchBowLatch); }
        public static bool ConsumeThrow() { return Take(ref s_throwLatch); }

        // ---- held accessors (safe anytime) ----
        public static bool JumpHeld => Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow);
        public static bool AttackHeld => Input.GetKey(KeyCode.J) || Input.GetKey(KeyCode.Z);
        public static bool ShootHeld => Input.GetKey(KeyCode.K) || Input.GetKey(KeyCode.X);
        public static bool InteractHeld => Input.GetKey(KeyCode.E) || Input.GetKey(KeyCode.F);

        public static void ClearAll()
        {
            s_jumpLatch = s_attackLatch = s_shootLatch = false;
            s_interactLatch = s_restartLatch = s_regenLatch = false;
            s_switchMeleeLatch = s_switchBowLatch = s_throwLatch = false;
        }

        static bool Take(ref bool flag)
        {
            if (!flag) return false;
            flag = false;
            return true;
        }
    }
}
