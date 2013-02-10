var cmd = __dirname + "\\..\\KuduSync.NET\\bin\\Release\\KuduSync.NET.exe";
var ignoredTestsMap = {};
ignoredTestsMap['Ignore files (file*) should not copy them'] = true;
ignoredTestsMap['Ignore files (bin/file*) should not copy them'] = true;
ignoredTestsMap['Ignore files (bin/**) should not copy them'] = true;
ignoredTestsMap['Ignore files (file1;bin/file3) should not copy them'] = true;
ignoredTestsMap['Ignore files (file1;bin/*) should not copy them'] = true;
ignoredTestsMap["Several files should not be sync'd with whatIf flag set to true"] = true;
ignoredTestsMap["Several files and direcotires should not be sync'd with whatIf flag set to true"] = true;

exports.cmd = cmd;
exports.ignoredTestsMap = ignoredTestsMap;
