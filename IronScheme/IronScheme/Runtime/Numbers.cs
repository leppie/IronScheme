#region License
/* ****************************************************************************
 * Copyright (c) Llewellyn Pritchard. 
 *
 * This source code is subject to terms and conditions of the Microsoft Public License. 
 * A copy of the license can be found in the License.html file at the root of this distribution. 
 * By using this source code in any fashion, you are agreeing to be bound by the terms of the 
 * Microsoft Public License.
 *
 * You must not remove this notice, or any other, from this software.
 * ***************************************************************************/
#endregion

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Scripting.Math;
using System.Reflection;
using Microsoft.Scripting.Utils;
using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Diagnostics;
using IronScheme.Compiler.Numbers;
using System.Globalization;
using Microsoft.Scripting;

namespace IronScheme.Runtime
{
  public partial class Builtins
  {
    [Builtin("decompose-flonum")]
    public static object DecomposeFlonum(object flonum)
    {
      double d = (double)flonum;

      BigInteger mantissa;
      BigInteger exponent;
      bool res = d.GetMantissaAndExponent(out mantissa, out exponent);

      if (res)
      {
        return Values(flonum, mantissa, exponent);
      }
      else
      {
        return FALSE;
      }
    }


    [Builtin("string->number", AllowConstantFold = true)]
    public static object StringToNumber(object obj)
    {
      string str = RequiresNotNull<string>(obj);

      if (str.Length == 0)
      {
        return FALSE;
      }

      Parser number_parser = new Parser();
      Scanner number_scanner = new Scanner();

      number_parser.scanner = number_scanner;
      number_scanner.SetSource(str,0);
      number_parser.result = null;
      number_scanner.yy_push_state(3);

      try
      {
        if (number_parser.Parse())
        {
          Debug.Assert(number_parser.result != null);
          return number_parser.result;
        }
        else
        {
          return FALSE;
        }
      }
      catch
      {
        return FALSE;
      }
    }

    [Builtin("string->number", AllowConstantFold = true)]
    public static object StringToNumber(object obj, object radix)
    {
      string str = RequiresNotNull<string>(obj);
      radix = radix ?? 10;
      int r = (int)radix;

      if (str.Length == 0)
      {
        return FALSE;
      }

      switch (r)
      {
        case 2:
          return StringToNumber("#b" + str);
        case 8:
          return StringToNumber("#o" + str);
        case 10:
          return StringToNumber(str);
        case 16:
          return StringToNumber("#x" + str);
        default:
          return FALSE;

      }
    }

    [Builtin("inexact=?", AllowConstantFold = true)]
    public static object InexactEqual(object a, object b)
    {
      return GetBool(ConvertToComplex(a) == ConvertToComplex(b));
    }

    [Builtin("exact-compare", AllowConstantFold = true)]
    public static object ExactCompare(object a, object b)
    {
      NumberClass f = GetNumberClass(a);
      NumberClass s = GetNumberClass(b);

      NumberClass effective = f & s;

      switch (effective)
      {
        case NumberClass.Integer:
          return RuntimeHelpers.Int32ToObject( ((int)a).CompareTo((int)b));
        case NumberClass.BigInteger:
          return RuntimeHelpers.Int32ToObject( ConvertToBigInteger(a).CompareTo(ConvertToBigInteger(b)));
        case NumberClass.Rational:
          return RuntimeHelpers.Int32ToObject( ConvertToRational(a).CompareTo(ConvertToRational(b)));

        default:
          return AssertionViolation("exact-compare", "not exact", a, b);
      }
    }

    [Builtin("inexact-compare", AllowConstantFold = true)]
    public static object InexactCompare(object a, object b)
    {
      NumberClass f = GetNumberClass(a);
      NumberClass s = GetNumberClass(b);

      NumberClass effective = f & s;

      switch (effective)
      {
        case NumberClass.Real:
          return RuntimeHelpers.Int32ToObject( ((double)a).CompareTo(b));
        default:
          return AssertionViolation("inexact-compare", "not a real", a, b);
      }
    }



    #region math

    //[Builtin("+", AllowConstantFold = true)]
    //public static object Add()
    //{
    //  return RuntimeHelpers.Int32ToObject(0);
    //}

    //[Builtin("+", AllowConstantFold = true)]
    //public static object Add(object first)
    //{
    //  if (GetNumberClass(first) != NumberClass.NotANumber)
    //  {
    //    return first;
    //  }
    //  else
    //  {
    //    return AssertionViolation("+", "not a number", first);
    //  }
    //}

    enum NumberClass
    {
      Complex = 1,
      Real = 2 | Complex,
      Rational = 4 | Real ,
      BigInteger = 8 | Rational,
      Integer = 16 | BigInteger,
      NotANumber = 0
    }

