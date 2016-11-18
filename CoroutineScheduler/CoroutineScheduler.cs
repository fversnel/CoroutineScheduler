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
            _routinePool = new ObjectPool<Routine>(factory: () => new Routine(CreateRoutine, RecycleRoutine), growthStep: growthStep);
            _routines = new List<Routine>(capacity: initialCapacity);
            _prevFrame = -1;
            _prevTime = 0f;
        }

        public IDisposable Run(IEnumerator<WaitCommand> fibre) {
            return RunInternal(fibre);
        }

        private Routine RunInternal(IEnumerator<WaitCommand> fibre) {
            var coroutine = CreateRoutine(fibre);
            _routines.Add(coroutine);
            return coroutine;
        }

        private Routine CreateRoutine(IEnumerator<WaitCommand> fibre) {
            if (fibre == null) {
                throw new Exception("Routine cannot be null");
            }

            var coroutine = _routinePool.Take();
            coroutine.Initialize(fibre);
            return coroutine;
        }

        private void RecycleRoutine(Routine r) {
            _routinePool.Return(r);
        }

        public void Update(long currentFrame, double currentTime) {
            var timePassed = new Duration(
                frameCount: (int) (currentFrame - _prevFrame),
                seconds: (float) (currentTime - _prevTime));

            for (int i = _routines.Count - 1; i >= 0; i--) {
                var routine = _routines[i];

                if (routine.IsFinished) {
                    _routines.RemoveAt(i);
                    RecycleRoutine(routine);
                } else {
                    routine.Update(timePassed);
                }
            }

            _prevFrame = currentFrame;
            _prevTime = currentTime;
        }
    }

    public struct Duration : IEquatable<Duration> {
        public readonly float Seconds;
        public readonly int FrameCount;

        public Duration(float seconds = 0f, int frameCount = 0) {
            Seconds = seconds;
            FrameCount = frameCount;
        }

        public static Duration operator -(Duration t1, Duration t2) {
            return new Duration(
                frameCount: Math.Max(t1.FrameCount - t2.FrameCount, 0),
                seconds: Math.Max(t1.Seconds - t2.Seconds, 0f));
        }

        public static Duration operator +(Duration t1, Duration t2) {
            return new Duration(
                frameCount: t1.FrameCount + t2.FrameCount,
                seconds: t1.Seconds + t2.Seconds);
        }

        public static Duration Min(Duration t1, Duration t2) {
            return new Duration(
                seconds: Math.Min(t1.Seconds, t2.Seconds),
                frameCount: Math.Min(t1.FrameCount, t2.FrameCount));
        }

        public static Duration Max(Duration t1, Duration t2) {
            return new Duration(
                seconds: Math.Max(t1.Seconds, t2.Seconds),
                frameCount: Math.Max(t1.FrameCount, t2.FrameCount));
        }

        public bool Equals(Duration other) {
            return Seconds.Equals(other.Seconds) && FrameCount == other.FrameCount;
        }

        public override bool Equals(object obj) {
            if (ReferenceEquals(null, obj)) return false;
            return obj is Duration && Equals((Duration) obj);
        }

        public override int GetHashCode() {
            unchecked {
                return (Seconds.GetHashCode() * 397) ^ FrameCount;
            }
        }

        public static bool operator ==(Duration left, Duration right) {
            return left.Equals(right);
        }

        public static bool operator !=(Duration left, Duration right) {
            return !left.Equals(right);
        }

        public bool IsTimeLeft {
            get { return Seconds > 0f || FrameCount > 0; }
        }

        public bool IsTimeUp {
            get { return !IsTimeLeft; }
        }
    }

    public struct WaitCommand {
        private static readonly IEnumerator<WaitCommand>[] EmptyRoutines = {};

        public readonly Duration? Duration;
        // TODO Optimize for single routine wait commands
        public readonly IEnumerator<WaitCommand>[] Routines;

        private WaitCommand(Duration duration) {
            Duration = duration;
            Routines = EmptyRoutines;
        }

        private WaitCommand(IEnumerator<WaitCommand>[] routines) {
            Duration = null;
            Routines = routines;
        }

        public static WaitCommand Wait(TimeSpan duration) {
            return new WaitCommand(new Duration(seconds: (float) duration.TotalSeconds));
        }

        public static WaitCommand WaitSeconds(float seconds) {
            return new WaitCommand(new Duration(seconds: seconds));
        }

        public static WaitCommand WaitFrames(int frameCount) {
            return new WaitCommand(new Duration(frameCount: frameCount));
        }

        public static WaitCommand WaitForNextFrame {
            get { return WaitFrames(1); }
        }

        public static WaitCommand DontWait {
            get { return new WaitCommand(new Duration(seconds: 0f, frameCount: 0)); }
        }

        public static WaitCommand WaitRoutine(IEnumerator<WaitCommand> routine) {
            return new WaitCommand(new[] { routine });
        }

        public static WaitCommand Interleave(params IEnumerator<WaitCommand>[] routines) {
            return new WaitCommand(routines);
        }

        public static WaitCommand operator -(WaitCommand command, Duration duration) {
            if (command.Duration.HasValue) {
                return new WaitCommand(command.Duration.Value - duration);
            }
            throw new ArgumentException("Cannot subtract time from a routine wait command");
        }

        public static WaitCommand operator +(WaitCommand command, Duration duration) {
            if (command.Duration.HasValue) {
                return new WaitCommand(command.Duration.Value + duration);
            }
            throw new ArgumentException("Cannot add time to a routine wait command");
        }

        public bool IsRoutine {
            get { return !Duration.HasValue; }
        }

        public bool IsFinished {
            get {
                if (Duration.HasValue) {
                    return Duration.Value.IsTimeUp;
                }
                return false;
            }
        }

        public IEnumerator<WaitCommand> AsRoutine {
            get {
                if (IsRoutine && Routines.Length == 1) {
                    return Routines[0];
                }
                return AsRoutineInternal();
            }
        }

        private IEnumerator<WaitCommand> AsRoutineInternal() {
            yield return this;
        }
    }

    public delegate bool Predicate();

    public static class WaitCommandExtensions {
        public static WaitCommand AsWaitCommand(this IEnumerator<WaitCommand> coroutine) {
            return WaitCommand.WaitRoutine(coroutine);
        }

        public static IEnumerator<WaitCommand> WaitUntilDone(this IAwaitable awaitable) {
            while(!awaitable.IsDone) {
                yield return WaitCommand.WaitForNextFrame;
            }
        }

        public static IEnumerator<WaitCommand> WaitUntil(this WaitCommand waitCommand, Predicate predicate) {
            return WaitUntil(waitCommand.AsRoutine, predicate);
        }

        public static IEnumerator<WaitCommand> WaitUntil(this IEnumerator<WaitCommand> routine, Predicate predicate) {
            while(!predicate()) {
                yield return WaitCommand.WaitForNextFrame;
            }
            yield return routine.AsWaitCommand();
        }

        public static IEnumerator<WaitCommand> WaitWhile(this WaitCommand waitCommand, Predicate predicate) {
            return WaitWhile(waitCommand.AsRoutine, predicate);
        }

        public static IEnumerator<WaitCommand> WaitWhile(this IEnumerator<WaitCommand> routine, Predicate predicate) {
            while(predicate()) {
                yield return WaitCommand.WaitForNextFrame;
            }
            yield return routine.AsWaitCommand();
        }

        public static IEnumerator<WaitCommand> RunWhile(this WaitCommand waitCommand, Predicate predicate) {
            return RunWhile(waitCommand.AsRoutine, predicate);
        }

        public static IEnumerator<WaitCommand> RunWhile(this IEnumerator<WaitCommand> routine, Predicate predicate) {
            while (routine.MoveNext() && predicate()) {
                // Recursively skip subroutines
                var currentInstruction = routine.Current;
                if (currentInstruction.IsRoutine) {
                    var instructionRoutines = routine.Current.Routines;

                    var wrappedRoutines = new IEnumerator<WaitCommand>[instructionRoutines.Length];
                    for (int i = 0; i < instructionRoutines.Length; i++) {
                        wrappedRoutines[i] = RunWhile(instructionRoutines[i], predicate);
                    }

                    yield return WaitCommand.Interleave(wrappedRoutines);
                } else {
                    yield return currentInstruction;
                }
            }
        }

        public static IEnumerator<WaitCommand> RunUntil(this IEnumerator<WaitCommand> routine, Predicate predicate) {
            return RunWhile(routine, () => !predicate());
        }

        public static IEnumerator<WaitCommand> RunUntil(this WaitCommand waitCommand, Predicate predicate) {
            return RunUntil(waitCommand.AsRoutine, predicate);
        }

        public static IEnumerator<WaitCommand> AndThen(this IEnumerator<WaitCommand> first, WaitCommand second) {
            return AndThen(first.AsWaitCommand(), second);
        }

        public static IEnumerator<WaitCommand> AndThen(this IEnumerator<WaitCommand> first, IEnumerator<WaitCommand> second) {
            return AndThen(first.AsWaitCommand(), second.AsWaitCommand());
        }

        public static IEnumerator<WaitCommand> AndThen(this WaitCommand first, IEnumerator<WaitCommand> second) {
            return AndThen(first, second.AsWaitCommand());
        }

        public static IEnumerator<WaitCommand> AndThen(this WaitCommand first, WaitCommand second) {
            yield return first;
            yield return second;
        }

        public static void Skip(this IEnumerator<WaitCommand> routine) {
            while (routine.MoveNext()) {
                // Recursively skip subroutines
                var instructionRoutines = routine.Current.Routines;
                for (int i = 0; i < instructionRoutines.Length; i++) {
                    instructionRoutines[i].Skip();
                }
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

        private readonly Func<IEnumerator<WaitCommand>, Routine> _createSubroutine;
        private readonly Action<Routine> _disposeRoutine;

        private IEnumerator<WaitCommand> _fibre;

        private readonly IList<Routine> _activeSubroutines;
        private WaitCommand _activeWaitCommand;
        private bool _isFinished;
        

        public Routine(Func<IEnumerator<WaitCommand>, Routine> createSubroutine, Action<Routine> disposeRoutine) {
            _createSubroutine = createSubroutine;
            _disposeRoutine = disposeRoutine;
            _activeSubroutines = new List<Routine>();
        }

        public void Initialize(IEnumerator<WaitCommand> fibre) {
            _fibre = fibre;
            _activeSubroutines.Clear();
            _activeWaitCommand = WaitCommand.DontWait;
            _isFinished = false;
            FetchNextInstruction();
        }

        public Duration Update(Duration timePassed) {
            Duration leftOverTime = timePassed;
            if (leftOverTime.IsTimeLeft) {
                // Find a new instruction and make it the current one
                if (IsRunningInstructionFinished) {
                    FetchNextInstruction();
                }

                // Update the current instruction
                if (!_isFinished) {
                    if (_activeSubroutines.Count > 0) {
                        for (int i = _activeSubroutines.Count - 1; i >= 0; i--) {
                            var subroutine = _activeSubroutines[i];
                            var subroutineTimeLeft = subroutine.Update(timePassed);
                            if (subroutine.IsFinished) {
                                subroutine.Dispose();
                                _activeSubroutines.RemoveAt(i);
                            }
                            leftOverTime = Duration.Min(leftOverTime, subroutineTimeLeft);
                        }
                    } else {
                        leftOverTime = timePassed - _activeWaitCommand.Duration.Value;
                        _activeWaitCommand = _activeWaitCommand - timePassed;
                    }

                    if (leftOverTime != timePassed) {
                        leftOverTime = Update(leftOverTime);
                    }
                }
            }
            return leftOverTime;
        }

        private void FetchNextInstruction() {
            // Push/Pop (sub-)coroutines until we get another instruction or we run out of instructions.
            while(!_isFinished && IsRunningInstructionFinished) {
                if (_fibre.MoveNext()) {
                    var newInstruction = _fibre.Current;
                    if (newInstruction.IsRoutine) {
                        for (int i = 0; i < newInstruction.Routines.Length; i++) {
                            var subroutine = newInstruction.Routines[i];
                            var startedSubroutine = _createSubroutine(subroutine);
                            _activeSubroutines.Add(startedSubroutine);
                        }

                        _activeWaitCommand = WaitCommand.DontWait;
                    } else {
                        _activeWaitCommand = newInstruction;
                    }
                } else {
                    _isFinished = true;
                }
            }
        }
        
        private bool IsRunningInstructionFinished {
            get { return _activeSubroutines.Count == 0 && _activeWaitCommand.IsFinished; }
        }

        public bool IsFinished {
            get { return _isFinished; }
        }

        public void Reset() {
            _fibre = null;
            for (int i = 0; i < _activeSubroutines.Count; i++) {
                var activeRoutine = _activeSubroutines[i];
                activeRoutine.Dispose();
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

    public interface IAwaitable {
        bool IsDone { get; }
    }
}