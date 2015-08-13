using System;
using Mono.Options;
using System.Collections.Generic;
using Couchbase.Lite;
using System.IO;
using System.Threading;
using System.Text;
using System.Diagnostics;

namespace cbreplicate
{
    class MainClass
    {
        static string _helpText = String.Empty;

        static bool _isPush;
        static bool _isPull;
        static bool _showHelp;

        static string _path;
        static string _url;

        static Database _db;

        static readonly ConsoleColor _defaultColor = Console.ForegroundColor;

        static ManualResetEvent _mre;

        static bool _done;

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
                if (extra.Count == 1)
                {
                    // Should be just a file path. Will validate below.
                    _path = extra[0];
                }
            } catch (OptionException e) {
                Console.Write("cbreplicate: ");
                OutputUsingColor(
                    color: ConsoleColor.Red, 
                    format: e.Message
                );
                Console.WriteLine();
                Console.WriteLine("Try `cbreplicate --help' for more information.");
                Environment.ExitCode = (int)Exit.InvalidOptions;
                return;
            }

            if (_showHelp || args.Length == 0) {
                var writer = new StringWriter(new StringBuilder("usage: cb-replicate [options] [file path]" + Environment.NewLine + Environment.NewLine));
                options.WriteOptionDescriptions(writer);

                _helpText = writer.ToString();

                writer.Close();

                ShowHelp();
                return;
            }

            int timeout;
            if(Int32.TryParse(timeoutStr, out timeout)) {
                Manager.DefaultOptions.RequestTimeout = TimeSpan.FromMilliseconds(timeout);   
            }

            if (String.IsNullOrWhiteSpace(_path)) {
                OutputUsingColor(
                    color: ConsoleColor.Red, 
                    format: "The path to the database was empty text or all whitespace.{0}",
                    args: Environment.NewLine
                );
                ShowHelp();
                Environment.ExitCode = (int)Exit.PathIsNullOrEmpty;
                return;
            }

            FileInfo file;
            if (!File.Exists(_path)) {
                // See if they gave us a relative path.
                _path = Path.Combine(Environment.CurrentDirectory, _path);
                if (!File.Exists(_path) && _isPush) {
                    OutputUsingColor(
                        color: ConsoleColor.Red, 
                        format: "The path {0} is not valid. A valid CBLite database is required for push replication",
                        args: _path
                    );
                    Environment.ExitCode = (int)Exit.PathDoesNotExist;
                    return;
                }
            }

            file = new FileInfo(_path);

            var man = new Manager(file.Directory, Manager.DefaultOptions);

            try
            {
                _db = _isPull ? man.GetDatabase(file.Name.Split('.')[0]) : man.GetExistingDatabase(file.Name.Split('.')[0]);
            }
            catch (Exception ex)
            {
                OutputUsingColor(
                    color: ConsoleColor.Red, 
                    format: "Error opening the database: {0}{1}",
                    args: new[] { ex.Message, Environment.NewLine }
                );
                Environment.ExitCode = (int)Exit.CannotOpenDatabase;
                return;
            }

            if (_db == null)
            {
                OutputUsingColor(
                    color: ConsoleColor.Red, 
                    format: "No CBLite db found at '{0}'. Push replication requires and existing CBLite database.",
                    args: _path
                );
                Environment.ExitCode = (int)Exit.PathDoesNotExist;
                return;
            }

            if (_isPush)
            {
                try
                {
                    Push();
                }
                catch (Exception ex)
                {
                    OutputUsingColor(
                        color: ConsoleColor.Red, 
                        format: "Unhandled exception during push replication: {0}",
                        args: ex.Message
                    );
                    Environment.ExitCode = (int)Exit.UnhandledException;
                    return;
                }
            }
            else if (_isPull)
            {
                try
                {
                    Pull();
                }
                catch (Exception ex)
                {
                    OutputUsingColor(
                        color: ConsoleColor.Red, 
                        format: "Unhandled exception during pull replication: {0}",
                        args: ex.Message
                    );
                    Environment.ExitCode = (int)Exit.UnhandledException;
                    return;
                }
            }

            bool doneWaiting;

            Console.CancelKeyPress += (sender, e) => {
                if (e.Cancel) {
                    _done = _mre.Set();
                }
            };

            do {
                doneWaiting = _mre.WaitOne(100);
            } while(!_done || !doneWaiting);
        }

        static void Push()
        {
            OutputUsingColor(
                color: ConsoleColor.Yellow, 
                format: "Starting Push replication to endpoint at {0}", 
                args: _url
            );
            var push = _db.CreatePushReplication(new Uri(_url));
            _mre = new ManualResetEvent(false);
            push.Changed += (sender, e) => {
                if (_done) {
                    return; // already exiting...
                }
                Console.WriteLine("\t{0}: {1} of {2}", push.Status, push.CompletedChangesCount, push.ChangesCount);
                if (push.LastError != null) {
                    Environment.ExitCode = (int)Exit.PullReplicationError;
                    var message = ToReplicationErrorMessage(push);
                    _done = _mre.Set(); // Continue to exit.
                    OutputUsingColor(
                        color: ConsoleColor.Red, 
                        format: "\tError during push replication: {0}",
                        args: message
                    );
                }
                if (push.Status == ReplicationStatus.Stopped || push.Status == ReplicationStatus.Idle)
                {
                    _done = _mre.Set();
                }
            };
            push.Start();
        }

        static void Pull()
        {
            OutputUsingColor(
                color: ConsoleColor.Yellow, 
                format: "Starting Pull replication with {0}", 
                args: _url
            );
            _mre = new ManualResetEvent(false);
            var pull = _db.CreatePullReplication(new Uri(_url));
            pull.Changed += (sender, e) => {

                if (_done) {
                    return; // already exiting...
                }
                Console.WriteLine("\t{0}: {1} of {2}", pull.Status, pull.CompletedChangesCount, pull.ChangesCount);


                if (pull.LastError != null) {
                    Environment.ExitCode = (int)Exit.PullReplicationError;
                    var message = ToReplicationErrorMessage(pull);
                    OutputUsingColor(
                        color: ConsoleColor.Red, 
                        format: "\tError during pull replication: {0}",
                        args: message
                    );
                    _done = _mre.Set(); // Continue to exit.
                }
                if (pull.Status == ReplicationStatus.Stopped || pull.Status == ReplicationStatus.Idle)
                {
                    _done = _mre.Set();
                }
            };
            pull.Start();
        }

        static string ToReplicationErrorMessage(Replication replication)
        {
            string message;
            if (replication.LastError is HttpResponseException)
            {
                var err = (HttpResponseException)replication.LastError;
                message = String.Format("HTTP error response: {0}/{1}", (int)err.StatusCode, err.StatusCode);
            } else if (replication.LastError != null && replication.LastError.InnerException != null) {
                message = replication.LastError.InnerException.Message;
            } else {
                message = replication.LastError.Message;
            }
            return message;
        }

        static void OutputUsingColor(ConsoleColor color, string format, params string[] args)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(format, args);
            Console.ForegroundColor = _defaultColor;
        }

        static void ShowHelp()
        {
            Console.WriteLine(_helpText);
        }
    }
}
