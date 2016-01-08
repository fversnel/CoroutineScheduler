using System;
using System.Collections.Generic;
using UnityEngine;
using RamjetAnvil.Coroutine;

public class UnityCoroutineScheduler : MonoBehaviour, ICoroutineScheduler {
    private CoroutineScheduler _scheduler;

    public IDisposable Run(IEnumerator<WaitCommand> fibre) {
        if (_scheduler == null) {
            _scheduler = new CoroutineScheduler();
        }

        return _scheduler.Run(fibre);
    }

    void Awake() {
        if (_scheduler == null) {
            _scheduler = new CoroutineScheduler();
        }
    }

    void Update() {
        _scheduler.Update(Time.frameCount, Time.realtimeSinceStartup);
    }
}
