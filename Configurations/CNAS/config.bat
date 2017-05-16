echo.
echo.
echo Copying configuration files from %~dp0

goto Start
:: FOLDERS
echo Scaffolding directories
mkdir "%~dp0..\..\Source\TPDoc\TPDocWeb\bin\doctools\sqldbdoc"
mkdir "%~dp0..\..\Source\TPDoc\TPDocWeb\bin\doctools\Templates"
mkdir "%~dp0..\..\Source\ScheduledUploader\ScheduledUploader\bin\Debug\doctools\sqldbdoc"
mkdir "%~dp0..\..\Source\ScheduledUploader\ScheduledUploader\bin\Release\doctools\Templates"
mkdir "%~dp0..\..\Source\Documenter\Documenter\bin\Release\doctools\sqldbdoc"
mkdir "%~dp0..\..\Source\Documenter\Documenter\bin\Release\doctools\Templates"
mkdir "%~dp0..\..\Source\Documenter\Documenter\bin\Debug\doctools\sqldbdoc"
mkdir "%~dp0..\..\Source\Documenter\Documenter\bin\Debug\doctools\Templates"

:Start

:: DOCTOOLS
echo Copying doctools
xcopy "%~dp0..\..\doctools" "%~dp0..\..\Source\TPDoc\TPDocWeb\bin\doctools" /E /I /Y
xcopy "%~dp0..\..\doctools" "%~dp0..\..\Source\Documenter\Documenter\bin\Release\doctools" /E /I /Y
xcopy "%~dp0..\..\doctools" "%~dp0..\..\Source\Documenter\Documenter\bin\Debug\doctools" /E /I /Y
xcopy "%~dp0..\..\doctools" "%~dp0..\..\Source\ScheduledUploader\ScheduledUploader\bin\Release\doctools" /E /I /Y
xcopy "%~dp0..\..\doctools" "%~dp0..\..\Source\ScheduledUploader\ScheduledUploader\bin\Debug\doctools" /E /I /Y


:: APP JSON CONFIG
echo Copying App.config.json to "\Source\Documenter\Documenter\bin\Release\App.config.json"
copy "%~dp0\App.config.json" "%~dp0..\..\Source\Documenter\Documenter\bin\Release\App.config.json" /V /Y
echo Copying App.config.json to "\Source\Documenter\Documenter\bin\Debug\App.config.json"
copy "%~dp0\App.config.json" "%~dp0..\..\Source\Documenter\Documenter\bin\Debug\App.config.json" /V /Y

echo Copying App.config.json to "\Source\TPDoc\TPDocWeb\bin\App.config.json"
copy "%~dp0\App.config.json" "%~dp0..\..\Source\TPDoc\TPDocWeb\bin\App.config.json" /V /Y

echo Copying App.config.json to "\Source\ScheduledUploader\ScheduledUploader\bin\Debug\App.config.json"
copy "%~dp0\App.config.json" "%~dp0..\..\Source\ScheduledUploader\ScheduledUploader\bin\Debug\App.config.json" /V /Y
echo Copying App.config.json to "\Source\ScheduledUploader\ScheduledUploader\bin\Release\App.config.json"
copy "%~dp0\App.config.json" "%~dp0..\..\Source\ScheduledUploader\ScheduledUploader\bin\Release\App.config.json" /V /Y


:: APP CONIFG
echo Copying App.config
copy "%~dp0\Documenter.App.config" "%~dp0..\..\Source\Documenter\Documenter\App.config" /V /Y


:: WEB CONFIG
echo Copying Web.config
copy "%~dp0\TPDocWeb.Web.config" "%~dp0..\..\Source\TPDoc\TPDocWeb\Web.config" /V /Y
echo Copying Web.Debug.config
copy "%~dp0\TPDocWeb.Web.Debug.config" "%~dp0..\..\Source\TPDoc\TPDocWeb\Web.Debug.config" /V /Y
echo Copying Web.Release.config
copy "%~dp0\TPDocWeb.Web.Release.config" "%~dp0..\..\Source\TPDoc\TPDocWeb\Web.Release.config" /V /Y


:: LOGO
echo Copying logo.png
copy "%~dp0\logo.png" "%~dp0..\..\Source\TPDoc\TPDocWeb\Content\logo.png" /V /Y


echo Done copying config files!
echo.