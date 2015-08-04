using System;
using Mono.Options;
using System.Collections.Generic;
using Couchbase.Lite;
using System.IO;
using System.Threading;

namespace cbreplicate
{
    class MainClass
    {
        const string _helpText = @"do the write thing.";

        static bool _isPush;
        static bool _isPull;
        static bool _showHelp;

        static string _path;
        static string _url;

        static Database _db;

        public static void Main(string[] args)
        {
            var timeoutStr = "90000";
            var options = new OptionSet() {
                { "push", "push replication", v => _isPush = v == "push" },
                { "pull", "pull replication", v => _isPull = v == "pull" },
                { "f|file=",  "CBLite file path", v => 
                    _path = v },
                { "u|url=",  "URL to replicate with", v => _url = v },
                { "t|timeout=", "Set the timeout for HTTP requests in milliseconds (default is 90000)", v => timeoutStr = v },
                { "h|help",  "show this message and exit", v => _showHelp = v != null },
                
            };

            List<string> extra;
            try {
                extra = options.Parse(args);
            } catch (OptionException e) {
                Console.Write("cbreplicate: ");
                Console.WriteLine(e.Message);
                Console.WriteLine("Try `cbreplicate --help' for more information.");
                return;
            }

            int timeout;
            if(Int32.TryParse(timeoutStr, out timeout)) {
                Manager.DefaultOptions.RequestTimeout = TimeSpan.FromMilliseconds(timeout);   
            }
            
            var file = new FileInfo(_path);
            var man = new Manager(file.Directory, Manager.DefaultOptions);

            _db = _isPull ? man.GetDatabase(file.Name.Split('.')[0]) : man.GetExistingDatabase(file.Name.Split('.')[0]);

            if (_db == null)
            {
                Console.WriteLine("No CBLite db found at '{0}'", _path);
                return;
            }

            if (_isPush)
            {
                Push();
            }
            else if (_isPull)
            {
                Pull();
            }
            
            Console.ReadKey(true);
        }

        static void Push()
        {
            Console.WriteLine("Starting Push replication with {0}", _url);
            var push = _db.CreatePushReplication(new Uri(_url));
            var mre = new ManualResetEvent(false);
            push.Changed += (sender, e) => {
                Console.WriteLine("{0}: {1}/{2}", push.Status, push.ChangesCount, push.CompletedChangesCount);
                if (push.Status == ReplicationStatus.Stopped)
                {
                    mre.Set();
                }
            };
            push.Start();
            mre.WaitOne();
        }

        static void Pull()
        {
            Console.WriteLine("Starting Pull replication with {0}", _url);
            var mre = new ManualResetEvent(false);
            var pull = _db.CreatePullReplication(new Uri(_url));
            pull.Continuous = true;
            pull.Changed += (sender, e) => {
                Console.WriteLine("{0}: {1}/{2}", pull.Status, pull.ChangesCount, pull.CompletedChangesCount);
                if (pull.Status == ReplicationStatus.Stopped || pull.Status == ReplicationStatus.Idle)
                {
                    mre.Set();
                }
            };
            pull.Start();
            mre.WaitOne();
        }

        static void ShowHelp()
        {
            Console.WriteLine(_helpText);
        }
    }
}
