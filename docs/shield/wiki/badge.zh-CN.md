# Badge 解读与发布

语言：[英文](badge.en.md)

Shield badge 是本地生成的 SVG。它适合放在 README、PR artifact 或发布说明里，让读者快速看到当前 self-check 姿态。

## 生成命令

```powershell
carves shield badge .carves/shield-evidence.json --output docs/shield-badge.svg
```

查看 JSON 元数据：

```powershell
carves shield badge .carves/shield-evidence.json --json
```

## Badge 展示什么

可见文字来自 Lite：

```text
CARVES Shield | 90/100 Strong
```

元数据保留 Standard：

```text
G8.H8.A8
```

所以用户可以同时看到：

- 一个容易理解的 Lite 分数。
- 一个更精确的 G/H/A 三维结构。

## 颜色

```text
red     Critical
gray    No Evidence
white   Basic
yellow  Disciplined
green   Strong
```

绿色通常表示强证据或持续证据。黄色表示有治理纪律但还不够强。白色表示基础配置。灰色表示没有可用证据。红色表示 Critical Gate 失败。

## 推荐 README 写法

```markdown
![CARVES Shield](docs/shield-badge.svg)

This badge is a CARVES Shield local self-check. It is not certification.
```

## 不推荐写法

不要写：

```text
Certified by CARVES.
Verified safe by CARVES.
CARVES proves this code is secure.
```

## 什么时候更新 Badge

建议在这些时候重新生成：

- 修改 Shield evidence。
- 修改 Guard/Handoff/Audit 配置。
- 添加 GitHub Actions proof。
- 处理了 block/review 决策。
- 发布 release 前。

## Badge 不能证明什么

Badge 不能证明：

- 源码没有 bug。
- AI 没有犯错。
- 项目通过安全审计。
- 操作系统层面有沙箱。
- CARVES 官方认证了这个项目。
