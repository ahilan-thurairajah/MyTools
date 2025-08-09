using System.Globalization;
using System.Text.RegularExpressions;

namespace MyTools.Core;

public class ExpressionEvaluator
{
    private readonly ICalculator _calc;
    private readonly Dictionary<string, Func<double[], double>> _funcs;
    public ExpressionEvaluator(ICalculator calc)
    {
        _calc = calc;
        _funcs = new(StringComparer.OrdinalIgnoreCase)
        {
            ["sin"] = a => Math.Sin(a[0]),
            ["cos"] = a => Math.Cos(a[0]),
            ["tan"] = a => Math.Tan(a[0]),
            ["asin"] = a => Math.Asin(a[0]),
            ["acos"] = a => Math.Acos(a[0]),
            ["atan"] = a => Math.Atan(a[0]),
            ["sqrt"] = a => Math.Sqrt(a[0]),
            ["abs"] = a => Math.Abs(a[0]),
            ["ln"] = a => Math.Log(a[0]),
            ["log"] = a => Math.Log10(a[0]),
            ["pow"] = a => Math.Pow(a[0], a[1]),
            ["min"] = a => a.Min(),
            ["max"] = a => a.Max(),
            ["pct"] = a => a[0] / 100.0,
            // Financial functions (Excel-compatible-ish)
            ["pmt"] = a =>
            {
                // pmt(rate, nper, pv, fv=0, type=0)
                var rate = a[0]; var nper = a[1]; var pv = a[2];
                var fv = a.Length > 3 ? a[3] : 0.0; var type = a.Length > 4 ? a[4] : 0.0;
                if (Math.Abs(rate) < 1e-12)
                    return -(pv + fv) / nper;
                var r1 = Math.Pow(1 + rate, nper);
                return -(rate * (pv * r1 + fv)) / ((1 + rate * type) * (r1 - 1));
            },
            ["fv"] = a =>
            {
                // fv(rate, nper, pmt, pv=0, type=0)
                var rate = a[0]; var nper = a[1]; var pmt = a[2];
                var pv = a.Length > 3 ? a[3] : 0.0; var type = a.Length > 4 ? a[4] : 0.0;
                if (Math.Abs(rate) < 1e-12)
                    return -(pv + pmt * nper);
                var r1 = Math.Pow(1 + rate, nper);
                return -(pv * r1 + pmt * (1 + rate * type) * (r1 - 1) / rate);
            },
            ["pv"] = a =>
            {
                // pv(rate, nper, pmt, fv=0, type=0)
                var rate = a[0]; var nper = a[1]; var pmt = a[2];
                var fv = a.Length > 3 ? a[3] : 0.0; var type = a.Length > 4 ? a[4] : 0.0;
                if (Math.Abs(rate) < 1e-12)
                    return -(fv + pmt * nper);
                var r1 = Math.Pow(1 + rate, nper);
                return -(fv + pmt * (1 + rate * type) * (r1 - 1) / rate) / r1;
            },
            ["nper"] = a =>
            {
                // nper(rate, pmt, pv, fv=0, type=0)
                var rate = a[0]; var pmt = a[1]; var pv = a[2];
                var fv = a.Length > 3 ? a[3] : 0.0; var type = a.Length > 4 ? a[4] : 0.0;
                if (Math.Abs(rate) < 1e-12)
                    return -(pv + fv) / pmt;
                return Math.Log((pmt * (1 + rate * type) - fv * rate) / (pmt * (1 + rate * type) + pv * rate)) / Math.Log(1 + rate);
            }
        };
    }

    public double Evaluate(string expression)
    {
        if (expression is null) throw new ArgumentNullException(nameof(expression));
        var tokens = Tokenize(expression);
        var rpn = ToRpn(tokens);
        return EvalRpn(rpn);
    }

    private static bool IsIdentStart(char c) => char.IsLetter(c) || c == '_';
    private static bool IsIdentPart(char c) => char.IsLetterOrDigit(c) || c == '_';

    private List<string> Tokenize(string expr)
    {
        expr = expr.Replace('×', '*').Replace('÷', '/');
        var tokens = new List<string>();
        int i = 0;
        while (i < expr.Length)
        {
            var ch = expr[i];
            if (char.IsWhiteSpace(ch)) { i++; continue; }

            if (char.IsDigit(ch) || ch == '.')
            {
                int start = i;
                i++;
                while (i < expr.Length && (char.IsDigit(expr[i]) || expr[i] == '.')) i++;
                tokens.Add(expr[start..i]);
                continue;
            }

            if (IsIdentStart(ch))
            {
                int start = i;
                i++;
                while (i < expr.Length && IsIdentPart(expr[i])) i++;
                var ident = expr[start..i];
                // constants
                if (string.Equals(ident, "pi", StringComparison.OrdinalIgnoreCase)) { tokens.Add(Math.PI.ToString(CultureInfo.InvariantCulture)); continue; }
                if (string.Equals(ident, "e", StringComparison.OrdinalIgnoreCase)) { tokens.Add(Math.E.ToString(CultureInfo.InvariantCulture)); continue; }
                // function? if followed by '('
                if (i < expr.Length && expr[i] == '(')
                {
                    tokens.Add($"fn:{ident}");
                    continue;
                }
                throw new FormatException($"Unknown identifier '{ident}'.");
            }

            if (ch == ',') { tokens.Add(","); i++; continue; }

            if (ch is '(' or ')')
            {
                tokens.Add(ch.ToString());
                i++;
                continue;
            }

            if (IsOperatorChar(ch))
            {
                // Handle unary minus/plus
                var isUnary = tokens.Count == 0 || tokens[^1] is "(" || IsOperator(tokens[^1]);
                if (isUnary && (ch == '-' || ch == '+'))
                {
                    tokens.Add(ch == '-' ? "u-" : "u+");
                }
                else
                {
                    tokens.Add(ch.ToString());
                }
                i++;
                continue;
            }

            throw new FormatException($"Unexpected character '{ch}' at position {i}.");
        }
        return tokens;
    }

