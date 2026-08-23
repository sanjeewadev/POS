using Microsoft.Data.Sqlite;
using System;

class Program {
    static void Main() {
        string dbPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ""POS"", ""pos_local.db"");
        
        using (var connection = new SqliteConnection($""Data Source={dbPath}"")) {
            connection.Open();
            
            using (var command = connection.CreateCommand()) {
                command.CommandText = ""SELECT DocumentType, NextSequenceNumber FROM DocumentSequences"";
                using (var reader = command.ExecuteReader()) {
                    while (reader.Read()) {
                        Console.WriteLine($""Seq: {reader.GetString(0)} - Next: {reader.GetInt32(1)}"");
                    }
                }
            }

            using (var command = connection.CreateCommand()) {
                command.CommandText = ""SELECT ItemCode FROM ItemParents LIMIT 5"";
                using (var reader = command.ExecuteReader()) {
                    while (reader.Read()) {
                        Console.WriteLine($""Code: {reader.GetString(0)}"");
                    }
                }
            }
        }
    }
}
