# 首页修改方案（评审，不改代码）

> 角色视角：产品经理 / 高级 UI / 真实客户（冷启动访客）
> 方法：design:design-critique（5 维框架）+ design:ux-copy + 对照 `AGENTS.md` 锁定决策 + 读源码/CSS
> 范围：`app/page.tsx` → `components/landing/*` + `components/site-header.tsx` + `app/globals.css`
> **本文只给方案，未改任何代码。** 下面所有 CSS / 文案都是「建议值」，标注「未应用」。

---

## 0. 结论先行（TL;DR）

- **最重要的一个判断**：首页那个 before/after 交互 demo（Teacher / Sales / Workplace / Client）**完全符合** `AGENTS.md` 锁定的定位；但**它周围的文案回退到了学生场景**（UseCases 的 Extension requests / Lecturer emails / Group-project、hero lead、nav 的 Students、Exam Week Pass）。也就是说，首页**自己跟自己打架**：demo 说「这是给老师/销售/职场/客户的」，紧接着的 UseCases 又说「这是给学生的」。这不是口味问题，是**偏离已锁定的产品定位**（`AGENTS.md` 明确写：通用回复助手，**NOT** student essay rewriting）。修正它＝让首页回到 spec，不是我的个人偏好。
- **最该今天就修的 3 件事（P0）**：① 移动端 Tone check 溢出（根因已定位到一行 CSS）；② 把 UseCases / hero lead / eyebrow 的措辞从「学生重」调回「通用真实回复」；③ 首屏价格信息从 4~5 处砍到 1 句低摩擦承诺。
- **我的评分 ≈ 7.0 / 10**（和你给的 7.3 接近）。扣分集中在：移动端 bug、定位一致性、首屏信息过载。视觉与品牌是加分项。

### 评分分解

| 维度 | 分 | 说明 |
|---|---|---|
| 视觉 / 品牌 | 8.0 | 暖米 `#f6f4ee` + forest `#1e6b4a`，可信、不像模板，差异化好 |
| 首屏价值传达 | 6.5 | H1 有情绪，但「独特价值」三点（像你/保事实/可直接发）被推后了 |
| 定位一致性 | 6.0 | demo 合规，但 UseCases/hero/nav 漂回学生场景，与 `AGENTS.md` 冲突 |
| 信息架构 / 长度 | 6.5 | 8 个区块偏长；Naturalness 整段与 demo 底部重复 |
| 文案 | 7.0 | 诚实、有温度；但 hero lead 一句话既报受众又报价值，两头不利落 |
| 移动端 | 5.5 | 390px 下 Tone check 行确定溢出（根因 = `.nat-bar{min-width:200px}` + 移动端无规则） |
| 可访问性 | 6.5 | 「?」提示对键盘/读屏不可达；触控目标偏小 |
| 转化设计 | 7.0 | demo 是强资产；但信任点（不编造/隐私）埋太深 |

---

## 1. 三视角速读

**产品经理视角 —**
首页目前在「我是谁」上不收敛。锁定定位是**通用回复助手**（teacher / sales / workplace / client，`AGENTS.md` §Core/Sub-positioning, line 327–366, 696–698）。而首页的 UseCases、hero lead、nav「Students」、pricing「Exam Week Pass / Most students start with…」把心智重新拉回学生。结果：老师、销售、职场用户第一眼可能误判「这是学生工具」而流失。**demo 已经对了，把文字对齐到 demo 即可**，不需要重做版式。

**高级 UI 视角 —**
视觉语言成立且克制得当（别再加径向绿光/大圆角/重阴影了）。真正的 UI 债是两块：① **移动端 Tone check 行溢出**（确定性 bug，根因在一行 CSS）；② **命名漂移**——同一个东西在页面上有 4 个名字：`Naturalness Check`（`AGENTS.md` canonical + 组件名）/ `Tone check`（UI 标签）/ `Signal`（区块编号）/ `AI-like signal`（Step 04）。对用户是认知噪音。

