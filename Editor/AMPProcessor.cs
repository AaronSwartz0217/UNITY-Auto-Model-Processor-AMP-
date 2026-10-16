using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Codex.AutoModelProcessor
{
    public interface IAMPProcessingExtension
    {
        void BeforeSave(GameObject prefabRoot, string sourcePath, AMPModelType type);
    }

    public enum AMPResultKind { Success, Skipped, Failed }

    public readonly struct AMPProcessResult
    {
        public readonly AMPResultKind Kind;
        public readonly string Message;
        public AMPProcessResult(AMPResultKind kind, string message) { Kind = kind; Message = message; }
    }

    public sealed class AMPConflictSession
    {
        internal bool? OverwriteAll { get; set; }
        public bool Cancelled { get; internal set; }
    }

    public static class AMPProcessor
    {
        private const StaticEditorFlags ObstacleFlags = StaticEditorFlags.BatchingStatic |
            StaticEditorFlags.ContributeGI | StaticEditorFlags.OccluderStatic |
            StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic;
        public static readonly List<IAMPProcessingExtension> Extensions = new List<IAMPProcessingExtension>();

        public static AMPProcessResult Process(string sourcePath, AMPModelType type, AMPConfig config,
            Material template, Action<string> log = null, AMPConflictSession conflictSession = null)
        {
            if (!sourcePath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                return new AMPProcessResult(AMPResultKind.Skipped, "非 FBX 文件，已跳过。");
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            if (source == null)
                return new AMPProcessResult(AMPResultKind.Failed, $"无法加载模型：{sourcePath}");

            return config.generationMode == AMPPrefabGenerationMode.RootObjects
                ? ProcessRootObjects(source, sourcePath, type, config, template, log, conflictSession)
                : ProcessWhole(source, sourcePath, type, config, template, log, conflictSession);
        }

        private static AMPProcessResult ProcessWhole(GameObject source, string sourcePath, AMPModelType type,
            AMPConfig config, Material template, Action<string> log, AMPConflictSession conflictSession)
        {

            EnsureFolder(config.OutputFor(type));
            var output = $"{config.OutputFor(type)}/{Path.GetFileNameWithoutExtension(sourcePath)}.prefab";
            if (!CanWrite(output, config.conflictPolicy, log, conflictSession))
                return new AMPProcessResult(AMPResultKind.Skipped, $"已跳过：{output}");

            GameObject root = null;
            try
            {
                root = new GameObject(Path.GetFileNameWithoutExtension(sourcePath));
                var model = (GameObject)PrefabUtility.InstantiatePrefab(source);
                model.name = "Model";
                model.transform.SetParent(root.transform, false);
                ApplyPivot(root, model, config.centerHorizontalPivot, log);
                ApplyIdentity(root, type);
                RemoveExistingColliders(root);
                if (config.ColliderModeFor(type) == AMPColliderMode.MeshCollider)
                    ApplyColliders(root, type, config, log);
                else log?.Invoke("碰撞体处理方式为 None：未添加 Collider 组件。");
                if (config.standardizeMaterials && config.materialMode != AMPMaterialMode.KeepImported)
                    ApplyMaterials(root, output, sourcePath, type, config, template, log);
                foreach (var extension in Extensions) extension?.BeforeSave(root, sourcePath, type);
                var saved = PrefabUtility.SaveAsPrefabAsset(root, output, out var ok);
                if (!ok || saved == null) throw new InvalidOperationException("PrefabUtility.SaveAsPrefabAsset 返回失败。");
                log?.Invoke($"成功生成：{output}");
                return new AMPProcessResult(AMPResultKind.Success, output);
            }
            catch (Exception ex)
            {
                log?.Invoke($"错误：{sourcePath} - {ex.Message}");
                return new AMPProcessResult(AMPResultKind.Failed, ex.Message);
            }
            finally { if (root != null) Object.DestroyImmediate(root); }
        }

        private static AMPProcessResult ProcessRootObjects(GameObject source, string sourcePath, AMPModelType type,
            AMPConfig config, Material template, Action<string> log, AMPConflictSession conflictSession)
        {
            GameObject sourceInstance = null;
            var success = 0;
            var skipped = 0;
            var failed = 0;
            var sourceName = Path.GetFileNameWithoutExtension(sourcePath);
            var outputFolder = config.OutputFor(type);
            if (config.splitIntoSourceSubfolder) outputFolder += "/" + Sanitize(sourceName);

            try
            {
                sourceInstance = (GameObject)PrefabUtility.InstantiatePrefab(source);
                var candidates = FindSplitCandidates(sourceInstance.transform);
                if (candidates.Count == 0)
                    return new AMPProcessResult(AMPResultKind.Failed, $"{sourcePath} 中未找到可拆分的 Mesh 对象。");

                EnsureFolder(outputFolder);
                log?.Invoke($"检测到 {candidates.Count} 个并列对象，开始拆分：{sourcePath}");
                var usedNames = new List<string>();
                for (var i = 0; i < candidates.Count; i++)
                {
                    if (conflictSession != null && conflictSession.Cancelled) break;
                    var candidate = candidates[i];
                    var baseName = config.splitNaming == AMPSplitNaming.FbxNameWithIndex
                        ? $"{sourceName}_{i + 1:00}"
                        : candidate.name;
                    var prefabName = MakeUniqueName(Sanitize(baseName), usedNames);
                    var output = $"{outputFolder}/{prefabName}.prefab";
                    if (!CanWrite(output, config.conflictPolicy, log, conflictSession)) { skipped++; continue; }

                    GameObject root = null;
                    try
                    {
                        root = new GameObject(prefabName);
                        var clone = Object.Instantiate(candidate.gameObject);
                        clone.name = candidate.name;
                        clone.transform.SetParent(root.transform, false);
                        clone.transform.localPosition = candidate.localPosition;
                        clone.transform.localRotation = candidate.localRotation;
                        clone.transform.localScale = candidate.localScale;
                        if (config.correctEachSplitPivot) ApplyPivot(root, clone, config.centerHorizontalPivot, log);
                        ApplyIdentity(root, type);
                        RemoveExistingColliders(root);
                        if (config.ColliderModeFor(type) == AMPColliderMode.MeshCollider)
                            ApplyColliders(root, type, config, log);
                        else log?.Invoke($"{prefabName}：碰撞体处理方式为 None，未添加 Collider 组件。");
                        if (config.standardizeMaterials && config.materialMode != AMPMaterialMode.KeepImported)
                            ApplyMaterials(root, output, sourcePath, type, config, template, log);
                        foreach (var extension in Extensions) extension?.BeforeSave(root, sourcePath, type);
                        var saved = PrefabUtility.SaveAsPrefabAsset(root, output, out var ok);
                        if (!ok || saved == null) throw new InvalidOperationException("Prefab 保存失败。");
                        success++;
                        log?.Invoke($"拆分成功 [{i + 1}/{candidates.Count}]：{output}");
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        log?.Invoke($"拆分失败：{candidate.name} - {ex.Message}");
                    }
                    finally { if (root != null) Object.DestroyImmediate(root); }
                }
            }
            catch (Exception ex)
            {
                log?.Invoke($"拆分错误：{sourcePath} - {ex.Message}");
                return new AMPProcessResult(AMPResultKind.Failed, ex.Message);
            }
            finally { if (sourceInstance != null) Object.DestroyImmediate(sourceInstance); }

            var summary = $"拆分完成：成功 {success}，失败 {failed}，跳过 {skipped}。";
            if (success > 0) return new AMPProcessResult(AMPResultKind.Success, summary);
            if (failed > 0) return new AMPProcessResult(AMPResultKind.Failed, summary);
            return new AMPProcessResult(AMPResultKind.Skipped, summary);
        }

        private static List<Transform> FindSplitCandidates(Transform importedRoot)
        {
            var result = new List<Transform>();
            for (var i = 0; i < importedRoot.childCount; i++)
            {
                var child = importedRoot.GetChild(i);
                if (HasMesh(child)) result.Add(child);
            }
            // Some exporters add one empty wrapper around all otherwise parallel objects.
            if (result.Count == 1 && !HasOwnMesh(result[0]) && result[0].childCount > 1)
            {
                var wrapper = result[0];
                result.Clear();
                for (var i = 0; i < wrapper.childCount; i++)
                    if (HasMesh(wrapper.GetChild(i))) result.Add(wrapper.GetChild(i));
            }
            // Some FBX exporters place the only Mesh directly on the imported asset root.
            // In that case there is no child object to discover, but the root itself is a valid prefab candidate.
            if (result.Count == 0 && HasOwnMesh(importedRoot)) result.Add(importedRoot);
            return result;
        }

        private static bool HasOwnMesh(Transform value) =>
            value.GetComponent<MeshFilter>() != null || value.GetComponent<SkinnedMeshRenderer>() != null;

        private static bool HasMesh(Transform value) =>
            value.GetComponentInChildren<MeshFilter>(true) != null ||
            value.GetComponentInChildren<SkinnedMeshRenderer>(true) != null;

        private static bool CanWrite(string output, AMPConflictPolicy policy, Action<string> log,
            AMPConflictSession session)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(output) == null) return true;
            if (policy == AMPConflictPolicy.Overwrite) return true;
            if (policy == AMPConflictPolicy.Skip) { log?.Invoke($"已存在，跳过：{output}"); return false; }
            if (session != null && session.Cancelled) return false;
            if (session != null && session.OverwriteAll.HasValue)
            {
                if (!session.OverwriteAll.Value) log?.Invoke($"按本次批处理规则跳过：{output}");
                return session.OverwriteAll.Value;
            }

            var choice = EditorUtility.DisplayDialogComplex("Auto Model Processor - 文件冲突",
                $"Prefab 已存在：\n{output}\n\n请选择本次批处理中所有同名文件的统一处理方式。",
                "全部覆盖", "取消批处理", "全部跳过");
            if (choice == 0)
            {
                if (session != null) session.OverwriteAll = true;
                log?.Invoke("本次批处理的冲突策略：全部覆盖。");
                return true;
            }
            if (choice == 2)
            {
                if (session != null) session.OverwriteAll = false;
                log?.Invoke("本次批处理的冲突策略：全部跳过。");
                return false;
            }
            if (session != null) session.Cancelled = true;
            log?.Invoke("用户取消了本次批处理。");
            return false;
        }

        private static string MakeUniqueName(string baseName, List<string> used)
        {
            if (string.IsNullOrWhiteSpace(baseName)) baseName = "Object";
            var value = baseName;
            var suffix = 2;
            while (used.Exists(x => string.Equals(x, value, StringComparison.OrdinalIgnoreCase)))
                value = $"{baseName}_{suffix++:00}";
            used.Add(value);
            return value;
        }

        private static void RemoveExistingColliders(GameObject root)
        {
            foreach (var collider in root.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(collider);
        }

        private static void ApplyPivot(GameObject root, GameObject model, bool centerHorizontal, Action<string> log)
        {
            var renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) { log?.Invoke($"警告：{root.name} 无 Renderer，保留默认原点。"); return; }
            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            var offset = centerHorizontal
                ? new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z)
                : new Vector3(0f, -bounds.min.y, 0f);
            model.transform.position += offset;
        }

        private static void ApplyIdentity(GameObject root, AMPModelType type)
        {
            root.layer = LayerMask.NameToLayer(type == AMPModelType.Plants ? "Grass" : "Obstacle");
            root.tag = type == AMPModelType.Plants ? "Untagged" : "NavMesh";
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                transform.gameObject.layer = root.layer;
                GameObjectUtility.SetStaticEditorFlags(transform.gameObject,
                    type == AMPModelType.Obstacles ? ObstacleFlags : 0);
            }
        }

        private static void ApplyColliders(GameObject root, AMPModelType type, AMPConfig config, Action<string> log)
        {
            var filters = root.GetComponentsInChildren<MeshFilter>(true);
            var skinnedRenderers = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (filters.Length == 0 && skinnedRenderers.Length == 0)
                throw new InvalidOperationException("模型不包含有效 MeshFilter 或 SkinnedMeshRenderer，无法生成碰撞体。");
            foreach (var filter in filters)
            {
                var mesh = filter.sharedMesh;
                if (mesh == null) continue;
                AddColliderForMesh(filter.gameObject, mesh, type, config, log);
            }
            foreach (var renderer in skinnedRenderers)
                if (renderer.sharedMesh != null)
                    AddColliderForMesh(renderer.gameObject, renderer.sharedMesh, type, config, log);

            var colliders = root.GetComponentsInChildren<Collider>(true);
            if (colliders.Length == 0)
                throw new InvalidOperationException("碰撞体生成后校验失败：Prefab 中没有 Collider。");
            foreach (var collider in colliders)
                log?.Invoke($"已添加碰撞体：{GetHierarchyPath(root.transform, collider.transform)} ({collider.GetType().Name})");
        }

        private static void AddColliderForMesh(GameObject target, Mesh mesh, AMPModelType type,
            AMPConfig config, Action<string> log)
        {
            long indexCount = 0;
            for (var subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
                indexCount += (long)mesh.GetIndexCount(subMesh);
            var convex = config.ConvexFor(type);
            if (convex && config.fallbackForComplexConvex &&
                indexCount / 3 > Mathf.Max(4, config.convexTriangleLimit))
            {
                AddPrimitiveCollider(target, mesh.bounds, config.colliderFallback);
                log?.Invoke($"警告：{target.name} 面数过高，已降级为 {config.colliderFallback} Collider。");
                return;
            }
            var collider = target.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
            collider.convex = convex;
            collider.isTrigger = false;
        }

        private static void AddPrimitiveCollider(GameObject target, Bounds bounds, AMPColliderFallback fallback)
        {
            switch (fallback)
            {
                case AMPColliderFallback.Sphere:
                    var sphere = target.AddComponent<SphereCollider>();
                    sphere.center = bounds.center; sphere.radius = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z); break;
                case AMPColliderFallback.Capsule:
                    var capsule = target.AddComponent<CapsuleCollider>();
                    capsule.center = bounds.center; capsule.radius = Mathf.Max(bounds.extents.x, bounds.extents.z);
                    capsule.height = Mathf.Max(capsule.radius * 2f, bounds.size.y); break;
                default:
                    var box = target.AddComponent<BoxCollider>(); box.center = bounds.center; box.size = bounds.size; break;
            }
        }

        private static void ApplyMaterials(GameObject root, string output, string sourcePath, AMPModelType type,
            AMPConfig config, Material template, Action<string> log)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null) throw new InvalidOperationException("找不到 URP/Lit 或 Standard Shader。");
            var materialFolder = $"Assets/AMP_Output/Materials/{type}";
            EnsureFolder(materialFolder);
            var cache = new Dictionary<Material, Material>();
            var perPrefab = config.materialMode == AMPMaterialMode.PerPrefabCopy || template != null;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                for (var i = 0; i < materials.Length; i++)
                {
                    var original = materials[i];
                    if (original == null) { log?.Invoke($"警告：{renderer.name} 存在空材质槽。"); continue; }
                    if (!cache.TryGetValue(original, out var editable))
                    {
                        var materialName = perPrefab
                            ? BuildPerPrefabMaterialName(sourcePath, output, original)
                            : BuildSharedMaterialName(sourcePath, original);
                        var path = $"{materialFolder}/{materialName}.mat";
                        editable = AssetDatabase.LoadAssetAtPath<Material>(path);
                        if (editable == null)
                        {
                            editable = template != null ? new Material(template) : new Material(original);
                            editable.name = materialName;
                            if (template == null) editable.shader = shader;
                            if (editable.HasProperty("_BaseColor")) editable.SetColor("_BaseColor", config.materialTint);
                            else if (editable.HasProperty("_Color")) editable.SetColor("_Color", config.materialTint);
                            AssetDatabase.CreateAsset(editable, path);
                            log?.Invoke(perPrefab ? $"已创建独立材质：{path}" : $"已创建共享材质：{path}");
                        }
                        else log?.Invoke($"复用共享材质：{path}");
                        cache.Add(original, editable);
                    }
                    materials[i] = editable;
                }
                renderer.sharedMaterials = materials;
            }
        }

        private static string BuildSharedMaterialName(string sourcePath, Material original)
        {
            var sourceName = Sanitize(Path.GetFileNameWithoutExtension(sourcePath));
            var materialName = Sanitize(original.name);
            if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(original, out string guid, out long localId))
                return $"{sourceName}_{materialName}_{localId}";
            return $"{sourceName}_{materialName}";
        }

        private static string BuildPerPrefabMaterialName(string sourcePath, string output, Material original)
        {
            var sourceName = Sanitize(Path.GetFileNameWithoutExtension(sourcePath));
            var prefabName = Sanitize(Path.GetFileNameWithoutExtension(output));
            var materialName = Sanitize(original.name);
            if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(original, out string guid, out long localId))
                return $"{sourceName}_{prefabName}_{materialName}_{localId}";
            return $"{sourceName}_{prefabName}_{materialName}";
        }

        private static string GetHierarchyPath(Transform root, Transform value)
        {
            var path = value.name;
            while (value.parent != null && value.parent != root)
            {
                value = value.parent;
                path = value.name + "/" + path;
            }
            return root.name + "/" + path;
        }

        private static string Sanitize(string value)
        {
            foreach (var c in Path.GetInvalidFileNameChars()) value = value.Replace(c, '_');
            return value;
        }

        public static void EnsureFolder(string assetPath)
        {
            var parts = assetPath.Replace('\\', '/').Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
