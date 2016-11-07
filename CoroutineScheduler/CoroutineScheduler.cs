using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace RamjetAnvil.Coroutine {
    public interface ICoroutineScheduler {
        IDisposable Run(IEnumerator<WaitCommand> fibre);
    }

    public class CoroutineScheduler : ICoroutineScheduler {
        private readonly ObjectPool<Routine> _routinePool; 
        private readonly IList<Routine> _routines;

        private long _prevFrame;
        private double _prevTime;

        public CoroutineScheduler(int initialCapacity = 10, int growthStep = 10) {
            _routinePool = new ObjectPool<Routine>(factory: () => new Routine(RunInternal, Stop), growthStep: growthStep);
            _routines = new List<Routine>(capacity: initialCapacity);
            _prevFrame = -1;
            _prevTime = 0f;
        }

        public IDisposable Run(IEnumerator<WaitCommand> fibre) {
            return RunInternal(fibre);
        }

        private Routine RunInternal(IEnumerator<WaitCommand> fibre) {
            if (fibre == null) {
                throw new Exception("Routine cannot be null");
            }

            var coroutine = _routinePool.Take();
            coroutine.Initialize(fibre);
            _routines.Add(coroutine);
            return coroutine;
        }

        private void Stop(Routine r) {
            for (int i = _routines.Count - 1; i >= 0; i--) {
                if (_routines[i].Equals(r)) {
                    _routines.RemoveAt(i);
                    _routinePool.Return(r);
                }
            }
        }

        public void Update(long currentFrame, double currentTime) {
            var timeInfo = new TimeInfo {
                DeltaFrames = (int) (currentFrame - _prevFrame),
                DeltaTime = (float) (currentTime - _prevTime)
            };

            for (int i = _routines.Count - 1; i >= 0; i--) {
                var routine = _routines[i];

                if (routine.IsFinished) {
                    _routines.RemoveAt(i);
                    _routinePool.Return(routine);
                } else {
                    routine.Update(timeInfo);
                }
            }

            _prevFrame = currentFrame;
            _prevTime = currentTime;
        }
    }

    public struct TimeInfo {
        public float DeltaTime;
        public int DeltaFrames;

        public TimeInfo(float deltaTime, int deltaFrames) {
            DeltaTime = deltaTime;
            DeltaFrames = deltaFrames;
        }

        public bool IsTimeLeft {
            get { return DeltaTime > 0f && DeltaFrames > 0; }
        }
    }

    public struct WaitCommand {
        public int Frames;
        public float Seconds;
        public IEnumerator<WaitCommand>[] Routines;

        public static WaitCommand WaitSeconds(float seconds) {
            return new WaitCommand { Seconds = seconds };
        }

        public static WaitCommand WaitFrames(int frames) {
            return new WaitCommand { Frames = frames };
        }

        public static WaitCommand WaitForNextFrame {
            get { return new WaitCommand { Frames = 1 }; }
        }

        public static WaitCommand DontWait {
            get { return new WaitCommand(); }
        }

        public static WaitCommand WaitRoutine(IEnumerator<WaitCommand> routine) {
            return new WaitCommand { Routines = new[] { routine } };
        }

        public bool IsRoutine {
            get { return Routines != null && Routines.Length > 0; }
        }

        public bool IsFinished {
            get { return Seconds <= 0f && Frames <= 0 && !IsRoutine; }
        }
    }

    public static class WaitCommandExtensions {
        public static WaitCommand AsWaitCommand(this IEnumerator<WaitCommand> coroutine) {
            return WaitCommand.WaitRoutine(coroutine);
        }

        public static IEnumerator<WaitCommand> AsRoutine(this WaitCommand waitCommand) {
            yield return waitCommand;
        } 
        
        public static IEnumerator<WaitCommand> Then(this IEnumerator<WaitCommand> first,
            IEnumerator<WaitCommand> second) {
            while (first.MoveNext()) {
                yield return first.Current;
            }
            while (second.MoveNext()) {
                yield return second.Current;
            }
        }

        public static IEnumerator<WaitCommand> Interleave(params IEnumerator<WaitCommand>[] routines) {
            yield return new WaitCommand { Routines = routines };
        }

        public static void Skip(this IEnumerator<WaitCommand> routine) {
            while (routine.MoveNext()) {
                // Ignore
            }
        }
    }

    public class AsyncResult<T> {
        private T _result;
        private bool _isResultAvailable;

        public void SetResult(T result) {
            _result = result;
            _isResultAvailable = true;
        }

        public T Result {
            get { return _result; }
        }

        public bool IsResultAvailable {
            get { return _isResultAvailable; }
        }

        public static AsyncResult<T> FromCallback(Action<Action<T>> invoke) {
            var asyncResult = new AsyncResult<T>();
            invoke(asyncResult.SetResult);
            return asyncResult;
        }

        public static EventResult SingleResultFromEvent(Event @event, Func<T, bool> predicate = null) {
            var asyncResult = new AsyncResult<T>();
            var awaitResult = Wait(asyncResult, @event, predicate);
            awaitResult.MoveNext();
            return new EventResult(asyncResult, awaitResult.AsWaitCommand());
        }

        private static IEnumerator<WaitCommand> Wait(AsyncResult<T> asyncResult, 
            Event @event, 
            Func<T, bool> predicate) {

            Action<T> setResult = @obj => {
                if (predicate == null || predicate(obj)) {
                    asyncResult.SetResult(obj);
                }
            };
            @event.AddHandler(setResult);
            while (!asyncResult.IsResultAvailable) {
                yield return WaitCommand.WaitForNextFrame;
            }
            @event.RemoveHandler(setResult);
        }

        public class EventResult {
            private readonly AsyncResult<T> _asyncResult;
            public readonly WaitCommand WaitUntilReady;

            public EventResult(AsyncResult<T> asyncResult, WaitCommand waitUntilReady) {
                _asyncResult = asyncResult;
                WaitUntilReady = waitUntilReady;
            }

            public T Result {
                get { return _asyncResult.Result; }    
            }
        }
    }

    public class Routine : IResetable, IDisposable {

        private readonly Func<IEnumerator<WaitCommand>, Routine> _startSubroutine;
        private readonly Action<Routine> _disposeRoutine;

        private IEnumerator<WaitCommand> _fibre;

        private readonly IList<Routine> _activeSubroutines;
        private WaitCommand _activeWaitCommand;
        private bool _isFinished;
        

        public Routine(Func<IEnumerator<WaitCommand>, Routine> startSubroutine, Action<Routine> disposeRoutine) {
            _startSubroutine = startSubroutine;
            _disposeRoutine = disposeRoutine;
            _activeSubroutines = new List<Routine>();
        }

        public void Initialize(IEnumerator<WaitCommand> fibre) {
            _fibre = fibre;
            _activeSubroutines.Clear();
            _activeWaitCommand = WaitCommand.DontWait;
            _isFinished = false;
            FetchNextInstruction(new TimeInfo(0f, 0));
        }

        public void Update(TimeInfo time) {
            if (time.IsTimeLeft) {
                // Find a new instruction and make it the current one
                if (IsRunningInstructionFinished) {
                    FetchNextInstruction(time);
                }

                // Update the current instruction
                if (!_isFinished) {
                    if (_activeSubroutines.Count == 0) {
                        var leftOverTime = new TimeInfo {
                            DeltaFrames = Math.Max(0, time.DeltaFrames - _activeWaitCommand.Frames),
                            DeltaTime = Math.Max(0f, time.DeltaTime - _activeWaitCommand.Seconds),
                        };

                        _activeWaitCommand = new WaitCommand {
                            Frames = _activeWaitCommand.Frames - time.DeltaFrames,
                            Seconds = _activeWaitCommand.Seconds - time.DeltaTime
                        };

                        Update(leftOverTime);
                    }
                }
            }
        }

        private void FetchNextInstruction(TimeInfo time) {
            // Push/Pop (sub-)coroutines until we get another instruction or we run out of instructions.
            while(!_isFinished && IsRunningInstructionFinished) {
                if (_fibre.MoveNext()) {
                    var newInstruction = _fibre.Current;
                    if (newInstruction.IsRoutine) {
                        for (int i = 0; i < newInstruction.Routines.Length; i++) {
                            var subroutine = newInstruction.Routines[i];
                            var startedSubroutine = _startSubroutine(subroutine);
                            _activeSubroutines.Add(startedSubroutine);
                            startedSubroutine.Update(time);
                        }

                        _activeWaitCommand = WaitCommand.DontWait;
                    } else {
                        _activeWaitCommand = newInstruction;
                        _activeSubroutines.Clear();
                    }
                } else {
                    _isFinished = true;
                }
            }
        }
        
        private bool IsRunningInstructionFinished {
            get {
                if (_activeSubroutines.Count > 0) {
                    var subRoutinesFinished = true;
                    foreach (var routine in _activeSubroutines) {
                        subRoutinesFinished = subRoutinesFinished && routine.IsFinished;
                    }
                    return subRoutinesFinished;
                } else {
                    return _activeWaitCommand.IsFinished;
                }
            }
        }

        public bool IsFinished {
            get { return _isFinished; }
        }

        public void Reset() {
            _fibre = null;
            for (int i = 0; i < _activeSubroutines.Count; i++) {
                var activeRoutine = _activeSubroutines[i];
                activeRoutine.Reset();
            }
            _activeSubroutines.Clear();
            _activeWaitCommand = WaitCommand.DontWait;
        }

        public void Dispose() {
            for (int i = 0; i < _activeSubroutines.Count; i++) {
                var activeRoutine = _activeSubroutines[i];
                activeRoutine.Dispose();
            }
            _disposeRoutine(this);
        }
    }
}