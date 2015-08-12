using System;
using Mono.Options;
using System.Collections.Generic;
using Couchbase.Lite;
using System.IO;
using System.Threading;
using System.Text;

namespace cbreplicate
{
	public enum Exit
	{
        UnhandledException = -1000,
        CannotOpenDatabase = -6,
        PushReplicationError = -5,
        PullReplicationError = -4,
        InvalidOptions = -3,
        PathIsNullOrEmpty = -2,
        PathDoesNotExist = -1,
        Success = 0
    }
}
