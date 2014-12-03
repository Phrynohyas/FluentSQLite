FluentSQLite
============

Fluent-style library aimed for constructing and filling SQLite databases.

This library allows to create and fill SQLite databases (both in-memory and persisted on disk ones) without the need to manually construct corresponding SQL statements.

Possible purposes:

* Recreation of damaged or missing SQLite databases on application startup
* Creation of in-memory SQL databases used by integration tests

And so on...


Adding FluentSQLite to the project
==================================

All needed code is contained within the single C# file *FluentSQLite.cs*. This file should be added to the project AS IS. Also the target project should reference SQLite ADO.NET drivers available via NuGet package **System.Data.SQLite.Core**.


Examples
========

This code snippet creates an in-memory database, adds a table to it and fills that table with sample data. Resulting connection can be used to access the data using conventional ADO.NET code. The database remains in memory until the connection is closed.

```csharp
var connection = Database.CreateDatabase()
						.AddTable("Table1")
							.AddField("PrimaryKey", FieldType.AutoInc, true, true)
							.AddField("SomeData", FieldType.String)
							.AddField("SomeOtherData", FieldType.Integer)
						.CommitStructure()
						.SelectTable("Table1")
							.InsertRow("String 1", 42)
							.InsertRow("String 2", 720)
						.CommitData()
						.Connection;
```


This code snippet uses the provided connection to access the database and also adds a table to it and fills that table with sample data.

```csharp
Database.CreateDatabase(connection)
		.AddTable("Table1")
			.AddField("PrimaryKey", FieldType.AutoInc, true, true)
			.AddField("SomeData", FieldType.String)
			.AddField("SomeOtherData", FieldType.Integer)
		.CommitStructure()
		.SelectTable("Table1")
			.InsertRow("String 1", 42)
		.CommitData();
```


API Reference
=============

The library exposes following data types:
* *FieldType* enumeration. The list of available database field datatypes.
* *IFieldContext* interface. Represents a single field on the database table.
* *ITableContext* interface. Represents a single database table.
* *IDatabaseContext* interface. Represents a whole SQLite database.

The main entry point is the *FluentSQLite.Database* static class that provides several way of creating an *IDatabaseContext* instance. The database context in turn provides API to add tables to the database or to select earlier added tables, that are represented as *ITableContext* instances.