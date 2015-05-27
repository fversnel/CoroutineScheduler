using System;
using System.Collections.Generic;

namespace RamjetAnvil.Coroutine {
    public interface ICoroutineScheduler {
        IDisposable Run(IEnumerator<WaitCommand> fibre);
    }

    public class CoroutineScheduler : ICoroutineScheduler {
        private readonly ObjectPool<Routine> _routinePool; 
        private readonly IList<Routine> _routines;

        private int _prevFrame;
        private float _prevTime;

        public CoroutineScheduler(int initialCapacity = 10, int growthStep = 10) {
            _routinePool = new ObjectPool<Routine>(factory: () => new Routine(Stop), growthStep: growthStep);
            _routines = new List<Routine>(capacity: initialCapacity);
            _prevFrame = -1;
            _prevTime = 0f;
        }

        public IDisposable Run(IEnumerator<WaitCommand> fibre) {
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

        public void Update(int currentFrame, float currentTime) {
            var timeInfo = new TimeInfo {
                DeltaFrames = currentFrame - _prevFrame,
                DeltaTime = currentTime - _prevTime
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
    }

    public struct WaitCommand {
        public int Frames;
        public float Seconds;
        public IEnumerator<WaitCommand> Routine;

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
            return new WaitCommand { Routine = routine };
        }

        public bool IsFinished {
            get { return Seconds <= 0f && Frames <= 0 && Routine == null; }
        }
    }

    public static class WaitCommandExtensions {
        public static WaitCommand AsWaitCommand(this IEnumerator<WaitCommand> coroutine) {
            return WaitCommand.WaitRoutine(coroutine);
        }

        public static IEnumerator<WaitCommand> AsRoutine(this WaitCommand waitCommand) {
            yield return waitCommand;
        } 

        public static WaitCommand Combine(this WaitCommand first, WaitCommand second) {
            return new WaitCommand {
                Frames = Math.Max(first.Frames, second.Frames),
                Seconds = Math.Max(first.Seconds, second.Seconds),
                Routine = null // Combining routines is not supported at the moment
            };
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

        public static IEnumerator<WaitCommand> Interleave(this IEnumerator<WaitCommand> first, 
            IEnumerator<WaitCommand> second) {
            var isRunning = true;
            while (isRunning) {
                var isFirst = first.MoveNext();
                var isSecond = second.MoveNext();

                WaitCommand command = WaitCommand.DontWait; 
                if (isFirst && isSecond) {
                    command = first.Current.Combine(second.Current);
                } else if (isFirst) {
                    command = first.Current;
                } else if (isSecond) {
                    command = second.Current;
                }

                yield return command;

                isRunning = isFirst || isSecond;
            }
        }

        public static void Skip(this IEnumerator<WaitCommand> routine) {
            while (routine.MoveNext()) {
                // Ignore
            }
        }
    }

    public class Routine : IResetable, IDisposable {

        private readonly Stack<WaitCommand> _instructionStack;
        private readonly Action<Routine> _disposeRoutine; 

        public Routine(Action<Routine> disposeRoutine) {
            _disposeRoutine = disposeRoutine;
            _instructionStack = new Stack<WaitCommand>();
        }

        public void Initialize(IEnumerator<WaitCommand> fibre) {
            _instructionStack.Push(new WaitCommand { Routine = fibre });
            FetchNextInstruction();
        }

        public void Update(TimeInfo time) {
            // Find a new instruction and make it the current one
            if (CurrentInstruction.IsFinished) {
                _instructionStack.Pop();
                FetchNextInstruction();
            }

            // Update the current instruction
            if (!IsFinished) {
                UpdateCurrentInstruction(new WaitCommand {
                    Frames = CurrentInstruction.Frames - time.DeltaFrames, 
                    Seconds = CurrentInstruction.Seconds - time.DeltaTime
                });
            }
        }

        private void FetchNextInstruction() {
            // Push/Pop (sub-)coroutines until we get another instruction or we run out of instructions.
            while (!IsFinished && CurrentInstruction.Routine != null) {
                if (CurrentInstruction.Routine.MoveNext()) {
                    // Skip empty instructions
                    var newInstruction = CurrentInstruction.Routine.Current;
                    if (!newInstruction.IsFinished) {
                        _instructionStack.Push(newInstruction);    
                    }
                } else {
                    _instructionStack.Pop();
                }
            }
        }

        private void UpdateCurrentInstruction(WaitCommand updatedInstruction) {
            _instructionStack.Pop();
            _instructionStack.Push(updatedInstruction);
        }

        private WaitCommand CurrentInstruction {
            get { return _instructionStack.Peek(); }
        }

        public bool IsFinished {
            get { return _instructionStack.Count == 0; }
        }

        public void Reset() {
            _instructionStack.Clear();
        }

        public void Dispose() {
            _disposeRoutine(this);
        }
    }
}