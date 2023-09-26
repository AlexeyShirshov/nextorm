using System.Data;
using System.Data.Common;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic.FileIO;

namespace nextorm.core;
public class SqlCommand : QueryCommand
{
    private DbCommand? _dbCommand;
    public SqlCommand(SqlClient sqlClient, LambdaExpression exp, Expression? condition) : base(sqlClient, exp ,condition)
    {
    }
    public override void ResetPreparation()
    {
        base.ResetPreparation();
        _dbCommand = null;
    }
    public override void PrepareCommand(CancellationToken cancellationToken)
    {
        //var resultType = _exp.ReturnType;

        // _hasCtor = resultType.IsValueType || resultType.GetConstructor(Type.EmptyTypes) is not null;

        // if (!_hasCtor)
        // {
        //     //resultType.GetConstructors().OrderByDescending(it=>it.GetParameters().Length).FirstOrDefault()
        // }

        if (_from is not null && _from.Table.IsT1 && !_from.Table.AsT1.IsPrepared)
            _from.Table.AsT1.PrepareCommand(cancellationToken);

        var selectList = _selectList;

        if (selectList is null)
        {
            if (_exp is null)
                throw new BuildSqlCommandException("Lambda expression for anonymous type must exists");

            selectList = new List<SelectExpression>();

            if (_exp.Body is NewExpression ctor)
            {
                for (var idx = 0; idx < ctor.Arguments.Count; idx++)
                {
                    if (cancellationToken.IsCancellationRequested)
                        return;

                    var arg = ctor.Arguments[idx];
                    var ctorParam = ctor.Constructor!.GetParameters()[idx];

                    selectList.Add(new SelectExpression(ctorParam.ParameterType) { Index = idx, PropertyName = ctorParam.Name!, Expression = arg });
                }

                if (selectList.Count == 0)
                    throw new BuildSqlCommandException("Select must return new anonymous type with at least one property");

            }
            else
            {
                throw new BuildSqlCommandException("Select must return new anonymous type");
            }
        }

        var srcType = _srcType;
        if (srcType is null)
        {
            if (_exp is null)
                throw new BuildSqlCommandException("Lambda expression for anonymous type must exists");
            
            srcType = _exp.Parameters[0].Type;
        }

        FromExpression? from = _from;

        if (from is null)
        {
            if (srcType != typeof(TableAlias))
            {
                var sqlTable = srcType.GetCustomAttribute<SqlTableAttribute>();

                var tableName = sqlTable is not null
                    ? sqlTable.Name
                    : (_sqlClient as SqlClient).GetTableName(srcType);

                from = new FromExpression(tableName);
            }
            else throw new BuildSqlCommandException($"From must be specified for {nameof(TableAlias)} as source type");
        }

        _isPrepared = true;
        _selectList = selectList;
        _srcType = srcType;
        _from = from;

    }
    internal DbCommand GetDbCommand(CancellationToken cancellationToken)
    {
        if (!_isPrepared) PrepareCommand(cancellationToken);
        return _dbCommand ??= (_sqlClient as SqlClient).CreateCommand(this);
    }
}