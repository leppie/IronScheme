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

namespace IronScheme.Runtime
{

  sealed class BuiltinMethod : ICallable
  {
    readonly MethodBinder meth;
    readonly MethodGroup methods;
    readonly Dictionary<int, Delegate> cache = new Dictionary<int, Delegate>();

    public MethodBinder Binder
    {
      get { return meth; }
    }

    readonly string name;

    public string Name
    {
      get { return name; }
    } 


    public MethodBase[] GetMethodBases()
    {
      return methods.GetMethodBases();
    }
    
    public BuiltinMethod(string name, MethodGroup mg)
    {
      this.name = name;
      this.methods = mg;
      meth = MethodBinder.MakeBinder(IronScheme.Compiler.BaseHelper.binder, mg.Name, mg.GetMethodBases(), BinderType.Normal);

    }

    bool baked = false;

    public object Call(object[] args)
    {
      if (args == null)
      {
        args = new object[0];
      }

      CodeContext context = Compiler.BaseHelper.cc;

      int nargs = args.Length;

      Delegate d;

      if (cache.TryGetValue(nargs, out d))
      {
        return Closure.Make(context, d, Name).Call(args);
      }

      if (!baked)
      {
        try
        {
          Type[] targs = Array.ConvertAll<object, Type>(args, delegate(object input)
          {
            if (input == null)
            {
              return typeof(object);
            }
            else
            {
              return input.GetType();
            }
          });

          MethodCandidate mc = meth.MakeBindingTarget(CallType.None, targs);
          if (mc != null)
          {
            MethodBase mb = mc.Target.Method;

            bool needContext = NeedContext(mb);


            Type dt = CallTargets.GetTargetType(needContext, nargs, false);
            d = Delegate.CreateDelegate(dt, mb as MethodInfo, false);
            if (d == null)
            {
              d = Delegate.CreateDelegate(needContext ? typeof(CallTargetWithContextN) : typeof(CallTargetN), mb as MethodInfo, false);
            }

            if (d != null)
            {
              cache[nargs] = d;
              return Closure.Make(context, d, Name).Call(args);
            }

          }
        }
        catch
        {
          ;
        }
      }
      // fallback
      baked = true;
      return meth.CallReflected(context, CallType.None, args);
    }

    static bool NeedContext(MethodBase mb)
    {
      foreach (ParameterInfo pi in mb.GetParameters())
      {
        return pi.ParameterType == typeof(CodeContext);
      }
      return false;
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
      ICallable inf = (ICallable)infunc;
      ICallable bodyf = (ICallable)bodyfunc;
      ICallable outf = (ICallable)outfunc;

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
        ICallable fce = Closure.Make(cc, exitproc, "invoke-continuation");
        return fc.Call( new object[] { fce });
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


    [Builtin]
    public static object Apply(object fn)
    {
      ICallable c = RequiresNotNull<ICallable>(fn);
      return c.Call();
    }

    //procedure:  (apply proc arg1 ... args) 
    //Proc must be a procedure and args must be a list. Calls proc with the elements of the list (append (list arg1 ...) args) as the actual arguments.
    
    [Builtin]
    public static object Apply(object fn, params object[] args)
    {
      if (args == null)
      {
        return Apply(fn, (object) null);
      }
      object[] head = ArrayUtils.RemoveLast(args);
      Cons last = args.Length > 0 ? Requires<Runtime.Cons>(args[args.Length - 1]) : null;

      return Apply(fn, Append(List(head), last));
    }


    [Builtin]
    public static object Apply(object fn, object list)
    {
      Cons args = Requires<Runtime.Cons>(list);
      ICallable c = RequiresNotNull<ICallable>(fn);

      List<object> targs = new List<object>();
      while (args != null)
      {
        targs.Add(args.Car);
        args = args.Cdr as Cons;
      }
      return c.Call(targs.ToArray());
    }

    [Builtin]
    public static Cons Map(object fn, object lst)
    {
      Cons list = Requires<Runtime.Cons>(lst);
      ArrayList returns = new ArrayList();
      while (list != null)
      {
        returns.Add(Apply(fn, new Cons(list.Car)));
        list = list.Cdr as Cons;
      }
      return Runtime.Cons.FromList(returns);
    }

    [Builtin]
    public static Cons Map(object fn, params object[] lists)
    {
      if (lists == null)
      {
        return null;
      }
      ArrayList returns = new ArrayList();
      foreach (Cons obj in new MultiEnumerable(lists))
      {
        returns.Add(Apply(fn, obj));
      }
      return Runtime.Cons.FromList(returns);
    }


    [Builtin("for-each")]
    public static object ForEach(object fn, object list)
    {
      Cons c = Requires<Runtime.Cons>(list);

      while (c != null)
      {
        Apply(fn, new Cons(c.Car));
        c = c.Cdr as Cons;
      }
      return Unspecified;
    }

    [Builtin("for-each")]
    public static object ForEach(object fn, params object[] lists)
    {
      foreach (Cons obj in new MultiEnumerable(lists))
      {
        Apply(fn, obj);
      }
      return Unspecified;
    }

  }
}
