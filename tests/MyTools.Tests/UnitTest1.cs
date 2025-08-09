using MyTools.Core;

namespace MyTools.Tests;

public class CalculatorTests
{
    private readonly Calculator _calc = new();

    [Fact]
    public void Add_Works()
    {
        Assert.Equal(5, _calc.Add(2, 3));
    }

    [Fact]
    public void Subtract_Works()
    {
        Assert.Equal(-1, _calc.Subtract(2, 3));
    }

    [Fact]
    public void Multiply_Works()
    {
        Assert.Equal(6, _calc.Multiply(2, 3));
    }

    [Fact]
    public void Divide_Works()
    {
        Assert.Equal(2, _calc.Divide(6, 3));
    }

    [Fact]
    public void Divide_ByZero_Throws()
    {
        Assert.Throws<DivideByZeroException>(() => _calc.Divide(1, 0));
    }
}

public class ExpressionEvaluatorTests
{
    private readonly ExpressionEvaluator _eval = new(new Calculator());

    [Theory]
    [InlineData("1+2*3", 7)]
    [InlineData("(1+2)*3", 9)]
    [InlineData("-5+2", -3)]
    [InlineData("2^3", 8)]
    [InlineData("4/2+6", 8)]
    [InlineData("10-2*3", 4)]
    public void Evaluate_Expressions(string expr, double expected)
        => Assert.Equal(expected, _eval.Evaluate(expr));
}
