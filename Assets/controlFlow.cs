/*
* This script is entirely for displaying data and stats. It is this long and hard to understand
* just because unity is not built for displaying data.
*/


using UnityEngine;
using TMPro;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System.Linq;

public class controlFlow : MonoBehaviour
{
    public GameObject statisticsPanel;
    public TextMeshProUGUI statisticsText;
    
    public GameObject traitGraphPanel;
    public GameObject emergenceGraphPanel;
    
    private bool isPanelVisible = false;
    private bool isTraitGraphVisible = false;
    private bool isEmergenceGraphVisible = false;
    
    // Data tracking for graphs
    private List<Dictionary<string, float>> traitAveragesHistory = new List<Dictionary<string, float>>();
    private List<Dictionary<string, int>> emergenceCountsHistory = new List<Dictionary<string, int>>();
    private float lastSampleTime = 0f;
    public float sampleInterval = 2f; // Sample every 2 seconds

    void Start()
    {
        // Create UI if it doesn't exist
        if (statisticsPanel == null)
        {
            CreateStatisticsUI();
            CreateTraitGraphUI();
            CreateEmergenceGraphUI();
        }
        
        if (statisticsPanel != null)
        {
            statisticsPanel.SetActive(false);
        }
        if (traitGraphPanel != null)
        {
            traitGraphPanel.SetActive(false);
        }
        if (emergenceGraphPanel != null)
        {
            emergenceGraphPanel.SetActive(false);
        }
    }

    void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        // Stats panel when U pressed
        if (keyboard.uKey.wasPressedThisFrame)
        {
            ToggleStatisticsPanel();
        }
        
        // Stats panel when I pressed
        if (keyboard.iKey.wasPressedThisFrame)
        {
            ToggleTraitGraphPanel();
        }
        
        // Stats panel when O pressed
        if (keyboard.oKey.wasPressedThisFrame)
        {
            ToggleEmergenceGraphPanel();
        }