    private static bool IsOperatorChar(char ch) => ch is '+' or '-' or '*' or '/' or '^';
    private static bool IsOperator(string t) => t is "+" or "-" or "*" or "/" or "^" or "u-" or "u+";

    private static int Precedence(string op) => op switch
    {
        "u-" or "u+" => 4,
        "^" => 3,
        "*" or "/" => 2,
        "+" or "-" => 1,
        _ => 0
    };

    private static bool IsRightAssociative(string op) => op == "^" || op == "u-" || op == "u+";

    private Queue<string> ToRpn(List<string> tokens)
    {
        var output = new Queue<string>();
        var ops = new Stack<string>();
        var argCount = new Stack<int?>(); // null when not a function-call paren, otherwise tracks args
        foreach (var t in tokens)
        {
            if (double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
            {
                output.Enqueue(t);
            }
            else if (t.StartsWith("fn:", StringComparison.Ordinal))
            {
                var name = t.Substring(3);
                if (!_funcs.ContainsKey(name)) throw new FormatException($"Unknown function '{name}'.");
                ops.Push(t); // push function marker; expect next token is '('
            }
            else if (IsOperator(t))
            {
                while (ops.Count > 0 && IsOperator(ops.Peek()))
                {
                    var top = ops.Peek();
                    if ((IsRightAssociative(t) && Precedence(t) < Precedence(top)) ||
                        (!IsRightAssociative(t) && Precedence(t) <= Precedence(top)))
                    {
                        output.Enqueue(ops.Pop());
                    }
                    else break;
                }
                ops.Push(t);
            }
            else if (t == "(")
            {
                // If top of ops is a function marker, this '(' starts its argument list
                if (ops.Count > 0 && ops.Peek().StartsWith("fn:", StringComparison.Ordinal))
                {
                    ops.Push(t);
                    argCount.Push(1); // at least one arg unless we see immediate ')', we will adjust below
                }
                else
                {
                    ops.Push(t);
                    argCount.Push(null);
                }
            }
            else if (t == ")")
            {
                while (ops.Count > 0 && ops.Peek() != "(")
                {
                    output.Enqueue(ops.Pop());
                }
                if (ops.Count == 0) throw new FormatException("Mismatched parentheses.");
                ops.Pop(); // remove '('
                var argc = argCount.Pop();
                // If a function is on stack, pop it and emit a function token with argc (but handle zero-arg)
                if (ops.Count > 0 && ops.Peek().StartsWith("fn:", StringComparison.Ordinal))
                {
                    var fn = ops.Pop().Substring(3);
                    var count = argc ?? 0; // if null, it's just grouping parens
                    // handle empty arg list 'fn()' -> argc 0
                    if (count == 1)
                    {
                        // detect empty: previous token was '(' directly
                        // We can't easily detect here; accept 1
                    }
                    output.Enqueue($"FN:{fn}:{count}");
                }
            }
            else if (t == ",")
            {
                // function argument separator
                while (ops.Count > 0 && ops.Peek() != "(")
                {
                    output.Enqueue(ops.Pop());
                }
                if (argCount.Count == 0 || argCount.Peek() is null)
                    throw new FormatException("Misplaced comma.");
                argCount.Push((argCount.Pop() ?? 0) + 1);
            }
            else
            {
                throw new FormatException($"Unexpected token '{t}'.");
            }
        }
        while (ops.Count > 0)
        {
            var op = ops.Pop();
            if (op == "(" || op == ")") throw new FormatException("Mismatched parentheses.");
            if (op.StartsWith("fn:", StringComparison.Ordinal))
                throw new FormatException("Function call missing parentheses.");
            output.Enqueue(op);
        }
        return output;
    }

    private double EvalRpn(Queue<string> rpn)
    {
        var stack = new Stack<double>();
        while (rpn.Count > 0)
        {
            var t = rpn.Dequeue();
            if (double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var num))
            {
                stack.Push(num);
            }
            else if (IsOperator(t))
            {
                if (t is "u-" or "u+")
                {
                    if (stack.Count < 1) throw new FormatException("Invalid expression.");
                    var a = stack.Pop();
                    stack.Push(t == "u-" ? -a : a);
                }
                else
                {
                    if (stack.Count < 2) throw new FormatException("Invalid expression.");
                    var b = stack.Pop();
                    var a = stack.Pop();
                    var res = t switch
                    {
                        "+" => _calc.Add(a, b),
                        "-" => _calc.Subtract(a, b),
                        "*" => _calc.Multiply(a, b),
                        "/" => _calc.Divide(a, b),
                        "^" => Math.Pow(a, b),
                        _ => throw new FormatException($"Unknown operator '{t}'.")
                    };
                    stack.Push(res);
                }
            }
            else if (t.StartsWith("FN:", StringComparison.Ordinal))
            {
                var parts = t.Split(':');
                var name = parts[1];
                var argc = int.Parse(parts[2], CultureInfo.InvariantCulture);
                if (!_funcs.TryGetValue(name, out var fn))
                    throw new FormatException($"Unknown function '{name}'.");
                if (stack.Count < argc) throw new FormatException("Invalid function arguments.");
                var args = new double[argc];
                for (int i = argc - 1; i >= 0; i--) args[i] = stack.Pop();
                var res = fn(args);
                stack.Push(res);
            }
            else
            {
                throw new FormatException($"Unexpected token '{t}'.");
            }
        }
        if (stack.Count != 1) throw new FormatException("Invalid expression.");
        return stack.Pop();
    }
}
