# GitHub Actions 接入

语言：[En](github-actions.en.md)

Shield 可以作为 pull request CI proof 运行。它只读取 `shield-evidence.v0` 摘要证据，并上传本地生成的 JSON/SVG artifact。

## 推荐文件

```text
.carves/shield-evidence.json
.github/workflows/carves-shield.yml
```

## 可复制 workflow

```yaml
name: carves-shield

on:
  pull_request:
  push:
    branches: [ main ]

jobs:
  shield:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 10.0.x

      - name: Install CARVES CLI
        run: dotnet tool install --global CARVES.Runtime.Cli --version 0.2.0-beta.1

      - name: Prepare Shield artifact directory
        run: mkdir -p artifacts/shield

      - name: Shield evaluate
        run: carves shield evaluate .carves/shield-evidence.json --json --output combined | tee artifacts/shield/shield-evaluate.json

      - name: Shield badge
        run: carves shield badge .carves/shield-evidence.json --json --output artifacts/shield/shield-badge.svg | tee artifacts/shield/shield-badge.json

      - name: Upload Shield artifacts
        uses: actions/upload-artifact@v4
        with:
          name: shield-proof
          path: artifacts/shield
          if-no-files-found: error
```

## 生成的 artifact

```text
artifacts/shield/shield-evaluate.json
artifacts/shield/shield-badge.json
artifacts/shield/shield-badge.svg
```

这些 artifact 适合给 reviewer 看。它们不应该包含源码、raw diff、prompt、模型回复、secret 或 credential。

## 什么时候 CI 会失败

`carves shield evaluate` 在这些情况下会返回非零：

- 证据文件不存在。
- JSON 无效。
- schema version 不支持。
- privacy posture 无效。
- 证据声明包含默认禁止的敏感材料。

`carves shield badge` 在证据无效时也会返回非零，并且不会写出正向 badge。

## 不需要什么

Shield v0 的 CI proof 不需要：

- provider secrets
- hosted API key
- network call
- source upload
- raw diff upload
- prompt upload
- model response upload
- secret upload
- credential upload
- public directory listing
- certification claim

## 私有仓库建议

私有仓库可以在 evidence 里使用 `repo_id_hash`，不要把 private owner/name 写进公开 artifact。

## 发布 Badge

如果想把 badge 放进 README，建议先从 CI artifact 下载 `shield-badge.svg`，确认结果和证据符合预期，再把 SVG 作为普通项目资产发布。

Badge 文字必须保持 self-check 语义，不能写成 verified 或 certified。
