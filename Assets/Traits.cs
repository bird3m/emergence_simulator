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
    [Range(-1f, 1f)] public float upperSlopeHeuristic;
    [Range(-1f, 1f)] public float lowerSlopeHeuristic;
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

    public float maxEnergy;
    public float currentEnergy;


    public Sprite corpseSprite;          // kurukafa sprite'ı buraya koy
    public float corpseNutrition = 30f;  // leşteki başlangıç besin

    public int generationIndex = 0;      // GA her jenerasyonda bunu set edecek
    public int carcassExpireAfterGenerations = 2;

    private void Awake()
    {
        // If chromosome exists and has values, load from it.
        if (chromosm != null && chromosm.Length >= 9)
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
        if (chromosm != null && chromosm.Length >= 9)
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

        upperSlopeHeuristic = Mathf.Clamp(chromosm[5], -1f, 1f);
        lowerSlopeHeuristic = Mathf.Clamp(chromosm[6], -1f, 1f);

        danger_weight = Mathf.Clamp01(chromosm[7]);
        camouflage = Mathf.Clamp01(chromosm[8]);
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
        InitializeEnergy();
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
        const float minDrain = 0.1f;
        const float maxExtra = 0.2f;
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

    public void InitializeEnergy()
    {
        const float BASE_ENERGY = 30f;
        const float METAB_ENERGY_BONUS = 70f;

        maxEnergy = BASE_ENERGY + METAB_ENERGY_BONUS * metabolic_rate;
        currentEnergy = maxEnergy * 0.5f; // start half-full
    }


    public void InitializeHealth()
    {
        const float BASE_HEALTH = 50f;
        const float MASS_HEALTH_BONUS = 100f;

        maxHealth = BASE_HEALTH + MASS_HEALTH_BONUS * mass;

        // If you re-initialize every generation spawn, you want full health.
        // If you want to preserve health across recompute calls, guard it.
        currentHealth = maxHealth;
    }

    public void Eat(float energy)
    {
        // 1) gain energy
        currentEnergy += energy;

        // clamp
        currentEnergy = Mathf.Clamp(currentEnergy, 0f, maxEnergy);

        // 2) optional: tiny instant health benefit ONLY if energy is already decent
        // prevents "heal from 0 by eating once"
        const float INSTANT_HEAL_FRACTION = 0.05f; // 5% of energy converts to health
        if (currentEnergy > 0.5f * maxEnergy)
        {
            currentHealth += energy * INSTANT_HEAL_FRACTION;
            currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
        }
    }


    public float GetTotalEnergyDrain(float movementDistance)
    {
        float baselineDrain = BaselineEnergyDrain;
        float movementDrain = MoveEnergyCostPerUnit * movementDistance;
        return baselineDrain + movementDrain;
    }

    public void UpdateVitals(float movementDistance, float deltaTime)
    {
        const float STARVATION_DAMAGE_SCALE = 0.15f; // tune
        const float HEALTH_REGEN_PER_SEC = 2f;     // optional, can set 0 if you want none
        const float REGEN_ENERGY_THRESHOLD = 0.7f; // must have >=70% energy to regen

        // 1) compute total energy drain
        float energyDrainPerSec = BaselineEnergyDrain + (MoveEnergyCostPerUnit * movementDistance);
        float energyLoss = energyDrainPerSec * 10f * deltaTime; // "10f" is your old HEALTH_DAMAGE_SCALE role; now it's energy scale

        // 2) pay from energy
        currentEnergy -= energyLoss;

        // 3) if energy below 0 -> convert deficit into health damage
        if (currentEnergy < 0f)
        {
            float deficit = -currentEnergy;
            currentEnergy = 0f;

            currentHealth -= deficit * STARVATION_DAMAGE_SCALE;
        }

        // 4) optional: slow health regen if energy is high
        if (currentEnergy / Mathf.Max(maxEnergy, 1e-4f) >= REGEN_ENERGY_THRESHOLD)
        {
            currentHealth += HEALTH_REGEN_PER_SEC * deltaTime;
        }

        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
        currentEnergy = Mathf.Clamp(currentEnergy, 0f, maxEnergy);

        if(IsDead())
            Destroy(gameObject);
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

    private void DieIntoCarcass()
    {
        isDead = true;

        // 1) Hareketi durdur
        OrganismBehaviour ob = GetComponent<OrganismBehaviour>();
        if (ob != null) ob.enabled = false;

        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        // 2) Kurukafa sprite
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null && corpseSprite != null)
            sr.sprite = corpseSprite;

        // 3) Carcass component ekle + initialize et
        Carcass c = GetComponent<Carcass>();
        if (c == null)
            c = gameObject.AddComponent<Carcass>();

        float nutrition = Mathf.Max(5f, corpseNutrition + currentEnergy * 0.5f);

        // bornGeneration = generationIndex (GA'dan gelen)
        c.Initialize(generationIndex, nutrition, carcassExpireAfterGenerations);

        // 4) Tag (scavengerlar bulsun)
        gameObject.tag = "Carcass";
    }

}
