@echo off
set DEFHOMEDRIVE=d:
set DEFHOMEDIR=%DEFHOMEDRIVE%%HOMEPATH%
set HOMEDIR=
set HOMEDRIVE=%CD:~0,2%

set RELEASEDIR=d:\Users\jbb\release
set ZIP="c:\Program Files\7-zip\7z.exe"
echo Default homedir: %DEFHOMEDIR%

set /p HOMEDIR= "Enter Home directory, or <CR> for default: "

if "%HOMEDIR%" == "" (
set HOMEDIR=%DEFHOMEDIR%
)
echo %HOMEDIR%

SET _test=%HOMEDIR:~1,1%
if "%_test%" == ":" (
set HOMEDRIVE=%HOMEDIR:~0,2%
)

cd Source
type SmartParts.version
set /p VERSION= "Enter version: "


mkdir %HOMEDIR%\install\GameData\SmartParts
mkdir %HOMEDIR%\install\GameData\SmartParts\Parts
mkdir %HOMEDIR%\install\GameData\SmartParts\Plugins
mkdir %HOMEDIR%\install\GameData\SmartParts\Sounds


del /Q %HOMEDIR%\install\GameData\SmartParts
del /Q %HOMEDIR%\install\GameData\SmartParts\Parts
del /Q %HOMEDIR%\install\GameData\SmartParts\Plugins
del /Q %HOMEDIR%\install\GameData\SmartParts\Sounds


copy /Y "%~dp0..\bin\Release\SmartParts.dll" "%HOMEDIR%\install\GameData\SmartParts\Plugins"

copy /Y "%~dp0SmartParts.version" "%HOMEDIR%\install\GameData\SmartParts"

xcopy /Y /S "%~dp0..\GameData\SmartParts\Parts" "%HOMEDIR%\install\GameData\SmartParts\Parts"


del %HOMEDIR%\install\GameData\SmartParts\Parts\Fuel-Breakers\*.tga
del %HOMEDIR%\install\GameData\SmartParts\Parts\Fuel-Controller\*.tga
del %HOMEDIR%\install\GameData\SmartParts\Parts\Smart-Controller\*.tga
del %HOMEDIR%\install\GameData\SmartParts\Parts\Valve\*.tga


copy /Y "%~dp0..\License.txt" "%HOMEDIR%\installv\GameData\SmartParts"
copy /Y "%~dp0..\README.md" "%HOMEDIR%\install\GameData\SmartParts"

copy /Y MiniAVC.dll  "%HOMEDIR%\install\GameData\SmartParts"

%HOMEDRIVE%
cd %HOMEDIR%\install

set FILE="%RELEASEDIR%\SmartParts-%VERSION%.zip"
IF EXIST %FILE% del /F %FILE%
%ZIP% a -tzip %FILE% Gamedata\SmartParts
