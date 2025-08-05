@echo off
echo ==================================================
echo      Kodakku Assist Repo Updater for Windows
echo ==================================================


echo.
echo [2/5] Generating OnlineRepo.json...
dotnet run --project ScriptParser/ScriptParser.csproj

echo.
echo ==================================================
echo      Update complete!
echo ==================================================
pause