@echo off
setlocal

if "%1" == "Debug" (
    copy "..\LethalCompanyUnityTemplate-main\AssetBundles\StandaloneWindows\tobogangasset" ".\Resources\TobogangMod"
    copy .\bin\Debug\netstandard2.1\SkreenZ.TobogangMod.dll "C:\Program Files (x86)\Steam\steamapps\common\Lethal Company\BepInEx\plugins"
    if %errorlevel% neq 0 exit /b %errorlevel%
    xcopy .\Resources\* "C:\Program Files (x86)\Steam\steamapps\common\Lethal Company\BepInEx\plugins" /e /Y
    if %errorlevel% neq 0 exit /b %errorlevel%
) else if "%1" == "Release" (
    copy .\bin\Release\netstandard2.1\SkreenZ.TobogangMod.dll .\Thunderstore\BepInEx\plugins
    if %errorlevel% neq 0 exit /b %errorlevel%
    xcopy .\Resources\* .\Thunderstore\BepInEx\plugins /e /Y
    if %errorlevel% neq 0 exit /b %errorlevel%
) else (
    echo Invalid argument. Please use "Debug" or "Release".
    exit 1
)

endlocal