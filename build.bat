@echo off
echo ========================================
echo   Building Universal Live Server
echo ========================================
echo.

set CSC="C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
set SRC=src\liveServer.cs
set OUT=liveServer.exe
set ICO=src\app.ico
set REF=System.Windows.Forms.dll,System.Drawing.dll,System.Core.dll

echo Compiling...
%CSC% -nologo -out:%OUT% -target:winexe -win32icon:%ICO% -reference:%REF% %SRC%

if %ERRORLEVEL% EQU 0 (
    echo.
    echo   Build successful!
) else (
    echo.
    echo   Build failed!
)
echo.
pause
