@echo off
setlocal
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
"%CSC%" /nologo /target:winexe /optimize+ /codepage:65001 /out:"%~dp0RpnNumpad.exe" ^
  /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll ^
  "%~dp0RpnNumpad.cs"
if errorlevel 1 (echo Build FAILED & exit /b 1)
echo Built "%~dp0RpnNumpad.exe"
