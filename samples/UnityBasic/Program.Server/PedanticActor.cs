using System;
using System.Threading.Tasks;
using Akka.Interfaced;
using UnityBasic.Interface;

#pragma warning disable 1998

namespace UnityBasic.Program.Server
{
    public class PedanticActor : InterfacedActor, IPedantic
    {
        async Task IPedantic.TestCall()
        {
            Console.WriteLine("JustCall");
        }

        async Task<int?> IPedantic.TestOptional(int? value)
        {
            return value;
        }

        async Task<Tuple<int, string>> IPedantic.TestTuple(Tuple<int, string> value)
        {
            return value;
        }

        async Task<int[]> IPedantic.TestParams(params int[] values)
        {
            return values;
        }

        async Task<string> IPedantic.TestPassClass(TestParam param)
        {
            return $"{param.Name}:{param.Price}";
        }

        async Task<TestResult> IPedantic.TestReturnClass(int value, int offset)
        {
            return new TestResult { Value = value, Offset = offset };
        }
    }
}

#pragma warning restore 1998
