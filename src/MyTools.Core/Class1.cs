namespace MyTools.Core;

public interface ICalculator
{
	double Add(double a, double b);
	double Subtract(double a, double b);
	double Multiply(double a, double b);
	double Divide(double a, double b);
}

public class Calculator : ICalculator
{
	public double Add(double a, double b) => a + b;

	public double Subtract(double a, double b) => a - b;

	public double Multiply(double a, double b) => a * b;

	public double Divide(double a, double b)
	{
		if (b == 0)
			throw new DivideByZeroException("Cannot divide by zero.");
		return a / b;
	}
}
