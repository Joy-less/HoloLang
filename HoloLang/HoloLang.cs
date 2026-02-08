using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace HoloLang;

/*public static class Result {
    public static Success Success() {
        return new Success();
    }
    public static ResultValue<T> FromValue<T>(T Value) {
        return new ResultValue<T>(Value);
    }
    public static ResultError<E> FromError<E>(E Error) {
        return new ResultError<E>(Error);
    }
}*/
public readonly struct Success {
}
/*public readonly struct ResultValue<T> {
    public T Value { get; }

    public ResultValue(T Value) {
        this.Value = Value;
    }
}
public readonly struct ResultError<E> {
    public E Error { get; }

    public ResultError(E Error) {
        this.Error = Error;
    }
}*/
public readonly struct Result<T, E> {
    public T? ValueOrDefault { get; }
    [MemberNotNullWhen(true, nameof(ErrorOrDefault))]
    public bool IsError { get; }
    public E? ErrorOrDefault { get; }

    [MemberNotNullWhen(true, nameof(ValueOrDefault))]
    public bool IsValue => !IsError;
    public T Value => IsValue ? ValueOrDefault : throw new InvalidOperationException($"Result was error: {ErrorOrDefault}");
    public E Error => IsError ? ErrorOrDefault : throw new InvalidOperationException($"Result was value: {ValueOrDefault}");

    private Result(bool IsError, E? ErrorOrDefault, T? ValueOrDefault) {
        this.IsError = IsError;
        this.ErrorOrDefault = ErrorOrDefault;
        this.ValueOrDefault = ValueOrDefault;
    }

    public override string ToString() {
        return IsError
            ? $"Error: {ErrorOrDefault}"
            : $"Value: {ValueOrDefault}";
    }
    public void ThrowIfError() {
        if (IsError) {
            throw (ErrorOrDefault as Exception) ?? new Exception(ErrorOrDefault.ToString());
        }
    }

    public static Result<Success, E> FromSuccess() {
        return new Result<Success, E>(false, default, default);
    }
    public static Result<T, E> FromValue(T Value) {
        return new Result<T, E>(false, default, Value);
    }
    public static Result<T, E> FromError(E Error) {
        return new Result<T, E>(true, Error, default);
    }

    /*public bool TryGetValue([NotNullWhen(true)] out T? Value, [NotNullWhen(false)] out E? Error) {
        Value = ValueOrDefault;
        Error = ErrorOrDefault;
        return IsValue;
    }*/

    public static implicit operator Result<T, E>(Success ResultSuccess) {
        _ = ResultSuccess;
        return new Result<T, E>(true, default, default);
    }
    /*public static implicit operator Result<T, E>(ResultValue<T> ResultValue) {
        return new Result<T, E>(true, default, ResultValue.Value);
    }
    public static implicit operator Result<T, E>(ResultError<E> ResultError) {
        return new Result<T, E>(true, ResultError.Error, default);
    }*/
    /*public static implicit operator Result<T, E>(T Value) {
        return new Result<T, E>(false, default, Value);
    }*/
}

public sealed class Parser {
    public string Source { get; }
    public int Index { get; private set; }

    private static readonly char[] NewlineChars = ['\n', '\r'];
    private static readonly char[] WhitespaceChars = [' ', '\t', '\v', '\f', ..NewlineChars];

    private Parser(string Source) {
        this.Source = Source;
        Index = 0;
    }

    public static Result<Expression, string> Parse(string Source) {
        Parser Parser = new(Source);

        Result<Expression, string> ExpressionResult = Parser.ParseExpression();
        if (ExpressionResult.IsError) {
            return Result<Expression, string>.FromError(ExpressionResult.Error);
        }

        Result<Success, string> EndOfInputResult = Parser.ParseEndOfInput();
        if (EndOfInputResult.IsError) {
            return Result<Expression, string>.FromError(EndOfInputResult.Error);
        }

        return Result<Expression, string>.FromValue(ExpressionResult.Value);
    }

