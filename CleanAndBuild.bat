@echo off
REM ============================================================
REM  SuperClip 一键清理 + 发布
REM  与 build.bat 区别：先清 bin/obj 避免缓存干扰，再发布单文件。
REM  适合每次发布前跑一次，确保产物完全重建。
REM  产物：bin\Release\netcoreapp3.1\win-x64\publish\SuperClip.exe
REM ============================================================
cd /d %~dp0

echo [1/3] 清理 bin/obj ...
if exist bin rmdir /s /q bin
if exist obj rmdir /s /q obj

echo [2/3] 检查 .NET SDK ...
dotnet --version >nul 2>&1
if errorlevel 1 (
    echo 错误：未检测到 .NET SDK，请先安装 .NET SDK (https://dotnet.microsoft.com/download/dotnet)
    pause
    exit /b 1
)
dotnet --version

echo [3/3] 发布单文件 ...
REM 命令行显式传 PublishSingleFile=true 兜底（防 csproj 里被覆盖或漏生效）
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
if errorlevel 1 (
    echo 发布失败
    pause
    exit /b 1
)

echo.
echo 完成。可执行文件位于：
echo %~dp0bin\Release\netcoreapp3.1\win-x64\publish\SuperClip.exe
echo.
pause
