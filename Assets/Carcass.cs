using UnityEngine;

public class Carcass : MonoBehaviour
{
    [Header("Food")]
    public float nutritionLeft = 30f;

    [Header("Expiry")]
    public int bornGeneration = 0;
    public int expireAfterGenerations = 2; // 2 jenerasyon sonra sil
    public float expireAfterSeconds = 0f;  // 0 ise kapalı

    private float bornTime;

    private void Awake()
    {
        bornTime = Time.time;
    }

    // GA veya Traits bunu çağıracak
    public void Initialize(int currentGeneration, float nutrition, int expireGens)
    {
        bornGeneration = currentGeneration;
        nutritionLeft = nutrition;
        expireAfterGenerations = expireGens;
        bornTime = Time.time;
    }

    // Bunu her frame GA’dan vereceğiz (en pratik)
    public void TickGeneration(int currentGeneration)
    {
        if (expireAfterGenerations > 0)
        {
            int ageGen = currentGeneration - bornGeneration;
            if (ageGen >= expireAfterGenerations)
            {
                Destroy(gameObject);
                return;
            }
        }

        if (expireAfterSeconds > 0f)
        {
            if (Time.time - bornTime >= expireAfterSeconds)
            {
                Destroy(gameObject);
                return;
            }
        }
    }

    public float Consume(float amount)
    {
        if (nutritionLeft <= 0f) return 0f;

        float taken = Mathf.Min(amount, nutritionLeft);
        nutritionLeft -= taken;

        if (nutritionLeft <= 0f)
        {
            Destroy(gameObject); 
        }

        return taken;
    }
}