    private Result<Success, string> ParseEndOfInput() {
        // Whitespace
        ReadWhitespace();

        // Invalid
        if (Index < Source.Length) {
            return Result<Success, string>.FromError($"Expected `;`, got `{Source[Index]}`");
        }
        return Result<Success, string>.FromSuccess();
    }
    private Result<Expression, string> ParseExpression() {
        List<Expression> Expressions = [];

        for (; Index < Source.Length; Index++) {
            // Whitespace
            ReadWhitespace();

            // End of input
            if (Index >= Source.Length) {
                break;
            }

            // String
            if (Source[Index] is '"' or '\'') {
                // Consume string
                int StringStartIndex = Index;
                Result<Success, string> StringResult = ReadString();
                if (StringResult.IsError) {
                    return Result<Expression, string>.FromError(StringResult.Error);
                }
                ReadOnlySpan<char> String = Source.AsSpan(StringStartIndex..Index);

                // Create string expression
                Expressions.Add(new StringExpression(Encoding.UTF8.GetBytes(new string(String)))); // TODO: improve performance of (ReadOnlySpan<char> -> IEnumerable<byte>)
            }
            // Number
            else if (Source[Index] is (>= '0' and <= '9') or '-' or '+') {
                // Consume number
                int NumberStartIndex = Index;
                Result<Success, string> NumberResult = ReadNumber();
                if (NumberResult.IsError) {
                    return Result<Expression, string>.FromError(NumberResult.Error);
                }
                ReadOnlySpan<char> Number = Source.AsSpan(NumberStartIndex..Index);

                // Create real expression
                if (Number.Contains('.')) {
                    Expressions.Add(new RealExpression(double.Parse(Number)));
                }
                // Create integer expression
                else {
                    Expressions.Add(new IntegerExpression(long.Parse(Number)));
                }
            }
            // Identifier
            else if (Source[Index] is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or '_') {
                // Consume identifier
                int IdentifierStartIndex = Index;
                Result<Success, string> IdentifierResult = ReadIdentifier();
                if (IdentifierResult.IsError) {
                    return Result<Expression, string>.FromError(IdentifierResult.Error);
                }
                ReadOnlySpan<char> Identifier = Source.AsSpan(IdentifierStartIndex..Index);

                // Consume whitespace
                ReadWhitespace();

                // Assignment
                if (Source[Index] is '=') {
                    Index++;

                    // Consume expression
                    Result<Expression, string> ValueResult = ParseExpression();
                    if (ValueResult.IsError) {
                        return Result<Expression, string>.FromError(ValueResult.Error);
                    }

                    // Create assign expression
                    Expressions.Add(new AssignExpression(null, new string(Identifier), ValueResult.Value));
                }
                else {
                    // Create get expression
                    Expressions.Add(new GetExpression(null, new string(Identifier)));
                }
            }
            // Box
            else if (Source[Index] is '{') {
                // Consume box expression
                Result<BoxExpression, string> BoxResult = ParseBox();
                if (BoxResult.IsError) {
                    return Result<Expression, string>.FromError(BoxResult.Error);
                }

                // Add box expression
                Expressions.Add(BoxResult.Value);
            }

            // Whitespace
            ReadWhitespace();

            // End of input
            if (Index >= Source.Length) {
                break;
            }

            // Semicolon
            if (Source[Index] is not ';') {
                break;
            }
        }

        if (Expressions.Count == 1) {
            return Result<Expression, string>.FromValue(Expressions[0]);
        }
        return Result<Expression, string>.FromValue(new MultiExpression(Expressions));
    }
    private Result<BoxExpression, string> ParseBox() {
        if (Source[Index] is not '{') {
            return Result<BoxExpression, string>.FromError("Expected `{` to start box");
        }
        Index++;

        Result<Expression, string> ExpressionsResult = ParseExpression();
        if (ExpressionsResult.IsError) {
            return Result<BoxExpression, string>.FromError(ExpressionsResult.Error);
        }

        if (Source[Index] is not '}') {
            return Result<BoxExpression, string>.FromError("Expected `}` to end box");
        }
        Index++;

        BoxExpression BoxExpression = new(ExpressionsResult.Value);
        return Result<BoxExpression, string>.FromValue(BoxExpression);
    }
    private void ReadWhitespace() {
        for (; Index < Source.Length; Index++) {
            if (!WhitespaceChars.Contains(Source[Index])) {
                return;
            }
        }
    }
    private Result<Success, string> ReadString() {
        if (Index >= Source.Length || Source[Index] is not ('"' or '\'')) {
            return Result<Success, string>.FromError("Expected string, got nothing");
        }
        char StartChar = Source[Index];
        Index++;

        for (; Index < Source.Length; Index++) {
            if (Source[Index] == StartChar) {
                Index++;
                return Result<Success, string>.FromSuccess();
            }
        }

        return Result<Success, string>.FromError("Expected end of string, got end of input");
    }
    private Result<Success, string> ReadNumber() {
        if (Index <= Source.Length && Source[Index] is '-' or '+') {
            Index++;
        }

        if (Source[Index] is not (>= '0' and <= '9')) {
            return Result<Success, string>.FromError("Expected digit to start number");
        }
        Index++;

        for (; Index < Source.Length; Index++) {
            if (Source[Index] is (>= '0' and <= '9')) {
                continue;
            }
            if (Source[Index] is '.') {
                if (Source[Index - 1] is not (>= '0' and <= '9')) {
                    return Result<Success, string>.FromError("Expected digit before `.` in number");
                }
                continue;
            }
            if (Source[Index] is '_') {
                if (Source[Index - 1] is not (>= '0' and <= '9')) {
                    return Result<Success, string>.FromError("Expected digit before `_` in number");
                }
                continue;
            }
            break;
        }

        if (Source[Index - 1] is '_') {
            return Result<Success, string>.FromError("Trailing `_` in number");
        }

        return Result<Success, string>.FromSuccess();
    }
    private Result<Success, string> ReadIdentifier() {
        if (Source[Index] is not ((>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or '_')) {
            return Result<Success, string>.FromError("Expected letter or `_` to start identifier");
        }
        Index++;

        for (; Index < Source.Length; Index++) {
            if (Source[Index] is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '_') {
                continue;
            }
            return Result<Success, string>.FromSuccess();
        }

        return Result<Success, string>.FromSuccess();
    }
}

public abstract class Expression {
}
public class MultiExpression : Expression {
    public List<Expression> Expressions { get; set; }

