using System;
using System.IO;

class Program {
    static void Main() {
        string dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ""POS"", ""pos_local.db"");
        Console.WriteLine(File.Exists(dbPath));
    }
}
