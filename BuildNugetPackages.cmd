@echo Off
setlocal

set netfx="net45"
set corefx="netcoreapp3.1"
set project=".\KuduSync.NET\KuduSync.NET.csproj"
set output=".\artifacts"

dotnet pack /p:Configuration=Release /p:TargetFramework=%corefx% /p:TargetFrameworks=%corefx% %project% --output "%output%"
dotnet pack /p:Configuration=Release /p:TargetFramework=%netfx% /p:TargetFrameworks=%netfx% %project% --output "%output%"