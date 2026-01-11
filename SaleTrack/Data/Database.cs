using Microsoft.Data.Sqlite;
using SaleTrack.Models;
using System;
using System.Collections.Generic;

namespace SaleTrack.Data
{
    public static class Database
    {
        private static string DbPath => "Data Source=saleTrack.db";

        public static void Initialize()
        {
            using var conn = new SqliteConnection(DbPath);
            conn.Open();
            var tableCmd = conn.CreateCommand();
            tableCmd.CommandText = @"CREATE TABLE IF NOT EXISTS Products (
                                            Id INTEGER PRIMARY KEY AUTOINCREMENT,
                                            Barcode TEXT UNIQUE,
                                            Name TEXT,
                                            UnitPrice REAL
                                        );
                                        CREATE TABLE IF NOT EXISTS Sales (
                                            Id INTEGER PRIMARY KEY AUTOINCREMENT,
                                            ProductId INTEGER,
                                            Quantity REAL,
                                            UnitPrice REAL,
                                            Total REAL,
                                            SoldAt TEXT
                                        );";
            tableCmd.ExecuteNonQuery();

            // Seed sample products if table empty
            var checkCmd = conn.CreateCommand();
            checkCmd.CommandText = "SELECT COUNT(*) FROM Products";
            var count = (long)checkCmd.ExecuteScalar();
            if (count == 0)
            {
                var insert = conn.CreateCommand();
                insert.CommandText = @"INSERT INTO Products (Barcode, Name, UnitPrice) VALUES
                                        ('012345678905','Apple',0.50),
                                        ('036000291452','Banana',0.30),
                                        ('049000042044','Milk',1.20);";
                insert.ExecuteNonQuery();
            }

            // Ensure remote backend tables exist when configured
            try
            {
                MySqlBackend.EnsureTables();
            }
            catch { }
        }

        public static Product? GetProductByBarcode(string barcode)
        {
            if (string.IsNullOrWhiteSpace(barcode)) return null;
            using var conn = new SqliteConnection(DbPath);
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Id, Barcode, Name, UnitPrice FROM Products WHERE Barcode = $b LIMIT 1";
            cmd.Parameters.AddWithValue("$b", barcode);
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return new Product
                {
                    Id = reader.GetInt32(0),
                    Barcode = reader.GetString(1),
                    Name = reader.GetString(2),
                    UnitPrice = reader.GetDecimal(3)
                };
            }
            return null;
        }

        public static void AddSale(int productId, decimal quantity, decimal unitPrice, decimal total)
        {
            using var conn = new SqliteConnection(DbPath);
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO Sales (ProductId, Quantity, UnitPrice, Total, SoldAt) VALUES ($p, $q, $u, $t, $s)";
            cmd.Parameters.AddWithValue("$p", productId);
            cmd.Parameters.AddWithValue("$q", quantity);
            cmd.Parameters.AddWithValue("$u", unitPrice);
            cmd.Parameters.AddWithValue("$t", total);
            cmd.Parameters.AddWithValue("$s", DateTime.UtcNow.ToString("o"));
            cmd.ExecuteNonQuery();

            // Forward to MySQL backend if configured
            try
            {
                MySqlBackend.ForwardSale(productId, quantity, unitPrice, total);
            }
            catch { }
        }

        public static IEnumerable<(string Name, decimal UnitPrice, decimal Quantity, decimal Total, string SoldAt)> GetRecentSales()
        {
            using var conn = new SqliteConnection(DbPath);
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"SELECT p.Name, s.UnitPrice, s.Quantity, s.Total, s.SoldAt
                                FROM Sales s
                                LEFT JOIN Products p ON p.Id = s.ProductId
                                ORDER BY s.Id DESC
                                LIMIT 100";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                yield return (
                    Name: reader.IsDBNull(0) ? "(unknown)" : reader.GetString(0),
                    UnitPrice: reader.IsDBNull(1) ? 0m : reader.GetDecimal(1),
                    Quantity: reader.IsDBNull(2) ? 0m : Convert.ToDecimal(reader.GetValue(2)),
                    Total: reader.IsDBNull(3) ? 0m : reader.GetDecimal(3),
                    SoldAt: reader.IsDBNull(4) ? "" : reader.GetString(4)
                );
            }
        }
    }
}
