# 新手教程：从零跑第一次 Shield 自检

语言：[En](shield-beginner-guide.en.md)

CARVES Shield 用来检查一个仓库是否有 AI 代码治理证据。

它不问“代码写得好不好”，而是问：

```text
AI 改代码时，这个项目有没有边界？
下一次 AI 接手时，有没有可靠交接？
出过的判断和阻断，能不能被查回去？
```

这件事重要，是因为 AI 写代码很快，但人类 review 的注意力有限。没有边界、没有交接、没有审计时，AI 很容易一次改太多、改到不该改的地方，或者在下一次会话里重复犯同一个错误。

Shield v0 的目标很朴素：让任何项目先拿到一个本地 self-check 结果，知道自己当前在哪个位置。

## 你需要准备什么

你只需要三样东西：

1. 一个 git 仓库。
2. CARVES CLI。
3. 一个 `shield-evidence.v0` 摘要证据文件。

摘要证据文件不是源码，也不是 diff。它只记录布尔值、计数、时间窗口、工作流路径、规则 id 这类安全摘要。

## 第一次运行

在安装好 CLI 后，先用仓库里的示例证据跑通命令：

```powershell
carves shield evaluate docs/shield/examples/shield-evidence-standard.example.json --json --output combined
```

你会看到两类结果：

```text
Standard: CARVES G8.H8.A8 /30d PASS
Lite: 90/100 strong
```

Standard 保留三个维度：

```text
G  Guard    输出治理，AI patch 有没有经过边界检查
H  Handoff  输入治理，下一次 AI 会话有没有可靠上下文
A  Audit    历史治理，过去的判断能不能被追溯和解释
```

Lite 把三个维度折算成一个 0-100 分，适合快速分享和比较。

G/H/A、Lite score、PASS、REVIEW、BLOCK、local self-check、challenge result 和 verification result 的准确定义见 [术语表](glossary.zh-CN.md)。

## 给自己的项目准备证据

建议在项目里创建：

```text
.carves/shield-evidence.json
```

然后从示例复制一份：

```powershell
Copy-Item docs/shield/examples/shield-evidence-standard.example.json .carves/shield-evidence.json
```

按你的项目实际情况修改里面的摘要字段。比如：

- 是否有 Guard policy。
- 是否在 GitHub Actions 里运行 Shield 或 Guard 检查。
- 最近 30 天有多少 allow、review、block 决策。
- 是否有 Handoff packet。
- 是否有可读 Audit log。

不要把源码、raw diff、prompt、模型回复、secret、credential 放进证据文件。

## 生成 Badge

本地生成一个静态 SVG：

```powershell
carves shield badge .carves/shield-evidence.json --output docs/shield-badge.svg
```

查看 JSON 元数据：

```powershell
carves shield badge .carves/shield-evidence.json --json
```

Badge 是 self-check 标记，不是官方认证。

## 接入 GitHub Actions

最小流程是：

1. checkout 仓库。
2. 安装 CARVES CLI。
3. 运行 `carves shield evaluate`。
4. 运行 `carves shield badge`。
5. 上传生成的 JSON 和 SVG artifact。

完整示例见 [GitHub Actions 接入](github-actions.zh-CN.md)。

## 如何解读颜色

```text
red     critical failure，配置了治理但关键门失败
gray    no evidence，没有可用证据
white   basic，有基础配置
yellow  disciplined，有持续纪律
green   strong，有强证据或持续证据
```

不要只看 Lite 分数。Standard 的 `G/H/A` 能告诉你到底是哪一维弱。

## 常见误解

Shield v0 不是：

- 不是代码质量评分。
- 不是安全漏洞扫描器。
- 不是 AI 编程工具。
- 不是 hosted API。
- 不是公开排行榜。
- 不是认证证书。
- 不是操作系统沙箱。

Shield v0 是：

- 本地 self-check。
- 摘要证据评估。
- AI 代码治理姿态的可读标签。
- 可以放进 CI 的轻量 proof。
