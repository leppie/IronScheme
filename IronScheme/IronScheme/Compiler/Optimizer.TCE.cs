﻿#region License
/* Copyright (c) 2007,2008,2009,2010,2011 Llewellyn Pritchard 
 * All rights reserved.
 * This source code is subject to terms and conditions of the BSD License.
 * See docs/license.txt. */
#endregion

using Microsoft.Scripting.Ast;
using System;
using System.Collections.Generic;
using IronScheme.Runtime;
using Microsoft.Scripting;
using System.Diagnostics;

namespace IronScheme.Compiler
{
  static partial class Optimizer
  {
    class TCE : OptimizerBase
    {
      public override void Optimize()
      {
        var fixups = new List<ReturnStatement>();

        Pass0 p0 = new Pass0(fixups);
        p0.WalkNode(Root);

        Root.Bind();
      }

      class Pass0 : DeepWalker
      {
        readonly List<ReturnStatement> fixups;

        public Pass0(List<ReturnStatement> fixups)
        {
          this.fixups = fixups;
        }

        static Expression Unwrap(Expression ex)
        {
          while (ex is UnaryExpression && ex.NodeType == AstNodeType.Convert)
          {
            ex = ((UnaryExpression)ex).Operand;
          }

          return ex;
        }

        protected override bool Walk(ReturnStatement node)
        {
          var mce = node.Expression as MethodCallExpression;
          if (mce != null && IsTCE(mce))
          {
            if (!(Current.Body is LabeledStatement))
            {
              Current.Body = Ast.Labeled(Current.Body);
            }

            var ee = new List<Expression>();
            int i = 0;
            var temps = new List<Variable>();
            foreach (var par in Current.Parameters)
            {
              var v = Current.CreateTemporaryVariable((SymbolId)Builtins.GenSym(par.Name), par.Type);
              ee.Add(Ast.Assign(v, mce.Arguments[i++]));
              temps.Add(v);
            }
            i = 0;
            foreach (var par in Current.Parameters)
            {
              ee.Add(Ast.Assign(par, Ast.Read(temps[i++])));
            }
            ee.Add(Ast.Void(Ast.Continue()));
            node.Expression = Ast.Comma(ee);

            fixups.Add(node);

            Current.Bind();
          }

          return base.Walk(node);
        }

        bool IsTCE(MethodCallExpression mce)
        {
          if (!mce.TailCall) return false;
          if (mce.Instance == null) return false;
          var i = Unwrap(mce.Instance);
          if (i.Type != typeof(Callable)) return false;
          var be = i as BoundExpression;
          if (be == null) return false;
          var var = be.Variable;
          if (!var.Lift || var.Type != typeof(Callable) || var.ReAssigned) return false;
          if (mce.Method.Name != "Call") return false;
          if (mce.Arguments.Count > 0 && mce.Arguments[0].Type == typeof(object[])) return false;
          var av = var.AssumedValue as MethodCallExpression;
          if (av == null || av.Type != typeof(Callable) || av.Method.Name != "Create") return false;
          var cbe = av.Arguments[0] as CodeBlockExpression;
          if (cbe == null || cbe.Block != Current) return false;
          if (mce.Arguments.Count > 8) return false;

          return true;
        }

      }
    }
  }
}