@echo off
rem Builds ProClicker.exe with the C# compiler that ships with Windows (.NET Framework 4).
rem logo.png is embedded in the exe (sidebar logo) and turned into icon.ico (exe icon).
rem Replace logo.png and delete icon.ico to change the icon.
cd /d "%~dp0"
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
set OPTS=/nologo /target:winexe /optimize+ /codepage:65001 /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll /resource:logo.png,logo.png

if not exist icon.ico (
  "%CSC%" %OPTS% /out:ProClicker.exe ProClicker.cs || goto fail
  start "" /wait "%~dp0ProClicker.exe" --make-icon logo.png icon.ico
)
"%CSC%" %OPTS% /win32icon:icon.ico /out:ProClicker.exe ProClicker.cs || goto fail
echo Built ProClicker.exe
exit /b 0

:fail
echo BUILD FAILED
pause
exit /b 1
