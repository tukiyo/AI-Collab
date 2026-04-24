@echo off
setlocal

echo --- WebView2 ビルドスクリプト (ZIP対応版) ---

echo 1. nuget.zip の展開...
if not exist "nuget.zip" (
    echo [エラー] nuget.zip が見つかりません。
    pause
    exit /b 1
)

rem 一時フォルダ（nuget_temp）を作成してそこに解凍します
set "EXTRACT_DIR=nuget_temp"
powershell -command "Expand-Archive -Path 'nuget.zip' -DestinationPath '%EXTRACT_DIR%' -Force"

echo.
echo 2. WebView2パッケージフォルダの検索...
set "PACKAGE_DIR="
rem ZIPの中身の階層が変わっても見つけられるように、サブフォルダを含めて検索します
for /f "delims=" %%D in ('dir /ad /s /b "%EXTRACT_DIR%\Microsoft.Web.WebView2.*" 2^>nul') do (
    set "PACKAGE_DIR=%%D"
    goto :FoundDir
)

:FoundDir
if "%PACKAGE_DIR%"=="" (
    echo [エラー] 展開したファイル内に Microsoft.Web.WebView2 のパッケージが見つかりません。
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
echo 4. 実行環境のアーキテクチャ判定とネイティブDLLのコピー...
set "ARCH=win-x86"
rem 64bit OSかどうかの判定
if /i "%PROCESSOR_ARCHITECTURE%"=="AMD64" set "ARCH=win-x64"
if /i "%PROCESSOR_ARCHITEW6432%"=="AMD64" set "ARCH=win-x64"
rem ARM64 OSかどうかの判定
if /i "%PROCESSOR_ARCHITECTURE%"=="ARM64" set "ARCH=win-arm64"

echo 判定されたアーキテクチャ: %ARCH%
copy /Y "%PACKAGE_DIR%\runtimes\%ARCH%\native\WebView2Loader.dll" . >nul

echo.
echo 5. csc.exe によるコンパイル...
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:winexe /reference:Microsoft.Web.WebView2.Core.dll /reference:Microsoft.Web.WebView2.WinForms.dll Program.cs

rem コンパイルの成否を記録
set COMPILE_ERRORLEVEL=%ERRORLEVEL%

echo.
echo 6. 後片付け (一時展開フォルダの削除)...
rmdir /s /q "%EXTRACT_DIR%" 2>nul

if %COMPILE_ERRORLEVEL% equ 0 (
    echo.
    echo [成功] ビルドが完了し、後片付けも終わりました。Program.exe を実行してください。
) else (
    echo.
    echo [エラー] コンパイルに失敗しました。
)
