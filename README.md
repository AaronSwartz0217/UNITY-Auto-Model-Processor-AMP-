# Auto Model Processor (AMP)

Auto Model Processor 是一个面向 Unity 2022.3 LTS 的 Editor 插件，用于把植物、障碍物等 FBX 模型批量转换为统一规范的 Prefab。插件可以拆分 FBX 中的并列对象、校正原点、复制材质、添加碰撞体，并设置项目约定的 Layer、Tag 和 Static 状态。处理过程不会修改源 FBX 或 Mesh 数据。

## 安装

将本仓库克隆或复制到 Unity 项目的以下位置：

```text
Packages/com.codex.auto-model-processor
```

Unity 完成脚本编译后，从菜单打开：

```text
Tools > AutoModelProcessor > Batch Processing Window
```

## 两个 AMP 文件夹的作用

插件默认在项目的 `Assets` 下使用两个顶层目录：

```text
Assets/
├─ AMP_RawInput/                 # 原始模型输入区
│  ├─ Plants/                    # 植物 FBX、贴图及 FBM 资源
│  └─ Obstacles/                 # 障碍物 FBX、贴图及 FBM 资源
└─ AMP_Output/                   # 插件生成结果
   ├─ Plants_Prefabs/            # 植物 Prefab
   ├─ Obstacles_Prefabs/         # 障碍物 Prefab
   └─ Materials/
      ├─ Plants/                 # 植物可编辑材质副本
      └─ Obstacles/              # 障碍物可编辑材质副本
```

### `AMP_RawInput`

这是插件的原始输入目录。把待处理的 FBX 及其贴图放入对应分类：

- 植物放入 `Assets/AMP_RawInput/Plants`；
- 障碍物放入 `Assets/AMP_RawInput/Obstacles`。

可以在分类目录下继续创建子目录。插件扫描 FBX 时会递归查找当前所选分类中的模型。该目录中的源文件不会被插件修改或删除。

### `AMP_Output`

这是插件的生成结果目录：

- `Plants_Prefabs` 保存植物 Prefab；
- `Obstacles_Prefabs` 保存障碍物 Prefab；
- `Materials` 保存从 FBX 材质复制出的可编辑材质资源。

启用“按 FBX 创建子目录”后，每个源 FBX 会拥有独立的输出子目录。重新处理同名模型时，如何处理既有 Prefab 由“冲突策略”决定。

`AMP_Output` 是可交付的 Unity 资源，不是 Library 缓存。如果场景或其他 Prefab 已引用其中的资源，不应直接删除。

## 使用流程

1. 把 FBX 和相关贴图放入 `AMP_RawInput/Plants` 或 `AMP_RawInput/Obstacles`。
2. 打开 **Tools > AutoModelProcessor > Batch Processing Window**。
3. 选择“模型类型”：`Plants` 或 `Obstacles`。
4. 点击“扫描 FBX”。
5. 在文件列表中勾选本次需要处理的 FBX。新扫描出的文件默认不勾选。
6. 设置 Prefab 生成方式、材质、原点和碰撞体选项。
7. 点击“开始批量处理”。插件只处理开始执行时已经勾选的文件。
8. 在 `AMP_Output` 中检查生成的 Prefab 和材质，并在提交前进行必要的视觉与碰撞验证。

“创建默认目录”可以自动建立上述输入、输出目录；“导出 JSON”和“导入 JSON”用于在不同项目或成员之间共享处理规则。

## 设置选项

### 路径配置

| 选项 | 作用 |
| --- | --- |
| Plants 输入 | 植物 FBX 的扫描目录，默认是 `Assets/AMP_RawInput/Plants`。 |
| Obstacles 输入 | 障碍物 FBX 的扫描目录，默认是 `Assets/AMP_RawInput/Obstacles`。 |
| Plants 输出 | 植物 Prefab 的输出目录，默认是 `Assets/AMP_Output/Plants_Prefabs`。 |
| Obstacles 输出 | 障碍物 Prefab 的输出目录，默认是 `Assets/AMP_Output/Obstacles_Prefabs`。 |

### 模型类型

- `Plants`：生成植物资源，默认使用 `Grass` Layer、`Untagged` Tag，并关闭 Static。
- `Obstacles`：生成障碍物资源，默认使用 `Obstacle` Layer、`NavMesh` Tag，并开启 Static。

如果 `Grass`、`Obstacle` Layer 或 `NavMesh` Tag 不存在，插件会在处理时创建它们。

