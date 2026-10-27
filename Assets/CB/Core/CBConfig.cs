using UnityEngine;

namespace CB
{
    /// <summary>
    /// Speed tiers used by Caveblazers weapons. Mapped to concrete fire intervals
    /// by <see cref="CBFireRates"/>. Values are the agreed replica mapping.
    /// </summary>
    public enum CBFireRate
    {
        Extreme = 0,
        Fast = 1,
        Medium = 2,
        Slow = 3,
        VerySlow = 4,
        SuperSlow = 5,
        Charge = 6
    }

    public static class CBFireRates
    {
        public static float Get(CBFireRate tier)
        {
            switch (tier)
            {
                case CBFireRate.Extreme: return 0.06f;
                case CBFireRate.Fast: return 0.18f;
                case CBFireRate.Medium: return 0.35f;
                case CBFireRate.Slow: return 0.60f;
                case CBFireRate.VerySlow: return 0.95f;
                case CBFireRate.SuperSlow: return 1.40f;
                case CBFireRate.Charge: return 1.80f;
                default: return 0.35f;
            }
        }
    }

    /// <summary>
    /// Single source of truth for every tunable number in the replica.
    /// Created at runtime by CBBootstrap via CreateInstance so the whole game
    /// can be assembled without any pre-authored .asset file.
    /// </summary>
    [CreateAssetMenu(menuName = "CB/Config", fileName = "CBConfig")]
    public class CBConfig : ScriptableObject
    {
        // ---------------- Simulation ----------------
        [Header("Simulation")]
        public float fixedStep = 1f / 60f;
        public int maxStepsPerFrame = 5;

        // ---------------- Player locomotion ----------------
        [Header("Player - Locomotion")]
        public float moveSpeed = 8.0f;
        public float groundAccel = 100f;
        public float groundFriction = 80f;
        public float airAccel = 55f;
        public float airFriction = 10f;
        public float airControlMultiplier = 0.78f;
        public float playerWidth = 0.80f;
        public float playerHeight = 1.60f;

        // ---------------- Player jump / gravity ----------------
        [Header("Player - Jump & Gravity")]
        public float gravity = 46f;
        public float fallGravityMultiplier = 1.35f;
        public float maxFallSpeed = 28f;
        public float jumpSpeed = 16.6f;
        public float doubleJumpSpeed = 14.6f;
        public float jumpCutMultiplier = 0.45f;
        public float coyoteTime = 0.10f;
        public float jumpBufferTime = 0.12f;
        public int maxJumps = 2;
        public float landRecovery = 0.06f;

        // ---------------- Player dive attack ----------------
        [Header("Player - Dive Attack")]
        public float diveSpeed = 30f;
        public float diveAirDrift = 5.0f;
        public float diveGravity = 55f;
        public float diveMinTime = 0.08f;
        public float diveLandRadius = 2.30f;
        public int diveLandDamage = 12;
        public float diveLandKnockback = 16f;
        public float diveLandHitstop = 0.07f;
        public float diveCooldown = 0.25f;

        // ---------------- Player health ----------------
        [Header("Player - Health")]
        public int maxHP = 100;
        public float invulnerableTime = 0.90f;
        public float hurtKnockback = 7f;
        public float hurtHitstop = 0.05f;
        public float hurtLift = 3.5f;
        public int potionHeal = 35;
        public float pickupMagnetRadius = 1.6f;
        public float pickupMagnetForce = 22f;

        // ---------------- Melee ----------------
        [Header("Combat - Melee")]
        public float meleeHitstop = 0.05f;
        public float heavyHitstop = 0.09f;
        public float comboResetTime = 0.45f;
        public float meleeCancelToJumpTime = 0.10f;
        public float[] comboWindup = { 0.06f, 0.06f, 0.10f };
        public float[] comboActive = { 0.08f, 0.08f, 0.13f };
        public float[] comboRecover = { 0.12f, 0.14f, 0.26f };
        public float[] comboDamageMul = { 1.0f, 1.0f, 1.8f };
        public float[] comboKnockMul = { 1.0f, 1.0f, 2.5f };
        public float meleeReachX = 1.15f;
        public float meleeHeight = 1.35f;
        public float meleeOffsetY = 0.05f;

