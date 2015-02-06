using System;
using System.Collections.Generic;

namespace RamjetAnvil.Coroutine {
    public class CoroutineScheduler {
        private readonly IList<Coroutine> _routines;

        public CoroutineScheduler() {
            _routines = new List<Coroutine>();
        }

        public Coroutine Start(IEnumerator<CoroutineInstruction> fibre) {
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

        public void Update(TimeInfo time) {
            for (int i = 0; i < _routines.Count; i++) {
                var routine = _routines[i];

                if (routine.IsFinished) {
                    _routines.Remove(routine);
                } else {
                    routine.Update(time);    
                }
            }
        }
    }

    public struct TimeInfo {
        public float DeltaTime;
        public int DeltaFrames;
    }

    public struct CoroutineInstruction {
        public int? WaitFrames;
        public float? WaitSeconds;
        public IEnumerator<CoroutineInstruction> WaitRoutine;

        public override string ToString() {
            if (WaitFrames.HasValue) {
                return "WaitFrames(" + WaitFrames.Value + ")";
            }
            if (WaitSeconds.HasValue) {
                return "WaitSeconds(" + WaitSeconds.Value + ")";
            }
            if (WaitRoutine != null) {
                return "WaitRoutine";
            }
            throw new ArgumentException("Unsupported instruction");
        }
    }

    public class Coroutine {

        private readonly Stack<CoroutineInstruction> _instructionStack;

        public Coroutine(IEnumerator<CoroutineInstruction> fibre) {
            _instructionStack = new Stack<CoroutineInstruction>();
            _instructionStack.Push(new CoroutineInstruction {WaitRoutine = fibre});
        }

        public Coroutine(Stack<CoroutineInstruction> instructionStack) {
            _instructionStack = instructionStack;
        }

        public void Update(TimeInfo time) {
            var instruction = _instructionStack.Peek();
            var isInstructionFinished = false;

            if (instruction.WaitFrames.HasValue) {
                instruction.WaitFrames -= time.DeltaFrames;
                isInstructionFinished = instruction.WaitFrames.Value <= 0;
                UpdateCurrentInstruction(instruction);
            } else if (instruction.WaitSeconds.HasValue) {
                instruction.WaitSeconds -= time.DeltaTime;
                isInstructionFinished = instruction.WaitSeconds.Value <= 0f;
                UpdateCurrentInstruction(instruction);
            } else if (instruction.WaitRoutine != null) {
                if (instruction.WaitRoutine.MoveNext()) {
                    // Push as long as we get wait routines
                    _instructionStack.Push(instruction.WaitRoutine.Current);
                    Update(time);
                } else {
                    isInstructionFinished = true;
                }
            }

            if (isInstructionFinished) {
                _instructionStack.Pop();
            }
        }

        private void UpdateCurrentInstruction(CoroutineInstruction instruction) {
            _instructionStack.Pop();
            _instructionStack.Push(instruction);
        }

        public bool IsFinished {
            get { return _instructionStack.Count == 0; }
        }
    }
}