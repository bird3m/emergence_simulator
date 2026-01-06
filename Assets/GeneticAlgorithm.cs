using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;


/* Simple GA that evolves Traits.chromosm (9 genes) and reads fitness directly from Traits.Fitness01().
** No extra Stats script needed.
*/
public class GeneticAlgorithm : MonoBehaviour
{
    [Header("Prefab")]
    public GameObject organismPrefab;
    
    [Header("Spawn")]
    public Transform[] spawnPoints;

    [Header("GA Settings")]
    public int populationSize = 15;
    public int eliteCount = 6;
    public float crossoverRate = 0.90f;
    public float mutationRate = 0.08f;
    public float mutationStep = 0.5f;
    
    [Header("Emergence Bonuses")]
    public float flyingFitnessBonus = 0.15f;
    public float carnivoreBonus = 0.10f;

    [Header("Evaluation")]
    public float evaluationSeconds = 20f;

    private List<Individual> population = new List<Individual>();
    private List<GameObject> spawned = new List<GameObject>();

    // Central registry of active organisms to avoid FindObjectsOfType calls.
    public static List<OrganismBehaviour> Organisms = new List<OrganismBehaviour>();

    public static void RegisterOrganism(OrganismBehaviour ob)
    {
        if (ob == null) return;
        if (!Organisms.Contains(ob)) Organisms.Add(ob);
    }

    public static void UnregisterOrganism(OrganismBehaviour ob)
    {
        if (ob == null) return;
        Organisms.Remove(ob);
    }

    private float timer = 0f;
    private int generation = 0;
    private System.Random rng = new System.Random();

    private global::Terrain terrain;
    [Header("UI")]
    public TMP_Text upperAvgText;
    public TMP_Text lowerAvgText;

    [Header("Plot")]
    public LineGraph upperGraph;
    public LineGraph lowerGraph;

    private List<float> upperHistory = new List<float>();
    private List<float> lowerHistory = new List<float>();
    public SourceSpawner spawner;
    public float alpha = 0.5f; // Blend factor for BLX-α Crossover

    private void Start()
    {
       terrain = FindObjectOfType<global::Terrain>();
        if (terrain == null)
        {
            // Debug log removed
            enabled = false;
            return;
        }

        InitPopulation();
        SpawnPopulation();
        UpdateHeuristicAveragesUI();
    }

    private void UpdateHeuristicAveragesUI()
    {
        if (population == null || population.Count == 0) return;

        double sumUpper = 0.0;
        double sumLower = 0.0;

        for (int i = 0; i < population.Count; i++)
        {
            float[] c = population[i].chrom;
            if (c == null || c.Length < 7) continue;

            sumUpper += c[5];
            sumLower += c[6];
        }

        float avgUpper = (float)(sumUpper / population.Count);
        float avgLower = (float)(sumLower / population.Count);

        if (upperAvgText != null) upperAvgText.text = $"Upper Heuristic Avg: {avgUpper:F3}";
        if (lowerAvgText != null) lowerAvgText.text = $"Lower Heuristic Avg: {avgLower:F3}";

        // history
        upperHistory.Add(avgUpper);
        lowerHistory.Add(avgLower);

        // refresh plots
        if (upperGraph != null)
        {
            upperGraph.seriesA = upperHistory;
            upperGraph.SetVerticesDirty();
        }
        if (lowerGraph != null)
        {
            lowerGraph.seriesB = lowerHistory;
            lowerGraph.SetVerticesDirty();
        }
    }