**客户（冷启动访客）视角 —**
2 秒内我被 H1「Send the message you've been avoiding.」打动了，也被 demo 说服了。但紧接着我有三个疑问没被第一时间回答：**它会不会瞎编？**（在 FAQ 第 2 条，太深）**我的内容会被存吗？**（FAQ 第 7 条，更深）**先试要不要付钱/绑卡？**（要，但首屏却先甩了 4 个套餐数字，制造了「这很贵很复杂」的错觉）。把这三点前移到 demo 附近，转化会更顺。

---

## 2. P0 必修（3 项，按优先级）

### P0-1　移动端 Tone check 溢出（确定性 bug，最高优先）

- **现象**：390px 视口下，demo 底部 `Tone check` 行的进度条 + `−X pts` 被挤出视口右侧（横向溢出）。
- **根因（已定位）**：
  - `app/globals.css:486` → `.nat-bar { min-width: 200px }`
  - `app/globals.css:453` → `.nat { display:flex; gap:18px; flex:1 }`（**不换行**，内含 label + bar + delta）
  - `@media (max-width:680px)`（`globals.css:1418–1433`）**完全没有** `.nat / .compare-foot / .nat-bar` 的规则。
  - 算账：390 − 48(wrap padding) − 内边距 ≈ 318px 可用；而 `label(~80) + 18 + bar(min 200) + 18 + delta(~60)` ≈ **376px**，装不下 → 最右的 `.nat-delta` 溢出。
- **建议修法（未应用，加进 680px 媒体块即可，桌面不受影响）**：

  ```css
  /* 建议加入 @media (max-width: 680px) —— 未应用 */
  .compare-foot { flex-direction: column; align-items: stretch; gap: 14px; }
  .nat          { flex-wrap: wrap; gap: 10px 12px; }
  .nat-label    { order: 1; }
  .nat-delta    { order: 2; margin-left: auto; }   /* label 左、delta 右，同一行 */
  .nat-bar      { order: 3; min-width: 0; flex-basis: 100%; }  /* 进度条独占一行 */
  ```

  思路：移动端把 foot 竖排，`.nat` 内部「label + delta」一行、进度条独占下一行，并解除 `min-width:200px`。约 6 行，纯移动端作用域。

### P0-2　定位回归「通用真实回复」（文案改动，**无版式改动**）

把三处「学生重」措辞对齐到锁定定位与下方 demo：

| 位置 | 现在 | 问题 | 建议（未应用） |
|---|---|---|---|
| Hero lead | `…for extension requests, lecturer emails, client replies, and group-project messages…` | 4 个例子里 3 个是学生场景 | 改为通用四件套 `teacher messages, sales follow-ups, workplace email, and client replies`（见 §4 文案库） |
| UseCases 5 卡 | Extension requests / Lecturer emails / Client replies / Group-project / Make this less rude | 学生编码，和 demo（T/S/W/C）讲两个故事 | 换成与 demo 对齐的通用 5 卡（见 §4） |
| Nav + Pricing | `Students` 导航、`Exam Week Pass`、`Most students start with…` | 把首页心智重新拉向学生 | 学生场景**收进已存在的 `/students` 页**承接；首页 nav 保留 Pricing / Developers，Students 作为次级入口或下移 |

> 注：老师**回复学生/家长**属于 in-spec（`AGENTS.md` line 346–350 就是 teacher→student 的例子）；要剔除的是「替学生写作业」这类 student-essay 编码。两者别混。

### P0-3　首屏价格「瘦身」为一句承诺

价格/配额数字目前在首屏出现 **5 处**：hero eyebrow、hero stats ×2（`55/mo`、`110/mo`）、（下方还有）Trust「Billing」、Pricing、Closing ×3 行。冷启动访客还没理解产品就被套餐淹没。

