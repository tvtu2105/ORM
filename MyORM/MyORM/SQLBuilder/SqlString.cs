﻿﻿using MyORM.Database;
using MyORM.Mapper;
using MyORM.ORMException;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace MyORM.SQLBuilder
{
    public class SqlString<T> : SqlBuilder<T> where T : class, new()
    {
        protected string sql;

        protected IDatabase Database;

        private static IDataMapper dataMapper = null;

        public SqlString(IDatabase database)
        {
            this.Database = database;
            dataMapper = new DataMapper();
        }

        public SqlString()
        {
            dataMapper = new DataMapper();
        }

        public SqlBuilder<T> Insert(T ob)
        {
            this.sql = "Insert Into " + dataMapper.GetTablename<T>() + "(" + getColumnName() + ") Values (" + getValues(ob) + ")";
            return this;
        }

        public SqlBuilder<T> Insert(List<T> arr)
        {
            string valueString = "";
            for (int i = 0; i < arr.Count - 1; i++)
            {
                valueString += "(" + getValues(arr[i]) + "),";
            }
            valueString += "(" + getValues(arr[arr.Count - 1]) + ")";
            this.sql = "Insert Into " + dataMapper.GetTablename<T>() + "(" + getColumnName() + ") Values " + valueString + ";";
            return this;
        }

        public SqlBuilder<T> AND(Expression<Func<T, bool>> clause)
        {
            this.sql += String.Format(" AND ({0})", parseClause(clause.Body));
            return this;
        }

        public SqlBuilder<T> OR(Expression<Func<T, bool>> clause)
        {
            this.sql += String.Format(" OR ({0})", parseClause(clause.Body));
            return this;
        }

        public SqlBuilder<T> SelectAll()
        {
            this.sql = String.Format("SELECT {0} FROM {1} AS {1}", getAllColumnName<T>(), dataMapper.GetTablename<T>());
            return this;
        }
        public SqlBuilder<T> Delete()
        {
            this.sql = "DELETE FROM " + dataMapper.GetTablename<T>();
            return this;
        }

        public bool SaveChanges()
		{
            return Database.Execute(this.sql);
		}

        public SqlBuilder<T> Update(T ob)
        {
            this.sql = "UPDATE " + dataMapper.GetTablename<T>() + " SET ";// lấy danh sách các thuộc tính của đối tượng
            string setString = "";
            foreach (PropertyInfo prop in ob.GetType().GetProperties())
            {
                string porpName = prop.Name;
                var porpValue = getValueByType(ob, prop);
                string columnName = dataMapper.GetColumName<T>(porpName);
                if (columnName != null)
                {
                    setString += columnName + "=";
                    if (prop.PropertyType == typeof(string) || prop.PropertyType == typeof(DateTime))
                    {
                        setString += ("'" + porpValue + "'" + ",");
                    }
                    else
                    {
                        setString += (porpValue.ToString() + ",");
                    }
                }
            }
            this.sql += setString.Remove(setString.Length - 1);
            return this;
        }

        public SqlBuilder<T> Where(Expression<Func<T, bool>> clause)
        {
            this.sql += String.Format(" WHERE ({0})", parseClause(clause.Body));
            return this;
        }
        private static string parseClause(Expression expr)
        {
            BinaryExpression be = expr as BinaryExpression;

            string ope = checkedExpess(be);
            if (ope != null)
            {
                return String.Format("{0} {1} {2}", parseClause(be.Left), ope, parseClause(be.Right));
            }
            return convertToString(be);
        }
        private static string checkedExpess(BinaryExpression be)
        {
            switch (be.NodeType)
            {
                case ExpressionType.AndAlso:
                    return "AND";
                case ExpressionType.OrElse:
                    return "OR";
            }
            return null;
        }

        private static string convertToString(Expression exp)
        {
            BinaryExpression be = exp as BinaryExpression;
            Expression left = be.Left;
            Expression right = be.Right;
            string oprearator = "";
            ////
            object rightValue = FactoryGetValue.getStrategy(right).getValue(right);
            //Expression left = eq.Left;
            string leftValue = left.ToString().Split('.')[1];
            Type myType = typeof(T);
            PropertyInfo myPropInfo = myType.GetProperty(leftValue);
            switch (be.NodeType)
            {
                case ExpressionType.Equal:

                    if (myPropInfo.PropertyType == typeof(string))
                    {
                        oprearator = " LIKE ";

                    }
                    else oprearator = "=";
                    break;
                case ExpressionType.NotEqual:

                    if (myPropInfo.PropertyType == typeof(string))
                    {
                        oprearator = "NOT LIKE ";
                    }
                    else
                        oprearator = "<>";
                    break;
                case ExpressionType.GreaterThan:
                    oprearator = ">";
                    break;
                case ExpressionType.GreaterThanOrEqual:
                    oprearator = ">=";
                    break;
                case ExpressionType.LessThan:
                    oprearator = "<";
                    break;
                case ExpressionType.LessThanOrEqual:
                    oprearator = "<=";
                    break;
            }
            if (myPropInfo.PropertyType == typeof(string) || myPropInfo.PropertyType == typeof(DateTime))
            {
                rightValue = String.Format("'{0}'", rightValue);
            }
            return String.Format("{0}.{1} {2} {3}", dataMapper.GetTablename<T>(), dataMapper.GetColumName<T>(leftValue), oprearator, rightValue);
        }

        private string getColumnName()
        {
            string columnNameString = "";
            foreach (PropertyInfo prop in typeof(T).GetProperties())
            {
                string porpName = prop.Name;
                if (dataMapper.IsPrimaryKey<T>(porpName))
                {
                    if (!isAutoIncrement(porpName))//Tự động tăng
                    {
                        dataMapper.GetColumName<T>(porpName);
                        columnNameString += (dataMapper.GetColumName<T>(porpName) + ",");
                    }
                }
                else if (dataMapper.GetColumName<T>(porpName) != null)
                {
                    dataMapper.GetColumName<T>(porpName);
                    columnNameString += (dataMapper.GetColumName<T>(porpName) + ",");
                }
            }
            return columnNameString.Remove(columnNameString.Length - 1);
        }
        private string getValues(object ob)
        {
            string valueString = "";
            foreach (PropertyInfo prop in typeof(T).GetProperties())
            {
                string porpName = prop.Name;
                if (dataMapper.GetColumName<T>(porpName) != null)
                {
                    bool flag = true;
                    bool isPrimakey = dataMapper.IsPrimaryKey<T>(porpName);
                    var porpValue = getValueByType(ob, prop);
                    if (isPrimakey)
                    {
                        if (!isAutoIncrement(porpName) && porpValue == null)
                            throw new SqlStringException();
                        else if (!isAutoIncrement(porpName) && porpValue != null)
                        {
                            flag = true;
                        }
                        else
                        {
                            flag = false;
                        }
                    }
                    if (flag)
                    {
                        if (porpValue != null)
                        {
                            if (prop.PropertyType == typeof(string) || prop.PropertyType == typeof(DateTime))
                            {
                                valueString += ("'" + porpValue + "'" + ",");
                                if (prop.PropertyType == typeof(string))
                                {

                                }
                            }
                            else
                            {
                                valueString += (porpValue.ToString() + ",");
                            }
                        }
                        else
                        {
                            valueString += "null,";
                        }
                    }
                }
            }
            return valueString.Remove(valueString.Length - 1);
        }
        private object getValueByType(object ob, PropertyInfo prop)
        {
            var porpValue = prop.GetValue(ob, null);
            if (prop.PropertyType == typeof(DateTime))
            {
                DateTime date = (DateTime)porpValue;
                porpValue = date.ToString("yyyy/MM/dd HH:mm:ss");
            }
            return porpValue;
        }
        private string getAllColumnName<T1>() where T1 : class, new()
        {
            string columnNameString = "";
            string tableName = dataMapper.GetTablename<T1>();
            foreach (PropertyInfo prop in typeof(T1).GetProperties())
            {
                string porpName = prop.Name;
                string coulumnName = dataMapper.GetColumName<T1>(porpName);
                if (coulumnName != null)
                    columnNameString += String.Format("{0}.{1} AS '{0}.{1}',", tableName, coulumnName);
            }
            return columnNameString.Remove(columnNameString.Length - 1);
        }

        private bool isAutoIncrement(string porpName)
        {
            return dataMapper.IsPrimaryKeyAutoIncrement<T>(porpName);
        }

        public SqlBuilder<T> GroupBy(Expression<Func<T, object>> clause)
        {
            string group = parseClauseGroupBy(clause.Body.ToString());
            //string select = GetAllColumnToSelect(typeof(T));
            string sql = "SELECT {0} " +
                         "FROM {1} as {1} " +
                         "where {2} in (" +
                         "select {2}" +
                         "from {1} " +
                         "group by {2}" +
                         ")" +
                         "order by {2} asc";
            this.sql = String.Format(sql, getAllColumnName<T>(), DBMapper.getTablename<T>(), group);
            return new SqlGroupByBuilder<T, T>(this.sql);
        }

        private static string parseClauseGroupBy(string clause)
        {
            if (clause.Contains("Convert"))
                clause = clause.Replace("Convert", "");
            char[] separators = new char[] { '(', ')', ' ', };
            string[] subs = clause.Split(separators, StringSplitOptions.RemoveEmptyEntries);
            string left = (subs[0].Split('.', (char)StringSplitOptions.RemoveEmptyEntries))[1];
            return DBMapper.getColumName<T>(left);
        }

        public SqlBuilder<T> Having(Expression<Func<IGroup<T>, bool>> clause)
        {
            int index = this.sql.IndexOf(")");
            string having = " Having " + parseClauseHaving(clause.Body);
            this.sql = this.sql.Insert(index, having);
            return new SqlGroupByBuilder<T, T>(this.sql);
        }

        private static string parseClauseHaving(Expression expr)
        {
            BinaryExpression be = expr as BinaryExpression;

            string ope = checkedExpess(be);
            if (ope != null)
            {
                return String.Format("{0} {1} {2}", parseClauseHaving(be.Left), ope, parseClauseHaving(be.Right));
            }
            return convertToStringHaving(be);
        }

        private static string convertToStringHaving(Expression exp)
        {
            BinaryExpression be = exp as BinaryExpression;
            Expression left = be.Left;
            Expression right = be.Right;
            object rightValue = null;
            string oprearator = "";
            if (right is MemberExpression)
            {
                MemberExpression member = (MemberExpression)right;

                if (member.Expression is MemberExpression) // right là một property trong object
                {
                    MemberExpression captureToProduct = (MemberExpression)member.Expression;
                    ConstantExpression captureConst = (ConstantExpression)captureToProduct.Expression;
                    object obj = ((FieldInfo)captureToProduct.Member).GetValue(captureConst.Value);
                    rightValue = ((PropertyInfo)member.Member).GetValue(obj, null);
                }
                else if (member.Expression is ConstantExpression) // right là một biến
                {
                    ConstantExpression captureConst = (ConstantExpression)member.Expression;
                    rightValue = ((FieldInfo)member.Member).GetValue(captureConst.Value);
                }
            }
            else // right là một biến hằng
            {
                rightValue = right;
            }
            int index = left.ToString().IndexOf('.');
            string leftValue = left.ToString().Substring(index + 1, left.ToString().Length - index - 1);
            if (leftValue.Contains('.'))
            {
                index = leftValue.IndexOf('(');
                int index2 = leftValue.LastIndexOf(".");
                leftValue = leftValue.Remove(index + 1, index2 - index);
            }

            Type myType = typeof(T);
            PropertyInfo myPropInfo = myType.GetProperty(leftValue);
            switch (be.NodeType)
            {
                case ExpressionType.Equal:

                    if (myPropInfo.PropertyType == typeof(string))
                    {
                        oprearator = " LIKE ";
                        rightValue = String.Format("{0}", rightValue);
                    }
                    else oprearator = "=";
                    break;
                case ExpressionType.NotEqual:
                    if (myPropInfo.PropertyType == typeof(string))
                    {
                        oprearator = "NOT LIKE ";
                        rightValue = String.Format("{0}", rightValue);
                    }
                    else
                        oprearator = "<>";
                    break;
                case ExpressionType.GreaterThan:
                    oprearator = ">";
                    break;
                case ExpressionType.GreaterThanOrEqual:
                    oprearator = ">=";
                    break;
                case ExpressionType.LessThan:
                    oprearator = "<";
                    break;
                case ExpressionType.LessThanOrEqual:
                    oprearator = "<=";
                    break;
            }
            return String.Format("{0} {1} {2}", DBMapper.getColumName<T>(leftValue), oprearator, rightValue);
        }


        private string getColumnName<T1>() where T1 : class, new()
        {
            string columnNameString = "";
            foreach (PropertyInfo prop in typeof(T1).GetProperties())
            {
                string porpName = prop.Name;
                if (dataMapper.IsPrimaryKey<T1>(porpName))
                {
                    if (isAutoIncrement(porpName))//Tự động tăng
                    {
                        dataMapper.GetColumName<T1>(porpName);
                        columnNameString += (dataMapper.GetColumName<T>(porpName) + ",");
                    }
                }
                else if (dataMapper.GetColumName<T1>(porpName) != null)
                {
                    dataMapper.GetColumName<T1>(porpName);
                    columnNameString += (dataMapper.GetColumName<T>(porpName) + ",");
                }
            }
            return columnNameString.Remove(columnNameString.Length - 1);
        }
        private string getValues<T1>(object ob) where T1 : class, new()
        {
            string valueString = "";
            foreach (PropertyInfo prop in typeof(T).GetProperties())
            {
                string porpName = prop.Name;
                if (dataMapper.GetColumName<T1>(porpName) != null)
                {
                    bool flag = true;
                    bool isPrimakey = dataMapper.IsPrimaryKey<T1>(porpName);
                    var porpValue = getValueByType(ob, prop);
                    if (isPrimakey)
                    {
                        if (isAutoIncrement(porpName) && porpValue == null)
                            throw new SqlStringException();
                        else if (isAutoIncrement(porpName) && porpValue != null)
                        {
                            flag = true;
                        }
                        else
                        {
                            flag = false;
                        }
                    }
                    if (flag)
                    {
                        if (porpValue != null)
                        {
                            if (prop.PropertyType == typeof(string) || prop.PropertyType == typeof(DateTime))
                            {
                                valueString += ("'" + porpValue + "'" + ",");
                                if (prop.PropertyType == typeof(string))
                                {

                                }
                            }
                            else
                            {
                                valueString += (porpValue.ToString() + ",");
                            }
                        }
                        else
                        {
                            valueString += "null,";
                        }
                    }
                }
            }
            return valueString.Remove(valueString.Length - 1);
        }
        private object getValueByType(object ob, PropertyInfo prop)
        {
            var porpValue = prop.GetValue(ob, null);
            if (prop.PropertyType == typeof(DateTime))
            {
                DateTime date = (DateTime)porpValue;
                porpValue = date.ToString("yyyy/MM/dd HH:mm:ss");
            }
            return porpValue;
        }
        private string getAllColumnName<T1>() where T1 : class, new()
        {
            string columnNameString = "";
            string tableName = dataMapper.GetTablename<T1>();
            foreach (PropertyInfo prop in typeof(T1).GetProperties())
            {
                string porpName = prop.Name;
                string coulumnName = dataMapper.GetColumName<T1>(porpName);
                if (coulumnName != null)
                    columnNameString += String.Format("{0}.{1} AS '{0}.{1}',", tableName, coulumnName);
            }
            return columnNameString.Remove(columnNameString.Length - 1);
        }
        private bool isAutoIncrement(string porpName)
        {
            return false;
        }
    }

        public virtual List<T> Get()
		{
            return Database.Read<T>(this.sql) as List<T>;
		}

		public virtual Dictionary<TKey, List<T>> GetGroupby<TKey>()
		{
            throw new MemberAccessException("The mothod is not support for select and where");
		}
	}
}
