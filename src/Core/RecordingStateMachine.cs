using System;

namespace StreamClipMarker.Core
{
    public enum RecordingState
    {
        Idle,
        WaitingForRecording,
        Recording,
        Finalizing,
        Completed
    }

    public class StateChangedEventArgs : EventArgs
    {
        public RecordingState OldState { get; private set; }
        public RecordingState NewState { get; private set; }
        public string Reason { get; private set; }

        public StateChangedEventArgs(RecordingState oldState, RecordingState newState, string reason = "")
        {
            OldState = oldState;
            NewState = newState;
            Reason = reason;
        }
    }

    public class RecordingStateMachine
    {
        private RecordingState _currentState = RecordingState.Idle;
        private readonly object _lock = new object();

        public event EventHandler<StateChangedEventArgs> StateChanged;

        public RecordingState CurrentState
        {
            get
            {
                lock (_lock)
                {
                    return _currentState;
                }
            }
        }

        public bool CanTransitionTo(RecordingState nextState)
        {
            lock (_lock)
            {
                switch (_currentState)
                {
                    case RecordingState.Idle:
                        return nextState == RecordingState.WaitingForRecording || nextState == RecordingState.Recording;

                    case RecordingState.WaitingForRecording:
                        return nextState == RecordingState.Recording || nextState == RecordingState.Idle;

                    case RecordingState.Recording:
                        return nextState == RecordingState.Finalizing || nextState == RecordingState.Completed || nextState == RecordingState.Idle;

                    case RecordingState.Finalizing:
                        return nextState == RecordingState.Completed || nextState == RecordingState.WaitingForRecording || nextState == RecordingState.Idle;

                    case RecordingState.Completed:
                        return nextState == RecordingState.WaitingForRecording || nextState == RecordingState.Recording || nextState == RecordingState.Idle;

                    default:
                        return true;
                }
            }
        }

        public bool TransitionTo(RecordingState newState, string reason = "")
        {
            RecordingState oldState;
            lock (_lock)
            {
                if (_currentState == newState) return false;
                if (!CanTransitionTo(newState)) return false;

                oldState = _currentState;
                _currentState = newState;
            }

            EventHandler<StateChangedEventArgs> handler = StateChanged;
            if (handler != null)
            {
                handler(this, new StateChangedEventArgs(oldState, newState, reason));
            }
            return true;
        }

        public void ForceState(RecordingState newState, string reason = "")
        {
            RecordingState oldState;
            lock (_lock)
            {
                oldState = _currentState;
                _currentState = newState;
            }

            EventHandler<StateChangedEventArgs> handler = StateChanged;
            if (handler != null)
            {
                handler(this, new StateChangedEventArgs(oldState, newState, reason));
            }
        }
    }
}