- Hero eyebrow：`Start free · Starter NZ$9.90/mo · Pro/API for developers` → **`3 free rewrites · no card required`**（单一低摩擦承诺）。
- Hero stats：去掉 `55/mo`、`110/mo` 两个配额数字（详见 §3 Hero）。
- 套餐细节**只**放 Pricing 区。

---

## 3. 逐区块建议（含具体值，均「未应用」）

> 严重度：🔴 必修　🟡 建议　🟢 打磨

### Header（`site-header.tsx`）
- 🟡 `Students` 与锁定定位张力最大。建议：导航主项＝`Pricing` / `Developers`；`Students` 作为页内/footer 次级入口，或并入「Use cases」下拉。品牌名 `Reply In My Voice` 与 mark `R` 都 OK。

### Hero（`hero.tsx`）
- 🔴 lead 改通用四件套（P0-2）。
- 🔴 eyebrow 改单句承诺（P0-3）。
- 🟡 `stats` 数组现在 4 项里 3 项是配额数字，1 项 `Warm · Direct` 其实是「功能」不是「指标」，混在 stats 里别扭。建议**整排换成信任行**（也顺手把 §1 客户的三个疑问前移）：
  - `Keeps facts intact` · `You review before sending` · `No card to start`
- 🟢 次级 CTA `See examples` 锚到 `#workflow`，但桌面端 demo 就在 hero 内、已经可见，点了像没反应。建议改 `Try the live demo` 并指向 demo，或改指 `#cases`。

### Interactive Demo（`interactive-demo.tsx` / `sample-cases.ts`）—— 最强资产，基本保留
- 🟢 tab 的字母 icon `T / S / W / C` 像占位符。`lucide-react`（`^0.468.0`）已是依赖，可换成语义图标（如 `GraduationCap / Phone / Briefcase / MessageSquare`）。
- 🟢 Sales 样例 `after = 41%` 略高于 `AGENTS.md` 提到的 naturalness 阈值 40%（line 253）。展示资产里出现「没过线」的数字会有一丝违和；可微调样例数据到 ≤40，或不展示该百分比、只展示 `−X pts` 落差。
- 🟢 「?」提示见 §6 可访问性。

### UseCases（`use-cases.tsx`）
- 🔴 5 张卡换通用集（P0-2，文案见 §4）。
- 🟢 字母 icon `E/L/C/G/!` 同样建议换 lucide。
- 🟢 标题 `Real reply moments. Not blank-box rewriting.` 很好，保留。

### How It Works（`how-it-works.tsx`）
- 🟡 Step 04 文案 `Compare the before/after AI-like signal`。`AI-like signal` 是 `AGENTS.md` **批准词**（line 1680），并非违规；但它是对冷启动访客**最像「检测规避」联想**的措辞。建议公开首屏改用更温和的同义批准词：`Compare the before/after naturalness reference`（或与全站统一后的那个名字，见 §5）。
- 🟢 4 步结构清晰，保留。

### Naturalness（`naturalness.tsx`）—— 建议精简/合并
- 🟡 这是**独立一整段**，但内容（before/after 进度条 + 免责声明）与 demo 底部高度重复，且把页面拉长（呼应你 review 的「页面偏长」）。同时它最依赖「signal」框架。建议：**把它压缩成 Trust 里的一张卡**（或一个 2–3 行的小注释块），既缩短页面、又弱化检测联想。一举两得。
- 🟡 免责声明写得诚实（合规友好），保留其实质，但不必占一屏。

### Trust（`trust-panel.tsx`）
- 🟡 4 卡里 `Billing` 又重复了 `NZ$9.90 / NZ$19.90`——这是价格的第 4 次出现。建议把 `Billing` 卡换成**「不编造」**卡（把 FAQ 第 2 条前移成信任点），价格统一只在 Pricing 区讲。
- 🟢 `Fact-credible / Decision layer / Tone check` 三卡是真正的差异化信任点，保留。

