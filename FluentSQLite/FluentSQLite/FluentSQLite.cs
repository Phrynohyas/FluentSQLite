using System;
// Author:
//  Anton Kasyanov <anton.v.kasyanov@gmail.com>
//
// Copyright (C) 2014 Anton Kasyanov <anton.v.kasyanov@gmail.com>
//
//Permission is hereby granted, free of charge, to any person obtaining a copy
//of this software and associated documentation files (the "Software"), to deal
//in the Software without restriction, including without limitation the rights
//to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
//copies of the Software, and to permit persons to whom the Software is
//furnished to do so, subject to the following conditions:

//The above copyright notice and this permission notice shall be included in all
//copies or substantial portions of the Software.

//THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
//SOFTWARE. 

using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Linq;
using System.Text;

namespace FluentSQLite
{
	public enum FieldType
	{
		AutoInc,
		Integer,
		Float,
		Decimal,
		DateTime,
		String,
		Blob
	}

	/// <summary>
	/// Field context
	/// </summary>
	public interface IFieldContext
	{
		/// <summary>
		/// Fuield Name
		/// </summary>
		string FieldName { get; }

		/// <summary>
		/// Filed type
		/// </summary>
		FieldType FieldType { get; }

		/// <summary>
		/// Flag indicating whether the current field belongs to the table Primary Key
		/// </summary>
		bool IsInPrimaryKey { get; }

		/// <summary>
		/// Flag indicating whether the field should be always assigned (ie that it cannot be left NULL in the database)
		/// </summary>
		bool IsRequired { get; }
	}

	/// <summary>
	/// Data table context
	/// </summary>
	public interface ITableContext
	{
		/// <summary>
		/// Database table name
		/// </summary>
		string TableName { get; }

		/// <summary>
		/// Collection of table fields
		/// </summary>
		IEnumerable<IFieldContext> Fields { get; }

		/// <summary>
		/// Adds a field to the table structure
		/// </summary>
		/// <param name="fieldName">Field name</param>
		/// <param name="fieldType">Field type</param>
		/// <param name="required">Flag indicating whether the field should be always assigned (ie that it cannot be left NULL in the database)</param>
		/// <param name="primaryKey">Flag indicating whether the current field belongs to the table Primary Key</param>
		/// <returns>Table context used by the FluentSQLite interface</returns>
		ITableContext AddField(string fieldName, FieldType fieldType, bool required = false, bool primaryKey = false);

		/// <summary>
		/// Commit structure changes
		/// </summary>
		/// <returns>Database context used by the FluentSQLite interface</returns>
		IDatabaseContext CommitStructure();

		/// <summary>
		/// Adds a data row to the database
		/// </summary>
		/// <param name="data">Field values</param>
		/// <returns>Table context used by the fluent interface</returns>
		ITableContext InsertRow(params object[] data);

		/// <summary>
		/// Commits queued data rows added via the InsertRow method
		/// </summary>
		/// <returns>Database context used by the FluentSQLite interface</returns>
		IDatabaseContext CommitData();
	}

	/// <summary>
	/// SQLite database context
	/// </summary>
	public interface IDatabaseContext
	{
		/// <summary>
		/// Database connection
		/// </summary>
		IDbConnection Connection { get; }

		/// <summary>
		/// Collection of data tables defined via the FluentSQLite interface earlier
		/// </summary>
		IEnumerable<ITableContext> DataTables { get; }

		/// <summary>
		/// Adds a data table to the database
		/// </summary>
		/// <param name="tableName">Data table name</param>
		/// <returns>Table context used by the FluentSQLite interface</returns>
		ITableContext AddTable(string tableName);

		/// <summary>
		/// Selects one of the data tables. Can be used to add data to
		/// data tables that already exist in the database, but weren't
		/// described via the FluentSQLite interface
		/// </summary>
		/// <returns>Table context used by the FluentSQLite interface</returns>
		ITableContext SelectTable(string tableName);

		/// <summary>
		/// Closes the database connection.
		/// For In-Memory databases this operation drops the database.
		/// </summary>
		void DetachDatabase();
	}

	/// <summary>
	/// Entry point
	/// </summary>
	public static class Database
	{
		private const string DefaultConnectionString = @"Data Source=:memory:;Version=3;New=True;Synchronous=Off;";

		/// <summary>
		/// Creates a new or creates to an existing SQLite database
		/// using the provided database connection string
		/// </summary>
		/// <param name="connectionString">Database connection string</param>
		/// <returns>Database context</returns>
		public static IDatabaseContext CreateDatabase(string connectionString = Database.DefaultConnectionString)
		{
			return Database.CreateDatabase(new SQLiteConnection(connectionString));
		}