    private void Update()
    {
        timer += Time.deltaTime;

        if (timer >= evaluationSeconds)
        {
            timer = 0f;

            // 1) Read fitness from Traits
            EvaluateFitnessFromWorld();

           /* // LOG BEST of CURRENT generation (before NextGeneration resets fitness)
            Individual bestOld = GetBest(population);
            Debug.Log($"Generation {generation} | best fitness: {bestOld.fitness:F3}");*/


            float avgFitness = GetAverageFitness(population);
            // debug log removed
            
            population = NextGeneration(population);
            generation++;

            UpdateHeuristicAveragesUI();

            // 3) Reset world
            DestroySpawned();
            
            // Reset all resources for next generation
            if (spawner != null)
            {
                spawner.ResetAllResources();
            }
            
            SpawnPopulation();
        }

        foreach (Carcass c in FindObjectsOfType<Carcass>())
        {
            c.TickGeneration(generation);
        }

    }


    // ----------------------------
    // Init / Spawn / Destroy
    // ----------------------------

    private void InitPopulation()
    {
        population.Clear();

        for (int i = 0; i < populationSize; i++)
        {
            Individual ind = new Individual();
            ind.chrom = RandomChromosome();
            ind.fitness = 0f;
            population.Add(ind);
        }
    }

    private void SpawnPopulation()
    {
        spawned.Clear();

        for (int i = 0; i < population.Count; i++)
        {
            Vector3 pos = GetSpawnPos(i);
            GameObject go = Instantiate(organismPrefab, pos, Quaternion.identity);
            go.GetComponent<OrganismBehaviour>().spawner = spawner;
            spawned.Add(go);

            Traits t = go.GetComponent<Traits>();
            if (t == null)
            {
                // debug log removed
                continue;
            }

            // Assign chromosome
            t.chromosm = new float[population[i].chrom.Length];
            Array.Copy(population[i].chrom, t.chromosm, population[i].chrom.Length);

            // IMPORTANT: recompute after GA sets chromosm
            t.ApplyChromosomeAndRecompute();
        }
    }

    private Vector3 GetSpawnPos(int i)
    {
        // If you still want to support manual spawn points, keep this block
        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            return spawnPoints[i % spawnPoints.Length].position;
        }

        // Terrain-bounded random cell spawn
        int x = UnityEngine.Random.Range(0, terrain.width);
        int y = UnityEngine.Random.Range(0, terrain.height);

        Vector3 p = terrain.CellCenterWorld(x, y);

