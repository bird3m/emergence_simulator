using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;


/* Simple GA that evolves Chromosm of 6 genes 
** Objective function is health of the individuals. There is no other penalty for anything
, so we can say fitness = object. That way we can maximize individuals that is most efficient.
** Individuals spend least energy, got most energy and not consumed by other individuals.
** Random Mutation + BLX-α Crossover.
** Elitism + Tournament Selection.
/**
*/
public class GeneticAlgorithm : MonoBehaviour
{
    //Organism object
    public GameObject organismPrefab;
    
    //Positions of spawning points
    public Transform[] spawnPoints;

    //Genetic algoritm parameters. Population size can be adjusted for each run. Other parameters are fixed
    public int populationSize = 15;
    public int eliteCount = 6;
    public float crossoverRate = 0.90f;
    public float mutationRate = 0.08f;
    public float mutationStep = 0.5f;
    
    //After a certain point of time, selection happens and loop starts again. Can be adjusted for each run.
    public float evaluationSeconds = 20f;

    private List<Individual> population = new List<Individual>();
    private List<GameObject> spawned = new List<GameObject>();

    // Spawned organisms are cached
    public static List<OrganismBehaviour> Organisms = new List<OrganismBehaviour>();

    // Time: O(n)
    public static void RegisterOrganism(OrganismBehaviour ob)
    {
        if (ob == null) return;
        if (!Organisms.Contains(ob)) Organisms.Add(ob);
    }

    //Unity specific function for removing spawned individuals to spawned list
    public static void UnregisterOrganism(OrganismBehaviour ob)
    {
        if (ob == null) 
            return;
        Organisms.Remove(ob);
    }

    //counter for evaluation seconds
    private float timer = 0f;
    //t counter for generations
    private int generation = 0;
    private System.Random rng = new System.Random();

    private global::Terrain terrain;

    //A reference to source spawner function 
    public SourceSpawner spawner;
    public float alpha = 0.5f; // Blend factor for BLX-α Crossover

    // Time: O(n) because initializing population, Space: O(n) because storing population
    private void Start()
    {
        // Read values from singleton if available, 
        // that enables simulation to read adjusted values.
        if (stats_for_simulation.Instance != null)
        {
            populationSize = stats_for_simulation.Instance.populationSize;
            evaluationSeconds = stats_for_simulation.Instance.evaluationTime;
            eliteCount = populationSize;
        }
        
        terrain = FindObjectOfType<global::Terrain>();
        if (terrain == null)
        {
            // Debug log removed
            enabled = false;
            return;
        }

        //initialize population with random values of genes
        InitPopulation();
        //Spawn random population
        SpawnPopulation();
    }


    /*
    ** Called at every frame. Here is where genetic algorithm loop happens
    */
    private void Update()
    {
        timer += Time.deltaTime;
        //if a certain amount of time has passed
        if (timer >= evaluationSeconds)
        {
            timer = 0f;

            // Evaluate current population (P_t)
            EvaluateFitnessFromWorld();
            
            // M_t := Selection(P_t) - Create mating pool
            List<Individual> matingPool = Selection(population);
            
            // Q_t := Variation(M_t) - Create offspring
            List<Individual> offspring = Variation(matingPool);
            
            // P_{t+1} := Survivor(P_t, Q_t) - Select survivors
            population = Survivor(population, offspring);
            
            generation++;

            // Reset world for next generation
            DestroySpawned();
            
            // Reset all resources for next generation
            if (spawner != null)
            {
                spawner.ResetAllResources();
            }
            
            SpawnPopulation();
        }


    }



    // Time: O(n) because creating n individuals, Space: O(n) because storing population
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

    // Time: O(n) because spawning n organisms, Space: O(n) because storing spawned list
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

    // Time: O(1) because simple calculations, Space: O(1)
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


    // Time: O(n) because destroying n objects, Space: O(1)
    private void DestroySpawned()
    {
        for (int i = 0; i < spawned.Count; i++)
        {
            if (spawned[i] != null) Destroy(spawned[i]);
        }
        spawned.Clear();
    }

 

    // Time: O(n) because it evaluates n individuals, Space: O(1)
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

