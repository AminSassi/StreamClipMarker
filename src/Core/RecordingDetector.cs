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
        public string FilePath { get; private set; }

        public RecordingEventArgs(string source, string details, string filePath = "")
        {
            Source = source;
            Details = details;
            FilePath = filePath ?? "";
        }
    }

    public class RecordingDetector : IDisposable
    {
        private FileSystemWatcher _fileWatcher;
        private System.Threading.Timer _monitorTimer;
        private bool _isProcessRecording = false;
        private bool _isRunning = false;

        private string _activeRecordingFilePath = null;
        private long _lastRecordedFileSize = -1;
        private int _fileUnlockedStreak = 0;
        private DateTime _lastDetectionTime = DateTime.MinValue;
        private string _lastDetectedFile = "";

        public RecordingStateMachine StateMachine { get; private set; }

        public event EventHandler<RecordingEventArgs> RecordingStarted;
        public event EventHandler<RecordingEventArgs> RecordingStopped;
        public event EventHandler<string> DiagnosticLogged;

        public bool Enabled { get; set; }
        public string WatchedFolder { get; set; }

        public string ActiveRecordingFileName
        {
            get
            {
                if (!string.IsNullOrEmpty(_activeRecordingFilePath))
                {
                    try { return Path.GetFileName(_activeRecordingFilePath); } catch { }
                }
                return "";
            }
        }

        public RecordingDetector(bool enabled, string watchedFolder)
        {
            Enabled = enabled;
            WatchedFolder = string.IsNullOrEmpty(watchedFolder) ?
                Environment.GetFolderPath(Environment.SpecialFolder.MyVideos) : watchedFolder;

            StateMachine = new RecordingStateMachine();
            StateMachine.StateChanged += (s, e) =>
            {
                LogDiagnostic(string.Format("State transition: {0} -> {1} (Reason: {2})", e.OldState, e.NewState, e.Reason));
            };
        }

        public void LogDiagnostic(string message)
        {
            string log = string.Format("[{0:HH:mm:ss.fff}] {1}", DateTime.Now, message);
            EventHandler<string> handler = DiagnosticLogged;
            if (handler != null)
            {
                handler(this, log);
            }
        }

        public void Start()
        {
            if (_isRunning || !Enabled) return;
            _isRunning = true;
            _activeRecordingFilePath = null;
            _fileUnlockedStreak = 0;
            _lastRecordedFileSize = -1;

            StateMachine.ForceState(RecordingState.WaitingForRecording, "Detector Started");
            LogDiagnostic("Detector started. Watching: " + WatchedFolder);

            // 1. Setup FileSystemWatcher
            try
            {
                if (Directory.Exists(WatchedFolder))
                {
                    _fileWatcher = new FileSystemWatcher(WatchedFolder)
                    {
                        IncludeSubdirectories = true,
                        NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime | NotifyFilters.Size | NotifyFilters.LastWrite
                    };

                    _fileWatcher.Created += FileWatcher_Created;
                    _fileWatcher.Changed += FileWatcher_Changed;
                    _fileWatcher.Renamed += FileWatcher_Renamed;
                    _fileWatcher.EnableRaisingEvents = true;
                }
                else
                {
                    LogDiagnostic("Watched folder does not exist: " + WatchedFolder);
                }
            }
            catch (Exception ex)
            {
                LogDiagnostic("Watcher error: " + ex.Message);
            }

            // 2. Setup Monitor Timer (runs every 1.5 seconds)
            _monitorTimer = new System.Threading.Timer(MonitorTick, null, 1500, 1500);
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
                    _fileWatcher.Changed -= FileWatcher_Changed;
                    _fileWatcher.Renamed -= FileWatcher_Renamed;
                    _fileWatcher.Dispose();
                }
                catch { }
                _fileWatcher = null;
            }

            if (_monitorTimer != null)
            {
                try
                {
                    _monitorTimer.Dispose();
                }
                catch { }
                _monitorTimer = null;
            }

            StateMachine.ForceState(RecordingState.Idle, "Detector Stopped");
            LogDiagnostic("Detector stopped.");
        }

        private bool IsVideoExtension(string ext)
        {
            if (string.IsNullOrEmpty(ext)) return false;
            string lower = ext.ToLowerInvariant();
            return lower == ".mp4" || lower == ".mkv" || lower == ".flv" || lower == ".ts" || lower == ".mov";
        }

        private bool IsTemporaryExtension(string ext)
        {
            if (string.IsNullOrEmpty(ext)) return false;
            string lower = ext.ToLowerInvariant();
            return lower == ".part" || lower == ".tmp" || lower == ".crdownload" || lower == ".partial";
        }

        private void HandleFileDetection(string fullPath, string eventName)
        {
            if (!_isRunning || !Enabled) return;

            try
            {
                string fileName = Path.GetFileName(fullPath);
                string ext = Path.GetExtension(fullPath);

                // Ignore non-video and temporary extensions until renamed
                if (!IsVideoExtension(ext)) return;

                // Debounce duplicate events for the same file or rapid multiple events within 3.0 seconds
                DateTime now = DateTime.UtcNow;
                if (_lastDetectedFile == fullPath && (now - _lastDetectionTime).TotalSeconds < 3.0)
                {
                    LogDiagnostic(string.Format("Duplicate {0} event ignored for: {1}", eventName, fileName));
                    return;
                }

                _lastDetectionTime = now;
                _lastDetectedFile = fullPath;

                LogDiagnostic(string.Format("FileSystem {0}: {1}", eventName, fileName));

                // If already actively recording the exact same file, do not start another session
                if (StateMachine.CurrentState == RecordingState.Recording && _activeRecordingFilePath == fullPath)
                {
                    return;
                }

                // If currently finalizing, wait a moment or update active file
                if (StateMachine.CurrentState == RecordingState.Finalizing)
                {
                    return;
                }

                // Transition to Recording state
                _activeRecordingFilePath = fullPath;
                _fileUnlockedStreak = 0;
                _lastRecordedFileSize = -1;

                if (StateMachine.TransitionTo(RecordingState.Recording, "File: " + fileName) ||
                    StateMachine.CurrentState == RecordingState.Recording)
                {
                    OnRecordingStarted(new RecordingEventArgs("File Created", fileName, fullPath));
                }
            }
            catch (Exception ex)
            {
                LogDiagnostic("Error handling file: " + ex.Message);
            }
        }

        private void FileWatcher_Created(object sender, FileSystemEventArgs e)
        {
            HandleFileDetection(e.FullPath, "Created");
        }

        private void FileWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            // Only consider Changed if we're waiting for recording and a valid video file appeared
            if (StateMachine.CurrentState == RecordingState.WaitingForRecording || StateMachine.CurrentState == RecordingState.Completed)
            {
                HandleFileDetection(e.FullPath, "Changed");
            }
        }

        private void FileWatcher_Renamed(object sender, RenamedEventArgs e)
        {
            // Recorders often write to name.mp4.part and rename to name.mp4 when finished or started
            string ext = Path.GetExtension(e.FullPath);
            if (IsVideoExtension(ext))
            {
                LogDiagnostic(string.Format("File Renamed: {0} -> {1}", e.OldName, e.Name));
                HandleFileDetection(e.FullPath, "Renamed");
            }
        }

        private void MonitorTick(object state)
        {
            if (!_isRunning || !Enabled) return;

            try
            {
                // A. Check active recording file status
                if (!string.IsNullOrEmpty(_activeRecordingFilePath))
                {
                    CheckActiveRecordingFileStatus();
                }

                // B. Check running processes & window titles
                CheckProcessesStatus();
            }
            catch (Exception ex)
            {
                LogDiagnostic("Monitor tick exception: " + ex.Message);
            }
        }

        private void CheckActiveRecordingFileStatus()
        {
            try
            {
                if (!File.Exists(_activeRecordingFilePath))
                {
                    // File was deleted or moved
                    string fileName = Path.GetFileName(_activeRecordingFilePath);
                    LogDiagnostic("Active recording file was moved or deleted: " + fileName);
                    TriggerRecordingStop("File Moved/Deleted", fileName);
                    return;
                }

                FileInfo fi = new FileInfo(_activeRecordingFilePath);
                long currentLength = fi.Length;
                bool locked = IsFileLocked(_activeRecordingFilePath);

                LogDiagnostic(string.Format("File Check: {0} | Size: {1:N0} bytes | Locked: {2}",
                    fi.Name, currentLength, locked));

                if (locked)
                {
                    // Recorder is actively writing to file
                    _fileUnlockedStreak = 0;
                    _lastRecordedFileSize = currentLength;
                }
                else
                {
                    // File is unlocked (recorder released file handle)
                    _fileUnlockedStreak++;

                    // Verify size is stable (not continuing to grow through another mechanism)
                    bool sizeStable = (_lastRecordedFileSize > 0 && currentLength == _lastRecordedFileSize);
                    _lastRecordedFileSize = currentLength;

                    // Require at least 2 consecutive unlocked ticks (~3s) to ensure clean stop
                    if (_fileUnlockedStreak >= 2)
                    {
                        string fileName = fi.Name;
                        LogDiagnostic(string.Format("File confirmed closed & unlocked ({0}). Finalizing.", fileName));
                        TriggerRecordingStop("File Finished Writing", fileName);
                    }
                }
            }
            catch (Exception ex)
            {
                LogDiagnostic("Error checking file status: " + ex.Message);
            }
        }

        private bool IsFileLocked(string filePath)
        {
            FileStream stream = null;
            try
            {
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

                        // 2. OBS Studio detection
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
                    LogDiagnostic("Process recording detected: " + sourceDetail);

                    if (StateMachine.TransitionTo(RecordingState.Recording, "Process: " + sourceDetail) ||
                        StateMachine.CurrentState == RecordingState.Recording)
                    {
                        OnRecordingStarted(new RecordingEventArgs("Process Active", sourceDetail));
                    }
                }
                else if (!activeRecordingFound && _isProcessRecording)
                {
                    _isProcessRecording = false;
                    LogDiagnostic("Process recording stopped: " + sourceDetail);

                    // If not currently waiting on a locked file, stop recording
                    if (string.IsNullOrEmpty(_activeRecordingFilePath))
                    {
                        TriggerRecordingStop("Process Stopped", sourceDetail);
                    }
                }
            }
            catch { }
        }

        private void TriggerRecordingStop(string reason, string details)
        {
            _activeRecordingFilePath = null;
            _fileUnlockedStreak = 0;
            _lastRecordedFileSize = -1;

            if (StateMachine.TransitionTo(RecordingState.Finalizing, reason))
            {
                OnRecordingStopped(new RecordingEventArgs(reason, details));
                StateMachine.TransitionTo(RecordingState.Completed, "Session Finalized");

                // If auto-detect is enabled, return to waiting for recording after a short pause
                if (Enabled)
                {
                    ThreadPool.QueueUserWorkItem(s =>
                    {
                        Thread.Sleep(2000);
                        if (_isRunning && Enabled && StateMachine.CurrentState == RecordingState.Completed)
                        {
                            StateMachine.TransitionTo(RecordingState.WaitingForRecording, "Ready for next recording");
                        }
                    });
                }
            }
        }

        public void NotifySessionEndedManually()
        {
            _activeRecordingFilePath = null;
            _fileUnlockedStreak = 0;
            _lastRecordedFileSize = -1;
            _isProcessRecording = false;
            StateMachine.ForceState(Enabled ? RecordingState.WaitingForRecording : RecordingState.Idle, "Manual End Session");
            LogDiagnostic("Session ended manually by user.");
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