        // IMPORTANT: If your sim is 2D birdview on XY, use (x,y)
        // If it is top-down on XZ, keep y as z.
        // Most likely you want XZ:
        return new Vector3(p.x, p.y, p.z);
    }


    private void DestroySpawned()
    {
        for (int i = 0; i < spawned.Count; i++)
        {
            if (spawned[i] != null) Destroy(spawned[i]);
        }
        spawned.Clear();
    }

    // ----------------------------
    // Fitness
    // ----------------------------

    private void EvaluateFitnessFromWorld()
    {
        for (int i = 0; i < population.Count; i++)
        {
            GameObject go = spawned[i];
            if (go == null)
            {
                population[i].fitness = 0f;
                continue;
            }

            Traits t = go.GetComponent<Traits>();
            if (t == null)
            {
                population[i].fitness = 0f;
                continue;
            }

            // Base fitness from health/energy
            float baseFitness = t.Fitness01();
            
            // Add bonuses for beneficial emergences
            float emergenceBonus = 0f;
            if (t.can_fly) emergenceBonus += flyingFitnessBonus;
            if (t.is_carnivore) emergenceBonus += carnivoreBonus;
          
            
            // Final fitness (clamped to [0,1])
            population[i].fitness = Mathf.Clamp01(baseFitness + emergenceBonus);
        }
    }

    // ----------------------------
    // GA Core
    // ----------------------------

    private List<Individual> NextGeneration(List<Individual> oldPop)
    {
        // Sort by fitness (desc)
        oldPop.Sort((a, b) => b.fitness.CompareTo(a.fitness));

        List<Individual> newPop = new List<Individual>();

        // 1) Elites
        for (int i = 0; i < eliteCount; i++)
        {
            Individual e = new Individual();
            e.chrom = (float[])oldPop[i].chrom.Clone();
            e.fitness = 0f;
            newPop.Add(e);
        }

        // 2) Children
        while (newPop.Count < populationSize)
        {
            Individual p1 = TournamentSelect(oldPop, 3);
            Individual p2 = TournamentSelect(oldPop, 3);

            float[] child = (float[])p1.chrom.Clone();

            if (Rand01() < crossoverRate)
            {
                child = BLXCrossover(p1.chrom, p2.chrom, alpha);  // Use BLX-α Crossover
            }

            Mutate(child);

            Individual c = new Individual();
            c.chrom = child;
            c.fitness = 0f;
            newPop.Add(c);
        }

        return newPop;
    }

    private Individual TournamentSelect(List<Individual> pop, int k)
    {
        Individual best = null;

        for (int i = 0; i < k; i++)
        {
            int idx = rng.Next(0, pop.Count);
            Individual cand = pop[idx];

            if (best == null || cand.fitness > best.fitness)
            {
                best = cand;
            }
        }

        return best;
    }

    private float[] BLXCrossover(float[] parent1, float[] parent2, float alpha = 0.3f)
    {
        float[] child = new float[parent1.Length];

        for (int i = 0; i < parent1.Length; i++)
        {
            float lower = Mathf.Min(parent1[i], parent2[i]);
            float upper = Mathf.Max(parent1[i], parent2[i]);

            // Blend gene within the bounds with random factor
            child[i] = lower + alpha * (upper - lower) * UnityEngine.Random.Range(0f, 1f);

            // Clamp based on gene index - heuristics are [-1,1], others are [0,1]
            child[i] = ClampGene(i, child[i]);
        }

        return child;
    }



    private void Mutate(float[] chrom, float mutationRate = 0.08f, float mutationStep = 0.15f)
    {
        for (int i = 0; i < chrom.Length; i++)
        {
            if (Rand01() < mutationRate)
            {
                // Apply a random mutation step
                float delta = UnityEngine.Random.Range(-mutationStep, mutationStep);
                chrom[i] += delta;

                // Clamp based on gene index - heuristics are [-1,1], others are [0,1]
                chrom[i] = ClampGene(i, chrom[i]);
            }
        }
    }



    // ----------------------------
    // Chromosome helpers
    // ----------------------------

    private float[] RandomChromosome()
    {
        float[] c = new float[9];

        // [0..1]
        c[0] = Rand01(); // mass
        c[1] = Rand01(); // muscle_mass
        c[2] = Rand01(); // metabolic_rate
        c[3] = Rand01(); // agression
        c[4] = Rand01(); // risk_aversion

        // heuristic [-1..1]
        c[5] = UnityEngine.Random.Range(-1f, 1f); // upperSlopeHeuristic
        c[6] = UnityEngine.Random.Range(-1f, 1f); // lowerSlopeHeuristic

        // [0..1]
        c[7] = Rand01(); // danger_weight


        return c;
    }


    private float ClampGene(int index, float value)
    {
        // heuristic gene is [-1..1], others [0..1]
        if (index == 5 || index == 6)
        {
            return Mathf.Clamp(value, -1f, 1f);
        }

        return Mathf.Clamp01(value);
    }

    private float Rand01()
    {
        return UnityEngine.Random.Range(0f, 1f);
    }

    private Individual GetBest(List<Individual> pop)
    {
        Individual best = pop[0];
        for (int i = 1; i < pop.Count; i++)
        {
            if (pop[i].fitness > best.fitness) best = pop[i];
        }
        return best;
    }

    private class Individual
    {
        public float[] chrom;
        public float fitness;
    }

    private float GetAverageFitness(List<Individual> population)
    {
        float totalFitness = 0f;
        int count = 0;

        // Population içindeki her organizmanın fitness'ını topla
        for (int i = 0; i < population.Count; i++)
        {
            totalFitness += population[i].fitness;
            count++;
        }

        // Ortalama fitness değeri
        return count > 0 ? totalFitness / count : 0f;
    }

}
