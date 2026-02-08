using HoloLang;

// Parse

Expression Expression = Parser.Parse("""
    "hello there";
    4.6;

    {
        a = 3;
    };
    """).Value;

_ = Expression;

// Evaluate

Actor Actor = new();
Box Target = new();

Result<Box, string> Result = Actor.Evaluate(Target, Expression);
_ = Result;