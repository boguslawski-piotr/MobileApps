﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace pbXNet.Database
{
	/// <summary>
	/// 
	/// </summary>
	/// <remarks>
	/// This class is not thread safe. The behavior of the same instance 
	/// used in different threads is undefined and can generate random data 
	/// or references to undefined data.
	/// </remarks>
	public class SqlTable<T> : ITable<T> where T : new()
	{
		public string Name { get; private set; }

		protected readonly IDatabase _db;

		protected readonly SqlBuilder _sqlBuilder;

		protected readonly List<MemberInfo> _columns;

		protected readonly List<MemberInfo> _pkColumns;

		public SqlTable(IDatabase db, string name)
		{
			Check.Null(db, nameof(db));
			Check.Empty(name, nameof(name));

			Name = name;

			_db = db;

			_sqlBuilder = _db.GetSqlBuilder();

			_columns = typeof(T).GetRuntimePropertiesAndFields().ToList();

			_pkColumns = _columns.Where(c => c.CustomAttributes.Any(a => a.AttributeType.Name == nameof(PrimaryKeyAttribute))).ToList();
		}

		public virtual void Dispose()
		{ }

		public static async Task<ITable<T>> OpenAsync(IDatabase db, string name)
		{
			SqlTable<T> table = new SqlTable<T>(db, name);
			await table.OpenAsync().ConfigureAwait(false);
			return table;
		}

		public virtual async Task OpenAsync()
		{
			// TODO: check if table has correct structure (as T definition), perform upgrade/downgrade if needed
		}

		public static async Task<ITable<T>> CreateAsync(IDatabase db, string name)
		{
			SqlTable<T> table = new SqlTable<T>(db, name);
			await table.CreateAsync().ConfigureAwait(false);
			return table;
		}

		public virtual async Task CreateAsync()
		{
			_sqlBuilder.Create().Table(Name);

			foreach (var c in _columns)
			{
				_sqlBuilder.C(c.Name);

				var attrs = c.CustomAttributes.ToList();

				bool notNull =
					_pkColumns.Contains(c) ||
					attrs.Find(a => a.AttributeType.Name == nameof(NotNullAttribute)) != null;

				int width = (int)(attrs.Find(a => a.AttributeType.Name == nameof(LengthAttribute))?.ConstructorArguments[0].Value ?? int.MaxValue);
				_sqlBuilder.T(_db.ConvertTypeToDbType(c.GetPropertyOrFieldType(), width));

				if (notNull)
					_sqlBuilder.NotNull();
				else
					_sqlBuilder.Null();
			}

			if (_pkColumns.Count > 0)
			{
				_sqlBuilder.Constraint($"PK_{Name}").PrimaryKey();
				foreach (var p in _pkColumns)
					_sqlBuilder.C(p.Name);
			}

			await _db.StatementAsync(_sqlBuilder.Build()).ConfigureAwait(false);

			await CreateIndexesAsync().ConfigureAwait(false);
		}

		public virtual async Task CreateIndexesAsync()
		{
			IDictionary<string, List<(string name, bool desc)>> indexes = new Dictionary<string, List<(string, bool)>>();

			foreach (var c in _columns)
			{
				CustomAttributeData indexAttr = c.CustomAttributes.ToList().Find(a => a.AttributeType.Name == nameof(IndexAttribute));
				if (indexAttr != null)
				{
					string indexName = (string)indexAttr.ConstructorArguments[0].Value;

					if (!indexes.TryGetValue(indexName, out List<(string, bool)> indexColumns))
						indexColumns = new List<(string, bool)>();

					indexColumns.Add((c.Name, (bool)indexAttr.ConstructorArguments[1].Value));

					indexes[indexName] = indexColumns;
				}
			}

			foreach (var index in indexes)
			{
				string indexName = $"IX_{Name}_{index.Key}";

				_sqlBuilder.Drop().Index(indexName).On(Name);
				await _db.StatementAsync(_sqlBuilder.Build()).ConfigureAwait(false);

				_sqlBuilder.Create().Index(indexName).On(Name);
				foreach (var indexColumn in index.Value)
				{
					_sqlBuilder.C(indexColumn.name);
					if (indexColumn.desc) _sqlBuilder.Desc();
				}
				await _db.StatementAsync(_sqlBuilder.Build()).ConfigureAwait(false);
			}
		}

		public virtual IQuery<T> Rows
		{
			get {
				return new SqlQuery<T>(_db, Name);
			}
		}

		protected virtual void BuildWhereForPrimaryKey(T pk, List<object> parameters)
		{
			if (_pkColumns.Count <= 0)
				throw new Exception(pbXNet.Localized.T("DB_PrimaryKeyNotDefined", Name));

			_sqlBuilder.Where();

			int n = 1;
			int pc = parameters.Count;
			foreach (var c in _pkColumns)
			{
				if (n > 1) _sqlBuilder.And();
				_sqlBuilder.C(c.Name).Eq.P(pc + n++);

				object v = c.GetValue(pk);
				if (v == null)
					throw new Exception($"The property '{c.Name}' that is part of the primary key can not be null."); // TODO: translation

				parameters.Add(v);
			}
		}

		public virtual async Task<bool> ExistsAsync(T pk)
		{
			Check.Null(pk, nameof(pk));

			var parameters = new List<object>();

			_sqlBuilder.Select().E("1").From(Name);
			BuildWhereForPrimaryKey(pk, parameters);

			return await _db.ScalarAsync<object>(_sqlBuilder.Build(), parameters.ToArray()).ConfigureAwait(false) != null;
		}

		public virtual async Task<T> FindAsync(T pk)
		{
			Check.Null(pk, nameof(pk));

			_sqlBuilder.Select();
			foreach (var c in _columns) _sqlBuilder.C(c.Name);
			_sqlBuilder.From(Name);

			var parametres = new List<object>();
			BuildWhereForPrimaryKey(pk, parametres);

			using (IQueryResult<T> r = await _db.QueryAsync<T>(_sqlBuilder.Build(), parametres.ToArray()).ConfigureAwait(false))
			{
				IEnumerator<T> e = r.GetEnumerator();
				return e.MoveNext() ? e.Current : default(T);
			}
		}

		public virtual async Task UpdateAsync(T o)
		{
			Check.Null(o, nameof(o));

			var parameters = new List<object>();

			_sqlBuilder.Update(Name);
			for (int n = 0; n < _columns.Count; n++)
			{
				var c = _columns[n];
				_sqlBuilder.C(c.Name).P(n + 1);
				parameters.Add(c.GetValue(o));
			}

			BuildWhereForPrimaryKey(o, parameters);

			if (await _db.StatementAsync(_sqlBuilder.Build(), parameters.ToArray()).ConfigureAwait(false) <= 0)
				throw new Exception("Record not found!"); // TODO: localization and much better text!
		}

		public virtual async Task<int> UpdateAsync<TA>(TA o, Expression<Func<TA, bool>> predicate)
		{
			Check.Null(o, nameof(o));
			Check.Null(predicate, nameof(predicate));

			IExpressionTranslator translator = _db.GetExpressionTranslator(typeof(TA));
			string sexpr = translator.Translate(predicate.Body);
			if (sexpr != null)
			{
				List<MemberInfo> columns = typeof(TA).GetRuntimePropertiesAndFields().ToList();
				List<(string, object)> parameters = translator.Parameters?.ToList() ?? new List<(string, object)>();

				_sqlBuilder.Update(Name);

				foreach (var c in columns)
				{
					object value = c.GetValue(o);
					if (value != null)
					{
						string paramName = _sqlBuilder.AutoGeneratedParameterPrefix + c.Name;

						_sqlBuilder.C(c.Name).P(paramName);

						parameters.Add((paramName, value));
					}
				}

				_sqlBuilder.Where().Ob().Text(sexpr).Cb();

				return await _db.StatementAsync(_sqlBuilder.Build(), parameters.ToArray()).ConfigureAwait(false);
			}
			else
				throw new Exception($"It is not possible to translate the expression '{predicate.Body.ToString()}' into a database-understandable expression."); // TODO: localization
		}

		public virtual async Task InsertOrUpdateAsync(T o)
		{
			Check.Null(o, nameof(o));

			if (_pkColumns.Count <= 0 || !await ExistsAsync(o).ConfigureAwait(false))
			{
				var parameters = new List<object>();

				_sqlBuilder.InsertInto(Name);
				foreach (var c in _columns)
				{
					_sqlBuilder.C(c.Name);
					parameters.Add(c.GetValue(o));
				}

				_sqlBuilder.Values();
				for (int n = 1; n <= _columns.Count; n++)
					_sqlBuilder.P(n);

				await _db.StatementAsync(_sqlBuilder.Build(), parameters.ToArray()).ConfigureAwait(false);
			}
			else
				await UpdateAsync(o).ConfigureAwait(false);
		}

		public virtual async Task<int> DeleteAsync(T o)
		{
			Check.Null(o, nameof(o));

			if (_pkColumns.Count <= 0 || !await ExistsAsync(o).ConfigureAwait(false))
			{
				// TODO: delete all rows that match T o
				throw new NotImplementedException();
			}
			else
			{
				var parametres = new List<object>();

				_sqlBuilder.Delete().From(Name);

				BuildWhereForPrimaryKey(o, parametres);

				return await _db.StatementAsync(_sqlBuilder.Build(), parametres.ToArray()).ConfigureAwait(false);
			}
		}

		public virtual async Task<int> DeleteAsync(Expression<Func<T, bool>> predicate)
		{
			Check.Null(predicate, nameof(predicate));

			IExpressionTranslator translator = _db.GetExpressionTranslator(typeof(T));
			string sexpr = translator.Translate(predicate.Body);
			if (sexpr != null)
			{
				_sqlBuilder.Delete().From(Name).Where().Ob().Text(sexpr).Cb();

				return await _db.StatementAsync(_sqlBuilder.Build(), translator.Parameters.ToArray()).ConfigureAwait(false);
			}
			else
				throw new Exception($"It is not possible to translate the expression '{predicate.Body.ToString()}' into a database-understandable expression."); // TODO: localization
		}
	}
}

