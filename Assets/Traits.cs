using System;
using UnityEngine;

public class Traits : MonoBehaviour
{
    #region genes (0..1 except heuristic)
    // Energy state (health now based on energy)
    public float maxEnergy;
    public float currentEnergy =50f;

    // physical traits
    [Range(0f, 1f)] public float mass;
    [Range(0f, 1f)] public float muscle_mass;
    [Range(0f, 1f)] public float metabolic_rate;

    // cognitive traits
    [Range(0f, 1f)] public float agression;
    [Range(0f, 1f)] public float risk_aversion;

    // heuristic traits
    [Range(-1f, 1f)] public float upperSlopeHeuristic;
    [Range(-1f, 1f)] public float lowerSlopeHeuristic;
    [Range(0f, 1f)] public float danger_weight;

    // emergences (derived, NOT genes)
    public bool can_fly;
    public bool is_scavenging;
    public bool is_carnivore;
    public bool can_cautiousPathing;
    #endregion

    [Header("Scarcity Tuning")]
    [Tooltip("If resources per organism falls below this ratio, aggression is boosted")]
    public float scarcityThreshold = 0.5f;
    [Tooltip("Maximum additional aggression added when scarcity is extreme (0..1)")]
    public float maxAggressionBoost = 0.6f;

    [Header("Scavenger Tuning")]
    [Tooltip("Carcass/org ratio above which scavenging preference increases")]
    public float carcassThresholdRatio = 0.02f;
    [Tooltip("Amount to boost effective risk_aversion/danger_weight and lower aggression when carcasses abundant (0..1)")]
    public float carcassBoost = 0.4f;

    private bool loggedCarnivoreChecked = false;

   
    public float[] chromosm;

    // derived from genes
    public float EffectiveMass;
    public float PowerToWeight;
    public float Speed;
    public float BaselineEnergyDrain;
    public float MoveEnergyCostPerUnit;
    public float Boldness;

    // optional: heuristic bias scale (heuristic gene is [-1,1])
    public float maxHeuristicBias = 0.30f; // +-0.10 as you described

    public Sprite corpseSprite;          // corpse sprite
    public float corpseNutrition = 30f;  // nutrition from corpse

    public int carcassExpireAfterGenerations = 2;
    public bool hasBecomeCarcass = false;
    
    // Movement tracking for fitness (higher movement = more active = better)
    public float totalMovementDistance = 0f;

     public bool flag = false;
    private bool loggedCanFly = false;

    private void Awake()
    {
        // If chromosome exists and has values, load from it.
        if (chromosm != null && chromosm.Length >= 9)
        {
            LoadFromChromosome();
        }

        RecomputeAll();
        InitializeEnergy();  // Initialize energy only

    }

    public void ApplyChromosomeAndRecompute()
    {
        if (chromosm != null && chromosm.Length >= 9)
        {
            LoadFromChromosome();
        }

        // Reset one-time debug flags so emergence logs will appear after GA assigns chromosome
        loggedCanFly = false;
        loggedCarnivoreChecked = false;

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
    }

    private void RecomputeAll()
    {
        EffectiveMass = GetEffectiveMass();
        PowerToWeight = GetPowerToWeight(EffectiveMass);
        Speed = GetSpeed(PowerToWeight);
        BaselineEnergyDrain = GetBaselineEnergyDrain();
        MoveEnergyCostPerUnit = GetMoveEnergyCostPerUnit(EffectiveMass, Speed);
        Boldness = GetBoldness();

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
        // Balanced speed calculation
        float metabolicSpeed = metabolic_rate * 1.6f;  
        float speed = (1.75f * powerToWeight) + (1.25f * metabolicSpeed);

        return Mathf.Clamp(speed, 0.1f, 5f);
    }

    public void InitializeEnergy()
    {
        // Kütle arttıkça enerji kapasitesi katlanarak artsın
        maxEnergy = 50f + (250f * Mathf.Pow(mass, 2)); 
        currentEnergy = maxEnergy * 0.8f;
    }

