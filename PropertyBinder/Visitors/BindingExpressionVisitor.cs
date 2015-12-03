﻿using System;
using System.Linq.Expressions;
using System.Reflection;
using PropertyBinder.Engine;
using PropertyBinder.Helpers;

namespace PropertyBinder.Visitors
{
    internal sealed class BindingExpressionVisitor<TContext> : ExpressionVisitor
        where TContext : class
    {
        private readonly IBindingNode<TContext> _rootNode;
        private readonly Type _rootParameterType;
        private readonly Action<TContext> _bindingAction;

        public BindingExpressionVisitor(IBindingNode<TContext> rootNode, Type rootParameterType, Action<TContext> bindingAction)
        {
            _rootNode = rootNode;
            _rootParameterType = rootParameterType;
            _bindingAction = bindingAction;
        }

        protected override Expression VisitMember(MemberExpression expr)
        {
            var path = expr.GetPathToParameter(_rootParameterType);
            if (path != null)
            {
                var node = _rootNode;
                MemberInfo parentMember = null;

                foreach (var entry in path)
                {
                    if (parentMember != null)
                    {
                        node = node.GetSubNode(parentMember);
                    }

                    var property = entry as PropertyInfo;
                    if (property != null)
                    {
                        node.AddAction(property, _bindingAction);
                    }

                    parentMember = entry;
                }

                return expr;
            }

            return base.VisitMember(expr);
        }

        protected override Expression VisitMethodCall(MethodCallExpression expr)
        {
            foreach (var arg in expr.Arguments)
            {
                var collectionItemType = arg.Type.ResolveCollectionItemType();
                if (collectionItemType == null)
                {
                    continue;
                }

                var path = arg.GetPathToParameter(_rootParameterType);
                if (path == null)
                {
                    continue;
                }

                var node = _rootNode;
                foreach (var entry in path)
                {
                    node = node.GetSubNode(entry);
                }

                var collectionNode = node.GetCollectionNode(collectionItemType);
                if (collectionNode == null)
                {
                    continue;
                }

                collectionNode.AddAction(_bindingAction);

                BindingExpressionVisitor<TContext> itemVisitor = null;
                foreach (var arg2 in expr.Arguments)
                {
                    if (arg2.NodeType == ExpressionType.Lambda)
                    {
                        if (itemVisitor == null)
                        {
                            itemVisitor = new BindingExpressionVisitor<TContext>(collectionNode.GetItemNode(), collectionItemType, _bindingAction);
                        }

                        itemVisitor.Visit(arg2);
                    }
                }
            }

            return base.VisitMethodCall(expr);
        }
    }
}
