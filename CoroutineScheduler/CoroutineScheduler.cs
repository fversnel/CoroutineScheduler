using System;
using System.Collections.Generic;

namespace RamjetAnvil.Coroutine {
    public class CoroutineScheduler {
        private readonly IList<Coroutine> _routines;

        private int _prevFrame;
        private float _prevTime;

        public CoroutineScheduler() {
            _routines = new List<Coroutine>();
            _prevFrame = -1;
            _prevTime = 0f;
        }

        public Coroutine Start(IEnumerator<WaitCommand> fibre) {
            if (fibre == null) {
                throw new Exception("Coroutine cannot be null");
            }

            var coroutine = new Coroutine(fibre);
            _routines.Add(coroutine);
            return coroutine;
        }

        public void Stop(Coroutine r) {
            _routines.Remove(r);
        }

        public void Update(int currentFrame, float currentTime) {
            var timeInfo = new TimeInfo {
                DeltaFrames = currentFrame - _prevFrame,
                DeltaTime = currentTime - _prevTime
            };

            for (int i = 0; i < _routines.Count; i++) {
                var routine = _routines[i];

                if (routine.IsFinished) {
                    _routines.Remove(routine);
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
        public int? Frames;
        public float? Seconds;
        public IEnumerator<WaitCommand> Routine;

        public static WaitCommand WaitSeconds(float seconds) {
            return new WaitCommand { Seconds = seconds };
        }

        public static WaitCommand WaitFrames(int frames) {
            return new WaitCommand { Frames = frames };
        }

        public static WaitCommand WaitForNextFrame {
            get { return new WaitCommand { Frames = 0 }; }
        }

        public static WaitCommand WaitRoutine(IEnumerator<WaitCommand> routine) {
            return new WaitCommand { Routine = routine };
        }

        public override string ToString() {
            if (Frames.HasValue) {
                return "Frames(" + Frames.Value + ")";
            }
            if (Seconds.HasValue) {
                return "Seconds(" + Seconds.Value + ")";
            }
            if (Routine != null) {
                return "Routine";
            }
            throw new ArgumentException("Unsupported instruction");
        }
    }

    public class Coroutine {

        private readonly Stack<WaitCommand> _instructionStack;

        public Coroutine(IEnumerator<WaitCommand> fibre) {
            _instructionStack = new Stack<WaitCommand>();
            _instructionStack.Push(new WaitCommand {Routine = fibre});
        }

        public Coroutine(Stack<WaitCommand> instructionStack) {
            _instructionStack = instructionStack;
        }

        public void Update(TimeInfo time) {
            var instruction = _instructionStack.Peek();
            var isInstructionFinished = false;

            if (instruction.Frames.HasValue) {
                instruction.Frames -= time.DeltaFrames;
                isInstructionFinished = instruction.Frames.Value <= 0;
                UpdateCurrentInstruction(instruction);
            } else if (instruction.Seconds.HasValue) {
                instruction.Seconds -= time.DeltaTime;
                isInstructionFinished = instruction.Seconds.Value <= 0f;
                UpdateCurrentInstruction(instruction);
            } else if (instruction.Routine != null) {
                if (instruction.Routine.MoveNext()) {
                    // Push as long as we get wait routines
                    _instructionStack.Push(instruction.Routine.Current);
                    Update(time);
                } else {
                    isInstructionFinished = true;
                }
            }

            if (isInstructionFinished) {
                _instructionStack.Pop();
            }
        }

        private void UpdateCurrentInstruction(WaitCommand instruction) {
            _instructionStack.Pop();
            _instructionStack.Push(instruction);
        }

        public bool IsFinished {
            get { return _instructionStack.Count == 0; }
        }
    }
}