<img width="676" height="380" alt="MCP for Unity" src="../images/logo.png" />

| [English](../../README.md) | [简体中文](README-zh.md) |
|----------------------|---------------------------------|

#### 由 [Coplay](https://www.coplay.dev/?ref=unity-mcp) 荣誉赞助并维护 —— Unity 最好的 AI 助手。

[![Discord](https://img.shields.io/badge/discord-join-red.svg?logo=discord&logoColor=white)](https://discord.gg/y4p8KfzrN4)
[![](https://img.shields.io/badge/Website-Visit-purple)](https://www.coplay.dev/?ref=unity-mcp)
[![](https://img.shields.io/badge/Unity-000000?style=flat&logo=unity&logoColor=blue 'Unity')](https://unity.com/releases/editor/archive)
[![Unity Asset Store](https://img.shields.io/badge/Unity%20Asset%20Store-Get%20Package-FF6A00?style=flat&logo=unity&logoColor=white)](https://assetstore.unity.com/packages/tools/generative-ai/mcp-for-unity-ai-driven-development-329908)
[![python](https://img.shields.io/badge/Python-3.10+-3776AB.svg?style=flat&logo=python&logoColor=white)](https://www.python.org)
[![](https://badge.mcpx.dev?status=on 'MCP Enabled')](https://modelcontextprotocol.io/introduction)
[![](https://img.shields.io/badge/License-MIT-red.svg 'MIT License')](https://opensource.org/licenses/MIT)

**用大语言模型创建你的 Unity 应用！** MCP for Unity 通过 [Model Context Protocol](https://modelcontextprotocol.io/introduction) 将 AI 助手（Claude、Cursor、VS Code 等）与你的 Unity Editor 连接起来。为你的大语言模型提供管理资源、控制场景、编辑脚本和自动化任务的工具。

<img alt="MCP for Unity building a scene" src="../images/building_scene.gif">

<details>
<summary><strong>最近更新</strong></summary>

* **v9.5.4 (beta)** — 新增 `unity_reflect` 和 `unity_docs` 工具用于 API 验证：通过反射检查实时 C# API，获取官方 Unity 文档（ScriptReference、Manual、包文档）。新增 `manage_packages` 工具：安装、移除、搜索和管理 Unity 包及作用域注册表。包含输入验证、移除时依赖检查和 git URL 警告。
* **v9.5.3** — 新增 `manage_graphics` 工具（33个操作）：体积/后处理、光照烘焙、渲染统计、管线设置、URP渲染器特性。3个新资源：`volumes`、`rendering_stats`、`renderer_features`。
* **v9.5.2** — 新增 `manage_camera` 工具，支持 Cinemachine（预设、优先级、噪声、混合、扩展）、`cameras` 资源、通过 SerializedProperty 修复优先级持久化问题。
* **v9.4.8** — 新编辑器 UI、通过 `manage_tools` 实时切换工具、技能同步窗口、多视图截图、一键 Roslyn 安装器、支持 Qwen Code 与 Gemini CLI 客户端、通过 `manage_probuilder` 进行 ProBuilder 网格编辑。

<details>
<summary>更早的版本</summary>

* **v9.4.7** — 支持按调用路由 Unity 实例、修复 macOS pyenv PATH 问题、脚本工具的域重载稳定性提升。
* **v9.4.6** — 新增 `manage_animation` 工具、支持 Cline 客户端、失效连接检测、工具状态跨重载持久化。
* **v9.4.4** — 可配置 `batch_execute` 限制、按会话状态过滤工具、修复 IPv6/IPv4 回环问题。

</details>
</details>

---

## 快速开始

### 前置要求

* **Unity 2021.3 LTS+** — [下载 Unity](https://unity.com/download)
* **Python 3.10+** 和 **uv** — [安装 uv](https://docs.astral.sh/uv/getting-started/installation/)
* **一个 MCP 客户端** — [Claude Desktop](https://claude.ai/download) | [Cursor](https://www.cursor.com/en/downloads) | [VS Code Copilot](https://code.visualstudio.com/docs/copilot/overview) | [GitHub Copilot CLI](https://docs.github.com/en/copilot/concepts/agents/about-copilot-cli) | [Windsurf](https://windsurf.com)

### 1. 安装 Unity 包

在 Unity 中：`Window > Package Manager > + > Add package from git URL...`

> [!TIP]
> ```text
> https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#main
> ```

**想要最新的 beta 版本？** 使用 beta 分支：
```text
https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#beta
```

<details>
<summary>其他安装方式（Asset Store、OpenUPM）</summary>

**Unity Asset Store：**
1. 访问 [Asset Store 上的 MCP for Unity](https://assetstore.unity.com/packages/tools/generative-ai/mcp-for-unity-ai-driven-development-329908)
2. 点击 `Add to My Assets`，然后通过 `Window > Package Manager` 导入

**OpenUPM：**
```bash
openupm add com.coplaydev.unity-mcp
```
</details>

### 2. 启动服务器并连接

1. 在 Unity 中：`Window > MCP for Unity`
2. 点击 **Start Server**（会在 `localhost:8080` 启动 HTTP 服务器）
3. 从下拉菜单选择你的 MCP Client，然后点击 **Configure**
4. 查找 🟢 "Connected ✓"
5. **连接你的客户端：** 一些客户端（Cursor、Windsurf、Antigravity）需要在设置里启用 MCP 开关；另一些（Claude Desktop、Claude Code）在配置后会自动连接。

**就这些！** 试试这样的提示词：*"Create a red, blue and yellow cube"* 或 *"Build a simple player controller"*

---

<details>
<summary><strong>功能与工具</strong></summary>

### 关键功能
* **自然语言控制** — 指示你的大语言模型执行 Unity 任务
* **强大工具** — 管理资源、场景、材质、脚本和编辑器功能
* **自动化** — 自动化重复的 Unity 工作流程
* **可扩展** — 可与多种 MCP Client 配合使用

### 可用工具
`apply_text_edits` • `batch_execute` • `create_script` • `debug_request_context` • `delete_script` • `editor_ui_automation` 🆕 • `execute_custom_tool` • `execute_menu_item` • `find_gameobjects` • `find_in_file` • `get_sha` • `get_test_job` • `manage_2d` 🆕 • `manage_animation` • `manage_asset` • `manage_audio` 🆕 • `manage_build` 🆕 • `manage_camera` • `manage_components` • `manage_editor` • `manage_gameobject` • `manage_graphics` • `manage_material` • `manage_navigation` 🆕 • `manage_packages` • `manage_physics` 🆕 • `manage_prefabs` • `manage_probuilder` • `manage_scene` • `manage_script` • `manage_script_capabilities` • `manage_scriptable_object` • `manage_shader` • `manage_terrain` 🆕 • `manage_texture` • `manage_timeline` 🆕 • `manage_tools` • `manage_ui` • `manage_vfx` • `read_console` • `refresh_unity` • `run_tests` • `script_apply_edits` • `set_active_instance` • `unity_docs` • `unity_reflect` • `validate_script`

### 可用资源
`cameras` • `custom_tools` • `renderer_features` • `rendering_stats` • `volumes` • `editor_active_tool` • `editor_prefab_stage` • `editor_selection` • `editor_state` • `editor_windows` • `gameobject` • `gameobject_api` • `gameobject_component` • `gameobject_components` • `get_tests` • `get_tests_for_mode` • `menu_items` • `prefab_api` • `prefab_hierarchy` • `prefab_info` • `project_info` • `project_layers` • `project_tags` • `tool_groups` • `unity_instances`

**性能提示：** 多个操作请使用 `batch_execute` — 比逐个调用快 10-100 倍！
</details>

<details>
<summary><strong>手动配置</strong></summary>

如果自动设置不生效，请把下面内容添加到你的 MCP Client 配置文件中：

**HTTP（默认 — 适用于 Claude Desktop、Cursor、Windsurf）：**
```json
{
  "mcpServers": {
    "unityMCP": {
      "url": "http://localhost:8080/mcp"
    }
  }
}
```

**VS Code：**
```json
{
  "servers": {
    "unityMCP": {
      "type": "http",
      "url": "http://localhost:8080/mcp"
    }
  }
}
```

<details>
<summary>Stdio 配置（uvx）</summary>

**macOS/Linux：**
```json
{
  "mcpServers": {
    "unityMCP": {
      "command": "uvx",
      "args": ["--from", "mcpforunityserver", "mcp-for-unity", "--transport", "stdio"]
    }
  }
}
```

**Windows：**
```json
{
  "mcpServers": {
    "unityMCP": {
      "command": "C:/Users/YOUR_USERNAME/AppData/Local/Microsoft/WinGet/Links/uvx.exe",
      "args": ["--from", "mcpforunityserver", "mcp-for-unity", "--transport", "stdio"]
    }
  }
}
```
</details>
</details>

<details>
<summary><strong>多个 Unity 实例</strong></summary>

MCP for Unity 支持多个 Unity Editor 实例。要将操作定向到某个特定实例：

1. 让你的大语言模型检查 `unity_instances` 资源
2. 使用 `set_active_instance` 并传入 `Name@hash`（例如 `MyProject@abc123`）
3. 后续所有工具都会路由到该实例
</details>

<details>
<summary><strong>Roslyn 脚本验证（高级）</strong></summary>

要使用能捕获未定义命名空间、类型和方法的 **Strict** 验证：

1. 安装 [NuGetForUnity](https://github.com/GlitchEnzo/NuGetForUnity)
2. `Window > NuGet Package Manager` → 安装 `Microsoft.CodeAnalysis` v5.0
3. 同时安装 `SQLitePCLRaw.core` 和 `SQLitePCLRaw.bundle_e_sqlite3` v3.0.2
4. 在 `Player Settings > Scripting Define Symbols` 中添加 `USE_ROSLYN`
5. 重启 Unity

  <details>
  <summary>手动 DLL 安装（如果 NuGetForUnity 不可用）</summary>

  1. 从 [NuGet](https://www.nuget.org/packages/Microsoft.CodeAnalysis.CSharp/) 下载 `Microsoft.CodeAnalysis.CSharp.dll` 及其依赖项
  2. 将 DLL 放到 `Assets/Plugins/` 目录
  3. 确保 .NET 兼容性设置正确
  4. 在 Scripting Define Symbols 中添加 `USE_ROSLYN`
  5. 重启 Unity
  </details>
</details>

<details>
<summary><strong>故障排除</strong></summary>

* **Unity Bridge 无法连接：** 检查 `Window > MCP for Unity` 状态，重启 Unity
* **Server 无法启动：** 确认 `uv --version` 可用，并检查终端错误
* **Client 无法连接：** 确认 HTTP server 正在运行，并且 URL 与你的配置一致

**详细的设置指南：**
* [Fix Unity MCP and Cursor, VSCode & Windsurf](https://github.com/CoplayDev/unity-mcp/wiki/1.-Fix-Unity-MCP-and-Cursor,-VSCode-&-Windsurf) — uv/Python 安装、PATH 问题
* [Fix Unity MCP and Claude Code](https://github.com/CoplayDev/unity-mcp/wiki/2.-Fix-Unity-MCP-and-Claude-Code) — Claude CLI 安装
* [Common Setup Problems](https://github.com/CoplayDev/unity-mcp/wiki/3.-Common-Setup-Problems) — macOS dyld 错误、FAQ

还是卡住？[开一个 Issue](https://github.com/CoplayDev/unity-mcp/issues) 或 [加入 Discord](https://discord.gg/y4p8KfzrN4)
</details>

<details>
<summary><strong>贡献</strong></summary>

开发环境设置见 [README-DEV.md](../development/README-DEV.md)。自定义工具见 [CUSTOM_TOOLS.md](../reference/CUSTOM_TOOLS.md)。

1. Fork → 创建 issue → 新建分支（`feature/your-idea`）→ 修改 → 提 PR
</details>

<details>
<summary><strong>遥测与隐私</strong></summary>

匿名、注重隐私的遥测（不包含代码、项目名或个人数据）。可通过 `DISABLE_TELEMETRY=true` 关闭。详见 [TELEMETRY.md](../reference/TELEMETRY.md)。
</details>

---

**许可证：** MIT — 查看 [LICENSE](../../LICENSE) | **需要帮助？** [Discord](https://discord.gg/y4p8KfzrN4) | [Issues](https://github.com/CoplayDev/unity-mcp/issues)

---

## Star 历史

[![Star History Chart](https://api.star-history.com/svg?repos=CoplayDev/unity-mcp&type=Date)](https://www.star-history.com/#CoplayDev/unity-mcp&Date)

<details>
<summary><strong>研究引用</strong></summary>
如果你正在进行与 Unity-MCP 相关的研究，请引用我们！

```bibtex
@inproceedings{10.1145/3757376.3771417,
author = {Wu, Shutong and Barnett, Justin P.},
title = {MCP-Unity: Protocol-Driven Framework for Interactive 3D Authoring},
year = {2025},
isbn = {9798400721366},
publisher = {Association for Computing Machinery},
address = {New York, NY, USA},
url = {https://doi.org/10.1145/3757376.3771417},
doi = {10.1145/3757376.3771417},
series = {SA Technical Communications '25}
}
```
</details>

## Coplay 的 Unity AI 工具

Coplay 提供 3 个 Unity AI 工具：
- **MCP for Unity** 在 MIT 许可证下免费提供。
- **Coplay** 是一个运行在 Unity 内的高级 Unity AI 助手，功能超过 MCP for Unity。
- **Coplay MCP** 是 Coplay 工具的“目前免费”版 MCP。

（这些工具有不同的技术栈。参见这篇博客文章：[comparing Coplay to MCP for Unity](https://coplay.dev/blog/coplay-vs-coplay-mcp-vs-unity-mcp)。）

<img alt="Coplay" src="../images/coplay-logo.png" />

## 免责声明

本项目是一个免费开源的 Unity Editor 工具，与 Unity Technologies 无关。

---

## Fork 增强功能

本 Fork 在上游 MCP for Unity 基础上新增了 **8 个工具（70 个 Actions）**，覆盖编辑器 UI 自动化和主要缺失的游戏开发子系统。

### 新增工具

#### 🖱️ `editor_ui_automation` — 编辑器 UI 自动化

让 AI 能看到并操作 Unity 编辑器窗口 — 同时支持 UIElements 和 IMGUI 窗口。

| Action | 说明 |
|--------|------|
| `snapshot` | 捕获编辑器所有窗口的 UI 树，为可交互元素分配 `@e1, @e2` 引用（仅 UIElements） |
| `screenshot` | 截取任意窗口为 base64 PNG 图片 — IMGUI 窗口也能用 |
| `click` | 通过引用点击 UI 元素 |
| `type` | 输入文字或发送按键（Enter、Tab、Ctrl+S 等） |
| `drag` | 拖拽元素或资产到目标位置 |
| `send_event` | 向指定坐标发送原始鼠标/键盘事件 — 兼容 IMGUI |
| `focus_window` | 按标题聚焦指定编辑器窗口 |

**两种工作流：**
- **UIElements 窗口：** `snapshot` → 通过 `@eN` 引用进行 `click`/`type`/`drag`
- **IMGUI 窗口：** `screenshot` → AI 分析截图 → 通过坐标 `send_event`

---

#### 🔨 `manage_build` — 构建发布

| Action | 说明 |
|--------|------|
| `get_player_settings` | 读取公司名、包标识符、版本号、脚本后端等 |
| `set_player_settings` | 更新 PlayerSettings |
| `get_build_settings` | 获取构建场景列表和当前目标平台 |
| `set_build_scenes` | 设置构建场景列表 |
| `switch_platform` | 切换目标平台（含 Domain Reload 恢复机制） |
| `build` | 执行 BuildPipeline.BuildPlayer |
| `get_scripting_defines` | 读取宏定义符号 |
| `set_scripting_defines` | 设置宏定义符号 |

---

#### ⚡ `manage_physics` — 物理系统

| Action | 说明 |
|--------|------|
| `raycast` / `raycast_all` | 射线检测，返回碰撞点/法线/距离/碰撞体信息 |
| `overlap` | 球形/盒形范围检测 |
| `create_physics_material` | 创建物理材质资产 |
| `get_settings` / `set_settings` | 读写全局物理设置（通过 SerializedObject 持久化） |
| `configure_rigidbody` | 一步配置 Rigidbody（质量、阻力、运动学、碰撞检测） |
| `configure_collider` | 添加/配置 Box/Sphere/Capsule/Mesh 碰撞体 |
| `configure_joint` | 添加/配置 Fixed/Hinge/Spring 关节 |

---

#### 🗺️ `manage_navigation` — 导航寻路

同时支持旧版 NavMeshBuilder（内置）和新版 AI Navigation 包（NavMeshSurface）。

| Action | 说明 |
|--------|------|
| `bake` | 烘焙导航网格（可配置 Agent 参数） |
| `clear` | 清除所有烘焙数据 |
| `get_settings` | 读取导航区域和烘焙设置 |
| `set_area` | 配置区域名称和通行代价 |
| `configure_agent` | 配置 NavMeshAgent 属性 |
| `configure_obstacle` | 配置 NavMeshObstacle（含 Carving） |
| `add_offmesh_link` | 创建离网连接 |
| `test_path` | 计算两点间路径，返回路径点和总距离 |

---

#### ⛰️ `manage_terrain` — 地形系统

工具组：`terrain`（通过 `manage_tools` 激活）

| Action | 说明 |
|--------|------|
| `create` | 创建 Terrain + TerrainData |
| `set_heightmap` | 从文件导入高度图（RAW16/PNG/EXR） |
| `export_heightmap` | 导出高度图到 RAW16 文件 |
| `get_heightmap` | 读取区域高度值（最大 64×64） |
| `raise_lower` | 在指定位置升降地形 |
| `smooth` | 在指定位置平滑地形 |
| `add_terrain_layer` / `paint_texture` | 添加纹理层并绘制 |
| `add_tree_prototype` / `paint_trees` | 注册树预制体并种植 |
| `add_detail_prototype` / `paint_details` | 注册草/细节并种植 |
| `set_properties` / `get_info` | 配置地形属性和查看信息 |

---

#### 🔊 `manage_audio` — 音频系统

| Action | 说明 |
|--------|------|
| `configure_source` | 一步配置 AudioSource（音频片段、音量、空间混合、循环等） |
| `get_info` | 读取 AudioSource 当前状态 |
| `set_import_settings` | 配置 AudioClip 导入设置（压缩格式、加载方式、采样率） |
| `play` / `stop` / `pause` | 播放控制（仅在 Play Mode 下有效） |
| `create_mixer` | 创建 AudioMixer 资产（实验性，使用内部 API） |

---

#### 🎬 `manage_timeline` — 时间轴

工具组：`timeline`（需安装 `com.unity.timeline` 包）

| Action | 说明 |
|--------|------|
| `create_asset` | 创建 TimelineAsset |
| `add_track` / `remove_track` | 添加/移除轨道（Animation、Audio、Activation 等） |
| `add_clip` / `set_clip_properties` | 放置和配置片段 |
| `set_binding` | 通过 PlayableDirector 将轨道绑定到场景对象 |
| `get_info` | 读取时间轴结构（轨道、片段、时长） |
| `add_marker` | 添加 SignalEmitter 标记 |

---

#### 🎮 `manage_2d` — 2D 工具

工具组：`2d`（需要 Tilemap 模块）

| Action | 说明 |
|--------|------|
| `tilemap_set_tile` | 在网格位置放置/清除 Tile |
| `tilemap_fill` | 填充矩形区域 |
| `tilemap_clear` | 清除整个 Tilemap |
| `tilemap_get_info` | 获取 Tilemap 边界和大小信息 |
| `create_sprite_atlas` | 创建 SpriteAtlas 资产 |
| `atlas_add_folders` | 添加文件夹到图集 |
| `atlas_pack` | 打包所有图集 |

---

### 兼容性

| 功能 | 要求 |
|------|------|
| 编辑器 UI 自动化（UIElements） | Unity 2021.3+ |
| 编辑器 UI 自动化（IMGUI 截图 + send_event） | Unity 2021.3+ |
| 构建管线 | Unity 2021.3+ |
| 物理系统（射线检测、范围检测、物理材质） | Unity 2021.3+ |
| 导航系统（旧版 NavMeshBuilder） | Unity 2021.3+（内置） |
| 导航系统（NavMeshSurface） | 需安装 `com.unity.ai.navigation` 包 |
| 地形系统 | Unity 2021.3+（内置） |
| 音频（AudioSource、导入设置） | Unity 2021.3+ |
| 音频（Mixer 创建） | Unity 2021.3+（实验性，内部 API） |
| 时间轴 | 需安装 `com.unity.timeline` 包 |
| 2D Tilemap | 需要 `com.unity.2d.tilemap` 模块（2D 项目内置） |
| 2D SpriteAtlas | Unity 2021.3+ |

### 已知限制

| 限制 | 详情 |
|------|------|
| IMGUI 窗口 | 无法枚举元素 — 需用 `screenshot` + `send_event` 通过坐标操作 |
| `switch_platform` | 触发 Domain Reload，可能短暂断开 MCP 连接 |
| 音频播放 | `play`/`stop`/`pause` 仅在 Play Mode 下有效 |
| AudioMixer 编辑 | 实验性功能 — 使用内部 API，不同 Unity 版本可能不兼容 |
| 地形高度图 | 大数据仅支持文件导入/导出，不支持 JSON 数组传输 |
| Timeline | 需要安装 `com.unity.timeline` 包 |
| 可选包依赖 | 运行时通过反射检测 — 缺少包不会导致编译报错 |
