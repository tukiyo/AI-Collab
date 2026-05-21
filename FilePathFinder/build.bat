@echo off
set CSC_PATH=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe

if not exist %CSC_PATH% (
    set CSC_PATH=C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe
)

echo 起動中の Program.exe があれば終了します...
:: /FI でイメージ名を設定し、実行中の場合のみ強制終了します（エラー出力は非表示）
taskkill /IM Program.exe /F >nul 2>&1

echo Windows GUIアプリとしてビルドを開始します...
%CSC_PATH% /nologo /target:winexe Program.cs

if %errorlevel% equ 0 (
    echo.
    echo ビルド成功
) else (
    echo.
    echo ビルドに失敗しました。ソースコードを確認してください。
)

start Program.exe