    static NumberClass GetNumberClass(object obj)
    {
      if (obj is int)
      {
        return NumberClass.Integer;
      }
      else if (obj is BigInteger)
      {
        return NumberClass.BigInteger;
      }
      else if (obj is Fraction)
      {
        return NumberClass.Rational;
      }
      else if (obj is double)
      {
        return NumberClass.Real;
      }
      else if (obj is Complex64)
      {
        return NumberClass.Complex;
      }
      else
      {
        return NumberClass.NotANumber;
      }
    }

    protected static int ConvertToInteger(object o)
    {
      if (o is int)
      {
        return (int)o;
      }
      return (int)AssertionViolation(GetCaller(), "not an integer", o);
    }

    protected internal static BigInteger ConvertToBigInteger(object o)
    {
      if (o is int)
      {
        return (int)o;
      }
      if (o is BigInteger)
      {
        return (BigInteger)o;
      }
      return (BigInteger) AssertionViolation(GetCaller(), "not a big integer", o);
    }

    static Fraction ConvertToRational(object o)
    {
      if (o is Fraction)
      {
        return (Fraction)o;
      }
      if (o is BigInteger)
      {
        return new Fraction((BigInteger)o, 1);
      }
      if (o is double)
      {
        return (Fraction)(double)o;
      }
      return (Fraction)FractionConverter.ConvertFrom(o);
    }

    protected static double ConvertToReal(object o)
    {
      return SafeConvert(o);
    }

    protected static Complex64 ConvertToComplex(object o)
    {
      if (o is Complex64)
      {
        return (Complex64)o;
      }
      else if (o is ComplexFraction)
      {
        return (ComplexFraction)o;
      }
      else
      {
        return Complex64.MakeReal(ConvertToReal(o));
      }
    }

    [Builtin("generic+", AllowConstantFold = true)]
    //[Builtin("+", AllowConstantFold = true)]
    public static object Add(object first, object second)
    {
      NumberClass f = GetNumberClass(first);

      if (f == NumberClass.NotANumber)
      {
        return AssertionViolation("+", "not a number", first);
      }
      
      NumberClass s = GetNumberClass(second);

      if (s == NumberClass.NotANumber)
      {
        return AssertionViolation("+", "not a number", second);
      }

      NumberClass effective = f & s;

      switch (effective)
      {
        case NumberClass.Integer:
          if (avoidoverflow || overflowcount > 10)
          {
            goto case NumberClass.BigInteger;
          }
          try
          {
            return checked(ConvertToInteger(first) + ConvertToInteger(second));
          }
          catch (OverflowException)
          {
            overflowcount++;
            avoidoverflow = true;
            return ConvertToBigInteger(first) + ConvertToBigInteger(second);
          }
        case NumberClass.BigInteger:
          return ToIntegerIfPossible(ConvertToBigInteger(first) + ConvertToBigInteger(second));
        case NumberClass.Rational:
          return IntegerIfPossible(ConvertToRational(first) + ConvertToRational(second));
        case NumberClass.Real:
          return ConvertToReal(first) + ConvertToReal(second);
        case NumberClass.Complex:
          return ConvertToComplex(first) + ConvertToComplex(second);
      }

      throw new NotImplementedException();
    }

    //[Builtin("+", AllowConstantFold = true)]
    //public static object Add(object car, params object[] args)
    //{
    //  for (int i = 0; i < args.Length; i++)
    //  {
    //    car = Add(car, args[i]); 
    //  }

    //  return car;
    //}

    //[Builtin("-", AllowConstantFold = true)]
    //public static object Subtract(object first)
    //{
    //  if (first is int)
    //  {
    //    int fi = (int)first;
    //    if (fi == int.MinValue)
    //    {
    //      return - (BigInteger)fi;
    //    }
    //    else
    //    {
    //      return -fi;
    //    }
    //  }
    //  if (first is double)
    //  {
    //    return -(double)first;
    //  }
    //  return Subtract(RuntimeHelpers.Int32ToObject(0), first);
    //}

    [Builtin("generic-", AllowConstantFold = true)]
    //[Builtin("-", AllowConstantFold = true)]
    public static object Subtract(object first, object second)
    {
      NumberClass f = GetNumberClass(first);

      if (f == NumberClass.NotANumber)
      {
        return AssertionViolation("-", "not a number", first);
      }

      NumberClass s = GetNumberClass(second);

      if (s == NumberClass.NotANumber)
      {
        return AssertionViolation("-", "not a number", second);
      }

      NumberClass effective = f & s;

