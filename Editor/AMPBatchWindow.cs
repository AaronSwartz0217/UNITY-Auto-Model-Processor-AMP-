using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Codex.AutoModelProcessor
{
    public sealed class AMPBatchWindow : EditorWindow
    {
        private sealed class Entry { public string path; public bool selected; }
        private AMPConfig config;
        private AMPModelType type;
        private Material template;
        private readonly List<Entry> files = new List<Entry>();
        private readonly List<string> logs = new List<string>();
        private Vector2 filesScroll, logScroll, mainScroll;
        private int success, failed, skipped;

        [MenuItem("Tools/AutoModelProcessor/Batch Processing Window")]
        public static void Open() => GetWindow<AMPBatchWindow>("Auto Model Processor");

        private void OnEnable() { config = AMPConfig.Load(); Scan(); }
        private void OnDisable() { config?.Save(); }

        private void OnGUI()
        {
            if (config == null) config = AMPConfig.Load();
            mainScroll = EditorGUILayout.BeginScrollView(mainScroll);
            EditorGUILayout.LabelField("Auto Model Processor", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("将 FBX 放入输入目录，选择类型并一键生成标准化 Prefab。原始 FBX 不会被修改。", MessageType.Info);
            DrawPaths();
            DrawFiles();
            DrawOptions();
            DrawExecution();
            DrawLogs();
            EditorGUILayout.EndScrollView();
        }

        private void DrawPaths()
        {
            EditorGUILayout.Space(); EditorGUILayout.LabelField("路径配置", EditorStyles.boldLabel);
            config.plantsInput = EditorGUILayout.TextField("Plants 输入", config.plantsInput);
            config.obstaclesInput = EditorGUILayout.TextField("Obstacles 输入", config.obstaclesInput);
            config.plantsOutput = EditorGUILayout.TextField("Plants 输出", config.plantsOutput);
            config.obstaclesOutput = EditorGUILayout.TextField("Obstacles 输出", config.obstaclesOutput);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("创建默认目录")) CreateFolders();
            if (GUILayout.Button("扫描 FBX")) Scan();
            if (GUILayout.Button("导出 JSON")) ExportJson();
            if (GUILayout.Button("导入 JSON")) ImportJson();
            EditorGUILayout.EndHorizontal();
            var activeInput = config.InputFor(type);
            if (!string.IsNullOrEmpty(activeInput) && !AssetDatabase.IsValidFolder(activeInput))
                EditorGUILayout.HelpBox($"当前 {type} 输入目录不存在：{activeInput}", MessageType.Error);
        }

        private void DrawFiles()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"文件列表（共 {files.Count}，已选 {files.FindAll(x => x.selected).Count}）", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("全选", GUILayout.Width(60))) files.ForEach(x => x.selected = true);
            if (GUILayout.Button("全不选", GUILayout.Width(60))) files.ForEach(x => x.selected = false);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            filesScroll = EditorGUILayout.BeginScrollView(filesScroll, GUILayout.MinHeight(110), GUILayout.MaxHeight(220));
            foreach (var entry in files)
            {
                EditorGUILayout.BeginHorizontal();
                entry.selected = EditorGUILayout.Toggle(entry.selected, GUILayout.Width(18));
                var info = new FileInfo(entry.path);
                EditorGUILayout.LabelField(Path.GetFileName(entry.path));
                EditorGUILayout.LabelField(info.Exists ? FormatBytes(info.Length) : "缺失", GUILayout.Width(80));
                if (config.generationMode == AMPPrefabGenerationMode.RootObjects)
                {
                    var splitFolder = config.OutputFor(type);
                    if (config.splitIntoSourceSubfolder) splitFolder += "/" + Path.GetFileNameWithoutExtension(entry.path);
                    EditorGUILayout.LabelField(AssetDatabase.IsValidFolder(splitFolder) ? "已拆分" : "待拆分", GUILayout.Width(55));
                }
                else
                {
                    var outPath = $"{config.OutputFor(type)}/{Path.GetFileNameWithoutExtension(entry.path)}.prefab";
                    EditorGUILayout.LabelField(AssetDatabase.LoadAssetAtPath<GameObject>(outPath) ? "已处理" : "待处理", GUILayout.Width(55));
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawOptions()
        {
            EditorGUILayout.Space(); EditorGUILayout.LabelField("处理选项", EditorStyles.boldLabel);
            var newType = (AMPModelType)EditorGUILayout.EnumPopup("模型类型", type);
            if (newType != type) { type = newType; Scan(); }
            config.conflictPolicy = (AMPConflictPolicy)EditorGUILayout.EnumPopup("冲突策略", config.conflictPolicy);
            if (config.conflictPolicy == AMPConflictPolicy.Prompt)
                EditorGUILayout.HelpBox("首次遇到同名 Prefab 时只询问一次，可选择全部覆盖、全部跳过或取消批处理。", MessageType.Info);
            config.generationMode = (AMPPrefabGenerationMode)EditorGUILayout.EnumPopup("Prefab 生成方式", config.generationMode);
            if (config.generationMode == AMPPrefabGenerationMode.RootObjects)
            {
                EditorGUI.indentLevel++;
                config.splitNaming = (AMPSplitNaming)EditorGUILayout.EnumPopup("拆分命名", config.splitNaming);
                config.splitIntoSourceSubfolder = EditorGUILayout.Toggle("按 FBX 创建子目录", config.splitIntoSourceSubfolder);
                config.correctEachSplitPivot = EditorGUILayout.Toggle("分别校正原点", config.correctEachSplitPivot);
                EditorGUI.indentLevel--;
            }
            template = (Material)EditorGUILayout.ObjectField("母版材质（可选）", template, typeof(Material), false);
            config.standardizeMaterials = EditorGUILayout.Toggle("标准化材质", config.standardizeMaterials);
            using (new EditorGUI.DisabledScope(!config.standardizeMaterials))
            {
                config.materialMode = (AMPMaterialMode)EditorGUILayout.EnumPopup("材质处理方式", config.materialMode);
                if (config.materialMode != AMPMaterialMode.KeepImported)
                    config.materialTint = EditorGUILayout.ColorField("材质 Tint", config.materialTint);
            }
            if (template != null)
                EditorGUILayout.HelpBox("指定母版材质时，每个 Prefab 会创建独立材质实例。", MessageType.Info);
            config.centerHorizontalPivot = EditorGUILayout.Toggle("水平居中原点", config.centerHorizontalPivot);
            EditorGUILayout.LabelField(type == AMPModelType.Plants ? "植物物理设置" : "障碍物物理设置", EditorStyles.boldLabel);
            var colliderMode = (AMPColliderMode)EditorGUILayout.EnumPopup("碰撞体处理方式", config.ColliderModeFor(type));
            config.SetColliderMode(type, colliderMode);
            using (new EditorGUI.DisabledScope(colliderMode == AMPColliderMode.None))
            {
                var convex = EditorGUILayout.Toggle("Convex", config.ConvexFor(type));
                config.SetConvex(type, convex);
                config.fallbackForComplexConvex = EditorGUILayout.Toggle("复杂凸体自动降级", config.fallbackForComplexConvex);
                using (new EditorGUI.DisabledScope(!config.fallbackForComplexConvex || !convex))
                {
                    config.convexTriangleLimit = EditorGUILayout.IntField("凸体三角面阈值", config.convexTriangleLimit);
                    config.colliderFallback = (AMPColliderFallback)EditorGUILayout.EnumPopup("降级碰撞体", config.colliderFallback);
                }
            }
            EditorGUILayout.HelpBox(type == AMPModelType.Plants
                ? "植物：Tag=Untagged，Layer=Grass，Static=关闭，MeshCollider，Convex=开启，Is Trigger=关闭。"
                : "障碍物：Tag=NavMesh，Layer=Obstacle，Static=开启，MeshCollider，Convex=开启，Is Trigger=关闭。",
                MessageType.None);
        }

        private void DrawExecution()
        {
            EditorGUILayout.Space(); EditorGUILayout.LabelField("执行", EditorStyles.boldLabel);
            var selectedCount = files.FindAll(x => x.selected).Count;
            using (new EditorGUI.DisabledScope(selectedCount == 0))
                if (GUILayout.Button($"开始处理已选文件（{selectedCount}）", GUILayout.Height(32))) ProcessSelected();
            if (selectedCount == 0)
                EditorGUILayout.HelpBox("当前没有勾选任何 FBX，不会执行处理或覆盖。", MessageType.Info);
            EditorGUILayout.LabelField($"成功 {success}  |  失败 {failed}  |  跳过 {skipped}");
        }

        private void DrawLogs()
        {
            EditorGUILayout.Space(); EditorGUILayout.LabelField("日志", EditorStyles.boldLabel);
            logScroll = EditorGUILayout.BeginScrollView(logScroll, EditorStyles.helpBox, GUILayout.MinHeight(130));
            foreach (var line in logs) EditorGUILayout.SelectableLabel(line, GUILayout.Height(EditorGUIUtility.singleLineHeight));
            EditorGUILayout.EndScrollView();
            if (GUILayout.Button("清空日志", GUILayout.Width(90))) logs.Clear();
        }

        private void ProcessSelected()
        {
            // Freeze the exact selection before any setup, folder creation or asset refresh can occur.
            var selectedPaths = files.FindAll(x => x.selected).ConvertAll(x => x.path);
            if (selectedPaths.Count == 0) { Log("没有勾选任何文件，已取消处理。"); return; }
            success = failed = skipped = 0; config.Save();
            try
            {
                Log($"本次处理已锁定 {selectedPaths.Count} 个 FBX：");
                foreach (var selectedPath in selectedPaths) Log("  - " + selectedPath);
                CreateFolders(false);
                AMPProjectSetup.EnsureProjectSettings(Log);
                var conflictSession = new AMPConflictSession();
                for (var i = 0; i < selectedPaths.Count; i++)
                {
                    if (EditorUtility.DisplayCancelableProgressBar("Auto Model Processor",
                        $"Processing {i + 1}/{selectedPaths.Count}: {Path.GetFileName(selectedPaths[i])}", (float)i / selectedPaths.Count))
                    { skipped += selectedPaths.Count - i; Log("用户取消了批处理。"); break; }
                    var result = AMPProcessor.Process(selectedPaths[i], type, config, template, Log, conflictSession);
                    if (result.Kind == AMPResultKind.Success) success++;
                    else if (result.Kind == AMPResultKind.Skipped) skipped++;
                    else failed++;
                    if (conflictSession.Cancelled)
                    {
                        skipped += selectedPaths.Count - i - 1;
                        Log("批处理已取消，剩余选中模型未处理。");
                        break;
                    }
                }
            }
            catch (Exception ex) { failed++; Log("错误：" + ex); }
            finally { EditorUtility.ClearProgressBar(); AssetDatabase.SaveAssets(); AssetDatabase.Refresh(); }
            Log($"处理完成：成功 {success}，失败 {failed}，跳过 {skipped}。");
            Repaint();
        }

        private void Scan()
        {
            var previousSelection = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in files) previousSelection[entry.path] = entry.selected;
            files.Clear();
            var folder = config?.InputFor(type);
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return;
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { folder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                    files.Add(new Entry
                    {
                        path = path,
                        selected = previousSelection.TryGetValue(path, out var wasSelected) && wasSelected
                    });
            }
        }

        private void CreateFolders(bool refreshFileList = true)
        {
            AMPProcessor.EnsureFolder(config.plantsInput); AMPProcessor.EnsureFolder(config.obstaclesInput);
            AMPProcessor.EnsureFolder(config.plantsOutput); AMPProcessor.EnsureFolder(config.obstaclesOutput);
            Log("目录检查完成。");
            if (refreshFileList) Scan();
        }

        private void ExportJson()
        {
            var path = EditorUtility.SaveFilePanel("导出 AMP 配置", Application.dataPath, "AMPConfig", "json");
            if (!string.IsNullOrEmpty(path)) { config.Export(path); Log("已导出配置：" + path); }
        }

        private void ImportJson()
        {
            var path = EditorUtility.OpenFilePanel("导入 AMP 配置", Application.dataPath, "json");
            if (string.IsNullOrEmpty(path)) return;
            try { config = AMPConfig.Import(path) ?? new AMPConfig(); config.Save(); Scan(); Log("已导入配置：" + path); }
            catch (Exception ex) { Log("配置导入失败：" + ex.Message); }
        }

        private void Log(string message) { logs.Add($"[{DateTime.Now:HH:mm:ss}] {message}"); logScroll.y = float.MaxValue; }
        private static string FormatBytes(long bytes) => bytes >= 1048576 ? $"{bytes / 1048576f:F1} MB" : $"{bytes / 1024f:F1} KB";
    }
}
