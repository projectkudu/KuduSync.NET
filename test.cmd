@echo off

echo Get functional tests file.
call cscript getFile.vbs
if %errorlevel%==1 goto error

echo Run tests

pushd test

call npm install
if %errorlevel%==1 goto error_p

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
