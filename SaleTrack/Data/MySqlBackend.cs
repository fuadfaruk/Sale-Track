using MySqlConnector;
using SaleTrack.Models;
using System;
using System.Collections.Generic;

namespace SaleTrack.Data
{
    // Simple MySQL backend to forward sales to a remote server.
    // Configure connection via environment variable SALETRACK_MYSQL or appsettings.
    public static class MySqlBackend
    {
        private static string? ConnectionString => Environment.GetEnvironmentVariable("SALETRACK_MYSQL");

        public static bool IsConfigured => !string.IsNullOrWhiteSpace(ConnectionString);

        public static void EnsureTables()
        {
            if (!IsConfigured) return;
            using var conn = new MySqlConnection(ConnectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS Products (
                                    Id INT AUTO_INCREMENT PRIMARY KEY,
                                    Barcode VARCHAR(200) UNIQUE,
                                    Name VARCHAR(500),
                                    UnitPrice DECIMAL(18,4)
                                );
                                CREATE TABLE IF NOT EXISTS Sales (
                                    Id INT AUTO_INCREMENT PRIMARY KEY,
                                    ProductId INT,
                                    Quantity DECIMAL(18,4),
                                    UnitPrice DECIMAL(18,4),
                                    Total DECIMAL(18,4),
                                    SoldAt DATETIME
                                );";
            cmd.ExecuteNonQuery();
        }

        public static void ForwardSale(int productId, decimal quantity, decimal unitPrice, decimal total)
        {
            if (!IsConfigured) return;
            try
            {
                using var conn = new MySqlConnection(ConnectionString);
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "INSERT INTO Sales (ProductId, Quantity, UnitPrice, Total, SoldAt) VALUES (@p, @q, @u, @t, @s)";
                cmd.Parameters.AddWithValue("@p", productId);
                cmd.Parameters.AddWithValue("@q", quantity);
                cmd.Parameters.AddWithValue("@u", unitPrice);
                cmd.Parameters.AddWithValue("@t", total);
                cmd.Parameters.AddWithValue("@s", DateTime.UtcNow);
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                // For now, swallow exception — in production log it.
                System.Diagnostics.Debug.WriteLine("Failed to forward sale to MySQL: " + ex.Message);
            }
        }
    }
}
