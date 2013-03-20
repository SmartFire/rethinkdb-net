﻿using System;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace RethinkDb.Test
{
    [DataContract]
    class TestObject
    {
        [DataMember(Name = "id")]
        public string Id;

        [DataMember(Name = "name")]
        public string Name;

        [DataMember(Name = "children")]
        public TestObject[] Children;
    }

    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                var task = TestSequence();
                task.Wait();
            }
            catch (Exception e)
            {
                Console.WriteLine("Error: {0}", e);
            }
        }

        private static async Task TestSequence()
        {
            using (var connection = new Connection())
            {
                DmlResponse resp;

                await connection.Connect(new IPEndPoint(IPAddress.Parse("10.210.27.106"), 28015));

                var dbList = await connection.Query<string[]>(Query.DbList());
                if (dbList.Contains("test"))
                {
                    resp = await connection.Write(Query.DbDrop("test"));
                    if (resp.Dropped != 1)
                        throw new Exception("DbDrop failed");
                }

                resp = await connection.Write(Query.DbCreate("test"));
                if (resp.Created != 1)
                    throw new Exception("DbCreate failed");

                var testDb = Query.Db("test");

                resp = await connection.Write(testDb.TableCreate("table"));
                if (resp.Created != 1)
                    throw new Exception("TableCreate failed");

                var testTable = testDb.Table("table");

                var obj = await connection.Query<TestObject>(testTable.Get("58379951-6208-46cc-a194-03da8ee1e13c"));
                if (obj != null)
                    throw new Exception("Expected null from fetching a random GUID");

                var enumerable = connection.Query<TestObject>(testTable);
                int count = 0;
                while (true)
                {
                    if (!await enumerable.MoveNext())
                        break;
                    ++count;
                }
                if (count != 0)
                    throw new Exception("Table query found unexpected objects");

                obj = new TestObject()
                {
                    Name = "Jim Brown",
                    Children = new TestObject[] {
                        new TestObject() { Name = "Scan" }
                    }
                };
                resp = await connection.Write(testTable.Insert<TestObject>(obj));
                if (resp.Inserted != 1)
                    throw new Exception("Insert failed");
                else if (resp.GeneratedKeys == null || resp.GeneratedKeys.Length != 1)
                    throw new Exception("Insert failed; GeneratedKeys bad");

                obj = await connection.Query<TestObject>(testTable.Get(resp.GeneratedKeys[0]));
                if (obj == null)
                    throw new Exception("Get failed from FetchSingleObject");

                resp = await connection.Write(testDb.TableDrop("table"));
                if (resp.Dropped != 1)
                    throw new Exception("TableDrop failed");

                resp = await connection.Write(Query.DbDrop("test"));
                if (resp.Dropped != 1)
                    throw new Exception("DbDrop failed");
            }
        }
    }
}