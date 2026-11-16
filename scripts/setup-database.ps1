# Reset HotelSaaS_DB to schema + seed state
$ErrorActionPreference = "Stop"
sqlcmd -S localhost -E -i "database\01_schema.sql" -b
sqlcmd -S localhost -E -i "database\02_seed.sql" -b
Write-Host "`nDatabase ready." -ForegroundColor Green
