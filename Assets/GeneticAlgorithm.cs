using System;
using System.Collections.Generic;
using UnityEngine;


/* Simple GA that evolves Traits.chromosm (8 genes) and reads fitness directly from Traits.Fitness01().
** No extra Stats script needed.
*/
public class GeneticAlgorithm : MonoBehaviour
{
    [Header("Prefab")]
    public GameObject organismPrefab;
    
    [Header("Spawn")]
    public Transform[] spawnPoints;

    [Header("GA Settings")]
    public int populationSize = 30;
    public int eliteCount = 4;
    public float crossoverRate = 0.90f;
    public float mutationRate = 0.08f;
    public float mutationStep = 0.10f;

    [Header("Evaluation")]
    public float evaluationSeconds = 20f;

    private List<Individual> population = new List<Individual>();
    private List<GameObject> spawned = new List<GameObject>();

    private float timer = 0f;
    private int generation = 0;
    private System.Random rng = new System.Random();

    private void Start()
    {
        InitPopulation();
        SpawnPopulation();
    }

    private void Update()
    {
        timer += Time.deltaTime;

        if (timer >= evaluationSeconds)
        {
            timer = 0f;

            // 1) Read fitness from Traits
            EvaluateFitnessFromWorld();

            // 2) Build next generation
            population = NextGeneration(population);

            generation++;
            Debug.Log($"Generation {generation} | best fitness: {GetBest(population).fitness:F3}");

            // 3) Reset world
            DestroySpawned();
            SpawnPopulation();
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
            spawned.Add(go);

            Traits t = go.GetComponent<Traits>();
            if (t == null)
            {
                Debug.LogError("Prefab missing Traits component!");
                continue;
            }

            // Assign chromosome
            t.chromosm = new float[8];
            Array.Copy(population[i].chrom, t.chromosm, 8);

            // IMPORTANT: recompute after GA sets chromosm
            t.ApplyChromosomeAndRecompute();
        }
    }

    private Vector3 GetSpawnPos(int i)
    {
        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            return spawnPoints[i % spawnPoints.Length].position;
        }

        // fallback grid spawn
        float x = (i % 10) * 1.5f;
        float z = (i / 10) * 1.5f;
        return new Vector3(x, 0f, z);
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

            // Fitness directly from Traits health
            population[i].fitness = t.Fitness01();
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
                child = UniformCrossover(p1.chrom, p2.chrom);
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

    private float[] UniformCrossover(float[] a, float[] b)
    {
        float[] child = new float[a.Length];

        for (int i = 0; i < a.Length; i++)
        {
            child[i] = (Rand01() < 0.5f) ? a[i] : b[i];
        }

        return child;
    }

    private void Mutate(float[] chrom)
    {
        for (int i = 0; i < chrom.Length; i++)
        {
            if (Rand01() < mutationRate)
            {
                float delta = UnityEngine.Random.Range(-mutationStep, mutationStep);
                chrom[i] += delta;
                chrom[i] = ClampGene(i, chrom[i]);
            }
        }
    }

    // ----------------------------
    // Chromosome helpers
    // ----------------------------

    private float[] RandomChromosome()
    {
        float[] c = new float[8];

        // [0..1]
        c[0] = Rand01(); // mass
        c[1] = Rand01(); // muscle_mass
        c[2] = Rand01(); // metabolic_rate
        c[3] = Rand01(); // agression
        c[4] = Rand01(); // risk_aversion

        // heuristic [-1..1]
        c[5] = UnityEngine.Random.Range(-1f, 1f);

        // [0..1]
        c[6] = Rand01(); // danger_weight
        c[7] = Rand01(); // camouflage

        return c;
    }

    private float ClampGene(int index, float value)
    {
        // heuristic gene is [-1..1], others [0..1]
        if (index == 5)
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
}
