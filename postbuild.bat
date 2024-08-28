copy .\bin\Debug\netstandard2.1\SkreenZ.TobogangMod.dll "C:\Program Files (x86)\Steam\steamapps\common\Lethal Company\BepInEx\plugins"
if %errorlevel% neq 0 exit /b %errorlevel%
xcopy .\Resources\* "C:\Program Files (x86)\Steam\steamapps\common\Lethal Company\BepInEx\plugins" /e /Y
if %errorlevel% neq 0 exit /b %errorlevel%