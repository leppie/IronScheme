﻿#region License
/* Copyright (c) 2007,2008,2009,2010 Llewellyn Pritchard 
 * All rights reserved.
 * This source code is subject to terms and conditions of the BSD License.
 * See docs/license.txt. */
#endregion

using Microsoft.Scripting.Ast;

namespace IronScheme.Compiler
{
  static partial class Optimizer
  {
    class FixupConditionals : OptimizerBase
    {
      public override void Optimize()
      {
        Pass0 p0 = new Pass0();
        p0.WalkNode(Root);
      }

      class Pass0 : DeepWalker
      {
        protected override bool Walk(ConditionalExpression node)
        {
          if (node.Test is UnaryExpression && node.Test.NodeType == AstNodeType.Not)
          {
            var tmp = node.IfFalse;
            node.IfFalse = node.IfTrue;
            node.IfTrue = tmp;

            var ue = node.Test as UnaryExpression;

            node.Test = ue.Operand;
          }
          return base.Walk(node);
        }
      }
    }
  }
}