    public MultiExpression(List<Expression> Expressions) {
        this.Expressions = Expressions;
    }
}
public class GetExpression : Expression {
    public Expression? Target { get; set; }
    public string Member { get; set; }

    public GetExpression(Expression? Target, string Member) {
        this.Target = Target;
        this.Member = Member;
    }
}
public class AssignExpression : Expression {
    public Expression? Target { get; set; }
    public string Member { get; set; }
    public Expression Value { get; set; }

    public AssignExpression(Expression? Target, string Member, Expression Value) {
        this.Target = Target;
        this.Member = Member;
        this.Value = Value;
    }
}
public class CallExpression : Expression {
    public Expression Target { get; set; }
    public Expression Argument { get; set; }

    public CallExpression(Expression Target, Expression Argument) {
        this.Target = Target;
        this.Argument = Argument;
    }
}
public class BoxExpression : Expression {
    public Expression? Expression { get; set; }

    public BoxExpression(Expression? Expression) {
        this.Expression = Expression;
    }
}
public class StringExpression : Expression {
    public byte[] String { get; set; }

    public StringExpression(byte[] String) {
        this.String = String;
    }
}
public class IntegerExpression : Expression {
    public long Integer { get; set; }

    public IntegerExpression(long Integer) {
        this.Integer = Integer;
    }
}
public class RealExpression : Expression {
    public double Real { get; set; }

    public RealExpression(double Real) {
        this.Real = Real;
    }
}
public class ExternalCallExpression : Expression {
    public Func<Box[], Box> ExternalFunction { get; set; }

    public ExternalCallExpression(Func<Box[], Box> ExternalFunction) {
        this.ExternalFunction = ExternalFunction;
    }
}

public sealed class Box {
    public const string ComponentsVariableName = "components";
    public const string CallVariableName = "call";
    public const string GetVariableName = "get";

    public Dictionary<string, Box> Variables { get; }
    public BoxMethod Method { get; }
    public object? Data { get; }

    public static Box Null { get; } = new();
    public static Box Boolean { get; } = new();
    public static Box Integer { get; } = new();
    public static Box Real { get; } = new();
    public static Box String { get; } = new();
    public static Box List { get; } = new();
    public static Box Dictionary { get; } = new();

    public Box() {
        Variables = [];
        Method = new BoxMethod();
        Data = null;
    }
    private Box(Dictionary<string, Box> Variables, BoxMethod Method, object? Data) {
        this.Variables = Variables;
        this.Method = Method;
        this.Data = Data;
    }