        // ---------------- Ranged ----------------
        [Header("Combat - Ranged")]
        public float arrowSpeed = 30f;
        public float arrowGravity = 18f;
        public float arrowLife = 4.0f;
        public float arrowRadius = 0.16f;
        public float arrowKnockback = 5f;
        public float bowMuzzleOffset = 0.55f;
        public float aimAssistVertical = 0.10f;

        // ---------------- Status effects ----------------
        [Header("Combat - Status Effects")]
        public int burnDps = 4;
        public float burnDuration = 3.0f;
        public int poisonDps = 3;
        public float poisonDuration = 4.0f;
        public float slowFactor = 0.45f;
        public float slowDuration = 2.0f;
        public float statusTickInterval = 0.5f;

        // ---------------- Explosions ----------------
        [Header("Combat - Explosions")]
        public float explosionRadius = 2.4f;
        public int explosionDamage = 14;
        public float explosionKnockback = 18f;
        public float explosionSelfKnockback = 6f;
        public float screenShakeExplosion = 0.35f;
        public float screenShakeHit = 0.12f;
        public float screenShakeDive = 0.45f;

        // ---------------- Enemies ----------------
        [Header("Enemy - Melee Chaser")]
        public float chaserSpeed = 4.6f;
        public float chaserJumpSpeed = 14.0f;
        public float chaserAccel = 40f;
        public int chaserHP = 30;
        public int chaserDamage = 9;
        public float chaserAttackRange = 1.25f;
        public float chaserWindup = 0.25f;
        public float chaserActive = 0.12f;
        public float chaserRecover = 0.35f;
        public float chaserKnockback = 7f;
        public float chaserWidth = 0.85f;
        public float chaserHeight = 1.35f;

        [Header("Enemy - Flying Bat")]
        public float batSpeed = 6.2f;
        public float batAccel = 22f;
        public int batHP = 18;
        public int batDamage = 7;
        public float batHoverRadius = 5.0f;
        public float batHoverBob = 0.9f;
        public float batDiveTelegraph = 0.40f;
        public float batDiveSpeed = 15f;
        public float batDiveDuration = 0.55f;
        public float batRecover = 0.60f;
        public float batRepathInterval = 0.35f;
        public float batWidth = 0.95f;
        public float batHeight = 0.70f;

        [Header("Enemy - Ranged Archer")]
        public float archerSpeed = 3.8f;
        public float archerAccel = 35f;
        public int archerHP = 24;
        public int archerDamage = 6;
        public float archerMinRange = 7.0f;
        public float archerMaxRange = 11.0f;
        public float archerFireInterval = 1.30f;
        public float archerWindup = 0.35f;
        public float archerArrowSpeed = 22f;
        public float archerArrowGravity = 16f;
        public float archerKnockback = 6f;
        public float archerWidth = 0.80f;
        public float archerHeight = 1.50f;

        [Header("Enemy - Common")]
        public float enemyHitInvuln = 0.12f;
        public float enemyRepathInterval = 0.30f;
        public int pathfinderMaxNodes = 4000;
        public float enemyActivationRange = 26f;
        public int coinDropMin = 1;
        public int coinDropMax = 4;
        public float potionDropChance = 0.18f;
        public float weaponDropChance = 0.10f;

        // ---------------- Level generation ----------------
        [Header("Level Generation")]
        public int roomCount = 5;
        public int roomWidth = 38;
        public int roomHeight = 22;
        public float initialWallChance = 0.46f;
        public int cellularIterations = 4;
        public int cellularWallThreshold = 5;
        public int corridorHeight = 4;
        public float accentTileChance = 0.06f;
        public int chestsPerRoom = 1;
        public int pedestalsPerRoom = 1;
        public int enemiesPerRoomMin = 2;
        public int enemiesPerRoomMax = 4;
        public int maxGenerationAttempts = 6;

        // ---------------- Camera ----------------
        [Header("Camera")]
        public float cameraOrthoSize = 12.0f;
        public float cameraFollowLerp = 8.0f;
        public float cameraLookAhead = 2.2f;
        public float cameraLookAheadLerp = 3.0f;
        public float cameraShakeDecay = 6.0f;

        // ---------------- Debug ----------------
        [Header("Debug")]
        public bool showDebugOverlay = true;
        public bool regenerateOnStart = false;
        public int randomSeed = 20261021;
    }
}
