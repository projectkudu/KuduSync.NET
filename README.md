KuduSync.NET
============

Tool for syncing files for deployment, will only copy changed files and delete files that doesn't exists in the destination but only if they were part of the previous deployment (manifest file).

This is the .NET version of [KuduSync](https://github.com/projectkudu/KuduSync).

### Usage

```
KuduSync.NET.exe -f [source path] -t [destination path] -n [path to next manifest path]
                 -p [path to current manifest path] -i <paths to ignore delimited by ;>
```

The tool will sync files from the `[source path]` path to the `[destination path]` path using the manifest file in `[path to current manifest path]` to help determine what was added/removed and will write the new manifest file at path `[path to current manifest path]`.
Paths in `<paths to ignore>` will be ignored in the process

### License

[Apache License 2.0](https://github.com/projectkudu/KuduSync.NET/blob/master/LICENSE.txt)


### Questions?

You can use the [forum](http://social.msdn.microsoft.com/Forums/en-US/azuregit/threads), chat on [JabbR](https://jabbr.net/#/rooms/kudu), or open issues in this repository.

This project is under the benevolent umbrella of the [.NET Foundation](http://www.dotnetfoundation.org/).
