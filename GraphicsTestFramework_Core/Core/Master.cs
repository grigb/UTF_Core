﻿using System;
using UnityEngine;

namespace GraphicsTestFramework
{
    // ------------------------------------------------------------------------------------
    // Master
    // - System data structures and returns
    // - Maintains persistence of other logic objects

    public class Master : MonoBehaviour
    {
        // ------------------------------------------------------------------------------------
        // Variables

        // Singleton
        private static Master _Instance = null;
        public static Master Instance
        {
            get
            {
                if (_Instance == null)
                    _Instance = (Master)FindObjectOfType(typeof(Master));
                return _Instance;
            }
        }

        // TODO - Remove
        public enum DebugMode { None, Messages, DummyData, OnlyMessages };
        public DebugMode debugMode;

        //Data
        public float applicationVersion;
        //public string buildDirectory; // TODO - Consider removing. Not currently using custom build menu
        //public string buildName; // TODO - Consider removing. Not currently using custom build menu

        // ------------------------------------------------------------------------------------
        // Setup

        // On Awake
        private void Awake()
        {
            DontDestroyOnLoad(gameObject); // Set this object to DontDestroy
        }

        // ------------------------------------------------------------------------------------
        // Get System Data

        // Get SystemData to use for building ResultsCommon
        public SystemData GetSystemData()
		{
            Console.Instance.Write(DebugLevel.Full, MessageLevel.Log, "Getting system data"); // Write to console
            SystemData output = new SystemData(); // Create new class instance
			output.UnityVersion = Application.unityVersion; // Get Unity version
			output.AppVersion = applicationVersion.ToString(); // Get application version
			output.Platform = Application.platform.ToString(); // Get platform
			output.API = SystemInfo.graphicsDeviceType.ToString(); // Get graphics device type
			return output; // Return
		}

        // Get the current system time
		public DateTime GetSystemTime()
        {
            Console.Instance.Write(DebugLevel.Full, MessageLevel.Log, "Getting system time"); // Write to console
            return DateTime.UtcNow; // Return current DateTime
		}
    }

    // ------------------------------------------------------------------------------------
    // Global Data Structures

    // System data class
    [System.Serializable]
	public class SystemData
	{
		public string UnityVersion;
		public string AppVersion;
		public string Platform;
		public string API;
	}
}
