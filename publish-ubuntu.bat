@echo off
chcp 65001 >nul
setlocal
cd /d "%~dp0"

echo [NoChat] 正在交叉编译：Ubuntu / Linux x64（自包含 + 裁剪 + 单文件）...
echo 若报错与运行时或文件占用有关，请先关闭正在运行的 NoChat.App 再重试。
echo.

dotnet publish NoChat.App\NoChat.App.csproj ^
  -c Release ^
  -r linux-x64 ^
  --self-contained true ^
  -p:PublishTrimmed=true ^
  -p:TrimMode=partial ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:InvariantGlobalization=true ^
  -o Publish\publish-ubuntu ^
  --nologo

if %ERRORLEVEL% neq 0 (
    echo 发布失败。
    exit /b 1
)

echo.
echo 输出目录: %CD%\Publish\publish-ubuntu
echo 主程序: Publish\publish-ubuntu\NoChat.App
echo.
echo 将 Publish\publish-ubuntu 整个目录拷贝到 Ubuntu 后，在终端执行：
echo   chmod +x NoChat.App
echo   ./NoChat.App
exit /b 0