            population[i].fitness = t.Fitness01();
        }
    }

    // ----------------------------
    // GA Core - Generalized Framework of EAs
    // ----------------------------

    /// <summary>
    /// Selection: M_t := Selection(P_t)
    /// Creates mating pool by selecting parents from current population
    /// </summary>
    private List<Individual> Selection(List<Individual> population)
    {
        List<Individual> matingPool = new List<Individual>();
        
        // Select populationSize parents (with replacement) for mating
        for (int i = 0; i < populationSize; i++)
        {
            Individual parent = TournamentSelect(population, 3);
            matingPool.Add(parent);
        }
        
        return matingPool;
    }

    /// <summary>
    /// Variation: Q_t := Variation(M_t)
    /// Creates offspring population through crossover and mutation
    /// </summary>
    private List<Individual> Variation(List<Individual> matingPool)
    {
        List<Individual> offspring = new List<Individual>();
        
        // Create offspring by pairing parents from mating pool
        for (int i = 0; i < populationSize; i++)
        {
            // Select two random parents from mating pool
            Individual p1 = matingPool[rng.Next(matingPool.Count)];
            Individual p2 = matingPool[rng.Next(matingPool.Count)];
            
            // Apply crossover
            float[] childChrom = (float[])p1.chrom.Clone();
            if (Rand01() < crossoverRate)
            {
                childChrom = BLXCrossover(p1.chrom, p2.chrom, alpha);
            }
            
            // Apply mutation
            Mutate(childChrom);
            
            // Create offspring individual
            Individual child = new Individual();
            child.chrom = childChrom;
            child.fitness = 0f;
            offspring.Add(child);
        }
        
        return offspring;
    }

    /// <summary>
    /// Survivor: P_{t+1} := Survivor(P_t, Q_t)
    /// Combines parent and offspring populations and selects survivors
    /// Uses elitism: keeps best from both populations
    /// </summary>
    private List<Individual> Survivor(List<Individual> parents, List<Individual> offspring)
    {
        // Combine parent and offspring populations
        List<Individual> combined = new List<Individual>();
        combined.AddRange(parents);
        combined.AddRange(offspring);
        
        // Sort by fitness (descending)
        combined.Sort((a, b) => b.fitness.CompareTo(a.fitness));
        
        // Select top populationSize individuals (elitism)
        List<Individual> survivors = new List<Individual>();
        for (int i = 0; i < populationSize && i < combined.Count; i++)
        {
            Individual survivor = new Individual();
            survivor.chrom = (float[])combined[i].chrom.Clone();
            survivor.fitness = 0f; // Will be evaluated next generation
            survivors.Add(survivor);
        }
        
        return survivors;
    }

    // Time: O(k) because k comparisons, Space: O(1)
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

    // Time: O(m) because iterating m genes
    // Space: O(m) we are creating a new child chromosome
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



    // Time: O(m) because iterating m genes, Space: O(1)
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





    // Time: O(1) because fixed size array
    // Space: O(1)
    private float[] RandomChromosome()
    {
        float[] c = new float[6];

        // [0..1]
        c[0] = Rand01(); // mass
        c[1] = Rand01(); // muscle_mass
        c[2] = Rand01(); // metabolic_rate
        c[3] = Rand01(); // agression
        c[4] = Rand01(); // risk_aversion

        // [0..1]
        c[5] = Rand01(); // danger_weight


        return c;
    }


    // Time: O(1)
    // Space: O(1)
    private float ClampGene(int index, float value)
    {
        // All genes are [0..1]
        return Mathf.Clamp01(value);
    }

    // Time: O(1)
    // Space: O(1)
    private float Rand01()
    {
        return UnityEngine.Random.Range(0f, 1f);
    }

    // Time: O(n) because iterating population
    // Space: O(1)
    //gets the best individual from the population
    private Individual GetBest(List<Individual> pop)
    {
        Individual best = pop[0];
        for (int i = 1; i < pop.Count; i++)
        {
            if (pop[i].fitness > best.fitness) best = pop[i];
        }
        return best;
    }

    private class Individual //individual class
    {
        public float[] chrom;
        public float fitness;
    }

    // Time: O(n) because we sum n values where n is the population size.
    //  Space: O(1)
    private float GetAverageFitness(List<Individual> population)
    {
        float totalFitness = 0f;
        int count = 0;

        for (int i = 0; i < population.Count; i++)
        {
            totalFitness += population[i].fitness;
            count++;
        }

        // Calculate and return average fitness
        if (count > 0)
        {
            float averageFitness = totalFitness / count;
            return averageFitness;
        }
        else
        {
            // No individuals, return 0
            return 0f;
        }
    }

}
