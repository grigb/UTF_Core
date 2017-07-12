﻿using UnityEngine;
using UnityEngine.UI;

namespace GraphicsTestFramework
{
    // ------------------------------------------------------------------------------------
    // FlipBookDisplay
    // - Controls context logic for TestViewer and ResultsViewer

    public class FlipBookDisplay : TestDisplay<FlipBookLogic>
    {
        // ------------------------------------------------------------------------------------
        // Variables

        Material m_Material; // Comparison image material
        public Material material // Public get/set
        {
            get
            {
                if (m_Material == null)
                    m_Material = new Material(Shader.Find("Hidden/FlipBook")) { hideFlags = HideFlags.DontSave };
                return m_Material;
            }
        }

        // ResultsViewer
        public RawImage resultsContextImage;
        public Button[] buttons;

        // ------------------------------------------------------------------------------------
        // TestViewer

        // Setup viewer tabs
        public override TestViewerTabData[] GetViewerTabs(ResultsBase resultsObject)
        {
            var typedSettings = (FlipBookSettings)logic.model.settings; // Set settings to local type
            TestViewerTabData[] output = new TestViewerTabData[1]; // Create empty output (mandatory)
            switch (logic.baselineExists)  // Switch on baseline exists
            {
                case false:
                    var localResultData = (FlipBookResults)resultsObject; // Convert the input results object to this types class (mandatory)
                    output = new TestViewerTabData[2] // Want two tabs
                    {
                        new TestViewerTabData("Live Camera", TestViewerTabType.Camera, typedSettings.captureCamera, null), // Live camera showing capture camera
                        new TestViewerTabData("Results Texture", TestViewerTabType.Texture, localResultData.resultFrames[0], null) // And the results texture
                    };
                    break;
                case true:
                    var comparisonData = (FlipBookLogic.ComparisonData)logic.GetComparisonData(resultsObject); // Get the comparison data for this test in this types class (mandatory)
                    SetupMaterial(comparisonData.baselineTex, comparisonData.resultsTex); // Setup the material
                    output = new TestViewerTabData[4] // Want four tabs
                    {
                        new TestViewerTabData("Live Camera", TestViewerTabType.Camera, typedSettings.captureCamera, null), // Live camera showing capture camera
                        new TestViewerTabData("Results Texture", TestViewerTabType.Texture, comparisonData.resultsTex, null), // And the results texture
                        new TestViewerTabData("Comparison Texture", TestViewerTabType.Material, material, new TestViewerTabData.TestViewerTabStatistic[] // And the material for the comparison display
                        {
                            new TestViewerTabData.TestViewerTabStatistic("Diff", comparisonData.DiffPercentage.ToString()) // Enable the statistics window and display the diff
                        }),
                        new TestViewerTabData("Baseline Texture", TestViewerTabType.Texture, comparisonData.baselineTex, null) // And the baseline texture
                    };
                    break;
            }
            return output; // Return
        }

        // ------------------------------------------------------------------------------------
        // ResultsViewer

        // Setup the results context object
        public override void SetupResultsContext(ResultsContext context, ResultsEntry inputEntry)
        {
            FlipBookResults inputResults = (FlipBookResults)logic.DeserializeResults(inputEntry.resultsData); // Deserialize input and cast to typed results
            FlipBookLogic.ComparisonData comparisonData = (FlipBookLogic.ComparisonData)logic.GetComparisonData(inputResults); // Get comparison data
            
            buttons = new Button[3]; // Create button array
            for(int i = 0; i < buttons.Length; i++) // Iterate
            { 
                buttons[i] = context.objects[i].GetComponent<Button>(); // Get the button
                int index = i; // Set this so delegate doesnt get reference that iterates
                buttons[i].onClick.AddListener(delegate { SetTextureContext(comparisonData, index); }); // Add listener
            }
            resultsContextImage = context.objects[3].GetComponent<RawImage>(); // Get image
            context.objects[4].GetComponent<Text>().text = comparisonData.DiffPercentage.ToString(); // Set diff to field
            SetTextureContext(comparisonData, 0); // Set default
        }

        // Set context for textures
        public void SetTextureContext(FlipBookLogic.ComparisonData comparisonData, int context)
        {
            foreach (Button b in buttons) // Iterate buttons
                b.interactable = true; // Enable
            buttons[context].interactable = false; // Disable requested
            switch (context)
            {
                case 0:    // Results
                    resultsContextImage.material = null; // Null material
                    resultsContextImage.texture = comparisonData.resultsTex; // Set texture
                    break;
                case 1:     // Comparison
                    SetupMaterial(comparisonData.baselineTex, comparisonData.resultsTex); // Setup material
                    resultsContextImage.texture = comparisonData.resultsTex; // Set texture
                    resultsContextImage.material = material; // Set material
                    break;
                case 2:     // Baseline
                    resultsContextImage.material = null; // Null material
                    resultsContextImage.texture = comparisonData.baselineTex; // Set texture
                    break;
            }
        }

        // ------------------------------------------------------------------------------------
        // Test Type Specific Methods

        // Setup comparison material
        void SetupMaterial(Texture2D baselineTex, Texture2D resultsTex)
        {
            material.SetTexture("_ReferenceTex", baselineTex); // Set baseline texture
            material.SetTexture("_MainTex", resultsTex); // Set results texture
        }
    }
}
