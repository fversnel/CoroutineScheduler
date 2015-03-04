using System;
using System.Collections.Generic;

namespace RamjetAnvil.Coroutine {
    public interface ICoroutineScheduler {
        Routine Run(IEnumerator<WaitCommand> fibre);
        void Stop(Routine r);
    }

    public class CoroutineScheduler : ICoroutineScheduler {
        private readonly ObjectPool<Routine> _routinePool; 
        private readonly IList<Routine> _routines;

        private int _prevFrame;
        private float _prevTime;

        public CoroutineScheduler(int initialCapacity = 10, int growthStep = 10) {
            _routinePool = new ObjectPool<Routine>(factory: () => new Routine(), growthStep: growthStep);
            _routines = new List<Routine>(capacity: initialCapacity);
            _prevFrame = -1;
            _prevTime = 0f;
        }

        public Routine Run(IEnumerator<WaitCommand> fibre) {
            if (fibre == null) {
                throw new Exception("Routine cannot be null");
            }

            var coroutine = _routinePool.Take();
            coroutine.Initialize(fibre);
            _routines.Add(coroutine);
            return coroutine;
        }

        public void Stop(Routine r) {
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

    public class Routine : IResetable {

        private readonly Stack<WaitCommand> _instructionStack;

        public Routine() {
            _instructionStack = new Stack<WaitCommand>();
        }

        public void Initialize(IEnumerator<WaitCommand> fibre) {
            _instructionStack.Push(new WaitCommand { Routine = fibre });
            FetchNextInstruction();
        }

        public void Update(TimeInfo time) {
            var instruction = _instructionStack.Peek();
            instruction.Frames -= time.DeltaFrames;
            instruction.Seconds -= time.DeltaTime;

            if (instruction.IsFinished) {
                _instructionStack.Pop();
            } else {
                // Update current instruction
                _instructionStack.Pop();
                _instructionStack.Push(instruction);
            }
            
            FetchNextInstruction();
        }

        private void FetchNextInstruction() {
            // Push/Pop (sub-)coroutines until we get another instruction or we run out of instructions.
            while (_instructionStack.Count > 0 && _instructionStack.Peek().Routine != null) {
                var instruction = _instructionStack.Peek();
                if (instruction.Routine.MoveNext()) {
                    // Skip instructions that are already finished
                    if (!instruction.Routine.Current.IsFinished) {
                        _instructionStack.Push(instruction.Routine.Current);
                    }
                } else {
                    _instructionStack.Pop();
                }
            }
        }

        public bool IsFinished {
            get { return _instructionStack.Count == 0; }
        }

        public void Reset() {
            _instructionStack.Clear();
        }
    }
}