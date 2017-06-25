using System;
using System.IO;
using System.Linq;

namespace Pidgin.CodeGen
{
    static class MapGenerator
    {
        public static void Generate()
        {
            File.WriteAllText("Pidgin/Parser.Map.Generated.cs", GenerateClassPart());
        }

        private static string GenerateClassPart()
        {
            var methodsAndClasses = Enumerable.Range(1, 8).Select(n => GenerateMethodAndClass(n));

            return $@"#region GeneratedCode
using System;
using System.Collections.Generic;
using Pidgin.ParseStates;

namespace Pidgin
{{
    // Generated by Pidgin.CodeGen.
    // Each of these methods is equivalent to
    //     return
    //         from x1 in p1
    //         from x2 in p2
    //         ...
    //         from xn in pn
    //         select func(x1, x2, ..., xn)
    // but this lower-level approach saves on allocations
    public static partial class Parser
    {{
        private abstract class MapParserBase<TToken, T> : Parser<TToken, T>
        {{
            protected MapParserBase(SortedSet<Expected<TToken>> expected) : base(expected)
            {{
            }}

            internal abstract MapParserBase<TToken, U> Map<U>(Func<T, U> func);
        }}

        {string.Join(Environment.NewLine, methodsAndClasses)}
    }}
}}
#endregion
";
        }


        private static string GenerateMethodAndClass(int num)
        {
            var nums = Enumerable.Range(1, num);
            var parserParams = nums.Select(n => $"Parser<TToken, T{n}> parser{n}");
            var parserFields = nums.Select(n => $"private readonly Parser<TToken, T{n}> _p{n};");
            var parserParamNames = nums.Select(n => $"parser{n}");
            var parserFieldNames = nums.Select(n => $"_p{n}");
            var parserFieldAssignments = nums.Select(n => $"_p{n} = parser{n};");
            var results = nums.Select(n => $"result{n}.GetValueOrDefault()");
            var types = string.Join(", ", nums.Select(n => "T" + n));
            var parts = nums.Select(GenerateMethodBodyPart);
            var mapMethodBody = num == 1
                ? $@"parser1 is MapParserBase<TToken, T1> p
                ? p.Map(func)
                : new Map{num}Parser<TToken, {types}, R>(func, {string.Join(", ", parserParamNames)})"
                : $"new Map{num}Parser<TToken, {types}, R>(func, {string.Join(", ", parserParamNames)})";
            var funcArgNames = nums.Select(n => "x" + n);

            var typeParamDocs = nums.Select(n => $"<typeparam name=\"T{n}\">The return type of the {EnglishNumber(n)} parser</typeparam>");
            var paramDocs = nums.Select(n => $"<param name=\"parser{n}\">The {EnglishNumber(n)} parser</param>");


            return $@"
        /// <summary>
        /// Creates a parser that applies the specified parsers sequentially and applies the specified transformation function to their results.
        /// </summary>
        /// <param name=""func"">A function to apply to the return values of the specified parsers</param>
        /// {string.Join($"{Environment.NewLine}        /// ", paramDocs)}
        /// <typeparam name=""TToken"">The type of tokens in the parser's input stream</typeparam>
        /// {string.Join($"{Environment.NewLine}        ///", typeParamDocs)}
        /// <typeparam name=""R"">The return type of the resulting parser</typeparam>
        public static Parser<TToken, R> Map<TToken, {types}, R>(
            Func<{types}, R> func,
            {string.Join($",{Environment.NewLine}            ", parserParams)}
        ) => {mapMethodBody};
        
        private sealed class Map{num}Parser<TToken, {types}, R> : MapParserBase<TToken, R>
        {{
            private readonly Func<{types}, R> _func;
            {string.Join($"{Environment.NewLine}            ", parserFields)}

            public Map{num}Parser(
                Func<{types}, R> func,
                {string.Join($",{Environment.NewLine}                ", parserParams)}
            ) : base(ExpectedUtil.Concat({string.Join(", ", parserParamNames.Select(n => $"{n}.Expected"))}))
            {{
                _func = func;
                {string.Join($"{Environment.NewLine}                ", parserFieldAssignments)}
            }}

            internal sealed override Result<TToken, R> Parse(IParseState<TToken> state)
            {{
                var consumedInput = false;

                {string.Join(Environment.NewLine, parts)}

                return Result.Success<TToken, R>(_func(
                    {string.Join($",{Environment.NewLine}                    ", results)}
                ), consumedInput);
            }}

            internal override MapParserBase<TToken, U> Map<U>(Func<R, U> func)
                => new Map{num}Parser<TToken, {types}, U>(
                    ({string.Join(", ", funcArgNames)}) => func(_func({string.Join(", ", funcArgNames)})),
                    {string.Join($",{Environment.NewLine}                    ", parserFieldNames)}
                );
        }}";
        }

        private static string GenerateMethodBodyPart(int num)
            => $@"
                var result{num} = _p{num}.Parse(state);
                consumedInput = consumedInput || result{num}.ConsumedInput;
                if (!result{num}.Success)
                {{
                    return Result.Failure<TToken, R>(
                        result{num}.Error.WithExpected(Expected),
                        consumedInput
                    );
                }}";
        
        private static string EnglishNumber(int num)
        {
            switch (num)
            {
                case 1: return "first";
                case 2: return "second";
                case 3: return "third";
                case 4: return "fourth";
                case 5: return "fifth";
                case 6: return "sixth";
                case 7: return "seventh";
                case 8: return "eighth";
            }
            throw new ArgumentOutOfRangeException(nameof(num));
        }
    }
}