      switch (effective)
      {
        case NumberClass.Integer:
          if (avoidoverflow || overflowcount > 25)
          {
            goto case NumberClass.BigInteger;
          }
          try
          {
            return checked(ConvertToInteger(first) - ConvertToInteger(second));
          }
          catch (OverflowException)
          {
            overflowcount++;
            avoidoverflow = true;
            return ConvertToBigInteger(first) - ConvertToBigInteger(second);
          }
        case NumberClass.BigInteger:
          return ToIntegerIfPossible(ConvertToBigInteger(first) - ConvertToBigInteger(second));
        case NumberClass.Rational:
          return IntegerIfPossible(ConvertToRational(first) - ConvertToRational(second));
        case NumberClass.Real:
          return ConvertToReal(first) - ConvertToReal(second);
        case NumberClass.Complex:
          return ConvertToComplex(first) - ConvertToComplex(second);
      }

      throw new NotImplementedException();
    }

    //[Builtin("-", AllowConstantFold = true)]
    //public static object Subtract(object car, params object[] args)
    //{
    //  for (int i = 0; i < args.Length; i++)
    //  {
    //    car = Subtract(car, args[i]);
    //  }

    //  return car;
    //}

    //[Builtin("*", AllowConstantFold = true)]
    //public static object Multiply()
    //{
    //  return RuntimeHelpers.Int32ToObject(1);
    //}

    //[Builtin("*", AllowConstantFold = true)]
    //public static object Multiply(object first)
    //{
    //  if (GetNumberClass(first) != NumberClass.NotANumber)
    //  {
    //    return first;
    //  }
    //  else
    //  {
    //    return AssertionViolation("*", "not a number", first);
    //  }
    //}

    static bool avoidoverflow = false;
    static int overflowcount = 0;

    [Builtin("generic*", AllowConstantFold = true)]
    //[Builtin("*", AllowConstantFold = true)]
    public static object Multiply(object first, object second)
    {
      NumberClass f = GetNumberClass(first);

      if (f == NumberClass.NotANumber)
      {
        return AssertionViolation("*", "not a number", first);
      }

      NumberClass s = GetNumberClass(second);

      if (s == NumberClass.NotANumber)
      {
        return AssertionViolation("*", "not a number", second);
      }

      NumberClass effective = f & s;

      switch (effective)
      {
        case NumberClass.Integer:
          if (avoidoverflow || overflowcount > 25)
          {
            goto case NumberClass.BigInteger;
          }
          try
          {
            return checked(ConvertToInteger(first) * ConvertToInteger(second));
          }
          catch (OverflowException)
          {
            overflowcount++;
            avoidoverflow = true;
            return ConvertToBigInteger(first) * ConvertToBigInteger(second);
          }
        case NumberClass.BigInteger:
          return ToIntegerIfPossible(ConvertToBigInteger(first) * ConvertToBigInteger(second));
        case NumberClass.Rational:
          return IntegerIfPossible(ConvertToRational(first) * ConvertToRational(second));
        case NumberClass.Real:
          return ConvertToReal(first) * ConvertToReal(second);
        case NumberClass.Complex:
          return ConvertToComplex(first) * ConvertToComplex(second);
      }

      throw new NotImplementedException();
    }

    protected internal static object ToIntegerIfPossible(BigInteger i)
    {
      if (i <= int.MaxValue && i >= int.MinValue)
      {
        avoidoverflow = false;
        return (int)i;
      }
      else
      {
        return i;
      }
    }

    //[Builtin("*", AllowConstantFold = true)]
    //public static object Multiply(object car, params object[] args)
    //{
    //  for (int i = 0; i < args.Length; i++)
    //  {
    //    car = Multiply(car, args[i]);
    //  }

    //  return car;
    //}

    static object ConvertNumber(object result, Type type)
    {
      try
      {
        return Convert.ChangeType(result, type);
      }
      catch (OverflowException)
      {
        if (type == typeof(int) || type == typeof(long))
        {
          return BigIntConverter.ConvertFrom(result);
        }

        throw;
      }
    }

    static TypeConverter BigIntConverter = TypeDescriptor.GetConverter(typeof(BigInteger));

    //[Builtin("/", AllowConstantFold = true)]
    //public static object Divide(object first)
    //{
    //  return Divide(RuntimeHelpers.Int32ToObject(1), first);
    //}

    [Builtin("generic/", AllowConstantFold = true)]
    //[Builtin("/", AllowConstantFold = true)]
    public static object Divide(object first, object second)
    {
      NumberClass f = GetNumberClass(first);

      if (f == NumberClass.NotANumber)
      {
        return AssertionViolation("/", "not a number", first);
      }

      NumberClass s = GetNumberClass(second);

      if (s == NumberClass.NotANumber)
      {
        return AssertionViolation("/", "not a number", second);
      }

      NumberClass effective = f & s;

      if (IsTrue(IsZero(first)) && IsTrue(IsZero(second)))
      {
        if (effective == NumberClass.BigInteger || effective == NumberClass.Integer)
        {
          return AssertionViolation("/", "divide by zero", first, second);
        }
        return double.NaN;
      }

