using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using RamjetAnvil.Coroutine;

namespace Test
{
    class Program
    {
        static void Main(string[] args) {
            Console.WriteLine("jaja1234");

            var scheduler = new CoroutineScheduler();
            scheduler.Run(SimpleRoutine());
            var timePassed = 0f;
            var frameCounter = -1;
            while (timePassed < 20f) {
                Console.WriteLine("ding " + timePassed);
                timePassed += 1f;
                frameCounter += 1;
                scheduler.Update(frameCounter, timePassed);
                Thread.Sleep(200);
            }
            Console.ReadLine();
        }

        static IEnumerator<WaitCommand> SimpleRoutine() {
            Console.WriteLine("jaja");
            yield return WaitCommand.WaitSeconds(1f);

            Console.WriteLine("jaja2");

            yield return WaitCommand.Interleave(
                WaitCommand.WaitSeconds(3).AsRoutine,
                Subroutine(false, "aa"),
                Subroutine(true, "bb"),
                Subroutine(false, "cc"));

            Console.WriteLine("first");
            yield return WaitCommand.WaitSeconds(1f);
            Console.WriteLine("second");
            yield return WaitCommand.WaitSeconds(1f);
        }

        static IEnumerator<WaitCommand> Subroutine(bool hasSubroutine, string prefix) {
            yield return WaitCommand.WaitSeconds(5f);
            if (hasSubroutine) {
                yield return WaitCommand.Interleave(
                    Subroutine(false, prefix + prefix),
                    Subroutine(false, prefix + prefix + "2"));
            }
            yield return WaitCommand.DontWait;
            Console.WriteLine(prefix + " subroutine");
        }
    }
}
