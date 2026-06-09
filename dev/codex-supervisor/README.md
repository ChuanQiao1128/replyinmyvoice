# Codex-as-MCP + Claude Supervisor 安装指南

目标：让 Claude（Code 和 Desktop）把 Codex CLI 当成一个 MCP 工具调用，自己只做规划/审查，把代码改动全部下发给 Codex。

> ✅ 你的 Codex CLI 已经原生支持 stdio MCP 服务模式（`codex mcp-server`），不需要额外 wrapper。

## 0. 先验证一下

在终端跑：

```bash
codex mcp-server --help     # 应该输出 MCP server 的说明
which codex                 # 记下绝对路径，Claude Desktop 会用到
codex login                 # 如果还没登录，先把 OpenAI 凭证存进去
```

`codex mcp-server` 是把 Codex 自己当成 server 启动；`codex mcp` 是把 Codex 当成 client 去管理它要连接的其他 server —— 两者方向相反，别混了。

## 1. 装到 Claude Code（CLI）

### 推荐：项目级 `.mcp.json`

```bash
cp /Users/qc/Desktop/CloudFlare/codex-supervisor/.mcp.json.example \
   /Users/qc/Desktop/CloudFlare/.mcp.json
```

下次在 `/Users/qc/Desktop/CloudFlare` 目录启动 `claude`，它会问是否信任这个 MCP server，选信任。`${OPENAI_API_KEY}` 会从你 shell 的环境变量展开 —— 但既然你已经 `codex login` 了，Codex 自己会用 keychain 凭证，env 那段可以删掉。

### 替代：用户级（所有项目都生效）

```bash
claude mcp add codex --scope user -- codex mcp-server
claude mcp list             # 验证 codex 出现
```

### 启用 supervisor 强约束（重要）

两步：

1. **权限封禁**。把 `settings.deny-edit.json` 里的 `permissions` 块合并进 `~/.claude/settings.json`（或项目内 `.claude/settings.json`）。这会硬性禁止 Claude Code 调用 `Edit` / `Write` / `NotebookEdit`，逼它走 `mcp__codex__*` 工具。

2. **行为指令**。把 `SUPERVISOR.md` 里"Hard Rules"和"Workflow Per Request"两节**手动追加**到你想用 supervisor 模式的项目的 `CLAUDE.md`。

   ⚠️ 不要直接覆盖 `/Users/qc/Desktop/CloudFlare/CLAUDE.md` —— 那里已有一套严格的项目规则（AGENTS.md 链接、banned terms 等），supervisor 是叠加进去的。

## 2. 装到 Claude Desktop（你现在用的这个 App）

1. 打开配置文件（不存在就新建）：

   ```bash
   open -e "$HOME/Library/Application Support/Claude/claude_desktop_config.json"
   ```

2. 把 `claude_desktop_config.snippet.json` 里 `mcpServers.codex` 那段合并进去。**改两处**：
   - `command`：换成 `which codex` 的输出（绝对路径，比如 `/Users/qc/.npm-global/bin/codex` 或 `/opt/homebrew/bin/codex`）
   - 如果你 `codex login` 过了，整个 `env` 块可以删掉；否则把 `OPENAI_API_KEY` 写成实际 key

3. **完全退出** Claude Desktop（Cmd+Q，不是关窗口），重启。

4. 新对话里问"你现在能看到哪些 MCP 工具？"，应该列出 `codex` 相关条目。

### Desktop 端的 supervisor 约束（弱）

Desktop 没有 `settings.json` 那样的工具黑名单，只能靠 prompt。两个选择：

- 每开新对话先粘贴 `SUPERVISOR.md` 的内容当开场指令
- 用 Claude Desktop 的 **Projects** 功能，把 SUPERVISOR.md 内容固化为该 Project 的 instructions

实际效果比 Claude Code 那边弱（CLI 是硬封禁，Desktop 是说服），所以**建议把 supervisor 流程放在 Claude Code 跑**，Desktop 端的 codex MCP 当成临时调用工具用。