      try
      {
        switch (effective)
        {
          case NumberClass.Integer:
          case NumberClass.BigInteger:
            return IntegerIfPossible(new Fraction(ConvertToBigInteger(first), ConvertToBigInteger(second)));
          case NumberClass.Rational:
            return IntegerIfPossible(ConvertToRational(first) / ConvertToRational(second));
          case NumberClass.Real:
            return ConvertToReal(first) / ConvertToReal(second);
          case NumberClass.Complex:
            return ConvertToComplex(first) / ConvertToComplex(second);
        }
      }
      catch (DivideByZeroException)
      {
        return AssertionViolation("/", "divide by zero", first, second);
      }

      throw new NotImplementedException();
    }

    //[Builtin("/", AllowConstantFold = true)]
    //public static object Divide(object car, params object[] args)
    //{
    //  object result = 1;
    //  for (int i = 0; i < args.Length; i++)
    //  {
    //    result = Multiply(result, args[i]);
    //  }

    //  return Divide(car, result);
    //}

    #endregion

    delegate R Function<T, R>(T t);
    delegate R Function<T1, T2, R>(T1 t1, T2 t2);

    static double SafeConvert(object obj)
    {
      try
      {
        if (obj is Complex64)
        {
          Complex64 c = (Complex64)obj;
          if (c.Imag == 0.0)
          {
            return c.Real;
          }
          else
          {
            return (double)AssertionViolation(GetCaller(), "no conversion to real possible", obj);
          }
        }
        return Convert.ToDouble(obj, CultureInfo.InvariantCulture);
      }
      catch (OverflowException)
      {
        return IsTrue(IsPositive(obj)) ? double.PositiveInfinity : double.NegativeInfinity;
      }
    }

    //based on lsqrt()
    [Builtin("bignum-sqrt", AllowConstantFold = true)]
    public static object SqrtBigInteger(object num)
    {
      BigInteger x = (BigInteger)num;
      BigInteger v0, q0, x1;

      if (x <= 1)
      {
        return x;
      }

      v0 = x;
      x = x / 2;
      while (true)
      {
        q0 = v0 / x;
        x1 = (x + q0) / 2;
        if (q0 >= x)
          break;
        x = x1;
      }
      if (x1 * x1 != v0)
      {
        return Math.Sqrt(v0);
      }
      return x1;
    }

    [Builtin("bignum-sqrt-exact", AllowConstantFold = true)]
    public static object ExactSqrtBigInteger(object num)
    {
      BigInteger x = (BigInteger)num;
      BigInteger v0, q0, x1;

      if (x <= 1)
      {
        return x;
      }

      v0 = x;
      x = x / 2;
      while (true)
      {
        q0 = v0 / x;
        x1 = (x + q0) / 2;
        if (q0 >= x)
          break;
        x = x1;
      }
      q0 = x1 * x1;

      if (q0 > v0)
      {
        x1 = x1 - 1;
        q0 = x1 * x1;
      }
      return Values(x1, v0 - q0);
    }


    static TypeConverter FractionConverter = TypeDescriptor.GetConverter(typeof(Fraction));

    #region Other Obsolete

    [Obsolete("", false)]
    static object IntegerIfPossible(object res)
    {
      if (IsTrue(IsIntegerValued(res)))
      {
        return Exact(res);
      }
      return res;
    }

    [Obsolete]
    static object MathHelper(Function<double, double> func, object obj)
    {
      if (obj is double)
      {
        return func((double)obj);
      }
      else if (obj is int)
      {
        return func((int)obj);
      }
      else
      {
        double d = SafeConvert(obj);
        return func(d);
      }
    }

    [Obsolete]
    static object MathHelper(Function<double, double, double> func, object num1, object num2)
    {
      if (num1 is double)
      {
        if (num2 is double)
        {
          return func((double)num1, (double)num2);
        }
        else if (num2 is int)
        {
          return func((double)num1, (int)num2);
        }
      }
      if (num1 is int)
      {
        if (num2 is int)
        {
          return func((int)num1, (int)num2);
        }
        else if (num2 is double)
        {
          return func((int)num1, (double)num2);
        }
      }
      double d1 = SafeConvert(num1);
      double d2 = SafeConvert(num2);
      return func(d1, d2);
    }


    [Obsolete]
    static object RemainderInternal(object first, object second)
    {
      NumberClass f = GetNumberClass(first);

      if (f == NumberClass.NotANumber)
      {
        return AssertionViolation("RemainderInternal", "not a number", first);
      }

      NumberClass s = GetNumberClass(second);

      if (s == NumberClass.NotANumber)
      {
        return AssertionViolation("RemainderInternal", "not a number", second);
      }