		/// <summary>
		/// Creates a new or creates to an existing SQLite database 
		/// using the provided database connection instance
		/// </summary>
		/// <param name="connection"></param>
		/// <returns>Database context</returns>
		public static IDatabaseContext CreateDatabase(IDbConnection connection)
		{
			return new DatabaseContext(connection);
		}
	}

	/// <summary>
	/// Internal alias for a data row context
	/// </summary>
	sealed class DataRowContext : List<object>
	{
		public DataRowContext(IEnumerable<object> data)
			: base(data)
		{
		}
	}

	sealed class FieldContext : IFieldContext
	{
		public FieldContext(string fieldName, FieldType fieldType, bool required, bool primaryKey)
		{
			this.FieldName = fieldName;
			this.FieldType = fieldType;
			this.IsRequired = required;
			this.IsInPrimaryKey = primaryKey;
		}

		public string FieldName { get; private set; }
		public FieldType FieldType { get; private set; }
		public bool IsInPrimaryKey { get; private set; }
		public bool IsRequired { get; private set; }
	}

	sealed class TableContext : ITableContext
	{
		#region Private field(s)
		private readonly IDatabaseContext _ownerContext;
		private readonly IList<IFieldContext> _fields;
		private readonly IList<DataRowContext> _rows;
		private bool _isAutoIncPresent;
		#endregion

		public TableContext(string tableName, IDatabaseContext ownerContext)
		{
			this._ownerContext = ownerContext;
			this.TableName = tableName;

			this._fields = new List<IFieldContext>(16);
			this._rows = new List<DataRowContext>(16);
			this._isAutoIncPresent = false;
		}

		public string TableName { get; private set; }

		public IEnumerable<IFieldContext> Fields
		{
			get
			{
				return new List<IFieldContext>(this._fields);
			}
		}

		public ITableContext AddField(string fieldName, FieldType fieldType, bool required = false, bool primaryKey = false)
		{
			// Additional checks to ensure that requested option set is consentient
			if (primaryKey)
				required = true;

			this.CheckPrimaryKeyRestrictions(fieldType, primaryKey);

			this._fields.Add(new FieldContext(fieldName, fieldType, required, primaryKey));

			return this;
		}

		private void CheckPrimaryKeyRestrictions(FieldType fieldType, bool primaryKey)
		{
			if (this._isAutoIncPresent && primaryKey)
				throw new FluentSQLiteException("AutoInc field should be the only Primary Key field");

			// Nothing more to check
			if (fieldType != FluentSQLite.FieldType.AutoInc)
				return;

			// Non-PK cannot be AutoInc
			if (!primaryKey)
				throw new FluentSQLiteException("AutoInc field should be Primary Key field");

			this._isAutoIncPresent = true;

			if (this._fields.Any(field => field.IsInPrimaryKey))
				throw new FluentSQLiteException("AutoInc field should be the only Primary Key field");
		}

		public IDatabaseContext CommitStructure()
		{
			var commandText = this.GenerateCreateTableSqlExpression();

			this.ExecuteCreateTableCommand(commandText);

			return this._ownerContext;
		}

