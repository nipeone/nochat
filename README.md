# NoChat - 局域网聊天

跨平台（Windows / macOS / Ubuntu）局域网聊天应用，基于 .NET 8 与 Avalonia UI。

## 功能

- **现代 UI 与主题**：深色界面、顶栏 Logo、圆角按钮与气泡；深色/浅色主题切换（顶栏「切换主题」）；窗口图标（运行时生成）
- **单例进程**：同一时间只允许一个 NoChat 进程；再次双击启动时会唤起已运行窗口并退出新进程。
- **单例进程**：同一时间只允许一个 NoChat 进程；再次双击启动时会唤起已运行窗口并退出新进程。
- **系统托盘**：支持最小化到托盘。**第一次**点击关闭按钮时弹出选择：「退出程序」或「最小化到托盘」，并**记住选择**，后续关闭均按该行为执行。**双击**托盘图标可显示主窗口；托盘右键菜单可「显示主窗口」或「退出」
- **好友列表**：支持**展开/收起**，左侧折叠条点击可隐藏或显示好友列表面板
- **好友自动发现**：同一局域网内打开应用即自动发现并显示好友，无需手动添加；上线后自动广播
- **私聊**：选择好友后输入消息发送，支持 Enter 发送
- **群聊**：核心层已支持群聊消息（向多成员发送），后续可在 UI 中增加群组入口
- **消息撤回**：可对本人发送的消息执行撤回（逻辑已接好，UI 可加右键菜单）
- **文件传输**：选择好友后点击「文件」选择文件发送
- **文件夹传输**：点击「文件夹」选择文件夹发送

## 运行

```bash
cd NoChat.App
dotnet run
```

或从 IDE 直接运行 **NoChat.App** 项目。

## 发布（减小体积 / AOT）

在项目目录下执行：

**1. 裁剪 + 单文件（推荐，体积小、单 exe）**
```bash
cd NoChat.App
dotnet publish -c Release -p:PublishProfile=trimmed-win-x64 -o ../publish-trimmed
```
生成单文件 `NoChat.App.exe`（约 38MB），可直接分发该 exe。

**2. Native AOT（启动快、体积较大）**
```bash
dotnet publish -c Release -p:PublishProfile=native-aot-win-x64 -o ../publish-aot
```
生成原生 exe 及依赖文件，需整体目录分发。其他平台可改 `-r linux-x64`、`osx-x64`、`osx-arm64` 等。

## 技术栈

- **.NET 8**
- **Avalonia UI**：跨平台桌面 UI
- **NoChat.Core**：UDP 发现、TCP 聊天与文件传输

## 端口

- `25560`：单例通知（本机 TCP，仅 127.0.0.1）
- `25565`：发现（UDP 广播）
- `25566`：聊天（TCP）
- `25567`：文件传输（TCP）

确保局域网内防火墙允许上述端口。

## 若无法发现好友（同一 WiFi 下互相看不到）

1. **防火墙**：在 Windows 防火墙中允许 NoChat.App（或 `dotnet`）的入站：UDP 25565、TCP 25566、TCP 25567；或临时关闭防火墙测试。
2. **路由器 AP 隔离**：部分路由器默认开启「AP 隔离 / 客户端隔离」，会禁止 WiFi 设备互相访问，需在路由器管理页面关闭该功能。
3. **发现机制**：程序会向本网段子网广播（如 `192.168.1.255`）和全局广播发送心跳，一般同一 WiFi 下即可互相发现。

## 项目结构

```
NoChat/
├── NoChat.sln
├── NoChat.App/          # Avalonia 桌面应用
│   ├── ViewModels/
│   ├── Converters/
│   ├── MainWindow.axaml
│   └── ...
└── NoChat.Core/         # 核心逻辑
    ├── Discovery/       # 好友发现（UDP）
    ├── Chat/            # 私聊/群聊（TCP）
    ├── FileTransfer/    # 文件/文件夹传输
    └── Models/
```

## 许可证

MIT