        // Stop time if 0 is pressed
        if (keyboard.digit0Key.wasPressedThisFrame || keyboard.numpad0Key.wasPressedThisFrame)
        {
            Time.timeScale = 0f;
            Debug.Log("Time paused (timeScale = 0)");
        }
        // Normal speed if 1 is pressed
        else if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame)
        {
            Time.timeScale = 1f;
            Debug.Log("Time speed: 1x");
        }
        //  5X speed if 1 is pressed
        else if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame)
        {
            Time.timeScale = 5f;
            Debug.Log("Time speed: 5x");
        }
        //  25X speed if 3 is pressed
        else if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame)
        {
            Time.timeScale = 25f;
            Debug.Log("Time speed: 25x");
        }
        //  50X speed if 3 is pressed
        else if (keyboard.digit4Key.wasPressedThisFrame || keyboard.numpad4Key.wasPressedThisFrame)
        {
            Time.timeScale = 50f;
            Debug.Log("Time speed: 50x");
        }
        
        //Update panel the time if any of panels are visible
        if (isPanelVisible && statisticsText != null)
        {
            UpdateStatisticsText();
        }
        
        // Sample data periodically for graphs
        if (Time.time - lastSampleTime >= sampleInterval)
        {
            SampleTraitData();
            lastSampleTime = Time.time;
        }
        
        // Update graphs if visible
        if (isTraitGraphVisible)
        {
            UpdateTraitGraph();
        }
        if (isEmergenceGraphVisible)
        {
            UpdateEmergenceGraph();
        }
    }

    void CreateStatisticsUI()
    {
        // Create canvas
        GameObject canvasGO = GameObject.Find("StatisticsCanvas");
        if (canvasGO == null)
        {
            canvasGO = new GameObject("StatisticsCanvas");
            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
            canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        }

        // Create Panel
        statisticsPanel = new GameObject("StatisticsPanel");
        statisticsPanel.transform.SetParent(canvasGO.transform, false);
        
        UnityEngine.UI.Image panelImage = statisticsPanel.AddComponent<UnityEngine.UI.Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.85f);
        
        RectTransform panelRect = statisticsPanel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(800f, 600f);
        panelRect.anchoredPosition = Vector2.zero;

        // Create text
        GameObject textGO = new GameObject("StatisticsText");
        textGO.transform.SetParent(statisticsPanel.transform, false);
        
        statisticsText = textGO.AddComponent<TextMeshProUGUI>();
        statisticsText.fontSize = 18;
        statisticsText.color = Color.white;
        statisticsText.alignment = TextAlignmentOptions.TopLeft;
        
        RectTransform textRect = textGO.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(20f, 20f);
        textRect.offsetMax = new Vector2(-20f, -20f);
        
        statisticsPanel.SetActive(false);
    }

    void ToggleStatisticsPanel()
    {
        isPanelVisible = !isPanelVisible;
        
        if (statisticsPanel != null)
        {
            statisticsPanel.SetActive(isPanelVisible);
            
            if (isPanelVisible)
            {
                // Close other panels
                if (traitGraphPanel != null) traitGraphPanel.SetActive(false);
                if (emergenceGraphPanel != null) emergenceGraphPanel.SetActive(false);
                isTraitGraphVisible = false;
                isEmergenceGraphVisible = false;
                
                UpdateStatisticsText();
            }
        }
    }

    void UpdateStatisticsText()
    {
        if (statisticsText == null) return;

        Traits[] allOrganisms = FindObjectsOfType<Traits>();
        
        if (allOrganisms.Length == 0)
        {
            statisticsText.text = "<b>TRAIT STATISTICS</b>\n\nNo organisms yet.";
            return;
        }

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine("<b><size=24>TRAIT STATISTICS</size></b>");
        sb.AppendLine($"<color=#FFD700>Total Organisms: {allOrganisms.Length}</color>\n");

        // Trait istatistikleri hesapla
        CalculateAndDisplayTraitStats(sb, allOrganisms, "Mass", t => t.mass);
        CalculateAndDisplayTraitStats(sb, allOrganisms, "Muscle Mass", t => t.muscle_mass);
        CalculateAndDisplayTraitStats(sb, allOrganisms, "Metabolic Rate", t => t.metabolic_rate);
        CalculateAndDisplayTraitStats(sb, allOrganisms, "Agression", t => t.agression);
        CalculateAndDisplayTraitStats(sb, allOrganisms, "Risk Aversion", t => t.risk_aversion);
        CalculateAndDisplayTraitStats(sb, allOrganisms, "Danger Weight", t => t.danger_weight);

        // Emergence statistics
        sb.AppendLine("\n<b><size=20>EMERGENCE STATISTICS</size></b>");
        
        int canFlyCount = allOrganisms.Count(t => t.can_fly);
        int can_cautiousPathingCount = allOrganisms.Count(t => t.can_cautiousPathing);
        int isScavengingCount = allOrganisms.Count(t => t.is_scavenging);
        int isCarnivoreCount = allOrganisms.Count(t => t.is_carnivore);

        sb.AppendLine($"<color=#00FF00>Can Fly:</color> {canFlyCount} ({(canFlyCount * 100f / allOrganisms.Length):F1}%)");
        sb.AppendLine($"<color=#00BFFF>Can Cautious Pathing:</color> {can_cautiousPathingCount} ({(can_cautiousPathingCount * 100f / allOrganisms.Length):F1}%)");
        sb.AppendLine($"<color=#FF6B6B>Is Scavenging:</color> {isScavengingCount} ({(isScavengingCount * 100f / allOrganisms.Length):F1}%)");
        sb.AppendLine($"<color=#FF0000>Is Carnivore:</color> {isCarnivoreCount} ({(isCarnivoreCount * 100f / allOrganisms.Length):F1}%)");

        statisticsText.text = sb.ToString();
    }

    void CalculateAndDisplayTraitStats(System.Text.StringBuilder sb, Traits[] organisms, string traitName, System.Func<Traits, float> traitGetter)
    {
        List<float> values = organisms.Select(traitGetter).ToList();
        
        float avg = values.Average();
        float min = values.Min();
        float max = values.Max();
        
        sb.AppendLine($"<b><color=#FFFF00>{traitName}:</color></b>");
        sb.AppendLine($"  Average: <color=#FFFFFF>{avg:F3}</color>  |  Min: <color=#FF6B6B>{min:F3}</color>  |  Max: <color=#00FF00>{max:F3}</color>");
    }
    
    void CreateTraitGraphUI()
    {
        GameObject canvasGO = GameObject.Find("StatisticsCanvas");
        if (canvasGO == null) return;
        
        // Panel
        traitGraphPanel = new GameObject("TraitGraphPanel");
        traitGraphPanel.transform.SetParent(canvasGO.transform, false);
        
        UnityEngine.UI.Image panelImage = traitGraphPanel.AddComponent<UnityEngine.UI.Image>();
        panelImage.color = new Color(0.05f, 0.05f, 0.05f, 0.95f);
        
        RectTransform panelRect = traitGraphPanel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(900f, 650f);
        panelRect.anchoredPosition = Vector2.zero;
        
        // Title
        GameObject titleGO = new GameObject("Title");
        titleGO.transform.SetParent(traitGraphPanel.transform, false);
        TextMeshProUGUI titleText = titleGO.AddComponent<TextMeshProUGUI>();
        titleText.text = "<b>TRAIT AVERAGES OVER TIME</b>";
        titleText.fontSize = 24;
        titleText.color = Color.white;
        titleText.alignment = TextAlignmentOptions.Center;
        
        RectTransform titleRect = titleGO.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.sizeDelta = new Vector2(0f, 50f);
        titleRect.anchoredPosition = new Vector2(0f, -25f);
        
        traitGraphPanel.SetActive(false);
    }
    
    void CreateEmergenceGraphUI()
    {
        GameObject canvasGO = GameObject.Find("StatisticsCanvas");
        if (canvasGO == null) return;
        
        // Panel
        emergenceGraphPanel = new GameObject("EmergenceGraphPanel");
        emergenceGraphPanel.transform.SetParent(canvasGO.transform, false);
        
        UnityEngine.UI.Image panelImage = emergenceGraphPanel.AddComponent<UnityEngine.UI.Image>();
        panelImage.color = new Color(0.05f, 0.05f, 0.05f, 0.95f);
        
        RectTransform panelRect = emergenceGraphPanel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(800f, 600f);
        panelRect.anchoredPosition = Vector2.zero;
        
        // Title
        GameObject titleGO = new GameObject("Title");
        titleGO.transform.SetParent(emergenceGraphPanel.transform, false);
        TextMeshProUGUI titleText = titleGO.AddComponent<TextMeshProUGUI>();
        titleText.text = "<b>EMERGENCE ACTIVATION COUNTS</b>";
        titleText.fontSize = 24;
        titleText.color = Color.white;
        titleText.alignment = TextAlignmentOptions.Center;
        
        RectTransform titleRect = titleGO.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.sizeDelta = new Vector2(0f, 50f);
        titleRect.anchoredPosition = new Vector2(0f, -25f);
        
        emergenceGraphPanel.SetActive(false);
    }
    
    void ToggleTraitGraphPanel()
    {
        isTraitGraphVisible = !isTraitGraphVisible;
        
        if (traitGraphPanel != null)
        {
            traitGraphPanel.SetActive(isTraitGraphVisible);
            if (isTraitGraphVisible)
            {
                // Close other panels
                if (statisticsPanel != null) statisticsPanel.SetActive(false);
                if (emergenceGraphPanel != null) emergenceGraphPanel.SetActive(false);
                isPanelVisible = false;
                isEmergenceGraphVisible = false;
                
                UpdateTraitGraph();
            }
        }
    }
    
    void ToggleEmergenceGraphPanel()
    {
        isEmergenceGraphVisible = !isEmergenceGraphVisible;
        
        if (emergenceGraphPanel != null)
        {
            emergenceGraphPanel.SetActive(isEmergenceGraphVisible);
            if (isEmergenceGraphVisible)
            {
                // Close other panels
                if (statisticsPanel != null) statisticsPanel.SetActive(false);
                if (traitGraphPanel != null) traitGraphPanel.SetActive(false);
                isPanelVisible = false;
                isTraitGraphVisible = false;
                
                UpdateEmergenceGraph();
            }
        }
    }
    
    void SampleTraitData()
    {
        Traits[] allOrganisms = FindObjectsOfType<Traits>();
        if (allOrganisms.Length == 0) return;
        
        // Sample trait averages
        Dictionary<string, float> traitAvgs = new Dictionary<string, float>
        {
            { "Mass", allOrganisms.Average(t => t.mass) },
            { "Muscle", allOrganisms.Average(t => t.muscle_mass) },
            { "Metabolic", allOrganisms.Average(t => t.metabolic_rate) },
            { "Aggression", allOrganisms.Average(t => t.agression) },
            { "Risk Aversion", allOrganisms.Average(t => t.risk_aversion) }
        };
        traitAveragesHistory.Add(traitAvgs);
        
        // Sample emergence counts
        Dictionary<string, int> emergenceCounts = new Dictionary<string, int>
        {
            { "Can Fly", allOrganisms.Count(t => t.can_fly) },
            { "Cautious Path", allOrganisms.Count(t => t.can_cautiousPathing) },
            { "Scavenging", allOrganisms.Count(t => t.is_scavenging) },
            { "Carnivore", allOrganisms.Count(t => t.is_carnivore) }
        };
        emergenceCountsHistory.Add(emergenceCounts);
        
        // Keep only last 100 samples
        if (traitAveragesHistory.Count > 100)
        {
            traitAveragesHistory.RemoveAt(0);
        }
        if (emergenceCountsHistory.Count > 100)
        {
            emergenceCountsHistory.RemoveAt(0);
        }
    }
    
    void UpdateTraitGraph()
    {
        if (traitGraphPanel == null || traitAveragesHistory.Count < 2) return;
        
        // Clear existing lines and axes
        foreach (Transform child in traitGraphPanel.transform)
        {
            if (child.name.StartsWith("Line_") || child.name.StartsWith("Axis_") || child.name == "Legend") 
                Destroy(child.gameObject);
        }
        
        // Draw axes first
        DrawAxes(traitGraphPanel);
        
        // Draw lines for each trait
        string[] traitNames = new[] { "Mass", "Muscle", "Metabolic", "Aggression", "Risk Aversion" };
        Color[] colors = new[] { 
            new Color(1f, 0.2f, 0.2f), // Red
            new Color(0.2f, 1f, 0.2f), // Green
            new Color(0.2f, 0.5f, 1f), // Blue
            new Color(1f, 1f, 0.2f),   // Yellow
            new Color(1f, 0.5f, 0.2f)  // Orange
        };
        
        for (int i = 0; i < traitNames.Length; i++)
        {
            DrawLineGraph(traitGraphPanel, traitNames[i], colors[i], i);
        }
        
        // Add legend
        DrawLegend(traitGraphPanel, traitNames, colors);
    }
    
    void DrawLineGraph(GameObject parent, string traitName, Color color, int index)
    {
        GameObject lineGO = new GameObject($"Line_{traitName}");
        lineGO.transform.SetParent(parent.transform, false);
        
        // Don't add an Image component to the parent - it will just fill the area
        RectTransform rect = lineGO.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.1f, 0.15f);
        rect.anchorMax = new Vector2(0.9f, 0.85f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        
        // Draw simple line representation using UI elements
        int dataCount = traitAveragesHistory.Count;
        if (dataCount < 2) return;
        
        float width = 800f * 0.8f;  // Approximate panel width * area
        float height = 650f * 0.7f; // Approximate panel height * area
        
        for (int i = 0; i < dataCount - 1; i++)
        {
            float x1 = (i / (float)(dataCount - 1)) * width - width * 0.5f;
            float x2 = ((i + 1) / (float)(dataCount - 1)) * width - width * 0.5f;
            
            float y1 = (traitAveragesHistory[i][traitName] - 0.5f) * height;
            float y2 = (traitAveragesHistory[i + 1][traitName] - 0.5f) * height;
            
            GameObject segment = new GameObject($"Segment_{i}");
            segment.transform.SetParent(lineGO.transform, false);
            
            UnityEngine.UI.Image segImg = segment.AddComponent<UnityEngine.UI.Image>();
            segImg.color = color;
            
            RectTransform segRect = segment.GetComponent<RectTransform>();
            segRect.anchorMin = new Vector2(0.5f, 0.5f);
            segRect.anchorMax = new Vector2(0.5f, 0.5f);
            
            Vector2 dir = new Vector2(x2 - x1, y2 - y1);
            float distance = dir.magnitude;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            
            segRect.sizeDelta = new Vector2(distance, 3f);
            segRect.anchoredPosition = new Vector2((x1 + x2) * 0.5f, (y1 + y2) * 0.5f);
            segRect.localRotation = Quaternion.Euler(0, 0, angle);
        }
    }
    
    void DrawLegend(GameObject parent, string[] names, Color[] colors)
    {
        GameObject legendGO = new GameObject("Legend");
        legendGO.transform.SetParent(parent.transform, false);
        
        RectTransform legendRect = legendGO.AddComponent<RectTransform>();
        legendRect.anchorMin = new Vector2(0.75f, 0.7f);
        legendRect.anchorMax = new Vector2(0.95f, 0.95f);
        legendRect.offsetMin = Vector2.zero;
        legendRect.offsetMax = Vector2.zero;
        
        for (int i = 0; i < names.Length; i++)
        {
            GameObject itemGO = new GameObject($"Item_{i}");
            itemGO.transform.SetParent(legendGO.transform, false);
            
            TextMeshProUGUI itemText = itemGO.AddComponent<TextMeshProUGUI>();
            itemText.text = $"<color=#{ColorUtility.ToHtmlStringRGB(colors[i])}>■</color> {names[i]}";
            itemText.fontSize = 14;
            itemText.color = Color.white;
            
            RectTransform itemRect = itemGO.GetComponent<RectTransform>();
            itemRect.anchorMin = new Vector2(0f, 1f - (i + 1) * 0.17f);
            itemRect.anchorMax = new Vector2(1f, 1f - i * 0.17f);
            itemRect.offsetMin = Vector2.zero;
            itemRect.offsetMax = Vector2.zero;
        }
    }
    
    void DrawAxes(GameObject parent)
    {
        // Y-axis labels (0.0, 0.2, 0.4, 0.6, 0.8, 1.0)
        for (int i = 0; i <= 5; i++)
        {
            float value = i * 0.2f;
            
            GameObject labelGO = new GameObject($"Axis_YLabel_{i}");
            labelGO.transform.SetParent(parent.transform, false);
            
            TextMeshProUGUI labelText = labelGO.AddComponent<TextMeshProUGUI>();
            labelText.text = value.ToString("F1");
            labelText.fontSize = 12;
            labelText.color = new Color(0.7f, 0.7f, 0.7f);
            labelText.alignment = TextAlignmentOptions.Right;
            
            RectTransform labelRect = labelGO.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0.02f, 0.15f + i * 0.14f);
            labelRect.anchorMax = new Vector2(0.08f, 0.15f + i * 0.14f);
            labelRect.sizeDelta = new Vector2(0f, 20f);
            labelRect.anchoredPosition = new Vector2(0f, 0f);
            
            // Draw horizontal grid line
            GameObject gridLineGO = new GameObject($"Axis_GridLine_{i}");
            gridLineGO.transform.SetParent(parent.transform, false);
            
            UnityEngine.UI.Image gridImg = gridLineGO.AddComponent<UnityEngine.UI.Image>();
            gridImg.color = new Color(0.3f, 0.3f, 0.3f, 0.3f);
            
            RectTransform gridRect = gridLineGO.GetComponent<RectTransform>();
            gridRect.anchorMin = new Vector2(0.1f, 0.15f + i * 0.14f);
            gridRect.anchorMax = new Vector2(0.9f, 0.15f + i * 0.14f);
            gridRect.sizeDelta = new Vector2(0f, 1f);
        }
        
        // X-axis label (Generation/Sample)
        GameObject xLabelGO = new GameObject("Axis_XLabel");
        xLabelGO.transform.SetParent(parent.transform, false);
        
        TextMeshProUGUI xLabelText = xLabelGO.AddComponent<TextMeshProUGUI>();
        xLabelText.text = $"Samples (Latest: {traitAveragesHistory.Count})";
        xLabelText.fontSize = 12;
        xLabelText.color = new Color(0.7f, 0.7f, 0.7f);
        xLabelText.alignment = TextAlignmentOptions.Center;
        
        RectTransform xLabelRect = xLabelGO.GetComponent<RectTransform>();
        xLabelRect.anchorMin = new Vector2(0.1f, 0.05f);
        xLabelRect.anchorMax = new Vector2(0.9f, 0.12f);
        xLabelRect.offsetMin = Vector2.zero;
        xLabelRect.offsetMax = Vector2.zero;
        
        // Y-axis label
        GameObject yLabelGO = new GameObject("Axis_YLabel_Title");
        yLabelGO.transform.SetParent(parent.transform, false);
        
        TextMeshProUGUI yLabelText = yLabelGO.AddComponent<TextMeshProUGUI>();
        yLabelText.text = "Trait Value";
        yLabelText.fontSize = 12;
        yLabelText.color = new Color(0.7f, 0.7f, 0.7f);
        yLabelText.alignment = TextAlignmentOptions.Center;
        
        RectTransform yLabelRect = yLabelGO.GetComponent<RectTransform>();
        yLabelRect.anchorMin = new Vector2(0.01f, 0.4f);
        yLabelRect.anchorMax = new Vector2(0.05f, 0.6f);
        yLabelRect.offsetMin = Vector2.zero;
        yLabelRect.offsetMax = Vector2.zero;
    }
    
    void UpdateEmergenceGraph()
    {
        if (emergenceGraphPanel == null || emergenceCountsHistory.Count == 0) return;
        
        // Clear existing bars and axes
        foreach (Transform child in emergenceGraphPanel.transform)
        {
            if (child.name.StartsWith("Bar_") || child.name.StartsWith("Axis_")) 
                Destroy(child.gameObject);
        }
        
        // Get latest emergence counts
        var latestCounts = emergenceCountsHistory[emergenceCountsHistory.Count - 1];
        string[] emergenceNames = new[] { "Can Fly", "Cautious Path", "Scavenging", "Carnivore" };
        Color[] colors = new[] {
            new Color(0.2f, 1f, 0.2f),  // Green
            new Color(0.2f, 0.8f, 1f),  // Cyan
            new Color(1f, 0.6f, 0.4f),  // Orange
            new Color(1f, 0.2f, 0.2f)   // Red
        };
        
        int maxCount = latestCounts.Values.Max();
        if (maxCount == 0) maxCount = 1;
        
        // Draw Y-axis with values
        DrawEmergenceAxes(emergenceGraphPanel, maxCount);
        
        for (int i = 0; i < emergenceNames.Length; i++)
        {
            DrawBar(emergenceGraphPanel, emergenceNames[i], latestCounts[emergenceNames[i]], maxCount, colors[i], i, emergenceNames.Length);
        }
    }
    
    void DrawBar(GameObject parent, string name, int count, int maxCount, Color color, int index, int total)
    {
        GameObject barGO = new GameObject($"Bar_{name}");
        barGO.transform.SetParent(parent.transform, false);
        
        UnityEngine.UI.Image barImg = barGO.AddComponent<UnityEngine.UI.Image>();
        barImg.color = color;
        
        RectTransform barRect = barGO.GetComponent<RectTransform>();
        
        float barWidth = 0.7f / total;
        float spacing = 0.05f;
        float xPos = 0.15f + index * (barWidth + spacing);
        
        barRect.anchorMin = new Vector2(xPos, 0.2f);
        barRect.anchorMax = new Vector2(xPos + barWidth, 0.2f + (count / (float)maxCount) * 0.6f);
        barRect.offsetMin = Vector2.zero;
        barRect.offsetMax = Vector2.zero;
        
        // Add label
        GameObject labelGO = new GameObject("Label");
        labelGO.transform.SetParent(barGO.transform, false);
        
        TextMeshProUGUI labelText = labelGO.AddComponent<TextMeshProUGUI>();
        labelText.text = $"{name}\n{count}";
        labelText.fontSize = 12;
        labelText.color = Color.white;
        labelText.alignment = TextAlignmentOptions.Center;
        
        RectTransform labelRect = labelGO.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, -0.3f);
        labelRect.anchorMax = new Vector2(1f, 0f);
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
    }
    
    void DrawEmergenceAxes(GameObject parent, int maxCount)
    {
        // Calculate nice round numbers for Y-axis
        int step = Mathf.Max(1, Mathf.CeilToInt(maxCount / 5f));
        int roundedMax = step * 5;
        
        // Y-axis labels (count values)
        for (int i = 0; i <= 5; i++)
        {
            int value = i * step;
            
            GameObject labelGO = new GameObject($"Axis_YLabel_{i}");
            labelGO.transform.SetParent(parent.transform, false);
            
            TextMeshProUGUI labelText = labelGO.AddComponent<TextMeshProUGUI>();
            labelText.text = value.ToString();
            labelText.fontSize = 12;
            labelText.color = new Color(0.7f, 0.7f, 0.7f);
            labelText.alignment = TextAlignmentOptions.Right;
            
            RectTransform labelRect = labelGO.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0.02f, 0.2f + i * 0.12f);
            labelRect.anchorMax = new Vector2(0.12f, 0.2f + i * 0.12f);
            labelRect.sizeDelta = new Vector2(0f, 20f);
            labelRect.anchoredPosition = new Vector2(0f, 0f);
            
            // Draw horizontal grid line
            GameObject gridLineGO = new GameObject($"Axis_GridLine_{i}");
            gridLineGO.transform.SetParent(parent.transform, false);
            
            UnityEngine.UI.Image gridImg = gridLineGO.AddComponent<UnityEngine.UI.Image>();
            gridImg.color = new Color(0.3f, 0.3f, 0.3f, 0.3f);
            
            RectTransform gridRect = gridLineGO.GetComponent<RectTransform>();
            gridRect.anchorMin = new Vector2(0.15f, 0.2f + i * 0.12f);
            gridRect.anchorMax = new Vector2(0.95f, 0.2f + i * 0.12f);
            gridRect.sizeDelta = new Vector2(0f, 1f);
        }
        
        // Y-axis title
        GameObject yTitleGO = new GameObject("Axis_YTitle");
        yTitleGO.transform.SetParent(parent.transform, false);
        
        TextMeshProUGUI yTitleText = yTitleGO.AddComponent<TextMeshProUGUI>();
        yTitleText.text = "Count";
        yTitleText.fontSize = 14;
        yTitleText.color = new Color(0.7f, 0.7f, 0.7f);
        yTitleText.alignment = TextAlignmentOptions.Center;
        
        RectTransform yTitleRect = yTitleGO.GetComponent<RectTransform>();
        yTitleRect.anchorMin = new Vector2(0.01f, 0.4f);
        yTitleRect.anchorMax = new Vector2(0.08f, 0.6f);
        yTitleRect.offsetMin = Vector2.zero;
        yTitleRect.offsetMax = Vector2.zero;
        
        // X-axis title
        GameObject xTitleGO = new GameObject("Axis_XTitle");
        xTitleGO.transform.SetParent(parent.transform, false);
        
        TextMeshProUGUI xTitleText = xTitleGO.AddComponent<TextMeshProUGUI>();
        xTitleText.text = "Emergence Types";
        xTitleText.fontSize = 14;
        xTitleText.color = new Color(0.7f, 0.7f, 0.7f);
        xTitleText.alignment = TextAlignmentOptions.Center;
        
        RectTransform xTitleRect = xTitleGO.GetComponent<RectTransform>();
        xTitleRect.anchorMin = new Vector2(0.15f, 0.05f);
        xTitleRect.anchorMax = new Vector2(0.95f, 0.15f);
        xTitleRect.offsetMin = Vector2.zero;
        xTitleRect.offsetMax = Vector2.zero;
    }
}