### 冲突策略

| 选项 | 作用 |
| --- | --- |
| `Overwrite` | 覆盖已经存在的同名 Prefab。 |
| `Skip` | 保留现有 Prefab，跳过冲突项。 |
| `Prompt` | 首次发生冲突时询问，可对本次批处理选择全部覆盖、全部跳过或取消。 |

### Prefab 生成方式

| 选项 | 作用 |
| --- | --- |
| `WholeFbx` | 把整个 FBX 作为一个 Prefab 处理。 |
| `RootObjects` | 将 FBX 根层级下的并列 Mesh 对象分别生成独立 Prefab。 |

使用 `RootObjects` 时还有以下选项：

- “拆分命名”：使用原对象名，或使用 `FBX名_01`、`FBX名_02` 等稳定序号；
- “按 FBX 创建子目录”：将同一个 FBX 拆出的 Prefab 放进独立子目录；
- “分别校正原点”：按每个拆分对象的 Bounds 单独调整原点。

如果导出软件在多个并列对象外额外包了一层空节点，插件会尝试自动展开这一层。已经合并为单一 Mesh 的几何不会按连通区域继续切割。

### 材质设置

| 选项 | 作用 |
| --- | --- |
| 母版材质 | 可选。指定后以母版为基础，为每个 Prefab 创建独立材质。 |
| 标准化材质 | 开启材质复制和 Shader 标准化；优先使用 `Universal Render Pipeline/Lit`，不存在时回退到 `Standard`。 |
| `KeepImported` | 保持 FBX 当前导入材质引用，不创建可编辑副本。 |
| `SharedEditableCopy` | 同一源材质只创建一份可编辑副本，由相关 Prefab 共享。默认选项。 |
| `PerPrefabCopy` | 每个 Prefab 创建独立材质，便于分别修改。 |
| Material Tint | 设置新建材质的基础颜色。默认是白色。 |

### 原点设置

“水平居中原点”开启后，插件会根据 Renderer Bounds 将模型原点调整到底部中心；关闭后只校正垂直方向，使原点落到模型底部。这里只调整生成 Prefab 内对象的 Transform，不修改 Mesh 顶点。

### 碰撞体设置

植物和障碍物分别保存自己的碰撞体配置。

| 选项 | 作用 |
| --- | --- |
| `MeshCollider` | 移除导入对象上已有的 Collider，并为实际持有 Mesh 的节点添加 MeshCollider。 |
| `None` | 移除已有 Collider，不生成新碰撞体。 |
| Convex | 控制新 MeshCollider 的 `convex` 属性。植物和障碍物默认均开启。 |
| Is Trigger | 插件生成的 Collider 固定关闭 `Is Trigger`。 |
| 复杂凸体自动降级 | 当凸 Mesh 面数超过限制时，改用基础碰撞体。默认关闭。 |
| 凸体三角面限制 | 判断是否触发基础碰撞体降级的三角面阈值，默认 255。 |
| 碰撞体降级类型 | 可选择 `Box`、`Sphere` 或 `Capsule`。 |

生成完成后应在 Unity 中检查碰撞体是否符合模型轮廓和运行时需求，尤其是复杂植物、薄片模型和大型障碍物。

## 处理规则与注意事项

- 源 FBX、Mesh 和动画数据不会被修改；
- 动画不会被复制到生成 Prefab 的新根节点；
- 原点校正通过子对象 Transform 偏移完成；
- `RootObjects` 的勾选单位仍然是 FBX，选中一个 FBX 会处理其中全部有效拆分对象；
- 批处理开始时会冻结本次勾选列表，资源刷新不会扩大处理范围；
- 配置默认保存在当前用户的 Unity `EditorPrefs` 中，可以通过 JSON 导入、导出共享；
- 项目集成可以实现 `IAMPProcessingExtension`，并注册到 `AMPProcessor.Extensions`，在 Prefab 保存前追加自定义处理。

## 插件目录

```text
com.codex.auto-model-processor/
├─ Editor/                       # 编辑器窗口、配置、处理逻辑和项目设置代码
├─ Samples~/QuickStart/          # Package Manager 可导入的示例配置与场景
├─ package.json                  # Unity Package Manager 包清单
├─ README.md                     # 使用说明
├─ 使用说明.md                   # 更详细的中文说明
├─ CHANGELOG.md                  # 版本变更记录
└─ LICENSE.md                    # 许可信息
```

插件是 Editor-only 包，不包含运行时程序集。
