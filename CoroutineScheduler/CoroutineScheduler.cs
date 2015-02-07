using System;
using System.Collections.Generic;

namespace RamjetAnvil.Coroutine {
    public class CoroutineScheduler {
        private readonly IList<Routine> _routines;

        private int _prevFrame;
        private float _prevTime;

        public CoroutineScheduler() {
            _routines = new List<Routine>();
            _prevFrame = -1;
            _prevTime = 0f;
        }

        public Routine Start(IEnumerator<WaitCommand> fibre) {
            if (fibre == null) {
                throw new Exception("Routine cannot be null");
            }

            var coroutine = new Routine(fibre);
            _routines.Add(coroutine);
            return coroutine;
        }

        public void Stop(Routine r) {
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

    public class Routine {

        private readonly Stack<WaitCommand> _instructionStack;

        public Routine(IEnumerator<WaitCommand> fibre) {
            _instructionStack = new Stack<WaitCommand>();
            _instructionStack.Push(new WaitCommand {Routine = fibre});
        }

        public Routine(Stack<WaitCommand> instructionStack) {
            _instructionStack = instructionStack;
        }

        public void Update(TimeInfo time) {
            // Push/Pop (sub-)coroutines until we get another instruction or we run out of instructions.
            while (_instructionStack.Count > 0 && _instructionStack.Peek().Routine != null) {
                var instruction = _instructionStack.Peek();
                if (instruction.Routine.MoveNext()) {
                    _instructionStack.Push(instruction.Routine.Current);
                } else {
                    _instructionStack.Pop();
                }
            }

            if (_instructionStack.Count > 0) {
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
                }

                if (isInstructionFinished) {
                    _instructionStack.Pop();
                }
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