    // 2. Metabolizmayı "Açlık Hızı" Yap, Kasları "Hız" Yap
    public float GetBaselineEnergyDrain()
    {
        // ULTRA OP CARNIVORE: Carnivores have ZERO baseline drain
        if (is_carnivore)
            return 0.05f; // ZERO baseline drain - carnivores extremely efficient
        
        // ULTRA OP FLYING: Flying organisms have 80% reduced baseline drain
        float flyingReduction = (can_fly) ? 0.2f : 1.0f;
        
        // PARETO BASKISI: Metabolizma hızı yüksek olanın "rölanti" harcaması da yüksek olur.
        // Ancak kütle arttıkça bazal tüketim verimliliği artar (Kleiber kanunu simülasyonu)
        const float minDrain = 0.3f;
        float metabolicTax = 1.2f * metabolic_rate;
        float sizeTax = 0.5f * mass;
        
        // LOW MASS PENALTY: Çok düşük mass = kırılgan vücut, daha fazla bakım gerektirir
        // Mass < 0.3 ise ekstra enerji harcar (homeostasis maliyeti)
        float lowMassPenalty = 0f;
        if (mass < 0.3f)
        {
            lowMassPenalty = (0.3f - mass) * 0.8f; // 0.1 mass -> +0.16 drain
        }

        return (minDrain + metabolicTax + sizeTax + lowMassPenalty) * flyingReduction;
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
    // can_fly, is_scavenging, is_carnivore, can_cautiousPathing
    // ---------------------------

    public void EvaluateEmergences()
    {
        // VERY LOWERED: Flying emergence thresholds very easy to achieve
        can_fly = (EffectiveMass <= 0.80f) && (PowerToWeight >= 0.35f) && (metabolic_rate >= 0.30f);

        // Increase effective aggression when resources per organism is low
        float effectiveAggression = agression;
        try
        {
            if (SourceManager.I != null)
            {
                int resourceCount = SourceManager.I.sources.Count;
                int orgCount = (GeneticAlgorithm.Organisms != null) ? GeneticAlgorithm.Organisms.Count : GameObject.FindObjectsOfType<OrganismBehaviour>().Length;
                float ratio = (float)resourceCount / Mathf.Max(1, orgCount);

                if (ratio < scarcityThreshold)
                {
                    float boost = ((scarcityThreshold - ratio) / scarcityThreshold) * maxAggressionBoost;
                    effectiveAggression = Mathf.Clamp01(agression + boost);
                }
            }
        }
        catch (Exception)
        {
            // ignore and use base aggression
        }

        // VERY LOWERED: Carnivore emergence thresholds very easy to achieve
        is_carnivore = (effectiveAggression >= 0.30f) && (PowerToWeight >= 0.30f) && (metabolic_rate >= 0.20f) && (risk_aversion <= 0.85f);
        // Adjust scavenging tendency if many carcasses exist
        float effectiveRiskAversion = risk_aversion;
        float effectiveDangerWeight = danger_weight;
        float effectiveAgressionForScav = agression;

        try
        {
            int carcassCount = GameObject.FindObjectsOfType<Carcass>().Length;
            int orgCount = (GeneticAlgorithm.Organisms != null) ? GeneticAlgorithm.Organisms.Count : GameObject.FindObjectsOfType<OrganismBehaviour>().Length;
            float carcassRatio = (float)carcassCount / Mathf.Max(1, orgCount);

            if (carcassRatio >= carcassThresholdRatio)
            {
                effectiveRiskAversion = Mathf.Clamp01(risk_aversion + carcassBoost);
                effectiveDangerWeight = Mathf.Clamp01(danger_weight + carcassBoost);
                effectiveAgressionForScav = Mathf.Clamp01(agression - carcassBoost);
            }
        }
        catch (Exception)
        {
            // ignore and use base genes
        }

        // VERY LOWERED: Scavenging emergence thresholds very easy to achieve
        is_scavenging = (effectiveRiskAversion >= 0.30f) && (effectiveDangerWeight >= 0.30f) && (effectiveAgressionForScav <= 0.80f);

        // Cautious pathing: VERY EASY threshold
        // Savunmacı strateji - yol bulma avantajı var
        can_cautiousPathing = (risk_aversion >= 0.30f) && (danger_weight >= 0.25f) && !is_carnivore && (agression <= 0.75f);

        // Debug: log the numeric values used for carnivore decision once so we can see why none emerge
        try
        {
            if (!loggedCarnivoreChecked)
            {
                //Debug.Log(gameObject.name + ": EvaluateEmergences values -> agression=" + agression.ToString("F2") + ", PowerToWeight=" + PowerToWeight.ToString("F2") + ", metabolic_rate=" + metabolic_rate.ToString("F2") + ", risk_aversion=" + risk_aversion.ToString("F2") + " => is_carnivore=" + is_carnivore);
                loggedCarnivoreChecked = true;
            }
        }
        catch (Exception)
        {
            // ignore
        }

        // Debug: log when flying emergence appears or disappears (only once per change)
        try
        {
            if (can_fly && !loggedCanFly)
            {
                //Debug.Log(gameObject.name + ": can_fly emerged (EffectiveMass=" + EffectiveMass.ToString("F2") + ", PowerToWeight=" + PowerToWeight.ToString("F2") + ")");
                loggedCanFly = true;
            }
            else if (!can_fly && loggedCanFly)
            {
                //Debug.Log(gameObject.name + ": can_fly lost");
                loggedCanFly = false;
            }
        }
        catch (Exception)
        {
            // Ignore logging errors in editor/runtime
        }
    }

    // ---------------------------
    // Energy + fitness
    // ---------------------------

    public void Eat(float energy)
    {
        // METABOLIC EFFICIENCY: Yüksek metabolizma = besinlerden daha fazla enerji
        // Yüksek metabolizma = hızlı VE verimli sindirim, besinlerden çok enerji alır
        // Düşük metabolizma = yavaş sindirim, besinlerden az enerji alır
        
        // Metabolic efficiency: 0.0 metabolizma -> 0.5x enerji, 1.0 metabolizma -> 1.5x enerji
        float metabolicEfficiency = Mathf.Lerp(0.5f, 1.5f, metabolic_rate);
        
        // ULTRA OP CARNIVORE: Carnivores extract 8x base energy from meat
        if (is_carnivore)
            metabolicEfficiency *= 8.0f; // 8x energy gain for carnivores (EXTREMELY OP)
        
        // ULTRA OP SCAVENGER: Scavengers extract 6x energy from carcasses
        if (is_scavenging)
            metabolicEfficiency *= 6.0f; // 6x energy gain for scavengers (EXTREMELY OP)
        
        float gainedEnergy = energy * metabolicEfficiency;
        currentEnergy += gainedEnergy;

        // Clamp energy to the max value
        currentEnergy = Mathf.Clamp(currentEnergy, 0f, maxEnergy); 
    }

    public void UpdateVitals(float movementDistance, float deltaTime)
    {
        const float ENERGY_REGEN_PER_SEC = 1.0f;
        const float REGEN_ENERGY_THRESHOLD = 0.85f; // must have >=85% energy to regen

        // 1) Compute total energy drain
        float dt = Mathf.Max(1e-6f, deltaTime);

        // movementDistance is a per-frame distance; convert to units/sec
        float movementSpeed = movementDistance / dt;

        float energyDrainPerSec = BaselineEnergyDrain + (MoveEnergyCostPerUnit * movementSpeed);

        // small aging/maintenance drain so there's always a gentle pressure to die over long runs
        float agingDrainPerSec = 0.02f + 0.025f * metabolic_rate;

        // use deltaTime passed from caller
        float energyLoss = (energyDrainPerSec + agingDrainPerSec) * dt;
      

        // 2) Pay from energys
        currentEnergy -= energyLoss;

        // 3) If energy below 0 -> convert deficit into health damage
        if (currentEnergy < 0f)
        {
            currentEnergy = 0f;
            Die();
        }

        //slow health regen if energy is high
        if (currentEnergy / Mathf.Max(maxEnergy, 1e-4f) >= REGEN_ENERGY_THRESHOLD)
        {
            currentEnergy += ENERGY_REGEN_PER_SEC * dt;
        }

        currentEnergy = Mathf.Clamp(currentEnergy, 0f, maxEnergy);
        
        Die();
       

    }

    public void Die()
    {
         if (IsDead() && !hasBecomeCarcass)
            DieIntoResource();
    }

    public bool IsDead()
    {
        return currentEnergy <= 0f;
    }

    /// <summary>
    /// Fitness in [0..1]. GA can maximize this.
    /// Simple: remaining energy fraction.
    /// </summary>
    public float Fitness01()
    {
        if (maxEnergy <= 1e-4f) return 0f;
        return Mathf.Clamp01(currentEnergy / maxEnergy);
    }

    public void DieIntoResource()
    {
        hasBecomeCarcass = true;

        // Stop movement (disable OrganismBehaviour)
        OrganismBehaviour ob = GetComponent<OrganismBehaviour>();
        if (ob != null) ob.enabled = false;

        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.simulated = false;  // Make it static
        }

        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null && corpseSprite != null)
            sr.sprite = corpseSprite;

        // 3) Add Resource component
        resource resource = GetComponent<resource>();
        if (resource == null)
            resource = gameObject.AddComponent<resource>();

        // ULTRA INCREASED: Carcass nutrition INSANELY high (5x + 100)
        // Scavenging emergence becomes ULTRA OP
        resource.nutrition = currentEnergy * 5.0f + 100f; // ULTRA arttırıldı: emergencelar ÇOK OP olsun

        if (SourceManager.I != null)
            SourceManager.I.Register(GetComponent<resource>());
    }

    private void OnDisable()
    {
        if (SourceManager.I != null)
            SourceManager.I.Unregister(GetComponent<resource>());
    }
}
