@echo off
echo ==================================================
echo      Kodakku Assist Repo Updater for Windows
echo ==================================================


echo.
echo Generating OnlineRepo.json...
dotnet run --project ScriptParser/ScriptParser.csproj

echo.
echo ==================================================
echo      Update complete!
echo ==================================================
pause