      NumberClass effective = f & s;

      switch (effective)
      {
        case NumberClass.Integer:
          try
          {
            return checked(ConvertToInteger(first) % ConvertToInteger(second));
          }
          catch (OverflowException)
          {
            return ConvertToBigInteger(first) % ConvertToBigInteger(second);
          }
          catch (ArithmeticException) // mono dodo
          {
            return ConvertToBigInteger(first) % ConvertToBigInteger(second);
          }
        case NumberClass.BigInteger:
          return ConvertToBigInteger(first) % ConvertToBigInteger(second);
        case NumberClass.Rational:
          return IntegerIfPossible(ConvertToRational(first) % ConvertToRational(second));
        case NumberClass.Real:
          return ConvertToReal(first) % ConvertToReal(second);
        case NumberClass.Complex:
          return ConvertToComplex(first) % ConvertToComplex(second);
      }

      throw new NotImplementedException();
    }

    #endregion

    #region Obsolete

     

    //[Builtin("expt", AllowConstantFold = true)]
    [Obsolete("Implemented in Scheme, do not use, remove if possible", false)]
    internal static object Expt(object obj1, object obj2)
    {
      if (obj1 is Complex64 || IsTrue(IsNegative(obj1)))
      {
        return Complex64.Pow(ConvertToComplex(obj1), ConvertToComplex(obj2));
      }

      bool exact = IsTrue(IsExact(obj1)) && IsTrue(IsExact(obj2));

      if (IsTrue(IsZero(obj1)) && !IsTrue(IsZero(obj2)))
      {
        return exact ? 0 : (object)0.0;
      }

      if (IsTrue(IsZero(obj2)))
      {
        return exact ? 1 : (object)1.0;
      }

      if (IsTrue(IsSame(obj1, 1)))
      {
        return exact ? 1 : (object)1.0;
      }

      if (IsTrue(IsSame(obj2, 1)))
      {
        return exact ? Exact(obj1) : Inexact(obj1);
      }


      bool isnegative = IsTrue(IsNegative(obj2));

      if (isnegative)
      {
        obj2 = Abs(obj2);
      }

      if (IsTrue(IsInteger(obj1)) && IsTrue(IsInteger(obj2)))
      {
        BigInteger a = ConvertToBigInteger(obj1);
        BigInteger r = a.Power(Convert.ToInt32(obj2));
        if (isnegative)
        {
          if (r == 0)
          {
            throw new NotSupportedException();
          }
          return Divide(1, r);
        }
        if (r < int.MaxValue && r > int.MinValue)
        {
          return r.ToInt32();
        }
        return r;
      }

      if (IsTrue(IsRational(obj1)) && IsTrue(IsIntegerValued(obj2)))
      {
        Fraction f = ConvertToRational(obj1);
        if (obj2 is Fraction)
        {
          obj2 = Divide(obj2, 1);
        }
        if (isnegative)
        {
          return Divide(Expt(f.Denominator, obj2), Expt(f.Numerator, obj2));
        }
        else
        {
          return Divide(Expt(f.Numerator, obj2), Expt(f.Denominator, obj2));
        }
      }

      if (IsTrue(IsReal(obj1)) && IsTrue(IsReal(obj2)))
      {
        object res = MathHelper(Math.Pow, obj1, obj2);
        if (isnegative)
        {
          return Divide(1, res);
        }
        else
        {
          //NumberClass e = GetNumberClass(obj1) & GetNumberClass(obj2);
          //return GetNumber(e, res);
          return res;
        }
      }

      throw new NotSupportedException();
    }


  
    [Builtin("inexact")]
    [Obsolete("Implemented in Scheme, do not use, remove if possible", false)]
    internal static object Inexact(object obj)
    {
      if (!IsTrue(IsNumber(obj)))
      {
        return AssertionViolation("inexact", "not a number", obj);
      }

      if (IsTrue(IsExact(obj)))
      {
        return SafeConvert(obj);
      }
      return obj;
    }

    [Builtin("exact")]
    [Obsolete("Implemented in Scheme, do not use, remove if possible", false)]
    internal static object Exact(object obj)
    {
      if (!IsTrue(IsNumber(obj)))
      {
        return AssertionViolation("exact", "not a number", obj);
      }
      if (obj is double)
      {
        double d = (double)obj;

        if (double.IsNaN(d) || double.IsInfinity(d))
        {
          return AssertionViolation("exact", "no exact equivalent", obj);
        }
        return Exact((Fraction)d);
      }
      if (obj is Complex64)
      {
        Complex64 c = (Complex64)obj;
        if (double.IsNaN(c.Real) || double.IsNaN(c.Imag) ||
            double.IsInfinity(c.Real) || double.IsInfinity(c.Imag))
        {
          return AssertionViolation("exact", "no exact equivalent", obj);
        }
        else
        {
          return new ComplexFraction((Fraction)c.Real, (Fraction)c.Imag);
        }
      }
      if (obj is long)
      {
        BigInteger r = (BigInteger)BigIntConverter.ConvertFrom(obj);
        int ir;
        if (r.AsInt32(out ir))
        {
          return ir;
        }
        return r;
      }
      if (obj is Fraction)
      {
        Fraction f = (Fraction)obj;
        if (f.Denominator == 1)
        {
          if (f.Numerator > int.MaxValue || f.Numerator < int.MinValue)
          {
            return (BigInteger)f.Numerator;
          }
          return (int)f.Numerator;
        }
        return f;
      }
      return obj;
    }

