msbuild /p:Configuration=Release
nuget pack -symbols -Prop Configuration=Release -NoPackageAnalysis
