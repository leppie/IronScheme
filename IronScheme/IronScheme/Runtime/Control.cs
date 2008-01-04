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
using System.Collections;
using Microsoft.Scripting;
using Microsoft.Scripting.Actions;
using Microsoft.Scripting.Ast;
using System.Diagnostics;
using IronScheme.Compiler;
using System.Threading;

namespace IronScheme.Runtime
{
  public static partial class BuiltinEmitters
  {
    static MethodInfo ICallable_Call = typeof(ICallable).GetMethod("Call", new Type[] { typeof(object[]) });
    static MethodInfo ListToVector = typeof(Builtins).GetMethod("ListToVector");
    static MethodInfo List = typeof(Builtins).GetMethod("List", new Type[] { typeof(object[]) });
    static MethodInfo Append = typeof(Builtins).GetMethod("Append");
    

    [InlineEmitter("apply")]
    public static Expression Apply(Expression[] args)
    {
      Expression c = Ast.ConvertHelper(args[0], typeof(ICallable));
      if (args.Length == 2)
      {
        return Ast.ComplexCallHelper(c, ICallable_Call, Ast.Call(ListToVector, args[1]));
      }
      else if (args.Length > 2)
      {
        Expression head = Ast.ComplexCallHelper(List, ArrayUtils.RemoveFirst(ArrayUtils.RemoveLast(args)));
        Expression cargs = Ast.ComplexCallHelper(Append, head, args[args.Length - 1]);
        
        return Ast.ComplexCallHelper(c, ICallable_Call, Ast.Call(ListToVector, cargs));
      }
      else
      {
        return null;
      }
    }

    [InlineEmitter("values")]
    public static Expression Values(Expression[] values)
    {
      return Ast.NewArray(typeof(object[]), values);
    }
  }

  public partial class Builtins
  {
    [Builtin]
    public static object Values(params object[] values)
    {
      return values;
    }


    [Builtin("dynamic-wind")]
    public static object DynamicWind(object infunc, object bodyfunc, object outfunc)
    {
      ICallable inf = RequiresNotNull<ICallable>(infunc);
      ICallable bodyf = RequiresNotNull<ICallable>(bodyfunc);
      ICallable outf = RequiresNotNull<ICallable>(outfunc);

      inf.Call();

      try
      {
        return bodyf.Call();
      }
      finally
      {
        outf.Call();
      }
    }


    internal class Continuation : Exception
    {
      readonly object value;

      public object Value
      {
        get { return this.value; }
      } 

      public Continuation(object value)
      {
        this.value = value;
      }
    }

    static object InvokeContinuation(object value)
    {
      throw new Continuation(value);
    }
    
    [Builtin("call-with-current-continuation"), Builtin("call/cc")]
    public static object CallWithCurrentContinuation(object fc1)
    {
      ICallable fc = RequiresNotNull<ICallable>(fc1);
      try
      {
        CallTarget1 exitproc = InvokeContinuation;
        ICallable fce = Closure.Make(cc, exitproc);
        return fc.Call(fce);
      }
      catch (Continuation c)
      {
        return c.Value;
      }
    }

    [Builtin]
    public static object Force(object promise)
    {
      Promise p = RequiresNotNull<Promise>(promise);
      return p.Force();
    }

    [Builtin("procedure?")]
    public static object IsProcedure(object obj)
    {
      return obj is ICallable; 
    }


    //procedure:  (apply proc arg1 ... args) 
    //Proc must be a procedure and args must be a list. Calls proc with the elements of the list (append (list arg1 ...) args) as the actual arguments.
    
    [Builtin]
    public static object Apply(object fn, params object[] args)
    {
      if (args == null)
      {
        return Apply(fn, (object)null);
      }
      object[] head = ArrayUtils.RemoveLast(args);
      object last = args.Length > 0 ? args[args.Length - 1] : null;

      return Apply(fn, Append(List(head), last));
    }


    [Builtin]
    public static object Apply(object fn, object list)
    {
      Cons args = Requires<Runtime.Cons>(list);
      ICallable c = RequiresNotNull<ICallable>(fn);

      if (args == null)
      {
        // verify this, probably invalid
        return c.Call();
      }
      List<object> targs = new List<object>();
      
      while (args != null)
      {
        targs.Add(args.car);
        args = args.cdr as Cons;
      }

      return c.Call(targs.ToArray());
    }

    [Builtin]
    public static object PMap(object fn, params object[] lists)
    {
      if (lists == null)
      {
        return null;
      }
      ICallable f = RequiresNotNull<ICallable>(fn);

      List<object[]> args = new List<object[]>();

      foreach (object[] r in new MultiEnumerable(lists))
      {
        args.Add(r);
      }

      object[] results = new object[args.Count];
      ManualResetEvent[] mre = new ManualResetEvent[results.Length];

      for (int i = 0; i < results.Length; i++)
      {
        mre[i] = new ManualResetEvent(false);
        ThreadPool.QueueUserWorkItem(delegate (object state) 
        {
          int index = (int)state;
          results[index] = f.Call(args[index]);
          mre[index].Set();
        }, i);
      }

      foreach (ManualResetEvent mr in mre)
      {
        mr.WaitOne();
      }

      return Runtime.Cons.FromArray(results);
    }


    [Builtin]
    public static object Map(object fn, object lst)
    {
      Cons list = Requires<Runtime.Cons>(lst);
      ICallable f = RequiresNotNull<ICallable>(fn);
      Cons h = null, head = null;
      while (list != null)
      {
        Cons r = new Cons(f.Call(list.car));
        if (head == null)
        {
          head = h = r;
        }
        else
        {
          h.cdr = r;
          h = r;
        }
        list = list.cdr as Cons;
      }
      return head;
    }

    [Builtin]
    public static object Map(object fn, params object[] lists)
    {
      if (lists == null)
      {
        return null;
      }
      ICallable f = RequiresNotNull<ICallable>(fn);
      Cons h = null, head = null;

      foreach (object[] obj in new MultiEnumerable(lists))
      {
        Cons r = new Cons(f.Call(obj));
        if (head == null)
        {
          head = h = r;
        }
        else
        {
          h.cdr = r;
          h = r;
        }
      }
      return head;
    }


    [Builtin("for-each")]
    public static object ForEach(object fn, object list)
    {
      Cons c = Requires<Runtime.Cons>(list);
      ICallable f = RequiresNotNull<ICallable>(fn);
      while (c != null)
      {
        f.Call(c.car);
        c = c.cdr as Cons;
      }
      return Unspecified;
    }

    [Builtin("for-each")]
    public static object ForEach(object fn, params object[] lists)
    {
      ICallable f = RequiresNotNull<ICallable>(fn);
      foreach (object[] obj in new MultiEnumerable(lists))
      {
        f.Call(obj);
      }
      return Unspecified;
    }

  }
}
