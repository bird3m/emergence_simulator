using System;
using UnityEngine;

public class Traits : MonoBehaviour
{
    #region genes (0..1 except heuristic)
    // Energy, also means health
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
    [Range(0f, 1f)] public float danger_weight;

    // emergences (derived, NOT genes)
    public bool can_fly;
    public bool is_scavenging;
    public bool is_carnivore;
    public bool can_cautiousPathing;
    #endregion

    //Being a scavenger more likely to happen if there are lots of carcasses on environment
    public float carcassThresholdRatio = 0.02f;
    public float carcassBoost = 0.4f;


   //CHROMOSM FOR GENETIC ALGORITHMS
    public float[] chromosm;

    // derived from genes
    public float EffectiveMass;
    public float PowerToWeight;
    public float Speed;
    public float BaselineEnergyDrain;
    public float MoveEnergyCostPerUnit;
    public float Boldness;


    public Sprite corpseSprite;          // corpse sprite
    public float corpseNutrition = 30f;  // nutrition from corpse

    public bool hasBecomeCarcass = false;

    /// Initialize traits from chromosome and compute derived values.
    /// Time Complexity: O(1) - now uses cached lists
    /// Space Complexity: O(1)
    private void Awake()
    {
        // If chromosome exists and has values, load from it.
        if (chromosm != null && chromosm.Length >= 6)
        {
            //load genes from chromosm
            LoadFromChromosome();
        }
        //derive traits and emergences from base traits
        RecomputeAll();
        InitializeEnergy();  // Initialize energy only

    }

    /// Apply chromosome values and recompute all derived traits.
    /// Called after GA assigns new chromosome.
    /// Time Complexity: O(1) - now uses cached lists
    /// Space Complexity: O(1)
    public void ApplyChromosomeAndRecompute()
    {
        if (chromosm != null && chromosm.Length >= 6)
        {
            LoadFromChromosome();
        }

        RecomputeAll();
    }

    /// Load gene values from chromosome array.
    /// Time Complexity: O(1) - fixed 6 gene assignments
    /// Space Complexity: O(1)
    private void LoadFromChromosome()
    {
        //Clamp01 helps us crop the value between 0 and 1
        mass = Mathf.Clamp01(chromosm[0]);
        muscle_mass = Mathf.Clamp01(chromosm[1]);
        metabolic_rate = Mathf.Clamp01(chromosm[2]);
        agression = Mathf.Clamp01(chromosm[3]);
        risk_aversion = Mathf.Clamp01(chromosm[4]);

        danger_weight = Mathf.Clamp01(chromosm[5]);
    }

    /// Recompute all derived traits and emergences from genes.
    /// Time Complexity: O(1) - now uses cached lists
    /// Space Complexity: O(1)
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

    //derived from muscle_mass and mass
    /// Calculate effective mass from base mass and muscle mass.
    /// Time Complexity: O(1)
    /// Space Complexity: O(1)
    public float GetEffectiveMass()
    {
        const float kMuscle = 0.60f;
        return Mathf.Clamp01(mass + kMuscle * muscle_mass);
    }

    //derived from mass and effective_mass
    /// Calculate power-to-weight ratio.
    /// Time Complexity: O(1)
    /// Space Complexity: O(1)
    public float GetPowerToWeight(float effectiveMass)
    {
        const float eps = 1e-4f;
        return Mathf.Clamp01(muscle_mass / (effectiveMass + eps));
    }

    //derived from metabolismic rate and power_to_weight
    /// Calculate organism speed from power-to-weight and metabolic rate.
    /// Time Complexity: O(1)
    /// Space Complexity: O(1)
    public float GetSpeed(float powerToWeight)
    {
        float metabolicSpeed = metabolic_rate * 1.6f;  
        float speed = (1.75f * powerToWeight) + (1.25f * metabolicSpeed);

        return Mathf.Clamp(speed, 0.1f, 5f);
    }

    /// Initialize maximum and current energy based on mass.
    /// Time Complexity: O(1)
    /// Space Complexity: O(1)
    public void InitializeEnergy()
    {
        // Energy capacity increased propertionally with mass
        maxEnergy = 50f + (250f * Mathf.Pow(mass, 2)); 
        currentEnergy = maxEnergy * 0.8f;
    }

    /// Calculate baseline energy drain per second (idle cost).
    /// Time Complexity: O(1)
    /// Space Complexity: O(1)
    public float GetBaselineEnergyDrain()
    {
        const float minDrain = 0.3f;
        float metabolicTax = 1.2f * metabolic_rate;
        float sizeTax = 0.5f * mass;

        return minDrain + metabolicTax + sizeTax;
    }

    /// Calculate energy cost per unit of movement.
    /// Time Complexity: O(1)
    /// Space Complexity: O(1)
    public float GetMoveEnergyCostPerUnit(float effectiveMass, float speed)
    {
        const float eps = 1e-4f;
        float cost = (0.30f + 0.70f * effectiveMass) / (0.35f + speed + eps);
        return Mathf.Clamp(cost, 0.1f, 3.0f);
    }

    /// Calculate boldness as inverse of risk aversion.
    /// Time Complexity: O(1)
    /// Space Complexity: O(1)
    public float GetBoldness()
    {
        return Mathf.Clamp01(1f - risk_aversion);
    }

