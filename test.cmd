@echo off

echo Get functional tests file.
call cscript getFile.vbs
if %errorlevel%==1 goto error

echo Run tests

pushd test

cal npm install
if %errorlevel%==1 goto error

call npm test
if %errorlevel%==1 goto error
popd

goto end

:error
echo ============
echo Tests failed
echo ============
exit /b 1

:end
echo ============
echo Tests passed
echo ============