## 3. 烟雾测试

在 Claude Code 项目里（已经合并好权限封禁），跑：

```
用 codex 工具帮我在 /tmp/codex-smoke-test.txt 里写一行 "hello from codex"。
你自己不要用 Write 工具。
```

预期：Claude 调用 `mcp__codex__*`，Codex 实际写文件，返回结果。如果 Claude 试图直接 Write，permissions deny 会拦下来，它应该自然切到 codex 路径。

## 4. 日常用法示例

```
> 给 app/api/rewrite/route.ts 加超时控制：OpenAI 调用超过 30 秒就返回 504，
  并触发已有的 quota 回滚逻辑。先读相关文件做 plan，然后交给 codex 实现。

[Claude 读 route.ts + quota 服务] →
[Claude 写 plan：1) AbortController；2) 包 openai call；3) 超时调 refundQuotaReservation；4) 返回 504] →
[Claude 调 mcp__codex__... 把 brief 传过去（包含文件路径 + AGENTS.md banned terms 提醒 + 验收条件）] →
[Codex 返回 diff/结果] →
[Claude 跑 git diff、npm test，报告结果。不通过就再次 delegate 修正]
```

## 文件清单

| 文件 | 作用 |
|------|------|
| `.mcp.json.example` | Claude Code 项目级配置模板 |
| `claude_desktop_config.snippet.json` | Claude Desktop 配置片段 |
| `settings.deny-edit.json` | Claude Code 权限封禁（禁 Edit/Write/危险 bash） |
| `SUPERVISOR.md` | supervisor 行为指令，追加到 CLAUDE.md |
| `server/codex_mcp_server.py` | **可选**：增强版 Python wrapper，会自动把 AGENTS.md 的 banned terms 注入到每个 prompt + 支持 inline 上下文文件。原生 `codex mcp-server` 已经够用，这个只在你想做更多 prompt 加工时启用 |

## 进阶：要不要切换到自写 wrapper

原生 `codex mcp-server` 把 Codex 的全部能力以 MCP 工具暴露出来，灵活但"裸"——每次 Claude 下发 prompt 时要自己把 banned terms、项目规则、文件清单写进去。`server/codex_mcp_server.py` 提供一个收紧的接口：只暴露一个 `codex_exec(task, working_dir, extra_context_files)` 工具，内部自动拼接 AGENTS.md 的硬约束。

何时换：你发现 Claude 经常忘记把 banned terms 提醒写进 brief，或者你想强制每次 codex 调用都附带特定上下文。

切换方法：把 `.mcp.json` 里的 `"args": ["mcp-server"]` 换成 `"command": "python3", "args": ["/Users/qc/Desktop/CloudFlare/codex-supervisor/server/codex_mcp_server.py"]`，并先跑一次 `pip install --user "mcp[cli]"` 装依赖。

## 已知坑

- **上下文不同步**：Codex 每次调用都是冷启动，没有对话历史。Claude 必须把所有上下文（文件路径、约束、相关代码片段）打包进每个 brief 里。SUPERVISOR.md 的 brief 模板就是为这个写的。
- **沙箱权限**：Codex 默认沙箱模式可能限制写权限。如果发现 Codex 改不了文件，让 Claude 在 brief 里传 `sandbox: "workspace-write"`，或者全局走 `codex login` 后设置 `~/.codex/config.toml` 的 `sandbox_mode = "workspace-write"`。
- **双倍 token 成本**：Claude 规划用 Anthropic token，Codex 执行用 OpenAI token。trivial 改动（改个变量名）不划算 —— SUPERVISOR.md 里有 watchdog 规则提醒。
- **banned terms 检查**：CloudFlare 项目 AGENTS.md 里禁词是 `humanizer/bypass/undetect/detector/evade`。每次 brief 都要带，否则 Codex 可能写出违规代码。
- **deploy 类操作**：`settings.deny-edit.json` 里禁了 `git commit/push`。需要你亲自推。