		private string GenerateCreateTableSqlExpression()
		{
			StringBuilder commandTextBuilder = new StringBuilder(256);
			commandTextBuilder.AppendFormat(@"CREATE TABLE ""{0}"" (", this.TableName);

			// First pass - table structure
			bool fieldAdded = false;
			foreach (var field in this._fields)
			{
				if (fieldAdded)
					commandTextBuilder.Append(@", ");
				else
					fieldAdded = true;

				commandTextBuilder.AppendFormat(@"""{0}"" {1}", field.FieldName, TableContext.ConvertEnumToSqlType(field.FieldType));

				if (field.IsRequired && field.FieldType != FieldType.AutoInc)
					commandTextBuilder.Append(@" NOT NULL");
			}

			// Second pass - Primary key
			bool primaryKeyDefined = false;
			foreach (var field in this._fields)
			{
				if (!field.IsInPrimaryKey)
					continue;

				if (field.FieldType == FieldType.AutoInc)
					continue;

				if (!primaryKeyDefined)
				{
					commandTextBuilder.Append(@", PRIMARY KEY(");
					primaryKeyDefined = true;
				}
				else
				{
					commandTextBuilder.Append(@", ");
				}

				commandTextBuilder.Append(field.FieldName);
			}
			if (primaryKeyDefined)
				commandTextBuilder.Append(@")"); // Close the PK definition clause

			commandTextBuilder.Append(@")"); // Close the structure definition clause

			return commandTextBuilder.ToString();
		}

		private void ExecuteCreateTableCommand(string commandText)
		{
			using (var command = this._ownerContext.Connection.CreateCommand())
			{
				command.CommandType = CommandType.Text;
				command.CommandText = commandText;

				command.ExecuteNonQuery();
			}
		}

		public ITableContext InsertRow(params object[] data)
		{
			this._rows.Add(new DataRowContext(data));

			return this;
		}

		public IDatabaseContext CommitData()
		{
			if (this._rows.Count == 0)
				return this._ownerContext;

			List<string> parameterNames = new List<String>(this._fields.Count);
			var commandText = this.GenerateDataInsertSqlExpression(parameterNames);

			this.ExecuteInsertDataCommand(commandText, parameterNames);

			return this._ownerContext;
		}

		private string GenerateDataInsertSqlExpression(List<string> parameterNames)
		{
			var commandTextBuilder = new StringBuilder(256);
			var valuesListBuilder = new StringBuilder(256);

			// Seed values
			commandTextBuilder.AppendFormat(@"INSERT INTO ""{0}"" (", this.TableName);
			valuesListBuilder.Append(@" VALUES (");

			bool fieldAdded = false;
			foreach (var field in this._fields)
			{
				if (field.FieldType == FieldType.AutoInc)
					continue;

				string parameterName = "@" + field.FieldName.Replace(' ', '_');
				if (fieldAdded)
				{
					commandTextBuilder.Append(@", ");
					valuesListBuilder.Append(@", ");
				}
				else
				{
					fieldAdded = true;
				}

				commandTextBuilder.AppendFormat(@"""{0}""", field.FieldName);
				valuesListBuilder.Append(parameterName);
				parameterNames.Add(parameterName);
			}
			commandTextBuilder.Append(@")");
			valuesListBuilder.Append(@")");

			commandTextBuilder.Append(valuesListBuilder);

			return commandTextBuilder.ToString();
		}

		private void ExecuteInsertDataCommand(string commandText, List<string> parameterNames)
		{
			using (var command = this._ownerContext.Connection.CreateCommand())
			{
				command.CommandType = CommandType.Text;
				command.CommandText = commandText;

				var parameters = new IDbDataParameter[parameterNames.Count];

				for (int i = 0; i < parameterNames.Count; i++)
				{
					var parameter = command.CreateParameter();
					parameter.ParameterName = parameterNames[i];

					parameters[i] = parameter;
					command.Parameters.Add(parameter);
				}

				foreach (var row in this._rows)
				{
					for (int j = 0; j < row.Count; j++)
					{
						parameters[j].Value = row[j];
					}

					command.ExecuteNonQuery();
				}
			}
		}

		private static string ConvertEnumToSqlType(FieldType value)
		{
			switch (value)
			{
				case FieldType.AutoInc:
					return "INTEGER PRIMARY KEY AUTOINCREMENT";

				case FieldType.Integer:
					return "INTEGER";

				case FieldType.Float:
					return "REAL";

				case FieldType.Decimal:
					return "NUMERIC";

				case FieldType.DateTime:
					return "DATETIME";

				case FieldType.String:
					return "CHAR";

				case FieldType.Blob:
					return "BLOB";

				default:
					return "CHAR";
			}
		}
	}

	/// <summary>
	/// Database context implementation
	/// </summary>
	sealed class DatabaseContext : IDatabaseContext
	{
		#region Private field(s)
		private readonly IDictionary<string, ITableContext> _tableContexts;
		#endregion

		public DatabaseContext(IDbConnection connection)
		{
			this._tableContexts = new Dictionary<string, ITableContext>(StringComparer.OrdinalIgnoreCase);
			this.Connection = connection;

			if (connection.State != ConnectionState.Open)
				connection.Open();
		}

		public IDbConnection Connection { get; private set; }

		public IEnumerable<ITableContext> DataTables
		{
			get
			{
				return this._tableContexts.Values;
			}
		}

		public ITableContext AddTable(string tableName)
		{
			if (this._tableContexts.ContainsKey(tableName))
				throw new FluentSQLiteException("Table already present in the database: " + tableName);

			ITableContext context = new TableContext(tableName, this);
			this._tableContexts[tableName] = context;

			return context;
		}

		public ITableContext SelectTable(string tableName)
		{
			ITableContext context;
			if (this._tableContexts.TryGetValue(tableName, out context))
				return context;

			return this.AddTable(tableName);
		}

		public void DetachDatabase()
		{
			this.Connection.Close();
			this.Connection.Dispose();
			this.Connection = null;
		}
	}

	[Serializable]
	public class FluentSQLiteException : Exception
	{
		public FluentSQLiteException(string message)
			: base(message)
		{
		}
	}
}
