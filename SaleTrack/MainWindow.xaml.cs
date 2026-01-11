using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SaleTrack.Data;
using SaleTrack.Models;

namespace SaleTrack
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Product? _currentProduct;
        private ObservableCollection<object> _sales = new ObservableCollection<object>();

        public MainWindow()
        {
            InitializeComponent();
            Database.Initialize();
            SalesGrid.ItemsSource = _sales;
        }

        private void Lookup(string barcode)
        {
            _currentProduct = Database.GetProductByBarcode(barcode);
            if (_currentProduct != null)
            {
                NameTextBox.Text = _currentProduct.Name;
                UnitPriceTextBox.Text = _currentProduct.UnitPrice.ToString("0.##");
                QuantityTextBox.Focus();
            }
            else
            {
                // allow manual entry: clear current product so user can type name/price
                _currentProduct = null;
                NameTextBox.Text = string.Empty;
                UnitPriceTextBox.Text = string.Empty;
                QuantityTextBox.Focus();
            }
        }

        private void LookupButton_Click(object sender, RoutedEventArgs e)
        {
            Lookup(BarcodeTextBox.Text.Trim());
        }

        private void BarcodeTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Lookup(BarcodeTextBox.Text.Trim());
            }
        }

        private void QuantityTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                AddCurrentSale();
            }
        }

        private void AddSaleButton_Click(object sender, RoutedEventArgs e)
        {
            AddCurrentSale();
        }

        private void AddCurrentSale()
        {
            // If product from DB is not set, build product from manual fields
            if (_currentProduct == null)
            {
                var name = NameTextBox.Text.Trim();
                if (string.IsNullOrEmpty(name))
                {
                    MessageBox.Show("No product selected and no name entered. Please scan or type a product name.");
                    return;
                }

                decimal price;
                if (!decimal.TryParse(UnitPriceTextBox.Text.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out price) || price < 0)
                {
                    MessageBox.Show("Enter a valid unit price.");
                    return;
                }

                // Insert or find product in DB by barcode if provided
                var barcode = BarcodeTextBox.Text.Trim();
                if (!string.IsNullOrWhiteSpace(barcode))
                {
                    // try to find existing product with barcode
                    var existing = Database.GetProductByBarcode(barcode);
                    if (existing != null)
                    {
                        _currentProduct = existing;
                    }
                    else
                    {
                        // add new product to SQLite
                        // Simple insertion: do not set Id here; retrieve by barcode after insert
                        using var conn = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=saleTrack.db");
                        conn.Open();
                        var cmd = conn.CreateCommand();
                        cmd.CommandText = "INSERT OR IGNORE INTO Products (Barcode, Name, UnitPrice) VALUES ($b, $n, $u)";
                        cmd.Parameters.AddWithValue("$b", barcode);
                        cmd.Parameters.AddWithValue("$n", name);
                        cmd.Parameters.AddWithValue("$u", price);
                        cmd.ExecuteNonQuery();
                        _currentProduct = Database.GetProductByBarcode(barcode);
                    }
                }
                else
                {
                    // No barcode — create a temporary product record in local DB with generated barcode null
                    using var conn = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=saleTrack.db");
                    conn.Open();
                    var cmd = conn.CreateCommand();
                    cmd.CommandText = "INSERT INTO Products (Barcode, Name, UnitPrice) VALUES (NULL, $n, $u);";
                    cmd.Parameters.AddWithValue("$n", name);
                    cmd.Parameters.AddWithValue("$u", price);
                    cmd.ExecuteNonQuery();

                    // get last inserted row
                    var get = conn.CreateCommand();
                    get.CommandText = "SELECT Id, Name, UnitPrice FROM Products WHERE rowid = last_insert_rowid() LIMIT 1";
                    using var reader = get.ExecuteReader();
                    if (reader.Read())
                    {
                        _currentProduct = new Product { Id = reader.GetInt32(0), Name = reader.GetString(1), UnitPrice = reader.GetDecimal(2), Barcode = string.Empty };
                    }
                }
            }

            if (_currentProduct == null)
            {
                MessageBox.Show("Failed to determine product.");
                return;
            }

            // Support decimal quantities optionally
            decimal qtyDecimal;
            if (AllowDecimalCheck.IsChecked == true)
            {
                if (!decimal.TryParse(QuantityTextBox.Text.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out qtyDecimal) || qtyDecimal <= 0)
                {
                    MessageBox.Show("Enter a valid quantity (decimal allowed).");
                    return;
                }
            }
            else
            {
                if (!int.TryParse(QuantityTextBox.Text.Trim(), out int qtyInt) || qtyInt <= 0)
                {
                    MessageBox.Show("Enter a valid integer quantity.");
                    return;
                }
                qtyDecimal = qtyInt;
            }

            var total = _currentProduct.UnitPrice * qtyDecimal;
            Database.AddSale(_currentProduct.Id, qtyDecimal, _currentProduct.UnitPrice, total);
            _sales.Insert(0, new { Name = _currentProduct.Name, UnitPrice = _currentProduct.UnitPrice, Quantity = qtyDecimal, Total = total, SoldAt = System.DateTime.Now.ToString("g") });

            // Clear inputs for next item
            BarcodeTextBox.Clear();
            NameTextBox.Clear();
            UnitPriceTextBox.Clear();
            QuantityTextBox.Clear();
            AllowDecimalCheck.IsChecked = false;
            _currentProduct = null;
            BarcodeTextBox.Focus();
        }
    }
}