### Pricing（`pricing-v2.tsx`）
- 🟡 paid 卡密度过高：Starter + Pro/API（并排）+ 列表里又塞 `Exam Week Pass` + `Top-up` + `no rollover` + `Most students start with…`。建议拆成 **3 个清晰入口**：`Free` / `Starter（月度主推）` / `One-time pass（Exam Week / Top-up）`；`Pro/API` 作为开发者次级入口（可链到 `/developers`）。
- 🔴 删掉 `Most students start with Exam Week Pass or Starter`（学生编码 + 与定位冲突）。

### FAQ（`faq.tsx`）
- 🟢 8 条质量高、诚实。但**第 2 条（不编造）和第 7 条（不保存内容）是转化关键**，建议把这两点**也**以一行信任条前移到 demo 附近（FAQ 仍可保留完整版）。
- 🟢 见 §6：FAQ 用 `div[role=button]` 可用，但原生 `<button>` 更稳。

### Closing CTA（`closing-cta.tsx`）
- 🟢 三行 meta（`3 free…` / `Starter NZ$9.90…` / `Exam Week Pass…`）信息略密。这里是页尾、放价格是合理的，但与 Pricing 区重复；可精简到 2 行：`3 free rewrites · No card required` + `Starter NZ$9.90/mo · Cancel anytime`。

---

## 4. 文案库（可直接取用，未应用）

### Hero H1
| 选项 | 文案 | 语气 | 适用 |
|---|---|---|---|
| A（保留） | `Send the message you've been avoiding.` | 共情、痛点 | 现有，情绪钩子强且**普适**（人人都拖过难发的信） |
| B | `Replies that sound like you — with the facts intact.` | 清晰、价值先行 | 想第一眼说清独特价值 |
| C | `Send the reply you've been putting off — in your own voice.` | 痛点 + 价值 | 想兼得情绪与价值 |

> 推荐：**保留 A**（普适且有情绪），把「独特价值」交给 lead 去补——见下。

### Hero lead（推荐替换）
- 主推：`Turn a rough or too-stiff draft into a reply that sounds like you, keeps every fact intact, and is ready to send — for teacher messages, sales follow-ups, workplace email, and client replies.`
- 更短：`Turn a rough draft into a reply that sounds like you, keeps the facts intact, and is ready to send.`（受众四件套下移到 UseCases）

> 要点：把产品三支柱 **sounds like you / keeps facts intact / ready to send** 在首屏第一时间讲清（呼应你 review 第 2 点）。

### Hero eyebrow
- 推荐：`3 free rewrites · no card required`
- 备选：`Start free · 3 rewrites, no card`

### UseCases 5 卡（对齐 demo 与锁定定位）
| # | 标题 | body（建议） |
|---|---|---|
| 01 | `Teacher & student replies` | `Reply to a student or parent clearly, while keeping the policy and the facts intact.` |
| 02 | `Sales follow-ups` | `Chase a quote or proposal without sounding pushy or robotic.` |
| 03 | `Workplace email` | `Deliver a delay, a no, or a status update that stays professional and human.` |
| 04 | `Client replies` | `Answer scope questions, delays, and awkward updates with care and precision.` |
| 05 | `Make it less sharp` | `Keep the point, lower the heat, and make the next step clear before you send.` |

> 学生专属场景（extension requests / group-project）→ 收进 `/students`。

### CTA 微文案
| 元素 | 现在 | 建议 |
|---|---|---|
| 主 CTA | `Start rewriting →` | 保留（动词开头、明确） |
| 次 CTA | `See examples` | `Try the live demo`（指向 demo）或 `See use cases`（指向 `#cases`） |
| 主 CTA 下方 | （无） | 加 `3 free rewrites · no card required`（信任前移） |
| Pricing free | `Create a free account →` | 保留 |
| Pricing paid | `Compare plans →` | 保留 |

---

## 5. 命名统一（一个东西，一个名字）

现状 4 个名字：`Naturalness Check`（`AGENTS.md` canonical line 674 + 组件名）/ `Tone check`（UI 标签）/ `Signal`（区块编号 04）/ `AI-like signal`（Step 04）。

