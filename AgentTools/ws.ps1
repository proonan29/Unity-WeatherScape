# Helper: run a Unity CLI command against the WeatherScape editor.
# Usage: .\AgentTools\tp.ps1 <command> [args...]
[Console]::OutputEncoding = [Text.Encoding]::UTF8
$env:Path += ";C:\Users\proon\AppData\Local\Unity\bin"
Set-Location D:\UnityAIProjs\DigitalTwin\WeatherScape
& unity command @args --no-banner 2>&1
