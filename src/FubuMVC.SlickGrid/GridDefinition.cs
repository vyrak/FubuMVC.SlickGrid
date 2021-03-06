using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using FubuMVC.Core;
using FubuMVC.Core.Registration.Nodes;
using FubuMVC.Core.Registration.Routes;
using FubuMVC.Core.UI.Templates;
using FubuMVC.Core.Urls;
using FubuMVC.Media.Projections;
using FubuCore;
using FubuMVC.Core.Resources.Conneg;

namespace FubuMVC.SlickGrid
{
    public abstract class GridDefinition<T> : IGridDefinition<T>, IFubuRegistryExtension
    {
        private readonly IList<IGridColumn> _columns = new List<IGridColumn>();
        private Type _queryType;
        private Type _sourceType;

        protected GridDefinition()
        {
            Projection = new Projection<T>();
        }

        public Type SourceType
        {
            get { return _sourceType; }
        }

        public AddExpression Add
        {
            get { return new AddExpression(this); }
        }

        void IFubuRegistryExtension.Configure(FubuRegistry registry)
        {
            registry.Configure(graph => {
                Type runnerType = DetermineRunnerType();
                if (runnerType == null) return;

                MethodInfo method = runnerType.GetMethod("Run");

                var call = new ActionCall(runnerType, method);
                var chain = new BehaviorChain();
                chain.AddToEnd(call);
                chain.Route = new RouteDefinition(DiagnosticConstants.UrlPrefix);
                chain.Route.Append("_data");
                chain.Route.Append(typeof (T).Name);

                chain.MakeAsymmetricJson();

                graph.AddChain(chain);
            });
        }

        string IGridDefinition.ToColumnJson()
        {
            var builder = new StringBuilder();
            builder.Append("[");
            var columns = _columns.OrderByDescending(x => x.IsFrozen).ToList();

            for (var i = 0; i < columns.Count - 1; i++)
            {
                var column = columns[i];
                column.WriteColumn(builder);
                builder.Append(", ");
            }

            columns.Last().WriteColumn(builder);

            builder.Append("]");

            return builder.ToString();
        }

        string IGridDefinition.SelectDataSourceUrl(IUrlRegistry urls)
        {
            if (_sourceType == null) return null;

            if (_queryType != null)
            {
                return urls.UrlFor(_queryType);
            }

            Type runnerType = DetermineRunnerType();

            return urls.UrlFor(runnerType);
        }

        public bool UsesHtmlConventions { get; set; }

        void IGridDefinition.SelectFormattersAndEditors(IColumnPolicies editors)
        {
            _columns.Each(x => x.SelectFormatterAndEditor(this, editors));
        }

        void IGridDefinition.WriteAnyTemplates(ITemplateWriter writer)
        {
            _columns.Each(x => x.WriteTemplates(writer));
        }

        public bool AllColumnsAreEditable { get; set; }
        public SlickGridFormatter DefaultFormatter { get; set; }

        public Projection<T> Projection { get; private set; }

        /// <summary>
        /// Source type must implement either IGridDataSource<T> or IGridDataSource<T, TQuery>
        /// </summary>
        /// <typeparam name="TSource"></typeparam>
        public void SourceIs<TSource>()
        {
            Type sourceType = typeof (TSource);
            var templateType = sourceType.FindInterfaceThatCloses(typeof (IGridDataSource<>));
            if (templateType != null)
            {
                if (templateType.GetGenericArguments().First() != typeof (T))
                {
                    throw new ArgumentOutOfRangeException("Wrong type as the argument to IGridDataSource<>");
                }

                _queryType = null;
                _sourceType = sourceType;

                return;
            }

            templateType = sourceType.FindInterfaceThatCloses(typeof (IGridDataSource<,>));
            if (templateType != null)
            {
                if (templateType.GetGenericArguments().First() != typeof (T))
                {
                    throw new ArgumentOutOfRangeException("Wrong type as the argument to IGridDataSource<>");
                }

                _queryType = templateType.GetGenericArguments().Last();
                _sourceType = sourceType;

                return;
            }

            throw new ArgumentOutOfRangeException("TSource must be either IGridDataSource<T> or IGridDataSource<TQuery>");
        }

        public ColumnDefinition<T, TProp> Column<TProp>(Expression<Func<T, TProp>> property)
        {
            var column = new ColumnDefinition<T, TProp>(property, Projection);
            _columns.Add(column);

            return column;
        }

        public AccessorProjection<T, TProp> Data<TProp>(Expression<Func<T, TProp>> property)
        {
            return Projection.Value(property);
        } 

        public Type DetermineRunnerType()
        {
            if (_sourceType == null) return null;

            return _queryType == null
                       ? typeof (GridRunner<,,>).MakeGenericType(typeof (T), GetType(), _sourceType)
                       : typeof (GridRunner<,,,>).MakeGenericType(typeof (T), GetType(), _sourceType, _queryType);
        }

       

        #region Nested type: AddExpression

        public class AddExpression
        {
            private readonly GridDefinition<T> _parent;

            public AddExpression(GridDefinition<T> parent)
            {
                _parent = parent;
            }

            public static AddExpression operator +(AddExpression original, IGridColumn column)
            {
                original._parent._columns.Add(column);

                return original;
            }
        }

        #endregion
    }
}