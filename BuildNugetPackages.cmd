@echo Off
setlocal

set netfx="net45"
set project=".\KuduSync.NET\KuduSync.NET.csproj"
set output=".\artifacts"

dotnet pack /p:Configuration=Release /p:NetFx=false %project% --output "%output%"
dotnet pack /p:Configuration=Release /p:NetFx=true /p:TargetFramework=%netfx% /p:TargetFrameworks=%netfx% %project% --output "%output%"