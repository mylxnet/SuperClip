@echo off
REM ============================================================
REM  SuperClip 便携安装包（ZIP 形态）
REM  流程：调 CleanAndBuild.bat 发布 → 抽取带版本号的单文件 exe →
REM       打包 install.bat / uninstall.bat / SuperClip_vX.Y.Z.exe / README 到 release\SuperClip_vX.Y.Z_portable.zip
REM       （GitHub Releases 附件名不支持中文字符，统一用英文 portable 命名）
REM  用户解压后双击 install.bat 即可装到 %ProgramFiles%\SuperClip\（统一安装为 SuperClip.exe）
REM  并创建开始菜单/桌面快捷方式。
REM  卸载：控制面板「程序和功能」/ 重跑 uninstall.bat。
REM ============================================================
setlocal
cd /d %~dp0

REM 1) 干净发布
call "%~dp0CleanAndBuild.bat"
if errorlevel 1 exit /b 1

REM 2) 取版本号（从 csproj 的 <Version> 解析，简单粗暴 grep 一下）
set VERSION=
for /f "tokens=2 delims=><" %%v in ('findstr /r /c:"<Version>" SuperClip.csproj') do set VERSION=%%v
if "%VERSION%"=="" set VERSION=1.1.2
echo 检出版本: %VERSION%

set PUB=bin\Release\netcoreapp3.1\win-x64\publish
set OUT_DIR=release\SuperClip_v%VERSION%_portable
set OUT_ZIP=release\SuperClip_v%VERSION%_portable.zip

if not exist "%PUB%\SuperClip.exe" (
    echo 错误：未找到发布产物 %PUB%\SuperClip.exe
    pause
    exit /b 1
)

if exist "%OUT_DIR%" rmdir /s /q "%OUT_DIR%"
if exist "%OUT_ZIP%" del /q "%OUT_ZIP%"
mkdir "%OUT_DIR%"

echo.
echo 拷贝产物到打包目录 ...
REM 优先取 csproj 发布目标生成的版本化单文件；不存在则退回复制+重命名
if exist "%PUB%\SuperClip_v%VERSION%.exe" (
    copy /y "%PUB%\SuperClip_v%VERSION%.exe" "%OUT_DIR%\"
) else (
    copy /y "%PUB%\SuperClip.exe" "%OUT_DIR%\SuperClip_v%VERSION%.exe"
)
copy /y "README.md" "%OUT_DIR%\"
copy /y "installer\install.bat" "%OUT_DIR%\"
copy /y "installer\uninstall.bat" "%OUT_DIR%\"

echo 创建 ZIP: %OUT_ZIP%
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "Compress-Archive -Path '%OUT_DIR%\*' -DestinationPath '%OUT_ZIP%' -Force"
if errorlevel 1 (
    echo 错误：PowerShell 压缩失败
    pause
    exit /b 1
)

echo 清理临时目录 ...
rmdir /s /q "%OUT_DIR%"

echo.
echo 完成：%OUT_ZIP%
echo.
pause
