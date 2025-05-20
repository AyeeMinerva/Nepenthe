# git-crypt

## 安装

在用户的powershell上
```ps1
Set-ExecutionPolicy RemoteSigned -Scope CurrentUser # 可选，如果执行策略阻止脚本运行
irm get.scoop.sh | iex
scoop install git-crypt
```

## 密钥分发（共享 key 文件）
`git-crypt init`
初始化后，导出密钥：
```
git-crypt export-key gitcrypt.key
```
协作者收到 gitcrypt.key 后，导入：
```
git-crypt unlock gitcrypt.key
```
之后协作者拉代码时自动解密，无需额外操作。

## 加密方式

在 .gitattributes 文件中，**git-crypt** 的加密语法如下：

```
<文件模式> filter=git-crypt diff=git-crypt
```

- `<文件模式>`：要加密的文件或目录的匹配模式，语法和 .gitignore 类似。
- `filter=git-crypt`：启用 git-crypt 加密。
- `diff=git-crypt`：让 git diff 显示友好的信息（而不是乱码）。

---

### 常见示例

```gitattributes
# 加密所有 .env 文件
.env filter=git-crypt diff=git-crypt

# 加密 secrets 目录下所有内容
secrets/** filter=git-crypt diff=git-crypt

# 加密所有 .key 文件
*.key filter=git-crypt diff=git-crypt

# 加密 config/secret.yaml
config/secret.yaml filter=git-crypt diff=git-crypt
```

---

### 注意事项

- 只对新提交的文件生效，历史提交不会自动加密。
- .gitattributes 文件本身不要加密。
- 你可以在同一个仓库里只加密部分文件。

---