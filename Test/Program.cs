using System;
using System.Collections.Generic;
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
            scheduler.Run(SimpleRoutine());
//            var timePassed = 0f;
//            var frameCounter = -1;
//            while (timePassed < 15f) {
//                Console.WriteLine("ding " + timePassed);
//                timePassed += 1f;
//                frameCounter += 1;
//                scheduler.Update(frameCounter, timePassed);
//                Thread.Sleep(200);
//            }
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
    }
}
