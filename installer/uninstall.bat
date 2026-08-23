@echo off
REM ============================================================
REM  SuperClip 卸载脚本
REM  - 删除 %ProgramFiles%\SuperClip\
REM  - 删除「开始菜单」与「桌面」快捷方式
REM  - 需管理员权限（删除 Program Files 必需）
REM ============================================================
setlocal

set INSTALL_DIR=%ProgramFiles%\SuperClip

net session >nul 2>&1
if errorlevel 1 (
    echo 错误：需要管理员权限。请右键本文件「以管理员身份运行」。
    pause
    exit /b 1
)

echo [1/3] 关闭可能运行的 SuperClip ...
taskkill /f /im SuperClip.exe >nul 2>&1
timeout /t 1 /nobreak >nul

echo [2/3] 删除文件 ...
if exist "%INSTALL_DIR%\SuperClip.exe" del /f /q "%INSTALL_DIR%\SuperClip.exe"
if exist "%INSTALL_DIR%" rmdir "%INSTALL_DIR%"

echo [3/3] 删除快捷方式 ...
del /f /q "%APPDATA%\Microsoft\Windows\Start Menu\Programs\SuperClip.lnk" 2>nul
del /f /q "%USERPROFILE%\Desktop\SuperClip.lnk" 2>nul

echo.
echo 卸载完成。
echo.
pause
