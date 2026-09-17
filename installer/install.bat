@echo off
REM ============================================================
REM  SuperClip 安装脚本
REM  - 复制 SuperClip.exe 到 %ProgramFiles%\SuperClip\
REM  - 创建「开始菜单」与「桌面」快捷方式
REM  - 需管理员权限（写入 Program Files 必需）
REM ============================================================
setlocal
cd /d %~dp0

set INSTALL_DIR=%ProgramFiles%\SuperClip
set EXE=SuperClip.exe
set STARTMENU=%APPDATA%\Microsoft\Windows\Start Menu\Programs
set DESKTOP=%USERPROFILE%\Desktop

REM 兼容带版本号的分发文件名（如 SuperClip_v2.0.1.exe）：
REM 优先用固定名；否则取目录内 SuperClip_v*.exe（多个时取最后一个）。
REM 安装时统一复制为 %INSTALL_DIR%\SuperClip.exe，固定名不受影响。
if exist "%EXE%" goto have_exe
for %%f in (SuperClip_v*.exe) do set EXE=%%f
:have_exe

REM 检测管理员权限
net session >nul 2>&1
if errorlevel 1 (
    echo 错误：需要管理员权限。请右键本文件「以管理员身份运行」。
    pause
    exit /b 1
)

if not exist "%EXE%" (
    echo 错误：未找到 SuperClip.exe 或 SuperClip_v*.exe。请将本文件放在与 exe 同目录后再运行。
    pause
    exit /b 1
)

echo [1/3] 创建安装目录 %INSTALL_DIR% ...
if not exist "%INSTALL_DIR%" mkdir "%INSTALL_DIR%"

echo [2/3] 复制文件 ...
REM 统一安装为固定名 SuperClip.exe（进程名 / 卸载脚本 / 快捷方式都依赖固定名）
copy /y "%EXE%" "%INSTALL_DIR%\SuperClip.exe"

echo [3/3] 创建快捷方式 ...
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$startMenu = [Environment]::GetFolderPath('StartMenu');" ^
  "$desktop = [Environment]::GetFolderPath('Desktop');" ^
  "$installDir = '%INSTALL_DIR%';" ^
  "$s1 = (New-Object -COM WScript.Shell).CreateShortcut((Join-Path $startMenu 'SuperClip.lnk'));" ^
  "$s1.TargetPath = Join-Path $installDir 'SuperClip.exe'; $s1.WorkingDirectory = $installDir; $s1.Save();" ^
  "$s2 = (New-Object -COM WScript.Shell).CreateShortcut((Join-Path $desktop 'SuperClip.lnk'));" ^
  "$s2.TargetPath = Join-Path $installDir 'SuperClip.exe'; $s2.WorkingDirectory = $installDir; $s2.Save()"

echo.
echo 安装完成。
echo 启动方式：双击桌面「SuperClip」图标，或开始菜单里搜 SuperClip。
echo 卸载方式：以管理员身份运行同目录的 uninstall.bat
echo.
pause
