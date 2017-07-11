﻿using System;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GraphicsTestFramework
{
    // ------------------------------------------------------------------------------------
    // TestLogicBase
    // - Lowest level TestLogic class that all logics derive from
    // - Hides most logic away from end user

    public abstract class TestLogicBase : MonoBehaviour
    {
        // ------------------------------------------------------------------------------------
        // Variables

        // Basic
        [HideInInspector] public TestEntry activeTestEntry;
        [HideInInspector] public bool baselineExists;
        public RunnerType activeRunType;
        public string suiteName;
        public bool waitingForCallback = false;
        bool testWasRan; // Track whether the test was ran

        // Type Specific
        [HideInInspector] public string testTypeName; // Test type name
        public Type display { get; set; } // Reference to the logics display type

        // Results
        public object baseline; //Baseline to compare to (cast to logic's result class)
        public ResultsBase activeResultData; //Results data to write to (cast to logic's result class)
        public Type results; // Type specific results class to cast to=

        // ------------------------------------------------------------------------------------
        // Broadcast

        // Broadcast to TestList when test has ended
        public static event Broadcast.EndTestAction endTestAction;
		public void BroadcastEndTestAction ()
		{
			if (endTestAction != null)
				endTestAction ();
		}

        public static event Broadcast.ContinueTest waitCallback;
        public void WaitCallback()
        {
            if (waitCallback != null)
                waitCallback();
        }

        // Subscribe to event delegates
        void OnEnable()
        {
            ResultsIO.endResultsSave += ConfirmResultsSaved;
            waitCallback += ContinueTest;
        }

        // Desubscribe from event delegates
        void OnDisable()
        {
            ResultsIO.endResultsSave -= ConfirmResultsSaved;
            waitCallback -= ContinueTest;
        }

        // ------------------------------------------------------------------------------------
        // Initialization

        public virtual void SetupLogic()
        {
            // Test type specific
        }

        public void SetName()
        {
            testTypeName = this.GetType().ToString().Replace("GraphicsTestFramework.", "").Replace("Logic", "");
        }

        public void SetSuiteName(string input)
        {
            suiteName = input;
        }

        public abstract void SetModel(TestModelBase inputModel);

        public abstract void SetDisplay();

        public abstract void SetResults();

        public abstract void SetSettings();

        public abstract void UseLocalResult(ResultsIOData localResult);

        // ------------------------------------------------------------------------------------
        // Test Execution

        // Set initial information for test at beginning of test run
        public void SetupTest(TestEntry inputEntry, RunnerType runType)
        {
            ProgressScreen.Instance.SetState(true, ProgressType.LocalSave, "Preparing test"); // Enable ProgressScreen
            testWasRan = false; // Reset
            activeTestEntry = inputEntry; // Store active TestEntry
            activeRunType = runType; // Store active RunnerType
            SetSettings(); // Set settings to internal
            SetupResultsStructs(); // Setup the results structs to be filled
            CheckForBaseline(); // Check for baselines
            Console.Instance.Write(DebugLevel.Full, MessageLevel.Log, this.GetType().Name + " set up test " + activeTestEntry.testName); // Write to console
            if(runType == RunnerType.Manual) // If manual runner
            {
                ResultsIOData localResult = ResultsIO.Instance.RetrieveResult(suiteName, GetType().ToString().Replace("GraphicsTestFramework.", "").Replace("Logic", ""), activeResultData.common); // Try get local result
                if (localResult == null) // If not found
                    TestPreProcess(); // Start pre-process
                else // If found
                    UseLocalResult(localResult); // Use local
            }
            else
                TestPreProcess(); // Start pre-process
        }

        // First injection point for custom code. Runs before any test logic.
        public virtual void TestPreProcess()
        {
            // Custom test pre-processing logic here
            StartTest(); // Start test
        }

        // Start main test logic
        public void StartTest()
        {
            ProgressScreen.Instance.SetState(true, ProgressType.LocalSave, "Running test"); // Enable ProgressScreen
            Console.Instance.Write(DebugLevel.Logic, MessageLevel.Log, this.GetType().Name + " started test " + activeTestEntry.testName); // Write to console
            testWasRan = true; // Track
            StartCoroutine(ProcessResult()); // Process test results
        }

        // Logic for creating results data
        public virtual IEnumerator ProcessResult()
        {
            yield return null;
            // Custom test result processing logic here
            BuildResultsStruct(null); // Null in base logic. Will not run.
        }

        // Call this to end wait timer on "Callback" mode
        public void ContinueTest()
        {
            waitingForCallback = false; // Reset
        }

        // Build results after main test logic is completed
        public void BuildResultsStruct(ResultsBase input)
        {
            if(input != null) // Null check
            {
                Console.Instance.Write(DebugLevel.Full, MessageLevel.Log, this.GetType().Name + " building results for test " + activeTestEntry.testName); // Write to console
                activeResultData = input; // Set active results data
            }
            TestPostProcess(); // Start post-process
        }

        // Last injection point for custom code. Runs after all test logic.
        public virtual void TestPostProcess()
        {
            // Custom test post-processing logic here
            EndTest(); // End test
        }

        // Logic for test end. Call to end test logic.
        public void EndTest()
        {
            Console.Instance.Write(DebugLevel.Logic, MessageLevel.Log, this.GetType().Name + " ending test " + activeTestEntry.testName); // Write to console
            if (activeRunType == RunnerType.Automation) // If automation run
                SubmitResults(baselineExists ? 0 : 1); // Submit results
            else // If manual run
            {
                bool resolve = TestRunner.Instance.runnerType == RunnerType.Resolve ? true : false; // Is resolve?
                GetComponent<TestDisplayBase>().EnableTestViewer(activeResultData, new TestViewerToolbar.State(!resolve, !resolve, !resolve && testWasRan, true, true)); // Enable test viewer with active results data
            }
        }

        // Check for test pass
        public bool CheckForTestPass()
        {
            if (activeResultData != null) // Check null
                return activeResultData.common.PassFail; // Return pass fail
            else
                return true; // Return true (continue)
        }

        // ------------------------------------------------------------------------------------
        // Results Methods

        // Abstract - Setting up results structs is dependant on each logic's results class
        public abstract void SetupResultsStructs();

        // Get the results struct as an object (never called from the base class as there is no Type for the object)
        public object GetResultsStruct()
        {
            Console.Instance.Write(DebugLevel.Full, MessageLevel.Log, this.GetType().Name + " getting results struct"); // Write to console
            return activeResultData; // Return the active results
        }

        // Submit results data to ResultsIO
        public void SubmitResults(int baseline)
        {
            ProgressScreen.Instance.SetState(true, ProgressType.LocalSave, "Submitting results"); // Enable ProgressScreen
            ResultsIOData output = SerializeResults(); // Serialize activeResultsData
            Console.Instance.Write(DebugLevel.Logic, MessageLevel.Log, this.GetType().Name + " sending data to ResultsIO for " + activeTestEntry.testName); // Write to console
            ResultsIO.Instance.ProcessResults(activeTestEntry.suiteName, activeTestEntry.typeName/*testTypeName*/, output, baseline); // Send data to ResultsIO
        }

        // Called from ConfirmResultsSave delegate when ResultsIO is done saving files
        void ConfirmResultsSaved()
        {
            if (TestTypeManager.Instance.GetActiveTestLogic() == this) // Check this is the active test logic
            {
                Console.Instance.Write(DebugLevel.Logic, MessageLevel.Log, this.GetType().Name + " confirmed results save for test"); // Write to console
                switch(activeRunType)
                {
                    case RunnerType.Resolve:
                        TestViewerToolbar.Instance.OnClickNext(); // Emulate OnClickNext on ViewerToolbar
                        break;
                    case RunnerType.Manual:
                        BroadcastEndTestAction(); // Broadcast to TestList that rest is completed
                        break;
                    case RunnerType.Automation:
                        if (Configuration.Instance.settings.testviewerOnAutomationTestFail) // Should test viewer open on auto fail?
                        {
                            if(CheckForTestPass()) // Did the test pass?
                                BroadcastEndTestAction(); // Broadcast to TestList that rest is completed
                            else
                                GetComponent<TestDisplayBase>().EnableTestViewer(activeResultData, new TestViewerToolbar.State(false, true, false, false, true)); // Enable test viewer with active results data
                        }
                        else
                            BroadcastEndTestAction(); // Broadcast to TestList that rest is completed
                        break;
                }
            }
        }

        // ------------------------------------------------------------------------------------
        // Comparison Methods

        // Get comparison data
        public object GetComparisonData(ResultsBase resultsData)
        {
            ResultsIOData baselineFetch = ResultsIO.Instance.RetrieveBaseline(suiteName, testTypeName, resultsData.common); // Get baseline data
            if (baselineFetch != null) // If successful
            {
                ResultsBase baselineData = (ResultsBase)DeserializeResults(baselineFetch); // Convert to results class
                return ProcessComparison(baselineData, resultsData); // Process comparison
            }
            else
                return null; // Return fail
        }

        public abstract object ProcessComparison(ResultsBase baselineData, ResultsBase resultsData);

        // ------------------------------------------------------------------------------------
        // Display Methods

        // Called by the TestViewer when restarting the current test
        public void RestartTest()
        {
            SetupResultsStructs(); // Update common
            StartTest(); // Restart
        }

        // ------------------------------------------------------------------------------------
        // Helper methods
        // TODO - Clean and comment

        // Check for a baseline (called once for every test that is run)
        public void CheckForBaseline()
        {
            ProgressScreen.Instance.SetState(true, ProgressType.LocalLoad, "Retrieving baseline data"); // Enable ProgressScreen
            baselineExists = ResultsIO.Instance.BaselineExists(activeTestEntry.suiteName, "Standard Legacy", activeTestEntry.typeName/*testTypeName*/, activeTestEntry.groupName, activeTestEntry.testName); // Check for baseline
        }

        //Convert an array on unknown type to a typed array
        // TODO - Revisit this. Requires hard coding a conversion for each object type
        public void GenerateGenericArray(FieldInfo fieldInfo, Type resultType, object resultObject, Type arrayType, string arrayValue)
        {
            var resultData = System.Convert.ChangeType(resultObject, resultType);
            string[] stringArray = arrayValue.Split(new string[1] { "|" }, StringSplitOptions.RemoveEmptyEntries);
            Console.Instance.Write(DebugLevel.Full, MessageLevel.Log, GetType().Name + " is generating Generic Array for " + resultData + " of type " + arrayType.ToString()); // Write to console
            switch (arrayType.ToString())
            {
                case "System.Object":
                    object[] objectArray = new object[stringArray.Length];
                    for (int i = 0; i < objectArray.Length; i++)
                        objectArray[i] = Convert.ChangeType(stringArray[i], arrayType);
                    fieldInfo.SetValue(resultData, objectArray);
                    break;
                case "System.String":
                    fieldInfo.SetValue(resultData, stringArray);
                    break;
                case "System.Byte":
                    byte[] byteArray = new byte[stringArray.Length];
                    for (int i = 0; i < byteArray.Length - 1; i++)
                        byteArray[i] = Convert.ToByte(stringArray[i]);
                    fieldInfo.SetValue(resultData, byteArray);
                    break;
            }
        }

        // ------------------------------------------------------------------------------------
        // Serialization
        // TODO - Clean and comment (DANGER)

        // Serialize ResultsData(class) to ResultsIOData(string arrays)
        public ResultsIOData SerializeResults()
        {
            ResultsIOData output = new ResultsIOData();
            for (int r = 0; r < 2; r++)
                output.resultsRow.Add(new ResultsIORow());
            BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
            FieldInfo[] commonFields = typeof(ResultsDataCommon).GetFields(bindingFlags);
            FieldInfo[] customFields = results.GetFields(bindingFlags);

            for (int f = 0; f < commonFields.Length; f++)
                output.resultsRow[0].resultsColumn.Add(commonFields[f].Name);
            for (int f = 0; f < customFields.Length-1; f++)
                output.resultsRow[0].resultsColumn.Add(customFields[f].Name);
            
            FieldInfo commonField = activeResultData.GetType().GetField("common");
            var commonFieldValue = commonField.GetValue(activeResultData);
            output.resultsRow[1].commonResultsIOData = (ResultsDataCommon)commonFieldValue;
            output.resultsRow[1].resultsColumn = new List<string>();
            ResultsDataCommon resultsCommonTemplate = new ResultsDataCommon();
            for (int f = 0; f < commonFields.Length; f++)
            {
                var typedResult = Convert.ChangeType(activeResultData, results); // TODO - Why does this work...
                FieldInfo typedCommonField = typedResult.GetType().GetField("common"); // TODO - Why does this work...
                var typedCommonValue = Convert.ChangeType(typedCommonField.GetValue(typedResult), resultsCommonTemplate.GetType()); // TODO - Why does this work...
                var commonResult = typedCommonValue.GetType().GetField(commonFields[f].Name).GetValue(typedCommonValue);
                output.resultsRow[1].resultsColumn.Add(commonResult.ToString());
            }
            for (int f = 0; f < customFields.Length-1; f++)
            {
                var customResult = activeResultData.GetType().GetField(customFields[f].Name).GetValue(activeResultData);
                if (activeResultData.GetType().GetField(customFields[f].Name).FieldType.IsArray) //If its an array (tough to handle)
                {
                    Array a = (Array)activeResultData.GetType().GetField(customFields[f].Name).GetValue(activeResultData);
                    if (a != null) // Null check incase custom results werent set on an array
                    {
                        string[] stringArray = new string[a.Length];
                        for (int i = 0; i < a.Length; i++)
                            stringArray[i] = a.GetValue(i).ToString();
                        customResult = Common.ConvertStringArrayToString(stringArray);
                        output.resultsRow[1].resultsColumn.Add(customResult.ToString());
                    }
                    else // Write blank when custom results werent set on an array
                        customResult = "";
                }
                else if (customResult != null) //If its a non-array type that has had values set
                    output.resultsRow[1].resultsColumn.Add(customResult.ToString());
                else //If its a non-array type that has not had values set
                    output.resultsRow[1].resultsColumn.Add("");
            }
            Console.Instance.Write(DebugLevel.Full, MessageLevel.Log, GetType().Name + " generated resultsIO data"); // Write to console
            return output;
        }

        // Deserialize ResultsIOData(string arrays) to ResultsData(class)
        public virtual object DeserializeResults(ResultsIOData resultsIOData)
        {
            return new object();
        }
    }

    // ------------------------------------------------------------------------------------
    // TestLogic
    // - Next level TestLogic class that all user facing logics derive from
    // - Adds an abstraction layer for defining model type

    public abstract class TestLogic<M, D, R> : TestLogicBase where M : TestModelBase where D : TestDisplayBase where R : ResultsBase
    {
        // ------------------------------------------------------------------------------------
        // Variables

        public M model { get; set; } // Reference to the logics model type        
        public float waitTimer = 0f; // Track wait timing for seconds

        // ------------------------------------------------------------------------------------
        // Set Methods

        // Set test model instance
        public override void SetModel(TestModelBase inputModel)
        {
            model = (M)inputModel; // Cast to type and set
        }

        // Set test display type
        public override void SetDisplay()
        {
            display = typeof(D); // Set type
        }

        // Initialize results structure
        public override void SetResults()
        {
            results = typeof(R); // Set type
            ResultsBase newData = (R)Activator.CreateInstance(results); // Create instance
            newData.common = new ResultsDataCommon(); // Initialize common
            activeResultData = newData; // Set as active
        }

        // Initialize settings structure
        public override void SetSettings()
        {
            model.SetSettings();
        }

        // ------------------------------------------------------------------------------------
        // Initialization

        // Setup the results structs every test
        public override void SetupResultsStructs()
        {
            ResultsBase newResultsData = (R)Activator.CreateInstance(results); // Create instance
            newResultsData.common = Common.GetCommonResultsData(); // Initialize common
            newResultsData.common.GroupName = activeTestEntry.groupName; // Set scene name
            newResultsData.common.TestName = activeTestEntry.testName; // Set test name
            activeResultData = newResultsData; // Set as active
        }

        // ------------------------------------------------------------------------------------
        // Test Execution

        // Use local result in Manual mode if it exists
        public override void UseLocalResult(ResultsIOData localResult)
        {
            ResultsBase typedResult = (R)DeserializeResults(localResult); // Deserialize result to typed
            activeResultData = typedResult; // Set to active results
            EndTest(); // End test
        }

        // Wait for specified timer (requires model data)
        public IEnumerator WaitForTimer()
        {
            switch(model.settings.waitType)
            {
                case SettingsBase.WaitType.Frames:
                    for (int i = 0; i < Mathf.Round(model.settings.waitTimer); i++) // Wait for requested wait frame count
                        yield return new WaitForEndOfFrame(); // Wait for end of frame
                    break;
                case SettingsBase.WaitType.Seconds:
                    while (waitTimer <= model.settings.waitTimer) // While timeris less than settings timer
                    {
                        waitTimer += Time.deltaTime; // Increment
                        yield return null; // Wait
                    }
                    waitTimer = 0f; // Reset
                    break;
                case SettingsBase.WaitType.StableFramerate:
                    yield return WaitForStableFramerate(); // Test for stable framerate
                    break;
                case SettingsBase.WaitType.Callback:
                    waitingForCallback = true; // Set waiting to true
                    while (waitingForCallback) // While waiting
                    {
                        waitTimer += Time.deltaTime; // Increment
                        if(waitTimer <= 60f)
                            yield return null;
                        else
                        {
                            waitTimer = 0f; // Reset
                            break;
                        }
                    } 
                    break;
            }
        }

        // ------------------------------------------------------------------------------------
        // Stable Framerate

        // Parameters
        StableFramerateParameters stableFramerateParameters = new StableFramerateParameters();
        class StableFramerateParameters
        {
            public float time; // Track time at previous timestamp
            public int samples; // Track samples at previous timestamp
            public int frameCount = 4;  // Amount of frames to track
            public List<float> frameTimes = new List<float>(); // List for tracking frame times
            public int maxFrames = 60;
            public int framesTested = 0;
            public float threshold = 0.5f; // Threshold for pass
        }

        // Wait for stable framerate
        IEnumerator WaitForStableFramerate()
        {
            while (stableFramerateParameters.frameTimes.Count < stableFramerateParameters.frameCount) // Still building frame list
            {
                stableFramerateParameters.frameTimes.Add(TimestampLight()); // Add timestamp
                yield return new WaitForEndOfFrame(); // Wait for frame
            }
            while (!EvaluateFramerateStability()) // Testing results for stability
            {
                stableFramerateParameters.frameTimes.RemoveAt(0); // Remove first entry
                stableFramerateParameters.frameTimes.Add(TimestampLight()); // Add timestamp
                stableFramerateParameters.framesTested++; //Increment max frames
                if (stableFramerateParameters.framesTested >= stableFramerateParameters.maxFrames) // Check whether hit max frames
                    break; // Exit
                yield return new WaitForEndOfFrame(); // Wait for frame
            }
        }

        // Evaulate the stored frames
        bool EvaluateFramerateStability()
        {
            float[] array = stableFramerateParameters.frameTimes.ToArray(); // Convert to array
            Array.Sort(array); // Sort the array
            if (array[array.Length - 1] - array[0] < stableFramerateParameters.threshold) // If difference between fastest and slowest is within threshold
                return true; // Pass
            else
                return false; // Fail
        }

        // Calculate time and frames since last Timestamp and return an average
        float TimestampLight()
        {
            float currentTime = Time.realtimeSinceStartup * 1000; // Get current time
            int currentSamples = Time.frameCount; // Get current samples
            float elapsedTime = currentTime - stableFramerateParameters.time; // Get elapsed time since last Timestamp
            int elapsedSamples = currentSamples - stableFramerateParameters.samples; // Get elapsed samples since last Timestamp
            stableFramerateParameters.time = currentTime; // Reset time
            stableFramerateParameters.samples = currentSamples; // Reset samples
            return elapsedTime / (float)elapsedSamples; // Return
        }

        // ------------------------------------------------------------------------------------
        // Serialization
        // TODO - Clean and comment (DANGER)

        // Deserialize ResultsIOData(string arrays) to ResultsData(class)
        public override object DeserializeResults(ResultsIOData resultsIOData)
        {
            //var resultData = Convert.ChangeType(activeBaselineData, results); // Create instance (Old - Used from base class)
            ResultsBase resultData = (R)Activator.CreateInstance(results); // Create instance
            var common = new ResultsDataCommon(); //blank common data

            BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
            FieldInfo[] commonFields = typeof(ResultsDataCommon).GetFields(bindingFlags);
            FieldInfo[] customFields = results.GetFields(bindingFlags);

            List<string> commonDataRaw = resultsIOData.resultsRow[0].resultsColumn.GetRange(0, commonFields.Length * 2);
            List<string> resultsDataRaw = resultsIOData.resultsRow[0].resultsColumn.GetRange(commonFields.Length * 2, resultsIOData.resultsRow[0].resultsColumn.Count - (commonFields.Length * 2));

            for (int f = 0; f < customFields.Length; f++)
            {
                if (f == 0)
                {
                    //do the common class
                    for (int cf = 0; cf < commonFields.Length; cf++)
                    {
                        string value = commonDataRaw[(cf * 2) + 1];
                        FieldInfo fieldInfo = common.GetType().GetField(commonFields[cf].Name);
                        fieldInfo.SetValue(common, Convert.ChangeType(value, fieldInfo.FieldType));
                    }
                }
                else
                {
                    var value = resultsDataRaw[(f * 2) - 1];
                    FieldInfo fieldInfo = resultData.GetType().GetField(customFields[0].Name); // TODO - Why did this become 0?
                    if (fieldInfo.FieldType.IsArray) // This handles arrays
                    {
                        Type type = resultData.GetType().GetField(customFields[f].Name).FieldType.GetElementType();
                        GenerateGenericArray(fieldInfo, resultData.GetType(), resultData, type, value);
                    }
                    else // Non array types
                    {
                        fieldInfo.SetValue(resultData, Convert.ChangeType(value, fieldInfo.FieldType));
                    }
                }
            }
            resultData.common = common; // Assign common
            return resultData;
        }
    }
}
