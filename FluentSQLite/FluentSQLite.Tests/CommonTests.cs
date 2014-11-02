using System.Data;
using NSubstitute;
using NUnit.Framework;

namespace FluentSQLite.Tests
{
	[TestFixture]
	public class CommonTests
	{
		[Test]
		public void TestGeneralExecution()
		{
			// General run test
			Database.CreateDatabase()
				.AddTable("Table1")
						.AddField("PrimaryKey", FieldType.AutoInc, true, true)
						.AddField("SomeData", FieldType.String)
						.AddField("SomeOtherData", FieldType.Integer)
				.CommitStructure()
				.SelectTable("Table1")
						.InsertRow("String 1", 42)
						.InsertRow("String 2", 1400)
				.CommitData();
		}

		[Test]
		public void CreateTableSqlExpression()
		{
			var command = Substitute.For<IDbCommand>();
			var connection = Substitute.For<IDbConnection>();
			connection.CreateCommand().Returns(command);

			Database.CreateDatabase(connection)
				.AddTable("Table1")
						.AddField("PrimaryKey", FieldType.AutoInc, true, true)
						.AddField("SomeData", FieldType.String)
						.AddField("SomeOtherData", FieldType.Integer)
				.CommitStructure();

			command.Received().ExecuteNonQuery();
			Assert.AreEqual("CREATE TABLE \"Table1\" (\"PrimaryKey\" INTEGER PRIMARY KEY AUTOINCREMENT, \"SomeData\" CHAR, \"SomeOtherData\" INTEGER)", command.CommandText);
		}

		[Test]
		public void InsertDataSqlExpression()
		{
			var command = Substitute.For<IDbCommand>();
			var connection = Substitute.For<IDbConnection>();
			connection.CreateCommand().Returns(command);

			Database.CreateDatabase(connection)
				.AddTable("Table1")
						.AddField("PrimaryKey", FieldType.AutoInc, true, true)
						.AddField("SomeData", FieldType.String)
						.AddField("SomeOtherData", FieldType.Integer)
				.CommitStructure()
				.SelectTable("Table1")
						.InsertRow("String 1", 42)
				.CommitData();

			command.Received().ExecuteNonQuery();
			Assert.AreEqual("INSERT INTO \"Table1\" (\"SomeData\", \"SomeOtherData\") VALUES (@SomeData, @SomeOtherData)", command.CommandText);
		}

		[Test]
		[ExpectedException(typeof(FluentSQLiteException))]
		public void AddingSameTableTwiceShouldFail()
		{
			Database.CreateDatabase()
				.AddTable("Table1")
						.AddField("PrimaryKey", FieldType.AutoInc, true, true)
						.AddField("SomeData", FieldType.String)
						.AddField("SomeOtherData", FieldType.Integer)
				.CommitStructure()
				.AddTable("Table1")
						.AddField("PrimaryKey", FieldType.AutoInc, true, true)
						.AddField("SomeData", FieldType.String)
						.AddField("SomeOtherData", FieldType.Integer)
				.CommitStructure();
		}

		[Test]
		public void ActualDataIsInserted()
		{
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

			using (var command = connection.CreateCommand())
			{
				command.CommandType = CommandType.Text;
				command.CommandText = "SELECT * FROM Table1 ORDER BY PrimaryKey";

				using (var reader = command.ExecuteReader())
				{
					reader.Read();

					Assert.AreEqual(1, reader[0]);
					Assert.AreEqual("String 1", reader[1]);
					Assert.AreEqual(42, reader[2]);

					reader.Read();

					Assert.AreEqual(2, reader[0]);
					Assert.AreEqual("String 2", reader[1]);
					Assert.AreEqual(720, reader[2]);
				}
			}
		}

		[Test]
		[ExpectedException(typeof(FluentSQLiteException))]
		public void AutoIncShouldBeTheOnlyPrimaryKey_AddedBeforeOtherFields()
		{
			// Test case #1
			// AutoInc is added before all other fields
			var table = Database.CreateDatabase()
				.AddTable("Table1")
						.AddField("PrimaryKey", FieldType.AutoInc, true, true)
						.AddField("SomeData", FieldType.String, false, true);
		}

		[Test]
		[ExpectedException(typeof(FluentSQLiteException))]
		public void AutoIncShouldBeTheOnlyPrimaryKey_AddedAfterOtherFirlds()
		{
			// Test case #2
			// AutoInc is added after other PK fields
			var table2 = Database.CreateDatabase()
				.AddTable("Table1")
						.AddField("SomeData", FieldType.String, false, true)
						.AddField("PrimaryKey", FieldType.AutoInc, true, true);
		}
	}
}
