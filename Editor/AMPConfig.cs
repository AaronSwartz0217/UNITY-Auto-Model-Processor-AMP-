using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Codex.AutoModelProcessor
{
    public enum AMPModelType { Plants, Obstacles }
    public enum AMPConflictPolicy { Overwrite, Skip, Prompt }
    public enum AMPColliderFallback { Box, Sphere, Capsule }
    public enum AMPPrefabGenerationMode { WholeFbx, RootObjects }
    public enum AMPSplitNaming { ObjectName, FbxNameWithIndex }
    public enum AMPMaterialMode { KeepImported, SharedEditableCopy, PerPrefabCopy }
    public enum AMPColliderMode { MeshCollider, None }

    [Serializable]
    public sealed class AMPConfig
    {
        public int configVersion = 25;
        public string plantsInput = "Assets/AMP_RawInput/Plants";
        public string obstaclesInput = "Assets/AMP_RawInput/Obstacles";
        public string plantsOutput = "Assets/AMP_Output/Plants_Prefabs";
        public string obstaclesOutput = "Assets/AMP_Output/Obstacles_Prefabs";
        public AMPConflictPolicy conflictPolicy = AMPConflictPolicy.Prompt;
        public bool standardizeMaterials = true;
        public bool centerHorizontalPivot = true;
        public bool fallbackForComplexConvex = false;
        public int convexTriangleLimit = 255;
        public AMPColliderFallback colliderFallback = AMPColliderFallback.Box;
        public Color materialTint = Color.white;
        public AMPPrefabGenerationMode generationMode = AMPPrefabGenerationMode.WholeFbx;
        public AMPSplitNaming splitNaming = AMPSplitNaming.ObjectName;
        public bool splitIntoSourceSubfolder = true;
        public bool correctEachSplitPivot = true;
        public AMPMaterialMode materialMode = AMPMaterialMode.SharedEditableCopy;
        public AMPColliderMode colliderMode = AMPColliderMode.MeshCollider;
        public AMPColliderMode plantsColliderMode = AMPColliderMode.MeshCollider;
        public AMPColliderMode obstaclesColliderMode = AMPColliderMode.MeshCollider;
        public bool plantsConvex = true;
        public bool obstaclesConvex = true;

        public string InputFor(AMPModelType type) => type == AMPModelType.Plants ? plantsInput : obstaclesInput;
        public string OutputFor(AMPModelType type) => type == AMPModelType.Plants ? plantsOutput : obstaclesOutput;
        public AMPColliderMode ColliderModeFor(AMPModelType type) =>
            type == AMPModelType.Plants ? plantsColliderMode : obstaclesColliderMode;
        public bool ConvexFor(AMPModelType type) => type == AMPModelType.Plants ? plantsConvex : obstaclesConvex;

        public void SetColliderMode(AMPModelType type, AMPColliderMode value)
        {
            if (type == AMPModelType.Plants) plantsColliderMode = value;
            else obstaclesColliderMode = value;
        }

        public void SetConvex(AMPModelType type, bool value)
        {
            if (type == AMPModelType.Plants) plantsConvex = value;
            else obstaclesConvex = value;
        }

        public static AMPConfig Load()
        {
            var json = EditorPrefs.GetString("Codex.AMP.Config", string.Empty);
            if (string.IsNullOrEmpty(json)) return new AMPConfig();
            try
            {
                var config = JsonUtility.FromJson<AMPConfig>(json) ?? new AMPConfig();
                if (config.configVersion < 25)
                {
                    config.configVersion = 25;
                    config.plantsColliderMode = AMPColliderMode.MeshCollider;
                    config.obstaclesColliderMode = AMPColliderMode.MeshCollider;
                    config.plantsConvex = true;
                    config.obstaclesConvex = true;
                    config.fallbackForComplexConvex = false;
                    config.Save();
                }
                return config;
            }
            catch { return new AMPConfig(); }
        }

        public void Save() => EditorPrefs.SetString("Codex.AMP.Config", JsonUtility.ToJson(this));

        public void Export(string path)
        {
            File.WriteAllText(path, JsonUtility.ToJson(this, true));
            AssetDatabase.Refresh();
        }

        public static AMPConfig Import(string path) => JsonUtility.FromJson<AMPConfig>(File.ReadAllText(path));
    }
}
