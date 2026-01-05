using UnityEngine;

public class Traits : MonoBehaviour
{
    #region genes (0..1 except heuristic)
    // Health state
    public float maxHealth;
    public float currentHealth;

    // physical traits
    [Range(0f, 1f)] public float mass;
    [Range(0f, 1f)] public float muscle_mass;
    [Range(0f, 1f)] public float metabolic_rate;

    // cognitive traits
    [Range(0f, 1f)] public float agression;
    [Range(0f, 1f)] public float risk_aversion;

    // heuristic traits
    // NOTE: this is your offset gene in [-1,1] (later mapped to +-bias)
    [Range(-1f, 1f)] public float heuristic;
    [Range(0f, 1f)] public float danger_weight;

    // independent trait
    [Range(0f, 1f)] public float camouflage;

    // emergences (derived, NOT genes)
    public bool can_fly;
    public bool can_herd;
    public bool is_scavenging;
    public bool is_carnivore;
    public bool can_camouflage;
    #endregion

    // chromosome (length 8)
    public float[] chromosm;

    // derived from genes
    public float EffectiveMass;
    public float PowerToWeight;
    public float Speed;
    public float BaselineEnergyDrain;
    public float MoveEnergyCostPerUnit;
    public float Boldness;

    // optional: heuristic bias scale (heuristic gene is [-1,1])
    public float maxHeuristicBias = 0.10f; // +-0.10 as you described

    private void Awake()
    {
        // If chromosome exists and has values, load from it.
        if (chromosm != null && chromosm.Length >= 8)
        {
            LoadFromChromosome();
        }

        RecomputeAll();
    }

    /// <summary>
    /// Call this after GA assigns chromosm to force recomputation.
    /// Minimal integration point for GA code.
    /// </summary>
    public void ApplyChromosomeAndRecompute()
    {
        if (chromosm != null && chromosm.Length >= 8)
        {
            LoadFromChromosome();
        }

        RecomputeAll();
    }

    private void LoadFromChromosome()
    {
        mass = Mathf.Clamp01(chromosm[0]);
        muscle_mass = Mathf.Clamp01(chromosm[1]);
        metabolic_rate = Mathf.Clamp01(chromosm[2]);
        agression = Mathf.Clamp01(chromosm[3]);
        risk_aversion = Mathf.Clamp01(chromosm[4]);

        heuristic = Mathf.Clamp(chromosm[5], -1f, 1f);

        danger_weight = Mathf.Clamp01(chromosm[6]);
        camouflage = Mathf.Clamp01(chromosm[7]);
    }

    private void RecomputeAll()
    {
        EffectiveMass = GetEffectiveMass();
        PowerToWeight = GetPowerToWeight(EffectiveMass);
        Speed = GetSpeed(PowerToWeight);
        BaselineEnergyDrain = GetBaselineEnergyDrain();
        MoveEnergyCostPerUnit = GetMoveEnergyCostPerUnit(EffectiveMass, Speed);
        Boldness = GetBoldness();

        InitializeHealth();
        EvaluateEmergences();
    }

    // ---------------------------
    // Derived equations
    // ---------------------------

    public float GetEffectiveMass()
    {
        const float kMuscle = 0.60f;
        return Mathf.Clamp01(mass + kMuscle * muscle_mass);
    }

    public float GetPowerToWeight(float effectiveMass)
    {
        const float eps = 1e-4f;
        return Mathf.Clamp01(muscle_mass / (effectiveMass + eps));
    }

    public float GetSpeed(float powerToWeight)
    {
        float s = 0.85f * powerToWeight + 0.15f * metabolic_rate;
        return Mathf.Clamp01(s);
    }

    public float GetBaselineEnergyDrain()
    {
        const float minDrain = 0.02f;
        const float maxExtra = 0.10f;
        return minDrain + maxExtra * metabolic_rate;
    }

    public float GetMoveEnergyCostPerUnit(float effectiveMass, float speed)
    {
        const float eps = 1e-4f;
        float cost = (0.30f + 0.70f * effectiveMass) / (0.35f + speed + eps);
        return Mathf.Clamp(cost, 0.1f, 3.0f);
    }

    public float GetBoldness()
    {
        return Mathf.Clamp01(1f - risk_aversion);
    }

    /// <summary>
    /// Your evolved heuristic offset (e.g. +-0.1) applied to a perfect heuristic.
    /// heuristic gene [-1,1] -> bias [-maxHeuristicBias, +maxHeuristicBias]
    /// </summary>
    public float HeuristicBias
    {
        get { return heuristic * maxHeuristicBias; }
    }

    // ---------------------------
    // Emergence checks
    // Final emergences:
    // can_herd, can_fly, is_scavenging, is_carnivore, can_camouflage
    // ---------------------------

    public void EvaluateEmergences()
    {
        // reset all first (important)
        can_fly = false;
        can_herd = false;
        is_scavenging = false;
        is_carnivore = false;
        can_camouflage = false;

        // independent
        can_camouflage = (camouflage >= 0.60f);

        // carnivore
        if ((agression >= 0.60f) &&
            (PowerToWeight >= 0.55f) &&
            (metabolic_rate >= 0.45f) &&
            (risk_aversion <= 0.70f))
        {
            is_carnivore = true;
        }

        // scavenging
        if ((risk_aversion >= 0.55f) &&
            (danger_weight >= 0.55f) &&
            (agression <= 0.65f))
        {
            is_scavenging = true;
        }

        // fly (abstract, wingless)
        if ((EffectiveMass <= 0.55f) &&
            (PowerToWeight >= 0.70f) &&
            (metabolic_rate >= 0.65f))
        {
            can_fly = true;
        }

        // herd (NOTE: ideally depends on local density too; keep minimal as you asked)
        if ((risk_aversion >= 0.60f) &&
            (danger_weight >= 0.60f))
        {
            can_herd = true;
        }
    }

    // ---------------------------
    // Health + fitness
    // ---------------------------

    public void InitializeHealth()
    {
        const float BASE_HEALTH = 50f;
        const float MASS_HEALTH_BONUS = 100f;

        maxHealth = BASE_HEALTH + MASS_HEALTH_BONUS * mass;

        // If you re-initialize every generation spawn, you want full health.
        // If you want to preserve health across recompute calls, guard it.
        currentHealth = maxHealth;
    }

    public float GetTotalEnergyDrain(float movementDistance)
    {
        float baselineDrain = BaselineEnergyDrain;
        float movementDrain = MoveEnergyCostPerUnit * movementDistance;
        return baselineDrain + movementDrain;
    }

    public void UpdateHealth(float movementDistance, float deltaTime)
    {
        const float HEALTH_DAMAGE_SCALE = 10f;

        float energyDrain = GetTotalEnergyDrain(movementDistance);
        float healthLoss = energyDrain * HEALTH_DAMAGE_SCALE * deltaTime;

        currentHealth -= healthLoss;
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
    }

    public bool IsDead()
    {
        return currentHealth <= 0f;
    }

    /// <summary>
    /// Fitness in [0..1]. GA can maximize this.
    /// Simple: remaining health fraction.
    /// </summary>
    public float Fitness01()
    {
        if (maxHealth <= 1e-4f) return 0f;
        return Mathf.Clamp01(currentHealth / maxHealth);
    }
}
