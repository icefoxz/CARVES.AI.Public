# CARVES Runtime Versioning Policy

Status: Runtime dist 与相关包的版本规则。

这个文件定义“每一种版本号到底代表什么”。以后升级版本时，先按这里判断，不要让版本定义散落在聊天、脚本、文档、tag、包和 dist manifest 里各说各话。

## 一句话规则

CARVES 不能只看一个版本号。

每次准备升级或发布，必须先说明这次改的是哪一个版本维度：

- Runtime dist version
- Runtime CLI package version
- product package version
- schema / protocol version
- release proof id
- git tag
- generated dist manifest source commit
- release checkpoint / BOM document

改了一个维度，不等于自动授权改其他维度。

## 版本号格式

包和 dist 候选默认使用 prerelease SemVer。Runtime 当前采用无候选编号的 prerelease 标签，唯一性由 `major.minor.patch` 承担：

```text
0.5.1-beta
| | | |
| | | 发布阶段
| | patch / 同能力线修补包
| minor / 能力线
major
```

大白话定义：

| 部分 | 含义 | 什么时候升级 |
| --- | --- | --- |
| `major` | 大边界版本。 | 老用户不能按原方式安全继续用，或产品定位变了。 |
| `minor` | 能力版本。 | 新增对外有意义的能力、入口、包结构或 proof lane。 |
| `patch` | 修补版本。 | 修 bug、修文档、修验证、修打包，不改变使用边界。 |
| `alpha` | 早期试用。 | 内部验证、dogfood、接口还可能变。 |
| `beta` | 有限制的外部试用。 | 主路径能跑通，但限制和人工 gate 仍要明说。 |
| `rc` | 发布候选。 | 功能冻结，只修阻塞发布的问题。 |

Runtime 不再使用 `beta.1` / `beta.2` 这类候选编号。原因很简单：一旦 dist 被外部项目绑定，每个可消费包都必须有唯一版本号，不能靠 prerelease suffix 承担 patch 的职责。

推荐序列：

```text
0.5.0-beta  # 新能力线
0.5.1-beta  # 同能力线修补包
0.5.2-beta  # 同能力线下一次修补包
0.6.0-beta  # 下一条新能力线
0.6.2-beta  # 0.6 线打包/验证修补包
```

历史包或子产品已经发布过的 `beta.N` 版本不回写。等对应产品线下一次升级时，再按这条规则迁移。

## 版本维度

| 维度 | 例子 | 它回答的问题 | 谁负责升级 |
| --- | --- | --- | --- |
| Runtime dist version | `CARVES.Runtime-0.5.0-beta` | 外部项目应该绑定哪个冻结 Runtime 文件夹？ | Runtime release |
| Runtime CLI package version | `CARVES.Runtime.Cli 0.5.0-beta` | `carves` 命令本身是什么包版本？ | Runtime CLI |
| Product package version | `CARVES.Guard.Cli 0.2.0-beta.1` | 某个工具自己的成熟度和行为到哪了？ | 对应工具 |
| Schema / protocol version | `shield-evidence.v0`, `matrix-verify.v0` | JSON 或协议格式是哪一版？ | 协议 owner |
| Release proof id | `matrix-v0.1.0-rc.1` | 这次 proof / readiness bundle 是哪一轮？ | release proof owner |
| Git tag | `carves-runtime-v0.5.0-beta` | 哪个源码 commit 被命名为这个发布点？ | operator gate |
| Dist manifest source commit | `MANIFEST.json.source_commit` | 这个 dist 是从哪个源码 commit 打出来的？ | pack script |
| Release checkpoint / BOM | `docs/release/runtime-0.5.0-beta-version-bom.md` | 这次候选包包含哪些版本、证据、限制和未完成 gate？ | release docs |

## 默认升级规则

用最小但诚实的版本变化：

| 情况 | 应该怎么做 |
| --- | --- |
| 现有 local dist stale，但还在同一条 release 线上。 | 升 patch，例如 `0.5.0-beta` 到 `0.5.1-beta`。 |
| 新增外部有意义的能力、入口、包结构或 proof lane。 | 升 minor，例如 `0.5.2-beta` 到 `0.6.0-beta`。 |
| `carves` 命令行为、入口或打包输出变了。 | 升 `CARVES.Runtime.Cli`，通常和 Runtime dist 候选对齐。 |
| Guard 规则或 Guard 输出语义变了。 | 只升 Guard 包。 |
| Handoff、Audit、Shield 或 Matrix 自己行为变了。 | 只升对应工具包。 |
| JSON schema 或协议破坏兼容。 | 升 schema / protocol version，不要复用旧 schema 名字。 |
| 只是用同一批产品包重新跑 proof。 | 刷新 release proof id，不一定升产品包版本。 |
| 只修历史文档，不改变当前用户入口。 | 通常不升产品版本。 |
| 要创建 GitHub release、tag 或 NuGet 发布。 | 需要 operator 明确批准，并记录发布决策。 |

## Runtime Dist BOM 要求

任何打算给外部项目绑定的 Runtime dist 候选，都必须有 BOM 或 release checkpoint 记录：

- Runtime dist version
- source commit SHA
- Runtime CLI package version
- included product package versions
- 本次 release claim 依赖的 schema / protocol versions
- proof commands 与 artifact locations
- known limitations
- public non-claims
- 尚未执行的 operator gates

Runtime dist 可以包含不同成熟度的子包，只要 BOM 写清楚。例如：

```text
Runtime dist: 0.5.0-beta
CARVES.Runtime.Cli: 0.5.0-beta
CARVES.Guard.Cli: 0.2.0-beta.1
CARVES.Handoff.Cli: 0.1.0-alpha.1
CARVES.Audit.Cli: 0.1.0-alpha.1
CARVES.Shield.Cli: 0.1.0-alpha.1
CARVES.Matrix.Cli: 0.2.0-alpha.1
```

这不表示所有子包都是 beta。它只表示这个 Runtime dist 候选里包含这些版本。

## Tag 命名

新的 Runtime tag 优先使用更明确的名字：

```text
carves-runtime-v0.5.0-beta
```

避免新的模糊 tag：

```text
carves-0.5.0-beta
```

历史 tag 不需要改名。

## 历史文档

不要为了“看起来整齐”批量改历史 release notes、phase docs 或 checkpoints。

只更新当前用户会照着跑的入口文档，以及新的 release checkpoint。历史文档应该保留当时的事实；如果确实要修正，需要明确标注这是 correction。

## Operator Gates

版本号升级不自动代表已经做了这些事：

- 创建 git tag
- 创建 GitHub release
- 上传 release assets
- 推 NuGet.org packages
- package signing
- 声称 hosted verification
- 声称 certification、leaderboard、model safety benchmark 或 OS sandboxing

这些仍然是 operator-owned gates。
