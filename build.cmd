@echo off
setlocal
cd /d "%~dp0"
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
  echo Could not find the .NET Framework 4 C# compiler.
  exit /b 1
)
if not exist bin mkdir bin
"%CSC%" /nologo /target:winexe /optimize+ /platform:anycpu /out:bin\LOQKeyboardRGB.exe ^
  /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll ^
  /resource:assets\avatar.png,LoqRgb.avatar.png ^
  src\*.cs
if errorlevel 1 exit /b 1
echo Built bin\LOQKeyboardRGB.exe