- **规则**：`AGENTS.md` 是合同，UI 不能默默偏离它（见 `CLAUDE.md`「AGENTS.md wins」）。
- **两条路，任选其一，但全站只用一个**：
  - **路径 A（对齐 spec）**：全站统一用 `Naturalness Check`。把 `Tone check`、`04 · Signal`、`AI-like signal` 都改成它。
  - **路径 B（偏好更温和的 Tone check）**：若你更想要 `Tone check` 的温度，则**先更新 `AGENTS.md`** 把 canonical 改成 `Tone check`，再让 UI 跟随（顺序很重要，别让 UI 单方面漂移）。
- **公开首屏的额外建议**：即便 `AI-like signal` 是批准词，也别让它当首屏主打词；首屏用 `Naturalness reference` / 统一后的名字更稳，`AI-like` 留给更深的解释文案（或不用）。

---

## 6. 可访问性 quick wins（未应用）

| 项 | 现状 | 建议 | 严重度 |
|---|---|---|---|
| demo「?」提示 | `.nat-label .q` 是 `aria-hidden=true` + 仅 `title` | 键盘/读屏用户拿不到解释。改成可聚焦 `<button aria-label="…">`，或把解释做成可见的小字 | 🟡 |
| 触控目标 | `.tone-toggle button` / `.compare-tab` padding `9px 14px`（≈33px 高） | 移动端确保 ≥44px 命中区 | 🟡（移动端） |
| NatBar 文字对比 | 白字压在 `--accent`(forest) / `--warn` 上 | forest 上白字大概率过 AA；`--warn`(暖橙) 上白字需实测，必要时加深或换深色字 | 🟢 待测 |
| FAQ 可点元素 | `div[role=button]` + tabIndex + keydown | 功能 OK；换原生 `<button>` 语义更稳、读屏更友好 | 🟢 |

---

## 7. 不要做（保持克制 / 别违规）

- **别把学生 essay 场景加回首页**——违反锁定定位与品牌安全（`AGENTS.md` line 366：明确 NOT student essay rewriting / academic misconduct）。
- **别再加装饰**——径向绿光、超大圆角、重阴影目前刚好，再加就有「AI SaaS 模板感」（呼应你 review 第 10 点）。
- **别让 UI 词汇单方面偏离 `AGENTS.md`**——改名先改 spec（见 §5）。
- **别往首屏再堆价格**。
- **禁用词**（CI grep：`humanizer / bypass / undetect / detector / evade`）——本方案任何落地都不得触发；公开文案也尽量避开「检测规避」联想。

---

## 8. 建议落地顺序（仅排序，不代表现在执行）

| 阶段 | 项 | 类型 | 工作量 |
|---|---|---|---|
| **P0** | ① 移动端 Tone check 溢出 | CSS（680px 媒体块 ~6 行） | 极小 |
| **P0** | ② UseCases / hero lead / eyebrow 调通用 | 纯文案 | 小 |
| **P0** | ③ 首屏价格瘦身为单句承诺 | 文案 + 删 stats | 小 |
| **P1** | ④ 命名统一（含 Step 04 软化） | 文案（可能含 `AGENTS.md`） | 小 |
| **P1** | ⑤ Naturalness 段并入 Trust，缩短页面 | 版式 + 文案 | 中 |
| **P1** | ⑥ Hero stats → 信任行（不编造/隐私/免卡前移） | 文案 + 小版式 | 小 |
| **P2** | ⑦ 字母 icon → lucide | 组件 | 中 |
| **P2** | ⑧ 可访问性（提示/触控/对比） | 组件 + CSS | 中 |
| **P2** | ⑨ Pricing 拆 3 入口、删学生编码 | 版式 + 文案 | 中 |

> 执行说明：本轮是交互式会话，真要改时——P0① 是几行 CSS，P0②③ 是纯文案，风险都很低；可直接改或走 Codex，由你定。**本文档本身不含任何代码改动。**
