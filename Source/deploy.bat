cd Source
mkdir R:\KSP_1.1.3_dev\GameData\SmartParts
mkdir R:\KSP_1.1.3_dev\GameData\SmartParts\Parts
mkdir R:\KSP_1.1.3_dev\GameData\SmartParts\Plugins
mkdir R:\KSP_1.1.3_dev\GameData\SmartParts\Sounds


copy /Y "%~dp0..\bin\Debug\SmartParts.dll" "R:\KSP_1.1.3_dev\GameData\SmartParts\Plugins"

copy /Y "%~dp0SmartParts.version" "R:\KSP_1.1.3_dev\GameData\SmartParts"


xcopy /Y /S "%~dp0..\GameData\SmartParts\Parts" "R:\KSP_1.1.3_dev\GameData\SmartParts\Parts"
xcopy /Y /S "%~dp0..\GameData\SmartParts\Sounds" "R:\KSP_1.1.3_dev\GameData\SmartParts\Sounds"

copy /Y "%~dp0..\License.txt" "R:\KSP_1.1.2_dev\GameData\SmartParts"
copy /Y "%~dp0..\README.md" "R:\KSP_1.1.2_dev\GameData\SmartParts"

del R:\KSP_1.1.3_dev\GameData\SmartParts\Parts\Fuel-Breakers\*.tga
del R:\KSP_1.1.3_dev\GameData\SmartParts\Parts\Fuel-Controller\*.tga
del R:\KSP_1.1.3_dev\GameData\SmartParts\Parts\Smart-Controller\*.tga
del R:\KSP_1.1.3_dev\GameData\SmartParts\Parts\Valve\*.tga
