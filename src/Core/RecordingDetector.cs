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

        private string _activeRecordingFilePath = null;
        private int _fileUnlockedStreak = 0;

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
            _activeRecordingFilePath = null;
            _fileUnlockedStreak = 0;

            // 1. Setup FileSystemWatcher for instant file creation detection
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

            // 2. Setup Monitor timer: checks file locks and process states every 1.5 seconds
            _processPollTimer = new System.Threading.Timer(MonitorPollCallback, null, 1500, 1500);
        }

        public void Stop()
        {
            _isRunning = false;
            _activeRecordingFilePath = null;

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
                // Common livestream & screen recording video formats
                if (ext == ".mp4" || ext == ".mkv" || ext == ".flv" || ext == ".ts" || ext == ".mov")
                {
                    // Debounce so quick multiple events don't re-trigger
                    if ((DateTime.UtcNow - _lastFileDetectionTime).TotalSeconds < 5.0) return;
                    _lastFileDetectionTime = DateTime.UtcNow;

                    _activeRecordingFilePath = e.FullPath;
                    _fileUnlockedStreak = 0;

                    string fileName = Path.GetFileName(e.FullPath);
                    OnRecordingStarted(new RecordingEventArgs("File Created", fileName));
                }
            }
            catch { }
        }

        private void MonitorPollCallback(object state)
        {
            if (!_isRunning || !Enabled) return;

            try
            {
                // A. Check active recording file lock status
                if (!string.IsNullOrEmpty(_activeRecordingFilePath))
                {
                    CheckActiveRecordingFileStatus();
                }

                // B. Check running processes & window titles
                CheckProcessesStatus();
            }
            catch { }
        }

        private void CheckActiveRecordingFileStatus()
        {
            try
            {
                if (!File.Exists(_activeRecordingFilePath))
                {
                    // File was completed and moved or renamed
                    string fileName = Path.GetFileName(_activeRecordingFilePath);
                    _activeRecordingFilePath = null;
                    _fileUnlockedStreak = 0;
                    OnRecordingStopped(new RecordingEventArgs("File Finalized", fileName));
                    return;
                }

                bool locked = IsFileLocked(_activeRecordingFilePath);
                if (locked)
                {
                    // Recording software is still holding the file handle open and writing frames
                    _fileUnlockedStreak = 0;
                }
                else
                {
                    // File is unlocked (recording application closed the file handle)
                    _fileUnlockedStreak++;
                    // Require 2 consecutive unlocked ticks (~3 seconds) to ensure it wasn't a momentary buffer flush
                    if (_fileUnlockedStreak >= 2)
                    {
                        string fileName = Path.GetFileName(_activeRecordingFilePath);
                        _activeRecordingFilePath = null;
                        _fileUnlockedStreak = 0;
                        OnRecordingStopped(new RecordingEventArgs("Recording File Closed", fileName));
                    }
                }
            }
            catch
            {
                _activeRecordingFilePath = null;
            }
        }

        private bool IsFileLocked(string filePath)
        {
            FileStream stream = null;
            try
            {
                // Try opening with exclusive access. If a recorder is writing, this throws an IOException (Sharing Violation)
                FileInfo file = new FileInfo(filePath);
                stream = file.Open(FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException)
            {
                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                if (stream != null)
                {
                    stream.Close();
                    stream.Dispose();
                }
            }
            return false;
        }

        private void CheckProcessesStatus()
        {
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
                                sourceDetail = "TikTok LIVE Studio";
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
                                sourceDetail = "OBS Studio";
                                break;
                            }
                        }

                        // 3. Windows Game Bar / Snipping Tool Screen Recorder
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
                    OnRecordingStopped(new RecordingEventArgs("Process Ended", sourceDetail));
                }
            }
            catch { }
        }

        public void NotifySessionEndedManually()
        {
            _activeRecordingFilePath = null;
            _fileUnlockedStreak = 0;
            _isProcessRecording = false;
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
