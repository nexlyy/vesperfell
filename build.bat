@echo off
rem Builds Vesperfell with the C# compiler that ships with Windows (.NET Framework 4.x).
rem No SDK, engine or package manager is required.
setlocal
cd /d "%~dp0"
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" (
  echo Could not find csc.exe at %CSC%
  exit /b 1
)
if not exist build mkdir build
set ICON=
if exist res\vesperfell.ico set ICON=/win32icon:res\vesperfell.ico
"%CSC%" /nologo /target:winexe /platform:x64 /optimize+ /unsafe /codepage:65001 /warn:3 /nowarn:0162,0168,0219,0414,0649,0169 ^
  /out:build\Vesperfell.exe %ICON% ^
  /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll ^
  /recurse:src\*.cs
if errorlevel 1 (
  echo BUILD FAILED
  exit /b 1
)
echo Build OK: build\Vesperfell.exe
exit /b 0