    [Builtin("round")]
    [Obsolete("Implemented in Scheme, do not use, remove if possible", true)]
    internal static object Round(object obj)
    {
      if (IsTrue(IsInteger(obj)))
      {
        return obj;
      }
      if (IsTrue(IsExact(obj)) && IsTrue(IsRational(obj)))
      {
        Fraction f = ConvertToRational(obj);
        BigInteger c = f.Numerator / f.Denominator;
        BigInteger d = f.Numerator % f.Denominator;
        if (d < 0)
        {
          if (-d > f.Denominator / 2)
          {
            return ToIntegerIfPossible(c - 1);
          }
          else if (-d < f.Denominator / 2)
          {
            return ToIntegerIfPossible(c);
          }
        }
        else if (d > 0)
        {
          if (d > f.Denominator / 2)
          {
            return ToIntegerIfPossible(c + 1);
          }
          else if (d < f.Denominator / 2)
          {
            return ToIntegerIfPossible(c);
          }
        }
        else
        {
          if (c % 2 == 0)
          {
            return ToIntegerIfPossible(c);
          }
          else
          {
            return ToIntegerIfPossible(c + 1);
          }
        }
      }
      object res = MathHelper(Math.Round, obj);
      if (IsTrue(IsExact(obj)))
      {
        return Exact(res);
      }
      else
      {
        return res;
      }
    }

    [Builtin("abs")]
    [Obsolete("Implemented in Scheme, do not use, remove if possible", true)]
    internal static object Abs(object obj)
    {
      if (obj is double)
      {
        return Math.Abs((double)obj);
      }
      else if (obj is int)
      {
        int i = (int)obj;
        if (i == int.MinValue)
        {
          return ((BigInteger)i).Abs();
        }
        else
        {
          return Math.Abs(i);
        }
      }
      else if (obj is BigInteger)
      {
        return ((BigInteger)obj).Abs();
      }
      else if (obj is Complex64)
      {
        return AssertionViolation("abs", "not a real", obj);
      }
      else if (obj is Fraction)
      {
        Fraction f = (Fraction)obj;
        if (f < 0)
        {
          return new Fraction(-f.Numerator, f.Denominator);
        }
        return obj;
      }
      else
      {
        double d = SafeConvert(obj);
        return Math.Abs(d);
      }
    }

    [Builtin("magnitude")]
    [Obsolete("Implemented in Scheme, do not use, remove if possible", true)]
    internal static object Magnitude(object obj)
    {
      if (obj is Complex64)
      {
        Complex64 c = ConvertToComplex(obj);
        double m = Math.Sqrt(c.Imag * c.Imag + c.Real * c.Real);
        return m;
      }
      else if (IsTrue(IsNumber(obj)))
      {
        return Abs(obj);
      }
      else
      {
        return AssertionViolation("magnitude", "not a number", obj);
      }
    }

    [Builtin("angle")]
    [Obsolete("Implemented in Scheme, do not use, remove if possible", true)]
    internal static object Angle(object obj)
    {
      Complex64 c = ConvertToComplex(obj);
      return Atan(c.Imag, c.Real);
    }

    [Builtin("atan")]
    [Obsolete("Implemented in Scheme, do not use, remove if possible", true)]
    internal static object Atan(object obj, object obj2)
    {
      return MathHelper(Math.Atan2, obj, obj2);
    }


    [Builtin("exact?")]
    [Obsolete("Implemented in Scheme, do not use, remove if possible", false)]
    internal static object IsExact(object obj)
    {
      if (IsTrue(IsNumber(obj)))
      {
        return GetBool(obj is int || obj is BigInteger || obj is Fraction);
      }
      return AssertionViolation("exact?", "not a number", obj);
    }

    [Builtin("=")]
    [Obsolete("Implemented in Scheme, do not use, remove if possible", false)]
    internal static object IsSame(object first, object second)
    {
      NumberClass f = GetNumberClass(first);

      if (f == NumberClass.NotANumber)
      {
        return AssertionViolation("=", "not a number", first);
      }

