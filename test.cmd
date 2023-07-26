@echo off

dotnet build /p:Configuration=Release

echo Get functional tests file.
curl -k -o test/functionalTests.js https://raw.githubusercontent.com/projectkudu/KuduSync/master/test/functionalTests.js

echo Run tests

pushd test

call npm install
if %errorlevel%==1 goto error_p

echo Run net45 tests
set KuduNetPath="\\..\\KuduSync.NET\\bin\\Release\\net45\\KuduSync.NET.exe"
call npm test
if %errorlevel%==1 goto error_p

echo Run netcoreapp3.1 tests
set KuduNetPath="\\..\\KuduSync.NET\\bin\\Release\\netcoreapp3.1\\KuduSync.NET.exe"
call npm test
if %errorlevel%==1 goto error_p

popd

goto end

:error_p
popd

:error
echo ============
echo Tests failed
echo ============
exit /b 1

:end
echo ============
echo Tests passed
echo ============
