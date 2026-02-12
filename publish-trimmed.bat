@echo off
chcp 65001 >nul
setlocal
cd /d "%~dp0"

echo [NoChat] 正在发布：裁剪 + 单文件 (win-x64) ...
echo 若报错「文件正在被使用」，请先关闭正在运行的 NoChat.App 再重新执行本脚本。
echo.
dotnet publish NoChat.App\NoChat.App.csproj -c Release -p:PublishProfile=trimmed-win-x64 -o Publish\publish-trimmed --nologo

if %ERRORLEVEL% neq 0 (
    echo 发布失败。
    exit /b 1
)

echo.
echo 输出目录: %CD%\publish\publish-trimmed
echo 主程序: publish\publish-trimmed\NoChat.App.exe
exit /b 0
