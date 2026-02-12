@echo off
chcp 65001 >nul
setlocal
cd /d "%~dp0"

echo [NoChat] 正在发布：Native AOT (win-x64)，耗时可能较长 ...
dotnet publish NoChat.App\NoChat.App.csproj -c Release -p:PublishProfile=native-aot-win-x64 -o Publish\publish-aot --nologo

if %ERRORLEVEL% neq 0 (
    echo 发布失败。
    exit /b 1
)

echo.
echo 输出目录: %CD%\publish\publish-aot
echo 主程序: publish\publish-aot\NoChat.App.exe
exit /b 0
