using System;
using UnityEditor;
using UnityEngine;

namespace Codex.AutoModelProcessor
{
    internal static class AMPProjectSetup
    {
        public static void EnsureProjectSettings(Action<string> log)
        {
            EnsureLayer("Grass", log);
            EnsureLayer("Obstacle", log);
            EnsureTag("NavMesh", log);
        }

        private static void EnsureLayer(string name, Action<string> log)
        {
            if (LayerMask.NameToLayer(name) >= 0) return;
            var manager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = manager.FindProperty("layers");
            for (var i = 8; i < 32; i++)
            {
                var slot = layers.GetArrayElementAtIndex(i);
                if (!string.IsNullOrEmpty(slot.stringValue)) continue;
                slot.stringValue = name;
                manager.ApplyModifiedProperties();
                log?.Invoke($"Layer '{name}' 不存在，已自动创建（User Layer {i}）。");
                return;
            }
            throw new InvalidOperationException($"无法创建 Layer '{name}'：User Layer 槽位已满。");
        }

        private static void EnsureTag(string name, Action<string> log)
        {
            try { GameObject.FindWithTag(name); return; }
            catch (UnityException) { }
            var manager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var tags = manager.FindProperty("tags");
            for (var i = 0; i < tags.arraySize; i++)
                if (tags.GetArrayElementAtIndex(i).stringValue == name) return;
            tags.InsertArrayElementAtIndex(tags.arraySize);
            tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = name;
            manager.ApplyModifiedProperties();
            log?.Invoke($"Tag '{name}' 不存在，已自动创建。");
        }
    }
}