    public static Box From(Box Components, BoxMethod Method, object? Data) {
        return new Box(new Dictionary<string, Box>() { [ComponentsVariableName] = Components }, Method, Data);
    }
    public static Box FromBoolean(bool BooleanData) {
        return From(FromList(Boolean), new BoxMethod(), BooleanData);
    }
    public static Box FromInteger(long IntegerData) {
        return From(FromList(Integer), new BoxMethod(), IntegerData);
    }
    public static Box FromReal(double RealData) {
        return From(FromList(Real), new BoxMethod(), RealData);
    }
    public static Box FromString(byte[] StringData) {
        return From(FromList(String), new BoxMethod(), StringData);
    }
    public static Box FromString(string StringData) {
        return FromString(Encoding.UTF8.GetBytes(StringData));
    }
    public static Box FromList(params IEnumerable<Box> ListData) {
        return From(List, new BoxMethod(), ListData);
    }
    public static Box FromDictionary(IReadOnlyDictionary<Box, Box> DictionaryData) {
        return From(FromList(Dictionary), new BoxMethod(), DictionaryData);
    }

    public Box? GetVariable(string Name) {
        if (Variables.TryGetValue(Name, out Box? Value)) {
            return Value;
        }
        return null;
    }
    public void SetVariable(string Name, Box? Value) {
        if (Value is null) {
            Variables.Remove(Name);
        }
        else {
            Variables[Name] = Value;
        }
    }
    public IEnumerable<Box>? GetComponents() {
        return GetVariable(ComponentsVariableName)?.Data as IEnumerable<Box>;
    }
    public void SetComponents(IEnumerable<Box> Components) {
        SetVariable(ComponentsVariableName, FromList(Components));
    }
    public BoxMethod GetMethod() {
        Box CurrentBox = this;
        while (true) {
            Box NewBox = CurrentBox.GetVariable(CallVariableName) ?? Null;
            if (NewBox == Null) {
                return CurrentBox.Method;
            }
            CurrentBox = NewBox;
        }
    }
}

public struct BoxMethod {
    public List<string> Parameters { get; set; }
    public Expression? Expression { get; set; }

    public BoxMethod(List<string> Parameters, Expression? Expression) {
        this.Parameters = Parameters;
        this.Expression = Expression;
    }
}

public sealed class Actor {
    private readonly Lock Lock = new();

    public Result<Box, string> Evaluate(Box Target, Expression Expression) {
        Stack<Box> Values = new();
        Values.Push(Box.Null);

        Stack<Frame> Frames = new();
        Frames.Push(new Frame(Target, Expression, 1));

        lock (Lock) {
            while (Frames.TryPeek(out Frame? CurrentFrame)) {
                Result<Success, string> EvaluateFrameResult = EvaluateFrame(Values, Frames, CurrentFrame);
                if (EvaluateFrameResult.IsError) {
                    return Result<Box, string>.FromError(EvaluateFrameResult.Error);
                }
            }

            return Result<Box, string>.FromValue(Values.Pop());
        }
    }

