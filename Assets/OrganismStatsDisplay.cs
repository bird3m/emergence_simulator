using UnityEngine;
using TMPro;
using UnityEngine.InputSystem; // Yeni Input System için gerekli import

public class OrganismStatsDisplay : MonoBehaviour
{
    public TextMeshProUGUI statsText;  // UI Text element to show stats

    private void Update()
    {
        if (Mouse.current.leftButton.wasPressedThisFrame)  // Yeni Input System ile mouse tıklama algıla
        {
            // Raycast from camera to detect what object was clicked
            if (Camera.main == null) return;

            Vector2 screenPos = Mouse.current.position.ReadValue();
            Vector2 worldPos = Camera.main.ScreenToWorldPoint(screenPos);

            Collider2D col = Physics2D.OverlapPoint(worldPos);
            if (col != null)
            {
                Traits organismTraits = col.GetComponent<Traits>();
                if (organismTraits != null)
                {
                    DisplayStats(organismTraits);
                }
            }
        }
    }

    private void Start()
    {
        if (statsText == null)
        {
            statsText = FindObjectOfType<TextMeshProUGUI>();
            if (statsText == null)
            {
                // Debug log removed
            }
        }
    }

    private void DisplayStats(Traits traits)
    {
        // Debug log removed
        // Display stats, you can show more or less information based on your needs
        string stats = $"Mass: {traits.mass:F2}\n" +
                       $"Muscle Mass: {traits.muscle_mass:F2}\n" +
                       $"Metabolic Rate: {traits.metabolic_rate:F2}\n" +
                       $"Aggression: {traits.agression:F2}\n" +
                       $"Risk Aversion: {traits.risk_aversion:F2}\n" +
                       $"Current Energy: {traits.currentEnergy:F2}\n" +
                       $"Speed: {traits.GetSpeed(traits.PowerToWeight):F2}\n" +
                       $"Is Dead: {traits.IsDead()}\n";  // Example stats, add or remove as needed

        // Update the UI Text with the stats
        statsText.text = stats;
    }
}