      NumberClass s = GetNumberClass(second);

      if (s == NumberClass.NotANumber)
      {
        return AssertionViolation("=", "not a number", second);
      }

      NumberClass effective = f & s;

      bool result = false;

      switch (effective)
      {
        case NumberClass.Integer:
          result = ConvertToInteger(first) == ConvertToInteger(second);
          break;
        case NumberClass.BigInteger:
          result = ConvertToBigInteger(first) == ConvertToBigInteger(second);
          break;
        case NumberClass.Rational:
          result = ConvertToRational(first) == ConvertToRational(second);
          break;
        case NumberClass.Real:
          double f1 = ConvertToReal(first);
          double f2 = ConvertToReal(second);
          result = f1 == f2
            || (double.IsNegativeInfinity(f1) && double.IsNegativeInfinity(f2))
            || (double.IsPositiveInfinity(f1) && double.IsPositiveInfinity(f2));
          break;
        case NumberClass.Complex:
          result = ConvertToComplex(first) == ConvertToComplex(second);
          break;
        default:
          return AssertionViolation("=", "not a number", first, second);
      }

      return GetBool(result);
    }

    static void CheckArgs(string who, object[] args)
    {
      if (args == null || args.Length == 0)
      {
        AssertionViolation(who, "expects 2 or more arguments");
      }
    }

    [Builtin("<")]
    [Obsolete("Implemented in Scheme, do not use, remove if possible", true)]
    internal static object IsLessThan(object first, object second)
    {
      NumberClass f = GetNumberClass(first);

      if (f == NumberClass.NotANumber)
      {
        return AssertionViolation("<", "not a number", first);
      }

      NumberClass s = GetNumberClass(second);

      if (s == NumberClass.NotANumber)
      {
        return AssertionViolation("<", "not a number", second);
      }

      NumberClass effective = f & s;

      bool result = false;

      switch (effective)
      {
        case NumberClass.Integer:
          result = ConvertToInteger(first) < ConvertToInteger(second);
          break;
        case NumberClass.BigInteger:
          result = ConvertToBigInteger(first) < ConvertToBigInteger(second);
          break;
        case NumberClass.Rational:
          result = ConvertToRational(first) < ConvertToRational(second);
          break;
        case NumberClass.Real:
          result = ConvertToReal(first) < ConvertToReal(second);
          break;
        case NumberClass.Complex:
          return AssertionViolation("<", "not real", first, second);
        default:
          return FALSE;
      }

      return GetBool(result);
    }

 
    [Builtin(">")]
    [Obsolete("Implemented in Scheme, do not use, remove if possible", true)]
    internal static object IsGreaterThan(object first, object second)
    {
      NumberClass f = GetNumberClass(first);

      if (f == NumberClass.NotANumber)
      {
        return AssertionViolation(">", "not a number", first);
      }

      NumberClass s = GetNumberClass(second);

      if (s == NumberClass.NotANumber)
      {
        return AssertionViolation(">", "not a number", second);
      }

      NumberClass effective = f & s;

      bool result = false;

      switch (effective)
      {
        case NumberClass.Integer:
          result = ConvertToInteger(first) > ConvertToInteger(second);
          break;
        case NumberClass.BigInteger:
          result = ConvertToBigInteger(first) > ConvertToBigInteger(second);
          break;
        case NumberClass.Rational:
          result = ConvertToRational(first) > ConvertToRational(second);
          break;
        case NumberClass.Real:
          result = ConvertToReal(first) > ConvertToReal(second);
          break;
        case NumberClass.Complex:
          return AssertionViolation(">", "not real", first, second);
        default:
          return FALSE;;
      }

      return GetBool(result);
    }
     
    [Obsolete("Implemented in Scheme, do not use, remove if possible", false)]
    static object IsZero(object obj)
    {
      return IsSame(obj, 0);
    }

    [Obsolete("Implemented in Scheme, do not use, remove if possible", false)]
    static object IsPositive(object obj)
    {
      if (IsTrue(IsRealValued(obj)))
      {
        return IsGreaterThan(obj, 0);
      }
      return AssertionViolation("positive?", "not a real", obj);
    }

    [Obsolete("Implemented in Scheme, do not use, remove if possible", true)]
    static object IsNegative(object obj)
    {
      if (IsTrue(IsRealValued(obj)))
      {
        return IsLessThan(obj, 0);
      }
      return AssertionViolation("negative?", "not a real", obj);
    }

    //static ICallable is_number;

    [Builtin("number?")]
    [Obsolete("Implemented in Scheme, do not use, remove if possible", false)]
    internal static object IsNumber(object obj)
    {
      //if (is_number == null)
      //{
      //  is_number = "number?".Eval<ICallable>();
      //}
      return IsComplex(obj);
    }

