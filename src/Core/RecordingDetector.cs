using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace StreamClipMarker.Core
{
    public class RecordingEventArgs : EventArgs
    {
        public string Source { get; private set; }
        public string Details { get; private set; }

        public RecordingEventArgs(string source, string details)
        {
            Source = source;
            Details = details;
        }
    }

    public class RecordingDetector : IDisposable
    {
        private FileSystemWatcher _fileWatcher;
        private System.Threading.Timer _processPollTimer;
        private bool _isProcessRecording = false;
        private bool _isRunning = false;
        private DateTime _lastFileDetectionTime = DateTime.MinValue;

        public event EventHandler<RecordingEventArgs> RecordingStarted;
        public event EventHandler<RecordingEventArgs> RecordingStopped;

        public bool Enabled { get; set; }
        public string WatchedFolder { get; set; }

        public RecordingDetector(bool enabled, string watchedFolder)
        {
            Enabled = enabled;
            WatchedFolder = string.IsNullOrEmpty(watchedFolder) ?
                Environment.GetFolderPath(Environment.SpecialFolder.MyVideos) : watchedFolder;
        }

        public void Start()
        {
            if (_isRunning || !Enabled) return;
            _isRunning = true;

            // 1. Setup FileSystemWatcher for instant 0% CPU detection on file creation
            try
            {
                if (Directory.Exists(WatchedFolder))
                {
                    _fileWatcher = new FileSystemWatcher(WatchedFolder)
                    {
                        IncludeSubdirectories = true,
                        NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime | NotifyFilters.Size
                    };

                    _fileWatcher.Created += FileWatcher_Created;
                    _fileWatcher.EnableRaisingEvents = true;
                }
            }
            catch { }

            // 2. Setup Process & Window Title Monitor (runs once every 2.5 seconds = ~0.0% CPU)
            _processPollTimer = new System.Threading.Timer(ProcessPollCallback, null, 1500, 2500);
        }

        public void Stop()
        {
            _isRunning = false;

            if (_fileWatcher != null)
            {
                try
                {
                    _fileWatcher.EnableRaisingEvents = false;
                    _fileWatcher.Created -= FileWatcher_Created;
                    _fileWatcher.Dispose();
                }
                catch { }
                _fileWatcher = null;
            }

            if (_processPollTimer != null)
            {
                try
                {
                    _processPollTimer.Dispose();
                }
                catch { }
                _processPollTimer = null;
            }
        }

        private void FileWatcher_Created(object sender, FileSystemEventArgs e)
        {
            if (!_isRunning || !Enabled) return;

            try
            {
                string ext = Path.GetExtension(e.FullPath).ToLowerInvariant();
                // Common livestream & screen recording video containers
                if (ext == ".mp4" || ext == ".mkv" || ext == ".flv" || ext == ".ts" || ext == ".mov")
                {
                    // Debounce so multiple filesystem events for the same file don't double trigger
                    if ((DateTime.UtcNow - _lastFileDetectionTime).TotalSeconds < 5.0) return;
                    _lastFileDetectionTime = DateTime.UtcNow;

                    string fileName = Path.GetFileName(e.FullPath);
                    OnRecordingStarted(new RecordingEventArgs("File Created", fileName));
                }
            }
            catch { }
        }

        private void ProcessPollCallback(object state)
        {
            if (!_isRunning || !Enabled) return;

            try
            {
                bool activeRecordingFound = false;
                string sourceDetail = "";

                Process[] processes = Process.GetProcesses();
                foreach (Process p in processes)
                {
                    try
                    {
                        string name = p.ProcessName.ToLowerInvariant();

                        // 1. TikTok LIVE Studio detection
                        if (name.Contains("tiktok") || name.Contains("livestudio"))
                        {
                            string title = p.MainWindowTitle;
                            if (!string.IsNullOrEmpty(title) &&
                                (title.IndexOf("live", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                 title.IndexOf("streaming", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                 title.IndexOf("rec", StringComparison.OrdinalIgnoreCase) >= 0))
                            {
                                activeRecordingFound = true;
                                sourceDetail = "TikTok LIVE Studio (" + title + ")";
                                break;
                            }
                        }

                        // 2. OBS Studio recording/streaming detection
                        if (name == "obs64" || name == "obs32" || name == "obs")
                        {
                            string title = p.MainWindowTitle;
                            if (!string.IsNullOrEmpty(title) &&
                                (title.IndexOf("recording", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                 title.IndexOf("streaming", StringComparison.OrdinalIgnoreCase) >= 0))
                            {
                                activeRecordingFound = true;
                                sourceDetail = "OBS Studio (" + title + ")";
                                break;
                            }
                        }

                        // 3. Windows Game Bar Screen Recorder
                        if (name == "bcastdvr" || name == "captureserver")
                        {
                            activeRecordingFound = true;
                            sourceDetail = "Windows Screen Recorder";
                            break;
                        }
                    }
                    catch { }
                }

                if (activeRecordingFound && !_isProcessRecording)
                {
                    _isProcessRecording = true;
                    OnRecordingStarted(new RecordingEventArgs("Process Active", sourceDetail));
                }
                else if (!activeRecordingFound && _isProcessRecording)
                {
                    _isProcessRecording = false;
                    OnRecordingStopped(new RecordingEventArgs("Process Ended", "Recording Stopped"));
                }
            }
            catch { }
        }

        protected virtual void OnRecordingStarted(RecordingEventArgs e)
        {
            EventHandler<RecordingEventArgs> handler = RecordingStarted;
            if (handler != null)
            {
                handler(this, e);
            }
        }

        protected virtual void OnRecordingStopped(RecordingEventArgs e)
        {
            EventHandler<RecordingEventArgs> handler = RecordingStopped;
            if (handler != null)
            {
                handler(this, e);
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
