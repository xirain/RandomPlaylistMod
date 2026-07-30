
@echo off
cls

set "PROJECT_DIR=D:\code\aidemo\bsmodrandom\RandomPlaylistMod"
set "BUILD_DIR=%PROJECT_DIR%\bin\Release"
set "TARGET_DIR_1=F:\paly\BSManager\BSInstances\1.40.8\Plugins"
set "TARGET_DIR_2=F:\paly\BSManager\BSInstances\1.44.0\Plugins"

echo ================================
echo RandomPlaylistMod Deploy Script
echo ================================
echo.

echo Step 1: Cleaning build cache...
cd /d %PROJECT_DIR%
dotnet clean --configuration Release /nologo

echo.
echo Step 2: Building project...
dotnet build --configuration Release /nologo
if %errorlevel% neq 0 (
    echo ERROR: Build failed!
    exit /b 1
)

echo.
echo Step 3: Deploying to game instances...

for %%D in ("%TARGET_DIR_1%", "%TARGET_DIR_2%") do (
    echo.
    echo Deploying to: %%~D
    echo   Deleting old files...
    del "%%~D\RandomPlaylistMod.dll" /f /q 2>nul
    del "%%~D\RandomPlaylistMod.pdb" /f /q 2>nul
    
    echo   Copying new files...
    copy /y "%BUILD_DIR%\RandomPlaylistMod.dll" "%%~D\"
    copy /y "%BUILD_DIR%\RandomPlaylistMod.pdb" "%%~D\"
    
    if %errorlevel% equ 0 (
        echo   SUCCESS!
    ) else (
        echo   ERROR: Copy failed!
    )
)

echo.
echo ================================
echo DEPLOYMENT COMPLETE!
echo ================================
echo.
echo Files deployed:
echo   - RandomPlaylistMod.dll
echo   - RandomPlaylistMod.pdb
echo.
echo Checking required dependencies...
echo.
@REM if not exist "r\SiraUtil.dll" (
@REM     echo   WARNING: SiraUtil.dll not found in 1.40.8!
@REM )
if not exist "F:\paly\BSManager\BSInstances\1.40.8\Plugins\BSML.dll" (
    echo   WARNING: BSML.dll not found in 1.40.8!
)
if not exist "F:\paly\BSManager\BSInstances\1.40.8\Plugins\SongCore.dll" (
    echo   WARNING: SongCore.dll not found in 1.40.8!
)
if not exist "F:\paly\BSManager\BSInstances\1.40.8\Plugins\BS_Utils.dll" (
    echo   WARNING: BS_Utils.dll not found in 1.40.8!
)
echo.
echo Please restart Beat Saber to test.

