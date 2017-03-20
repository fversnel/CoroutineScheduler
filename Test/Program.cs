using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using RamjetAnvil.Coroutine;
using RamjetAnvil.Coroutine.Time;

namespace Test
{
    class Program
    {
        static void Main(string[] args) {
            var scheduler = new CoroutineScheduler();
            //scheduler.Run(SimpleRoutine());
            scheduler.Run(Fade("aa"));
            scheduler.Run(Fade("bb"));

            int currentFrame = 0;
            float timePassed = 0f;
            for (int i = 0; i < 100; i++) {
                scheduler.Update(currentFrame, timePassed);
                timePassed += 0.1f;
                currentFrame++;
            }

            //scheduler.Update(0, 0f);
            //scheduler.Update(1, 0f);
//            var timePassed = 0f;
//            var frameCounter = -1;
//            while (timePassed < 15f) {
//                Console.WriteLine("ding " + timePassed);
//                timePassed += 1f;
//                frameCounter += 1;
//                scheduler.Update(frameCounter, timePassed);
//                Thread.Sleep(200);
//            }

            //scheduler.Run(Transition("aap"));
            Console.ReadLine();
        }

        static IEnumerator<WaitCommand> SimpleRoutine() {
            yield return WaitCommand.WaitForNextFrame;
            Console.WriteLine("jaja");
            yield return WaitCommand.Wait(1f.Seconds());

            Console.WriteLine("jaja2");

            yield return WaitCommand.Interleave(
                WaitCommand.Wait(20.Seconds()).AsRoutine,
                Subroutine(false, "aa"),
                Subroutine(true, "bb"),
                Subroutine(false, "cc"));

            Console.WriteLine("first");
            yield return WaitCommand.Wait(1f.Seconds());
            Console.WriteLine("second");
            yield return WaitCommand.Wait(1f.Seconds());
        }

        static IEnumerator<WaitCommand> Fade(string id) {
            Console.WriteLine("before fade out " + id);
            yield return FadeOut(id).AsWaitCommand();
            Console.WriteLine("in between fades " + id);
            yield return FadeIn(id).AsWaitCommand();
            Console.WriteLine("after fade in " + id);
        }

        static IEnumerator<WaitCommand> FadeIn(string id) {
            yield return Routines.Animate(
                () => 0.1.Seconds(), 
                1.Seconds(), 
                value => Console.WriteLine("fade in " + value + " " + id), 
                Routines.EaseInOutAnimation.Reverse()).AsWaitCommand();
        }

        static IEnumerator<WaitCommand> FadeOut(string id) {
            yield return Routines.Animate(
                () => 0.1.Seconds(), 
                1.Seconds(), 
                value => Console.WriteLine("fade out " + value + " " + id), 
                Routines.EaseInOutAnimation).AsWaitCommand();
        }

        static IEnumerator<WaitCommand> Subroutine(bool hasSubroutine, string prefix) {
            yield return WaitCommand.Wait(5f.Seconds());
            if (hasSubroutine) {
                yield return WaitCommand.Interleave(
                    Subroutine(false, prefix + prefix),
                    Subroutine(false, prefix + prefix + "2"));
            }
            yield return WaitCommand.DontWait;
            Console.WriteLine(prefix + " subroutine");
        }

        static IEnumerator<WaitCommand> InterleaveTest() {
            yield return WaitCommand.Interleave(
                WaitSeconds("z", 5),
                InterleaveTest("a"),
                InterleaveTest("b"),
                InterleaveTest("c"));
        }

        static IEnumerator<WaitCommand> WaitSeconds(string prefix, float seconds) {
            yield return WaitCommand.WaitSeconds(seconds);
            Console.WriteLine(seconds + " seconds waited");
        }

        static IEnumerator<WaitCommand> InterleaveTest(string prefix) {
            for (int i = 0; i < 10; i++) {
                Console.WriteLine(prefix + " " + i);
                yield return WaitCommand.WaitForNextFrame;
            }
        }

        static IEnumerator<WaitCommand> NeverWait(int iterationCount) {
            if (iterationCount >= 100) {
                Console.WriteLine("First command");
                yield return WaitCommand.WaitForNextFrame;
                //yield return WaitCommand.WaitForNextFrame;
            }

            if (iterationCount <= 0) {
                Console.WriteLine("I'm done");
            } else {
                yield return WaitCommand.DontWait;
                Console.WriteLine("iteration " + iterationCount);
                yield return NeverWait(iterationCount - 1).AsWaitCommand();
            }
        }

        private static IEnumerator<WaitCommand> Transition(string state) {
            Console.WriteLine("before exit old state");
            yield return ExitOldState(state).AsWaitCommand();
            Console.WriteLine("after exit old state");
            yield return EnterNewState(state).AsWaitCommand();
        }

        private static IEnumerator<WaitCommand> ExitOldState(string state) {
            yield return (OnExit()).Visit(command => {
                Console.WriteLine("on exit (" + state + ")");
            }).AsWaitCommand();    
        }

        private static  IEnumerator<WaitCommand> EnterNewState(string state) {
            yield return (OnEnter()).Visit(command => {
                Console.WriteLine("on enter (" + state + ")");
            }).AsWaitCommand();    
        }

        private static IEnumerator<WaitCommand> OnExit() {
            Console.WriteLine("exit old state");
            yield return WaitCommand.DontWait;
        }

        private static  IEnumerator<WaitCommand> OnEnter() {
            Console.WriteLine("enter new state");
            yield return WaitCommand.DontWait;
        }
    }
}
