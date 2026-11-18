@echo off
rem Usage: build.cmd [output.exe]   (default: bin\LOQKeyboardRGB.exe)
setlocal
cd /d "%~dp0"
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
  echo Could not find the .NET Framework 4 C# compiler.
  exit /b 1
)
set "OUT=%~1"
if "%OUT%"=="" set "OUT=bin\LOQKeyboardRGB.exe"
if not exist bin mkdir bin
"%CSC%" /nologo /target:winexe /optimize+ /platform:anycpu "/out:%OUT%" ^
  /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Management.dll ^
  /resource:assets\avatar.png,LoqRgb.avatar.png ^
  src\*.cs
if errorlevel 1 exit /b 1
echo Built %OUT%
