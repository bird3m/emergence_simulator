using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class stats_for_simulation : MonoBehaviour
{
    public static stats_for_simulation Instance { get; private set; }

    [Header("Resource Settings")]
    public int resourceCount = 50;
    public const int MIN_RESOURCE_COUNT = 1;
    public const int MAX_RESOURCE_COUNT = 500;
    
    public float resourceNutrition = 10f;
    public const float MIN_RESOURCE_NUTRITION = 1f;
    public const float MAX_RESOURCE_NUTRITION = 50f;

    [Header("Terrain Settings")]
    public int terrainSize = 50;
    public const int MIN_TERRAIN_SIZE = 10;
    public const int MAX_TERRAIN_SIZE = 200;

    [Header("Simulation Settings")]
    public float evaluationTime = 60f;
    public const float MIN_EVALUATION_TIME = 5f;
    public const float MAX_EVALUATION_TIME = 300f;
    
    public int populationSize = 50;
    public const int MIN_POPULATION_SIZE = 1;
    public const int MAX_POPULATION_SIZE = 500;

    [Header("UI References")]
    public Slider resourceCountSlider;
    public TMP_Text resourceCountText;
    
    public Slider resourceNutritionSlider;
    public TMP_Text resourceNutritionText;
    
    public Slider terrainSizeSlider;
    public TMP_Text terrainSizeText;
    
    public Slider evaluationTimeSlider;
    public TMP_Text evaluationTimeText;
    
    public Slider populationSizeSlider;
    public TMP_Text populationSizeText;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        InitializeUI();
    }

    private void InitializeUI()
    {
        // Sadece text'leri güncelle, slider değerlerini değiştirme
        if (resourceCountSlider != null)
        {
            resourceCount = Mathf.RoundToInt(resourceCountSlider.value);
            UpdateResourceCountText(resourceCount);
        }

        if (resourceNutritionSlider != null)
        {
            resourceNutrition = resourceNutritionSlider.value;
            UpdateResourceNutritionText(resourceNutrition);
        }

        if (terrainSizeSlider != null)
        {
            terrainSize = Mathf.RoundToInt(terrainSizeSlider.value);
            UpdateTerrainSizeText(terrainSize);
        }

        if (evaluationTimeSlider != null)
        {
            evaluationTime = evaluationTimeSlider.value;
            UpdateEvaluationTimeText(evaluationTime);
        }

        if (populationSizeSlider != null)
        {
            populationSize = Mathf.RoundToInt(populationSizeSlider.value);
            UpdatePopulationSizeText(populationSize);
        }
    }

    // UI Slider Metodları
    public void SetResourceCount(float value)
    {
        resourceCount = Mathf.RoundToInt(Mathf.Clamp(value, MIN_RESOURCE_COUNT, MAX_RESOURCE_COUNT));
        UpdateResourceCountText(resourceCount);
    }

    public void SetResourceNutrition(float value)
    {
        resourceNutrition = Mathf.Clamp(value, MIN_RESOURCE_NUTRITION, MAX_RESOURCE_NUTRITION);
        UpdateResourceNutritionText(resourceNutrition);
    }

    public void SetTerrainSize(float value)
    {
        terrainSize = Mathf.RoundToInt(Mathf.Clamp(value, MIN_TERRAIN_SIZE, MAX_TERRAIN_SIZE));
        UpdateTerrainSizeText(terrainSize);
    }

    public void SetEvaluationTime(float value)
    {
        evaluationTime = Mathf.Clamp(value, MIN_EVALUATION_TIME, MAX_EVALUATION_TIME);
        UpdateEvaluationTimeText(evaluationTime);
    }

    public void SetPopulationSize(float value)
    {
        populationSize = Mathf.RoundToInt(Mathf.Clamp(value, MIN_POPULATION_SIZE, MAX_POPULATION_SIZE));
        UpdatePopulationSizeText(populationSize);
    }

    // Text güncelleme metodları
    private void UpdateResourceCountText(int value)
    {
        if (resourceCountText != null)
            resourceCountText.text = value.ToString();
    }

    private void UpdateResourceNutritionText(float value)
    {
        if (resourceNutritionText != null)
            resourceNutritionText.text = value.ToString("F1");
    }

    private void UpdateTerrainSizeText(int value)
    {
        if (terrainSizeText != null)
            terrainSizeText.text = value.ToString();
    }

    private void UpdateEvaluationTimeText(float value)
    {
        if (evaluationTimeText != null)
            evaluationTimeText.text = value.ToString("F0") + "s";
    }

    private void UpdatePopulationSizeText(int value)
    {
        if (populationSizeText != null)
            populationSizeText.text = value.ToString();
    }
}
