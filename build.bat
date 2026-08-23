@echo off
REM ============================================================
REM  SuperClip 一键发布脚本
REM  前提：已安装 .NET SDK（Windows x64，如 .NET 8 SDK 即可编译 .NET Core 3.1）
REM  用法：双击本文件，或在命令行执行 build.bat
REM  产物：bin\Release\netcoreapp3.1\win-x64\publish\SuperClip.exe
REM ============================================================
cd /d %~dp0

echo [1/3] 检查 .NET SDK ...
dotnet --version >nul 2>&1
if errorlevel 1 (
    echo 错误：未检测到 .NET SDK，请先安装 .NET SDK (https://dotnet.microsoft.com/download/dotnet)
    pause
    exit /b 1
)
dotnet --version

echo [2/3] 开始发布 (.NET Core 3.1 / win-x64 自包含 / 单文件，支持 Windows 7+) ...
echo 注：WPF 框架不支持 IL 剪裁，故不启用 PublishTrimmed。
echo     单文件模式已固化到 csproj（无需命令行参数）；分发时不分发 .pdb，产物仅 SuperClip.exe。
echo     已限定卫星程序集为 en;zh-Hans；未启用 InvariantGlobalization（WPF TextBox 需全量区域以兼容中文/日文 IME）。
REM 命令行显式传 PublishSingleFile=true 兜底
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true

echo [3/3] 完成。
echo 可执行文件位于：
echo %~dp0bin\Release\netcoreapp3.1\win-x64\publish\SuperClip.exe
echo.
pause
