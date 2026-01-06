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

    // independent trait
    [Range(0f, 1f)] public float camouflage;

    // emergences (derived, NOT genes)
    public bool can_fly;
    public bool can_herd;
    public bool is_scavenging;
    public bool is_carnivore;
    public bool can_camouflage;
    #endregion

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
        
        float metabolicSpeed = metabolic_rate * 1.6f;  
        float speed = (1.75f * powerToWeight) + (1.25f * metabolicSpeed);

       
        return Mathf.Clamp(speed, 0.1f, 5f); //maximum speed value
    }

    public float GetBaselineEnergyDrain()
    {
        const float minDrain = 0.6f;
        const float maxExtra = 1.05f;
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
        can_camouflage = (camouflage >= 0.60f);

        can_fly = (EffectiveMass <= 0.55f) && (PowerToWeight >= 0.70f) && (metabolic_rate >= 0.65f);
        can_herd = (risk_aversion >= 0.60f) && (danger_weight >= 0.60f);

        is_carnivore = (agression >= 0.60f) && (PowerToWeight >= 0.55f) && (metabolic_rate >= 0.45f) && (risk_aversion <= 0.70f);
        is_scavenging = (risk_aversion >= 0.55f) && (danger_weight >= 0.55f) && (agression <= 0.65f);

        // Debug: log the numeric values used for carnivore decision once so we can see why none emerge
        try
        {
            if (!loggedCarnivoreChecked)
            {
                Debug.Log(gameObject.name + ": EvaluateEmergences values -> agression=" + agression.ToString("F2") + ", PowerToWeight=" + PowerToWeight.ToString("F2") + ", metabolic_rate=" + metabolic_rate.ToString("F2") + ", risk_aversion=" + risk_aversion.ToString("F2") + " => is_carnivore=" + is_carnivore);
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
                Debug.Log(gameObject.name + ": can_fly emerged (EffectiveMass=" + EffectiveMass.ToString("F2") + ", PowerToWeight=" + PowerToWeight.ToString("F2") + ")");
                loggedCanFly = true;
            }
            else if (!can_fly && loggedCanFly)
            {
                Debug.Log(gameObject.name + ": can_fly lost");
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

    public void InitializeEnergy()
    {
        const float BASE_ENERGY = 45f;
        const float METAB_ENERGY_BONUS = 70f;

        maxEnergy = BASE_ENERGY + METAB_ENERGY_BONUS * metabolic_rate;
        currentEnergy = maxEnergy * 0.45f; // start a bit under half to encourage mild attrition

         flag = true;
        
    }

    public void Eat(float energy)
    {
        // Gain energy
        currentEnergy += energy;

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
         if (flag && IsDead() && !hasBecomeCarcass)
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
        Debug.Log("dead as hell"); 

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

        // Set nutrition value based on energy
        resource.nutrition = currentEnergy * 0.5f + 5f;

        if (SourceManager.I != null)
            SourceManager.I.Register(GetComponent<resource>());
    }

    private void OnDisable()
    {
        if (SourceManager.I != null)
            SourceManager.I.Unregister(GetComponent<resource>());
    }
}