    private static Result<Success, string> EvaluateFrame(Stack<Box> Values, Stack<Frame> Frames, Frame CurrentFrame) {
        switch (CurrentFrame.Expression) {

            case MultiExpression MultiExpression:
                if (CurrentFrame.Counter < MultiExpression.Expressions.Count) {
                    while (Values.Count > CurrentFrame.ValuesCount) {
                        Values.Pop();
                    }
                    Expression NextExpression = MultiExpression.Expressions[CurrentFrame.Counter];
                    Frames.Push(new Frame(CurrentFrame.Target, NextExpression, Values.Count));
                }
                else {
                    Frames.Pop();
                    break;
                }
                break;

            case GetExpression GetExpression:
                if (GetExpression.Target is not null) {
                    switch (CurrentFrame.Counter) {
                        case 0:
                            Frames.Push(new Frame(CurrentFrame.Target, GetExpression.Target, Values.Count));
                            break;
                        case 1:
                            Box GetTarget = Values.Pop();

                            Box? GetValue = GetTarget.GetVariable(GetExpression.Member);
                            if (GetValue is null) {
                                return Result<Success, string>.FromError($"Variable `{GetExpression.Member}` not found");
                            }
                            Values.Push(GetValue);
                            break;
                        default:
                            Frames.Pop();
                            break;
                    }
                }
                else {
                    switch (CurrentFrame.Counter) {
                        case 0:
                            Box? GetValue = CurrentFrame.Target.GetVariable(GetExpression.Member);
                            if (GetValue is null) {
                                return Result<Success, string>.FromError($"Variable `{GetExpression.Member}` not found");
                            }
                            Values.Push(GetValue);
                            break;
                        default:
                            Frames.Pop();
                            break;
                    }
                }
                break;

            case AssignExpression AssignExpression:
                if (AssignExpression.Target is not null) {
                    switch (CurrentFrame.Counter) {
                        case 0:
                            Frames.Push(new Frame(CurrentFrame.Target, AssignExpression.Target, Values.Count));
                            break;
                        case 1:
                            Frames.Push(new Frame(CurrentFrame.Target, AssignExpression.Value, Values.Count));
                            break;
                        case 2:
                            Box AssignValue = Values.Pop();
                            Box AssignTarget = Values.Pop();

                            AssignTarget.SetVariable(AssignExpression.Member, AssignValue);
                            Values.Push(AssignValue);
                            break;
                        default:
                            Frames.Pop();
                            break;
                    }
                }
                else {
                    switch (CurrentFrame.Counter) {
                        case 0:
                            Frames.Push(new Frame(CurrentFrame.Target, AssignExpression.Value, Values.Count));
                            break;
                        case 1:
                            Box AssignValue = Values.Pop();

                            CurrentFrame.Target.SetVariable(AssignExpression.Member, AssignValue);
                            Values.Push(AssignValue);
                            break;
                        default:
                            Frames.Pop();
                            break;
                    }
                }
                break;

            case CallExpression CallExpression:
                switch (CurrentFrame.Counter) {
                    case 0:
                        Frames.Push(new Frame(CurrentFrame.Target, CallExpression.Target, Values.Count));
                        break;
                    case 1:
                        Frames.Push(new Frame(CurrentFrame.Target, CallExpression.Argument, Values.Count));
                        break;
                    case 2:
                        Box CallArgument = Values.Pop();
                        Box CallTarget = Values.Pop();

                        BoxMethod CallMethod = CallTarget.GetMethod();

                        if (CallMethod.Expression is not null) {
                            Box CallScope = Box.From(Box.FromList(CallTarget), CallMethod, null);
                            foreach (string Parameter in CallScope.Method.Parameters) {
                                CallScope.SetVariable(Parameter, CallArgument.GetVariable("get")); // TODO
                            }
                            Frames.Push(new Frame(CallScope, CallScope.Method.Expression!, Values.Count));
                        }
                        break;
                    default:
                        Frames.Pop();
                        break;
                }
                break;

            case BoxExpression BoxExpression:
                switch (CurrentFrame.Counter) {
                    case 0:
                        Box Box = new();
                        Values.Push(Box);
                        if (BoxExpression.Expression is not null) {
                            Frames.Push(new Frame(Box, BoxExpression.Expression, Values.Count));
                        }
                        break;
                    case 1:
                        while (Values.Count > CurrentFrame.ValuesCount + 1) {
                            Values.Pop();
                        }
                        break;
                    default:
                        Frames.Pop();
                        break;
                }
                break;

            case StringExpression StringExpression:
                switch (CurrentFrame.Counter) {
                    case 0:
                        Values.Push(Box.FromString(StringExpression.String));
                        break;
                    default:
                        Frames.Pop();
                        break;
                }
                break;

            case IntegerExpression IntegerExpression:
                switch (CurrentFrame.Counter) {
                    case 0:
                        Values.Push(Box.FromInteger(IntegerExpression.Integer));
                        break;
                    default:
                        Frames.Pop();
                        break;
                }
                break;

            case RealExpression RealExpression:
                switch (CurrentFrame.Counter) {
                    case 0:
                        Values.Push(Box.FromReal(RealExpression.Real));
                        break;
                    default:
                        Frames.Pop();
                        break;
                }
                break;

            case ExternalCallExpression ExternalCallExpression:
                switch (CurrentFrame.Counter) {
                    case 0:
                        throw new NotImplementedException();
                    default:
                        Frames.Pop();
                        break;
                }
                break;

            default:
                return Result<Success, string>.FromError($"Unknown expression: {CurrentFrame.Expression.GetType()}");

        }

        CurrentFrame.Counter++;

        return Result<Success, string>.FromSuccess();
    }

    private sealed class Frame {
        public Box Target { get; set; }
        public Expression Expression { get; set; }
        public int Counter { get; set; }
        public int ValuesCount { get; set; }

        public Frame(Box Target, Expression Expression, int ValuesCount) {
            this.Target = Target;
            this.Expression = Expression;
            Counter = 0;
            this.ValuesCount = ValuesCount;
        }
    }
}