    [Builtin("complex?")]
    [Obsolete("Implemented in Scheme, do not use, remove if possible", true)]
    internal static object IsComplex(object obj)
    {
      return GetBool(IsTrue(IsReal(obj)) || obj is Complex64 || obj is ComplexFraction);
    }

    [Builtin("real?")]
    [Obsolete("Implemented in Scheme, do not use, remove if possible", true)]
    internal static object IsReal(object obj)
    {
      return GetBool(IsTrue(IsRational(obj)) || obj is double);
    }

    [Builtin("rational?")]
    [Obsolete("Implemented in Scheme, do not use, remove if possible", true)]
    internal static object IsRational(object obj)
    {
      if (IsTrue(IsInteger(obj)) || obj is Fraction)
      {
        return TRUE;
      }

      if (obj is double)
      {
        return IsRational(RealToExact(obj));
      }

      return FALSE;
    }

    [Builtin("integer?")]
    [Obsolete("Implemented in Scheme, do not use, remove if possible", true)]
    internal static object IsInteger(object obj)
    {
      if (obj is int || obj is BigInteger)
      {
        return TRUE;
      }

      if (obj is Fraction)
      {
        return GetBool(((Fraction)obj).Denominator == 1);
      }

      if (obj is double)
      {
        return IsInteger(RealToExact(obj));
      }

      return FALSE;
    }

    [Obsolete]
    static object RealToExact(object obj)
    {
      double d = (double)obj;

      if (double.IsNaN(d) || double.IsInfinity(d))
      {
        return FALSE;
      }
      try
      {
        Fraction f = (Fraction)d;
        if (f.Denominator == 1)
        {
          if (f.Numerator > int.MaxValue || f.Numerator < int.MinValue)
          {
            return (BigInteger)f.Numerator;
          }
          return (int)f.Numerator;
        }
        return f;
      }
      catch (DivideByZeroException)
      {
        // fall back to bigint
      }
      catch (OverflowException)
      {
        // fall back to bigint
      }
      BigInteger r = (BigInteger)BigIntConverter.ConvertFrom(Round(obj));
      int ir;
      if (r.AsInt32(out ir))
      {
        return ir;
      }
      return r;
    }

    [Builtin("integer-valued?")]
    [Obsolete("Implemented in Scheme, do not use, remove if possible", true)]
    internal static object IsIntegerValued(object obj)
    {
      if (obj is int || obj is BigInteger)
      {
        return TRUE;
      }
      if (obj is Complex64)
      {
        return GetBool(((Complex64)obj).Imag == 0 && IsTrue(IsIntegerValued(((Complex64)obj).Real)));
      }
      if (obj is Fraction)
      {
        return GetBool(((Fraction)obj).Denominator == 1);
      }
      if (IsTrue(IsNan(obj)) || IsTrue(IsInfinite(obj)))
      {
        return FALSE;
      }
      return IsZero(RemainderInternal(obj, 1));
    }

    [Builtin("rational-valued?")]
    [Obsolete("Implemented in Scheme, do not use, remove if possible", true)]
    internal static object IsRationalValued(object obj)
    {
      if (obj is Fraction)
      {
        return TRUE;
      }

      bool iv = IsTrue(IsIntegerValued(obj));
      if (iv)
      {
        return TRUE;
      }

      if (obj is Complex64)
      {
        Complex64 c = (Complex64)obj;
        if (c.Imag == 0)
        {
          return IsRationalValued(c.Real);
        }
        return FALSE;
      }

      if (IsTrue(IsNumber(obj)))
      {
        double d = SafeConvert(obj);
        if (double.IsNaN(d) || double.IsInfinity(d))
        {
          return FALSE;
        }
        return GetBool(d == (double)(Fraction)d);
      }
      return FALSE;
    }

    [Builtin("real-valued?")]
    [Obsolete("Implemented in Scheme, do not use, remove if possible", true)]
    internal static object IsRealValued(object obj)
    {
      if (obj is Complex64)
      {
        Complex64 c = (Complex64)obj;
        if (c.Imag != 0)
        {
          return FALSE;
        }
      }
      return IsNumber(obj);
    }

    [Builtin("infinite?")]
    [Obsolete("Implemented in Scheme, do not use, remove if possible", true)]
    internal static object IsInfinite(object obj)
    {
      if (obj is double)
      {
        return GetBool(double.IsInfinity((double)obj));
      }

      return FALSE;
    }

    [Builtin("nan?")]
    [Obsolete("Implemented in Scheme, do not use, remove if possible", true)]
    internal static object IsNan(object obj)
    {
      if (obj is double)
      {
        return GetBool(double.IsNaN((double)obj));
      }
      if (obj is float)
      {
        return GetBool(float.IsNaN((float)obj));
      }

      return FALSE;
    }

    #endregion
  }
}