    //EMERGENCE CHECKS

    /// Evaluate emergent behaviors: carnivore, flying, scavenging, cautious pathing.
    /// Time Complexity: O(1) - uses cached lists instead of FindObjectsOfType
    /// Space Complexity: O(1) - no additional data structures allocated
    public void EvaluateEmergences()
    {
        //carnivorism requires high agression, powerToWeight, metabolic_rate and low agression
        is_carnivore = (agression >= 0.50f) && (PowerToWeight >= 0.60f) && (metabolic_rate >= 0.40f) && (risk_aversion <= 0.65f);

        // Flying requires high investment: low mass, high power, high metabolism
        can_fly = (EffectiveMass <= 0.45f) && (PowerToWeight >= 0.65f) && (metabolic_rate >= 0.60f);

        // Adjust scavenging tendency if many carcasses exist
        float effectiveRiskAversion = risk_aversion;
        float effectiveDangerWeight = danger_weight;
        float effectiveAgressionForScav = agression;

        try
        {
            int carcassCount = GeneticAlgorithm.CarcassCount;
            int orgCount = (GeneticAlgorithm.Organisms != null) ? GeneticAlgorithm.Organisms.Count : 1;
            float carcassRatio = (float)carcassCount / Mathf.Max(1, orgCount);

            //being scavenger is easier if many carcasses exist
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

        //being a scavenger requires high risk aversion, high danger weight and low agression
        is_scavenging = (effectiveRiskAversion >= 0.30f) && (effectiveDangerWeight >= 0.30f) && (effectiveAgressionForScav <= 0.80f);

        // if not carnivore and has high risk aversion and danger weight, A* can consider carnivores as obstacles. Safer PathFinding 
        can_cautiousPathing = (risk_aversion >= 0.30f) && (danger_weight >= 0.25f) && !is_carnivore && (agression <= 0.75f);

    }

    //ENERGY AND FITNESS

    /// Consume energy/nutrition and add to current energy with metabolic efficiency.
    /// Time Complexity: O(1)
    /// Space Complexity: O(1)
    public void Eat(float energy)
    {
       
        // Metabolic efficiency: 0.0 metabolism -> 0.5x energy, 1.0 metabolism -> 1.5x energy
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

    /// Update energy based on movement, baseline drain, and aging per frame.
    /// Time Complexity: O(1)
    /// Space Complexity: O(1)
    public void UpdateVitals(float movementDistance, float deltaTime)
    {
        const float ENERGY_REGEN_PER_SEC = 1.0f;
        const float REGEN_ENERGY_THRESHOLD = 0.85f; // must have >=85% energy to regen

        // Compute total energy drain
        float dt = Mathf.Max(1e-6f, deltaTime);

        // movementDistance is a per-frame distance; convert to units/sec
        float movementSpeed = movementDistance / dt;

        float energyDrainPerSec = BaselineEnergyDrain + (MoveEnergyCostPerUnit * movementSpeed);      

        float energyLoss = energyDrainPerSec * dt;
        // Pay from energys
        currentEnergy -= energyLoss;

        // If energy below 0, convert deficit into health damage
        if (currentEnergy < 0f)
        {
            currentEnergy = 0f;
            Die();
        }

        currentEnergy = Mathf.Clamp(currentEnergy, 0f, maxEnergy);
        
        Die();
    
    }

    /// Check if dead and convert to carcass resource.
    /// Time Complexity: O(1)
    /// Space Complexity: O(1)
    public void Die()
    {
         if (IsDead() && !hasBecomeCarcass)
            DieIntoResource();
    }

    /// Check if organism is dead (energy <= 0).
    /// Time Complexity: O(1)
    /// Space Complexity: O(1)
    public bool IsDead()
    {
        return currentEnergy <= 0f;
    }

    //Return ratio of currentEnergy over maxEnergy
    public float Fitness01()
    {
        if (maxEnergy <= 1e-4f) return 0f;
        return Mathf.Clamp01(currentEnergy / maxEnergy);
    }

    // Becomes a carcass and is available to eat if dead
    /// Convert dead organism into a carcass resource that can be consumed.
    /// Time Complexity: O(1) - assumes SourceManager.Register is O(1)
    /// Space Complexity: O(1)
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

        //change appearance
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null && corpseSprite != null)
            sr.sprite = corpseSprite;

        // Add Resource component if non-existent
        resource resource = GetComponent<resource>();
        if (resource == null)
            resource = gameObject.AddComponent<resource>();

        // Scavenging emergence becomes selectable
        resource.nutrition = currentEnergy * 3.0f + 60f;

        if (SourceManager.I != null)
            SourceManager.I.Register(GetComponent<resource>());
        
        // Register as carcass in GeneticAlgorithm counter
        GeneticAlgorithm.RegisterCarcass();
    }

    /// Unity lifecycle: unregister from SourceManager when disabled.
    /// Time Complexity: O(1) - considering SourceManager.Unregister is O(1)
    /// Space Complexity: O(1)
    private void OnDisable()
    {
        if (SourceManager.I != null)
            SourceManager.I.Unregister(GetComponent<resource>());
        
        // Unregister carcass if it was one
        if (hasBecomeCarcass)
        {
            GeneticAlgorithm.UnregisterCarcass();
        }
    }
}
