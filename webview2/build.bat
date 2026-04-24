@echo off
setlocal

echo --- WebView2 ビルドスクリプト (終了シグナル対応版) ---

echo 0-1. 実行中の Program.exe を確認...
tasklist /FI "IMAGENAME eq Program.exe" 2>NUL | find /I /N "Program.exe">NUL
if "%ERRORLEVEL%"=="0" (
    echo Program.exe が実行中のため、終了シグナルを送信します...
    echo. > exit.signal
    
    :WaitLoop
    echo 終了を待機中...
    timeout /t 1 /nobreak > nul
    tasklist /FI "IMAGENAME eq Program.exe" 2>NUL | find /I /N "Program.exe">NUL
    if "%ERRORLEVEL%"=="0" goto :WaitLoop
    
    del exit.signal
    echo 終了を確認しました。
)

echo.
echo 0-2. 必要なDLLの存在確認...
if exist "Microsoft.Web.WebView2.Core.dll" (
    if exist "Microsoft.Web.WebView2.WinForms.dll" (
        if exist "WebView2Loader.dll" (
            echo DLLが揃っています。展開処理をスキップします。
            goto :CompileStep
        )
    )
)

echo.
echo 1. nuget.zip の展開...
if not exist "nuget.zip" (
    echo [エラー] nuget.zip が見つかりません。
    pause
    exit /b 1
)

set "EXTRACT_DIR=nuget_temp"
powershell -command "Expand-Archive -Path 'nuget.zip' -DestinationPath '%EXTRACT_DIR%' -Force"

echo.
echo 2. WebView2パッケージフォルダの検索...
set "PACKAGE_DIR="
for /f "delims=" %%D in ('dir /ad /s /b "%EXTRACT_DIR%\Microsoft.Web.WebView2.*" 2^>nul') do (
    set "PACKAGE_DIR=%%D"
    goto :FoundDir
)

:FoundDir
if "%PACKAGE_DIR%"=="" (
    echo [エラー] パッケージが見つかりません。
    rmdir /s /q "%EXTRACT_DIR%" 2>nul
    pause
    exit /b 1
)
echo 見つかりました: %PACKAGE_DIR%

echo.
echo 3. マネージドDLLのコピー...
copy /Y "%PACKAGE_DIR%\lib\net462\Microsoft.Web.WebView2.Core.dll" . >nul
copy /Y "%PACKAGE_DIR%\lib\net462\Microsoft.Web.WebView2.WinForms.dll" . >nul

echo.
echo 4. アーキテクチャ判定とネイティブDLLのコピー...
set "ARCH=win-x86"
if /i "%PROCESSOR_ARCHITECTURE%"=="AMD64" set "ARCH=win-x64"
if /i "%PROCESSOR_ARCHITEW6432%"=="AMD64" set "ARCH=win-x64"
if /i "%PROCESSOR_ARCHITECTURE%"=="ARM64" set "ARCH=win-arm64"

echo 判定されたアーキテクチャ: %ARCH%
copy /Y "%PACKAGE_DIR%\runtimes\%ARCH%\native\WebView2Loader.dll" . >nul

:CompileStep
echo.
echo 5. csc.exe によるコンパイル...
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:winexe /reference:Microsoft.Web.WebView2.Core.dll /reference:Microsoft.Web.WebView2.WinForms.dll Program.cs

set COMPILE_ERRORLEVEL=%ERRORLEVEL%

echo.
echo 6. 後片付け (一時展開フォルダの削除)...
if exist "nuget_temp" rmdir /s /q "nuget_temp" 2>nul

if %COMPILE_ERRORLEVEL% equ 0 (
    echo.
    echo [成功] ビルド完了。
) else (
    echo.
    echo [エラー] コンパイル失敗。
)

Program.exe